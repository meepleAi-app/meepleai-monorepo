using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries.ProposalMigrations;
using Api.BoundedContexts.UserLibrary.Domain.Repositories;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers.ProposalMigrations;

/// <summary>
/// Handler for retrieving pending ProposalMigrations for a user.
/// Issue #3666: Phase 5 - Migration Choice Flow.
/// </summary>
internal sealed class GetPendingMigrationsQueryHandler : IQueryHandler<GetPendingMigrationsQuery, List<ProposalMigrationDto>>
{
    private readonly IProposalMigrationRepository _migrationRepository;
    private readonly MeepleAiDbContext _dbContext;
    private readonly ILogger<GetPendingMigrationsQueryHandler> _logger;

    public GetPendingMigrationsQueryHandler(
        IProposalMigrationRepository migrationRepository,
        MeepleAiDbContext dbContext,
        ILogger<GetPendingMigrationsQueryHandler> logger)
    {
        _migrationRepository = migrationRepository ?? throw new ArgumentNullException(nameof(migrationRepository));
        _dbContext = dbContext ?? throw new ArgumentNullException(nameof(dbContext));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<List<ProposalMigrationDto>> Handle(GetPendingMigrationsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Get pending migrations for user
        var migrations = await _migrationRepository.GetPendingByUserIdAsync(query.UserId, cancellationToken).ConfigureAwait(false);

        if (migrations.Count == 0)
        {
            return new List<ProposalMigrationDto>();
        }

        // Fetch related game data for enrichment (PrivateGame titles, SharedGame titles)
        var privateGameIds = migrations.Select(m => m.PrivateGameId).ToList();
        var sharedGameIds = migrations.Select(m => m.SharedGameId).ToList();

        var privateGames = await _dbContext.PrivateGames
            .Where(g => privateGameIds.Contains(g.Id))
            .AsNoTracking()
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken)
            .ConfigureAwait(false);

        var sharedGames = await _dbContext.SharedGames
            .Where(g => sharedGameIds.Contains(g.Id))
            .AsNoTracking()
            .ToDictionaryAsync(g => g.Id, g => g.Title, cancellationToken)
            .ConfigureAwait(false);

        // Map to DTOs with enriched data
        var dtos = migrations.Select(m => new ProposalMigrationDto(
            Id: m.Id,
            ShareRequestId: m.ShareRequestId,
            PrivateGameId: m.PrivateGameId,
            PrivateGameTitle: privateGames.TryGetValue(m.PrivateGameId, out var privateTitle) ? privateTitle : "Unknown",
            SharedGameId: m.SharedGameId,
            SharedGameTitle: sharedGames.TryGetValue(m.SharedGameId, out var sharedTitle) ? sharedTitle : "Unknown",
            Choice: m.Choice,
            CreatedAt: m.CreatedAt,
            ChoiceAt: m.ChoiceAt
        )).ToList();

        _logger.LogInformation(
            "Retrieved {Count} pending migrations for user {UserId}",
            dtos.Count,
            query.UserId);

        return dtos;
    }
}
