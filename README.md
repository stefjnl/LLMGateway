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

# Run API server
dotnet run --project src/LLMGateway.Api

# Access Swagger UI
open http://localhost:5047/swagger

# Or run with Aspire (includes database)
dotnet run --project src/LLMGateway.AppHost

# Access Aspire dashboard
open http://localhost:15500
```

## API Documentation

### Endpoints

#### POST /v1/ChatCompletion
Send a chat completion request to the LLM Gateway.

**Request Body:**
```json
{
  "messages": [
    {
      "role": "user",
      "content": "Hello, world!"
    }
  ],
  "model": "optional-model-name",
  "temperature": 0.7,
  "maxTokens": 100
}
```

**Response:**
```json
{
  "content": "Hello! How can I help you today?",
  "model": "z-ai/glm-4.6",
  "tokensUsed": 25,
  "estimatedCostUsd": 0.000001,
  "responseTime": "00:00:01.234"
}
```

**Error Response:**
```json
{
  "type": "https://tools.ietf.org/html/rfc7231#section-6.5.1",
  "title": "Bad Request",
  "status": 400,
  "detail": "The Messages field is required.",
  "correlationId": "abc-123-def-456"
}
```

#### GET /health
Basic liveness probe.

**Response:** `Healthy`

#### GET /health/ready
Readiness probe checking database and external services.

**Response:** `Healthy` or `Unhealthy` with detailed status.

### Configuration

Set your OpenRouter API key:

```bash
# Using User Secrets (recommended)
dotnet user-secrets set "OpenRouter:ApiKey" "sk-or-v1-..."

# Or using environment variables
export OpenRouter__ApiKey="sk-or-v1-..."
```

### Health Checks

The API includes comprehensive health checks:
- **Database**: PostgreSQL connectivity
- **OpenRouter**: Provider availability
- **Readiness**: All dependencies healthy

## Architecture Decisions

See [docs/adr](docs/adr/) for detailed architectural decision records.

## License

MIT
