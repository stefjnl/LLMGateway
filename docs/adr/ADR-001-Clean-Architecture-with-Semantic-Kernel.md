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