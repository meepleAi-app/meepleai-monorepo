using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNights;

/// <summary>
/// Handles retrieval of RSVPs for a game night.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Enriches DTO with UserName via batch user lookup, uses NotFoundException.
/// </summary>
internal sealed class GetGameNightRsvpsQueryHandler : IQueryHandler<GetGameNightRsvpsQuery, IReadOnlyList<GameNightRsvpDto>>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _context;

    public GetGameNightRsvpsQueryHandler(
        IGameNightEventRepository repository,
        MeepleAiDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<GameNightRsvpDto>> Handle(GetGameNightRsvpsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        var userIds = gameNight.Rsvps.Select(r => r.UserId).Distinct().ToList();
        var userNames = await GetUserDisplayNamesAsync(userIds, cancellationToken).ConfigureAwait(false);

        return gameNight.Rsvps.Select(r => GameNightMapperHelper.MapToRsvpDto(
            r, userNames.GetValueOrDefault(r.UserId, "Unknown"))).ToList();
    }

    private async Task<Dictionary<Guid, string>> GetUserDisplayNamesAsync(List<Guid> userIds, CancellationToken cancellationToken)
    {
        var users = await _context.Users
            .AsNoTracking()
            .Where(u => userIds.Contains(u.Id))
            .Select(u => new { u.Id, u.DisplayName, u.Email })
            .ToListAsync(cancellationToken)
            .ConfigureAwait(false);

        return users.ToDictionary(u => u.Id, u => u.DisplayName ?? u.Email ?? "Unknown");
    }
}
