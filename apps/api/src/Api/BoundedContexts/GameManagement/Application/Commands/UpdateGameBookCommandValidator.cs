using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class UpdateGameBookCommandValidator : AbstractValidator<UpdateGameBookCommand>
{
    public UpdateGameBookCommandValidator()
    {
        RuleFor(x => x.BookId).NotEqual(Guid.Empty);
        RuleFor(x => x.DisplayName).NotEmpty().MaximumLength(120);
        RuleFor(x => x.Roles).GreaterThan(0);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
    }
}
