using System.Net;
using FluentAssertions;
using LLMGateway.Api.Tests.TestFixtures;
using Microsoft.AspNetCore.Mvc.Testing;
using Xunit;

namespace LLMGateway.Api.Tests.IntegrationTests;

public class HealthCheckEndpointTests : IClassFixture<CustomWebApplicationFactory>
{
    private readonly HttpClient _client;

    public HealthCheckEndpointTests(CustomWebApplicationFactory factory)
    {
        _client = factory.CreateClient();
    }

    [Fact]
    public async Task Get_Health_Returns200OK()
    {
        // Act
        var response = await _client.GetAsync("/health");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }

    [Fact]
    public async Task Get_HealthReady_Returns200OK_WhenDependenciesHealthy()
    {
        // Act
        var response = await _client.GetAsync("/health/ready");

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
    }
}