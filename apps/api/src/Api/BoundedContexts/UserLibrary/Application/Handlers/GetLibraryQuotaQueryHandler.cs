using Api.BoundedContexts.Authentication.Domain.ValueObjects;
using Api.BoundedContexts.UserLibrary.Application.DTOs;
using Api.BoundedContexts.UserLibrary.Application.Queries;
using Api.BoundedContexts.UserLibrary.Domain.Services;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Exceptions;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.UserLibrary.Application.Handlers;

/// <summary>
/// Handler for getting user's library quota information.
/// </summary>
internal class GetLibraryQuotaQueryHandler : IQueryHandler<GetLibraryQuotaQuery, LibraryQuotaDto>
{
    private readonly IGameLibraryQuotaService _quotaService;
    private readonly MeepleAiDbContext _db;
    private readonly ILogger<GetLibraryQuotaQueryHandler> _logger;

    public GetLibraryQuotaQueryHandler(
        IGameLibraryQuotaService quotaService,
        MeepleAiDbContext db,
        ILogger<GetLibraryQuotaQueryHandler> logger)
    {
        _quotaService = quotaService ?? throw new ArgumentNullException(nameof(quotaService));
        _db = db ?? throw new ArgumentNullException(nameof(db));
        _logger = logger ?? throw new ArgumentNullException(nameof(logger));
    }

    public async Task<LibraryQuotaDto> Handle(GetLibraryQuotaQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        // Get user tier and role from database
        var user = await _db.Users
            .AsNoTracking()
            .Where(u => u.Id == query.UserId)
            .Select(u => new { u.Id, u.Tier, u.Role })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);

        if (user == null)
        {
            _logger.LogError("User {UserId} not found during library quota query", query.UserId);
            throw new DomainException($"User with ID {query.UserId} not found");
        }

        var userTier = UserTier.Parse(user.Tier);
        var userRole = Role.Parse(user.Role);

        var quotaInfo = await _quotaService.GetQuotaInfoAsync(
            query.UserId,
            userTier,
            userRole,
            cancellationToken).ConfigureAwait(false);

        // Calculate percentage used (Issue #2445)
        var percentageUsed = quotaInfo.IsUnlimited || quotaInfo.MaxGames == 0
            ? 0
            : (int)Math.Round((double)quotaInfo.GamesInLibrary / quotaInfo.MaxGames * 100);

        return new LibraryQuotaDto(
            CurrentCount: quotaInfo.GamesInLibrary,
            MaxAllowed: quotaInfo.MaxGames,
            UserTier: userTier.Value.ToLowerInvariant(),
            RemainingSlots: quotaInfo.RemainingSlots,
            PercentageUsed: percentageUsed
        );
    }
}
