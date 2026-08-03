using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>Boundary validation for <see cref="RemoveCoverAssignmentCommand"/>.</summary>
internal sealed class RemoveCoverAssignmentCommandValidator : AbstractValidator<RemoveCoverAssignmentCommand>
{
    public RemoveCoverAssignmentCommandValidator()
    {
        RuleFor(x => x.GameId).NotEqual(Guid.Empty).WithMessage("GameId is required");
        RuleFor(x => x.AdminId).NotEqual(Guid.Empty).WithMessage("AdminId is required");
        RuleFor(x => x.Context).IsInEnum().WithMessage("Context must be a defined CoverContext");
    }
}
