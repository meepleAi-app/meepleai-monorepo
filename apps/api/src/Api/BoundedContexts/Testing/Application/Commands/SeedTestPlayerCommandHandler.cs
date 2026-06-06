using Api.BoundedContexts.Testing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-8) — Handler for
/// <see cref="SeedTestPlayerCommand"/>. Persists a GameNightRsvpEntity
/// (User-linked) or GameNightInvitationEntity (guest stub) stamped with
/// explicit TestRunId column for cleanup scope.
/// </summary>
internal sealed class SeedTestPlayerCommandHandler
    : IRequestHandler<SeedTestPlayerCommand, SeedTestPlayerResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedTestPlayerCommandHandler> _logger;

    public SeedTestPlayerCommandHandler(
        MeepleAiDbContext db,
        ILogger<SeedTestPlayerCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestPlayerResponse> Handle(
        SeedTestPlayerCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var gameNight = await _db.GameNightEvents
            .SingleOrDefaultAsync(g => g.Id == request.GameNightId, cancellationToken)
            .ConfigureAwait(false);

        if (gameNight is null)
        {
            throw new NotFoundException("GameNight", request.GameNightId.ToString());
        }

        Guid playerId;
        bool isGuest;

        if (request.Role is "host")
        {
            // Return existing organizer; no new entity persisted.
            playerId = gameNight.OrganizerId;
            isGuest = false;
        }
        else if (request.Role is "player")
        {
            // User-linked RSVP. Create User if UserId not provided.
            var userId = request.UserId ?? Guid.NewGuid();
            if (request.UserId is null)
            {
                var displayName = request.DisplayName ?? $"E2E Player {request.TestRunId[..16]}";
                var userEntity = new UserEntity
                {
                    Id = userId,
                    Email = $"player-{userId:N}@e2e.test",
                    DisplayName = displayName,
                    PasswordHash = null!, // E2E seed: login bypass via admin session, no auth flow
                    Role = "user",
                    Tier = "free",
                    CreatedAt = DateTime.UtcNow,
                    EmailVerified = true,
                    Language = "en",
                    EmailNotifications = true,
                    Theme = "system",
                    DataRetentionDays = 90,
                    TestRunId = request.TestRunId
                };
                _db.Users.Add(userEntity);
            }

            var rsvp = new GameNightRsvpEntity
            {
                Id = Guid.NewGuid(),
                EventId = request.GameNightId,
                UserId = userId,
                Status = "Accepted",
                RespondedAt = DateTimeOffset.UtcNow,
                CreatedAt = DateTimeOffset.UtcNow,
                TestRunId = request.TestRunId
            };
            _db.Set<GameNightRsvpEntity>().Add(rsvp);
            playerId = userId;
            isGuest = false;
        }
        else
        {
            // guest — Invitation stub (no User).
            var invitation = new GameNightInvitationEntity
            {
                Id = Guid.NewGuid(),
                Token = Guid.NewGuid().ToString("N")[..16],
                GameNightId = request.GameNightId,
                Email = $"guest-{Guid.NewGuid():N}@e2e.test",
                Status = "Accepted",
                ExpiresAt = DateTimeOffset.UtcNow.AddDays(7),
                RespondedAt = DateTimeOffset.UtcNow,
                RespondedByUserId = null,
                RespondedByName = request.DisplayName,
                CreatedAt = DateTimeOffset.UtcNow,
                CreatedBy = gameNight.OrganizerId,
                TestRunId = request.TestRunId
            };
            _db.Set<GameNightInvitationEntity>().Add(invitation);
            playerId = invitation.Id;
            isGuest = true;
        }

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded Player {PlayerId} gameNight={GameNightId} role={Role} isGuest={IsGuest} testRunId={TestRunId} durationMs={Duration}",
            playerId, request.GameNightId, request.Role, isGuest, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestPlayerResponse(playerId, request.GameNightId, request.Role, isGuest, request.TestRunId);
    }
}
