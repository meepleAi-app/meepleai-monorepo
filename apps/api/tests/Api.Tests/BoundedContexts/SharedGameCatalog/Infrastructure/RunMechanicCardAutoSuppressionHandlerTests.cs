using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.Infrastructure;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// La fixture di <see cref="RunMechanicCardAutoSuppressionHandlerTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Host condiviso per la classe, database fresco per test. Prima questa classe
/// costruiva l'host dentro il proprio <c>InitializeAsync</c>, che xUnit chiama una volta per
/// METODO: ~24s a test, contro ~0,1s per il clone del database.
/// </para>
/// </remarks>
public sealed class RunMechanicCardAutoSuppressionHandlerTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "me534_handler");

/// <summary>
/// Integration tests for the #534 auto-suppression batch command, sent through the real MediatR pipeline:
/// threshold breach → suppress + audit + counters; AND-condition; count-floor; kill-switch; config override.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class RunMechanicCardAutoSuppressionHandlerTests
    : IClassFixture<RunMechanicCardAutoSuppressionHandlerTestsHostFixture>, IAsyncLifetime
{
    private readonly RunMechanicCardAutoSuppressionHandlerTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public RunMechanicCardAutoSuppressionHandlerTests(RunMechanicCardAutoSuppressionHandlerTestsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _factory = _hostFixture.Factory;
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
    }

    // Host, client e database appartengono alla fixture.
    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private async Task<AutoSuppressionResult> RunAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        return await mediator.Send(new RunMechanicCardAutoSuppressionCommand());
    }

    [Fact]
    public async Task Breaching_Card_IsSuppressed_WithAudit_AndCounters()
    {
        Guid cardId;
        using (var scope = _factory.Services.CreateScope())
        {
            cardId = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 5, positives: 0);
        }

        var result = await RunAsync();

        result.Suppressed.Should().Be(1);
        using var read = _factory.Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var card = await db.MechanicCards.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == cardId);
        card.IsSuppressed.Should().BeTrue();
        card.SuppressedReason.Should().Contain("auto_feedback");
        card.ErrorReportsCount.Should().Be(5);
        (await db.MechanicCardAuditLog.CountAsync(a => a.CardId == cardId)).Should().Be(1);
    }

    [Fact]
    public async Task TwoBreachingCards_BothSuppressed_AcrossLoopIterations()
    {
        Guid cardA, cardB;
        using (var scope = _factory.Services.CreateScope())
        {
            cardA = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 5, positives: 0);
            cardB = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 6, positives: 1);
        }

        var result = await RunAsync();

        result.Suppressed.Should().Be(2);
        using var read = _factory.Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var suppressed = await db.MechanicCards.IgnoreQueryFilters().AsNoTracking()
            .Where(c => c.Id == cardA || c.Id == cardB).ToListAsync();
        suppressed.Should().OnlyContain(c => c.IsSuppressed);
        // Each suppression writes exactly one audit row — no cross-iteration event/tracker leakage.
        (await db.MechanicCardAuditLog.CountAsync(a => a.CardId == cardA || a.CardId == cardB)).Should().Be(2);
    }

    [Fact]
    public async Task HighScore_Card_IsNotSuppressed_ButCountersUpdated()
    {
        Guid cardId;
        using (var scope = _factory.Services.CreateScope())
        {
            // 5 negatives / 10 positives → score 0.667 ≥ 0.5 threshold → AND fails.
            cardId = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 5, positives: 10);
        }

        var result = await RunAsync();

        result.Suppressed.Should().Be(0);
        using var read = _factory.Services.CreateScope();
        var db = read.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var card = await db.MechanicCards.IgnoreQueryFilters().AsNoTracking().SingleAsync(c => c.Id == cardId);
        card.IsSuppressed.Should().BeFalse();
        card.ErrorReportsCount.Should().Be(5);
    }

    [Fact]
    public async Task BelowCountThreshold_IsNotSuppressed()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 4, positives: 0); // 4 < 5
        }

        (await RunAsync()).Suppressed.Should().Be(0);
    }

    [Fact]
    public async Task KillSwitchDisabled_SuppressesNothing()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 5, positives: 0);
            await MechanicCardAutoSuppressionSeed.SetConfigAsync(db, "MechanicCard:AutoSuppressionEnabled", "false", "bool", _userId);
        }

        (await RunAsync()).Should().BeEquivalentTo(new AutoSuppressionResult(0, 0));
    }

    [Fact]
    public async Task ConfigOverride_LowersThreshold()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 3, positives: 0);
            await MechanicCardAutoSuppressionSeed.SetConfigAsync(db, "MechanicCard:ErrorReportsThreshold", "3", "int", _userId);
        }

        (await RunAsync()).Suppressed.Should().Be(1);
    }
}
