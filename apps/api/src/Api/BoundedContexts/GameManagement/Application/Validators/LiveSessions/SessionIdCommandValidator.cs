using Api.BoundedContexts.GameManagement.Application.Commands.LiveSessions;
using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Validators.LiveSessions;

/// <summary>
/// Validators for simple session ID-only commands (Start, Pause, Resume, Complete, Save, AdvanceTurn).
/// Issue #4749: CQRS validation for live sessions.
/// </summary>
internal sealed class StartLiveSessionCommandValidator : AbstractValidator<StartLiveSessionCommand>
{
    public StartLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
    }
}

internal sealed class PauseLiveSessionCommandValidator : AbstractValidator<PauseLiveSessionCommand>
{
    public PauseLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
    }
}

internal sealed class ResumeLiveSessionCommandValidator : AbstractValidator<ResumeLiveSessionCommand>
{
    public ResumeLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
    }
}

internal sealed class CompleteLiveSessionCommandValidator : AbstractValidator<CompleteLiveSessionCommand>
{
    public CompleteLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
    }
}

internal sealed class SaveLiveSessionCommandValidator : AbstractValidator<SaveLiveSessionCommand>
{
    public SaveLiveSessionCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
    }
}

internal sealed class AdvanceLiveSessionTurnCommandValidator : AbstractValidator<AdvanceLiveSessionTurnCommand>
{
    public AdvanceLiveSessionTurnCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("Session ID is required");
    }
}
