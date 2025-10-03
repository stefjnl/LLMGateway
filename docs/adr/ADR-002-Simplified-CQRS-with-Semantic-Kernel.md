# ADR-002: Simplified CQRS with Semantic Kernel

**Status:** Accepted  
**Date:** 2025-10-03  
**Decision Makers:** Stefan  
**Tags:** architecture, design-patterns, cqrs, semantic-kernel

## Context

Original CQRS pattern (ADR-002 v1) used MediatR for full command/query separation. With SK as orchestrator, we need lighter CQRS.

## Decision

Implement **thin CQRS layer** as API contracts, with SK Kernel handling orchestration instead of MediatR handlers.

## Structure

```
Application/
├── Orchestration/
│   ├── KernelOrchestrator.cs
│   └── KernelFactory.cs
├── Plugins/
│   ├── ModelSelectionPlugin.cs
│   ├── CostTrackingPlugin.cs
│   └── GuardrailsPlugin.cs (future)
├── Commands/
│   └── SendChatCompletionCommand.cs (thin DTO)
└── Queries/
    └── GetRequestLogQuery.cs (direct DB query)
```

## Command Flow

**Before (MediatR):**
```csharp
// Handler contained ALL logic
public class SendChatCompletionCommandHandler 
    : IRequestHandler<SendChatCompletionCommand, ChatResponse>
{
    public async Task<ChatResponse> Handle(...) {
        // Model selection logic
        // Provider call
        // Cost tracking
        // Return response
    }
}
```

**After (SK Orchestrator):**
```csharp
// Command is thin DTO
public record SendChatCompletionCommand(
    IEnumerable<Message> Messages,
    string? Model = null
);

// Orchestrator delegates to SK
public class KernelOrchestrator {
    public async Task<ChatResponse> SendChatCompletion(
        SendChatCompletionCommand command) 
    {
        var kernel = _kernelFactory.CreateWithPlugins();
        
        // SK automatically invokes plugins as needed
        var result = await kernel.InvokePromptAsync(
            command.Messages.ToPromptString(),
            new KernelArguments {
                ["userModel"] = command.Model,
                ["tokenCount"] = EstimateTokens(command.Messages)
            }
        );
        
        return MapToResponse(result);
    }
}
```

**Plugin contains business logic:**
```csharp
public class ModelSelectionPlugin {
    [KernelFunction("select_model")]
    [Description("Selects optimal model based on context size")]
    public string SelectModel(
        int tokenCount, 
        string? userModel = null)
    {
        if (userModel != null) return userModel;
        if (tokenCount > 10000) return "Kimi-K2";
        return "glm-4.6";
    }
}
```

## Query Pattern (Unchanged)

Queries bypass SK—direct database access via repositories:
```csharp
public class GetRequestLogQuery {
    public async Task<IEnumerable<RequestLogDto>> Execute() {
        return await _dbContext.RequestLogs
            .OrderByDescending(x => x.Timestamp)
            .Take(100)
            .ToListAsync();
    }
}
```

## When to Use MediatR vs SK

**Use SK Kernel for:**
- Anything involving LLM calls
- Multi-step orchestration
- Plugin-based logic

**Use direct methods for:**
- Simple queries (read logs)
- Health checks
- Configuration endpoints

**No MediatR needed** for MVP—SK replaces it for command orchestration.

## Consequences

**Positive:**
- Simpler than full CQRS + MediatR
- SK's orchestration is more powerful than MediatR pipelines
- Plugins are reusable across agents

**Negative:**
- Less ceremony/structure than traditional CQRS
- Controllers call orchestrator directly (no mediator indirection)

**Mitigation:**
- Maintain clear command DTOs for API contracts
- Keep orchestrator thin—logic goes in plugins
- Document SK invocation patterns

## Related Decisions

- **ADR-001**: SK positioned in Application layer
- **ADR-005**: Plugin architecture details