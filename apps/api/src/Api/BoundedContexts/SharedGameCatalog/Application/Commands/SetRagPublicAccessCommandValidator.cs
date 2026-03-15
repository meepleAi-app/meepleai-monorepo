using FluentValidation;

namespace Api.BoundedContexts.SharedGameCatalog.Application.Commands;

/// <summary>
/// Validator for SetRagPublicAccessCommand.
/// </summary>
internal class SetRagPublicAccessCommandValidator : AbstractValidator<SetRagPublicAccessCommand>
{
    public SetRagPublicAccessCommandValidator()
    {
        RuleFor(x => x.SharedGameId)
            .NotEmpty()
            .WithMessage("SharedGameId is required");
    }
}
