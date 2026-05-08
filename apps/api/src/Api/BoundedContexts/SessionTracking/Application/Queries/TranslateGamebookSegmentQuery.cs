using Api.BoundedContexts.SessionTracking.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

public sealed record TranslateGamebookSegmentQuery(
    Guid CampaignId,
    Guid PhotoId,
    int ParagraphNumber,
    Guid CallerUserId) : IStreamingQuery<TranslateChunk>;
