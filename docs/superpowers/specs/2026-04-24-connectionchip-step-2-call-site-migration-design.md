# ConnectionChip Step 2 — Call-Site Migration & Legacy Cleanup Design

> **Status**: Draft — awaiting user review
> **Author**: Claude (Opus 4) + brainstorming session 2026-04-24
> **Date**: 2026-04-24
> **Preceding step**: PR #549 (Step 1.6 — renderer integration), squash `1c4605c69` on `main-dev`
> **Following step**: Step 1.7 (manaPips migration, separate effort) — out of scope here
> **Original deprecation deadline**: `2026-07-15` — superseded by aggressive in-step removal (see §1)

---

## 1. Goal

Migrate the **17 production call-sites** currently passing `navItems=` to `MeepleCard` over to the `connections=` channel introduced in Step 1.6, **and remove the legacy code physically in the same PR**. The migration is a pure parity refactor (no UX-visible change) wrapped around an aggressive cleanup that supersedes the original 2026-07-15 deprecation window.

After this PR ships, the codebase will have:
- **One** nav-channel for `MeepleCard` (`connections`), down from three (`navItems` removed, `manaPips` retained but slated for Step 1.7).
- **No** dead-API surface (`navItems` prop gone from `MeepleCardProps`).
- **No** transitional adapter (`navItemsToConnections` deleted).
- **No** dual-source ESLint guard (rule unnecessary once dual is impossible by API shape).

## 2. Non-goals

| Non-goal | Rationale |
|---|---|
| Migrate `manaPips` (3 call-sites: HeroCard, library sections × 2) | Visual treatment differs (decorative pips vs nav chips); migration would break parity. Candidate for Step 1.7. |
| Introduce ConnectionChip popover/`onCreate`/count-click UX on migrated surfaces | Parity = no UX change. Activation deferred to product-led effort post-Step 2. |
| Add Playwright visual regression coverage | Coverage strategy is programmatic DOM equality + dev playground audit (see §4). Playwright stays out per Step 1.6 spec. |
| Remove `__useConnectionsForNavItems` flag from `MeepleCardProps` | Flag remains useful for test infrastructure (toggle path in unit tests). Not a public API. |
| Modify `ConnectionChip` primitive, popover, or strip implementation | Out of scope — Step 1.5 + Step 1.6 already validated these. |
| Modify routing, data fetching, or query layers feeding migrated cards | Pure rendering migration; no data-layer touches. |

## 3. Context — current state (2026-04-24)

### 3.1 Production call-site inventory

| BC | File | Lines (approx) | Notes |
|---|---|---|---|
| KnowledgeBase | `components/chat-unified/MeepleChatCard.tsx` | ~5 navItems entries | Chat sessions list |
| KnowledgeBase | `components/documents/MeepleKbCard.tsx` | ~3 navItems entries | Document/KB cards |
| GameManagement | `components/catalog/MeepleGameCatalogCard.tsx` | ~4 navItems entries | Catalog browser |
| GameManagement | `components/library/MeepleLibraryGameCard.tsx` | ~4 navItems entries | Library game card |
| GameManagement | `components/collection/CollectionGameGrid.tsx` | ~3 navItems entries | Collection grid |
| GameManagement | `components/playlists/MeeplePlaylistCard.tsx` | ~3 navItems entries | Playlist cards |
| SessionTracking | `components/session/MeepleSessionCard.tsx` | ~5 navItems entries | Live/finalized sessions |
| SessionTracking | `components/session/MeepleResumeSessionCard.tsx` | ~3 navItems entries | Resume CTA |
| SessionTracking | `components/session/MeepleParticipantCard.tsx` | ~3 navItems entries | Participant in session |
| SessionTracking | `components/library/private-game-detail/MeeplePausedSessionCard.tsx` | ~3 navItems entries | Paused session (cross-located in library/) |
| SessionTracking | `components/play-records/PlayHistory.tsx` | ~3 navItems entries | Play history list |
| AgentMemory | `components/agent/MeepleAgentCard.tsx` | ~5 navItems entries | Agent character card |
| GameNight | `components/game-night/MeepleEventCard.tsx` | ~3 navItems entries | Event card |
| GameNight | `components/game-night/planning/MeepleGameNightCard.tsx` | ~4 navItems entries | Planned game night |
| GameToolbox | `components/toolbox/ToolboxKitCard.tsx` | ~3 navItems entries | Toolbox kit |
| SharedGameCatalog | `components/shared-games/MeepleContributorCard.tsx` | ~3 navItems entries | Contributor profile |
| UserLibrary | `components/library/MeepleUserLibraryCard.tsx` | ~3 navItems entries | User collection card |

**Total**: 17 files, ~62 `navItems` array entries (each entry being a `NavFooterItem` with `label`/`entity`/`count`/`href`/`icon`).

### 3.2 Infrastructure already in place (from Step 1.6)

| Asset | Path | Role in Step 2 |
|---|---|---|
| `useConnectionSource` hook | `components/ui/data-display/meeple-card/hooks/useConnectionSource.ts` | Already routes `connections` ahead of `navItems`. After migration, `navItems` branch becomes dead code → removed in cleanup commit. |
| `navItemsToConnections` adapter | `components/ui/data-display/meeple-card/adapters/navItemsToConnections.ts` | Used by Gate 1 test to validate parity during migration commits; deleted in cleanup commit. |
| `connection-source-parity.test.tsx` | `components/ui/data-display/meeple-card/__tests__/` | Existing parity-baseline test; reformulated post-migration to test `connections`-only path. |
| ESLint `no-dual-connection-source` rule | `eslint-rules/no-dual-connection-source.js` | Static guard preventing co-presence; deleted in cleanup commit (impossible by API shape after `navItems` prop removal). |
| `__useConnectionsForNavItems` internal flag | `components/ui/data-display/meeple-card/types.ts` | Retained for test infrastructure (programmatic before/after rendering). |
| Dev playground | `app/(public)/dev/meeple-card/page.tsx` | Extended with Step 2 audit section (Gate 2). |

## 4. Architecture

### 4.1 Migration mechanics — per call-site

Each migration is a 1:1 mechanical replacement of the prop name and entry shape:

**Before**:
```tsx
<MeepleSessionCard
  // ...
  navItems={[
    { label: '5 sessioni', entity: 'session', count: 5, href: '/sessions/...' },
    { label: '3 partecipanti', entity: 'player', count: 3, href: '/sessions/.../players' },
  ]}
/>
```

**After**:
```tsx
<MeepleSessionCard
  // ...
  connections={[
    { label: '5 sessioni', entityType: 'session', count: 5, href: '/sessions/...' },
    { label: '3 partecipanti', entityType: 'player', count: 3, href: '/sessions/.../players' },
  ]}
/>
```

**Mapping rules** (codified by existing adapter, applied by hand in Step 2):
| `NavFooterItem` field | `ConnectionChipProps` field | Notes |
|---|---|---|
| `label` | `label` | identical |
| `entity` | `entityType` | rename only |
| `count` | `count` | identical |
| `href` | `href` | identical |
| `icon` | `iconOverride` | (only if call-site passes custom icon — most don't) |
| `onPlusClick` + `showPlus` | `onCreate` (when `count === 0`) | none of the current 17 call-sites use this; if discovered during migration, treat as edge case (see §6) |

### 4.2 Hook simplification (cleanup commit)

After the last call-site is migrated, `useConnectionSource` is reduced to:

```tsx
function useConnectionSource(props: MeepleCardProps) {
  if (props.connections !== undefined) {
    return { source: 'connections', items: props.connections, variant: props.connectionsVariant ?? 'inline' };
  }
  if (props.manaPips !== undefined) {
    return { source: 'manaPips', items: props.manaPips, variant: undefined };
  }
  return { source: null, items: [], variant: undefined };
}
```

`navItems` branch + dev warn removed. `manaPips` branch retained (Step 1.7 territory).

## 5. Validation strategy (Gate 1 + Gate 2)

### 5.1 Gate 1 — Programmatic call-site coverage test

**New file**: `src/components/ui/data-display/meeple-card/__tests__/call-site-coverage.test.tsx`

```tsx
// Pseudocode — exact implementation in Plan
import { glob } from 'glob';
import { parse } from '@babel/parser';

describe('Step 2 — call-site migration coverage', () => {
  it('zero production files use navItems= on Meeple* cards', async () => {
    const files = await glob('src/{app,components}/**/*.tsx', {
      ignore: ['**/__tests__/**', '**/dev/**', '**/showcase/**'],
    });
    const hits: Array<{ file: string; line: number }> = [];
    for (const file of files) {
      const ast = parse(readFileSync(file, 'utf8'), { plugins: ['jsx', 'typescript'], sourceType: 'module' });
      // walk JSX, find <Meeple*Card> with prop named "navItems"
      // push into hits
    }
    expect(hits, `Found ${hits.length} unmigrated call-sites`).toEqual([]);
  });
});
```

**Behavior during migration**: this test FAILS until commit 7 (last BC migrated). That's intentional — it locks in progress and prevents the cleanup commit from landing if any call-site was missed.

### 5.2 Gate 2 — Dev playground audit section

**Modified file**: `src/app/(public)/dev/meeple-card/page.tsx`

New section "**Step 2 Audit — Migration Coverage**":
- 17 rows, one per migrated call-site
- Each row renders the actual production card component with **fixed realistic fixtures** (no random)
- Layout: `[Card preview] [filename + BC label] [✅ if connections, before/after toggle button]`
- Toggle button switches `__useConnectionsForNavItems={false}` to force-render via legacy adapter path for visual diff
- Row data fixtures live in a sibling file `step-2-audit-fixtures.ts` to keep page.tsx readable

**PR template extension**: a markdown checklist embedded in the PR description body, one item per call-site:
```markdown
## Step 2 Visual QA Checklist
- [ ] MeepleChatCard — before/after match
- [ ] MeepleKbCard — before/after match
... (17 entries)
```

## 6. Mega-PR commit structure

Single PR, 8 atomic commits, reviewable commit-by-commit.

| # | Commit message | Scope | Files | Gate 1 status after |
|---|---|---|---|---|
| 1 | `test(meeple-card): add programmatic call-site coverage gate` | Add Gate 1 test infra | +1 test file | ❌ FAIL (17 call-sites remain) |
| 2 | `feat(dev): add Step 2 migration audit section to dev playground` | Add Gate 2 dev tooling | dev/meeple-card/page.tsx + fixtures | ❌ FAIL (still 17) |
| 3 | `refactor(knowledge-base): migrate KbCard, ChatCard to connections` | KnowledgeBase BC | 2 files | ❌ FAIL (15 remain) |
| 4 | `refactor(games): migrate Catalog, Library, Collection, Playlist game cards` | GameManagement BC | 4 files | ❌ FAIL (11 remain) |
| 5 | `refactor(sessions): migrate Session, ResumeSession, Participant, PausedSession, PlayHistory` | SessionTracking BC | 5 files | ❌ FAIL (6 remain) |
| 6 | `refactor(agents): migrate AgentCard to connections` | AgentMemory BC | 1 file | ❌ FAIL (5 remain) |
| 7 | `refactor(misc): migrate Event, GameNight, Toolbox, Contributor, UserLibrary cards` | GameNight + GameToolbox + SharedGameCatalog + UserLibrary | 5 files | ✅ PASS (0 remain) |
| 8 | `chore(meeple-card): remove legacy navItems channel` | Cleanup deletes + internal forwarder edits | -NavFooter.tsx, -adapter, -prop, -ESLint rule, -devWarn branch, +edits to GridCard/FeaturedCard/FocusCard/ListCard/EntityTable to drop `navItems` rendering, +MeepleCard.tsx dedup/warn removal | ✅ PASS |

**Reviewability rationale**:
- Each commit 3-7 has a uniform diff shape (rename `navItems` → `connections`, rename `entity` → `entityType` per entry). Reviewer pattern-matches once, applies mentally to all files in the commit.
- Commit 1 + 2 land first so Gate 1 fails for a clear, expected reason (lock-in).
- Commit 8 is pure deletion — reviewer verifies no live reference to deleted symbols (grep-driven review).
- Squash on merge produces a single clean entry on `main-dev` with a complete summary.

## 7. Cleanup commit 8 — exact deletion list

| Path | Action | Verification |
|---|---|---|
| `parts/NavFooter.tsx` | Delete | `grep -r "NavFooter" src/` → 0 hits |
| `parts/index.ts` | Edit — remove `NavFooter` export | typecheck passes |
| `adapters/navItemsToConnections.ts` | Delete | `grep -r "navItemsToConnections" src/` → 0 hits |
| `adapters/__tests__/navItemsToConnections.test.tsx` | Delete | — |
| `adapters/index.ts` | Edit — remove adapter export, keep file if other adapters exist; else delete | typecheck passes |
| `types.ts` | Edit — remove `navItems?: NavFooterItem[]` from `MeepleCardProps`; remove `NavFooterItem` type if unused elsewhere | typecheck passes |
| `hooks/useConnectionSource.ts` | Edit — remove `'navItems'` source branch and dev warn; reduce to `connections` + `manaPips` precedence | passing tests |
| `hooks/__tests__/useConnectionSource.test.ts` | Edit — remove `navItems` test cases; keep `connections` and `manaPips` cases | passing tests |
| `hooks/devWarn.ts` | Conditional — if no other consumer uses `devWarnOnce`, delete; else keep | grep verification |
| `hooks/__tests__/devWarn.test.ts` | Conditional — same as above | — |
| `__tests__/no-warn-in-production.test.tsx` | Delete (test for removed warn) | — |
| `__tests__/connection-source-parity.test.tsx` | Reformulate — drop the legacy-vs-connections branch comparison; keep as `connections`-only structural test | passing tests |
| `eslint-rules/no-dual-connection-source.js` | Delete | — |
| `eslint-rules/__tests__/no-dual-connection-source.test.ts` | Delete | — |
| `eslint.config.mjs` | Edit — remove rule registration | lint passes |
| `bundle-size-baseline.json` | Update — record new (smaller) size; expected reduction | bundle-size CI gate green |
| `variants/GridCard.tsx`, `variants/FeaturedCard.tsx`, `variants/FocusCard.tsx`, `variants/ListCard.tsx` | Edit — remove `navItems` prop destructuring, adapter import, and `source === 'navItems'` rendering branches (both legacy NavFooter and `__useConnectionsForNavItems` ConnectionChipStrip paths). Keep `connections` rendering. | typecheck + tests pass |
| `features/EntityTable.tsx` | Edit — remove `card.navItems` fallback (line ~249) and `navItems.map` rendering branches (~351); render via `connections` only. | typecheck + tests pass |
| `variants/CompactCard.tsx` | Edit — remove the comment referencing `navItems` (line ~13). No code change. | — |
| Per-variant tests under `variants/__tests__/` | Edit — remove `navItems` test cases that asserted legacy path; keep `connections` cases | passing tests |
| `MeepleCard.tsx` | Edit — remove deprecation JSDoc, `__resetDeprecationDedup` export, dedup registry, deprecation warn `useEffect` (lines ~24-62), and any conditional rendering tied to `navItems` source. Update test imports that referenced `__resetDeprecationDedup`. | passing tests |
| Tests importing `__resetDeprecationDedup` | Edit — remove `import` and `beforeEach(() => __resetDeprecationDedup())` calls (e.g. `variants/__tests__/GridCard.test.tsx`) | passing tests |

**Retained (not deleted)**:
- `ConnectionChip*` primitives (the path forward)
- `useConnectionSource` (simplified)
- `__useConnectionsForNavItems` internal flag in types.ts (kept for test infrastructure even though the legacy path is gone — used by audit fixtures and unit tests)

> **Note on `__useConnectionsForNavItems`**: Once `navItems` prop is removed, this flag's only purpose is to let unit tests exercise the (now sole) `connections` path with synthetic legacy-shaped inputs. If during plan execution this proves unused after cleanup, it can be removed too — verify in plan.

## 8. Definition of Done

- [ ] `grep -rE "navItems\s*=" src/{app,components} --include="*.tsx"` (excluding `__tests__/`, `dev/`, `showcase/`) returns **0 hits**
- [ ] Gate 1 test (`call-site-coverage.test.tsx`) green in CI
- [ ] Gate 2 dev playground audit section live at `/dev/meeple-card`, 17 rows visible with before/after toggle functional
- [ ] PR description contains the 17-item visual QA checklist, all items checked by reviewer
- [ ] `pnpm typecheck` passes
- [ ] `pnpm lint` passes (with `no-dual-connection-source` rule removed from config)
- [ ] `pnpm test` passes (full unit suite — expect ~14500+ tests, no regression)
- [ ] E2E critical paths green
- [ ] Bundle size CI gate green (expect reduction; update baseline)
- [ ] No live references in source to deleted symbols (`NavFooter`, `navItemsToConnections`, `NavFooterItem` if removed)
- [ ] `useConnectionSource` simplified — `navItems` branch and dev warn removed
- [ ] PR squash-merged onto `main-dev` (per branch parent config)
- [ ] MEMORY.md updated with merge SHA + Step 2 closure note
- [ ] Local feature branch deleted, `git remote prune origin` run

## 9. Risks & mitigations

| Risk | Likelihood | Mitigation |
|---|---|---|
| Visual regression on a low-traffic surface (e.g. PausedSessionCard) escapes both Gate 1 (DOM equality holds but pixels differ) and Gate 2 (reviewer skims past) | Medium | Gate 2 enforces side-by-side rendering at the same viewport; toggle requires conscious comparison per row. PR template forces explicit ✅ per call-site. |
| A call-site uses `onPlusClick`/`showPlus` (lossy adapter path noted in Step 1.6 spec §3.3) | Low | Pre-migration grep `grep -rE "onPlusClick\|showPlus" src/components` to confirm zero occurrences before starting. Result documented in plan. |
| A call-site passes a custom `icon: ReactNode` that needs `iconOverride` mapping | Low-medium | Same pre-migration grep for `icon:` within `navItems=` arrays. Edge cases listed in plan. |
| Cleanup commit 8 misses a transitive reference (e.g. a story file, docs example, type re-export) | Medium | Cleanup commit includes a final repo-wide grep for each deleted symbol; CI typecheck catches the rest. Plan includes verification step. |
| `__useConnectionsForNavItems` flag becomes dead but isn't removed | Low | Explicit verification in plan post-cleanup. |
| Bundle size CI gate fails because reduction is smaller than expected (or unexpected increase) | Low | Bundle size baseline updated atomically in commit 8; if CI gate has hard floor, plan addresses with explicit baseline override. |
| Mega-PR sits open due to reviewer fatigue | Medium | Commit-per-BC structure makes review chunked; PR description leads with summary of mechanical changes; visual QA checklist makes reviewer's job concrete (✅ per row). |

## 10. Out-of-band considerations

- **`manaPips` Step 1.7**: Should be planned post-Step 2 merge. With `navItems` and adapter gone, the codebase is one channel cleaner; Step 1.7 will be even simpler structurally (only HeroCard + 2 library sections to address).
- **Hard deadline 2026-07-15**: Becomes irrelevant once this PR ships. MEMORY.md should explicitly note "deprecation deadline superseded".
- **Future call-sites**: Once `navItems` prop is gone, TypeScript prevents new misuse at compile time. ESLint rule deletion is safe.

---

## Approval & next step

If approved, the implementation plan is generated by the `writing-plans` skill, producing `docs/superpowers/plans/2026-04-24-connectionchip-step-2-call-site-migration-plan.md` with task-by-task TDD steps for each commit.
