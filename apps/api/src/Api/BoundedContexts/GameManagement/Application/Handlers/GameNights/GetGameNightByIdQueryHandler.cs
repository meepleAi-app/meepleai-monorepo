using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Application.Queries.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Infrastructure;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.GameManagement.Application.Handlers.GameNights;

/// <summary>
/// Handles retrieval of a game night by ID.
/// Issue #46: GameNight API endpoints.
/// Issue #43: Enriches DTO with OrganizerName, uses NotFoundException.
/// </summary>
internal sealed class GetGameNightByIdQueryHandler : IQueryHandler<GetGameNightByIdQuery, GameNightDto>
{
    private readonly IGameNightEventRepository _repository;
    private readonly MeepleAiDbContext _context;

    public GetGameNightByIdQueryHandler(
        IGameNightEventRepository repository,
        MeepleAiDbContext context)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
        _context = context ?? throw new ArgumentNullException(nameof(context));
    }

    public async Task<GameNightDto> Handle(GetGameNightByIdQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        var organizerName = await GetUserDisplayNameAsync(gameNight.OrganizerId, cancellationToken).ConfigureAwait(false);

        return GameNightMapperHelper.MapToDto(gameNight, organizerName);
    }

    private async Task<string> GetUserDisplayNameAsync(Guid userId, CancellationToken cancellationToken)
    {
        var user = await _context.Users
            .AsNoTracking()
            .FirstOrDefaultAsync(u => u.Id == userId, cancellationToken)
            .ConfigureAwait(false);

        return user?.DisplayName ?? user?.Email ?? "Unknown";
    }
}
