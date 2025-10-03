namespace LLMGateway.Application.DTOs;

public class ChatRequest
{
    public IEnumerable<Message> Messages { get; set; } = Enumerable.Empty<Message>();
    public string? Model { get; set; }
    public decimal? Temperature { get; set; }
    public int? MaxTokens { get; set; }
}