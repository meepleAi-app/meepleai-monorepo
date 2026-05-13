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

## What ships next (WS-E.2)

| Item | Deliverable |
|------|-------------|
| `scripts/check-constraint-implementation.ts` | For each `metric_type`, grep the codebase for implementation markers (telemetry counters, UI data-attributes, dedicated tests) and emit a coverage report |
| `mockup-spec-sync.yml` workflow | Weekly cron + trigger on `admin-mockups/design_files/**` PR change. Diff fresh extraction vs committed JSON snapshot; create / update `mockup-spec-debt` issues for constraints without implementation markers (dedup by `(mockup, id, text-hash)`) |
| JSON Schema sidecar | `mockup-smart-constraints.schema.json` for IDE validation |

## Refs

- Spec: `docs/for-developers/specs/2026-05-12-mockup-conformity-roadmap.md` §3 WS-E
- Umbrella: [#1066](https://github.com/meepleAi-app/meepleai-monorepo/issues/1066)
- WS-E issue: [#1071](https://github.com/meepleAi-app/meepleai-monorepo/issues/1071)
- Sibling workstream: WS-F headers + dashboard (`docs/for-developers/testing/frontend/mockup-conformity.md`)
