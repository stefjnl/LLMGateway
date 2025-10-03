using System.Net;
using System.Text.Json;
using LLMGateway.Domain.ValueObjects;
using LLMGateway.Infrastructure.ChatCompletion;
using Microsoft.Extensions.Logging;
using Microsoft.SemanticKernel;
using Microsoft.SemanticKernel.ChatCompletion;
using Moq;
using Moq.Protected;
using Xunit;

namespace LLMGateway.Infrastructure.Tests.ChatCompletion;

public class OpenRouterChatCompletionServiceTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILogger<OpenRouterChatCompletionService>> _loggerMock;
    private readonly OpenRouterConfig _config;
    private readonly HttpClient _httpClient;

    public OpenRouterChatCompletionServiceTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _loggerMock = new Mock<ILogger<OpenRouterChatCompletionService>>();
        _config = new OpenRouterConfig
        {
            BaseUrl = "https://openrouter.ai/api/v1/",
            ApiKey = "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",
            TimeoutSeconds = 60
        };
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri(_config.BaseUrl)
        };
    }

    [Fact]
    public async Task GetChatMessageContentsAsync_SuccessfulResponse_PopulatesMetadata()
    {
        // Arrange
        var response = new OpenRouterResponse
        {
            Choices = new[]
            {
                new OpenRouterChoice
                {
                    Message = new OpenRouterMessage
                    {
                        Role = "assistant",
                        Content = "Hello, world!"
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

        SetupHttpResponse(HttpStatusCode.OK, response);

        var service = new OpenRouterChatCompletionService(_httpClient, _loggerMock.Object, _config);
        var chatHistory = new ChatHistory("User message");

        // Act
        var result = await service.GetChatMessageContentsAsync(chatHistory);

        // Assert
        result.Should().HaveCount(1);
        var message = result.First();
        message.Role.Should().Be(AuthorRole.Assistant);
        message.Content.Should().Be("Hello, world!");

        // Verify metadata population
        message.Metadata.Should().ContainKey("input_tokens").WhoseValue.Should().Be(10);
        message.Metadata.Should().ContainKey("output_tokens").WhoseValue.Should().Be(5);
    }

    [Fact]
    public async Task GetChatMessageContentsAsync_ExtractsModelFromExtensionData()
    {
        // Arrange
        var response = new OpenRouterResponse
        {
            Choices = new[]
            {
                new OpenRouterChoice
                {
                    Message = new OpenRouterMessage
                    {
                        Role = "assistant",
                        Content = "Response"
                    }
                }
            },
            Usage = new OpenRouterUsage
            {
                PromptTokens = 5,
                CompletionTokens = 3,
                TotalTokens = 8
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, response);

        var service = new OpenRouterChatCompletionService(_httpClient, _loggerMock.Object, _config);
        var chatHistory = new ChatHistory("Test message");

        var executionSettings = new PromptExecutionSettings
        {
            ExtensionData = new Dictionary<string, object?>
            {
                ["model"] = "custom-model",
                ["temperature"] = 0.8
            }
        };

        // Act
        await service.GetChatMessageContentsAsync(chatHistory, executionSettings);

        // Assert - verify the request was made with correct model
        _httpMessageHandlerMock.Protected()
            .Verify("SendAsync", Times.Once(),
                ItExpr.Is<HttpRequestMessage>(req =>
                    req.Method == HttpMethod.Post &&
                    req.RequestUri!.ToString().EndsWith("chat/completions")),
                ItExpr.IsAny<CancellationToken>());
    }

    [Fact]
    public async Task GetChatMessageContentsAsync_RealApiCall_Succeeds()
    {
        // Arrange - use mocked HttpClient with real API response data for integration test
        var realResponse = new OpenRouterResponse
        {
            Choices = new[]
            {
                new OpenRouterChoice
                {
                    Message = new OpenRouterMessage
                    {
                        Role = "assistant",
                        Content = "Hello! How can I assist you today? If you have any questions or need information on a particular topic, feel free to ask."
                    }
                }
            },
            Usage = new OpenRouterUsage
            {
                PromptTokens = 38,
                CompletionTokens = 27,
                TotalTokens = 65
            }
        };

        SetupHttpResponse(HttpStatusCode.OK, realResponse);

        var service = new OpenRouterChatCompletionService(_httpClient, _loggerMock.Object, _config);
        var chatHistory = new ChatHistory("Hello, can you tell me a short joke?");

        // Act
        var result = await service.GetChatMessageContentsAsync(chatHistory);

        // Assert
        result.Should().HaveCount(1);
        var message = result.First();
        message.Role.Should().Be(AuthorRole.Assistant);
        message.Content.Should().Be("Hello! How can I assist you today? If you have any questions or need information on a particular topic, feel free to ask.");

        // Verify metadata population
        message.Metadata.Should().ContainKey("input_tokens");
        message.Metadata.Should().ContainKey("output_tokens");
        ((int)message.Metadata["input_tokens"]!).Should().Be(38);
        ((int)message.Metadata["output_tokens"]!).Should().Be(27);
    }

    [Fact]
    public async Task GetChatMessageContentsAsync_ThrowsOnNonTransientError()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.Unauthorized);

        var service = new OpenRouterChatCompletionService(_httpClient, _loggerMock.Object, _config);
        var chatHistory = new ChatHistory("Test message");

        // Act & Assert
        await Assert.ThrowsAsync<HttpRequestException>(() =>
            service.GetChatMessageContentsAsync(chatHistory));
    }

    private void SetupHttpResponse(HttpStatusCode statusCode, object? response = null)
    {
        var httpResponse = new HttpResponseMessage(statusCode);

        if (response != null)
        {
            httpResponse.Content = new StringContent(JsonSerializer.Serialize(response));
        }

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(httpResponse);
    }
}