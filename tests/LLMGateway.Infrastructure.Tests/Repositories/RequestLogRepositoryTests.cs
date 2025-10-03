using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;
using LLMGateway.Infrastructure.Persistence;
using LLMGateway.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Infrastructure.Tests.Repositories;

public class RequestLogRepositoryTests : IDisposable
{
    private readonly GatewayDbContext _context;
    private readonly RequestLogRepository _repository;

    public RequestLogRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GatewayDbContext(options);
        _repository = new RequestLogRepository(_context);
    }

    [Fact]
    public async Task SaveAsync_SavesRequestLogWithValueObjects()
    {
        // Arrange
        var model = ModelName.From("test-model");
        var inputTokens = TokenCount.From(10);
        var outputTokens = TokenCount.From(5);
        var cost = CostAmount.FromUsd(0.001m);
        var responseTime = TimeSpan.FromMilliseconds(500);

        var requestLog = RequestLog.Create(
            model,
            inputTokens,
            outputTokens,
            cost,
            "TestProvider",
            responseTime,
            wasFallback: true);

        // Act
        var savedLog = await _repository.SaveAsync(requestLog);

        // Assert
        savedLog.Should().NotBeNull();
        savedLog.Id.Should().NotBeEmpty();
        savedLog.ModelUsed.Should().Be(model);
        savedLog.InputTokens.Should().Be(inputTokens);
        savedLog.OutputTokens.Should().Be(outputTokens);
        savedLog.EstimatedCost.Should().Be(cost);
        savedLog.ProviderName.Should().Be("TestProvider");
        savedLog.ResponseTime.Should().Be(responseTime);
        savedLog.WasFallback.Should().BeTrue();
    }

    [Fact]
    public async Task GetRecentAsync_ReturnsOrderedByTimestampDescending()
    {
        // Arrange
        var model = ModelName.From("test-model");
        var inputTokens = TokenCount.From(5);
        var outputTokens = TokenCount.From(3);
        var cost = CostAmount.FromUsd(0.0005m);
        var responseTime = TimeSpan.FromMilliseconds(200);

        // Create logs with different timestamps
        var log1 = RequestLog.Create(model, inputTokens, outputTokens, cost, "Provider", responseTime);
        var log2 = RequestLog.Create(model, inputTokens, outputTokens, cost, "Provider", responseTime);
        var log3 = RequestLog.Create(model, inputTokens, outputTokens, cost, "Provider", responseTime);

        await _repository.SaveAsync(log1);
        await Task.Delay(10); // Ensure different timestamps
        await _repository.SaveAsync(log2);
        await Task.Delay(10);
        await _repository.SaveAsync(log3);

        // Act
        var recentLogs = await _repository.GetRecentAsync(2);

        // Assert
        recentLogs.Should().HaveCount(2);
        recentLogs.First().Id.Should().Be(log3.Id);
        recentLogs.Last().Id.Should().Be(log2.Id);
    }

    [Fact]
    public async Task GetTotalCostAsync_CalculatesSumForDateRange()
    {
        // Arrange
        var model = ModelName.From("test-model");
        var inputTokens = TokenCount.From(10);
        var outputTokens = TokenCount.From(5);
        var responseTime = TimeSpan.FromMilliseconds(200);

        var oldDate = DateTime.UtcNow.AddDays(-2);
        var recentDate = DateTime.UtcNow.AddDays(-1);

        // Create logs with different dates
        var oldLog = CreateLogWithSpecificTimestamp(model, inputTokens, outputTokens,
            CostAmount.FromUsd(0.001m), responseTime, oldDate);
        var recentLog1 = RequestLog.Create(model, inputTokens, outputTokens,
            CostAmount.FromUsd(0.002m), "Provider", responseTime);
        var recentLog2 = RequestLog.Create(model, inputTokens, outputTokens,
            CostAmount.FromUsd(0.003m), "Provider", responseTime);

        await _context.RequestLogs.AddAsync(oldLog);
        await _repository.SaveAsync(recentLog1);
        await _repository.SaveAsync(recentLog2);
        await _context.SaveChangesAsync();

        // Act
        var totalCost = await _repository.GetTotalCostAsync(recentDate);

        // Assert
        totalCost.ValueUsd.Should().Be(0.005m); // 0.002m + 0.003m
    }

    private RequestLog CreateLogWithSpecificTimestamp(
        ModelName model,
        TokenCount inputTokens,
        TokenCount outputTokens,
        CostAmount cost,
        TimeSpan responseTime,
        DateTime timestamp)
    {
        var log = RequestLog.Create(model, inputTokens, outputTokens, cost, "Provider", responseTime);
        // Use reflection to set the private Timestamp property for testing
        typeof(RequestLog).GetProperty("Timestamp")!.SetValue(log, timestamp);
        return log;
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}