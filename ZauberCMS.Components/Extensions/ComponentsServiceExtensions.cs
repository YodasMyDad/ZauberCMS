using Microsoft.Extensions.DependencyInjection;

namespace ZauberCMS.Components.Extensions;

public static class ComponentsServiceExtensions
{
    /// <summary>
    /// Registers ZauberCMS Components services
    /// </summary>
    public static IServiceCollection AddZauberComponents(this IServiceCollection services)
    {
        // Register MediaValidationService for RTE media validation

        
        return services;
    }
}

