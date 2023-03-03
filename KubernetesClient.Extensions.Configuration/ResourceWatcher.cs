using k8s;
using k8s.Models;

namespace KubeOps.KubernetesClient.Extensions.Configuration;

internal sealed class ResourceWatcher : IDisposable
{
    internal sealed class Watcher
    {
        public Watcher(IDisposable? instance)
        {
            NeedsRestart = false;
            Instance = instance;
        }

        public bool NeedsRestart { get; set; }
        public IDisposable? Instance { get; set; }
    }
    
    private readonly KubernetesClient _kubernetesClient;
    private readonly KubernetesClientConfiguration _clientConfiguration;
    private readonly Dictionary<Type, Watcher> _resourceWatchers = new();
    private readonly CancellationTokenSource _cts;
    private bool _disposed = false;
    private readonly object _watcherLock = new();
    private readonly object _cacheLock;
    private ResourceCache Cache { get; }
    private KubeResourceConfigurationSource Source { get; }
    
    public ResourceWatcher(KubernetesClient kubernetesClient, object cacheLock, ResourceCache cache,
        KubeResourceConfigurationSource source, KubernetesClientConfiguration clientConfiguration)
    {
        _kubernetesClient = kubernetesClient;
        _cacheLock = cacheLock;
        Cache = cache;
        Source = source;
        _clientConfiguration = clientConfiguration;
        
        _cts = new CancellationTokenSource();
        Task.Factory.StartNew(WatcherRestartLoop, _cts.Token);
    }

    public void Watch(params Type[] resources)
    {
        var method = typeof(ResourceWatcher).GetMethod(nameof(StartResourceWatcher));

        if (method is null)
        {
            throw new Exception(); // TODO: concrete
        }

        foreach (var resource in resources)
        {
            var genericMethod = method.MakeGenericMethod(resource);
            genericMethod.Invoke(this, null);
        }
    }
    
    private async Task WatcherRestartLoop(object? state)
    {
        var token = (CancellationToken) (state ?? throw new ArgumentNullException(nameof(state)));
        while (!token.IsCancellationRequested)
        {
            await Task.Delay(Source.ReloadDelay, token);
            
            lock (_watcherLock)
            {
                Watch(_resourceWatchers
                    .Where(w => w.Value.NeedsRestart)
                    .Select(w => w.Key)
                    .ToArray());
            }
        }
    }

    private void SignalRestart<TResource>()
        where TResource : IKubernetesObject<V1ObjectMeta>
    {
        lock (_watcherLock)
        {
            if (!_resourceWatchers.ContainsKey(typeof(TResource)))
            {
                return;
            }

            _resourceWatchers[typeof(TResource)].Instance?.Dispose();
            _resourceWatchers[typeof(TResource)].NeedsRestart = true;
        }
    }
    
    private void StartResourceWatcher<TResource>()
        where TResource : IKubernetesObject<V1ObjectMeta>
    {
        lock (_watcherLock)
        {
            if (_disposed)
            {
                return;
            }

            if (!_resourceWatchers.ContainsKey(typeof(TResource)))
            {
                _resourceWatchers.Add(typeof(TResource), new Watcher(null));
            }

            if (!_resourceWatchers[typeof(TResource)].NeedsRestart)
            {
                return;
            }
            
            var watcherTask = CreateResourceWatcher<TResource>();
            watcherTask.Wait();
            
            _resourceWatchers[typeof(TResource)].Instance = watcherTask.Result;
            _resourceWatchers[typeof(TResource)].NeedsRestart = false;
        }
    }

    private async Task<Watcher<TResource>> CreateResourceWatcher<TResource>()
        where TResource : IKubernetesObject<V1ObjectMeta>
    {
        return await _kubernetesClient.Watch<TResource>(
            TimeSpan.FromSeconds(60),
            @namespace: _clientConfiguration.Namespace,
            labelSelector: Source.LabelSelector ?? (Source.DefaultSelector ? $"app={_clientConfiguration.Host}" : null),
            onEvent: (type, item) =>
            {
                switch (type)
                {
                    case WatchEventType.Modified:
                        goto case WatchEventType.Added;
                    case WatchEventType.Added:
                        lock (_cacheLock)
                        {
                            Cache.Store(item);    
                        }
                        break;
                    case WatchEventType.Deleted:
                        lock (_cacheLock)
                        {
                            Cache.Remove<TResource>(item.Metadata.Uid);    
                        }
                        break;
                }

                if (type is WatchEventType.Error)
                {
                    SignalRestart<TResource>();
                }
            },
            onClose: SignalRestart<TResource>);
    }

    /// <inheritdoc />
    public void Dispose()
    {
        _cts.Cancel();
        
        lock (_watcherLock)
        {
            _disposed = true;
            foreach (var watcher in _resourceWatchers.Values.Where(watcher => !watcher.NeedsRestart))
            {
                watcher.Instance?.Dispose();
            }
        }
    }
}