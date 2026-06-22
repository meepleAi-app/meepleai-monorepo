using FluentValidation;

namespace Api.BoundedContexts.GameManagement.Application.Commands;

internal sealed class AttachKbSourceCommandValidator : AbstractValidator<AttachKbSourceCommand>
{
    public AttachKbSourceCommandValidator()
    {
        RuleFor(x => x.BookId).NotEqual(Guid.Empty);
        RuleFor(x => x.PdfDocId).NotEqual(Guid.Empty);
        RuleFor(x => x.RequestedBy).NotEqual(Guid.Empty);
    }
}
