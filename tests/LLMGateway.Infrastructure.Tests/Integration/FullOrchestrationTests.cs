using System.Net;
using System.Text.Json;
using LLMGateway.Application.Commands;
using LLMGateway.Application.DTOs;
using LLMGateway.Application.Orchestration;
using LLMGateway.Application.Plugins;
using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;
using LLMGateway.Infrastructure.ChatCompletion;
using LLMGateway.Infrastructure.Persistence;
using LLMGateway.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Moq.Protected;

namespace LLMGateway.Infrastructure.Tests.Integration;

public class FullOrchestrationTests : IDisposable
{
    private readonly IServiceProvider _serviceProvider;
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly KernelOrchestrator _orchestrator;

    public FullOrchestrationTests()
    {
        // Setup DI container with mocked HTTP client
        var services = new ServiceCollection();

        // Mock HTTP handler for OpenRouter
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        var httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };

        // Register services
        services.AddLogging();
        services.AddSingleton(httpClient);

        // Register in-memory database with fixed name for consistent seeding
        services.AddDbContext<GatewayDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: "TestDb"));

        // Register repositories
        services.AddScoped<IRequestLogRepository, RequestLogRepository>();
        services.AddScoped<IModelPricingRepository, ModelPricingRepository>();
        services.AddScoped<IProviderHealthChecker, ProviderHealthChecker>();
        services.AddSingleton<IProviderConfig>(new TestProviderConfig());

        // Register OpenRouter service
        var config = new OpenRouterConfig
        {
            BaseUrl = "https://openrouter.ai/api/v1/",
            ApiKey = "test-key"
        };
        services.AddSingleton(config);
        services.AddSingleton<IChatCompletionService>(serviceProvider =>
        {
            var logger = serviceProvider.GetRequiredService<ILogger<OpenRouterChatCompletionService>>();
            return new OpenRouterChatCompletionService(httpClient, logger, config);
        });

        // Register plugins
        services.AddSingleton<ModelSelectionPlugin>();
        services.AddSingleton<CostTrackingPlugin>();
        services.AddSingleton<ProviderFallbackPlugin>();

        // Register KernelFactory
        services.AddSingleton<KernelFactory>();

        // Register orchestrator
        services.AddSingleton<KernelOrchestrator>();

        _serviceProvider = services.BuildServiceProvider();
        _orchestrator = _serviceProvider.GetRequiredService<KernelOrchestrator>();

        // Seed database with test data
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
        SeedDatabase(context);
    }

    [Fact]
    public async Task SendChatCompletion_FullFlowWithMetadata_PopulatesResponseCorrectly()
    {
        // Arrange
        var openRouterResponse = new OpenRouterResponse
        {
            Choices = new[]
            {
                new OpenRouterChoice
                {
                    Message = new OpenRouterMessage
                    {
                        Role = "assistant",
                        Content = "This is a test response from OpenRouter."
                    }
                }
            },
            Usage = new OpenRouterUsage
            {
                PromptTokens = 15,
                CompletionTokens = 8,
                TotalTokens = 23
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(openRouterResponse))
            })
            .Verifiable();

        var command = new SendChatCompletionCommand(
            Messages: new[] { new Message { Role = "user", Content = "Hello, how are you?" } },
            Model: "google/gemini-2.5-flash-lite-preview-09-2025",
            Temperature: 0.7m);

        // Act
        var response = await _orchestrator.SendChatCompletionAsync(command);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().Be("This is a test response from OpenRouter.");
        response.Model.Should().Be("google/gemini-2.5-flash-lite-preview-09-2025");
        response.TokensUsed.Should().Be(23); // 15 + 8

        // Debug: Check if cost is actually calculated
        if (response.EstimatedCostUsd == 0)
        {
            // Let's check the database to see if seeding worked
            using var scope = _serviceProvider.CreateScope();
            var context = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();
            var pricing = context.ModelPricings.FirstOrDefault();
            if (pricing == null)
            {
                throw new Exception("No model pricing found in database - seeding failed");
            }
            else
            {
                throw new Exception($"Model pricing found: {pricing.Model.Value}, but cost calculation returned 0");
            }
        }

        response.EstimatedCostUsd.Should().BeGreaterThan(0);
        response.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);

        // Verify the mock was called
        _httpMessageHandlerMock.Protected().Verify(
            "SendAsync",
            Times.Once(),
            ItExpr.IsAny<HttpRequestMessage>(),
            ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task SendChatCompletion_MetadataFlowsFromServiceToResponse()
    {
        // Arrange
        var openRouterResponse = new OpenRouterResponse
        {
            Choices = new[]
            {
                new OpenRouterChoice
                {
                    Message = new OpenRouterMessage
                    {
                        Role = "assistant",
                        Content = "Metadata test response."
                    }
                }
            },
            Usage = new OpenRouterUsage
            {
                PromptTokens = 10,
                CompletionTokens = 5,
                TotalTokens = 15
            }
        };

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(openRouterResponse))
            });

        var command = new SendChatCompletionCommand(
            Messages: new[] { new Message { Role = "user", Content = "Test metadata flow" } },
            Model: "google/gemini-2.5-flash-lite-preview-09-2025");

        // Act
        var response = await _orchestrator.SendChatCompletionAsync(command);

        // Assert - verify metadata flows correctly
        response.TokensUsed.Should().Be(15); // 10 + 5

        // Verify cost calculation based on metadata
        // z-ai/glm-4.6: input=0.0001, output=0.0002 per 1M tokens
        // Cost = (10/1M * 0.0001) + (5/1M * 0.0002) = 0.000000001 + 0.000000001 = very small
        response.EstimatedCostUsd.Should().BeGreaterThan(0);
    }

    private void SeedDatabase(GatewayDbContext context)
    {
        // Seed model pricing data with higher costs for testing
        var models = new[]
        {
            ModelPricing.Create(
                ModelName.From("z-ai/glm-4.6"),
                "OpenRouter",
                0.10m,  // $0.10 per 1M input tokens
                0.20m,  // $0.20 per 1M output tokens
                128000),
            ModelPricing.Create(
                ModelName.From("deepseek-ai/DeepSeek-V3.1-Terminus"),
                "OpenRouter",
                0.30m,  // $0.30 per 1M input tokens
                0.50m,  // $0.50 per 1M output tokens
                64000),
            ModelPricing.Create(
                ModelName.From("google/gemini-2.5-flash-lite-preview-09-2025"),
                "OpenRouter",
                0.05m,  // $0.05 per 1M input tokens (faster, cheaper model)
                0.10m,  // $0.10 per 1M output tokens
                128000)
        };

        context.ModelPricings.AddRange(models);
        var result = context.SaveChanges();
        Console.WriteLine($"Seeded {result} entities");
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}

internal class TestProviderConfig : LLMGateway.Domain.Interfaces.IProviderConfig
{
    public string ProviderName => "OpenRouter";
}