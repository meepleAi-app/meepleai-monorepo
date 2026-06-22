using System.Text.Json;
using Api.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.Infrastructure;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Caching.Hybrid;
using Microsoft.Extensions.DependencyInjection;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries.CostBreakdown;

/// <summary>
/// Unit tests for <see cref="GetCostBreakdownByFeatureQueryHandler"/>
/// (Issue #1838 SP5 F4-C5). Uses EF in-memory + real HybridCache to exercise
/// the per-feature aggregation + provider drill-down.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class GetCostBreakdownByFeatureQueryHandlerTests : IAsyncLifetime
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly GetCostBreakdownByFeatureQueryHandler _handler;

    public GetCostBreakdownByFeatureQueryHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        _cache = provider.GetRequiredService<HybridCache>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"BreakdownByFeatureTests_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        _handler = new GetCostBreakdownByFeatureQueryHandler(_context, _cache, TimeProvider.System);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Handle_AggregatesByFeatureAndDrillsDownByProvider()
    {
        var d = DateTime.UtcNow.AddDays(-2);

        // 80 USD AI Token Usage (60 OpenAI + 20 Anthropic) + 40 Infrastructure.
        await SeedExpenseAsync(d, 60m, LedgerCategory.TokenUsage, "openai/gpt-4o");
        await SeedExpenseAsync(d, 20m, LedgerCategory.TokenUsage, "anthropic/claude-sonnet-4-6");
        await SeedExpenseAsync(d, 40m, LedgerCategory.Infrastructure, modelId: null);

        var result = await _handler.Handle(
            new GetCostBreakdownByFeatureQuery(CostBreakdownRange.ThirtyDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(120m);

        // Features sorted DESC by total — AI Token Usage (80) > Infrastructure (40).
        result.Features.Select(f => f.Feature).Should().ContainInOrder(
            "AI Token Usage", "Infrastructure");

        var tokenUsageFeature = result.Features.First(f => f.Feature == "AI Token Usage");
        tokenUsageFeature.TotalCost.Should().Be(80m);
        tokenUsageFeature.PercentageOfTotal.Should().BeApproximately(66.67m, 0.01m);
        tokenUsageFeature.Providers.Should().HaveCount(2);
        tokenUsageFeature.Providers[0].Provider.Should().Be("OpenAI",
            "Provider drill must be sorted DESC by cost");
        tokenUsageFeature.Providers[0].Cost.Should().Be(60m);
        tokenUsageFeature.Providers[1].Provider.Should().Be("Anthropic");
        tokenUsageFeature.Providers[1].Cost.Should().Be(20m);

        var infraFeature = result.Features.First(f => f.Feature == "Infrastructure");
        infraFeature.TotalCost.Should().Be(40m);
        infraFeature.Providers.Should().HaveCount(1);
        infraFeature.Providers[0].Provider.Should().Be("Infrastructure");
    }

    [Fact]
    public async Task Handle_EmptyLedger_ReturnsEmptyAggregation()
    {
        var result = await _handler.Handle(
            new GetCostBreakdownByFeatureQuery(CostBreakdownRange.SevenDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(0m);
        result.Features.Should().BeEmpty();
        result.Range.Should().Be("7d");
    }

    [Fact]
    public async Task Handle_OnlyIncludesExpenseRows()
    {
        var d = DateTime.UtcNow.AddDays(-1);

        await SeedExpenseAsync(d, 25m, LedgerCategory.TokenUsage, "openai/gpt-4o");
        await SeedIncomeAsync(d, 9999m, LedgerCategory.Subscription);

        var result = await _handler.Handle(
            new GetCostBreakdownByFeatureQuery(CostBreakdownRange.ThirtyDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(25m);
        result.Features.Should().HaveCount(1);
        result.Features[0].Feature.Should().Be("AI Token Usage");
    }

    [Fact]
    public async Task Handle_ExcludesRowsOutsideRange()
    {
        await SeedExpenseAsync(DateTime.UtcNow.AddDays(-3), 5m, LedgerCategory.TokenUsage, "openai/gpt-4o");
        await SeedExpenseAsync(DateTime.UtcNow.AddDays(-100), 9999m, LedgerCategory.TokenUsage, "openai/gpt-4o");

        var result = await _handler.Handle(
            new GetCostBreakdownByFeatureQuery(CostBreakdownRange.SevenDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(5m);
    }

    [Fact]
    public async Task Handle_PercentageZero_WhenGrandTotalZero()
    {
        // Edge case: empty ledger, percentages should not crash with divide-by-zero.
        var result = await _handler.Handle(
            new GetCostBreakdownByFeatureQuery(CostBreakdownRange.NinetyDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(0m);
        result.Features.Should().BeEmpty();
    }

    private async Task SeedExpenseAsync(DateTime date, decimal amount, LedgerCategory cat, string? modelId)
    {
        var metadata = modelId is null ? null : JsonSerializer.Serialize(new { modelId });
        var entry = LedgerEntry.CreateAutoEntry(date, LedgerEntryType.Expense, cat, amount, "USD", description: null, metadata: metadata);
        _context.LedgerEntries.Add(entry);
        await _context.SaveChangesAsync();
    }

    private async Task SeedIncomeAsync(DateTime date, decimal amount, LedgerCategory cat)
    {
        var entry = LedgerEntry.CreateAutoEntry(date, LedgerEntryType.Income, cat, amount, "USD", description: null, metadata: null);
        _context.LedgerEntries.Add(entry);
        await _context.SaveChangesAsync();
    }
}
