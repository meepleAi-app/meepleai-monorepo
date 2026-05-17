# A11y Color-Contrast Restoration Audit

| Field | Value |
|---|---|
| Status | Phase 0 reconciliation complete; Phase A pending |
| Started | 2026-05-17 |
| Parent issue | [#1094](https://github.com/meepleAi-app/meepleai-monorepo/issues/1094) — restoration of `frontend-a11y` CI gate to blocking |
| Phase 0 sub-issue | [#1209](https://github.com/meepleAi-app/meepleai-monorepo/issues/1209) — this preface |
| Scope | All routes flagged by axe-core color-contrast rule (WCAG 2.1 AA, 4.5:1 normal / 3:1 large/non-text) |
| Trigger | Post-#1090 (DS-15/16 test re-alignment) CI run [25751692381](https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/25751692381) on commit `c449eab6b` |
| Owner | TBD per phase (see #1094 phases) |

## Document map

| Section | Status | Phase | Source |
|---|---|---|---|
| §0 Preface (this file head) | ✅ complete 2026-05-17 | Phase 0 (#1209) | this PR |
| §1 Per-node inventory | ⏳ pending | Phase A | sub-issue TBD |
| §2 Root-cause grouping | ⏳ pending | Phase B | sub-issue TBD |
| §3 Fix-pass tracking | ⏳ pending | Phase C | mini-PRs TBD |
| §4 Gate-flip post-mortem | ⏳ pending | Phase D | sub-issue TBD |

---

# §0 Preface — Phase 0 reconciliation

Phase 0 establishes ground truth BEFORE the per-node audit begins (Phase A). Its purpose is to resolve premise contradictions surfaced during the `/sc:spec-panel #1094` review on 2026-05-17 — specifically: why is the `frontend-a11y` job still non-blocking when its named blocker (`#752`) is already closed, and how does this work relate to the parallel `#1015` effort.

The findings below are derived from issue-history evidence; the "preliminary" qualifier means counts in the §0.1 origin table are populated later (Phase A) once per-node detail exists. The qualitative classifications are final.

## §0.1 Origin classification of the 159 violations

The most authoritative statement comes from the closing comment of **#752** itself, posted 2026-05-12T19:36:38Z (verbatim):

> Tracking issue opened: #1094 captures the multi-route color-contrast inventory (13 distinct UI states, ~159 violation nodes) and the 4-phase restoration plan. This issue (#752) can be closed — its original target was eliminated in DS-13/14/16, the residual was cleaned up in #1085, and the broader work is now scoped in #1094.

This tells us three things:

1. **#752 original target was eliminated in DS-13/14/16** — the `apps/web/src/components/v2/session-live/` directory (the original D.2 Foundation scope) no longer exists post token-canonicalization.
2. **Residual non-Foundation `text-slate-500` occurrences were cleaned up in PR [#1085](https://github.com/meepleAi-app/meepleai-monorepo/pull/1085)** (merged 2026-05-12T15:17:12Z, 7 source occurrences across `gamebook/StepIndicator.tsx`, `add-game-sheet/StepIndicator.tsx`, etc.).
3. **The 159 violations inventoried in #1094 are the *broader* color-contrast surface** that became visible AFTER #1085 + #1090 unblocked the `frontend-a11y` job to actually execute. They are not regressions; they are a previously-invisible long tail.

### Classification table

The 159 nodes split across four origin categories. Counts are populated in Phase A (per-node audit) by cross-referencing each row's git blame against the boundary commits below.

| Origin category | Definition | Boundary | Expected magnitude | Phase A count |
|---|---|---|---|---:|
| (a) DS-15 regression | New violation introduced AFTER PR #1085 by DS-15/16 token migration | Commit range: post-#1085 merge (2026-05-12T15:17:12Z) through #1090 merge | likely **0** — DS-15 enforced AA semantic tokens via `local/no-hardcoded-color-utility` lint rule | TBD |
| (b) #752 residue | Pre-existing `text-slate-500`-style violations NOT covered by #1085's narrow 7-file scope | Files unchanged by #1085 but using same shade families | likely **majority** — #1085 was a surgical fix, not an exhaustive sweep | TBD |
| (b') Post-#1085 residue | Violations on non-`text-slate-500` color pairs, surfaced by #1090 test re-alignment but never specifically tracked | Components outside #752 and #1085 scope that use other low-contrast pairs | likely **secondary chunk** | TBD |
| (c) Unrelated new surface | New routes/components added between #1085 and #1090 with their own violations | `git log --diff-filter=A` for `apps/web/src/components/**` and `apps/web/src/app/**` in that window | likely **small** | TBD |

**Decision**: the 159 violations are *predominantly* category **(b) + (b')** — pre-existing residue and adjacent surface. They are **not regressions from DS-15** (category a is expected zero). The full quantitative split is deferred to Phase A.

**Phase A directive**: when populating §1 per-node inventory, each row gets an additional `origin` column with one of {a, b, b', c}. Boundary classification reference:

- DS-15/16 token canonicalization spec: `docs/for-developers/specs/2026-05-12-token-canonicalization.md`
- DS-15 enforcement lint rule: `local/no-hardcoded-color-utility` (mode: **error** since DS-15, per CLAUDE.md §Active Freezes)
- #1085 narrow scope: see PR body for the 4 source files touched
- #1090 test re-alignment: see PR #1090 body for the 36 token-drift tests that gated the job

## §0.2 Supersede chain — `ci.yml:616-633` comment block

The `continue-on-error: true` on the `frontend-a11y` job in `.github/workflows/ci.yml` (today at line 640 — **identify by job name `frontend-a11y`, not by line number, the file drifts**) carries an inline rationale comment block at `ci.yml:616-633` that still names **#752** as the active blocker:

> Re-enabling requires resolving the residual dark-mode contrast violations tracked in #752 (text-slate-500 on dark surfaces — fails WCAG AA 4.5:1, ~20-36 nodes per state).

Since #752 is CLOSED and the broader scope is now tracked under **#1094**, this comment is stale and must be rewritten.

### Decision

**#1094 supersedes #752** as the named blocker for the `frontend-a11y` bypass. The rewrite is **not** executed in this Phase 0 PR — it is the responsibility of **Phase D.2** (the gate-flip PR at the end of the restoration). Doing it here would orphan the comment (claim "blocker = #1094" while #1094 is still open).

### Execution checklist (for the future Phase D PR)

When Phase D opens, the implementer MUST:

- [ ] Rewrite `ci.yml:616-633` to describe the unblock event as #1094 closure, removing the obsolete `text-slate-500` rationale.
- [ ] Update the NOTE comment at `ci.yml:740-745` ("frontend-a11y has continue-on-error: true at job level (line 600...)") — fix the obsolete line number OR rephrase to reference the job name (the latter is drift-proof and preferred).
- [ ] Identify the gate by job name (`frontend-a11y`), not by line number, when flipping `continue-on-error: false`.

## §0.3 Scope boundary with #1015

**#1015 (OPEN)** — *Frontend A11y E2E: baseline-diff gating per release* — was filed during the #979 release-PR triage. Its problem statement (verbatim from issue body):

> `Frontend - A11y E2E` check fails on release PR #979. Current behavior: axe-core E2E runs on every PR via Playwright a11y spec suite. Failure mode: any violation (NEW or pre-existing in main-dev) → workflow fail. This conflates **regression introduced by the PR** with **pre-existing debt tolerated in main-dev**.

**#1015 proposal**: baseline diff — snapshot violations from `main-staging` head, fail only if PR adds NEW ones; OR maintain an explicit allow-list of known pre-existing violations.

### Comparison

| Dimension | #1094 | #1015 |
|---|---|---|
| Goal | Absolute **zero** color-contrast violations on `main-dev` | Tolerate pre-existing debt; **delta-only** check per PR |
| Scope | Color-contrast rule only (WCAG 4.5:1 / 3:1) | All axe categories (focus order, ARIA roles, etc.) |
| Output | 0-violation baseline + blocking gate (`continue-on-error: false`) | Baseline-diff infrastructure (allow-list or snapshot comparison) |
| Timeline | 6-7h spread across Phase A-D | TBD, separate effort |
| Mechanism | Fix all violations, then flip the bypass | Add new CI step that diffs against baseline |

### Decision

**Complementary, not duplicative**:

- For the **color-contrast** category specifically, once #1094 reaches zero, the baseline-diff problem becomes trivial ("any new violation fails" = "absolute zero" + "delta-only check" converge).
- For **other axe categories** (focus order, ARIA roles, landmarks, etc.) where pre-existing debt may persist longer, #1015's baseline-diff infrastructure remains the right tool. It generalizes beyond a single category.

### Action

Once #1094 closes (post-Phase D), #1015 owner should decide whether to:

- (a) Close #1015 as superseded if all categories converge to zero, or
- (b) Scope #1015 down to non-color-contrast categories only, with #1094 as the named precedent for "absolute-zero" approach where feasible.

A cross-reference comment is posted on #1015 as part of this PR's AC (see AC0.2 in #1209).

## §0.4 Methodology note for Phase A

Phase A produces a per-node inventory with this schema (per #1094 hardened body):

```typescript
interface ViolationRow {
  route: string;          // e.g. "/library"
  state: string;          // matches inventory in #1094, e.g. "filtered-empty"
  viewport: 'desktop-chrome' | 'mobile-chrome';
  theme: 'light' | 'dark';
  selector: string;       // axe target CSS selector
  fgColor: string;        // computed foreground (rgb / rgba)
  bgColor: string;        // computed background (rgb / rgba)
  contrastRatio: number;  // measured ratio (e.g. 3.21)
  sharedComponent?: string; // best-guess shared parent (Phase B)
  origin?: 'a' | 'b' | "b'" | 'c'; // §0.1 classification (Phase B)
}
```

### Precedent — mathematical computation from token values

`docs/for-developers/audits/2026-05-12-dashboard-contrast.md` (2026-05-12, Dashboard restyle PR #1079 review) demonstrates a complementary methodology: **mathematical computation of contrast ratio from HSL token values** using sRGB linearization + WCAG 2.1 relative-luminance formula. This is preferred over runtime axe extraction when:

- The source `fgColor`/`bgColor` is a known design token (`--c-game`, `--bg-card`, etc.) — token values are deterministic, no manual color-picker measurement needed.
- The component composition is small enough to enumerate fg/bg pairs by hand (e.g. focus ring × 4 surfaces × 2 themes = 8 pairs).

For Phase A's 159 violations spanning 13 states, the runtime axe-extract path is more practical for breadth, but **whenever a violation's color pair resolves to a known token combination**, cross-check against the mathematical methodology in `2026-05-12-dashboard-contrast.md` to catch token-level systemic issues (e.g. a single token pair failing AA produces dozens of node violations on a single PR).

### Persistence policy

Per #1094 AC A.2, each Phase A axe run persists results in two forms:

- **GitHub Actions artifact**: `a11y-violations-${{ github.run_number }}.json` — ephemeral debug snapshot (90-day retention).
- **Committed markdown table** in §1 of this file — permanent reference for Phase B grouping and Phase C tracking.

The MD table is the source of truth; the JSON artifact is for delta debugging when re-runs diverge.

---

## §1 Per-node inventory

⏳ **Pending Phase A** — to be populated in a sub-issue derived from #1094 Phase A AC. The table headers below are the agreed schema; rows added by Phase A implementer.

| Route | State | Viewport | Theme | Selector | fgColor | bgColor | Ratio | sharedComponent | origin | Status |
|---|---|---|---|---|---|---|---:|---|---|---|
| _(populated by Phase A)_ | | | | | | | | | | |

## §2 Root-cause grouping

⏳ **Pending Phase B** — see #1094 Phase B AC (B.1 grouping by `sharedComponent`, B.2 fix-path taxonomy, B.3 effort breakdown per group).

## §3 Fix-pass tracking

⏳ **Pending Phase C** — see #1094 Phase C AC. Each fix PR updates §1 inventory rows with `Status: fixed in #<PR>`.

## §4 Gate-flip post-mortem

⏳ **Pending Phase D** — see #1094 Phase D AC. After the gate flips to blocking, this section documents the unblock event, the rewritten `ci.yml` comment, and the closure cross-references.

---

## References

- Parent issue: [#1094](https://github.com/meepleAi-app/meepleai-monorepo/issues/1094) — restoration of `frontend-a11y` CI to blocking
- This sub-issue: [#1209](https://github.com/meepleAi-app/meepleai-monorepo/issues/1209) — Phase 0 reconciliation
- Originally-named blocker: [#752](https://github.com/meepleAi-app/meepleai-monorepo/issues/752) (CLOSED 2026-05-12) — D.2 Foundation dark-mode contrast
- Narrow residual cleanup: [PR #1085](https://github.com/meepleAi-app/meepleai-monorepo/pull/1085) (MERGED 2026-05-12T15:17:12Z) — 7 `text-slate-500` source files migrated to `text-muted-foreground`
- Test re-alignment that surfaced the broader scope: [PR #1090](https://github.com/meepleAi-app/meepleai-monorepo/pull/1090) (MERGED 2026-05-12) — DS-15/16 token drift tests
- Complementary work: [#1015](https://github.com/meepleAi-app/meepleai-monorepo/issues/1015) (OPEN) — release-level baseline-diff gating
- DS-15/16 token canonicalization spec: `docs/for-developers/specs/2026-05-12-token-canonicalization.md`
- Precedent audit methodology: `docs/for-developers/audits/2026-05-12-dashboard-contrast.md`
- Originating CI run: [25751692381](https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/25751692381) on commit `c449eab6b`
