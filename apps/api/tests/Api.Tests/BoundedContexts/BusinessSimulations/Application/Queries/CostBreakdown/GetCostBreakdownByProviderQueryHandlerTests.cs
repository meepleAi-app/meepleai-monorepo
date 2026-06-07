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
/// Unit tests for <see cref="GetCostBreakdownByProviderQueryHandler"/>
/// (Issue #1838 SP5 F4-C5). Uses EF in-memory + real HybridCache so we exercise
/// the aggregation logic and provider resolution against actual LedgerEntry rows.
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class GetCostBreakdownByProviderQueryHandlerTests : IAsyncLifetime
{
    private readonly MeepleAiDbContext _context;
    private readonly HybridCache _cache;
    private readonly GetCostBreakdownByProviderQueryHandler _handler;

    public GetCostBreakdownByProviderQueryHandlerTests()
    {
        var services = new ServiceCollection();
        services.AddHybridCache();
        var provider = services.BuildServiceProvider();
        _cache = provider.GetRequiredService<HybridCache>();

        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"BreakdownByProviderTests_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<Api.SharedKernel.Application.Services.IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        _handler = new GetCostBreakdownByProviderQueryHandler(_context, _cache, TimeProvider.System);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Handle_WithMixedProviderSpend_AggregatesByProviderAndDay()
    {
        var dayA = DateTime.UtcNow.AddDays(-3);
        var dayB = DateTime.UtcNow.AddDays(-1);

        // 2 OpenAI rows on dayA, 1 Anthropic row on dayA, 1 DeepSeek row on dayB,
        // 1 Infrastructure row on dayB.
        await SeedAsync(
            (dayA, 12m, LedgerCategory.TokenUsage, "openai/gpt-4o-mini"),
            (dayA, 8m, LedgerCategory.TokenUsage, "openai/gpt-4o"),
            (dayA, 4m, LedgerCategory.TokenUsage, "anthropic/claude-sonnet-4-6"),
            (dayB, 5m, LedgerCategory.TokenUsage, "deepseek/deepseek-chat"),
            (dayB, 40m, LedgerCategory.Infrastructure, null));

        var result = await _handler.Handle(
            new GetCostBreakdownByProviderQuery(CostBreakdownRange.SevenDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(69m);

        var providerTotalsByName = result.ProviderTotals.ToDictionary(p => p.Provider, p => p.TotalCost);
        providerTotalsByName.Should().ContainKey("OpenAI").WhoseValue.Should().Be(20m);
        providerTotalsByName.Should().ContainKey("Anthropic").WhoseValue.Should().Be(4m);
        providerTotalsByName.Should().ContainKey("DeepSeek").WhoseValue.Should().Be(5m);
        providerTotalsByName.Should().ContainKey("Infrastructure").WhoseValue.Should().Be(40m);

        // Provider totals sorted DESC.
        result.ProviderTotals.Select(p => p.TotalCost)
            .Should().BeInDescendingOrder();

        // Days sorted ASC.
        result.Days.Select(d => d.Date).Should().BeInAscendingOrder();
    }

    [Fact]
    public async Task Handle_OnlyIncludesExpenseRows()
    {
        var d = DateTime.UtcNow.AddDays(-1);

        await SeedExpenseAsync(d, 100m, LedgerCategory.TokenUsage, "openai/gpt-4o");
        await SeedIncomeAsync(d, 9999m, LedgerCategory.Subscription);

        var result = await _handler.Handle(
            new GetCostBreakdownByProviderQuery(CostBreakdownRange.ThirtyDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(100m);
        result.ProviderTotals.Should().HaveCount(1);
        result.ProviderTotals[0].Provider.Should().Be("OpenAI");
    }

    [Fact]
    public async Task Handle_ExcludesRowsOutsideRange()
    {
        var insideRange = DateTime.UtcNow.AddDays(-3);
        var outsideRange = DateTime.UtcNow.AddDays(-100);

        await SeedAsync(
            (insideRange, 10m, LedgerCategory.TokenUsage, "openai/gpt-4o"),
            (outsideRange, 9999m, LedgerCategory.TokenUsage, "openai/gpt-4o"));

        var result = await _handler.Handle(
            new GetCostBreakdownByProviderQuery(CostBreakdownRange.SevenDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_EmptyLedger_ReturnsEmptyAggregation()
    {
        var result = await _handler.Handle(
            new GetCostBreakdownByProviderQuery(CostBreakdownRange.SevenDays),
            CancellationToken.None);

        result.GrandTotal.Should().Be(0m);
        result.Days.Should().BeEmpty();
        result.ProviderTotals.Should().BeEmpty();
        result.Range.Should().Be("7d");
    }

    [Fact]
    public async Task Handle_RowWithUnknownProvider_FallsBackToUnknownLabel()
    {
        var d = DateTime.UtcNow.AddDays(-2);
        await SeedAsync(
            (d, 7m, LedgerCategory.TokenUsage, null), // no metadata at all
            (d, 3m, LedgerCategory.TokenUsage, "valid/model")); // valid for compare

        var result = await _handler.Handle(
            new GetCostBreakdownByProviderQuery(CostBreakdownRange.ThirtyDays),
            CancellationToken.None);

        result.ProviderTotals.Should().Contain(p => p.Provider == ProviderResolver.Unknown && p.TotalCost == 7m);
        result.ProviderTotals.Should().Contain(p => p.Provider == "Valid" && p.TotalCost == 3m);
    }

    private async Task SeedAsync(params (DateTime date, decimal amount, LedgerCategory cat, string? modelId)[] rows)
    {
        foreach (var (date, amount, cat, modelId) in rows)
        {
            await SeedExpenseAsync(date, amount, cat, modelId);
        }
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
