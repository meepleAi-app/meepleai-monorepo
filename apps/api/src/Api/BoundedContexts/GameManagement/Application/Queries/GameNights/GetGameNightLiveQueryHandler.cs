using Api.BoundedContexts.GameManagement.Application.DTOs.GameNights;
using Api.BoundedContexts.GameManagement.Domain.Entities.GameNightEvent;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNights;

/// <summary>
/// Handles <see cref="GetGameNightLiveQuery"/>. Loads the <c>GameNightEvent</c> (its child
/// <c>Sessions</c> are already Included by the repository) and projects it to the live read model,
/// ordering sessions by play order.
/// </summary>
internal sealed class GetGameNightLiveQueryHandler : IQueryHandler<GetGameNightLiveQuery, GameNightLiveDto>
{
    private readonly IGameNightEventRepository _repository;

    public GetGameNightLiveQueryHandler(IGameNightEventRepository repository)
    {
        _repository = repository ?? throw new ArgumentNullException(nameof(repository));
    }

    public async Task<GameNightLiveDto> Handle(GetGameNightLiveQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);

        var gameNight = await _repository.GetByIdAsync(query.GameNightId, cancellationToken).ConfigureAwait(false)
            ?? throw new NotFoundException("GameNightEvent", query.GameNightId.ToString());

        var sessions = gameNight.Sessions
            .OrderBy(s => s.PlayOrder)
            .Select(s => new GameNightSessionDto(
                s.SessionId,
                s.GameId,
                s.GameTitle,
                s.PlayOrder,
                s.Status,
                s.WinnerId,
                s.StartedAt,
                s.CompletedAt))
            .ToList();

        return new GameNightLiveDto(gameNight.Id, gameNight.Title, gameNight.Status, sessions);
    }
}
