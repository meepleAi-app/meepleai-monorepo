# Tier Components Ecosystem Audit (Gate G0.2)

**Audit Date:** 2026-04-09
**Gate Owner:** @user
**Purpose:** Reframe GitHub issue meepleAi-app/meepleai-monorepo#291 from "integrate orphans" to "consolidate tier kit"

## Executive Summary

This audit identifies **three distinct overlapping implementations of progress-bar-based quota visualization** and **two competing upgrade CTA patterns** across the meepleAI web app tier-gating system. Components were developed in parallel during Epic #4068 (tier-gate UX kit) and Game Night Improvvisata E2-4/E2-5 without integration planning.

**Key finding:** The most mature, production-deployed implementation is **UsageWidget + QuotaRow** in the library sidebar. Newer "canonical" tier components were built in parallel (UsageMeter, UpgradeCta, UsageSummary) creating redundancy. Meanwhile, orphaned UI feedback components (CollectionProgressBar, UpgradePrompt) have **richer feature sets** than production code.

## Component Inventory

### `components/tier/`

| Component | LOC | Props | Consumers | Status |
|---|---|---|---|---|
| `UsageMeter.tsx` | 88 | `{ label, current, max, className? }` | UsageSummary (orphan) + test | **DEPRECATE** |
| `UpgradeCta.tsx` | 74 | `{ limitType, current, max, className? }` | UsageSummary (orphan) | **DEPRECATE** |
| `UsageSummary.tsx` | 124 | `{ className? }` | **NONE** (exported but never imported) | **DEPRECATE** |
| `PricingCard.tsx` | 130 | `{ name, price, features[], isCurrent?, isPopular?, ... }` | **NONE** | KEEP (awaits `/pricing` page) |

### `components/ui/feedback/`

| Component | LOC | Props | Consumers | Status |
|---|---|---|---|---|
| `collection-progress-bar.tsx` | 132 | `{ current, max, label, unit?, showWarning?, className? }` | CollectionLimitIndicator (orphan) + showcase | **DEPRECATE** |
| `upgrade-prompt.tsx` | 183 | `{ requiredTier, featureName?, variant: 'inline'\|'modal', onUpgrade?, className? }` | GateSystemScene (showcase only) | **KEEP + MOVE** to `tier/` |
| `tier-badge.tsx` | 78 | `{ tier, className?, showIcon? }` | **UsageWidget (PRODUCTION)** + GateSystemScene | **KEEP** |

### `components/library/`

| Component | LOC | Props | Consumers | Status |
|---|---|---|---|---|
| `UsageWidget.tsx` | 218 | `{ tier?, variant: 'full'\|'compact', className? }` — includes **inline QuotaRow** | **PersonalLibraryPage (PRODUCTION)** | **KEEP + EXTRACT QuotaRow** |

### `components/ui/data-display/`

| Component | LOC | Props | Consumers | Status |
|---|---|---|---|---|
| `CollectionLimitIndicator.tsx` | 148 | `{ tier, currentGames, currentStorageMB, maxGames, maxStorageMB, onUpgrade?, className? }` | admin/ui-library/component-map only | **DEFER** (reassess after QuotaRow extraction) |

## Overlap Analysis

### Group 1: Progress Bar Implementations (3 variants)

| Impl | Thresholds (warn/crit) | Styling | Unlimited Handling | Extra Features |
|---|---|---|---|---|
| **UsageMeter** | 80% / 100% | Tailwind classes | `max > 999_999` → 0% bar | None |
| **QuotaRow** (inline) | 80% / 95% | Tailwind + computed | `max >= 2_147_483_647` → no bar | Unlimited short-circuit, `∞` display |
| **CollectionProgressBar** | 75% / 90% | Inline HSL CSS | `MAX_SAFE_INTEGER` → no bar | Alert icon, warning text, GB/MB unit conversion |

**Divergence problems:**
- **Threshold semantics differ** (80/100 vs 80/95 vs 75/90) → users see different warning points depending on which UI they hit
- **Styling approaches diverge** (Tailwind vs inline HSL) → theme consistency risk
- **Unlimited constants inconsistent** (999_999 vs 2_147_483_647 vs MAX_SAFE_INTEGER)
- **CollectionProgressBar** is most feature-rich but orphaned

### Group 2: Upgrade CTA Implementations (2 variants)

| Impl | Variants | Tier Awareness | Benefits List | Status |
|---|---|---|---|---|
| **UpgradeCta** | inline card only | No (generic "Upgrade") | No | Simple, orphan |
| **UpgradePrompt** | inline + **modal** | Yes (tier-aware copy) | Yes (hardcoded per tier) | Rich, orphan |

**UpgradePrompt is objectively more featureful** (modal variant, tier-aware, benefits list). UpgradeCta adds nothing over it.

## KEEP / DEPRECATE / MERGE Decisions

### Final Verdict Table

| Component | Location | Status | Verdict | Action |
|---|---|---|---|---|
| **UsageMeter** | tier/ | Orphan | 🔴 DEPRECATE | Delete; consumers migrate to QuotaRow |
| **UpgradeCta** | tier/ | Orphan | 🔴 DEPRECATE | Delete; consumers migrate to UpgradePrompt inline |
| **UsageSummary** | tier/ | Orphan | 🔴 DEPRECATE | Delete (never imported anyway) |
| **PricingCard** | tier/ | Orphan | 🟡 KEEP | Awaiting `/pricing` page; add TODO comment |
| **QuotaRow** (inline) | library/UsageWidget.tsx | Production | 🟢 KEEP + EXTRACT | Move to `tier/QuotaRow.tsx`, enhance with CP features |
| **CollectionProgressBar** | ui/feedback/ | Orphan | 🔴 DEPRECATE | Features merge into QuotaRow; delete file |
| **UpgradePrompt** | ui/feedback/ | Orphan | 🟢 KEEP + MOVE | Move to `tier/UpgradePrompt.tsx`; export from tier/ |
| **TierBadge** | ui/feedback/ | Production | 🟢 KEEP | No changes; actively used in UsageWidget |
| **CollectionLimitIndicator** | ui/data-display/ | Orphan | 🟡 DEFER | Reassess after QuotaRow extraction |
| **UsageWidget** | library/ | Production | 🟢 KEEP | No changes; domain-appropriate location |

## Migration Plan (for #291 execution)

### Phase 3.1 — Extract & Consolidate QuotaRow

**Files to create:**
1. `apps/web/src/components/tier/QuotaRow.tsx` — extract from UsageWidget inline
   - Add props: `showWarning?: boolean`, `unit?: string`
   - Port CollectionProgressBar features: unit formatting, AlertTriangle icon, warning text
   - Unified threshold: **80% amber / 95% red** (matches current production UsageWidget)

**Files to update:**
1. `apps/web/src/components/library/UsageWidget.tsx`
   - Import QuotaRow from `../tier/QuotaRow`
   - Remove inline QuotaRow definition (lines ~40-75)
2. `apps/web/src/components/tier/index.ts`
   - Add `export { QuotaRow, type QuotaRowProps } from './QuotaRow';`
   - Remove `export { UsageMeter }`
3. `apps/web/src/components/admin/ui-library/component-map.ts`
   - Update imports to new QuotaRow path

**Files to delete:**
1. `apps/web/src/components/tier/UsageMeter.tsx`
2. `apps/web/src/components/tier/__tests__/UsageMeter.test.tsx` (port tests to QuotaRow.test.tsx)
3. `apps/web/src/components/ui/feedback/collection-progress-bar.tsx`
4. `apps/web/src/components/ui/feedback/__tests__/collection-progress-bar.test.tsx` (if exists)
5. `apps/web/src/components/showcase/stories/collection-progress.story.tsx`

### Phase 3.2 — Consolidate Upgrade CTAs

**Files to move:**
1. `apps/web/src/components/ui/feedback/upgrade-prompt.tsx` → `apps/web/src/components/tier/UpgradePrompt.tsx`

**Files to update:**
1. `apps/web/src/components/tier/index.ts`
   - Add `export { UpgradePrompt, type UpgradePromptProps } from './UpgradePrompt';`
   - Remove `export { UpgradeCta }`
2. `apps/web/src/components/admin/ui-library/scenes/GateSystemScene.tsx`
   - Update import path
3. `apps/web/src/components/showcase/stories/upgrade-prompt.story.tsx`
   - Update import path

**Files to delete:**
1. `apps/web/src/components/tier/UpgradeCta.tsx`

### Phase 3.3 — Deprecate Unused Exports

**Files to update:**
1. `apps/web/src/components/tier/index.ts`
   - Remove `export { UsageSummary }` (never imported)
   - Keep `export { PricingCard }` with comment: "// Awaiting /pricing page implementation"

**Files to delete:**
1. `apps/web/src/components/tier/UsageSummary.tsx`

### Phase 3.4 — Tests & Validation

**Create:**
1. `apps/web/src/components/tier/__tests__/QuotaRow.test.tsx`
   - Port UsageMeter tests
   - Add tests for unit formatting, AlertTriangle icon, warning text

**Validate:**
1. `apps/web/src/components/library/__tests__/UsageWidget.test.tsx` — all existing tests pass
2. Run full frontend test suite
3. Visual QA: PersonalLibraryPage sidebar renders identically
4. Showcase: `[ORPHAN]` prefixes removed from `collection-progress`, `upgrade-prompt` stories metadata

## File Count Delta

**Before:** 9 distinct tier-gate related files
- tier/: UsageMeter, UpgradeCta, UsageSummary, PricingCard (4)
- ui/feedback/: CollectionProgressBar, UpgradePrompt, TierBadge (3)
- library/: UsageWidget (1)
- ui/data-display/: CollectionLimitIndicator (1)

**After:** 6 distinct tier-gate related files
- tier/: PricingCard, QuotaRow, UpgradePrompt (3)
- ui/feedback/: TierBadge (1)
- library/: UsageWidget (1)
- ui/data-display/: CollectionLimitIndicator (1, or deprecated)

**Delta:** **−3 files**, single-source-of-truth per concept.

## Risks & Mitigations

| Risk | Severity | Mitigation |
|---|---|---|
| QuotaRow extraction breaks UsageWidget in production | HIGH | Run full PersonalLibraryPage tests + visual QA post-extraction |
| Threshold semantics change (80/95 vs 75/90 vs 80/100) | MEDIUM | Document change in QuotaRow props; stake on UsageWidget current (80/95) |
| Unit conversion for GB/MB introduces bugs | MEDIUM | Port CollectionProgressBar logic as-is with tests |
| UpgradePrompt modal variant never gets wired | LOW | Keep code intact; document "future: depends on TierGate product decision" |
| PricingCard remains orphaned indefinitely | LOW | Mark with TODO; review next sprint |

## Test Coverage Requirements (Post-Migration)

- [ ] `QuotaRow.test.tsx` — port UsageMeter tests + add new tests for unit/warning features
- [ ] `UsageWidget.test.tsx` — existing tests pass with new QuotaRow import
- [ ] `UpgradePrompt.test.tsx` — create if missing (currently no tests for this component)
- [ ] Visual regression: PersonalLibraryPage screenshot comparison
- [ ] Lint: no `@status ORPHAN` comments remain in codebase

## Recommendation

**PROCEED with #291 scope reframe.**

This audit confirms:
1. ✅ Production UX (UsageWidget) is solid — only needs QuotaRow extraction
2. ✅ Orphaned tier components can be safely consolidated without affecting shipped features
3. ✅ Progress bar + upgrade CTA redundancy eliminated by extracting/moving 3 components
4. ✅ One canonical progress bar (QuotaRow) + one canonical CTA (UpgradePrompt) cover all use cases
5. ⚠️ **Blocker:** Product decision needed on "Gate fallback UX — inline prompt, modal, or custom per-gate?" BEFORE wiring new gate flows (part of G0.3)

**Next step:** Update GitHub issue #291 with reframed scope (+comment referencing this audit) and await G0.3 gate decision for the modal/inline question.

## Files Referenced

All file:line references verified as of commit `7c8fed73d` (2026-04-09):
- `apps/web/src/components/tier/UsageMeter.tsx` (1-88)
- `apps/web/src/components/tier/UpgradeCta.tsx` (1-74)
- `apps/web/src/components/tier/UsageSummary.tsx` (1-124)
- `apps/web/src/components/tier/PricingCard.tsx` (1-130)
- `apps/web/src/components/tier/index.ts`
- `apps/web/src/components/library/UsageWidget.tsx` (1-218)
- `apps/web/src/components/ui/feedback/collection-progress-bar.tsx` (1-132)
- `apps/web/src/components/ui/feedback/upgrade-prompt.tsx` (1-183)
- `apps/web/src/components/ui/feedback/tier-badge.tsx` (1-78)
- `apps/web/src/components/ui/data-display/CollectionLimitIndicator.tsx` (1-148)
- `apps/web/src/hooks/useUsage.ts` (1-30)
