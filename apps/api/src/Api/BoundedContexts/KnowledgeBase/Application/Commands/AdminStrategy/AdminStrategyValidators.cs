using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Commands.AdminStrategy;

/// <summary>
/// Issue #5314: Validators for Admin Strategy commands.
/// </summary>
public sealed class CreateAdminStrategyValidator : AbstractValidator<CreateAdminStrategyCommand>
{
    public CreateAdminStrategyValidator()
    {
        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Strategy name is required.")
            .MinimumLength(3).WithMessage("Strategy name must be at least 3 characters.")
            .MaximumLength(100).WithMessage("Strategy name must not exceed 100 characters.");

        RuleFor(x => x.StepsJson)
            .NotEmpty().WithMessage("Steps are required.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("Admin user ID is required.");
    }
}

public sealed class UpdateAdminStrategyValidator : AbstractValidator<UpdateAdminStrategyCommand>
{
    public UpdateAdminStrategyValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty().WithMessage("Strategy ID is required.");

        RuleFor(x => x.Name)
            .NotEmpty().WithMessage("Strategy name is required.")
            .MinimumLength(3).WithMessage("Strategy name must be at least 3 characters.")
            .MaximumLength(100).WithMessage("Strategy name must not exceed 100 characters.");

        RuleFor(x => x.StepsJson)
            .NotEmpty().WithMessage("Steps are required.");

        RuleFor(x => x.AdminUserId)
            .NotEmpty().WithMessage("Admin user ID is required.");
    }
}
