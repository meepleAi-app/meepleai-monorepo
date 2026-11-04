namespace Api.BoundedContexts.SharedGameCatalog.Domain.Enums;

/// <summary>Kind of a mechanic claim (spec 2026-10-08 §2). Persisted as int: append-only.</summary>
public enum MechanicClaimKind
{
    Rule = 0,
    Exception = 1,
    Clarification = 2,
    Example = 3
}
