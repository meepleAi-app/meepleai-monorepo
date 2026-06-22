using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record UpsertGlossaryEntryCommand(
    Guid CampaignId,
    Guid EntryId,
    string TermEn,
    string TermIt,
    Guid CallerUserId) : IRequest<GamebookGlossaryEntryDto>;
