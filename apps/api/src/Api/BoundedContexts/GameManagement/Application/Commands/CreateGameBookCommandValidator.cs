using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class CreateGameBookCommandValidator : AbstractValidator<CreateGameBookCommand>
{
    public CreateGameBookCommandValidator()
    {
        RuleFor(x => x.GameRef).NotNull();
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Roles).GreaterThan(0).WithMessage("at least one role required");
        RuleFor(x => x.Language).NotEmpty().Length(2);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
        RuleFor(x => x).Must(c => !(c.PhysicalOnly && c.KbSourceDocId.HasValue))
                       .WithMessage("physicalOnly=true is incompatible with kbSourceDocId");
    }
}
