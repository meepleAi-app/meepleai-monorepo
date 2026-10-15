using Api.Services;
using Api.SharedKernel.Application.Interfaces;
using FluentValidation;

#pragma warning disable MA0048 // File name must match type name - command, validator and handler
namespace Api.BoundedContexts.KnowledgeBase.Application.SessionAssistant.Evaluation;

/// <summary>
/// Esegue un set di scenari con un modello, attraverso il gateway vero (spec §7.1). Con <c>OfferRules</c>
/// falso <c>cerca_regole</c> non viene offerto: sono gli strumenti del turno finché la fetta A5 non lo
/// consegna (#4270).
/// </summary>
internal sealed record RunSessionAssistantEvaluationCommand(
    AssistantScenarioSet Scenarios,
    string PrimaryModelId,
    string? FallbackModelId,
    double Temperature,
    int MaxTokens,
    int MaxToolIterations,
    AssistantPromptOverride? Prompts = null,
    bool OfferRules = true) : ICommand<AssistantEvaluationReport>;

/// <summary>
/// Un prompt alternativo da misurare al posto di quello del codice (#4256). Una lingua lasciata a
/// <c>null</c> usa il prompt predefinito.
/// </summary>
internal sealed record AssistantPromptOverride(string? Italian, string? English);

internal sealed class RunSessionAssistantEvaluationCommandValidator : AbstractValidator<RunSessionAssistantEvaluationCommand>
{
    internal const int MaxPromptLength = 8000;

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
        When(c => c.Prompts is not null, () =>
        {
            RuleFor(c => c.Prompts!)
                .Must(p => !string.IsNullOrWhiteSpace(p.Italian) || !string.IsNullOrWhiteSpace(p.English))
                .WithMessage("'prompts' needs a prompt for at least one language.");
            RuleFor(c => c.Prompts!.Italian).Must(BeUsablePrompt)
                .WithMessage($"'prompts.italian' must not be blank or longer than {MaxPromptLength} characters.");
            RuleFor(c => c.Prompts!.English).Must(BeUsablePrompt)
                .WithMessage($"'prompts.english' must not be blank or longer than {MaxPromptLength} characters.");
        });
        RuleFor(c => c.Temperature).InclusiveBetween(0, 2);
        RuleFor(c => c.MaxTokens).InclusiveBetween(64, 4000);
        RuleFor(c => c.MaxToolIterations).InclusiveBetween(1, 10);
    }

    private static bool BeUsablePrompt(string? prompt) =>
        prompt is null || (!string.IsNullOrWhiteSpace(prompt) && prompt.Length <= MaxPromptLength);
}

internal sealed class RunSessionAssistantEvaluationCommandHandler(SessionAssistantEvaluator evaluator)
    : ICommandHandler<RunSessionAssistantEvaluationCommand, AssistantEvaluationReport>
{
    public async Task<AssistantEvaluationReport> Handle(RunSessionAssistantEvaluationCommand command, CancellationToken cancellationToken)
    {
        var settings = new AssistantModelSettings(
            command.PrimaryModelId, command.FallbackModelId, command.Temperature, command.MaxTokens,
            command.MaxToolIterations, ProfileVersionId: null, RequestSource.Evaluation);

        var prompts = SessionAssistantPrompts.Default.With(command.Prompts?.Italian, command.Prompts?.English);
        var results = new List<AssistantScenarioResult>();
        foreach (var scenario in command.Scenarios.Scenarios)
        {
            results.Add(await evaluator.RunAsync(scenario, settings, prompts, command.OfferRules, cancellationToken).ConfigureAwait(false));
        }

        return AssistantEvaluationReport.From(command.PrimaryModelId, results, prompts);
    }
}
