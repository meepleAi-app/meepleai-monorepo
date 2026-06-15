# ADR-078 — Auto-Issue Noise Thresholds and Batch Grouping Policy

**Status**: Proposed
**Date**: 2026-06-15
**Deciders**: @badsworm (pending ratification at PR review)
**Tracking**: [#2363](https://github.com/meepleAi-app/meepleai-monorepo/issues/2363) Wave 4 — US-INT-6 (CI/CD quality gates)
**Related**: [umbrella #2342](https://github.com/meepleAi-app/meepleai-monorepo/issues/2342) · `spec-debt-false-positive-handler.yml` · `dev-auto-revert.yml` · issue #1071 (WS-E.2b false-positive override)

## Context

MeepleAI's CI/CD infrastructure includes several automated workflows that create GitHub issues based on detected conditions:

**Existing auto-issue workflows (evidenced in `.github/workflows/`)**:

1. **`spec-debt-false-positive-handler.yml`** (issue #1071, WS-E.2b): Creates auto-PRs and updates an allowlist (`spec-debt-false-positive-allowlist.json`) when a `mockup-spec-debt` issue receives the `spec-debt-false-positive` label. Uses a `dedup_key` hash in the issue body marker (`<!-- spec-debt-key: {key}; -->`) to prevent re-opening the same issue.

2. **`dev-auto-revert.yml`** (ADR-055, issue #843): A Phase 1 shadow-mode bot that classifies main-dev failures every 15 minutes. Emits Slack alerts when `verdict=real-failure` crosses a threshold. In Phase 2 (not yet active), it would open revert PRs. Uses a failure-mode catalog (8 categories: `success`, `real-failure`, `infra-flake`, `concurrency-cancelled`, etc.) to classify before acting.

3. **`ci-minutes-digest.yml`**: Presumably creates or updates a digest issue with CI minute consumption.

4. **`runner-health-check.yml`** and **`monitor-runner-queue.yml`**: Runner health monitoring — likely emit issues or Slack alerts on runner anomalies.

5. **`validate-secrets.yml`**: Validates secrets configuration — could emit issues on secret drift.

6. **`docs-linkcheck.yml`**: Link checking — could emit issues on broken links.

**Current noise management evidence**:
- The `dedup_key` pattern in `spec-debt-false-positive-handler.yml` shows that at least one workflow uses content-hash fingerprinting to prevent duplicate issue creation.
- The `dev-auto-revert.yml` uses a failure-mode classification with threshold logic ("would-revert only after 30min continuous red") before taking action — a noise-reduction strategy.
- The `spec-debt-false-positive-allowlist.json` is an explicit allowlist pattern for suppressing known-false-positive auto-issues.

**Problem space**: as the number of automated monitors grows (security scans, dependency drift, spec debt, seed snapshot freshness, runner health), each generating potential issues, the GitHub issue tracker can be polluted with:
- **Noise issues** (same condition detected on consecutive scans, creating N identical issues).
- **Burst issues** (a single root cause — e.g. a network blip — causing 10 simultaneous "health check failed" issues across 10 monitors).
- **Stale issues** (a condition that auto-resolved but the issue remains open, accumulating false backlog).

The existing codebase has partial solutions (`dedup_key`, threshold checks) but no unified cross-workflow policy for noise management.

## Problem

The specific architectural question: **what is the canonical policy for auto-issue creation across all MeepleAI automated monitors — including thresholds, batch grouping, deduplication strategy, and escalation tiers — such that the GitHub issue tracker reflects actionable signal rather than automated noise?**

## Options Considered

### Option A — Hard Daily Cap (max 1 issue per day per workflow)

Each auto-issue workflow reads a `last_issue_created_at` value (stored as a workflow output, a GH variable, or a file in the repo) and skips issue creation if one was already created within the past 24h.

**Pros**:
- Simple and predictable: each monitor can create at most 1 issue per day.
- Reduces backlog growth rate to O(monitors × days).

**Cons**:
- Misses a P1 burst: if a security vulnerability is detected at 11:00 and the cap was already used at 09:00 for a different occurrence of the same monitor, the P1 is silently dropped until the next day.
- The 24h window is arbitrary: a monitor that fires on a 5-minute schedule has a 288x reduction in sensitivity.
- Does not help with stale issues: the cap prevents creation but does not manage issue lifecycle (reopen vs close).
- State storage for `last_issue_created_at` across GHA workflow runs requires a GitHub Actions variable or a file commit — operational complexity.

**Risks**: Silent drops of P1 issues. Not recommended as the sole mechanism.

**Impact**: ~1 day per workflow adoption. Fragile state management.

---

### Option B — Batch Grouping (1 issue per scan, N findings as bullet list)

Each scan cycle (cron run) produces a single issue containing all findings as bullet points:
```
## Scan findings: 2026-06-15 02:00 UTC
- [ ] Broken doc link: `docs/for-developers/ops/manual.md:42` → 404
- [ ] Broken doc link: `docs/for-developers/api/ref.md:17` → 404
- [ ] Stale secret: `OPENROUTER_API_KEY` not found in vault
```

Issue title: `[auto] Scan findings — 2026-06-15` — one issue per day per scan type.

**Pros**:
- Linear issue growth: 1 issue per day per scan type, regardless of finding count.
- Findings are co-located: the reviewer sees all issues in one place for triage.
- Natural batch for a single CI run — each scan run owns its issues.

**Cons**:
- Issue granularity is lost: the batch issue cannot be labelled, assigned, or tracked per finding. Assigning "broken link in API ref" to a specific developer is awkward if it's item 7 in a 15-item list.
- Progress tracking per finding requires checkbox state in the issue body — GitHub doesn't track individual checkboxes as sub-tasks.
- If the scan finds 0 issues, no issue is created but the previous batch issue remains open (stale backlog problem).
- "N findings in 1 issue" can mask escalation: if the finding count doubles from 5 to 50 between scans, the issue count stays at 1 — the severity increase is invisible without reading the body.

**Risks**: Triage complexity. Sub-optimal for large batches (>10 findings).

**Impact**: ~1.5 days per workflow to implement batch aggregation.

---

### Option C — Deduplication via Fingerprint Hash + Reopen-Old (recommended as primary mechanism)

Each auto-issue carries a `dedup_key` in the issue body (HTML comment marker, per the existing `spec-debt-false-positive-handler.yml` pattern): `<!-- auto-issue-key: {sha256_fingerprint}; -->`. The fingerprint is derived from the finding's stable attributes (e.g. `sha256(monitor_name + finding_id + affected_path)`).

Before creating a new issue, the workflow uses the GH API to search for open issues with the same `dedup_key` body marker. If found: update the existing issue (add a comment "re-detected: {timestamp}") and skip creation. If not found: create a new issue.

When the condition auto-resolves (monitor next scan returns clean), the workflow closes the old issue: `github.rest.issues.update({ state: 'closed', state_reason: 'completed' })`.

**Pros**:
- Prevents duplicate issue creation for the same condition across scan cycles — the key pattern is already proven in `spec-debt-false-positive-handler.yml`.
- Issues remain open exactly as long as the condition exists — lifecycle follows the actual problem, not the scan schedule.
- Reopening an existing issue (vs creating a new one) preserves the issue history (comments, assignment, labels) — useful for conditions that flap.
- Compatible with the existing `spec-debt-false-positive-allowlist.json` pattern: allowlisted keys are skipped at the search step.

**Cons**:
- GH API search for `dedup_key` is a GitHub Issues Search API call — subject to GH API rate limits (1,000 search requests/hour per workflow). With many monitors running in parallel, this could be constrained.
- The fingerprint must be stable across scan runs: if the finding description includes unstable attributes (e.g. a line number that shifts with code changes), the fingerprint changes on every scan and dedup fails. Fingerprint design is critical.
- `is:open` search is not instant — the GH API has up to 30s indexing lag for newly created issues. A race condition exists if two monitor runs start simultaneously (both see `is:open count=0` and both create a new issue).

**Risks**: Fingerprint instability → dedup failure → duplicate issues. API rate limits under burst conditions. Race condition on simultaneous parallel monitors (rare, mitigated by `concurrency` group settings already used in the codebase).

**Impact**: ~2 days. Shared GHA action (`auto-issue-dedup`) that all monitors call, implementing the search + create-or-update-or-close logic.

---

### Option D — Threshold + Escalation Tiers (recommended as escalation layer, combined with Option C)

Build on the existing `dev-auto-revert.yml` pattern: classify findings by severity before deciding the response:

```
Tier 0 (advisory):  finding count ≤ 2        → no issue (log to workflow summary only)
Tier 1 (tracking):  finding count 3-9         → create/update deduplicated issue (Option C)
Tier 2 (alert):     finding count 10-24       → issue + Slack DM to @badsworm
Tier 3 (P1):        finding count ≥ 25        → issue + Slack @channel + block merge
```

Additionally, some findings are P1 regardless of count (e.g. leaked secret, failing auth, production 500): these bypass the threshold and always create an issue + Slack alert.

**Combined with Option C**: the dedup hash prevents duplicate issues; the tier system determines response intensity.

**Pros**:
- Low-count noise is suppressed at Tier 0 (advisory log only): common transient conditions (1 flaky test, 1 stale link) do not generate issues.
- High-count bursts are escalated: 25 simultaneous failures indicate a systemic problem, not random noise — the P1 tier ensures immediate response.
- P1 bypass for specific finding types (leaked secrets) ensures critical issues are never suppressed by thresholds.
- Mirrors the `dev-auto-revert.yml` classification approach already proven in production.

**Cons**:
- Per-monitor threshold calibration required: the threshold for "alert on doc link failures" (Tier 1: ≥ 3) differs from "alert on flaky tests" (Tier 0: advisory always, since 1 flaky test is already a regression per CLAUDE.md Known Flaky Tests policy).
- The tier thresholds (3, 10, 25) are suggested defaults and may need tuning per monitor type.
- Combining with Option C adds complexity: the workflow must implement both the dedup hash check and the tier classification — more logic per workflow.

**Risks**: Threshold mis-calibration → either too noisy (threshold too low) or missed fires (threshold too high). Requires per-monitor configuration.

**Impact**: ~3 days for the shared escalation framework + per-monitor threshold configuration. Higher implementation cost, higher value.

## Decision

**Adopt Option C as the deduplication primitive, combined with Option D's tier system as the escalation layer.** Together, these form the canonical auto-issue policy.

**Canonical auto-issue creation policy**:
1. **Fingerprint every finding**: SHA-256 of `{monitor_name}:{stable_finding_id}`. Stable finding IDs are path-based (file path, issue ID) not line-number-based.
2. **Search for open issue with `dedup_key`** before creating. On match: add a re-detection comment; skip creation.
3. **Tier classification** (per-monitor configurable defaults: Tier 0 ≤ 2, Tier 1 3-9, Tier 2 10-24, Tier 3 ≥ 25). P1 finding types bypass all thresholds.
4. **Auto-close** when the condition resolves on next scan: `state_reason: 'completed'`.
5. **Allowlist integration**: check the `spec-debt-false-positive-allowlist.json` pattern before creating — if `dedup_key` is in the allowlist, skip (no issue, no comment).

**P1 bypass types** (issue always created regardless of count, no Tier 0 suppression):
- Leaked secret detected (`validate-secrets.yml`).
- Production auth failure.
- DB migration conflict.
- Dependency with known CVE (CVSS ≥ 7.0).

**Rationale**: Option A (daily cap) risks silent drops of P1 findings. Option B (batch grouping) reduces issue count but loses per-finding granularity and assignment capability. Options C+D provide maximum signal quality: dedup prevents noise accumulation, tiers ensure response proportional to severity, and auto-close maintains backlog hygiene without manual intervention.

## Consequences

**Positive**:
- Issue tracker reflects only active, actionable problems — resolved conditions are auto-closed.
- P1 conditions always surface regardless of threshold settings.
- The `dedup_key` pattern is consistent with `spec-debt-false-positive-handler.yml` — no new issue body conventions are introduced.
- Auto-close on resolution eliminates the "stale open issue" category of backlog debt.

**Negative**:
- Tier 0 suppression means 1-2 instances of a condition are logged only to workflow summaries — a developer must actively check workflow runs to discover Tier-0 conditions. This is the intended trade-off (Tier 0 = expected transient noise).
- GH API search latency and rate limits must be managed per the constraint above. Recommendation: all monitors use a shared `concurrency` group name `auto-issue-{monitor_type}` with `cancel-in-progress: false` to prevent parallel searches from the same monitor.
- Per-monitor threshold configuration must be maintained as a `workflow_inputs` or a centralised JSON config file.

**Trade-offs**:
- The race condition in Option C (two parallel monitors both see 0 open issues and both create one) is mitigated but not eliminated by the `concurrency` group. If it occurs, the duplicate is detected on the next scan run and the extra issue is auto-closed with a `state_reason: 'not_planned'` comment. Acceptable residual risk.

## Implementation Guidance

1. **Shared GHA action**: create `.github/actions/auto-issue-dedup/action.yml` with inputs: `monitor_name`, `finding_id`, `finding_title`, `finding_body`, `tier_threshold` (JSON: `{"warn":3,"alert":10,"p1":25}`), `is_p1` (boolean), `allowlist_path`. Outputs: `action` (`created | updated | skipped | allowlisted`), `issue_number`.

2. **Fingerprint generation**:
   ```javascript
   const crypto = require('node:crypto');
   const dedupKey = crypto.createHash('sha256')
     .update(`${monitorName}:${findingId}`)
     .digest('hex')
     .substring(0, 16); // 16-char prefix sufficient for uniqueness
   ```
   Embed in issue body: `<!-- auto-issue-key: ${dedupKey}; -->`.

3. **Search query**: `repo:meepleAi-app/meepleai-monorepo is:issue is:open body:"auto-issue-key: ${dedupKey};"` — uses GH Issues Search API.

4. **Auto-close on resolution**: each monitor workflow has a "check if condition still exists" step. If the condition is resolved and an open issue with matching `dedup_key` is found, close it: `github.rest.issues.update({ issue_number, state: 'closed', state_reason: 'completed' })` + comment "Auto-resolved: {condition} not detected on scan at {timestamp}".

5. **Adopt in existing workflows**: 
   - `dev-auto-revert.yml` Phase 2: add `dedup_key` to would-revert issues + auto-close when main-dev goes green.
   - `docs-linkcheck.yml`: use the shared action with `tier_threshold: {"warn":3,"alert":15,"p1":999}` (no P1 for doc links).
   - `validate-secrets.yml`: use with `is_p1: true` for leaked secret findings.

6. **Monitoring**: add a `ci-minutes-digest.yml`-style weekly digest that reports: open auto-issues by monitor type, auto-closes in the past 7 days, dedup hits (re-detections without new issue creation). This gives visibility into noise reduction effectiveness.

## Rollback / Reversibility

The shared `auto-issue-dedup` action is consumed by each monitor workflow independently. Removing the action from a workflow reverts that monitor to its pre-ADR behaviour (typically: always create a new issue, no dedup). The `spec-debt-false-positive-allowlist.json` format is unchanged. No schema or DB changes.

## References

- `spec-debt-false-positive-handler.yml` — `.github/workflows/spec-debt-false-positive-handler.yml` (existing `dedup_key` pattern)
- `dev-auto-revert.yml` — `.github/workflows/dev-auto-revert.yml` (failure-mode classification + threshold + shadow-mode pattern)
- `spec-debt-false-positive-allowlist.json` — `docs/for-developers/audits/spec-debt-false-positive-allowlist.json` (allowlist pattern)
- CLAUDE.md § Known Flaky Tests (0-baseline policy: flaky tests = regression, not noise)
- CLAUDE.md § Active Freezes (existing P1 conditions: BGG asset ban, token canonicalization)
- `ci.yml` — `.github/workflows/ci.yml` (reference for `concurrency` group pattern used across CI)
- Issue #1071 (WS-E.2b false-positive handler), issue #843 (dev-auto-revert ADR-055)
