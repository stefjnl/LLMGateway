using System.Net;
using System.Text.Json;
using LLMGateway.Domain.Exceptions;
using Microsoft.AspNetCore.Mvc;

namespace LLMGateway.Api.Middleware;

public class ExceptionHandlingMiddleware
{
    private readonly RequestDelegate _next;
    private readonly ILogger<ExceptionHandlingMiddleware> _logger;

    public ExceptionHandlingMiddleware(
        RequestDelegate next,
        ILogger<ExceptionHandlingMiddleware> logger)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        try
        {
            await _next(context);
        }
        catch (Exception exception)
        {
            await HandleExceptionAsync(context, exception);
        }
    }

    private async Task HandleExceptionAsync(HttpContext context, Exception exception)
    {
        var correlationId = context.Items["CorrelationId"] as string ?? "unknown";

        _logger.LogError(
            exception,
            "Unhandled exception occurred. CorrelationId: {CorrelationId}",
            correlationId);

        var problemDetails = CreateProblemDetails(exception, correlationId);

        context.Response.ContentType = "application/problem+json";
        context.Response.StatusCode = problemDetails.Status ?? StatusCodes.Status500InternalServerError;

        var options = new JsonSerializerOptions
        {
            PropertyNamingPolicy = JsonNamingPolicy.CamelCase,
            WriteIndented = false
        };

        await context.Response.WriteAsync(JsonSerializer.Serialize(problemDetails, options));
    }

    private ProblemDetails CreateProblemDetails(Exception exception, string correlationId)
    {
        return exception switch
        {
            AllProvidersFailedException =>
                new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.4",
                    Title = "Service Unavailable",
                    Status = StatusCodes.Status503ServiceUnavailable,
                    Detail = "All LLM providers are currently unavailable. Please try again later.",
                    Extensions = { ["correlationId"] = correlationId }
                },

            TokenLimitExceededException =>
                new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Bad Request",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "The request exceeds the maximum token limit.",
                    Extensions = { ["correlationId"] = correlationId }
                },

            ModelNotFoundException =>
                new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.5.1",
                    Title = "Bad Request",
                    Status = StatusCodes.Status400BadRequest,
                    Detail = "The specified model is not available.",
                    Extensions = { ["correlationId"] = correlationId }
                },

            _ =>
                new ProblemDetails
                {
                    Type = "https://tools.ietf.org/html/rfc7231#section-6.6.1",
                    Title = "Internal Server Error",
                    Status = StatusCodes.Status500InternalServerError,
                    Detail = "An unexpected error occurred. Please try again later.",
                    Extensions = { ["correlationId"] = correlationId }
                }
        };
    }
}

public static class ExceptionHandlingMiddlewareExtensions
{
    public static IApplicationBuilder UseExceptionHandling(this IApplicationBuilder app)
    {
        return app.UseMiddleware<ExceptionHandlingMiddleware>();
    }
}