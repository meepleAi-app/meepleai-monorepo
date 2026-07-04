using Api.BoundedContexts.SessionTracking.Domain.Entities;
using Api.BoundedContexts.SessionTracking.Domain.Repositories;
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

namespace Api.Tests.Integration.SessionTracking;

/// <summary>
/// Regression for #2660: <see cref="ISessionRepository.UpdateAsync"/> must persist scalar
/// domain mutations (StartedAt/FinalizedAt/Status/...) through the real pipeline.
///
/// The DbContext defaults to <c>QueryTrackingBehavior.NoTracking</c> (PERF-06), so the
/// entity <c>UpdateAsync</c> loads to copy scalars onto must be loaded <c>.AsTracking()</c> —
/// otherwise the mutation is a silent no-op and SaveChanges persists nothing. Existing tests
/// missed this because they asserted <c>domain_event_logs</c> (which persist via the collector
/// regardless of entity tracking), not the scalar columns.
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SessionTracking")]
[Trait("Issue", "2660")]
public sealed class SessionRepositoryUpdatePersistenceTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _databaseName = $"session_update_persist_{Guid.NewGuid():N}";
    private WebApplicationFactory<Program> _factory = null!;

    public SessionRepositoryUpdatePersistenceTests(SharedTestcontainersFixture fixture) => _fixture = fixture;

    public async ValueTask InitializeAsync()
    {
        var conn = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);
        await TestcontainersWaitHelpers.WaitForPostgresReadyAsync(conn);
        _factory = IntegrationWebApplicationFactory.Create(conn);
        using var scope = _factory.Services.CreateScope();
        await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Database.MigrateAsync();
    }

    public async ValueTask DisposeAsync()
    {
        if (_factory != null) await _factory.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    [Fact(DisplayName = "#2660: UpdateAsync persists Session.StartedAt (OpenLiveMode) to the scalar column")]
    public async Task UpdateAsync_PersistsStartedAt()
    {
        Guid sessionId;
        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db);
            var gameId = await TestSessionHelper.SeedSharedGameAsync(db, title: "Update-Persist Game");
            var session = Session.Create(userId, gameId, SessionType.Generic);
            await repo.AddAsync(session, CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
            sessionId = session.Id;
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var repo = scope.ServiceProvider.GetRequiredService<ISessionRepository>();
            var uow = scope.ServiceProvider.GetRequiredService<IUnitOfWork>();

            var session = await repo.GetByIdAsync(sessionId, CancellationToken.None);
            session!.OpenLiveMode();
            await repo.UpdateAsync(session, CancellationToken.None);
            await uow.SaveChangesAsync(CancellationToken.None);
        }

        await using (var scope = _factory.Services.CreateAsyncScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var entity = await db.SessionTrackingSessions.AsNoTracking().FirstAsync(s => s.Id == sessionId);
            entity.StartedAt.Should().NotBeNull(
                "UpdateAsync must persist the StartedAt scalar set by OpenLiveMode (#2660 — .AsTracking() under the NoTracking default)");
        }
    }
}
