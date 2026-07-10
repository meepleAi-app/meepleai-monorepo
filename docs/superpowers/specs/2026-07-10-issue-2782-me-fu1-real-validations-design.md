# #2782 (ME-M1.4 FU-1) — Real per-claim T1–T4 validations + scores — Design

**Status:** design (pending adversarial review + user approval)
**Issue:** [#2782](https://github.com/meepleAi-app/meepleai-monorepo/issues/2782) (folds ADR [#2786](https://github.com/meepleAi-app/meepleai-monorepo/issues/2786))
**Branch:** `feature/issue-2782-me-fu1-real-validations` (parent `main-dev`)
**Date:** 2026-07-10

## Goal

Replace the DERIVED all-`pass` `MechanicClaimValidations.DerivePass()` (shipped by #526) with REAL per-claim T1–T4 guardrail outcomes + scores captured at pipeline time, so the admin review UI surfaces genuine `fail`/`notRun` states, the `reject-all-failing-T2` bulk predicate becomes meaningful, and #527's mechanic-card snapshot can populate its `validations[]` (currently hard-coded empty).

## Context (as of 2026-07-10, post #526 + #527)

- **#526** added a review-side DTO `MechanicClaimValidationDto {Rule, Outcome, Message}` (camelCase JSON `{rule, outcome: pass|fail|notRun, message}`) surfaced on `MechanicClaimDto.Validations`, populated by `MechanicClaimValidations.DerivePass()` = 4 hard-coded all-`pass` badges. `ValidationBadges` (FE) already renders 3 states (pass/fail/notRun).
- **#527** added `MechanicCard` + `mechanic_cards.content` JSONB snapshot. `MechanicCardContent.FromAnalysis` hard-codes `Validations = Array.Empty<MechanicCardValidationSnapshot>()` (card-side shape `{rule, passed: bool, score: double?}` snake_case) with the comment "not yet persisted on the claim graph (wired by #526)" — but #526 did NOT persist them. **The two validation shapes are structurally different and never wired together.**
- **The pipeline** (`MechanicAnalysisPipeline.RunSectionAsync` + `RunAsync`): each of 6 sections runs an LLM call → guardrail validation (`MechanicOutputValidator.ValidateAsync`, **fail-fast** at the first failing guardrail) → retry-with-augmented-prompt up to `MaxRetriesPerSection+1`. The **first section to fail-after-retries aborts the entire pipeline** (`RunAsync` returns `BuildAbortResult` on `sectionAbort`); the failing section's output is **discarded** (`Output: null`). `SectionOutputs` therefore contains only guardrail-PASSING sections. The executor (`MechanicAnalysisExecutor`) parses `SectionOutputs` into claims → **every persisted claim is all-`pass` by construction**.
- **Guardrails** (`IMechanicGuardrail.EvaluateAsync` → `IReadOnlyList<MechanicValidationViolation>`) return violations on fail, nothing on pass. **No score is exposed** even for T3 grounding (it computes pass/fail internally, discards any similarity score).

## Locked decisions (from brainstorming, 2026-07-10)

1. **Scope = Full.** Surface real `fail`/`notRun` states + scores (not just "persist the derived all-pass").
2. **Pipeline = run-all-retain.** The pipeline no longer aborts on a validation failure: all 6 sections run, each retained with its final outcome. Guardrails become **advisory** (annotate), the **human reviewer is the gate**. Hard aborts remain only for LLM-failure / cost-cap / crash.
3. **Canonical shape = 3-state superset** `{rule, outcome: pass|fail|notRun, message, score}` (review-side). The card side projects **down** (`passed = outcome == "pass"`), never up — so `notRun` is never lost on the review side. This is the decision ADR #2786 records.

## Design

### D1 — Validator returns per-rule outcomes (not just violations)
`MechanicOutputValidator.ValidateAsync` changes from returning a single Valid/Invalid+violations to returning a **per-rule outcome list** for T1–T4, preserving fail-fast semantics:
- Rules that ran and passed → `pass`.
- The first rule that fails → `fail` (+ message from its violation).
- Rules **downstream** of the first failure (never run, due to fail-fast) → `notRun`.
- Each rule carries an optional `score: double?`.

`MechanicValidationResult` gains a `IReadOnlyList<MechanicRuleOutcome>` (Rule, Outcome, Message?, Score?). The existing retry loop still keys on "any fail → retry" (unchanged trigger); only the returned shape is richer.

### D2 — Guardrail score exposure (default: T3 only)
`IMechanicGuardrail` (or `MechanicValidationViolation` / a new pass-result) is extended so a guardrail can report a numeric `score` for its rule **even on pass**. Only **T3 grounding** populates a real score (its internal similarity); T1 (QuoteCap), T2 (long-verbatim/RejectionSampling), T4 (PageSubstring) leave `score = null` (binary rules). *(Vetoable: extend scores to other rules.)*

### D3 — Pipeline run-all-retain
`RunAsync` no longer early-returns on `sectionAbort == AbortedValidation`. Instead:
- Each section always contributes its **final output** to `SectionOutputs` when the output is well-formed JSON (even if a guardrail failed), plus its per-rule outcome to a new `SectionOutcomes: IReadOnlyDictionary<MechanicSection, IReadOnlyList<MechanicRuleOutcome>>` on `MechanicPipelineResult`.
- `RunSectionAsync` returns the last attempt's output + outcomes (not `null`) on validation failure.
- **Hard aborts unchanged:** LLM-failure (`AbortedLlmFailed`) and cost-cap (`AbortedCostCap`) still short-circuit `RunAsync`. A `well_formed` failure (malformed JSON, unparseable) yields **no claims** for that section (as today) and no outcomes to attach — that section is simply absent (out of scope to surface).
- Pipeline `Outcome` is `Succeeded` even when some sections failed guardrails (advisory).

### D4 — Correlation: section outcome → each claim in the section
Guardrails validate a section's whole output, so the outcome granularity is **section-level**. The executor/parser attaches the section's per-rule outcomes to **every claim** parsed from that section. `MechanicPipelineResult.SectionOutcomes` is threaded into `MechanicOutputParser` / the executor so each minted `MechanicClaim` receives its section's `MechanicRuleOutcome[]`.

### D5 — Persistence (default: JSON column)
New nullable column `mechanic_claims.validations jsonb` (+ EF migration, `HasColumnName("validations")`). `MechanicClaim` carries `IReadOnlyList<MechanicClaimValidation>` (Rule, Outcome, Message?, Score?), set at construction/parse time. `MechanicClaimValidations.DerivePass()` is **replaced** by reading the persisted column (fallback to the derived all-`pass` only for legacy claims with `validations = null`, so pre-FU-1 analyses still render). The read DTO `MechanicClaimValidationDto` shape is unchanged (already the canonical `{rule, outcome, message}`), plus `score` added. *(Vetoable: child table `mechanic_claim_validations` instead of a JSON column.)*

### D6 — Card reconciliation (#527) + ADR #2786
`MechanicCardContent.FromAnalysis` populates `Validations` from the claim's persisted outcomes, **projecting down** to the card shape: `MechanicCardValidationSnapshot { rule, passed = (outcome == "pass"), score }`. `notRun`/`fail` both map to `passed: false` on the card (a published card of accepted claims can lose the 3-state nuance; the review side keeps it). The ADR records: canonical = review-side 3-state superset; card = lossy down-projection; the JSON key casing on each side is frozen.

### D7 — Frontend
- `ValidationBadges` (already 3-state) receives real data — no change beyond the DTO gaining `score` (optional T3 score display, vetoable).
- New bulk predicate **`reject-all-failing-T2`** in `ClaimsSection` (mirrors the shipped `reject-long-quote`): selects claims whose `validations` contains a T2 `fail`, bulk-rejects them.

### D8 — Lifecycle consequence
A run whose sections fail guardrails now → **InReview with flagged claims** (previously → Rejected / PartiallyExtracted for `ValidationFailedBeyondRetry`). `AutoRejectionReasons.ValidationFailedBeyondRetry` becomes largely unused (kept for `well_formed`/no-parseable-claims edge, where a section yields nothing). Publishing (#527) is unaffected structurally, but see the open question.

## Open questions (for review / user)

- **OQ1 — Publish of failed claims:** With advisory guardrails, a `fail`-flagged claim can reach InReview. Should #527 publish **block** approving/publishing a claim still flagged `fail` (guardrail as a soft gate at publish), or is human approval an intentional override (card shows `passed:false`)? Default assumption: human approval overrides; the card faithfully records `passed:false`.
- **OQ2 — Cost impact:** run-all means sections after an early failure now incur LLM calls that today are skipped. Bounded by cost-cap. Acceptable? (Assumed yes per brainstorming.)
- **OQ3 — `notRun` vs run-all-guardrails:** we keep per-section fail-fast (so downstream rules = `notRun`). Alternative: run ALL guardrails per section (no `notRun`, always pass/fail). We keep fail-fast because #2782 explicitly wants `notRun` to light up + it's cheaper.

## Testing

- BE: validator per-rule-outcome unit tests (pass/fail/notRun ordering + score); pipeline run-all-retain tests (section fails → retained + outcomes, no abort; cost-cap/LLM-fail still abort); executor correlation (section outcome → claims); **Testcontainers round-trip** persisting + reloading `validations` (the #526 lesson: WRITE-mapper must copy the column); card `FromAnalysis` projection tests.
- FE: `reject-all-failing-T2` predicate + bulk flow; `ValidationBadges` real fail/notRun render; schema `score`.
- Regression: existing pipeline/executor/guardrail suites (behavior change is significant — verify no unintended abort-path breakage).

## Delivery (decomposition — multi-PR stacked, like #526)

- **BE-1** — validator per-rule outcomes + T3 score + pipeline run-all-retain (+ `SectionOutcomes` on the result). No persistence yet.
- **BE-2** — `mechanic_claims.validations` column + migration + `MechanicClaim` field + parser/executor correlation + flip `DerivePass()` + DTO `score`. (Carries the #526 WRITE-mapper trap.)
- **BE-3** — `MechanicCardContent.FromAnalysis` down-projection + ADR #2786 doc.
- **FE** — `reject-all-failing-T2` predicate + optional T3 score display.

Executed via subagent-driven-development (implementer + spec/quality reviewer per task, final whole-branch review), TDD, then merge + cleanup — same workflow as #526.
