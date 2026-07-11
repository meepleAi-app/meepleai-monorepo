# #2783 ME-M1.4 FU-2 — Finalize / Publish reconciliation — Design

**Issue**: [#2783](https://github.com/meepleAi-app/meepleai-monorepo/issues/2783) (follow-up of #526)
**Parent ADR**: [ADR-051 mechanic-extractor IP policy](../../for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md)
**Date**: 2026-07-11

## 1. Problem

The Mechanic Extractor has **two parallel authoring paths**, and their "commit" vocabulary
overlaps in a way that made #526's AC-4 wording factually wrong and left one command
misleadingly named:

1. **Variant-C manual path** (`MechanicDraft` → `RulebookAnalysis`): a human writes per-section
   notes (Save), optionally gets AI assistance on *their own notes only* (AiAssist), keeps chosen
   AI text per section (Accept), and finally converts the draft into a durable, copyright-compliant
   `RulebookAnalysis` via **`FinalizeMechanicAnalysisCommand`** (which calls `RulebookAnalysis.CreateManual`
   and flips the draft to `MechanicDraftStatus.Activated`). This command touches a `MechanicDraft`,
   NOT the AI-first `MechanicAnalysis` aggregate, and never creates a `mechanic_card`.

2. **AI-first pipeline** (`MechanicAnalysis` aggregate, ADR-051): Draft → InReview (SubmitForReview)
   → Published (Approve, requires every claim `Approved`) / Rejected. `Approve` reaches the
   **Published lifecycle state** but does NOT create a card. **`PublishMechanicCardCommand` (#527)**
   is the explicit admin act that snapshots an approved analysis into a `mechanic_card` row and links
   it back via `analysis.MarkPublished(cardId)`.

Three concrete defects follow (the three #2783 tasks):

- **T1 (docs)** — the CLOSED #526 issue body's AC-4 asserts *"Approve analysis (becomes mechanic_card)"*
  and a `NeedsReview → Approved/Rejected` transition, both false: no `Approved`/`NeedsReview` state exists
  (the model is `InReview → Published/Rejected`), and card creation belongs to #527's explicit Publish
  (ADR-051 §7). (The in-repo #526 spec/plan already flag this — only the GitHub issue body is stale.)
- **T2 (naming)** — `FinalizeMechanicAnalysisCommand` reads as "finalize a `MechanicAnalysis`" but
  actually finalizes a `MechanicDraft` into a `RulebookAnalysis`. Misleading; collides conceptually
  with the AI-first Approve/Publish vocabulary.
- **T3 (hardening)** — the analysis→card handoff already works (Approve → Published lifecycle → Publish),
  but two publish preconditions (`no claims` / `not all claims Approved`) are enforced **only** inside the
  `MechanicCard.PublishFromAnalysis` factory. They surface as generic `409`s via a catch-all, without a
  labeled `ConflictReason` — inconsistent with the handler's F1–F5/F9 telemetry. The factory even
  comments *"the handler already guards these"*, which is currently untrue.

## 2. Scope

**In scope**: one command rename (Variant-C), two explicit publish-handler guards with labeled
conflict reasons, and documentation reconciliation. **No behavioral change** to the Variant-C finalize
flow or to the analysis lifecycle state machine. No new `MechanicAnalysis.Finalize` transition is
introduced — the reconciliation confirms none is needed.

**Out of scope**: retiring the Variant-C path (it remains wired); the `#528` card reader; any change
to the AI-first Approve/Publish behavior beyond the two additive guards.

## 3. Design

### WS1 — Rename `FinalizeMechanicAnalysisCommand` → `FinalizeMechanicDraftCommand`

Pure rename (chosen for minimal churn: keeps the familiar `Finalize` verb, corrects the misleading
noun `Analysis` → `Draft`). Rename the class and its file in all three places, update the single
call site, and clarify the XML docs.

- `.../MechanicExtractor/FinalizeMechanicAnalysisCommand.cs` → `FinalizeMechanicDraftCommand.cs`
- `.../FinalizeMechanicAnalysisCommandHandler.cs` → `FinalizeMechanicDraftCommandHandler.cs`
- `.../FinalizeMechanicAnalysisCommandValidator.cs` → `FinalizeMechanicDraftCommandValidator.cs`
- `Routing/AdminMechanicExtractorEndpoints.cs` — update the `POST /finalize` reference to the new type.

The **route stays `/api/v1/admin/mechanic-extractor/finalize`** (no API break; it belongs to the
Variant-C `mechanic-extractor` group). The command still returns `RulebookAnalysisDto` and still takes
`(DraftId, UserId)` — signature unchanged. XML doc updated to state it finalizes a `MechanicDraft` into
a `RulebookAnalysis` (Variant-C copyright-compliant path), explicitly not the AI-first `MechanicAnalysis`.

**Coverage**: the command currently has **no tests**. Add a minimal `FinalizeMechanicDraftCommandHandler`
handler test — happy path (draft with accepted Summary + Mechanics → 201-equivalent `RulebookAnalysisDto`,
draft marked `Activated`) and the already-finalized guard (`Status == Activated` → `ConflictException`).

### WS2 — Publish precondition hardening

In `PublishMechanicCardCommandHandler.Handle`, **after F2 (already-published) and before the game
lookup**, add two explicit guards mirroring the factory checks, each with the existing log shape
(`ConflictReason=<reason>`) and a `ConflictException`:

```csharp
// no claims to publish.
if (analysis.Claims.Count == 0)
{
    _logger.LogWarning(
        "Publish rejected for analysis {AnalysisId}: no claims (ConflictReason={ConflictReason}).",
        analysis.Id, "no_claims");
    throw new ConflictException("Analysis has no claims to publish.");
}

// not every claim is Approved.
if (analysis.Claims.Any(c => c.Status != MechanicClaimStatus.Approved))
{
    _logger.LogWarning(
        "Publish rejected for analysis {AnalysisId}: not all claims Approved (ConflictReason={ConflictReason}).",
        analysis.Id, "claims_not_approved");
    throw new ConflictException("All claims must be Approved before publishing.");
}
```

Reason strings are lowercase-snake, consistent with `not_published` / `already_published` /
`race_active` / `stale_concurrency` / `previous_not_suppressed`. The `MechanicCard.PublishFromAnalysis`
factory checks are **kept** as defense-in-depth; update the factory comment so *"the handler already
guards these"* becomes accurate (it now does). The handler loads the analysis with its claims via the
existing `GetByIdWithClaimsIgnoringFiltersAsync`, so `analysis.Claims` is populated.

**Guard order rationale**: place after F1 (`not_published`) and F2 (`already_published`) — a
non-published or already-published analysis is rejected first; only then do we assert the claim graph is
publishable. This keeps the most-specific lifecycle errors ahead of content errors.

### WS3 — Documentation reconciliation

**Where the wrong wording actually lives** (verified 2026-07-11): the in-repo #526 spec/plan are
**already correct** — `...526...-design.md:28,50,59,113` and `...526...-core.md:1454,1459` already state
that `mechanic_card` is created by #527's explicit Publish and flag the `FinalizeMechanicAnalysisCommand`
collision as an FU-2 task. **No in-repo amendment is required.** The inaccurate DoD is in the **CLOSED
GitHub issue #526 body, AC-4 section** (lines ~40/90–100): *"Approve analysis (becomes mechanic_card)"*,
the non-existent `NeedsReview → Approved/Rejected` states, and a *"nuovo `FinalizeMechanicAnalysisCommand`"*
that collided with the existing Variant-C command.

- **#526 issue** — #526 is CLOSED; do **not** edit its historical body. Add a **clarifying comment** on
  #526 that corrects AC-4: card creation is #527's explicit Publish (ADR-051 §7); the real states are
  `InReview → Published/Rejected` (no `NeedsReview`/`Approved`); the AI-first pipeline uses `Approve`
  (lifecycle) + `Publish` (card), and `FinalizeMechanicDraftCommand` is the Variant-C draft verb — no new
  AI-first "finalize" command was needed. Link to this design doc.
- **Document the handoff** — a "Lifecycle handoff" section in this design doc (the canonical statement):
  `SubmitForReview` (Draft→InReview) → per-claim review → `Approve` (InReview→Published *lifecycle*,
  requires all claims Approved) → explicit `PublishMechanicCardCommand` (#527) creates the card +
  `analysis.MarkPublished(cardId)`. There is **no** `MechanicAnalysis.Finalize` transition and none is
  needed; `Finalize*` is exclusively the Variant-C draft verb (now `FinalizeMechanicDraftCommand`).
- **ADR-051 §7 note** — add a short clarifying sentence to ADR-051 §7 (the human-review-gate section)
  pointing out that Approve reaches Published lifecycle while the card is minted only by the explicit
  #527 Publish — so the review gate and the publication act are two distinct steps.
- **#526 issue** — #526 is CLOSED; do **not** edit its historical body. Add a clarifying comment linking
  to this reconciliation and the amended in-repo wording.

## 4. Lifecycle handoff (canonical)

```
AI-first pipeline (MechanicAnalysis aggregate):
  Draft ──SubmitForReview──▶ InReview ──(per-claim Approve/Reject)──▶
    Approve (all claims Approved) ──▶ Published  [LIFECYCLE state only — no card]
                                          │
                                          ▼  explicit admin act (#527)
                              PublishMechanicCardCommand
                                 ├─ MechanicCard.PublishFromAnalysis(...)  → mechanic_cards row
                                 └─ analysis.MarkPublished(cardId)         → link back

Variant-C manual path (MechanicDraft aggregate) — SEPARATE, unchanged:
  Save ▶ AiAssist ▶ Accept(per section) ▶ FinalizeMechanicDraftCommand → RulebookAnalysis (Activated)
```

`Finalize*` belongs to the Variant-C draft path only. The AI-first pipeline uses `Approve` (lifecycle)
+ `Publish` (card). No third "finalize analysis" verb exists.

## 5. Testing

- **WS1**: `FinalizeMechanicDraftCommandHandler` handler test — happy path (accepted Summary+Mechanics →
  `RulebookAnalysis` created & active, draft → `Activated`) + already-finalized → `ConflictException`.
- **WS2**: `PublishMechanicCardCommandHandler` tests —
  (a) analysis Published but **zero claims** → `ConflictException`, reason `no_claims`, factory never reached;
  (b) analysis Published with a **non-Approved claim** → `ConflictException`, reason `claims_not_approved`;
  (c) regression: a valid publish (≥1 claim, all Approved) still succeeds (existing happy-path test stays green).
- **WS3**: docs only — no test.

All existing `PublishMechanicCardCommandHandler` / publish integration tests must stay green.

## 6. Risks

- The rename touches a route-wired command; the endpoint reference must be updated in the same change or
  the build breaks (caught by compile). Low risk — 4 files, mechanical.
- The new publish guards are **additive and stricter-labeled but not stricter in outcome** (the factory
  already rejected these cases, just with a generic 409). No previously-succeeding publish becomes a
  failure; only the 409's telemetry/label improves. Existing tests that assert a 409 on these cases (if
  any) keep passing; new tests assert the labeled reason.
