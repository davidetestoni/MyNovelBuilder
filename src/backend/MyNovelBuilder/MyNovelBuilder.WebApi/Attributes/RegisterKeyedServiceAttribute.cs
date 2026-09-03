using Microsoft.Extensions.DependencyInjection;

namespace MyNovelBuilder.WebApi.Attributes;

/// <summary>
/// Attribute to mark a class for automatic registration as a keyed service.
/// </summary>
/// <param name="key">The key for the service registration.</param>
/// <param name="useHttpClient">Whether to also register a typed HttpClient for this implementation.</param>
/// <param name="lifetime">The service lifetime. Defaults to Singleton.</param>
[AttributeUsage(AttributeTargets.Class, Inherited = false)]
public sealed class RegisterKeyedServiceAttribute(
    object key,
    bool useHttpClient = false,
    ServiceLifetime lifetime = ServiceLifetime.Singleton) : Attribute
{
    /// <summary>
    /// The key for the service registration.
    /// </summary>
    public object Key { get; } = key;

    /// <summary>
    /// Whether to also register a typed HttpClient for this implementation.
    /// </summary>
    public bool UseHttpClient { get; } = useHttpClient;

    /// <summary>
    /// The service lifetime.
    /// </summary>
    public ServiceLifetime Lifetime { get; } = lifetime;
}
