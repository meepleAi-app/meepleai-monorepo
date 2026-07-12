# #2782 (ME-M1.4 FU-1) Real per-claim validations + scores — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the derived all-`pass` `MechanicClaimValidations.DerivePass()` (#526) with REAL per-claim guardrail outcomes + a T3b grounding score captured at pipeline time, persisted on the claim, projected into the #527 card snapshot, and surfaced in the admin review UI as genuine `fail`/`notRun` states with a working `reject-all-failing-T2` bulk predicate and an approve-time warning.

**Architecture:** The guardrail chain (`MechanicOutputValidator.ValidateAsync`, fail-fast) accumulates per-guardrail `{rule, outcome, message?, path?, score?}` **during its single fail-fast pass** (guardrails that ran → `pass`/`fail`; guardrails after the fail-fast stop → `notRun`) and returns them on `MechanicValidationResult.RuleOutcomes` — no separate re-run pass, no extra embedding. The pipeline (`MechanicAnalysisPipeline.RunAsync`) stops aborting on guardrail failure (**run-all-retain**): guardrail-failing sections are retained with their FINAL-attempt rule outcomes; only `well_formed` (malformed JSON → section absent), grounding `unavailable` (embedding outage → hard abort), cost-cap and LLM-fail still hard-abort. The parser stamps a stable **source-index JSONPath anchor** on each minted claim so a violation's `Path` correlates to exactly one claim. Outcomes are persisted in a new `mechanic_claims.validations jsonb` column (jsonb value-converter), read-mapped onto the domain `MechanicClaim`, and consumed by the 5 DTO-construction sites (`DeriveLegacyAllPassFallback()` becomes a legacy fallback for pre-FU-1 nulls only). The #527 card down-projects the outcomes; the FE widens 4→5 badges (T1/T2/T3a/T3b/T4), corrects `reject-all-failing-T2`, and adds an approve-warning + score display.

**Tech Stack:** .NET 9 (Minimal APIs + MediatR + EF Core + Npgsql jsonb + FluentValidation), Next.js 16 (React 19, Zod, React Query, shadcn), xUnit/Moq + Testcontainers (Postgres 16), Vitest.

**Design spec:** `docs/superpowers/specs/2026-07-10-issue-2782-me-fu1-real-validations-design.md`

## Global Constraints

- **Branch:** `feature/issue-2782-me-fu1-real-validations` (parent `main-dev`). Push with `git push -u origin feature/issue-2782-me-fu1-real-validations`.
- **CQRS:** endpoints call only `IMediator.Send()`. Exceptions: `ConflictException`(409), `NotFoundException`(404) — never `InvalidOperationException`(500).
- **EF snake_case:** explicit `HasColumnName("snake_case")` — no auto snake_case convention. LiveSession-style handlers `AddAsync`/`Update` MUST be followed by `SaveChangesAsync` (already the case in the executor: `_analysisRepository.Update(analysis)` then `await _unitOfWork.SaveChangesAsync(...)`).
- **jsonb converter (M4) — a ValueComparer is NOT required for correctness:** the new `validations jsonb` column needs a `ValueConverter<List<MechanicClaimValidation>?, string?>` so the list stores as jsonb. It does **not** need a `ValueComparer` to be correct, because the only write path — `MechanicAnalysisRepository.Update()` — maps a **fresh detached entity** and force-sets `EntityState.Modified` on it and each claim (verified: `Update()` calls `MapToEntity(analysis)`, `Attach`es it, sets `State = EntityState.Modified`, then per-claim `State = Added|Modified`). EF then writes **all columns unconditionally**; the snapshot-diff that a comparer feeds is never consulted for this write path, so a comparer is **inert** here. You MAY add a comparer (harmless), but do NOT claim it is load-bearing and do NOT let a test purport to red-gate it. The nearest converter precedent is `ModelCompatibilityEntryEntityConfiguration` (`.HasColumnType("jsonb").HasConversion(...)`, no comparer) — mirror it.
- **The #526 WRITE+READ-mapper trap (M4), 4 points:** persisting a claim-level field touches FOUR mapper points, all in the SharedGameCatalog infra layer:
  1. `MechanicClaimEntity` property (`Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs`)
  2. `MechanicClaimEntityConfiguration` mapping (`Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs`)
  3. `MapClaimToEntity` **write** (`Infrastructure/Repositories/MechanicAnalysisRepository.cs:395`)
  4. `MapClaimToDomain` **read** (`MechanicAnalysisRepository.cs:325`)
  The Moq handler tests + API responses read the **in-memory** domain object and stay green even if the DB WRITE mapper drops the field — only the Testcontainers **write → reload → mutate → re-save → reload** round-trip catches a dropped WRITE (or READ) mapper copy. That is what the Task 8 test genuinely red-gates.
- **5-site `DerivePass()` flip (M3), two overloads (NOT homogeneous):** the derivation is inlined at 5 DTO-construction sites. **One reads the ENTITY, four read the DOMAIN aggregate:**
  - `GetMechanicAnalysisClaimsQueryHandler.cs:72` — queries `_dbContext.MechanicClaims` (`AsNoTracking().IgnoreQueryFilters()`); its claim variable `c` is a **`MechanicClaimEntity`** → use `MechanicClaimValidations.FromEntity(c)` (reads `entity.Validations` jsonb column).
  - `ApproveMechanicClaimCommandHandler.cs:135`, `RejectMechanicClaimCommandHandler.cs:119`, `BulkApproveMechanicClaimsCommandHandler.cs:142`, `BulkRejectMechanicClaimsCommandHandler.cs:155` — project from `analysis.Claims` (in-memory **domain** `MechanicClaim`) → use `MechanicClaimValidations.FromDomain(claim)`. Validations MUST live on the domain `MechanicClaim` and be read-mapped by `MapClaimToDomain` for these four.
- **Backend test commands:** `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~<Name>" --project ../../tests/Api.Tests/Api.Tests.csproj` (the test project is at `apps/api/tests/Api.Tests/Api.Tests.csproj`, NOT `tests/Api.Tests`). **Kill testhost first if a DLL lock error appears:** `tasklist | grep testhost` → `taskkill //PID <PID> //F`. Do NOT run two `dotnet test` in parallel locally (DLL lock crash). Testcontainers integration tests require **Docker Desktop running**.
- **Frontend test commands:** `cd apps/web && pnpm test <path>` / `pnpm typecheck` / `pnpm lint`. `vi.mock` paths are relative to the TEST file; use `vi.hoisted()` for mock fns referenced inside `vi.mock`.
- **Migrations location:** `apps/api/src/Api/Infrastructure/Migrations/` (NOT `apps/api/src/Api/Migrations`). The single snapshot is `MeepleAiDbContextModelSnapshot.cs`; stage both the new migration file AND the modified snapshot. DbContext = `MeepleAiDbContext`.
- **Guardrail → rule taxonomy (verified, 5 explicit rules — dec. 6):** T1 `QuoteCapGuardrail` (Order 10, emits `T1_quote_cap`); T3a `CitationPresenceGuardrail` (Order 15, `T3_citation_required`); T4 `PageSubstringGuardrail` (Order 20, `T4_page_out_of_range`/`T4_quote_not_substring`); T2 `RejectionSamplingGuardrail` (Order 30, `T2_long_verbatim`); T3b `GroundingGuardrail` (Order 40, `T3_grounding`/`T3_grounding_unavailable`). `well_formed` is a pre-guardrail check in `ValidateSectionAsync`. Canonical badge rules = `{T1, T2, T3a, T3b, T4}` (= the guardrails' `RuleFamily` values). `IMechanicGuardrail.RuleFamily` already returns these exact strings.
- **BE-core is ATOMIC (dec. 7):** the pipeline behavior change (run-all) + persistence + DTO flip merge together — never a window where run-all-retained claims render as fake all-`pass`. BE-core may be internally staged as commits but is one merge unit.

---

## File map

**Create (backend)**
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimValidation.cs` — domain VO `{Rule, Outcome, Message?, Score?}`.
- Migration `…/Infrastructure/Migrations/<ts>_AddMechanicClaimValidations.cs` (jsonb column).
- Migration `…/Infrastructure/Migrations/<ts>_WidenMechanicSectionRunStatusRange.cs` (CHECK 0..3).
- `docs/for-claude/architecture/adr/adr-084-mechanic-validation-canonical-shape.md` — ADR #2786.
- Test files under `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/…` (per task).

**Modify (backend)**
- `…/Application/Services/MechanicExtractor/Guardrails/IMechanicGuardrail.cs` — `EvaluateDetailedAsync` returning violations+score.
- `…/Guardrails/GroundingGuardrail.cs` — surface the min cosine as a score.
- `…/Guardrails/{QuoteCap,CitationPresence,PageSubstring,RejectionSampling}Guardrail.cs` — no change (inherit default `EvaluateDetailedAsync`, score=null).
- `…/IMechanicOutputValidator.cs` + `…/MechanicOutputValidator.cs` — `MechanicRuleOutcome`; `MechanicValidationResult.RuleOutcomes` accumulated during the single fail-fast `ValidateAsync` pass (guardrails after the stop → `notRun`).
- `…/MechanicOutputParser.cs` — source-index anchor on every minted claim (all 6 Parse* methods + `BuildClaim`).
- `…/MechanicAnalysisPipeline.cs` — `MechanicPipelineResult.SectionOutcomes` (init property, default empty), run-all-retain + well_formed/grounding-unavailable branches, `RunSectionAsync` keeps final `RuleOutcomes` + Status=3.
- `…/MechanicAnalysisExecutor.cs` — correlate `SectionOutcomes` → claims via `CorrelateValidations(parsed, …)` in `ApplySuccessAsync` and `CorrelateValidations(salvaged, …)` in `ApplyAbort`; keep `ValidationFailedBeyondRetry` only for no-claims edge.
- `…/Domain/Entities/MechanicClaim.cs` — `SourceAnchor` + `Validations` + `AttachValidations()`; `Reconstitute`/`CreateWithId` params.
- `…/Domain/ValueObjects/MechanicCardContent.cs` — down-project claim validations; `CurrentSchemaVersion = 2`.
- `…/Application/DTOs/MechanicClaimValidationDto.cs` — add `Score`; rename `DerivePass` → `DeriveLegacyAllPassFallback`; `Families` → 5 rules; add `MechanicClaimValidations.FromDomain(MechanicClaim)` + `FromEntity(MechanicClaimEntity)`.
- `…/Application/Queries/MechanicExtractor/GetMechanicAnalysisClaimsQueryHandler.cs` — flip to `FromEntity(c)` (entity source).
- `…/Application/Commands/MechanicExtractor/{Approve,Reject,BulkApprove,BulkReject}MechanicClaimsCommandHandler.cs` — flip the 4 domain sites to `FromDomain`.
- `…/Application/Commands/MechanicExtractor/BulkApproveMechanicClaimsCommandHandler.cs` — exclude fail-flagged claims.
- `…/Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs` + `…/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs` — `Validations` jsonb (converter; comparer optional/non-load-bearing).
- `…/Infrastructure/Repositories/MechanicAnalysisRepository.cs` — `MapClaimToEntity` write + `MapClaimToDomain` read.
- `…/Configurations/SharedGameCatalog/MechanicAnalysisSectionRunEntityConfiguration.cs` — CHECK 0..3.

**Modify (frontend)**
- `apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts` — `score` on `MechanicClaimValidationDtoSchema`.
- `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx` — 5-badge render, `reject-all-failing-T2` predicate, score display.
- `apps/web/src/components/admin/mechanic-extractor/claims/ApproveClaimDialog.tsx` — approve-time fail warning.
- Test files under `apps/web/src/**/__tests__/` (per task).

---

## Delivery — 3 stacked slices

The design decomposes into **3 stacked slices** onto `main-dev` (each independently green; later slices branch off the prior). BE-core is the large atomic unit; BE-card and FE stack on top.

- **BE-core (Tasks 1–11, ATOMIC):** D1 validator accumulate-during-fail-fast-pass + D2 T3b score + D4 parser source-index anchor + D3 pipeline run-all-retain / well_formed / grounding-unavailable carve-outs + validator→pipeline outcome propagation + correlation in the executor + D5 jsonb column/migration/4-mapper-points + 5-site `DerivePass` flip (`FromEntity`+`FromDomain`) + D9 section-run Status=3 CHECK migration. Merged as one unit — no fake-all-pass window. May itself be a stacked pair merged back-to-back.
- **BE-card (Tasks 12–14):** D6 card down-projection + `SchemaVersion`→2 + ADR #2786 + D8 server-side `BulkApprove` exclude-fail guard.
- **FE (Tasks 15–18):** D7 5-badge widen + `reject-all-failing-T2` reads validations + approve-warning + T3b score + Zod `score` contract test.

Executed via subagent-driven-development (implementer + spec/quality reviewer per task, final whole-branch review), TDD, then merge + cleanup — same workflow as #526.

---

## Task 1: BE-core — `MechanicClaimValidation` domain VO

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimValidation.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimValidationTests.cs` (create)

**Interfaces:**
- Produces: `sealed record MechanicClaimValidation(string Rule, string Outcome, string? Message = null, double? Score = null)`; `static class MechanicClaimValidationOutcomes { const string Pass = "pass"; const string Fail = "fail"; const string NotRun = "notRun"; }`.

- [ ] **Step 1: Write the failing test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicClaimValidationTests
{
    [Fact]
    public void Record_CarriesRuleOutcomeMessageScore()
    {
        var v = new MechanicClaimValidation("T3b", MechanicClaimValidationOutcomes.Pass, Message: null, Score: 0.87);

        v.Rule.Should().Be("T3b");
        v.Outcome.Should().Be("pass");
        v.Score.Should().Be(0.87);
    }

    [Fact]
    public void Outcomes_ExposeCanonicalStrings()
    {
        MechanicClaimValidationOutcomes.Pass.Should().Be("pass");
        MechanicClaimValidationOutcomes.Fail.Should().Be("fail");
        MechanicClaimValidationOutcomes.NotRun.Should().Be("notRun");
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicClaimValidationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — `MechanicClaimValidation` / `MechanicClaimValidationOutcomes` do not exist.

- [ ] **Step 3: Create the VO**

`MechanicClaimValidation.cs`:
```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;

/// <summary>
/// A real per-claim guardrail outcome captured at pipeline time (#2782 FU-1).
/// Rule ∈ {T1,T2,T3a,T3b,T4}; Outcome ∈ {pass,fail,notRun}. Score is populated only for T3b
/// (grounding cosine); null for all other rules.
/// </summary>
public sealed record MechanicClaimValidation(
    string Rule,
    string Outcome,
    string? Message = null,
    double? Score = null);

/// <summary>Canonical outcome strings for <see cref="MechanicClaimValidation.Outcome"/>.</summary>
public static class MechanicClaimValidationOutcomes
{
    public const string Pass = "pass";
    public const string Fail = "fail";
    public const string NotRun = "notRun";
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicClaimValidationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicClaimValidation.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 add MechanicClaimValidation domain VO (D5)"
```

---

## Task 2: BE-core — guardrail detailed contract + T3b score (D1 + D2)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/Guardrails/IMechanicGuardrail.cs`
- Modify: `…/Guardrails/GroundingGuardrail.cs`
- Modify: `…/Guardrails/QuoteCapGuardrail.cs`, `CitationPresenceGuardrail.cs`, `PageSubstringGuardrail.cs`, `RejectionSamplingGuardrail.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/MechanicExtractor/Guardrails/GroundingGuardrailScoreTests.cs` (create)

> **Test-tree note (verified):** the existing guardrail/validator tests live under `.../Application/MechanicExtractor/Guardrails/` (namespace `Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor.Guardrails`, **no `Services`** segment) — e.g. `MechanicGuardrailTests.cs`. The executor tests use `.../Application/Services/MechanicExtractor/` (**with `Services`**). Both trees exist. New guardrail/validator tests go under the **no-`Services`** tree; new executor/pipeline tests go under the **`Services`** tree.

**Interfaces:**
- Produces: `sealed record MechanicGuardrailResult(IReadOnlyList<MechanicValidationViolation> Violations, double? Score)`; `IMechanicGuardrail.EvaluateDetailedAsync(context, ct) : Task<MechanicGuardrailResult>` (default-implemented via `EvaluateAsync` for the 4 non-scoring guardrails). Grounding overrides it to attach the **minimum observed cosine** as `Score`. Used by `MechanicOutputValidator.ValidateAsync` (Task 3) to capture each guardrail's score+violations as it runs the single fail-fast pass.
- Consumes: existing `IMechanicGuardrail.{RuleFamily, Order, EvaluateAsync}` + `GroundingGuardrail.Cosine(float[],float[])`.

- [ ] **Step 1: Write the failing test** — assert the grounding guardrail returns the min cosine as a score even when it passes. Use a stub `IEmbeddingService` returning fixed vectors so cosine is deterministic.

```csharp
using System.Text.Json;
using Api.BoundedContexts.SharedGameCatalog.Application.Configuration;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Services;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor.Guardrails;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class GroundingGuardrailScoreTests
{
    private sealed class FixedEmbeddings : IEmbeddingService
    {
        private readonly float[] _vec;
        public FixedEmbeddings(float[] vec) => _vec = vec;
        public Task<float[]> EmbedAsync(string text, CancellationToken ct) => Task.FromResult(_vec);
        // If IEmbeddingService has more members, throw NotSupportedException for them.
    }

    [Fact]
    public async Task EvaluateDetailedAsync_PopulatesScore_WithMinCosine_OnPass()
    {
        // identical vectors → cosine 1.0 → passes threshold, but score must still be surfaced
        var embeddings = new FixedEmbeddings(new[] { 1f, 0f });
        var guardrail = new GroundingGuardrail(embeddings);

        var json = """
        {"summary":{"text":"players score points","citations":[{"pdf_page":1,"quote":"score points","chunk_id":"11111111-1111-4111-8111-111111111111"}]}}
        """;
        using var doc = JsonDocument.Parse(json);
        var chunks = new[] { new MechanicSourceChunk(0, 1, Guid.Parse("11111111-1111-4111-8111-111111111111"), "score points") };
        var ctx = new MechanicGuardrailContext(MechanicSection.Summary, doc.RootElement, chunks, 10,
            new MechanicGuardrailOptions { MinClaimGroundingSimilarity = 0.5 });

        var result = await guardrail.EvaluateDetailedAsync(ctx, CancellationToken.None);

        result.Violations.Should().BeEmpty();
        result.Score.Should().NotBeNull();
        result.Score!.Value.Should().BeApproximately(1.0, 0.001);
    }
}
```
> If `MechanicGuardrailOptions` uses a constructor rather than an object initializer, or `IEmbeddingService` has extra members, adjust the stub — verify against `Application/Configuration/MechanicGuardrailOptions.cs` and `Domain/Services/IEmbeddingService.cs` before running.

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GroundingGuardrailScoreTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — `EvaluateDetailedAsync` / `MechanicGuardrailResult` do not exist.

- [ ] **Step 3: Add the detailed contract to `IMechanicGuardrail.cs`** — add the result record + a default-implemented method so the 4 non-scoring guardrails need no override:

```csharp
/// <summary>Detailed guardrail result: the violations plus an optional numeric score (T3b only).</summary>
public sealed record MechanicGuardrailResult(
    IReadOnlyList<MechanicValidationViolation> Violations,
    double? Score = null);

public interface IMechanicGuardrail
{
    string RuleFamily { get; }
    int Order { get; }

    Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
        MechanicGuardrailContext context,
        CancellationToken cancellationToken);

    /// <summary>
    /// Collect-all detailed evaluation (#2782 D1/D2). Default: wrap <see cref="EvaluateAsync"/>
    /// with a null score. Only <c>GroundingGuardrail</c> overrides to surface its cosine.
    /// </summary>
    async Task<MechanicGuardrailResult> EvaluateDetailedAsync(
        MechanicGuardrailContext context,
        CancellationToken cancellationToken)
    {
        var violations = await EvaluateAsync(context, cancellationToken).ConfigureAwait(false);
        return new MechanicGuardrailResult(violations, Score: null);
    }
}
```

- [ ] **Step 4: Override `EvaluateDetailedAsync` in `GroundingGuardrail.cs`** — refactor so the cosine loop records the **minimum** observed cosine (the worst-grounded claim) and returns it as the score. Replace the body so both `EvaluateAsync` (kept for the fail-fast path) and the new detailed method share a private core:

```csharp
public async Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(
    MechanicGuardrailContext context, CancellationToken cancellationToken)
{
    var (violations, _) = await EvaluateCoreAsync(context, cancellationToken).ConfigureAwait(false);
    return violations;
}

public async Task<MechanicGuardrailResult> EvaluateDetailedAsync(
    MechanicGuardrailContext context, CancellationToken cancellationToken)
{
    var (violations, minCosine) = await EvaluateCoreAsync(context, cancellationToken).ConfigureAwait(false);
    return new MechanicGuardrailResult(violations, minCosine);
}

private async Task<(IReadOnlyList<MechanicValidationViolation> Violations, double? MinCosine)> EvaluateCoreAsync(
    MechanicGuardrailContext context, CancellationToken cancellationToken)
{
    var violations = new List<MechanicValidationViolation>();
    var targets = new List<(string claim, string path, MechanicSourceChunk chunk)>();

    MechanicJsonWalker.ForEachObject(context.Root, "$", (obj, path) =>
    {
        if (obj.ValueKind != JsonValueKind.Object) { return; }
        string? claim = null;
        foreach (var f in ClaimFields)
        {
            if (obj.TryGetProperty(f, out var e) && e.ValueKind == JsonValueKind.String)
            {
                var s = e.GetString();
                if (!string.IsNullOrWhiteSpace(s)) { claim = s; break; }
            }
        }
        if (claim is null) { return; }
        if (!obj.TryGetProperty("citations", out var cits) || cits.ValueKind != JsonValueKind.Array) { return; }
        foreach (var c in cits.EnumerateArray())
        {
            var chunk = ResolveChunk(c, context.SourceChunks);
            if (chunk != null) { targets.Add((claim!, path, chunk)); }
        }
    });

    double? minCosine = null;
    foreach (var (claim, path, chunk) in targets)
    {
        cancellationToken.ThrowIfCancellationRequested();
        try
        {
            var a = await _embeddings.EmbedAsync(claim, cancellationToken).ConfigureAwait(false);
            var b = await _embeddings.EmbedAsync(chunk.Content, cancellationToken).ConfigureAwait(false);
            var cos = Cosine(a, b);
            minCosine = minCosine is null ? cos : Math.Min(minCosine.Value, cos);
            if (cos < context.Options.MinClaimGroundingSimilarity)
            {
                violations.Add(new MechanicValidationViolation(
                    "T3_grounding",
                    $"claim '{Trunc(claim)}' has cosine {cos:0.00} with cited chunk #{chunk.ChunkIndex} " +
                    $"(page {chunk.PageNumber}), below threshold {context.Options.MinClaimGroundingSimilarity}",
                    path));
            }
        }
        catch (OperationCanceledException) { throw; }
        catch (Exception ex)
        {
            violations.Add(new MechanicValidationViolation(
                "T3_grounding_unavailable",
                $"embedding service unavailable, failing closed: {ex.Message}",
                path));
            break; // outage is global
        }
    }

    return (violations, minCosine);
}
```
> The 4 non-scoring guardrails (`QuoteCap`, `CitationPresence`, `PageSubstring`, `RejectionSampling`) need NO code change — they inherit the default `EvaluateDetailedAsync` returning `Score: null`. (Add nothing to them; the default interface method covers it.)

- [ ] **Step 5: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~GroundingGuardrailScoreTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS + build OK.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/Guardrails apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 guardrail EvaluateDetailedAsync + T3b grounding score (D1/D2)"
```

---

## Task 3: BE-core — validator accumulates RuleOutcomes during the fail-fast pass (D1)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/IMechanicOutputValidator.cs`
- Modify: `…/MechanicOutputValidator.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/MechanicExtractor/MechanicOutputValidatorRuleOutcomesTests.cs` (create — no-`Services` tree, alongside `MechanicGuardrailTests.cs`)

**Interfaces:**
- Consumes: `MechanicGuardrailResult` (Task 2), `IMechanicGuardrail.EvaluateDetailedAsync`.
- Produces: `sealed record MechanicRuleOutcome(string Rule, string Outcome, string? Message, string? Path, double? Score, IReadOnlyList<MechanicValidationViolation> Violations)`; `MechanicValidationResult` gains `IReadOnlyList<MechanicRuleOutcome> RuleOutcomes` (populated on EVERY `ValidateAsync` call). **No `ValidateAllAsync`** — `ValidateAsync` accumulates the outcomes during its existing single fail-fast pass. `Rule` in an outcome is the guardrail `RuleFamily` (`T1/T2/T3a/T3b/T4`). `IsValid`/`Violations` semantics (retry trigger) UNCHANGED. Guardrails before the fail-fast stop → `pass`; the failing guardrail → `fail`; guardrails AFTER the stop → `notRun`.

> **Why this shape (verified):** `ValidateAsync` already loops `_guardrails` in `Order` and `return`s `MechanicValidationResult.Invalid(violations)` at the first guardrail with violations. We (a) switch the per-guardrail call from `EvaluateAsync` to `EvaluateDetailedAsync` (to capture the T3b score for `pass` too), (b) record a `MechanicRuleOutcome` for each guardrail as it runs, (c) at the fail-fast point, append `notRun` outcomes for every remaining guardrail and return `Invalid` with the accumulated outcomes, (d) on all-pass return `Valid` with all-pass outcomes. This is the SAME single evaluation — no extra embedding on pass, no re-run.

- [ ] **Step 1: Write the failing test** — a stub guardrail set (T1 pass, T2 fail, T3b never reached). Assert `RuleOutcomes` reports T1 `pass`, T2 `fail` (with Path), T3b `notRun`; and `IsValid` is still `false`.

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor.Guardrails;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using System.Text.Json;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicOutputValidatorRuleOutcomesTests
{
    private sealed class StubGuardrail : IMechanicGuardrail
    {
        private readonly IReadOnlyList<MechanicValidationViolation> _violations;
        private readonly double? _score;
        public StubGuardrail(string family, int order, IReadOnlyList<MechanicValidationViolation> violations, double? score = null)
        { RuleFamily = family; Order = order; _violations = violations; _score = score; }
        public string RuleFamily { get; }
        public int Order { get; }
        public Task<IReadOnlyList<MechanicValidationViolation>> EvaluateAsync(MechanicGuardrailContext c, CancellationToken ct) => Task.FromResult(_violations);
        public Task<MechanicGuardrailResult> EvaluateDetailedAsync(MechanicGuardrailContext c, CancellationToken ct) => Task.FromResult(new MechanicGuardrailResult(_violations, _score));
    }

    private static MechanicGuardrailContext EmptyContext()
    {
        using var doc = JsonDocument.Parse("{}");
        return new MechanicGuardrailContext(MechanicSection.Summary, doc.RootElement.Clone(), Array.Empty<MechanicSourceChunk>(), 1, new());
    }

    [Fact]
    public async Task ValidateAsync_FailFast_StillAccumulatesPassFailNotRunOutcomes()
    {
        var t1Pass = new StubGuardrail("T1", 10, Array.Empty<MechanicValidationViolation>());
        var t2Fail = new StubGuardrail("T2", 30, new[] { new MechanicValidationViolation("T2_long_verbatim", "long verbatim", "$.mechanics[1].description") });
        var t3bPass = new StubGuardrail("T3b", 40, Array.Empty<MechanicValidationViolation>(), score: 0.9);
        // Ordered by Order → T1(10), T2(30), T3b(40). Fail-fast stops after T2 → T3b is notRun.
        var validator = new MechanicOutputValidator(new IMechanicGuardrail[] { t3bPass, t2Fail, t1Pass }, NullLogger<MechanicOutputValidator>.Instance);

        var result = await validator.ValidateAsync(EmptyContext(), CancellationToken.None);

        result.IsValid.Should().BeFalse(); // retry trigger unchanged
        result.RuleOutcomes.Select(o => o.Rule).Should().Equal("T1", "T2", "T3b"); // Order-preserved
        result.RuleOutcomes.Single(o => o.Rule == "T1").Outcome.Should().Be("pass");
        result.RuleOutcomes.Single(o => o.Rule == "T2").Outcome.Should().Be("fail");
        result.RuleOutcomes.Single(o => o.Rule == "T2").Path.Should().Be("$.mechanics[1].description");
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Outcome.Should().Be("notRun");
    }

    [Fact]
    public async Task ValidateAsync_AllPass_CapturesScoresWithoutExtraWork()
    {
        var t1Pass = new StubGuardrail("T1", 10, Array.Empty<MechanicValidationViolation>());
        var t3bPass = new StubGuardrail("T3b", 40, Array.Empty<MechanicValidationViolation>(), score: 0.83);
        var validator = new MechanicOutputValidator(new IMechanicGuardrail[] { t3bPass, t1Pass }, NullLogger<MechanicOutputValidator>.Instance);

        var result = await validator.ValidateAsync(EmptyContext(), CancellationToken.None);

        result.IsValid.Should().BeTrue();
        result.RuleOutcomes.Should().OnlyContain(o => o.Outcome == "pass");
        result.RuleOutcomes.Single(o => o.Rule == "T3b").Score.Should().Be(0.83);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicOutputValidatorRuleOutcomesTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — `MechanicValidationResult.RuleOutcomes` / `MechanicRuleOutcome` do not exist.

- [ ] **Step 3: Add `MechanicRuleOutcome` + `RuleOutcomes` to `IMechanicOutputValidator.cs`.** Add the record and extend `MechanicValidationResult` (keep the `Valid()`/`Invalid()` factories working — add overloads that carry outcomes, and default the existing factories to an empty outcome list so no other caller breaks):

```csharp
/// <summary>Outcome of validation — either valid or a list of violations, PLUS the per-guardrail
/// rule outcomes accumulated during the fail-fast pass (#2782 D1).</summary>
public sealed record MechanicValidationResult(
    bool IsValid,
    IReadOnlyList<MechanicValidationViolation> Violations,
    IReadOnlyList<MechanicRuleOutcome> RuleOutcomes)
{
    public static MechanicValidationResult Valid() =>
        new(true, Array.Empty<MechanicValidationViolation>(), Array.Empty<MechanicRuleOutcome>());

    public static MechanicValidationResult Valid(IReadOnlyList<MechanicRuleOutcome> ruleOutcomes) =>
        new(true, Array.Empty<MechanicValidationViolation>(), ruleOutcomes);

    public static MechanicValidationResult Invalid(IReadOnlyList<MechanicValidationViolation> violations) =>
        new(false, violations, Array.Empty<MechanicRuleOutcome>());

    public static MechanicValidationResult Invalid(
        IReadOnlyList<MechanicValidationViolation> violations,
        IReadOnlyList<MechanicRuleOutcome> ruleOutcomes) =>
        new(false, violations, ruleOutcomes);
}

/// <summary>One guardrail's outcome captured during the fail-fast pass (#2782 D1). Rule is the
/// guardrail RuleFamily (T1/T2/T3a/T3b/T4). Outcome ∈ {pass,fail,notRun} — notRun = the guardrail
/// was downstream of the first failing guardrail and never ran.</summary>
public sealed record MechanicRuleOutcome(
    string Rule,
    string Outcome,
    string? Message,
    string? Path,
    double? Score,
    IReadOnlyList<MechanicValidationViolation> Violations);
```
> The positional-record `MechanicValidationResult` gains a 3rd param — the two `Invalid(violations)` / `Valid()` factories keep the existing single-arg call sites compiling (e.g. `ValidateSectionAsync`'s `well_formed` returns). Verify no code constructs `new MechanicValidationResult(...)` positionally (grep `new MechanicValidationResult(` → expect only the factories); if any exists, add the 3rd arg.

- [ ] **Step 4: Rewrite `ValidateAsync` in `MechanicOutputValidator.cs`** to accumulate outcomes during the single fail-fast pass. Replace the loop so each guardrail records a `MechanicRuleOutcome`, and at the fail-fast point it appends `notRun` for the rest and returns `Invalid(violations, outcomes)`:

```csharp
public async Task<MechanicValidationResult> ValidateAsync(
    MechanicGuardrailContext context, CancellationToken cancellationToken)
{
    var outcomes = new List<MechanicRuleOutcome>(_guardrails.Count);

    for (var i = 0; i < _guardrails.Count; i++)
    {
        var guardrail = _guardrails[i];
        cancellationToken.ThrowIfCancellationRequested();
        var stopwatch = Stopwatch.StartNew();
        var detailed = await guardrail.EvaluateDetailedAsync(context, cancellationToken).ConfigureAwait(false);
        var violations = detailed.Violations;
        stopwatch.Stop();

        var outcomeLabel = violations.Count == 0 ? "pass" : "fail";
        MeepleAiMetrics.MechanicValidatorInvocations.Add(1, new System.Diagnostics.TagList
        {
            { "validator", guardrail.RuleFamily },
            { "outcome", outcomeLabel }
        });

        _logger.LogInformation(
            "Mechanic guardrail {Validator} {Outcome} for analysis {AnalysisId} section {Section} " +
            "(retry {RetryCount}) in {LatencyMs}ms{ViolationRule}",
            guardrail.RuleFamily, outcomeLabel, context.AnalysisId, context.Section, context.RetryCount,
            stopwatch.ElapsedMilliseconds,
            violations.Count == 0 ? string.Empty : $" — {violations[0].Rule}");

        var first = violations.Count > 0 ? violations[0] : null;
        outcomes.Add(new MechanicRuleOutcome(
            Rule: guardrail.RuleFamily,
            Outcome: violations.Count == 0 ? MechanicClaimValidationOutcomes.Pass : MechanicClaimValidationOutcomes.Fail,
            Message: first?.Message,
            Path: first?.Path,
            Score: detailed.Score,
            Violations: violations));

        if (violations.Count > 0)
        {
            foreach (var v in violations)
            {
                MeepleAiMetrics.MechanicValidatorViolations.Add(1, new System.Diagnostics.TagList
                {
                    { "rule", v.Rule }
                });
            }

            // Fail-fast: every guardrail AFTER this one is notRun.
            for (var j = i + 1; j < _guardrails.Count; j++)
            {
                outcomes.Add(new MechanicRuleOutcome(
                    Rule: _guardrails[j].RuleFamily,
                    Outcome: MechanicClaimValidationOutcomes.NotRun,
                    Message: null, Path: null, Score: null,
                    Violations: Array.Empty<MechanicValidationViolation>()));
            }

            return MechanicValidationResult.Invalid(violations, outcomes);
        }
    }

    return MechanicValidationResult.Valid(outcomes);
}
```
Add `using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;` for `MechanicClaimValidationOutcomes`. (The existing `using System.Diagnostics;` for `Stopwatch` + `Api.Observability` for `MeepleAiMetrics` stay.)

- [ ] **Step 5: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicOutputValidatorRuleOutcomesTests|FullyQualifiedName~MechanicOutputValidatorChainTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS (new outcomes tests + existing `MechanicOutputValidatorChainTests` in `MechanicGuardrailTests.cs`, which assert `IsValid`/`Violations` — unchanged) + build OK.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/IMechanicOutputValidator.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputValidator.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 validator accumulates RuleOutcomes during fail-fast pass (D1)"
```

---

## Task 4: BE-core — parser source-index anchor on every claim (D4)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs` (add `SourceAnchor` + carry it through `CreateWithId`/`Reconstitute`)
- Modify: `…/Application/Services/MechanicExtractor/MechanicOutputParser.cs` (stamp anchor in all 6 Parse* methods + `BuildClaim`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputParserAnchorTests.cs` (create)

**Interfaces:**
- Consumes: existing `MechanicClaim.CreateWithId(...)`, `MechanicOutputParser.Parse(Guid, IReadOnlyDictionary<MechanicSection,string>)`, `BuildClaim(...)`.
- Produces: `MechanicClaim.SourceAnchor : string` (e.g. `$.mechanics[2]`, `$.victory`, `$.summary`); `CreateWithId(..., string sourceAnchor)` (new LAST required param); `Reconstitute(..., string? sourceAnchor = null)` (new optional-LAST param); `BuildClaim(..., string sourceAnchor)`.

> ⚠️ **The anchor must be the RAW source array index**, captured BEFORE drops/reorder/compaction. `displayOrder` is compacted (only increments on emitted claims) and Phases are reordered by `order`, so `displayOrder ≠ source index`. `ParsePhases` already buffers a `SourceIndex` — reuse it. The array sections (`Mechanics`/`Resources`/`Faq`) currently use a bare `foreach` — add an explicit `sourceIndex++` counter that increments on EVERY item (including dropped ones).

- [ ] **Step 1: Write the failing test** — a Mechanics section whose item #0 is DROPPED (no citations) and item #1 is emitted; assert the emitted claim's `SourceAnchor` is `$.mechanics[1]` (raw index), NOT `$.mechanics[0]` (which `displayOrder` would suggest). Also assert Victory primary + alternative both anchor `$.victory`.

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicOutputParserAnchorTests
{
    [Fact]
    public void Parse_StampsRawSourceIndexAnchor_EvenWhenEarlierItemsDropped()
    {
        // item[0] has NO citations → dropped; item[1] emitted. Anchor must be $.mechanics[1].
        var json = """
        {"mechanics":[
          {"description":"no cite mechanic"},
          {"description":"good mechanic","citations":[{"pdf_page":2,"quote":"do the thing"}]}
        ]}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().ContainSingle();
        claims[0].SourceAnchor.Should().Be("$.mechanics[1]");
        claims[0].DisplayOrder.Should().Be(0); // compacted — proves anchor != displayOrder
    }

    [Fact]
    public void Parse_Victory_AnchorsPrimaryAndAlternatives_ToVictoryObject()
    {
        var json = """
        {"victory":{"primary":"most points wins","alternatives":["instant win on 10 gems"],
          "citations":[{"pdf_page":5,"quote":"points win"}]}}
        """;
        var outputs = new Dictionary<MechanicSection, string> { [MechanicSection.Victory] = json };

        var claims = MechanicOutputParser.Parse(Guid.NewGuid(), outputs);

        claims.Should().HaveCount(2);
        claims.Should().OnlyContain(c => c.SourceAnchor == "$.victory");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicOutputParserAnchorTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — `MechanicClaim.SourceAnchor` does not exist.

- [ ] **Step 3: Add `SourceAnchor` to `MechanicClaim`.** Add the property + thread it through `CreateWithId` and `Reconstitute`:

```csharp
    /// <summary>
    /// Stable JSONPath anchor of this claim's RAW source object, captured by the parser before any
    /// drop/reorder/compaction (#2782 D4), e.g. "$.mechanics[2]" or "$.victory". Used to correlate a
    /// guardrail violation's Path to exactly one claim WITHIN the originating pipeline execution.
    /// NOT persisted or reloaded — it is empty on ALL reconstituted claims (there is no
    /// mechanic_claims column for it), so it is only meaningful during the run that parsed the claim.
    /// </summary>
    public string SourceAnchor { get; private set; } = string.Empty;
```
In `CreateWithId`, add `string sourceAnchor` as the LAST required parameter and set `claim.SourceAnchor = sourceAnchor;`. In `Reconstitute`, add `string? sourceAnchor = null` as the LAST optional parameter (after `reviewNote`) and set `claim.SourceAnchor = sourceAnchor ?? string.Empty;`. (`Create` — the pending-claim factory used outside the parser — is untouched; leave `SourceAnchor` empty there.)

- [ ] **Step 4: Stamp the anchor in the parser.** In `MechanicOutputParser.cs`, change `BuildClaim` to accept + forward the anchor:

```csharp
private static MechanicClaim BuildClaim(
    Guid claimId, Guid analysisId, MechanicSection section, string text,
    int displayOrder, IReadOnlyList<MechanicCitation> citations, string sourceAnchor)
{
    return MechanicClaim.CreateWithId(
        id: claimId, analysisId: analysisId, section: section,
        text: text.Trim(), displayOrder: displayOrder, citations: citations,
        sourceAnchor: sourceAnchor);
}
```
Then at each `BuildClaim(...)` call site pass the raw anchor:
- **Array sections** (`ParseMechanics`, `ParseResources`, `ParseFaq`): add `var sourceIndex = 0;` before the `foreach`, increment it on EVERY iteration (BEFORE any `continue`), and pass `sourceAnchor: $"$.{arrayKey}[{sourceIndex}]"` where `arrayKey` is `mechanics`/`resources`/`faq`. Example for `ParseMechanics`:
  ```csharp
  var sourceIndex = 0;
  var displayOrder = 0;
  foreach (var item in arr.EnumerateArray())
  {
      var anchor = $"$.mechanics[{sourceIndex}]";
      sourceIndex++;
      // ... existing drop guards (continue) unchanged ...
      yield return BuildClaim(claimId, analysisId, MechanicSection.Mechanics, text!, displayOrder++, citations, anchor);
  }
  ```
- **`ParsePhases`**: it already buffers `(Order, SourceIndex, Element)`. Pass `sourceAnchor: $"$.phases[{sourceIndex}]"` using the buffered `SourceIndex` (destructure it back out — change `foreach (var (_, _, item) in ordered)` to `foreach (var (_, srcIdx, item) in ordered)`).
- **`ParseSummary`**: single object, pass `sourceAnchor: "$.summary"`.
- **`ParseVictory`**: pass `sourceAnchor: "$.victory"` for BOTH the primary and every alternative `BuildClaim` (the documented approximation — Victory violations attribute to the primary; alternatives inherit pass).

- [ ] **Step 5: Fix the compile of existing callers** — `CreateWithId` now requires `sourceAnchor`. The ONLY production caller is `BuildClaim` (patched). Any test that calls `MechanicClaim.CreateWithId(...)` directly must add the new arg — build to find them, then add `sourceAnchor: "$.test"` to each. `Reconstitute`'s new param is optional-and-last, so its callers keep compiling.

- [ ] **Step 6: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicOutputParserAnchorTests|FullyQualifiedName~MechanicOutputParserTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS (new anchor tests + existing parser tests) + build OK.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicOutputParser.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 stamp raw-source-index anchor on parsed claims (D4)"
```

---

## Task 5: BE-core — pipeline SectionOutcomes + run-all-retain + carve-outs (D3)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisPipeline.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisPipelineTests.cs` (create — **no RunAsync tests exist today**)

**Interfaces:**
- Consumes: `MechanicValidationResult.RuleOutcomes` (Task 3), `MechanicRuleOutcome` (Task 3), existing `RunSectionAsync`, `MechanicPipelineResult`, `MechanicPipelineOutcome`, `MechanicAnalysisSectionRunEntity`. (The validator's `ValidateAsync` already returns `RuleOutcomes` — the pipeline just keeps the LAST attempt's; no second validator call.)
- Produces: `MechanicPipelineResult.SectionOutcomes : IReadOnlyDictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>` (new **init-only property with an empty default**, NOT a positional param — see Step 3); run-all-retain behavior; `MechanicAnalysisSectionRunEntity.Status = 3` (RetainedWithGuardrailFlags) on retained guardrail-failed sections.

> **Verified reality vs. spec prose:** the result type is `MechanicPipelineResult(Outcome, SectionRuns, SectionOutputs, TotalPromptTokens, TotalCompletionTokens, TotalCostUsd, AbortDetail)` where `SectionOutputs : IReadOnlyDictionary<MechanicSection,string>` and `SectionRuns : IReadOnlyList<MechanicAnalysisSectionRunEntity>`. `RunSectionAsync` returns `(MechanicAnalysisSectionRunEntity Run, string? Output, MechanicPipelineOutcome? Abort)` and builds the section-run entity directly (Status int: 0=Succeeded, 1=Failed). `ValidateSectionAsync` does the `well_formed` pre-check then calls the fail-fast `_validator.ValidateAsync` (which now — Task 3 — returns `RuleOutcomes` accumulated during that pass). `RunSectionAsync`'s current final-failure branch returns `Abort: AbortedValidation` and `Output: null`, discarding the output. This task changes that branch to KEEP the last attempt's `MechanicValidationResult.RuleOutcomes` (no re-validation, no `ValidateAllAsync`), classify by the last result's violation rule strings, and either retain (Status=3, `Abort: null`, output kept) or hard-abort (grounding-unavailable → AbortedValidation) or leave absent (never well-formed → `Abort: null, Output: null, Outcomes: empty`).
>
> **well_formed distinction (verified):** `ValidateSectionAsync` returns a `well_formed` violation both for empty output and for a `JsonException`. To classify at the end of the retry loop, hoist two locals OUT of the loop: `MechanicValidationResult? lastValidation` and `string? lastCleanedResponse`, assigned on every attempt. After the loop, the section is "never well-formed" iff `lastValidation` is null OR its violations are ALL `Rule == "well_formed"` → return `(run Status=1, Output: null, Abort: null, Outcomes: empty)` so the section is simply absent. Otherwise at least one real guardrail ran → retain (or hard-abort on grounding-unavailable).

- [ ] **Step 1: Write the failing tests** — construct `MechanicAnalysisPipeline` with a mock `ILlmService`, a stubbed `IMechanicOutputValidator` (so you control `RuleOutcomes` per attempt), a stub `IMechanicPromptProvider`, `TimeProvider.System`, `IOptions<MechanicGuardrailOptions>`. Cover four cases. Because wiring the full pipeline is heavy, use small fakes; verify the classification, not LLM internals.

```csharp
// MechanicAnalysisPipelineTests.cs (namespace ...Application.Services.MechanicExtractor — WITH Services) — 4 scenarios:
// (a) guardrail-fail section (validator returns Invalid([T2]) with RuleOutcomes
//     [T1 pass, T2 fail, T3a notRun, T3b notRun, T4 notRun], JSON well-formed) →
//     Outcome == Succeeded (NOT AbortedValidation); SectionOutputs contains the section;
//     SectionOutcomes[section] has a T2 fail; the section-run Status == 3.
// (b) well_formed fail (LLM always returns "not json" → validator returns Invalid([well_formed]),
//     RuleOutcomes empty) → section ABSENT from SectionOutputs, ABSENT from SectionOutcomes;
//     pipeline continues to next sections (no hard abort from this alone).
// (c) grounding UNAVAILABLE (validator's last Invalid carries a violation Rule=="T3_grounding_unavailable")
//     → Outcome == AbortedValidation (hard abort).
// (d) LLM failure (ILlmService returns Success=false) → Outcome == AbortedLlmFailed (unchanged).
```
Sketch of case (a) (adapt fakes to the real ctor signature — inspect `MechanicAnalysisPipeline`'s constructor + `ILlmService.GenerateCompletionWithModelAsync` return shape first). The stub validator returns the same `Invalid(violations, ruleOutcomes)` on every attempt so the retry budget is spent, then the pipeline retains the LAST result's `RuleOutcomes`:
```csharp
[Fact]
public async Task RunAsync_GuardrailFailSection_IsRetained_NotAborted_WithStatus3()
{
    var request = BuildRequest(sections: new[] { MechanicSection.Summary });
    // LLM returns well-formed JSON with a T2-violating claim on every attempt;
    // stub validator.ValidateAsync → Invalid([T2], RuleOutcomes=[T1 pass, T2 fail, T3a/T3b/T4 notRun]) each attempt.
    var pipeline = BuildPipeline(/* fakes */);

    var result = await pipeline.RunAsync(request, CancellationToken.None);

    result.Outcome.Should().Be(MechanicPipelineOutcome.Succeeded);
    result.SectionOutputs.Should().ContainKey(MechanicSection.Summary);
    result.SectionOutcomes[MechanicSection.Summary].Single(o => o.Rule == "T2").Outcome.Should().Be("fail");
    result.SectionRuns.Single(r => r.Section == (int)MechanicSection.Summary).Status.Should().Be(3);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicAnalysisPipelineTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — `SectionOutcomes` does not exist / current code aborts on validation.

- [ ] **Step 3: Add `SectionOutcomes` to `MechanicPipelineResult` as a NON-positional init property with a default.** Do NOT add it as a positional record param — that would break `MechanicAnalysisExecutorApplyAbortTests.cs`'s `BuildAbortResult` (verified: it constructs `new MechanicPipelineResult(...)` with the 7 named positional args). Mirror how `MechanicPipelineRequest.SourceChunksBySection` is declared (init property + default). In `IMechanicAnalysisPipeline.cs`, leave the 7-arg primary constructor UNCHANGED and add inside the record body:

```csharp
public sealed record MechanicPipelineResult(
    MechanicPipelineOutcome Outcome,
    IReadOnlyList<MechanicAnalysisSectionRunEntity> SectionRuns,
    IReadOnlyDictionary<MechanicSection, string> SectionOutputs,
    int TotalPromptTokens,
    int TotalCompletionTokens,
    decimal TotalCostUsd,
    string? AbortDetail)
{
    public bool IsSuccess => Outcome == MechanicPipelineOutcome.Succeeded;

    /// <summary>Per-section final-attempt guardrail rule outcomes (#2782 D3). Empty on sections that
    /// never produced well-formed output. Init-only with an empty default so existing positional
    /// constructions (incl. the ApplyAbort tests) keep compiling.</summary>
    public IReadOnlyDictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>> SectionOutcomes { get; init; }
        = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>();

    // ... existing AbortReason switch unchanged ...
}
```
Add `using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;` if `MechanicRuleOutcome` is not already in scope in `IMechanicAnalysisPipeline.cs` (both live under `...Application.Services.MechanicExtractor`, so likely no import needed). Producers set it via object-initializer: `new MechanicPipelineResult(...) { SectionOutcomes = sectionOutcomesMap }`.

- [ ] **Step 4: Change `RunSectionAsync` to KEEP the last attempt's `RuleOutcomes` (no re-validation) + classify.** Two edits inside `RunSectionAsync`:

  **(i) In the retry loop**, hoist and assign two locals on every attempt so the classification can inspect the final state:
  ```csharp
  MechanicValidationResult? lastValidation = null;
  string? lastCleanedResponse = null;
  // ... inside the loop, after computing `cleanedResponse` and `validation`:
  lastValidation = validation;
  lastCleanedResponse = cleanedResponse;
  ```
  On the SUCCESS branch (`validation.IsValid`), return the run (Status=0) with `Outcomes: validation.RuleOutcomes` (they were captured during the passing pass, incl. the T3b score) — see (iii) for the tuple change.

  **(ii) Replace the post-loop validation-failure tail** (currently builds `validationFailureRun` Status=1 and returns `Abort: AbortedValidation`). New classification using `lastValidation`:
  - **Never well-formed** — `lastValidation is null` OR `lastValidation.Violations.All(v => v.Rule == "well_formed")`: return `(validationFailureRun with Status=1, Output: null, Abort: null, Outcomes: Array.Empty<MechanicRuleOutcome>())` → section absent (case b). Keep the existing `ErrorMessage = $"Validation failed after {attempts} attempts: {lastValidationError}"`.
  - **Grounding outage** — `lastValidation.Violations.Any(v => v.Rule == "T3_grounding_unavailable")`: return `(validationFailureRun with Status=1, Output: null, Abort: MechanicPipelineOutcome.AbortedValidation, Outcomes: Array.Empty<MechanicRuleOutcome>())` → hard abort (fail-closed, unchanged behaviour).
  - **Ordinary guardrail fail (retain)** — else: build the run with `Status = 3` and return `(retainedRun, Output: lastCleanedResponse, Abort: null, Outcomes: lastValidation.RuleOutcomes)`. (The `ErrorMessage` summary is set in Task 10.)

  **(iii) Change `RunSectionAsync`'s return tuple** to carry the outcomes:
  ```csharp
  private async Task<(MechanicAnalysisSectionRunEntity Run, string? Output,
      MechanicPipelineOutcome? Abort, IReadOnlyList<MechanicRuleOutcome> Outcomes)>
      RunSectionAsync(...)
  ```
  The LLM-fail early-return (`result.Success == false`) returns `Outcomes: Array.Empty<MechanicRuleOutcome>()`.

- [ ] **Step 5: Consume the outcomes in `RunAsync`.** In the section loop:
  - Destructure the 4-tuple: `var (sectionRun, sectionOutput, sectionAbort, sectionOutcomes) = await RunSectionAsync(...)`.
  - Add a `var sectionOutcomesMap = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>();` before the loop; after each section, `if (sectionOutcomes.Count > 0) sectionOutcomesMap[section] = sectionOutcomes;`.
  - The existing `if (sectionAbort is not null) return BuildAbortResult(...)` branch is **kept as-is** — Step 4 now returns `Abort: null` for retained + never-well-formed sections and `Abort: AbortedValidation` ONLY for grounding-unavailable, so this branch correctly hard-aborts only on grounding-unavailable + LLM-fail. Cost-cap check unchanged.
  - Retained sections (Abort null, Output non-null) are added to `outputs[section]` as today; never-well-formed sections (Abort null, Output null) are skipped by the existing `if (sectionOutput is not null)` guard.
  - Pass `sectionOutcomesMap` into the final success result via object-initializer: `new MechanicPipelineResult(...) { SectionOutcomes = sectionOutcomesMap }`. Extend `BuildAbortResult` to take the partial `sectionOutcomesMap` and set it the same way on its returned result (so salvaged claims on a grounding/cost/LLM abort still correlate).

- [ ] **Step 6: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicAnalysisPipelineTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS (4 scenarios) + build OK.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisPipeline.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 pipeline run-all-retain + SectionOutcomes + carve-outs (D3)"
```

---

## Task 6: BE-core — correlate SectionOutcomes → claims in the executor (D4)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisExecutor.cs`
- Modify: `…/Domain/Entities/MechanicClaim.cs` (add `AttachValidations`)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisExecutorCorrelationTests.cs` (create) — the **core anti-false-positive test**

**Interfaces:**
- Consumes: `MechanicPipelineResult.SectionOutcomes` (Task 5), `MechanicClaim.SourceAnchor` (Task 4), `MechanicRuleOutcome` (Task 3), `MechanicClaimValidation` VO (Task 1).
- Produces: `MechanicClaim.AttachValidations(IReadOnlyList<MechanicClaimValidation>)`; a private `CorrelateValidations(analysis, result)` in the executor that, for each claim, computes its per-rule validations by matching each `RuleOutcome`'s violation `Path` against the claim's `SourceAnchor` prefix (no violation for a rule that ran → `pass`).

> **Correlation algorithm:** for a claim with anchor `A` and section `S`, for each `RuleOutcome` in `SectionOutcomes[S]`:
> - if the rule's `Outcome == pass` → the claim gets `{rule, pass, null, score}`;
> - if `fail` → the claim gets `fail` ONLY IF some violation in that outcome has a `Path` whose prefix matches `A` (i.e. `path == A` or `path.StartsWith(A + ".")` or `path.StartsWith(A + "[")`); otherwise the claim gets `pass` for that rule (the failure belonged to a sibling claim);
> - `notRun` → `notRun`.
> **Victory carve-out:** all Victory claims share anchor `$.victory`, so a Victory violation attributes to ALL Victory claims sharing the anchor — but per D4 the parser only emits the primary as independently evaluated; alternatives inherit whatever the primary gets. In practice matching `$.victory` prefix flags all victory claims identically; the plan accepts this documented approximation (a Victory `fail` means the section's primary failed).

- [ ] **Step 1: Write the failing correlation test** — a Mechanics section with 2 claims (anchors `$.mechanics[0]`, `$.mechanics[1]`); T2 outcome fails with a violation Path `$.mechanics[1].description`. Assert ONLY the second claim carries a T2 `fail`; the first is all `pass`.

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Services.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicAnalysisExecutorCorrelationTests
{
    [Fact]
    public void Correlate_FlagsOnlyTheOffendingClaim_SiblingsPass()
    {
        // Build two claims with distinct anchors via the parser (raw-index anchors).
        var json = """
        {"mechanics":[
          {"description":"clean mechanic","citations":[{"pdf_page":1,"quote":"clean"}]},
          {"description":"verbatim mechanic","citations":[{"pdf_page":2,"quote":"verbatim"}]}
        ]}
        """;
        var claims = MechanicOutputParser.Parse(Guid.NewGuid(),
            new Dictionary<MechanicSection, string> { [MechanicSection.Mechanics] = json }).ToList();

        var outcomes = new Dictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>
        {
            [MechanicSection.Mechanics] = new[]
            {
                new MechanicRuleOutcome("T1", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T2", "fail", "long verbatim", "$.mechanics[1].description", null,
                    new[] { new MechanicValidationViolation("T2_long_verbatim", "long verbatim", "$.mechanics[1].description") }),
                new MechanicRuleOutcome("T3a", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T3b", "pass", null, null, 0.8, Array.Empty<MechanicValidationViolation>()),
                new MechanicRuleOutcome("T4", "pass", null, null, null, Array.Empty<MechanicValidationViolation>()),
            }
        };

        MechanicAnalysisExecutor.CorrelateValidations(claims, outcomes); // internal static, exposed for test

        claims[0].Validations.Single(v => v.Rule == "T2").Outcome.Should().Be("pass");
        claims[1].Validations.Single(v => v.Rule == "T2").Outcome.Should().Be("fail");
        claims[1].Validations.Should().HaveCount(5); // T1,T2,T3a,T3b,T4
        claims[1].Validations.Single(v => v.Rule == "T3b").Score.Should().Be(0.8);
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicAnalysisExecutorCorrelationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — `AttachValidations` / `CorrelateValidations` do not exist.

- [ ] **Step 3: Add `AttachValidations` to `MechanicClaim`** (after `SourceAnchor`):

```csharp
    private readonly List<MechanicClaimValidation> _validations = new();
    public IReadOnlyList<MechanicClaimValidation> Validations => _validations.AsReadOnly();

    /// <summary>Attach the correlated per-rule guardrail outcomes captured at pipeline time (#2782 D4).</summary>
    internal void AttachValidations(IReadOnlyList<MechanicClaimValidation> validations)
    {
        ArgumentNullException.ThrowIfNull(validations);
        _validations.Clear();
        _validations.AddRange(validations);
    }
```
Add `using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;`. Also accept an optional `IEnumerable<MechanicClaimValidation>? validations = null` in `Reconstitute` — append it as the LAST optional parameter, i.e. AFTER the `sourceAnchor` param that Task 4 added (final order: `…, reviewNote, sourceAnchor, validations`). Inside `Reconstitute`, `if (validations is not null) { claim._validations.AddRange(validations); }` so the read-mapper (Task 8) can pre-fill `_validations` on reload. Both new params are optional, so `MapClaimToDomain` skips `sourceAnchor` (not persisted) via named args — see Task 8 Step 4.

- [ ] **Step 4: Implement `CorrelateValidations` in the executor** as an `internal static` method (testable) + call it from `ApplySuccessAsync`/`ApplyAbort` after `AddClaim`:

```csharp
internal static void CorrelateValidations(
    IReadOnlyList<MechanicClaim> claims,
    IReadOnlyDictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>> sectionOutcomes)
{
    foreach (var claim in claims)
    {
        if (!sectionOutcomes.TryGetValue(claim.Section, out var outcomes) || outcomes.Count == 0)
        {
            continue; // section had no captured outcomes (e.g. succeeded pre-anchor path) → leave empty
        }

        var perClaim = new List<MechanicClaimValidation>(outcomes.Count);
        foreach (var o in outcomes)
        {
            var outcome = o.Outcome;
            if (outcome == MechanicClaimValidationOutcomes.Fail)
            {
                var hits = o.Violations.Any(v => MatchesAnchor(v.Path, claim.SourceAnchor));
                outcome = hits ? MechanicClaimValidationOutcomes.Fail : MechanicClaimValidationOutcomes.Pass;
            }
            perClaim.Add(new MechanicClaimValidation(o.Rule, outcome,
                outcome == MechanicClaimValidationOutcomes.Fail ? o.Message : null, o.Score));
        }
        claim.AttachValidations(perClaim);
    }
}

private static bool MatchesAnchor(string? violationPath, string anchor)
{
    if (string.IsNullOrEmpty(violationPath) || string.IsNullOrEmpty(anchor)) { return false; }
    return violationPath == anchor
        || violationPath.StartsWith(anchor + ".", StringComparison.Ordinal)
        || violationPath.StartsWith(anchor + "[", StringComparison.Ordinal);
}
```
Wire it in `ApplySuccessAsync` by calling `CorrelateValidations(parsed, result.SectionOutcomes);` immediately BEFORE the `foreach (var claim in parsed) analysis.AddClaim(claim);` loop (verified: `parsed` is the `IReadOnlyList<MechanicClaim>` returned by `MechanicOutputParser.Parse(...)`, and `AddClaim` adds those same references — so attaching validations to `parsed` before the loop persists them on the aggregate's claims). In `ApplyAbort` (verified `internal static`, salvages into a local `salvaged`), call `CorrelateValidations(salvaged, result.SectionOutcomes);` immediately BEFORE its `foreach (var claim in salvaged) analysis.AddClaim(claim);` loop. Do NOT reference a `parsedIds` set — none exists.

- [ ] **Step 5: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicAnalysisExecutorCorrelationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS + build OK.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisExecutor.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Entities/MechanicClaim.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 correlate SectionOutcomes to claims by anchor (D4)"
```

---

## Task 7: BE-core — `validations jsonb` column + migration + jsonb converter (D5)

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs`
- Create: migration `AddMechanicClaimValidations`
- Test: covered by Task 8's round-trip (config compiles + migration applies here)

**Interfaces:**
- Produces: `MechanicClaimEntity.Validations : List<MechanicClaimValidation>?` mapped to nullable `validations jsonb` with a JSON `ValueConverter` (reuse the domain VO `MechanicClaimValidation` directly as the persistence shape `{Rule, Outcome, Message?, Score?}` — no parallel infra record).

> **A ValueComparer is NOT required for correctness (M4 reframe, verified).** `MechanicAnalysisRepository.Update()` maps a **fresh detached entity** and force-sets `EntityState.Modified` on each claim, so EF writes every column unconditionally — the snapshot-diff a comparer feeds is never consulted for the only write path. The converter alone stores the list as jsonb correctly on that write. A comparer would be inert here; **you may add one** (harmless, and future-proof for any tracked-mutation path) but it is optional and it is NOT what Task 8's round-trip red-gates (that test proves the WRITE + READ mapper points). Adding it is a judgement call — the mirror precedent `ModelCompatibilityEntryEntityConfiguration` has none, so the DEFAULT here is: converter only.

- [ ] **Step 1: Add the entity property.** In `MechanicClaimEntity.cs`, after `ReviewNote`:

```csharp
    /// <summary>
    /// Real per-claim guardrail outcomes captured at pipeline time (#2782 FU-1). Null for pre-FU-1
    /// claims (legacy all-pass derivation applies). Stored as jsonb via a value converter. No value
    /// comparer is needed because MechanicAnalysisRepository.Update() rebuilds a detached entity and
    /// force-writes all columns (the snapshot-diff a comparer feeds is never consulted for that path).
    /// </summary>
    public List<MechanicClaimValidation>? Validations { get; set; }
```
Add `using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;`.

- [ ] **Step 2: Map the column with a jsonb converter.** In `MechanicClaimEntityConfiguration.cs`, after the `ReviewNote` mapping (mirror `ModelCompatibilityEntryEntityConfiguration`'s converter-only jsonb pattern — no comparer):

```csharp
        var validationsConverter = new ValueConverter<List<MechanicClaimValidation>?, string?>(
            v => v == null ? null : JsonSerializer.Serialize(v, (JsonSerializerOptions?)null),
            v => v == null ? null : JsonSerializer.Deserialize<List<MechanicClaimValidation>>(v, (JsonSerializerOptions?)null));

        builder.Property(c => c.Validations)
            .HasColumnName("validations")
            .HasColumnType("jsonb")
            .HasConversion(validationsConverter);
```
Add `using System.Text.Json;`, `using Microsoft.EntityFrameworkCore.Storage.ValueConversion;`, `using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;`. (The correctness driver is the converter + the WRITE/READ mappers in Task 8, NOT a comparer — see the note above.)

- [ ] **Step 3: Generate the migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddMechanicClaimValidations`
Expected: migration adds nullable `validations jsonb` to `mechanic_claims`; snapshot updated.

- [ ] **Step 4: Review the migration SQL.** Confirm `Up` is a single additive `AddColumn<string>(name: "validations", ... type: "jsonb", nullable: true)` on `mechanic_claims`; `Down` is `DropColumn`. No other tables touched. Do not hand-edit.

- [ ] **Step 5: Apply + build**

Run: `cd apps/api/src/Api && dotnet ef database update && dotnet build`
Expected: applies; build OK. (Requires the dev Postgres running.)

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs apps/api/src/Api/Infrastructure/Migrations
git commit -m "feat(mechanic-extractor): #2782 add validations jsonb column + converter (D5)"
```

---

## Task 8: BE-core — write + read mappers + mutate-then-resave round-trip (D5, M4)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicAnalysisRepository.cs` (`MapClaimToEntity` write + `MapClaimToDomain` read)
- Test: `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/MechanicAnalysisRepositoryValidationsIntegrationTests.cs` (create — Testcontainers)

**Interfaces:**
- Consumes: `MechanicClaim.Validations` + `Reconstitute(... validations)` (Task 6), `MechanicClaim.AttachValidations` (Task 6), `MechanicClaimEntity.Validations` (Task 7).
- Produces: round-trip persistence of `validations` incl. survival across an UPDATE.

> **The M4 trap (WRITE+READ mappers, not a comparer):** the write mapper `MapClaimToEntity` (`MechanicAnalysisRepository.cs:395`) and read mapper `MapClaimToDomain` (`:325`) both need the field. The Moq/handler tests read the in-memory domain object and stay green even if the WRITE mapper drops it — ONLY this round-trip catches a dropped mapper copy. Note that `Update()` rebuilds a **fresh detached entity** and force-writes all columns (`EntityState.Modified`), so the WRITE always fires when the mapper copies the field; no value-comparer is involved. The test's mutate-then-resave leg proves the field survives a second write (i.e. the mapper still copies it after an unrelated mutation), which is exactly the #526 regression shape (a mapper copy silently omitted). The INSERT leg proves the READ mapper reconstitutes the column.

- [ ] **Step 1: Write the failing integration test** (Testcontainers) — seed an analysis with a claim carrying validations, save, reload (proves INSERT + READ mapper), then **mutate** (approve the reloaded claim), `Update`, reload again, assert validations survived the UPDATE (proves the WRITE mapper still copies the field).

```csharp
[Fact]
public async Task ClaimValidations_SurviveInsertReloadMutateAndResave()
{
    // 1. Build an InReview analysis with one claim carrying real validations.
    var analysis = BuildInReviewAnalysisWithValidatedClaim(); // seed helper: claim.AttachValidations([T1 pass, T2 fail(...)])
    await _repository.AddAsync(analysis, CancellationToken.None);
    await _unitOfWork.SaveChangesAsync(CancellationToken.None);

    // 2. Reload → validations survived the INSERT (write mapper) + reconstitute (read mapper).
    var afterInsert = await _repository.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id, CancellationToken.None);
    var claim = afterInsert!.Claims.Single();
    claim.Validations.Should().HaveCount(2);
    claim.Validations.Single(v => v.Rule == "T2").Outcome.Should().Be("fail");

    // 3. MUTATE the claim graph (approve it) + Update — Update rebuilds a detached entity and
    //    force-writes every column, so this proves the WRITE mapper still copies validations
    //    after an unrelated mutation (the #526 dropped-mapper regression shape).
    afterInsert.ApproveClaim(claim.Id, Guid.NewGuid(), DateTime.UtcNow);
    _repository.Update(afterInsert);
    await _unitOfWork.SaveChangesAsync(CancellationToken.None);

    var afterUpdate = await _repository.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id, CancellationToken.None);
    var reloaded = afterUpdate!.Claims.Single();
    reloaded.Status.Should().Be(MechanicClaimStatus.Approved);
    reloaded.Validations.Should().HaveCount(2); // validations still there after UPDATE
    reloaded.Validations.Single(v => v.Rule == "T2").Message.Should().NotBeNull();
}
```
> Reuse the existing Testcontainers fixture pattern (`SharedTestcontainersFixture` / `[Collection("Integration-GroupC")]`) and the `_repository`/`_unitOfWork`/`GetByIdWithClaimsIgnoringFiltersAsync` seen in `MechanicAnalysisRepository`. Add `BuildInReviewAnalysisWithValidatedClaim` locally (mirror existing seed helpers; call `claim.AttachValidations(...)` before `AddAsync`).

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicAnalysisRepositoryValidationsIntegrationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — mappers don't copy `Validations` (validations null after reload). (Docker must be running.)

- [ ] **Step 3: Patch the WRITE mapper** — in `MapClaimToEntity` (`:395`), add after `ReviewNote = claim.ReviewNote,`:

```csharp
        Validations = claim.Validations.Count == 0
            ? null
            : claim.Validations.ToList(),
```

- [ ] **Step 4: Patch the READ mapper** — in `MapClaimToDomain` (`:325`), pass the jsonb column into `Reconstitute` via the named `validations:` arg (skip `sourceAnchor` — it is not persisted, and named args let us omit it):

```csharp
        return MechanicClaim.Reconstitute(
            id: entity.Id,
            analysisId: entity.AnalysisId,
            section: (MechanicSection)entity.Section,
            text: entity.Text,
            displayOrder: entity.DisplayOrder,
            status: (MechanicClaimStatus)entity.Status,
            reviewedBy: entity.ReviewedBy,
            reviewedAt: entity.ReviewedAt,
            rejectionNote: entity.RejectionNote,
            citations: citations,
            reviewNote: entity.ReviewNote,
            validations: entity.Validations);
```

- [ ] **Step 5: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicAnalysisRepositoryValidationsIntegrationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS (insert + update round-trip) + build OK.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Infrastructure/Repositories/MechanicAnalysisRepository.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 persist claim validations across insert+update (D5 M4)"
```

---

## Task 9: BE-core — section-run Status=3 CHECK migration (D9)

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicAnalysisSectionRunEntityConfiguration.cs`
- Create: migration `WidenMechanicSectionRunStatusRange`
- Test: `apps/api/tests/Api.Tests/Integration/SharedGameCatalog/MechanicSectionRunStatus3IntegrationTests.cs` (create — Testcontainers)

**Interfaces:**
- Produces: `ck_mechanic_section_runs_status_range` widened to `status BETWEEN 0 AND 3`; `3 = RetainedWithGuardrailFlags` accepted; `ck_..._error_when_failed` (`status <> 1 OR error_message IS NOT NULL`) UNCHANGED (Status=3 rows may have null error_message).

- [ ] **Step 1: Write the failing test** — insert a section-run row with `Status = 3` and assert it succeeds (proving the CHECK allows it).

```csharp
[Fact]
public async Task SectionRun_WithStatus3_RetainedWithGuardrailFlags_IsAccepted()
{
    var analysis = await SeedAnalysisAsync(); // existing seed helper producing a persisted analysis id
    var run = new MechanicAnalysisSectionRunEntity
    {
        Id = Guid.NewGuid(), AnalysisId = analysis.Id, Section = 0, RunOrder = 0,
        Provider = "deepseek", ModelUsed = "deepseek-chat",
        PromptTokens = 1, CompletionTokens = 1, TotalTokens = 2, EstimatedCostUsd = 0.0001m,
        LatencyMs = 5, Status = 3, ErrorMessage = null,
        StartedAt = DateTime.UtcNow, CompletedAt = DateTime.UtcNow
    };
    _dbContext.MechanicAnalysisSectionRuns.Add(run);

    var act = async () => await _dbContext.SaveChangesAsync();

    await act.Should().NotThrowAsync(); // fails today: CHECK status BETWEEN 0 AND 2 rejects 3
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicSectionRunStatus3IntegrationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — Postgres rejects Status=3 (`ck_mechanic_section_runs_status_range`).

- [ ] **Step 3: Widen the CHECK in the config** — in `MechanicAnalysisSectionRunEntityConfiguration.cs`, change:

```csharp
            t.HasCheckConstraint(
                "ck_mechanic_section_runs_status_range",
                "status BETWEEN 0 AND 3");
```
(Leave `ck_mechanic_section_runs_error_when_failed` = `"status <> 1 OR error_message IS NOT NULL"` unchanged — Status=3 is not Status=1, so no error_message is required.) Add a doc-comment noting `3 = RetainedWithGuardrailFlags` (D9).

- [ ] **Step 4: Generate + review the migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add WidenMechanicSectionRunStatusRange`
Expected: `Up` drops + re-adds `ck_mechanic_section_runs_status_range` with `BETWEEN 0 AND 3`; `Down` reverts to `0 AND 2`. Confirm no data-loss statements.

- [ ] **Step 5: Apply + run to verify it passes**

Run: `cd apps/api/src/Api && dotnet ef database update && dotnet test --filter "FullyQualifiedName~MechanicSectionRunStatus3IntegrationTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicAnalysisSectionRunEntityConfiguration.cs apps/api/src/Api/Infrastructure/Migrations apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 widen section-run status CHECK to 0..3 (D9)"
```

---

## Task 10: BE-core — wire Status=3 in the executor's section-run persistence (D9)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisPipeline.cs` (already sets Status=3 in Task 5) — verify the retained-section run entity carries Status=3 end-to-end through `MechanicAnalysisExecutor` persistence.
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisPipelineTests.cs` (extend Task 5's file)

**Interfaces:**
- Consumes: Task 5's `RunSectionAsync` Status=3 branch; the executor already `AddRangeAsync(result.SectionRuns, ...)` (no mapping — the pipeline builds the entities directly).

> **Verified:** `MechanicAnalysisExecutor.ExecuteAsync` persists `result.SectionRuns` verbatim via `_dbContext.MechanicAnalysisSectionRuns.AddRangeAsync(result.SectionRuns, ...)`. Because the pipeline builds `MechanicAnalysisSectionRunEntity` directly (Task 5 sets Status=3), no executor mapping change is needed — this task is a **verification test**, not new production code, folded into Task 5's suite. It exists as a distinct task only so a reviewer can confirm the Status=3 row reaches the DB honestly.

- [ ] **Step 1: Extend the pipeline test** — assert a retained guardrail-failed section produces a `SectionRuns` entry with `Status == 3` AND that entry has a non-empty `ErrorMessage` describing the flagged rules (telemetry stays honest, but the CHECK does not require it for Status=3):

```csharp
[Fact]
public async Task RunAsync_RetainedGuardrailSection_ProducesStatus3RunWithFlagSummary()
{
    var request = BuildRequest(sections: new[] { MechanicSection.Summary });
    var pipeline = BuildPipeline(/* T2 fails, JSON well-formed */);

    var result = await pipeline.RunAsync(request, CancellationToken.None);

    var run = result.SectionRuns.Single(r => r.Section == (int)MechanicSection.Summary);
    run.Status.Should().Be(3);
    run.ErrorMessage.Should().Contain("T2"); // honest flag summary
}
```
> If Task 5's Status=3 branch left `ErrorMessage` null, update it to summarize the failing rule families (join distinct `RuleFamily` of failed outcomes) so telemetry is legible.

- [ ] **Step 2: Run to verify it fails then implement the ErrorMessage summary**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~RunAsync_RetainedGuardrailSection" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL if the summary is missing. In `RunSectionAsync`'s retained (Status=3) branch, derive the failed families from the kept outcomes and set the message:
```csharp
var failedRuleFamilies = lastValidation!.RuleOutcomes
    .Where(o => o.Outcome == MechanicClaimValidationOutcomes.Fail)
    .Select(o => o.Rule)
    .Distinct();
// on the retained run entity:
ErrorMessage = $"Retained with guardrail flags: {string.Join(", ", failedRuleFamilies)}",
```
Add `using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;` to the pipeline file if not already present (for `MechanicClaimValidationOutcomes`).

- [ ] **Step 3: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicAnalysisPipelineTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS + build OK.

- [ ] **Step 4: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Services/MechanicExtractor/MechanicAnalysisPipeline.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 honest Status=3 flag summary on retained sections (D9)"
```

---

## Task 11: BE-core — flip the 5 DerivePass sites to persisted validations (D5, M3)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationDto.cs`
- Modify: `…/Application/Queries/MechanicExtractor/GetMechanicAnalysisClaimsQueryHandler.cs` (ENTITY source → `FromEntity`)
- Modify: `…/Application/Commands/MechanicExtractor/{Approve,Reject,BulkApprove,BulkReject}MechanicClaimsCommandHandler.cs` (4 DOMAIN sites → `FromDomain`)
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationsTests.cs` (existing — rename its method off `DerivePass_ReturnsFourPassBadges_T1ToT4`, call `DeriveLegacyAllPassFallback()`, assert the 5-family sequence)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationsMappingTests.cs` (create — `FromDomain`/`FromEntity` mapping)

**Interfaces:**
- Consumes: `MechanicClaim.Validations` (Task 6); `MechanicClaimEntity.Validations` (Task 7).
- Produces: `MechanicClaimValidationDto` gains `double? Score` (LAST positional param); `MechanicClaimValidations.Families = { "T1","T2","T3a","T3b","T4" }`; `MechanicClaimValidations.FromDomain(MechanicClaim claim)` (for the 4 command handlers) AND `MechanicClaimValidations.FromEntity(MechanicClaimEntity entity)` (for the query handler, reading `entity.Validations` jsonb) — both `: IReadOnlyList<MechanicClaimValidationDto>`, both falling back to `DeriveLegacyAllPassFallback()` when there are no validations; `DerivePass()` renamed `DeriveLegacyAllPassFallback()`.

> **Verified:** `MechanicClaimValidationDto` already exists as `record MechanicClaimValidationDto(string Rule, string Outcome, string? Message = null)`; the FE Zod schema + `MechanicClaimDto.Validations` already exist (shipped in #526). The 5 sites are NOT homogeneous: `GetMechanicAnalysisClaimsQueryHandler` queries `_dbContext.MechanicClaims` (entity `MechanicClaimEntity`) with `AsNoTracking().IgnoreQueryFilters()` — its `c` is the ENTITY → `FromEntity(c)`. The 4 command handlers project from `analysis.Claims` (domain `MechanicClaim`) → `FromDomain(claim)`. This task adds `Score`, the two overloads, and swaps the derivation source. There is also an existing test `MechanicClaimValidationsTests.DerivePass_ReturnsFourPassBadges_T1ToT4` asserting the OLD 4-rule `{T1,T2,T3,T4}` sequence + calling `DerivePass()` directly — it MUST be updated in this task or it fails to compile (the method is renamed).

- [ ] **Step 1: Write the failing mapping test**

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.DTOs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicClaimValidationsMappingTests
{
    [Fact]
    public void FromDomain_MapsRealValidations_IncludingScore()
    {
        var claim = MechanicClaim.CreateWithId(Guid.NewGuid(), Guid.NewGuid(), MechanicSection.Summary,
            "text", 0, Array.Empty<MechanicCitation>(), sourceAnchor: "$.summary");
        claim.AttachValidations(new[]
        {
            new MechanicClaimValidation("T2", "fail", "long verbatim", null),
            new MechanicClaimValidation("T3b", "pass", null, 0.83),
        });

        var dtos = MechanicClaimValidations.FromDomain(claim);

        dtos.Single(d => d.Rule == "T2").Outcome.Should().Be("fail");
        dtos.Single(d => d.Rule == "T3b").Score.Should().Be(0.83);
    }

    [Fact]
    public void FromDomain_FallsBackToLegacyAllPass_WhenClaimHasNoValidations()
    {
        var claim = MechanicClaim.CreateWithId(Guid.NewGuid(), Guid.NewGuid(), MechanicSection.Summary,
            "text", 0, Array.Empty<MechanicCitation>(), sourceAnchor: "$.summary");

        var dtos = MechanicClaimValidations.FromDomain(claim);

        dtos.Select(d => d.Rule).Should().Equal("T1", "T2", "T3a", "T3b", "T4");
        dtos.Should().OnlyContain(d => d.Outcome == "pass");
    }

    [Fact]
    public void FromEntity_MapsJsonbColumn_AndFallsBackWhenNull()
    {
        var withData = new Api.Infrastructure.Entities.SharedGameCatalog.MechanicClaimEntity
        {
            Validations = new List<MechanicClaimValidation>
            {
                new("T3b", "pass", null, 0.71),
            }
        };
        MechanicClaimValidations.FromEntity(withData).Single(d => d.Rule == "T3b").Score.Should().Be(0.71);

        var legacy = new Api.Infrastructure.Entities.SharedGameCatalog.MechanicClaimEntity { Validations = null };
        MechanicClaimValidations.FromEntity(legacy).Select(d => d.Rule).Should().Equal("T1", "T2", "T3a", "T3b", "T4");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicClaimValidationsMappingTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — `FromDomain` / `Score` do not exist.

- [ ] **Step 3: Extend `MechanicClaimValidationDto.cs`** — add `Score`, the two overloads (`FromDomain` for domain claims, `FromEntity` for the query handler's entity), a shared private mapper, and rename `DerivePass` → `DeriveLegacyAllPassFallback`:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Infrastructure.Entities.SharedGameCatalog;

namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Per-claim guardrail badge outcome. Rule ∈ {T1,T2,T3a,T3b,T4}; Outcome ∈ {pass,fail,notRun}.
/// Score is populated only for T3b (grounding cosine); null otherwise (#2782 FU-1).
/// </summary>
public sealed record MechanicClaimValidationDto(string Rule, string Outcome, string? Message = null, double? Score = null);

public static class MechanicClaimValidations
{
    /// <summary>The 5 canonical badge rules (dec. 6). Matches the guardrail RuleFamily values.</summary>
    public static readonly IReadOnlyList<string> Families = new[] { "T1", "T2", "T3a", "T3b", "T4" };

    /// <summary>
    /// Map a DOMAIN claim's REAL persisted validations to DTOs (used by the 4 command handlers that
    /// project from the in-memory aggregate). Falls back to the legacy all-pass shape ONLY for
    /// pre-FU-1 claims that carry no validations.
    /// </summary>
    public static IReadOnlyList<MechanicClaimValidationDto> FromDomain(MechanicClaim claim)
    {
        ArgumentNullException.ThrowIfNull(claim);
        return Map(claim.Validations);
    }

    /// <summary>
    /// Map an ENTITY claim's REAL persisted validations to DTOs (used by
    /// <c>GetMechanicAnalysisClaimsQueryHandler</c>, which queries <c>MechanicClaimEntity</c> directly).
    /// Reads the <c>validations</c> jsonb column; same legacy-null fallback as <see cref="FromDomain"/>.
    /// </summary>
    public static IReadOnlyList<MechanicClaimValidationDto> FromEntity(MechanicClaimEntity entity)
    {
        ArgumentNullException.ThrowIfNull(entity);
        return Map(entity.Validations);
    }

    private static IReadOnlyList<MechanicClaimValidationDto> Map(
        IReadOnlyList<MechanicClaimValidation>? validations)
    {
        if (validations is null || validations.Count == 0)
        {
            return DeriveLegacyAllPassFallback();
        }
        return validations
            .Select(v => new MechanicClaimValidationDto(v.Rule, v.Outcome, v.Message, v.Score))
            .ToList();
    }

    /// <summary>
    /// LEGACY fallback for pre-#2782 claims with no persisted validations. Returns all-pass across the
    /// 5 rules. NOT the primary path anymore — real outcomes come from <see cref="FromDomain"/>/<see cref="FromEntity"/>.
    /// </summary>
    public static IReadOnlyList<MechanicClaimValidationDto> DeriveLegacyAllPassFallback() =>
        Families.Select(f => new MechanicClaimValidationDto(f, "pass")).ToList();
}
```
> `entity.Validations` is `List<MechanicClaimValidation>?` (Task 7) — the same domain VO type — so `Map` takes both `claim.Validations` (`IReadOnlyList<MechanicClaimValidation>`) and `entity.Validations` (`List<...>?`) uniformly.

- [ ] **Step 4: Flip the 5 construction sites** (verified sources — do NOT use `FromDomain` at the query site):
- `GetMechanicAnalysisClaimsQueryHandler.cs:72` — `c` is a **`MechanicClaimEntity`** (query over `_dbContext.MechanicClaims`). Replace `Validations: MechanicClaimValidations.DerivePass()` with `Validations: MechanicClaimValidations.FromEntity(c)`.
- `ApproveMechanicClaimCommandHandler.cs:135` (`ToDto(claim, analysisId)`, `claim` is domain) — `Validations: MechanicClaimValidations.FromDomain(claim)`.
- `RejectMechanicClaimCommandHandler.cs:119` (`claim` is domain) — `Validations: MechanicClaimValidations.FromDomain(claim)`.
- `BulkApproveMechanicClaimsCommandHandler.cs:142` (`c` is domain from `analysis.Claims`) — `Validations: MechanicClaimValidations.FromDomain(c)`.
- `BulkRejectMechanicClaimsCommandHandler.cs:155` (`c` is domain from `analysis.Claims`) — `Validations: MechanicClaimValidations.FromDomain(c)`.
Grep to confirm no site is missed: `grep -rn "DerivePass" apps/api/src/Api/BoundedContexts/SharedGameCatalog apps/api/tests` must return **0** hits after the rename.

- [ ] **Step 4b: Update the existing `MechanicClaimValidationsTests.cs`** — rename the method off `DerivePass_ReturnsFourPassBadges_T1ToT4`, call the renamed method, and assert the NEW 5-family sequence:

```csharp
    [Fact]
    public void DeriveLegacyAllPassFallback_ReturnsFivePassBadges_T1ToT4WithT3Split()
    {
        var validations = MechanicClaimValidations.DeriveLegacyAllPassFallback();

        validations.Select(v => v.Rule).Should().Equal("T1", "T2", "T3a", "T3b", "T4");
        validations.Should().OnlyContain(v => v.Outcome == "pass" && v.Message == null);
    }
```

- [ ] **Step 5: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicClaimValidationsMappingTests|FullyQualifiedName~MechanicClaimValidationsTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS (new mapping tests + the updated legacy-fallback test) + build OK (query site uses `FromEntity`, 4 command sites use `FromDomain`).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationDto.cs "apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/MechanicExtractor/GetMechanicAnalysisClaimsQueryHandler.cs" "apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/"*Handler.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 flip DerivePass sites to persisted validations (FromEntity+FromDomain) + Score (D5 M3)"
```

> **BE-core slice boundary.** After Task 11, run the whole context suite before merging: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=SharedGameCatalog" --project ../../tests/Api.Tests/Api.Tests.csproj` — green, no growth over the known-flaky baseline. BE-core merges as ONE unit (atomic, dec. 7).

---

## Task 12: BE-card — card down-projection + SchemaVersion=2 (D6)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContent.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContentValidationsTests.cs` (create)

**Interfaces:**
- Consumes: `MechanicClaim.Validations` (Task 6).
- Produces: `MechanicCardContent.FromAnalysis` populates each claim's `Validations` array with `MechanicCardValidationSnapshot { Rule, Passed = (outcome=="pass"), Score }` (down-projection); `CurrentSchemaVersion = 2`.

> **Verified:** `MechanicCardContent.FromAnalysis` currently hard-codes `Validations = Array.Empty<MechanicCardValidationSnapshot>()`; `MechanicCardValidationSnapshot` already exists as `{ [rule] string Rule; [passed] bool Passed; [score] double? Score }`; `CurrentSchemaVersion = 1`. The card keeps the 5-rule shape (one snapshot per validation).

- [ ] **Step 1: Write the failing test** — an analysis whose claim carries a T2 fail + a T3b pass with score; assert the card snapshot down-projects `fail → passed:false`, `pass → passed:true`, carries the T3b score, and `SchemaVersion == 2`.

```csharp
[Fact]
public void FromAnalysis_DownProjectsClaimValidations_AndBumpsSchemaVersion()
{
    var analysis = BuildPublishedAnalysisWithValidatedClaim(); // claim.AttachValidations([T2 fail, T3b pass(0.9)])
    var ctx = BuildGameContext();

    var content = MechanicCardContent.FromAnalysis(analysis, ctx, DateTime.UtcNow);

    content.SchemaVersion.Should().Be(2);
    var claim = content.Claims.Single();
    claim.Validations.Single(v => v.Rule == "T2").Passed.Should().BeFalse();
    claim.Validations.Single(v => v.Rule == "T3b").Passed.Should().BeTrue();
    claim.Validations.Single(v => v.Rule == "T3b").Score.Should().Be(0.9);
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicCardContentValidationsTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — validations empty, SchemaVersion is 1.

- [ ] **Step 3: Implement.** In `MechanicCardContent.cs`, bump the const and replace the hard-coded empty:

```csharp
    public const int CurrentSchemaVersion = 2; // #2782: real validations projected; write-only until a card reader (#528) exists.
```
In `FromAnalysis`, replace `Validations = Array.Empty<MechanicCardValidationSnapshot>()` with:
```csharp
            Validations = c.Validations
                .Select(v => new MechanicCardValidationSnapshot
                {
                    Rule = v.Rule,
                    Passed = v.Outcome == MechanicClaimValidationOutcomes.Pass,
                    Score = v.Score
                })
                .ToList()
```
Add `using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;` if `MechanicClaimValidationOutcomes` is not already in scope (it lives in the same namespace, so likely no import needed).

- [ ] **Step 4: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicCardContentValidationsTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS + build OK.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContent.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 down-project claim validations into card + schema_version=2 (D6)"
```

---

## Task 13: BE-card — server-side BulkApprove exclude-fail guard (D8)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/BulkApproveMechanicClaimsCommandHandler.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/BulkApproveExcludeFailFlaggedTests.cs` (create)

**Interfaces:**
- Consumes: `MechanicClaim.Validations` (Task 6).
- Produces: `BulkApprove`'s implicit approve-all-pending SKIPS claims carrying any `fail` validation (an admin can't one-click rubber-stamp hallucinations). The skipped-fail count is **LOG-ONLY** — no DTO field is added (keeps the response contract + FE Zod schema unchanged); the fail-flagged claims simply stay Pending.

> **Verified:** the handler approves `analysis.Claims.Where(c => c.Status == Pending)` in a loop. Add a fail-flag filter to the pending set.

- [ ] **Step 1: Write the failing test** — 2 pending claims, one carrying a T2 fail. BulkApprove approves only the clean one; the fail-flagged one stays Pending.

```csharp
[Fact]
public async Task Handle_ExcludesFailFlaggedClaims_FromImplicitApproveAll()
{
    var analysis = BuildInReviewAnalysisWithTwoPendingClaims(); // claim[0] clean, claim[1] AttachValidations([T2 fail])
    SetupRepo(analysis, analysis.Id);

    await _handler.Handle(new BulkApproveMechanicClaimsCommand(analysis.Id, Guid.NewGuid()), CancellationToken.None);

    analysis.Claims.ElementAt(0).Status.Should().Be(MechanicClaimStatus.Approved);
    analysis.Claims.ElementAt(1).Status.Should().Be(MechanicClaimStatus.Pending); // fail-flagged, skipped
}
```
> Confirm the `BulkApproveMechanicClaimsCommand` constructor arity from the existing handler test file before writing.

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BulkApproveExcludeFailFlaggedTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal`
Expected: FAIL — both claims approved.

- [ ] **Step 3: Implement.** In `BulkApproveMechanicClaimsCommandHandler`, change the pending selection to exclude fail-flagged claims:

```csharp
        var pendingClaimIds = analysis.Claims
            .Where(c => c.Status == MechanicClaimStatus.Pending
                        && !c.Validations.Any(v => v.Outcome == MechanicClaimValidationOutcomes.Fail))
            .Select(c => c.Id)
            .ToList();

        var skippedFailFlaggedCount = analysis.Claims
            .Count(c => c.Status == MechanicClaimStatus.Pending
                        && c.Validations.Any(v => v.Outcome == MechanicClaimValidationOutcomes.Fail));
```
Add `using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;`. **LOG-ONLY** — do NOT add a DTO field: emit `_logger.LogInformation("BulkApprove skipped {Count} fail-flagged claim(s) for analysis {AnalysisId}", skippedFailFlaggedCount, analysis.Id)` (only when `skippedFailFlaggedCount > 0`) and leave those claims Pending. Confirm the handler has an `ILogger<BulkApproveMechanicClaimsCommandHandler>` in scope; if not, inject one. Do NOT change the aggregate gate — this is a handler-level guard.

- [ ] **Step 4: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~BulkApproveExcludeFailFlaggedTests" --project ../../tests/Api.Tests/Api.Tests.csproj -v minimal && dotnet build`
Expected: PASS + build OK.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/BulkApproveMechanicClaimsCommandHandler.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #2782 BulkApprove excludes fail-flagged claims (D8)"
```

---

## Task 14: BE-card — ADR #2786 (canonical validation shape)

**Files:**
- Create: `docs/for-claude/architecture/adr/adr-084-mechanic-validation-canonical-shape.md`

**Interfaces:** none (documentation).

> Latest ADR is `adr-083`; #2786 becomes `adr-084`. Confirm no `adr-084-*.md` exists (`ls docs/for-claude/architecture/adr/adr-084*`) before creating; if taken, use the next free number.

- [ ] **Step 1: Write the ADR** — record the locked decisions from D6:

```markdown
# ADR-084 — Mechanic Extractor validation canonical shape (#2786, folded into #2782)

**Status:** Accepted (2026-07-10)
**Context:** #2782 FU-1 introduces real per-claim guardrail validations. Two consumers exist: the
admin review DTO (superset) and the #527 card snapshot (lossy).

## Decision
1. **Canonical = review-side 5-rule superset.** `MechanicClaimValidation {Rule ∈ {T1,T2,T3a,T3b,T4}, Outcome ∈ {pass,fail,notRun}, Message?, Score?}`. Score is populated only for T3b (grounding cosine).
2. **Card = lossy down-projection.** `MechanicCardValidationSnapshot {rule, passed:bool, score:double?}` — `passed = (outcome=="pass")`; `notRun`/`fail` both collapse to `passed:false`. `mechanic_cards.content.schema_version` is bumped to **2** and is **write-only** until a card reader (#528) exists; it does NOT gate anything today.
3. **JSON casing frozen.** `validations` jsonb serializes with the domain record's default casing; card snapshot keys are `rule`/`passed`/`score` (explicit `[JsonPropertyName]`).
4. **No GIN index** on `mechanic_claims.validations` — every consumer filters client-side per single analysis (small N); a GIN index is unwarranted.
5. **Correlation approximation for Victory.** Victory violations attribute to the section's primary claim; alternative claims inherit the primary's outcome (they are never independently walked by the guardrails). Documented, not a bug.

## Consequences
- The review UI can surface real fail/notRun + T3b score; `reject-all-failing-T2` becomes meaningful.
- The card cannot distinguish `fail` from `notRun` — acceptable; the card is a public summary, not an audit trail.
```

- [ ] **Step 2: Commit**

```bash
git add docs/for-claude/architecture/adr/adr-084-mechanic-validation-canonical-shape.md
git commit -m "docs(adr): #2782 ADR-084 mechanic validation canonical shape (folds #2786) (D6)"
```

> **BE-card slice boundary.** After Task 14, run `cd apps/api/src/Api && dotnet test --filter "BoundedContext=SharedGameCatalog" --project ../../tests/Api.Tests/Api.Tests.csproj` — green. BE-card stacks on merged BE-core.

---

## Task 15: FE — Zod `score` contract test + schema field (D7, M7)

**Files:**
- Modify: `apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts`
- Test: `apps/web/src/lib/api/schemas/__tests__/mechanic-analyses.schemas.test.ts` (**exists** — append a new `describe` block; the file already has a `describe('mechanic-analyses schemas #526', …)` block)

**Interfaces:**
- Produces: `MechanicClaimValidationDtoSchema` gains `score: z.number().nullable().optional()`.

> **Verified:** `MechanicClaimValidationDtoSchema = z.object({ rule: z.string(), outcome: z.enum(['pass','fail','notRun']), message: z.string().nullable().optional() })` at line 237. Zod strips unknown keys silently → without this positive test, `score` can be dropped on the wire and never noticed (M7). The test file already exists with one `describe` block — **append** the new block (do not recreate the file); the existing `import { z }`/vitest imports may be reused or re-imported per-block.

- [ ] **Step 1: Write the failing contract test** — append this `describe` block after the existing `mechanic-analyses schemas #526` block:

```ts
import { describe, expect, it } from 'vitest';
import { MechanicClaimValidationDtoSchema } from '../mechanic-analyses.schemas';

describe('MechanicClaimValidationDtoSchema #2782', () => {
  it('preserves the T3b score field (guards against Zod silent-drop)', () => {
    const parsed = MechanicClaimValidationDtoSchema.parse({
      rule: 'T3b', outcome: 'pass', message: null, score: 0.87,
    });
    expect(parsed.score).toBe(0.87);
  });

  it('allows a null/absent score for non-T3b rules', () => {
    const parsed = MechanicClaimValidationDtoSchema.parse({ rule: 'T1', outcome: 'fail', message: 'too long' });
    expect(parsed.score ?? null).toBeNull();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/lib/api/schemas/__tests__/mechanic-analyses.schemas.test.ts`
Expected: FAIL — `score` is stripped (`parsed.score` is `undefined`, not `0.87`).

- [ ] **Step 3: Add `score` to the schema.** In `mechanic-analyses.schemas.ts` at `MechanicClaimValidationDtoSchema`:

```ts
export const MechanicClaimValidationDtoSchema = z.object({
  rule: z.string(),
  outcome: z.enum(['pass', 'fail', 'notRun']),
  message: z.string().nullable().optional(),
  score: z.number().nullable().optional(),
});
```

- [ ] **Step 4: Run to verify it passes + typecheck**

Run: `cd apps/web && pnpm test src/lib/api/schemas/__tests__/mechanic-analyses.schemas.test.ts && pnpm typecheck`
Expected: PASS + typecheck OK.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts apps/web/src/lib/api/schemas/__tests__/mechanic-analyses.schemas.test.ts
git commit -m "feat(mechanic-extractor): #2782 add validation score to Zod schema + contract test (D7 M7)"
```

---

## Task 16: FE — 5-badge render + T3b score display (D7, B2)

**Files:**
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx` (the exported `ValidationBadges`)
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.badges5.test.tsx` (create)

**Interfaces:**
- Consumes: `MechanicClaimDto.validations` (now with 5 rules + score).
- Produces: `ValidationBadges` renders one badge per rule for the 5-rule taxonomy, shows the T3b score in the badge title/label when present.

> **Verified:** `ValidationBadges` already exists (renders `VALIDATION_BADGE_CLASS[v.outcome]` per validation, `data-testid` per rule). It is data-driven (maps `validations`), so it already renders however many rules the DTO carries — the change is (a) a test locking the 5-rule render + fail/notRun styling, and (b) surfacing `score` in the badge for T3b.

- [ ] **Step 1: Write the failing test**

```tsx
/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ValidationBadges } from '../ClaimsSection';

describe('ValidationBadges 5-rule + score', () => {
  it('renders all 5 rules and shows the T3b score', () => {
    render(
      <ValidationBadges
        validations={[
          { rule: 'T1', outcome: 'pass', message: null, score: null },
          { rule: 'T2', outcome: 'fail', message: 'long verbatim', score: null },
          { rule: 'T3a', outcome: 'pass', message: null, score: null },
          { rule: 'T3b', outcome: 'pass', message: null, score: 0.82 },
          { rule: 'T4', outcome: 'notRun', message: null, score: null },
        ]}
      />
    );
    for (const rule of ['T1', 'T2', 'T3a', 'T3b', 'T4']) {
      expect(screen.getByTestId(`claim-validation-badge-${rule}`)).toBeInTheDocument();
    }
    expect(screen.getByTestId('claim-validation-badge-T3b')).toHaveAttribute(
      'title', expect.stringMatching(/0\.82/)
    );
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.badges5.test.tsx`
Expected: FAIL — the T3b `title` does not include the score (5-rule render likely already works, but the score assertion fails).

- [ ] **Step 3: Surface the score in `ValidationBadges`.** In the badge `title`/`aria-label`, append the score when present:

```tsx
          title={
            v.score != null
              ? `${v.rule} ${v.outcome} (score ${v.score.toFixed(2)})`
              : (v.message ?? `${v.rule} ${v.outcome}`)
          }
          aria-label={`${v.rule} ${v.outcome}${v.score != null ? ` score ${v.score.toFixed(2)}` : ''}${v.message ? `: ${v.message}` : ''}`}
```
(The existing per-rule `data-testid={`claim-validation-badge-${v.rule}`}` + `VALIDATION_BADGE_CLASS[v.outcome]` styling already handle the 5-rule render; keep them.)

- [ ] **Step 4: Run to verify it passes + existing claims tests + typecheck**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims && pnpm typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.badges5.test.tsx
git commit -m "feat(mechanic-extractor): #2782 5-rule badges + T3b score in ClaimsSection (D7)"
```

---

## Task 17: FE — `reject-all-failing-T2` reads validations (D7, M6)

**Files:**
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx`
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.rejectT2.test.tsx` (create)

**Interfaces:**
- Consumes: `MechanicClaimDto.validations`.
- Produces: the bulk predicate + `BulkActionKind` renamed/repurposed from `reject-long-quote` (T1 quote-length heuristic) to `reject-all-failing-T2` (reads `validations` for a `T2` `fail`). Reject reason text updated.

> **Verified:** `ClaimsSection.tsx` currently has `wordCount` (lines 54–56) + `claimsWithLongQuote` (lines 59–61, filters on `wordCount(cit.quote) > LONG_QUOTE_WORDS`), `LONG_QUOTE_WORDS = 20` (line 52), `BulkActionKind = 'approve-pending' | 'reject-long-quote'` (line 63), `longQuoteClaims` memo (line 266), `bulkActionTargets`/`bulkActionTitle` (lines 268–272), and `handleBulkActionConfirm` (lines 274–283) with reason `'Citazione supera 20 parole (ADR-051 T1) — rifiuto bulk.'`. The bulk dropdown is a **shadcn/Radix `Select`** (`SelectTrigger data-testid="bulk-action-select"`, `SelectItem value="reject-long-quote"` with visible text `Reject all with quote >20 words (N)` — lines 346–363). Selecting an option sets a `bulk-action-count` element + a `bulk-action-confirm` button. **The existing test `ClaimsSection.bulk.test.tsx` drives it via `fireEvent.click(select)` → `await screen.findByRole('listbox')` → `fireEvent.click(within(listbox).getByText(/…/))`** — NOT `fireEvent.change`. This task replaces the T1 heuristic with a real T2-fail read (M6) AND updates that existing test.

- [ ] **Step 1: Write the failing test** (new file `ClaimsSection.rejectT2.test.tsx`) — 2 claims: one has a `T2 fail` validation, one is all pass. Drive the Radix `Select` the same way `ClaimsSection.bulk.test.tsx:64–67` does, and assert the bulk `reject-all-failing-T2` action targets only the T2-fail claim.

```tsx
/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent, within, waitFor } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetClaims = vi.hoisted(() => vi.fn());
const mockBulkReject = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({
    getMechanicAnalysisClaims: mockGetClaims,
    bulkApproveMechanicClaims: vi.fn(),
    bulkRejectMechanicClaims: mockBulkReject,
  }),
}));
const MockHttpClient = vi.hoisted(() => class MockHttpClient {});
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));

import { ClaimsSection } from '../ClaimsSection';

const base = {
  analysisId: 'a', section: 1, text: 't', displayOrder: 0, status: 0,
  reviewedBy: null, reviewedAt: null, rejectionNote: null, reviewNote: null,
  citations: [{ id: 'c', pdfPage: 1, quote: 'q', displayOrder: 0 }],
};
const claims = [
  { ...base, id: 'd1', validations: [{ rule: 'T2', outcome: 'fail', message: 'verbatim', score: null }] },
  { ...base, id: 'd2', validations: [{ rule: 'T2', outcome: 'pass', message: null, score: null }] },
];

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection reject-all-failing-T2', () => {
  it('rejects only claims carrying a T2 fail validation', async () => {
    mockGetClaims.mockResolvedValue(claims);
    mockBulkReject.mockResolvedValue({ rejectedCount: 1, skippedAlreadyRejectedCount: 0, claims });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });

    // Radix Select: click the trigger, wait for the listbox, click the option by its visible text.
    const select = await screen.findByTestId('bulk-action-select');
    fireEvent.click(select);
    const listbox = await screen.findByRole('listbox');
    fireEvent.click(within(listbox).getByText(/Reject all failing T2/));

    fireEvent.click(screen.getByTestId('bulk-action-confirm'));

    await waitFor(() =>
      expect(mockBulkReject).toHaveBeenCalledWith('a', expect.objectContaining({ claimIds: ['d1'] }))
    );
  });
});
```
> Do NOT use `fireEvent.change` on a Radix `Select` — it is not a native `<select>`. Mirror `ClaimsSection.bulk.test.tsx:64–67`.

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.rejectT2.test.tsx`
Expected: FAIL — the `reject-all-failing-T2` option (label `Reject all failing T2`) does not exist.

- [ ] **Step 3: Replace the T1 heuristic with a T2-fail read.** In `ClaimsSection.tsx`:
- Replace the `wordCount` (lines 54–56) + `claimsWithLongQuote` (lines 59–61) helpers with:
  ```tsx
  /** Claims carrying a real T2 (long-verbatim) guardrail FAIL (#2782). */
  function claimsFailingT2(claims: MechanicClaimDto[]): MechanicClaimDto[] {
    return claims.filter(c => c.validations.some(v => v.rule === 'T2' && v.outcome === 'fail'));
  }
  ```
- Remove `LONG_QUOTE_WORDS` (line 52) once no other reference remains (grep `LONG_QUOTE_WORDS` + `wordCount` first).
- Change `BulkActionKind` (line 63) to `'approve-pending' | 'reject-all-failing-T2'`.
- Replace `longQuoteClaims` (line 266) with `const failingT2Claims = useMemo(() => claimsFailingT2(claims), [claims]);`.
- Update `bulkActionTargets` (line 268 → `bulkAction === 'approve-pending' ? pendingClaims : failingT2Claims`), `bulkActionTitle` (lines 269–272 → `'Reject claims that failed the T2 guardrail?'`), the `SelectItem` (lines 358–360 → `value="reject-all-failing-T2"`, visible text `Reject all failing T2 ({failingT2Claims.length})`, `disabled={failingT2Claims.length === 0}`), and `handleBulkActionConfirm` (line 277 → `else if (bulkAction === 'reject-all-failing-T2')` with reason `'Claim ha fallito il guardrail T2 (long-verbatim) — rifiuto bulk (#2782).'`).

- [ ] **Step 3b: Update the pre-existing `ClaimsSection.bulk.test.tsx`.** Its 2 tests click `within(listbox).getByText(/Reject all with quote >20 words/)` (lines ~66 and ~92) — that label no longer exists after Step 3, so they must be rewritten to the new option/label. Change BOTH matchers to `/Reject all failing T2/`, and update the two `claims` fixtures (currently `validations: []` with a 25-word `longQuote` citation) so the offending claim carries a real T2 fail:
  ```tsx
  // Replace the `longQuote` fixture setup: give claim d1 a T2 fail, d2 a T2 pass.
  const claims = [
    { /* d1 base */ id: 'd1', /* … */ validations: [{ rule: 'T2', outcome: 'fail', message: 'verbatim', score: null }],
      citations: [{ id: 'c1', pdfPage: 1, quote: 'short', displayOrder: 0 }] },
    { /* d2 base */ id: 'd2', /* … */ validations: [{ rule: 'T2', outcome: 'pass', message: null, score: null }],
      citations: [{ id: 'c2', pdfPage: 1, quote: 'short quote', displayOrder: 0 }] },
  ];
  ```
  The first test still asserts `bulk-action-count` shows `1` and `mockBulkReject` is called with `claimIds: ['d1']`; the amber-warning test (skipped-already-rejected) keeps its `mockBulkReject` shape but selects the new label. The `approve-pending` test is unaffected (keep as-is). Do NOT leave any `/Reject all with quote >20 words/` matcher — grep the file to confirm 0 remain. Stage this file in the SAME commit as Step 3.

- [ ] **Step 4: Run to verify it passes + existing claims tests + typecheck + lint**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims && pnpm typecheck && pnpm lint`
Expected: PASS (new rejectT2 test + the rewritten bulk test + all other claims tests).

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.rejectT2.test.tsx apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.bulk.test.tsx
git commit -m "feat(mechanic-extractor): #2782 reject-all-failing-T2 reads validations, not quote length (D7 M6)"
```

---

## Task 18: FE — approve-time fail warning (D7, D8)

**Files:**
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ApproveClaimDialog.tsx`
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx` (pass the target claim's validations to the dialog)
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ApproveClaimDialog.warning.test.tsx` (create)

**Interfaces:**
- Consumes: `MechanicClaimDto.validations` for the claim being approved.
- Produces: `ApproveClaimDialog` renders a warning banner (`data-testid="approve-fail-warning"`) when the claim carries any `fail`. Approve remains allowed (human override, recorded).

> **Verified:** `ApproveClaimDialog` exists and is opened from `ClaimsSection` via `approveTarget` state (line 137) + rendered at line ~402 with `claimPreview`. Extend its props with the target's validations and render a conditional warning.

- [ ] **Step 1: Write the failing test**

```tsx
/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';
import { ApproveClaimDialog } from '../ApproveClaimDialog';

describe('ApproveClaimDialog fail warning', () => {
  it('shows a warning when the claim carries a fail validation', () => {
    render(
      <ApproveClaimDialog
        open
        onOpenChange={() => {}}
        onConfirm={() => {}}
        isPending={false}
        claimPreview="some claim"
        validations={[{ rule: 'T2', outcome: 'fail', message: 'long verbatim', score: null }]}
      />
    );
    expect(screen.getByTestId('approve-fail-warning')).toBeInTheDocument();
  });

  it('renders no warning when all validations pass', () => {
    render(
      <ApproveClaimDialog
        open onOpenChange={() => {}} onConfirm={() => {}} isPending={false}
        claimPreview="ok" validations={[{ rule: 'T1', outcome: 'pass', message: null, score: null }]}
      />
    );
    expect(screen.queryByTestId('approve-fail-warning')).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims/__tests__/ApproveClaimDialog.warning.test.tsx`
Expected: FAIL — `validations` prop / warning banner missing.

- [ ] **Step 3: Implement.** In `ApproveClaimDialog.tsx`:
- Add the import: `import type { MechanicClaimValidationDto } from '@/lib/api/schemas/mechanic-analyses.schemas';`.
- Extend `ApproveClaimDialogProps` with `validations?: MechanicClaimValidationDto[];` and add `validations` to the destructured props.
- Compute `const hasFail = (validations ?? []).some(v => v.outcome === 'fail');` and render the banner inside `<AlertDialogContent>` (e.g. right after `</AlertDialogHeader>`, before the `<div className="space-y-2">` note block):
  ```tsx
  {hasFail && (
    <div
      className="rounded-md border border-amber-300 bg-amber-50 p-2 text-xs text-amber-900 dark:border-amber-800 dark:bg-amber-950/20 dark:text-amber-300"
      role="alert"
      data-testid="approve-fail-warning"
    >
      Questo claim ha fallito uno o più guardrail. Approvando confermi un override manuale.
    </div>
  )}
  ```
  The file's existing `/* eslint-disable local/no-hardcoded-color-utility … */` admin-scope header (line 1) already covers the amber classes.

- Then in `ClaimsSection.tsx`, at the `<ApproveClaimDialog .../>` call site (lines ~401–412), add the `validations` prop:
  ```tsx
  <ApproveClaimDialog
    open={!!approveTarget}
    onOpenChange={open => {
      if (!open) setApproveTarget(null);
    }}
    onConfirm={note => {
      if (!approveTarget) return;
      approveMutation.mutate({ claimId: approveTarget.id, note });
    }}
    isPending={approveMutation.isPending}
    claimPreview={approveTarget ? truncate(approveTarget.text, 120) : undefined}
    validations={approveTarget?.validations}
  />
  ```
  (`approveTarget` is `MechanicClaimDto | null` at line 137, so `approveTarget?.validations` is `MechanicClaimValidationDto[] | undefined` — matches the new optional prop. No new import is needed in `ClaimsSection.tsx` since it already imports `MechanicClaimDto`.)

- [ ] **Step 4: Run to verify it passes + existing claims tests + typecheck**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims && pnpm typecheck && pnpm lint`
Expected: PASS + lint clean.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/claims/ApproveClaimDialog.tsx apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ApproveClaimDialog.warning.test.tsx
git commit -m "feat(mechanic-extractor): #2782 approve-time fail warning in ApproveClaimDialog (D7 D8)"
```

---

## Task 19: Full-suite verification + issue close-out

- [ ] **Step 1: Backend suite (all three slices merged locally)**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=SharedGameCatalog" --project ../../tests/Api.Tests/Api.Tests.csproj`
Expected: green; no growth over the known-flaky baseline (currently empty). Docker must be running for the Testcontainers tests (Tasks 8, 9).

- [ ] **Step 2: Frontend quality gates**

Run: `cd apps/web && pnpm typecheck && pnpm lint && pnpm test src/components/admin/mechanic-extractor src/lib/api/schemas`
Expected: green.

- [ ] **Step 3: Confirm the DerivePass rename is complete**

Run: `cd apps/api/src/Api && grep -rn "DerivePass\b" BoundedContexts ; grep -rn "DerivePass\b" ../../tests/Api.Tests ; echo "hits above (expect 0)"`
Expected: 0 hits (query site uses `FromEntity`, the 4 command sites use `FromDomain`, the test uses `DeriveLegacyAllPassFallback`; only `DeriveLegacyAllPassFallback` remains).

- [ ] **Step 4: File the deferred D8 "N/6 sections" follow-up issue** — the "N/6 sections produced claims" visibility signal (so a `well_formed`-dropped section is not invisible) is explicitly **deferred** by spec D8. File a tracked GitHub follow-up so it is not silently lost:

```bash
gh issue create --repo meepleAi-app/meepleai-monorepo \
  --title "ME-FU-1 follow-up: surface \"N/6 sections produced claims\" (well_formed section-absent visibility)" \
  --body "Deferred from #2782 (spec D8). When a section fails the well_formed check across all retries it is silently absent from the review queue. Add a lightweight \"N/M sections produced claims\" signal to the mechanic-analysis status query so a dropped section is visible to the reviewer. Non-blocking; no schema change expected. See ADR-084 + #2782 design D8." \
  --label enhancement
```
Capture the new issue number for Step 5.

- [ ] **Step 5: Update #2782 + push + PR** — comment the delivered D-sections + the 3-slice delivery on #2782; note ADR-084 folds #2786 and reference the D8 "N/6 sections" follow-up issue filed in Step 4. Push the branch and open the PR to `main-dev` (BE-core first if stacked). Follow the #526 close-out workflow (code-review before merge).

---

## Self-Review — spec section → task mapping

| Spec section | Task(s) | Notes |
|---|---|---|
| **D1** validator accumulate-during-fail-fast + Path + score + notRun | Task 2 (guardrail `EvaluateDetailedAsync` + T3b score), Task 3 (`RuleOutcomes` on `MechanicValidationResult`) | single fail-fast pass; `notRun` = guardrails after the stop; `IsValid`/`Violations` retry trigger untouched |
| **D2** T3b grounding score | Task 2 | min cosine surfaced via `EvaluateDetailedAsync`; other guardrails default null |
| **D3** pipeline run-all-retain + well_formed + grounding-outage carve-outs + validator→pipeline propagation | Task 5 (`SectionOutcomes` init-property, run-all, 3-way branch keeping the LAST `RuleOutcomes`), Task 6 (executor correlation consumes it) | NEW pipeline tests (none existed); grounding-unavailable stays hard-abort; well_formed via hoisted last result |
| **D4** correlation violation Path → claim (+ source anchor) + Victory carve-out | Task 4 (parser anchor), Task 6 (`CorrelateValidations` anti-false-positive test) | raw source index, not displayOrder; anchor not persisted; Victory attributes to primary |
| **D5** jsonb column/migration/4-mapper-points/5-site DerivePass flip (`FromEntity`+`FromDomain`) | Task 1 (VO), Task 7 (column+converter, comparer optional/non-load-bearing), Task 8 (write+read mappers + insert→reload→mutate→resave), Task 11 (5-site flip + rename + existing-test update) | comparer inert on Update's rebuild-detached path; round-trip red-gates the mappers |
| **D6** card down-projection + schema_version + ADR #2786 | Task 12 (projection + SchemaVersion=2), Task 14 (ADR-084) | |
| **D7** FE 5 badges + reject-all-failing-T2 + approve-warning + score + Zod contract test | Task 15 (Zod score, append block), Task 16 (5 badges + score), Task 17 (reject-T2 Radix-driven + existing bulk test rewrite), Task 18 (approve-warning + call-site JSX) | |
| **D8** safety gate: soft FE warning + server-side BulkApprove exclude-fail; N/6 signal deferred | Task 13 (server guard, LOG-ONLY), Task 18 (FE warning), Task 19 Step 4 (N/6 follow-up issue filed) | domain gate unchanged (human override recorded) |
| **D9** section-run Status=3 + CHECK migration | Task 9 (CHECK 0..3 migration), Task 10 (Status=3 wired + honest ErrorMessage) | `error_when_failed` CHECK unchanged |
| **Testing: Victory-vs-other correlation** | Task 6 | item #2 fails → only that claim flagged |
| **Testing: insert→reload→mutate→resave round-trip** | Task 8 | insert → reload → ApproveClaim → Update → reload (red-gates WRITE+READ mappers) |
| **Testing: pipeline run-all / well_formed / grounding-unavailable / cost-cap / LLM-fail** | Task 5 (4 scenarios) + Task 10 | |
| **Testing: card FromAnalysis down-projection** | Task 12 | |
| **Testing: BulkApprove excludes fail-flagged** | Task 13 | |
| **Testing: 5-badge render / reject-all-failing-T2 / Zod score** | Tasks 16, 17, 15 | |
| **Locked review dec.: correlation Path→claim (4) / soft+outage-fail-closed gate (5) / 5-badge taxonomy (6) / BE-core atomic (7) / M4 4-mapper points** | Tasks 6, 5+13+18, 11+16, delivery, 8 | all 5 locked decisions mapped |
| **Delivery: BE-core atomic / BE-card / FE** | Tasks 1–11 / 12–14 / 15–18 (+ 19 close-out) | slice boundaries called out after Tasks 11, 14 |

**Placeholder scan:** no TBD/TODO/"handle edge cases" — every code step shows real code + exact commands + expected output.

**Type consistency:** `MechanicClaimValidation` (VO, Task 1) ↔ `MechanicClaimValidationDto`+`Score` (Task 11) ↔ `MechanicCardValidationSnapshot` (Task 12) ↔ Zod `score` (Task 15); `MechanicRuleOutcome` (Task 3) ↔ `MechanicValidationResult.RuleOutcomes` (Task 3) ↔ `SectionOutcomes` init-property (Task 5) ↔ `CorrelateValidations` (Task 6); `SourceAnchor` (Task 4) ↔ `MatchesAnchor` (Task 6); `EvaluateDetailedAsync`/`MechanicGuardrailResult` (Task 2) ↔ `ValidateAsync`'s per-guardrail loop (Task 3). No `ValidateAllAsync` anywhere. `FromEntity(MechanicClaimEntity)` (query site) + `FromDomain(MechanicClaim)` (4 command sites) both feed `MechanicClaimValidationDto`. Rule strings `{T1,T2,T3a,T3b,T4}` = guardrail `RuleFamily` values throughout.

---

## Execution Handoff

**Plan complete and saved to `docs/superpowers/plans/2026-07-10-issue-2782-me-fu1-real-validations.md`. Two execution options:**

**1. Subagent-Driven (recommended)** — dispatch a fresh subagent per task, review between tasks, fast iteration. BE-core (Tasks 1–11) merges atomically before BE-card/FE stack on top.

**2. Inline Execution** — execute tasks in this session with checkpoints for review.

**Which approach?**
