using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Exceptions;

public class TokenLimitExceededException : Exception
{
    public TokenCount RequestedTokens { get; }
    public int MaxTokens { get; }

    public TokenLimitExceededException(TokenCount requested, int max)
        : base($"Token count {requested.Value} exceeds limit {max}")
    {
        RequestedTokens = requested;
        MaxTokens = max;
    }
}