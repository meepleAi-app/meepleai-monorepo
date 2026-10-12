using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

#pragma warning disable MA0048 // File name must match type name - command, validator and handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>Esegue un set di scenari con un modello, attraverso il gateway vero (spec §7.1).</summary>
internal sealed record RunSessionAssistantEvaluationCommand(
    AssistantScenarioSet Scenarios,
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    int MaxToolIterations) : ICommand<AssistantEvaluationReport>;

internal sealed class RunSessionAssistantEvaluationCommandValidator : AbstractValidator<RunSessionAssistantEvaluationCommand>
{
    public RunSessionAssistantEvaluationCommandValidator()
    {
        RuleFor(c => c.PrimaryModelId).NotEmpty();
        RuleFor(c => c.Scenarios).NotNull();
        // La cascata Stop è globale (ApplicationServiceExtensions): dopo un NotEmpty fallito il Must non gira.
        RuleFor(c => c.Scenarios.Scenarios).NotEmpty().Must(s => s.Count <= 200).When(c => c.Scenarios is not null);
        RuleForEach(c => c.Scenarios.Scenarios)
            .Custom((scenario, context) =>
            {
                if (AssistantScenarioRules.Problem(scenario) is { } problem)
                {
                    context.AddFailure(problem);
                }
            })
            .When(c => c.Scenarios?.Scenarios is not null);
        RuleFor(c => c.Temperature).InclusiveBetween(0, 2);
        RuleFor(c => c.MaxTokens).InclusiveBetween(64, 4000);
        RuleFor(c => c.MaxToolIterations).InclusiveBetween(1, 10);
    }
}

internal sealed class RunSessionAssistantEvaluationCommandHandler(SessionAssistantEvaluator evaluator)
    : ICommandHandler<RunSessionAssistantEvaluationCommand, AssistantEvaluationReport>
{
    public async Task<AssistantEvaluationReport> Handle(RunSessionAssistantEvaluationCommand command, CancellationToken cancellationToken)
    {
        var settings = new AssistantModelSettings(
            command.PrimaryModelId, command.FallbackModelId, command.Temperature, command.MaxTokens,
            command.MaxToolIterations, ProfileVersionId: null, RequestSource.Evaluation);

        var results = new List<AssistantScenarioResult>();
        foreach (var scenario in command.Scenarios.Scenarios)
        {
            results.Add(await evaluator.RunAsync(scenario, settings, cancellationToken).ConfigureAwait(false));
        }

        return AssistantEvaluationReport.From(command.PrimaryModelId, results);
    }
}
