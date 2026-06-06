using Api.BoundedContexts.Testing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.GameManagement;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1928 Task B (DEC-B-1, DEC-B-3, DEC-B-8) — Handler for
/// <see cref="CleanupTestEntitiesCommand"/>. Cascade-delete by explicit
/// TestRunId column on 5 persistence entities. FK dependency order:
/// Sessions → Rsvps → Invitations → GameNightEvents → Users.
/// </summary>
internal sealed class CleanupTestEntitiesCommandHandler
    : IRequestHandler<CleanupTestEntitiesCommand, CleanupTestEntitiesResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<CleanupTestEntitiesCommandHandler> _logger;

    public CleanupTestEntitiesCommandHandler(
        MeepleAiDbContext db,
        ILogger<CleanupTestEntitiesCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<CleanupTestEntitiesResponse> Handle(
        CleanupTestEntitiesCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // FK dependency order: child rows first, parent rows last.
        var deletedSessions = await _db.Set<GameNightSessionEntity>()
            .Where(s => s.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedRsvps = await _db.Set<GameNightRsvpEntity>()
            .Where(r => r.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedInvitations = await _db.Set<GameNightInvitationEntity>()
            .Where(i => i.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedGameNights = await _db.GameNightEvents
            .Where(g => g.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        var deletedUsers = await _db.Users
            .Where(u => u.TestRunId == request.TestRunId)
            .ExecuteDeleteAsync(cancellationToken)
            .ConfigureAwait(false);

        stopwatch.Stop();

        _logger.LogInformation(
            "Cleaned up testRunId={TestRunId} gameNights={GN} sessions={S} invitations={I} rsvps={R} users={U} durationMs={Duration}",
            request.TestRunId, deletedGameNights, deletedSessions, deletedInvitations, deletedRsvps, deletedUsers, stopwatch.ElapsedMilliseconds);

        return new CleanupTestEntitiesResponse(
            request.TestRunId,
            deletedGameNights,
            deletedSessions,
            deletedInvitations,
            deletedRsvps,
            deletedUsers,
            stopwatch.ElapsedMilliseconds);
    }
}
