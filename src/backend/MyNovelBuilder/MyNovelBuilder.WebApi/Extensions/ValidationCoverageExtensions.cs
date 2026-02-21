using System.Reflection;
using FluentValidation;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Routing;

namespace MyNovelBuilder.WebApi.Extensions;

/// <summary>
/// Utilities to ensure controller input models are consistently backed by FluentValidation validators.
/// </summary>
public static class ValidationCoverageExtensions
{
    /// <summary>
    /// Throws at startup when a POST/PUT input model does not have a registered FluentValidation validator.
    /// </summary>
    public static void EnsurePostPutInputValidatorsAreRegistered(this IServiceProvider services)
    {
        using var scope = services.CreateScope();
        var provider = scope.ServiceProvider;
        var missingValidators = FindMissingValidators(provider);

        if (missingValidators.Count == 0)
        {
            return;
        }

        var missing = string.Join(Environment.NewLine, missingValidators.Select(x => $"- {x}"));
        throw new InvalidOperationException(
            $"Missing FluentValidation validator registrations for POST/PUT input models:{Environment.NewLine}{missing}");
    }

    /// <summary>
    /// Finds POST/PUT input models from API controllers that have no registered FluentValidation validator.
    /// </summary>
    public static IReadOnlyList<string> FindMissingValidators(IServiceProvider services)
    {
        var controllerTypes = typeof(Program).Assembly
            .GetTypes()
            .Where(t => !t.IsAbstract && typeof(ControllerBase).IsAssignableFrom(t));

        var modelTypes = controllerTypes
            .SelectMany(t => t.GetMethods(BindingFlags.Public | BindingFlags.Instance | BindingFlags.DeclaredOnly))
            .Where(IsPostOrPutAction)
            .SelectMany(GetInputModelTypes)
            .Distinct()
            .ToList();

        var missingValidators = new List<string>();

        foreach (var modelType in modelTypes)
        {
            var validatorType = typeof(IValidator<>).MakeGenericType(modelType);
            var validators = services.GetServices(validatorType);

            if (!validators.Any())
            {
                missingValidators.Add(modelType.FullName ?? modelType.Name);
            }
        }

        return missingValidators;
    }

    private static bool IsPostOrPutAction(MethodInfo method)
    {
        var attributes = method.GetCustomAttributes(inherit: true);
        return attributes.OfType<HttpMethodAttribute>()
            .Any(a => a.HttpMethods.Any(m =>
                m.Equals("POST", StringComparison.OrdinalIgnoreCase)
                || m.Equals("PUT", StringComparison.OrdinalIgnoreCase)));
    }

    private static IEnumerable<Type> GetInputModelTypes(MethodInfo method)
    {
        return method.GetParameters()
            .Where(p => !p.GetCustomAttributes(typeof(FromServicesAttribute), inherit: true).Any())
            .Select(p => p.ParameterType)
            .Where(IsAppInputModelType);
    }

    private static bool IsAppInputModelType(Type type)
    {
        if (type == typeof(CancellationToken)
            || type == typeof(string)
            || type == typeof(Guid)
            || type == typeof(Guid?)
            || type == typeof(DateTime)
            || type == typeof(DateTime?)
            || type == typeof(DateTimeOffset)
            || type == typeof(DateTimeOffset?)
            || type == typeof(TimeSpan)
            || type == typeof(TimeSpan?)
            || type == typeof(decimal)
            || type == typeof(decimal?)
            || type == typeof(IFormFile)
            || type == typeof(IFormFileCollection))
        {
            return false;
        }

        if (type.IsPrimitive || type.IsEnum)
        {
            return false;
        }

        return type.Namespace?.StartsWith("MyNovelBuilder.WebApi", StringComparison.Ordinal) == true;
    }
}
