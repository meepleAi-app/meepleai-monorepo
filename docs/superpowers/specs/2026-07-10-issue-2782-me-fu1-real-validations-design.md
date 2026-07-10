# #2782 (ME-M1.4 FU-1) — Real per-claim validations + scores — Design (v2, hardened)

**Status:** design v2 — hardened by adversarial review (3 lenses, 6 blockers + 7 majors resolved). Pending user approval.
**Issue:** [#2782](https://github.com/meepleAi-app/meepleai-monorepo/issues/2782) (folds ADR [#2786](https://github.com/meepleAi-app/meepleai-monorepo/issues/2786))
**Branch:** `feature/issue-2782-me-fu1-real-validations` (parent `main-dev`)
**Date:** 2026-07-10

## Goal

Replace the DERIVED all-`pass` `MechanicClaimValidations.DerivePass()` (#526) with REAL **per-claim** guardrail outcomes + scores captured at pipeline time, so the admin review UI surfaces genuine `fail`/`notRun` states, a `reject-all-failing-T2` bulk predicate becomes meaningful, and #527's card snapshot can populate `validations[]` (currently hard-coded empty).

## Context (verified against code, 2026-07-10)

- **#526**: review DTO `MechanicClaimValidationDto {Rule, Outcome, Message}` (+ this design adds `Score`) on `MechanicClaimDto.Validations`, populated by `DerivePass()` (4 hard-coded all-`pass`). `ValidationBadges` (FE) renders 3 states; `Families = {T1,T2,T3,T4}`.
- **#527**: `mechanic_cards.content` JSONB. `MechanicCardContent.FromAnalysis` hard-codes `Validations = Array.Empty<>()`; card shape `{rule, passed:bool, score:double?}`. `SchemaVersion` is written (=1) but **never read** (no card deserializer/GET exists) — it does NOT currently "guard" anything.
- **The guardrail chain is FIVE guardrails, non-monotonic Order** (verified `Guardrails/*.cs`): **T1** QuoteCap (Order 10), **T3a** CitationPresence (Order 15, rule `T3_citation_required`), **T4** PageSubstring (Order 20), **T2** RejectionSampling (Order 30, `T2_long_verbatim`), **T3b** Grounding (Order 40, `T3_grounding` / `T3_grounding_unavailable`). `well_formed` (malformed JSON) is a PRE-guardrail check in `ValidateSectionAsync` sharing the same fail/retry path. `MechanicOutputValidator.ValidateAsync` is **fail-fast** — returns at the first guardrail with any violation, discarding the rest.
- **Violations carry a `Path`** (e.g. `$.mechanics[2].citations[1].quote`, `MechanicValidationViolation{Rule,Message,Path}`) identifying the offending object — but fail-fast + the parser discard it. The parser (`MechanicOutputParser.Parse`) mints N claims per section (array items), reorders (Phases by `order`), splits (Victory primary+alternatives), and DROPS items whose citations all fail.
- **Pipeline**: first section to fail-after-retries aborts the whole run (`RunAsync` → `BuildAbortResult`); failing output discarded. Persisted claims all-`pass` by construction. Nothing gates approve/publish on guardrail outcome (`MechanicClaim.Approve`/`MechanicAnalysis.Approve`/`MechanicCard.PublishFromAnalysis` only check `Status == Approved`) — safe today ONLY because of the all-pass invariant that this feature removes.
- **Persistence**: domain `MechanicClaim` ≠ infra `MechanicClaimEntity`; `MechanicAnalysisRepository` has manual `MapClaimToDomain` (read) + `MapClaimToEntity` (write). Section telemetry `MechanicAnalysisSectionRunEntity.Status ∈ {0,1,2}` with DB CHECK constraints (`status BETWEEN 0 AND 2`, `status<>1 OR error_message IS NOT NULL`).

## Locked decisions

**From brainstorming:** (1) Scope = Full (surface real fail/notRun + scores). (2) Pipeline = run-all-retain (guardrails advisory, human reviewer gates; hard aborts remain for LLM-fail/cost-cap/crash). (3) Canonical shape = superset review-side, project down to card.

**From adversarial review (2026-07-10, user-defaulted recommendations):**
4. **Correlation = Path→claim (real per-claim).** Collect ALL violations per section with their `Path`; the parser stamps a stable JSONPath anchor on each minted claim; match `violation.Path` → claim anchor. NO section-level broadcast.
5. **Safety gate = soft + grounding-outage fail-closed.** Approve-time warning when a claim carries any `fail`; `BulkApprove`'s implicit approve-all-pending SKIPS fail-flagged claims; the **grounding `unavailable` (embedding outage) case stays HARD fail-closed** (section aborts) even in advisory mode. Other fails are advisory (reviewer decides).
6. **Badge taxonomy = 5 explicit.** Rules `{T1, T2, T3a, T3b, T4}` (T3a citation-presence binary; T3b grounding scored). `Families` (BE DTO) + FE badge rendering widen to 5.
7. **Delivery = BE-core atomic.** The pipeline behavior change (run-all) + persistence + DTO flip ship together (no window where run-all claims render fake all-pass).

## Design

### D1 — Validator: collect-all per-guardrail outcomes (+ Path, + score)
`MechanicOutputValidator.ValidateAsync` keeps **fail-fast for the retry trigger** (any violation → retry) but, on the **final** evaluation of a section (last attempt), performs a **collect-all** pass: run every guardrail, capture per-guardrail `{rule ∈ T1|T2|T3a|T3b|T4, outcome, message?, path?, score?, violations[]}`. `notRun` is only for guardrails genuinely not executed (e.g. T3b grounding skipped because T3a citation-presence already made it moot, or an outage short-circuit). `MechanicValidationResult` gains `IReadOnlyList<MechanicRuleOutcome> RuleOutcomes` (with per-violation `Path`s). The retry loop is unchanged in trigger; only the FINAL attempt returns full outcomes.

### D2 — Guardrail score (T3b grounding only)
`IMechanicGuardrail` exposes an optional numeric `score` for its rule even on pass. Only **T3b Grounding** populates it (its internal similarity). T1/T2/T3a/T4 leave `score = null`.

### D3 — Pipeline run-all-retain (+ well_formed + grounding-outage carve-outs)
`RunAsync` no longer aborts on `AbortedValidation`:
- Each section runs its retry budget; on final validation failure the section's **last well-formed output** is retained in `SectionOutputs` + its `RuleOutcomes` in a new `SectionOutcomes` map on `MechanicPipelineResult`.
- **`well_formed` fail (malformed JSON, unparseable)** → distinct branch: NO output retained, NO outcomes, section absent (as today). Not conflated with guardrail-fail.
- **Grounding `unavailable` (embedding outage)** → **hard abort** (fail-closed IP protection) even under advisory mode — this is an infra outage, not a real low-grounding signal.
- Hard aborts unchanged: LLM-fail, cost-cap.
- Pipeline `Outcome = Succeeded` when only guardrail (not outage/LLM/cost) fails occurred.

### D4 — Correlation: violation Path → specific claim
The parser stamps each minted `MechanicClaim` with a stable **source anchor** (its originating JSONPath, e.g. `$.mechanics[2]`). The executor matches each `RuleOutcome`/violation `Path` to the claim whose anchor is a prefix of the violation path, attaching the outcome to **that claim only**. Claims with no matching violation for a rule that ran → `pass` for that rule. This delivers true per-claim precision and makes `reject-all-failing-T2` correct.

### D5 — Persistence (jsonb column, all mapper points)
- New nullable `mechanic_claims.validations jsonb` (+ EF migration, `HasColumnName("validations")`, jsonb value converter **WITH a value comparer** so EF change-tracking detects list mutations — else UPDATEs silently no-op).
- Domain `MechanicClaim` carries `IReadOnlyList<MechanicClaimValidation> {Rule, Outcome, Message?, Score?}`.
- **Four mapper edit points** (the #526 trap, doubled): `MechanicClaimEntity` property, `MechanicClaimEntityConfiguration` mapping, `MapClaimToEntity` (write), `MapClaimToDomain` (read).
- `DerivePass()` flip touches **all 5 DTO construction sites** (`GetMechanicAnalysisClaimsQueryHandler`, `Approve`/`Reject`/`BulkApprove`/`BulkReject` handlers) — 4 project from the in-memory aggregate, so validations MUST live on the domain `MechanicClaim` + be read-mapped. Rename `DerivePass()` → `DeriveLegacyAllPassFallback()`, used ONLY for `validations IS NULL` (pre-FU-1 claims); rewrite its doc comment.

### D6 — Card down-projection (#527) + schema_version + ADR #2786
`MechanicCardContent.FromAnalysis` projects each claim's outcomes **down** to `{rule, passed = (outcome=="pass"), score}` (T3a+T3b both present card-side too, or collapsed per the card's needs — card keeps 5 or folds to the card's existing shape). Bump `CurrentSchemaVersion` to **2** + comment that it is write-only until a card reader (#528) exists. ADR #2786 records: canonical = review-side 5-rule superset; card = lossy down-projection; JSON casing frozen; **no GIN index** on the jsonb column (all consumers filter client-side per-analysis).

### D7 — Frontend
- `ValidationBadges` + `Families` widen to **5** rules (T1, T2, T3a, T3b, T4); real data (fail/notRun/score).
- **`reject-all-failing-T2`** bulk predicate = `claim.validations` contains `rule=="T2" && outcome=="fail"` (NOT the T1 quote-length heuristic that `reject-long-quote` uses — that mislabel is corrected here).
- **Approve-time warning** in `ApproveClaimDialog` when the claim carries any `fail`.
- Optional T3b score display. A Zod **contract test** asserts `MechanicClaimValidationDtoSchema` includes `score` (Zod strips unknown keys silently → without a positive test the field can drift).

### D8 — Safety gate + lifecycle
- Approve: soft warning (FE) on fail-flagged claims; domain unchanged (human override allowed, recorded).
- `BulkApproveMechanicClaimsCommandHandler` implicit approve-all-pending **excludes** fail-flagged claims (server-side guard) so an admin can't rubber-stamp hallucinations in one click.
- Lifecycle: guardrail-failing sections now → `Succeeded`/InReview with flagged claims (was Rejected/PartiallyExtracted). `AutoRejectionReasons.ValidationFailedBeyondRetry` remains only for the no-parseable-claims edge.
- **well_formed section-absent UX**: accepted gap; surface a lightweight "N/6 sections produced claims" signal via the existing status query (cheap) so a silently-dropped section is visible. (If deferred → tracked follow-up, not silent.)

### D9 — Section-run telemetry status
Add a 4th `MechanicAnalysisSectionRunEntity.Status` value (`3 = RetainedWithGuardrailFlags`) + widen the `ck_..._status_range` (0..3) and keep `error_when_failed` semantics; migration ships in BE-core. A retained guardrail-failed section gets Status=3 (not 1) so telemetry stays honest.

## Testing (hardened per review)

- **Validator**: collect-all per-rule outcomes (pass/fail/notRun + Path + T3b score); fail-fast retry trigger unchanged.
- **Pipeline** (NEW — no `RunAsync` tests exist today): run-all-retain (guardrail-fail section → retained + outcomes, no abort); `well_formed` → section absent; grounding-**unavailable** → hard abort; cost-cap/LLM-fail → hard abort; update the now-unreachable `ApplyAbort(AbortedValidation)` tests.
- **Correlation**: multi-claim section where item #2 fails → ONLY that claim flagged, siblings `pass` (the core anti-false-positive test).
- **Persistence**: Testcontainers round-trip incl. **mutate-then-resave** (reconstitute → ApproveClaim → Update → reload) proving `validations` survives an UPDATE (value-comparer), not just insert.
- **Card**: `FromAnalysis` down-projection (fail/notRun → passed:false; T3b score carried); schema_version=2.
- **Safety**: bulk-approve-all-pending excludes fail-flagged; publish of an approved fail-flagged claim records `passed:false` (executable OQ1 decision).
- **FE**: 5-badge render; `reject-all-failing-T2` selects only T2-fail claims; approve-warning; Zod `score` contract test.

## Delivery (revised — decomposition)

- **BE-core** (atomic — pipeline behavior + persistence together): D1 validator collect-all + D2 score + D3 run-all/carve-outs + D4 parser anchor + correlation + D5 column/migration/mappers/DerivePass-flip + D9 status migration. Ships as one cohesive unit (may be internally staged as multiple commits but merged together to avoid the fake-all-pass window).
- **BE-card**: D6 card projection + schema_version bump + ADR #2786 + D8 server-side bulk-approve guard.
- **FE**: D7 (5 badges, reject-all-failing-T2, approve-warning, score, Zod test).

Executed via subagent-driven-development (implementer + spec/quality reviewer per task, final whole-branch review), TDD, then merge + cleanup — same workflow as #526. Given BE-core's size, it may itself be a stacked pair whose PRs merge back-to-back (not left half-deployed).

## Resolved review findings

| # | Severity | Finding | Resolution |
|---|---|---|---|
| B1 | blocker | section-level broadcast mislabels claims | **D4** path→claim correlation (dec. 4) |
| B2 | blocker | 5 guardrails, non-monotonic, ≠ T1-T4 model | **D1/D6/D7** 5-rule taxonomy (dec. 6) |
| B3 | blocker | section-run Status overload + CHECK | **D9** status=3 + CHECK migration |
| B4 | blocker | publish of fail-flagged (OQ1) | **D8** soft gate + bulk guard (dec. 5) |
| B5 | blocker | schema_version write-only / false compat | **D6** bump to 2 + honest note |
| M1 | major | grounding fail-closed → advisory (IP) | **D3** outage stays hard fail-closed (dec. 5) |
| M2/M5 | major | BE-1-alone regression | **Delivery** BE-core atomic (dec. 7) |
| M3 | major | DerivePass = 5 sites | **D5** domain field + 5 sites + read-map |
| M4 | major | WRITE-mapper = 4 points + value-comparer | **D5** 4 points + comparer + mutate test |
| M6 | major | reject-T2 mirrors T1 quote-length | **D7** reads validations T2-fail |
| M7 | major | Zod silent-drop of score | **D7** contract test |
| minors | — | DerivePass rename/doc, no-GIN note, well_formed UX, no pipeline tests, cost | folded into D5/D6/D8/Testing |
