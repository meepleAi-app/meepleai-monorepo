using MediatR;

namespace Api.BoundedContexts.KbQuality.Application.Queries.GetEvaluation;

/// <summary>
/// Fetches a single <see cref="Domain.Evaluation.DocumentEvaluationRun"/> projection
/// for the admin UI (#1675 Task 15). The (<paramref name="DocId"/>, <paramref name="EvaluationId"/>)
/// pair is intentional: the endpoint is nested under <c>/admin/kb/docs/{docId}</c>, so the
/// handler verifies the eval belongs to that doc before returning the projection (defense in
/// depth against cross-doc id leakage). Returns <c>null</c> when no match exists; the
/// endpoint maps that to <c>404 Not Found</c>.
/// </summary>
public sealed record GetEvaluationQuery(Guid DocId, Guid EvaluationId) : IRequest<EvaluationDetailDto?>;
