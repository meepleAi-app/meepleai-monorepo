using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Infrastructure.Seeders.Core;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using MediatR;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Host e database una volta per classe (#4050), con gli eventi di dominio consegnati in linea (<c>Hybrid</c>): nel
/// default <c>OutboxOnly</c> dell'host restano nella tabella dell'outbox e nessun gestore girerebbe.
/// </summary>
public sealed class UserBadgeEventsHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "user_badge_events")
{
    protected override WebApplicationFactory<Program> ConfigureHost(WebApplicationFactory<Program> factory) =>
        factory.WithWebHostBuilder(builder => builder.ConfigureTestServices(services =>
        {
            services.RemoveAll<IOptions<DomainEventOutboxOptions>>();
            services.AddSingleton<IOptions<DomainEventOutboxOptions>>(
                Options.Create(new DomainEventOutboxOptions { Mode = DomainEventDispatchMode.Hybrid }));
        }));
}

/// <summary>
/// #5110 — <c>UserBadgeRepository</c> non raccoglieva gli eventi di dominio, quindi <c>BadgeEarnedEvent</c> non
/// arrivava ai gestori e nessuno riceveva la notifica di un badge. I due gestori di quell'evento spedivano poi con lo
/// stesso <c>SourceEventId</c>, e il dispatcher scartava il secondo: per una pietra miliare, il testo dipendeva
/// dall'ordine dei gestori. I badge si assegnano come fanno BadgeEvaluationOnApprovalHandler e RecalculateBadges.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "5110")]
public sealed class UserBadgeDomainEventsIntegrationTests : IClassFixture<UserBadgeEventsHostFixture>
{
    private const string FirstContributionMessage = "Welcome to the MeepleAI community!";

    private readonly WebApplicationFactory<Program> _factory;

    public UserBadgeDomainEventsIntegrationTests(UserBadgeEventsHostFixture host)
    {
        _factory = host.Factory;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task AnAwardedBadge_NotifiesItsUser_Once()
    {
        var userId = await SeedUserAsync();
        var badge = await AwardAsync(userId, "CONTRIBUTOR_5");

        var notifications = await BadgeNotificationsAsync(userId);

        notifications.Should().ContainSingle().Which.Message.Should().Contain(badge.Name);
    }

    [Fact]
    public async Task AMilestoneBadge_IsOneNotification_WithTheMilestoneMessage()
    {
        var userId = await SeedUserAsync();
        await AwardAsync(userId, "FIRST_CONTRIBUTION");

        var notifications = await BadgeNotificationsAsync(userId);

        notifications.Should().ContainSingle().Which.Message.Should().Contain(FirstContributionMessage);
    }

    [Fact]
    public async Task ABadgeEventDeliveredAgainByTheOutbox_ChangesNothing()
    {
        var userId = await SeedUserAsync();
        await AwardAsync(userId, "FIRST_CONTRIBUTION");

        // Hybrid delivers the event inline and leaves its outbox row for the processor: deliver that row the way the
        // processor does (DomainEventOutboxProcessor), from its JSON.
        await DeliverOutboxRowAgainAsync(userId);

        (await BadgeNotificationsAsync(userId)).Should().ContainSingle();
    }

    private async Task<Guid> SeedUserAsync()
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var (userId, _) = await TestSessionHelper.CreateUserSessionAsync(db, cancellationToken: Ct);
        // Idempotent: the badges the evaluator awards, under their codes.
        await BadgeSeeder.SeedBadgesAsync(db, NullLogger.Instance, Ct);
        return userId;
    }

    // The way BadgeEvaluationOnApprovalHandler awards one: UserBadge.Award, AddAsync, IUnitOfWork.SaveChangesAsync.
    private async Task<Badge> AwardAsync(Guid userId, string badgeCode)
    {
        using var scope = _factory.Services.CreateScope();
        var badge = (await scope.ServiceProvider.GetRequiredService<IBadgeRepository>().GetByCodeAsync(badgeCode, Ct))!;
        await scope.ServiceProvider.GetRequiredService<IUserBadgeRepository>()
            .AddAsync(UserBadge.Award(userId, badge.Id, badge.Code), Ct);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return badge;
    }

    private async Task DeliverOutboxRowAgainAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        var db = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();
        var resolver = new DomainEventTypeResolver();
        var rows = await db.DomainEventOutbox.AsNoTracking().ToListAsync(Ct);
        var redelivered = rows
            .Where(row => resolver.Resolve(row.EventType) == typeof(BadgeEarnedEvent))
            .Select(row => (BadgeEarnedEvent)JsonSerializer.Deserialize(
                row.PayloadJson, typeof(BadgeEarnedEvent), DomainEventJsonOptions.Default)!)
            .Should().ContainSingle(e => e.UserId == userId).Subject;

        await scope.ServiceProvider.GetRequiredService<IMediator>().Publish(redelivered, Ct);
    }

    private async Task<List<NotificationEntity>> BadgeNotificationsAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == "badge_earned")
            .ToListAsync(Ct);
    }
}
