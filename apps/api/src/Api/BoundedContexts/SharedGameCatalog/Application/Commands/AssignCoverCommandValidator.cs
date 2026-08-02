using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Boundary validation for <see cref="AssignCoverCommand"/> — turns bad input into a
/// 400 rather than letting the domain factory throw a 500. Mirrors the domain guards
/// on <c>GameCoverAssignment.Create</c> (focal in [0,1], defined enums).
/// </summary>
internal sealed class AssignCoverCommandValidator : AbstractValidator<AssignCoverCommand>
{
    public AssignCoverCommandValidator()
    {
        RuleFor(x => x.GameId).NotEqual(Guid.Empty).WithMessage("GameId is required");
        RuleFor(x => x.AdminId).NotEqual(Guid.Empty).WithMessage("AdminId is required");
        RuleFor(x => x.Context).IsInEnum().WithMessage("Context must be a defined CoverContext");
        RuleFor(x => x.Source).IsInEnum().WithMessage("Source must be a defined CoverAssignmentSource");
        RuleFor(x => x.FocalX).InclusiveBetween(0.0, 1.0).WithMessage("FocalX must be in [0, 1]");
        RuleFor(x => x.FocalY).InclusiveBetween(0.0, 1.0).WithMessage("FocalY must be in [0, 1]");
    }
}
