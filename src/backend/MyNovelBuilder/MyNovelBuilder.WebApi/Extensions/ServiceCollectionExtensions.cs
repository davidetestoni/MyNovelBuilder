using System.Reflection;
using MyNovelBuilder.WebApi.Attributes;

namespace MyNovelBuilder.WebApi.Extensions;

/// <summary>
/// Extension methods for <see cref="IServiceCollection"/> to automate service registration.
/// </summary>
public static class ServiceCollectionExtensions
{
    /// <summary>
    /// Scans the assembly for classes implementing <typeparamref name="TInterface"/> and
    /// decorated with <see cref="RegisterKeyedServiceAttribute"/>, then registers them.
    /// </summary>
    /// <typeparam name="TInterface">The interface to register the implementation against.</typeparam>
    /// <param name="services">The service collection.</param>
    public static IServiceCollection RegisterKeyedServicesFromAssembly<TInterface>(this IServiceCollection services)
        where TInterface : class
    {
        var implementations = Assembly.GetExecutingAssembly().GetTypes()
            .Where(t => t is { IsClass: true, IsAbstract: false } && t.IsAssignableTo(typeof(TInterface)));

        foreach (var implementation in implementations)
        {
            var attribute = implementation.GetCustomAttribute<RegisterKeyedServiceAttribute>();
            if (attribute == null) continue;

            // Register the keyed service
            switch (attribute.Lifetime)
            {
                case ServiceLifetime.Singleton:
                    services.AddKeyedSingleton(typeof(TInterface), attribute.Key, implementation);
                    break;
                case ServiceLifetime.Scoped:
                    services.AddKeyedScoped(typeof(TInterface), attribute.Key, implementation);
                    break;
                case ServiceLifetime.Transient:
                    services.AddKeyedTransient(typeof(TInterface), attribute.Key, implementation);
                    break;
                default:
                    throw new InvalidDataException($"Invalid lifetime specified: {attribute.Lifetime}");
            }

            // If an HTTP client is needed, register it for the implementation
            if (attribute.UseHttpClient)
            {
                services.AddHttpClient(implementation);
            }
        }

        return services;
    }

    /// <summary>
    /// Adds a typed HTTP client for the specified implementation type.
    /// </summary>
    /// <param name="services">The service collection.</param>
    /// <param name="implementation">The implementation type.</param>
    private static void AddHttpClient(this IServiceCollection services, Type implementation)
    {
        // Using reflection to call AddHttpClient<TImplementation>(services)
        var method = typeof(HttpClientFactoryServiceCollectionExtensions)
            .GetMethods()
            .FirstOrDefault(m => m.Name == nameof(HttpClientFactoryServiceCollectionExtensions.AddHttpClient) &&
                        m.IsGenericMethod &&
                        m.GetGenericArguments().Length == 1 &&
                        m.GetParameters().Length == 1 &&
                        m.GetParameters()[0].ParameterType == typeof(IServiceCollection));

        if (method == null)
        {
            throw new InvalidOperationException("Could not find AddHttpClient extension method.");
        }

        var generic = method.MakeGenericMethod(implementation);
        generic.Invoke(null, [services]);
    }
}
