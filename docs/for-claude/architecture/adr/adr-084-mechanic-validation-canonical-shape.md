# ADR-084 — Mechanic Extractor validation canonical shape (#2786, folded into #2782)

**Status**: Accepted (2026-07-10)
**Context**: #2782 FU-1 introduces real per-claim guardrail validations. Two consumers exist: the
admin review DTO (a superset) and the #527 card snapshot (a lossy down-projection).
**Related**: ADR-051 (Mechanic Extractor IP policy, `mechanic_cards.content` JSONB contract) ·
#2786 (canonical shape decision) · #527 (card reader, not yet built)

## Context

FU-1 wired real per-claim guardrail validation (rules T1/T2/T3a/T3b/T4) into the Mechanic
Extractor pipeline. Two places in the codebase need to represent "the outcome of validating a
claim":

1. **Admin review** (`MechanicClaimValidation`, `Api.BoundedContexts.SharedGameCatalog.Domain.ValueObjects`)
   — the reviewer needs to see *why* a claim failed or wasn't checked, with a numeric grounding
   score where applicable, so `reject-all-failing-T2` and human triage are meaningful.
2. **Published card snapshot** (`MechanicCardValidationSnapshot`, embedded in
   `mechanic_cards.content` JSONB per ADR-051) — an immutable, publish-time snapshot that never
   dereferences the live claim graph; it exists to answer "was this claim accepted," not to
   reproduce the review audit trail.

Without a documented shape decision these two models drift independently (e.g. a card reader
built against review-shaped assumptions, or a reviewer feature built assuming the card carries
`notRun` information it never had). This ADR fixes the two shapes, their relationship, and the
supporting infrastructure decisions (JSON casing, indexing, schema versioning, and the Victory
correlation approximation) so both BE-core (review side) and BE-card (publish side) implement
against the same contract.

## Decision

### 1. Canonical shape = the review-side 5-rule, 3-state superset

```csharp
public sealed record MechanicClaimValidation(
    string Rule,       // one of {T1, T2, T3a, T3b, T4}
    string Outcome,    // one of {pass, fail, notRun}
    string? Message = null,
    double? Score = null);
```

- 5 rule families: `T1`, `T2`, `T3a` (citation-presence, binary pass/fail), `T3b` (grounding,
  cosine-scored), `T4`.
- `Score` is populated **only** for `T3b` (grounding cosine similarity); `null` for every other
  rule.
- `Outcome` is 3-state: `pass`, `fail`, `notRun` — `notRun` exists because not every rule applies
  to every claim/section combination, and a claim that was never evaluated by a rule is not the
  same fact as one that failed it.
- This is the **only** shape that carries the 3-state nuance and the score. It lives on
  `MechanicClaim.Validations` (`IReadOnlyList<MechanicClaimValidation>`) and is what the admin
  review DTO (`MechanicClaimValidationDto`) and `BulkApproveMechanicClaimsCommandHandler`'s
  fail-flag guard (#2782 D8) both read.

### 2. Card shape = a lossy down-projection, never projected up

```csharp
public sealed record MechanicCardValidationSnapshot
{
    [JsonPropertyName("rule")]
    public string Rule { get; init; }

    [JsonPropertyName("passed")]
    public bool Passed { get; init; }

    [JsonPropertyName("score")]
    public double? Score { get; init; }
}
```

Built in `MechanicCardContent.FromAnalysis` (`MechanicCardContent.cs`) via:

```csharp
Validations = c.Validations
    .Select(v => new MechanicCardValidationSnapshot
    {
        Rule = v.Rule,
        Passed = string.Equals(v.Outcome, MechanicClaimValidationOutcomes.Pass, StringComparison.Ordinal),
        Score = v.Score
    })
    .ToList()
```

- `Passed = (Outcome == "pass")`. Both `fail` **and** `notRun` collapse to `Passed: false`.
- The projection is **one-directional**: review → card. The card is a published snapshot of
  *accepted* claims (the analysis was already approved when the card was generated), not an
  audit trail — it is not meant to explain *why* a claim didn't pass, only whether it did. There
  is no path, and none is planned, to reconstruct `fail` vs. `notRun` from a card snapshot, nor
  to feed card data back into a `MechanicClaimValidation`.
- Consequence: a reviewer who wants to distinguish "guardrail actively rejected this" from
  "guardrail never ran on this" must use the review UI/DTO — the card intentionally cannot answer
  that question.

### 3. JSON key casing is frozen

- **Review side (persisted jsonb)**: the `mechanic_claims.validations` value converter serializes
  `MechanicClaimValidation` with `JsonSerializer` **default options**, i.e. the domain record's
  PascalCase property names — `{Rule, Outcome, Message, Score}`. Write and read use the same
  default options, so the round-trip is internally consistent (proven by the Testcontainers
  round-trip test). This on-disk casing is an internal storage detail — it never reaches the
  client, because the API returns the separate `MechanicClaimValidationDto` record, which the
  global API JSON policy serializes to camelCase (`{rule, outcome, message, score}`) for the FE
  Zod schema.
- **Card side**: `MechanicCardValidationSnapshot` uses explicit `[JsonPropertyName]` snake_case —
  `{rule, passed, score}` — matching every other field in `MechanicCardContent` (`schema_version`,
  `snapshot_at`, `source_analysis_id`, etc.), which is on-disk JSONB per ADR-051.
- Both casings are now **frozen**: FU-1 persists (`mechanic_claims.validations` jsonb) and
  publishes (`mechanic_cards.content` jsonb) these shapes, so changing either key set is a
  breaking schema change requiring a migration/version bump, not a casual rename.

### 4. No GIN index on `mechanic_claims.validations`

`mechanic_claims.validations` (jsonb) has no GIN index. Every current and planned consumer
(admin review list, bulk-approve fail-flag guard, card projection) filters over an
already-loaded, per-analysis claim list — the query boundary is always "claims of one
`MechanicAnalysis`," a small N fetched by primary/foreign key, never a cross-analysis jsonb
containment search. A GIN index would add write overhead with no read path to justify it. Revisit
if a future requirement needs cross-analysis validation querying (e.g. "find all claims across
every analysis that failed T3b").

### 5. `schema_version` bumped to 2, write-only until #528

`MechanicCardContent.CurrentSchemaVersion = 2` (bumped from 1 by #2782, since real validations are
now projected into every new card). It is **write-only**: no card reader exists yet (#528), so no
consumer branches on `schema_version` today. The bump exists so that when #528 ships, it can
distinguish pre-FU-1 cards (schema 1, `validations: []` or absent) from post-FU-1 cards (schema 2,
real `Passed`/`Score` per claim) without guessing from content shape alone.

### 6. Victory correlation approximation (accepted, not a bug)

Victory-section guardrail validations are computed once per section and broadcast to **every**
claim anchored under the section's shared `$.victory` JSON-pointer anchor, rather than being
walked independently per alternative claim within that section. This means sibling Victory claims
that share an anchor show identical validation outcomes even if a human would judge them
differently in isolation. This is a plan-accepted approximation (precision follow-up tracked
separately, not scheduled as part of FU-1) — documented here so it is not mistaken for a defect
during a future audit of validation results.

## Consequences

**Positive**
- The review UI can surface real `fail`/`notRun` distinctions plus the `T3b` grounding score;
  `reject-all-failing-T2` and the BulkApprove fail-flag guard (#2782 D8) become meaningful
  server-side gates instead of no-ops.
- The card format stays a stable, minimal, on-disk public contract (ADR-051) — adding richer
  review-side validation detail did not require widening the card's JSONB shape or breaking
  existing readers.
- `mechanic_claims.validations` stays index-free, keeping claim writes cheap; no premature
  optimization for a query pattern that doesn't exist yet.

**Negative / trade-offs**
- The card cannot distinguish `fail` from `notRun` — accepted, since the card's job is "was this
  claim accepted," not "reproduce the review audit trail." A future card-reader feature that
  wants that distinction must go back to the review-side data (which the card, by design, no
  longer references after snapshot time — ADR-051 AD-1).
- `schema_version = 2` provides no runtime behavior yet; it is inert until #528 lands, so its
  correctness is currently unverified by any consumer — a latent risk if the version-bump
  semantics are wrong and nobody notices until #528.
- Victory-claim validation results are approximate for sibling alternative claims sharing an
  anchor; a reviewer relying on per-claim precision within a Victory section should be aware
  outcomes are section-level, not claim-level, for that section only.

## Alternatives considered

- **Single shared shape for review and card** (reuse `MechanicClaimValidation` verbatim in the
  card JSONB). Rejected: it would leak review-only fields (`Message`, the 3-state `Outcome`) into
  a supposedly minimal public snapshot contract (ADR-051), and camelCase review casing conflicts
  with the card's established snake_case convention — either would force a casing/shape migration
  on every existing card row.
- **Card retains 3-state `Outcome` instead of collapsing to `Passed: bool`.** Rejected: the card
  has no reader today that would use the distinction (#528 not built), and the review side already
  owns that nuance — duplicating it into the card doubles the surface that must stay in sync for
  no current consumer benefit.
- **GIN index on `mechanic_claims.validations` added preemptively.** Rejected: YAGNI — no query
  pattern needs it; every consumer is already scoped to a single analysis's claim list.

## References
- #2782 (FU-1 epic) · #2786 (this decision) · #527 (card reader, not yet built) · #2782 D6
  (task brief that specified this ADR) · #2782 D8 (BulkApprove fail-flag guard, consumes the
  review-side shape).
- ADR-051 (Mechanic Extractor IP policy — establishes `mechanic_cards.content` as an immutable
  publish-time snapshot, never dereferencing the live claim graph).
- Code: `Domain/ValueObjects/MechanicClaimValidation.cs`, `Domain/ValueObjects/MechanicCardContent.cs`
  (`Api.BoundedContexts.SharedGameCatalog`).
