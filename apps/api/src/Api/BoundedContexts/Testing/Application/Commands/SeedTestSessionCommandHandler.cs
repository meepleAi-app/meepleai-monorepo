using Api.BoundedContexts.Testing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.GameManagement;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-8) — Handler for
/// <see cref="SeedTestSessionCommand"/>. Persists a GameNightSessionEntity
/// stamped with explicit <c>TestRunId</c> column for cleanup scope.
/// </summary>
internal sealed class SeedTestSessionCommandHandler
    : IRequestHandler<SeedTestSessionCommand, SeedTestSessionResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedTestSessionCommandHandler> _logger;

    public SeedTestSessionCommandHandler(
        MeepleAiDbContext db,
        ILogger<SeedTestSessionCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestSessionResponse> Handle(
        SeedTestSessionCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        var gameNight = await _db.GameNightEvents
            .Include(g => g.Sessions)
            .SingleOrDefaultAsync(g => g.Id == request.GameNightId, cancellationToken)
            .ConfigureAwait(false);

        if (gameNight is null)
        {
            throw new NotFoundException("GameNight", request.GameNightId.ToString());
        }

        var sessionId = Guid.NewGuid();
        var nextPlayOrder = gameNight.Sessions.Count + 1;
        var session = new GameNightSessionEntity
        {
            Id = sessionId,
            GameNightEventId = request.GameNightId,
            SessionId = sessionId,
            GameId = Guid.NewGuid(),
            GameTitle = $"E2E Game {request.TestRunId[..16]}",
            PlayOrder = nextPlayOrder,
            Status = request.IsLive ? "InProgress" : "Pending",
            StartedAt = request.IsLive ? DateTimeOffset.UtcNow : null,
            CompletedAt = null,
            WinnerId = null,
            TestRunId = request.TestRunId
        };

        _db.Set<GameNightSessionEntity>().Add(session);
        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded Session {SessionId} gameNight={GameNightId} isLive={IsLive} testRunId={TestRunId} durationMs={Duration}",
            sessionId, request.GameNightId, request.IsLive, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestSessionResponse(sessionId, request.GameNightId, request.IsLive, request.TestRunId);
    }
}
