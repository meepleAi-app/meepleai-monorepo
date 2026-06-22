# Citation PDF Viewer (G4 v3) — Implementation Plan

**Status**: ✅ COMPLETED (PR #926)
> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Estendere il `CitationModal` (PR #918) con tab `Snippet | PDF originale`, ownership gate via `Citation.isPublic` + `getGameDocuments` fallback, embed `react-pdf` viewer per owner / upsell card per non-owner, sblocco di `onOpenInKb` in `GameChatTabV2`.

**Architecture:** Frontend-only PR su `apps/web`. 3 nuovi componenti + 1 hook + modifiche a 2 file esistenti. Riusa `react-pdf` + `pdfjs-dist` + logica `PdfViewerModal.tsx` esistente. Backend zero modifiche. Pattern Wave B.1 puro/orchestrator + TDD strict.

**Tech Stack:** React 19 + Next.js 16 App Router, TypeScript strict, Tailwind 4 (token v2 `--c-kb` teal + `--c-warning` + `--c-success` + `--c-chat`), Vitest, `react-pdf` v10, `pdfjs-dist` v5, `@radix-ui/react-tabs` (via shadcn `Tabs` primitive).

**Spec:** [`docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md`](../specs/2026-05-10-citation-pdf-viewer-design.md)
**Mockup:** [`admin-mockups/design_files/sp4-citation-pdf-viewer.html`](../../../admin-mockups/design_files/sp4-citation-pdf-viewer.html)
**Issue:** [#921](https://github.com/meepleAi-app/meepleai-monorepo/issues/921) (riformulato)
**Branch:** `feature/issue-921-kb-detail-page` (già creato dai commit spec + mockup)

---

## File Structure

| Status | Path | Responsibility |
|---|---|---|
| Create | `apps/web/src/hooks/queries/useCanViewPdf.ts` | Hook ownership check: `isPublic` shortcut + `getGameDocuments(gameId).some(d => d.id === documentId)` fallback |
| Create | `apps/web/src/hooks/queries/__tests__/useCanViewPdf.test.tsx` | Unit test hook (5 scenari) |
| Create | `apps/web/src/components/v2/game-chat/CitationOwnershipUpsell.tsx` | Pure: upsell card "Carica il mio PDF" → CTA `/upload?gameId=X` |
| Create | `apps/web/src/components/v2/game-chat/__tests__/CitationOwnershipUpsell.test.tsx` | Unit test (4 scenari) |
| Create | `apps/web/src/components/v2/game-chat/CitationPdfTab.tsx` | Body del tab PDF: gating + viewer embed o upsell |
| Create | `apps/web/src/components/v2/game-chat/__tests__/CitationPdfTab.test.tsx` | Integration test (5 scenari con MSW + react-pdf mock) |
| Modify | `apps/web/src/components/v2/game-chat/CitationModal.tsx` | Aggiungi tab system; rimuovi `onOpenInKb`; aggiungi `gameId` prop; lazy mount tab PDF |
| Modify | `apps/web/src/components/v2/game-chat/__tests__/CitationModal.test.tsx` | Rimuovi 2 test `onOpenInKb`, aggiungi 4 test tab + ownership |
| Modify | `apps/web/src/components/v2/game-chat/index.ts` | Aggiungi export `CitationPdfTab`, `CitationOwnershipUpsell`, `useCanViewPdf` (rispettivamente da components+hooks) |
| Modify | `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx` | Sostituisci `onOpenInKb={undefined}` con `gameId={gameId}` |
| Modify | `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx` | Update test "citation chip click opens CitationModal" — rimuovi check su KB button assenza |
| Modify | `docs/for-developers/frontend/v2-migration-matrix.md` | Riga `/kb/[id]` → status `deferred` con link a spec G4 v3 |
| Add comment | I 6 stub `apps/web/src/components/v2/kb-detail/*.tsx` | Header comment "DEFERRED — vedi spec G4 v3" |

**Decomposition logic:**
- 4 task implementativi (hook + 2 componenti + integration tab) + 2 task wiring (CitationModal + GameChatTabV2) + 1 task cleanup (matrix + stub comments) + 1 task PR
- Pattern strict puro/controllato/orchestrator. NO refactoring di `PdfViewerModal.tsx` esistente — copy-paste accettabile dei ~40 LOC `Document+Page` body.

---

## Pre-flight

- [ ] **Step 0.1: Verifica branch corrente**

```bash
git -C "D:/Repositories/meepleai-monorepo-main" branch --show-current
```
Expected: `feature/issue-921-kb-detail-page`

- [ ] **Step 0.2: Verifica clean typecheck baseline**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori. Se errori preesistenti, registrarli come baseline.

- [ ] **Step 0.3: Verifica `react-pdf` + `pdfjs-dist` nel bundle**

```bash
grep -E "react-pdf|pdfjs-dist" apps/web/package.json
```
Expected output:
```
"pdfjs-dist": "5.5.207",
"react-pdf": "^10.4.1",
```

Se mancano: STOP. Aggiungere prima al package.json (`pnpm add react-pdf pdfjs-dist`).

- [ ] **Step 0.4: Verifica `Tabs` primitive disponibile**

```bash
ls apps/web/src/components/ui/navigation/tabs.tsx
```
Expected: file esiste (shadcn `Tabs` primitive).

Se NO: usa pattern HTML manuale con `aria-selected` (vedi mockup `sp4-citation-pdf-viewer.html` linea 178-192) — accettabile fallback.

---

## Task 1: Hook `useCanViewPdf`

**Files:**
- Create: `apps/web/src/hooks/queries/useCanViewPdf.ts`
- Test: `apps/web/src/hooks/queries/__tests__/useCanViewPdf.test.tsx`

**Contract:**
```ts
interface UseCanViewPdfArgs {
  readonly documentId: string;
  readonly gameId?: string;
  readonly enabled?: boolean;  // default true
}

interface UseCanViewPdfResult {
  readonly canView: boolean;
  readonly isLoading: boolean;
  readonly isError: boolean;
}

export function useCanViewPdf(args: UseCanViewPdfArgs): UseCanViewPdfResult;
```

**Algoritmo:**
1. Se `!enabled` → `{ canView: false, isLoading: false, isError: false }` (NO fetch)
2. Se `!gameId` → `{ canView: false, isLoading: false, isError: false }` (impossibile decidere)
3. Altrimenti `useQuery(['game-documents', gameId], () => api.knowledgeBase.getGameDocuments(gameId), { enabled, staleTime: 5*60_000 })`
4. `canView = data?.some(d => d.id === documentId) ?? false`

> Nota: il check `Citation.isPublic` è gestito a monte da `CitationPdfTab` (shortcut prima di chiamare l'hook). L'hook si occupa solo del fallback per non-public.

- [ ] **Step 1.1: Test failing**

Crea `apps/web/src/hooks/queries/__tests__/useCanViewPdf.test.tsx`:

```tsx
/**
 * useCanViewPdf — unit tests
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.4
 */
import { renderHook, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getGameDocuments: vi.fn(),
    },
  },
}));

import { api } from '@/lib/api';
import { useCanViewPdf } from '../useCanViewPdf';

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

const sampleDocument = {
  id: 'pdf-wingspan-123',
  title: 'Wingspan Manuale base',
  status: 'indexed' as const,
  pageCount: 24,
  createdAt: '2026-03-08T10:00:00Z',
  category: 'Rulebook' as const,
  versionLabel: 'v1.2',
};

describe('useCanViewPdf', () => {
  beforeEach(() => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockReset();
  });

  it('returns canView=false without fetching when enabled=false', () => {
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-1', gameId: 'game-1', enabled: false }),
      { wrapper }
    );
    expect(result.current.canView).toBe(false);
    expect(result.current.isLoading).toBe(false);
    expect(api.knowledgeBase.getGameDocuments).not.toHaveBeenCalled();
  });

  it('returns canView=false without fetching when gameId missing', () => {
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-1' }),
      { wrapper }
    );
    expect(result.current.canView).toBe(false);
    expect(api.knowledgeBase.getGameDocuments).not.toHaveBeenCalled();
  });

  it('returns canView=true when documentId is in user documents', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([sampleDocument]);
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-wingspan-123', gameId: 'game-wingspan' }),
      { wrapper }
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.canView).toBe(true);
  });

  it('returns canView=false when documentId is NOT in user documents', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([sampleDocument]);
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-OTHER-456', gameId: 'game-wingspan' }),
      { wrapper }
    );
    await waitFor(() => expect(result.current.isLoading).toBe(false));
    expect(result.current.canView).toBe(false);
  });

  it('returns isError=true when API rejects', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockRejectedValueOnce(new Error('500'));
    const { result } = renderHook(
      () => useCanViewPdf({ documentId: 'pdf-1', gameId: 'game-1' }),
      { wrapper }
    );
    await waitFor(() => expect(result.current.isError).toBe(true));
    expect(result.current.canView).toBe(false);
  });
});
```

- [ ] **Step 1.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useCanViewPdf.test
```
Expected: FAIL — module `../useCanViewPdf` not found.

- [ ] **Step 1.3: Implementa l'hook**

Crea `apps/web/src/hooks/queries/useCanViewPdf.ts`:

```ts
/**
 * useCanViewPdf — verifica se l'utente corrente può visualizzare il PDF
 * di un documentId specifico (G4 v3 ownership gate).
 *
 * Logica:
 *   1. Se !enabled → no fetch, canView=false
 *   2. Se !gameId → impossibile decidere → canView=false (safe default)
 *   3. Altrimenti fetch getGameDocuments(gameId) e verifica se documentId è nella lista
 *
 * NB: Citation.isPublic è gestito a monte (shortcut in CitationPdfTab); questo
 * hook si attiva SOLO per il fallback per documenti non-public.
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.4
 */
import { useQuery } from '@tanstack/react-query';

import { api } from '@/lib/api';

export interface UseCanViewPdfArgs {
  readonly documentId: string;
  readonly gameId?: string;
  readonly enabled?: boolean;
}

export interface UseCanViewPdfResult {
  readonly canView: boolean;
  readonly isLoading: boolean;
  readonly isError: boolean;
}

const DOCUMENTS_STALE_TIME_MS = 5 * 60 * 1000;

export function useCanViewPdf({
  documentId,
  gameId,
  enabled = true,
}: UseCanViewPdfArgs): UseCanViewPdfResult {
  const queryEnabled = enabled && !!gameId;

  const query = useQuery({
    queryKey: ['game-documents', gameId],
    queryFn: () => api.knowledgeBase.getGameDocuments(gameId!),
    enabled: queryEnabled,
    staleTime: DOCUMENTS_STALE_TIME_MS,
  });

  if (!queryEnabled) {
    return { canView: false, isLoading: false, isError: false };
  }

  return {
    canView: query.data?.some(d => d.id === documentId) ?? false,
    isLoading: query.isLoading,
    isError: query.isError,
  };
}
```

- [ ] **Step 1.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/hooks/queries/__tests__/useCanViewPdf.test
```
Expected: PASS (5/5).

- [ ] **Step 1.5: Commit**

```bash
git add apps/web/src/hooks/queries/useCanViewPdf.ts apps/web/src/hooks/queries/__tests__/useCanViewPdf.test.tsx
git commit -m "feat(web): #921 useCanViewPdf hook (G4 ownership check)"
```

---

## Task 2: `CitationOwnershipUpsell` (pure component)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/CitationOwnershipUpsell.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/CitationOwnershipUpsell.test.tsx`

**Contract:**
```ts
interface CitationOwnershipUpsellProps {
  readonly gameId?: string;
  readonly className?: string;
}
```

Renderizza upsell card con icona, titolo, testo, CTA verso `/upload?gameId=X` (fallback `/upload` se gameId undefined), footnote.

- [ ] **Step 2.1: Test failing**

Crea `apps/web/src/components/v2/game-chat/__tests__/CitationOwnershipUpsell.test.tsx`:

```tsx
/**
 * CitationOwnershipUpsell — pure component tests
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.3
 */
import { render, screen } from '@testing-library/react';
import { describe, it, expect } from 'vitest';

import { CitationOwnershipUpsell } from '../CitationOwnershipUpsell';

describe('CitationOwnershipUpsell', () => {
  it('renders title and copyright message', () => {
    render(<CitationOwnershipUpsell />);
    expect(screen.getByText(/PDF originale protetto da copyright/i)).toBeInTheDocument();
    expect(screen.getByText(/carica.*tua copia/i)).toBeInTheDocument();
  });

  it('renders CTA with gameId in href when provided', () => {
    render(<CitationOwnershipUpsell gameId="wingspan" />);
    const cta = screen.getByRole('link', { name: /carica.*pdf/i });
    expect(cta).toHaveAttribute('href', '/upload?gameId=wingspan');
  });

  it('renders CTA without gameId param when missing', () => {
    render(<CitationOwnershipUpsell />);
    const cta = screen.getByRole('link', { name: /carica.*pdf/i });
    expect(cta).toHaveAttribute('href', '/upload');
  });

  it('renders anti-duplication footnote', () => {
    render(<CitationOwnershipUpsell />);
    expect(screen.getByText(/anti-duplicazione|hash identico/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 2.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationOwnershipUpsell.test
```
Expected: FAIL — `CitationOwnershipUpsell` not found.

- [ ] **Step 2.3: Implementa**

Crea `apps/web/src/components/v2/game-chat/CitationOwnershipUpsell.tsx`:

```tsx
/**
 * CitationOwnershipUpsell — card visualizzata nel tab PDF del CitationModal
 * quando l'utente NON possiede il PDF citato (non l'ha caricato).
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.3
 * Mockup: admin-mockups/design_files/sp4-citation-pdf-viewer.html (stato 3)
 */
import type { ReactElement } from 'react';

import clsx from 'clsx';

export interface CitationOwnershipUpsellProps {
  readonly gameId?: string;
  readonly className?: string;
}

export function CitationOwnershipUpsell({
  gameId,
  className,
}: CitationOwnershipUpsellProps): ReactElement {
  const uploadHref = gameId ? `/upload?gameId=${encodeURIComponent(gameId)}` : '/upload';
  return (
    <div
      data-slot="citation-ownership-upsell"
      className={clsx(
        'flex flex-col items-center gap-3 rounded-lg border-2 border-dashed p-6 text-center',
        'border-[hsl(var(--c-warning)/0.5)] bg-[hsl(var(--c-warning)/0.04)]',
        className
      )}
    >
      <span aria-hidden="true" className="text-5xl leading-none">
        🔒
      </span>
      <h4 className="text-lg font-bold text-[hsl(var(--c-warning))]">
        PDF originale protetto da copyright
      </h4>
      <p className="max-w-md text-sm leading-relaxed text-muted-foreground">
        Per visualizzare la pagina del manuale, devi caricare la <strong>tua copia personale</strong>{' '}
        del PDF che possiedi.
      </p>
      <a
        href={uploadHref}
        className={clsx(
          'mt-2 inline-flex items-center gap-1.5 rounded-full px-5 py-3',
          'bg-[hsl(var(--c-chat))] text-white text-base font-bold',
          'hover:opacity-90 transition-opacity',
          'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-[hsl(var(--c-chat))]'
        )}
      >
        📤 Carica il mio PDF
      </a>
      <p className="mt-3 max-w-md font-mono text-[10px] text-muted-foreground">
        ⚙️ <strong>Anti-duplicazione</strong>: l'app rifiuta upload di PDF già indicizzati con hash
        identico — non sprecherai banda se è lo stesso file di un altro utente.
      </p>
    </div>
  );
}
```

- [ ] **Step 2.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationOwnershipUpsell.test
```
Expected: PASS (4/4).

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/CitationOwnershipUpsell.tsx apps/web/src/components/v2/game-chat/__tests__/CitationOwnershipUpsell.test.tsx
git commit -m "feat(web): #921 CitationOwnershipUpsell card (G4 non-owner state)"
```

---

## Task 3: `CitationPdfTab` (integration component)

**Files:**
- Create: `apps/web/src/components/v2/game-chat/CitationPdfTab.tsx`
- Test: `apps/web/src/components/v2/game-chat/__tests__/CitationPdfTab.test.tsx`

**Contract:**
```ts
interface CitationPdfTabProps {
  readonly documentId: string;
  readonly gameId?: string;
  readonly initialPage: number;
  readonly isPublic?: boolean;
}
```

**Logica FSM:**
1. `isPublic === true` → render PDF viewer immediato (NO ownership check)
2. Altrimenti `useCanViewPdf({ documentId, gameId })`:
   - `isLoading` → spinner
   - `canView: true` → PDF viewer
   - `canView: false` → `<CitationOwnershipUpsell gameId={gameId} />`
   - `isError` → upsell come fallback safe (mai mostrare PDF se incerto)
3. PDF viewer: usa `react-pdf` `Document + Page` con `pdfClient.getPdfDownloadUrl(documentId)` come URL, fetch via blob (vedi `PdfViewerModal.tsx:50-80` come reference). Toolbar prev/next + page info. NO download button.

**Riuso da `PdfViewerModal.tsx`** (apps/web/src/components/pdf/PdfViewerModal.tsx):
- Linee 11-20: import `Document, Page, pdfjs` + workerSrc setup
- Linee 50-80: fetch blob con `AbortController`
- Pattern Document loading + Page render

> ⚠️ NON modifichiamo `PdfViewerModal.tsx`. Copy-paste accettabile (~40 LOC core).

- [ ] **Step 3.1: Test failing**

Crea `apps/web/src/components/v2/game-chat/__tests__/CitationPdfTab.test.tsx`:

```tsx
/**
 * CitationPdfTab — integration tests con MSW per ownership API
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.2
 */
import { render, screen, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import type { ReactNode } from 'react';

// Mock react-pdf to avoid pdfjs worker loading in jsdom
vi.mock('react-pdf', () => ({
  Document: ({ children, onLoadSuccess }: any) => {
    // Auto-trigger onLoadSuccess simulating successful PDF load
    setTimeout(() => onLoadSuccess?.({ numPages: 24 }), 0);
    return <div data-testid="pdf-document">{children}</div>;
  },
  Page: ({ pageNumber }: { pageNumber: number }) => (
    <div data-testid="pdf-page" data-page-number={pageNumber}>
      Page {pageNumber}
    </div>
  ),
  pdfjs: { GlobalWorkerOptions: { workerSrc: '' } },
}));

vi.mock('@/lib/api', () => ({
  api: {
    knowledgeBase: {
      getGameDocuments: vi.fn(),
    },
  },
  // pdfClient.getPdfDownloadUrl is sync, return predictable URL
}));

vi.mock('@/lib/api/clients/pdfClient', () => ({
  buildPdfClient: () => ({
    getPdfDownloadUrl: (id: string) => `http://test/api/v1/pdfs/${id}/download`,
  }),
}));

// Mock global fetch for blob fetch in viewer
const mockFetch = vi.fn();
vi.stubGlobal('fetch', mockFetch);

import { api } from '@/lib/api';
import { CitationPdfTab } from '../CitationPdfTab';

const sampleDocument = {
  id: 'pdf-wingspan-123',
  title: 'Wingspan Manuale base',
  status: 'indexed' as const,
  pageCount: 24,
  createdAt: '2026-03-08T10:00:00Z',
  category: 'Rulebook' as const,
  versionLabel: 'v1.2',
};

function wrapper({ children }: { children: ReactNode }) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return <QueryClientProvider client={qc}>{children}</QueryClientProvider>;
}

describe('CitationPdfTab', () => {
  beforeEach(() => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockReset();
    mockFetch.mockReset();
    mockFetch.mockResolvedValue({
      ok: true,
      blob: () => Promise.resolve(new Blob([new Uint8Array([0x25, 0x50, 0x44, 0x46])], { type: 'application/pdf' })),
    });
  });

  it('renders PDF viewer immediately when isPublic=true (no ownership fetch)', async () => {
    render(
      <CitationPdfTab
        documentId="pdf-public-1"
        gameId="game-1"
        initialPage={12}
        isPublic
      />,
      { wrapper }
    );
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(api.knowledgeBase.getGameDocuments).not.toHaveBeenCalled();
  });

  it('renders PDF viewer when ownership confirmed (canView=true)', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([sampleDocument]);
    render(
      <CitationPdfTab
        documentId="pdf-wingspan-123"
        gameId="game-wingspan"
        initialPage={12}
      />,
      { wrapper }
    );
    await waitFor(() => expect(screen.getByTestId('pdf-page')).toBeInTheDocument());
    expect(screen.getByTestId('pdf-page')).toHaveAttribute('data-page-number', '12');
  });

  it('renders upsell card when ownership denied (canView=false)', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockResolvedValueOnce([
      { ...sampleDocument, id: 'pdf-OTHER' },
    ]);
    render(
      <CitationPdfTab
        documentId="pdf-wingspan-123"
        gameId="game-wingspan"
        initialPage={12}
      />,
      { wrapper }
    );
    await waitFor(() =>
      expect(screen.getByText(/PDF originale protetto da copyright/i)).toBeInTheDocument()
    );
    expect(screen.queryByTestId('pdf-page')).not.toBeInTheDocument();
  });

  it('renders loading state while ownership check in flight', () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockReturnValueOnce(
      new Promise(() => {})
    );
    render(
      <CitationPdfTab
        documentId="pdf-wingspan-123"
        gameId="game-wingspan"
        initialPage={12}
      />,
      { wrapper }
    );
    expect(screen.getByRole('status', { name: /caricamento|loading/i })).toBeInTheDocument();
  });

  it('renders upsell as fallback when ownership API errors', async () => {
    vi.mocked(api.knowledgeBase.getGameDocuments).mockRejectedValueOnce(new Error('500'));
    render(
      <CitationPdfTab
        documentId="pdf-wingspan-123"
        gameId="game-wingspan"
        initialPage={12}
      />,
      { wrapper }
    );
    await waitFor(() =>
      expect(screen.getByText(/PDF originale protetto da copyright/i)).toBeInTheDocument()
    );
  });
});
```

- [ ] **Step 3.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationPdfTab.test
```
Expected: FAIL — `CitationPdfTab` not found.

- [ ] **Step 3.3: Implementa**

Crea `apps/web/src/components/v2/game-chat/CitationPdfTab.tsx`:

```tsx
/**
 * CitationPdfTab — body del tab "PDF originale" del CitationModal v3.
 *
 * Logica:
 *   - isPublic=true → render PDF viewer immediato
 *   - altrimenti useCanViewPdf:
 *       loading → spinner
 *       canView=true → PDF viewer (Document+Page react-pdf)
 *       canView=false OR isError → CitationOwnershipUpsell
 *
 * Anti-leak:
 *   - oncontextmenu disabled sul container PDF
 *   - NO download button
 *   - NO link al /api/v1/pdfs/{id}/download esposto al click
 *
 * Riusa il pattern fetch-blob di PdfViewerModal.tsx (apps/web/src/components/pdf/).
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.2 §4.2
 */
'use client';

import { useState, useEffect, useCallback, useRef } from 'react';
import type { ReactElement } from 'react';

import { Document, Page, pdfjs } from 'react-pdf';
import 'react-pdf/dist/Page/AnnotationLayer.css';
import 'react-pdf/dist/Page/TextLayer.css';

import clsx from 'clsx';

import { useCanViewPdf } from '@/hooks/queries/useCanViewPdf';
import { buildPdfClient } from '@/lib/api/clients/pdfClient';

import { CitationOwnershipUpsell } from './CitationOwnershipUpsell';

// pdfjs worker setup (mirror of PdfViewerModal.tsx:17-20)
pdfjs.GlobalWorkerOptions.workerSrc = new URL(
  'pdfjs-dist/build/pdf.worker.min.mjs',
  import.meta.url
).toString();

export interface CitationPdfTabProps {
  readonly documentId: string;
  readonly gameId?: string;
  readonly initialPage: number;
  readonly isPublic?: boolean;
  readonly className?: string;
}

export function CitationPdfTab({
  documentId,
  gameId,
  initialPage,
  isPublic = false,
  className,
}: CitationPdfTabProps): ReactElement {
  // Ownership check (skipped via enabled=false if isPublic)
  const ownership = useCanViewPdf({
    documentId,
    gameId,
    enabled: !isPublic,
  });

  const canRenderPdf = isPublic || ownership.canView;
  const isCheckingOwnership = !isPublic && ownership.isLoading;
  const showUpsell = !canRenderPdf && !isCheckingOwnership;

  if (isCheckingOwnership) {
    return (
      <div
        data-slot="citation-pdf-loading-ownership"
        role="status"
        aria-label="Caricamento"
        className={clsx('flex flex-col items-center justify-center gap-3 p-9', className)}
      >
        <div
          aria-hidden="true"
          className={clsx(
            'h-10 w-10 rounded-full border-[3px] border-[hsl(var(--c-kb)/0.2)]',
            'border-t-[hsl(var(--c-kb))] motion-safe:animate-spin'
          )}
        />
        <div className="font-mono text-xs text-muted-foreground">
          Verifica accesso al PDF…
        </div>
      </div>
    );
  }

  if (showUpsell) {
    return <CitationOwnershipUpsell gameId={gameId} className={className} />;
  }

  return (
    <PdfRenderer
      documentId={documentId}
      initialPage={initialPage}
      className={className}
    />
  );
}

// ─────────────────────────────────────────────────────────────────────────────
// PdfRenderer — mirror of PdfViewerModal.tsx body (lines 36-220 condensed),
// embedded inline (no Dialog wrapper), no download button, oncontextmenu blocked
// ─────────────────────────────────────────────────────────────────────────────

interface PdfRendererProps {
  readonly documentId: string;
  readonly initialPage: number;
  readonly className?: string;
}

function PdfRenderer({ documentId, initialPage, className }: PdfRendererProps): ReactElement {
  const [numPages, setNumPages] = useState<number>(0);
  const [currentPage, setCurrentPage] = useState<number>(initialPage);
  const [pdfBlob, setPdfBlob] = useState<Blob | null>(null);
  const [loadError, setLoadError] = useState<string | null>(null);
  const [loading, setLoading] = useState(true);
  const containerRef = useRef<HTMLDivElement>(null);

  const onDocumentLoadSuccess = useCallback(({ numPages }: { numPages: number }) => {
    setNumPages(numPages);
  }, []);

  // Fetch PDF as blob with credentials (mirror PdfViewerModal.tsx pattern)
  useEffect(() => {
    const pdfClient = buildPdfClient();
    const pdfUrl = pdfClient.getPdfDownloadUrl(documentId);
    const controller = new AbortController();

    setLoading(true);
    setLoadError(null);
    setNumPages(0);
    setCurrentPage(initialPage);
    setPdfBlob(null);

    fetch(pdfUrl, { credentials: 'include', signal: controller.signal })
      .then(res => {
        if (!res.ok) throw new Error(`HTTP ${res.status}: ${res.statusText}`);
        return res.blob();
      })
      .then(blob => {
        setPdfBlob(blob);
        setLoading(false);
      })
      .catch(err => {
        if (err.name !== 'AbortError') {
          setLoadError(err.message);
          setLoading(false);
        }
      });

    return () => controller.abort();
  }, [documentId, initialPage]);

  const goPrev = () => setCurrentPage(p => Math.max(1, p - 1));
  const goNext = () => setCurrentPage(p => Math.min(numPages, p + 1));

  return (
    <div
      ref={containerRef}
      data-slot="citation-pdf-renderer"
      className={clsx('flex flex-col gap-3', className)}
    >
      {/* Toolbar: prev/next + page info + anti-leak indicator */}
      <div
        className={clsx(
          'flex items-center gap-2 rounded-md bg-muted px-3 py-2',
          'font-mono text-xs flex-wrap'
        )}
      >
        <button
          type="button"
          onClick={goPrev}
          disabled={currentPage <= 1 || loading}
          className={clsx(
            'rounded-sm border border-border bg-card px-2 py-1',
            'hover:bg-muted hover:border-[hsl(var(--c-kb))]',
            'disabled:opacity-40 disabled:cursor-not-allowed'
          )}
        >
          ← Prev
        </button>
        <span className="flex-1 text-center">
          Pagina <strong>{currentPage}</strong> / {numPages || '?'}
        </span>
        <button
          type="button"
          onClick={goNext}
          disabled={currentPage >= numPages || loading}
          className={clsx(
            'rounded-sm border border-border bg-card px-2 py-1',
            'hover:bg-muted hover:border-[hsl(var(--c-kb))]',
            'disabled:opacity-40 disabled:cursor-not-allowed'
          )}
        >
          Next →
        </button>
        <span className="inline-flex items-center gap-1 text-[10px] text-[hsl(var(--c-success))]">
          🔒 Solo visualizzazione
        </span>
      </div>

      {/* PDF canvas — anti-leak: no right-click, no user-select */}
      <div
        onContextMenu={e => e.preventDefault()}
        className={clsx(
          'relative overflow-hidden rounded-md border border-border bg-white',
          'select-none [&_canvas]:max-w-full',
          'min-h-[400px]'
        )}
        style={{ aspectRatio: '210/297' }}
      >
        {loading && (
          <div
            role="status"
            aria-label="Caricamento PDF"
            className="absolute inset-0 flex items-center justify-center"
          >
            <div
              aria-hidden="true"
              className="h-10 w-10 rounded-full border-[3px] border-[hsl(var(--c-kb)/0.2)] border-t-[hsl(var(--c-kb))] motion-safe:animate-spin"
            />
          </div>
        )}
        {loadError && (
          <div className="flex h-full items-center justify-center p-6 text-sm text-destructive">
            Errore caricamento PDF: {loadError}
          </div>
        )}
        {pdfBlob && (
          <Document
            file={pdfBlob}
            onLoadSuccess={onDocumentLoadSuccess}
            loading={null}
            error={null}
          >
            <Page
              pageNumber={currentPage}
              renderAnnotationLayer={false}
              renderTextLayer={false}
            />
          </Document>
        )}
      </div>
    </div>
  );
}
```

- [ ] **Step 3.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationPdfTab.test
```
Expected: PASS (5/5).

> Se test fail per `pdfjs.GlobalWorkerOptions` in jsdom, verifica che il `vi.mock('react-pdf', ...)` copra anche `pdfjs`. Vedi mock in Step 3.1.

- [ ] **Step 3.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/CitationPdfTab.tsx apps/web/src/components/v2/game-chat/__tests__/CitationPdfTab.test.tsx
git commit -m "feat(web): #921 CitationPdfTab with ownership gate + react-pdf embed"
```

---

## Task 4: Modifica `CitationModal` (tab system)

**Files:**
- Modify: `apps/web/src/components/v2/game-chat/CitationModal.tsx`
- Modify: `apps/web/src/components/v2/game-chat/__tests__/CitationModal.test.tsx`

**Cambiamenti contract:**
```ts
// PRIMA (PR #918)
interface CitationModalProps {
  citation: Citation | null;
  open: boolean;
  onClose: () => void;
  onOpenInKb?: () => void;  // ← RIMOSSO
}

// DOPO (G4 v3)
interface CitationModalProps {
  citation: Citation | null;
  open: boolean;
  onClose: () => void;
  gameId?: string;  // ← NUOVO (necessario per upsell + ownership check)
}
```

- [ ] **Step 4.1: Aggiorna test esistente**

Modifica `apps/web/src/components/v2/game-chat/__tests__/CitationModal.test.tsx`:

1. **Rimuovi** i 2 test esistenti su `onOpenInKb`:
   - `'hides "Apri nella KB" footer when onOpenInKb is undefined'`
   - `'shows "Apri nella KB" when onOpenInKb provided + calls it on click'`

2. **Aggiungi** in fondo al `describe('CitationModal', ...)` blocco questi 4 nuovi test:

```tsx
  it('renders snippet tab content by default', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="game-1" />);
    expect(screen.getByRole('tab', { name: /snippet/i })).toHaveAttribute('aria-selected', 'true');
    expect(screen.getByText(/Ogni potere/)).toBeInTheDocument();
  });

  it('renders both tabs (Snippet + PDF originale) always visible', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="game-1" />);
    expect(screen.getByRole('tab', { name: /snippet/i })).toBeInTheDocument();
    expect(screen.getByRole('tab', { name: /pdf originale/i })).toBeInTheDocument();
  });

  it('switching to PDF tab triggers lazy mount of CitationPdfTab', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="game-1" />);
    // Initially PDF tab content NOT mounted
    expect(screen.queryByTestId('citation-pdf-tab-mounted')).not.toBeInTheDocument();
    // Click PDF tab
    fireEvent.click(screen.getByRole('tab', { name: /pdf originale/i }));
    expect(screen.getByRole('tab', { name: /pdf originale/i })).toHaveAttribute('aria-selected', 'true');
    // Now CitationPdfTab is mounted (we use a marker via data-slot)
    expect(screen.getByTestId('citation-pdf-tab-mounted')).toBeInTheDocument();
  });

  it('passes gameId and citation fields to CitationPdfTab', () => {
    render(<CitationModal citation={sampleCitation} open onClose={vi.fn()} gameId="wingspan" />);
    fireEvent.click(screen.getByRole('tab', { name: /pdf originale/i }));
    const tab = screen.getByTestId('citation-pdf-tab-mounted');
    expect(tab).toHaveAttribute('data-document-id', sampleCitation.documentId);
    expect(tab).toHaveAttribute('data-game-id', 'wingspan');
    expect(tab).toHaveAttribute('data-initial-page', String(sampleCitation.pageNumber));
  });
```

> Aggiungi anche il mock di `CitationPdfTab` in cima al file di test (per evitare di caricare react-pdf):
> ```tsx
> vi.mock('../CitationPdfTab', () => ({
>   CitationPdfTab: (props: any) => (
>     <div
>       data-testid="citation-pdf-tab-mounted"
>       data-document-id={props.documentId}
>       data-game-id={props.gameId}
>       data-initial-page={props.initialPage}
>     >
>       PDF tab mock
>     </div>
>   ),
> }));
> ```

- [ ] **Step 4.2: Run test, verify FAIL**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationModal.test
```
Expected: FAIL — `onOpenInKb` test rimossi (4 test mancanti che asseriscono tabs/lazy mount).

- [ ] **Step 4.3: Implementa modifiche al `CitationModal.tsx`**

Sostituisci interamente il body di `apps/web/src/components/v2/game-chat/CitationModal.tsx` con:

```tsx
/**
 * CitationModal — preview citation con tab Snippet (default) + PDF originale (lazy).
 *
 * Tab Snippet (default): rendering testo della citation (snippet o paraphrasedSnippet
 * se copyrightTier='protected'). Pattern PR #918 invariato per il body Snippet.
 *
 * Tab PDF originale (lazy mount): delega a <CitationPdfTab> che gestisce ownership
 * gate via useCanViewPdf + isPublic shortcut. Il contenuto NON viene montato finché
 * l'utente non clicca il tab (anti-fetch).
 *
 * Spec: docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md §3.1 §4.4
 */
'use client';

import { useEffect, useRef, useState } from 'react';
import type { ReactElement } from 'react';

import clsx from 'clsx';

import type { Citation } from '@/types';

import { CitationPdfTab } from './CitationPdfTab';

export interface CitationModalProps {
  readonly citation: Citation | null;
  readonly open: boolean;
  readonly onClose: () => void;
  readonly gameId?: string;
}

type TabKind = 'snippet' | 'pdf';

export function CitationModal({
  citation,
  open,
  onClose,
  gameId,
}: CitationModalProps): ReactElement | null {
  const containerRef = useRef<HTMLDivElement>(null);
  const [activeTab, setActiveTab] = useState<TabKind>('snippet');
  const [pdfTabEverOpened, setPdfTabEverOpened] = useState(false);

  // Reset state on each open
  useEffect(() => {
    if (open) {
      setActiveTab('snippet');
      setPdfTabEverOpened(false);
    }
  }, [open]);

  useEffect(() => {
    if (!open) return;
    containerRef.current?.focus();
    const handleEsc = (e: KeyboardEvent) => {
      if (e.key === 'Escape') onClose();
    };
    document.addEventListener('keydown', handleEsc);
    return () => document.removeEventListener('keydown', handleEsc);
  }, [open, onClose]);

  if (!open || !citation) return null;

  const handleTabChange = (tab: TabKind) => {
    setActiveTab(tab);
    if (tab === 'pdf') setPdfTabEverOpened(true);
  };

  const displayedSnippet =
    citation.copyrightTier === 'protected' && citation.paraphrasedSnippet
      ? citation.paraphrasedSnippet
      : citation.snippet;

  return (
    <div
      role="dialog"
      aria-modal="true"
      aria-labelledby="citation-modal-title"
      data-slot="citation-modal"
      className={clsx(
        'fixed inset-0 z-50 flex items-center justify-center',
        'bg-black/50 backdrop-blur-sm'
      )}
      onClick={onClose}
    >
      <div
        ref={containerRef}
        tabIndex={-1}
        className={clsx(
          'mx-4 flex max-h-[90%] w-full max-w-[600px] flex-col',
          'overflow-hidden rounded-lg border border-border bg-card shadow-xl outline-none'
        )}
        onClick={e => e.stopPropagation()}
      >
        {/* Header */}
        <header className="flex items-center gap-3 border-b border-border-light px-4 py-3">
          <h2 id="citation-modal-title" className="text-lg font-bold">
            📖 p. {citation.pageNumber}
          </h2>
          <span className="flex-1" />
          <button
            type="button"
            aria-label="Chiudi"
            onClick={onClose}
            className="rounded-sm p-1 text-muted-foreground hover:bg-muted"
          >
            ×
          </button>
        </header>

        {/* Tabs */}
        <nav className="flex gap-0 border-b border-border-light px-4" role="tablist">
          <TabButton kind="snippet" active={activeTab} onSelect={handleTabChange}>
            📝 Snippet
          </TabButton>
          <TabButton kind="pdf" active={activeTab} onSelect={handleTabChange}>
            📄 PDF originale
          </TabButton>
        </nav>

        {/* Tab body */}
        <div className="flex flex-1 flex-col gap-3 overflow-y-auto p-4">
          {activeTab === 'snippet' && (
            <blockquote
              className={clsx(
                'rounded-r-md border-l-4 px-4 py-3 italic',
                'border-l-[hsl(var(--c-kb))] bg-[hsl(var(--c-kb)/0.05)]',
                'text-base leading-relaxed text-foreground'
              )}
            >
              {displayedSnippet || (
                <span className="text-sm not-italic text-muted-foreground">
                  Nessuna anteprima disponibile.
                </span>
              )}
            </blockquote>
          )}

          {activeTab === 'pdf' && pdfTabEverOpened && (
            <CitationPdfTab
              documentId={citation.documentId}
              gameId={gameId}
              initialPage={citation.pageNumber}
              isPublic={citation.isPublic}
            />
          )}
        </div>

        {/* Footer */}
        <footer className="flex items-center gap-2 border-t border-border-light bg-card px-4 py-3">
          <span className="flex-1" />
          <button
            type="button"
            onClick={onClose}
            className={clsx(
              'rounded-full bg-[hsl(var(--c-chat))] px-4 py-1.5',
              'text-sm font-semibold text-white hover:opacity-90'
            )}
          >
            Chiudi
          </button>
        </footer>
      </div>
    </div>
  );
}

interface TabButtonProps {
  readonly kind: TabKind;
  readonly active: TabKind;
  readonly onSelect: (kind: TabKind) => void;
  readonly children: React.ReactNode;
}

function TabButton({ kind, active, onSelect, children }: TabButtonProps): ReactElement {
  const isSelected = active === kind;
  return (
    <button
      type="button"
      role="tab"
      aria-selected={isSelected}
      onClick={() => onSelect(kind)}
      className={clsx(
        'relative inline-flex items-center gap-1.5 px-4 py-3',
        'font-semibold text-sm transition-colors',
        isSelected
          ? 'text-[hsl(var(--c-kb))] after:absolute after:inset-x-3 after:-bottom-px after:h-0.5 after:rounded-full after:bg-[hsl(var(--c-kb))]'
          : 'text-muted-foreground hover:text-foreground'
      )}
    >
      {children}
    </button>
  );
}
```

- [ ] **Step 4.4: Run test fino a passare**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/CitationModal.test
```
Expected: PASS (3 originali rimasti + 4 nuovi = 7/7).

> Se i test esistenti mostrano breaking change su `onOpenInKb`, conferma che siano stati rimossi correttamente nello Step 4.1.

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/CitationModal.tsx apps/web/src/components/v2/game-chat/__tests__/CitationModal.test.tsx
git commit -m "feat(web): #921 CitationModal tabs (Snippet + PDF lazy) + remove onOpenInKb prop"
```

---

## Task 5: Wiring `GameChatTabV2.tsx`

**Files:**
- Modify: `apps/web/src/components/v2/game-chat/GameChatTabV2.tsx`
- Modify: `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx`

- [ ] **Step 5.1: Aggiorna test orchestrator**

Modifica `apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx`:

Trova il test `'citation chip click opens CitationModal'` e **rimuovi l'asserzione**:

```tsx
// RIMUOVI questa riga (PR #918 era valida quando onOpenInKb=undefined):
expect(screen.queryByRole('button', { name: /apri nella kb/i })).not.toBeInTheDocument();
```

Mantieni le altre asserzioni del test. Il test ora verifica solo che il modal si apre — il comportamento dei tab è in `CitationModal.test.tsx`.

- [ ] **Step 5.2: Modifica `GameChatTabV2.tsx`**

Cerca la sezione finale dove `<CitationModal>` viene renderizzato (nel return finale, vicino a `</div>`):

```tsx
// PRIMA (PR #918)
<CitationModal
  citation={selectedCitation}
  open={selectedCitation !== null}
  onClose={() => setSelectedCitation(null)}
  onOpenInKb={undefined /* TODO: enable when G4 lands — () => router.push(`/kb/${gameId}#chunk-${selectedCitation?.chunkId}`) */}
/>
```

Sostituisci con:

```tsx
// DOPO (G4 v3)
<CitationModal
  citation={selectedCitation}
  open={selectedCitation !== null}
  onClose={() => setSelectedCitation(null)}
  gameId={gameId}
/>
```

- [ ] **Step 5.3: Run test orchestrator**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat/__tests__/GameChatTabV2.test
```
Expected: tutti i test passano (5/5 originali).

- [ ] **Step 5.4: Run typecheck**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori.

- [ ] **Step 5.5: Commit**

```bash
git add apps/web/src/components/v2/game-chat/GameChatTabV2.tsx apps/web/src/components/v2/game-chat/__tests__/GameChatTabV2.test.tsx
git commit -m "feat(web): #921 wire CitationModal gameId in GameChatTabV2 (sblocca G4)"
```

---

## Task 6: Aggiorna barrel + cleanup KB-detail stub

**Files:**
- Modify: `apps/web/src/components/v2/game-chat/index.ts`
- Modify (header comment): tutti i 6 file in `apps/web/src/components/v2/kb-detail/`
- Modify: `docs/for-developers/frontend/v2-migration-matrix.md`

- [ ] **Step 6.1: Aggiungi export al barrel game-chat**

Modifica `apps/web/src/components/v2/game-chat/index.ts` aggiungendo in fondo:

```ts
export { CitationPdfTab } from './CitationPdfTab';
export type { CitationPdfTabProps } from './CitationPdfTab';

export { CitationOwnershipUpsell } from './CitationOwnershipUpsell';
export type { CitationOwnershipUpsellProps } from './CitationOwnershipUpsell';
```

- [ ] **Step 6.2: Aggiungi header comment ai 6 stub kb-detail**

Per ognuno dei 6 file in `apps/web/src/components/v2/kb-detail/` (KbHeader, KbChunkListPanel, KbChunkPreview, ChunkSearchBox, MarkdownRenderBlock, KbProcessingState), aggiungi questo header **PRIMA** del codice esistente (preserva il body, è solo un commento iniziale):

```tsx
/**
 * DEFERRED — vedi spec G4 v3 §5.2:
 * docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md
 *
 * Questo componente fa parte di un progetto "/kb/[id]" page (originariamente
 * Wave 3 Tier M) che è stato cancellato dopo il pivot legale del 2026-05-10:
 * il flusso "serata di gioco" ora apre il PDF via CitationModal con ownership
 * gate, NON via pagina dedicata.
 *
 * Lo stub resta per:
 *   - Future "browse KB" feature autonoma (post-Alpha)
 *   - Mantenere v2-migration-matrix coerente
 *
 * NON IMPORTARE questo componente. Ritorna null finché un nuovo plan dedicato
 * non lo riprende.
 */
```

> Procedura sicura: usa Edit tool per ognuno, inserendo il commento subito dopo l'eventuale "// TODO: implement..." esistente. Body invariato.

- [ ] **Step 6.3: Aggiorna v2-migration-matrix**

Trova nella matrix la riga `/kb/[id]` (sezione Tier mapping) e nella sezione tabella `Wave 3 — KB detail`. Modifica entrambe:

Sezione Tier mapping (riga ~105):
```markdown
| `/kb/[id]` | **M** | KB header + chunks + search | **deferred** — pivot legale 2026-05-10, vedi `2026-05-10-citation-pdf-viewer-design.md` (G4 v3) |
```

Sezione Wave 3 KB detail (cerca le righe `KbHeader`, `KbChunkListPanel`, ecc.). Cambia per ognuna lo `Status` da `pending` a `deferred — vedi G4 v3` e nota nel `PR` colonna `—`.

- [ ] **Step 6.4: Run typecheck full**

```bash
cd apps/web && pnpm typecheck
```
Expected: 0 errori. Gli stub kb-detail con commento aggiunto compilano ancora (sono solo `return null`).

- [ ] **Step 6.5: Commit cleanup**

```bash
git add apps/web/src/components/v2/game-chat/index.ts apps/web/src/components/v2/kb-detail/ docs/for-developers/frontend/v2-migration-matrix.md
git commit -m "chore(web): #921 mark kb-detail stubs deferred + update v2-matrix (G4 v3 pivot)"
```

---

## Task 7: Final verify + matrix + PR

- [ ] **Step 7.1: Lint + typecheck full pass**

```bash
cd apps/web && pnpm lint && pnpm typecheck
```
Expected: 0 errori, baseline ~52 warning pre-existing.

- [ ] **Step 7.2: Run full game-chat test suite**

```bash
cd apps/web && pnpm vitest run src/components/v2/game-chat src/hooks/queries/__tests__/useCanViewPdf
```
Expected: tutti i test passano (56 game-chat originali + 4 CitationOwnershipUpsell + 5 CitationPdfTab + 4 CitationModal nuovi + 5 useCanViewPdf = ~74 test totali).

- [ ] **Step 7.3: Smoke test manuale**

Avvia ambiente:
```bash
cd infra && make dev-core
```

Apri `http://localhost:3000/library/games/[gameId]?tab=aiChat` (con KB indicizzata):
- Invia messaggio test → ricevi risposta con citation chip
- Click chip → modal apre con tab **Snippet** attivo (blockquote teal)
- Click tab **PDF originale**:
  - Se l'utente HA caricato il PDF (verifica con `/upload`) → spinner brevemente → PDF.js renderizza pagina + toolbar prev/next + indicator "🔒"
  - Altrimenti → upsell card immediato (no spinner) con CTA "Carica il mio PDF"
- Click CTA upsell → naviga a `/upload?gameId=<X>`
- Verifica: nessun bottone download visibile, right-click sul PDF disabilitato

Annota differenze visive come issue follow-up. NON correggere in questa PR salvo regressioni gravi.

- [ ] **Step 7.4: Push + apri PR verso `main-dev`**

```bash
git push -u origin feature/issue-921-kb-detail-page
gh pr create --repo meepleAi-app/meepleai-monorepo --base main-dev --title "feat(web): #921 G4 v3 — Citation PDF viewer with ownership gate" --body "$(cat <<'EOF'
## Summary

Estende il \`CitationModal\` (PR #918) con tab \`Snippet | PDF originale\`, ownership gate, embed PDF.js viewer e upsell card per non-owner. Sblocca lo \`onOpenInKb\` di PR #918 (rimosso, logica vive nel modal).

Closes #921.

### Componenti nuovi (3 + 1 hook)
- \`useCanViewPdf\` hook — ownership check via \`getGameDocuments\` + \`isPublic\` shortcut
- \`CitationOwnershipUpsell\` pure component — card "Carica il mio PDF" → CTA \`/upload?gameId=X\`
- \`CitationPdfTab\` — body del tab PDF (gating + viewer embed riusa pattern \`PdfViewerModal.tsx\`)

### Modifiche
- \`CitationModal\`: aggiunto tab system, lazy mount tab PDF, rimosso \`onOpenInKb\` prop, aggiunto \`gameId\` prop
- \`GameChatTabV2\`: passa \`gameId\` invece di \`onOpenInKb\`

### Backend
**Zero modifiche.** Riusa endpoint esistenti:
- \`GET /api/v1/knowledge-base/{gameId}/documents\` (ownership check)
- \`GET /api/v1/pdfs/{pdfId}/download\` (PDF blob)

### Pivot storia (importante per il reviewer)

Lo spec G4 originale prevedeva una **nuova pagina** \`/kb/[documentId]\` con TOC + chunks markdown. Durante brainstorming socratico è emerso il vero modello legale (PDF protetti per-documentId, anti-leak, upload-only). Lo spec è stato riscritto come **estensione del CitationModal**, non pagina nuova. Vedi commit \`bab4dfad3\` per dettagli pivot. I 6 stub V2 KB-detail restano come deferred.

### Anti-leak

- \`oncontextmenu={preventDefault}\` sul container PDF
- NO download button visibile
- \`user-select: none\` sul canvas
- Indicator esplicito "🔒 Solo visualizzazione · No download"

### Test plan

- [x] Unit \`useCanViewPdf\` (5 scenari: enabled=false · gameId missing · canView=true · canView=false · isError)
- [x] Unit \`CitationOwnershipUpsell\` (4 scenari: title · CTA con gameId · CTA senza · footnote)
- [x] Unit \`CitationPdfTab\` (5 scenari MSW: isPublic shortcut · owner · non-owner · loading · error)
- [x] Unit \`CitationModal\` (4 nuovi scenari: snippet default · entrambe tab visibili · lazy mount PDF · prop pass)
- [x] No regression \`GameChatTabV2\` test (5/5)
- [ ] Smoke test manuale richiesta al reviewer

### Spec / Plan / Mockup

- Spec: [\`docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md\`](docs/superpowers/specs/2026-05-10-citation-pdf-viewer-design.md)
- Plan: [\`docs/superpowers/plans/2026-05-10-citation-pdf-viewer-g4.md\`](docs/superpowers/plans/2026-05-10-citation-pdf-viewer-g4.md)
- Mockup: [\`admin-mockups/design_files/sp4-citation-pdf-viewer.html\`](admin-mockups/design_files/sp4-citation-pdf-viewer.html)

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 7.5: Aggiorna issue #921 con PR link + chiusura**

```bash
gh issue comment 921 --repo meepleAi-app/meepleai-monorepo --body "PR aperta: #YYY (sostituisci con numero reale dopo gh pr create)"
```

---

## Out of scope (follow-up plans)

| Item | Reason | Plan target |
|---|---|---|
| Refactor `PdfViewerModal.tsx` per estrarre `PdfRenderer` riusabile | Copy-paste accettabile in scope G4 v3 (~40 LOC) | Plan separato post-merge se serve |
| Pagina autonoma `/kb/[documentId]` per "browse KB" | Pivot legale esclude scope game-night | Plan futuro post-Alpha (V3?) |
| Watermark dinamico sul PDF (email utente sovrapposta) | Anti-leak basic basta per Alpha | Follow-up se richiesto |
| 6 stub KB-detail completion (KbHeader, etc.) | Deferred — vedi commento header | Plan dedicato "browse KB" |
| Backend endpoint `/api/v1/pdfs/{id}/can-view` | Frontend deduce da `getGameDocuments` (sufficiente) | Plan dedicato se admin/public flag estensione necessaria |

---

## Spec Self-Review (eseguito)

**Spec coverage**: tutte le sezioni dello spec G4 v3 §1-§7 sono coperte:
- §2 Architecture → Task 1 (hook), 2 (upsell), 3 (CitationPdfTab), 4 (CitationModal modify), 5 (GameChatTabV2 wiring)
- §3 Component contracts → contracts in Task 1-3 corrispondono 1:1
- §4 Logic decisions → Task 3 implementa hybrid C ownership + lazy mount Task 4
- §4.3 Sblocco PR #918 → Task 5
- §5 Cancellazioni cleanup → Task 6
- §6 Testing → ogni task ha unit + integration test (TDD strict)

**Placeholder scan**: zero `TBD/TODO/...later` accidentali. Il `TODO` originale di PR #918 in `GameChatTabV2.tsx` viene **eliminato** in Task 5.2 (sostituito con prop `gameId`). Placeholder intenzionali:
- `#YYY` (PR number) sostituito a Step 7.5

**Type consistency**:
- `Citation` type importato da `@/types` in tutti i file ✓
- `UseCanViewPdfArgs` props match in test e impl ✓
- `CitationPdfTabProps` (`documentId`, `gameId`, `initialPage`, `isPublic`) coerenti tra Task 3 + Task 4 (CitationModal pass-through) ✓
- `GameDocument.id` matcha `Citation.documentId` (entrambi UUID PDF, verificato in pre-flight) ✓

**Risk noted**:
- Test `CitationPdfTab` mockano `react-pdf` per evitare pdfjs worker in jsdom — pattern verificato in `apps/web/src/test-utils/__mocks__/react-pdf-viewer-core.ts` esistente.
- Step 6.2 modifica 6 stub kb-detail — solo aggiunta header comment, body invariato. Se gli stub erano già stati implementati da qualcun altro, lo Step va saltato (verifica prima con `cat apps/web/src/components/v2/kb-detail/KbHeader.tsx`).
- `buildPdfClient` viene chiamato dentro `PdfRenderer` — se il client esistente è esposto come singleton già istanziato (`api.pdf.getPdfDownloadUrl`), preferisci quello. Verifica nello Step 3.3.
