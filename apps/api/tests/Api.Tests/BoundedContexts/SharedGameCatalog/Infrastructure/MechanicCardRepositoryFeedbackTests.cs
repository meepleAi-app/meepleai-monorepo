using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Infrastructure;

/// <summary>
/// La fixture di <see cref="MechanicCardRepositoryFeedbackTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Host condiviso per la classe, database fresco per test. Prima questa classe
/// costruiva l'host dentro il proprio <c>InitializeAsync</c>, che xUnit chiama una volta per
/// METODO: ~24s a test, contro ~0,1s per il clone del database.
/// </para>
/// </remarks>
public sealed class MechanicCardRepositoryFeedbackTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "me534_repo");

/// <summary>
/// Integration tests for the #534 repository additions: the active-card feedback aggregate query
/// (excludes suppressed cards) and the tracked <see cref="IMechanicCardRepository.Update"/> path.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardRepositoryFeedbackTests
    : IClassFixture<MechanicCardRepositoryFeedbackTestsHostFixture>, IAsyncLifetime
{
    private readonly MechanicCardRepositoryFeedbackTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public MechanicCardRepositoryFeedbackTests(MechanicCardRepositoryFeedbackTestsHostFixture hostFixture)
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

    [Fact]
    public async Task GetActiveCardFeedbackAggregates_CountsPosNeg_AndExcludesSuppressed()
    {
        Guid activeCard, suppressedCard;
        using (var scope = _factory.Services.CreateScope())
        {
            activeCard = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 3, positives: 2);
            suppressedCard = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 4, positives: 0, isSuppressed: true);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IMechanicCardRepository>();
            var aggs = await repo.GetActiveCardFeedbackAggregatesAsync();

            var active = aggs.Should().ContainSingle(a => a.CardId == activeCard).Which;
            active.NegativeCount.Should().Be(3);
            active.PositiveCount.Should().Be(2);
            aggs.Should().NotContain(a => a.CardId == suppressedCard);
        }
    }

    [Fact]
    public async Task Update_PersistsMutatedAggregates()
    {
        Guid cardId;
        using (var scope = _factory.Services.CreateScope())
        {
            cardId = await MechanicCardAutoSuppressionSeed.CardWithFeedbackAsync(scope, _userId, negatives: 0, positives: 0);
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<IMechanicCardRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();
            var card = await repo.GetByIdIgnoringFiltersAsync(cardId);
            card!.ApplyFeedbackAggregates(7, 0.30m, DateTime.UtcNow);
            repo.Update(card);
            await uow.SaveChangesAsync();
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.MechanicCards.AsNoTracking().SingleAsync(c => c.Id == cardId);
            row.ErrorReportsCount.Should().Be(7);
            row.FeedbackScore.Should().Be(0.30m);
        }
    }
}
