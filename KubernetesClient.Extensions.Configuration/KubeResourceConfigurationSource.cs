using k8s;
using Microsoft.Extensions.Configuration;

namespace KubeOps.KubernetesClient.Extensions.Configuration;

public class KubeResourceConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Determines if loading the file is optional.
    /// </summary>
    public bool Optional { get; set; }

    /// <summary>
    /// Determines whether the source will be loaded if the underlying file changes.
    /// </summary>
    public bool ReloadOnChange { get; set; }

    /// <summary>
    /// Number of milliseconds that reload will wait before calling Load. This helps
    /// avoid triggering reload before a file is completely written.
    /// </summary>
    public int ReloadDelay { get; set; }

    /// <summary>
    /// Kubernetes label selector for config map
    /// </summary>
    public string? LabelSelector { get; set; }
    
    /// <summary>
    /// Whether to use default selector
    /// ("app=<see cref="KubernetesClientConfiguration.Host"/>")
    /// when <see cref="LabelSelector"/> is null
    /// </summary>
    public bool DefaultSelector { get; set; }

    /// <summary>
    /// Builds the <see cref="IConfigurationProvider"/> for this source.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
    /// <returns>A <see cref="IConfigurationProvider"/></returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new KubeResourceConfigurationProvider(this);
    }
}