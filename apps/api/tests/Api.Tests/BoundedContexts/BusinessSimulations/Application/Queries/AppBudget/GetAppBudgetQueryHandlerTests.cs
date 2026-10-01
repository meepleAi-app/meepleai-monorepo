using Api.BoundedContexts.BusinessSimulations.Application.Queries.AppBudget;
using Api.BoundedContexts.BusinessSimulations.Domain.Entities;
using Api.BoundedContexts.BusinessSimulations.Domain.Enums;
using Api.BoundedContexts.BusinessSimulations.Domain.Repositories;
using Api.BoundedContexts.BusinessSimulations.Domain.ValueObjects;
using Api.BoundedContexts.BusinessSimulations.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using FluentAssertions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Moq;
using Xunit;
using AppBudgetAggregate = Api.BoundedContexts.BusinessSimulations.Domain.Aggregates.AppBudgets.AppBudget;

namespace Api.Tests.BoundedContexts.BusinessSimulations.Application.Queries.AppBudget;

/// <summary>
/// Handler-driven unit tests for <see cref="GetAppBudgetQueryHandler"/>
/// (Issue #1838 SP5 F4-C5).
///
/// <para>Uses EF in-memory + real <see cref="AppBudgetRepository"/> so the
/// query exercises the full pipeline (repo lookup + LedgerEntry aggregation)
/// rather than mocking the spend computation. This pattern follows the
/// MEMORY.md "feedback_acceptance_tests_must_exercise_real_pipeline" lesson
/// from PR #1555.</para>
/// </summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "BusinessSimulations")]
[Trait("Issue", "1838")]
public sealed class GetAppBudgetQueryHandlerTests : IAsyncLifetime
{
    private readonly MeepleAiDbContext _context;
    private readonly AppBudgetRepository _repository;
    private readonly GetAppBudgetQueryHandler _handler;
    private readonly FakeTimeProvider _timeProvider;

    /// <summary>Anchor date for "today" — picked to land mid-month with at
    /// least 5 days remaining so the projection math is exercised.
    /// Chosen relative to a recent real date so LedgerEntry's hard guard
    /// against future-dated rows doesn't fire during seed.</summary>
    private static readonly DateTime AnchorToday = new DateTime(2026, 6, 1, 15, 0, 0, DateTimeKind.Utc);

    public GetAppBudgetQueryHandlerTests()
    {
        var options = new DbContextOptionsBuilder<MeepleAiDbContext>()
            .UseInMemoryDatabase($"GetAppBudgetTests_{Guid.NewGuid()}")
            .Options;

        var mockMediator = new Mock<IMediator>();
        var mockEventCollector = new Mock<IDomainEventCollector>();
        mockEventCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _context = new MeepleAiDbContext(options, mockMediator.Object, mockEventCollector.Object);
        _repository = new AppBudgetRepository(_context, new DomainEventCollector());

        // Anchor the fake clock so the spend window covers a deterministic
        // calendar month. Tests can override via _timeProvider.SetNow.
        _timeProvider = new FakeTimeProvider(AnchorToday);
        _handler = new GetAppBudgetQueryHandler(_repository, _context, _timeProvider);
    }

    public ValueTask InitializeAsync() => ValueTask.CompletedTask;

    public ValueTask DisposeAsync()
    {
        _context.Database.EnsureDeleted();
        _context.Dispose();
        return ValueTask.CompletedTask;
    }

    [Fact]
    public async Task Handle_NoBudgetConfigured_ReturnsNull()
    {
        var result = await _handler.Handle(new GetAppBudgetQuery(), CancellationToken.None);
        result.Should().BeNull("First-visit empty state");
    }

    [Fact]
    public async Task Handle_BudgetConfigured_ReturnsConfigPlusZeroSpendWhenNoLedger()
    {
        var budget = AppBudgetAggregate.Create(Money.Create(1200m, "USD"), 80, 95, "admin");
        await _repository.UpsertAsync(budget, CancellationToken.None);

        var result = await _handler.Handle(new GetAppBudgetQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.MonthlyLimit.Should().Be(1200m);
        result.Currency.Should().Be("USD");
        result.AlertThresholdPct.Should().Be(80);
        result.CriticalThresholdPct.Should().Be(95);
        result.IsEnabled.Should().BeTrue();
        result.Spent.Today.Should().Be(0m);
        result.Spent.ThisMonth.Should().Be(0m);
        result.Spent.ProjectedMonthEnd.Should().Be(0m);
        // AnchorToday = 2026-06-01 → 30 - 1 = 29 days remaining in June.
        result.DaysRemaining.Should().Be(29);
        // In-memory provider doesn't populate the Postgres xmin system column;
        // the DTO surfaces whatever the repo carried (0 here). Real xmin
        // round-tripping is covered by the Testcontainers integration suite.
        result.Xmin.Should().Be(0u);
    }

    [Fact]
    public async Task Handle_ComputesSpendBreakdownAndProjection()
    {
        // Ancora FISSA con giorno > 1, e non `DateTime.UtcNow`: il test distingue «oggi» da
        // «prima di oggi ma in questo mese», e il giorno 1 non ammette quella distinzione —
        // qualunque istante «prima di oggi» cade nel mese precedente. Il ramo condizionale
        // che c'era prima provava a cavarsela con `todayMorning.AddHours(-1)`, che e' ancora
        // oggi: il primo di ogni mese i 80 finivano in `Today` e l'assert cadeva. Osservato
        // il 2026-10-01, con `Spent.Today` a 92.40 invece di 12.40.
        //
        // La data resta nel passato reale per sempre, quindi la guardia di LedgerEntry
        // (`date > DateTime.UtcNow.AddDays(1)`, che legge l'orologio di sistema e non il
        // TimeProvider) la accetta e non invecchia. `AnchorToday` della classe non e'
        // riusabile qui: e' il 2026-06-01, cioe' proprio un giorno 1.
        var anchor = new DateTime(2026, 6, 15, 12, 0, 0, DateTimeKind.Utc);
        _timeProvider.SetNow(anchor);

        var budget = AppBudgetAggregate.Create(Money.Create(1200m, "USD"), 80, 95, "admin");
        await _repository.UpsertAsync(budget, CancellationToken.None);

        // Tre righe di spesa: una oggi, una prima in questo mese, una nel mese precedente
        // (deve essere esclusa).
        var todayMorning = new DateTime(anchor.Year, anchor.Month, anchor.Day, 6, 0, 0, DateTimeKind.Utc);
        var earlierThisMonth = new DateTime(anchor.Year, anchor.Month, 1, 12, 0, 0, DateTimeKind.Utc);
        var lastMonth = anchor.AddMonths(-1);

        await SeedExpenseAsync(todayMorning, 12.40m, LedgerCategory.TokenUsage);
        await SeedExpenseAsync(earlierThisMonth, 80m, LedgerCategory.TokenUsage);
        await SeedExpenseAsync(lastMonth, 9999m, LedgerCategory.TokenUsage);

        var result = await _handler.Handle(new GetAppBudgetQuery(), CancellationToken.None);

        result.Should().NotBeNull();
        result!.Spent.Today.Should().Be(12.40m, "solo la riga di stamattina cade oggi");

        // Riga del mese precedente esclusa → oggi + prima-in-questo-mese = 92.40
        result.Spent.ThisMonth.Should().Be(92.40m, "la riga di maggio e' fuori dal mese corrente");

        // Projection = thisMonth / dayOfMonth * monthLength (round HalfUp 2dp):
        // 92.40 / 15 * 30 = 184.80
        var monthLength = DateTime.DaysInMonth(anchor.Year, anchor.Month);
        var expectedProjection = Math.Round(
            92.40m / anchor.Day * monthLength,
            2,
            MidpointRounding.AwayFromZero);
        result.Spent.ProjectedMonthEnd.Should().Be(expectedProjection);
    }

    [Fact]
    public async Task Handle_ExcludesIncomeRowsFromSpendComputation()
    {
        var realNow = DateTime.UtcNow;
        var anchor = new DateTime(realNow.Year, realNow.Month, Math.Min(realNow.Day, 28), 12, 0, 0, DateTimeKind.Utc);
        _timeProvider.SetNow(anchor);

        var budget = AppBudgetAggregate.Create(Money.Create(1200m, "USD"), 80, 95, "admin");
        await _repository.UpsertAsync(budget, CancellationToken.None);

        var todayMorning = new DateTime(anchor.Year, anchor.Month, anchor.Day, 6, 0, 0, DateTimeKind.Utc);

        // Income rows must NOT inflate spend.
        var income = LedgerEntry.CreateAutoEntry(todayMorning, LedgerEntryType.Income, LedgerCategory.Subscription, 5000m, "USD");
        _context.LedgerEntries.Add(income);
        await SeedExpenseAsync(todayMorning, 10m, LedgerCategory.TokenUsage);

        var result = await _handler.Handle(new GetAppBudgetQuery(), CancellationToken.None);

        result!.Spent.Today.Should().Be(10m);
        result.Spent.ThisMonth.Should().Be(10m);
    }

    [Fact]
    public async Task Handle_DaysRemainingClampedToZero_OnLastDayOfMonth()
    {
        _timeProvider.SetNow(new DateTime(2026, 6, 30, 23, 0, 0, DateTimeKind.Utc));

        var budget = AppBudgetAggregate.Create(Money.Create(1200m, "USD"), 80, 95, "admin");
        await _repository.UpsertAsync(budget, CancellationToken.None);

        var result = await _handler.Handle(new GetAppBudgetQuery(), CancellationToken.None);

        result!.DaysRemaining.Should().Be(0);
    }

    private async Task SeedExpenseAsync(DateTime date, decimal amount, LedgerCategory cat)
    {
        var entry = LedgerEntry.CreateAutoEntry(date, LedgerEntryType.Expense, cat, amount, "USD");
        _context.LedgerEntries.Add(entry);
        await _context.SaveChangesAsync();
    }

    /// <summary>Minimal TimeProvider stub for predictable test clocks.</summary>
    private sealed class FakeTimeProvider : TimeProvider
    {
        private DateTimeOffset _now;
        public FakeTimeProvider(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public void SetNow(DateTime utcNow) => _now = new DateTimeOffset(utcNow, TimeSpan.Zero);
        public override DateTimeOffset GetUtcNow() => _now;
    }
}
