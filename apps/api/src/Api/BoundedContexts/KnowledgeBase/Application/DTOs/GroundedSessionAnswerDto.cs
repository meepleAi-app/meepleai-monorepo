using Api.SharedKernel.Domain.Enums;

namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

/// <summary>
/// #3390 Slice 2 — public cross-BC result of a non-streaming grounded answer for the
/// in-session agent. Returned by <see cref="Api.BoundedContexts.KnowledgeBase.Application.Queries.AskGroundedSessionQuery"/>
/// and consumed by SessionTracking's image/text agent path via <c>IMediator</c>.
///
/// This is the Published Language boundary: SessionTracking receives this DTO — NOT the
/// KnowledgeBase-internal <c>AssembledPrompt</c>/<c>ChunkCitation</c> models (DDD: no shared
/// internal models between bounded contexts). <see cref="Citations"/> reuses the same
/// <see cref="CitationDto"/> wire shape the SSE text path already emits, so the frontend
/// citation mapper is reused unchanged.
/// </summary>
internal sealed record GroundedSessionAnswerDto(
    string Answer,
    IReadOnlyList<Api.Models.CitationDto> Citations,
    GroundingStatus GroundingStatus,
    double? Confidence);
