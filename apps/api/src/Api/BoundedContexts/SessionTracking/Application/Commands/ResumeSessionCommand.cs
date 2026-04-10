using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Commands;

/// <summary>
/// Session Flow v2.1 — T5.
/// Resumes a Paused session. Only the session owner can resume.
/// Auto-pauses every other Active session inside the same GameNight envelope to
/// preserve the "1 Active session per GameNight" invariant at handler level
/// (complementing the partial unique index introduced in T1).
/// </summary>
public sealed record ResumeSessionCommand(Guid SessionId, Guid UserId) : ICommand;

public sealed class ResumeSessionCommandValidator : AbstractValidator<ResumeSessionCommand>
{
    public ResumeSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty();
        RuleFor(x => x.UserId).NotEmpty();
    }
}
