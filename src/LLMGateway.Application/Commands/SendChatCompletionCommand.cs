using LLMGateway.Application.DTOs;

namespace LLMGateway.Application.Commands;

public record SendChatCompletionCommand(
    IEnumerable<Message> Messages,
    string? Model = null,
    decimal? Temperature = null,
    int? MaxTokens = null)
{
    public void Validate()
    {
        if (Messages == null || !Messages.Any())
            throw new ArgumentException("Messages cannot be empty", nameof(Messages));

        if (Messages.Any(m => string.IsNullOrWhiteSpace(m.Content)))
            throw new ArgumentException("Message content cannot be empty", nameof(Messages));

        if (Temperature.HasValue && (Temperature < 0 || Temperature > 2))
            throw new ArgumentException(
                "Temperature must be between 0 and 2",
                nameof(Temperature));

        if (MaxTokens.HasValue && MaxTokens <= 0)
            throw new ArgumentException(
                "MaxTokens must be positive",
                nameof(MaxTokens));
    }
}