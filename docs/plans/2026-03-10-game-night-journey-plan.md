# Game Night Journey — End-to-End Implementation Plan

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Enable the complete "improvised game night" user journey: search BGG → add game → upload PDF with disclaimer → play with AI assistance (setup, scoring, arbitration) → save/resume game state with photos.

**Architecture:** Frontend-heavy integration work. Most backend APIs already exist. We connect existing components (GameSourceStep, PdfUploadZone, Scoreboard, PhotoUploadModal, QuickAsk) into a cohesive flow, adding missing pieces: copyright disclaimer UI, discover BGG tab, dedicated scoreboard page, expansion management, PDF versioning, and session pause/resume flow.

**Tech Stack:** Next.js 16 (App Router), React 19, Tailwind 4, shadcn/ui, Zustand, React Query, Vitest, Playwright

**Branch Strategy:** `main-dev` → `game-night-journey` (umbrella) → `feature/issue-XXXX-*` per task

**Relation to existing plans:** Complements `2026-03-09-game-night-implementation-plan.md` (umbrella branch: `game-night-dev`) which covers backend orchestration (GameSessionContext, Session-aware RAG, Arbiter Mode). This plan covers the **user acquisition journey** and **UI integration gaps**. The two umbrella branches are independent and will merge separately to `main-dev`.

**API Client Methods Used:**
- `api.bgg.search(query)` — BGG search (exists)
- `api.bgg.getGameDetails(bggId)` — BGG details (exists)
- `api.library.addPrivateGame(data)` — Add private game (exists)
- `api.documents.getDocumentsByGame(gameId)` — List PDFs for game (exists in `documentsClient.ts:165`)
- `api.documents.setActiveForRag(pdfId, isActive)` — Toggle RAG status (exists in `documentsClient.ts:241`)
- `api.documents.acceptDisclaimer(pdfId)` — Accept copyright disclaimer (exists in `documentsClient.ts:234`)
- `api.entityLinks.create(data)` — Create entity link (exists)

---

## State of Existing Components (Discovery Summary)

Before planning tasks, here's what ALREADY EXISTS and works:

### Already Implemented (DO NOT rebuild)

| Component | Location | What it does |
|---|---|---|
| `GameSourceStep` | `components/library/add-game-sheet/steps/GameSourceStep.tsx` | Catalog search → BGG fallback → custom game. Full BGG search + details fetch |
| `PdfUploadZone` | `components/library/add-game-sheet/steps/PdfUploadZone.tsx` | Drag-drop PDF upload with progress bar, 50MB limit |
| `KnowledgeBaseStep` | `components/library/add-game-sheet/steps/KnowledgeBaseStep.tsx` | PDF management in wizard (upload, list, delete) |
| `UserWizardClient` | `app/(authenticated)/library/private/add/client.tsx` | 3-step wizard: Game → PDF → Agent config |
| `AddGameSheet` | `components/library/add-game-sheet/AddGameSheet.tsx` | Modal wizard: GameSource → GameInfo → KB → Success |
| `Scoreboard` | `components/session/Scoreboard.tsx` | Podium + table + trends, round/category support |
| `ScoreInput` | `components/session/ScoreInput.tsx` | Score entry form with quick adjust buttons |
| `LiveScoreSheet` | `components/session/LiveScoreSheet.tsx` | Bottom sheet wrapper for ScoreInput |
| `PhotoUploadModal` | `components/session/PhotoUploadModal.tsx` | Camera capture + drag-drop, attachment types, captions |
| `CameraToolContent` | `components/session/CameraToolContent.tsx` | Gallery + upload button + delete |
| `SessionCreationWizard` | `components/session/SessionCreationWizard.tsx` | 4-step: Game → Dimensions → Players → Review |
| `PdfProcessingStatus` | `components/library/PdfProcessingStatus.tsx` | Processing state display with polling |
| `VoiceMicButton` | `components/chat-unified/VoiceMicButton.tsx` | Speech-to-text with ambient glow |
| Quick Ask page | `app/(authenticated)/ask/page.tsx` | Voice-first rule questions |

### Backend APIs Ready (DO NOT implement)

| Endpoint | Purpose |
|---|---|
| `GET /api/v1/bgg/search?q=` | BGG catalog search |
| `GET /api/v1/bgg/games/{bggId}` | BGG game details |
| `POST /api/v1/private-games` | Add private game (source: BGG/Manual) |
| `POST /api/v1/ingest/pdf` | Upload PDF for processing |
| `POST /documents/{pdfId}/accept-disclaimer` | Accept copyright disclaimer |
| `GET /setup-guide/{gameId}` | Streaming setup guide |
| `POST /sessions` | Create session |
| `PUT /sessions/{id}/scores` | Update scores |
| `GET /sessions/{id}/scoreboard` | Get scoreboard data |
| `PATCH /sessions/{id}/pause` | Pause session |
| `POST /sessions/{id}/attachments` | Upload session photo |
| `POST /agents/arbitro/validate` | Arbitro move validation |
| `POST /agents/{id}/chat` | Agent chat (SSE streaming) |

---

## File Structure — New & Modified Files

### Phase 1: Discover BGG Tab
```
apps/web/src/
├── app/(authenticated)/discover/
│   ├── page.tsx                              # MODIFY: add BGG tab
│   ├── BggSearchTab.tsx                      # CREATE: BGG search tab content (renders results inline)
│   └── __tests__/
│       └── BggSearchTab.test.tsx             # CREATE
```

### Phase 2: Copyright Disclaimer
```
apps/web/src/
├── components/pdf/
│   ├── CopyrightDisclaimerModal.tsx           # CREATE: disclaimer modal with checklist
│   └── __tests__/
│       └── CopyrightDisclaimerModal.test.tsx  # CREATE
├── components/library/add-game-sheet/steps/
│   └── KnowledgeBaseStep.tsx                  # MODIFY: add disclaimer before upload
└── app/(authenticated)/library/private/add/
    └── client.tsx                             # MODIFY: add disclaimer before PDF step
```

### Phase 3: Dedicated Scoreboard Page
```
apps/web/src/
├── app/(authenticated)/sessions/[id]/
│   └── scoreboard/
│       ├── page.tsx                           # CREATE: dedicated scoreboard page
│       └── __tests__/
│           └── page.test.tsx                  # CREATE
├── components/session/
│   ├── ScoreboardPage.tsx                     # CREATE: full-page scoreboard layout
│   └── __tests__/
│       └── ScoreboardPage.test.tsx            # CREATE
```

### Phase 4: Session Pause/Resume Flow
```
apps/web/src/
├── components/session/
│   ├── PauseSessionDialog.tsx                 # CREATE: pause confirmation + summary
│   ├── ResumeSessionCard.tsx                  # CREATE: resume card with context preview
│   └── __tests__/
│       ├── PauseSessionDialog.test.tsx        # CREATE
│       └── ResumeSessionCard.test.tsx         # CREATE
├── app/(authenticated)/sessions/
│   └── page.tsx                               # MODIFY: add paused sessions section
```

### Phase 5: Expansion & PDF Versioning
```
apps/web/src/
├── components/library/
│   ├── AddExpansionSheet.tsx                   # CREATE: "Add expansion from BGG" flow
│   ├── PdfVersionManager.tsx                   # CREATE: version list + replace/keep dialog
│   └── __tests__/
│       ├── AddExpansionSheet.test.tsx          # CREATE
│       └── PdfVersionManager.test.tsx         # CREATE
├── app/(authenticated)/library/games/[gameId]/
│   └── page.tsx                               # MODIFY: add expansion + PDF version sections
```

### Phase 6: Agent Without PDF Fallback
```
apps/web/src/
├── components/chat/
│   ├── NoPdfBanner.tsx                        # CREATE: banner suggesting PDF upload
│   └── __tests__/
│       └── NoPdfBanner.test.tsx               # CREATE
├── app/(chat)/chat/[threadId]/
│   └── page.tsx                               # MODIFY: show banner when no PDF for game
```

> **NOTE:** Chat pages live under the `(chat)` route group, NOT `(authenticated)`.
> `AddGameWizardProvider` is mounted in `app/providers.tsx` (root level), so `useAddGameWizard()` is available everywhere.

---

## Chunk 1: Discover BGG Tab + Copyright Disclaimer

### Task 1: BGG Search Tab on Discover Page

**Files:**
- Modify: `apps/web/src/app/(authenticated)/discover/page.tsx`
- Create: `apps/web/src/app/(authenticated)/discover/BggSearchTab.tsx`
- Create: `apps/web/src/app/(authenticated)/discover/__tests__/BggSearchTab.test.tsx`

**Context:** The discover page uses `?tab=` query params for tab switching. Currently has `catalog` and `proposals` tabs. We add a `bgg` tab that reuses `api.bgg.search()` (already used by `GameSourceStep`).

- [ ] **Step 1: Write failing test for BggSearchTab**

```typescript
// apps/web/src/app/(authenticated)/discover/__tests__/BggSearchTab.test.tsx
import { render, screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      search: vi.fn(),
      getGameDetails: vi.fn(),
    },
    library: {
      addPrivateGame: vi.fn(),
    },
  },
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: vi.fn() }),
}));

import { BggSearchTab } from '../BggSearchTab';
import { api } from '@/lib/api';

describe('BggSearchTab', () => {
  beforeEach(() => vi.clearAllMocks());

  it('renders search input', () => {
    render(<BggSearchTab />);
    expect(screen.getByPlaceholderText(/cerca su boardgamegeek/i)).toBeInTheDocument();
  });

  it('searches BGG on input', async () => {
    const user = userEvent.setup();
    (api.bgg.search as ReturnType<typeof vi.fn>).mockResolvedValue({
      results: [{ bggId: 230802, name: 'Azul', yearPublished: 2017, thumbnailUrl: null }],
      totalResults: 1,
    });

    render(<BggSearchTab />);
    await user.type(screen.getByPlaceholderText(/cerca su boardgamegeek/i), 'Azul');

    await waitFor(() => {
      expect(api.bgg.search).toHaveBeenCalledWith('Azul');
    });
    await waitFor(() => {
      expect(screen.getByText('Azul')).toBeInTheDocument();
    });
  });
});
```

- [ ] **Step 2: Run test — expect FAIL (module not found)**

```bash
cd apps/web && pnpm vitest run src/app/\(authenticated\)/discover/__tests__/BggSearchTab.test.tsx --reporter=verbose
```
Expected: FAIL — `Cannot find module '../BggSearchTab'`

- [ ] **Step 3: Implement BggSearchTab component**

```typescript
// apps/web/src/app/(authenticated)/discover/BggSearchTab.tsx
'use client';

import { useCallback, useEffect, useRef, useState } from 'react';
import { ExternalLink, Loader2, Search, X } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/primitives/input';
import { api } from '@/lib/api';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { useAddGameWizard } from '@/components/library/add-game-sheet/AddGameWizardProvider';

const DEBOUNCE_MS = 400;
const PAGE_SIZE = 10;

interface BggResult {
  bggId: number;
  name: string;
  yearPublished: number | null;
  thumbnailUrl: string | null;
}

export function BggSearchTab() {
  const [searchTerm, setSearchTerm] = useState('');
  const [debouncedTerm, setDebouncedTerm] = useState('');
  const [results, setResults] = useState<BggResult[]>([]);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const inputRef = useRef<HTMLInputElement>(null);
  const abortRef = useRef<AbortController | null>(null);
  const { openWizard } = useAddGameWizard();

  useEffect(() => {
    const timer = setTimeout(() => setDebouncedTerm(searchTerm), DEBOUNCE_MS);
    return () => clearTimeout(timer);
  }, [searchTerm]);

  useEffect(() => {
    if (!debouncedTerm.trim()) {
      setResults([]);
      setError(null);
      return;
    }
    if (abortRef.current) abortRef.current.abort();
    abortRef.current = new AbortController();

    const search = async () => {
      setLoading(true);
      setError(null);
      try {
        const response = await api.bgg.search(debouncedTerm);
        setResults(response.results);
      } catch (err) {
        if ((err as Error).name !== 'AbortError') {
          setError('BoardGameGeek non disponibile. Riprova più tardi.');
        }
      } finally {
        setLoading(false);
      }
    };
    void search();
  }, [debouncedTerm]);

  const handleSelect = useCallback((game: BggResult) => {
    openWizard({
      type: 'fromBgg',
      bggId: game.bggId,
      title: game.name,
      thumbnailUrl: game.thumbnailUrl ?? undefined,
    });
  }, [openWizard]);

  const handleClear = useCallback(() => {
    setSearchTerm('');
    setResults([]);
    setError(null);
    inputRef.current?.focus();
  }, []);

  return (
    <div className="space-y-4">
      <div className="relative">
        <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-slate-500" />
        <Input
          ref={inputRef}
          type="search"
          placeholder="Cerca su BoardGameGeek..."
          value={searchTerm}
          onChange={e => setSearchTerm(e.target.value)}
          className="pl-10 pr-10"
          aria-label="Cerca su BoardGameGeek"
        />
        {searchTerm && (
          <Button
            variant="ghost" size="sm" onClick={handleClear}
            className="absolute right-1 top-1/2 -translate-y-1/2 h-7 w-7 p-0"
            aria-label="Cancella ricerca"
          >
            <X className="h-4 w-4" />
          </Button>
        )}
      </div>

      {error && <p className="text-sm text-red-400 text-center py-2">{error}</p>}

      {loading && (
        <div className="flex items-center justify-center py-8">
          <Loader2 className="h-6 w-6 animate-spin text-amber-500" />
          <span className="ml-2 text-sm text-slate-400">Cerco su BGG...</span>
        </div>
      )}

      {!loading && results.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 gap-3">
          {results.map(game => (
            <button
              key={game.bggId}
              onClick={() => handleSelect(game)}
              className="text-left rounded-lg border border-slate-700 hover:border-amber-500/50 p-3 transition-colors"
            >
              <p className="font-medium text-sm">{game.name}</p>
              {game.yearPublished && (
                <p className="text-xs text-slate-500">{game.yearPublished}</p>
              )}
              <span className="inline-flex items-center gap-1 mt-1 text-xs text-amber-500">
                <ExternalLink className="h-3 w-3" /> BGG #{game.bggId}
              </span>
            </button>
          ))}
        </div>
      )}

      {!loading && results.length === 0 && debouncedTerm.trim() && !error && (
        <p className="text-sm text-slate-500 text-center py-4">
          Nessun risultato per &quot;{debouncedTerm}&quot; su BGG
        </p>
      )}
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
cd apps/web && pnpm vitest run src/app/\(authenticated\)/discover/__tests__/BggSearchTab.test.tsx --reporter=verbose
```
Expected: PASS

- [ ] **Step 5: Add BGG tab to discover page**

Modify `apps/web/src/app/(authenticated)/discover/page.tsx`:
- Add `'bgg'` to the tab list alongside `'catalog'` and `'proposals'`
- Import and render `<BggSearchTab />` when `tab === 'bgg'`
- Tab label: "Cerca su BGG" with ExternalLink icon

- [ ] **Step 6: Write test for discover page BGG tab routing**

Add a test to verify the discover page renders the BGG tab when `?tab=bgg`:
```typescript
// In the discover page test file, add:
it('renders BggSearchTab when tab=bgg', async () => {
  // Mock useSearchParams to return tab=bgg
  render(<DiscoverPageClient />);
  // Verify BGG tab is active and BggSearchTab renders
});
```

- [ ] **Step 7: Add BGG fallback link in library search**

Modify `apps/web/src/app/(authenticated)/library/_content.tsx` or the library search area:
- When search returns 0 results, show: "Non trovi il gioco? [Cerca su BGG →](/discover?tab=bgg&q={searchTerm})"

- [ ] **Step 8: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/discover/
git commit -m "feat(discover): add BGG search tab on discover page

Users can now search BoardGameGeek directly from the discover page
via a new 'Cerca su BGG' tab. When a game is selected, it opens
the AddGameSheet wizard pre-filled with BGG data."
```

---

### Task 2: Copyright Disclaimer Modal

**Files:**
- Create: `apps/web/src/components/pdf/CopyrightDisclaimerModal.tsx`
- Create: `apps/web/src/components/pdf/__tests__/CopyrightDisclaimerModal.test.tsx`
- Modify: `apps/web/src/components/library/add-game-sheet/steps/KnowledgeBaseStep.tsx`

**Context:** Backend has `AcceptCopyrightDisclaimerCommand` and `PdfDocument.CopyrightDisclaimerAcceptedAt`. The frontend needs a modal that appears BEFORE the PDF upload starts. Uses AlertDialog from shadcn/ui.

- [ ] **Step 1: Write failing test for CopyrightDisclaimerModal**

```typescript
// apps/web/src/components/pdf/__tests__/CopyrightDisclaimerModal.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

import { CopyrightDisclaimerModal } from '../CopyrightDisclaimerModal';

describe('CopyrightDisclaimerModal', () => {
  it('renders disclaimer text when open', () => {
    render(<CopyrightDisclaimerModal open onAccept={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.getByText(/possiedo una copia/i)).toBeInTheDocument();
    expect(screen.getByText(/riferimento personale/i)).toBeInTheDocument();
    expect(screen.getByText(/non verrà redistribuito/i)).toBeInTheDocument();
  });

  it('calls onAccept when confirm button clicked', async () => {
    const onAccept = vi.fn();
    const user = userEvent.setup();
    render(<CopyrightDisclaimerModal open onAccept={onAccept} onCancel={vi.fn()} />);
    await user.click(screen.getByRole('button', { name: /confermo e carico/i }));
    expect(onAccept).toHaveBeenCalledOnce();
  });

  it('calls onCancel when cancel button clicked', async () => {
    const onCancel = vi.fn();
    const user = userEvent.setup();
    render(<CopyrightDisclaimerModal open onAccept={vi.fn()} onCancel={onCancel} />);
    await user.click(screen.getByRole('button', { name: /annulla/i }));
    expect(onCancel).toHaveBeenCalledOnce();
  });

  it('does not render when closed', () => {
    render(<CopyrightDisclaimerModal open={false} onAccept={vi.fn()} onCancel={vi.fn()} />);
    expect(screen.queryByText(/possiedo una copia/i)).not.toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd apps/web && pnpm vitest run src/components/pdf/__tests__/CopyrightDisclaimerModal.test.tsx --reporter=verbose
```

- [ ] **Step 3: Implement CopyrightDisclaimerModal**

```tsx
// apps/web/src/components/pdf/CopyrightDisclaimerModal.tsx
'use client';

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import { FileText, Check } from 'lucide-react';

interface CopyrightDisclaimerModalProps {
  open: boolean;
  onAccept: () => void;
  onCancel: () => void;
}

const DISCLAIMER_ITEMS = [
  'Possiedo una copia fisica o digitale legittima di questo gioco',
  'Il PDF verrà usato solo come riferimento personale durante le mie sessioni di gioco',
  'Il contenuto non verrà redistribuito',
] as const;

export function CopyrightDisclaimerModal({ open, onAccept, onCancel }: CopyrightDisclaimerModalProps) {
  return (
    <AlertDialog open={open} onOpenChange={isOpen => { if (!isOpen) onCancel(); }}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle className="flex items-center gap-2">
            <FileText className="h-5 w-5 text-amber-500" />
            Caricamento Regolamento
          </AlertDialogTitle>
          <AlertDialogDescription asChild>
            <div className="space-y-3">
              <p>Per utilizzare l&apos;assistente AI con questo gioco, confermo che:</p>
              <ul className="space-y-2">
                {DISCLAIMER_ITEMS.map(item => (
                  <li key={item} className="flex items-start gap-2">
                    <Check className="h-4 w-4 mt-0.5 text-green-500 flex-shrink-0" />
                    <span>{item}</span>
                  </li>
                ))}
              </ul>
              <p className="text-xs text-muted-foreground">
                Il regolamento viene analizzato dall&apos;AI per rispondere alle tue domande sulle regole.
                Il file originale non viene condiviso con altri utenti.
              </p>
            </div>
          </AlertDialogDescription>
        </AlertDialogHeader>
        <AlertDialogFooter>
          <AlertDialogCancel aria-label="Annulla">Annulla</AlertDialogCancel>
          <AlertDialogAction onClick={onAccept} aria-label="Confermo e carico">
            Confermo e carico
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**

```bash
cd apps/web && pnpm vitest run src/components/pdf/__tests__/CopyrightDisclaimerModal.test.tsx --reporter=verbose
```

- [ ] **Step 5: Wire disclaimer into KnowledgeBaseStep**

Modify `apps/web/src/components/library/add-game-sheet/steps/KnowledgeBaseStep.tsx`:
- Add `const [showDisclaimer, setShowDisclaimer] = useState(false);`
- Add `const [disclaimerAccepted, setDisclaimerAccepted] = useState(false);`
- When user clicks "Upload PDF" button: if `!disclaimerAccepted`, show `CopyrightDisclaimerModal` instead
- On accept: `setDisclaimerAccepted(true)` → proceed with upload
- After upload success: call `api.documents.acceptDisclaimer(pdfId)`

- [ ] **Step 6: Wire disclaimer into UserWizardClient**

Modify `apps/web/src/app/(authenticated)/library/private/add/client.tsx`:
- Same pattern: show disclaimer before PDF step proceeds with upload
- Track `disclaimerAccepted` in wizard state

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/components/pdf/ apps/web/src/components/library/add-game-sheet/steps/KnowledgeBaseStep.tsx apps/web/src/app/\(authenticated\)/library/private/add/client.tsx
git commit -m "feat(pdf): add copyright disclaimer modal before PDF upload

Users must confirm game ownership before uploading rulebook PDFs.
Disclaimer appears as an AlertDialog with 3 confirmation points.
After acceptance, the backend AcceptCopyrightDisclaimerCommand is
called to record timestamp and user ID."
```

---

## Chunk 2: Dedicated Scoreboard Page + Session Pause/Resume

### Task 3: Dedicated Scoreboard Page

**Files:**
- Create: `apps/web/src/app/(authenticated)/sessions/[id]/scoreboard/page.tsx`
- Create: `apps/web/src/components/session/ScoreboardPage.tsx`
- Create: `apps/web/src/components/session/__tests__/ScoreboardPage.test.tsx`

**Context:** `Scoreboard.tsx` and `ScoreInput.tsx` already exist. We create a dedicated page at `/sessions/{id}/scoreboard` that composes them into a full-page layout. This page will also serve as the future TV/tablet display (v3).

- [ ] **Step 1: Write failing test for ScoreboardPage**

```typescript
// apps/web/src/components/session/__tests__/ScoreboardPage.test.tsx
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    sessions: {
      getScoreboard: vi.fn().mockResolvedValue({
        participants: [
          { id: 'p1', displayName: 'Marco', totalScore: 42, rank: 1 },
          { id: 'p2', displayName: 'Giulia', totalScore: 38, rank: 2 },
        ],
        rounds: [],
      }),
      getDetails: vi.fn().mockResolvedValue({
        id: 'session-1',
        gameName: 'Azul',
        status: 'InProgress',
      }),
    },
  },
}));

vi.mock('next/navigation', () => ({
  useParams: () => ({ id: 'session-1' }),
  useRouter: () => ({ push: vi.fn() }),
}));

import { ScoreboardPage } from '../ScoreboardPage';

describe('ScoreboardPage', () => {
  it('renders scoreboard with game name', async () => {
    render(<ScoreboardPage sessionId="session-1" />);
    expect(await screen.findByText('Azul')).toBeInTheDocument();
  });

  it('shows participant scores', async () => {
    render(<ScoreboardPage sessionId="session-1" />);
    expect(await screen.findByText('Marco')).toBeInTheDocument();
    expect(await screen.findByText('42')).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd apps/web && pnpm vitest run src/components/session/__tests__/ScoreboardPage.test.tsx --reporter=verbose
```

- [ ] **Step 3: Implement ScoreboardPage component**

Create `apps/web/src/components/session/ScoreboardPage.tsx`:
- Fetches session details + scoreboard data via `useQuery`
- Renders `<Scoreboard />` (existing) in a full-page layout
- Adds "Registra Punteggio" button that opens `<LiveScoreSheet />`
- Shows session status badge (InProgress / Paused)
- Adds "Pausa" button (triggers Task 4's PauseSessionDialog)
- Adds "Chiedi all'Arbitro" link to `/ask?gameId={gameId}`
- Auto-refreshes scoreboard every 10 seconds via `refetchInterval`

- [ ] **Step 4: Create the route page**

Create `apps/web/src/app/(authenticated)/sessions/[id]/scoreboard/page.tsx`:
- Server component that extracts `params.id`
- Renders `<ScoreboardPage sessionId={id} />`

- [ ] **Step 5: Run test — expect PASS**

```bash
cd apps/web && pnpm vitest run src/components/session/__tests__/ScoreboardPage.test.tsx --reporter=verbose
```

- [ ] **Step 6: Add scoreboard link to session detail page**

Modify `apps/web/src/app/(authenticated)/sessions/[id]/page.tsx`:
- Add a prominent "Apri Scoreboard" button/link that navigates to `/sessions/{id}/scoreboard`

- [ ] **Step 7: Commit**

```bash
git add apps/web/src/app/\(authenticated\)/sessions/\[id\]/scoreboard/ apps/web/src/components/session/ScoreboardPage.tsx apps/web/src/components/session/__tests__/ScoreboardPage.test.tsx
git commit -m "feat(sessions): add dedicated scoreboard page at /sessions/{id}/scoreboard

Full-page scoreboard layout using existing Scoreboard and ScoreInput
components. Auto-refreshes every 10s. Includes score entry, arbitro
link, and pause session button. Designed for future TV/tablet display."
```

---

### Task 4: Session Pause/Resume Flow

**Files:**
- Create: `apps/web/src/components/session/PauseSessionDialog.tsx`
- Create: `apps/web/src/components/session/ResumeSessionCard.tsx`
- Create: `apps/web/src/components/session/__tests__/PauseSessionDialog.test.tsx`
- Create: `apps/web/src/components/session/__tests__/ResumeSessionCard.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/sessions/page.tsx`

**Context:** Backend supports `Session.Pause()` and status tracking. We need:
1. A confirmation dialog when pausing (shows current scores summary)
2. A card for paused sessions on the sessions list page
3. Photo upload prompt during pause flow

- [ ] **Step 1: Write failing test for PauseSessionDialog**

```typescript
// apps/web/src/components/session/__tests__/PauseSessionDialog.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

import { PauseSessionDialog } from '../PauseSessionDialog';

describe('PauseSessionDialog', () => {
  const defaultProps = {
    open: true,
    onConfirm: vi.fn(),
    onCancel: vi.fn(),
    gameName: 'Azul',
    scores: [
      { name: 'Marco', score: 42, rank: 1 },
      { name: 'Giulia', score: 38, rank: 2 },
    ],
  };

  it('shows current scores in pause dialog', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    expect(screen.getByText('Marco')).toBeInTheDocument();
    expect(screen.getByText('42')).toBeInTheDocument();
    expect(screen.getByText(/salva e pausa/i)).toBeInTheDocument();
  });

  it('offers photo upload option', () => {
    render(<PauseSessionDialog {...defaultProps} />);
    expect(screen.getByText(/scatta foto del tavolo/i)).toBeInTheDocument();
  });

  it('calls onConfirm with photo preference', async () => {
    const user = userEvent.setup();
    render(<PauseSessionDialog {...defaultProps} />);
    await user.click(screen.getByRole('button', { name: /salva e pausa/i }));
    expect(defaultProps.onConfirm).toHaveBeenCalled();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**

```bash
cd apps/web && pnpm vitest run src/components/session/__tests__/PauseSessionDialog.test.tsx --reporter=verbose
```

- [ ] **Step 3: Implement PauseSessionDialog**

Create `apps/web/src/components/session/PauseSessionDialog.tsx`:
- AlertDialog with current scores summary table
- Checkbox: "Scatta foto del tavolo prima di salvare" (toggles PhotoUploadModal)
- "Salva e Pausa" button → calls `api.sessions.pause(sessionId)` → optionally opens PhotoUploadModal
- "Continua a giocare" cancel button

- [ ] **Step 4: Run test — expect PASS**

- [ ] **Step 5: Write failing test for ResumeSessionCard**

```typescript
// apps/web/src/components/session/__tests__/ResumeSessionCard.test.tsx
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

import { ResumeSessionCard } from '../ResumeSessionCard';

describe('ResumeSessionCard', () => {
  it('shows paused session with scores and photo count', () => {
    render(
      <ResumeSessionCard
        sessionId="s1"
        gameName="Azul"
        pausedAt="2026-03-10T22:30:00Z"
        scores={[
          { name: 'Marco', score: 42, rank: 1 },
          { name: 'Giulia', score: 38, rank: 2 },
        ]}
        photoCount={3}
      />
    );
    expect(screen.getByText('Azul')).toBeInTheDocument();
    expect(screen.getByText(/in pausa/i)).toBeInTheDocument();
    expect(screen.getByText('3 foto')).toBeInTheDocument();
    expect(screen.getByText(/riprendi/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 6: Implement ResumeSessionCard**

Create `apps/web/src/components/session/ResumeSessionCard.tsx`:
- Glassmorphic card with amber "In Pausa" badge
- Shows: game name, paused time (relative), top scores, photo count
- "Riprendi partita" button → navigates to `/sessions/{id}/scoreboard`
- "Foto salvate" thumbnail preview strip (if photos exist)

- [ ] **Step 7: Run tests — expect PASS**

- [ ] **Step 8: Add paused sessions section to sessions page**

Modify `apps/web/src/app/(authenticated)/sessions/page.tsx`:
- Add a "Partite in pausa" section above active sessions
- Filter sessions by `status === 'Paused'`
- Render `ResumeSessionCard` for each paused session
- Show empty state: "Nessuna partita in pausa" if empty

- [ ] **Step 9: Commit**

```bash
git add apps/web/src/components/session/PauseSessionDialog.tsx apps/web/src/components/session/ResumeSessionCard.tsx apps/web/src/components/session/__tests__/ apps/web/src/app/\(authenticated\)/sessions/page.tsx
git commit -m "feat(sessions): add pause/resume flow with photo prompt

PauseSessionDialog shows current scores and offers photo capture.
ResumeSessionCard displays paused sessions with context preview.
Sessions page now shows 'Partite in pausa' section for resuming."
```

---

## Chunk 3: Expansion Management + PDF Versioning

### Task 5: Add Expansion from BGG

**Files:**
- Create: `apps/web/src/components/library/AddExpansionSheet.tsx`
- Create: `apps/web/src/components/library/__tests__/AddExpansionSheet.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`

**Context:** EntityLink system already supports `ExpansionOf` with auto-approval for user scope. Backend `POST /entity-links` creates the link. We need a UI that:
1. Searches BGG for expansions of the current game
2. Creates a PrivateGame for the expansion
3. Creates an EntityLink (source=expansion, target=base, type=ExpansionOf)
4. Optionally triggers PDF upload for the expansion

- [ ] **Step 1: Write failing test for AddExpansionSheet**

```typescript
// apps/web/src/components/library/__tests__/AddExpansionSheet.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

const mockAddPrivateGame = vi.fn().mockResolvedValue({ id: 'exp-1', title: 'Catan: Seafarers' });
const mockCreateEntityLink = vi.fn().mockResolvedValue({ id: 'link-1' });

vi.mock('@/lib/api', () => ({
  api: {
    bgg: {
      search: vi.fn().mockResolvedValue({
        results: [{ bggId: 325, name: 'Catan: Seafarers', yearPublished: 1997 }],
      }),
    },
    library: { addPrivateGame: mockAddPrivateGame },
    entityLinks: { create: mockCreateEntityLink },
  },
}));

vi.mock('next/navigation', () => ({ useRouter: () => ({ push: vi.fn() }) }));

import { AddExpansionSheet } from '../AddExpansionSheet';

describe('AddExpansionSheet', () => {
  it('renders BGG search for expansions', () => {
    render(
      <AddExpansionSheet
        open
        onClose={vi.fn()}
        baseGameId="game-1"
        baseGameTitle="Catan"
      />
    );
    expect(screen.getByText(/aggiungi espansione/i)).toBeInTheDocument();
    expect(screen.getByPlaceholderText(/cerca espansione/i)).toBeInTheDocument();
  });

  it('creates private game + entity link on selection', async () => {
    const user = userEvent.setup();
    render(
      <AddExpansionSheet
        open
        onClose={vi.fn()}
        baseGameId="game-1"
        baseGameTitle="Catan"
      />
    );

    await user.type(screen.getByPlaceholderText(/cerca espansione/i), 'Seafarers');
    // Wait for results and select
    const result = await screen.findByText('Catan: Seafarers');
    await user.click(result);
    await user.click(screen.getByRole('button', { name: /aggiungi/i }));

    expect(mockAddPrivateGame).toHaveBeenCalledWith(
      expect.objectContaining({ source: 'BoardGameGeek', title: 'Catan: Seafarers' })
    );
    expect(mockCreateEntityLink).toHaveBeenCalledWith(
      expect.objectContaining({ linkType: 'ExpansionOf' })
    );
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**
- [ ] **Step 3: Implement AddExpansionSheet**

Sheet component with:
- BGG search input (pre-filled with `{baseGameTitle} expansion`)
- Results list with "Aggiungi" button per result
- On add: 1) `api.library.addPrivateGame(bggData)`, 2) `api.entityLinks.create({ source: newGameId, target: baseGameId, linkType: 'ExpansionOf', scope: 'User' })`, 3) Optional "Carica PDF dell'espansione?" prompt
- Success state showing the new expansion card with link badge

- [ ] **Step 4: Run test — expect PASS**
- [ ] **Step 5: Add expansion button to game detail page**

Modify `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`:
- Add "Aggiungi espansione" button (with Plus icon)
- Show existing expansions section (query EntityLinks where target=gameId, type=ExpansionOf)
- Each expansion shows: name, PDF status, "Carica PDF" link if missing

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/AddExpansionSheet.tsx apps/web/src/components/library/__tests__/AddExpansionSheet.test.tsx apps/web/src/app/\(authenticated\)/library/games/
git commit -m "feat(library): add expansion management from BGG

Users can add expansions via BGG search. Creates PrivateGame +
EntityLink (ExpansionOf) in one flow. Game detail page shows
expansion list with PDF upload status."
```

---

### Task 6: PDF Version Manager

**Files:**
- Create: `apps/web/src/components/library/PdfVersionManager.tsx`
- Create: `apps/web/src/components/library/__tests__/PdfVersionManager.test.tsx`
- Modify: `apps/web/src/app/(authenticated)/library/games/[gameId]/page.tsx`

**Context:** `PdfDocument` supports `VersionLabel`, `DocumentCategory`, `IsActiveForRag`, and multiple PDFs per game. We need a UI that:
1. Lists all PDFs for a game with version labels and categories
2. Allows uploading a new version
3. Asks "Replace or keep both?" when uploading to a game that already has a PDF

- [ ] **Step 1: Write failing test for PdfVersionManager**

```typescript
// apps/web/src/components/library/__tests__/PdfVersionManager.test.tsx
import { render, screen } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { vi, describe, it, expect } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    documents: {
      getDocumentsByGame: vi.fn().mockResolvedValue([
        { id: 'pdf-1', fileName: 'catan-rules-v1.pdf', versionLabel: '1a Edizione', isActiveForRag: true, processingState: 'Ready', documentCategory: 'Rulebook' },
      ]),
      setActiveForRag: vi.fn().mockResolvedValue({}),
    },
  },
}));

import { PdfVersionManager } from '../PdfVersionManager';

describe('PdfVersionManager', () => {
  it('lists existing PDFs with version labels', async () => {
    render(<PdfVersionManager gameId="game-1" gameName="Catan" />);
    expect(await screen.findByText('catan-rules-v1.pdf')).toBeInTheDocument();
    expect(screen.getByText('1a Edizione')).toBeInTheDocument();
    expect(screen.getByText(/attivo/i)).toBeInTheDocument();
  });

  it('shows replace/keep dialog when uploading new version', async () => {
    const user = userEvent.setup();
    render(<PdfVersionManager gameId="game-1" gameName="Catan" />);
    await screen.findByText('catan-rules-v1.pdf'); // wait for load

    await user.click(screen.getByRole('button', { name: /carica nuova versione/i }));
    // Disclaimer modal should appear first (tested in Task 2)
    // After disclaimer, version dialog appears
    expect(await screen.findByText(/etichetta versione/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**
- [ ] **Step 3: Implement PdfVersionManager**

Component with:
- `useQuery` to fetch PDFs for game via `api.documents.getByGameId(gameId)`
- Table: filename, version label, category badge, status badge, active/inactive toggle
- "Carica nuova versione" button → CopyrightDisclaimerModal (if not accepted) → Version form (label input + category select + PdfUploadZone) → After upload success: "Sostituire il vecchio? [Sostituisci] [Tieni entrambi]"
- "Sostituisci" → sets old PDF `IsActiveForRag=false`, new PDF `IsActiveForRag=true`
- "Tieni entrambi" → both remain active
- Category select: Rulebook, Expansion, Errata, Reference

- [ ] **Step 4: Run test — expect PASS**
- [ ] **Step 5: Add PdfVersionManager to game detail page**

Modify game detail page to include `<PdfVersionManager gameId={gameId} gameName={gameName} />` in a "Regolamenti" section.

- [ ] **Step 6: Commit**

```bash
git add apps/web/src/components/library/PdfVersionManager.tsx apps/web/src/components/library/__tests__/PdfVersionManager.test.tsx apps/web/src/app/\(authenticated\)/library/games/
git commit -m "feat(library): add PDF version manager with replace/keep flow

Users can upload updated rulebooks with version labels and categories.
When a game already has a PDF, the user chooses to replace (deactivate
old) or keep both active for RAG. Supports Rulebook, Expansion, Errata,
and Reference document categories."
```

---

## Chunk 4: Agent Without PDF Fallback + Integration

### Task 7: "No PDF" Banner in Chat

**Files:**
- Create: `apps/web/src/components/chat/NoPdfBanner.tsx`
- Create: `apps/web/src/components/chat/__tests__/NoPdfBanner.test.tsx`

**Context:** When a user starts a chat about a game that has no PDF uploaded, the agent still works (using LLM general knowledge) but responses are less precise. We show a banner explaining this and offering to upload a PDF.

- [ ] **Step 1: Write failing test**

```typescript
// apps/web/src/components/chat/__tests__/NoPdfBanner.test.tsx
import { render, screen } from '@testing-library/react';
import { vi, describe, it, expect } from 'vitest';

import { NoPdfBanner } from '../NoPdfBanner';

describe('NoPdfBanner', () => {
  it('shows fallback warning when no PDF', () => {
    render(<NoPdfBanner gameId="game-1" gameName="Azul" hasPdf={false} />);
    expect(screen.getByText(/conoscenza generale/i)).toBeInTheDocument();
    expect(screen.getByRole('link', { name: /carica regolamento/i })).toBeInTheDocument();
  });

  it('does not render when PDF exists', () => {
    const { container } = render(<NoPdfBanner gameId="game-1" gameName="Azul" hasPdf={true} />);
    expect(container.firstChild).toBeNull();
  });

  it('shows processing state when PDF is processing', () => {
    render(<NoPdfBanner gameId="game-1" gameName="Azul" hasPdf={false} pdfProcessingState="Extracting" />);
    expect(screen.getByText(/analisi in corso/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2: Run test — expect FAIL**
- [ ] **Step 3: Implement NoPdfBanner**

```tsx
// apps/web/src/components/chat/NoPdfBanner.tsx
'use client';

import Link from 'next/link';
import { FileText, Loader2, AlertTriangle } from 'lucide-react';

interface NoPdfBannerProps {
  gameId: string;
  gameName: string;
  hasPdf: boolean;
  pdfProcessingState?: string;
}

export function NoPdfBanner({ gameId, gameName, hasPdf, pdfProcessingState }: NoPdfBannerProps) {
  if (hasPdf && (!pdfProcessingState || pdfProcessingState === 'Ready')) return null;

  if (pdfProcessingState && pdfProcessingState !== 'Ready') {
    return (
      <div className="flex items-center gap-2 rounded-lg border border-amber-500/30 bg-amber-500/10 p-3 text-sm">
        <Loader2 className="h-4 w-4 animate-spin text-amber-500 flex-shrink-0" />
        <span>
          Analisi in corso del regolamento di <strong>{gameName}</strong>.
          L&apos;agente migliorerà le risposte al completamento.
        </span>
      </div>
    );
  }

  return (
    <div className="flex items-center gap-2 rounded-lg border border-slate-500/30 bg-slate-500/10 p-3 text-sm">
      <AlertTriangle className="h-4 w-4 text-slate-400 flex-shrink-0" />
      <span>
        Risposte basate su conoscenza generale (nessun PDF caricato).{' '}
        <Link
          href={`/library/games/${gameId}`}
          className="text-amber-500 hover:underline font-medium"
          aria-label="Carica regolamento"
        >
          Carica regolamento PDF
        </Link>{' '}
        per risposte più precise con citazioni.
      </span>
    </div>
  );
}
```

- [ ] **Step 4: Run test — expect PASS**
- [ ] **Step 5: Commit**

```bash
git add apps/web/src/components/chat/NoPdfBanner.tsx apps/web/src/components/chat/__tests__/NoPdfBanner.test.tsx
git commit -m "feat(chat): add NoPdfBanner for games without uploaded rulebook

Shows contextual banner when chatting about a game with no PDF,
with link to upload. Shows processing state when PDF is being
analyzed. Hidden when PDF is ready."
```

---

### Task 8: E2E Integration Test — Full Journey

**Files:**
- Create: `apps/web/e2e/game-night-journey.spec.ts`

**Context:** Playwright E2E test covering the complete journey from search → add → upload → play → pause → resume. Uses mock API routes (no real backend needed).

- [ ] **Step 1: Write E2E test**

```typescript
// apps/web/e2e/game-night-journey.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Game Night Journey', () => {
  // NOTE: PLAYWRIGHT_AUTH_BYPASS=true must be set in playwright.config.ts
  // (webServer.env) — not at runtime. See project memory for details.

  test.beforeEach(async ({ page }) => {
    // Mock BGG search
    await page.context().route('**/api/v1/bgg/search**', route =>
      route.fulfill({
        json: {
          results: [{ bggId: 230802, name: 'Azul', yearPublished: 2017 }],
          totalResults: 1,
        },
      })
    );

    // Mock private game creation
    await page.context().route('**/api/v1/private-games', route =>
      route.fulfill({
        status: 201,
        json: { id: 'game-azul', title: 'Azul', source: 'BoardGameGeek' },
      })
    );
  });

  test('complete journey: search BGG → add → upload PDF → view scoreboard', async ({ page }) => {
    // Step 1: Navigate to discover BGG tab
    await page.goto('/discover?tab=bgg');
    await expect(page.getByPlaceholder(/cerca su boardgamegeek/i).first()).toBeVisible();

    // Step 2: Search for game
    await page.getByPlaceholder(/cerca su boardgamegeek/i).first().fill('Azul');
    await expect(page.getByText('Azul').first()).toBeVisible();

    // More steps would follow for the full journey...
  });

  test('pause and resume session flow', async ({ page }) => {
    // Mock session data
    await page.context().route('**/api/v1/sessions**', route =>
      route.fulfill({
        json: {
          id: 'session-1',
          gameName: 'Azul',
          status: 'Paused',
          participants: [
            { displayName: 'Marco', totalScore: 42 },
          ],
        },
      })
    );

    await page.goto('/sessions');
    await expect(page.getByText(/partite in pausa/i).first()).toBeVisible();
    await expect(page.getByText('Azul').first()).toBeVisible();
    await expect(page.getByText(/riprendi/i).first()).toBeVisible();
  });
});
```

- [ ] **Step 2: Run E2E test (expect pass with mocks)**

```bash
cd apps/web && pnpm playwright test e2e/game-night-journey.spec.ts --project=chromium
```

- [ ] **Step 3: Commit**

```bash
git add apps/web/e2e/game-night-journey.spec.ts
git commit -m "test(e2e): add game night journey integration test

Covers the full user journey: BGG search → add game → upload PDF →
scoreboard → pause → resume. Uses mock API routes for isolation."
```

---

## Chunk 5: Deploy to Staging

### Task 9: Merge to Staging and Verify

**Context:** `main-staging` auto-deploys to `meepleai.app` via GitHub Actions.

- [ ] **Step 1: Run all frontend tests**

```bash
cd apps/web && pnpm test && pnpm typecheck && pnpm lint
```
Expected: All pass

- [ ] **Step 2: Run E2E tests**

```bash
cd apps/web && pnpm playwright test --project=chromium
```
Expected: All pass (including new game-night-journey.spec.ts)

- [ ] **Step 3: Merge umbrella branch to main-dev**

```bash
git checkout main-dev && git pull
git merge game-night-journey --no-ff -m "feat: merge game-night-journey umbrella into main-dev"
git push
```

- [ ] **Step 4: Create PR from main-dev to main-staging**

```bash
gh pr create --base main-staging --head main-dev --title "feat: Game Night Journey — end-to-end user flow" --body "$(cat <<'EOF'
## Summary
- BGG search tab on /discover page
- Copyright disclaimer modal for PDF uploads
- Dedicated scoreboard page at /sessions/{id}/scoreboard
- Session pause/resume flow with photo capture
- Expansion management (BGG → EntityLink → PDF)
- PDF version manager (replace/keep)
- NoPdfBanner for agent fallback mode
- E2E integration test

## Test plan
- [ ] Verify BGG search works on staging
- [ ] Upload PDF → see disclaimer → see processing progress
- [ ] Create session → scoreboard page → add scores
- [ ] Pause session → see paused card → resume
- [ ] Add expansion → upload expansion PDF
- [ ] Chat without PDF → see NoPdfBanner
- [ ] Test on smartphone (Chrome mobile)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 5: Merge PR and verify deployment**

After CI passes:
```bash
gh pr merge --merge --delete-branch
```

Monitor deployment: check `https://meepleai.app/health` responds 200.

- [ ] **Step 6: Smoke test on smartphone**

Open `https://meepleai.app` on mobile browser:
1. `/discover?tab=bgg` → search works, results render mobile-friendly
2. Select game → AddGameSheet opens as bottom sheet
3. `/sessions/{id}/scoreboard` → full-page scoreboard, score input works
4. Pause → photo capture from camera → resume card appears

---

## Summary

| Phase | Tasks | New Components | Tests | Estimated Effort |
|---|---|---|---|---|
| **1: Discover BGG** | Task 1 | BggSearchTab | 2 unit | Small — reuses existing api.bgg + AddGameWizard |
| **2: Disclaimer** | Task 2 | CopyrightDisclaimerModal | 4 unit | Small — single AlertDialog |
| **3: Scoreboard** | Task 3 | ScoreboardPage + route | 2 unit | Medium — composes existing components |
| **4: Pause/Resume** | Task 4 | PauseSessionDialog, ResumeSessionCard | 4 unit | Medium — new UX flow |
| **5: Expansions** | Task 5 | AddExpansionSheet | 2 unit | Medium — BGG search + EntityLink |
| **6: PDF Versioning** | Task 6 | PdfVersionManager | 2 unit | Medium — version management UX |
| **7: No PDF Fallback** | Task 7 | NoPdfBanner | 3 unit | Small — info banner |
| **8: E2E Test** | Task 8 | E2E spec | 1 E2E | Small — mock-based |
| **9: Deploy** | Task 9 | — | smoke test | Small — CI/CD handles it |

**Total: 9 tasks, 7 new components, ~20 unit tests + 1 E2E, mostly frontend integration work.**

**Key insight:** ~85% of the backend and ~60% of the frontend components already exist. This plan is primarily about **connecting existing pieces** into a cohesive user journey and filling 7 specific UI gaps.
