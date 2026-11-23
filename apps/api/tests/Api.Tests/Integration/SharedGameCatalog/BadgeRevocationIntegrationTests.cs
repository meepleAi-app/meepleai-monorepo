using Api.BoundedContexts.SharedGameCatalog.Application.Commands;
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Application.EventHandlers;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Events;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure;
using Api.Infrastructure.DomainEventOutbox;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserNotifications;
using Api.Infrastructure.Seeders.Core;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Api.Tests.TestHelpers;
using FluentAssertions;
using Microsoft.AspNetCore.Mvc.Testing;
using Microsoft.AspNetCore.TestHost;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.DependencyInjection.Extensions;
using Microsoft.Extensions.Logging.Abstractions;
using Microsoft.Extensions.Options;
using Moq;
using Xunit;

namespace Api.Tests.Integration.SharedGameCatalog;

/// <summary>
/// Host e database una volta per classe (#4050), con gli eventi di dominio consegnati in linea (<c>Hybrid</c>): così
/// la notifica di un badge assegnato si vede, e si vede anche che una riattivazione non ne manda una seconda.
/// </summary>
public sealed class BadgeRevocationHostFixture(SharedTestcontainersFixture shared)
    : IntegrationHostFixture(shared, "badge_revocation")
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
/// #5116 — il ricalcolo revocava i badge solo in memoria: <c>GetByUserIdAsync</c> legge <c>AsNoTracking</c> e il
/// repository non aveva un aggiornamento. Decisione del 2026-11-23: si revocano solo i badge di classifica, e un badge
/// revocato si riassegna riattivando la sua riga, perché l'indice univoco <c>(UserId, BadgeId)</c> ne rifiuta una
/// seconda con 23505, senza una seconda notifica. I due percorsi che assegnano (ricalcolo e approvazione) girano qui
/// come in produzione, con un valutatore che decide l'idoneità al posto delle statistiche.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "5116")]
public sealed class BadgeRevocationIntegrationTests : IClassFixture<BadgeRevocationHostFixture>
{
    private const string Leaderboard = "TOP_CONTRIBUTOR";
    private const string QualityStreak = "QUALITY_CONTRIBUTOR";
    private const string Milestone = "FIRST_CONTRIBUTION";

    private readonly WebApplicationFactory<Program> _factory;

    public BadgeRevocationIntegrationTests(BadgeRevocationHostFixture host)
    {
        _factory = host.Factory;
    }

    private static CancellationToken Ct => TestContext.Current.CancellationToken;

    [Fact]
    public async Task ALeaderboardBadge_NoLongerEarned_StaysRevokedAfterTheSave()
    {
        var userId = await SeedUserAsync();
        await AwardAsync(userId, Leaderboard);

        var response = await RecalculateAsync(userId, eligible: []);

        response.BadgesRevoked.Should().Be(1);
        var row = (await RowsAsync(userId, Leaderboard)).Should().ContainSingle().Subject;
        row.RevokedAt.Should().NotBeNull("the count of revocations must count revocations that exist");
        row.RevocationReason.Should().Be("No longer meets badge requirements");
        row.IsDisplayed.Should().BeTrue("the display preference is kept for when the badge comes back");
    }

    [Fact]
    public async Task AFullRecalculation_GivesBackABadge_ToAUserWhoseOnlyBadgeWasRevoked()
    {
        // From the review of #5130: the full run picked its users among the active rows, so a user left with a revoked
        // badge only was never evaluated again.
        var userId = await SeedUserAsync();
        var awarded = await AwardAsync(userId, Leaderboard);
        await RecalculateAsync(userId, eligible: []);

        await RecalculateEveryoneAsync(eligibleUserId: userId, eligible: [Leaderboard]);

        var row = (await RowsAsync(userId, Leaderboard)).Should().ContainSingle().Subject;
        row.Id.Should().Be(awarded.Id);
        row.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ARevokedLeaderboardBadge_EarnedAgain_ReinstatesTheSameRow_WithoutASecondNotification()
    {
        var userId = await SeedUserAsync();
        var awarded = await AwardAsync(userId, Leaderboard);
        await RecalculateAsync(userId, eligible: []);

        var response = await RecalculateAsync(userId, eligible: [Leaderboard]);

        response.BadgesAwarded.Should().Be(1);
        var row = (await RowsAsync(userId, Leaderboard)).Should().ContainSingle("a second row would be a 23505").Subject;
        row.Id.Should().Be(awarded.Id);
        row.RevokedAt.Should().BeNull();
        row.RevocationReason.Should().BeNull();
        row.IsDisplayed.Should().BeTrue();
        (await BadgeNotificationsAsync(userId)).Should().ContainSingle("the user was told once, when they first earned it");
    }

    [Fact]
    public async Task AQualityStreakBadge_NoLongerEarned_IsKept()
    {
        var userId = await SeedUserAsync();
        await AwardAsync(userId, QualityStreak);

        var response = await RecalculateAsync(userId, eligible: []);

        response.BadgesRevoked.Should().Be(0);
        (await RowsAsync(userId, QualityStreak)).Should().ContainSingle().Which.RevokedAt.Should().BeNull();
    }

    [Fact]
    public async Task ARevokedBadge_EarnedAgainOnAnApproval_IsReinstated_AndTheOtherBadgesOfThatApprovalAreAwarded()
    {
        // The approval handler swallows its failures: with a second row for the revoked badge, the SaveChanges would
        // fail on 23505 and the milestone awarded in the same approval would be lost with it.
        var userId = await SeedUserAsync();
        var awarded = await AwardAsync(userId, Leaderboard);
        await RecalculateAsync(userId, eligible: []);

        await ApproveAsync(userId, eligible: [Leaderboard, Milestone]);

        var row = (await RowsAsync(userId, Leaderboard)).Should().ContainSingle().Subject;
        row.Id.Should().Be(awarded.Id);
        row.RevokedAt.Should().BeNull();
        (await RowsAsync(userId, Milestone)).Should().ContainSingle().Which.RevokedAt.Should().BeNull();
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

    // The way BadgeEvaluationOnApprovalHandler awarded one before #5116: UserBadge.Award, AddAsync, SaveChangesAsync.
    private async Task<UserBadge> AwardAsync(Guid userId, string badgeCode)
    {
        using var scope = _factory.Services.CreateScope();
        var badge = (await scope.ServiceProvider.GetRequiredService<IBadgeRepository>().GetByCodeAsync(badgeCode, Ct))!;
        var userBadge = UserBadge.Award(userId, badge.Id, badge.Code);
        await scope.ServiceProvider.GetRequiredService<IUserBadgeRepository>().AddAsync(userBadge, Ct);
        await scope.ServiceProvider.GetRequiredService<IUnitOfWork>().SaveChangesAsync(Ct);
        return userBadge;
    }

    private Task<RecalculateBadgesResponse> RecalculateAsync(Guid userId, string[] eligible) =>
        RunRecalculationAsync(new RecalculateBadgesCommand { UserId = userId }, userId, eligible);

    // UserId null: every user with a badge row in this class's database. Only eligibleUserId is eligible for anything.
    private Task<RecalculateBadgesResponse> RecalculateEveryoneAsync(Guid eligibleUserId, string[] eligible) =>
        RunRecalculationAsync(new RecalculateBadgesCommand(), eligibleUserId, eligible);

    private async Task<RecalculateBadgesResponse> RunRecalculationAsync(
        RecalculateBadgesCommand command, Guid eligibleUserId, string[] eligible)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        var handler = new RecalculateBadgesCommandHandler(
            await EligibilityAsync(services, eligibleUserId, eligible),
            services.GetRequiredService<IUserBadgeRepository>(),
            services.GetRequiredService<IBadgeRepository>(),
            services.GetRequiredService<IUnitOfWork>(),
            NullLogger<RecalculateBadgesCommandHandler>.Instance);

        return await handler.Handle(command, Ct);
    }

    private async Task ApproveAsync(Guid userId, string[] eligible)
    {
        using var scope = _factory.Services.CreateScope();
        var services = scope.ServiceProvider;
        // The handler reads the request only for its author.
        var shareRequest = ShareRequest.Create(userId, Guid.NewGuid(), ContributionType.NewGame);
        var shareRequests = new Mock<IShareRequestRepository>();
        shareRequests
            .Setup(r => r.GetByIdAsync(shareRequest.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(shareRequest);

        var handler = new BadgeEvaluationOnApprovalHandler(
            services.GetRequiredService<MeepleAiDbContext>(),
            await EligibilityAsync(services, userId, eligible),
            services.GetRequiredService<IUserBadgeRepository>(),
            shareRequests.Object,
            services.GetRequiredService<IUnitOfWork>(),
            NullLogger<BadgeEvaluationOnApprovalHandler>.Instance);

        await handler.Handle(new ShareRequestApprovedEvent(shareRequest.Id, userId, targetSharedGameId: null), Ct);
    }

    private static async Task<IBadgeEvaluator> EligibilityAsync(
        IServiceProvider services, Guid eligibleUserId, string[] eligibleCodes)
    {
        var badges = services.GetRequiredService<IBadgeRepository>();
        var eligible = new List<Badge>();
        foreach (var code in eligibleCodes)
            eligible.Add((await badges.GetByCodeAsync(code, Ct))!);
        return new FixedEligibility(eligibleUserId, eligible);
    }

    private async Task<List<UserBadgeEntity>> RowsAsync(Guid userId, string badgeCode)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Set<UserBadgeEntity>().AsNoTracking()
            .Where(e => e.UserId == userId && e.Badge!.Code == badgeCode)
            .ToListAsync(Ct);
    }

    private async Task<List<NotificationEntity>> BadgeNotificationsAsync(Guid userId)
    {
        using var scope = _factory.Services.CreateScope();
        return await scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>().Notifications.AsNoTracking()
            .Where(n => n.UserId == userId && n.Type == "badge_earned")
            .ToListAsync(Ct);
    }

    /// <summary>
    /// One user is eligible for these badges and no others, whatever their statistics say; every other user for none.
    /// </summary>
    private sealed class FixedEligibility(Guid eligibleUserId, List<Badge> eligible) : IBadgeEvaluator
    {
        public Task<List<Badge>> EvaluateEligibleBadgesAsync(Guid userId, CancellationToken cancellationToken = default) =>
            Task.FromResult(userId == eligibleUserId ? eligible : []);

        public Task<bool> CheckBadgeRequirementAsync(
            Guid userId, BadgeRequirement requirement, CancellationToken cancellationToken = default) =>
            throw new NotSupportedException("The recalculation and the approval handler call EvaluateEligibleBadgesAsync only");
    }
}
