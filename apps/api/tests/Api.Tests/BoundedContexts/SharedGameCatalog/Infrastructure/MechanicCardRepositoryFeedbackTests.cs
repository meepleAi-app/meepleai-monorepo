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
/// Integration tests for the #534 repository additions: the active-card feedback aggregate query
/// (excludes suppressed cards) and the tracked <see cref="IMechanicCardRepository.Update"/> path.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class MechanicCardRepositoryFeedbackTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public MechanicCardRepositoryFeedbackTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me534_repo_{Guid.NewGuid():N}";
    }

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_testDbName);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        await db.Database.MigrateAsync();
        (_userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, Guid.NewGuid());
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory is not null)
        {
            await _factory.DisposeAsync();
        }
        await _fixture.DropIsolatedDatabaseAsync(_testDbName);
    }

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
