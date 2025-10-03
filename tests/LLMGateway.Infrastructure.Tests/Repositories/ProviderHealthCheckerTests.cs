using System.Net;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Infrastructure.Persistence.Repositories;
using Microsoft.Extensions.Logging;
using Moq;
using Moq.Protected;

namespace LLMGateway.Infrastructure.Tests.Repositories;

public class ProviderHealthCheckerTests
{
    private readonly Mock<HttpMessageHandler> _httpMessageHandlerMock;
    private readonly Mock<ILogger<ProviderHealthChecker>> _loggerMock;
    private readonly HttpClient _httpClient;
    private readonly ProviderHealthChecker _healthChecker;

    public ProviderHealthCheckerTests()
    {
        _httpMessageHandlerMock = new Mock<HttpMessageHandler>();
        _loggerMock = new Mock<ILogger<ProviderHealthChecker>>();
        _httpClient = new HttpClient(_httpMessageHandlerMock.Object)
        {
            BaseAddress = new Uri("https://openrouter.ai/api/v1/")
        };
        _healthChecker = new ProviderHealthChecker(_httpClient, _loggerMock.Object);
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsTrueForSuccessfulResponse()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        var result = await _healthChecker.IsHealthyAsync("OpenRouter");

        // Assert
        result.Should().BeTrue();
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalseForUnsuccessfulResponse()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.InternalServerError);

        // Act
        var result = await _healthChecker.IsHealthyAsync("OpenRouter");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalseForTimeout()
    {
        // Arrange
        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ThrowsAsync(new TaskCanceledException());

        // Act
        var result = await _healthChecker.IsHealthyAsync("OpenRouter");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task IsHealthyAsync_ReturnsFalseForUnknownProvider()
    {
        // Act
        var result = await _healthChecker.IsHealthyAsync("UnknownProvider");

        // Assert
        result.Should().BeFalse();
    }

    [Fact]
    public async Task CheckAllProvidersAsync_ReturnsDictionaryWithOpenRouter()
    {
        // Arrange
        SetupHttpResponse(HttpStatusCode.OK);

        // Act
        var results = await _healthChecker.CheckAllProvidersAsync();

        // Assert
        results.Should().ContainKey("OpenRouter");
        results.Should().HaveCount(1);
    }

    private void SetupHttpResponse(HttpStatusCode statusCode)
    {
        var response = new HttpResponseMessage(statusCode);
        if (statusCode == HttpStatusCode.OK)
        {
            response.Content = new StringContent("{\"models\": []}");
        }

        _httpMessageHandlerMock.Protected()
            .Setup<Task<HttpResponseMessage>>("SendAsync", ItExpr.IsAny<HttpRequestMessage>(), ItExpr.IsAny<CancellationToken>())
            .ReturnsAsync(response);
    }
}