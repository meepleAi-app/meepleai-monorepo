using Api.BoundedContexts.Testing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities;
using Api.Infrastructure.Entities.SharedGameCatalog;
using Api.Infrastructure.Entities.UserLibrary;
using MediatR;
using Microsoft.EntityFrameworkCore;
using Microsoft.Extensions.Logging;

namespace Api.BoundedContexts.Testing.Application.Commands;

/// <summary>
/// Issue #1929 Task C Macro 3a (DEC-C-8, DEC-B-8) — Handler for
/// <see cref="SeedTestLibraryGameCommand"/>. Creates (or reuses) the owner
/// User row, persists a SharedGameEntity (Published, GameDataStatus=Complete,
/// minimal content) + UserLibraryEntryEntity (junction) stamped with the
/// explicit <c>TestRunId</c> column for cleanup scope.
/// </summary>
internal sealed class SeedTestLibraryGameCommandHandler
    : IRequestHandler<SeedTestLibraryGameCommand, SeedTestLibraryGameResponse>
{
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<SeedTestLibraryGameCommandHandler> _logger;

    public SeedTestLibraryGameCommandHandler(
        MeepleAiDbContext db,
        ILogger<SeedTestLibraryGameCommandHandler> logger)
    {
        _db = db;
        _logger = logger;
    }

    public async Task<SeedTestLibraryGameResponse> Handle(
        SeedTestLibraryGameCommand request,
        CancellationToken cancellationToken)
    {
        var stopwatch = System.Diagnostics.Stopwatch.StartNew();

        // 1. Lookup or create UserEntity by OwnerEmail (TestRunId stamped if created).
        var owner = await _db.Users
            .SingleOrDefaultAsync(u => u.Email == request.OwnerEmail, cancellationToken)
            .ConfigureAwait(false);

        if (owner is null)
        {
            owner = new UserEntity
            {
                Id = Guid.NewGuid(),
                Email = request.OwnerEmail,
                DisplayName = $"E2E Owner {request.TestRunId[..16]}",
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
            _db.Users.Add(owner);
        }

        // 2. Create SharedGameEntity (catalog row).
        var gameId = Guid.NewGuid();
        var sharedGame = new SharedGameEntity
        {
            Id = gameId,
            Title = request.Title ?? $"E2E Library Game {request.TestRunId[..8]}",
            YearPublished = DateTime.UtcNow.Year,
            Description = $"E2E test game seeded for {request.TestRunId}",
            MinPlayers = request.MinPlayers ?? 2,
            MaxPlayers = request.MaxPlayers ?? 4,
            PlayingTimeMinutes = 60,
            MinAge = 8,
            ImageUrl = string.Empty,
            ThumbnailUrl = string.Empty,
            Status = 1, // Published
            GameDataStatus = 5, // Complete
            CreatedBy = owner.Id,
            CreatedAt = DateTime.UtcNow,
            TestRunId = request.TestRunId
        };
        _db.SharedGames.Add(sharedGame);

        // 3. Create UserLibraryEntryEntity (junction owner <-> sharedGame).
        var libraryEntryId = Guid.NewGuid();
        var libraryEntry = new UserLibraryEntryEntity
        {
            Id = libraryEntryId,
            UserId = owner.Id,
            SharedGameId = sharedGame.Id,
            AddedAt = DateTime.UtcNow,
            TestRunId = request.TestRunId
        };
        _db.UserLibraryEntries.Add(libraryEntry);

        await _db.SaveChangesAsync(cancellationToken).ConfigureAwait(false);
        stopwatch.Stop();

        _logger.LogInformation(
            "Seeded LibraryGame gameId={GameId} libraryEntryId={LibraryEntryId} ownerId={OwnerId} testRunId={TestRunId} durationMs={Duration}",
            gameId, libraryEntryId, owner.Id, request.TestRunId, stopwatch.ElapsedMilliseconds);

        return new SeedTestLibraryGameResponse(gameId, libraryEntryId, owner.Id, request.TestRunId);
    }
}
