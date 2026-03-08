using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Handler for GetGameAnalysisQuery — returns all active analyses for a game.
/// Issue #5454: Analysis results UI.
/// </summary>
internal sealed class GetGameAnalysisQueryHandler
    : IQueryHandler<GetGameAnalysisQuery, List<RulebookAnalysisDto>>
{
    private readonly IRulebookAnalysisRepository _repository;

    public GetGameAnalysisQueryHandler(IRulebookAnalysisRepository repository)
    {
        _repository = repository;
    }

    public async Task<List<RulebookAnalysisDto>> Handle(
        GetGameAnalysisQuery query,
        CancellationToken cancellationToken)
    {
        var analyses = await _repository.GetBySharedGameIdAsync(
            query.SharedGameId, cancellationToken).ConfigureAwait(false);

        return analyses
            .Where(a => a.IsActive)
            .Select(GetActiveRulebookAnalysisQueryHandler.MapToDto)
            .ToList();
    }
}
