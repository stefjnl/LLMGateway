using Polly;
using Polly.CircuitBreaker;
using Polly.Retry;
using Microsoft.Extensions.Logging;
using LLMGateway.Infrastructure.ChatCompletion;

namespace LLMGateway.Infrastructure.Resilience;

/// <summary>
/// Factory for creating Polly resilience policies for OpenRouter API calls.
/// </summary>
public static class PollyPolicies
{
    /// <summary>
    /// Creates a combined retry + circuit breaker policy for HTTP calls.
    /// Retry wraps circuit breaker to prevent false positives.
    /// </summary>
    public static IAsyncPolicy<HttpResponseMessage> CreateOpenRouterPolicy(
        ILogger logger,
        OpenRouterConfig config)
    {
        // Circuit breaker: opens after N consecutive failures, stays open for cooldown period
        var circuitBreaker = Policy
            .HandleResult<HttpResponseMessage>(IsTransientError)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .CircuitBreakerAsync(
                handledEventsAllowedBeforeBreaking: config.CircuitBreakerFailureThreshold,
                durationOfBreak: TimeSpan.FromSeconds(config.CircuitBreakerCooldownSeconds),
                onBreak: (result, timespan) =>
                {
                    logger.LogWarning("Circuit breaker opened for {Duration}. Last result: {Result}",
                        timespan, result?.Result?.StatusCode.ToString() ?? "Exception");
                },
                onReset: () =>
                {
                    logger.LogInformation("Circuit breaker reset - service is healthy again");
                },
                onHalfOpen: () =>
                {
                    logger.LogInformation("Circuit breaker half-open - testing service health");
                });

        // Retry policy: exponential backoff for transient errors
        var retry = Policy
            .HandleResult<HttpResponseMessage>(IsTransientError)
            .Or<HttpRequestException>()
            .Or<TaskCanceledException>()
            .WaitAndRetryAsync(
                retryCount: config.MaxRetries,
                sleepDurationProvider: (attempt) => TimeSpan.FromSeconds(Math.Pow(2, attempt - 1)), // 1s, 2s, 4s
                onRetry: (result, timespan, attempt, context) =>
                {
                    logger.LogWarning("Retry attempt {Attempt} after {Delay}. Result: {Result}",
                        attempt, timespan, result?.Result?.StatusCode.ToString() ?? "Exception");
                });

        // Combine: retry wraps circuit breaker
        return Policy.WrapAsync(retry, circuitBreaker);
    }

    /// <summary>
    /// Determines if an HTTP response represents a transient error that should be retried.
    /// </summary>
    private static bool IsTransientError(HttpResponseMessage response)
    {
        return response.StatusCode switch
        {
            System.Net.HttpStatusCode.TooManyRequests => true, // 429
            System.Net.HttpStatusCode.InternalServerError => true, // 500
            System.Net.HttpStatusCode.BadGateway => true, // 502
            System.Net.HttpStatusCode.ServiceUnavailable => true, // 503
            System.Net.HttpStatusCode.GatewayTimeout => true, // 504
            _ => false
        };
    }
}