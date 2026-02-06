using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands.TokenManagement;

/// <summary>
/// Command to update tier token limits (Issue #3692)
/// </summary>
public sealed record UpdateTierLimitsCommand(
    string Tier,
    int TokensPerMonth) : ICommand;

public sealed class UpdateTierLimitsCommandValidator : AbstractValidator<UpdateTierLimitsCommand>
{
    public UpdateTierLimitsCommandValidator()
    {
        RuleFor(x => x.Tier)
            .NotEmpty()
            .Must(t => t is "Free" or "Basic" or "Pro" or "Enterprise")
            .WithMessage("Tier must be one of: Free, Basic, Pro, Enterprise");

        RuleFor(x => x.TokensPerMonth)
            .GreaterThan(0)
            .WithMessage("Tokens per month must be positive");
    }
}
