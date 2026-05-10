using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal sealed class ProbeProviderCommandValidator : AbstractValidator<ProbeProviderCommand>
{
    // Allowlist of known providers is enforced at runtime by ProviderProbeExecutorFactory
    // (single source of truth) → unknown names map to 404 via UnknownProviderException.
    // The validator here only enforces format/shape constraints.
    public ProbeProviderCommandValidator()
    {
        RuleFor(c => c.ProviderName).NotEmpty().MaximumLength(64);
        RuleFor(c => c.ActorId).NotEmpty();
    }
}
