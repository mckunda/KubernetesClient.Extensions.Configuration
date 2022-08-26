using Microsoft.Extensions.Configuration;

namespace KubernetesClient.Extensions.Configuration;

/// <summary>
/// Extension methods for adding <see cref="KubernetesConfigMapConfigurationExtensions"/>.
/// </summary>
public static class KubernetesConfigMapConfigurationExtensions
{
    /// <summary>
    /// Adds a YAML configuration source to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <param name="reloadOnChange">Whether the configuration should be reloaded if the file changes.</param>
    /// <param name="reloadDelay">
    /// Number of milliseconds that reload will wait before calling Load. This helps
    /// avoid triggering reload before a file is completely written. Default is 250.
    /// </param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddKubernetesConfigMap(this IConfigurationBuilder builder,
        bool optional = false, bool reloadOnChange = true, int reloadDelay = 250)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.AddKubernetesConfigMap(s =>
        {
            s.Optional = optional;
            s.ReloadOnChange = reloadOnChange;
            s.ReloadDelay = reloadDelay;
        });
    }

    /// <summary>
    /// Adds a YAML configuration source to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="configureSource">Configures the source.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddKubernetesConfigMap(this IConfigurationBuilder builder,
        Action<KubernetesConfigMapConfigurationSource> configureSource)
        => builder.Add(configureSource);
}