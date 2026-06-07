using Api.BoundedContexts.Testing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.UserLibrary;
using Api.Middleware.Exceptions;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1929 Task C Macro 4 (DEC-C-10 REVISION, DEC-B-8) — Handler for
/// <see cref="SeedTestUserGameSessionCommand"/>. Creates a UserGameSessionEntity
/// linked to an existing UserLibraryEntry, stamped with an explicit TestRunId
/// column for cascade-delete scope in CleanupTestEntitiesCommandHandler.
///
/// FK guard: verifies UserLibraryEntryId exists before insertion (BadRequestException).
/// Defaults: PlayedAt=UtcNow, DurationMinutes=60 when not provided.
/// </summary>
internal sealed class SeedTestUserGameSessionCommandHandler
    : IRequestHandler<SeedTestUserGameSessionCommand, SeedTestUserGameSessionResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedTestUserGameSessionCommandHandler> _logger;

    public SeedTestUserGameSessionCommandHandler(
        MeepleAiDbContext db,
        ILogger<SeedTestUserGameSessionCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestUserGameSessionResponse> Handle(
        SeedTestUserGameSessionCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Guard: verify UserLibraryEntryId exists (FK child requires parent row).
        var entryExists = await _db.UserLibraryEntries
            .AnyAsync(e => e.Id == request.UserLibraryEntryId, cancellationToken)
            .ConfigureAwait(false);

        if (!entryExists)
        {
            throw new BadRequestException(
                $"UserLibraryEntryId {request.UserLibraryEntryId} not found in UserLibraryEntries");
        }

        // 2. Create UserGameSessionEntity with defaults for optional fields.
        var sessionId = Guid.NewGuid();
        var session = new UserGameSessionEntity
        {
            Id = sessionId,
            UserLibraryEntryId = request.UserLibraryEntryId,
            PlayedAt = request.PlayedAt ?? DateTime.UtcNow,
            DurationMinutes = request.DurationMinutes ?? 60,
            DidWin = request.DidWin,
            Players = request.Players,
            Notes = null,
            CreatedAt = DateTime.UtcNow,
            TestRunId = request.TestRunId
        };
        _db.Set<UserGameSessionEntity>().Add(session);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded UserGameSession sessionId={SessionId} libraryEntryId={LibraryEntryId} testRunId={TestRunId} durationMs={Duration}",
            sessionId, request.UserLibraryEntryId, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestUserGameSessionResponse(sessionId, request.UserLibraryEntryId, request.TestRunId);
    }
}
