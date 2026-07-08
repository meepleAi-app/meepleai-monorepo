# #526 ME-M1.4 Admin Review UI (core iteration) — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Complete the core of the Mechanic Extractor admin review UI (#526): T1–T4 guardrail badges (derived), a PDF citation quote-highlighter, bulk-reject-by-predicate, the ADR-051 footer swap, an approve-with-note MVP, one observability counter, a11y, and tests — plus a `validations[]` DTO contract that unblocks #527.

**Architecture:** The backend already ships the bulk-reject endpoint + analysis lifecycle. This plan adds a derived `validations[]` field to the claim read-model, a nullable `review_note` column, one metric, and wires the frontend `ClaimsSection` (badges, citation viewer, bulk dropdown, approve-note) plus two new components (`PdfQuoteHighlighter`, `MechanicAnalysisFooterAttribution`). Real per-claim validation persistence (AC-1) and analysis-finalize reconciliation with #527 (AC-4) are OUT — deferred to follow-ups FU-1 / FU-2.

**Tech Stack:** .NET 9 (Minimal APIs + MediatR + EF Core + FluentValidation), Next.js 16 (React 19, Zod, React Query, shadcn primitives), react-pdf@10.4.1 + pdfjs-dist@5.7.284, Vitest + Playwright + xUnit/Moq.

**Design spec:** `docs/superpowers/specs/2026-07-08-issue-526-me-m14-admin-review-core-design.md`

## Global Constraints

- **Branch:** `feature/issue-526-me-m14-admin-review-ui` (parent `main-dev`). Push with `git push -u origin feature/issue-526-me-m14-admin-review-ui`.
- **CQRS:** endpoints call only `IMediator.Send()`. Exceptions: `ConflictException`(409), `NotFoundException`(404) — never `InvalidOperationException`(500).
- **EF:** explicit `HasColumnName("snake_case")` — no auto snake_case. LiveSession-style handlers `AddAsync`/`Update` MUST be followed by `SaveChangesAsync` (already the case in existing handlers).
- **Admin FE conventions:** shadcn primitives from `@/components/ui/{primitives,data-display,overlays}/…` (NOT MeepleCard). Keep the file-level `/* eslint-disable local/no-hardcoded-color-utility … DS-13d admin scope */` header on admin files with amber/green/rose class maps — do NOT introduce `--admin-*` tokens. Pervasive `data-testid`. React Query keys `['mechanic-analysis', id, 'claims']`.
- **Bulk-reject contract (already shipped BE):** `POST /api/v1/admin/mechanic-analyses/{id}/claims/bulk-reject`, body `{ claimIds: Guid[], reason: string }` (reason 1–500 chars), response `{ rejectedCount, skippedAlreadyRejectedCount, claims: MechanicClaimDto[] }`. 404 stale id / 409 not-InReview.
- **ADR-051 footer string (canonical):** `"Analisi elaborata dall'AI sul manuale del gioco. Ogni affermazione è riformulata in parole originali e cita la pagina del regolamento. Copyright © degli editori per il testo originale del manuale."` The forbidden string to delete lives only at `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx:277` as entity-encoded `L&apos;AI non ha mai letto il testo del PDF originale.`
- **Guardrail → badge families:** T1=QuoteCap, T2=RejectionSampling(long-verbatim), T3=Grounding+CitationPresence, T4=PageSubstring.
- **Test commands:** BE `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~<Name>"` (kill testhost first if locked). FE `cd apps/web && pnpm test <path>` / `pnpm typecheck` / `pnpm lint`. E2E `pnpm test:e2e <spec>`.

---

## File map

**Create (backend)**
- `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationDto.cs` — validation badge DTO + `MechanicClaimValidations.DerivePass()` helper.
- `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.MechanicReview.cs` — `MechanicReviewBulkActions` counter.
- Migration (generated) `…/Migrations/<ts>_AddMechanicClaimReviewNote.cs`.
- `apps/api/tests/…/MechanicExtractor/MechanicReviewMetricsTests.cs`.

**Modify (backend)**
- `…/Application/DTOs/MechanicClaimDto.cs` — add `Validations` positional param.
- `…/Application/Queries/MechanicExtractor/GetMechanicAnalysisClaimsQueryHandler.cs` + every other `new MechanicClaimDto(` site — emit `Validations`.
- `…/Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs` + `…/Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs` — `ReviewNote` / `review_note`.
- `…/Domain/Entities/MechanicClaim.cs` (`Approve` note) + `…/Domain/Aggregates/MechanicAnalysis.cs` (`ApproveClaim` note) + `…/Application/Commands/MechanicExtractor/ApproveMechanicClaimCommand.cs` + `…Handler.cs` + `…Validator.cs`.
- `…/Application/Commands/MechanicExtractor/BulkRejectMechanicClaimsCommandHandler.cs` + `BulkApproveMechanicClaimsCommandHandler.cs` — metric increment.

**Create (frontend)**
- `apps/web/src/components/pdf/PdfQuoteHighlighter.tsx` + `__tests__/PdfQuoteHighlighter.test.tsx`.
- `apps/web/src/components/pdf/pdf-quote-highlight.ts` — pure normalize/match helper + `__tests__`.
- `apps/web/src/components/admin/mechanic-extractor/MechanicAnalysisFooterAttribution.tsx` + `__tests__`.
- `apps/web/src/components/admin/mechanic-extractor/claims/BulkActionDialog.tsx` + `ApproveClaimDialog.tsx`.

**Modify (frontend)**
- `apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts` — `MechanicClaimValidationDtoSchema` + `validations` field + `bulkRejectClaims` route + `BulkRejectMechanicClaimsResponseDtoSchema` + `BulkRejectMechanicClaimsRequest`.
- `apps/web/src/lib/api/clients/admin/adminContentClient.ts` — `bulkRejectMechanicClaims` + optional `note` on `approveMechanicClaim`.
- `apps/web/src/components/pdf/PdfInlineViewer.tsx` — `renderTextLayer?` + `highlightQuote?` + `onQuoteMatch?` props.
- `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx` — badges, citation-click, bulk dropdown, approve-note, `pdfDocumentId` prop.
- `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/analyses/page.tsx` — pass `pdfDocumentId` to `ClaimsSection`.
- `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx` — footer swap.
- `apps/web/e2e/admin-mechanic-extractor-validation/load-existing-analysis.spec.ts` — bulk-reject + citation-open flows.

---

## Task 1: Backend — derived `validations[]` on `MechanicClaimDto`

**Files:**
- Create: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationDto.cs`
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimDto.cs`
- Modify: every `new MechanicClaimDto(` construction site (grep below)
- Test: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationsTests.cs` (create)

**Interfaces:**
- Produces: `record MechanicClaimValidationDto(string Rule, string Outcome, string? Message = null)`; `static IReadOnlyList<MechanicClaimValidationDto> MechanicClaimValidations.DerivePass()`; `MechanicClaimDto.Validations : IReadOnlyList<MechanicClaimValidationDto>` (last positional param).

- [ ] **Step 1: Write the failing unit test** (pure — no DB) — `MechanicClaimValidationsTests.cs`. The handler wiring is verified by the build (every construction site must compile) + the E2E/existing tests; here we lock the derivation contract:

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.DTOs;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.DTOs;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicClaimValidationsTests
{
    [Fact]
    public void DerivePass_ReturnsFourPassBadges_T1ToT4()
    {
        var validations = MechanicClaimValidations.DerivePass();

        validations.Select(v => v.Rule).Should().Equal("T1", "T2", "T3", "T4");
        validations.Should().OnlyContain(v => v.Outcome == "pass" && v.Message == null);
    }
}
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicClaimValidationsTests" -v minimal`
Expected: FAIL — `MechanicClaimValidations` / `MechanicClaimValidationDto` do not exist.

- [ ] **Step 3: Create the DTO + derivation helper**

`MechanicClaimValidationDto.cs`:
```csharp
namespace Api.BoundedContexts.SharedGameCatalog.Application.DTOs;

/// <summary>
/// Per-claim guardrail badge outcome (#526 AC-1). Rule ∈ {T1,T2,T3,T4}; Outcome ∈ {pass,fail,notRun}.
/// </summary>
/// <remarks>
/// CORE ITERATION: values are DERIVED, not persisted. Every claim that reaches the review queue
/// passed its section's guardrails by the pipeline pass-invariant (rejection sampling retries a
/// section until its output satisfies T1–T4, else aborts to PartiallyExtracted/Rejected), so all
/// persisted claims are surfaced as <c>pass</c>. FU-1 (#526 follow-up) replaces this with real
/// per-claim outcomes + scores captured at pipeline time; the <c>fail</c>/<c>notRun</c> states and
/// <see cref="Message"/> light up then. #527 snapshots this array into <c>mechanic_cards.content</c>.
/// </remarks>
public sealed record MechanicClaimValidationDto(string Rule, string Outcome, string? Message = null);

/// <summary>Derivation of the AC-1 badge families for the core iteration.</summary>
public static class MechanicClaimValidations
{
    /// <summary>Badge families, ordered T1→T4 (T3 = grounding + citation-present).</summary>
    public static readonly IReadOnlyList<string> Families = new[] { "T1", "T2", "T3", "T4" };

    /// <summary>Derived all-pass outcomes (see <see cref="MechanicClaimValidationDto"/> remarks).</summary>
    public static IReadOnlyList<MechanicClaimValidationDto> DerivePass() =>
        Families.Select(f => new MechanicClaimValidationDto(f, "pass")).ToList();
}
```

- [ ] **Step 4: Add `Validations` to `MechanicClaimDto`** — append as the last positional param (after `Citations`) in `MechanicClaimDto.cs`:

```csharp
public sealed record MechanicClaimDto(
    Guid Id,
    Guid AnalysisId,
    MechanicSection Section,
    string Text,
    int DisplayOrder,
    MechanicClaimStatus Status,
    Guid? ReviewedBy,
    DateTime? ReviewedAt,
    string? RejectionNote,
    IReadOnlyList<MechanicCitationDto> Citations,
    IReadOnlyList<MechanicClaimValidationDto> Validations);
```

- [ ] **Step 5: Update every construction site.** Find them:

Run: `cd apps/api/src/Api && grep -rn "new MechanicClaimDto(" .`
For each site (GetMechanicAnalysisClaimsQueryHandler, ApproveMechanicClaimCommandHandler.ToDto, RejectMechanicClaimCommandHandler, BulkApproveMechanicClaimsCommandHandler, BulkRejectMechanicClaimsCommandHandler), add the final argument:
```csharp
                    .ToList(),
            Validations: MechanicClaimValidations.DerivePass()));
```
(i.e. after the `Citations: … .ToList()` argument, add `Validations: MechanicClaimValidations.DerivePass()`.)

- [ ] **Step 6: Run test to verify it passes**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicClaimValidationsTests" -v minimal`
Expected: PASS. Then `dotnet build` to confirm all `new MechanicClaimDto(` construction sites compile with the new `Validations` argument.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimValidationDto.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/DTOs/MechanicClaimDto.cs \
        "apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Queries/MechanicExtractor/GetMechanicAnalysisClaimsQueryHandler.cs" \
        "apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/"*Handler.cs \
        apps/api/tests
git commit -m "feat(mechanic-extractor): #526 derive T1-T4 validations[] on claim DTO (AC-1 contract)"
```

---

## Task 2: Backend — `review_note` column (AC-6 storage)

**Files:**
- Modify: `apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs`
- Modify: `apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs`
- Create: migration `AddMechanicClaimReviewNote`

**Interfaces:**
- Produces: `MechanicClaimEntity.ReviewNote : string?` mapped to nullable `review_note character varying(2000)`.

- [ ] **Step 1: Add the entity property** — after `RejectionNote` in `MechanicClaimEntity.cs`:

```csharp
    public string? RejectionNote { get; set; }

    /// <summary>Optional free-form note captured on APPROVE (#526 AC-6). Distinct from RejectionNote.</summary>
    public string? ReviewNote { get; set; }
```

- [ ] **Step 2: Map the column** — after the `RejectionNote` mapping block in `MechanicClaimEntityConfiguration.cs`:

```csharp
        builder.Property(c => c.RejectionNote)
            .HasColumnName("rejection_note")
            .HasMaxLength(2000);

        builder.Property(c => c.ReviewNote)
            .HasColumnName("review_note")
            .HasMaxLength(2000);
```

- [ ] **Step 3: Generate the migration**

Run: `cd apps/api/src/Api && dotnet ef migrations add AddMechanicClaimReviewNote`
Expected: a new migration adding `review_note` (`character varying(2000)`, nullable) to `mechanic_claims`.

- [ ] **Step 4: Review the migration SQL.** Open the generated `Up`/`Down`; confirm it is a single additive `AddColumn` (nullable, no default) and `DropColumn` on down. No other tables touched. (Idempotent-terminator lesson #2763: EF-generated `AddColumn` is safe; do not hand-edit.)

- [ ] **Step 5: Apply + verify build**

Run: `cd apps/api/src/Api && dotnet ef database update && dotnet build`
Expected: migration applies; build succeeds.

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Entities/SharedGameCatalog/MechanicClaimEntity.cs \
        apps/api/src/Api/Infrastructure/Configurations/SharedGameCatalog/MechanicClaimEntityConfiguration.cs \
        apps/api/src/Api/Migrations
git commit -m "feat(mechanic-extractor): #526 add nullable review_note column (AC-6)"
```

---

## Task 3: Backend — thread optional approve note

**Files:**
- Modify: `…/Domain/Entities/MechanicClaim.cs` (`Approve`)
- Modify: `…/Domain/Aggregates/MechanicAnalysis.cs` (`ApproveClaim`)
- Modify: `…/Application/Commands/MechanicExtractor/ApproveMechanicClaimCommand.cs`, `…Handler.cs`, `…Validator.cs`
- Modify: `…/Application/DTOs/MechanicClaimDto.cs` (add `ReviewNote`) + all construction sites
- Test: `apps/api/tests/…/MechanicExtractor/ApproveMechanicClaimCommandHandlerTests.cs`

**Interfaces:**
- Consumes: `MechanicClaimValidations.DerivePass()` (Task 1).
- Produces: `ApproveMechanicClaimCommand(Guid AnalysisId, Guid ClaimId, Guid ReviewerId, string? Note = null)`; `MechanicClaim.Approve(Guid, DateTime, string?)`; `MechanicAnalysis.ApproveClaim(Guid, Guid, DateTime, string?)`; `MechanicClaimDto.ReviewNote : string?`.

- [ ] **Step 1: Write the failing domain test** — in `ApproveMechanicClaimCommandHandlerTests.cs` (mirror the Moq/`BuildInReviewAnalysis` pattern from `BulkRejectMechanicClaimsCommandHandlerTests.cs`):

```csharp
[Fact]
public async Task Handle_WithNote_StoresReviewNote()
{
    var analysis = BuildInReviewAnalysis(1);
    var claimId = analysis.Claims.Single().Id;
    SetupRepo(analysis, analysis.Id);

    var result = await _handler.Handle(
        new ApproveMechanicClaimCommand(analysis.Id, claimId, Guid.NewGuid(), "looks good, matches p.4"),
        CancellationToken.None);

    result.Status.Should().Be(MechanicClaimStatus.Approved);
    result.ReviewNote.Should().Be("looks good, matches p.4");
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~ApproveMechanicClaimCommandHandlerTests.Handle_WithNote"`
Expected: FAIL — `ApproveMechanicClaimCommand` has 3 params / `MechanicClaimDto` has no `ReviewNote`.

- [ ] **Step 3: Entity** — change `MechanicClaim.Approve` (add `ReviewNote` property + note param):

```csharp
    /// <summary>Optional note captured on approval (#526 AC-6).</summary>
    public string? ReviewNote { get; private set; }

    internal void Approve(Guid reviewerId, DateTime utcNow, string? note = null)
    {
        if (reviewerId == Guid.Empty)
        {
            throw new ArgumentException("ReviewerId cannot be empty.", nameof(reviewerId));
        }

        Status = MechanicClaimStatus.Approved;
        ReviewedBy = reviewerId;
        ReviewedAt = utcNow;
        RejectionNote = null;
        ReviewNote = string.IsNullOrWhiteSpace(note) ? null : note.Trim();
    }
```
Also add `ReviewNote` to `Reconstitute` (new param + assignment) and set `ReviewNote = null` in `ResetToPending`. Update the `Reconstitute` call site in the repository's `MapToDomain` (grep `Reconstitute(` in `MechanicAnalysisRepository`).

- [ ] **Step 4: Aggregate** — `MechanicAnalysis.ApproveClaim`:

```csharp
    public void ApproveClaim(Guid claimId, Guid reviewerId, DateTime utcNow, string? note = null)
    {
        var claim = RequireClaimUnderReview(claimId, "approve claim");
        claim.Approve(reviewerId, utcNow, note);
    }
```

- [ ] **Step 5: Command + validator + handler**

Command (`ApproveMechanicClaimCommand.cs`):
```csharp
internal record ApproveMechanicClaimCommand(
    Guid AnalysisId,
    Guid ClaimId,
    Guid ReviewerId,
    string? Note = null) : ICommand<MechanicClaimDto>;
```
Validator (`ApproveMechanicClaimCommandValidator.cs`, after the ReviewerId rule):
```csharp
        RuleFor(c => c.Note)
            .MaximumLength(2000).WithMessage("Note must be 2000 characters or fewer.")
            .When(c => c.Note is not null);
```
Handler (`ApproveMechanicClaimCommandHandler.cs`, line ~76): `analysis.ApproveClaim(request.ClaimId, request.ReviewerId, utcNow, request.Note);` and in `ToDto` add `ReviewNote: claim.ReviewNote,` after `RejectionNote:`.

- [ ] **Step 6: DTO + entity mapping + all construction sites**

Add `string? ReviewNote` to `MechanicClaimDto` (after `RejectionNote`); in `GetMechanicAnalysisClaimsQueryHandler` projection add `ReviewNote: c.ReviewNote,` (the query reads the `MechanicClaimEntity`, so `c.ReviewNote` resolves after Task 2); repeat for the Reject/BulkApprove/BulkReject projection sites (add `ReviewNote: c.ReviewNote,`). Confirm the entity→domain map (`MechanicClaimEntityConfiguration` already maps the column; the repository `Reconstitute` call must pass `entity.ReviewNote`).

- [ ] **Step 7: Run to verify it passes + build**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~ApproveMechanicClaimCommandHandlerTests" && dotnet build`
Expected: PASS + build OK (all `new MechanicClaimDto(` sites compile with `ReviewNote`).

- [ ] **Step 8: Commit**

```bash
git add -A
git commit -m "feat(mechanic-extractor): #526 thread optional approve note through to review_note (AC-6)"
```

---

## Task 4: Backend — `mechanic_review_bulk_actions_total` counter (AC-7)

**Files:**
- Create: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.MechanicReview.cs`
- Modify: `…/Commands/MechanicExtractor/BulkRejectMechanicClaimsCommandHandler.cs` + `BulkApproveMechanicClaimsCommandHandler.cs`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/MechanicReviewMetricsTests.cs`

**Interfaces:**
- Produces: `MeepleAiMetrics.MechanicReviewBulkActions` (`Counter<long>`), incremented `Add(1, TagList{{"action", "bulk_reject"|"bulk_approve"}})`.

- [ ] **Step 1: Write the failing test** — mirror `MechanicValidatorMetricsTests` (name-filtered `MeterListener`, `ContainSingle`, Approach 1 / #2752):

```csharp
using System.Diagnostics.Metrics;
using Api.Observability;
using FluentAssertions;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
public class MechanicReviewMetricsTests
{
    private const string Counter = "mechanic_review_bulk_actions_total";

    [Fact]
    public void MechanicReviewBulkActions_Emits_ActionTag()
    {
        var events = new List<string>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Meter.Name == MeepleAiMetrics.MeterName && instrument.Name == Counter)
                    l.EnableMeasurementEvents(instrument);
            }
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
                if (tag.Key == "action" && tag.Value is string a) events.Add(a);
        });
        listener.Start();

        MeepleAiMetrics.MechanicReviewBulkActions.Add(1, new System.Diagnostics.TagList { { "action", "bulk_reject" } });

        events.Should().ContainSingle().Which.Should().Be("bulk_reject");
    }
}
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicReviewMetricsTests"`
Expected: FAIL — `MechanicReviewBulkActions` does not exist.

- [ ] **Step 3: Declare the counter** — `MeepleAiMetrics.MechanicReview.cs`:

```csharp
using System.Diagnostics.Metrics;

namespace Api.Observability;

/// <summary>#526 ME-M1.4 admin-review observability (AC-7): bulk-action counter.</summary>
internal static partial class MeepleAiMetrics
{
    /// <summary>Admin mechanic-review bulk actions, tagged {action=bulk_approve|bulk_reject}.</summary>
    public static readonly Counter<long> MechanicReviewBulkActions =
        Meter.CreateCounter<long>(
            "mechanic_review_bulk_actions_total",
            description: "Admin mechanic-review bulk actions by action.");
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/api/src/Api && dotnet test --filter "FullyQualifiedName~MechanicReviewMetricsTests"`
Expected: PASS.

- [ ] **Step 5: Increment in the handlers.** In `BulkRejectMechanicClaimsCommandHandler.Handle` (after the successful `SaveChangesAsync`/log region, before building the response) add:

```csharp
        MeepleAiMetrics.MechanicReviewBulkActions.Add(1, new System.Diagnostics.TagList { { "action", "bulk_reject" } });
```
Add `using System.Diagnostics;` and `using Api.Observability;` at the top. Do the same in `BulkApproveMechanicClaimsCommandHandler` with `{ "action", "bulk_approve" }`.

- [ ] **Step 6: Build**

Run: `cd apps/api/src/Api && dotnet build`
Expected: succeeds.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Observability apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/BulkRejectMechanicClaimsCommandHandler.cs apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/BulkApproveMechanicClaimsCommandHandler.cs apps/api/tests
git commit -m "feat(mechanic-extractor): #526 mechanic_review_bulk_actions_total counter (AC-7)"
```

---

## Task 5: Frontend — schemas + client (validations, bulk-reject, approve note)

**Files:**
- Modify: `apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts`
- Modify: `apps/web/src/lib/api/clients/admin/adminContentClient.ts`
- Test: `apps/web/src/lib/api/schemas/__tests__/mechanic-analyses.schemas.test.ts` (create if absent)

**Interfaces:**
- Produces: `MechanicClaimValidationDtoSchema`; `MechanicClaimDto.validations` + `.reviewNote`; `MECHANIC_ANALYSES_ROUTES.bulkRejectClaims`; `BulkRejectMechanicClaimsResponseDtoSchema`/`Dto`; `BulkRejectMechanicClaimsRequest`; `adminClient.bulkRejectMechanicClaims(id, req)`; `adminClient.approveMechanicClaim(id, claimId, note?)`.

- [ ] **Step 1: Write the failing schema test**

```ts
import { describe, expect, it } from 'vitest';
import { MechanicClaimDtoSchema, BulkRejectMechanicClaimsResponseDtoSchema } from '../mechanic-analyses.schemas';

describe('mechanic-analyses schemas #526', () => {
  it('parses validations[] + reviewNote on a claim', () => {
    const parsed = MechanicClaimDtoSchema.parse({
      id: '11111111-1111-4111-8111-111111111111',
      analysisId: '22222222-2222-4222-8222-222222222222',
      section: 1, text: 't', displayOrder: 0, status: 0,
      reviewedBy: null, reviewedAt: null, rejectionNote: null, reviewNote: null,
      citations: [],
      validations: [{ rule: 'T1', outcome: 'pass', message: null }],
    });
    expect(parsed.validations[0].rule).toBe('T1');
  });

  it('parses bulk-reject response', () => {
    const r = BulkRejectMechanicClaimsResponseDtoSchema.parse({
      rejectedCount: 2, skippedAlreadyRejectedCount: 0, claims: [],
    });
    expect(r.rejectedCount).toBe(2);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/lib/api/schemas/__tests__/mechanic-analyses.schemas.test.ts`
Expected: FAIL — `validations`/`reviewNote` unknown, `BulkRejectMechanicClaimsResponseDtoSchema` undefined.

- [ ] **Step 3: Add the validation schema + fields.** After `MechanicCitationDtoSchema` (line 234) add:

```ts
export const MechanicClaimValidationDtoSchema = z.object({
  rule: z.string(),
  outcome: z.enum(['pass', 'fail', 'notRun']),
  message: z.string().nullable().optional(),
});
export type MechanicClaimValidationDto = z.infer<typeof MechanicClaimValidationDtoSchema>;
```
In `MechanicClaimDtoSchema`, after `citations: z.array(MechanicCitationDtoSchema),` add:
```ts
  reviewNote: z.string().nullable(),
  validations: z.array(MechanicClaimValidationDtoSchema),
```

- [ ] **Step 4: Add route + response + request.** After the `bulkApproveClaims` route entry:
```ts
  bulkRejectClaims: (id: string) => `/api/v1/admin/mechanic-analyses/${id}/claims/bulk-reject`,
```
After `BulkApproveMechanicClaimsResponseDto` type export:
```ts
export const BulkRejectMechanicClaimsResponseDtoSchema = z.object({
  rejectedCount: z.number().int(),
  skippedAlreadyRejectedCount: z.number().int(),
  claims: MechanicClaimsListSchema,
});
export type BulkRejectMechanicClaimsResponseDto = z.infer<
  typeof BulkRejectMechanicClaimsResponseDtoSchema
>;

/** Body for `POST .../claims/bulk-reject` (#526). Reviewer id comes from the session. */
export interface BulkRejectMechanicClaimsRequest {
  claimIds: string[];
  reason: string;
}
```

- [ ] **Step 5: Add client methods.** In `adminContentClient.ts` imports, add `BulkRejectMechanicClaimsResponseDtoSchema` (value) + `type BulkRejectMechanicClaimsResponseDto`, `type BulkRejectMechanicClaimsRequest` (preserve alpha order). After `bulkApproveMechanicClaims`:

```ts
    async bulkRejectMechanicClaims(
      analysisId: string,
      request: BulkRejectMechanicClaimsRequest
    ): Promise<BulkRejectMechanicClaimsResponseDto> {
      const result = await http.post(
        MECHANIC_ANALYSES_ROUTES.bulkRejectClaims(analysisId),
        request,
        BulkRejectMechanicClaimsResponseDtoSchema
      );
      if (!result) throw new Error('Failed to bulk-reject claims');
      return result;
    },
```
Change `approveMechanicClaim` signature to `(analysisId: string, claimId: string, note?: string)` and its POST body from `{}` to `note !== undefined ? { note } : {}`.

- [ ] **Step 6: Run to verify it passes + typecheck**

Run: `cd apps/web && pnpm test src/lib/api/schemas/__tests__/mechanic-analyses.schemas.test.ts && pnpm typecheck`
Expected: PASS + typecheck OK.

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/lib/api/schemas/mechanic-analyses.schemas.ts apps/web/src/lib/api/clients/admin/adminContentClient.ts apps/web/src/lib/api/schemas/__tests__
git commit -m "feat(mechanic-extractor): #526 FE schemas+client for validations, bulk-reject, approve note"
```

---

## Task 6: Frontend — pure quote-highlight helper + PdfInlineViewer opt-in text layer

**Files:**
- Create: `apps/web/src/components/pdf/pdf-quote-highlight.ts` + `__tests__/pdf-quote-highlight.test.ts`
- Modify: `apps/web/src/components/pdf/PdfInlineViewer.tsx`

**Interfaces:**
- Produces: `normalizeQuoteText(s: string): string`; `makeQuoteTextRenderer(quote: string): { render: (item: { str: string }) => string; matched: () => boolean }`; `PdfInlineViewer` props `renderTextLayer?: boolean`, `highlightQuote?: string`, `onQuoteMatch?: (found: boolean) => void`.

- [ ] **Step 1: Write the failing helper test**

```ts
import { describe, expect, it } from 'vitest';
import { normalizeQuoteText, makeQuoteTextRenderer } from '../pdf-quote-highlight';

describe('pdf-quote-highlight', () => {
  it('normalizes whitespace, soft hyphens, case', () => {
    expect(normalizeQuoteText('Score  1­point\nper  Road')).toBe('score 1point per road');
  });

  it('wraps text items contained in the quote and reports a match', () => {
    const r = makeQuoteTextRenderer('players score one point per road');
    expect(r.render({ str: 'score one point' })).toContain('<mark');
    expect(r.render({ str: 'per road' })).toContain('<mark');
    expect(r.matched()).toBe(true);
  });

  it('escapes HTML and does not wrap non-quote items', () => {
    const r = makeQuoteTextRenderer('players score one point');
    expect(r.render({ str: '<b>bonus</b>' })).toBe('&lt;b&gt;bonus&lt;/b&gt;');
    expect(r.matched()).toBe(false);
  });

  it('ignores trivially short items to reduce false positives', () => {
    const r = makeQuoteTextRenderer('a player scores one point');
    expect(r.render({ str: 'a' })).toBe('a'); // len<=2 not wrapped
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/pdf/__tests__/pdf-quote-highlight.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the helper** — `pdf-quote-highlight.ts`:

```ts
/** Normalize for tolerant substring matching: lowercase, strip soft hyphens, collapse whitespace. */
export function normalizeQuoteText(s: string): string {
  return s
    .replace(/­/g, '')
    .toLowerCase()
    .replace(/\s+/g, ' ')
    .trim();
}

function escapeHtml(s: string): string {
  return s
    .replace(/&/g, '&amp;')
    .replace(/</g, '&lt;')
    .replace(/>/g, '&gt;')
    .replace(/"/g, '&quot;');
}

/**
 * Best-effort per-item highlighter for react-pdf `customTextRenderer` (AC-2 Pattern A).
 * A text item is wrapped in <mark> when its normalized string (len>2) is a substring of the
 * normalized quote. Imperfect for very common short items — the caller shows a fallback banner
 * via `matched()`. FU could upgrade to contiguous-run matching or Pattern-B coordinates.
 */
export function makeQuoteTextRenderer(quote: string): {
  render: (item: { str: string }) => string;
  matched: () => boolean;
} {
  const normQuote = normalizeQuoteText(quote);
  let didMatch = false;
  return {
    render: ({ str }) => {
      const norm = normalizeQuoteText(str);
      if (norm.length > 2 && normQuote.includes(norm)) {
        didMatch = true;
        return `<mark class="pdf-quote-highlight">${escapeHtml(str)}</mark>`;
      }
      return escapeHtml(str);
    },
    matched: () => didMatch,
  };
}
```

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/web && pnpm test src/components/pdf/__tests__/pdf-quote-highlight.test.ts`
Expected: PASS.

- [ ] **Step 5: Parameterize PdfInlineViewer.** Add to `PdfInlineViewerProps`:
```ts
  readonly renderTextLayer?: boolean;
  readonly highlightQuote?: string;
  readonly onQuoteMatch?: (found: boolean) => void;
```
Destructure them (with `renderTextLayer` default `false`). Build the renderer + effect:
```tsx
import { makeQuoteTextRenderer } from './pdf-quote-highlight';
// … inside component:
  const quoteRenderer = useMemo(
    () => (highlightQuote ? makeQuoteTextRenderer(highlightQuote) : null),
    [highlightQuote]
  );
```
Change the `<Page>` (line ~290) to:
```tsx
            <Page
              pageNumber={currentPage}
              scale={scale}
              renderAnnotationLayer={false}
              renderTextLayer={renderTextLayer || !!highlightQuote}
              customTextRenderer={quoteRenderer ? ({ str }) => quoteRenderer.render({ str }) : undefined}
              onRenderTextLayerSuccess={
                quoteRenderer && onQuoteMatch ? () => onQuoteMatch(quoteRenderer.matched()) : undefined
              }
            />
```
Add a highlight style (module-level or in `apps/web/src/app/globals.css`): `.pdf-quote-highlight { background-color: rgba(255, 235, 59, 0.4); }`.

- [ ] **Step 6: Typecheck + existing viewer tests still green**

Run: `cd apps/web && pnpm typecheck && pnpm test src/components/pdf`
Expected: PASS (new props are optional; default behavior unchanged — `renderTextLayer` still off when not requested).

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/pdf/pdf-quote-highlight.ts apps/web/src/components/pdf/__tests__/pdf-quote-highlight.test.ts apps/web/src/components/pdf/PdfInlineViewer.tsx apps/web/src/app/globals.css
git commit -m "feat(pdf): #526 opt-in text-layer quote highlighting on PdfInlineViewer (AC-2 base)"
```

---

## Task 7: Frontend — `<PdfQuoteHighlighter>` modal

**Files:**
- Create: `apps/web/src/components/pdf/PdfQuoteHighlighter.tsx`
- Test: `apps/web/src/components/pdf/__tests__/PdfQuoteHighlighter.test.tsx`

**Interfaces:**
- Consumes: `PdfInlineViewer` (Task 6).
- Produces: `<PdfQuoteHighlighter open onOpenChange documentId page quote />` (default export named `PdfQuoteHighlighter`). Renders a Dialog with the viewer at `page`, highlights `quote`, and shows a fallback banner when no match.

- [ ] **Step 1: Write the failing test** (mock `PdfInlineViewer` to drive `onQuoteMatch`):

```tsx
/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

vi.mock('../PdfInlineViewer', () => ({
  PdfInlineViewer: ({ onQuoteMatch }: { onQuoteMatch?: (f: boolean) => void }) => {
    onQuoteMatch?.(false); // simulate "not found" → banner should show
    return <div data-testid="pdf-inline-viewer" />;
  },
}));

import { PdfQuoteHighlighter } from '../PdfQuoteHighlighter';

describe('PdfQuoteHighlighter', () => {
  it('shows the fallback banner when the quote is not matched', () => {
    render(
      <PdfQuoteHighlighter open onOpenChange={() => {}} documentId="d1" page={4} quote="x" />
    );
    expect(screen.getByTestId('pdf-inline-viewer')).toBeInTheDocument();
    expect(screen.getByTestId('pdf-quote-fallback')).toHaveTextContent(/verifica manualmente/i);
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/pdf/__tests__/PdfQuoteHighlighter.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement** — `PdfQuoteHighlighter.tsx`:

```tsx
'use client';

import React, { useEffect, useState } from 'react';

import {
  Dialog,
  DialogContent,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';

import { PdfInlineViewer } from './PdfInlineViewer';

export interface PdfQuoteHighlighterProps {
  readonly open: boolean;
  readonly onOpenChange: (open: boolean) => void;
  readonly documentId: string;
  readonly page: number;
  readonly quote: string;
}

/**
 * #526 AC-2 / #530 AD-1 — shared citation quote viewer. Opens the source PDF at `page`, highlights
 * `quote` via PdfInlineViewer's text-layer search (Pattern A), and shows a page-level fallback
 * banner when the quote can't be located automatically. Consumed by admin review (#526) and,
 * later, #528 public card + #530 chat citations.
 */
export function PdfQuoteHighlighter({
  open,
  onOpenChange,
  documentId,
  page,
  quote,
}: PdfQuoteHighlighterProps): React.JSX.Element {
  const [matched, setMatched] = useState<boolean | null>(null);

  useEffect(() => {
    if (open) setMatched(null); // reset per open
  }, [open, documentId, page, quote]);

  return (
    <Dialog open={open} onOpenChange={onOpenChange}>
      <DialogContent className="max-w-4xl">
        <DialogHeader>
          <DialogTitle>Citazione — p.{page}</DialogTitle>
        </DialogHeader>
        {matched === false && (
          <div
            className="rounded-md border border-amber-300 bg-amber-50 p-2 text-xs text-amber-900"
            role="status"
            data-testid="pdf-quote-fallback"
          >
            Quote non individuabile automaticamente a p.{page}; verifica manualmente.
          </div>
        )}
        <div className="max-h-[70vh] overflow-auto">
          <PdfInlineViewer
            documentId={documentId}
            initialPage={page}
            highlightQuote={quote}
            onQuoteMatch={setMatched}
            features={{ jumpToPage: true, zoom: true }}
          />
        </div>
      </DialogContent>
    </Dialog>
  );
}
```
(If `@/components/ui/overlays/dialog` exports differ, mirror the imports used by an existing modal, e.g. `PdfViewerModal.tsx`.)

- [ ] **Step 4: Run to verify it passes**

Run: `cd apps/web && pnpm test src/components/pdf/__tests__/PdfQuoteHighlighter.test.tsx && pnpm typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/pdf/PdfQuoteHighlighter.tsx apps/web/src/components/pdf/__tests__/PdfQuoteHighlighter.test.tsx
git commit -m "feat(pdf): #526 PdfQuoteHighlighter modal at components/pdf/ (AC-2, reconciles #530 AD-1)"
```

---

## Task 8: Frontend — T1–T4 badges in ClaimsSection (AC-1 UI)

**Files:**
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx`
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.badges.test.tsx`

**Interfaces:**
- Consumes: `MechanicClaimDto.validations` (Task 5).
- Produces: a `ValidationBadges` sub-component rendered in each `ClaimRow`, `data-testid="claim-validation-badge-<rule>-<claimId>"`, `aria-label` per badge.

- [ ] **Step 1: Write the failing test** (mount `ClaimRow` via the exported `ClaimsSection` with a mocked client, or export `ValidationBadges` and test it directly — prefer exporting the small badge component). Add near the top of `ClaimsSection.tsx`: `export function ValidationBadges({ validations }: { validations: MechanicClaimValidationDto[] })`. Test:

```tsx
/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { ValidationBadges } from '../ClaimsSection';

describe('ValidationBadges', () => {
  it('renders one badge per rule with pass/fail/notRun styling + aria-label', () => {
    render(
      <ValidationBadges
        validations={[
          { rule: 'T1', outcome: 'pass', message: null },
          { rule: 'T2', outcome: 'fail', message: 'too long' },
          { rule: 'T3', outcome: 'notRun', message: null },
        ]}
      />
    );
    expect(screen.getByTestId('claim-validation-badge-T1')).toHaveAttribute('aria-label', expect.stringMatching(/T1.*pass/i));
    expect(screen.getByTestId('claim-validation-badge-T2')).toHaveAttribute('aria-label', expect.stringMatching(/T2.*fail/i));
    expect(screen.getByTestId('claim-validation-badge-T3')).toHaveAttribute('aria-label', expect.stringMatching(/T3.*not/i));
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.badges.test.tsx`
Expected: FAIL — `ValidationBadges` not exported.

- [ ] **Step 3: Implement `ValidationBadges`** in `ClaimsSection.tsx` (add the type import `MechanicClaimValidationDto` from the schemas module) and a color map:

```tsx
const VALIDATION_BADGE_CLASS: Record<string, string> = {
  pass: 'bg-green-100 text-green-800 border-green-300',
  fail: 'bg-rose-100 text-rose-800 border-rose-300',
  notRun: 'bg-slate-100 text-slate-600 border-slate-300',
};

export function ValidationBadges({
  validations,
}: {
  validations: MechanicClaimValidationDto[];
}): React.JSX.Element | null {
  if (!validations || validations.length === 0) return null;
  return (
    <span className="flex flex-wrap gap-1" data-testid="claim-validation-badges">
      {validations.map(v => (
        <Badge
          key={v.rule}
          variant="outline"
          className={VALIDATION_BADGE_CLASS[v.outcome] ?? VALIDATION_BADGE_CLASS.notRun}
          data-testid={`claim-validation-badge-${v.rule}`}
          aria-label={`${v.rule} ${v.outcome}${v.message ? `: ${v.message}` : ''}`}
          title={v.message ?? `${v.rule} ${v.outcome}`}
        >
          {v.outcome === 'pass' ? '✓' : v.outcome === 'fail' ? '✗' : '—'} {v.rule}
        </Badge>
      ))}
    </span>
  );
}
```
Render `<ValidationBadges validations={claim.validations} />` inside `ClaimRow`, in the right-hand action cluster before the status `Badge` (around line 409).

- [ ] **Step 4: Run to verify it passes + existing ClaimsSection tests green**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims && pnpm typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.badges.test.tsx
git commit -m "feat(mechanic-extractor): #526 T1-T4 validation badges in ClaimsSection (AC-1 UI)"
```

---

## Task 9: Frontend — citation click → PdfQuoteHighlighter (AC-2 wire)

**Files:**
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx` (add `pdfDocumentId` prop; citation rows become buttons)
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/analyses/page.tsx` (pass `pdfDocumentId={status.pdfDocumentId}`)
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.citation.test.tsx`

**Interfaces:**
- Consumes: `PdfQuoteHighlighter` (Task 7).
- Produces: `ClaimsSectionProps.pdfDocumentId?: string`; citation button `data-testid="claim-citation-open-<citationId>"`.

- [ ] **Step 1: Write the failing test** — mock `PdfQuoteHighlighter`, mock `adminClient.getMechanicAnalysisClaims` to return one claim with a citation, click the citation, assert the highlighter opened with the right props.

```tsx
/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetClaims = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({ getMechanicAnalysisClaims: mockGetClaims }),
}));
class MockHttpClient {}
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));
const mockHighlighter = vi.hoisted(() => vi.fn());
vi.mock('@/components/pdf/PdfQuoteHighlighter', () => ({
  PdfQuoteHighlighter: (props: Record<string, unknown>) => {
    mockHighlighter(props);
    return props.open ? <div data-testid="highlighter-open" /> : null;
  },
}));

import { ClaimsSection } from '../ClaimsSection';

const claim = {
  id: 'dddddddd-dddd-4ddd-8ddd-dddddddddddd', analysisId: 'a', section: 1, text: 't',
  displayOrder: 0, status: 0, reviewedBy: null, reviewedAt: null, rejectionNote: null,
  reviewNote: null, validations: [],
  citations: [{ id: 'c1', pdfPage: 4, quote: 'score one point', displayOrder: 0 }],
};

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection citation viewer', () => {
  it('opens PdfQuoteHighlighter with documentId/page/quote on citation click', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    render(<ClaimsSection analysisId="a" pdfDocumentId="pdf-99" />, { wrapper: Wrapper });
    // expand citations then click
    fireEvent.click(await screen.findByTestId(`claim-citations-toggle-${claim.id}`));
    fireEvent.click(screen.getByTestId('claim-citation-open-c1'));
    expect(mockHighlighter).toHaveBeenCalledWith(
      expect.objectContaining({ documentId: 'pdf-99', page: 4, quote: 'score one point', open: true })
    );
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.citation.test.tsx`
Expected: FAIL — `pdfDocumentId` prop / citation button / highlighter wiring missing.

- [ ] **Step 3: Implement.** Add `pdfDocumentId?: string` to `ClaimsSectionProps` and thread it to `SectionGroup` → `ClaimRow`. Add highlighter state in `ClaimsSection` (`const [citationTarget, setCitationTarget] = useState<{ documentId: string; page: number; quote: string } | null>(null)`), a callback `onOpenCitation`, and render `<PdfQuoteHighlighter open={!!citationTarget} onOpenChange={o => !o && setCitationTarget(null)} {...citationTarget} />` guarded by `citationTarget`. In `ClaimRow` citation `<li>` (line ~462), replace the static text with a button when `pdfDocumentId` is present:

```tsx
{claim.citations.map(c => (
  <li key={c.id} className="text-muted-foreground">
    {pdfDocumentId ? (
      <button
        type="button"
        className="text-left hover:underline"
        onClick={() => onOpenCitation({ documentId: pdfDocumentId, page: c.pdfPage, quote: c.quote })}
        data-testid={`claim-citation-open-${c.id}`}
      >
        <span className="font-medium text-foreground">p.{c.pdfPage}</span> — &ldquo;{c.quote}&rdquo;
      </button>
    ) : (
      <>
        <span className="font-medium text-foreground">p.{c.pdfPage}</span> — &ldquo;{c.quote}&rdquo;
      </>
    )}
  </li>
))}
```
Thread `onOpenCitation` and `pdfDocumentId` down the `SectionGroup`/`ClaimRow` prop chain.

- [ ] **Step 4: Pass `pdfDocumentId` from the parent.** In `analyses/page.tsx`, find where `<ClaimsSection analysisId=… isClaimsActionable=… />` renders and add `pdfDocumentId={status?.pdfDocumentId}` (the status query already carries `pdfDocumentId`).

- [ ] **Step 5: Run to verify it passes + existing tests + typecheck**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims && pnpm typecheck`
Expected: PASS.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx "apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/analyses/page.tsx" apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.citation.test.tsx
git commit -m "feat(mechanic-extractor): #526 open PdfQuoteHighlighter from citation rows (AC-2 wire)"
```

---

## Task 10: Frontend — bulk-action dropdown (AC-3)

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/claims/BulkActionDialog.tsx`
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx`
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.bulk.test.tsx`

**Interfaces:**
- Consumes: `adminClient.bulkApproveMechanicClaims`, `adminClient.bulkRejectMechanicClaims` (Task 5).
- Produces: a `Select`-driven bulk menu with options `approve-pending` + `reject-long-quote`; `BulkActionDialog` confirm showing the predicted count. `data-testid` `bulk-action-select`, `bulk-action-confirm`, `bulk-action-count`.

- [ ] **Step 1: Write the failing test** — pick "reject all with quote >20 words", assert the confirm dialog shows the count and calls `bulkRejectMechanicClaims` with the computed ids.

```tsx
/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent } from '@testing-library/react';
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
class MockHttpClient {}
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));

import { ClaimsSection } from '../ClaimsSection';

const longQuote = Array.from({ length: 25 }, (_, i) => `w${i}`).join(' ');
const claims = [
  { id: 'd1', analysisId: 'a', section: 1, text: 't1', displayOrder: 0, status: 0,
    reviewedBy: null, reviewedAt: null, rejectionNote: null, reviewNote: null, validations: [],
    citations: [{ id: 'c1', pdfPage: 1, quote: longQuote, displayOrder: 0 }] },
  { id: 'd2', analysisId: 'a', section: 1, text: 't2', displayOrder: 1, status: 0,
    reviewedBy: null, reviewedAt: null, rejectionNote: null, reviewNote: null, validations: [],
    citations: [{ id: 'c2', pdfPage: 1, quote: 'short quote', displayOrder: 0 }] },
];

function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection bulk reject by quote length', () => {
  it('rejects only the >20-word-quote claim after count-confirm', async () => {
    mockGetClaims.mockResolvedValue(claims);
    mockBulkReject.mockResolvedValue({ rejectedCount: 1, skippedAlreadyRejectedCount: 0, claims });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });

    fireEvent.change(await screen.findByTestId('bulk-action-select'), {
      target: { value: 'reject-long-quote' },
    });
    expect(screen.getByTestId('bulk-action-count')).toHaveTextContent('1');
    fireEvent.click(screen.getByTestId('bulk-action-confirm'));

    expect(mockBulkReject).toHaveBeenCalledWith('a', expect.objectContaining({ claimIds: ['d1'] }));
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.bulk.test.tsx`
Expected: FAIL — bulk select / confirm not implemented.

- [ ] **Step 3: Implement the predicate + dialog.** Add a pure predicate helper near the top of `ClaimsSection.tsx`:
```tsx
const LONG_QUOTE_WORDS = 20;
function wordCount(s: string): number {
  return s.trim().split(/\s+/).filter(Boolean).length;
}
function claimsWithLongQuote(claims: MechanicClaimDto[]): MechanicClaimDto[] {
  return claims.filter(c => c.citations.some(cit => wordCount(cit.quote) > LONG_QUOTE_WORDS));
}
```
Create `BulkActionDialog.tsx` (an `AlertDialog` mirroring `RejectClaimDialog`) with props `{ open, onOpenChange, title, count, onConfirm, isPending }` rendering `data-testid="bulk-action-count"` and `data-testid="bulk-action-confirm"`. In the `ClaimsSection` header (replace the single bulk-approve button, lines ~220–235) add a `Select` (`data-testid="bulk-action-select"`) with options `Approve all pending ({pending})` → value `approve-pending`, `Reject all with quote >20 words ({n})` → value `reject-long-quote`. On change, compute the target set and open `BulkActionDialog` with the count. On confirm:
- `approve-pending` → `bulkApproveMutation.mutate()` (existing).
- `reject-long-quote` → `bulkRejectMutation.mutate({ claimIds: targets.map(c => c.id), reason: 'Citazione supera 20 parole (ADR-051 T1) — rifiuto bulk.' })`.
Add `bulkRejectMutation` (mirror `bulkApproveMutation`; on success set an amber warning if `skippedAlreadyRejectedCount > 0`, then `invalidateClaimsAndStatus()`).

- [ ] **Step 4: Run to verify it passes + typecheck + existing tests**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims && pnpm typecheck`
Expected: PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/claims
git commit -m "feat(mechanic-extractor): #526 bulk-action dropdown with reject-by-quote-length + count-confirm (AC-3)"
```

---

## Task 11: Frontend — approve-with-note dialog (AC-6 UI)

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/claims/ApproveClaimDialog.tsx`
- Modify: `apps/web/src/components/admin/mechanic-extractor/claims/ClaimsSection.tsx`
- Test: `apps/web/src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.approveNote.test.tsx`

**Interfaces:**
- Consumes: `adminClient.approveMechanicClaim(id, claimId, note?)` (Task 5).
- Produces: an optional-note approve flow. `data-testid` `approve-claim-note-input`, `approve-claim-confirm`.

- [ ] **Step 1: Write the failing test** — click Approve → dialog → type a note → confirm → assert `approveMechanicClaim('a','d1','matches p.4')`, and the reviewNote renders after refetch.

```tsx
/** @vitest-environment jsdom */
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

const mockGetClaims = vi.hoisted(() => vi.fn());
const mockApprove = vi.hoisted(() => vi.fn());
vi.mock('@/lib/api/clients/adminClient', () => ({
  createAdminClient: () => ({ getMechanicAnalysisClaims: mockGetClaims, approveMechanicClaim: mockApprove }),
}));
class MockHttpClient {}
vi.mock('@/lib/api/core/httpClient', () => ({ HttpClient: MockHttpClient }));

import { ClaimsSection } from '../ClaimsSection';
const claim = { id: 'd1', analysisId: 'a', section: 1, text: 't', displayOrder: 0, status: 0,
  reviewedBy: null, reviewedAt: null, rejectionNote: null, reviewNote: null, validations: [], citations: [] };
function Wrapper({ children }: { children: React.ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('ClaimsSection approve with note', () => {
  it('sends the optional note on approve', async () => {
    mockGetClaims.mockResolvedValue([claim]);
    mockApprove.mockResolvedValue({ ...claim, status: 1, reviewNote: 'matches p.4' });
    render(<ClaimsSection analysisId="a" />, { wrapper: Wrapper });
    fireEvent.click(await screen.findByTestId('claim-approve-d1'));
    fireEvent.change(screen.getByTestId('approve-claim-note-input'), { target: { value: 'matches p.4' } });
    fireEvent.click(screen.getByTestId('approve-claim-confirm'));
    expect(mockApprove).toHaveBeenCalledWith('a', 'd1', 'matches p.4');
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims/__tests__/ClaimsSection.approveNote.test.tsx`
Expected: FAIL — approve now opens a dialog instead of firing immediately.

- [ ] **Step 3: Implement.** Create `ApproveClaimDialog.tsx` (`AlertDialog`, optional textarea, `data-testid="approve-claim-note-input"` + `approve-claim-confirm`; note is optional so confirm is always enabled). In `ClaimsSection`, change the Approve button to set an `approveTarget` (like `rejectTarget`) instead of calling `approveMutation.mutate` directly; render `<ApproveClaimDialog … onConfirm={note => approveMutation.mutate({ claimId: approveTarget.id, note })} />`. Change `approveMutation` to accept `{ claimId, note }` and call `adminClient.approveMechanicClaim(analysisId, claimId, note || undefined)`. Render `claim.reviewNote` in `ClaimRow` (a small green note block mirroring the rejection-note block) when present.

- [ ] **Step 4: Run to verify it passes + existing claims tests + typecheck**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/claims && pnpm typecheck`
Expected: PASS. (Note: existing tests that click `claim-approve-<id>` and expect immediate approval must be updated to go through the dialog — update them in this step.)

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/claims
git commit -m "feat(mechanic-extractor): #526 approve-with-note dialog (AC-6 UI)"
```

---

## Task 12: Frontend — footer attribution swap (AC-5)

**Files:**
- Create: `apps/web/src/components/admin/mechanic-extractor/MechanicAnalysisFooterAttribution.tsx`
- Modify: `apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx`
- Test: `apps/web/src/components/admin/mechanic-extractor/__tests__/MechanicAnalysisFooterAttribution.test.tsx`

**Interfaces:**
- Produces: `<MechanicAnalysisFooterAttribution totalTokensUsed? estimatedCostUsd? />` rendering the ADR-051 canonical string.

- [ ] **Step 1: Write the failing test**

```tsx
/** @vitest-environment jsdom */
import { render, screen } from '@testing-library/react';
import { describe, expect, it } from 'vitest';
import { MechanicAnalysisFooterAttribution } from '../MechanicAnalysisFooterAttribution';

describe('MechanicAnalysisFooterAttribution', () => {
  it('renders the ADR-051 attribution and NOT the forbidden Variant-C string', () => {
    render(<MechanicAnalysisFooterAttribution totalTokensUsed={500} estimatedCostUsd={0.002} />);
    expect(screen.getByText(/riformulata in parole originali e cita la pagina/i)).toBeInTheDocument();
    expect(screen.queryByText(/non ha mai letto il testo del PDF originale/i)).not.toBeInTheDocument();
    expect(screen.getByText(/500 tokens/)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run to verify it fails**

Run: `cd apps/web && pnpm test src/components/admin/mechanic-extractor/__tests__/MechanicAnalysisFooterAttribution.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the component** — `MechanicAnalysisFooterAttribution.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin tools chrome. DS-13d admin scope. */
import React from 'react';

export interface MechanicAnalysisFooterAttributionProps {
  readonly totalTokensUsed?: number;
  readonly estimatedCostUsd?: number;
}

/**
 * #526 AC-5 — ADR-051 canonical attribution footer. Shared between the admin review page and the
 * public card (#528). Replaces the retired Variant-C "L'AI non ha mai letto…" copy.
 */
export function MechanicAnalysisFooterAttribution({
  totalTokensUsed,
  estimatedCostUsd,
}: MechanicAnalysisFooterAttributionProps): React.JSX.Element {
  return (
    <div className="rounded-lg border border-green-200 bg-green-50/50 p-4 text-center text-xs text-green-800 dark:border-green-800 dark:bg-green-950/20 dark:text-green-300 print:border-green-400">
      <strong>&copy; 2026 MeepleAI</strong> — Contenuto originale.
      <br />
      <span className="opacity-70">
        Analisi elaborata dall&apos;AI sul manuale del gioco. Ogni affermazione è riformulata in
        parole originali e cita la pagina del regolamento. Copyright &copy; degli editori per il
        testo originale del manuale.
      </span>
      {(totalTokensUsed ?? 0) > 0 && (
        <span className="ml-2 opacity-70">
          | {totalTokensUsed} tokens, ${(estimatedCostUsd ?? 0).toFixed(4)}
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Swap in `review/page.tsx`.** Add the import to the `@/components/admin/mechanic-extractor/…` cluster; replace the footer `<div>` block (lines 271–285, the `{/* Copyright Footer */}` comment + div) with:
```tsx
      {/* Copyright Footer (ADR-051, #526 AC-5) */}
      <MechanicAnalysisFooterAttribution
        totalTokensUsed={draft.totalTokensUsed}
        estimatedCostUsd={draft.estimatedCostUsd}
      />
```

- [ ] **Step 5: Verify the forbidden string is gone + existing page test passes**

Run:
```
cd apps/web && grep -rn "non ha mai letto il testo del PDF originale" src ; echo "hits above (expect 0)"
pnpm test src/components/admin/mechanic-extractor/__tests__/MechanicAnalysisFooterAttribution.test.tsx
pnpm test src/__tests__/app/admin/knowledge-base/mechanic-extractor/review.test.tsx
```
Expected: **0 grep hits**; both tests PASS. NB the existing `review.test.tsx:85` asserts `/L'AI non ha mai letto…/` — that assertion is now stale and MUST be updated in this step to assert the new attribution text (`/riformulata in parole originali/i`) instead. Update it.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/admin/mechanic-extractor/MechanicAnalysisFooterAttribution.tsx apps/web/src/components/admin/mechanic-extractor/__tests__ "apps/web/src/app/admin/(dashboard)/knowledge-base/mechanic-extractor/review/page.tsx" apps/web/src/__tests__/app/admin/knowledge-base/mechanic-extractor/review.test.tsx
git commit -m "feat(mechanic-extractor): #526 swap Variant-C footer for ADR-051 attribution (AC-5)"
```

---

## Task 13: E2E — bulk-reject + citation-open flows (AC-9)

**Files:**
- Modify: `apps/web/e2e/admin-mechanic-extractor-validation/load-existing-analysis.spec.ts`

**Interfaces:**
- Consumes: all prior tasks (the running app).

- [ ] **Step 1: Add a claims fixture with two claims (one long-quote) + a bulk-reject route mock.** In `mockAnalysisRoutes`, add `bulkReject: 0` to `calls`, extend `buildClaimsResponse()` to return two claims (`CLAIM_ID` with a >20-word quote + a second short-quote claim), and register:
```ts
  await page.context().route(
    new RegExp(`/api/v1/admin/mechanic-analyses/${ANALYSIS_ID}/claims/bulk-reject$`),
    async (route: Route) => {
      if (route.request().method() === 'POST') {
        calls.bulkReject += 1;
        await route.fulfill({ status: 200, contentType: 'application/json',
          body: JSON.stringify({ rejectedCount: 1, skippedAlreadyRejectedCount: 0, claims: buildClaimsResponse() }) });
        return;
      }
      await route.continue();
    });
```
(Analysis status must be `InReview` for the bulk UI to be actionable — add an `InReview` status builder or parameterize `buildStatusDto()`.)

- [ ] **Step 2: Add the bulk-reject test** inside the `test.describe`:
```ts
  test('bulk-reject by quote length calls the endpoint with computed ids', async ({ page }) => {
    await setAdminSessionCookies(page);
    const calls = await mockAnalysisRoutes(page);
    await page.goto(`${ANALYSES_PATH}?analysisId=${ANALYSIS_ID}`);

    await expect(page.getByTestId('claims-section')).toBeVisible();
    await page.getByTestId('bulk-action-select').selectOption('reject-long-quote');
    await expect(page.getByTestId('bulk-action-count')).toContainText('1');
    await page.getByTestId('bulk-action-confirm').click();

    await expect.poll(() => calls.bulkReject, { timeout: 5000 }).toBeGreaterThanOrEqual(1);
  });
```

- [ ] **Step 3: Add the citation-open test:**
```ts
  test('clicking a citation opens the quote highlighter', async ({ page }) => {
    await setAdminSessionCookies(page);
    await mockAnalysisRoutes(page);
    await page.goto(`${ANALYSES_PATH}?analysisId=${ANALYSIS_ID}`);

    await page.getByTestId(`claim-citations-toggle-${CLAIM_ID}`).click();
    await page.getByTestId(/^claim-citation-open-/).first().click();
    await expect(page.getByRole('dialog')).toBeVisible();
  });
```

- [ ] **Step 4: Run the spec**

Run: `cd apps/web && pnpm test:e2e admin-mechanic-extractor-validation/load-existing-analysis.spec.ts`
Expected: PASS (requires `NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED=true` at server start). If the PDF highlighter cannot load a blob in E2E, assert only the dialog opening (as above) — do not assert on-page highlight in E2E.

- [ ] **Step 5: Commit**

```bash
git add apps/web/e2e/admin-mechanic-extractor-validation/load-existing-analysis.spec.ts
git commit -m "test(mechanic-extractor): #526 E2E bulk-reject + citation-open flows (AC-9)"
```

---

## Task 14: Full-suite verification + follow-up issues

- [ ] **Step 1: Backend suite**

Run: `cd apps/api/src/Api && dotnet test --filter "BoundedContext=SharedGameCatalog"`
Expected: green; no growth over the known-flaky baseline.

- [ ] **Step 2: Frontend quality gates**

Run: `cd apps/web && pnpm typecheck && pnpm lint && pnpm test src/components/admin/mechanic-extractor src/components/pdf src/lib/api`
Expected: green. Also run `pnpm lint:tokens` if any admin CSS changed (should be none beyond the allowed eslint-disable pattern).

- [ ] **Step 3: File the two follow-up issues** (referenced by #526's DoD):
  - **FU-1 — AC-1 real validation persistence**: persist per-claim guardrail outcomes + scores at pipeline time; flip `MechanicClaimValidations.DerivePass()` to persisted data; enable the *reject-all-failing-T2* predicate + T3 score display.
  - **FU-2 — AC-4 analysis finalize**: reconcile analysis-level finalize with #527/ADR-051 (card creation belongs to #527 Publish); resolve the `FinalizeMechanicAnalysisCommand` name collision. Amend #526 AC-4/DoD "becomes mechanic_card".

  Run (adjust body files as needed):
  ```bash
  gh issue create --repo meepleAi-app/meepleai-monorepo --title "[ME-M1.4 FU-1] Persist per-claim T1-T4 validation outcomes + scores" --label enhancement,area/backend,mechanic-extractor --body "Follow-up of #526. Replace the derived MechanicClaimValidations.DerivePass() with real per-claim guardrail outcomes captured at pipeline time (incl. T3 grounding score). Unblocks the reject-all-failing-T2 predicate + score display in the admin badges and #527 snapshot."
  gh issue create --repo meepleAi-app/meepleai-monorepo --title "[ME-M1.4 FU-2] Analysis finalize + #527 publish reconciliation" --label enhancement,area/backend,mechanic-extractor --body "Follow-up of #526. Reconcile the analysis-level finalize with #527/ADR-051: card creation belongs to #527 Publish, not #526. Amend #526 AC-4/DoD 'becomes mechanic_card'. Resolve the FinalizeMechanicAnalysisCommand name collision with the legacy Variant-C draft flow."
  ```

- [ ] **Step 4: Update #526** — comment linking the delivered ACs + the two follow-ups; note the AC-1 (derived, FU-1) and AC-4 (deferred, FU-2) scope amendments so the DoD reflects delivered scope. Push the branch and open the PR to `main-dev` (see execution handoff).

---

## Manual verification checklist (before PR)

- [ ] Admin can open a real analysis (`?analysisId=`), see 4 green T1–T4 badges per claim.
- [ ] Clicking a citation opens the PDF at the right page; the quote highlights, or the amber fallback banner shows if not matched. **Verify admin PDF authorization**: `api.pdf.getPdfDownloadUrl(pdfDocumentId)` must resolve for an admin against a shared-game rulebook — if it 403s, file a small follow-up for an admin PDF-fetch route (not a #526 blocker for the rest).
- [ ] Bulk "reject >20 words" shows the correct count and rejects only those claims.
- [ ] Approve-with-note stores + displays the note (green block).
- [ ] Review page footer shows the ADR-051 string; `grep "non ha mai letto"` → 0 hits.
