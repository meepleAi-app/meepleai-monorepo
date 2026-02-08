using Api.BoundedContexts.Administration.Application.Commands.Operations;
using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Validators;

/// <summary>
/// Validator for RestartServiceCommand.
/// Issue #3696: Operations - Service Control Panel.
/// </summary>
internal sealed class RestartServiceCommandValidator : AbstractValidator<RestartServiceCommand>
{
    private static readonly string[] AllowedServices = { "API" };

    public RestartServiceCommandValidator()
    {
        RuleFor(x => x.ServiceName)
            .NotEmpty()
            .WithMessage("Service name is required");

        RuleFor(x => x.ServiceName)
            .Must(name => AllowedServices.Contains(name, StringComparer.OrdinalIgnoreCase))
            .WithMessage($"Invalid service name. Allowed values: {string.Join(", ", AllowedServices)}");

        RuleFor(x => x.AdminUserId)
            .NotEmpty()
            .WithMessage("Admin user ID is required");
    }
}
