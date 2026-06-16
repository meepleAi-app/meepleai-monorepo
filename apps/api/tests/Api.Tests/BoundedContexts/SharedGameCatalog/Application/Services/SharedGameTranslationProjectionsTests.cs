using Api.BoundedContexts.SharedGameCatalog.Application;
using Api.BoundedContexts.SharedGameCatalog.Application.Services;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services;

/// <summary>
/// Drift-guard reflection tests for <see cref="SharedGameTranslationProjections"/>.
/// Issue #2400 (#2379 follow-up).
///
/// Motivation (PR #2398 F2): the projections file consolidates the previously
/// separated <c>SharedGameTranslationMapper.ToDetailDto</c> and the private
/// <c>GameTitleResolver.ToDto</c> so a new field added to
/// <see cref="SharedGameTranslation"/> can't drift between the list and the
/// detail endpoints. These tests pin that promise: if a developer adds an
/// aggregate field WITHOUT propagating it to the matching DTO (or explicitly
/// allow-listing it as audit-only / soft-delete / concurrency), one of the
/// reflection assertions below will fail at unit-test time instead of leaking
/// to production.
/// </summary>
[Trait("Category", "Unit")]
[Trait("BoundedContext", "SharedGameCatalog")]
public sealed class SharedGameTranslationProjectionsTests
{
    // Fields intentionally NOT projected to either DTO. Updating this list is a
    // deliberate signal that a field is internal-only (audit, concurrency,
    // soft-delete bookkeeping).
    private static readonly HashSet<string> AuditAndBookkeepingFields = new(StringComparer.Ordinal)
    {
        "IsDeleted",
        "DeletedAt",
        "DeletedBy",
    };

    // Fields projected ONLY to ToDetailDto (admin surface), not to ToDto (list).
    private static readonly HashSet<string> DetailOnlyFields = new(StringComparer.Ordinal)
    {
        "Id",
        // Aggregate property name is `SharedGameId`; DTO renames to `GameId`.
        "SharedGameId",
        "CreatedAt",
        "CreatedBy",
        "UpdatedAt",
        "UpdatedBy",
        "Xmin",
    };

    [Fact]
    public void SharedGameTranslationProjections_ConsolidatesBothMappers()
    {
        // Sanity: confirm both projection methods are reachable via the
        // consolidated type. If a future refactor splits them back out into
        // separate files, this test catches the regression of the F2 promise.
        var methods = typeof(SharedGameTranslationProjections)
            .GetMethods(System.Reflection.BindingFlags.Public | System.Reflection.BindingFlags.Static)
            .Select(m => m.Name)
            .ToHashSet();

        methods.Should().Contain("ToDto", "compact projection must live in the consolidated type");
        methods.Should().Contain("ToDetailDto", "detail projection must live in the consolidated type");
    }

    [Fact]
    public void ToDto_ProjectsEveryNonAuditNonDetailOnlyAggregateField()
    {
        // GIVEN every public instance property on the aggregate
        var aggregateFields = typeof(SharedGameTranslation)
            .GetProperties()
            .Select(p => p.Name)
            .Where(name =>
                !AuditAndBookkeepingFields.Contains(name) &&
                !DetailOnlyFields.Contains(name))
            .ToHashSet(StringComparer.Ordinal);

        // WHEN we read the compact DTO's properties
        var dtoFields = typeof(SharedGameTranslationDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        // THEN the compact DTO must include every non-audit, non-detail-only
        // aggregate field. If a new aggregate field appears here, the dev must
        // either: (a) add it to SharedGameTranslationDto + ToDto, or
        // (b) explicitly allow-list it in AuditAndBookkeepingFields /
        // DetailOnlyFields above.
        var missing = aggregateFields.Except(dtoFields, StringComparer.Ordinal).ToList();
        missing.Should().BeEmpty(
            "every aggregate field MUST be projected to SharedGameTranslationDto " +
            "OR explicitly allow-listed as audit/detail-only — found {0} unmapped field(s)",
            missing.Count);
    }

    [Fact]
    public void ToDetailDto_ProjectsEveryNonAuditAggregateField()
    {
        // GIVEN every public instance property on the aggregate
        var aggregateFields = typeof(SharedGameTranslation)
            .GetProperties()
            .Select(p => p.Name)
            .Where(name => !AuditAndBookkeepingFields.Contains(name))
            .ToHashSet(StringComparer.Ordinal);

        // WHEN we read the detail DTO's properties — but the DetailDto renames
        // `SharedGameId` to `GameId`, so we add the original name to the DTO
        // set to short-circuit the equivalence (DTO contract is stable; the
        // rename is intentional).
        var dtoFields = typeof(SharedGameTranslationDetailDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);
        if (dtoFields.Contains("GameId"))
        {
            dtoFields.Add("SharedGameId");
        }

        // THEN the detail DTO must include every non-audit aggregate field.
        var missing = aggregateFields.Except(dtoFields, StringComparer.Ordinal).ToList();
        missing.Should().BeEmpty(
            "every non-audit aggregate field MUST be projected to " +
            "SharedGameTranslationDetailDto — found {0} unmapped field(s)",
            missing.Count);
    }

    [Fact]
    public void ToDto_DoesNotLeakAuditOrDetailOnlyFields()
    {
        // The compact DTO MUST NOT expose audit or detail-only fields — a
        // mistake here would leak admin metadata into the public list surface.
        var dtoFields = typeof(SharedGameTranslationDto)
            .GetProperties()
            .Select(p => p.Name)
            .ToHashSet(StringComparer.Ordinal);

        foreach (var auditField in AuditAndBookkeepingFields)
        {
            dtoFields.Should().NotContain(
                auditField,
                "audit field '{0}' must not appear on the compact DTO",
                auditField);
        }

        foreach (var detailOnlyField in DetailOnlyFields)
        {
            // SharedGameId would surface as GameId on the detail DTO; the
            // compact DTO doesn't expose any game-id at all, so a stricter
            // check still holds.
            var leaked = detailOnlyField == "SharedGameId"
                ? dtoFields.Contains("GameId") || dtoFields.Contains("SharedGameId")
                : dtoFields.Contains(detailOnlyField);

            leaked.Should().BeFalse(
                "detail-only field '{0}' must not appear on the compact DTO",
                detailOnlyField);
        }
    }

    [Fact]
    public void ToDto_ProjectsHappyPathFieldValuesEndToEnd()
    {
        // Behavioral smoke alongside the reflection guards above: a field that
        // satisfies the static contract but mis-wires the value (copy-paste
        // bug, swapped variable) still surfaces here.
        var t = NewActiveTranslation();
        var dto = SharedGameTranslationProjections.ToDto(t);

        dto.Locale.Should().Be(t.Locale.Value);
        dto.Title.Should().Be(t.Title);
        dto.Description.Should().Be(t.Description);
        dto.Source.Should().Be(TranslationSourceMapper.ToPersistedString(t.Source));
    }

    [Fact]
    public void ToDetailDto_ProjectsHappyPathFieldValuesEndToEnd()
    {
        var t = NewActiveTranslation();
        var dto = SharedGameTranslationProjections.ToDetailDto(t);

        dto.Id.Should().Be(t.Id);
        dto.GameId.Should().Be(t.SharedGameId);
        dto.Locale.Should().Be(t.Locale.Value);
        dto.Title.Should().Be(t.Title);
        dto.Description.Should().Be(t.Description);
        dto.Source.Should().Be(TranslationSourceMapper.ToPersistedString(t.Source));
        dto.CreatedAt.Should().Be(t.CreatedAt);
        dto.CreatedBy.Should().Be(t.CreatedBy);
        dto.UpdatedAt.Should().Be(t.UpdatedAt);
        dto.UpdatedBy.Should().Be(t.UpdatedBy);
        dto.Xmin.Should().Be(t.Xmin);
    }

    private static SharedGameTranslation NewActiveTranslation()
    {
        return SharedGameTranslation.Create(
            sharedGameId: Guid.NewGuid(),
            locale: Locale.Create("it"),
            title: "I Coloni di Catan",
            description: "Costruisci e scambia sull'isola di Catan",
            source: TranslationSource.Manual,
            createdBy: Guid.NewGuid(),
            now: new DateTimeOffset(2026, 6, 15, 12, 0, 0, TimeSpan.Zero));
    }
}
