namespace LLMGateway.Domain.Exceptions;

public class AllProvidersFailedException : Exception
{
    public IReadOnlyList<string> AttemptedProviders { get; }

    public AllProvidersFailedException(IReadOnlyList<string> providers)
        : base($"All providers failed: {string.Join(", ", providers)}")
    {
        AttemptedProviders = providers;
    }
}