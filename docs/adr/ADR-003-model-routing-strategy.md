# ADR-003: Model Routing Strategy

**Status:** Accepted  
**Date:** 2025-10-01  
**Decision Makers:** Stefan  
**Tags:** business-logic, routing, models

## Context

The LLM Gateway must intelligently route requests across multiple models based on cost, performance, and context size. We need a decision tree that balances simplicity (for MVP) with extensibility (for future phases).

## Decision

We will implement a **3-rule decision tree** for MVP with a **circular fallback chain** to handle provider failures.

## Rationale

### 3-Rule Decision Tree

**Rule 1: User-Specified Model**
- **Trigger:** Request includes explicit `model` parameter
- **Action:** Use specified model directly
- **Rationale:** Respect user intent—developers may need specific model capabilities

**Rule 2: Large Context Detection**
- **Trigger:** Estimated tokens > 10,000
- **Action:** Route to `moonshotai/Kimi-K2-Instruct-0905` (200K context window)
- **Rationale:** Large prompts require specialized models; routing to standard models would fail or truncate

**Rule 3: Default Fast/Cheap**
- **Trigger:** No specific model requested, context < 10K tokens
- **Action:** Route to `z-ai/glm-4.6` (lowest cost, fastest response)
- **Rationale:** Optimize for cost on routine requests

**Why 3 Rules for MVP?**
- Simple enough to implement in 15-20 minutes
- Covers 90% of use cases (explicit, large context, default)
- Easy to test and verify behavior
- Avoids premature optimization (no quality scoring, latency prediction, etc.)

### Fallback Model Selection

**Circular Fallback Chain:**
```
z-ai/glm-4.6 
    → deepseek-ai/DeepSeek-V3.1-Terminus 
        → moonshotai/Kimi-K2-Instruct-0905 
            → z-ai/glm-4.6 (throw exception on cycle)
```

**Rationale:**
- **Graduated fallback**: Fast → Balanced → Large context
- **Cycle detection**: After 3 failures, throw exception (all providers down)
- **Fail-fast**: Don't retry indefinitely; surface errors to client

**Why Circular Instead of Linear?**
- Ensures maximum resilience—tries all available providers
- Simple to implement and reason about
- Prevents infinite retry loops with explicit cycle detection

**Transient Error Handling:**
Fallback triggers on:
- `429 Too Many Requests` (rate limiting)
- `500/502/503/504` (server errors)
- `HttpRequestException`, `TimeoutException`

Does NOT trigger on:
- `401 Unauthorized` (configuration issue, not transient)
- `400 Bad Request` (client error, retry won't help)

### Future Extensibility

**Phase 2 Enhancements (deferred from MVP):**

**Cost-Based Routing:**
- Add budget tracking per tenant
- Route to cheaper models when approaching budget limit
- Example: If tenant spent >80% of budget, prefer `glm-4.6` over `Kimi-K2`

**Quality-Based Routing:**
- Benchmark models on standard tasks (coding, reasoning, creativity)
- Route based on task type detection in prompt
- Example: "Write a poem" → creative model, "Debug this code" → coding model

**Latency-Based Routing:**
- Track P95 latency per model
- Route latency-sensitive requests to fastest model
- Example: Real-time chat UI → prioritize speed over quality

**Load Balancing:**
- Track provider health/availability
- Distribute requests across healthy providers
- Example: If `glm-4.6` has 3 consecutive failures, prefer `DeepSeek` temporarily

**Model Capabilities Matching:**
- Maintain model capability registry (function calling, vision, etc.)
- Route based on required capabilities
- Example: Request with images → vision-capable model

### Configuration-Driven Routing (Future)

MVP hardcodes routing rules for speed. Future versions should externalize configuration to allow dynamic rule updates without code changes.

## Consequences

### Positive

- **Simple to implement**: 3 rules = clear if/else logic
- **Predictable behavior**: Easy to debug and explain
- **Resilient**: Fallback chain handles provider outages
- **Cost-optimized**: Defaults to cheapest model for routine requests
- **Extensible**: Clear path to add sophistication in later phases

### Negative

- **No quality optimization**: May route complex requests to simple models
- **Hardcoded thresholds**: 10K token threshold is arbitrary
- **No load balancing**: Always tries same provider first
- **No A/B testing**: Can't experiment with routing strategies

### Mitigation

- Monitor request logs to identify misrouted requests
- Collect feedback on response quality per model
- Phase 2: Add quality scoring based on real usage data
- Document routing decisions in logs for observability

## Alternatives Considered

### 1. ML-Based Router
**Deferred because:**
- Requires training data (model performance on various prompts)
- Adds significant complexity and latency
- Premature optimization—need baseline data first
- Consider in Phase 3+ after collecting usage patterns

### 2. Round-Robin Load Balancing
**Rejected because:**
- Ignores model capabilities (all models are not equal)
- Poor cost optimization (distributes across expensive models)
- Better suited for homogeneous provider pool

### 3. Multi-Model Ensemble
**Deferred because:**
- Requires calling multiple models per request (high cost)
- Adds latency (wait for slowest model)
- Useful for quality-critical applications, not general gateway
- Consider for specific premium tier in future

### 4. Prompt Complexity Analysis
**Deferred because:**
- Requires sophisticated NLP analysis of prompt
- Adds latency before routing
- Marginal benefit over simple token counting
- Revisit in Phase 4 if demand exists

## Implementation Notes

### Token Estimation
MVP uses simple character-based estimation:
```
estimatedTokens = totalCharacters / 4
```

**Limitations:**
- Inaccurate for non-English languages
- Doesn't account for special tokens
- Good enough for routing decisions (order of magnitude correct)

**Future:** Use proper tokenizer library (tiktoken) in Phase 2

### Fallback Retry Strategy
- **Max retries:** 3 (one per provider in chain)
- **Backoff:** Exponential (1s, 2s, 4s)
- **Timeout per attempt:** 30 seconds
- **Total timeout:** ~2 minutes maximum

## Metrics to Track

Monitor these to inform Phase 2 improvements:
- Model selection frequency (which models are most used?)
- Fallback trigger rate (how often do providers fail?)
- Cost per request by model
- User overrides (how often do users specify models?)
- Token estimation accuracy vs. actual usage

## Related Decisions

- **ADR-002**: Router logic lives in Infrastructure layer, called from Application command handler
- **ADR-005** (future): May extract routing to separate microservice if complexity grows

## References

- OpenRouter documentation on model capabilities
- Token estimation strategies: https://platform.openai.com/tokenizer
- Circuit breaker pattern: Microsoft resilience guidelines