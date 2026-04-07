using Api.BoundedContexts.Administration.Application.Commands.Infrastructure;
using Api.BoundedContexts.Administration.Domain.ValueObjects;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators.Infrastructure;

internal sealed class RestartInfraServiceCommandValidator : AbstractValidator<RestartInfraServiceCommand>
{
    public RestartInfraServiceCommandValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required")
            .Must(ServiceRegistry.IsKnownService)
            .WithMessage("Unknown service name. Valid services: " +
                string.Join(", ", ServiceRegistry.AllServiceNames));
    }
}
