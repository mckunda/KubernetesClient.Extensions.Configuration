using k8s;
using k8s.Models;
using Microsoft.Extensions.Configuration;

namespace KubernetesClient.Extensions.Configuration;

public class KubernetesConfigMapConfigurationProvider : ConfigurationProvider, IDisposable
{
    private readonly Kubernetes _kubernetesClient;
    private readonly KubernetesClientConfiguration _clientConfiguration;

    private IDisposable? _configMapWatcher;
    private bool _disposed = false;
    private readonly object _watcherLock = new();

    /// <summary>
    /// The source settings for this provider.
    /// </summary>
    public KubernetesConfigMapConfigurationSource Source { get; }

    public KubernetesConfigMapConfigurationProvider(KubernetesConfigMapConfigurationSource source)
    {
        Source = source ?? throw new ArgumentNullException(nameof(source));
        
        _clientConfiguration = KubernetesClientConfiguration.InClusterConfig();
        _kubernetesClient = new Kubernetes(_clientConfiguration);

        if (Source.ReloadOnChange)
        {
            StartConfigMapWatcher();
        }
    }

    /// <inheritdoc />
    public void Dispose()
    {
        lock (_watcherLock)
        {
            _disposed = true;
            _configMapWatcher?.Dispose();
        }
    }

    private void StartConfigMapWatcher()
    {
        lock (_watcherLock)
        {
            if (_disposed)
            {
                return;
            }
            
            _configMapWatcher?.Dispose();
            _configMapWatcher = CreateConfigMapWatcher();
        }
    }

    private IDisposable CreateConfigMapWatcher()
    {
        var task = _kubernetesClient.CoreV1.ListNamespacedConfigMapWithHttpMessagesAsync("default", watch: true);
        return task.Watch<V1ConfigMap, V1ConfigMapList>((type, item) =>
        {
            switch (type)
            {
                case WatchEventType.Added:
                case WatchEventType.Modified:
                case WatchEventType.Deleted:
                    Thread.Sleep(Source.ReloadDelay);
                    Load();
                    break;
                case WatchEventType.Error:
                    StartConfigMapWatcher();
                    break;
            }
        });
    }

    public override void Load()
    {
        var configs = _kubernetesClient.ListNamespacedConfigMap(
            _clientConfiguration.Namespace,
            labelSelector: $"app: {_clientConfiguration.Host}");

        if (configs is null)
        {
            if (Source.Optional)
            {
                Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);
            }
            else
            {
                throw new Exception(); // TODO: Concrete exception
            }

            OnReload();
            return;
        }

        Data = new Dictionary<string, string>(StringComparer.OrdinalIgnoreCase);

        foreach (var configMap in configs.Items)
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

        OnReload();
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
            newKey = $"{prefix}:{newKey}";
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