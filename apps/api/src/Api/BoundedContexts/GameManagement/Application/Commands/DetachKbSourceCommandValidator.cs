using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class DetachKbSourceCommandValidator : AbstractValidator<DetachKbSourceCommand>
{
    public DetachKbSourceCommandValidator()
    {
        RuleFor(x => x.BookId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
    }
}
