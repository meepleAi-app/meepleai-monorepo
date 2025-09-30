namespace Api.Models;

// RuleSpec v0 - Formal specification for normalized board game rules

public record RuleSpecV0(
    string GameId,
    string Version,
    RuleSpecMetadata Metadata,
    GameSetup Setup,
    IReadOnlyList<GamePhase> Phases,
    IReadOnlyList<GameAction> Actions,
    ScoringRules Scoring,
    IReadOnlyList<EndCondition> EndConditions,
    IReadOnlyList<EdgeCase>? EdgeCases = null,
    IReadOnlyList<GlossaryTerm>? Glossary = null
);

public record RuleSpecMetadata(
    string Name,
    DateTime CreatedAt,
    string? Description = null,
    DateTime? UpdatedAt = null,
    PlayerCountRange? PlayerCount = null,
    PlayTimeRange? PlayTime = null,
    AgeRange? AgeRange = null
);

public record PlayerCountRange(int Min, int Max);

public record PlayTimeRange(int Min, int Max);

public record AgeRange(int Min);

public record GameSetup(
    IReadOnlyList<SetupStep> Steps,
    string? Description = null,
    IReadOnlyList<GameComponent>? Components = null
);

public record SetupStep(
    string Id,
    string Text,
    int? Order = null,
    IReadOnlyList<string>? Components = null
);

public record GameComponent(
    string Id,
    string Name,
    int Quantity,
    string? Description = null
);

public record GamePhase(
    string Id,
    string Name,
    string? Description = null,
    int? Order = null,
    IReadOnlyList<PhaseStep>? Steps = null,
    IReadOnlyList<string>? AllowedActions = null
);

public record PhaseStep(
    string Id,
    string Text,
    int? Order = null,
    bool Optional = false
);

public record GameAction(
    string Id,
    string Name,
    ActionType Type,
    string? Description = null,
    IReadOnlyList<string>? Prerequisites = null,
    IReadOnlyList<string>? Effects = null,
    ActionCost? Cost = null
);

public enum ActionType
{
    Mandatory,
    Optional,
    Triggered
}

public record ActionCost(
    Dictionary<string, int>? Resources = null,
    string? Description = null
);

public record ScoringRules(
    ScoringMethod Method,
    string? Description = null,
    IReadOnlyList<ScoringSource>? Sources = null,
    IReadOnlyList<Tiebreaker>? Tiebreakers = null
);

public enum ScoringMethod
{
    Points,
    Elimination,
    Objective,
    Hybrid
}

public record ScoringSource(
    string Id,
    string Name,
    int Value,
    string? Description = null,
    string? When = null
);

public record Tiebreaker(
    int Order,
    string Rule
);

public record EndCondition(
    string Id,
    EndConditionType Type,
    string Description,
    int? Value = null
);

public enum EndConditionType
{
    Rounds,
    Points,
    Elimination,
    Objective,
    Custom
}

public record EdgeCase(
    string Id,
    EdgeCaseCategory Category,
    string Text,
    IReadOnlyList<string>? RelatedActions = null,
    IReadOnlyList<string>? RelatedPhases = null
);

public enum EdgeCaseCategory
{
    Exception,
    Clarification,
    Variant,
    FAQ
}

public record GlossaryTerm(
    string Term,
    string Definition,
    IReadOnlyList<string>? Examples = null
);