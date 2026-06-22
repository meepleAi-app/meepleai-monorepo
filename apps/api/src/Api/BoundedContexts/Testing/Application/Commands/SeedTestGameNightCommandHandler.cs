using Api.BoundedContexts.Testing.Application.DTOs;
using Api.BoundedContexts.Testing.Infrastructure;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;
using System.Text.Json;

namespace Api.BoundedContexts.Testing.Application.Commands;

internal sealed class SeedTestGameNightCommandHandler
    : IRequestHandler<SeedTestGameNightCommand, SeedTestGameNightResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedTestGameNightCommandHandler> _logger;

    public SeedTestGameNightCommandHandler(
        MeepleAiDbContext db,
        ILogger<SeedTestGameNightCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestGameNightResponse> Handle(
        SeedTestGameNightCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // Issue #1929 Macro 4 (DEC-C-10 PIVOT): verify GameId exists in SharedGames if provided.
        if (request.GameId.HasValue)
        {
            var gameExists = await _db.SharedGames
                .AnyAsync(g => g.Id == request.GameId.Value, cancellationToken)
                .ConfigureAwait(false);
            if (!gameExists)
            {
                throw new BadRequestException($"GameId {request.GameId.Value} not found in SharedGames catalog");
            }
        }

        var ownerId = Guid.NewGuid();
        var ownerEntity = new UserEntity
        {
            Id = ownerId,
            Email = request.OwnerEmail,
            DisplayName = $"E2E Host {request.TestRunId[..16]}",
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

        _db.Users.Add(ownerEntity);

        var gameNightId = Guid.NewGuid();

        // Determine final status: sessions require Published state, so InProgress/Completed map to Published
        var finalStatus = request.Status switch
        {
            "InProgress" or "Completed" => "Published",
            _ => request.Status
        };

        // Issue #1929 Macro 4 (DEC-C-10 PIVOT): include GameId in GameIdsJson when provided.
        var gameIds = request.GameId.HasValue
            ? new List<Guid> { request.GameId.Value }
            : new List<Guid>();

        var gameNightEntity = new GameNightEventEntity
        {
            Id = gameNightId,
            OrganizerId = ownerId,
            Title = $"E2E GameNight {request.TestRunId[..16]}",
            Description = "Seeded for E2E testing",
            ScheduledAt = DateTimeOffset.UtcNow.AddDays(7),
            Location = "E2E Location",
            MaxPlayers = null,
            GameIdsJson = JsonSerializer.Serialize(gameIds),
            Status = finalStatus,
            CreatedAt = DateTime.UtcNow,
            UpdatedAt = DateTime.UtcNow,
            Rsvps = [],
            Sessions = []
        };

        // Create sessions if InProgress or Completed requested.
        // Issue #1929 Macro 4 (DEC-C-10 PIVOT): when request.GameId is provided (linked SharedGame),
        // propagate it to the session's GameId so the child entity references a real catalog entry
        // instead of a phantom Guid. Fall back to Guid.NewGuid() for legacy callers that omit GameId.
        if (request.Status is "InProgress" or "Completed")
        {
            var sessionId = Guid.NewGuid();
            var session = new GameNightSessionEntity
            {
                Id = sessionId,
                GameNightEventId = gameNightId,
                SessionId = sessionId,
                GameId = request.GameId ?? Guid.NewGuid(),
                GameTitle = "E2E Game Title",
                PlayOrder = 1,
                Status = request.Status is "Completed" ? "Completed" : "InProgress",
                StartedAt = DateTimeOffset.UtcNow,
                CompletedAt = request.Status is "Completed" ? DateTimeOffset.UtcNow : null,
                WinnerId = null,
                TestRunId = request.TestRunId
            };
            gameNightEntity.Sessions.Add(session);
        }

        gameNightEntity.TestRunId = request.TestRunId;
        _db.GameNightEvents.Add(gameNightEntity);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded GameNight {GameNightId} status={Status} testRunId={TestRunId} durationMs={Duration}",
            gameNightId, request.Status, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestGameNightResponse(gameNightId, ownerId, request.TestRunId);
    }
}
