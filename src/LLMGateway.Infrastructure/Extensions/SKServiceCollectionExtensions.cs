using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;
using LLMGateway.Infrastructure.ChatCompletion;
using LLMGateway.Infrastructure.Persistence;
using LLMGateway.Infrastructure.Persistence.Repositories;
using LLMGateway.Infrastructure.Resilience;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel.ChatCompletion;

namespace LLMGateway.Infrastructure.Extensions;

/// <summary>
/// Extension methods for registering Infrastructure layer services.
/// </summary>
public static class SKServiceCollectionExtensions
{
    /// <summary>
    /// Adds Infrastructure layer services to the DI container.
    /// Registers repositories, EF Core context, Polly policies, and external services.
    /// </summary>
    public static IServiceCollection AddInfrastructure(
        this IServiceCollection services,
        IConfiguration configuration)
    {
        if (services == null)
            throw new ArgumentNullException(nameof(services));

        if (configuration == null)
            throw new ArgumentNullException(nameof(configuration));

        // Register OpenRouter configuration
        services.Configure<OpenRouterConfig>(configuration.GetSection("OpenRouter"));

        // Register EF Core context
        services.AddDbContext<GatewayDbContext>(options =>
        {
            var connectionString = configuration.GetConnectionString("PostgreSQL");
            if (string.IsNullOrEmpty(connectionString))
                throw new InvalidOperationException("PostgreSQL connection string is required");

            options.UseNpgsql(connectionString, npgsqlOptions =>
            {
                // Specify migrations assembly
                npgsqlOptions.MigrationsAssembly(typeof(GatewayDbContext).Assembly.GetName().Name);
            });
        });

        // Register repositories
        services.AddScoped<IRequestLogRepository, RequestLogRepository>();
        services.AddScoped<IModelPricingRepository, ModelPricingRepository>();
        services.AddScoped<IProviderHealthChecker, ProviderHealthChecker>();

        // Register HttpClient for OpenRouter with Polly policies
        services.AddHttpClient("OpenRouter", (serviceProvider, client) =>
        {
            var config = serviceProvider.GetRequiredService<IConfiguration>()
                .GetSection("OpenRouter").Get<OpenRouterConfig>();

            client.BaseAddress = new Uri(config?.BaseUrl ?? "https://openrouter.ai/api/v1/");
            client.Timeout = TimeSpan.FromSeconds(config?.TimeoutSeconds ?? 30);

            // Add authorization header if API key is configured
            var apiKey = config?.ApiKey;
            if (!string.IsNullOrEmpty(apiKey))
            {
                client.DefaultRequestHeaders.Add("Authorization", $"Bearer {apiKey}");
            }
        })
        .AddPolicyHandler((serviceProvider, request) =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<OpenRouterChatCompletionService>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>()
                .GetSection("OpenRouter").Get<OpenRouterConfig>();

            return PollyPolicies.CreateOpenRouterPolicy(logger, config!);
        });

        // Register OpenRouter chat completion service
        services.AddSingleton<IChatCompletionService, OpenRouterChatCompletionService>(serviceProvider =>
        {
            var httpClientFactory = serviceProvider.GetRequiredService<IHttpClientFactory>();
            var httpClient = httpClientFactory.CreateClient("OpenRouter");
            var logger = serviceProvider.GetRequiredService<ILogger<OpenRouterChatCompletionService>>();
            var config = serviceProvider.GetRequiredService<IConfiguration>()
                .GetSection("OpenRouter").Get<OpenRouterConfig>();

            return new OpenRouterChatCompletionService(httpClient, logger, config!);
        });

        return services;
    }
}