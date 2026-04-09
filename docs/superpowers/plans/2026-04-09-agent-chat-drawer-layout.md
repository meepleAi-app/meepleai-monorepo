# Agent Chat Drawer — Layout 2-colonne Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Rimpiazzare il drawer agente tab-based con un layout a 2 colonne chat-centrico che corrisponde al mockup `admin-mockups/chatDesktop.png`.

**Architecture:** Nuovo componente `AgentChatDrawerLayout` con sidebar sinistra (contesto + nuova chat + recenti + KB) e area destra (chat). Tutta la logica di fetch esistente viene riutilizzata. `AgentDrawerContent` in `ExtraMeepleCardDrawer` viene aggiornato per usare il nuovo layout.

**Tech Stack:** Next.js 16, React 19, TypeScript, Tailwind 4, shadcn/ui, Vitest + React Testing Library

**Branch:** `feature/issue-agent-chat-drawer-layout` (parent: `main-dev`)

---

## Struttura File

**Creare:**
- `apps/web/src/components/ui/data-display/extra-meeple-card/entities/AgentChatDrawerLayout.tsx`
- `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx`

**Modificare:**
- `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`
  - `AgentDrawerContent`: sostituisce `AgentExtraMeepleCard` con `AgentChatDrawerLayout`
  - Label agent: `'Dettaglio Agente'` → `"Chat con l'agente"`
  - SheetContent: aggiungere classe width override per agent (`sm:w-[800px] sm:max-w-[800px]`)

---

### Task 1: Scaffolding + test failing

**Files:**
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx`
- Create: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/AgentChatDrawerLayout.tsx`

- [ ] **Step 1.1: Scrivi il test failing per la struttura layout**

```tsx
// apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { AgentChatDrawerLayout } from '../AgentChatDrawerLayout';
import type { AgentDetailData } from '../../types';

vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentThreads: () => ({ data: [], isLoading: false }),
  useAgentKbDocs: () => ({ data: [], isLoading: false }),
}));
vi.mock('@/hooks/useAgentStatus', () => ({
  useAgentStatus: () => ({ status: null, isLoading: false, error: null }),
}));

const mockAgent: AgentDetailData = {
  id: 'agent-1',
  name: 'Azul Assistant',
  type: 'qa',
  strategyName: 'hybrid-rag',
  strategyParameters: {},
  isActive: true,
  isIdle: false,
  invocationCount: 3,
  lastInvokedAt: null,
  createdAt: '2024-01-01T00:00:00Z',
  gameId: 'game-1',
  gameName: 'Azul',
};

describe('AgentChatDrawerLayout', () => {
  it('renders sidebar and chat area in 2-column layout', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByTestId('agent-chat-sidebar')).toBeInTheDocument();
    expect(screen.getByTestId('agent-chat-area')).toBeInTheDocument();
  });

  it('shows game name in sidebar context', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
  });

  it('shows "+ Nuova chat" button', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByTestId('new-chat-button')).toBeInTheDocument();
  });

  it('shows CHAT RECENTI section label', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByText('CHAT RECENTI')).toBeInTheDocument();
  });

  it('shows KNOWLEDGE BASE section label', () => {
    render(<AgentChatDrawerLayout data={mockAgent} />);
    expect(screen.getByText('KNOWLEDGE BASE')).toBeInTheDocument();
  });

  it('shows empty state when no game linked', () => {
    const noGameAgent = { ...mockAgent, gameId: undefined, gameName: undefined };
    render(<AgentChatDrawerLayout data={noGameAgent} />);
    expect(screen.getByTestId('no-game-placeholder')).toBeInTheDocument();
  });
});
```

- [ ] **Step 1.2: Esegui il test per verificare che fallisce**

```bash
cd apps/web && pnpm test src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx
```

Atteso: `FAIL — Cannot find module '../AgentChatDrawerLayout'`

- [ ] **Step 1.3: Crea lo scaffolding minimo `AgentChatDrawerLayout.tsx`**

```tsx
// apps/web/src/components/ui/data-display/extra-meeple-card/entities/AgentChatDrawerLayout.tsx
'use client';

import React, { useState } from 'react';
import { cn } from '@/lib/utils';
import type { AgentDetailData } from '../types';

export interface AgentChatDrawerLayoutProps {
  data: AgentDetailData;
  className?: string;
  'data-testid'?: string;
}

export const AgentChatDrawerLayout = React.memo(function AgentChatDrawerLayout({
  data,
  className,
  'data-testid': testId,
}: AgentChatDrawerLayoutProps) {
  return (
    <div
      className={cn('flex h-full w-full overflow-hidden', className)}
      data-testid={testId ?? 'agent-chat-drawer-layout'}
    >
      <div data-testid="agent-chat-sidebar" className="w-[220px] shrink-0">
        {data.gameName ? (
          <span>{data.gameName}</span>
        ) : (
          <span data-testid="no-game-placeholder" />
        )}
        <button data-testid="new-chat-button">+ Nuova chat</button>
        <p>CHAT RECENTI</p>
        <p>KNOWLEDGE BASE</p>
      </div>
      <div data-testid="agent-chat-area" className="flex-1" />
    </div>
  );
});
```

- [ ] **Step 1.4: Esegui test — devono passare**

```bash
cd apps/web && pnpm test src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx
```

Atteso: `PASS — 6 tests passed`

- [ ] **Step 1.5: Commit scaffolding**

```bash
git add apps/web/src/components/ui/data-display/extra-meeple-card/entities/AgentChatDrawerLayout.tsx apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx
git commit -m "feat(agent-drawer): scaffold AgentChatDrawerLayout with failing-then-passing tests"
```

---

### Task 2: Sidebar completa + AgentChatArea

**Files:**
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/AgentChatDrawerLayout.tsx`
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx`

**Dipendenze riutilizzate (esistenti, NON ricreare):**
- `useAgentThreads(agentId)` da `@/hooks/queries/useAgentData`
- `useAgentKbDocs(gameId)` da `@/hooks/queries/useAgentData`
- `useAgentStatus(agentId)` da `@/hooks/useAgentStatus` — ritorna `{ status: { isReady, documentCount, ragStatus, blockingReason }, isLoading, error }`
- `ChatThreadView` da `@/components/chat-unified/ChatThreadView`
- `api.chat.createThread({ agentId, title })` da `@/lib/api`
- Tipi: `AgentDetailData`, `ChatThreadPreview`, `KbDocumentPreview` da `../types`

**Comportamento:**
- Sidebar 220px fissa, border-r, bg-slate-50/80
- `AgentContextCard`: mostra icona Gamepad2 + gameName in card ambra; se no gameId → `data-testid="no-game-placeholder"`
- `+ Nuova chat`: blu quando isReady, grigio disabled quando non ready o readinessLoading
- `CHAT RECENTI`: lista `RecentThreadItem` clickable; click → imposta `selectedThreadId`; `data-testid="thread-item-{thread.id}"`; item selezionato ha classe blu
- `KNOWLEDGE BASE`: lista `KbDocItem` con fileName + dot colorato (emerald=indexed, amber=processing, red=failed, slate=none)
- Area chat (flex-1): state machine
  - `readinessLoading` → spinner
  - `readiness.isReady === false` → BlockingUI con link a `/admin/ai-lab/agents/{agentId}/edit`
  - `selectedThreadId` nullo → empty state con testo "Seleziona una chat recente oppure avvia una nuova conversazione", `data-testid="chat-empty-state"`
  - `selectedThreadId === 'new'` → chiama `api.chat.createThread` → poi passa a stato sotto
  - `selectedThreadId` = UUID → `<ChatThreadView threadId={selectedThreadId} />`
- Area chat: `data-thread-id={selectedThreadId ?? ''}`

- [ ] **Step 2.1: Aggiungi test thread click e KB**

Aggiungi nel file test esistente (importa `userEvent` da `@testing-library/user-event`):

```tsx
import userEvent from '@testing-library/user-event';

const mockThreads = [
  { id: 't1', createdAt: '2024-03-01T00:00:00Z', messageCount: 5, firstMessagePreview: 'Qual è la regola finale?' },
];

// Override mock per thread
vi.mock('@/hooks/queries/useAgentData', () => ({
  useAgentThreads: vi.fn(() => ({ data: mockThreads, isLoading: false })),
  useAgentKbDocs: vi.fn(() => ({ data: [], isLoading: false })),
}));

it('shows thread preview in recent chats', () => {
  render(<AgentChatDrawerLayout data={mockAgent} />);
  expect(screen.getByText('Qual è la regola finale?')).toBeInTheDocument();
});

it('clicking a recent thread updates chat area data-thread-id', async () => {
  render(<AgentChatDrawerLayout data={mockAgent} />);
  const threadItem = screen.getByTestId('thread-item-t1');
  await userEvent.click(threadItem);
  expect(screen.getByTestId('agent-chat-area')).toHaveAttribute('data-thread-id', 't1');
});
```

- [ ] **Step 2.2: Implementa `AgentChatDrawerLayout.tsx` completo**

Rimpiazza il file con l'implementazione completa seguendo le specifiche sopra. Struttura suggerita:

```
AgentChatDrawerLayout (componente root, gestisce selectedThreadId state)
├── aside[data-testid="agent-chat-sidebar"]
│   ├── AgentContextCard (locale)
│   ├── button[data-testid="new-chat-button"]
│   ├── SidebarSection label="CHAT RECENTI"
│   │   └── RecentThreadItem[] (o empty state)
│   └── SidebarSection label="KNOWLEDGE BASE"
│       └── KbDocItem[] (o empty state)
└── div[data-testid="agent-chat-area"][data-thread-id=...]
    └── AgentChatArea (locale, state machine)
```

- [ ] **Step 2.3: Esegui tutti i test**

```bash
cd apps/web && pnpm test src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx
```

Atteso: tutti pass.

- [ ] **Step 2.4: Typecheck**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -30
```

Atteso: 0 errori nei nuovi file.

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/extra-meeple-card/entities/AgentChatDrawerLayout.tsx apps/web/src/components/ui/data-display/extra-meeple-card/entities/__tests__/AgentChatDrawerLayout.test.tsx
git commit -m "feat(agent-drawer): implement full AgentChatDrawerLayout — sidebar + chat area"
```

---

### Task 3: Wire ExtraMeepleCardDrawer

**Files:**
- Modify: `apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx`

**Contesto del file (righe chiave):**
- Riga ~105: `ENTITY_CONFIG` — cambia label agent
- Riga ~145-157: `SheetContent` className — aggiungi width override per agent
- Riga ~301-314: `AgentDrawerContent` — sostituisce `AgentExtraMeepleCard` con `AgentChatDrawerLayout`
- Import da aggiungere: `import { AgentChatDrawerLayout } from './entities/AgentChatDrawerLayout';`
- Import da RIMUOVERE dall'uso interno (ma non dalla re-export): non rimuovere l'import di `AgentExtraMeepleCard` se è usato in altri posti del file; cambia solo `AgentDrawerContent`

**Modifiche esatte:**

1. In `ENTITY_CONFIG`, riga agent:
```tsx
// PRIMA
agent: { label: 'Dettaglio Agente', color: '38 92% 50%', Icon: Bot },
// DOPO
agent: { label: "Chat con l'agente", color: '38 92% 50%', Icon: Bot },
```

2. In `SheetContent` className, sostituisci la riga width con:
```tsx
entityType === 'agent'
  ? 'w-full sm:w-[800px] sm:max-w-[800px]'
  : 'w-full sm:w-[600px] sm:max-w-[600px]',
```

3. `AgentDrawerContent`:
```tsx
function AgentDrawerContent({ entityId }: { entityId: string }) {
  const { data, loading, error, retry } = useAgentDetail(entityId);
  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;
  return <AgentChatDrawerLayout data={data} className="h-full" />;
}
```

- [ ] **Step 3.1: Leggi il file attuale**

```bash
# Solo per capire le righe esatte prima di modificare
head -160 apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx
```

- [ ] **Step 3.2: Applica le 3 modifiche**

Applica le modifiche descritte sopra.

- [ ] **Step 3.3: Typecheck + test**

```bash
cd apps/web && pnpm typecheck 2>&1 | head -20
cd apps/web && pnpm test src/components/ui/data-display/extra-meeple-card/ --run
```

Atteso: 0 errori, tutti i test passano.

- [ ] **Step 3.4: Lint**

```bash
cd apps/web && pnpm lint 2>&1 | grep -E "error" | head -10
```

Atteso: 0 errori nei file modificati.

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/components/ui/data-display/extra-meeple-card/ExtraMeepleCardDrawer.tsx
git commit -m "feat(agent-drawer): wire AgentDrawerContent to AgentChatDrawerLayout, update label+width"
```
