using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.GameManagement.Application.Commands.GameNights;

/// <summary>Organiser revokes the summary share token — Issue #2702.</summary>
internal record RevokeGameNightShareTokenCommand(Guid GameNightId, Guid UserId) : ICommand;
