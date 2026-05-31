using Api.BoundedContexts.GameToolkit.Application.DTOs;
using Api.BoundedContexts.GameToolkit.Domain.Enums;
using FluentValidation;

namespace Api.BoundedContexts.GameToolkit.Application.Validators;

/// <summary>
/// Validates AI-generated toolkit suggestion output for structural integrity
/// BEFORE it reaches ApplyAiToolkitSuggestionCommand. Catches common LLM mistakes
/// surfaced by the 2026-05-31 spike (claudedocs/2026-05-31-spike-toolkit-ai-generation.md):
///
/// - Points-as-counter hallucination (no counter named "Points"/"Score"/"VP")
/// - Dummy TimerTools with DurationSeconds=0 (use empty array instead)
/// - Overrides omission (all 3 booleans must be present, even if false)
/// - Counter MinValue/MaxValue invariants
/// - Custom dice without CustomFaces
///
/// Issue #1747 (B19-3c).
///
/// Use:
///   var result = await validator.ValidateAsync(suggestion);
///   if (!result.IsValid) { /* log + force RequiresHumanReview=true + retry */ }
/// </summary>
internal sealed class AiToolkitSuggestionValidator : AbstractValidator<AiToolkitSuggestionDto>
{
    // Tokens that should NEVER appear as counter names — they are end-game derived score
    private static readonly string[] ForbiddenCounterNameTokens =
    [
        "point", "points",
        "score", "scores",
        "vp", "victory point", "victory points",
    ];

    public AiToolkitSuggestionValidator()
    {
        RuleFor(s => s.ToolkitName)
            .NotEmpty().WithMessage("ToolkitName is required (LLM produced empty/null).");

        RuleFor(s => s.DiceTools)
            .NotNull().WithMessage("DiceTools must be an array (use [] if no dice).");

        RuleFor(s => s.CounterTools)
            .NotNull().WithMessage("CounterTools must be an array (use [] if no counters).");

        RuleFor(s => s.TimerTools)
            .NotNull().WithMessage("TimerTools must be an array (use [] if no timer).");

        RuleFor(s => s.Reasoning)
            .NotEmpty().WithMessage("Reasoning is required (LLM must cite rule excerpts).");

        // Overrides must always be provided (verdict spike: LLMs frequently omit this)
        RuleFor(s => s.Overrides)
            .NotNull().WithMessage(
                "Overrides MUST be provided (use {OverridesTurnOrder:false,OverridesScoreboard:false,OverridesDiceSet:false} if no game customisations).");

        // No "Points" counter — Points are derived at game end, not tracked
        RuleForEach(s => s.CounterTools)
            .ChildRules(c =>
            {
                c.RuleFor(t => t.Name)
                    .Must(n => !ContainsForbiddenScoreToken(n))
                    .WithMessage(
                        "Counter name '{PropertyValue}' looks like end-game score (Points/Score/VP). Score is DERIVED at game end, never tracked as a running counter.");

                c.RuleFor(t => t.MinValue)
                    .LessThanOrEqualTo(t => t.MaxValue)
                    .WithMessage("CounterTool MinValue must be <= MaxValue.");

                c.RuleFor(t => t.DefaultValue)
                    .GreaterThanOrEqualTo(t => t.MinValue)
                    .LessThanOrEqualTo(t => t.MaxValue)
                    .WithMessage("CounterTool DefaultValue must be within [MinValue, MaxValue].");
            });

        // Custom dice must declare faces
        RuleForEach(s => s.DiceTools)
            .ChildRules(d =>
            {
                d.RuleFor(t => t.CustomFaces)
                    .NotNull()
                    .Must(faces => faces is not null && faces.Length > 0)
                    .When(t => t.DiceType == DiceType.Custom)
                    .WithMessage(
                        "Custom DiceType requires CustomFaces array with at least 1 face (LLM declared Custom but omitted faces).");

                d.RuleFor(t => t.Quantity)
                    .GreaterThan(0)
                    .WithMessage("DiceTool Quantity must be > 0.");
            });

        // Timer entries must have positive duration unless explicitly using Chess pattern
        RuleForEach(s => s.TimerTools)
            .ChildRules(t =>
            {
                t.RuleFor(x => x.DurationSeconds)
                    .GreaterThan(0)
                    .When(x => x.TimerType != TimerType.Chess)
                    .WithMessage(
                        "Timer DurationSeconds must be > 0. If the game has no timer, output an empty TimerTools array instead of a dummy entry.");
            });

        // ExcludedTools entries must have both ToolType and Reason
        When(s => s.ExcludedTools is not null && s.ExcludedTools.Count > 0, () =>
        {
            RuleForEach(s => s.ExcludedTools!)
                .ChildRules(e =>
                {
                    e.RuleFor(x => x.ToolType)
                        .NotEmpty()
                        .WithMessage("ExcludedTool.ToolType is required.");
                    e.RuleFor(x => x.Reason)
                        .NotEmpty()
                        .WithMessage("ExcludedTool.Reason is required (cite the rule that justifies exclusion).");
                });
        });
    }

    private static bool ContainsForbiddenScoreToken(string? name)
    {
        if (string.IsNullOrWhiteSpace(name))
        {
            return false;
        }
        var normalized = name.Trim().ToLowerInvariant();
        // Exact whole-word or starts-with match — avoid false positives on e.g. "Action points"? Yes —
        // "action points" should also be flagged because action points are derived.
        // But "Stone points" or "Build points" could be legitimate per-game currency.
        // Pragmatic: reject only when the name is dominated by the forbidden token.
        return ForbiddenCounterNameTokens.Any(token =>
            string.Equals(normalized, token, StringComparison.Ordinal)
            || normalized.EndsWith(' ' + token, StringComparison.Ordinal)
            || normalized.StartsWith(token + ' ', StringComparison.Ordinal));
    }
}
