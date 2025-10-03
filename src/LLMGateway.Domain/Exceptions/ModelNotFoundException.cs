using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Exceptions;

public class ModelNotFoundException : Exception
{
    public ModelName RequestedModel { get; }

    public ModelNotFoundException(ModelName model)
        : base($"Model '{model.Value}' not found in any provider")
    {
        RequestedModel = model;
    }
}