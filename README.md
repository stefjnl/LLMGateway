# LLM Gateway

Enterprise LLM Gateway with Semantic Kernel orchestration - demonstrating Clean Architecture in .NET 9.

## Overview

This repository contains an LLM Gateway, a service that acts as an intelligent router for requests to various Large Language Models (LLMs). It's built with .NET 9 and uses Microsoft's Semantic Kernel for AI orchestration.

The main purpose is to provide a centralized point of control for using LLMs, with features like:

Model Routing: Intelligently selects the best LLM for a given request based on factors like prompt size, user preference, and cost.
Resilience: Automatically falls back to a different model if a provider fails.
Cost Tracking: (As implied by the project structure) Logs requests and tracks costs.
Observability: Uses .NET Aspire for dashboards and monitoring.
Architecture
The project follows a Clean Architecture (also known as Onion Architecture), which keeps the core business logic independent of external frameworks and dependencies. Here’s a breakdown of the layers, from the inside out:

LLMGateway.Domain: This is the core of the application. It contains the business entities (like RequestLog), value objects, and interfaces for repositories. It has zero dependencies on any external frameworks, including Semantic Kernel.

LLMGateway.Application: This layer orchestrates the application's use cases. Its key feature is the integration of Semantic Kernel.

Business logic is implemented within SK Plugins (e.g., ModelSelectionPlugin, CostTrackingPlugin).
A KernelOrchestrator configures and runs the Semantic Kernel pipeline, invoking the plugins as needed.
It uses a simplified CQRS (Command Query Responsibility Segregation) pattern. Commands (like sending a chat request) are handled by the orchestrator, while Queries (like fetching logs) can access the database more directly.
LLMGateway.Infrastructure: This layer contains the concrete implementations of the interfaces defined in the Domain layer.

It provides data access via EF Core and a PostgreSQL database.
It includes services that interact with external systems, such as the HTTP clients for calling different LLM providers.
LLMGateway.Api: This is the outermost layer, exposing the gateway's functionality via a web API. Its controllers are lightweight and delegate all the work to the KernelOrchestrator in the Application layer.

Finally, the LLMGateway.AppHost project uses .NET Aspire to manage and launch the different parts of the application, providing a unified dashboard for observability.

## Architecture

**Clean Architecture (Onion Pattern)** with Semantic Kernel as orchestration engine.

```
Domain → Application (SK Plugins) → Infrastructure (SK Services) → API
```

## Tech Stack

- **.NET 9** - Latest LTS framework
- **Semantic Kernel** - AI orchestration
- **PostgreSQL** - Request logging & cost tracking
- **EF Core** - ORM with repository pattern
- **Serilog** - Structured logging
- **Polly** - Resilience patterns
- **.NET Aspire** - Orchestration & observability

## Project Structure

```
src/
├── LLMGateway.Domain         # Entities, value objects, interfaces
├── LLMGateway.Application    # SK plugins, orchestration, DTOs
├── LLMGateway.Infrastructure # SK services, EF Core, repositories
├── LLMGateway.Api            # Controllers, middleware
└── LLMGateway.AppHost        # Aspire orchestration

tests/
├── LLMGateway.Domain.Tests
├── LLMGateway.Application.Tests
└── LLMGateway.Api.Tests
```

## Getting Started

### Prerequisites

- .NET 9 SDK
- Docker Desktop (for PostgreSQL)
- OpenRouter API key

### Quick Start

```bash
# Restore packages
dotnet restore

# Run with Aspire
dotnet run --project src/LLMGateway.AppHost

# Access Aspire dashboard
open http://localhost:15500
```

## Architecture Decisions

See [docs/adr](docs/adr/) for detailed architectural decision records.

## License

MIT
