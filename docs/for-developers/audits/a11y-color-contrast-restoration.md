# A11y Color-Contrast Restoration Audit

| Field | Value |
|---|---|
| Status | Phase 0 + Phase A structural complete; **Phase A live-run is HARD BLOCKER for any Phase C work** (pivot 2026-05-17 — see §1.3 false-positive lesson). |
| Started | 2026-05-17 |
| Parent issue | [#1094](https://github.com/meepleAi-app/meepleai-monorepo/issues/1094) — restoration of `frontend-a11y` CI gate to blocking |
| Phase 0 sub-issue | [#1209](https://github.com/meepleAi-app/meepleai-monorepo/issues/1209) — preface |
| Scope | All routes flagged by axe-core color-contrast rule (WCAG 2.1 AA, 4.5:1 normal / 3:1 large/non-text) |
| Trigger | Post-#1090 (DS-15/16 test re-alignment) CI run [25751692381](https://github.com/meepleAi-app/meepleai-monorepo/actions/runs/25751692381) on commit `c449eab6b` |
| Owner | TBD per phase (see #1094 phases) |

## Document map

| Section | Status | Phase | Source |
|---|---|---|---|
| §0 Preface | ✅ complete 2026-05-17 | Phase 0 (#1209) | PR #1210 |
| §1.0 Methodology disclaimer | ✅ complete 2026-05-17 | Phase A structural | this PR |
| §1.1 Route × state matrix (12 routes × N states) | ✅ complete 2026-05-17 | Phase A structural | this PR |
| §1.2 Inventory cross-reference to #1094 counts | ✅ complete 2026-05-17 | Phase A structural | this PR |
| §1.3 Static-grep companion (suspected source components) | ✅ complete 2026-05-17 + ⚠️ **pivoted 2026-05-17** (false-positive lesson) | Phase A structural | PR #1212 + this PR (audit pivot) |
| §1.4 Per-node detail rows (selector / fg / bg / ratio) | ⏳ pending live axe run — **HARD PREREQUISITE** for Phase C | Phase A live | sub-issue (opening today) |
| §2.0 Grouping methodology | ✅ complete 2026-05-17 | Phase B prelim | PR #1212 |
| §2.1 Suspected clusters (from #1094 hints + static analysis) | ✅ complete 2026-05-17 + ⚠️ **C1 ruled out 2026-05-17** | Phase B prelim | PR #1212 + this PR |
| §2.2 Fix-path taxonomy per cluster | ✅ complete 2026-05-17 | Phase B prelim | PR #1212 |
| §2.3 Effort breakdown per cluster | ✅ complete 2026-05-17 + ⚠️ **revised down to 2-4 PRs** | Phase B prelim | PR #1212 + this PR |
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

### §1.0 Methodology disclaimer (Phase A — structural pass)

The `ViolationRow` schema (per #1094 hardened body and §0.4 above) requires per-node `selector` / `fgColor` / `bgColor` / `contrastRatio` data extracted from a **live axe-core run** across the dual-viewport × dual-theme matrix. That data is the **source-of-truth** for Phase B grouping and Phase C fix targeting.

A full live run was attempted in this PR (`feature/issue-1094-phase-a-audit`) and **deferred to a follow-up sub-issue** because the local execution path requires:

- Stopping the long-running `docker compose` web service on port 3000 (currently `meepleai-web` healthy ~18h), which is an interactive infrastructure change.
- Allowing Playwright to start its own dedicated Next.js dev server with `PLAYWRIGHT_AUTH_BYPASS=true` + `NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1` (the docker prod image does not carry these flags, so test fixtures like `[data-slot="library-hub-v2"]` never mount and tests fail at `waitForSelector` before reaching axe analysis).
- Sequential execution of ~13 spec files at workers=1 (axe race-condition mitigation per playwright.config.ts) → ~10-15 min per spec × 13 = 2-3h, before counting the dual-viewport/dual-theme multiplication.
- A clean test-environment setup (`.env.test` from template, build caches warm, no port collision).

**Therefore this PR delivers Phase A in two phases**:

- **§1.1 / §1.2 / §1.3 — Phase A structural** (this PR): route × state matrix from the 12 existing `e2e/a11y/*.spec.ts` spec files cross-referenced against the inventory in #1094 body, plus a static-grep companion identifying suspected source components by inspection of hardcoded color literals in `apps/web/src/`. This unblocks **Phase B preliminary grouping** (§2.1–§2.3 below) and gives an immediate fix-path hypothesis for Phase C planning.
- **§1.4 — Phase A live** (sub-issue TBD, recommended title `[a11y][#1094 Phase A.live] Run per-node axe-core extraction across dual-viewport × dual-theme matrix`): per-node detail rows populated from the live axe run, dual-viewport, dual-theme. Validates the §2.1 hypothesis and adjusts cluster boundaries before Phase C opens.

This sequencing is faithful to #1094's AC1 intent (per-node audit doc committed) while honoring the realistic constraint of a single working session.

### §1.1 Route × state matrix (from existing `e2e/a11y/*.spec.ts` specs)

The 12 spec files in `apps/web/src/e2e/a11y/` (verified via grep on `test.describe(`/`it(`) cover the following surfaces. Each row corresponds to a unique `(route, state)` combination already exercised by an axe-core call in CI. Columns:

- **Live-run target**: present if `test('axe-core: …')` invoked in the spec file
- **Viewport tested today**: `desktop-chrome` only (per `playwright.config.ts` — mobile-chrome is Phase A.4 expansion)
- **Theme tested today**: `light` default; `dark` only where the spec explicitly calls `page.emulateMedia({ colorScheme: 'dark' })` or navigates to a dark-fixture URL

| Route | State | Spec file | Live-run target? | Theme today | Mobile today |
|---|---|---|:---:|:---:|:---:|
| `/agents` | `default` | `agents-index.spec.ts:45` | ✅ | light | ❌ |
| `/agents` | `filtered-empty` | `agents-index.spec.ts:64` | ✅ | light | ❌ |
| `/agents/[id]` | `default` | `agent-detail.spec.ts:67` | ✅ | light | ❌ |
| `/agents/[id]` | `not-found` | `agent-detail.spec.ts:91` | ✅ | light | ❌ |
| `/games` (catalog) | `default` | `games-library.spec.ts:45` | ✅ | light | ❌ |
| `/games` (catalog) | `filtered-empty` | `games-library.spec.ts:64` | ✅ | light | ❌ |
| `/library/games/[id]` | `default` | `game-detail.spec.ts:57` | ✅ | light | ❌ |
| `/library/games/[id]` | `not-found` | `game-detail.spec.ts:81` | ✅ | light | ❌ |
| `/gamebooks` | inferred (loading / quota-soft / quota-hard / empty-photos) | `gamebook-index.spec.ts` | ✅ (subset) | light | ❌ |
| `/gamebooks/upload` | `step1-default` | `gamebook-upload.spec.ts:85` | ✅ | light | ❌ |
| `/gamebooks/upload` | `step1-no-results` | `gamebook-upload.spec.ts:89` | ✅ | light | ❌ |
| `/gamebooks/upload` | `step2-ready` (placeholder) | `gamebook-upload.spec.ts:93` | ✅ | light | ❌ |
| `/gamebooks/upload` | `step2-denied` (placeholder) | `gamebook-upload.spec.ts:97` | ✅ | light | ❌ |
| `/gamebooks/upload` | `step3-progress` (placeholder) | `gamebook-upload.spec.ts:101` | ✅ | light | ❌ |
| `/gamebooks/upload` | `step3-cancel-modal` (placeholder) | `gamebook-upload.spec.ts:105` | ✅ | light | ❌ |
| `/library` | `default` | `library.spec.ts:44` | ✅ | light | ❌ |
| `/library` | `filtered-empty` | `library.spec.ts:63` | ✅ | light | ❌ |
| `/players` | `default` | `players-index.spec.ts:50` | ✅ | light | ❌ |
| `/players` | `filtered-empty` | `players-index.spec.ts:69` | ✅ | light | ❌ |
| `/players/[id]` | `default` | `player-detail.spec.ts:57` | ✅ | light | ❌ |
| `/players/[id]` | `not-found` | `player-detail.spec.ts:81` | ✅ | light | ❌ |
| `/sessions` | `default` | `sessions-index.spec.ts:56` | ✅ | light | ❌ |
| `/sessions` | `filtered-empty` | `sessions-index.spec.ts:76` | ✅ | light | ❌ |
| `/sessions/[id]/live` | `dark-default` | `session-live.spec.ts:71` | ✅ | **dark** | ❌ |
| `/sessions/[id]/live` | `loading` | `session-live.spec.ts:92` | ✅ | light | ❌ |
| `/sessions/[id]/live` | `not-found` | `session-live.spec.ts:113` | ✅ | light | ❌ |
| `/sessions/[id]/live` | `pause-overlay-open` | `session-live.spec.ts:374` | ✅ | light | ❌ |
| `/sessions/[id]/live` | `endgame-dialog-open` | `session-live.spec.ts:403` | ✅ | light | ❌ |
| `/sessions/[id]/summary` | `default` | `session-summary.spec.ts:86` | ✅ | light | ❌ |
| `/sessions/[id]/summary` | `tied-podium` | `session-summary.spec.ts:101` | ✅ | light | ❌ |
| `/sessions/[id]/summary` | `empty-photos` | `session-summary.spec.ts:117` | ✅ | light | ❌ |
| `/sessions/[id]/summary` | `empty-achievements` | `session-summary.spec.ts:130` | ✅ | light | ❌ |
| `/sessions/[id]/summary` | `diary-filter` | `session-summary.spec.ts:145` | ✅ | light | ❌ |
| `/sessions/[id]/summary` | `ShareCard-dark-preview` | `session-summary.spec.ts:163` | ✅ | **dark** | ❌ |

**Total live-run targets today**: ~30 `(route, state)` combinations across 12 routes. **Phase A.4** doubles this on the mobile-chrome viewport (~60) and again on dark theme (~60). Estimated full Phase A live matrix: **~120 axe-core runs** if mobile-chrome is wired into `test:a11y:e2e`.

### §1.2 Cross-reference to #1094 inventory counts

#1094 body inventoried 13 state names × ~159 node violations. The mapping to spec coverage:

| #1094 state name | Instances (body) | Spec file(s) covering this state | Gap? |
|---|---:|---|---|
| `default` | 19 | 10 spec files cover their own `default` state (agents-index, agent-detail, games-library, game-detail, library, players-index, player-detail, sessions-index, session-live `dark-default`, session-summary) | ⚠️ Inventory count of 19 suggests `default` is exercised across multiple **panels-within-page** that each emit their own state instance. Live run will disambiguate. |
| `filtered-empty` | 12 | 4 spec files (agents-index, games-library, library, sessions-index) | ✅ Aligned (~3 nodes per instance avg per body distribution) |
| `pause-overlay` | 6 | session-live (1 spec, line 374 `pause-overlay-open` state) | ✅ Single source |
| `not-found` | 6 | 4 spec files (session-live, agent-detail, game-detail, player-detail) | ✅ Aligned |
| `loading` | 6 | gamebook-index (per body) + `session-live.spec.ts:92` | ✅ Multi-source |
| `endgame-dialog` | 6 | 2 spec files (session-summary, session-live) | ✅ Aligned |
| `tied` | 3 | session-summary (1 spec, line 101) | ✅ Single source |
| `empty-photos` | 3 | gamebook-index (per body) AND/OR session-summary line 117 | ⚠️ #1094 attributes to `gamebook-index`; spec line 117 attributes to session-summary. **Discrepancy** — live run resolves. |
| `empty-achievements` | 3 | #1094 attributes to `player-detail`; spec line 130 of `session-summary.spec.ts` exercises this state | ⚠️ **Discrepancy** — #1094 inventory likely has misattribution. Live run resolves. |
| `step1-default` | 1 | gamebook-upload line 85 | ✅ Single source |
| `step1-no-results` | 1 | gamebook-upload line 89 | ✅ Single source |
| `quota-soft` | 1 | gamebook-index (per body) | ⏳ Spec inspection needed |
| `quota-hard` | 1 | gamebook-index (per body) | ⏳ Spec inspection needed |

**Discrepancy log** (for Phase A live run to confirm/correct):
- `empty-photos` attribution: gamebook-index vs session-summary.
- `empty-achievements` attribution: player-detail vs session-summary.

These were flagged to Phase 0 by the `/sc:spec-panel #1094` review (CRIT-1 reconciliation). Phase A live run output overrides the #1094 body numbers as source of truth.

### §1.3 Static-grep companion (suspected source components)

> ⚠️ **Lesson learned (2026-05-17, post §1.3 v1 dispatch)**: the static-grep heuristic below is **too coarse to predict color-contrast violations** without a live axe run. A direct inspection of the §1.3 v1 highest-confidence candidate revealed a **false positive**:
>
> The 3 hardcoded `text-slate-[1-9]00` occurrences in `SessionLiveView.tsx` (lines 188, 219, 1007) are all `text-slate-100` or `text-slate-200` — **light** text — used **on a dark background** (`bg-[hsl(240,40%,8%)]`, the explicit `data-theme="dark"` session-live root, plus inherited dark bg for nested error/not-found shells). Mathematical contrast: `text-slate-100` (#f1f5f9) vs dark `hsl(240,40%,8%)` (~#0a0b1f) ≈ **14:1**, well above WCAG AA 4.5:1. Same math for `text-slate-200` (#e2e8f0) on the same bg ≈ **13:1**. These are **compliant**, not violations.
>
> **Root cause of the false positive**: the grep regex `text-(slate|gray|...)-[1-9]00` matches the full 100–900 shade range, but the dark shades (100–300) are the *correct* DS-15 choice for text on dark surfaces, not a debt pattern. A discriminating heuristic would require ALSO knowing the rendered background — which is unobtainable from static analysis alone.
>
> **Consequence for Phase B/C**: the §2.1 "C1 session-live single-file" cluster is **downgraded to 🔴 RULED OUT pending live axe data**. The component family may still have other violations (`text-muted-foreground` resolved against an unexpected bg, for example), but **the static-grep signal does not predict them**.
>
> **Phase A.live is therefore promoted from "complement" to HARD PREREQUISITE for any Phase C kickoff**. No Phase C PR ships without per-node axe data anchoring the fix target.

The original §1.3 v1 table is preserved below for historical traceability and to feed back into Phase A.live methodology (when running live axe, **invert** the regex: filter axe results by component path and check whether any of these hardcoded-color files actually appears in `sharedComponent` — that's the cross-check between static suspicion and live evidence).

| Component / file | Hardcoded text-color count (any shade) | Inventory-relevant route(s) | Status after 2026-05-17 inspection |
|---|---:|---|---|
| `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` | 3 (all `text-slate-100`/`text-slate-200` on dark bg) | `/sessions/[id]/live` | ❌ **false positive** — 3/3 occurrences AA-compliant (light text on dark surface, ~13-14:1) |
| `apps/web/src/app/(authenticated)/players/[id]/achievements/page.tsx` | 1 | `/players/[id]` subroute | ⏳ NOT yet inspected — apply same fg/bg check before Phase C |
| `apps/web/src/app/(authenticated)/players/[id]/sessions/page.tsx` | 1 | `/players/[id]` subroute | ⏳ NOT yet inspected — same caveat |
| `apps/web/src/app/(authenticated)/library/proposals/MyProposalsClient.tsx` | 1 | `/library` adjacent | ⏳ NOT yet inspected |
| `apps/web/src/app/(authenticated)/toolkit/play/page.tsx` | 1 | `/toolkit/play` (NOT in inventory) | Out of scope |
| Admin pages (16 files, 40+ counts) | 40+ | Admin routes (NOT in inventory) | Out of scope (separate a11y effort) |

**Revised key observation**: the static-grep signal is **necessary but not sufficient** to identify violations. A surviving `text-slate-X` (any shade) outside the DS-15 token system is *evidence of debt* (the file carries an `// eslint-disable local/no-hardcoded-color-utility` directive), but only the live axe run determines whether that debt manifests as a *measured AA failure*. Phase C must target *measured* failures, not *suspected* debt.

**Lint rule behavior note (unchanged)**: `local/no-hardcoded-color-utility` is set to `error` in `apps/web/eslint.config.mjs` since DS-15. The 50+ surviving violations across `apps/web/src/` therefore carry file-level `// eslint-disable local/no-hardcoded-color-utility` directives. Cleaning these up is a **DS-16 codemod concern** (per CLAUDE.md §Active Freezes), independent of #1094. Phase C should NOT bundle generic eslint-disable cleanup with axe-failure fixes — keep the two concerns separate to preserve atomicity.

### §1.4 Per-node detail rows (live axe-core run)

⏳ **Pending Phase A live-run sub-issue** (TBD). When opened, that sub-issue MUST:

1. Wire a one-shot script (`apps/web/scripts/phase-a-color-contrast-audit.mjs` or equivalent) that runs axe-core against each `(route, state)` in §1.1 above, filters `color-contrast` rule only, and dumps per-node `ViolationRow` records to `apps/web/audits/phase-a-color-contrast-runtime.json`.
2. Append the rows to §1.4 of this doc as a single markdown table, sorted by `sharedComponent` (or by route+state if no sharedComponent inferable).
3. Resolve the §1.2 discrepancy log (empty-photos / empty-achievements attribution) explicitly.
4. Extend the live run to **mobile-chrome** viewport and **dark** theme matrix (Phase A.4 in #1094 hardened body).
5. Cross-validate §1.3 hypothesis: every component in §1.3 should show up in the per-node rows OR the discrepancy is documented.

Recommended branch pattern: `feature/issue-{phase-a-live-N}-phase-a-live-axe-run`.

| Route | State | Viewport | Theme | Selector | fgColor | bgColor | Ratio | sharedComponent | origin | Status |
|---|---|---|---|---|---|---|---:|---|---|---|
| _(populated by Phase A live-run sub-issue)_ | | | | | | | | | | |

## §2 Root-cause grouping (Phase B preliminary)

### §2.0 Grouping methodology

This is a **preliminary** grouping derived from §1.1 (route × state matrix) + §1.2 (#1094 count attribution) + §1.3 (static-grep companion). Once §1.4 live run lands, cluster boundaries are refined and re-validated against per-node `sharedComponent` data. Until then, the clusters below are **hypotheses** that drive Phase C planning under explicit "subject to live-run confirmation" caveats.

The clustering rules used here:

1. **Single-file-source**: if §1.3 shows one component file with >2 hardcoded colors AND that file is on the render path of multiple inventory states, it's its own cluster (high ROI).
2. **Modal/overlay shared backdrop**: states whose name implies overlay (`pause-overlay`, `endgame-dialog`) are clustered together — the WCAG violation typically lives in the backdrop/title fg/bg combo, shared between dialog variants.
3. **EmptyZone-family**: states whose name implies "no rows / no results" (`filtered-empty`, `not-found`, `empty-photos`, `empty-achievements`) likely share an `<EmptyZone>` or `<MiniNav>` primitive across routes — biggest potential per-fix leverage but also highest uncertainty without live data.
4. **Per-route catch-all**: routes whose `default` / `loading` states dominate the inventory count and don't fit clusters 1-3.

### §2.1 Suspected clusters

> ⚠️ **Critical addendum (2026-05-17)**: §1.3 inspection revealed that the C1 cluster's static-grep signal is a **false positive** (see §1.3 yellow box above). C1 is downgraded from 🟢 high to 🔴 RULED OUT pending live data. The other clusters (C2–C5) were already 🟡 medium-confidence — their fate also depends on Phase A.live data. **Phase C cannot kick off any PR until Phase A.live lands per-node `sharedComponent` data**. The matrix below preserves cluster boundaries as *Phase A.live discriminators* (each row becomes a question to answer with live data), not as fix-ship-ready targets.

| Cluster ID | Name | Suspected sharedComponent | Routes affected | #1094 states covered | Estimated node attribution | Status post-2026-05-17 |
|---|---|---|---|---|---:|---|
| **C1** | session-live single-file | `apps/web/src/app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx` | `/sessions/[id]/live` | `dark-default`, `loading`, `not-found`, `pause-overlay-open`, `endgame-dialog-open` (5 states) | ~24-30 nodes (per #1094 distribution) | 🔴 **RULED OUT** — static-grep hits are all compliant light-on-dark (~13-14:1). If session-live has real violations, root cause is elsewhere (likely nested components or color computed from CSS vars at runtime). Phase A.live answers. |
| **C2** | modal/overlay shared backdrop | TBD — likely a shared `<DialogOverlay>` / `<BackdropScrim>` primitive used by `PauseOverlay` and `EndgameDialog` (Radix UI primitive variants) | `/sessions/[id]/live` (and possibly `/sessions/[id]/summary` if endgame-dialog attribution overlaps) | `pause-overlay`, `endgame-dialog` | ~12 nodes (6 + 6) | 🟡 medium (unaffected by C1 downgrade — independent hypothesis). Live run disambiguates. |
| **C3** | EmptyZone family across catalog routes | TBD — possibly a shared `<EmptyZone>` / `<MiniNav>` primitive used by `filtered-empty` and `not-found` across catalog index routes | `/agents`, `/games`, `/library`, `/sessions`, `/players`, plus `*-detail` `not-found` states | `filtered-empty` (×12), `not-found` (×6), `default` (partial) | ~18-30 nodes (12 + 6 + partial) | 🟡 medium (unaffected by C1 downgrade). Highest potential leverage IF a single primitive is the culprit. |
| **C4** | session-summary partial-empty states | TBD — likely `session-summary` `_components/` family (e.g. `PhotosSection`, `AchievementsSection`) | `/sessions/[id]/summary` | `tied`, `empty-photos`, `empty-achievements` (if not gamebook or player as #1094 inventory suggests), `diary-filter` | ~9-12 nodes (3 + 3 + 3 + partial) | 🟡 medium |
| **C5** | gamebook routes residual | TBD — `gamebook-index` and `gamebook-upload` may share a loader/quota-banner primitive | `/gamebooks`, `/gamebooks/upload` | `step1-default`, `step1-no-results`, `quota-soft`, `quota-hard`, `loading` (partial), `empty-photos` (if gamebook not session-summary) | ~5-8 nodes (1+1+1+1 + partials) | 🟡 medium (small, low priority) |

**Total estimated coverage post-C1-downgrade**: ~46-60 of 159 nodes covered by C2-C5 hypotheses. Remaining ~99+ nodes (including all the 24-30 previously attributed to C1) are now **unattributed** until Phase A.live lands per-node data.

**Revised Phase C kickoff blocker**: NO Phase C PR opens until §1.4 live data exists. The previous statement that "C1 ships first as low-risk validator" is **retracted** — the C1 fix would have been a no-op (light text on dark surfaces is already compliant).

### §2.2 Fix-path taxonomy per cluster (Phase B.2)

Per #1094 hardened body B.2, three fix paths exist. Recommendation per cluster:

| Cluster | Recommended fix path | Rationale |
|---|---|---|
| **C1 session-live single-file** | **(a) Replace hardcoded color with semantic token** — swap the 3 `text-slate-X-[400-500]` (or similar) in `SessionLiveView.tsx` for `text-muted-foreground` / `text-foreground-muted`. Remove `// eslint-disable local/no-hardcoded-color-utility` if present. | Lowest-effort, single file, lint-rule-aligned. |
| **C2 modal/overlay shared backdrop** | **(b) Refactor shared component to expose token slot** — if violation is in the Radix overlay's default classes, wrap in our `<DialogOverlay>` primitive that injects token colors. | One refactor unblocks both PauseOverlay and EndgameDialog. |
| **C3 EmptyZone family** | **(a) preferred, (b) fallback** — start with (a) on the suspected shared primitive (`EmptyZone` / `MiniNav` once §1.4 confirms it exists); fall back to (b) if multiple primitives share the offending pattern. | Highest leverage IF (a) works; bail to (b) early if §1.4 shows N>2 distinct components contribute. |
| **C4 session-summary partial-empty** | **(a) Replace hardcoded color** — likely small, isolated changes in 2-3 `_components/` files within `session-summary`. | Auto-contained to one route. |
| **C5 gamebook residual** | **(c) Per-surface override (last resort)** — small node count + likely surface-specific (quota banners are typically branded). | Don't over-engineer; small enough that per-surface overrides are acceptable. |

### §2.3 Effort breakdown per cluster (Phase B.3) + PR boundary

| Cluster | Estimated LOC delta | Estimated effort | PR boundary recommendation |
|---|---:|---:|---|
| ~~**C1**~~ | — | — | ❌ **REMOVED** (2026-05-17 inspection) — static-grep hits are AA-compliant. No standalone Phase C PR for session-live unless §1.4 surfaces actual violations there. |
| **C2** | 20-40 LOC | 1 h | **PR candidate** — opens after §1.4 confirms PauseOverlay/EndgameDialog actually have measured color-contrast failures in their backdrop/title combo. |
| **C3** | 20-60 LOC depending on whether ≥2 shared primitives | 1.5-2 h | **PR candidate** — highest variance — open AFTER §1.4 live run confirms shared-primitive count (single primitive vs N>2). Bundle with C4 only if size stays <500 LOC per CLAUDE.md guidance. |
| **C4** | 10-30 LOC | 45 min | **PR candidate** (or bundled into C3 if same route family). |
| **C5** | 5-15 LOC | 30 min | **PR candidate** (small, opportunistic). |

**Revised Total Phase C estimated**: **2-4 PRs** (down from 4-5; C1 removed). Effort 3-4h cumulative. In line with #1094 hardened body estimate of "2-4 PRs".

**Critical sequencing (post-2026-05-17 pivot)**:

1. ✅ **Phase A.live sub-issue** is **THE single blocker** for any Phase C kickoff (see §1.4 checklist for sub-issue requirements). Static-grep alone cannot identify fix targets reliably (C1 demonstrated this).
2. **No Phase C PR before §1.4 lands.** All cluster boundaries (C2–C5) are hypotheses pending live data.
3. **Phase C PR ordering** after §1.4: pick the cluster with highest measured node count AND lowest LOC delta. Bundle small adjacent clusters when same route family.
4. **Final acceptance** — `pnpm test:a11y:e2e` returns 0 violations across the full dual-viewport × dual-theme matrix → open Phase D sub-issue.

### §2.4 Open questions for Phase A.live to resolve

1. **C2 shared primitive**: does PauseOverlay / EndgameDialog actually share a `<DialogOverlay>` primitive, or each ship its own backdrop styles? Live `sharedComponent` data answers this.
2. **C3 shared EmptyZone**: does `filtered-empty` across `/agents`, `/games`, `/library`, `/sessions`, `/players` share a single primitive, or are there N>2 distinct empty-state components? This dictates PR #3 vs N mini-PRs.
3. **§1.2 discrepancies**: empty-photos and empty-achievements attribution (gamebook-index vs session-summary; player-detail vs session-summary).
4. **Mobile-chrome delta**: does the mobile viewport surface NEW color-contrast violations not present on desktop (e.g. responsive collapsed nav fg/bg)? Phase A.4 matrix answers this.
5. **Dark theme delta**: does dark theme expose violations that light theme masks (e.g. semi-transparent overlays read very differently on `#14100a` dark vs `#f7f3ee` light)? Most of §1.1 above today only exercises `light` — `dark-default` exists only for session-live and ShareCard preview.

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
