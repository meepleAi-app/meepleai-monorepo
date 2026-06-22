using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Queries;

/// <summary>
/// Validator for <see cref="GetAllAgentsQuery"/> (BE-2 #1589).
/// Only constrains the scope fields; the legacy global fields stay unconstrained
/// so existing callers (global GET /agents, GET /games/{id}/agents) are unaffected.
/// </summary>
internal sealed class GetAllAgentsQueryValidator : AbstractValidator<GetAllAgentsQuery>
{
    private static readonly string[] AllowedScopes = ["my-library"];

    public GetAllAgentsQueryValidator()
    {
        RuleFor(x => x.Scope)
            .Must(s => s is null || AllowedScopes.Contains(s, StringComparer.Ordinal))
            .WithMessage($"Scope must be one of: {string.Join(", ", AllowedScopes)} (or null for global).");

        RuleFor(x => x.ScopeUserId)
            .Must(id => id.HasValue && id.Value != Guid.Empty)
            .When(x => string.Equals(x.Scope, "my-library", StringComparison.Ordinal))
            .WithMessage("ScopeUserId is required (non-empty) when Scope is 'my-library'.");
    }
}
