# ADR-020: ValueObject Record Syntax Evaluation

**Status**: Rejected
**Date**: 2026-01-14
**Deciders**: Engineering Lead, Backend Team
**Context**: Code Quality - DDD Value Objects Pattern

---

## Context

Issue #2384 proposed evaluating C# 9+ record syntax with primary constructors for simple ValueObjects to reduce boilerplate. The codebase contains 43 ValueObject implementations with varying complexity.

**Current Pattern** (class-based):
```csharp
internal sealed class FAQAnswer : ValueObject
{
    public string Value { get; }

    public FAQAnswer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("FAQ answer cannot be empty", nameof(value));
        Value = value.Trim();
    }

    protected override IEnumerable<object?> GetEqualityComponents()
    {
        yield return Value;
    }

    public override string ToString() => Value;
}
```

**Proposed Alternative** (record syntax):
```csharp
internal sealed record FAQAnswer : ValueObject
{
    public string Value { get; init; }
    // ... similar implementation
}
```

### ValueObject Analysis Summary

| Category | Count | Description |
|----------|-------|-------------|
| Simple (1-2 props, minimal logic) | 20 | Potential candidates |
| Medium (2-3 props or methods) | 16 | Not suitable |
| Complex (>3 props or operators) | 7 | Not suitable |
| **Total** | **43** | |

---

## Decision

**Rejected** - Keep class-based ValueObjects. Do not convert to record syntax.

### Technical Blocker

C# records **cannot inherit from non-record classes**. The current `ValueObject` base class is:

```csharp
public abstract class ValueObject : IEquatable<ValueObject>
{
    protected abstract IEnumerable<object?> GetEqualityComponents();
    // ... equality implementation
}
```

Converting ValueObjects to records would require one of:

1. **Convert base to `abstract record`** - Breaks existing class-based VOs
2. **Remove inheritance** - Loses shared equality logic, violates DRY
3. **Duplicate equality in each record** - Defeats purpose of abstraction
4. **Interface-based approach** - Partial solution, but records still need equality override

---

## Analysis

### Option 1: Convert Base Class to Abstract Record

```csharp
public abstract record ValueObject
{
    protected abstract IEnumerable<object?> GetEqualityComponents();

    public virtual bool Equals(ValueObject? other) { ... }
    public override int GetHashCode() { ... }
}
```

**Problems**:
- Records use property-based equality by default, overriding to component-based is unusual
- Would require updating ALL 43 ValueObjects to `record` syntax
- Some complex VOs use operators (`>, <, >=, <=`) which require manual implementation regardless
- EF Core mapping may behave differently with records
- Breaking change across entire codebase

### Option 2: Remove Inheritance for Simple VOs

```csharp
internal sealed record FAQAnswer
{
    public string Value { get; init; }

    public FAQAnswer(string value)
    {
        if (string.IsNullOrWhiteSpace(value))
            throw new ArgumentException("FAQ answer cannot be empty");
        Value = value.Trim();
    }
}
```

**Problems**:
- Loses consistent equality behavior across VOs
- Mixed patterns (some inherit `ValueObject`, some are standalone records)
- Cannot use polymorphism (`IEnumerable<ValueObject>`)
- Inconsistent approach confuses developers

### Option 3: Keep Class-Based Pattern (Selected)

Keep current implementation. The "boilerplate" is actually meaningful:
- `GetEqualityComponents()` forces explicit equality consideration
- Consistent pattern across all 43 ValueObjects
- Well-tested, proven approach in DDD community
- EF Core compatibility guaranteed

---

## Consequences

### Positive

✅ **Consistency**: Single pattern for all ValueObjects
✅ **Maintainability**: Team knows exactly what to expect
✅ **Type Safety**: Base class provides compile-time guarantees
✅ **EF Core**: No mapping surprises with established class pattern
✅ **Polymorphism**: Can work with `IEnumerable<ValueObject>`

### Negative

⚠️ **Verbosity**: 20-30 lines per simple ValueObject (vs ~15 with record)
⚠️ **Modern C# Features**: Not using latest language constructs

**Mitigation**: The "extra" code is meaningful - it explicitly defines equality semantics and validation.

---

## Alternatives Considered

### Alternative A: Hybrid Approach
Use records for new simple VOs, keep classes for complex ones.

**Rejected**: Creates inconsistency, confuses developers, complicates code reviews.

### Alternative B: Source Generator
Generate ValueObject boilerplate from attributes.

**Rejected**: Adds build complexity, debugging difficulty, marginal benefit for 43 VOs.

### Alternative C: Composition over Inheritance
```csharp
internal sealed record FAQAnswer : IValueObject
{
    // ... equality via interface default methods
}
```

**Rejected**: C# interfaces can't enforce equality contract, still requires boilerplate.

---

## Recommendations

Instead of record conversion, consider these improvements:

1. **Code Snippets**: Create VS/Rider snippets for ValueObject creation
2. **Analyzer**: Add custom Roslyn analyzer to enforce ValueObject patterns
3. **Documentation**: Ensure ValueObject patterns are well-documented

---

## ValueObject Inventory (for reference)

### Simple ValueObjects (20) - NOT converting
- AlertSeverity, Email, TotpSecret, CollectionName, FileName
- LanguageCode, FAQAnswer, FAQQuestion, Publisher, SessionStatus
- YearPublished, Confidence, LibraryNotes, NotificationSeverity
- NotificationType, WorkflowUrl, ConfigKey, Percentage, Citation

### Medium Complexity (16) - Inherently unsuitable
- PasswordHash, Role, UserTier, BackupCode, FileSize, PageCount
- PdfVersion, GameTitle, PlayerCount, PlayTime, SessionPlayer
- Vector, ExportedChatData, GameRules, HnswConfiguration, QuantizationConfiguration

### Complex (7) - Operators/Comparisons required
- DocumentType, Version, AccessibilityMetrics, E2EMetrics
- PerformanceMetrics, ChunkPayload, DocumentVersion

---

## References

- [C# Records Documentation](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record)
- [Record Inheritance Limitations](https://learn.microsoft.com/en-us/dotnet/csharp/language-reference/builtin-types/record#inheritance)
- [DDD Value Objects Pattern](https://docs.microsoft.com/en-us/dotnet/architecture/microservices/microservice-ddd-cqrs-patterns/implement-value-objects)
- [EF Core with Records](https://learn.microsoft.com/en-us/ef/core/modeling/constructors)

---

**Decision Maker**: Engineering Lead
**Outcome**: Maintain current class-based ValueObject pattern
**Implementation**: None (keep existing code)
