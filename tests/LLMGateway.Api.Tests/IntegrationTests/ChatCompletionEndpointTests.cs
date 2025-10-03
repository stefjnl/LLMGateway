using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LLMGateway.Api.Tests.TestFixtures;
using LLMGateway.Application.DTOs;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Xunit;

namespace LLMGateway.Api.Tests.IntegrationTests;

public class ChatCompletionEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;
    private readonly Mock<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService> _mockChatService;

    public ChatCompletionEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
        _mockChatService = factory.MockChatService;
    }

    [Fact]
    public async Task Post_ValidChatRequest_Returns200WithChatResponse()
    {
        // Arrange
        var request = new ChatRequest
        {
            Messages = new[]
            {
                new Message { Role = "user", Content = "Hello, world!" }
            },
            Temperature = 0.7m,
            MaxTokens = 100
        };

        var expectedResponse = new ChatMessageContent(AuthorRole.Assistant, "Hello! How can I help you?");
        expectedResponse.Metadata = new Dictionary<string, object?>
        {
            ["input_tokens"] = 5,
            ["output_tokens"] = 7
        };

        _mockChatService
            .Setup(x => x.GetChatMessageContentsAsync(
                It.IsAny<ChatHistory>(),
                It.IsAny<PromptExecutionSettings>(),
                It.IsAny<Kernel>(),
                It.IsAny<CancellationToken>()))
            .ReturnsAsync(new[] { expectedResponse });

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);

        var chatResponse = await response.Content.ReadFromJsonAsync<ChatResponse>();
        chatResponse.Should().NotBeNull();
        chatResponse!.Content.Should().Be("Hello! How can I help you?");
        chatResponse.Model.Should().NotBeNullOrEmpty();
        chatResponse.TokensUsed.Should().Be(12); // 5 + 7
        chatResponse.EstimatedCostUsd.Should().Be(0); // No pricing data in test environment
        chatResponse.ResponseTime.Should().BeGreaterThan(TimeSpan.Zero);
    }

    [Fact]
    public async Task Post_EmptyMessages_Returns400BadRequest()
    {
        // Arrange
        var request = new ChatRequest
        {
            Messages = Array.Empty<Message>(),
            Temperature = 0.7m,
            MaxTokens = 100
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_InvalidTemperature_Returns400BadRequest()
    {
        // Arrange
        var request = new ChatRequest
        {
            Messages = new[]
            {
                new Message { Role = "user", Content = "Hello" }
            },
            Temperature = 3.0m, // Invalid: should be 0-2
            MaxTokens = 100
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }

    [Fact]
    public async Task Post_NegativeMaxTokens_Returns400BadRequest()
    {
        // Arrange
        var request = new ChatRequest
        {
            Messages = new[]
            {
                new Message { Role = "user", Content = "Hello" }
            },
            Temperature = 0.7m,
            MaxTokens = -1 // Invalid: should be positive
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
    }
}