using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class SoftDeleteGameBookCommandValidator : AbstractValidator<SoftDeleteGameBookCommand>
{
    public SoftDeleteGameBookCommandValidator()
    {
        RuleFor(x => x.BookId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
    }
}
