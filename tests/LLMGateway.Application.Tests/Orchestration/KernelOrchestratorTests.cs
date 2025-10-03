using FluentAssertions;
using LLMGateway.Application.Commands;
using LLMGateway.Application.DTOs;
using LLMGateway.Application.Orchestration;
using LLMGateway.Application.Plugins;
using LLMGateway.Application.Tests.TestDoubles;
using LLMGateway.Domain.Interfaces;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;

namespace LLMGateway.Application.Tests.Orchestration;

public class KernelOrchestratorTests
{
    private readonly Mock<IChatCompletionService> _mockCompletionService;
    private readonly KernelOrchestrator _orchestrator;

    public KernelOrchestratorTests()
    {
        // Setup in-memory repositories
        var logRepository = new InMemoryRequestLogRepository();
        var pricingRepository = new InMemoryModelPricingRepository();

        // Setup mock completion service
        _mockCompletionService = new Mock<IChatCompletionService>();

        // Setup mock provider config
        var mockProviderConfig = new Mock<IProviderConfig>();
        mockProviderConfig.Setup(x => x.ProviderName).Returns("OpenRouter");

        // Setup DI container
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddSingleton(_mockCompletionService.Object);
        services.AddSingleton<IRequestLogRepository>(logRepository);
        services.AddSingleton<IModelPricingRepository>(pricingRepository);
        services.AddSingleton<IProviderConfig>(mockProviderConfig.Object);
        services.AddSingleton<ModelSelectionPlugin>();
        services.AddSingleton<CostTrackingPlugin>();
        services.AddSingleton<ProviderFallbackPlugin>();
        services.AddSingleton<KernelFactory>();

        var serviceProvider = services.BuildServiceProvider();

        // Create orchestrator
        var factory = serviceProvider.GetRequiredService<KernelFactory>();
        var modelSelection = serviceProvider.GetRequiredService<ModelSelectionPlugin>();
        var costTracking = serviceProvider.GetRequiredService<CostTrackingPlugin>();
        var providerFallback = serviceProvider.GetRequiredService<ProviderFallbackPlugin>();
        var providerConfig = serviceProvider.GetRequiredService<IProviderConfig>();
        var logger = serviceProvider.GetRequiredService<ILogger<KernelOrchestrator>>();

        _orchestrator = new KernelOrchestrator(
            factory,
            modelSelection,
            costTracking,
            providerFallback,
            providerConfig,
            logger);
    }

    [Fact]
    public async Task SendChatCompletion_ValidRequest_ReturnsResponse()
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: new[]
            {
                new Message { Role = "user", Content = "Hello, world!" }
            });

        var mockResult = new ChatMessageContent(
            AuthorRole.Assistant,
            "Hello! How can I help you?");

        _mockCompletionService
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { mockResult });

        // Act
        var response = await _orchestrator.SendChatCompletionAsync(command);

        // Assert
        response.Should().NotBeNull();
        response.Content.Should().Be("Hello! How can I help you?");
        response.Model.Should().NotBeNullOrEmpty();
        response.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task SendChatCompletion_InvalidCommand_ThrowsArgumentException()
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: Enumerable.Empty<Message>());

        // Act
        var act = () => _orchestrator.SendChatCompletionAsync(command);

        // Assert
        await act.Should().ThrowAsync<ArgumentException>();
    }

    [Fact]
    public async Task SendChatCompletion_CallsCompletionServiceWithCorrectSettings()
    {
        // Arrange
        var command = new SendChatCompletionCommand(
            Messages: new[]
            {
                new Message { Role = "user", Content = "Test" }
            },
            Temperature: 0.9m,
            MaxTokens: 500);

        var mockResult = new ChatMessageContent(
            AuthorRole.Assistant,
            "Response");

        _mockCompletionService
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<ChatMessageContent> { mockResult });

        // Act
        await _orchestrator.SendChatCompletionAsync(command);

        // Assert
        _mockCompletionService.Verify(
            x => x.GetChatMessageContentsAsync(
                It.Is<ChatHistory>(h => h.Count == 1),
                It.Is<PromptExecutionSettings>(s =>
                    s.ExtensionData != null &&
                    (double)s.ExtensionData["temperature"] == 0.9 &&
                    (int)s.ExtensionData["max_tokens"] == 500),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()),
            Times.Once);
    }
}