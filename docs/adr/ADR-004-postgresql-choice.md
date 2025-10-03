# ADR-004: PostgreSQL Over SQL Server

**Status:** Accepted  
**Date:** 2025-10-01  
**Decision Makers:** Development Team  
**Tags:** infrastructure, database, cost

## Context

The LLM Gateway requires a relational database for storing request logs, cost tracking, and configuration. We must choose between PostgreSQL and SQL Server for both development and production deployment.

## Decision

We will use **PostgreSQL** as the primary database for all environments (development, staging, production).

## Rationale

### Cost Considerations

**PostgreSQL:**
- **Open source** and free in all environments
- Azure Database for PostgreSQL: Free tier available (up to 750 hours/month)
- Flexible Server: $13/month for basic production tier
- No licensing costs for self-hosted

**SQL Server:**
- **Developer Edition**: Free for development but cannot be used in production
- **Express Edition**: Free but limited to 10GB database size
- **Azure SQL Database**: Starts at ~$5/month (Basic tier) but limited performance
- **Standard/Enterprise**: Expensive licensing ($931+/year for Standard)

**Verdict:** PostgreSQL eliminates licensing concerns and provides better cost predictability.

### Performance Characteristics

**For LLM Gateway Use Case:**

**Write-heavy workload** (request logs):
- PostgreSQL: Excellent MVCC (multi-version concurrency control) for high insert throughput
- SQL Server: Good performance but more locking overhead

**Simple queries** (retrieve recent logs, health checks):
- Both perform well for simple SELECT queries
- PostgreSQL's query planner is highly optimized

**JSON support** (future: storing prompt/response as JSON):
- PostgreSQL: Native JSONB type with indexing, operators, and functions
- SQL Server: JSON support via nvarchar, less performant

**Indexing:**
- Both support standard B-tree indexes
- PostgreSQL offers more index types (GiST, GIN, BRIN) for advanced use cases

**Verdict:** PostgreSQL's MVCC and JSONB support align better with our write-heavy, semi-structured data needs.

### Docker/Aspire Compatibility

**.NET Aspire Integration:**
- PostgreSQL: First-class support via `Aspire.Hosting.PostgreSQL` package
- SQL Server: Also supported but PostgreSQL is more commonly used in Aspire examples

**Docker:**
- PostgreSQL: Official `postgres` image, 150MB compressed
- SQL Server: Official `mcr.microsoft.com/mssql/server` image, 1.5GB compressed

**Local Development:**
- PostgreSQL: Starts in ~2 seconds, minimal resource usage
- SQL Server: Slower startup, requires more memory (2GB minimum)

**Verdict:** PostgreSQL's smaller footprint and faster startup improve developer experience.

### .NET Ecosystem Support

**Entity Framework Core:**
- PostgreSQL: Excellent support via `Npgsql.EntityFrameworkCore.PostgreSQL`
- SQL Server: Native support, slightly better tooling in Visual Studio

**Dapper:**
- Both have equivalent support

**Migration Tools:**
- Both support EF Core migrations
- PostgreSQL has additional options (Flyway, Liquibase)

**Verdict:** EF Core support is equivalent; no significant difference.

### Cloud Deployment

**Azure:**
- **Azure Database for PostgreSQL Flexible Server**: Modern, feature-rich
  - Zone-redundant HA available
  - Point-in-time restore (35 days)
  - Automatic backups
- **Azure SQL Database**: Also feature-rich but more expensive

**AWS (future consideration):**
- PostgreSQL: Amazon RDS or Aurora PostgreSQL
- SQL Server: Amazon RDS, but less common and more expensive

**Google Cloud (future consideration):**
- PostgreSQL: Cloud SQL for PostgreSQL (native)
- SQL Server: Cloud SQL for SQL Server (supported but less common)

**Verdict:** PostgreSQL offers better multi-cloud portability and cost.

### Developer Experience

**Tooling:**
- PostgreSQL: pgAdmin, DBeaver, TablePlus, psql CLI
- SQL Server: SSMS (Windows only), Azure Data Studio (cross-platform)

**Both have:**
- Good VS Code extensions
- EF Core designer support
- Database migration tools

**Familiarity:**
- If team knows SQL Server better, learning curve is minimal
- SQL dialects are 90% similar for basic operations

**Verdict:** Slight preference for PostgreSQL due to cross-platform tooling.

## Consequences

### Positive

- **Zero licensing costs** in all environments
- **Better Aspire/Docker experience** with smaller image size
- **Multi-cloud portability** (easier to migrate between providers)
- **JSONB support** future-proofs for semi-structured data
- **Open source** ecosystem with active community

### Negative

- **Learning curve** if team is SQL Server-centric (minimal impact)
- **No SSMS** for Windows-native tooling (mitigated by Azure Data Studio, pgAdmin)
- **Case sensitivity** in identifiers (PostgreSQL convention is snake_case)

### Mitigation

- Use **EF Core abstractions** to minimize database-specific code
- Follow **snake_case naming convention** for PostgreSQL tables/columns
- Document PostgreSQL-specific patterns (JSONB queries, array types) in README
- Provide docker-compose setup for local development

## Implementation Notes

### Connection String Format
```json
{
  "ConnectionStrings": {
    "PostgreSQL": "Host=localhost;Database=llmgateway;Username=postgres;Password=postgres"
  }
}
```

### Naming Conventions
Follow PostgreSQL conventions:
- Tables: `request_logs` (lowercase, snake_case)
- Columns: `model_used`, `estimated_cost_usd` (lowercase, snake_case)
- Indexes: `idx_request_logs_timestamp`

### EF Core Configuration
```csharp
builder.Services.AddDbContext<GatewayDbContext>(options =>
    options.UseNpgsql(
        builder.Configuration.GetConnectionString("PostgreSQL")
    ));
```

### Aspire Configuration
```csharp
var postgres = builder.AddPostgres("postgres")
    .WithPgAdmin()
    .AddDatabase("llmgateway");

var api = builder.AddProject<Projects.LLMGateway_Api>("api")
    .WithReference(postgres);
```

## Alternatives Considered

### 1. SQL Server
**Rejected due to:**
- Licensing costs for production
- Larger Docker image size
- Less cost-effective on Azure free tier

**Would reconsider if:**
- Team has deep SQL Server expertise and no PostgreSQL experience
- Using SQL Server-specific features (spatial data, Always Encrypted)
- Already standardized on SQL Server in organization

### 2. MySQL/MariaDB
**Not chosen because:**
- Less feature-rich than PostgreSQL (no JSONB, fewer index types)
- Oracle ownership concerns (MySQL)
- PostgreSQL has better .NET ecosystem support

### 3. NoSQL (MongoDB, CosmosDB)
**Rejected because:**
- Overkill for simple relational data (request logs)
- SQL queries are simpler for time-series data
- Cost tracking requires aggregations (better in SQL)

**Would reconsider if:**
- Storing complex, deeply nested prompt/response structures
- Schema flexibility is critical
- Document-oriented queries dominate

### 4. SQLite
**Rejected for production because:**
- Not suitable for concurrent writes (single writer lock)
- No built-in replication or backup
- Poor fit for multi-instance deployments

**Could use for:**
- Local development without Docker
- Embedded scenarios (desktop app version)

## Future Considerations

### Phase 3+: Consider Specialized Databases

**Time-series data (request logs):**
- **TimescaleDB** (PostgreSQL extension) for high-volume time-series
- Partitioning by time range for faster queries

**Analytics/Reporting:**
- Replicate logs to **ClickHouse** or **DuckDB** for OLAP queries
- Keep PostgreSQL for transactional workload

**Caching:**
- Add **Redis** for frequently accessed data (model pricing, configuration)
- Keep PostgreSQL as source of truth

## Migration Path

If we need to switch databases later:

**To SQL Server:**
- EF Core migrations are database-agnostic
- Change provider: `UseNpgsql()` → `UseSqlServer()`
- Update snake_case to PascalCase naming

**To NoSQL:**
- Requires application-level changes (repository implementations)
- Keep PostgreSQL for transactional data, add NoSQL for documents

## Related Decisions

- **ADR-001**: PostgreSQL chosen as Infrastructure layer dependency
- **ADR-006** (future): May add Redis caching layer in Phase 3

## References

- Azure Database for PostgreSQL pricing: https://azure.microsoft.com/en-us/pricing/details/postgresql/
- PostgreSQL vs SQL Server comparison: https://www.postgresql.org/about/featurematrix/
- .NET Aspire PostgreSQL integration: https://learn.microsoft.com/en-us/dotnet/aspire/database/postgresql-component