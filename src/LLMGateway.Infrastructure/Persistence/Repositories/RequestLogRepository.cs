using LLMGateway.Domain.Entities;
using LLMGateway.Domain.Interfaces;
using LLMGateway.Domain.ValueObjects;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Infrastructure.Persistence.Repositories;

/// <summary>
/// EF Core implementation of IRequestLogRepository.
/// Persists request logs to PostgreSQL database.
/// </summary>
public class RequestLogRepository : IRequestLogRepository
{
    private readonly GatewayDbContext _context;

    public RequestLogRepository(GatewayDbContext context)
    {
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<RequestLog> SaveAsync(
        RequestLog log,
        CancellationToken cancellationToken = default)
    {
        if (log == null)
            throw new ArgumentNullException(nameof(log));

        await _context.RequestLogs.AddAsync(log, cancellationToken);
        await _context.SaveChangesAsync(cancellationToken);

        return log;
    }

    public async Task<IEnumerable<RequestLog>> GetRecentAsync(
        int count,
        CancellationToken cancellationToken = default)
    {
        if (count <= 0)
            throw new ArgumentException("Count must be positive", nameof(count));

        return await _context.RequestLogs
            .OrderByDescending(rl => rl.Timestamp)
            .Take(count)
            .ToListAsync(cancellationToken);
    }

    public async Task<CostAmount> GetTotalCostAsync(
        DateTime since,
        CancellationToken cancellationToken = default)
    {
        var totalCost = await _context.RequestLogs
            .Where(rl => rl.Timestamp >= since)
            .SumAsync(rl => rl.EstimatedCost.ValueUsd, cancellationToken);

        return CostAmount.FromUsd(totalCost);
    }
}