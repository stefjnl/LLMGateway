using LLMGateway.Domain.Entities;
using LLMGateway.Domain.ValueObjects;
using LLMGateway.Infrastructure.Persistence;
using LLMGateway.Infrastructure.Persistence.Repositories;
using Microsoft.EntityFrameworkCore;

namespace LLMGateway.Infrastructure.Tests.Repositories;

public class ModelPricingRepositoryTests : IDisposable
{
    private readonly GatewayDbContext _context;
    private readonly ModelPricingRepository _repository;

    public ModelPricingRepositoryTests()
    {
        var options = new DbContextOptionsBuilder<GatewayDbContext>()
            .UseInMemoryDatabase(databaseName: Guid.NewGuid().ToString())
            .Options;

        _context = new GatewayDbContext(options);
        _repository = new ModelPricingRepository(_context);
    }

    [Fact]
    public async Task GetByModelAsync_ReturnsMatchingModelPricing()
    {
        // Arrange
        var model = ModelName.From("test-model");
        var pricing = ModelPricing.Create(
            model,
            "TestProvider",
            0.0001m,
            0.0002m,
            128000);

        await _context.ModelPricings.AddAsync(pricing);
        await _context.SaveChangesAsync();

        // Act
        var result = await _repository.GetByModelAsync(model);

        // Assert
        result.Should().NotBeNull();
        result!.Model.Should().Be(model);
        result.ProviderName.Should().Be("TestProvider");
        result.InputCostPer1MTokens.Should().Be(0.0001m);
        result.OutputCostPer1MTokens.Should().Be(0.0002m);
        result.MaxContextTokens.Should().Be(128000);
    }

    [Fact]
    public async Task GetByModelAsync_ReturnsNullForNonExistentModel()
    {
        // Arrange
        var model = ModelName.From("non-existent-model");

        // Act
        var result = await _repository.GetByModelAsync(model);

        // Assert
        result.Should().BeNull();
    }

    [Fact]
    public async Task GetAllAsync_ReturnsAllModelPricingsOrderedByModelName()
    {
        // Arrange
        var model1 = ModelName.From("model-a");
        var model2 = ModelName.From("model-b");
        var model3 = ModelName.From("model-c");

        var pricing1 = ModelPricing.Create(model1, "Provider", 0.0001m, 0.0002m, 1000);
        var pricing2 = ModelPricing.Create(model2, "Provider", 0.0001m, 0.0002m, 1000);
        var pricing3 = ModelPricing.Create(model3, "Provider", 0.0001m, 0.0002m, 1000);

        // Add in reverse order to test sorting
        await _context.ModelPricings.AddAsync(pricing3);
        await _context.ModelPricings.AddAsync(pricing1);
        await _context.ModelPricings.AddAsync(pricing2);
        await _context.SaveChangesAsync();

        // Act
        var results = await _repository.GetAllAsync();

        // Assert
        results.Should().HaveCount(3);
        results.Select(p => p.Model.Value).Should().BeInAscendingOrder();
        results.First().Model.Value.Should().Be("model-a");
        results.Last().Model.Value.Should().Be("model-c");
    }

    public void Dispose()
    {
        _context.Dispose();
    }
}