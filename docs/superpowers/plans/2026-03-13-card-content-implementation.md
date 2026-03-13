# Card Content Specification — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Consolidate `'document'` + `'kb_card'` into a single `'kb'` entity type, add `'player'` to the card navigation system, update the entity navigation graph for the card-focus layout, and build 4 new mobile components (CardFocusLayout, HandStack, FocusedCardArea, useHandContext).

**Architecture:** KB consolidation touches 6 core source files and 11 test files — all `'document'` and `'kb_card'` references become `'kb'` with teal color `174 60% 40%`. New mobile components follow Zustand + Framer Motion patterns already used in the codebase. CardCapabilityProvider is a new declarative pattern for enable/disable conditions per entity type.

**Tech Stack:** React 19, Next.js 16, Zustand, Framer Motion, Vitest, Tailwind 4, shadcn/ui

---

## File Structure

### Files to MODIFY (KB Consolidation)

| File | Responsibility | Changes |
|------|---------------|---------|
| `apps/web/src/components/ui/data-display/meeple-card-styles.ts` | Entity type union + colors + drawer map | Remove `'document'` and `'kb_card'`, add `'kb'` |
| `apps/web/src/config/entity-navigation.ts` | Navigation graph | Rename `document` key → `kb`, update all `entity: 'document'` refs |
| `apps/web/src/components/ui/data-display/meeple-card-compound.tsx` | Compound wrappers | Remove `DocumentCard` + `KbCardCard`, add `KbCard` |
| `apps/web/src/components/ui/data-display/meeple-card-features/navigation-icons.tsx` | Icon map | Remove `document` + `kb_card`, add `kb` |
| `apps/web/src/components/ui/data-display/meeple-card-features/CardNavigationFooter.tsx` | Nav footer HSL map | Remove `document`, add `kb` |
| `apps/web/src/components/ui/navigation/card-stack-panel.tsx` | Card stack panel | Remove `document` + `kb_card`, add `kb` |
| `apps/web/src/components/documents/MeepleKbCard.tsx` | KB card adapter | `entity="document"` → `entity="kb"` |
| `apps/web/src/components/ui/data-display/meeple-card-features/DocumentStatusBadge.tsx` | Status badge | Rename exports to `KbStatusBadge` + `KbIndexingStatus` |

### Files to MODIFY (Tests)

| File | Changes |
|------|---------|
| `apps/web/src/components/ui/data-display/__tests__/meeple-card.test.tsx` | `'document'` → `'kb'` |
| `apps/web/src/components/ui/data-display/__tests__/meeple-card.a11y.test.tsx` | `'document'` → `'kb'` |
| `apps/web/src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx` | `Document` → `Kb`, `KbCard` refs |
| `apps/web/src/components/ui/data-display/__tests__/meeple-card-contexts.test.tsx` | `'document'` → `'kb'` |
| `apps/web/src/components/ui/data-display/__tests__/meeple-card-improvements.test.tsx` | `'document'` → `'kb'` |
| `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/CardNavigationFooter.test.tsx` | `'document'` → `'kb'` |
| `apps/web/src/config/__tests__/entity-navigation.test.ts` | `'document'` → `'kb'` |
| `apps/web/src/hooks/__tests__/useEntityActions.test.tsx` | `'document'` → `'kb'` |
| `apps/web/src/__tests__/hooks/useCollections.test.tsx` | `'document'` → `'kb'` |
| `apps/web/src/components/admin/knowledge-base/__tests__/vector-collection-card.test.tsx` | `'document'`/`'kb_card'` → `'kb'` |
| `apps/web/e2e/n8n-templates.spec.ts` | `'document'` → `'kb'` |

### Files to CREATE (New Components)

| File | Responsibility |
|------|---------------|
| `apps/web/src/hooks/use-hand-context.ts` | Zustand store: cards[], focusedIdx, handContext type |
| `apps/web/src/components/ui/navigation/focused-card-area.tsx` | Swipeable center area (Framer Motion) |
| `apps/web/src/components/ui/navigation/hand-stack.tsx` | Left-edge mini-card stack |
| `apps/web/src/components/ui/navigation/card-focus-layout.tsx` | Layout orchestrator |
| `apps/web/src/lib/card-capabilities.ts` | CardCapabilityProvider utility |
| `apps/web/src/hooks/__tests__/use-hand-context.test.ts` | Tests for hand store |
| `apps/web/src/components/ui/navigation/__tests__/focused-card-area.test.tsx` | Tests for swipeable area |
| `apps/web/src/components/ui/navigation/__tests__/hand-stack.test.tsx` | Tests for mini-card stack |
| `apps/web/src/components/ui/navigation/__tests__/card-focus-layout.test.tsx` | Tests for layout |
| `apps/web/src/lib/__tests__/card-capabilities.test.ts` | Tests for capability provider |

---

## Chunk 1: KB Entity Type Consolidation (Core Files)

> Rename `'document'` + `'kb_card'` → `'kb'` across 8 source files. All changes are mechanical renames — no logic changes.

### Task 1.1: Update MeepleEntityType union and colors

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-styles.ts`

- [ ] **Step 1: Write the failing test**

Add a test that asserts `'kb'` is a valid entity type and `'document'`/`'kb_card'` are NOT in the union.

Create file `apps/web/src/components/ui/data-display/__tests__/kb-entity-consolidation.test.ts`:

```typescript
import { entityColors, DRAWER_ENTITY_TYPE_MAP } from '../meeple-card-styles';
import type { MeepleEntityType } from '../meeple-card-styles';

describe('KB Entity Consolidation', () => {
  it('should have kb as a valid entity type', () => {
    const kbType: MeepleEntityType = 'kb';
    expect(entityColors[kbType]).toBeDefined();
    expect(entityColors[kbType].hsl).toBe('174 60% 40%');
    expect(entityColors[kbType].name).toBe('KB');
  });

  it('should NOT have document or kb_card as entity types', () => {
    const keys = Object.keys(entityColors);
    expect(keys).not.toContain('document');
    expect(keys).not.toContain('kb_card');
  });

  it('should map kb to kb drawer type', () => {
    expect(DRAWER_ENTITY_TYPE_MAP.kb).toBe('kb');
  });

  it('should NOT have document or kb_card in drawer map', () => {
    const keys = Object.keys(DRAWER_ENTITY_TYPE_MAP);
    expect(keys).not.toContain('document');
    expect(keys).not.toContain('kb_card');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/kb-entity-consolidation.test.ts`
Expected: FAIL — `'kb'` is not in the union, `'document'` still exists.

- [ ] **Step 3: Update meeple-card-styles.ts**

In `apps/web/src/components/ui/data-display/meeple-card-styles.ts`:

Replace the `MeepleEntityType` union:
```typescript
export type MeepleEntityType =
  | 'game'
  | 'player'
  | 'session'
  | 'agent'
  | 'kb'
  | 'chatSession'
  | 'event'
  | 'toolkit'
  | 'custom';
```

Replace the `entityColors` record — remove `document` and `kb_card` entries, add `kb`:
```typescript
export const entityColors: Record<MeepleEntityType, { hsl: string; name: string }> = {
  game: { hsl: '25 95% 45%', name: 'Game' },
  player: { hsl: '262 83% 58%', name: 'Player' },
  session: { hsl: '240 60% 55%', name: 'Session' },
  agent: { hsl: '38 92% 50%', name: 'Agent' },
  kb: { hsl: '174 60% 40%', name: 'KB' },
  chatSession: { hsl: '220 80% 55%', name: 'Chat' },
  event: { hsl: '350 89% 60%', name: 'Event' },
  toolkit: { hsl: '142 70% 45%', name: 'Toolkit' },
  custom: { hsl: '220 70% 50%', name: 'Custom' },
};
```

Replace the `DRAWER_ENTITY_TYPE_MAP` — remove `document` and `kb_card`, add `kb`:
```typescript
export const DRAWER_ENTITY_TYPE_MAP: Partial<
  Record<MeepleEntityType, 'game' | 'agent' | 'chat' | 'kb' | 'links'>
> = {
  game: 'game',
  agent: 'agent',
  chatSession: 'chat',
  kb: 'kb',
};
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/kb-entity-consolidation.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-styles.ts apps/web/src/components/ui/data-display/__tests__/kb-entity-consolidation.test.ts
git commit -m "refactor(entity): consolidate document+kb_card → kb entity type"
```

### Task 1.2: Update entity-navigation.ts

**Files:**
- Modify: `apps/web/src/config/entity-navigation.ts`

- [ ] **Step 1: Write the failing test**

Add to existing test file or create assertion in `apps/web/src/config/__tests__/entity-navigation.test.ts`:

```typescript
// Add this test case to existing suite
it('should use kb instead of document in navigation graph', () => {
  expect(ENTITY_NAVIGATION_GRAPH).toHaveProperty('kb');
  expect(ENTITY_NAVIGATION_GRAPH).not.toHaveProperty('document');
});

it('should reference kb entity in game navigation targets', () => {
  const gameTargets = ENTITY_NAVIGATION_GRAPH.game!;
  const kbTarget = gameTargets.find(t => t.entity === 'kb');
  expect(kbTarget).toBeDefined();
  expect(kbTarget!.label).toBe('KB');
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/config/__tests__/entity-navigation.test.ts`
Expected: FAIL — `ENTITY_NAVIGATION_GRAPH` has `document` not `kb`.

- [ ] **Step 3: Update entity-navigation.ts**

In `apps/web/src/config/entity-navigation.ts`, make these changes:

1. In the `game` entry (line 77-78), change `entity: 'document'` → `entity: 'kb'`
2. In the `agent` entry (line 105-106), change `entity: 'document'` → `entity: 'kb'`
3. Rename the `document:` key (line 121) → `kb:`
4. In the `session` entry, no `document` references — no change needed
5. In `chatSession`, no `document` references — no change needed

The `kb` graph entry becomes:
```typescript
kb: [
  {
    entity: 'game',
    label: 'Game',
    idKey: 'gameId',
    buildHref: id => `/library/${id}`,
  },
  {
    entity: 'agent',
    label: 'Agent',
    idKey: 'agentId',
    buildHref: id => `/agents/${id}`,
  },
],
```

Also update the JSDoc comment to reference `kb` instead of `Document`:
```
 * KB ──► Game (library), Agent
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/config/__tests__/entity-navigation.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/config/entity-navigation.ts apps/web/src/config/__tests__/entity-navigation.test.ts
git commit -m "refactor(nav): rename document → kb in entity navigation graph"
```

### Task 1.3: Update meeple-card-compound.tsx

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-compound.tsx`

- [ ] **Step 1: Write the failing test**

In `apps/web/src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx`, add:

```typescript
it('should export MeepleCards.Kb', () => {
  expect(MeepleCards.Kb).toBeDefined();
});

it('should NOT export MeepleCards.Document', () => {
  expect((MeepleCards as Record<string, unknown>).Document).toBeUndefined();
});

it('should NOT export MeepleCards.KbCard', () => {
  expect((MeepleCards as Record<string, unknown>).KbCard).toBeUndefined();
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx`
Expected: FAIL — `MeepleCards.Kb` does not exist.

- [ ] **Step 3: Update meeple-card-compound.tsx**

Remove `DocumentCard` (lines 49-52) and `KbCardCard` (lines 69-72). Add:

```typescript
const KbCard = React.memo(function KbCard(props: EntityCardProps) {
  return <MeepleCard entity="kb" {...props} />;
});
KbCard.displayName = 'MeepleCards.Kb';
```

Update the `MeepleCards` export — remove `Document` and `KbCard`, add:
```typescript
/** Knowledge base card (teal accent) */
Kb: KbCard,
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-compound.tsx apps/web/src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx
git commit -m "refactor(compound): consolidate Document+KbCard → Kb compound wrapper"
```

### Task 1.4: Update navigation-icons.tsx

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/navigation-icons.tsx`

- [ ] **Step 1: Update the icon map**

In `navigation-icons.tsx`, replace the `ENTITY_NAV_ICONS` record. Remove `document` and `kb_card` entries, add `kb`:

```typescript
export const ENTITY_NAV_ICONS: Record<MeepleEntityType, React.ComponentType<IconProps>> = {
  game: GameIcon,
  agent: AgentIcon,
  kb: DocumentIcon,
  session: SessionIcon,
  player: PlayerIcon,
  chatSession: ChatIcon,
  event: GameIcon,
  toolkit: ToolkitIcon,
  custom: GameIcon,
};
```

Note: The icon function stays named `DocumentIcon` internally — it's the SVG shape (a page with lines), and renaming the internal function is unnecessary since it's not exported by name.

- [ ] **Step 2: Run typecheck to verify no errors**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | head -30`
Expected: No errors related to navigation-icons.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/navigation-icons.tsx
git commit -m "refactor(icons): consolidate document+kb_card → kb in nav icon map"
```

### Task 1.5: Update CardNavigationFooter.tsx

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/CardNavigationFooter.tsx`

- [ ] **Step 1: Update NAV_ENTITY_HSL map**

In `CardNavigationFooter.tsx`, replace `document: '210 40% 55%'` with `kb: '174 60% 40%'` in the `NAV_ENTITY_HSL` constant (line 28):

```typescript
const NAV_ENTITY_HSL: Record<string, string> = {
  game: '25 95% 45%',
  player: '262 83% 58%',
  session: '240 60% 55%',
  agent: '38 92% 50%',
  kb: '174 60% 40%',
  chatSession: '220 80% 55%',
  event: '350 89% 60%',
  toolkit: '142 70% 45%',
  custom: '220 70% 50%',
};
```

- [ ] **Step 2: Run existing CardNavigationFooter tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/meeple-card-features/__tests__/CardNavigationFooter.test.tsx`
Expected: PASS (or update `'document'` refs in tests — see Task 2).

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/CardNavigationFooter.tsx
git commit -m "refactor(nav-footer): document → kb in entity HSL color map"
```

### Task 1.6: Update card-stack-panel.tsx

**Files:**
- Modify: `apps/web/src/components/ui/navigation/card-stack-panel.tsx`

- [ ] **Step 1: Update ENTITY_HSL and ENTITY_LABELS**

In `card-stack-panel.tsx`, replace both `document` and `kb_card` entries with single `kb` entry:

For `ENTITY_HSL` (line 34-45):
```typescript
const ENTITY_HSL: Record<MeepleEntityType, string> = {
  game: '25 95% 45%',
  player: '262 83% 58%',
  session: '240 60% 55%',
  agent: '38 92% 50%',
  kb: '174 60% 40%',
  chatSession: '220 80% 55%',
  event: '350 89% 60%',
  toolkit: '142 70% 45%',
  custom: '220 70% 50%',
};
```

For `ENTITY_LABELS` (line 47-58):
```typescript
const ENTITY_LABELS: Record<MeepleEntityType, string> = {
  game: 'Game',
  player: 'Player',
  session: 'Session',
  agent: 'Agent',
  kb: 'KB',
  chatSession: 'Chat',
  event: 'Event',
  toolkit: 'Toolkit',
  custom: 'Item',
};
```

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | head -30`
Expected: No type errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/navigation/card-stack-panel.tsx
git commit -m "refactor(card-stack): consolidate document+kb_card → kb in panel"
```

### Task 1.7: Update MeepleKbCard.tsx adapter

**Files:**
- Modify: `apps/web/src/components/documents/MeepleKbCard.tsx`

- [ ] **Step 1: Update entity references**

In `MeepleKbCard.tsx`:

1. Line 158: `entity="document"` → `entity="kb"`
2. Line 171: `getNavigationLinks('document', {` → `getNavigationLinks('kb', {`
3. Line 190 (skeleton): `entity="document"` → `entity="kb"`

- [ ] **Step 2: Run typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | head -30`
Expected: No errors.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/documents/MeepleKbCard.tsx
git commit -m "refactor(kb-card): use kb entity type instead of document"
```

### Task 1.8: Rename DocumentStatusBadge → KbStatusBadge

**Files:**
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/DocumentStatusBadge.tsx`

- [ ] **Step 1: Rename exports**

In `DocumentStatusBadge.tsx`:

1. Rename type: `DocumentIndexingStatus` → `KbIndexingStatus`
2. Rename component: `DocumentStatusBadge` → `KbStatusBadge`
3. Update `displayName` and internal function names
4. Update `aria-label`: `Stato documento:` → `Stato KB:`
5. Update `testId` prefix: `document-status-` → `kb-status-`
6. Keep file name as-is for now (rename is optional, less disruptive to keep)

Add backward-compat re-exports at the bottom:
```typescript
/** @deprecated Use KbIndexingStatus instead */
export type DocumentIndexingStatus = KbIndexingStatus;
/** @deprecated Use KbStatusBadge instead */
export const DocumentStatusBadge = KbStatusBadge;
```

- [ ] **Step 2: Update imports in MeepleKbCard.tsx**

In `MeepleKbCard.tsx`, change:
```typescript
import type { KbIndexingStatus } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
```

And rename internal usage: `DocumentIndexingStatus` → `KbIndexingStatus`.

- [ ] **Step 3: Run typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty 2>&1 | head -30`
Expected: No errors (backward compat re-exports keep other consumers working).

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/ui/data-display/meeple-card-features/DocumentStatusBadge.tsx apps/web/src/components/documents/MeepleKbCard.tsx
git commit -m "refactor(status): rename DocumentStatusBadge → KbStatusBadge with compat alias"
```

---

## Chunk 2: Test File Updates

> Update all test files that reference `'document'` or `'kb_card'` entity types to use `'kb'`.

### Task 2.1: Update meeple-card core tests

**Files:**
- Modify: `apps/web/src/components/ui/data-display/__tests__/meeple-card.test.tsx`
- Modify: `apps/web/src/components/ui/data-display/__tests__/meeple-card.a11y.test.tsx`
- Modify: `apps/web/src/components/ui/data-display/__tests__/meeple-card-contexts.test.tsx`
- Modify: `apps/web/src/components/ui/data-display/__tests__/meeple-card-improvements.test.tsx`

- [ ] **Step 1: Search and replace in each file**

In each file, perform these replacements:
- `entity="document"` → `entity="kb"`
- `entity: 'document'` → `entity: 'kb'`
- `'document'` (as entity type literal) → `'kb'`
- `entity="kb_card"` → `entity="kb"`
- `entity: 'kb_card'` → `entity: 'kb'`

**Important**: Only replace entity type references. Do NOT replace `document` in other contexts (e.g., `document.getElementById`, `Document` as a DOM type).

- [ ] **Step 2: Run all meeple-card tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/`
Expected: All PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/ui/data-display/__tests__/
git commit -m "test(card): update document/kb_card → kb in meeple-card tests"
```

### Task 2.2: Update compound and navigation tests

**Files:**
- Modify: `apps/web/src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx`
- Modify: `apps/web/src/components/ui/data-display/meeple-card-features/__tests__/CardNavigationFooter.test.tsx`
- Modify: `apps/web/src/config/__tests__/entity-navigation.test.ts`

- [ ] **Step 1: Update compound test**

In `meeple-card-compound.test.tsx`:
- Replace `MeepleCards.Document` → `MeepleCards.Kb`
- Replace `MeepleCards.KbCard` → remove (consolidated into `MeepleCards.Kb`)
- Update display name assertions: `'MeepleCards.Document'` → `'MeepleCards.Kb'`

- [ ] **Step 2: Update CardNavigationFooter test**

In `CardNavigationFooter.test.tsx`:
- Replace `entity: 'document'` → `entity: 'kb'`
- Update any label assertions accordingly

- [ ] **Step 3: Update entity-navigation test**

In `entity-navigation.test.ts`:
- Replace `'document'` entity key references → `'kb'`
- Update `getNavigationLinks('document', ...)` → `getNavigationLinks('kb', ...)`

- [ ] **Step 4: Run all affected tests**

Run: `cd apps/web && pnpm vitest run src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx src/components/ui/data-display/meeple-card-features/__tests__/CardNavigationFooter.test.tsx src/config/__tests__/entity-navigation.test.ts`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/data-display/__tests__/meeple-card-compound.test.tsx apps/web/src/components/ui/data-display/meeple-card-features/__tests__/CardNavigationFooter.test.tsx apps/web/src/config/__tests__/entity-navigation.test.ts
git commit -m "test(nav): update document → kb in compound and navigation tests"
```

### Task 2.3: Update remaining test files

**Files:**
- Modify: `apps/web/src/hooks/__tests__/useEntityActions.test.tsx`
- Modify: `apps/web/src/__tests__/hooks/useCollections.test.tsx`
- Modify: `apps/web/src/components/admin/knowledge-base/__tests__/vector-collection-card.test.tsx`
- Modify: `apps/web/e2e/n8n-templates.spec.ts`

- [ ] **Step 1: Update each file**

In each file, replace `'document'` and `'kb_card'` entity type references with `'kb'`.

- [ ] **Step 2: Run affected tests**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/useEntityActions.test.tsx src/__tests__/hooks/useCollections.test.tsx src/components/admin/knowledge-base/__tests__/vector-collection-card.test.tsx`
Expected: All PASS.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/__tests__/useEntityActions.test.tsx apps/web/src/__tests__/hooks/useCollections.test.tsx apps/web/src/components/admin/knowledge-base/__tests__/vector-collection-card.test.tsx apps/web/e2e/n8n-templates.spec.ts
git commit -m "test(kb): update document/kb_card → kb in remaining test files"
```

### Task 2.4: Full test suite verification

- [ ] **Step 1: Run typecheck**

Run: `cd apps/web && pnpm tsc --noEmit --pretty`
Expected: 0 errors.

- [ ] **Step 2: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: All tests PASS. If any test references `'document'` as entity type and fails, fix it.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No new warnings/errors.

- [ ] **Step 4: Commit (if any fixes needed)**

```bash
git add -A
git commit -m "fix(tests): resolve remaining document → kb migration issues"
```

---

## Chunk 3: useHandContext Store + CardCapabilityProvider

> Build the Zustand store for hand-of-cards state and the declarative capability provider.

### Task 3.1: Create useHandContext Zustand store

**Files:**
- Create: `apps/web/src/hooks/use-hand-context.ts`
- Create: `apps/web/src/hooks/__tests__/use-hand-context.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/hooks/__tests__/use-hand-context.test.ts`:

```typescript
import { renderHook, act } from '@testing-library/react';

import { useHandContext, type HandCard, type HandContextType } from '../use-hand-context';

const mockCard = (id: string, entity: string = 'game'): HandCard => ({
  id,
  entity: entity as HandCard['entity'],
  title: `Card ${id}`,
  href: `/library/${id}`,
});

describe('useHandContext', () => {
  beforeEach(() => {
    // Reset the store between tests
    const { result } = renderHook(() => useHandContext());
    act(() => result.current.clear());
  });

  it('should start with empty hand', () => {
    const { result } = renderHook(() => useHandContext());
    expect(result.current.cards).toHaveLength(0);
    expect(result.current.focusedIdx).toBe(-1);
    expect(result.current.handContext).toBe('library');
  });

  it('should add a card and auto-focus it', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => result.current.addCard(mockCard('1')));
    expect(result.current.cards).toHaveLength(1);
    expect(result.current.focusedIdx).toBe(0);
  });

  it('should not add duplicate cards', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('1'));
    });
    expect(result.current.cards).toHaveLength(1);
  });

  it('should limit hand to 7 cards', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      for (let i = 0; i < 10; i++) {
        result.current.addCard(mockCard(`${i}`));
      }
    });
    expect(result.current.cards.length).toBeLessThanOrEqual(7);
  });

  it('should remove a card by id', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.removeCard('1');
    });
    expect(result.current.cards).toHaveLength(1);
    expect(result.current.cards[0].id).toBe('2');
  });

  it('should focus a card by index', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.focusCard(1);
    });
    expect(result.current.focusedIdx).toBe(1);
  });

  it('should swipe to next/previous card', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.addCard(mockCard('3'));
      result.current.focusCard(0);
    });
    act(() => result.current.swipeNext());
    expect(result.current.focusedIdx).toBe(1);
    act(() => result.current.swipePrev());
    expect(result.current.focusedIdx).toBe(0);
  });

  it('should clamp swipe at boundaries', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.focusCard(0);
    });
    act(() => result.current.swipePrev());
    expect(result.current.focusedIdx).toBe(0);
    act(() => result.current.swipeNext());
    expect(result.current.focusedIdx).toBe(0); // only 1 card
  });

  it('should set hand context type', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => result.current.setHandContext('sessions'));
    expect(result.current.handContext).toBe('sessions');
  });

  it('should clear all cards', () => {
    const { result } = renderHook(() => useHandContext());
    act(() => {
      result.current.addCard(mockCard('1'));
      result.current.addCard(mockCard('2'));
      result.current.clear();
    });
    expect(result.current.cards).toHaveLength(0);
    expect(result.current.focusedIdx).toBe(-1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/use-hand-context.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement the store**

Create `apps/web/src/hooks/use-hand-context.ts`:

```typescript
/**
 * useHandContext - Zustand store for "hand of cards" navigation state
 *
 * Manages the collection of cards in the user's hand, focus state,
 * and swipe navigation for the mobile card-focus layout.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

import { create } from 'zustand';

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface HandCard {
  id: string;
  entity: MeepleEntityType;
  title: string;
  href: string;
  subtitle?: string;
  imageUrl?: string;
}

export type HandContextType =
  | 'library'
  | 'sessions'
  | 'agents'
  | 'chats'
  | 'kbs'
  | 'toolkits'
  | 'players';

const MAX_HAND_SIZE = 7;

// ============================================================================
// Store
// ============================================================================

interface HandContextState {
  cards: HandCard[];
  focusedIdx: number;
  handContext: HandContextType;
  addCard: (card: HandCard) => void;
  removeCard: (id: string) => void;
  focusCard: (index: number) => void;
  swipeNext: () => void;
  swipePrev: () => void;
  setHandContext: (context: HandContextType) => void;
  clear: () => void;
}

export const useHandContext = create<HandContextState>((set, get) => ({
  cards: [],
  focusedIdx: -1,
  handContext: 'library',

  addCard: (card) => {
    const { cards } = get();
    // Skip duplicates
    if (cards.some((c) => c.id === card.id)) {
      // Focus the existing card instead
      const idx = cards.findIndex((c) => c.id === card.id);
      set({ focusedIdx: idx });
      return;
    }
    // Enforce max hand size — drop oldest (first) if full
    const next = cards.length >= MAX_HAND_SIZE
      ? [...cards.slice(1), card]
      : [...cards, card];
    set({ cards: next, focusedIdx: next.length - 1 });
  },

  removeCard: (id) => {
    const { cards, focusedIdx } = get();
    const idx = cards.findIndex((c) => c.id === id);
    if (idx === -1) return;
    const next = cards.filter((c) => c.id !== id);
    const newFocused = next.length === 0
      ? -1
      : focusedIdx >= next.length
        ? next.length - 1
        : focusedIdx > idx
          ? focusedIdx - 1
          : focusedIdx;
    set({ cards: next, focusedIdx: newFocused });
  },

  focusCard: (index) => {
    const { cards } = get();
    if (index >= 0 && index < cards.length) {
      set({ focusedIdx: index });
    }
  },

  swipeNext: () => {
    const { focusedIdx, cards } = get();
    if (focusedIdx < cards.length - 1) {
      set({ focusedIdx: focusedIdx + 1 });
    }
  },

  swipePrev: () => {
    const { focusedIdx } = get();
    if (focusedIdx > 0) {
      set({ focusedIdx: focusedIdx - 1 });
    }
  },

  setHandContext: (context) => set({ handContext: context }),

  clear: () => set({ cards: [], focusedIdx: -1 }),
}));
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/hooks/__tests__/use-hand-context.test.ts`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/use-hand-context.ts apps/web/src/hooks/__tests__/use-hand-context.test.ts
git commit -m "feat(hand): add useHandContext Zustand store for card-focus layout"
```

### Task 3.2: Create CardCapabilityProvider

**Files:**
- Create: `apps/web/src/lib/card-capabilities.ts`
- Create: `apps/web/src/lib/__tests__/card-capabilities.test.ts`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/lib/__tests__/card-capabilities.test.ts`:

```typescript
import {
  type CardCapabilities,
  type CardCapabilityContext,
  resolveCapabilities,
  requires,
} from '../card-capabilities';

describe('CardCapabilityProvider', () => {
  const baseContext: CardCapabilityContext = {
    entity: 'game',
    hasAgent: true,
    isOnline: true,
    isAdmin: false,
    isEditor: true,
    agentStatus: 'ready',
    kbStatus: 'indexed',
    sessionState: undefined,
  };

  describe('resolveCapabilities', () => {
    it('should enable AI chat when agent is ready and online', () => {
      const caps = resolveCapabilities(baseContext);
      expect(caps.aiChat).toBe(true);
    });

    it('should disable AI chat when no agent', () => {
      const caps = resolveCapabilities({ ...baseContext, hasAgent: false });
      expect(caps.aiChat).toBe(false);
    });

    it('should disable AI chat when offline', () => {
      const caps = resolveCapabilities({ ...baseContext, isOnline: false });
      expect(caps.aiChat).toBe(false);
    });

    it('should enable reindex for admin when not processing', () => {
      const ctx: CardCapabilityContext = { ...baseContext, entity: 'kb', isAdmin: true };
      const caps = resolveCapabilities(ctx);
      expect(caps.reindex).toBe(true);
    });

    it('should disable reindex for non-admin', () => {
      const ctx: CardCapabilityContext = { ...baseContext, entity: 'kb', isAdmin: false };
      const caps = resolveCapabilities(ctx);
      expect(caps.reindex).toBe(false);
    });

    it('should enable session scoring during active session', () => {
      const ctx: CardCapabilityContext = {
        ...baseContext,
        entity: 'session',
        sessionState: 'active',
      };
      const caps = resolveCapabilities(ctx);
      expect(caps.scoring).toBe(true);
    });

    it('should disable session scoring when session is completed', () => {
      const ctx: CardCapabilityContext = {
        ...baseContext,
        entity: 'session',
        sessionState: 'completed',
      };
      const caps = resolveCapabilities(ctx);
      expect(caps.scoring).toBe(false);
    });
  });

  describe('requires helper', () => {
    it('should return true when all required capabilities are enabled', () => {
      const caps: CardCapabilities = {
        aiChat: true, reindex: false, scoring: true,
        download: true, delete: false, flip: true,
        drawer: true, navigate: true, quickActions: true,
      };
      expect(requires(caps, 'aiChat', 'scoring')).toBe(true);
    });

    it('should return false when any required capability is disabled', () => {
      const caps: CardCapabilities = {
        aiChat: true, reindex: false, scoring: true,
        download: true, delete: false, flip: true,
        drawer: true, navigate: true, quickActions: true,
      };
      expect(requires(caps, 'aiChat', 'reindex')).toBe(false);
    });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/card-capabilities.test.ts`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement card-capabilities.ts**

Create `apps/web/src/lib/card-capabilities.ts`:

```typescript
/**
 * CardCapabilityProvider - Declarative enable/disable for card actions
 *
 * Resolves which actions/features are available on a card based on
 * entity type, user role, connectivity, and entity state.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface CardCapabilities {
  aiChat: boolean;
  reindex: boolean;
  scoring: boolean;
  download: boolean;
  delete: boolean;
  flip: boolean;
  drawer: boolean;
  navigate: boolean;
  quickActions: boolean;
}

export type AgentStatus = 'ready' | 'not_ready' | 'no_agent' | 'error' | 'offline' | 'out_of_budget';
export type KbStatus = 'indexed' | 'processing' | 'failed' | 'none';
export type SessionState = 'planning' | 'active' | 'paused' | 'completed';

export interface CardCapabilityContext {
  entity: MeepleEntityType;
  hasAgent: boolean;
  isOnline: boolean;
  isAdmin: boolean;
  isEditor: boolean;
  agentStatus?: AgentStatus;
  kbStatus?: KbStatus;
  sessionState?: SessionState;
}

// ============================================================================
// Resolver
// ============================================================================

const DEFAULTS: CardCapabilities = {
  aiChat: false,
  reindex: false,
  scoring: false,
  download: false,
  delete: false,
  flip: true,
  drawer: true,
  navigate: true,
  quickActions: true,
};

export function resolveCapabilities(ctx: CardCapabilityContext): CardCapabilities {
  const caps = { ...DEFAULTS };

  // AI Chat: requires agent + online + agent ready
  caps.aiChat = ctx.hasAgent && ctx.isOnline && ctx.agentStatus === 'ready';

  // Reindex: KB entity + admin + not currently processing
  caps.reindex = ctx.entity === 'kb' && ctx.isAdmin && ctx.kbStatus !== 'processing';

  // Scoring: session entity + active state
  caps.scoring = ctx.entity === 'session' && ctx.sessionState === 'active';

  // Download: editor or admin
  caps.download = ctx.isEditor || ctx.isAdmin;

  // Delete: admin only
  caps.delete = ctx.isAdmin;

  return caps;
}

// ============================================================================
// Helper
// ============================================================================

/**
 * Check if all required capabilities are enabled.
 *
 * @example
 * ```tsx
 * if (requires(caps, 'aiChat', 'navigate')) {
 *   // show AI chat + navigation
 * }
 * ```
 */
export function requires(
  caps: CardCapabilities,
  ...required: (keyof CardCapabilities)[]
): boolean {
  return required.every((key) => caps[key]);
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/lib/__tests__/card-capabilities.test.ts`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/card-capabilities.ts apps/web/src/lib/__tests__/card-capabilities.test.ts
git commit -m "feat(caps): add CardCapabilityProvider for declarative action enable/disable"
```

---

## Chunk 4: Mobile Layout Components

> Build the 3 new UI components for the mobile card-focus layout.

### Task 4.1: Create FocusedCardArea

**Files:**
- Create: `apps/web/src/components/ui/navigation/focused-card-area.tsx`
- Create: `apps/web/src/components/ui/navigation/__tests__/focused-card-area.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/ui/navigation/__tests__/focused-card-area.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';

import { FocusedCardArea } from '../focused-card-area';

describe('FocusedCardArea', () => {
  it('should render children', () => {
    render(
      <FocusedCardArea onSwipeLeft={jest.fn()} onSwipeRight={jest.fn()}>
        <div data-testid="card-content">Test Card</div>
      </FocusedCardArea>
    );
    expect(screen.getByTestId('card-content')).toBeInTheDocument();
  });

  it('should render with focused-card-area testid', () => {
    render(
      <FocusedCardArea onSwipeLeft={jest.fn()} onSwipeRight={jest.fn()}>
        <div>Card</div>
      </FocusedCardArea>
    );
    expect(screen.getByTestId('focused-card-area')).toBeInTheDocument();
  });

  it('should show swipe indicators when hasPrev/hasNext', () => {
    render(
      <FocusedCardArea
        onSwipeLeft={jest.fn()}
        onSwipeRight={jest.fn()}
        hasPrev
        hasNext
      >
        <div>Card</div>
      </FocusedCardArea>
    );
    expect(screen.getByLabelText('Previous card')).toBeInTheDocument();
    expect(screen.getByLabelText('Next card')).toBeInTheDocument();
  });

  it('should call onSwipeLeft when next button clicked', () => {
    const onSwipeLeft = jest.fn();
    render(
      <FocusedCardArea onSwipeLeft={onSwipeLeft} onSwipeRight={jest.fn()} hasNext>
        <div>Card</div>
      </FocusedCardArea>
    );
    fireEvent.click(screen.getByLabelText('Next card'));
    expect(onSwipeLeft).toHaveBeenCalledTimes(1);
  });

  it('should call onSwipeRight when prev button clicked', () => {
    const onSwipeRight = jest.fn();
    render(
      <FocusedCardArea onSwipeLeft={jest.fn()} onSwipeRight={onSwipeRight} hasPrev>
        <div>Card</div>
      </FocusedCardArea>
    );
    fireEvent.click(screen.getByLabelText('Previous card'));
    expect(onSwipeRight).toHaveBeenCalledTimes(1);
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/__tests__/focused-card-area.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement FocusedCardArea**

Create `apps/web/src/components/ui/navigation/focused-card-area.tsx`:

```tsx
/**
 * FocusedCardArea - Swipeable center area for card-focus mobile layout
 *
 * Wraps a single MeepleCard in a swipeable container with Framer Motion
 * drag gestures. Displays swipe arrow indicators when prev/next cards exist.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

'use client';

import { type ReactNode } from 'react';

import { motion, useAnimation, type PanInfo } from 'framer-motion';
import { ChevronLeft, ChevronRight } from 'lucide-react';

import { cn } from '@/lib/utils';

interface FocusedCardAreaProps {
  children: ReactNode;
  onSwipeLeft: () => void;
  onSwipeRight: () => void;
  hasPrev?: boolean;
  hasNext?: boolean;
  className?: string;
}

const SWIPE_THRESHOLD = 50;

export function FocusedCardArea({
  children,
  onSwipeLeft,
  onSwipeRight,
  hasPrev = false,
  hasNext = false,
  className,
}: FocusedCardAreaProps) {
  const controls = useAnimation();

  const handleDragEnd = (_: unknown, info: PanInfo) => {
    const { offset } = info;
    if (offset.x < -SWIPE_THRESHOLD && hasNext) {
      onSwipeLeft();
    } else if (offset.x > SWIPE_THRESHOLD && hasPrev) {
      onSwipeRight();
    }
    controls.start({ x: 0, transition: { type: 'spring', stiffness: 300, damping: 30 } });
  };

  return (
    <div
      className={cn('relative flex-1 flex items-center justify-center overflow-hidden', className)}
      data-testid="focused-card-area"
    >
      {/* Prev indicator */}
      {hasPrev && (
        <button
          onClick={onSwipeRight}
          className="absolute left-2 z-10 p-1.5 rounded-full bg-card/80 backdrop-blur-sm border border-border/30 text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Previous card"
        >
          <ChevronLeft className="w-5 h-5" />
        </button>
      )}

      {/* Swipeable content */}
      <motion.div
        drag="x"
        dragConstraints={{ left: 0, right: 0 }}
        onDragEnd={handleDragEnd}
        animate={controls}
        className="w-full max-w-md px-4"
      >
        {children}
      </motion.div>

      {/* Next indicator */}
      {hasNext && (
        <button
          onClick={onSwipeLeft}
          className="absolute right-2 z-10 p-1.5 rounded-full bg-card/80 backdrop-blur-sm border border-border/30 text-muted-foreground hover:text-foreground transition-colors"
          aria-label="Next card"
        >
          <ChevronRight className="w-5 h-5" />
        </button>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/__tests__/focused-card-area.test.tsx`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/navigation/focused-card-area.tsx apps/web/src/components/ui/navigation/__tests__/focused-card-area.test.tsx
git commit -m "feat(mobile): add FocusedCardArea with swipe gestures"
```

### Task 4.2: Create HandStack

**Files:**
- Create: `apps/web/src/components/ui/navigation/hand-stack.tsx`
- Create: `apps/web/src/components/ui/navigation/__tests__/hand-stack.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/ui/navigation/__tests__/hand-stack.test.tsx`:

```tsx
import { render, screen, fireEvent } from '@testing-library/react';

import { HandStack } from '../hand-stack';

import type { HandCard } from '@/hooks/use-hand-context';

const mockCards: HandCard[] = [
  { id: '1', entity: 'game', title: 'Catan', href: '/library/1' },
  { id: '2', entity: 'session', title: 'Session #1', href: '/sessions/2' },
  { id: '3', entity: 'agent', title: 'RAG Agent', href: '/agents/3' },
];

describe('HandStack', () => {
  it('should render all cards as mini items', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={jest.fn()} />
    );
    expect(screen.getByText('Catan')).toBeInTheDocument();
    expect(screen.getByText('Session #1')).toBeInTheDocument();
    expect(screen.getByText('RAG Agent')).toBeInTheDocument();
  });

  it('should render with hand-stack testid', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={jest.fn()} />
    );
    expect(screen.getByTestId('hand-stack')).toBeInTheDocument();
  });

  it('should highlight the focused card', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={1} onCardClick={jest.fn()} />
    );
    const items = screen.getAllByTestId(/^hand-stack-item-/);
    expect(items[1]).toHaveAttribute('data-focused', 'true');
  });

  it('should call onCardClick with index when clicked', () => {
    const onCardClick = jest.fn();
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={onCardClick} />
    );
    fireEvent.click(screen.getByText('RAG Agent'));
    expect(onCardClick).toHaveBeenCalledWith(2);
  });

  it('should render nothing when cards is empty', () => {
    const { container } = render(
      <HandStack cards={[]} focusedIdx={-1} onCardClick={jest.fn()} />
    );
    expect(container.firstChild).toBeNull();
  });

  it('should show entity-colored accent', () => {
    render(
      <HandStack cards={mockCards} focusedIdx={0} onCardClick={jest.fn()} />
    );
    const items = screen.getAllByTestId(/^hand-stack-item-/);
    expect(items[0]).toHaveStyle({ '--hand-hsl': '25 95% 45%' });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/__tests__/hand-stack.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement HandStack**

Create `apps/web/src/components/ui/navigation/hand-stack.tsx`:

```tsx
/**
 * HandStack - Left-edge mini-card stack for mobile card-focus layout
 *
 * Displays up to 7 mini-cards vertically. Each mini-card shows the entity
 * icon + truncated title with entity-colored accent. The focused card is
 * highlighted.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

'use client';

import { entityColors } from '@/components/ui/data-display/meeple-card-styles';
import { ENTITY_NAV_ICONS } from '@/components/ui/data-display/meeple-card-features/navigation-icons';
import type { HandCard } from '@/hooks/use-hand-context';
import { cn } from '@/lib/utils';

interface HandStackProps {
  cards: HandCard[];
  focusedIdx: number;
  onCardClick: (index: number) => void;
  className?: string;
}

export function HandStack({ cards, focusedIdx, onCardClick, className }: HandStackProps) {
  if (cards.length === 0) return null;

  return (
    <div
      className={cn(
        'flex flex-col gap-1.5 py-2 px-1.5',
        'w-14 shrink-0',
        'overflow-y-auto scrollbar-none',
        className
      )}
      data-testid="hand-stack"
    >
      {cards.map((card, i) => {
        const Icon = ENTITY_NAV_ICONS[card.entity] ?? ENTITY_NAV_ICONS.game;
        const hsl = entityColors[card.entity]?.hsl ?? '220 70% 50%';
        const isFocused = i === focusedIdx;

        return (
          <button
            key={card.id}
            type="button"
            onClick={() => onCardClick(i)}
            className={cn(
              'flex flex-col items-center gap-0.5 p-1.5 rounded-lg',
              'transition-all duration-200 cursor-pointer',
              'border',
              isFocused
                ? 'bg-[hsl(var(--hand-hsl)/0.12)] border-[hsl(var(--hand-hsl)/0.4)] scale-105'
                : 'bg-card/60 border-border/30 hover:bg-card/80'
            )}
            style={{ '--hand-hsl': hsl } as React.CSSProperties}
            data-testid={`hand-stack-item-${card.id}`}
            data-focused={isFocused ? 'true' : 'false'}
            title={card.title}
          >
            <Icon
              className={cn(
                'w-4 h-4',
                isFocused ? 'text-[hsl(var(--hand-hsl))]' : 'text-muted-foreground'
              )}
            />
            <span
              className={cn(
                'text-[8px] font-nunito font-bold truncate w-full text-center leading-tight',
                isFocused ? 'text-[hsl(var(--hand-hsl))]' : 'text-muted-foreground/70'
              )}
            >
              {card.title}
            </span>
          </button>
        );
      })}
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/__tests__/hand-stack.test.tsx`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/navigation/hand-stack.tsx apps/web/src/components/ui/navigation/__tests__/hand-stack.test.tsx
git commit -m "feat(mobile): add HandStack mini-card component"
```

### Task 4.3: Create CardFocusLayout

**Files:**
- Create: `apps/web/src/components/ui/navigation/card-focus-layout.tsx`
- Create: `apps/web/src/components/ui/navigation/__tests__/card-focus-layout.test.tsx`

- [ ] **Step 1: Write the failing test**

Create `apps/web/src/components/ui/navigation/__tests__/card-focus-layout.test.tsx`:

```tsx
import { render, screen } from '@testing-library/react';

import { CardFocusLayout } from '../card-focus-layout';

// Mock the useHandContext store
const mockStore = {
  cards: [
    { id: '1', entity: 'game' as const, title: 'Catan', href: '/library/1' },
    { id: '2', entity: 'session' as const, title: 'Session', href: '/sessions/2' },
  ],
  focusedIdx: 0,
  handContext: 'library' as const,
  addCard: jest.fn(),
  removeCard: jest.fn(),
  focusCard: jest.fn(),
  swipeNext: jest.fn(),
  swipePrev: jest.fn(),
  setHandContext: jest.fn(),
  clear: jest.fn(),
};

jest.mock('@/hooks/use-hand-context', () => ({
  useHandContext: () => mockStore,
}));

describe('CardFocusLayout', () => {
  it('should render the layout with hand stack and focused area', () => {
    render(
      <CardFocusLayout>
        <div data-testid="card">Focused Card</div>
      </CardFocusLayout>
    );
    expect(screen.getByTestId('card-focus-layout')).toBeInTheDocument();
    expect(screen.getByTestId('hand-stack')).toBeInTheDocument();
    expect(screen.getByTestId('focused-card-area')).toBeInTheDocument();
  });

  it('should show context label', () => {
    render(
      <CardFocusLayout>
        <div>Card</div>
      </CardFocusLayout>
    );
    expect(screen.getByText(/la tua mano/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/__tests__/card-focus-layout.test.tsx`
Expected: FAIL — module not found.

- [ ] **Step 3: Implement CardFocusLayout**

Create `apps/web/src/components/ui/navigation/card-focus-layout.tsx`:

```tsx
/**
 * CardFocusLayout - Mobile layout orchestrator for card-focus view
 *
 * Arranges HandStack (left edge) + FocusedCardArea (center) for mobile.
 * Reads from useHandContext store for state management.
 *
 * Usage: In AppShell, render this layout when isMobile is true.
 *
 * @see docs/superpowers/specs/2026-03-13-card-content-specification-design.md
 */

'use client';

import { type ReactNode } from 'react';

import { useHandContext, type HandContextType } from '@/hooks/use-hand-context';
import { cn } from '@/lib/utils';

import { FocusedCardArea } from './focused-card-area';
import { HandStack } from './hand-stack';

// ============================================================================
// Context labels
// ============================================================================

const CONTEXT_LABELS: Record<HandContextType, string> = {
  library: 'La tua mano',
  sessions: 'Le tue sessioni',
  agents: 'I tuoi agenti',
  chats: 'Le tue chat',
  kbs: 'Le tue KB',
  toolkits: 'I tuoi toolkit',
  players: 'I tuoi giocatori',
};

// ============================================================================
// Component
// ============================================================================

interface CardFocusLayoutProps {
  children: ReactNode;
  className?: string;
}

export function CardFocusLayout({ children, className }: CardFocusLayoutProps) {
  const { cards, focusedIdx, handContext, focusCard, swipeNext, swipePrev } = useHandContext();

  const contextLabel = CONTEXT_LABELS[handContext] ?? 'La tua mano';
  const hasPrev = focusedIdx > 0;
  const hasNext = focusedIdx < cards.length - 1;

  return (
    <div
      className={cn('flex h-full', className)}
      data-testid="card-focus-layout"
    >
      {/* Left: Hand stack */}
      <div className="flex flex-col border-r border-border/30 bg-background/50">
        <div className="px-1.5 py-2">
          <p className="text-[8px] font-bold uppercase tracking-wider text-muted-foreground/60 text-center font-nunito">
            {contextLabel}
          </p>
        </div>
        <HandStack
          cards={cards}
          focusedIdx={focusedIdx}
          onCardClick={focusCard}
        />
      </div>

      {/* Center: Focused card area */}
      <FocusedCardArea
        onSwipeLeft={swipeNext}
        onSwipeRight={swipePrev}
        hasPrev={hasPrev}
        hasNext={hasNext}
      >
        {children}
      </FocusedCardArea>
    </div>
  );
}
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/components/ui/navigation/__tests__/card-focus-layout.test.tsx`
Expected: All PASS.

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/ui/navigation/card-focus-layout.tsx apps/web/src/components/ui/navigation/__tests__/card-focus-layout.test.tsx
git commit -m "feat(mobile): add CardFocusLayout orchestrator component"
```

---

## Chunk 5: Player Card + Navigation Graph Update

> Add Player entity to the navigation system and update the navigation graph for the card-focus layout.

### Task 5.1: Add Player to navigation graph

**Files:**
- Modify: `apps/web/src/config/entity-navigation.ts`

- [ ] **Step 1: Write the failing test**

In `apps/web/src/config/__tests__/entity-navigation.test.ts`, add:

```typescript
it('should have player in the navigation graph', () => {
  expect(ENTITY_NAVIGATION_GRAPH).toHaveProperty('player');
  const playerTargets = ENTITY_NAVIGATION_GRAPH.player!;
  expect(playerTargets.length).toBeGreaterThanOrEqual(2);
});
```

- [ ] **Step 2: Run test to verify it fails or passes**

Run: `cd apps/web && pnpm vitest run src/config/__tests__/entity-navigation.test.ts`
Expected: Should PASS — player already exists in the graph (lines 161-172).

- [ ] **Step 3: Verify and commit (no-op if already present)**

Player is already in the navigation graph. No changes needed.

### Task 5.2: Full integration test

- [ ] **Step 1: Run full typecheck**

Run: `cd apps/web && pnpm tsc --noEmit`
Expected: 0 errors.

- [ ] **Step 2: Run full test suite**

Run: `cd apps/web && pnpm vitest run`
Expected: All tests PASS.

- [ ] **Step 3: Run lint**

Run: `cd apps/web && pnpm lint`
Expected: No new errors.

- [ ] **Step 4: Final commit**

```bash
git add -A
git commit -m "feat(card-content): complete KB consolidation + mobile card-focus layout components"
```

---

## Post-Implementation Checklist

- [ ] All `'document'` and `'kb_card'` entity type references removed from source and test files
- [ ] `MeepleEntityType` union has 9 members: game, player, session, agent, kb, chatSession, event, toolkit, custom
- [ ] `entityColors` has teal `174 60% 40%` for `kb` entity
- [ ] `ENTITY_NAVIGATION_GRAPH` uses `kb` key (not `document`)
- [ ] `MeepleCards.Kb` compound wrapper exported (not `.Document` or `.KbCard`)
- [ ] `ENTITY_NAV_ICONS` maps `kb` → DocumentIcon
- [ ] `useHandContext` store passes all 10 tests
- [ ] `CardCapabilityProvider` resolves capabilities for all entity types
- [ ] `FocusedCardArea` renders with swipe gesture support
- [ ] `HandStack` renders mini-cards with entity-colored accents
- [ ] `CardFocusLayout` orchestrates hand + focus area
- [ ] `pnpm tsc --noEmit` passes with 0 errors
- [ ] `pnpm vitest run` — all tests pass
- [ ] `pnpm lint` — no new warnings
