using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Queries.MechanicExtractor;

/// <summary>
/// Returns the current Mechanic Extractor prompt (system + per-section) so an admin can inspect
/// what the pipeline sends to the LLM (#539 follow-up). Read-only; sourced from
/// <c>IMechanicPromptProvider</c>, no DB access, no parameters.
/// </summary>
internal sealed record GetMechanicPromptQuery : IQuery<MechanicPromptDto>;
