using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

/// <summary>
/// Rejects one mechanic analysis that is still in <c>Draft</c> because its pipeline never finished (#4503). Dispatched
/// once per id by <see cref="ExpireStaleMechanicAnalysisDraftsCommand"/>; it reloads the aggregate fresh and answers
/// 404 / 409 when the row is gone or has moved on, which the batch treats as "nothing to do".
/// </summary>
internal sealed record ExpireStaleMechanicAnalysisDraftCommand(Guid AnalysisId) : ICommand;
