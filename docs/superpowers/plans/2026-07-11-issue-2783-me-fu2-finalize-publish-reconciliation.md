# #2783 ME-M1.4 FU-2 — Finalize / Publish reconciliation — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Reconcile the Mechanic Extractor finalize/publish vocabulary — rename the misnamed Variant-C `FinalizeMechanicAnalysisCommand`, promote two publish preconditions to explicit labeled handler guards, and correct the stale #526 AC-4 documentation.

**Architecture:** Three independent workstreams in the `SharedGameCatalog` bounded context. WS1 is a pure class rename (Variant-C draft flow). WS2 adds two defense-in-depth guards to `PublishMechanicCardCommandHandler` with the existing `ConflictReason=<reason>` log shape. WS3 is docs-only (ADR note + closed-issue comment + factory comment fix).

**Tech Stack:** .NET 9, MediatR (CQRS), xUnit + Moq (unit), FluentValidation.

## Global Constraints

- Backend test path is `apps/api/tests/Api.Tests` — run with explicit csproj: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "..."`. Kill testhost before running; never run two `dotnet test` in parallel.
- Exceptions: `ConflictException` (409), `NotFoundException` (404) — never `InvalidOperationException` (500) from a handler.
- The publish guard reason strings are lowercase-snake, matching existing ones: `not_published`, `already_published`, `race_active`, `stale_concurrency`, `previous_not_suppressed`. New: `no_claims`, `claims_not_approved`.
- The Variant-C `POST /api/v1/admin/mechanic-extractor/finalize` route and its OpenAPI `WithName("AdminFinalizeMechanicAnalysis")` operationId stay **unchanged** (no API break) — only the internal command type is renamed.
- Commit messages: `feat|fix|docs|refactor|test(mechanic-extractor): #2783 <desc>` ending with the Co-Authored-By trailer.

---

### Task 1: WS1 — Rename `FinalizeMechanicAnalysisCommand` → `FinalizeMechanicDraftCommand`

**Files:**
- Rename: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/FinalizeMechanicAnalysisCommand.cs` → `FinalizeMechanicDraftCommand.cs`
- Rename: `.../FinalizeMechanicAnalysisCommandHandler.cs` → `FinalizeMechanicDraftCommandHandler.cs`
- Rename: `.../FinalizeMechanicAnalysisCommandValidator.cs` → `FinalizeMechanicDraftCommandValidator.cs`
- Modify: `apps/api/src/Api/Routing/AdminMechanicExtractorEndpoints.cs` (the `new FinalizeMechanicAnalysisCommand(...)` call site, ~line 135)
- Test (create): `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/FinalizeMechanicDraftCommandHandlerTests.cs`

**Interfaces:**
- Produces: `internal record FinalizeMechanicDraftCommand(Guid DraftId, Guid UserId) : ICommand<RulebookAnalysisDto>` and `internal sealed class FinalizeMechanicDraftCommandHandler : ICommandHandler<FinalizeMechanicDraftCommand, RulebookAnalysisDto>`. Signature/behavior unchanged from the old names.

- [ ] **Step 1: Rename the three files and their type names**

Rename each file (git mv) and replace the type name inside. The behavior does not change — this is a mechanical rename.

`FinalizeMechanicDraftCommand.cs` — update the class name + XML doc:
```csharp
/// <summary>
/// Command to finalize a Variant-C <see cref="Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates.MechanicDraft"/>
/// into a copyright-compliant <c>RulebookAnalysis</c>. This is the terminal step of the manual/legacy
/// Variant-C draft flow (Save → AiAssist → Accept → Finalize); it operates on a <c>MechanicDraft</c> and
/// does NOT touch the AI-first <c>MechanicAnalysis</c> aggregate or create a <c>mechanic_card</c> (#2783).
/// </summary>
internal record FinalizeMechanicDraftCommand(
    Guid DraftId,
    Guid UserId)
    : ICommand<RulebookAnalysisDto>;
```

`FinalizeMechanicDraftCommandValidator.cs` — rename class + its `AbstractValidator<T>` type arg (keep the two rules verbatim):
```csharp
internal sealed class FinalizeMechanicDraftCommandValidator : AbstractValidator<FinalizeMechanicDraftCommand>
```

`FinalizeMechanicDraftCommandHandler.cs` — rename the class, its `ICommandHandler<...>` type arg, the `ILogger<...>` field/ctor-param type, and the `Handle(FinalizeMechanicDraftCommand request, ...)` parameter type. Update the class XML doc to say "finalizes a Variant-C MechanicDraft into a RulebookAnalysis". Body logic is unchanged.

- [ ] **Step 2: Update the endpoint call site**

In `AdminMechanicExtractorEndpoints.cs`, change the single construction (keep the route, `WithName`, request DTO, and 201 result untouched):
```csharp
var command = new FinalizeMechanicDraftCommand(
    request.DraftId,
    request.UserId);
```

- [ ] **Step 3: Build to verify the rename compiles**

Run: `dotnet build apps/api/src/Api/Api.csproj -c Debug --nologo -v q`
Expected: `Compilazione completata. Errori: 0`. (A missed reference to the old type name fails here.)

- [ ] **Step 4: Write the handler smoke test**

Create `FinalizeMechanicDraftCommandHandlerTests.cs`. Two cases prove the renamed handler is invocable and its guards fire. Mock `IMechanicDraftRepository`; the analysis repo / UoW are only reached on the happy path (not exercised here).

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Entities;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2783")]
public sealed class FinalizeMechanicDraftCommandHandlerTests
{
    private readonly Mock<IMechanicDraftRepository> _draftRepo = new();
    private readonly Mock<IRulebookAnalysisRepository> _analysisRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private FinalizeMechanicDraftCommandHandler CreateHandler() =>
        new(_draftRepo.Object, _analysisRepo.Object, _uow.Object,
            NullLogger<FinalizeMechanicDraftCommandHandler>.Instance);

    [Fact]
    public async Task Handle_DraftNotFound_ThrowsNotFound()
    {
        var draftId = Guid.NewGuid();
        _draftRepo.Setup(r => r.GetByIdAsync(draftId, It.IsAny<CancellationToken>()))
            .ReturnsAsync((MechanicDraft?)null);

        var act = () => CreateHandler().Handle(
            new FinalizeMechanicDraftCommand(draftId, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<NotFoundException>();
    }

    [Fact]
    public async Task Handle_AlreadyFinalized_ThrowsConflict()
    {
        var draft = MechanicDraft.Create(Guid.NewGuid(), Guid.NewGuid(), Guid.NewGuid());
        // Drive the draft to Activated so the idempotency guard trips.
        draft.AcceptDraft(MechanicSection.Summary, "s");
        draft.AcceptDraft(MechanicSection.Mechanics, "m");
        draft.MarkActivated();
        _draftRepo.Setup(r => r.GetByIdAsync(draft.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(draft);

        var act = () => CreateHandler().Handle(
            new FinalizeMechanicDraftCommand(draft.Id, Guid.NewGuid()), TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>()
            .WithMessage("*already been finalized*");
    }
}
```

> **Verify before writing:** confirm the `MechanicDraft.Create(...)` factory signature and `AcceptDraft(MechanicSection, string)` / `MarkActivated()` methods against `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicDraft.cs` (see `MechanicDraftTests.cs` for the exact construction) and adjust the fixture setup to match. The two assertions (404, 409) are the contract; the construction details follow the domain factory.

- [ ] **Step 5: Run the smoke test**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~FinalizeMechanicDraftCommandHandlerTests"`
Expected: PASS (2 tests).

- [ ] **Step 6: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/FinalizeMechanicDraft*.cs \
        apps/api/src/Api/Routing/AdminMechanicExtractorEndpoints.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/FinalizeMechanicDraftCommandHandlerTests.cs
git commit -m "refactor(mechanic-extractor): #2783 rename FinalizeMechanicAnalysisCommand -> FinalizeMechanicDraftCommand (WS1)"
```

---

### Task 2: WS2 — Publish precondition hardening (`no_claims` + `claims_not_approved` guards)

**Files:**
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/PublishMechanicCardCommandHandler.cs` (insert two guards after F2, ~line 82)
- Modify: `apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicCard.cs` (fix the now-accurate defense-in-depth comment, ~line 99-100)
- Test (create): `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/PublishMechanicCardCommandHandlerGuardsTests.cs`

**Interfaces:**
- Consumes: `PublishMechanicCardCommandHandler` ctor `(IMechanicAnalysisRepository, IMechanicCardRepository, ISharedGameRepository, IUnitOfWork, TimeProvider, ILogger<...>)`; `analysis.Claims` (populated by `GetByIdWithClaimsIgnoringFiltersAsync`); `MechanicClaimStatus.Approved`.
- Test builder: `MechanicAnalysis.Reconstitute(...)` + `MechanicClaim.Reconstitute(...)` + `MechanicCitation.Reconstitute(...)` (bypass aggregate invariants — the only way to construct an *invalid* Published analysis, since `Approve` forbids it). Mirror the `BuildAnalysis(status, claimCount)` helper in `CalculateMechanicAnalysisMetricsHandlerTests.cs:664-720`.

> **Note (adjudicated with the user):** a `Published` analysis produced through `MechanicAnalysis.Approve` (the only lifecycle path) already satisfies "≥1 claim ∧ all Approved" (`MechanicAnalysis.cs:443-455`), so these two handler guards are **defense-in-depth for a state unreachable via the normal path**. The tests deliberately construct that unreachable state via `Reconstitute` to exercise the guards. This is intentional per the #2783 scope decision.

- [ ] **Step 1: Write the two failing guard tests**

Create `PublishMechanicCardCommandHandlerGuardsTests.cs`. Both build a `Published` analysis in an invalid claim-state via `Reconstitute`, invoke the handler, assert a `ConflictException` with the **handler's** message (distinct from the factory's), and verify the card repo is never touched (guard fired before the factory).

```csharp
using Api.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;
using Api.BoundedContexts.SharedGameCatalog.Domain.Aggregates;
using Api.BoundedContexts.SharedGameCatalog.Domain.Enums;
using Api.BoundedContexts.SharedGameCatalog.Domain.Repositories;
using Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects;
using Api.Middleware.Exceptions;
using Api.SharedKernel.Infrastructure.Persistence;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging.Abstractions;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.SharedGameCatalog.Application.Commands.MechanicExtractor;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "SharedGameCatalog")]
[Trait("Issue", "2783")]
public sealed class PublishMechanicCardCommandHandlerGuardsTests
{
    private readonly Mock<IMechanicAnalysisRepository> _analysisRepo = new();
    private readonly Mock<IMechanicCardRepository> _cardRepo = new();
    private readonly Mock<ISharedGameRepository> _gameRepo = new();
    private readonly Mock<IUnitOfWork> _uow = new();

    private PublishMechanicCardCommandHandler CreateHandler() =>
        new(_analysisRepo.Object, _cardRepo.Object, _gameRepo.Object, _uow.Object,
            TimeProvider.System, NullLogger<PublishMechanicCardCommandHandler>.Instance);

    private void SetupAnalysis(MechanicAnalysis analysis) =>
        _analysisRepo.Setup(r => r.GetByIdWithClaimsIgnoringFiltersAsync(analysis.Id, It.IsAny<CancellationToken>()))
            .ReturnsAsync(analysis);

    [Fact]
    public async Task Handle_PublishedAnalysisWithNoClaims_ThrowsConflict_BeforeFactory()
    {
        var analysis = BuildPublishedAnalysis(claimStatuses: Array.Empty<MechanicClaimStatus>());
        SetupAnalysis(analysis);

        var act = () => CreateHandler().Handle(
            new PublishMechanicCardCommand(analysis.Id, Guid.NewGuid(), "A valid title"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*no claims to publish*");
        _cardRepo.Verify(r => r.AddAsync(It.IsAny<MechanicCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PublishedAnalysisWithUnapprovedClaim_ThrowsConflict_BeforeFactory()
    {
        var analysis = BuildPublishedAnalysis(claimStatuses: new[] { MechanicClaimStatus.Pending });
        SetupAnalysis(analysis);

        var act = () => CreateHandler().Handle(
            new PublishMechanicCardCommand(analysis.Id, Guid.NewGuid(), "A valid title"),
            TestContext.Current.CancellationToken);

        await act.Should().ThrowAsync<ConflictException>().WithMessage("*must be Approved*");
        _cardRepo.Verify(r => r.AddAsync(It.IsAny<MechanicCard>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    // Builds a Published analysis with claims in the given statuses, bypassing Approve's invariants.
    private static MechanicAnalysis BuildPublishedAnalysis(MechanicClaimStatus[] claimStatuses)
    {
        var analysisId = Guid.NewGuid();
        var claims = new List<MechanicClaim>();
        for (var i = 0; i < claimStatuses.Length; i++)
        {
            var claimId = Guid.NewGuid();
            var citation = MechanicCitation.Reconstitute(
                id: Guid.NewGuid(), claimId: claimId, pdfPage: i + 1,
                quote: $"Quote {i}", chunkId: null, displayOrder: 0);
            claims.Add(MechanicClaim.Reconstitute(
                id: claimId, analysisId: analysisId, section: MechanicSection.Summary,
                text: $"Claim {i}", displayOrder: i, status: claimStatuses[i],
                reviewedBy: Guid.NewGuid(), reviewedAt: DateTime.UtcNow, rejectionNote: null,
                citations: new[] { citation }));
        }

        return MechanicAnalysis.Reconstitute(
            id: analysisId, sharedGameId: Guid.NewGuid(), pdfDocumentId: Guid.NewGuid(),
            promptVersion: "v1", status: MechanicAnalysisStatus.Published,
            createdBy: Guid.NewGuid(), createdAt: DateTime.UtcNow,
            reviewedBy: Guid.NewGuid(), reviewedAt: DateTime.UtcNow, rejectionReason: null,
            totalTokensUsed: 0, estimatedCostUsd: 0m, modelUsed: "gpt-4", provider: "openai",
            costCapUsd: 1m, costCapOverrideAt: null, costCapOverrideBy: null, costCapOverrideReason: null,
            isSuppressed: false, suppressedAt: null, suppressedBy: null, suppressionReason: null,
            suppressionRequestedAt: null, suppressionRequestSource: null, claims: claims);
    }
}
```

> **Verify before writing:** confirm the `PublishMechanicCardCommand` constructor arity/param names (`AnalysisId`, `PublisherId`, `Title`, optional `PreviousCardId`) against `PublishMechanicCardCommand.cs`, and the `MechanicClaim.Reconstitute` / `MechanicCitation.Reconstitute` signatures against `CalculateMechanicAnalysisMetricsHandlerTests.cs:673-690`. Adjust the command construction to match the real record.

- [ ] **Step 2: Run the tests to verify they FAIL**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PublishMechanicCardCommandHandlerGuardsTests"`
Expected: FAIL — currently the handler has no such guards, so the flow reaches the factory, which throws its OWN `InvalidOperationException` ("Cannot publish MechanicAnalysis ...: it has no claims." / "... not all claims are Approved.") → wrapped as `ConflictException`. The message assertions (`*no claims to publish*` / `*must be Approved*`) DON'T match the factory wording, so the tests fail (wrong message), proving the labeled handler guard is missing.

- [ ] **Step 3: Add the two handler guards**

In `PublishMechanicCardCommandHandler.Handle`, immediately after the F2 block (the `if (analysis.PublishedCardId is not null)` throw, ~line 81) and before `var game = await _sharedGameRepository...`, insert:

```csharp
        // no claims to publish (defense-in-depth; Approve already guarantees ≥1 claim before Published).
        if (analysis.Claims.Count == 0)
        {
            _logger.LogWarning(
                "Publish rejected for analysis {AnalysisId}: no claims (ConflictReason={ConflictReason}).",
                analysis.Id, "no_claims");
            throw new ConflictException("Analysis has no claims to publish.");
        }

        // not every claim is Approved (defense-in-depth; Approve already enforces this before Published).
        if (analysis.Claims.Any(c => c.Status != MechanicClaimStatus.Approved))
        {
            _logger.LogWarning(
                "Publish rejected for analysis {AnalysisId}: not all claims Approved (ConflictReason={ConflictReason}).",
                analysis.Id, "claims_not_approved");
            throw new ConflictException("All claims must be Approved before publishing.");
        }
```

Ensure `System.Linq` is available (the file already uses LINQ via EF; add `using System.Linq;` if the build flags `Any`).

- [ ] **Step 4: Run the guard tests to verify they PASS**

Run: `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PublishMechanicCardCommandHandlerGuardsTests"`
Expected: PASS (2 tests). The `_cardRepo.AddAsync` `Times.Never` verifications confirm the guard fires before the factory.

- [ ] **Step 5: Make the factory comment accurate**

In `MechanicCard.cs` (~line 99-100), the defense-in-depth comment reads *"the handler already guards these, but the factory re-checks..."*. This is now literally true for all four checks (Status, PublishedCardId, no-claims, not-approved). Update the comment to name #2783:

```csharp
        // Defense in depth: the PublishMechanicCardCommandHandler already guards all four of these
        // (status, published-card, no-claims, not-all-approved — #2783), but the factory re-checks so
        // the aggregate can never be constructed from a non-publishable analysis (e.g. a direct caller).
```

- [ ] **Step 6: Run the existing publish tests to confirm no regression**

Run (Docker up for Testcontainers): `dotnet test apps/api/tests/Api.Tests/Api.Tests.csproj --filter "FullyQualifiedName~PublishMechanicCardEndpointIntegrationTests"`
Expected: PASS — the happy-path publish (≥1 claim, all Approved) is unaffected; the new guards only add labeled short-circuits for invalid states.

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/PublishMechanicCardCommandHandler.cs \
        apps/api/src/Api/BoundedContexts/SharedGameCatalog/Domain/Aggregates/MechanicCard.cs \
        apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Application/Commands/MechanicExtractor/PublishMechanicCardCommandHandlerGuardsTests.cs
git commit -m "feat(mechanic-extractor): #2783 explicit labeled publish guards no_claims/claims_not_approved (WS2)"
```

---

### Task 3: WS3 — Documentation reconciliation

**Files:**
- Modify: `docs/for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md` (§7 review-gate clarification)
- No in-repo #526 spec/plan edits (already correct — verified 2026-07-11).
- External: a clarifying comment on GitHub issue #526 (do NOT edit its closed body).

**Interfaces:** none (docs).

- [ ] **Step 1: Add the ADR-051 §7 lifecycle-handoff clarification**

Open `adr-051-mechanic-extractor-ip-policy.md`, find §7 (the human-review-gate section — grep `§ 7` / `review gate` / `revisione`). Append a short clarifying paragraph (match the doc's language — the ADR is written in Italian/mixed; keep the surrounding style):

```markdown
> **Handoff lifecycle ↔ pubblicazione (#2783):** `Approve` porta l'analisi allo stato *lifecycle* `Published`
> ma **non** crea la card. La `mechanic_card` user-facing è coniata solo dall'atto esplicito e distinto
> `PublishMechanicCardCommand` (#527), che richiede `Status == Published` e poi chiama
> `analysis.MarkPublished(cardId)`. Non esiste una transizione `MechanicAnalysis.Finalize`: `Finalize*` è
> esclusivamente il verbo del flusso Variant-C draft (`FinalizeMechanicDraftCommand` → `RulebookAnalysis`).
> Il gate di revisione (questa §7) e l'atto di pubblicazione sono due passi separati.
```

- [ ] **Step 2: Post the clarifying comment on the closed #526 issue**

```bash
gh issue comment 526 --repo meepleAi-app/meepleai-monorepo --body "$(cat <<'EOF'
### Rettifica AC-4 (via #2783 FU-2)

L'AC-4 originale di questa issue descriveva `NeedsReview → Approved/Rejected` e "Approve analysis (becomes mechanic_card)". Nel modello effettivamente implementato:

- Gli stati sono `Draft → InReview → Published/Rejected` — **non** esistono `NeedsReview`/`Approved`.
- `Approve` raggiunge lo stato *lifecycle* `Published` ma **non** crea la card.
- La `mechanic_card` user-facing è creata dall'atto esplicito e distinto **`PublishMechanicCardCommand` (#527)** (ADR-051 §7), che poi collega `analysis.MarkPublished(cardId)`.
- Non serviva un nuovo `FinalizeMechanicAnalysisCommand` per la pipeline AI-first: quel nome apparteneva già al flusso Variant-C draft ed è stato rinominato `FinalizeMechanicDraftCommand` (#2783 WS1). La pipeline AI-first usa `Approve` (lifecycle) + `Publish` (card).

Riconciliazione completa: `docs/superpowers/specs/2026-07-11-issue-2783-me-fu2-finalize-publish-reconciliation-design.md`.
EOF
)"
```

- [ ] **Step 3: Commit the ADR change**

```bash
git add docs/for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md
git commit -m "docs(mechanic-extractor): #2783 ADR-051 §7 lifecycle-handoff clarification (WS3)"
```

---

## Self-Review — spec section → task mapping

| Spec section | Task | Notes |
|---|---|---|
| WS1 rename (§3) | Task 1 | 3 files + endpoint + smoke test; route/operationId unchanged |
| WS1 no-coverage → smoke test (§3, §5) | Task 1 Step 4 | 404 + already-finalized 409 |
| WS2 two labeled guards (§3, §4 reason strings) | Task 2 Steps 3 | `no_claims`, `claims_not_approved` after F2 |
| WS2 factory comment accuracy (§3) | Task 2 Step 5 | comment now true for all four checks |
| WS2 tests incl. unreachable-state construction (§5, §6 risk) | Task 2 Steps 1-2, 4, 6 | `Reconstitute` builder; `Times.Never` on card repo; regression on integration test |
| WS3 in-repo already correct → no edit (§3) | Task 3 | verified; only ADR note + #526 comment |
| WS3 handoff canonical doc (§4) | committed in spec | the spec §4 IS the canonical doc; ADR-051 §7 links the concept |
| WS3 ADR-051 §7 note (§3) | Task 3 Step 1 | |
| WS3 #526 comment, no body edit (§3) | Task 3 Step 2 | |

**Placeholder scan:** the two "Verify before writing" notes point the implementer to confirm domain-factory signatures (`MechanicDraft.Create`, `MechanicClaim.Reconstitute`, `PublishMechanicCardCommand` ctor) against the real source before writing the test fixtures — these are construction details the implementer reads once, not logic gaps. All guard code, rename mapping, reason strings, and commit commands are complete and exact.

**Type consistency:** `FinalizeMechanicDraftCommand`/`...Handler`/`...Validator` used consistently across Task 1 and the commit; guard reason strings `no_claims`/`claims_not_approved` match between spec §4, Task 2 Step 3 (impl) and the test message assertions (`*no claims to publish*` / `*must be Approved*` are the exception messages, distinct from the reason-string log tags); `MechanicAnalysis.Reconstitute` param names match the copied `BuildAnalysis` helper.
```
