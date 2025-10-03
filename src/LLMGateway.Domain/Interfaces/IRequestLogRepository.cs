using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Domain.Interfaces;

public interface IRequestLogRepository
{
    Task<RequestLog> SaveAsync(
        RequestLog log,
        CancellationToken cancellationToken = default);

    Task<IEnumerable<RequestLog>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default);

    Task<CostAmount> GetTotalCostAsync(
        DateTime since,
        CancellationToken cancellationToken = default);
}