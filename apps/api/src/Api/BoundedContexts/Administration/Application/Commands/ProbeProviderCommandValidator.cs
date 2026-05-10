using FluentValidation;

namespace Api.BoundedContexts.Administration.Application.Commands;

internal sealed class ProbeProviderCommandValidator : AbstractValidator<ProbeProviderCommand>
{
    private static readonly HashSet<string> AllowedProviders = new(StringComparer.OrdinalIgnoreCase)
    { "openrouter", "openai", "deepseek", "ollama" };

    public ProbeProviderCommandValidator()
    {
        RuleFor(c => c.ProviderName)
            .NotEmpty()
            .Must(name => AllowedProviders.Contains(name))
            .WithMessage("Unknown provider");
        RuleFor(c => c.ActorId).NotEmpty();
    }
}
