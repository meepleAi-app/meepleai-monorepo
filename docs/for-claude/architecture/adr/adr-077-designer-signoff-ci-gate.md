# ADR-077 — Designer Signoff CI Gate

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 4 — US-INT-6 (CI/CD quality gates)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · mockup audit DS-17 Phase B (#2127) · fidelity.json pattern · `ci.yml`

## Context

The MeepleAI design system (DS-17) introduced `*.fidelity.json` files alongside each mockup in `admin-mockups/design_files/`. Each fidelity file carries a `designer_approved_by` and `designer_approved_on` field:

```json
{
  "acceptance": {
    "designer_approved_by": "",
    "designer_approved_on": ""
  }
}
```

The `generate-deliverables.mjs` script populates these as empty strings by default. The mockup audit pattern (CLAUDE.md § Mockup audit — DS-17 Phase B) requires that new mockups carry a `design_intent` ∈ `{current, forward-refactor, forward-refactor-obsolete}`. The `pnpm lint:fidelity` gate validates the schema but does not currently enforce `designer_approved_by` being non-empty.

**P250 self-waiver pattern**: the project has an established single-person team exception. Several `fidelity.json` files in the audit queue contain or anticipate a value like:
```json
"designer_approved_by": "user@meepleAi (self-waiver P250, single-person team)"
```
This pattern acknowledges that the developer and the designer are the same person — the "designer review" is the developer reviewing their own mockup against the design intent.

**Current CI gates** (`ci.yml`):
- `pnpm lint:tokens` — blocking (token canonicalization).
- `pnpm mockup-annotations:audit --threshold 80` — blocking (mockup annotation coverage).
- `pnpm lint:bgg` — blocking (BGG URL guard).
- `pnpm lint:fidelity` — runs but validates schema only, not approval status.
- `Frontend - A11y E2E` — blocking (0 axe AA violations required).

The `designer_approved_by` field has no current CI enforcement. Mockups flow through the audit queue (`docs/for-developers/frontend/mockup-designer-review-queue.md`) but there is no mechanical gate preventing a PR from merging a new `design_intent: "current"` component without setting `designer_approved_by`.

**Design surface definition**: "designer review" applies to PRs that modify `apps/web/src/` components tagged with `area/frontend` **and** that implement or modify a user-facing UI surface mapped in `admin-mockups/MOCKUPS_INDEX.md`. Infrastructure-only, backend-only, and documentation PRs are explicitly excluded.

## Problem

The specific architectural question: **should `designer_approved_by` enforcement be a hard CI blocker, an advisory annotation, or a time-boxed default-approve gate — and how does this interact with the solo-maintainer P250 self-waiver pattern?**

## Options Considered

### Option A — Blocking Gate (hard CI block until signoff label added)

A new CI check `Designer Signoff` runs on all PRs that touch `apps/web/src/components/**` or `apps/web/src/app/**` (path filter). The check:
1. Identifies all `*.fidelity.json` files modified or added in the PR diff.
2. Checks `designer_approved_by` is non-empty for each.
3. Fails the check if any fidelity file lacks an approval.

Alternatively, implemented via a GitHub branch protection rule requiring a label `designer-approved` before merge.

**Pros**:
- Strongest enforcement: no component reaches production without explicit signoff.
- Prevents design debt accumulation — the cost of deferring signoff is immediate (PR cannot merge).

**Cons**:
- Blocks developer velocity for every frontend PR, even trivial ones (e.g. fixing a typo in a label). Not all frontend changes require a design review.
- The P250 self-waiver pattern means the developer must manually add the self-waiver string before each merge — a CI-unfriendly ceremony that adds friction without real quality value in a solo-maintainer context.
- False blocking: a PR that modifies a backend-side component handler (e.g. a `.tsx` file inside `app/(authenticated)/sessions/[id]/live/_components/`) may not have a corresponding fidelity file but would trigger the check by path filter.
- A blocked PR that has been reviewed and merged in the development workflow (developer self-reviewed as P250) creates a ghost blocker that must be manually cleared.

**Risks**: Frequent block → developers learn to always pre-populate `designer_approved_by` before opening a PR, which defeats the purpose of the gate (if approval is automatic, it carries no review signal). The gate becomes a rubber-stamp ceremony.

**Impact**: ~1 day. New GHA workflow step or label-required branch protection.

---

### Option B — Advisory (CI check runs but does not block merge)

The `pnpm lint:fidelity` gate is extended to also check `designer_approved_by` and emit a `warning` annotation (not a failure) when empty. The PR summary comment (posted by CI) includes a section: "⚠️ Pending designer signoff: `sp4-play-records-stats.fidelity.json`". The developer sees the warning but can merge.

**Pros**:
- Zero velocity impact: developer and reviewer are aware of missing signoffs without being blocked.
- Visible in PR: the warning comment creates a conversation anchor for the designer review.
- Compatible with P250 self-waiver: the developer can fill in the self-waiver field at any time and the warning clears on the next CI run (or the reviewer can accept the warning as understood).

**Cons**:
- No enforcement: if the warning is consistently ignored (which it will be in a solo-maintainer context where the developer is also the designer), it becomes noise.
- The design review queue (`mockup-designer-review-queue.md`) may fall behind as warnings accumulate without resolution.
- Advisory warnings that are consistently non-blocking tend to be ignored system-wide — a well-studied problem in CI warning fatigue.

**Risks**: Low — no blocking risk. The primary risk is the warning becoming invisible noise over time.

**Impact**: ~0.5 days. Extend `pnpm lint:fidelity` to emit warning annotations.

---

### Option C — Timeout-Default-Approve (blocking for 48h, then auto-approves)

A GHA workflow posts a "designer review pending" label on a PR when frontend-UI changes are detected. If the label has not been replaced by `designer-approved` within 48h, a bot (GHA `workflow_dispatch` cron + GH API call) auto-applies `designer-approved` and the blocking check clears. This models "if no designer responds in 48h, the change is implicitly approved."

**Pros**:
- Forces at least a 48h design review window — creates space for a real review if a designer is available.
- Self-heals for solo-maintainer: after 48h, the PR can merge without manual ceremony.
- Auditable: the auto-approve event is logged in the PR timeline.

**Cons**:
- GHA bot requires `issues:write` and `pull-requests:write` permissions — a `GITHUB_TOKEN` with elevated scope.
- The 48h window is arbitrary: a trivial frontend fix (correcting a token name) is blocked for 48h even if no design review is needed.
- Complexity: a new GHA cron workflow + label management + PR detection logic. More moving parts than Option B or D.
- Solo-maintainer reality: the developer will simply wait 48h for the auto-approve. The gate becomes a time tax, not a quality gate.

**Risks**: Bot workflow failures (see memory: `gh-pr-merge-auto-stale-sha.md`) — auto-merge bots can be unreliable. Label management in GHA has known edge cases.

**Impact**: ~2 days. New GHA cron workflow + bot permissions + label logic.

---

### Option D — Per-Area Blocking, with Explicit P250 Self-Waiver as a Valid Approval (recommended)

Extend `pnpm lint:fidelity` to check `designer_approved_by` for fidelity files with `design_intent == "current"` (not `forward-refactor` or `forward-refactor-obsolete`). The check **blocks** the CI on PRs that:
1. Add or modify a `design_intent: "current"` fidelity file **and**
2. Have `designer_approved_by` empty.

The check explicitly **accepts** the P250 self-waiver pattern — if `designer_approved_by` contains the substring `"self-waiver P250"`, the check passes.

**For PRs with no fidelity file changes**: the check trivially passes (no fidelity files in diff → not a frontend-UI surface PR, by definition).

**For PRs changing `forward-refactor` or `forward-refactor-obsolete` fidelity files**: advisory warning only, not blocking. These files are not yet approved for implementation.

This maps the P250 pattern from an informal convention to a formally recognised CI-accepted token. The developer fills in `"user@meepleAi (self-waiver P250, single-person team)"` before opening a PR — a 5-second ceremony — and the gate passes.

**Pros**:
- Only blocks when a `design_intent: "current"` surface is added/modified without signoff. Trivial frontend PRs (label fixes, token corrections) that do not add or modify fidelity files are not blocked.
- P250 self-waiver is explicit CI documentation of the solo-maintainer exception — not a workaround but a first-class approved pattern.
- Scoped: `forward-refactor` and `forward-refactor-obsolete` files are advisory-only — they are design aspirations, not current surfaces, and do not need blocking enforcement.
- No new GHA workflow complexity: `pnpm lint:fidelity` is extended in the existing `ci.yml` frontend job.

**Cons**:
- Requires every `design_intent: "current"` fidelity file to have `designer_approved_by` filled before PR open — minor discipline required.
- If the solo-maintainer team grows and a real designer is hired, the P250 pattern must be retired and replaced with a genuine design review workflow. The `pnpm lint:fidelity` check will need to be updated to reject `self-waiver P250` tokens at that point.
- The fidelity file may not be modified in the same PR as the component implementation — if the developer modifies a `.tsx` file without touching the `.fidelity.json`, the check does not fire. Fidelity files are only checked when they appear in the PR diff.

**Risks**: Low. The check is scoped to diff-changed fidelity files and accepts an explicit waiver string. No false positives for infrastructure/backend PRs. The P250 acceptance string is documented here and in the relevant spec files.

**Impact**: ~0.5 days. Extend `pnpm lint:fidelity` script to check `designer_approved_by` for `design_intent: "current"` files and accept `self-waiver P250`.

## Decision

**Adopt Option D**: per-area blocking gate scoped to `design_intent: "current"` fidelity files, with `self-waiver P250` as a formally accepted approval token.

**Rationale**: Option A blocks too broadly (all frontend PRs, not just mockup-surface changes). Option B provides no enforcement signal. Option C adds 48h time tax + GHA bot complexity without quality benefit. Option D is surgical: it blocks only when a current-design surface is added or modified without documented approval, and explicitly codifies the solo-maintainer exception without blocking velocity.

## Consequences

**Positive**:
- Developers cannot accidentally ship a `design_intent: "current"` surface without deliberately setting `designer_approved_by` — even if only with the P250 self-waiver.
- The `pnpm lint:fidelity` gate becomes a complete schema + approval validator — one command covers both concerns.
- The P250 self-waiver pattern is CI-documented, reducing confusion about when the exception applies.
- `forward-refactor` and `forward-refactor-obsolete` files remain advisory — no friction for speculative design work.

**Negative**:
- Requires existing `design_intent: "current"` fidelity files with empty `designer_approved_by` to be backfilled (either with the P250 waiver or with a genuine approval) before the gate is enabled. Audit scope: ~38 `design_intent: "current"` fidelity files (per DS-17 Phase B audit count).
- If the gate is enabled before backfill, any PR that touches a fidelity file with empty `designer_approved_by` will fail CI — gate should be enabled only after the backfill PR merges.

**Trade-offs**:
- The gate does not prevent a developer from pre-filling the self-waiver without actually reviewing the design. The gate is a documentation discipline tool, not a proof-of-review mechanism. In a solo-maintainer context, this is the correct trade-off: the waiver documents the exception, not bypasses a real review.

## Implementation Guidance

1. **Extend `pnpm lint:fidelity`** (`apps/web/scripts/audit-mockups/` or the `pnpm` script target in `package.json`):
   - For each `.fidelity.json` in the PR diff with `design_intent == "current"`:
     - If `designer_approved_by` is empty → fail with message: `"ERROR: sp4-play-records-stats.fidelity.json (design_intent: current) is missing designer_approved_by. Fill in the signoff or use 'self-waiver P250' for solo-maintainer context."`.
     - If `designer_approved_by` contains `"self-waiver P250"` → pass (emit `INFO: accepted P250 self-waiver`).
     - Otherwise (real approver name) → pass.
   - For `design_intent != "current"`: emit advisory warning only, do not fail.

2. **Backfill PR**: before enabling the check, submit a single PR that populates `designer_approved_by: "badsworm@gmail.com (self-waiver P250, single-person team)"` and `designer_approved_on: "2026-06-15"` on all existing `design_intent: "current"` fidelity files with empty approval fields.

3. **CI integration** (`ci.yml`): add `pnpm lint:fidelity:approvals` (or extend the existing `pnpm lint:fidelity` script) as a step in the frontend job, after the existing `pnpm lint:fidelity` schema validation. Gate on `if: steps.changes.outputs.frontend == 'true'` (same condition as other frontend checks).

4. **Future team growth**: when a second designer joins the team, remove the P250 waiver acceptance from `pnpm lint:fidelity` and require `designer_approved_by` to match a configured list of approved designer email addresses. This is a one-line config change in the lint script.

5. **Documentation**: update `docs/for-developers/frontend/mockup-annotation-pattern.md` to document the `designer_approved_by` field requirement and the P250 self-waiver pattern.

## Rollback / Reversibility

Removing the `designer_approved_by` check from `pnpm lint:fidelity` reverts to schema-only validation (Option B advisory). No schema or migration changes are involved. The backfill PR is additive (fidelity JSON field values) and can be reverted if needed.

## References

- Fidelity JSON pattern — `admin-mockups/design_files/sp4-play-records-stats.fidelity.json` (example)
- `generate-deliverables.mjs` — `apps/web/scripts/audit-mockups/generate-deliverables.mjs:50` (`designer_approved_by: ''` default)
- Mockup annotation pattern — `docs/for-developers/frontend/mockup-annotation-pattern.md`
- CLAUDE.md § Mockup audit — DS-17 Phase B (#2127) (P250 self-waiver pattern documented)
- `ci.yml` — `.github/workflows/ci.yml` (frontend job with existing lint gates)
- `pnpm lint:fidelity` — defined in `apps/web/package.json`
- Designer review queue — `docs/for-developers/frontend/mockup-designer-review-queue.md`
