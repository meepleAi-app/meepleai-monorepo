using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — T5.
/// Pauses an Active session. Only the session owner can pause.
/// Emits a <c>session_paused</c> diary event scoped to the parent GameNight (if any).
/// </summary>
public sealed record PauseSessionCommand(Guid SessionId, Guid UserId) : ICommand;

public sealed class PauseSessionCommandValidator : AbstractValidator<PauseSessionCommand>
{
    public PauseSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
