using Api.BoundedContexts.UserNotifications.Application.Commands;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.BoundedContexts.UserNotifications.Infrastructure;

/// <summary>
/// La fixture di <see cref="UpdateNotificationPreferencesHandlerTests"/>.
/// </summary>
/// <remarks>
/// <para>
/// #4050. Host condiviso per la classe, database fresco per test. Prima questa classe
/// costruiva l'host dentro il proprio <c>InitializeAsync</c>, che xUnit chiama una volta per
/// METODO: ~24s a test, contro ~0,1s per il clone del database.
/// </para>
/// </remarks>
public sealed class UpdateNotificationPreferencesHandlerTestsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "t2849_notifpref");

/// <summary>
/// Issue #2849 / finding #T: <c>PUT /api/v1/notifications/preferences</c> returned 204 but did
/// not persist — the command handler staged the repository Add/Update but never committed the
/// unit of work (ADR-060). These tests drive the command through MediatR (full pipeline) and then
/// re-read the row in a fresh scope to assert persistence.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class UpdateNotificationPreferencesHandlerTests
    : IClassFixture<UpdateNotificationPreferencesHandlerTestsHostFixture>, IAsyncLifetime
{
    private readonly UpdateNotificationPreferencesHandlerTestsHostFixture _hostFixture;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public UpdateNotificationPreferencesHandlerTests(UpdateNotificationPreferencesHandlerTestsHostFixture hostFixture)
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

    private static UpdateNotificationPreferencesCommand BuildCommand(Guid userId, bool emailOnDocumentReady) =>
        new(
            UserId: userId,
            EmailOnDocumentReady: emailOnDocumentReady,
            EmailOnDocumentFailed: true,
            EmailOnRetryAvailable: true,
            PushOnDocumentReady: true,
            PushOnDocumentFailed: true,
            PushOnRetryAvailable: true,
            InAppOnDocumentReady: true,
            InAppOnDocumentFailed: true,
            InAppOnRetryAvailable: true);

    [Fact]
    public async Task Command_CreatesAndPersistsPrefs_WhenMissing()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(BuildCommand(_userId, emailOnDocumentReady: false));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.Set<NotificationPreferencesEntity>()
                .AsNoTracking()
                .SingleOrDefaultAsync(p => p.UserId == _userId);

            row.Should().NotBeNull("the PUT must persist a preferences row");
            row!.EmailOnDocumentReady.Should().BeFalse();
        }
    }

    [Fact]
    public async Task Command_UpdatesAndPersistsExistingPrefs()
    {
        // Seed an existing prefs row with EmailOnDocumentReady = true (the finding's starting state).
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.Set<NotificationPreferencesEntity>().Add(new NotificationPreferencesEntity
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                EmailOnDocumentReady = true,
                TimeZone = "UTC",
            });
            await db.SaveChangesAsync();
        }

        // Toggle EmailOnDocumentReady true -> false via the command.
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(BuildCommand(_userId, emailOnDocumentReady: false));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.Set<NotificationPreferencesEntity>()
                .AsNoTracking()
                .SingleAsync(p => p.UserId == _userId);

            row.EmailOnDocumentReady.Should().BeFalse("the PUT must persist the toggled value");
        }
    }
}
