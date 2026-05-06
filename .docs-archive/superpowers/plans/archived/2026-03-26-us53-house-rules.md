# US-53: House Rules (Personal) Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Add personal house rules UI to game detail pages (Game Table layout), allowing users to view, add, and manage their house rules for each game.

**Architecture:** Frontend components for house rules display and creation, wired to existing AgentMemory API (`api.agentMemory`). The existing `agentMemoryClient.ts` already exposes `getGameMemory()` and `addHouseRule()`. A new React Query hook `useGameMemory` will coordinate data fetching. House rules are shown as a new card row inside `GameTableZoneKnowledge`, fitting the zone-based Game Table layout. Community voting is deferred to Phase 2 (separate plan).

**Tech Stack:** Next.js 16, React 19, Tailwind 4, shadcn/ui, React Query, Zod

**Parent branch:** `frontend-dev`
**PR target:** `frontend-dev`

---

## File Map

| File | Action | Responsibility |
|------|--------|---------------|
| `apps/web/src/hooks/queries/useGameMemory.ts` | **CREATE** | React Query hook for game memory (house rules + notes) |
| `apps/web/src/components/library/game-table/HouseRulesSection.tsx` | **CREATE** | House rules card row for Game Table Knowledge zone |
| `apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx` | MODIFY | Add HouseRulesSection below KB documents |
| `apps/web/src/components/library/game-table/index.ts` | MODIFY | Export HouseRulesSection |
| `apps/web/src/components/library/game-table/__tests__/HouseRulesSection.test.tsx` | **CREATE** | Unit tests for HouseRulesSection |
| `apps/web/src/components/library/game-table/__tests__/GameTableZoneKnowledge.test.tsx` | MODIFY | Add test for house rules section rendering |
| `apps/web/src/hooks/queries/__tests__/useGameMemory.test.ts` | **CREATE** | Unit tests for the hook |

---

### Task 1: Create `useGameMemory` React Query hook

**Files:**
- Create: `apps/web/src/hooks/queries/useGameMemory.ts`

- [ ] **Step 1: Create the hook with query + mutation**

```typescript
// apps/web/src/hooks/queries/useGameMemory.ts

'use client';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { toast } from 'sonner';

import { api } from '@/lib/api';
import type { GameMemoryDto } from '@/lib/api/clients/agentMemoryClient';

// ─── Query Keys ──────────────────────────────────────────────────────────────

export const gameMemoryKeys = {
  all: ['game-memory'] as const,
  detail: (gameId: string) => [...gameMemoryKeys.all, gameId] as const,
};

// ─── Queries ─────────────────────────────────────────────────────────────────

export function useGameMemory(gameId: string) {
  return useQuery<GameMemoryDto | null>({
    queryKey: gameMemoryKeys.detail(gameId),
    queryFn: () => api.agentMemory.getGameMemory(gameId),
    enabled: !!gameId,
  });
}

// ─── Mutations ───────────────────────────────────────────────────────────────

export function useAddHouseRule(gameId: string) {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: (description: string) => api.agentMemory.addHouseRule(gameId, description),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: gameMemoryKeys.detail(gameId) });
      toast.success('Regola aggiunta');
    },
    onError: () => {
      toast.error('Errore', {
        description: 'Impossibile aggiungere la regola. Riprova.',
      });
    },
  });
}
```

- [ ] **Step 2: Verify types compile**

```bash
cd apps/web && npx tsc --noEmit --pretty 2>&1 | head -30
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/queries/useGameMemory.ts
git commit -m "feat(house-rules): add useGameMemory React Query hook"
```

---

### Task 2: Create `HouseRulesSection` component

**Files:**
- Create: `apps/web/src/components/library/game-table/HouseRulesSection.tsx`

- [ ] **Step 1: Create the component**

The component follows the same dark card-row styling (`CARD_ROW`) used throughout `GameTableZoneTools` and `GameTableZoneKnowledge`. It displays the user's house rules list with an inline add input. The existing `HouseRulesDisplay` in `session/live/` is for live sessions and uses light theme — this one uses the Game Table dark theme.

```typescript
// apps/web/src/components/library/game-table/HouseRulesSection.tsx

/**
 * HouseRulesSection — House Rules card row for the Game Table Knowledge zone
 *
 * Displays the user's personal house rules for a game and allows adding new ones.
 * Uses the AgentMemory API via useGameMemory hook.
 *
 * US-53 — House Rules
 */

'use client';

import React, { useState } from 'react';

import { BookOpen, Gavel, Plus, User } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { useAddHouseRule, useGameMemory } from '@/hooks/queries/useGameMemory';
import type { HouseRuleDto } from '@/lib/api/clients/agentMemoryClient';

// ─── Types ───────────────────────────────────────────────────────────────────

export interface HouseRulesSectionProps {
  gameId: string;
}

// ─── Styling ─────────────────────────────────────────────────────────────────

const CARD_ROW = 'bg-[#21262d] rounded-lg p-3 border border-[#30363d]';

// ─── Source Badge ────────────────────────────────────────────────────────────

function SourceBadge({ source }: { source: string }) {
  if (source === 'DisputeOverride') {
    return (
      <Badge
        variant="outline"
        className="bg-red-900/30 border-red-800/50 text-red-400 gap-1 text-xs font-nunito"
      >
        <Gavel className="h-3 w-3" />
        Disputa
      </Badge>
    );
  }

  return (
    <Badge
      variant="outline"
      className="bg-amber-900/30 border-amber-800/50 text-amber-400 gap-1 text-xs font-nunito"
    >
      <User className="h-3 w-3" />
      Aggiunta
    </Badge>
  );
}

// ─── Component ───────────────────────────────────────────────────────────────

export function HouseRulesSection({ gameId }: HouseRulesSectionProps): React.ReactNode {
  const { data: gameMemory, isLoading } = useGameMemory(gameId);
  const addHouseRule = useAddHouseRule(gameId);
  const [newRule, setNewRule] = useState('');

  const rules: HouseRuleDto[] = gameMemory?.houseRules ?? [];

  const handleAdd = () => {
    const trimmed = newRule.trim();
    if (!trimmed) return;
    addHouseRule.mutate(trimmed);
    setNewRule('');
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter') {
      handleAdd();
    }
  };

  return (
    <div className={CARD_ROW} data-testid="house-rules-section">
      {/* Header */}
      <div className="flex items-center gap-2 mb-2">
        <BookOpen className="h-4 w-4 text-amber-400" />
        <span className="text-sm font-quicksand font-semibold text-[#e6edf3]">
          House Rules
        </span>
        <span
          className="ml-auto text-xs text-[#8b949e] font-nunito"
          data-testid="house-rules-count"
        >
          {isLoading ? '...' : rules.length}
        </span>
      </div>

      {/* Loading */}
      {isLoading && (
        <div className="space-y-2">
          {[1, 2].map(i => (
            <Skeleton
              key={i}
              className="h-6 bg-[#30363d] rounded"
              data-testid="house-rule-skeleton"
            />
          ))}
        </div>
      )}

      {/* Empty state */}
      {!isLoading && rules.length === 0 && (
        <p className="text-xs text-[#8b949e] font-nunito italic" data-testid="house-rules-empty">
          Nessuna regola della casa
        </p>
      )}

      {/* Rules list */}
      {!isLoading && rules.length > 0 && (
        <ul className="space-y-1.5 mb-2" role="list" data-testid="house-rules-list">
          {rules.map((rule, index) => (
            <li
              key={`${rule.description}-${index}`}
              className="flex items-start gap-2 rounded-md bg-[#161b22] px-3 py-2"
              data-testid="house-rule-item"
            >
              <span className="flex-1 text-sm text-[#e6edf3] font-nunito">
                {rule.description}
              </span>
              <SourceBadge source={rule.source} />
            </li>
          ))}
        </ul>
      )}

      {/* Add rule input */}
      <div className="flex gap-2 mt-2">
        <Input
          value={newRule}
          onChange={e => setNewRule(e.target.value)}
          onKeyDown={handleKeyDown}
          placeholder="Aggiungi regola..."
          className="flex-1 text-sm font-nunito bg-[#161b22] border-[#30363d] text-[#e6edf3] placeholder:text-[#484f58]"
          aria-label="Nuova regola della casa"
          disabled={addHouseRule.isPending}
          data-testid="house-rule-input"
        />
        <Button
          size="sm"
          variant="ghost"
          onClick={handleAdd}
          disabled={!newRule.trim() || addHouseRule.isPending}
          className="text-amber-400 hover:text-amber-300 hover:bg-[#30363d] gap-1"
          data-testid="house-rule-add-btn"
        >
          <Plus className="h-3 w-3" />
          Aggiungi
        </Button>
      </div>
    </div>
  );
}
```

- [ ] **Step 2: Verify types compile**

```bash
cd apps/web && npx tsc --noEmit --pretty 2>&1 | head -30
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/library/game-table/HouseRulesSection.tsx
git commit -m "feat(house-rules): create HouseRulesSection for Game Table"
```

---

### Task 3: Wire HouseRulesSection into GameTableZoneKnowledge

**Files:**
- Modify: `apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx`
- Modify: `apps/web/src/components/library/game-table/index.ts`

- [ ] **Step 1: Add HouseRulesSection to GameTableZoneKnowledge**

Add the import at the top:

```typescript
import { HouseRulesSection } from './HouseRulesSection';
```

Add the component inside the return JSX, between the KB Documents section and the Chat preview section. Insert it as a sibling `<div>`:

```tsx
{/* House Rules — positioned after KB docs and before Chat */}
<HouseRulesSection gameId={gameId} />
```

Place it after the closing `</div>` of the KB Documents card row (the one with `data-testid="kb-docs-section"`) and before the `{resolvedAgentId && (` chat preview block.

- [ ] **Step 2: Export from index.ts**

In `apps/web/src/components/library/game-table/index.ts`, add:

```typescript
export { HouseRulesSection } from './HouseRulesSection';
```

- [ ] **Step 3: Visual verification**

Start the dev server and navigate to `/library/games/<any-gameId>` to verify:
- The House Rules section appears in the Knowledge zone
- Loading state shows skeletons
- Empty state shows "Nessuna regola della casa"
- The add input renders and accepts text

```bash
cd apps/web && pnpm dev
# Open http://localhost:3000/library/games/<gameId>
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx
git add apps/web/src/components/library/game-table/index.ts
git commit -m "feat(house-rules): wire HouseRulesSection into Knowledge zone"
```

---

### Task 4: Unit tests for HouseRulesSection

**Files:**
- Create: `apps/web/src/components/library/game-table/__tests__/HouseRulesSection.test.tsx`

- [ ] **Step 1: Create test file**

```typescript
// apps/web/src/components/library/game-table/__tests__/HouseRulesSection.test.tsx

/**
 * HouseRulesSection Tests — US-53
 */

import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import { HouseRulesSection } from '../HouseRulesSection';

// ─── Mocks ───────────────────────────────────────────────────────────────────

const mockGetGameMemory = vi.hoisted(() => vi.fn());
const mockAddHouseRule = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    agentMemory: {
      getGameMemory: mockGetGameMemory,
      addHouseRule: mockAddHouseRule,
    },
  },
}));

const mockGameMemory = {
  id: 'mem-1',
  gameId: 'game-123',
  ownerId: 'user-1',
  houseRules: [
    { description: 'No trading on first turn', addedAt: '2026-01-01T00:00:00Z', source: 'UserAdded' },
    { description: 'Timer is 60 seconds', addedAt: '2026-01-02T00:00:00Z', source: 'DisputeOverride' },
  ],
  notes: [],
};

// ─── Tests ───────────────────────────────────────────────────────────────────

describe('HouseRulesSection', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders house rules list', async () => {
    mockGetGameMemory.mockResolvedValue(mockGameMemory);

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByText('No trading on first turn')).toBeInTheDocument();
    });
    expect(screen.getByText('Timer is 60 seconds')).toBeInTheDocument();
  });

  it('shows count in header', async () => {
    mockGetGameMemory.mockResolvedValue(mockGameMemory);

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByTestId('house-rules-count')).toHaveTextContent('2');
    });
  });

  it('shows loading skeletons', () => {
    mockGetGameMemory.mockReturnValue(new Promise(() => {}));

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    expect(screen.getAllByTestId('house-rule-skeleton')).toHaveLength(2);
  });

  it('shows empty state when no rules', async () => {
    mockGetGameMemory.mockResolvedValue({
      ...mockGameMemory,
      houseRules: [],
    });

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByTestId('house-rules-empty')).toBeInTheDocument();
    });
  });

  it('shows empty state when game memory is null', async () => {
    mockGetGameMemory.mockResolvedValue(null);

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByTestId('house-rules-empty')).toBeInTheDocument();
    });
  });

  it('shows add input', async () => {
    mockGetGameMemory.mockResolvedValue(mockGameMemory);

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByTestId('house-rule-input')).toBeInTheDocument();
    });
    expect(screen.getByTestId('house-rule-add-btn')).toBeInTheDocument();
  });

  it('disables add button when input is empty', async () => {
    mockGetGameMemory.mockResolvedValue(mockGameMemory);

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByTestId('house-rule-add-btn')).toBeDisabled();
    });
  });

  it('calls addHouseRule on button click', async () => {
    mockGetGameMemory.mockResolvedValue(mockGameMemory);
    mockAddHouseRule.mockResolvedValue(undefined);
    const user = userEvent.setup();

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByTestId('house-rule-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('house-rule-input'), 'New house rule');
    await user.click(screen.getByTestId('house-rule-add-btn'));

    await waitFor(() => {
      expect(mockAddHouseRule).toHaveBeenCalledWith('game-123', 'New house rule');
    });
  });

  it('calls addHouseRule on Enter key', async () => {
    mockGetGameMemory.mockResolvedValue(mockGameMemory);
    mockAddHouseRule.mockResolvedValue(undefined);
    const user = userEvent.setup();

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByTestId('house-rule-input')).toBeInTheDocument();
    });

    await user.type(screen.getByTestId('house-rule-input'), 'Enter key rule{Enter}');

    await waitFor(() => {
      expect(mockAddHouseRule).toHaveBeenCalledWith('game-123', 'Enter key rule');
    });
  });

  it('renders source badges correctly', async () => {
    mockGetGameMemory.mockResolvedValue(mockGameMemory);

    renderWithQuery(<HouseRulesSection gameId="game-123" />);

    await waitFor(() => {
      expect(screen.getByText('Aggiunta')).toBeInTheDocument();
    });
    expect(screen.getByText('Disputa')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run the tests**

```bash
cd apps/web && pnpm vitest run src/components/library/game-table/__tests__/HouseRulesSection.test.tsx
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/library/game-table/__tests__/HouseRulesSection.test.tsx
git commit -m "test(house-rules): add HouseRulesSection unit tests"
```

---

### Task 5: Unit tests for useGameMemory hook

**Files:**
- Create: `apps/web/src/hooks/queries/__tests__/useGameMemory.test.ts`

- [ ] **Step 1: Create test file**

```typescript
// apps/web/src/hooks/queries/__tests__/useGameMemory.test.ts

/**
 * useGameMemory Hook Tests — US-53
 */

import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { createQueryWrapper } from '@/__tests__/utils/query-test-utils';

import { useGameMemory, useAddHouseRule, gameMemoryKeys } from '../useGameMemory';

const mockGetGameMemory = vi.hoisted(() => vi.fn());
const mockAddHouseRule = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    agentMemory: {
      getGameMemory: mockGetGameMemory,
      addHouseRule: mockAddHouseRule,
    },
  },
}));

// Suppress sonner toasts in tests
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const mockMemory = {
  id: 'mem-1',
  gameId: 'game-123',
  ownerId: 'user-1',
  houseRules: [
    { description: 'Rule 1', addedAt: '2026-01-01T00:00:00Z', source: 'UserAdded' },
  ],
  notes: [],
};

describe('useGameMemory', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('fetches game memory', async () => {
    mockGetGameMemory.mockResolvedValue(mockMemory);

    const { result } = renderHook(() => useGameMemory('game-123'), {
      wrapper: createQueryWrapper(),
    });

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(result.current.data).toEqual(mockMemory);
    expect(mockGetGameMemory).toHaveBeenCalledWith('game-123');
  });

  it('does not fetch when gameId is empty', () => {
    renderHook(() => useGameMemory(''), {
      wrapper: createQueryWrapper(),
    });

    expect(mockGetGameMemory).not.toHaveBeenCalled();
  });

  it('exposes query keys', () => {
    expect(gameMemoryKeys.all).toEqual(['game-memory']);
    expect(gameMemoryKeys.detail('game-123')).toEqual(['game-memory', 'game-123']);
  });
});

describe('useAddHouseRule', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('calls addHouseRule API', async () => {
    mockAddHouseRule.mockResolvedValue(undefined);

    const { result } = renderHook(() => useAddHouseRule('game-123'), {
      wrapper: createQueryWrapper(),
    });

    result.current.mutate('New rule');

    await waitFor(() => {
      expect(result.current.isSuccess).toBe(true);
    });

    expect(mockAddHouseRule).toHaveBeenCalledWith('game-123', 'New rule');
  });
});
```

**Note:** If `createQueryWrapper` is not exported from the test utils, use the same wrapper pattern found in other hook tests in the repo (check `apps/web/src/__tests__/utils/query-test-utils.ts`).

- [ ] **Step 2: Run the tests**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useGameMemory.test.ts
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/hooks/queries/__tests__/useGameMemory.test.ts
git commit -m "test(house-rules): add useGameMemory hook tests"
```

---

### Task 6: Update GameTableZoneKnowledge test

**Files:**
- Modify: `apps/web/src/components/library/game-table/__tests__/GameTableZoneKnowledge.test.tsx`

- [ ] **Step 1: Add mock for agentMemory client**

In the existing `vi.mock('@/lib/api', ...)` block, add `agentMemory` alongside existing mocks:

```typescript
agentMemory: {
  getGameMemory: vi.fn().mockResolvedValue(null),
  addHouseRule: vi.fn().mockResolvedValue(undefined),
},
```

- [ ] **Step 2: Add test for house rules section presence**

```typescript
it('renders house rules section', async () => {
  // ... render with standard props ...
  await waitFor(() => {
    expect(screen.getByTestId('house-rules-section')).toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Run the test file**

```bash
cd apps/web && pnpm vitest run src/components/library/game-table/__tests__/GameTableZoneKnowledge.test.tsx
```

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/library/game-table/__tests__/GameTableZoneKnowledge.test.tsx
git commit -m "test(house-rules): update Knowledge zone test for house rules"
```

---

### Task 7: Quality checks + PR

- [ ] **Step 1: Run all affected tests**

```bash
cd apps/web && pnpm vitest run src/components/library/game-table/__tests__/ src/hooks/queries/__tests__/useGameMemory.test.ts
```

- [ ] **Step 2: TypeScript check**

```bash
cd apps/web && npx tsc --noEmit --pretty 2>&1 | tail -5
```

- [ ] **Step 3: Lint**

```bash
cd apps/web && pnpm lint
```

- [ ] **Step 4: Create PR**

```bash
# From the feature branch (e.g. feature/us-53-house-rules)
gh pr create \
  --base frontend-dev \
  --title "feat(house-rules): personal house rules UI in Game Table" \
  --body "## Summary
- Add useGameMemory React Query hook for AgentMemory game memory
- Create HouseRulesSection component (dark theme, Game Table style)
- Wire into GameTableZoneKnowledge zone
- Full unit test coverage

## Test plan
- [ ] HouseRulesSection renders rules from API
- [ ] Empty state displays correctly
- [ ] Add rule via button click works
- [ ] Add rule via Enter key works
- [ ] Loading skeletons display
- [ ] Source badges (UserAdded / DisputeOverride) render correctly
- [ ] Knowledge zone shows house rules section
- [ ] TypeScript compiles clean
- [ ] Lint passes

Closes #53"
```

- [ ] **Step 5: Code review**

Run `/code-review:code-review <PR-URL>` on the PR.

- [ ] **Step 6: Merge and cleanup**

```bash
# After approval
gh pr merge <PR-NUMBER> --squash
git checkout frontend-dev && git pull
git branch -D feature/us-53-house-rules
git remote prune origin
```

- [ ] **Step 7: Update issue**

Update issue #53 status to Done on GitHub.

---

## Phase 2: Community Voting (DEFERRED — separate plan)

This plan covers only personal house rules (Phase 1). The community voting feature requires backend work and should be planned separately:

**Backend additions needed:**
- `HouseRuleVote` entity in AgentMemory BC
- `POST /api/v1/agent-memory/games/{gameId}/house-rules/{ruleId}/vote` (upvote/downvote)
- `GET /api/v1/agent-memory/games/{gameId}/house-rules/public` (community rules, sorted by votes)
- `PublicHouseRuleDto` with vote counts

**Frontend additions needed:**
- Vote buttons (up/down) on each rule
- Public house rules tab/section (community rules, read-only for other users)
- Sort by votes, filter by source
- Separate API client methods and React Query hooks

---

## Architecture Notes

### Why GameTableZoneKnowledge and not a sub-page?

The existing game detail uses a zone-based Game Table layout (`GameTableLayout`) with three zones: Tools, Knowledge, Sessions. House rules are game knowledge — they belong in the Knowledge zone alongside KB documents and chat. A separate `/games/[id]/house-rules` sub-page would break the established pattern. Sub-pages (`/games/[id]/faqs`, `/games/[id]/strategies`) exist for the public game catalog (`(authenticated)/games/[id]/*`), not for the library game table (`(authenticated)/library/games/[gameId]/*`).

### Existing infrastructure reused

- `agentMemoryClient.ts` — already has `getGameMemory()` and `addHouseRule()` with Zod validation
- `HouseRuleDto` / `GameMemoryDto` types — already defined and exported
- `api.agentMemory` — already wired in `lib/api/index.ts`
- Dark card row styling — reused from `GameTableZoneKnowledge` and `GameTableZoneTools`
- `renderWithQuery` test utility — reused from existing test infrastructure
