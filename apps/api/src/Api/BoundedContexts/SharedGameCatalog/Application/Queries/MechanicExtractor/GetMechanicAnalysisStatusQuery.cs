using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Loads the current status of an AI-generated mechanic analysis (ISSUE-524 / M1.2). Admin
/// clients poll this endpoint after receiving <c>202 Accepted</c> from
/// <c>POST /api/v1/admin/mechanic-analyses</c> to observe transitions out of <c>Draft</c>.
/// </summary>
/// <param name="AnalysisId">Primary key of the analysis to load.</param>
internal sealed record GetMechanicAnalysisStatusQuery(Guid AnalysisId)
    : IQuery<MechanicAnalysisStatusDto?>;
