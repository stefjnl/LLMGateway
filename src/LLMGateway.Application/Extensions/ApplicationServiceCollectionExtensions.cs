using LLMGateway.Application.Orchestration;
using LLMGateway.Application.Plugins;
using LLMGateway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;

namespace LLMGateway.Application.Extensions;

/// <summary>
/// Extension methods for registering Application layer services.
/// </summary>
public static class ApplicationServiceCollectionExtensions
{
    /// <summary>
    /// Adds Application layer services to the DI container.
    /// Registers orchestrators, plugins, and domain services.
    /// </summary>
    public static IServiceCollection AddApplication(this IServiceCollection services)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        // Register provider configuration
        services.AddSingleton<IProviderConfig, OpenRouterProviderConfig>();

        // Register plugins
        services.AddScoped<ModelSelectionPlugin>();
        services.AddScoped<CostTrackingPlugin>();
        services.AddScoped<ProviderFallbackPlugin>();

        // Register orchestration components
        services.AddScoped<KernelFactory>();
        services.AddScoped<KernelOrchestrator>();

        return services;
    }

    /// <summary>
    /// Simple implementation of IProviderConfig for OpenRouter.
    /// </summary>
    private class OpenRouterProviderConfig : IProviderConfig
    {
        public string ProviderName => "OpenRouter";
    }
}