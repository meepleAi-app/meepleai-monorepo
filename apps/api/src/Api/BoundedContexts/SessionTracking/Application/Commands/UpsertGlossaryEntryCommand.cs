using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record UpsertGlossaryEntryCommand(
    Guid CampaignId,
    Guid EntryId,
    string TermEn,
    string TermIt,
    Guid CallerUserId,
    // #2638 / SI-7: null = leave existing contexts unchanged; non-null = full-set replace.
    IReadOnlyList<GlossaryContextDto>? Contexts = null) : IRequest<GamebookGlossaryEntryDto>;
