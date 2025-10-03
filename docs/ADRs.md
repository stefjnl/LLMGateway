# ADR-001: Clean Architecture with Semantic Kernel

**Status:** Accepted  
**Date:** 2025-10-03  
**Decision Makers:** Stefan  
**Tags:** architecture, design-patterns, semantic-kernel

## Context

LLM Gateway v2 requires Clean Architecture that integrates Semantic Kernel as the orchestration engine while maintaining dependency inversion and testability.

## Decision

Implement **Clean Architecture (Onion)** with **Semantic Kernel as the Application Layer orchestrator**, not just an Infrastructure implementation detail.

## Architecture

```
┌─────────────────────────────────────────┐
│         API Layer                       │
│  (Controllers, Middleware)              │
└──────────────┬──────────────────────────┘
               │ depends on
               ▼
┌─────────────────────────────────────────┐
│      Application Layer                  │
│  ┌────────────────────────────────┐    │
│  │  Kernel Orchestrator           │    │
│  │  (Configures SK Kernel)        │    │
│  └────────────────────────────────┘    │
│  ┌────────────────────────────────┐    │
│  │  SK Plugins (Your Logic)       │    │
│  │  - ModelSelectionPlugin        │    │
│  │  - CostTrackingPlugin          │    │
│  │  - GuardrailsPlugin            │    │
│  └────────────────────────────────┘    │
│  Commands/Queries (Thin wrappers)      │
└──────────────┬──────────────────────────┘
               │ depends on
               ▼
┌─────────────────────────────────────────┐
│         Domain Layer                    │
│  (Entities, Value Objects, Rules)      │
│  - RequestLog, CostEstimate            │
│  - ModelName, TokenCount               │
│  - NO SK dependencies                  │
└─────────────────────────────────────────┘
               ▲ implements
               │
┌──────────────┴──────────────────────────┐
│    Infrastructure Layer                 │
│  ┌────────────────────────────────┐    │
│  │  SK Services                   │    │
│  │  - IChatCompletionService      │    │
│  │  - IMemoryStore (future)       │    │
│  └────────────────────────────────┘    │
│  - PostgreSQL DbContext                │
│  - OpenRouter HTTP Client              │
└─────────────────────────────────────────┘
```

## Layer Responsibilities

**Domain Layer (Pure):**
- Business entities, value objects, business rules
- **Zero SK dependencies**
- Interfaces for data persistence only

**Application Layer (SK-Powered):**
- `KernelOrchestrator`: Configures SK Kernel with plugins
- **SK Plugins**: Your business logic as `[KernelFunction]` methods
- Thin CQRS commands that delegate to Kernel
- DTOs for API contracts

**Infrastructure Layer:**
- SK service implementations (`IChatCompletionService`)
- PostgreSQL repositories
- HTTP clients for providers

**API Layer:**
- Controllers delegate to KernelOrchestrator
- Minimal logic

## Key Difference from ADR-001 v1

**Old approach:** Application layer contained explicit orchestration logic (routers, handlers with business rules).

**New approach:** SK Kernel *is* the orchestrator. Your logic lives in plugins that SK invokes automatically.

## Consequences

**Positive:**
- SK handles complex orchestration (function calling, planning)
- Plugins are independently testable
- Scales naturally to multi-agent workflows
- Leverages SK's built-in token counting, prompt engineering

**Negative:**
- Less explicit control flow (SK decides execution order)
- Learning curve for SK plugin model
- CQRS becomes less prominent (SK is the mediator)

**Mitigation:**
- Keep plugins simple (single responsibility)
- Use SK's `KernelArguments` for explicit parameter passing
- Maintain thin CQRS layer for API contracts

## Related Decisions

- **ADR-002**: CQRS simplified for SK integration
- **ADR-005**: SK integration strategy details

---

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

---

# ADR-005: Semantic Kernel Integration Strategy

**Status:** Accepted  
**Date:** 2025-10-03  
**Decision Makers:** Stefan  
**Tags:** semantic-kernel, ai-orchestration, plugins

## Context

Semantic Kernel can be used as a simple provider wrapper OR as a full orchestration engine. We must decide integration depth.

## Decision

Use **Semantic Kernel as the primary orchestration engine** with custom plugins for business logic.

## Integration Approach

### SK Responsibilities

**Kernel handles:**
- Prompt execution and response parsing
- Token counting (via tokenizer)
- Function calling (auto-invokes plugins)
- Future: Planning, multi-agent coordination

**Our plugins provide:**
- Model selection logic (ADR-003 rules)
- Cost tracking to PostgreSQL
- Guardrails (future: PII detection)
- Provider fallback logic

### Plugin Architecture

```csharp
// Model Selection Plugin (ADR-003 as plugin)
public class ModelSelectionPlugin {
    [KernelFunction("select_model")]
    [Description("Selects model based on context and user preference")]
    public string SelectModel(
        [Description("Estimated token count")] int tokenCount,
        [Description("User-specified model")] string? userModel = null)
    {
        if (userModel != null) return userModel;
        if (tokenCount > 10000) return "moonshotai/Kimi-K2-Instruct-0905";
        return "z-ai/glm-4.6";
    }
}

// Cost Tracking Plugin
public class CostTrackingPlugin {
    private readonly IRequestLogRepository _repository;
    
    [KernelFunction("track_cost")]
    [Description("Logs request cost to database")]
    public async Task TrackCost(
        [Description("Model used")] string model,
        [Description("Tokens consumed")] int tokens,
        [Description("Response content")] string response)
    {
        var cost = CalculateCost(model, tokens);
        await _repository.SaveAsync(new RequestLog {
            ModelUsed = model,
            TokenCount = tokens,
            EstimatedCostUsd = cost,
            Timestamp = DateTime.UtcNow
        });
    }
}
```

### Kernel Configuration

```csharp
public class KernelFactory {
    public Kernel CreateWithPlugins() {
        var builder = Kernel.CreateBuilder();
        
        // Register chat completion service
        builder.AddOpenAIChatCompletion(
            modelId: "gpt-4", // Overridden by plugin
            apiKey: _config["OpenRouter:ApiKey"],
            httpClient: _httpClientFactory.CreateClient("OpenRouter")
        );
        
        // Register plugins
        builder.Plugins.AddFromObject(
            new ModelSelectionPlugin(), "ModelSelection");
        builder.Plugins.AddFromObject(
            new CostTrackingPlugin(_repository), "CostTracking");
        
        return builder.Build();
    }
}
```

### Orchestrator Pattern

```csharp
public class KernelOrchestrator {
    private readonly KernelFactory _factory;
    
    public async Task<ChatResponse> SendChatCompletion(
        SendChatCompletionCommand command)
    {
        var kernel = _factory.CreateWithPlugins();
        
        // Prepare arguments for plugins
        var args = new KernelArguments {
            ["tokenCount"] = EstimateTokens(command.Messages),
            ["userModel"] = command.Model
        };
        
        // SK automatically:
        // 1. Calls ModelSelectionPlugin.select_model()
        // 2. Executes chat completion with selected model
        // 3. Calls CostTrackingPlugin.track_cost()
        var result = await kernel.InvokePromptAsync(
            BuildPrompt(command.Messages),
            args
        );
        
        return new ChatResponse {
            Content = result.ToString(),
            Model = args["selectedModel"]?.ToString()
        };
    }
}
```

## Provider Fallback Strategy

**Handled via custom plugin:**
```csharp
public class ProviderFallbackPlugin {
    [KernelFunction("fallback_handler")]
    public async Task<string> HandleFailure(
        string failedModel,
        Exception error)
    {
        // Circular fallback: glm-4.6 → DeepSeek → Kimi-K2
        var fallbackChain = new[] {
            "z-ai/glm-4.6",
            "deepseek-ai/DeepSeek-V3.1-Terminus",
            "moonshotai/Kimi-K2-Instruct-0905"
        };
        
        var index = Array.IndexOf(fallbackChain, failedModel);
        if (index == -1 || index == fallbackChain.Length - 1)
            throw new AllProvidersFailedException();
        
        return fallbackChain[index + 1];
    }
}
```

## SK Service Implementation

**Infrastructure layer implements SK interfaces:**
```csharp
public class OpenRouterChatCompletionService 
    : IChatCompletionService
{
    public async Task<IReadOnlyList<ChatMessageContent>> 
        GetChatMessageContentsAsync(
            ChatHistory chatHistory,
            PromptExecutionSettings? settings = null,
            Kernel? kernel = null,
            CancellationToken cancellationToken = default)
    {
        // Call OpenRouter API
        // Map to SK's ChatMessageContent
        // Handle errors → trigger fallback plugin
    }
}
```

## Testing Strategy

**Unit test plugins independently:**
```csharp
[Fact]
public void SelectModel_LargeContext_ReturnsKimi() {
    var plugin = new ModelSelectionPlugin();
    var result = plugin.SelectModel(tokenCount: 15000);
    Assert.Equal("moonshotai/Kimi-K2-Instruct-0905", result);
}
```

**Integration test with mock kernel:**
```csharp
[Fact]
public async Task SendCompletion_UsesModelSelectionPlugin() {
    var kernel = CreateTestKernel();
    var orchestrator = new KernelOrchestrator(kernel);
    
    var response = await orchestrator.SendChatCompletion(
        new SendChatCompletionCommand(largeMessages)
    );
    
    Assert.Equal("moonshotai/Kimi-K2-Instruct-0905", response.Model);
}
```

## Future Extensibility

**Phase 2: Guardrails Plugin**
```csharp
[KernelFunction("detect_pii")]
public async Task<bool> ContainsPII(string content) {
    // Use Presidio or custom regex
}
```

**Phase 4: MCP Server Plugin**
```csharp
[KernelFunction("query_mcp_tool")]
public async Task<string> QueryMCPServer(
    string serverName, 
    string toolName, 
    object parameters) {
    // Call MCP server via HTTP
}
```

**Phase 5: Multi-Agent Orchestration**
```csharp
var researchAgent = kernel.Plugins["ResearchAgent"];
var analysisAgent = kernel.Plugins["AnalysisAgent"];

var researchResult = await kernel.InvokeAsync(
    researchAgent["gather_data"], args);
var analysis = await kernel.InvokeAsync(
    analysisAgent["analyze"], researchResult);
```

## Consequences

**Positive:**
- SK handles orchestration complexity
- Plugins are testable, reusable units
- Natural path to multi-agent workflows
- Built-in token counting, prompt engineering

**Negative:**
- Less explicit control than custom orchestration
- SK learning curve for team
- Plugin invocation order controlled by SK

**Mitigation:**
- Start with simple plugins (MVP: 2 plugins only)
- Document SK invocation patterns
- Use SK's `KernelArguments` for explicit data flow

## Alternatives Considered

**1. SK as Provider Wrapper Only**
Rejected—underutilizes SK's orchestration capabilities.

**2. Custom Orchestration + SK Services**
Rejected—reinvents wheels SK already solves.

**3. LangChain**
Rejected—Python-first, .NET support is limited.

## Related Decisions

- **ADR-001**: SK positioned in Application layer
- **ADR-002**: CQRS simplified around SK
- **ADR-003**: Model routing now lives in plugin

---

# Clean Architecture Tree with Semantic Kernel

```
LLMGateway/
├── src/
│   ├── LLMGateway.Domain/
│   │   ├── Entities/
│   │   │   ├── RequestLog.cs
│   │   │   └── CostEstimate.cs
│   │   ├── ValueObjects/
│   │   │   ├── ModelName.cs
│   │   │   ├── TokenCount.cs
│   │   │   └── CostAmount.cs
│   │   ├── Interfaces/
│   │   │   └── IRequestLogRepository.cs
│   │   └── Exceptions/
│   │       ├── AllProvidersFailedException.cs
│   │       └── TokenLimitExceededException.cs
│   │
│   ├── LLMGateway.Application/
│   │   ├── Orchestration/
│   │   │   ├── KernelOrchestrator.cs         ← Entry point
│   │   │   └── KernelFactory.cs              ← Configures SK
│   │   ├── Plugins/
│   │   │   ├── ModelSelectionPlugin.cs       ← ADR-003 logic
│   │   │   ├── CostTrackingPlugin.cs         ← Logs to DB
│   │   │   └── ProviderFallbackPlugin.cs     ← Retry logic
│   │   ├── DTOs/
│   │   │   ├── ChatRequest.cs
│   │   │   ├── ChatResponse.cs
│   │   │   └── RequestLogDto.cs
│   │   ├── Commands/
│   │   │   └── SendChatCompletionCommand.cs  ← Thin DTO
│   │   ├── Queries/
│   │   │   └── GetRequestLogQuery.cs         ← Direct DB
│   │   └── Mappers/
│   │       └── ResponseMapper.cs
│   │
│   ├── LLMGateway.Infrastructure/
│   │   ├── SemanticKernel/
│   │   │   ├── Services/
│   │   │   │   └── OpenRouterChatCompletionService.cs  ← IChatCompletionService
│   │   │   └── Configuration/
│   │   │       └── SKServiceCollectionExtensions.cs
│   │   ├── Persistence/
│   │   │   ├── GatewayDbContext.cs
│   │   │   ├── Repositories/
│   │   │   │   └── RequestLogRepository.cs
│   │   │   └── Configurations/
│   │   │       └── RequestLogConfiguration.cs
│   │   └── Http/
│   │       └── OpenRouterHttpClient.cs
│   │
│   └── LLMGateway.Api/
│       ├── Controllers/
│       │   ├── ChatCompletionController.cs    ← Calls KernelOrchestrator
│       │   └── HealthController.cs
│       ├── Middleware/
│       │   └── ExceptionHandlingMiddleware.cs
│       ├── Program.cs
│       └── appsettings.json
│
├── tests/
│   ├── LLMGateway.Domain.Tests/
│   ├── LLMGateway.Application.Tests/
│   │   └── Plugins/
│   │       ├── ModelSelectionPluginTests.cs
│   │       └── CostTrackingPluginTests.cs
│   └── LLMGateway.Api.Tests/
│
└── docs/
    └── adr/
        ├── 001-clean-architecture.md
        ├── 002-cqrs-pattern.md
        ├── 003-model-routing-strategy.md
        ├── 004-postgresql-choice.md
        └── 005-sk-integration-strategy.md
```

---

# Request Flow

```
┌─────────────────────────────────────────────────────────────────┐
│ 1. API Layer                                                    │
│    POST /v1/chat/completions                                    │
│    ChatCompletionController receives request                    │
└────────────────┬────────────────────────────────────────────────┘
                 │ Maps to Command
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│ 2. Application Layer                                            │
│    KernelOrchestrator.SendChatCompletion(command)              │
│    ├─ Creates Kernel via KernelFactory                         │
│    ├─ Prepares KernelArguments (tokenCount, userModel)         │
│    └─ Invokes kernel.InvokePromptAsync()                       │
└────────────────┬────────────────────────────────────────────────┘
                 │ SK orchestrates plugins
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│ 3. SK Automatic Plugin Invocation                              │
│    ┌──────────────────────────────────────────────┐            │
│    │ Step 1: ModelSelectionPlugin.select_model()  │            │
│    │   → Returns "z-ai/glm-4.6"                   │            │
│    └──────────────────────────────────────────────┘            │
│    ┌──────────────────────────────────────────────┐            │
│    │ Step 2: IChatCompletionService.GetChatAsync()│            │
│    │   → Calls OpenRouter API                     │            │
│    │   → Returns ChatMessageContent               │            │
│    └──────────────────────────────────────────────┘            │
│    ┌──────────────────────────────────────────────┐            │
│    │ Step 3: CostTrackingPlugin.track_cost()     │            │
│    │   → Calculates cost                          │            │
│    │   → Calls IRequestLogRepository              │            │
│    └──────────────────────────────────────────────┘            │
└────────────────┬────────────────────────────────────────────────┘
                 │ Returns FunctionResult
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│ 4. Infrastructure Layer                                         │
│    OpenRouterChatCompletionService                             │
│    ├─ HTTP POST to api.openrouter.ai                           │
│    ├─ Handles 429/500 errors → triggers ProviderFallbackPlugin │
│    └─ Maps response to ChatMessageContent                      │
│                                                                 │
│    RequestLogRepository                                         │
│    └─ INSERT INTO request_logs (model, tokens, cost, timestamp)│
└────────────────┬────────────────────────────────────────────────┘
                 │ Returns to orchestrator
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│ 5. Application Layer                                            │
│    KernelOrchestrator maps result to ChatResponse              │
└────────────────┬────────────────────────────────────────────────┘
                 │ Returns DTO
                 ▼
┌─────────────────────────────────────────────────────────────────┐
│ 6. API Layer                                                    │
│    Controller returns 200 OK with ChatResponse                  │
└─────────────────────────────────────────────────────────────────┘
```

---

**Key takeaway:** The business logic (ADR-003 routing, cost tracking) is now **plugin-based**. SK handles orchestration. Clean Architecture preserved—Domain is pure, SK sits in Application/Infrastructure boundary.
