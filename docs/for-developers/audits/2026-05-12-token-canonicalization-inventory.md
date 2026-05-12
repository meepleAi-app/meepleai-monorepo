# Token Canonicalization Inventory — Stage 3 cluster prioritization

| Field | Value |
|---|---|
| **Date** | 2026-05-12 |
| **Stage** | DS-3 (cluster prioritization) |
| **Spec** | [`2026-05-12-token-canonicalization.md`](../specs/2026-05-12-token-canonicalization.md) |
| **Generator** | `pnpm lint:tokens` against `main-dev` HEAD `01ef5851e..f3702921d` (post DS-1 merge: bridge active, but consumers not yet migrated) |
| **Companion JSON** | [`audits/2026-05-12-token-violations.json`](../../../audits/2026-05-12-token-violations.json) (regenerable, gitignored) |
| **Source rule** | `local/no-hardcoded-color-utility` (DS-2) |

## Method

1. Auto-generated cluster list from `pnpm lint:tokens` heuristic (top-level groups under `apps/web/src/`).
2. **Manual re-aggregation** for cluster boundaries — the auto-heuristic split too aggressively (308 buckets, many single-file). This document collapses related sub-clusters into the canonical DS-N stage assignment.
3. Effort estimate per stage based on violations count, cross-cluster dependency, and visual-diff complexity (cards/heroes vs background-only).

## Summary stats

| Group | Violations | Files | % of total | Stage(s) |
|-------|-----------:|------:|-----------:|----------|
| **admin/** | 2310 | ~150 | 40.5% | DS-13a..d (NEW) |
| **ui/** primitives | 519 | ~40 | 9.1% | DS-12 (NEW) |
| **session*** | 511 | ~60 | 9.0% | DS-5, DS-6 |
| **app/(authenticated)/** routes (non-admin, non-library) | 350 | ~30 | 6.1% | DS-4, distributed |
| **library*** | 256 | ~25 | 4.5% | DS-11 |
| **features/** | 251 | ~30 | 4.4% | DS-7, DS-8, DS-10 |
| **toolkit-drawer/** | 147 | ~12 | 2.6% | DS-14 (NEW — secondary) |
| **stories/** (Storybook) | 128 | ~15 | 2.2% | DS-14 (NEW — secondary) |
| **agent/** (mixed) | 117 | ~10 | 2.0% | DS-8 |
| **collection/** | 93 | ~10 | 1.6% | DS-11 |
| **game-night/** | 81 | ~8 | 1.4% | DS-7 |
| Other (≤70 ea.) | 947 | ~340 | 16.6% | DS-14 (NEW), manual-review |
| **TOTAL** | **5710** | **729** | 100% | — |

## Stage assignment (canonical, supersedes spec §3 heuristics)

### Tier 1 — high-traffic user surfaces (DS-4..DS-11)

| Stage | Cluster(s) | Violations | Files | Effort | Notes |
|-------|-----------|-----------:|------:|--------|-------|
| **DS-4** | `app/(authenticated)/dashboard/*` + `dashboard/*` | 45 | ~5 | 0.5d | Landing surface — high visibility, low blast radius |
| **DS-5** | `features/sessions/*` + `session/*` (excl. live) | ~330 | ~25 | 1d | Includes `session/Scoreboard.tsx` (44), `session/WhiteboardTool.tsx` (58) |
| **DS-6** | `features/session-live/*` + `features/session-summary/*` + `session/live/*` | ~250 | ~15 | 1d | A11y critical, touches dark UI surfaces |
| **DS-7** | `features/games/*` + `features/game-detail/*` + `app/(authenticated)/games/*` + `game-night/*` | ~150 | ~15 | 1d | Catalog + event UI |
| **DS-8** | `features/agents/*` + `features/agent-detail/*` + `agent/config/*` + `agent/settings/*` | ~150 | ~15 | 1d | Wave B.2 / γ surfaces |
| **DS-9** | `features/players/*` + `features/player-detail/*` | ~80 | ~10 | 0.5d | Wave 3 pending |
| **DS-10** | `features/gamebook/*` + `features/game-chat/*` + `chat-unified/*` | ~250 | ~25 | 1.5d | SP6 — large surface, custom drawer/wizard UI |
| **DS-11** | `library/*` + `app/(authenticated)/library/*` + `collection/*` | ~370 | ~35 | 1.5d | Add-game wizard, multi-tab library |

**Subtotal**: ~1625 violations, ~145 files, **~7 days**.

### Tier 2 — primitives (DS-12, NEW — promoted from "final bridge removal")

| Stage | Cluster | Violations | Files | Effort |
|-------|---------|-----------:|------:|--------|
| **DS-12** | `ui/data-display/meeple-card/*` + `ui/data-display/extra-meeple-card/*` + `ui/data-display/entity-list-view/*` + other `ui/*` | 519 | ~40 | 2d |

**Notes**:
- The legacy bridge layer can **not** be removed until DS-12 lands — primitives are the deepest consumers and many `--mc-*` tokens have no alias yet (intentionally left out of `token-bridge.css`).
- DS-12 was the bridge-removal stage in the original spec; the bridge-removal step has been renumbered to **DS-15**.

### Tier 3 — admin scope (NEW, DS-13a..d)

The original spec scoped DS-4..DS-11 to **user-facing features only**. The DS-2 inventory revealed admin consumes 40% of all violations (2310). Per user request 2026-05-12, admin is now in scope.

| Stage | Cluster | Violations | Files | Effort | Notes |
|-------|---------|-----------:|------:|--------|-------|
| **DS-13a** | `app/admin/(dashboard)/*` | 1269 | ~50 | 3d | Highest-violation cluster; many dashboards/widgets with custom palettes |
| **DS-13b** | `admin/knowledge-base/*` | 314 | ~25 | 1.5d | RAG admin, processing-metrics, upload-zone — high custom-styling density |
| **DS-13c** | `admin/shared-games/*` (129) + `admin/games/*` (70) + `admin/agents/*` (83) + `admin/users/*` (75) | 357 | ~30 | 1.5d | CRUD tables/forms, similar patterns |
| **DS-13d** | `admin/command-center/*` (54) + `admin/mechanic-extractor/*` (52) + `admin/sandbox/*` (34) + `admin/infrastructure/*` (21) + remaining single-file admin/* | ~370 | ~30 | 1.5d | Long tail of admin tools |

**Subtotal**: 2310 violations, ~135 files, **~7.5 days**.

### Tier 4 — secondary / orphan (DS-14, NEW)

| Stage | Clusters | Violations | Files | Effort |
|-------|----------|-----------:|------:|--------|
| **DS-14** | `toolkit-drawer/*` (147) + `toolkit/*` (56) + `stories/*` (128) + `layout/*` (68) + `errors/*` (52) + `modals/*` (33) + `loading/*` (58) + `empty-state/*` (28) + `editor/*` (24) + `legal/*` (23) + `pdf/*` (21) + `auth/*` (33) + `chat-unified/*` (already in DS-10 — exclude) + remaining single-file | ~600 | ~80 | 2.5d |

**Notes**:
- Storybook stories (`stories/Animations.stories.tsx` 79 violations etc.) are deferred to DS-14 because their visual diff is decoupled from production routes.
- Single-file orphan clusters (`session/WhiteboardTool.tsx`, `loading/SkeletonLoader.tsx`, etc.) are absorbed into the nearest related Tier 1 cluster when possible, otherwise DS-14.

### Tier 5 — bridge removal & lint→error (renumbered DS-15)

| Stage | Action | Effort |
|-------|--------|--------|
| **DS-15** | Delete `token-bridge.css`, delete `premium-gaming.css`, prune `design-tokens.css` legacy declarations, flip lint rule to `error`, add `--max-warnings 0` to `lint:tokens` CI step. | 0.5d |

## Revised sequencing

| Stage | PR # | Status | Effort | Parallel? |
|-------|------|--------|--------|-----------|
| DS-1 | #1044 | open | 1d | — |
| DS-2 | #1045 | open | 0.5d | — |
| DS-3 | **this PR** | draft | 0.5d | — |
| DS-4..DS-11 | — | pending | ~7d | YES (8 PRs) |
| DS-12 | — | pending | 2d | independent |
| DS-13a..d | — | pending | ~7.5d | YES (4 PRs after DS-1+DS-2) |
| DS-14 | — | pending | 2.5d | YES |
| DS-15 | — | pending | 0.5d | NO — final |

**Total revised**: **~21 days** with 1 dev FTE, or **~10 days** with 2-3 dev parallel on Tiers 1+3+4.

## Manual-review items (require designer/architect sign-off)

These items emerged during the heuristic aggregation and need a human decision:

1. **`app/admin/(dashboard)` cluster (1269 violations)** — many of these are inline `bg-gradient-to-r from-X-500 to-Y-700` patterns where the hue is admin-decorative (not entity-semantic). Some may need a new `--admin-*` token family OR they should use existing `--brand`. Decision in DS-13a kickoff.

2. **`stories/Animations.stories.tsx` (79 violations)** — Storybook-only file. Decision: lint suppression (Storybook stories are documentation, not production surfaces) or migration to canonical?

3. **`ui/data-display/meeple-card/*` (~150 of the 519 ui/* total)** — `MeepleCard` uses a `--mc-*` token family that intentionally has NO bridge alias (legacy palette being phased out). DS-12 must produce a mapping decision: alias to canonical OR redesign card chrome.

4. **`session/WhiteboardTool.tsx` (58 single-file)** — large standalone tool. Could be its own DS sub-stage or rolled into DS-5. Recommendation: roll into DS-5 if it shares card patterns; otherwise DS-14.

5. **`loading/SkeletonLoader.tsx` (54 single-file)** — shimmer color stops likely hardcoded. May need a new `--skeleton-*` token in the canonical layer. DS-14 manual-review.

## Per-cluster file count tail

The full byCluster JSON has 308 buckets; the long tail (215 buckets with ≤2 violations) is suppressed here. Refer to [`audits/2026-05-12-token-violations.json`](../../../audits/2026-05-12-token-violations.json) for the raw inventory.

## Acceptance criteria

- [x] AC3.1: 100% file with violations classified to a stage (no `manual` cluster in heuristic output after this doc lands).
- [x] AC3.2: Stage order documented with rationale (traffic, blast radius, dependencies).
- [x] AC3.3: Admin scope decision recorded (in-scope, 4 sub-stages).
- [ ] AC3.4: Spec markdown updated with revised stage numbers and effort (companion change in DS-3 PR).

## Next actions

1. Land this PR with the inventory doc.
2. **Spec update deferred to a follow-up PR after DS-1 (#1044) merges** — the spec markdown (`docs/for-developers/specs/2026-05-12-token-canonicalization.md`) is currently owned by the DS-1 branch and would conflict if edited here. A small doc-only PR will revise §3 (stage plan) and §5 (sequencing) once DS-1 lands on `main-dev`.
3. Update `v2-migration-matrix.md` with a `Token compliance` column (referenced by DS-4+ PR templates).
4. Open the first cluster PR (DS-4 — dashboard) as the canonical migration template.

## References

- Parent spec: [`2026-05-12-token-canonicalization.md`](../specs/2026-05-12-token-canonicalization.md)
- De-versioning spec: [`2026-05-11-design-system-deversioning.md`](../specs/2026-05-11-design-system-deversioning.md)
- Mockup conformity audit (Stage 1): [`2026-05-11-mockup-conformity.md`](./2026-05-11-mockup-conformity.md)
- Bridge map: [`../frontend/token-bridge-map.md`](../frontend/token-bridge-map.md)
