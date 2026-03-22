using Api.BoundedContexts.Authentication.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.Authentication.Application.Commands.Sessions;

/// <summary>
/// Validates ExtendSessionCommand.
/// Ensures session ID and requesting user ID are provided, and extension duration is reasonable.
/// </summary>
internal sealed class ExtendSessionCommandValidator : AbstractValidator<ExtendSessionCommand>
{
    private static readonly TimeSpan MaxExtension = TimeSpan.FromDays(90);

    public ExtendSessionCommandValidator()
    {
        RuleFor(x => x.SessionId)
            .NotEmpty()
            .WithMessage("Session ID is required");

        RuleFor(x => x.RequestingUserId)
            .NotEmpty()
            .WithMessage("Requesting user ID is required");

        RuleFor(x => x.ExtensionDuration)
            .Must(duration => !duration.HasValue || (duration.Value > TimeSpan.Zero && duration.Value <= MaxExtension))
            .WithMessage($"Extension duration must be between 0 and {MaxExtension.TotalDays} days");
    }
}
