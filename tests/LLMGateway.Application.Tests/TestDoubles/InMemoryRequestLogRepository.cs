using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;

namespace LLMGateway.Application.Tests.TestDoubles;

public class InMemoryRequestLogRepository : IRequestLogRepository
{
    private readonly List<RequestLog> _logs = new();

    public Task<RequestLog> SaveAsync(
        RequestLog log,
        CancellationToken cancellationToken = default)
    {
        _logs.Add(log);
        return Task.FromResult(log);
    }

    public Task<IEnumerable<RequestLog>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        return Task.FromResult(
            _logs.OrderByDescending(x => x.Timestamp)
                 .Take(count)
                 .AsEnumerable());
    }

    public Task<CostAmount> GetTotalCostAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        var total = _logs
            .Where(x => x.Timestamp >= since)
            .Select(x => x.EstimatedCost)
            .Aggregate(CostAmount.Zero, (acc, cost) => acc.Add(cost));

        return Task.FromResult(total);
    }
}