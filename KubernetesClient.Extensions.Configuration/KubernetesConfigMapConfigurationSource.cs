using Microsoft.Extensions.Configuration;

namespace KubernetesClient.Extensions.Configuration;

public class KubernetesConfigMapConfigurationSource : IConfigurationSource
{
    /// <summary>
    /// Determines if loading the file is optional.
    /// </summary>
    public bool Optional { get; set; } = true;

    /// <summary>
    /// Determines whether the source will be loaded if the underlying file changes.
    /// </summary>
    public bool ReloadOnChange { get; set; } = true;

    /// <summary>
    /// Number of milliseconds that reload will wait before calling Load. This helps
    /// avoid triggering reload before a file is completely written. Default is 250.
    /// </summary>
    public int ReloadDelay { get; set; } = 250;


    /// <summary>
    /// Builds the <see cref="IConfigurationProvider"/> for this source.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/>.</param>
    /// <returns>A <see cref="IConfigurationProvider"/></returns>
    public IConfigurationProvider Build(IConfigurationBuilder builder)
    {
        return new KubernetesConfigMapConfigurationProvider(this);
    }
}