using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries;

/// <summary>
/// Query to get all active rulebook analyses for a game.
/// Issue #5454: Analysis results UI.
/// </summary>
internal record GetGameAnalysisQuery(Guid SharedGameId) : IQuery<List<RulebookAnalysisDto>>;
