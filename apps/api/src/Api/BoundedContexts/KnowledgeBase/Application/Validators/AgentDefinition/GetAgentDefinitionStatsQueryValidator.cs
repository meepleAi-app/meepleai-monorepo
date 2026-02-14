using Api.BoundedContexts.KnowledgeBase.Application.Queries.AgentDefinition;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators.AgentDefinition;

/// <summary>
/// Validator for GetAgentDefinitionStatsQuery.
/// Issue #3708: Validates stats query parameters.
/// </summary>
internal sealed class GetAgentDefinitionStatsQueryValidator : AbstractValidator<GetAgentDefinitionStatsQuery>
{
    public GetAgentDefinitionStatsQueryValidator()
    {
        // ActiveOnly is boolean, no validation needed (always valid)
    }
}
