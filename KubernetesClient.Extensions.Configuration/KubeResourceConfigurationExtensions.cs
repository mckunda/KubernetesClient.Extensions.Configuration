using Microsoft.Extensions.Configuration;

namespace KubeOps.KubernetesClient.Extensions.Configuration;

/// <summary>
/// Extension methods for adding <see cref="KubeResourceConfigurationExtensions"/>.
/// </summary>
public static class KubeResourceConfigurationExtensions
{
    /// <summary>
    /// Adds a default Kubernetes ConfigMap configuration source to <paramref name="builder"/>.
    /// </summary>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddDefaultKubernetesConfigMap(this IConfigurationBuilder builder)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        return builder.AddKubernetesConfigMap(true);
    }
    
    /// <summary>
    /// Adds a Kubernetes ConfigMap configuration source to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="defaultSelector">
    /// Whether to use default selector
    /// (<c>"app=[host name]"</c>)
    /// when <see cref="labelSelectorAction"/> is null.
    /// </param>
    /// <param name="labelSelectorAction">Configures the label selector</param>
    /// <param name="optional">Whether the file is optional.</param>
    /// <param name="reloadOnChange">Whether the configuration should be reloaded if the file changes.</param>
    /// <param name="reloadDelay">
    /// Number of milliseconds that reload will wait before calling Load. This helps
    /// avoid triggering reload before a file is completely written. Default is 250.
    /// </param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddKubernetesConfigMap(this IConfigurationBuilder builder,
        bool defaultSelector = false, Action<KubeLabelSelectorBuilder>? labelSelectorAction = null,
        bool optional = false, bool reloadOnChange = true, int reloadDelay = 250)
    {
        if (builder == null)
        {
            throw new ArgumentNullException(nameof(builder));
        }

        string? labelSelector = null;

        if (labelSelectorAction is not null)
        {
            var labelSelectorBuilder = new KubeLabelSelectorBuilder();

            labelSelectorAction.Invoke(labelSelectorBuilder);

            labelSelector = labelSelectorAction.ToString();
        }

        return builder.AddKubernetesConfigMap(s =>
        {
            s.Optional = optional;
            s.ReloadOnChange = reloadOnChange;
            s.ReloadDelay = reloadDelay;
            s.LabelSelector = labelSelector;
            s.DefaultSelector = defaultSelector;
        });
    }

    /// <summary>
    /// Adds a Kubernetes ConfigMap configuration source to <paramref name="builder"/>.
    /// </summary>
    /// <param name="builder">The <see cref="IConfigurationBuilder"/> to add to.</param>
    /// <param name="configureSource">Configures the source.</param>
    /// <returns>The <see cref="IConfigurationBuilder"/>.</returns>
    public static IConfigurationBuilder AddKubernetesConfigMap(this IConfigurationBuilder builder,
        Action<KubeResourceConfigurationSource> configureSource)
        => builder.Add(configureSource);
}