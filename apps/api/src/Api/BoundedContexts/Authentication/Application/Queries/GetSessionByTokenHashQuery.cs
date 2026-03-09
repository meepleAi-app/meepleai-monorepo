using Api.BoundedContexts.Authentication.Domain.Entities;
using Api.BoundedContexts.Authentication.Infrastructure.Persistence;
using Api.SharedKernel.Application.Interfaces;

#pragma warning disable MA0048 // File name must match type name - Contains Query with Handler
namespace Api.BoundedContexts.Authentication.Application.Queries;

/// <summary>
/// Query to look up a session by its token hash.
/// Used by session-extend endpoint to resolve session ID from cookie token.
/// </summary>
internal record GetSessionByTokenHashQuery(string TokenHash) : IQuery<Session?>;

internal class GetSessionByTokenHashQueryHandler : IQueryHandler<GetSessionByTokenHashQuery, Session?>
{
    private readonly ISessionRepository _sessionRepository;

    public GetSessionByTokenHashQueryHandler(ISessionRepository sessionRepository)
    {
        _sessionRepository = sessionRepository ?? throw new ArgumentNullException(nameof(sessionRepository));
    }

    public async Task<Session?> Handle(GetSessionByTokenHashQuery request, CancellationToken cancellationToken)
    {
        ArgumentNullException.ThrowIfNull(request);
        return await _sessionRepository.GetByTokenHashAsync(request.TokenHash, cancellationToken).ConfigureAwait(false);
    }
}
