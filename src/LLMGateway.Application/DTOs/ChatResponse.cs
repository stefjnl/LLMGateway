namespace LLMGateway.Application.DTOs;

public class ChatResponse
{
    public string Content { get; set; } = string.Empty;
    public string Model { get; set; } = string.Empty;
    public int TokensUsed { get; set; }
    public decimal EstimatedCostUsd { get; set; }
    public TimeSpan ResponseTime { get; set; }
}