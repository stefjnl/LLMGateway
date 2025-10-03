using LLMGateway.Api.HealthChecks;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Infrastructure.ChatCompletion;
using LLMGateway.Infrastructure.Persistence;
using Microsoft.AspNetCore.Hosting;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Moq;

namespace LLMGateway.Api.Tests.TestFixtures;

public class CustomWebApplicationFactory : WebApplicationFactory<Program>
{
    public Mock<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService> MockChatService { get; private set; } = null!;

    protected override void ConfigureWebHost(IWebHostBuilder builder)
    {
        builder.ConfigureServices(services =>
        {
            // Remove the real database context
            var dbContextDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(DbContextOptions<GatewayDbContext>));
            if (dbContextDescriptor != null)
            {
                services.Remove(dbContextDescriptor);
            }

            var dbContextServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(GatewayDbContext));
            if (dbContextServiceDescriptor != null)
            {
                services.Remove(dbContextServiceDescriptor);
            }

            // Add in-memory database
            services.AddDbContext<GatewayDbContext>(options =>
            {
                options.UseInMemoryDatabase("TestDatabase");
            });

            // Mock the OpenRouter service to avoid real API calls
            var openRouterServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(OpenRouterChatCompletionService));
            if (openRouterServiceDescriptor != null)
            {
                services.Remove(openRouterServiceDescriptor);
            }

            var chatCompletionServiceDescriptor = services.SingleOrDefault(
                d => d.ServiceType == typeof(Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService));
            if (chatCompletionServiceDescriptor != null)
            {
                services.Remove(chatCompletionServiceDescriptor);
            }

            // Add mock chat completion service
            MockChatService = new Mock<Microsoft.SemanticKernel.ChatCompletion.IChatCompletionService>();
            services.AddSingleton(MockChatService.Object);

            // Mock provider health checker
            var mockHealthChecker = new Mock<IProviderHealthChecker>();
            mockHealthChecker.Setup(x => x.IsHealthyAsync(It.IsAny<string>(), It.IsAny<CancellationToken>()))
                .ReturnsAsync(true); // Assume healthy for tests
            services.AddSingleton(mockHealthChecker.Object);

            // Ensure health checks can resolve dependencies
            services.AddScoped<DatabaseHealthCheck>();
            services.AddScoped<OpenRouterHealthCheck>();
        });
    }
}