# Game Table Detail Page — Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Replace the game detail page with a board-game table layout — MeepleCard hero center, 3 thematic zones, drawer overlay, responsive (3-col grid → accordion).

**Architecture:** New `GameTableLayout` component composes existing building blocks (MeepleCard, TavoloSection, ManaLinkFooter, StatusBadges) into a responsive 3-zone grid. A Zustand micro-store manages drawer state. The authenticated page is rewritten; the public page becomes a minimal preview. 11 legacy files are deleted (the `game-detail/` directory).

**Tech Stack:** Next.js 16 App Router, React 19, Tailwind 4, Zustand, React Query, framer-motion, Vitest + React Testing Library

**Spec:** `docs/superpowers/specs/2026-03-19-game-table-detail-design.md`

---

## Phase Overview

| Phase | Description | Depends On | Delivers |
|-------|-------------|------------|----------|
| 0 | Extract shared agent hooks (pre-requisite) | — | Shared `useAgentData.ts` |
| 1 | Zustand drawer store + GameTableLayout | — | Store + responsive layout shell |
| 2 | GameTableDrawer overlay | Phase 1 | Drawer with animation + dismiss |
| 3 | Zone components (Tools, Knowledge, Sessions) | Phase 0, 1 | 3 zone panels |
| 4 | GameStatsPanel | Phase 1 | Stats drawer content |
| 5 | Authenticated page assembly | Phase 1-4 | Working `/library/games/[gameId]` |
| 6 | Public page preview | Phase 1 | Minimal `/games/[id]` |
| 7 | Legacy cleanup | Phase 5, 6 | 11 files deleted (game-detail/ directory) |
| 8 | Verification | Phase 7 | Typecheck + lint + no dead imports |

---

## Phase 0: Extract Shared Agent Hooks

### Task 0.1: Create shared useAgentData hooks

**Files:**
- Create: `apps/web/src/hooks/queries/useAgentData.ts`
- Test: `apps/web/src/hooks/queries/__tests__/useAgentData.test.ts`
- Modify: `apps/web/src/components/agent/AgentCharacterSheet.tsx`

**Context:** `useAgentKbDocs` and `useAgentThreads` are duplicated inline in `AgentCharacterSheet.tsx` and `AgentExtraMeepleCard.tsx`. Extract them into shared React Query hooks. The inline versions use raw `fetch` + `useState` + `useEffect`; convert to `useQuery` for consistency with project patterns.

**Note:** `useAgentStatus` already exists at `apps/web/src/hooks/useAgentStatus.ts` as a standalone hook. Do NOT duplicate it — reuse it directly in `GameTableZoneKnowledge`. Only extract `useAgentKbDocs` and `useAgentThreads`.

- [ ] **Step 1: Write failing tests for useAgentKbDocs**

```typescript
// apps/web/src/hooks/queries/__tests__/useAgentData.test.tsx
/**
 * @vitest-environment jsdom
 */
import { type ReactNode } from 'react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { useAgentKbDocs, useAgentThreads } from '../useAgentData';

// Mock fetch
const mockFetch = vi.fn();
global.fetch = mockFetch;

function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: { queries: { retry: false } },
  });
  return function Wrapper({ children }: { children: ReactNode }) {
    return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
  };
}

describe('useAgentKbDocs', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns docs for a valid gameId', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [{ id: 'doc-1', fileName: 'rules.pdf', status: 'Indexed' }],
    });

    const { result } = renderHook(() => useAgentKbDocs('game-123'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data).toHaveLength(1);
    expect(result.current.data?.[0].id).toBe('doc-1');
  });

  it('is disabled when gameId is empty', () => {
    const { result } = renderHook(() => useAgentKbDocs(''), {
      wrapper: createWrapper(),
    });

    expect(result.current.isLoading).toBe(false);
    expect(result.current.data).toBeUndefined();
  });
});

describe('useAgentThreads', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('returns threads for a valid agentId', async () => {
    mockFetch.mockResolvedValueOnce({
      ok: true,
      json: async () => [{ id: 'thread-1', topic: 'Rules question', lastMessageAt: '2026-03-19T10:00:00Z' }],
    });

    const { result } = renderHook(() => useAgentThreads('agent-123'), {
      wrapper: createWrapper(),
    });

    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.data).toHaveLength(1);
  });
});
```

- [ ] **Step 2: Run tests to verify they fail**

Run: `cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useAgentData.test.ts`
Expected: FAIL — module `useAgentData` not found

- [ ] **Step 3: Implement useAgentData hooks**

Read the inline implementations in `AgentCharacterSheet.tsx` (search for `function useAgentKbDocs` and `function useAgentThreads`). Extract them as `useQuery`-based hooks:

```typescript
// apps/web/src/hooks/queries/useAgentData.ts
import { useQuery, type UseQueryResult } from '@tanstack/react-query';

// ============================================================================
// Types
// ============================================================================

export interface KbDocumentPreview {
  id: string;
  fileName: string;
  status: string;
  uploadedAt?: string;
  pageCount?: number;
}

export interface ChatThreadPreview {
  id: string;
  topic: string;
  lastMessageAt: string;
  messageCount?: number;
}

export interface AgentStatus {
  isReady: boolean;
  documentCount: number;
  ragStatus: string;
  blockingReason?: string | null;
}

// ============================================================================
// Query Keys
// ============================================================================

export const agentDataKeys = {
  all: ['agent-data'] as const,
  kbDocs: (gameId: string) => [...agentDataKeys.all, 'kb-docs', gameId] as const,
  threads: (agentId: string) => [...agentDataKeys.all, 'threads', agentId] as const,
  status: (agentId: string) => [...agentDataKeys.all, 'status', agentId] as const,
};

// ============================================================================
// Hooks
// ============================================================================

export function useAgentKbDocs(
  gameId: string,
): UseQueryResult<KbDocumentPreview[]> {
  return useQuery({
    queryKey: agentDataKeys.kbDocs(gameId),
    queryFn: async () => {
      const res = await fetch(`/api/v1/knowledge-base/${gameId}/documents`);
      if (!res.ok) throw new Error('Failed to fetch KB docs');
      return (await res.json()) as KbDocumentPreview[];
    },
    enabled: !!gameId,
    staleTime: 60_000,
  });
}

export function useAgentThreads(
  agentId: string,
): UseQueryResult<ChatThreadPreview[]> {
  return useQuery({
    queryKey: agentDataKeys.threads(agentId),
    queryFn: async () => {
      const res = await fetch(`/api/v1/chat-threads/my?agentId=${agentId}`);
      if (!res.ok) throw new Error('Failed to fetch threads');
      return (await res.json()) as ChatThreadPreview[];
    },
    enabled: !!agentId,
    staleTime: 30_000,
  });
}

// NOTE: useAgentStatus already exists at '@/hooks/useAgentStatus' — reuse it, don't duplicate.
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useAgentData.test.ts`
Expected: PASS

- [ ] **Step 5: Update AgentCharacterSheet to use shared hooks**

In `apps/web/src/components/agent/AgentCharacterSheet.tsx`:
1. Remove the inline `useAgentKbDocs` and `useAgentThreads` function definitions
2. Add import: `import { useAgentKbDocs, useAgentThreads } from '@/hooks/queries/useAgentData';`
3. Update call sites — the shared hooks return `{ data, isLoading }` instead of `{ docs, loading }` / `{ threads, loading }`. Update destructuring accordingly.

Do the same for `AgentExtraMeepleCard.tsx` if it contains duplicates.

- [ ] **Step 6: Verify existing tests still pass**

Run: `cd apps/web && pnpm vitest run src/components/agent/`
Expected: All existing agent tests PASS

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/hooks/queries/useAgentData.ts apps/web/src/hooks/queries/__tests__/useAgentData.test.ts apps/web/src/components/agent/
git commit -m "refactor(hooks): extract shared useAgentKbDocs, useAgentThreads, useAgentStatus hooks"
```

---

## Phase 1: Foundation — Store + Layout

### Task 1.1: Create useGameTableDrawer Zustand store

**Files:**
- Create: `apps/web/src/lib/stores/gameTableDrawerStore.ts`
- Test: `apps/web/src/lib/stores/__tests__/gameTableDrawerStore.test.ts`

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/lib/stores/__tests__/gameTableDrawerStore.test.ts
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, beforeEach } from 'vitest';
import { act } from '@testing-library/react';

import { useGameTableDrawer } from '../gameTableDrawerStore';

describe('useGameTableDrawer', () => {
  beforeEach(() => {
    act(() => useGameTableDrawer.getState().close());
  });

  it('starts closed with no content', () => {
    const state = useGameTableDrawer.getState();
    expect(state.isOpen).toBe(false);
    expect(state.content).toBeNull();
  });

  it('opens with typed content', () => {
    act(() => useGameTableDrawer.getState().open({ type: 'chat', agentId: 'a1' }));

    const state = useGameTableDrawer.getState();
    expect(state.isOpen).toBe(true);
    expect(state.content).toEqual({ type: 'chat', agentId: 'a1' });
  });

  it('closes and clears content', () => {
    act(() => useGameTableDrawer.getState().open({ type: 'stats', gameId: 'g1' }));
    act(() => useGameTableDrawer.getState().close());

    const state = useGameTableDrawer.getState();
    expect(state.isOpen).toBe(false);
    expect(state.content).toBeNull();
  });

  it('replaces content when opened again', () => {
    act(() => useGameTableDrawer.getState().open({ type: 'chat', agentId: 'a1' }));
    act(() => useGameTableDrawer.getState().open({ type: 'kb', gameId: 'g1' }));

    const state = useGameTableDrawer.getState();
    expect(state.content).toEqual({ type: 'kb', gameId: 'g1' });
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/gameTableDrawerStore.test.ts`
Expected: FAIL — module not found

- [ ] **Step 3: Implement the store**

```typescript
// apps/web/src/lib/stores/gameTableDrawerStore.ts
import { create } from 'zustand';
import { devtools } from 'zustand/middleware';

// ============================================================================
// Types
// ============================================================================

export type DrawerContent =
  | { type: 'chat'; agentId: string; threadId?: string }
  | { type: 'stats'; gameId: string }
  | { type: 'kb'; gameId: string }
  | { type: 'toolkit'; gameId: string }
  | { type: 'document'; documentId: string }
  | { type: 'session'; sessionId: string };

interface GameTableDrawerState {
  isOpen: boolean;
  content: DrawerContent | null;
  open: (content: DrawerContent) => void;
  close: () => void;
}

// ============================================================================
// Store
// ============================================================================

export const useGameTableDrawer = create<GameTableDrawerState>()(
  devtools(
    (set) => ({
      isOpen: false,
      content: null,

      open: (content) => set({ isOpen: true, content }, false, 'open'),
      close: () => set({ isOpen: false, content: null }, false, 'close'),
    }),
    { name: 'GameTableDrawer' }
  )
);
```

- [ ] **Step 4: Run test to verify it passes**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/gameTableDrawerStore.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/lib/stores/gameTableDrawerStore.ts apps/web/src/lib/stores/__tests__/gameTableDrawerStore.test.ts
git commit -m "feat(store): add useGameTableDrawer Zustand store with discriminated union types"
```

---

### Task 1.2: Create GameTableLayout component

**Files:**
- Create: `apps/web/src/components/library/game-table/GameTableLayout.tsx`
- Test: `apps/web/src/components/library/game-table/__tests__/GameTableLayout.test.tsx`

**Reference:** Read `apps/web/src/components/dashboard/tavolo/TavoloLayout.tsx` for dark theme patterns. Read `apps/web/src/components/dashboard/tavolo/TavoloSection.tsx` for zone header pattern.

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/library/game-table/__tests__/GameTableLayout.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { GameTableLayout } from '../GameTableLayout';

describe('GameTableLayout', () => {
  const defaultProps = {
    card: <div data-testid="card">Card</div>,
    toolsZone: <div data-testid="tools">Tools</div>,
    knowledgeZone: <div data-testid="knowledge">Knowledge</div>,
    sessionsZone: <div data-testid="sessions">Sessions</div>,
  };

  it('renders card and all zones', () => {
    render(<GameTableLayout {...defaultProps} />);

    expect(screen.getByTestId('card')).toBeInTheDocument();
    expect(screen.getByTestId('tools')).toBeInTheDocument();
    expect(screen.getByTestId('knowledge')).toBeInTheDocument();
    expect(screen.getByTestId('sessions')).toBeInTheDocument();
  });

  it('renders drawer when drawerOpen is true', () => {
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div data-testid="drawer-content">Drawer</div>}
        drawerOpen={true}
        onDrawerClose={vi.fn()}
      />
    );

    expect(screen.getByTestId('drawer-content')).toBeInTheDocument();
  });

  it('does not render drawer when drawerOpen is false', () => {
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div data-testid="drawer-content">Drawer</div>}
        drawerOpen={false}
        onDrawerClose={vi.fn()}
      />
    );

    expect(screen.queryByTestId('drawer-content')).not.toBeInTheDocument();
  });

  it('calls onDrawerClose when backdrop is clicked', () => {
    const onClose = vi.fn();
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div>Drawer</div>}
        drawerOpen={true}
        onDrawerClose={onClose}
      />
    );

    const backdrop = screen.getByTestId('drawer-backdrop');
    fireEvent.click(backdrop);
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('calls onDrawerClose on Escape key', () => {
    const onClose = vi.fn();
    render(
      <GameTableLayout
        {...defaultProps}
        drawer={<div>Drawer</div>}
        drawerOpen={true}
        onDrawerClose={onClose}
      />
    );

    fireEvent.keyDown(document, { key: 'Escape' });
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('renders zone headers with TavoloSection pattern', () => {
    render(<GameTableLayout {...defaultProps} />);

    expect(screen.getByText('Strumenti')).toBeInTheDocument();
    expect(screen.getByText('Conoscenza')).toBeInTheDocument();
    expect(screen.getByText('Sessioni')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/library/game-table/__tests__/GameTableLayout.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement GameTableLayout**

```typescript
// apps/web/src/components/library/game-table/GameTableLayout.tsx
'use client';

import { type ReactNode, useState, useEffect, useCallback } from 'react';

import { AnimatePresence, motion } from 'framer-motion';

import { TavoloSection } from '@/components/dashboard/tavolo';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface GameTableLayoutProps {
  card: ReactNode;
  toolsZone: ReactNode;
  knowledgeZone: ReactNode;
  sessionsZone: ReactNode;
  drawer?: ReactNode;
  drawerOpen?: boolean;
  onDrawerClose?: () => void;
}

// ============================================================================
// Accordion (mobile only)
// ============================================================================

type ZoneKey = 'tools' | 'knowledge' | 'sessions';

const ZONES: { key: ZoneKey; icon: string; title: string }[] = [
  { key: 'tools', icon: '🧰', title: 'Strumenti' },
  { key: 'knowledge', icon: '📚', title: 'Conoscenza' },
  { key: 'sessions', icon: '📊', title: 'Sessioni' },
];

// ============================================================================
// Component
// ============================================================================

export function GameTableLayout({
  card,
  toolsZone,
  knowledgeZone,
  sessionsZone,
  drawer,
  drawerOpen = false,
  onDrawerClose,
}: GameTableLayoutProps) {
  const [openZone, setOpenZone] = useState<ZoneKey | null>(null);
  const [isFocused, setIsFocused] = useState(false);

  const toggleZone = useCallback((key: ZoneKey) => {
    setOpenZone((prev) => (prev === key ? null : key));
  }, []);

  const zoneContent: Record<ZoneKey, ReactNode> = {
    tools: toolsZone,
    knowledge: knowledgeZone,
    sessions: sessionsZone,
  };

  // Escape key to close drawer
  useEffect(() => {
    if (!drawerOpen) return;
    const handler = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onDrawerClose?.();
    };
    document.addEventListener('keydown', handler);
    return () => document.removeEventListener('keydown', handler);
  }, [drawerOpen, onDrawerClose]);

  return (
    <div className="relative min-h-screen bg-[#0d1117]">
      {/* ============ DESKTOP/TABLET: 3-col grid ============ */}
      <div className="hidden sm:grid sm:grid-cols-[1fr_1.5fr_1fr] lg:grid-cols-[1.2fr_2fr_1.2fr] sm:grid-rows-[1fr_auto] gap-4 p-4 max-w-7xl mx-auto min-h-[80vh]">
        {/* Left zone: Tools */}
        <div className="min-w-0">
          <TavoloSection icon="🧰" title="Strumenti">
            {toolsZone}
          </TavoloSection>
        </div>

        {/* Center: MeepleCard */}
        <div className="flex items-start justify-center pt-8">
          {card}
        </div>

        {/* Right zone: Knowledge */}
        <div className="min-w-0">
          <TavoloSection icon="📚" title="Conoscenza">
            {knowledgeZone}
          </TavoloSection>
        </div>

        {/* Bottom: Sessions (full width) */}
        <div className="sm:col-span-3">
          <TavoloSection icon="📊" title="Sessioni">
            {sessionsZone}
          </TavoloSection>
        </div>
      </div>

      {/* ============ MOBILE: Card + Accordions ============ */}
      <div className="sm:hidden flex flex-col">
        {/* Card area */}
        <motion.div
          layout
          className={cn(
            'flex items-center justify-center px-4 pt-4 transition-all',
            isFocused ? 'min-h-[85vh]' : 'min-h-[60vh]',
          )}
          onClick={() => setIsFocused((prev) => !prev)}
        >
          {card}
        </motion.div>

        {/* "Back to table" button in focus mode */}
        {isFocused && (
          <motion.button
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            className="mx-auto mb-4 text-sm text-[#8b949e] underline"
            onClick={(e) => {
              e.stopPropagation();
              setIsFocused(false);
            }}
          >
            Torna al tavolo
          </motion.button>
        )}

        {/* Accordion zones */}
        {!isFocused && (
          <div className="px-4 pb-4 space-y-2">
            {ZONES.map(({ key, icon, title }) => (
              <div key={key} className="border border-[#30363d] rounded-lg overflow-hidden">
                <button
                  className="w-full flex items-center gap-2 px-4 py-3 text-left bg-[#161b22] hover:bg-[#21262d] transition-colors"
                  onClick={() => toggleZone(key)}
                  aria-expanded={openZone === key}
                  aria-controls={`zone-${key}`}
                >
                  <span className="text-sm">{icon}</span>
                  <span className="text-sm font-semibold uppercase tracking-wider text-[#8b949e]">
                    {title}
                  </span>
                  <span className="ml-auto text-[#484f58] text-xs">
                    {openZone === key ? '▲' : '▼'}
                  </span>
                </button>
                <AnimatePresence>
                  {openZone === key && (
                    <motion.div
                      id={`zone-${key}`}
                      initial={{ height: 0, opacity: 0 }}
                      animate={{ height: 'auto', opacity: 1 }}
                      exit={{ height: 0, opacity: 0 }}
                      transition={{ duration: 0.2 }}
                      className="overflow-hidden"
                    >
                      <div className="p-4 bg-[#0d1117]">
                        {zoneContent[key]}
                      </div>
                    </motion.div>
                  )}
                </AnimatePresence>
              </div>
            ))}
          </div>
        )}
      </div>

      {/* ============ DRAWER OVERLAY ============ */}
      <AnimatePresence>
        {drawerOpen && drawer && (
          <>
            {/* Backdrop */}
            <motion.div
              data-testid="drawer-backdrop"
              initial={{ opacity: 0 }}
              animate={{ opacity: 1 }}
              exit={{ opacity: 0 }}
              className="fixed inset-0 z-50 bg-black/60 backdrop-blur-sm"
              onClick={onDrawerClose}
            />
            {/* Drawer panel — TODO: wrap in focus-trap-react or use <dialog> for focus trap */}
            <motion.div
              role="dialog"
              aria-modal="true"
              initial={{ y: '100%', opacity: 0 }}
              animate={{ y: 0, opacity: 1 }}
              exit={{ y: '100%', opacity: 0 }}
              transition={{ type: 'spring', damping: 25, stiffness: 300 }}
              className={cn(
                'fixed z-50 bg-[#161b22] border border-[#30363d] overflow-hidden',
                // Mobile: bottom sheet
                'inset-x-0 bottom-0 h-[90vh] rounded-t-2xl',
                // Tablet
                'sm:inset-auto sm:top-[10%] sm:left-[7.5%] sm:right-[7.5%] sm:bottom-auto sm:h-[80vh] sm:rounded-2xl',
                // Desktop
                'lg:left-[20%] lg:right-[20%] lg:top-[15%] lg:h-[70vh]',
              )}
            >
              {drawer}
            </motion.div>
          </>
        )}
      </AnimatePresence>
    </div>
  );
}
```

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/library/game-table/__tests__/GameTableLayout.test.tsx`
Expected: PASS

- [ ] **Step 5: Create barrel export**

```typescript
// apps/web/src/components/library/game-table/index.ts
export { GameTableLayout } from './GameTableLayout';
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/game-table/
git commit -m "feat(game-table): add GameTableLayout with responsive 3-zone grid and accordion"
```

---

## Phase 2: GameTableDrawer

### Task 2.1: Create GameTableDrawer overlay

**Files:**
- Create: `apps/web/src/components/library/game-table/GameTableDrawer.tsx`
- Test: `apps/web/src/components/library/game-table/__tests__/GameTableDrawer.test.tsx`

**Reference:** Read `apps/web/src/lib/stores/gameTableDrawerStore.ts` for `DrawerContent` type.

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/library/game-table/__tests__/GameTableDrawer.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { GameTableDrawer } from '../GameTableDrawer';
import type { DrawerContent } from '@/lib/stores/gameTableDrawerStore';

// Mock heavy children
vi.mock('@/components/chat/ChatThreadView', () => ({
  ChatThreadView: () => <div data-testid="chat-thread">Chat</div>,
}));

describe('GameTableDrawer', () => {
  it('renders header with close button', () => {
    const content: DrawerContent = { type: 'stats', gameId: 'g1' };
    render(<GameTableDrawer content={content} onClose={vi.fn()} />);

    expect(screen.getByText('Statistiche')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /chiudi/i })).toBeInTheDocument();
  });

  it('calls onClose when close button clicked', () => {
    const onClose = vi.fn();
    const content: DrawerContent = { type: 'kb', gameId: 'g1' };
    render(<GameTableDrawer content={content} onClose={onClose} />);

    fireEvent.click(screen.getByRole('button', { name: /chiudi/i }));
    expect(onClose).toHaveBeenCalledOnce();
  });

  it('renders correct title for each content type', () => {
    const titles: Record<string, string> = {
      chat: 'Chat AI',
      stats: 'Statistiche',
      kb: 'Knowledge Base',
      toolkit: 'Toolkit',
    };

    for (const [type, title] of Object.entries(titles)) {
      const { unmount } = render(
        <GameTableDrawer content={{ type, gameId: 'g1' } as DrawerContent} onClose={vi.fn()} />
      );
      expect(screen.getByText(title)).toBeInTheDocument();
      unmount();
    }
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm vitest run src/components/library/game-table/__tests__/GameTableDrawer.test.tsx`
Expected: FAIL

- [ ] **Step 3: Implement GameTableDrawer**

```typescript
// apps/web/src/components/library/game-table/GameTableDrawer.tsx
'use client';

import { lazy, Suspense } from 'react';

import { X } from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import type { DrawerContent } from '@/lib/stores/gameTableDrawerStore';

// Lazy load heavy drawer content
// NOTE: ChatThreadView requires a threadId (not agentId). For the 'chat' drawer type,
// if threadId is provided, render ChatThreadView directly.
// If only agentId is provided (no threadId), render an agent chat landing that lists
// threads (from useAgentThreads) and lets the user pick one, or creates a new thread.
// Read the actual ChatThreadView at apps/web/src/components/chat-unified/ChatThreadView.tsx
// to confirm its exact props before implementing.
const ChatThreadView = lazy(() =>
  import('@/components/chat-unified/ChatThreadView').then((m) => ({ default: m.ChatThreadView }))
);
const GameStatsPanel = lazy(() =>
  import('./GameStatsPanel').then((m) => ({ default: m.GameStatsPanel }))
);

// ============================================================================
// Types
// ============================================================================

interface GameTableDrawerProps {
  content: DrawerContent;
  onClose: () => void;
}

const DRAWER_TITLES: Record<DrawerContent['type'], string> = {
  chat: 'Chat AI',
  stats: 'Statistiche',
  kb: 'Knowledge Base',
  toolkit: 'Toolkit',
  document: 'Documento',
  session: 'Sessione',
};

const DRAWER_ICONS: Record<DrawerContent['type'], string> = {
  chat: '💬',
  stats: '📊',
  kb: '📚',
  toolkit: '🧰',
  document: '📄',
  session: '🎲',
};

// ============================================================================
// Component
// ============================================================================

export function GameTableDrawer({ content, onClose }: GameTableDrawerProps) {
  return (
    <div className="flex flex-col h-full">
      {/* Sticky header */}
      <div className="flex items-center justify-between px-4 py-3 border-b border-[#30363d] bg-[#161b22] flex-shrink-0">
        <div className="flex items-center gap-2">
          <span className="text-sm">{DRAWER_ICONS[content.type]}</span>
          <h2 className="text-base font-semibold text-[#e6edf3] font-quicksand">
            {DRAWER_TITLES[content.type]}
          </h2>
        </div>
        <Button
          variant="ghost"
          size="sm"
          onClick={onClose}
          aria-label="Chiudi"
          className="text-[#8b949e] hover:text-[#e6edf3]"
        >
          <X className="h-4 w-4" />
        </Button>
      </div>

      {/* Scrollable content */}
      <div className="flex-1 overflow-y-auto p-4">
        <Suspense fallback={<DrawerSkeleton />}>
          <DrawerContentRenderer content={content} />
        </Suspense>
      </div>
    </div>
  );
}

// ============================================================================
// Content Router
// ============================================================================

function DrawerContentRenderer({ content }: { content: DrawerContent }) {
  switch (content.type) {
    case 'chat':
      // ChatThreadView expects { threadId: string }. If we only have agentId,
      // show a thread picker first. Read the actual component to confirm props.
      if (content.threadId) {
        return <ChatThreadView threadId={content.threadId} />;
      }
      return <div className="text-[#8b949e]">Select a chat thread for agent {content.agentId}</div>;
    case 'stats':
      return <GameStatsPanel gameId={content.gameId} />;
    case 'kb':
      return <div className="text-[#8b949e]">Knowledge Base — {content.gameId}</div>;
    case 'toolkit':
      return <div className="text-[#8b949e]">Toolkit — {content.gameId}</div>;
    case 'document':
      return <div className="text-[#8b949e]">Document — {content.documentId}</div>;
    case 'session':
      return <div className="text-[#8b949e]">Session — {content.sessionId}</div>;
    default:
      return null;
  }
}

function DrawerSkeleton() {
  return (
    <div className="space-y-3">
      <Skeleton className="h-8 w-3/4" />
      <Skeleton className="h-4 w-full" />
      <Skeleton className="h-4 w-5/6" />
      <Skeleton className="h-32 w-full" />
    </div>
  );
}
```

Note: `ChatThreadView` import path and props need verification — read the actual component before implementing. The `kb`, `toolkit`, `document`, and `session` content types use placeholder divs initially; they'll be wired to real components in Phase 3+ or as separate follow-up tasks.

- [ ] **Step 4: Run tests to verify they pass**

Run: `cd apps/web && pnpm vitest run src/components/library/game-table/__tests__/GameTableDrawer.test.tsx`
Expected: PASS

- [ ] **Step 5: Update barrel export**

Add to `apps/web/src/components/library/game-table/index.ts`:
```typescript
export { GameTableDrawer } from './GameTableDrawer';
```

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/game-table/
git commit -m "feat(game-table): add GameTableDrawer overlay with lazy content routing"
```

---

## Phase 3: Zone Components

### Task 3.1: GameTableZoneTools

**Files:**
- Create: `apps/web/src/components/library/game-table/GameTableZoneTools.tsx`
- Test: `apps/web/src/components/library/game-table/__tests__/GameTableZoneTools.test.tsx`

**Reference:** Read existing `DeclareOwnershipButton`, `RagAccessBadge`, `RelatedEntitiesSection`, `EditNotesModal`, `RemoveGameDialog` to understand their props.

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/library/game-table/__tests__/GameTableZoneTools.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi } from 'vitest';
import { render, screen, fireEvent } from '@testing-library/react';

import { GameTableZoneTools } from '../GameTableZoneTools';

// Mock heavy dependencies
vi.mock('@/components/library/DeclareOwnershipButton', () => ({
  DeclareOwnershipButton: () => <div data-testid="ownership-btn">Ownership</div>,
}));
vi.mock('@/components/library/RagAccessBadge', () => ({
  RagAccessBadge: () => <div data-testid="rag-badge">RAG</div>,
}));
vi.mock('@/components/ui/data-display/entity-link/related-entities-section', () => ({
  RelatedEntitiesSection: () => <div data-testid="related-entities">Links</div>,
}));

const mockGameDetail = {
  gameId: 'g1',
  gameTitle: 'Catan',
  notes: 'My strategy notes',
  currentState: 'Owned',
  hasRagAccess: true,
} as any;

describe('GameTableZoneTools', () => {
  it('renders toolkit link', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g1" />);
    const link = screen.getByRole('link', { name: /toolkit/i });
    expect(link).toHaveAttribute('href', '/library/games/g1/toolkit');
  });

  it('renders notes preview', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g1" />);
    expect(screen.getByText(/My strategy notes/)).toBeInTheDocument();
  });

  it('renders ownership and RAG badge', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g1" />);
    expect(screen.getByTestId('ownership-btn')).toBeInTheDocument();
    expect(screen.getByTestId('rag-badge')).toBeInTheDocument();
  });

  it('renders remove game button', () => {
    render(<GameTableZoneTools gameDetail={mockGameDetail} gameId="g1" />);
    expect(screen.getByRole('button', { name: /rimuovi/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement GameTableZoneTools**

The component receives `gameDetail: LibraryGameDetail` and `gameId: string` as props. It renders:
1. Toolkit link (`<Link href={/library/games/${gameId}/toolkit}>`)
2. Notes preview with "Edit" button that opens `EditNotesModal`
3. `RelatedEntitiesSection` for entity links
4. `DeclareOwnershipButton` + `RagAccessBadge`
5. "Rimuovi dalla libreria" danger button that opens `RemoveGameDialog`

Style: Dark theme items with `bg-[#21262d]` cards, `border-[#30363d]`, entity-colored accents.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(game-table): add GameTableZoneTools with toolkit, notes, links, ownership, remove"
```

---

### Task 3.2: GameTableZoneKnowledge

**Files:**
- Create: `apps/web/src/components/library/game-table/GameTableZoneKnowledge.tsx`
- Test: `apps/web/src/components/library/game-table/__tests__/GameTableZoneKnowledge.test.tsx`

**Reference:** Read `apps/web/src/components/ui/data-display/meeple-card-features/DocumentStatusBadge.tsx` and `AgentStatusBadge.tsx` for badge props.

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/library/game-table/__tests__/GameTableZoneKnowledge.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect, vi } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameTableZoneKnowledge } from '../GameTableZoneKnowledge';

vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentKbDocs: () => ({
    data: [{ id: 'doc-1', fileName: 'rules.pdf', status: 'Indexed' }],
    isLoading: false,
  }),
  useAgentThreads: () => ({
    data: [{ id: 't-1', topic: 'Rules Q', lastMessageAt: '2026-03-19T10:00:00Z' }],
    isLoading: false,
  }),
}));

vi.mock('@/hooks/useAgentStatus', () => ({
  useAgentStatus: () => ({
    status: { isReady: true, documentCount: 3, ragStatus: 'ready' },
    isLoading: false,
    error: null,
  }),
}));

describe('GameTableZoneKnowledge', () => {
  it('renders KB document list', () => {
    render(<GameTableZoneKnowledge gameId="g1" agentId="a1" />);
    expect(screen.getByText('rules.pdf')).toBeInTheDocument();
  });

  it('renders chat thread preview', () => {
    render(<GameTableZoneKnowledge gameId="g1" agentId="a1" />);
    expect(screen.getByText(/Rules Q/)).toBeInTheDocument();
  });

  it('renders agent status', () => {
    render(<GameTableZoneKnowledge gameId="g1" agentId="a1" />);
    expect(screen.getByText(/ready/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement GameTableZoneKnowledge**

The component receives `gameId: string` and `agentId: string` (derived from game data). It:
1. Calls `useAgentKbDocs(gameId)` — lists documents with `DocumentStatusBadge`
2. Calls `useAgentThreads(agentId)` — shows last thread preview, "Open chat" button triggers `useGameTableDrawer.open({ type: 'chat', agentId })`
3. Calls `useAgentStatus(agentId)` — shows `AgentStatusBadge`

Each item is a dark card (`bg-[#21262d]`) with entity-colored left border.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(game-table): add GameTableZoneKnowledge with KB, chat, agent status"
```

---

### Task 3.3: GameTableZoneSessions

**Files:**
- Create: `apps/web/src/components/library/game-table/GameTableZoneSessions.tsx`
- Test: `apps/web/src/components/library/game-table/__tests__/GameTableZoneSessions.test.tsx`

- [ ] **Step 1: Write failing tests**

```typescript
// apps/web/src/components/library/game-table/__tests__/GameTableZoneSessions.test.tsx
/**
 * @vitest-environment jsdom
 */
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';

import { GameTableZoneSessions } from '../GameTableZoneSessions';

const mockGameDetail = {
  gameId: 'g1',
  timesPlayed: 42,
  winRate: '38%',
  lastPlayed: '2026-03-15T18:00:00Z',
  avgDuration: '60 min',
  recentSessions: [
    { id: 's1', playedAt: '2026-03-15T18:00:00Z', durationMinutes: 65, durationFormatted: '1h 5m', didWin: true, players: '4', notes: null },
  ],
} as any;

describe('GameTableZoneSessions', () => {
  it('renders play statistics', () => {
    render(<GameTableZoneSessions gameDetail={mockGameDetail} gameId="g1" />);
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText('38%')).toBeInTheDocument();
  });

  it('renders recent session', () => {
    render(<GameTableZoneSessions gameDetail={mockGameDetail} gameId="g1" />);
    expect(screen.getByText(/1h 5m/)).toBeInTheDocument();
  });

  it('renders new session button', () => {
    render(<GameTableZoneSessions gameDetail={mockGameDetail} gameId="g1" />);
    expect(screen.getByRole('button', { name: /nuova sessione/i })).toBeInTheDocument();
  });

  it('handles empty sessions gracefully', () => {
    const empty = { ...mockGameDetail, timesPlayed: 0, recentSessions: [] };
    render(<GameTableZoneSessions gameDetail={empty} gameId="g1" />);
    expect(screen.getByText(/nessuna partita/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement GameTableZoneSessions**

The component receives `gameDetail: LibraryGameDetail` and `gameId: string`. It:
1. If `recentSessions` has entries, renders the first one using `ActiveSessionCard` pattern (or a summary card)
2. Displays stats row: `timesPlayed` partite, `winRate`, `lastPlayed` (formatted with date-fns, it locale), `avgDuration`
3. "Nuova sessione" button → navigates to session creation

Horizontal layout on desktop (flex row), vertical on mobile.

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Commit**

```bash
git commit -m "feat(game-table): add GameTableZoneSessions with stats, recent session, new session CTA"
```

- [ ] **Step 6: Update barrel export with all zone components**

Add to `apps/web/src/components/library/game-table/index.ts`:
```typescript
export { GameTableZoneTools } from './GameTableZoneTools';
export { GameTableZoneKnowledge } from './GameTableZoneKnowledge';
export { GameTableZoneSessions } from './GameTableZoneSessions';
```

Commit: `git commit -m "chore(game-table): update barrel export with zone components"`

---

## Phase 4: GameStatsPanel

### Task 4.1: Create GameStatsPanel for drawer

**Files:**
- Create: `apps/web/src/components/library/game-table/GameStatsPanel.tsx`
- Test: `apps/web/src/components/library/game-table/__tests__/GameStatsPanel.test.tsx`

- [ ] **Step 1: Write failing tests**

Test: renders stat cards (times played, win rate, last played, avg duration), renders session history list, handles empty state.

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement GameStatsPanel**

The component receives `gameId: string`. Calls `useLibraryGameDetail(gameId)`. Displays:
1. Stat cards in 2x2 grid: timesPlayed, winRate, lastPlayed, avgDuration
2. Session history: maps `recentSessions` array to rows (date, score, win/loss badge, players)
3. Empty state if `timesPlayed === 0`: "Nessuna partita registrata"

- [ ] **Step 4: Run tests to verify they pass**

- [ ] **Step 5: Update barrel export**

Add to `index.ts`: `export { GameStatsPanel } from './GameStatsPanel';`

- [ ] **Step 6: Commit**

```bash
git commit -m "feat(game-table): add GameStatsPanel with play statistics and session history"
```

---

## Phase 5: Authenticated Page Assembly

### Task 5.1: Replace library game detail page

**Files:**
- Replace: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`

**Reference:** Read the current page to understand all data flows: `useLibraryGameDetail`, `useQueryClient`, event-driven modals, RAG access, ownership.

- [ ] **Step 1: Read the current page**

Read `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx` to capture all behaviors that must be preserved:
- `useLibraryGameDetail(gameId)` for data
- `useQueryClient` for invalidation after ownership change
- Event-driven modal state (edit notes, remove game)
- Tab routing via `searchParams.get('tab')`
- Loading state (`LibraryGameDetailLoading`)
- Error state and not-found state

- [ ] **Step 2: Write the new page**

Replace the page with `GameTableLayout` assembly. Key points:
- Remove tab-based routing (zones replace tabs)
- Remove event-driven modals (now handled inside `GameTableZoneTools`)
- Wire `useGameTableDrawer` for drawer state
- Build MeepleCard hero with MtG overlay: `mechanicIcon` from first mechanic, `stateLabel` from `currentState`
- Build `ManaLinkFooter` with linked entity counts
- Loading: skeleton matching table layout
- Error/not-found: keep existing pattern

```typescript
// Pseudocode structure:
'use client';
import { GameTableLayout, GameTableDrawer, GameTableZoneTools, GameTableZoneKnowledge, GameTableZoneSessions } from '@/components/library/game-table';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { MechanicIcon } from '@/components/icons/mechanics/MechanicIcon';
import { ManaLinkFooter } from '@/components/ui/data-display/mana/ManaLinkFooter';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useGameTableDrawer } from '@/lib/stores/gameTableDrawerStore';

export default function LibraryGameDetailPage() {
  const params = useParams();
  const gameId = params?.gameId as string;
  const { data: gameDetail, isLoading, error } = useLibraryGameDetail(gameId);
  const drawer = useGameTableDrawer();

  if (isLoading) return <GameTableSkeleton />;
  if (error || !gameDetail) return <GameTableError />;

  return (
    <GameTableLayout
      card={/* MeepleCard hero with all config */}
      toolsZone={<GameTableZoneTools gameDetail={gameDetail} gameId={gameId} />}
      knowledgeZone={<GameTableZoneKnowledge gameId={gameId} agentId={...} />}
      sessionsZone={<GameTableZoneSessions gameDetail={gameDetail} gameId={gameId} />}
      drawer={drawer.content ? <GameTableDrawer content={drawer.content} onClose={drawer.close} /> : undefined}
      drawerOpen={drawer.isOpen}
      onDrawerClose={drawer.close}
    />
  );
}
```

- [ ] **Step 3: Verify the page renders**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(game-table): replace library game detail page with GameTableLayout"
```

---

### Task 5.2: Create GameTableSkeleton loading state

**Files:**
- Create: `apps/web/src/components/library/game-table/GameTableSkeleton.tsx`
- Test: `apps/web/src/components/library/game-table/__tests__/GameTableSkeleton.test.tsx`

- [ ] **Step 1: Write failing test**

Test that skeleton renders card placeholder + 3 zone placeholders. On desktop: grid layout. Test with `getByTestId('game-table-skeleton')`.

- [ ] **Step 2: Run test to verify it fails**

- [ ] **Step 3: Implement GameTableSkeleton**

Matches the GameTableLayout structure:
- Desktop: 3-col grid with pulsing `Skeleton` rectangles (center card 3:4 aspect, left/right zones with 3 rows each, bottom session bar)
- Mobile: Card skeleton (~60vh) + 3 accordion header skeletons (collapsible bars)

Use `apps/web/src/components/ui/feedback/skeleton.tsx` (existing Skeleton component).

- [ ] **Step 4: Run tests, verify pass**

- [ ] **Step 5: Update page to use GameTableSkeleton**

In the page from Task 5.1, replace `if (isLoading) return <GameTableSkeleton />;` placeholder with the actual import.

- [ ] **Step 6: Add to barrel export and commit**

```bash
git commit -m "feat(game-table): add GameTableSkeleton loading state matching table layout"
```

---

### Task 5.3: Add page assembly tests

**Files:**
- Test: `apps/web/src/components/library/game-table/__tests__/GameTablePage.test.tsx`

- [ ] **Step 1: Write integration tests for the page assembly**

Test (mock `useLibraryGameDetail`, `useGameTableDrawer`):
1. Loading state renders `GameTableSkeleton`
2. Error state renders error alert with "Torna alla Libreria" button
3. Loaded state renders `GameTableLayout` with card, 3 zones
4. MeepleCard hero receives correct mechanic icon (first from mechanics array)
5. ManaLinkFooter receives entity counts
6. Drawer opens when `useGameTableDrawer.isOpen` is true

- [ ] **Step 2: Run tests, verify they pass**

- [ ] **Step 3: Commit**

```bash
git commit -m "test(game-table): add page assembly integration tests"
```

---

## Phase 6: Public Page Preview

### Task 6.1: Replace public game detail page

**Files:**
- Replace: `apps/web/src/app/(public)/games/[id]/page.tsx`

- [ ] **Step 1: Read the current page**

Read `apps/web/src/app/(public)/games/[id]/page.tsx` to understand data flow: `api.sharedGames.getById`, auth state, library status, local storage notes/favorites.

- [ ] **Step 2: Write the minimal preview page**

Simple page:
- MeepleCard hero (read-only, not flippable, no action buttons)
- MtG overlay: mechanic icon + "Catalogo" state label
- Mana pips: all inactive
- Metadata chips: players, duration, complexity, rating
- CTA based on auth state:
  - Not authenticated: "Registrati per il tavolo completo" → `/register`
  - Authenticated, not in library: "Aggiungi alla collezione" button
  - Authenticated, in library: "Vai al tavolo" → `/library/games/${gameId}`

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 4: Commit**

```bash
git commit -m "feat(game-table): replace public game detail with minimal preview + CTA"
```

---

## Phase 7: Legacy Cleanup

### Task 7.1: Delete legacy game-detail components

**Files:**
- Delete: entire `apps/web/src/components/library/game-detail/` directory (11 files)

- [ ] **Step 1: Delete files**

```bash
rm -rf apps/web/src/components/library/game-detail/
# Note: loading.tsx may or may not exist. Delete only if present:
# rm apps/web/src/app/\(authenticated\)/library/games/\[gameId\]/loading.tsx
```

- [ ] **Step 2: Verify no dead imports**

Run: `cd apps/web && grep -r "game-detail" src/ --include="*.ts" --include="*.tsx" | grep -v node_modules | grep -v ".next"`
Expected: No results (or only references in the spec/plan docs)

Run: `cd apps/web && grep -r "GameDetailHeroCard\|GameDetailOverviewTab\|GameDetailAgentTab\|GameDetailKbTab\|GameDetailSessionsTab\|CatalogDetailsSection\|UserActionSection\|GameDetailHero" src/ --include="*.ts" --include="*.tsx"`
Expected: No results

- [ ] **Step 3: Verify build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds with no errors

- [ ] **Step 4: Commit**

```bash
git add -A
git commit -m "chore(cleanup): remove legacy game-detail/ directory replaced by GameTableLayout"
```

---

## Phase 8: Verification

### Task 8.1: Full verification pass

- [ ] **Step 1: Run all game-table tests**

Run: `cd apps/web && pnpm vitest run src/components/library/game-table/`
Expected: All tests PASS

- [ ] **Step 2: Run agent data hook tests**

Run: `cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useAgentData.test.ts`
Expected: PASS

- [ ] **Step 3: Run store tests**

Run: `cd apps/web && pnpm vitest run src/lib/stores/__tests__/gameTableDrawerStore.test.ts`
Expected: PASS

- [ ] **Step 4: TypeScript typecheck**

Run: `cd apps/web && pnpm typecheck`
Expected: No errors

- [ ] **Step 5: Lint**

Run: `cd apps/web && pnpm lint`
Expected: No errors

- [ ] **Step 6: Full build**

Run: `cd apps/web && pnpm build`
Expected: Build succeeds

- [ ] **Step 7: Verify no dead imports from legacy**

Run: `cd apps/web && grep -r "from.*game-detail" src/ --include="*.tsx" --include="*.ts"`
Expected: No results

- [ ] **Step 8: Final commit (if any fixes needed)**

```bash
git commit -m "fix(game-table): address verification findings"
```
