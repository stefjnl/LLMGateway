using System.Net;
using System.Net.Http.Json;
using FluentAssertions;
using LLMGateway.Api.Tests.TestFixtures;
using LLMGateway.Application.DTOs;
using Microsoft.AspNetCore.Mvc;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LLMGateway.Api.Tests.IntegrationTests;

public class ValidationTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public ValidationTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Post_ChatCompletionWithValidationErrors_Returns400WithProblemDetails()
    {
        // Arrange
        var request = new ChatRequest
        {
            Messages = Array.Empty<Message>(), // Invalid: empty array
            Temperature = 5.0m, // Invalid: > 2.0
            MaxTokens = 0 // Invalid: <= 0
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);

        var problemDetails = await response.Content.ReadFromJsonAsync<ProblemDetails>();
        problemDetails.Should().NotBeNull();
        problemDetails!.Title.Should().Be("One or more validation errors occurred.");
        problemDetails.Status.Should().Be(400);
        problemDetails.Type.Should().Contain("tools.ietf.org");
    }

    [Fact]
    public async Task Post_ChatCompletionWithCorrelationId_IncludesCorrelationIdInResponse()
    {
        // Arrange
        var correlationId = "test-correlation-id-123";
        _client.DefaultRequestHeaders.Add("X-Correlation-ID", correlationId);

        var request = new ChatRequest
        {
            Messages = new[]
            {
                new Message { Role = "user", Content = "Hello" }
            }
        };

        // Act
        var response = await _client.PostAsJsonAsync("/v1/chat/completions", request);

        // Assert
        response.Headers.Contains("X-Correlation-ID").Should().BeTrue();
        var responseCorrelationId = response.Headers.GetValues("X-Correlation-ID").First();
        responseCorrelationId.Should().Be(correlationId);
    }
}