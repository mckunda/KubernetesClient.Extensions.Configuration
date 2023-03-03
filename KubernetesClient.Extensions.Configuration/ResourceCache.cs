using System.Collections.Immutable;
using k8s;
using k8s.Models;

namespace KubeOps.KubernetesClient.Extensions.Configuration;

internal class ResourceCache
{
    private readonly Dictionary<Type, Dictionary<string, IKubernetesObject<V1ObjectMeta>>> _cache = new();
    public bool Updated { get; private set; }

    public void Store<TResource>(TResource value)
        where TResource : IKubernetesObject<V1ObjectMeta>
    {
        if (!_cache.ContainsKey(typeof(TResource)))
        {
            _cache.Add(typeof(TResource), new Dictionary<string, IKubernetesObject<V1ObjectMeta>>());
        }

        var cacheTable = _cache[typeof(TResource)];

        if (cacheTable.ContainsKey(value.Metadata.Uid))
        {
            cacheTable[value.Metadata.Uid] = value;
        }
        else
        {
            cacheTable.Add(value.Metadata.Uid, value);
        }

        Updated = true;
    }

    public void Remove<TResource>(string key)
        where TResource : IKubernetesObject<V1ObjectMeta>
    {
        if (!_cache.ContainsKey(typeof(TResource)))
        {
            return;
        }

        _cache[typeof(TResource)].Remove(key);

        Updated = true;
    }

    public ImmutableList<TResource>? Get<TResource>()
        where TResource : IKubernetesObject<V1ObjectMeta>
    {
        if (!_cache.ContainsKey(typeof(TResource)))
        {
            return null;
        }

        var cacheTable = _cache[typeof(TResource)];
        return cacheTable.Values
            .Select<IKubernetesObject<V1ObjectMeta>, TResource>(v => (TResource) v)
            .ToImmutableList();
    }
}