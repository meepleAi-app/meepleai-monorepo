using FluentValidation;

namespace Api.BoundedContexts.SessionTracking.Application.Queries;

internal sealed class GenerateInviteTokenCommandValidator : AbstractValidator<GenerateInviteTokenCommand>
{
    public GenerateInviteTokenCommandValidator()
    {
        RuleFor(x => x.SessionId).NotEmpty().WithMessage("SessionId is required");
        RuleFor(x => x.RequestedBy).NotEmpty().WithMessage("RequestedBy is required");
        RuleFor(x => x.ExpiresInHours)
            .GreaterThan(0).When(x => x.ExpiresInHours.HasValue)
            .WithMessage("ExpiresInHours must be greater than 0");
    }
}

internal sealed class JoinSessionByInviteCommandValidator : AbstractValidator<JoinSessionByInviteCommand>
{
    public JoinSessionByInviteCommandValidator()
    {
        RuleFor(x => x.InviteToken).NotEmpty().MaximumLength(500)
            .WithMessage("InviteToken is required and must be at most 500 characters");
        RuleFor(x => x.UserId).NotEmpty().WithMessage("UserId is required");
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(100)
            .WithMessage("DisplayName is required and must be at most 100 characters");
    }
}
