# Phase 4: Chat AI — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Create a simplified mobile-first chat experience: conversation list grouped by game, quick prompt chips, citation bottom sheets, and clear AI error states.

**Architecture:** Create new mobile chat pages that reuse the existing `useAgentChatStream` SSE hook and `useChatSessions`/chat API clients. Simplify the 667-line ChatThreadView into a focused mobile chat component. No voice, no debug panel, no agent switching for regular users.

**Tech Stack:** Next.js 16, React 19, Tailwind CSS 4, existing SSE streaming hook, existing chat API clients, Phase 1 components

**Spec:** `docs/superpowers/specs/2026-03-28-user-pages-redesign-design.md` — Section 4

---

## File Structure

### New Files

| File | Responsibility |
|------|---------------|
| `src/components/chat-unified/ChatListMobile.tsx` | Conversation list grouped by game (replaces agent grouping) |
| `src/components/chat-unified/ChatMobile.tsx` | Simplified mobile chat view (messages + input + quick prompts) |
| `src/components/chat-unified/QuickPromptChips.tsx` | Horizontal scrollable prompt suggestions |
| `src/components/chat-unified/QuickPromptChips.test.tsx` | Tests |
| `src/components/chat-unified/CitationSheet.tsx` | Bottom sheet for viewing cited PDF page |
| `src/components/chat-unified/CitationSheet.test.tsx` | Tests |
| `src/components/chat-unified/AiLoadingState.tsx` | Loading/timeout/error states for AI responses |
| `src/components/chat-unified/ChatEmptyState.tsx` | Empty state for no conversations |

### Modified Files

| File | Changes |
|------|---------|
| `src/app/(chat)/chat/page.tsx` | Switch to ChatListMobile on mobile |
| `src/app/(chat)/chat/[threadId]/page.tsx` | Switch to ChatMobile on mobile |

### All paths relative to `apps/web/`

---

## Task 1: Create QuickPromptChips and ChatEmptyState

**Files:**
- Create: `src/components/chat-unified/QuickPromptChips.tsx`
- Create: `src/components/chat-unified/QuickPromptChips.test.tsx`
- Create: `src/components/chat-unified/ChatEmptyState.tsx`

- [ ] **Step 1: Write QuickPromptChips test**

```tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { QuickPromptChips } from './QuickPromptChips';

const prompts = ['Come si prepara il gioco?', 'Quanti giocatori servono?', 'Spiega il turno'];

describe('QuickPromptChips', () => {
  it('renders all prompt chips', () => {
    render(<QuickPromptChips prompts={prompts} onSelect={() => {}} />);
    expect(screen.getByText('Come si prepara il gioco?')).toBeInTheDocument();
    expect(screen.getByText('Quanti giocatori servono?')).toBeInTheDocument();
  });

  it('calls onSelect with prompt text when clicked', () => {
    const onSelect = vi.fn();
    render(<QuickPromptChips prompts={prompts} onSelect={onSelect} />);
    fireEvent.click(screen.getByText('Spiega il turno'));
    expect(onSelect).toHaveBeenCalledWith('Spiega il turno');
  });

  it('does not render when prompts is empty', () => {
    const { container } = render(<QuickPromptChips prompts={[]} onSelect={() => {}} />);
    expect(container.firstChild).toBeNull();
  });

  it('is hidden when hidden prop is true', () => {
    const { container } = render(<QuickPromptChips prompts={prompts} onSelect={() => {}} hidden />);
    expect(container.firstChild).toBeNull();
  });
});
```

- [ ] **Step 2: Write QuickPromptChips**

```tsx
'use client';

import React from 'react';
import { cn } from '@/lib/utils';

export interface QuickPromptChipsProps {
  prompts: string[];
  onSelect: (prompt: string) => void;
  hidden?: boolean;
  className?: string;
}

export function QuickPromptChips({ prompts, onSelect, hidden, className }: QuickPromptChipsProps) {
  if (hidden || prompts.length === 0) return null;

  return (
    <div className={cn('flex gap-2 overflow-x-auto px-4 py-2 scrollbar-none', className)}>
      {prompts.map((prompt) => (
        <button
          key={prompt}
          type="button"
          onClick={() => onSelect(prompt)}
          className={cn(
            'shrink-0 rounded-full px-3 py-1.5 text-xs font-medium',
            'bg-amber-500/10 text-amber-300 border border-amber-500/20',
            'transition-colors hover:bg-amber-500/20'
          )}
        >
          {prompt}
        </button>
      ))}
    </div>
  );
}
```

- [ ] **Step 3: Write ChatEmptyState**

```tsx
import React from 'react';
import Link from 'next/link';
import { MessageCircle } from 'lucide-react';

export function ChatEmptyState() {
  return (
    <div className="flex flex-col items-center gap-4 px-4 py-16 text-center">
      <div className="flex h-16 w-16 items-center justify-center rounded-full bg-purple-500/10">
        <MessageCircle className="h-8 w-8 text-purple-400" />
      </div>
      <div>
        <p className="text-sm font-medium text-[var(--gaming-text-primary)]">
          Nessuna conversazione
        </p>
        <p className="mt-1 text-xs text-[var(--gaming-text-secondary)]">
          Vai alla libreria e chiedi qualcosa sulle regole di un gioco!
        </p>
      </div>
      <Link
        href="/library"
        className="text-sm font-medium text-amber-400 hover:text-amber-300"
      >
        Vai alla Libreria
      </Link>
    </div>
  );
}
```

- [ ] **Step 4: Run tests, verify, commit**

```bash
cd apps/web && pnpm vitest run src/components/chat-unified/QuickPromptChips.test.tsx
npx tsc --noEmit
git add apps/web/src/components/chat-unified/QuickPromptChips* apps/web/src/components/chat-unified/ChatEmptyState.tsx
git commit -m "feat(chat): add QuickPromptChips and ChatEmptyState"
```

---

## Task 2: Create CitationSheet and AiLoadingState

**Files:**
- Create: `src/components/chat-unified/CitationSheet.tsx`
- Create: `src/components/chat-unified/CitationSheet.test.tsx`
- Create: `src/components/chat-unified/AiLoadingState.tsx`

- [ ] **Step 1: Read existing CitationBadge.tsx and PdfPageModal for understanding**

Read `src/components/chat-unified/CitationBadge.tsx` and search for `PdfPageModal` to understand citation data shape.

- [ ] **Step 2: Write CitationSheet test**

```tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { CitationSheet } from './CitationSheet';

const citation = {
  documentId: 'doc-1',
  pageNumber: 5,
  snippet: 'Each player starts with 3 settlement and 4 road pieces.',
  relevanceScore: 0.92,
  copyrightTier: 'full' as const,
};

describe('CitationSheet', () => {
  it('renders citation snippet when open', () => {
    render(<CitationSheet open citation={citation} onOpenChange={() => {}} />);
    expect(screen.getByText(/each player starts/i)).toBeInTheDocument();
  });

  it('renders page number', () => {
    render(<CitationSheet open citation={citation} onOpenChange={() => {}} />);
    expect(screen.getByText(/pagina 5/i)).toBeInTheDocument();
  });

  it('does not render when no citation', () => {
    render(<CitationSheet open citation={null} onOpenChange={() => {}} />);
    expect(screen.queryByText(/each player/i)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 3: Write CitationSheet**

```tsx
'use client';

import React from 'react';
import { BottomSheet } from '@/components/ui/overlays/BottomSheet';
import { FileText } from 'lucide-react';

export interface CitationData {
  documentId: string;
  pageNumber: number;
  snippet: string;
  relevanceScore: number;
  copyrightTier: 'full' | 'protected';
  paraphrasedSnippet?: string;
}

export interface CitationSheetProps {
  open: boolean;
  citation: CitationData | null;
  onOpenChange: (open: boolean) => void;
}

export function CitationSheet({ open, citation, onOpenChange }: CitationSheetProps) {
  if (!citation) return null;

  return (
    <BottomSheet open={open} onOpenChange={onOpenChange} title="Fonte">
      <div className="flex flex-col gap-4">
        <div className="flex items-center gap-2 text-sm text-amber-400">
          <FileText className="h-4 w-4" />
          <span>Pagina {citation.pageNumber}</span>
        </div>
        <blockquote className="rounded-lg bg-white/5 px-4 py-3 text-sm leading-relaxed text-[var(--gaming-text-primary)] border-l-2 border-amber-500/50">
          {citation.copyrightTier === 'protected' && citation.paraphrasedSnippet
            ? citation.paraphrasedSnippet
            : citation.snippet}
        </blockquote>
        <p className="text-xs text-[var(--gaming-text-secondary)]">
          Rilevanza: {Math.round(citation.relevanceScore * 100)}%
        </p>
      </div>
    </BottomSheet>
  );
}
```

- [ ] **Step 4: Write AiLoadingState**

```tsx
import React from 'react';
import { Loader2, AlertCircle, FileQuestion } from 'lucide-react';
import { GradientButton } from '@/components/ui/buttons/GradientButton';

export type AiState = 'loading' | 'timeout' | 'no-results' | 'no-pdf';

export interface AiLoadingStateProps {
  state: AiState;
  onRetry?: () => void;
  onUploadPdf?: () => void;
  statusMessage?: string | null;
}

export function AiLoadingState({ state, onRetry, onUploadPdf, statusMessage }: AiLoadingStateProps) {
  if (state === 'loading') {
    return (
      <div className="flex items-center gap-3 px-4 py-3 text-sm text-[var(--gaming-text-secondary)]">
        <Loader2 className="h-4 w-4 animate-spin text-amber-400" />
        <span>{statusMessage || 'Sto cercando nelle regole...'}</span>
      </div>
    );
  }

  if (state === 'timeout') {
    return (
      <div className="flex flex-col gap-2 px-4 py-3">
        <div className="flex items-center gap-2 text-sm text-red-400">
          <AlertCircle className="h-4 w-4" />
          <span>Non riesco a trovare una risposta. Prova a riformulare.</span>
        </div>
        {onRetry && (
          <button type="button" onClick={onRetry} className="text-sm font-medium text-amber-400">
            Riprova
          </button>
        )}
      </div>
    );
  }

  if (state === 'no-results') {
    return (
      <div className="px-4 py-3 text-sm text-[var(--gaming-text-secondary)]">
        Non ho trovato questa informazione nel regolamento. Vuoi che provi a rispondere in base alla mia conoscenza generale?
      </div>
    );
  }

  if (state === 'no-pdf') {
    return (
      <div className="flex flex-col gap-2 px-4 py-3">
        <div className="flex items-center gap-2 text-sm text-[var(--gaming-text-secondary)]">
          <FileQuestion className="h-4 w-4" />
          <span>Carica il regolamento per risposte più precise</span>
        </div>
        {onUploadPdf && (
          <GradientButton size="sm" onClick={onUploadPdf}>
            Carica PDF
          </GradientButton>
        )}
      </div>
    );
  }

  return null;
}
```

- [ ] **Step 5: Run tests, verify, commit**

```bash
cd apps/web && pnpm vitest run src/components/chat-unified/CitationSheet.test.tsx
npx tsc --noEmit
git add apps/web/src/components/chat-unified/CitationSheet* apps/web/src/components/chat-unified/AiLoadingState.tsx
git commit -m "feat(chat): add CitationSheet and AiLoadingState"
```

---

## Task 3: Create ChatListMobile

**Files:**
- Create: `src/components/chat-unified/ChatListMobile.tsx`

Conversation list grouped by game instead of agent.

- [ ] **Step 1: Read useRecentChatSessions hook and ChatSessionSummaryDto**

Read `src/hooks/queries/useChatSessions.ts` and `src/lib/api/schemas/chat-sessions.schemas.ts` to understand the data shape.

- [ ] **Step 2: Create ChatListMobile**

```tsx
'use client';

import React from 'react';
import Link from 'next/link';
import { cn } from '@/lib/utils';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { ChatEmptyState } from './ChatEmptyState';
import { useRecentChatSessions } from '@/hooks/queries/useChatSessions';
import { MessageCircle } from 'lucide-react';

export function ChatListMobile() {
  const { data: sessions, isLoading } = useRecentChatSessions(100);

  // Group sessions by gameTitle (or "Generale" if no game)
  const grouped = React.useMemo(() => {
    if (!sessions?.sessions) return new Map<string, typeof sessions.sessions>();
    const map = new Map<string, typeof sessions.sessions>();
    for (const session of sessions.sessions) {
      const key = session.gameTitle || 'Generale';
      const group = map.get(key) || [];
      group.push(session);
      map.set(key, group);
    }
    return map;
  }, [sessions]);

  return (
    <div className="min-h-screen bg-[var(--gaming-bg-base)]">
      <MobileHeader title="Chat" />

      <div className="pb-20 pt-2">
        {isLoading ? (
          <div className="flex flex-col gap-2 px-4 pt-4">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-16 animate-pulse rounded-lg bg-white/5" />
            ))}
          </div>
        ) : grouped.size === 0 ? (
          <ChatEmptyState />
        ) : (
          Array.from(grouped.entries()).map(([gameTitle, gameSessions]) => (
            <div key={gameTitle} className="mb-4">
              <h3 className="px-4 py-2 text-xs font-medium uppercase text-[var(--gaming-text-secondary)]">
                {gameTitle}
              </h3>
              {gameSessions.map((session) => (
                <Link
                  key={session.id}
                  href={`/chat/${session.id}`}
                  className={cn(
                    'flex items-center gap-3 px-4 py-3',
                    'border-b border-[var(--gaming-border-glass)]',
                    'transition-colors hover:bg-white/5'
                  )}
                >
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-full bg-purple-500/10">
                    <MessageCircle className="h-5 w-5 text-purple-400" />
                  </div>
                  <div className="min-w-0 flex-1">
                    <p className="truncate text-sm font-medium text-[var(--gaming-text-primary)]">
                      {session.title || 'Nuova conversazione'}
                    </p>
                    {session.lastMessagePreview && (
                      <p className="truncate text-xs text-[var(--gaming-text-secondary)]">
                        {session.lastMessagePreview}
                      </p>
                    )}
                  </div>
                  {session.lastMessageAt && (
                    <span className="shrink-0 text-[10px] text-[var(--gaming-text-secondary)]">
                      {new Date(session.lastMessageAt).toLocaleDateString('it-IT', { day: 'numeric', month: 'short' })}
                    </span>
                  )}
                </Link>
              ))}
            </div>
          ))
        )}
      </div>
    </div>
  );
}
```

IMPORTANT: After reading the hook, verify:
- `useRecentChatSessions` return shape (might be `data.sessions` or just `data` as array)
- Session fields: `session.gameTitle`, `session.title`, `session.lastMessagePreview`, `session.lastMessageAt`
- The URL path for chat thread (might be `/chat/${session.id}` or needs threadId)

- [ ] **Step 3: Verify, commit**

```bash
npx tsc --noEmit
git add apps/web/src/components/chat-unified/ChatListMobile.tsx
git commit -m "feat(chat): add ChatListMobile grouped by game"
```

---

## Task 4: Create ChatMobile (simplified chat view)

**Files:**
- Create: `src/components/chat-unified/ChatMobile.tsx`

Simplified mobile chat reusing `useAgentChatStream` for SSE streaming.

- [ ] **Step 1: Read useAgentChatStream hook**

Read `src/hooks/useAgentChatStream.ts` to understand:
- How to call `sendMessage(agentId, message, chatThreadId)`
- The state shape (`AgentChatStreamState`)
- How to get the thread's agentId

Also read `src/lib/api/clients/chatClient.ts` to understand `getThreadById`.

- [ ] **Step 2: Create ChatMobile**

This is the most complex component. It should:
- Load thread via `api.chat.getThreadById(threadId)` on mount
- Display messages in a scroll area (user=right amber, assistant=left glass)
- Show citations as clickable chips → CitationSheet
- Show QuickPromptChips when no messages or after AI response
- Use `useAgentChatStream` for sending messages with SSE streaming
- Show streaming response with pulse animation
- Handle errors via AiLoadingState
- Fixed input area at bottom with send button

Key structure:
```tsx
'use client';

import React, { useState, useEffect, useRef, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import { cn } from '@/lib/utils';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { QuickPromptChips } from './QuickPromptChips';
import { CitationSheet, type CitationData } from './CitationSheet';
import { AiLoadingState } from './AiLoadingState';
import { Send } from 'lucide-react';
import { useAgentChatStream } from '@/hooks/useAgentChatStream';
import { api } from '@/lib/api';

interface ChatMessage {
  id: string;
  role: 'user' | 'assistant' | 'system';
  content: string;
  timestamp: string;
  citations?: CitationData[];
}

const defaultPrompts = [
  'Come si prepara il gioco?',
  'Quanti giocatori servono?',
  'Spiega il turno di gioco',
  'Cosa succede in caso di pareggio?',
];

interface ChatMobileProps {
  threadId: string;
}
```

The component should:
1. Fetch thread data on mount
2. Map thread messages to ChatMessage[]
3. Render messages in a scrollable area
4. On send: add user message to local state, call `sendMessage` via SSE hook
5. Show streaming answer as it arrives from `streamState.currentAnswer`
6. When streaming completes (type 4), add the full answer to messages
7. Show QuickPromptChips above input (hidden while typing/streaming)
8. Input: textarea + send button, fixed at bottom

IMPORTANT: Read `useAgentChatStream` carefully to understand:
- The `sendMessage` function signature
- How to get `agentId` from the thread (might be `thread.agentId`)
- How streaming state flows (`isStreaming`, `currentAnswer`, `error`, `statusMessage`)
- What happens on `Complete` event (type 4)

- [ ] **Step 3: Verify, commit**

```bash
npx tsc --noEmit
git add apps/web/src/components/chat-unified/ChatMobile.tsx
git commit -m "feat(chat): add ChatMobile with SSE streaming and citations"
```

---

## Task 5: Wire chat pages

**Files:**
- Modify: `src/app/(chat)/chat/page.tsx`
- Modify: `src/app/(chat)/chat/[threadId]/page.tsx`

- [ ] **Step 1: Read both page files**

- [ ] **Step 2: Update chat list page**

In `src/app/(chat)/chat/page.tsx`, add responsive rendering:
- Mobile: `ChatListMobile`
- Desktop: Keep existing implementation

- [ ] **Step 3: Update chat thread page**

In `src/app/(chat)/chat/[threadId]/page.tsx`, add responsive rendering:
- Mobile: `ChatMobile` with threadId from params
- Desktop: Keep existing `ChatThreadView`

- [ ] **Step 4: Verify, commit**

```bash
npx tsc --noEmit
git add apps/web/src/app/\(chat\)/
git commit -m "feat(chat): wire mobile chat pages"
```

---

## Task 6: Integration Verification

- [ ] **Step 1: Run tests**

```bash
cd apps/web && pnpm vitest run src/components/chat-unified/QuickPromptChips.test.tsx src/components/chat-unified/CitationSheet.test.tsx
```

- [ ] **Step 2: Typecheck + lint**

```bash
cd apps/web && npx tsc --noEmit && pnpm lint
```

---

## Summary

| Task | Component | Tests | Status |
|------|-----------|-------|--------|
| 1 | QuickPromptChips + ChatEmptyState | 4 | ☐ |
| 2 | CitationSheet + AiLoadingState | 3 | ☐ |
| 3 | ChatListMobile | 0 (integration) | ☐ |
| 4 | ChatMobile (SSE streaming) | 0 (integration) | ☐ |
| 5 | Page wiring | 0 | ☐ |
| 6 | Integration verification | — | ☐ |

**Total new tests: 7**
**Total new files: 8**
**Total modified files: 2**

**Key reuse**: `useAgentChatStream` (SSE), `useRecentChatSessions`, `api.chat.*`, `BottomSheet`, `MobileHeader`, `GradientButton`
