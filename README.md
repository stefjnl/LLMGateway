# LLM Gateway

Enterprise LLM Gateway with Semantic Kernel orchestration - demonstrating Clean Architecture in .NET 9.

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

## Roadmap

- [x] US-001: Project initialization
- [ ] US-002: Domain layer implementation
- [ ] US-003: Application layer (SK plugins)
- [ ] US-004: Infrastructure layer (SK services + EF Core)
- [ ] US-005: API layer + endpoints
- [ ] US-006: End-to-end testing

## Architecture Decisions

See [docs/adr](docs/adr/) for detailed architectural decision records.

## License

MIT