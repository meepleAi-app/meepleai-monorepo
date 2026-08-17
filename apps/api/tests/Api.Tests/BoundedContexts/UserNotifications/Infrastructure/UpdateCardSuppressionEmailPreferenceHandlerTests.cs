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
/// #535: the dedicated card-suppression email preference command persists the opt-in flag (backend
/// settability; the FE checkbox is a follow-up).
/// </summary>
[Collection("Integration-GroupD")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "UserNotifications")]
public sealed class UpdateCardSuppressionEmailPreferenceHandlerTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private readonly string _testDbName;
    private WebApplicationFactory<Program> _factory = null!;
    private Guid _userId;

    public UpdateCardSuppressionEmailPreferenceHandlerTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
        _testDbName = $"me535_pref_{Guid.NewGuid():N}";
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
    public async Task Command_PersistsFlag_CreatingPrefsWhenMissing()
    {
        using (var scope = _factory.Services.CreateScope())
        {
            var mediator = scope.ServiceProvider.GetRequiredService<IMediator>();
            await mediator.Send(new UpdateCardSuppressionEmailPreferenceCommand(_userId, true));
        }

        using (var scope = _factory.Services.CreateScope())
        {
            var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
            var row = await db.Set<NotificationPreferencesEntity>().AsNoTracking().SingleAsync(p => p.UserId == _userId);
            row.EmailOnCardSuppressed.Should().BeTrue();
        }
    }
}
