# Mockup SMART Constraint Taxonomy

> **WS-E.1** (#1071, umbrella #1066) — AC-E.4
> **Status**: foundation taxonomy. Implementation check + auto-issue creation ship in **WS-E.2**.

## Purpose

Mockups in `admin-mockups/design_files/*.html` embed acceptance constraints in their leading HTML comment, e.g. (sp4-game-chat-tab):

```
Vincoli SMART (da spec G1+G5):
  - 90% query con citazione esplicita (pagina+sezione)
  - confidence < 70% → marker incertezza obbligatorio
  - latency P95 ≤ 10s con spinner accettabile
  - chat handoff <3s (G2 — non coperto qui ma compatibile con UX)
```

These are **SMART** (Specific · Measurable · Achievable · Relevant · Time-bound) acceptance criteria that the design contract asserts the implementation must satisfy. WS-E.1 extracts each into a structured record so future tooling (E.2) can cross-reference them against telemetry, UI markers, or test coverage.

## Schema

Output: `docs/for-developers/audits/mockup-smart-constraints.json`

```typescript
{
  generatedAt: string;        // ISO timestamp of extraction
  totalMockups: number;        // mockups scanned
  totalConstraints: number;    // constraints extracted
  byMetricType: Record<MetricType, number>;
  constraints: ConstraintRecord[];
}

interface ConstraintRecord {
  mockup: string;                    // source HTML filename
  id: string | null;                 // spec id if `G4.2:` prefix present
  text: string;                      // raw constraint text
  metric_type: MetricType;           // see taxonomy below
  target_value: TargetValue | null;  // parsed numeric threshold (if any)
  applies_to: string[];              // mockup file(s) — single-source today
}

interface TargetValue {
  comparator: '<' | '≤' | '=' | '≥' | '>';
  value: number;
  unit: 'ms' | 's' | 'min' | 'h' | '%' | 'fps' | 'px' | 'kb' | 'mb' | 'gb';
}
```

JSON Schema sidecar (for IDE validation) will land in WS-E.2 alongside the auto-issue workflow.

## Metric Types

Classification is **heuristic** (regex on `text`). When multiple rules match, **the first listed wins** — order is intentional, see `KEYWORD_TYPE_MAP` in `extract-mockup-smart-constraints.ts`. Manual override is currently not supported; future work could add an inline `[type: ...]` tag.

| Type | Definition | Example | Quantitative? |
|------|------------|---------|---------------|
| **`citation`** | Source provenance / citation chip / inline reference | "ogni risposta include citazione page+section" | usually no |
| **`confidence`** | Uncertainty markers / confidence indicators | "confidence < 70% → marker incertezza obbligatorio" | sometimes (`< X%`) |
| **`latency`** | Wall-clock, user-perceived end-to-end timing | "latency P95 ≤ 10s con spinner", "chat handoff <3s" | yes (`s`, `min`, percentile) |
| **`performance`** | Sub-perceptual rendering / paint / frame metrics | "upsell render <100ms", "scroll @ 60fps" | yes (`ms`, `fps`) |
| **`coverage`** | Percentage of cases that must satisfy a criterion | "90% query con citazione esplicita" | yes (`%`) |
| **`accessibility`** | WCAG / a11y / contrast / keyboard nav | "WCAG 2.1 AA contrast", "tab navigation completa" | sometimes (`AA`/`AAA`) |
| **`ux-constraint`** | UI element visibility / interaction shape | "tab sempre visibile", "niente download button" | qualitative |
| **`other`** | Unclassified; fall-through, manual review queue | (anything that doesn't match above) | — |

## Classification priority

```
1. accessibility    (most specific terminology)
2. coverage         (leading "N% ..." → percentage threshold trumps soft matches)
3. confidence
4. citation
5. latency          (keyword OR `<Xs` form)
6. performance      (keyword OR `<Xms` form)
7. ux-constraint
8. other            (fall-through)
```

Rationale for the order:
- **Coverage before citation**: "90% query con citazione esplicita" is fundamentally a *coverage* constraint (about which fraction of queries comply), not about citations as a feature.
- **Latency before performance**: "P95 ≤ 10s" is user-perceived latency; "render <100ms" is sub-perceptual rendering. Different time scales, different SLOs.
- **ux-constraint last**: most qualitative; broad keywords like "visible"/"disabled" must not preempt quantitative classifications.

## Quantitative parsing

When `text` starts with `N%` OR contains a comparator+number+unit (e.g. `≤ 10s`, `<100ms`), the parser populates `target_value`:

```typescript
"latency P95 ≤ 10s ..."         → { comparator: '≤', value: 10,  unit: 's'  }
"upsell render <100ms ..."      → { comparator: '<', value: 100, unit: 'ms' }
"90% query con citazione ..."   → { comparator: '≥', value: 90,  unit: '%'  }
"niente download button visibile" → null (qualitative)
```

Leading-`N%` form is treated as `≥` (covering at-least semantics — "90% of queries" means *at least* 90%, not exactly).

## Extending the taxonomy

To add a new metric type:

1. Add it to `MetricType` union in `extract-mockup-smart-constraints.ts`.
2. Append a `{ re, type }` entry to `KEYWORD_TYPE_MAP` in the right priority position.
3. Update this doc with a row + rationale.
4. Add a unit test case for `classifyMetricType` covering the new keyword.
5. Re-run `pnpm tsx scripts/extract-mockup-smart-constraints.ts` and commit the refreshed `mockup-smart-constraints.json`.

Avoid:
- Overlapping rules without clear priority (test cases must enforce ordering).
- Highly specific patterns that match only one constraint (suggests a manual tag would be cleaner).

## Confidence tiers (AC-E.2 rewritten — WS-E.2a)

`check-constraint-implementation.ts` assigns each extracted constraint a tier based on what evidence exists in the codebase:

| Tier | Signal | Auto-issue (WS-E.2b)? |
|------|--------|-----------------------|
| `verified` | Explicit `// @spec-ref <id>` in scoped code | No |
| `likely-implemented` | Grep pattern (AC-E.5 table) matched in scoped path | No |
| `manual-review` | `metric_type ∈ {ux-constraint, other}` (no auto-grep) | No (surface in summary) |
| `unknown` | Neither @spec-ref nor pattern match (default) | Yes, after ≥30d age |
| `missing` | Manual annotation via `<!-- spec-missing: <id> -->` | Yes immediately |

The grep patterns are best-effort heuristics (file globs and regex per `metric_type`). A pattern match elevates a constraint to `likely-implemented` but is **not proof** — only an explicit `@spec-ref <id>` comment promotes to `verified`.

Run locally:

```bash
cd apps/web
pnpm audit:mockup-smart-impl
# Writes docs/for-developers/audits/mockup-smart-implementation.json
```

## Implementation-marker map (AC-E.5)

`PATTERN_MAP` in `check-constraint-implementation.ts` declares for each `metric_type`:

- `pathScopes` — relative path segments where matches count (ignores out-of-scope)
- `patterns` — regex run against file contents, any-of semantics
- `manualOnly` — short-circuit to `manual-review` (for `ux-constraint` and `other`)

Reference table:

| metric_type | Path scope | Pattern examples |
|---|---|---|
| `latency` | `apps/web/src`, `apps/api/src` | `histogram.*latency`, `recordLatency`, OpenTelemetry meter |
| `performance` | `apps/web/src` | `lighthouse.*budget`, `perf.*test`, `Profiler` |
| `citation` | `apps/web/src` | `CitationChip`, `data-citation`, `<Citation` |
| `confidence` | `apps/web/src` | `ConfidenceIndicator`, `data-confidence` |
| `coverage` | `__tests__`, `e2e` | `expect.*toBeGreaterThan.*0\.\d` |
| `accessibility` | `__tests__`, `e2e/a11y`, `e2e` | `axe.run`, `toBeAccessible`, `@axe-core` |
| `ux-constraint` | — | manual review only |
| `other` | — | manual review only |

To strengthen a constraint from `likely-implemented` to `verified`, add a comment like:

```typescript
// @spec-ref G4.2 — tab "PDF originale" always visible per WS-E spec
```

The check script greps for `@spec-ref <id>` literally. Free-form references in PR descriptions or commits do **not** count.

## WS-E.2b (shipped)

The sync workflow + false-positive override hook + allowlist ledger ship together.

### `mockup-spec-sync.yml`

Three triggers:

| Trigger | Mode | Notes |
|---------|------|-------|
| `schedule '0 8 * * *'` (daily 08:00 UTC) | always `dry-run` | Canonical sync path; never auto-creates issues without explicit human go-ahead |
| `pull_request` on `admin-mockups/design_files/**` + the audit scripts | always `dry-run` | Opportunistic feedback when a mockup change lands |
| `workflow_dispatch` with `mode` input | `dry-run` (default) or `apply` | Manual escalation. Promoting to `apply` requires editing the workflow file's `default: 'dry-run'` line — git history shows the explicit decision |

Both `dry-run` and `apply` emit a GitHub Actions job summary. `apply` mode is capped at `max-issues-per-run` (default 5) — excess candidates are deferred to the next run.

Auto-issue selection rule (AC-E.2 rewritten):

- `tier === 'missing'` → eligible immediately
- `tier === 'unknown'` AND `now - evaluated_at >= min_unknown_age_days` (default 30d) → eligible
- All other tiers → skipped (verified / likely-implemented / manual-review)
- `dedup_key ∈ allowlist` → skipped (false-positive override)

Dedup is via `<!-- spec-debt-key: <hash>; ... -->` header marker in the issue body. Reapplication of the same key updates the existing issue instead of creating a duplicate (AC-E.7).

### `spec-debt-false-positive-handler.yml`

Triggers on `issues.labeled` with label `spec-debt-false-positive`. Steps:

1. Parses the `dedup_key` from the issue body marker
2. Appends the key to `docs/for-developers/audits/spec-debt-false-positive-allowlist.json` (idempotent, sorted)
3. Closes the labelled issue with `state_reason: not_planned` and a tracking comment
4. Opens an auto-PR with the allowlist change for human review via `peter-evans/create-pull-request@22a9089` (SHA-pinned)

Reviewer checklist on the auto-PR: confirm the FP determination is genuine, then merge to activate the override. To re-enable auto-issue creation later, remove the entry from the allowlist via PR — the next sync run will re-open a fresh debt issue.

### Allowlist file

`docs/for-developers/audits/spec-debt-false-positive-allowlist.json`:

```json
{
  "version": 1,
  "keys": []
}
```

The list is the audit trail. Manual edits are tracked in git history. Removing an entry is a deliberate revert decision (auto-issue will be recreated by the next workflow run).

## Future work

| Item | Deliverable |
|------|-------------|
| JSON Schema sidecars | `mockup-smart-constraints.schema.json` + `mockup-smart-implementation.schema.json` for IDE validation |
| `first_seen` tracking | Persist the first-evaluation timestamp across check-implementation runs so `unknown ≥ 30d` truly tracks the constraint's lifespan rather than just the last evaluation date |
| Weekly summary observability | Optional dashboard listing top dedup_keys, by-tier counts, allowlist age (analogue of AC-C.5.7) |

## Refs

- Spec: `docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md` §3 WS-E
- Umbrella: [#1066](https://github.com/meepleAi-app/meepleai-monorepo/issues/1066)
- WS-E issue: [#1071](https://github.com/meepleAi-app/meepleai-monorepo/issues/1071)
- Sibling workstream: WS-F headers + dashboard (`docs/for-developers/testing/frontend/mockup-conformity.md`)
