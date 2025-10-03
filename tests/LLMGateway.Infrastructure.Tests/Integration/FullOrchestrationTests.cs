using System.Net;
using System.Text.Json;
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
using Microsoft.SemanticKernel;
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

        // Register in-memory database
        services.AddDbContext<GatewayDbContext>(options =>
            options.UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString()));

        // Register repositories
        services.AddScoped<IRequestLogRepository, RequestLogRepository>();
        services.AddScoped<IModelPricingRepository, ModelPricingRepository>();
        services.AddScoped<IProviderHealthChecker, ProviderHealthChecker>();

        // Register OpenRouter service
        var config = new OpenRouterConfig
        {
            BaseUrl = "https://openrouter.ai/api/v1/",
            ApiKey = "test-key"
        };
        services.AddSingleton(config);
        services.AddSingleton<IChatCompletionService, OpenRouterChatCompletionService>();

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
        SeedDatabase();
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
            .Setup("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(openRouterResponse))
            });

        var request = new ChatCompletionRequest
        {
            Messages = new[] { "Hello, how are you?" },
            Model = "z-ai/glm-4.6",
            Temperature = 0.7f
        };

        // Act
        var response = await _orchestrator.SendChatCompletionAsync(request);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().Be("This is a test response from OpenRouter.");
        response.Model.Should().Be("z-ai/glm-4.6");
        response.InputTokens.Should().Be(15);
        response.OutputTokens.Should().Be(8);
        response.TotalTokens.Should().Be(23);
        response.EstimatedCostUsd.Should().BeGreaterThan(0);
        response.Provider.Should().Be("OpenRouter");
        response.ResponseTimeMs.Should().BeGreaterThan(0);
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
            .Setup("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(new HttpResponseMessage(HttpStatusCode.OK)
            {
                Content = new StringContent(JsonSerializer.Serialize(openRouterResponse))
            });

        var request = new ChatCompletionRequest
        {
            Messages = new[] { "Test metadata flow" },
            Model = "z-ai/glm-4.6"
        };

        // Act
        var response = await _orchestrator.SendChatCompletionAsync(request);

        // Assert - verify metadata flows correctly
        response.InputTokens.Should().Be(10);
        response.OutputTokens.Should().Be(5);
        response.TotalTokens.Should().Be(15);

        // Verify cost calculation based on metadata
        // z-ai/glm-4.6: input=0.0001, output=0.0002 per 1M tokens
        // Cost = (10/1M * 0.0001) + (5/1M * 0.0002) = 0.000000001 + 0.000000001 = very small
        response.EstimatedCostUsd.Should().BeGreaterThan(0);
    }

    private void SeedDatabase()
    {
        using var scope = _serviceProvider.CreateScope();
        var context = scope.ServiceProvider.GetRequiredService<GatewayDbContext>();

        // Seed model pricing data
        var models = new[]
        {
            ModelPricing.Create(
                ModelName.From("z-ai/glm-4.6"),
                "OpenRouter",
                0.0001m,
                0.0002m,
                128000),
            ModelPricing.Create(
                ModelName.From("deepseek-ai/DeepSeek-V3.1-Terminus"),
                "OpenRouter",
                0.0003m,
                0.0005m,
                64000)
        };

        context.ModelPricings.AddRange(models);
        context.SaveChanges();
    }

    public void Dispose()
    {
        if (_serviceProvider is IDisposable disposable)
        {
            disposable.Dispose();
        }
    }
}