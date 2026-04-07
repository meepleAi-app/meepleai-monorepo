using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.Infrastructure;

internal sealed class UpdateServiceConfigCommandValidator : AbstractValidator<UpdateServiceConfigCommand>
{
    public UpdateServiceConfigCommandValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required")
            .Must(ServiceRegistry.IsKnownService)
            .WithMessage("Unknown service name");

        RuleFor(x => x.Parameters)
            .NotEmpty()
            .WithMessage("At least one parameter is required");
    }
}
