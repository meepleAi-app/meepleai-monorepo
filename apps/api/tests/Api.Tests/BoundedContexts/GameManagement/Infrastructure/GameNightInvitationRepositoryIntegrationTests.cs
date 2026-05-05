using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.BoundedContexts.GameManagement.Domain.Enums;
using Api.BoundedContexts.GameManagement.Infrastructure.Persistence;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.SharedKernel.Application.Services;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.GameManagement.Infrastructure;

/// <summary>
/// Integration tests for <see cref="GameNightInvitationRepository"/> against a real PostgreSQL
/// database via Testcontainers. Validates schema-level constraints (unique token index, FK
/// cascade delete) and aggregate-level repository semantics (duplicate-pending guard,
/// AcceptedSoFar count formula) that cannot be exercised by EF Core InMemory.
/// Issue #607 (Wave A.5a): GameNight token-based RSVP backend extension.
/// </summary>
[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "GameManagement")]
public sealed class GameNightInvitationRepositoryIntegrationTests : IAsyncLifetime
{
    private readonly SharedTestcontainersFixture _fixture;
    private MeepleAiDbContext _dbContext = null!;
    private GameNightInvitationRepository _repository = null!;
    private string _databaseName = null!;
    private string _connectionString = null!;
    private Guid _organizerId;

    private static readonly DateTimeOffset Now =
        new(2026, 4, 28, 12, 0, 0, TimeSpan.Zero);
    private static readonly DateTimeOffset OneWeekFromNow = Now.AddDays(7);

    public GameNightInvitationRepositoryIntegrationTests(SharedTestcontainersFixture fixture)
    {
        _fixture = fixture;
    }

    public async ValueTask InitializeAsync()
    {
        _databaseName = $"test_invitation_repo_{Guid.NewGuid():N}";
        _connectionString = await _fixture.CreateIsolatedDatabaseAsync(_databaseName);

        _dbContext = _fixture.CreateDbContext(_connectionString);
        await _dbContext.Database.MigrateAsync();

        _organizerId = Guid.NewGuid();

        var mockCollector = new Mock<IDomainEventCollector>();
        mockCollector
            .Setup(e => e.GetAndClearEvents())
            .Returns(new List<Api.SharedKernel.Domain.Interfaces.IDomainEvent>().AsReadOnly());

        _repository = new GameNightInvitationRepository(
            _dbContext,
            mockCollector.Object,
            NullLogger<GameNightInvitationRepository>.Instance);
    }

    public async ValueTask DisposeAsync()
    {
        await _dbContext.DisposeAsync();
        await _fixture.DropIsolatedDatabaseAsync(_databaseName);
    }

    private async Task<Guid> SeedGameNightEventAsync()
    {
        var eventId = Guid.NewGuid();
        _dbContext.GameNightEvents.Add(new GameNightEventEntity
        {
            Id = eventId,
            OrganizerId = _organizerId,
            Title = "Test Night",
            ScheduledAt = Now.AddHours(48),
            Location = "Home",
            MaxPlayers = 6,
            GameIdsJson = "[]",
            Status = "Published",
            CreatedAt = Now,
            UpdatedAt = Now
        });
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();
        return eventId;
    }

    private GameNightInvitation NewInvitation(Guid gameNightId, string email)
    {
        return GameNightInvitation.Create(
            gameNightId: gameNightId,
            email: email,
            expiresAt: OneWeekFromNow,
            createdBy: _organizerId,
            utcNow: Now);
    }

    // ============================================================
    // Round-trip: AddAsync → GetByTokenAsync / GetByIdAsync
    // ============================================================

    [Fact]
    public async Task AddAsync_ThenGetByToken_RoundTripsAggregate()
    {
        var gameNightId = await SeedGameNightEventAsync();
        var invitation = NewInvitation(gameNightId, "guest@example.com");

        await _repository.AddAsync(invitation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _repository.GetByTokenAsync(invitation.Token);

        loaded.Should().NotBeNull();
        loaded!.Id.Should().Be(invitation.Id);
        loaded.Token.Should().Be(invitation.Token);
        loaded.GameNightId.Should().Be(gameNightId);
        loaded.Email.Should().Be("guest@example.com");
        loaded.Status.Should().Be(GameNightInvitationStatus.Pending);
        loaded.ExpiresAt.Should().BeCloseTo(OneWeekFromNow, TimeSpan.FromMilliseconds(1));
        loaded.CreatedBy.Should().Be(_organizerId);
    }

    // ============================================================
    // Schema constraint: UNIQUE index on token
    // ============================================================

    [Fact]
    public async Task AddAsync_WithDuplicateToken_ThrowsOnSave()
    {
        var gameNightId = await SeedGameNightEventAsync();

        // Persist first row directly via raw entity to control the token.
        const string sharedToken = "abcdefghijklmnopqrstuv";
        _dbContext.GameNightInvitations.Add(new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = sharedToken,
            GameNightId = gameNightId,
            Email = "first@example.com",
            Status = "Pending",
            ExpiresAt = OneWeekFromNow,
            CreatedAt = Now,
            CreatedBy = _organizerId
        });
        await _dbContext.SaveChangesAsync();

        // Attempt to add a second row with the same token.
        _dbContext.GameNightInvitations.Add(new GameNightInvitationEntity
        {
            Id = Guid.NewGuid(),
            Token = sharedToken,
            GameNightId = gameNightId,
            Email = "second@example.com",
            Status = "Pending",
            ExpiresAt = OneWeekFromNow,
            CreatedAt = Now,
            CreatedBy = _organizerId
        });

        var act = async () => await _dbContext.SaveChangesAsync();

        await act.Should().ThrowAsync<DbUpdateException>(
            "the IX_game_night_invitations_token unique index must reject duplicate tokens");
    }

    // ============================================================
    // Schema constraint: FK cascade delete from game_night_events
    // ============================================================

    [Fact]
    public async Task DeleteParentEvent_CascadesToInvitations()
    {
        var gameNightId = await SeedGameNightEventAsync();
        var invitation = NewInvitation(gameNightId, "guest@example.com");

        await _repository.AddAsync(invitation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Sanity: invitation exists.
        (await _dbContext.GameNightInvitations.AnyAsync(i => i.Id == invitation.Id))
            .Should().BeTrue();

        // Delete the parent event.
        var parent = await _dbContext.GameNightEvents.SingleAsync(e => e.Id == gameNightId);
        _dbContext.GameNightEvents.Remove(parent);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        (await _dbContext.GameNightInvitations.AnyAsync(i => i.Id == invitation.Id))
            .Should().BeFalse("cascade delete must purge invitations when the parent event is removed");
    }

    // ============================================================
    // Repository semantics: duplicate-pending guard
    // ============================================================

    [Fact]
    public async Task ExistsPendingByEmailAsync_FindsCaseInsensitiveMatch()
    {
        var gameNightId = await SeedGameNightEventAsync();
        var invitation = NewInvitation(gameNightId, "Guest@Example.com");

        await _repository.AddAsync(invitation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        // Lookup with mixed-case email — repository must normalize internally.
        var existsExact = await _repository.ExistsPendingByEmailAsync(gameNightId, "Guest@Example.com");
        var existsLower = await _repository.ExistsPendingByEmailAsync(gameNightId, "guest@example.com");
        var existsUpper = await _repository.ExistsPendingByEmailAsync(gameNightId, "GUEST@EXAMPLE.COM");
        var existsPadded = await _repository.ExistsPendingByEmailAsync(gameNightId, "  guest@example.com  ");

        existsExact.Should().BeTrue();
        existsLower.Should().BeTrue();
        existsUpper.Should().BeTrue();
        existsPadded.Should().BeTrue();
    }

    [Fact]
    public async Task ExistsPendingByEmailAsync_DoesNotMatch_NonPendingInvitation()
    {
        var gameNightId = await SeedGameNightEventAsync();
        var invitation = NewInvitation(gameNightId, "guest@example.com");
        invitation.Accept(userId: Guid.NewGuid(), utcNow: Now.AddHours(1));

        await _repository.AddAsync(invitation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var exists = await _repository.ExistsPendingByEmailAsync(gameNightId, "guest@example.com");

        exists.Should().BeFalse("only Pending invitations should block re-issuance");
    }

    [Fact]
    public async Task ExistsPendingByEmailAsync_ScopesByGameNightId()
    {
        var firstEventId = await SeedGameNightEventAsync();
        var secondEventId = await SeedGameNightEventAsync();
        var invitation = NewInvitation(firstEventId, "guest@example.com");

        await _repository.AddAsync(invitation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var existsInFirst = await _repository.ExistsPendingByEmailAsync(firstEventId, "guest@example.com");
        var existsInSecond = await _repository.ExistsPendingByEmailAsync(secondEventId, "guest@example.com");

        existsInFirst.Should().BeTrue();
        existsInSecond.Should().BeFalse(
            "duplicate-pending guard must scope by event so the same email can be invited to different events");
    }

    // ============================================================
    // Repository semantics: AcceptedSoFar count formula
    // ============================================================

    [Fact]
    public async Task CountAcceptedByGameNightIdAsync_CountsOnlyAcceptedRows()
    {
        var gameNightId = await SeedGameNightEventAsync();
        var otherGameNightId = await SeedGameNightEventAsync();
        var responder = Guid.NewGuid();

        // Two Accepted in target event.
        var accepted1 = NewInvitation(gameNightId, "a1@example.com");
        accepted1.Accept(responder, Now.AddHours(1));
        var accepted2 = NewInvitation(gameNightId, "a2@example.com");
        accepted2.Accept(responder, Now.AddHours(1));

        // One Pending and one Declined in target event.
        var pending = NewInvitation(gameNightId, "p1@example.com");
        var declined = NewInvitation(gameNightId, "d1@example.com");
        declined.Decline(responder, Now.AddHours(1));

        // One Accepted in *other* event (must not bleed in).
        var crossEvent = NewInvitation(otherGameNightId, "x1@example.com");
        crossEvent.Accept(responder, Now.AddHours(1));

        foreach (var i in new[] { accepted1, accepted2, pending, declined, crossEvent })
        {
            await _repository.AddAsync(i);
        }
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var count = await _repository.CountAcceptedByGameNightIdAsync(gameNightId);

        count.Should().Be(2,
            "only Accepted rows scoped to the target event should be counted");
    }

    // ============================================================
    // UpdateAsync round-trip preserves status transitions
    // ============================================================

    [Fact]
    public async Task UpdateAsync_PersistsStatusTransition()
    {
        var gameNightId = await SeedGameNightEventAsync();
        var invitation = NewInvitation(gameNightId, "guest@example.com");

        await _repository.AddAsync(invitation);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var loaded = await _repository.GetByTokenAsync(invitation.Token);
        loaded!.Accept(userId: Guid.NewGuid(), utcNow: Now.AddHours(2));

        await _repository.UpdateAsync(loaded);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var refreshed = await _repository.GetByTokenAsync(invitation.Token);
        refreshed.Should().NotBeNull();
        refreshed!.Status.Should().Be(GameNightInvitationStatus.Accepted);
        refreshed.RespondedAt.Should().NotBeNull();
        refreshed.RespondedByUserId.Should().NotBeNull();
    }

    // ============================================================
    // GetByGameNightIdAsync returns ordered list
    // ============================================================

    [Fact]
    public async Task GetByGameNightIdAsync_ReturnsInvitationsOrderedByCreatedAtDesc()
    {
        var gameNightId = await SeedGameNightEventAsync();

        var first = GameNightInvitation.Create(
            gameNightId: gameNightId,
            email: "first@example.com",
            expiresAt: OneWeekFromNow,
            createdBy: _organizerId,
            utcNow: Now);

        var second = GameNightInvitation.Create(
            gameNightId: gameNightId,
            email: "second@example.com",
            expiresAt: OneWeekFromNow,
            createdBy: _organizerId,
            utcNow: Now.AddMinutes(5));

        await _repository.AddAsync(first);
        await _repository.AddAsync(second);
        await _dbContext.SaveChangesAsync();
        _dbContext.ChangeTracker.Clear();

        var result = await _repository.GetByGameNightIdAsync(gameNightId);

        result.Should().HaveCount(2);
        result[0].Email.Should().Be("second@example.com", "newest invitation comes first");
        result[1].Email.Should().Be("first@example.com");
    }
}
