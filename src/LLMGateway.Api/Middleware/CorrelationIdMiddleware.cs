using Serilog.Context;

namespace LLMGateway.Api.Middleware;

public class CorrelationIdMiddleware
{
    private readonly RequestDelegate _next;

    public CorrelationIdMiddleware(RequestDelegate next)
    {
        _next = next ?? throw new ArgumentNullException(nameof(next));
    }

    public async Task InvokeAsync(HttpContext context)
    {
        const string correlationIdHeader = "X-Correlation-ID";

        // Check if correlation ID is already provided in request
        if (!context.Request.Headers.TryGetValue(correlationIdHeader, out var correlationId))
        {
            // Generate new correlation ID
            correlationId = Guid.NewGuid().ToString();
        }

        // Add to response headers
        context.Response.Headers[correlationIdHeader] = correlationId;

        // Add to request context for logging
        context.Items["CorrelationId"] = correlationId;

        // Add to Serilog LogContext for structured logging
        LogContext.PushProperty("CorrelationId", correlationId);

        await _next(context);
    }
}

public static class CorrelationIdMiddlewareExtensions
{
    public static IApplicationBuilder UseCorrelationId(this IApplicationBuilder app)
    {
        return app.UseMiddleware<CorrelationIdMiddleware>();
    }
}