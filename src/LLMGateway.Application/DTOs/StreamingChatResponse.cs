namespace LLMGateway.Application.DTOs;

/// <summary>
/// Response for streaming chat completion with Server-Sent Events
/// </summary>
public class StreamingChatResponse
{
    /// <summary>
    /// Type of event: 'chunk' for partial content, 'complete' for final metadata
    /// </summary>
    public string Type { get; set; } = string.Empty;

    /// <summary>
    /// Partial content chunk (only present for 'chunk' events)
    /// </summary>
    public string? Content { get; set; }

    /// <summary>
    /// Final metadata (only present for 'complete' event)
    /// </summary>
    public StreamingMetadata? Metadata { get; set; }
}

/// <summary>
/// Final metadata for streaming response
/// </summary>
public class StreamingMetadata
{
    /// <summary>
    /// Complete response content
    /// </summary>
    public string Content { get; set; } = string.Empty;

    /// <summary>
    /// Model used for the response
    /// </summary>
    public string Model { get; set; } = string.Empty;

    /// <summary>
    /// Total tokens used (input + output)
    /// </summary>
    public int TotalTokens { get; set; }

    /// <summary>
    /// Total response time in milliseconds
    /// </summary>
    public double ResponseTimeMs { get; set; }

    /// <summary>
    /// Average tokens per second (tokens ÷ seconds)
    /// </summary>
    public double AverageTokensPerSecond { get; set; }

    /// <summary>
    /// Estimated cost in USD
    /// </summary>
    public decimal EstimatedCostUsd { get; set; }

    /// <summary>
    /// Provider name
    /// </summary>
    public string Provider { get; set; } = string.Empty;
}