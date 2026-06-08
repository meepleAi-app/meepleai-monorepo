using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles retrieval of recently completed game nights.
/// F20 #1974 (audit 2026-06-07): wires the dashboard "Recenti" slot
/// (Asse C P2 follow-up). Mirrors <see cref="GetUpcomingGameNightsQueryHandler"/>
/// in structure — fetches a batch of completed events and enriches them with
/// the organizer display name via a single user lookup.
/// </summary>
internal sealed class GetCompletedGameNightsQueryHandler : IQueryHandler<GetCompletedGameNightsQuery, IReadOnlyList<GameNightDto>>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _context;

    public GetCompletedGameNightsQueryHandler(
        IGameNightEventRepository repository,
        MeepleAiDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<IReadOnlyList<GameNightDto>> Handle(GetCompletedGameNightsQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var events = await _repository.GetCompletedAsync(query.Limit, cancellationToken).ConfigureAwait(false);

        var organizerIds = events.Select(e => e.OrganizerId).Distinct().ToList();
        var organizerNames = await GetUserDisplayNamesAsync(organizerIds, cancellationToken).ConfigureAwait(false);

        return events.Select(e => GameNightMapperHelper.MapToDto(
            e, organizerNames.GetValueOrDefault(e.OrganizerId, "Unknown"))).ToList();
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
