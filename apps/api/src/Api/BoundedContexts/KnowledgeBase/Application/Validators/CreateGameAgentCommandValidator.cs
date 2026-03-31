using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.KnowledgeBase.Application.Validators;

/// <summary>
/// Validator for CreateGameAgentCommand.
/// Issue #5: Backend Agent Creation API.
/// </summary>
internal class CreateGameAgentCommandValidator : AbstractValidator<CreateGameAgentCommand>
{
    public CreateGameAgentCommandValidator()
    {
        RuleFor(x => x.GameId)
            .NotEmpty()
            .WithMessage("Game ID is required");

        RuleFor(x => x.AgentDefinitionId)
            .NotEmpty()
            .WithMessage("Agent Definition ID is required");

        RuleFor(x => x.StrategyName)
            .NotEmpty()
            .WithMessage("Strategy name is required")
            .Must(BeValidStrategy)
            .WithMessage("Invalid RAG strategy. Valid values: Fast, Balanced, Precise, Expert, Consensus, SentenceWindow, Iterative, Custom, MultiAgent, StepBack, QueryExpansion, RagFusion");

        RuleFor(x => x.UserId)
            .NotEmpty()
            .WithMessage("User ID is required");

        RuleFor(x => x.StrategyParameters)
            .Must(BeValidJsonOrNull)
            .WithMessage("Strategy parameters must be valid JSON or null");
    }

    private static bool BeValidStrategy(string strategyName)
    {
        return RagStrategyExtensions.TryParse(strategyName, out _);
    }

    private static bool BeValidJsonOrNull(string? json)
    {
        if (string.IsNullOrWhiteSpace(json))
            return true;

        try
        {
            System.Text.Json.JsonDocument.Parse(json);
            return true;
        }
        catch
        {
            return false;
        }
    }
}
