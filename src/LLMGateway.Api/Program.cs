using System.IO;
using LLMGateway.Api.HealthChecks;
using LLMGateway.Api.Middleware;
using LLMGateway.Application.Extensions;
using LLMGateway.Infrastructure.Extensions;
using Serilog;

var builder = WebApplication.CreateBuilder(args);

// Configure Serilog
builder.Host.UseSerilog((context, services, configuration) =>
{
    configuration.ReadFrom.Configuration(context.Configuration);
});

// Load shared configuration and secrets
var sharedSecretsPath = Path.Combine(
    builder.Environment.ContentRootPath,
    "..", "..", "Shared.Configuration", "appsettings.secrets.json");

builder.Configuration.AddJsonFile(
    sharedSecretsPath,
    optional: true,
    reloadOnChange: true);

// Load User Secrets (for local development)
builder.Configuration.AddUserSecrets<Program>();

// Add services to the container.

// Register Application layer services
builder.Services.AddApplication();

// Register Infrastructure layer services
builder.Services.AddInfrastructure(builder.Configuration);

// Register API layer services
builder.Services.AddControllers();
builder.Services.AddEndpointsApiExplorer();
builder.Services.AddSwaggerGen();

// Register health checks
builder.Services.AddHealthChecks();

var app = builder.Build();

// Configure the HTTP request pipeline.

// Exception handling middleware (must be first)
app.UseExceptionHandling();

// Serilog request logging (must be early to capture all requests)
app.UseSerilogRequestLogging();

// Correlation ID middleware
app.UseCorrelationId();

// HTTPS redirection
app.UseHttpsRedirection();

// Authorization
app.UseAuthorization();

// Map controllers
app.MapControllers();

// Map health check endpoints
app.MapHealthChecks("/health", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = _ => true // Include all health checks
});

app.MapHealthChecks("/health/ready", new Microsoft.AspNetCore.Diagnostics.HealthChecks.HealthCheckOptions
{
    Predicate = hc => hc.Name == "database" || hc.Name == "openrouter" // Only critical dependencies
});

// Swagger UI (Development only)
if (app.Environment.IsDevelopment())
{
    app.UseSwagger();
    app.UseSwaggerUI();
}

app.Run();
