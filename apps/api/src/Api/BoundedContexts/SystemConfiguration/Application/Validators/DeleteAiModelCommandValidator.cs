using Api.BoundedContexts.SystemConfiguration.Application.Commands;
using FluentValidation;

namespace Api.BoundedContexts.SystemConfiguration.Application.Validators;

internal sealed class DeleteAiModelCommandValidator : AbstractValidator<DeleteAiModelCommand>
{
    public DeleteAiModelCommandValidator()
    {
        RuleFor(x => x.Id)
            .NotEmpty()
            .WithMessage("Id is required");
    }
}
