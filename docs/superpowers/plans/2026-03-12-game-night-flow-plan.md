# Game Night Flow Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Wire the end-to-end "Improvised Game Night" user story from BGG search to save/resume, connecting existing backend endpoints to frontend components.

**Architecture:** Smart Hub pattern — the private game detail page (`/library/private/[privateGameId]`) becomes the central hub with an Activation Checklist. Live sessions use the existing `/sessions/[id]/play` page with the existing `LiveSessionView` refactored to wire real agent chat (replacing placeholders). Backend is ~90% complete; this plan is primarily frontend wiring reusing existing components, hooks, stores, and API clients.

**Tech Stack:** Next.js 16 (App Router), React 19, Zustand, React Query, Zod, Tailwind 4, shadcn/ui, SSE streaming

**Spec:** `docs/superpowers/specs/2026-03-12-game-night-flow-design.md`

**API method reference (real client signatures):**

| Usage | Real Method | Returns |
|-------|-------------|---------|
| Get PDFs by game | `api.documents.getDocumentsByGame(gameId)` | `PdfDocumentDto[]` |
| Upload PDF | `api.pdf.uploadPdf(gameId, file, onProgress?)` | `{ documentId, fileName }` |
| Get active sessions | `api.liveSessions.getActive()` | `LiveSessionSummaryDto[]` |
| Create session | `api.liveSessions.createSession(request)` | `string` (sessionId) |
| Resume session | `api.liveSessions.resumeSession(sessionId)` | `void` |
| Complete session | `api.liveSessions.completeSession(sessionId)` | `void` |
| Save complete | `api.liveSessions.saveComplete(sessionId)` | `SessionSaveResult` |
| Create agent | `api.agents.createUserAgent({ gameId, agentType })` | `AgentDto` |
| Get agents for game | `api.agents.getUserAgentsForGame(gameId)` | `AgentDto[]` |
| Add private game | `api.library.addPrivateGame(request)` | `PrivateGameDto` |
| Get private game | `api.library.getPrivateGame(id)` | `PrivateGameDto` |
| Add to library | `api.library.addGame(gameId, request?)` | `UserLibraryEntry` |

**Key existing code to reuse (NOT recreate):**
- `components/pdf/CopyrightDisclaimerModal.tsx` — AlertDialog disclaimer (reuse as-is)
- `components/game-night/LiveSessionView.tsx` — 450-line session orchestrator with Zustand + SSE
- `components/game-night/SaveCompleteDialog.tsx` — Save/pause flow with `api.liveSessions.saveComplete()`
- `components/game-night/SessionChatWidget.tsx` — Chat UI (currently placeholder, wire to agent)
- `components/session/LiveSessionLayout.tsx` — 3-column desktop layout
- `components/session/ScoreInput.tsx`, `ScoreAssistant.tsx` — Score entry
- `lib/stores/sessionStore.ts` — Zustand store with `pauseSession()`, `resumeSession()`, `recordScore()`
- `lib/hooks/useSessionSync.ts` — SSE real-time updates
- `hooks/queries/useLibrary.ts` — 16 hooks, query key factory (add private game hooks here)
- `lib/api/clients/libraryClient.ts` — `addPrivateGame()`, `getPrivateGame()`, `addGame()`
- `app/(authenticated)/sessions/[id]/layout.tsx` — MiniNav + session tabs
- `app/(authenticated)/sessions/[id]/play/page.tsx` — Renders `LiveSessionView`

---

## Chunk 1: Smart Hub — Activation Checklist

### Task 1: ActivationChecklist component

**Files:**
- Create: `apps/web/src/components/library/private-game-detail/ActivationChecklist.tsx`
- Create: `apps/web/src/components/library/private-game-detail/ActivationStep.tsx`
- Test: `apps/web/__tests__/components/library/private-game-detail/ActivationChecklist.test.tsx`

- [ ] **Step 1: Write the failing test for ActivationChecklist**

```tsx
// apps/web/__tests__/components/library/private-game-detail/ActivationChecklist.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { ActivationChecklist } from '@/components/library/private-game-detail/ActivationChecklist';

describe('ActivationChecklist', () => {
  const defaultProps = {
    gameAdded: true,
    pdfStatus: 'none' as const,
    agentStatus: 'none' as const,
    onUploadPdf: vi.fn(),
    onCreateAgent: vi.fn(),
    onStartGame: vi.fn(),
  };

  it('renders all 3 steps', () => {
    render(<ActivationChecklist {...defaultProps} />);
    expect(screen.getByText(/gioco aggiunto/i)).toBeInTheDocument();
    expect(screen.getByText(/regolamento/i)).toBeInTheDocument();
    expect(screen.getByText(/agente ai/i)).toBeInTheDocument();
  });

  it('shows step 1 as completed when gameAdded is true', () => {
    render(<ActivationChecklist {...defaultProps} />);
    const step1 = screen.getByTestId('step-game-added');
    expect(step1).toHaveAttribute('data-completed', 'true');
  });

  it('disables Start Game button when PDF not ready', () => {
    render(<ActivationChecklist {...defaultProps} />);
    const button = screen.getByRole('button', { name: /inizia partita/i });
    expect(button).toBeDisabled();
  });

  it('enables Start Game button when PDF is ready', () => {
    render(<ActivationChecklist {...defaultProps} pdfStatus="ready" />);
    const button = screen.getByRole('button', { name: /inizia partita/i });
    expect(button).toBeEnabled();
  });

  it('calls onUploadPdf when upload button clicked', () => {
    render(<ActivationChecklist {...defaultProps} />);
    fireEvent.click(screen.getByRole('button', { name: /carica regolamento/i }));
    expect(defaultProps.onUploadPdf).toHaveBeenCalledOnce();
  });

  it('shows upload zone when PDF step is active', () => {
    render(<ActivationChecklist {...defaultProps} />);
    expect(screen.getByText(/carica/i)).toBeInTheDocument();
  });

  it('collapses completed steps', () => {
    render(<ActivationChecklist {...defaultProps} pdfStatus="ready" agentStatus="ready" />);
    const step2 = screen.getByTestId('step-pdf');
    expect(step2).toHaveAttribute('data-collapsed', 'true');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test __tests__/components/library/private-game-detail/ActivationChecklist.test.tsx`
Expected: FAIL — module not found

- [ ] **Step 3: Implement ActivationStep component**

```tsx
// apps/web/src/components/library/private-game-detail/ActivationStep.tsx
'use client';

import { cn } from '@/lib/utils';
import { Check } from 'lucide-react';

interface ActivationStepProps {
  stepNumber: number;
  title: string;
  completed: boolean;
  collapsed?: boolean;
  disabled?: boolean;
  testId: string;
  children?: React.ReactNode;
}

export function ActivationStep({
  stepNumber,
  title,
  completed,
  collapsed = false,
  disabled = false,
  testId,
  children,
}: ActivationStepProps) {
  return (
    <div
      data-testid={testId}
      data-completed={completed}
      data-collapsed={collapsed}
      className={cn(
        'rounded-lg border p-4 transition-all',
        completed ? 'border-green-500/30 bg-green-500/5' : 'border-border',
        disabled && 'opacity-50',
      )}
    >
      <div className="flex items-center gap-3">
        <div
          className={cn(
            'flex h-7 w-7 items-center justify-center rounded-full text-sm font-medium',
            completed
              ? 'bg-green-500 text-white'
              : 'border border-muted-foreground text-muted-foreground',
          )}
        >
          {completed ? <Check className="h-4 w-4" /> : stepNumber}
        </div>
        <span className={cn('font-medium', completed && 'text-green-600 dark:text-green-400')}>
          {title}
        </span>
      </div>
      {!collapsed && children && <div className="mt-3 pl-10">{children}</div>}
    </div>
  );
}
```

- [ ] **Step 4: Implement ActivationChecklist component**

```tsx
// apps/web/src/components/library/private-game-detail/ActivationChecklist.tsx
'use client';

import { Button } from '@/components/ui/button';
import { Gamepad2 } from 'lucide-react';
import { ActivationStep } from './ActivationStep';

export type PdfStatus = 'none' | 'uploading' | 'processing' | 'ready' | 'failed';
export type AgentStatus = 'none' | 'creating' | 'ready';

interface ActivationChecklistProps {
  gameAdded: boolean;
  pdfStatus: PdfStatus;
  agentStatus: AgentStatus;
  onUploadPdf: () => void;
  onCreateAgent: () => void;
  onStartGame: () => void;
  children?: React.ReactNode; // Slot for upload zone / progress bar
}

export function ActivationChecklist({
  gameAdded,
  pdfStatus,
  agentStatus,
  onUploadPdf,
  onCreateAgent,
  onStartGame,
  children,
}: ActivationChecklistProps) {
  const pdfReady = pdfStatus === 'ready';
  const agentReady = agentStatus === 'ready';
  const canStartGame = gameAdded && pdfReady;

  return (
    <div className="space-y-3">
      <h3 className="text-lg font-semibold">Attiva l&apos;Assistente AI</h3>

      <ActivationStep
        stepNumber={1}
        title="Gioco aggiunto alla libreria"
        completed={gameAdded}
        collapsed={gameAdded}
        testId="step-game-added"
      />

      <ActivationStep
        stepNumber={2}
        title="Carica il regolamento (PDF)"
        completed={pdfReady}
        collapsed={pdfReady}
        testId="step-pdf"
      >
        {pdfStatus === 'none' && (
          <div className="space-y-2">
            <p className="text-sm text-muted-foreground">
              Carica il PDF del regolamento per attivare l&apos;assistente AI.
            </p>
            <Button variant="outline" size="sm" onClick={onUploadPdf}>
              Carica regolamento
            </Button>
          </div>
        )}
        {(pdfStatus === 'uploading' || pdfStatus === 'processing') && children}
        {pdfStatus === 'failed' && (
          <div className="space-y-2">
            <p className="text-sm text-destructive">Elaborazione fallita.</p>
            <Button variant="outline" size="sm" onClick={onUploadPdf}>
              Riprova
            </Button>
          </div>
        )}
      </ActivationStep>

      <ActivationStep
        stepNumber={3}
        title="Agente AI pronto"
        completed={agentReady}
        collapsed={agentReady}
        disabled={!pdfReady}
        testId="step-agent"
      >
        {pdfReady && agentStatus === 'none' && (
          <Button variant="outline" size="sm" onClick={onCreateAgent}>
            Crea agente
          </Button>
        )}
        {agentStatus === 'creating' && (
          <p className="text-sm text-muted-foreground">Creazione in corso...</p>
        )}
      </ActivationStep>

      <Button
        className="w-full mt-4"
        size="lg"
        disabled={!canStartGame}
        onClick={onStartGame}
      >
        <Gamepad2 className="mr-2 h-5 w-5" />
        Inizia Partita
      </Button>
      {!canStartGame && (
        <p className="text-xs text-center text-muted-foreground">
          Completa almeno i primi 2 step per iniziare
        </p>
      )}
    </div>
  );
}
```

- [ ] **Step 5: Run tests and verify they pass**

Run: `cd apps/web && pnpm test __tests__/components/library/private-game-detail/ActivationChecklist.test.tsx`
Expected: PASS

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/private-game-detail/ActivationChecklist.tsx
git add apps/web/src/components/library/private-game-detail/ActivationStep.tsx
git add apps/web/__tests__/components/library/private-game-detail/ActivationChecklist.test.tsx
git commit -m "feat(frontend): add ActivationChecklist component for Smart Hub"
```

---

### Task 2: Private Game hooks + Smart Hub page

**Files:**
- Modify: `apps/web/src/hooks/queries/useLibrary.ts` — add private game query keys + hooks
- Create: `apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx`
- Create: `apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx`
- Create: `apps/web/src/app/(authenticated)/library/private/[privateGameId]/page.tsx`
- Test: `apps/web/__tests__/components/library/private-game-detail/PrivateGameHub.test.tsx`
- Test: `apps/web/__tests__/components/library/private-game-detail/PausedSessionCard.test.tsx`

- [ ] **Step 1: Add private game hooks to useLibrary.ts**

Add to `apps/web/src/hooks/queries/useLibrary.ts` — extend the existing `libraryKeys` factory and add new hooks following the existing pattern:

```ts
// Add to libraryKeys factory (after existing keys)
export const libraryKeys = {
  // ... existing keys ...
  privateGame: (id: string) => [...libraryKeys.all, 'privateGame', id] as const,
  privateGames: (params?: Record<string, unknown>) => [...libraryKeys.all, 'privateGames', { params }] as const,
};

// Add hook — follows same pattern as useLibraryGameDetail
export function usePrivateGame(
  privateGameId: string | undefined,
  enabled = true,
): UseQueryResult<PrivateGameDto> {
  return useQuery({
    queryKey: libraryKeys.privateGame(privateGameId ?? ''),
    queryFn: () => api.library.getPrivateGame(privateGameId!),
    enabled: enabled && !!privateGameId,
  });
}

// Add mutation — follows same pattern as useAddGameToLibrary
export function useAddPrivateGameFromBgg() {
  const queryClient = useQueryClient();

  return useMutation({
    mutationFn: async (request: AddPrivateGameRequest) => {
      const privateGame = await api.library.addPrivateGame(request);
      // Also add to library as Owned
      await api.library.addGame(privateGame.id, { state: 'Owned' });
      return privateGame;
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: libraryKeys.all });
    },
  });
}
```

Import the needed types at the top of the file:
```ts
import type { PrivateGameDto, AddPrivateGameRequest } from '@/lib/api/schemas/library.schemas';
```

- [ ] **Step 2: Run existing useLibrary tests to ensure no regressions**

Run: `cd apps/web && pnpm test __tests__/hooks/queries/useLibrary`
Expected: PASS (existing tests still pass)

- [ ] **Step 3: Write failing test for PrivateGameHub**

```tsx
// apps/web/__tests__/components/library/private-game-detail/PrivateGameHub.test.tsx
import { render, screen } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PrivateGameHub } from '@/components/library/private-game-detail/PrivateGameHub';

vi.mock('@/hooks/queries/useLibrary', () => ({
  usePrivateGame: () => ({
    data: {
      id: 'game-1',
      title: 'Azul',
      bggId: 230802,
      minPlayers: 2,
      maxPlayers: 4,
      yearPublished: 2017,
    },
    isLoading: false,
  }),
}));

vi.mock('@/lib/api', () => ({
  api: {
    documents: { getDocumentsByGame: vi.fn().mockResolvedValue([]) },
    agents: { getUserAgentsForGame: vi.fn().mockResolvedValue([]) },
    liveSessions: { getActive: vi.fn().mockResolvedValue([]) },
  },
}));

describe('PrivateGameHub', () => {
  it('renders game title and activation checklist', () => {
    render(<PrivateGameHub privateGameId="game-1" />);
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText(/attiva l'assistente ai/i)).toBeInTheDocument();
  });

  it('shows Start Game button', () => {
    render(<PrivateGameHub privateGameId="game-1" />);
    expect(screen.getByRole('button', { name: /inizia partita/i })).toBeInTheDocument();
  });
});
```

- [ ] **Step 4: Run test to verify it fails**

Run: `cd apps/web && pnpm test __tests__/components/library/private-game-detail/PrivateGameHub.test.tsx`
Expected: FAIL

- [ ] **Step 5: Write failing test for PausedSessionCard**

```tsx
// apps/web/__tests__/components/library/private-game-detail/PausedSessionCard.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { PausedSessionCard } from '@/components/library/private-game-detail/PausedSessionCard';

describe('PausedSessionCard', () => {
  const session = {
    id: 'sess-1',
    sessionDate: new Date(Date.now() - 3600_000).toISOString(), // 1h ago
    participants: [{ displayName: 'Alice', score: 42 }, { displayName: 'Bob', score: 38 }],
    hasPhotos: false,
    hasNotes: true,
    hasAgentSummary: false,
  };

  it('renders participant scores', () => {
    render(<PausedSessionCard session={session} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.getByText(/alice.*42/i)).toBeInTheDocument();
  });

  it('calls onResume with sessionId', () => {
    const onResume = vi.fn();
    render(<PausedSessionCard session={session} onResume={onResume} onAbandon={vi.fn()} />);
    fireEvent.click(screen.getByRole('button', { name: /riprendi/i }));
    expect(onResume).toHaveBeenCalledWith('sess-1');
  });

  it('shows "Vecchia" badge for sessions >30 days old', () => {
    const oldSession = { ...session, sessionDate: new Date(Date.now() - 31 * 86400_000).toISOString() };
    render(<PausedSessionCard session={oldSession} onResume={vi.fn()} onAbandon={vi.fn()} />);
    expect(screen.getByText(/vecchia/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 6: Implement PausedSessionCard**

```tsx
// apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx
'use client';

import { Button } from '@/components/ui/button';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Pause, Play, Trash2, FileText, Bot } from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';

interface PausedSession {
  id: string;
  sessionDate: string;
  currentTurn?: number;
  totalTurns?: number;
  participants: { displayName: string; score: number }[];
  hasPhotos: boolean;
  hasNotes: boolean;
  hasAgentSummary: boolean;
}

interface PausedSessionCardProps {
  session: PausedSession;
  onResume: (sessionId: string) => void;
  onAbandon: (sessionId: string) => void;
}

export function PausedSessionCard({ session, onResume, onAbandon }: PausedSessionCardProps) {
  const isOld = Date.now() - new Date(session.sessionDate).getTime() > 30 * 24 * 60 * 60 * 1000;

  return (
    <Card className="border-amber-500/30 bg-amber-500/5">
      <CardContent className="p-4">
        <div className="flex items-center gap-2 mb-2">
          <Pause className="h-4 w-4 text-amber-500" />
          <span className="font-medium">Partita in pausa</span>
          {isOld && (
            <span className="text-xs bg-amber-100 dark:bg-amber-900 text-amber-700 dark:text-amber-300 px-2 py-0.5 rounded">
              Vecchia
            </span>
          )}
        </div>

        <p className="text-sm text-muted-foreground mb-1">
          {formatDistanceToNow(new Date(session.sessionDate), { addSuffix: true, locale: it })}
          {session.currentTurn && session.totalTurns && (
            <> &middot; Turno {session.currentTurn} di {session.totalTurns}</>
          )}
        </p>

        <p className="text-sm mb-3">
          {session.participants.map((p) => `${p.displayName}: ${p.score}`).join(' | ')}
        </p>

        <div className="flex items-center gap-2 text-xs text-muted-foreground mb-3">
          {session.hasNotes && <span className="flex items-center gap-1"><FileText className="h-3 w-3" /> Note</span>}
          {session.hasAgentSummary && <span className="flex items-center gap-1"><Bot className="h-3 w-3" /> Riepilogo AI</span>}
        </div>

        <div className="flex gap-2">
          <Button size="sm" onClick={() => onResume(session.id)}>
            <Play className="mr-1 h-3 w-3" /> Riprendi
          </Button>
          <Button size="sm" variant="outline" className="text-destructive" onClick={() => onAbandon(session.id)}>
            <Trash2 className="mr-1 h-3 w-3" /> Abbandona
          </Button>
        </div>
      </CardContent>
    </Card>
  );
}
```

- [ ] **Step 7: Implement PrivateGameHub (derives status from server state)**

```tsx
// apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx
'use client';

import { useState, useEffect, useCallback } from 'react';
import { useRouter } from 'next/navigation';
import Image from 'next/image';
import { usePrivateGame } from '@/hooks/queries/useLibrary';
import { ActivationChecklist, type PdfStatus, type AgentStatus } from './ActivationChecklist';
import { PausedSessionCard } from './PausedSessionCard';
import { CopyrightDisclaimerModal } from '@/components/pdf/CopyrightDisclaimerModal';
import { PdfProcessingProgressBar } from '@/components/pdf/PdfProcessingProgressBar';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { api } from '@/lib/api';

interface PrivateGameHubProps {
  privateGameId: string;
}

export function PrivateGameHub({ privateGameId }: PrivateGameHubProps) {
  const router = useRouter();
  const { data: game, isLoading } = usePrivateGame(privateGameId);
  const [showDisclaimer, setShowDisclaimer] = useState(false);
  const [pdfStatus, setPdfStatus] = useState<PdfStatus>('none');
  const [agentStatus, setAgentStatus] = useState<AgentStatus>('none');
  const [activePdfId, setActivePdfId] = useState<string | null>(null);
  const [pausedSessions, setPausedSessions] = useState<Array<{
    id: string;
    sessionDate: string;
    participants: { displayName: string; score: number }[];
    hasPhotos: boolean;
    hasNotes: boolean;
    hasAgentSummary: boolean;
  }>>([]);

  // Derive PDF and agent status from server on mount
  useEffect(() => {
    if (!privateGameId) return;
    let cancelled = false;

    async function loadStatus() {
      try {
        // Check existing PDFs for this game
        const pdfs = await api.documents.getDocumentsByGame(privateGameId);
        if (pdfs.length > 0) {
          const latestPdf = pdfs[0];
          if (latestPdf.processingState === 'Ready' || latestPdf.processingState === 'Indexed') {
            setPdfStatus('ready');
            setActivePdfId(latestPdf.id);
          } else if (['Extracting', 'Chunking', 'Embedding', 'Indexing'].includes(latestPdf.processingState)) {
            setPdfStatus('processing');
            setActivePdfId(latestPdf.id);
          } else if (latestPdf.processingState === 'Failed') {
            setPdfStatus('failed');
          }
        }

        // Check existing agents for this game
        const agents = await api.agents.getUserAgentsForGame(privateGameId);
        if (agents.length > 0) {
          setAgentStatus('ready');
        }

        // Load paused sessions (getActive returns all active/paused sessions)
        const sessions = await api.liveSessions.getActive();
        const paused = sessions
          .filter((s) => s.status === 'Paused' && s.gameId === privateGameId)
          .sort((a, b) => new Date(b.sessionDate).getTime() - new Date(a.sessionDate).getTime());
        setPausedSessions(paused.map((s) => ({
          id: s.id,
          sessionDate: s.sessionDate,
          participants: s.players?.map(p => ({
            displayName: p.displayName,
            score: p.totalScore,
          })) ?? [],
          hasPhotos: false,
          hasNotes: false,
          hasAgentSummary: false,
        })));
      } catch {
        // Non-critical — checklist will start from defaults
      }
    }

    loadStatus();
    return () => { cancelled = true; };
  }, [privateGameId]);

  // Auto-create agent when PDF becomes ready (per spec)
  useEffect(() => {
    if (pdfStatus === 'ready' && agentStatus === 'none') {
      setAgentStatus('creating');
      api.agents.createUserAgent({ gameId: privateGameId, agentType: 'TutorAgent' })
        .then(() => setAgentStatus('ready'))
        .catch(() => setAgentStatus('none'));
    }
  }, [pdfStatus, agentStatus, privateGameId]);

  const handleUploadPdf = useCallback(() => setShowDisclaimer(true), []);

  const handleDisclaimerAccept = useCallback(async () => {
    setShowDisclaimer(false);
    setPdfStatus('uploading');
    try {
      // File will be wired in Task 4 via file picker — placeholder for now
      const result = await api.pdf.uploadPdf(privateGameId, null as unknown as File);
      setActivePdfId(result.documentId);
      setPdfStatus('processing');
    } catch {
      setPdfStatus('failed');
    }
  }, [privateGameId]);

  const handleCreateAgent = useCallback(async () => {
    setAgentStatus('creating');
    try {
      await api.agents.createUserAgent({ gameId: privateGameId, agentType: 'TutorAgent' });
      setAgentStatus('ready');
    } catch {
      setAgentStatus('none');
    }
  }, [privateGameId]);

  const handleStartGame = useCallback(async () => {
    try {
      const sessionId = await api.liveSessions.createSession({ gameId: privateGameId });
      router.push(`/sessions/${sessionId}/play`);
    } catch {
      // Show error toast
    }
  }, [privateGameId, router]);

  const handleResumeSession = useCallback(async (sessionId: string) => {
    try {
      await api.liveSessions.resumeSession(sessionId);
      router.push(`/sessions/${sessionId}/play`);
    } catch {
      // Show error toast
    }
  }, [router]);

  const handleAbandonSession = useCallback(async (sessionId: string) => {
    if (!confirm('La partita verra\' archiviata. Sei sicuro?')) return;
    try {
      await api.liveSessions.completeSession(sessionId);
      setPausedSessions(prev => prev.filter(s => s.id !== sessionId));
    } catch {
      // Show error toast
    }
  }, []);

  if (isLoading) {
    return <div className="space-y-4"><Skeleton className="h-48" /><Skeleton className="h-64" /></div>;
  }

  if (!game) return <p>Gioco non trovato.</p>;

  return (
    <div className="max-w-2xl mx-auto space-y-6">
      {/* Game Header */}
      <div className="flex gap-4">
        {game.thumbnailUrl && (
          <Image src={game.thumbnailUrl} alt={game.title} width={96} height={96} className="rounded-lg object-cover" />
        )}
        <div>
          <h1 className="text-2xl font-bold">{game.title}</h1>
          {game.yearPublished && <p className="text-muted-foreground">{game.yearPublished}</p>}
          <p className="text-sm text-muted-foreground">
            {game.minPlayers}–{game.maxPlayers} giocatori
          </p>
        </div>
      </div>

      {/* Paused Sessions (most recent first) */}
      {pausedSessions.length > 0 && (
        <div className="space-y-3">
          <h3 className="text-lg font-semibold">Partite in pausa</h3>
          {pausedSessions.map(session => (
            <PausedSessionCard
              key={session.id}
              session={session}
              onResume={handleResumeSession}
              onAbandon={handleAbandonSession}
            />
          ))}
        </div>
      )}

      {/* Activation Checklist */}
      <ActivationChecklist
        gameAdded={true}
        pdfStatus={pdfStatus}
        agentStatus={agentStatus}
        onUploadPdf={handleUploadPdf}
        onCreateAgent={handleCreateAgent}
        onStartGame={handleStartGame}
      >
        {activePdfId && (pdfStatus === 'uploading' || pdfStatus === 'processing') && (
          <PdfProcessingProgressBar pdfId={activePdfId} />
        )}
      </ActivationChecklist>

      {/* Reuse existing CopyrightDisclaimerModal from components/pdf/ */}
      <CopyrightDisclaimerModal
        open={showDisclaimer}
        onAccept={handleDisclaimerAccept}
        onCancel={() => setShowDisclaimer(false)}
      />
    </div>
  );
}
```

- [ ] **Step 8: Create the page route**

```tsx
// apps/web/src/app/(authenticated)/library/private/[privateGameId]/page.tsx
import { PrivateGameHub } from '@/components/library/private-game-detail/PrivateGameHub';

interface Props {
  params: Promise<{ privateGameId: string }>;
}

export default async function PrivateGameDetailPage({ params }: Props) {
  const { privateGameId } = await params;
  return <PrivateGameHub privateGameId={privateGameId} />;
}
```

Note: The existing route at `library/private/[privateGameId]/` already has a `toolkit/` sub-route. This adds the index page.

- [ ] **Step 9: Run tests and verify they pass**

Run: `cd apps/web && pnpm test __tests__/components/library/private-game-detail/`
Expected: PASS for all tests

- [ ] **Step 10: Commit**

```bash
git add apps/web/src/hooks/queries/useLibrary.ts
git add apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx
git add apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx
git add apps/web/src/app/(authenticated)/library/private/[privateGameId]/page.tsx
git add apps/web/__tests__/components/library/private-game-detail/
git commit -m "feat(frontend): add Private Game Hub page with server-derived status"
```

---

## Chunk 2: BGG → Add Private Game Flow

### Task 3: "Aggiungi" button on BGG search results

**Files:**
- Modify: `apps/web/src/app/(authenticated)/discover/BggSearchTab.tsx` — add private game shortcut
- Test: `apps/web/__tests__/hooks/queries/useLibrary.test.ts` — test useAddPrivateGameFromBgg

Note: The existing `BggSearchTab` uses `openWizard({ type: 'fromSearch', bggId })` to open the AddGameWizard. The plan adds an alternative "Aggiungi come privato" option that bypasses the wizard for the quick-add flow, while keeping the existing wizard button for shared catalog adds.

- [ ] **Step 1: Write test for useAddPrivateGameFromBgg mutation**

```ts
// apps/web/__tests__/hooks/queries/useAddPrivateGameFromBgg.test.ts
import { renderHook, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { useAddPrivateGameFromBgg } from '@/hooks/queries/useLibrary';
import { api } from '@/lib/api';

vi.mock('@/lib/api', () => ({
  api: {
    library: {
      addPrivateGame: vi.fn(),
      addGame: vi.fn(),
    },
  },
}));

const wrapper = ({ children }: { children: React.ReactNode }) => {
  const queryClient = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>;
};

describe('useAddPrivateGameFromBgg', () => {
  beforeEach(() => vi.clearAllMocks());

  it('calls addPrivateGame then addGame in sequence', async () => {
    const mockGame = { id: 'pg-1', title: 'Azul' };
    vi.mocked(api.library.addPrivateGame).mockResolvedValue(mockGame);
    vi.mocked(api.library.addGame).mockResolvedValue({} as never);

    const { result } = renderHook(() => useAddPrivateGameFromBgg(), { wrapper });

    result.current.mutate({ source: 'BoardGameGeek', bggId: 230802, title: 'Azul' } as never);

    await waitFor(() => expect(result.current.isSuccess).toBe(true));
    expect(api.library.addPrivateGame).toHaveBeenCalledOnce();
    expect(api.library.addGame).toHaveBeenCalledWith('pg-1', { state: 'Owned' });
  });

  it('surfaces 409 conflict error for shared catalog match', async () => {
    const conflictError = { response: { status: 409 } };
    vi.mocked(api.library.addPrivateGame).mockRejectedValue(conflictError);

    const { result } = renderHook(() => useAddPrivateGameFromBgg(), { wrapper });

    result.current.mutate({ source: 'BoardGameGeek', bggId: 230802 } as never);

    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.error).toBeDefined();
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test __tests__/hooks/queries/useAddPrivateGameFromBgg.test.ts`
Expected: FAIL (hook already added in Task 2 Step 1, but test should initially fail for the assertions)

- [ ] **Step 3: Add "Aggiungi come privato" option to BggSearchTab**

Modify `apps/web/src/app/(authenticated)/discover/BggSearchTab.tsx`:
- The existing "Aggiungi" label (line ~183) opens the wizard via `openWizard({ type: 'fromSearch', bggId })`
- Add a dropdown or secondary button next to each BGG result: "Aggiungi come privato"
- On click: call `useAddPrivateGameFromBgg().mutate({ source: 'BoardGameGeek', bggId, title, ... })`
- Show loading spinner on the button while mutation is in flight
- On success: `router.push(`/library/private/${data.id}`)`
- On 409 error: show inline message "Questo gioco esiste nel catalogo condiviso" with option to "Aggiungi dal catalogo" (opens wizard) or "Aggiungi come privato comunque"

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test __tests__/hooks/queries/useAddPrivateGameFromBgg.test.ts`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/hooks/queries/useLibrary.ts
git add apps/web/src/app/(authenticated)/discover/BggSearchTab.tsx
git add apps/web/__tests__/hooks/queries/useAddPrivateGameFromBgg.test.ts
git commit -m "feat(frontend): add BGG to private game flow with conflict detection"
```

---

### Task 4: Wire PDF upload + disclaimer in Smart Hub

**Files:**
- Modify: `apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx` — wire actual file picker

Note: The `PrivateGameHub` already has `handleDisclaimerAccept` wired. This task connects the actual file picker UI that appears after the disclaimer is accepted. The existing `CopyrightDisclaimerModal` from `components/pdf/` is reused as-is (no `gameName` prop needed — the existing disclaimer text is generic enough).

- [ ] **Step 1: Wire file input into the upload flow**

In `PrivateGameHub.tsx`, update `handleDisclaimerAccept`:
- After disclaimer accepted, show a file picker (use existing `PdfUploadForm` or native `<input type="file">`)
- Call `api.pdf.upload({ privateGameId, file })` with the selected file
- On upload success: store `pdfId`, set `pdfStatus = 'processing'`
- The `PdfProcessingProgressBar` (already in the component) handles SSE progress display
- When SSE reports `Ready`: the `useEffect` that checks server state will detect it on next load, or add an `onComplete` callback to the progress bar

- [ ] **Step 2: Verify auto-agent creation**

The `useEffect` in `PrivateGameHub` already auto-creates the agent when `pdfStatus === 'ready'` and `agentStatus === 'none'`. Verify this triggers correctly when the progress bar reports completion.

- [ ] **Step 3: Manual test the full Smart Hub flow**

Open browser → Discover → BGG search → Add as private → Redirected to Smart Hub → Upload PDF → Accept disclaimer → Watch progress → Agent auto-created → Start game.

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx
git commit -m "feat(frontend): wire PDF upload and auto-agent creation in Smart Hub"
```

---

## Chunk 3: Live Session — Wire Agent Chat

**Important:** This chunk refactors the existing `LiveSessionView` (450 lines) to wire real agent integration. It does NOT create a parallel system. The existing `SessionChatWidget` has a placeholder response — we replace it with real `useAgentChatStream` calls. The existing `QuickActions` for Rules and Arbiter have placeholder text — we wire them to real agent endpoints.

### Task 5: Wire SessionChatWidget to TutorAgent (Setup chat)

**Files:**
- Modify: `apps/web/src/components/game-night/LiveSessionView.tsx` — replace placeholder chat with real agent
- Existing: `apps/web/src/hooks/useAgentChatStream.ts` — SSE streaming hook
- Existing: `apps/web/src/components/chat-unified/ChatThreadView.tsx` — unified chat UI
- Test: `apps/web/__tests__/components/game-night/LiveSessionView.test.tsx` — add agent chat test

- [ ] **Step 1: Write test for agent chat wiring**

```tsx
// apps/web/__tests__/components/game-night/LiveSessionChatWiring.test.tsx
import { describe, it, expect, vi } from 'vitest';

// Test that LiveSessionView uses useAgentChatStream instead of placeholder
vi.mock('@/hooks/useAgentChatStream', () => ({
  useAgentChatStream: vi.fn().mockReturnValue({
    messages: [],
    isStreaming: false,
    sendMessage: vi.fn(),
  }),
}));

describe('LiveSessionView agent wiring', () => {
  it('calls useAgentChatStream when agentId is available', async () => {
    const { useAgentChatStream } = await import('@/hooks/useAgentChatStream');
    // After LiveSessionView mounts with a session that has an agentId,
    // useAgentChatStream should be called
    expect(useAgentChatStream).toBeDefined();
  });
});
```

- [ ] **Step 2: Replace placeholder chat in LiveSessionView**

In `apps/web/src/components/game-night/LiveSessionView.tsx`:

Replace the `handleChatSend` callback (lines ~192-216) that uses `setTimeout` placeholder with real agent chat:

```tsx
// Add import at top
import { useAgentChatStream } from '@/hooks/useAgentChatStream';

// Inside the component, replace the chat handling:
// 1. Get agentId from the active session
const agentId = activeSession?.agentId;

// 2. Use real agent chat stream instead of placeholder
const {
  messages: agentMessages,
  isStreaming: agentStreaming,
  sendMessage: sendAgentMessage,
} = useAgentChatStream({
  agentId: agentId ?? '',
  threadId: `session-${sessionId}-setup`,
  enabled: !!agentId,
});

// 3. Update chatMessages and isChatStreaming to use agent data
// Map agentMessages to ChatMessage format for SessionChatWidget
const chatMessages: ChatMessage[] = agentMessages.map(m => ({
  id: m.id,
  role: m.role as 'user' | 'assistant',
  content: m.content,
  timestamp: new Date(m.createdAt),
}));

const handleChatSend = useCallback((message: string) => {
  if (agentId) {
    sendAgentMessage(message);
  }
}, [agentId, sendAgentMessage]);
```

Remove the old `chatTimerRef`, `setChatMessages`, `setIsChatStreaming` state and related cleanup.

- [ ] **Step 3: Run tests**

Run: `cd apps/web && pnpm test __tests__/components/game-night/`
Expected: PASS

- [ ] **Step 4: Commit**

```bash
git add apps/web/src/components/game-night/LiveSessionView.tsx
git add apps/web/__tests__/components/game-night/
git commit -m "feat(frontend): wire SessionChatWidget to TutorAgent via useAgentChatStream"
```

---

### Task 6: Wire Rules Sheet to ArbitroAgent

**Files:**
- Modify: `apps/web/src/components/game-night/LiveSessionView.tsx` — wire rules sheet to real agent

The existing `LiveSessionView` has a Rules Sheet (lines ~319-335) with placeholder text. Wire it to the ArbitroAgent.

- [ ] **Step 1: Wire rules sheet content**

In the Rules Sheet content (currently showing "Il modulo regole verrà collegato..."), replace with:
- Embed a chat interface using `useAgentChatStream` with agent type `ArbitroAgent`
- Use thread ID `session-${sessionId}-rules` (separate from setup chat)
- Add chip suggestions at the top: "Chi ha ragione?", "Si può fare X?", "Cosa succede quando..."
- The sheet already has proper SheetHeader with title "Regole del gioco"

```tsx
// In the overlays section, replace the Rules Sheet content:
<Sheet open={rulesOpen} onOpenChange={setRulesOpen}>
  <SheetContent side="right" className="w-full sm:max-w-md overflow-y-auto">
    <SheetHeader>
      <SheetTitle>Regole del gioco</SheetTitle>
      <SheetDescription>
        Chiedi all&apos;arbitro AI per risolvere dispute sulle regole.
      </SheetDescription>
    </SheetHeader>
    <div className="mt-4 flex flex-col h-[calc(100vh-8rem)]">
      <SessionChatWidget
        messages={rulesMessages}
        isStreaming={rulesStreaming}
        onSend={sendRulesMessage}
        defaultExpanded={true}
        suggestions={['Chi ha ragione?', 'Si può fare X?', 'Cosa succede quando...']}
      />
    </div>
  </SheetContent>
</Sheet>
```

Add the ArbitroAgent hook call:
```tsx
const {
  messages: rulesAgentMessages,
  isStreaming: rulesStreaming,
  sendMessage: sendRulesAgentMessage,
} = useAgentChatStream({
  agentId: activeSession?.arbitroAgentId ?? '',
  threadId: `session-${sessionId}-rules`,
  enabled: !!activeSession?.arbitroAgentId,
});

const rulesMessages: ChatMessage[] = rulesAgentMessages.map(m => ({
  id: m.id,
  role: m.role as 'user' | 'assistant',
  content: m.content,
  timestamp: new Date(m.createdAt),
}));

const sendRulesMessage = useCallback((message: string) => {
  if (activeSession?.arbitroAgentId) {
    sendRulesAgentMessage(message);
  }
}, [activeSession?.arbitroAgentId, sendRulesAgentMessage]);
```

- [ ] **Step 2: Wire Arbiter dialog similarly**

Replace the arbiter dialog placeholder (lines ~337-348) with the same ArbitroAgent chat, or redirect the "Chiedi all'arbitro" button to switch to the Rules Sheet instead of a separate dialog.

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/game-night/LiveSessionView.tsx
git commit -m "feat(frontend): wire Rules sheet and Arbiter to ArbitroAgent chat"
```

---

### Task 7: Ensure score tracking works end-to-end

**Files:**
- Existing: `apps/web/src/components/game-night/LiveSessionView.tsx` — already has score wiring
- Existing: `apps/web/src/components/session/ScoreInput.tsx` — already functional
- Existing: `apps/web/src/components/game-night/ScoreAssistant.tsx` — quick score entry

The existing `LiveSessionView` already has full score tracking: `ScoreInput`, `ScoreAssistant`, `LiveScoreboard`, and the `recordScore` store action. This task verifies it works and fixes any gaps.

- [ ] **Step 1: Verify score flow**

Run the app → Create session → Open score sheet → Record scores → Verify `LiveScoreboard` updates in real time via SSE.

- [ ] **Step 2: Fix any issues found**

If `ScoreAssistant` or `ScoreInput` need adjustments for the game-night flow (e.g., missing participant data), fix them.

- [ ] **Step 3: Commit if changes needed**

```bash
git add apps/web/src/components/game-night/
git commit -m "fix(frontend): ensure score tracking works in live session flow"
```

---

## Chunk 4: Save, Resume & Navigation

### Task 8: Extend SaveCompleteDialog with notes and photo

**Files:**
- Modify: `apps/web/src/components/game-night/SaveCompleteDialog.tsx` — add optional notes/photo/AI summary
- Test: `apps/web/__tests__/components/game-night/SaveCompleteDialog.test.tsx`

The existing `SaveCompleteDialog` calls `api.liveSessions.saveComplete(sessionId)` with a 3-phase flow (confirm → saving → done). Extend it to collect optional notes, photo, and AI summary before saving.

- [ ] **Step 1: Write test for extended save dialog**

```tsx
// apps/web/__tests__/components/game-night/SaveCompleteDialog.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { describe, it, expect, vi } from 'vitest';
import { SaveCompleteDialog } from '@/components/game-night/SaveCompleteDialog';

vi.mock('@/lib/api', () => ({
  api: { liveSessions: { saveComplete: vi.fn().mockResolvedValue({ savedAt: new Date().toISOString() }) } },
}));

describe('SaveCompleteDialog', () => {
  const props = {
    open: true,
    onOpenChange: vi.fn(),
    sessionId: 'session-1',
    onSaveComplete: vi.fn(),
  };

  it('renders notes textarea', () => {
    render(<SaveCompleteDialog {...props} />);
    expect(screen.getByPlaceholderText(/note/i)).toBeInTheDocument();
  });

  it('renders optional photo upload area', () => {
    render(<SaveCompleteDialog {...props} />);
    expect(screen.getByText(/foto/i)).toBeInTheDocument();
  });

  it('renders Generate AI Summary button', () => {
    render(<SaveCompleteDialog {...props} />);
    expect(screen.getByRole('button', { name: /riepilogo/i })).toBeInTheDocument();
  });

  it('calls saveComplete on confirm', async () => {
    const { api } = await import('@/lib/api');
    render(<SaveCompleteDialog {...props} />);
    fireEvent.click(screen.getByRole('button', { name: /salva/i }));
    expect(api.liveSessions.saveComplete).toHaveBeenCalledWith('session-1');
  });
});
```

- [ ] **Step 2: Run test to verify it fails**

Run: `cd apps/web && pnpm test __tests__/components/game-night/SaveCompleteDialog.test.tsx`
Expected: FAIL (new UI elements not yet added)

- [ ] **Step 3: Extend SaveCompleteDialog**

In the `confirm` phase content of `SaveCompleteDialog.tsx`, add before the save button:

```tsx
{/* Optional notes */}
<textarea
  className="w-full rounded-md border bg-background px-3 py-2 text-sm"
  placeholder="Note sulla partita (opzionale)..."
  rows={3}
  value={notes}
  onChange={(e) => setNotes(e.target.value)}
/>

{/* Optional photo */}
<div className="flex items-center gap-2">
  <Button variant="outline" size="sm" onClick={() => photoInputRef.current?.click()}>
    <Camera className="mr-1 h-4 w-4" /> Foto
  </Button>
  <input ref={photoInputRef} type="file" accept="image/*" className="hidden" onChange={handlePhotoSelect} />
  {photoFile && <span className="text-xs text-muted-foreground">{photoFile.name}</span>}
</div>

{/* Optional AI summary */}
<Button variant="outline" size="sm" onClick={handleGenerateSummary} disabled={isSummarizing}>
  {isSummarizing ? <Loader2 className="mr-1 h-4 w-4 animate-spin" /> : <Bot className="mr-1 h-4 w-4" />}
  Genera riepilogo AI
</Button>
{aiSummary && <p className="text-sm bg-muted p-2 rounded">{aiSummary}</p>}
```

Add state:
```tsx
const [notes, setNotes] = useState('');
const [photoFile, setPhotoFile] = useState<File | null>(null);
const [aiSummary, setAiSummary] = useState('');
const [isSummarizing, setIsSummarizing] = useState(false);
const photoInputRef = useRef<HTMLInputElement>(null);
```

Extend `handleSave` to upload notes/photo/summary before calling `saveComplete`:
```tsx
const handleSave = useCallback(async () => {
  setPhase('saving');
  try {
    if (notes) await api.liveSessions.addNote(sessionId, { content: notes });
    if (photoFile) await api.liveSessions.uploadMedia(sessionId, { type: 'Photo', file: photoFile });
    const saveResult = await api.liveSessions.saveComplete(sessionId);
    setResult(saveResult);
    setPhase('done');
  } catch {
    setErrorMsg('Errore durante il salvataggio.');
    setPhase('error');
  }
}, [sessionId, notes, photoFile]);
```

- [ ] **Step 4: Run tests and verify they pass**

Run: `cd apps/web && pnpm test __tests__/components/game-night/SaveCompleteDialog.test.tsx`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/game-night/SaveCompleteDialog.tsx
git add apps/web/__tests__/components/game-night/SaveCompleteDialog.test.tsx
git commit -m "feat(frontend): extend SaveCompleteDialog with notes, photo, and AI summary"
```

---

### Task 9: Resume from Smart Hub + >30 day confirmation

**Files:**
- Modify: `apps/web/src/components/library/private-game-detail/PrivateGameHub.tsx` — already renders PausedSessionCard
- Modify: `apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx` — add >30 day confirm

The `PrivateGameHub` already loads paused sessions and renders `PausedSessionCard` (Task 2). This task adds the >30 day confirmation dialog per spec.

- [ ] **Step 1: Add >30 day confirmation to resume flow**

In `PausedSessionCard.tsx`, modify `onResume`:

```tsx
const handleResume = useCallback(() => {
  if (isOld) {
    if (!confirm('Questa partita e\' stata messa in pausa piu\' di 30 giorni fa. Vuoi ancora riprenderla?')) return;
  }
  onResume(session.id);
}, [isOld, onResume, session.id]);
```

- [ ] **Step 2: Write test for >30 day confirmation**

Add to the existing `PausedSessionCard.test.tsx`:

```tsx
it('prompts for confirmation on old session resume', () => {
  const confirmSpy = vi.spyOn(window, 'confirm').mockReturnValue(false);
  const onResume = vi.fn();
  const oldSession = { ...session, sessionDate: new Date(Date.now() - 31 * 86400_000).toISOString() };
  render(<PausedSessionCard session={oldSession} onResume={onResume} onAbandon={vi.fn()} />);
  fireEvent.click(screen.getByRole('button', { name: /riprendi/i }));
  expect(confirmSpy).toHaveBeenCalled();
  expect(onResume).not.toHaveBeenCalled();
  confirmSpy.mockRestore();
});
```

- [ ] **Step 3: Verify agent context on resume**

When a session resumes, the `LiveSessionView` loads via `useSessionStore.loadSession(id)`. The agent chat should show a context message if an `AgentGameStateSnapshot` exists. Add to `LiveSessionView`:

```tsx
// After session loads, check for resume context
useEffect(() => {
  if (activeSession?.lastSnapshotSummary && chatMessages.length === 0) {
    const resumeMsg: ChatMessage = {
      id: 'resume-context',
      role: 'assistant',
      content: `Ecco dove eravamo rimasti: ${activeSession.lastSnapshotSummary}`,
      timestamp: new Date(),
    };
    // Prepend as first message in chat
  }
}, [activeSession?.lastSnapshotSummary]);
```

- [ ] **Step 4: Run tests**

Run: `cd apps/web && pnpm test __tests__/components/library/private-game-detail/`
Expected: PASS

- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/library/private-game-detail/PausedSessionCard.tsx
git add apps/web/__tests__/components/library/private-game-detail/PausedSessionCard.test.tsx
git add apps/web/src/components/game-night/LiveSessionView.tsx
git commit -m "feat(frontend): add >30 day confirmation and resume context message"
```

---

## Chunk 5: Integration & Polish

### Task 10: Notification deep link for PDF ready

**Files:**
- Modify: `apps/web/src/components/notifications/NotificationItem.tsx` — add deep link handler
- Test: `apps/web/__tests__/components/notifications/NotificationDeepLink.test.tsx`

- [ ] **Step 1: Write test for deep link**

```tsx
// apps/web/__tests__/components/notifications/NotificationDeepLink.test.tsx
import { describe, it, expect } from 'vitest';

describe('Notification deep link', () => {
  it('generates correct href for PdfUploadCompleted notification', () => {
    const notification = {
      type: 'PdfUploadCompleted',
      metadata: { privateGameId: 'game-1' },
    };
    const expectedHref = '/library/private/game-1';
    expect(expectedHref).toContain('library/private/game-1');
  });
});
```

- [ ] **Step 2: Add deep link for PdfUploadCompleted notification**

In `NotificationItem.tsx`, when notification type is `PdfUploadCompleted`:
- Extract `privateGameId` from notification metadata
- Navigate to `/library/private/${privateGameId}` on click
- The hub will show the checklist with PDF step completed

- [ ] **Step 3: Commit**

```bash
git add apps/web/src/components/notifications/NotificationItem.tsx
git add apps/web/__tests__/components/notifications/NotificationDeepLink.test.tsx
git commit -m "feat(frontend): add notification deep link to Smart Hub on PDF ready"
```

---

### Task 11: Private games list → hub navigation

**Files:**
- Modify: `apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx` — add click handler

- [ ] **Step 1: Make private game cards clickable**

In `PrivateGamesClient.tsx`:
- Each private game card should navigate to `/library/private/${gameId}` on click
- If the game has an agent ready, show a small play icon for quick-start

- [ ] **Step 2: Commit**

```bash
git add apps/web/src/app/(authenticated)/library/private/PrivateGamesClient.tsx
git commit -m "feat(frontend): make private game cards navigate to Smart Hub"
```

---

### Task 12: End-to-end manual test

- [ ] **Step 1: Full flow test**

1. Go to Discover → BGG Search → search "Azul" → click "Aggiungi come privato"
2. Redirected to Smart Hub → checklist shows step 1 completed
3. Click upload → disclaimer modal (existing AlertDialog) → accept → upload PDF
4. Watch progress bar (or navigate away and wait for notification)
5. Agent auto-created → all steps green
6. Click "Inizia Partita" → redirected to Live Session at `/sessions/{id}/play`
7. Chat: ask "Come si prepara per 3 giocatori?" → verify SSE streaming + RAG citations
8. Scores: use ScoreInput → record scores → verify LiveScoreboard updates
9. Rules sheet: ask "Si possono prendere tessere di due colori?" → verify ArbitroAgent response
10. Click Pause → SaveCompleteDialog → add notes + optional photo → save
11. Go back to Smart Hub → see PausedSessionCard → click Resume
12. Verify scores, turn, and chat history are preserved
13. Test >30 day old session resume confirmation (if testable)
14. Test "Abbandona" flow (soft-delete confirmation)
15. Test "navigate away during PDF processing" → notification bell → click → back to hub

- [ ] **Step 2: Fix any issues found**

- [ ] **Step 3: Final commit**

```bash
git commit -m "fix(frontend): address integration issues from E2E testing"
```

---

## Summary

| Chunk | Tasks | Key Changes |
|-------|-------|-------------|
| 1: Smart Hub | Tasks 1-2 (Checklist, Hub page, hooks, PausedSessionCard) | New components + server-derived status |
| 2: BGG Flow | Tasks 3-4 (Add from BGG, wire upload) | Extend BggSearchTab + hook in useLibrary.ts |
| 3: Live Session | Tasks 5-7 (Wire agent chat, rules, verify scores) | Refactor existing LiveSessionView — NO parallel system |
| 4: Save/Resume | Tasks 8-9 (Extend SaveCompleteDialog, resume flow) | Extend existing dialog + >30 day confirm |
| 5: Polish | Tasks 10-12 (Notifications, navigation, E2E) | Integration wiring + testing |
| **Total** | **12 tasks** | **~12-16 days** |

### Key Reuse Decisions

| Existing Component | Reuse Strategy |
|-------------------|----------------|
| `CopyrightDisclaimerModal` (pdf/) | Reuse as-is — no gameName prop needed |
| `LiveSessionView` (game-night/) | Refactor: replace placeholder chat with real agent |
| `SaveCompleteDialog` (game-night/) | Extend: add notes/photo/AI summary |
| `SessionChatWidget` (game-night/) | Keep: wire via useAgentChatStream |
| `useSessionStore` (stores/) | Keep: use existing pause/resume/recordScore |
| `sessions/[id]/play` route | Keep: no new session routes needed |
| `useLibrary.ts` hooks | Extend: add privateGame query + mutation hooks |

### Dependencies

```
Task 1 (ActivationChecklist) ─┐
                               ├─→ Task 2 (Hub page + hooks + PausedSessionCard)
                               │
Task 3 (BGG Add flow) ────────┤
                               ├─→ Task 4 (Wire upload in hub)
                               │
Task 5 (Wire agent chat) ─────┤
Task 6 (Wire rules sheet) ────┤ (5, 6, 7 are parallel)
Task 7 (Verify scores) ───────┤
                               │
Task 8 (Extend SaveCompleteDialog) ─→ Task 9 (Resume + >30 day)
                               │
Tasks 2,4,7,9 ────────────────┴─→ Tasks 10-12 (Polish)
```

Tasks 1, 3, 5+6+7, 8 can start in parallel.
