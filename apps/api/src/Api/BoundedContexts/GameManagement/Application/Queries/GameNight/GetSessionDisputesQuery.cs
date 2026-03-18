using Api.BoundedContexts.GameManagement.Domain.Repositories;
using Api.BoundedContexts.GameManagement.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using MediatR;

namespace Api.BoundedContexts.GameManagement.Application.Queries.GameNight;

/// <summary>
/// Returns all rule dispute entries for a given live game session.
/// Game Night Improvvisata — E3: Arbitro history.
/// </summary>
/// <param name="SessionId">The live session to retrieve disputes for.</param>
public sealed record GetSessionDisputesQuery(Guid SessionId) : IRequest<GetSessionDisputesResult>;

/// <summary>
/// Result carrying the dispute list for a session.
/// </summary>
/// <param name="SessionId">The queried session.</param>
/// <param name="Disputes">All disputes recorded in the session, ordered by timestamp ascending.</param>
public sealed record GetSessionDisputesResult(
    Guid SessionId,
    IReadOnlyList<DisputeSummary> Disputes);

/// <summary>
/// Lightweight summary of a single rule dispute for client consumption.
/// </summary>
public sealed record DisputeSummary(
    Guid Id,
    string Description,
    string Verdict,
    List<string> RuleReferences,
    string RaisedByPlayerName,
    DateTime Timestamp);

/// <summary>
/// Handles <see cref="GetSessionDisputesQuery"/>.
/// Loads the live session from the in-memory repository and projects its dispute collection.
/// </summary>
internal sealed class GetSessionDisputesQueryHandler
    : IRequestHandler<GetSessionDisputesQuery, GetSessionDisputesResult>
{
    private readonly ILiveSessionRepository _sessionRepository;

    public GetSessionDisputesQueryHandler(ILiveSessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<GetSessionDisputesResult> Handle(
        GetSessionDisputesQuery request,
        CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);

        var session = await _sessionRepository
            .GetByIdAsync(request.SessionId, cancellationToken)
            .ConfigureAwait(false)
            ?? throw new NotFoundException("LiveGameSession", request.SessionId.ToString());

        var disputes = session.Disputes
            .OrderBy(d => d.Timestamp)
            .Select(d => new DisputeSummary(
                Id: d.Id,
                Description: d.Description,
                Verdict: d.Verdict,
                RuleReferences: d.RuleReferences,
                RaisedByPlayerName: d.RaisedByPlayerName,
                Timestamp: d.Timestamp))
            .ToList();

        return new GetSessionDisputesResult(request.SessionId, disputes);
    }
}
