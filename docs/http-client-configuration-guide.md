# HTTP Client Configuration Guide

This guide explains the optimized HTTP client configuration implemented in the LLMGateway to address performance, resilience, and scalability issues.

## Overview

The LLMGateway now features a comprehensive HTTP client configuration that addresses the following issues:

- **Fixed 30-second timeout without connection pooling**
- **Missing HTTP/2 support and connection limits**
- **Risk of socket exhaustion under load**
- **Excessive retry delays and circuit breaker overhead**

## Configuration Properties

### OpenRouter Configuration (`appsettings.json`)

```json
"OpenRouter": {
  "ApiKey": "your-api-key-here",
  "BaseUrl": "https://openrouter.ai/api/v1/",
  "TimeoutSeconds": 60,
  "MaxRetries": 2,
  "CircuitBreakerFailureThreshold": 3,
  "CircuitBreakerDurationSeconds": 30,
  "HealthCheckTimeoutSeconds": 5,
  "MaxConnectionsPerServer": 100,
  "ConnectionLifetimeMinutes": 5,
  "UseHttp2": true
}
```

### Configuration Properties Explained

#### Timeout and Resilience
- **TimeoutSeconds**: HTTP request timeout (60 seconds)
- **MaxRetries**: Number of retry attempts (2 attempts)
- **CircuitBreakerFailureThreshold**: Failures before circuit opens (3 failures)
- **CircuitBreakerDurationSeconds**: Circuit breaker cooldown period (30 seconds)
- **HealthCheckTimeoutSeconds**: Health check timeout (5 seconds)

#### Connection Management
- **MaxConnectionsPerServer**: Maximum concurrent connections (100 connections)
- **ConnectionLifetimeMinutes**: Connection pool lifetime (5 minutes)
- **UseHttp2**: Enable HTTP/2 protocol (true)

## Performance Optimizations

### Connection Pooling
- **Socket Exhaustion Prevention**: Connection pooling prevents socket exhaustion by reusing connections
- **Connection Limits**: Configurable maximum connections per server
- **Connection Lifetime**: Automatic connection recycling every 5 minutes

### HTTP/2 Support
- **Protocol Efficiency**: HTTP/2 reduces latency through header compression and multiplexing
- **Backward Compatibility**: Falls back to HTTP/1.1 if HTTP/2 is not supported

### Resilience Policies

#### Retry Policy
- **Reduced Overhead**: 2 retries instead of 3 (1.5s total delay vs 7s)
- **Jitter**: Random delay variation prevents synchronized retry storms
- **Exponential Backoff**: 0.5s, 1s delays with jitter

#### Circuit Breaker
- **Proper Wrapping**: Circuit breaker now wraps retry policy (critical fix)
- **Reasonable Threshold**: 3 failures trigger circuit breaker
- **30-second Cooldown**: Prevents unnecessary blocking

## Implementation Details

### Polly Policy Configuration

```csharp
// Correct policy wrapping order
var resiliencePolicy = Policy.WrapAsync(circuitBreaker, retry);
```

### SocketsHttpHandler Configuration

```csharp
var handler = new SocketsHttpHandler
{
    PooledConnectionLifetime = TimeSpan.FromMinutes(config.ConnectionLifetimeMinutes),
    MaxConnectionsPerServer = config.MaxConnectionsPerServer,
    EnableMultipleHttp2Connections = true
};
```

## Performance Impact

### Before Optimization
- **Single Request**: Up to 37 seconds (30s timeout + 7s retry delays)
- **Socket Exhaustion**: High risk under moderate load
- **No Connection Pooling**: New sockets for each request

### After Optimization
- **Single Request**: Up to 61.5 seconds (60s timeout + 1.5s retry delays)
- **Socket Management**: Connection pooling prevents exhaustion
- **HTTP/2**: Reduced latency and improved throughput

## Configuration Recommendations

### Development Environment
```json
"TimeoutSeconds": 30,
"MaxRetries": 1,
"HealthCheckTimeoutSeconds": 2
```

### Production Environment
```json
"TimeoutSeconds": 60,
"MaxRetries": 2,
"MaxConnectionsPerServer": 200,
"ConnectionLifetimeMinutes": 10
```

### High-Volume Environment
```json
"TimeoutSeconds": 90,
"MaxRetries": 3,
"MaxConnectionsPerServer": 500,
"CircuitBreakerFailureThreshold": 5
```

## Monitoring and Troubleshooting

### Health Checks
- **Endpoint**: `/health/openrouter`
- **Timeout**: Configurable via `HealthCheckTimeoutSeconds`
- **Response**: JSON with provider status and response time

### Logging
- **Request Timeouts**: Logged with correlation IDs
- **Circuit Breaker Events**: Open/close transitions logged
- **Retry Attempts**: Each retry attempt logged

### Metrics
- **Connection Pool Usage**: Monitor connection count
- **Request Duration**: Track response times
- **Error Rates**: Monitor failure patterns

## Migration Guide

### From Previous Version
1. Update `appsettings.json` with new configuration properties
2. Verify health check endpoints are working
3. Monitor connection pool usage during load testing
4. Adjust timeouts based on observed performance

### Configuration Validation
- **Required Properties**: ApiKey, BaseUrl, TimeoutSeconds
- **Optional Properties**: All other settings have sensible defaults
- **Validation**: Configuration validated at startup

## Best Practices

### Timeout Configuration
- Set timeouts based on expected LLM response times
- Consider network latency and provider SLAs
- Use shorter timeouts for health checks

### Connection Pooling
- Monitor connection pool usage under load
- Adjust `MaxConnectionsPerServer` based on expected concurrency
- Use `ConnectionLifetimeMinutes` to prevent stale connections

### Resilience
- Test circuit breaker behavior with simulated failures
- Monitor retry patterns during provider outages
- Use health checks for proactive monitoring

## Troubleshooting Common Issues

### Socket Exhaustion
- **Symptom**: "Unable to connect" errors under load
- **Solution**: Increase `MaxConnectionsPerServer`
- **Diagnosis**: Monitor connection pool metrics

### Timeout Issues
- **Symptom**: Frequent timeouts
- **Solution**: Adjust `TimeoutSeconds` or investigate provider performance
- **Diagnosis**: Check request duration metrics

### Circuit Breaker Tripping
- **Symptom**: Requests blocked despite provider being healthy
- **Solution**: Adjust `CircuitBreakerFailureThreshold`
- **Diagnosis**: Monitor failure patterns and health checks