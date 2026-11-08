using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// #4759 on a real Postgres. <c>ParticipatedBy</c> composes a <c>DbSet</c> captured in a closure as a
/// correlated subquery inside the three list queries; the InMemory provider in
/// <see cref="GameNightEventRepositoryTests"/> evaluates that in memory and proves nothing about the
/// Npgsql translation. If the translation failed, every dashboard list would throw at runtime with
/// the unit tests green. One run here, naming the three methods and the detail gate.
/// </summary>
/// <remarks>
/// The repository is <c>internal</c>, so a public test class cannot derive from the generic base over
/// it; the repository is built here instead.
/// </remarks>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public class GameNightEventRepositoryPostgresTests : SharedDatabaseTestBase
{
    public GameNightEventRepositoryPostgresTests(SharedTestcontainersFixture fixture) : base(fixture)
    {
    }

    private GameNightEventRepository Repository =>
        new(DbContext, new Mock<IDomainEventCollector>().Object, NullLogger<GameNightEventRepository>.Instance);

    private GameNightEventEntity SeedNight(Guid organizerId, string status, DateTimeOffset scheduledAt)
    {
        var now = DateTimeOffset.UtcNow;
        var entity = new GameNightEventEntity
        {
            Id = Guid.NewGuid(),
            OrganizerId = organizerId,
            Title = "Night",
            ScheduledAt = scheduledAt,
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = status,
            CreatedAt = now,
            UpdatedAt = now
        };
        DbContext.GameNightEvents.Add(entity);
        return entity;
    }

    private Guid SeedUser()
    {
        var id = Guid.NewGuid();
        DbContext.Users.Add(new Api.Infrastructure.Entities.UserEntity
        {
            Id = id,
            Email = $"{id:N}@example.com",
            DisplayName = "Viewer",
            PasswordHash = "x",
            Role = "user",
            CreatedAt = DateTime.UtcNow,
            IsTwoFactorEnabled = false
        });
        return id;
    }

    private void SeedEmailInvitation(Guid gameNightId, Guid? respondedBy, string status, string? email = null)
    {
        var now = DateTimeOffset.UtcNow;
        DbContext.GameNightInvitations.Add(new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = Guid.NewGuid().ToString("N"),
            GameNightId = gameNightId,
            Email = email ?? (respondedBy is { } user ? $"{user:N}@example.com" : $"{Guid.NewGuid():N}@example.com"),
            Status = status,
            RespondedByUserId = respondedBy,
            RespondedAt = respondedBy is null ? null : now,
            ExpiresAt = now.AddDays(7),
            CreatedAt = now,
            CreatedBy = Guid.NewGuid(),
        });
    }

    [Fact]
    public async Task TheThreeLists_AndTheDetailGate_SeeAnAcceptedEmailInvitation_OnPostgres()
    {
        var ct = TestContext.Current.CancellationToken;
        var viewer = SeedUser();
        var organizer = Guid.NewGuid();
        var upcoming = SeedNight(organizer, "Published", DateTimeOffset.UtcNow.AddDays(2));
        var completed = SeedNight(organizer, "Completed", DateTimeOffset.UtcNow.AddDays(-3));
        var declinedNight = SeedNight(organizer, "Published", DateTimeOffset.UtcNow.AddDays(3));
        var strangersNight = SeedNight(organizer, "Published", DateTimeOffset.UtcNow.AddDays(4));
        var forwardedNight = SeedNight(organizer, "Published", DateTimeOffset.UtcNow.AddDays(5));
        SeedEmailInvitation(upcoming.Id, viewer, "Accepted");
        SeedEmailInvitation(completed.Id, viewer, "Accepted");
        SeedEmailInvitation(declinedNight.Id, viewer, "Declined");
        SeedEmailInvitation(strangersNight.Id, SeedUser(), "Accepted");
        SeedEmailInvitation(forwardedNight.Id, viewer, "Accepted", email: "somebody.else@example.com");
        await DbContext.SaveChangesAsync(ct);
        DbContext.ChangeTracker.Clear();

        var repository = Repository;

        (await repository.GetUpcomingAsync(viewer, ct)).Select(n => n.Id)
            .Should().BeEquivalentTo([upcoming.Id]);
        (await repository.GetCompletedAsync(viewer, 10, ct)).Select(n => n.Id)
            .Should().BeEquivalentTo([completed.Id]);
        (await repository.GetByUserAsync(viewer, ct)).Select(n => n.Id)
            .Should().BeEquivalentTo([upcoming.Id, completed.Id]);

        (await repository.HasAcceptedEmailInvitationAsync(upcoming.Id, viewer, ct)).Should().BeTrue();
        (await repository.HasAcceptedEmailInvitationAsync(declinedNight.Id, viewer, ct)).Should().BeFalse();
        (await repository.HasAcceptedEmailInvitationAsync(strangersNight.Id, viewer, ct)).Should().BeFalse();
        (await repository.HasAcceptedEmailInvitationAsync(forwardedNight.Id, viewer, ct)).Should().BeFalse("a forwarded link");

        // #4921: the state the guest reads as, per night, with the same binding. The decline is theirs
        // too (they can read it, and change it from the app); the stranger's and the forwarded one are not.
        var statuses = await repository.GetEmailGuestRsvpStatusesAsync(
            [upcoming.Id, completed.Id, declinedNight.Id, strangersNight.Id, forwardedNight.Id], viewer, ct);
        statuses.Should().BeEquivalentTo(new Dictionary<Guid, RsvpStatus>
        {
            [upcoming.Id] = RsvpStatus.Accepted,
            [completed.Id] = RsvpStatus.Accepted,
            [declinedNight.Id] = RsvpStatus.Declined,
        });

        // And the row the guest answers from the app (#4921), from the invitation repository.
        var invitations = new GameNightInvitationRepository(
            DbContext, new Mock<IDomainEventCollector>().Object, NullLogger<GameNightInvitationRepository>.Instance);
        (await invitations.GetAnsweredByUserAsync(declinedNight.Id, viewer, ct))!.Status
            .Should().Be(GameNightInvitationStatus.Declined);
        (await invitations.GetAnsweredByUserAsync(strangersNight.Id, viewer, ct)).Should().BeNull();
        (await invitations.GetAnsweredByUserAsync(forwardedNight.Id, viewer, ct)).Should().BeNull("a forwarded link");
    }
}
