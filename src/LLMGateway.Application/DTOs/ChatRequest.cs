using System.ComponentModel.DataAnnotations;

namespace LLMGateway.Application.DTOs;

public class ChatRequest
{
    [Required(ErrorMessage = "Messages field is required")]
    [MinLength(1, ErrorMessage = "At least one message is required")]
    public IEnumerable<Message> Messages { get; set; } = Enumerable.Empty<Message>();

    public string? Model { get; set; }

    [Range(0, 2, ErrorMessage = "Temperature must be between 0 and 2")]
    public decimal? Temperature { get; set; }

    [Range(1, int.MaxValue, ErrorMessage = "MaxTokens must be greater than 0")]
    public int? MaxTokens { get; set; }
}