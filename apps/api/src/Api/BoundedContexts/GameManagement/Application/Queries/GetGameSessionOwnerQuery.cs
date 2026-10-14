using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Queries;

/// <summary>
/// Who owns a game session, and on which game it is played (#4282).
/// </summary>
/// <param name="OwnerUserId">
/// The user who created the session; <c>null</c> for a legacy session created without one.
/// </param>
/// <param name="GameId">The shared game the session is played on.</param>
internal sealed record GameSessionOwnerDto(Guid? OwnerUserId, Guid GameId);

/// <summary>
/// The owner and the game of a game session, or <c>null</c> when it does not exist (#4282).
/// </summary>
/// <remarks>
/// For contexts that authorize an action on a game session they do not own, such as
/// KnowledgeBase launching a session agent: per ADR-090 they ask through <c>IMediator</c>
/// instead of reading the repository, and need only these two facts.
/// </remarks>
internal sealed record GetGameSessionOwnerQuery(Guid GameSessionId) : IQuery<GameSessionOwnerDto?>;

internal sealed class GetGameSessionOwnerQueryHandler : IQueryHandler<GetGameSessionOwnerQuery, GameSessionOwnerDto?>
{
    private readonly IGameSessionRepository _sessionRepository;

    public GetGameSessionOwnerQueryHandler(IGameSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<GameSessionOwnerDto?> Handle(GetGameSessionOwnerQuery query, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(query);
        var session = await _sessionRepository.GetByIdAsync(query.GameSessionId, cancellationToken).ConfigureAwait(false);
        return session is null ? null : new GameSessionOwnerDto(session.CreatedByUserId, session.GameId);
    }
}
