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
/// Issue #2995 (ADR-076): quiet-hours are settable e2e via <c>UpdateQuietHoursCommand</c>. These drive
/// the command through the real MediatR pipeline (Testcontainers Postgres) and re-read the persisted
/// COLUMNS in a fresh NoTracking scope — the guard against the PERF-06 "load+mutate+save is a silent
/// no-op" class of bug the project rules call out.
/// </summary>
[Collection("Integration-GroupA")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class UpdateQuietHoursHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public UpdateQuietHoursHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"t2995_quiethours_{Guid.NewGuid():N}";
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

    private async Task SendAsync(UpdateQuietHoursCommand command)
    {
        using var scope = _factory.Services.CreateScope();
        var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
        await mediator.Send(command);
    }

    private async Task<NotificationPreferencesEntity> ReadRowAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        return await db.Set<NotificationPreferencesEntity>()
            .AsNoTracking()
            .SingleAsync(p => p.UserId == _userId);
    }

    [Fact]
    public async Task Command_PersistsQuietHoursWindow_WhenMissing()
    {
        await SendAsync(new UpdateQuietHoursCommand(_userId, "Europe/Rome", "22:00", "08:00"));

        var row = await ReadRowAsync();
        row.TimeZone.Should().Be("Europe/Rome");
        row.QuietHoursStart.Should().Be(new TimeOnly(22, 0));
        row.QuietHoursEnd.Should().Be(new TimeOnly(8, 0));
    }

    [Fact]
    public async Task Command_UpdatesExistingQuietHours()
    {
        // Seed an existing prefs row with a different window.
        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            db.Set<NotificationPreferencesEntity>().Add(new NotificationPreferencesEntity
            {
                Id = Guid.NewGuid(),
                UserId = _userId,
                TimeZone = "UTC",
                QuietHoursStart = new TimeOnly(9, 0),
                QuietHoursEnd = new TimeOnly(17, 0),
            });
            await db.SaveChangesAsync();
        }

        await SendAsync(new UpdateQuietHoursCommand(_userId, "America/New_York", "23:30", "07:15"));

        var row = await ReadRowAsync();
        row.TimeZone.Should().Be("America/New_York");
        row.QuietHoursStart.Should().Be(new TimeOnly(23, 30));
        row.QuietHoursEnd.Should().Be(new TimeOnly(7, 15));
    }

    [Fact]
    public async Task Command_ClearsQuietHours_WhenTimesNull()
    {
        // Seed a configured window first.
        await SendAsync(new UpdateQuietHoursCommand(_userId, "Europe/Rome", "22:00", "08:00"));

        // Then clear it by passing null start/end (timezone is retained).
        await SendAsync(new UpdateQuietHoursCommand(_userId, "Europe/Rome", null, null));

        var row = await ReadRowAsync();
        row.TimeZone.Should().Be("Europe/Rome");
        row.QuietHoursStart.Should().BeNull();
        row.QuietHoursEnd.Should().BeNull();
    }
}
