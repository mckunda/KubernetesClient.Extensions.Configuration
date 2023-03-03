using System.Collections.Immutable;
using System.Reflection;
using System.Reflection.Emit;
using System.Text;
using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;

namespace KubeOps.KubernetesClient.Extensions.Configuration;

public class KubeResourceConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly KubernetesClient _kubernetesClient;
    private readonly KubernetesClientConfiguration _clientConfiguration;

    private Dictionary<Type, IDisposable?> _resourceWatchers = new();
    private readonly CancellationTokenSource _cts;
    private readonly object _cacheLock = new();

    private ResourceCache Cache { get; } = new();
    private ResourceWatcher Watcher { get; }

    /// <summary>
    /// The source settings for this provider.
    /// </summary>
    public KubeResourceConfigurationSource Source { get; }

    public KubeResourceConfigurationProvider(KubeResourceConfigurationSource source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));

        _clientConfiguration = KubernetesClientConfiguration.InClusterConfig();
        _kubernetesClient = new KubernetesClient(_clientConfiguration);

        Watcher = new ResourceWatcher(_kubernetesClient, _cacheLock, Cache, Source, _clientConfiguration);
        
        if (Source.ReloadOnChange)
        {
            Watcher.Watch(typeof(V1ConfigMap), typeof(V1Secret));
        }

        _cts = new CancellationTokenSource();
        Task.Factory.StartNew(LoaderLoop, _cts.Token);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        Watcher.Dispose();
    }

    private async Task LoaderLoop(object? state)
    {
        var token = (CancellationToken) (state ?? throw new ArgumentNullException(nameof(state)));
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(Source.ReloadDelay, token);
            
            lock (_cacheLock)
            {
                if (!Cache.Updated) continue;
                
                Load(Cache.Get<V1ConfigMap>());
                Load(Cache.Get<V1Secret>());
            }
        }
    }

    

    public override void Load()
    {
        var configs = _kubernetesClient.List<V1ConfigMap>(
            _clientConfiguration.Namespace,
            labelSelector: Source.LabelSelector ??
                           (Source.DefaultSelector ? $"app={_clientConfiguration.Host}" : null));

        configs.Wait();

        Load(configs.Result.ToImmutableList());

        var secrets = _kubernetesClient.List<V1Secret>(
            _clientConfiguration.Namespace,
            labelSelector: Source.LabelSelector ??
                           (Source.DefaultSelector ? $"app={_clientConfiguration.Host}" : null));

        secrets.Wait();

        Load(secrets.Result.ToImmutableList());
    }

    private void Load<TResource>(ImmutableList<TResource>? resources)
    {
        switch (resources)
        {
            case ImmutableList<V1Secret?> secrets:
                LoadSecret(secrets);
                break;
            case ImmutableList<V1ConfigMap?> configMaps:
                LoadConfigMap(configMaps);
                break;
        }

        OnReload();
    }

    private void LoadSecret(ImmutableList<V1Secret?>? secrets)
    {
        if (secrets is null || secrets.Count == 0)
        {
            if (!Source.Optional)
            {
                throw new Exception(); // TODO: Concrete exception
            }

            return;
        }

        var removeKeys = Data.Keys.Where(k => k.StartsWith("Secrets"));
        foreach (var removeKey in removeKeys)
        {
            Data.Remove(removeKey);
        }

        foreach (var secret in secrets)
        {
            if (secret is null || secret.Data.Count == 0)
            {
                continue;
            }

            foreach (var kvp in secret.Data)
            {
                Data.Add(ConvertKey(ConfigurationPath.Combine("Secrets", kvp.Key), secret.Name()),
                    Encoding.UTF8.GetString(kvp.Value));
            }
        }
    }

    private void LoadConfigMap(ImmutableList<V1ConfigMap?>? configMaps)
    {
        if (configMaps is null || configMaps.Count == 0)
        {
            if (!Source.Optional)
            {
                throw new Exception(); // TODO: Concrete exception
            }

            return;
        }

        var removeKeys = Data.Keys.Where(k => !k.StartsWith("Secrets"));
        foreach (var removeKey in removeKeys)
        {
            Data.Remove(removeKey);
        }

        foreach (var configMap in configMaps)
        {
            if (configMap is null || configMap.Data.Count == 0)
            {
                continue;
            }

            foreach (var kvp in configMap.Data)
            {
                Data.Add(ConvertKey(kvp.Key, configMap.Name()), kvp.Value);
            }

            if (configMap.BinaryData.Count == 0)
            {
                continue;
            }

            foreach (var kvp in configMap.BinaryData)
            {
                Data.Add(ConvertKey(kvp.Key, configMap.Name()), Convert.ToBase64String(kvp.Value));
            }
        }
    }

    /// <summary>
    /// Converts a key from data field of a Kubernetes Config Map to a form suitable for
    /// <see cref="ConfigurationProvider"/>
    /// </summary>
    /// <param name="key">Original key from Kubernetes Config Map.</param>
    /// <param name="prefix">The name of the Kubernetes Config Map.</param>
    /// <returns>
    /// string representation of <see cref="ConfigurationProvider"/>-compatible
    /// key for <see cref="ConfigurationProvider.Data"/>
    /// </returns>
    /// <exception cref="ArgumentException">
    /// If any of arguments is null, empty or whitespace 
    /// </exception>
    /// <remarks>
    /// All non-alphanumeric character are deleted from the resulting string. Additionally, all digits are skipped
    /// until the first occurrence of an alphabetical character.<br/>
    /// If the original key starts with ".", the prefix won't be added.<br/>
    /// If the original key ends with ".", then all dots of the original key will be deleted before processing.
    /// </remarks>
    private static string ConvertKey(string? key, string? prefix)
    {
        if (string.IsNullOrWhiteSpace(prefix))
        {
            throw new ArgumentException("Argument cannot be empty", nameof(prefix));
        }

        if (string.IsNullOrWhiteSpace(key))
        {
            throw new ArgumentException("Argument cannot be empty", nameof(key));
        }

        var newKey = key;

        if (key.EndsWith('.'))
        {
            newKey = newKey.Replace(".", "");
        }

        if (!key.StartsWith('.'))
        {
            newKey = ConfigurationPath.Combine(prefix, newKey);
        }

        return new string(newKey
            .Where(c => char.IsLetterOrDigit(c) || c == ':')
            .SkipWhile(c => !char.IsLetter(c))
            .ToArray());
    }

    /// <summary>
    /// Generates a string representing this provider name and relevant details.
    /// </summary>
    /// <returns>The configuration name.</returns>
    public override string ToString()
        => $"{GetType().Name} ({(Source.Optional ? "Optional" : "Required")})";
}