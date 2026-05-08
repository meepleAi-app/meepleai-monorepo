using Api.BoundedContexts.SessionTracking.Application.DTOs;
using MediatR;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

public sealed record BootstrapGamebookGlossaryCommand(
    Guid CampaignId,
    Guid CallerUserId) : IRequest<IReadOnlyList<GamebookGlossaryEntryDto>>;
