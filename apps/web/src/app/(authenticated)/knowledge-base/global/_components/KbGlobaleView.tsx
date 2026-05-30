/**
 * KbGlobaleView.tsx
 * Issue #1482 Task 6 — Client orchestrator for /knowledge-base/global
 * Issue #1482 Task 9 — Viewer + drawer lazy branches
 *
 * URL SSOT: reads ?q= and ?mode= from search params; pushes updated URL on
 * HeroSearch submit or clear. Branches on `q.trim().length > 0`:
 *   - Home branch (q empty)  → <KbHomeDesktop> (recent docs from useUserKbDocs)
 *   - Results branch (q set) → <KbSearchResultsDesktop> (useGlobalKbSearch)
 *
 * Phase 2 URL parameters:
 *   - ?docId=   → lazy-loads KbDocViewerDesktop (status='ready' only)
 *   - ?ask=1    → lazy-loads DrawerShell (useKbAskStream)
 *
 * HeroSearch is always visible at the top of the page regardless of branch.
 *
 * No double-fetch design:
 *   - useUserKbDocs is always called (cached 5min, cheap, feeds home branch).
 *   - useGlobalKbSearch is always called but gated via `enabled: !isHomeBranch`.
 *   - useKbDocDetail is always called but gated via `enabled: Boolean(docIdParam)`.
 *
 * fileUrl derivation: KbDocDetail has NO fileUrl field (Task 5 finding).
 * Derived as `/api/v1/pdfs/${doc.id}/download` (PdfRetrievalEndpoints.cs:61).
 *
 * Labels: in-file constants for v1. Task 10 extracts them to i18n catalog.
 *
 * @see Issue #1482 Phase 1 Foundation
 * @see Issue #1482 Phase 2 D-G (viewer), D-H (doc detail), D-J (mobile), D-L (drawer)
 */

'use client';

import { type JSX, useCallback, useEffect, useMemo } from 'react';

import dynamic from 'next/dynamic';
import { useRouter, useSearchParams } from 'next/navigation';

import { HeroSearch } from '@/components/features/kb-globale/HeroSearch';
import type { KbDocViewerCitation } from '@/components/features/kb-globale/KbDocViewerDesktop';
import { KbHomeDesktop } from '@/components/features/kb-globale/KbHomeDesktop';
import { KbSearchResultsDesktop } from '@/components/features/kb-globale/KbSearchResultsDesktop';
import { useGlobalKbSearch } from '@/hooks/queries/useGlobalKbSearch';
import { useKbChunkDetail } from '@/hooks/queries/useKbChunkDetail';
import { useKbDocDetail } from '@/hooks/queries/useKbDocDetail';
import { useUserKbDocs } from '@/hooks/queries/useUserKbDocs';
import { useKbAskStream } from '@/hooks/useKbAskStream';
import type { SearchMode } from '@/lib/api/schemas/kb-globale.schemas';
import type { KbDoc } from '@/lib/library/hybrid-hub.mappers';

// ---------------------------------------------------------------------------
// Lazy-loaded Phase 2 components (ssr: false — react-pdf requires browser env)
// ---------------------------------------------------------------------------

const KbDocViewerDesktopLazy = dynamic(
  () =>
    import('@/components/features/kb-globale/KbDocViewerDesktop').then(m => ({
      default: m.KbDocViewerDesktop,
    })),
  { ssr: false }
);

const DrawerShellLazy = dynamic(
  () =>
    import('@/components/features/kb-globale/DrawerShell').then(m => ({
      default: m.DrawerShell,
    })),
  { ssr: false }
);

// ---------------------------------------------------------------------------
// In-file label constants (v1 — i18n extraction deferred to Task 8)
// ---------------------------------------------------------------------------

const LABELS = {
  hero: {
    placeholder: 'Cerca nella knowledge base…',
    submit: 'Cerca',
    clear: 'Cancella',
    modeLabel: 'Modalità di ricerca',
    modeOptions: {
      Semantic: 'Semantica',
    } as Record<SearchMode, string>,
  },
  home: {
    heading: 'Documenti recenti',
    empty: {
      title: 'Nessun documento ancora',
      description: 'Carica un PDF dalla libreria per iniziare.',
      cta: 'Vai alla libreria',
    },
    errorTitle: 'Impossibile caricare i documenti',
    errorDescription: 'Si è verificato un errore. Riprova tra qualche istante.',
    retry: 'Riprova',
    docCardAriaLabel: (doc: KbDoc) => `${doc.fileName}${doc.gameName ? ` — ${doc.gameName}` : ''}`,
  },
  results: {
    resultsCount: (n: number, q: string) => `${n} risultat${n === 1 ? 'o' : 'i'} per «${q}»`,
    loadMore: 'Carica altri',
    loadingMore: 'Caricamento…',
    empty: {
      title: 'Nessun risultato',
      description: "Prova con termini diversi o controlla l'ortografia.",
    },
    errorTitle: 'Errore nella ricerca',
    errorDescription: 'La ricerca non è riuscita. Riprova tra qualche istante.',
    retry: 'Riprova',
    resultAriaLabel: (r: { docTitle: string; gameName: string }) => `${r.docTitle} — ${r.gameName}`,
    pageLabel: (page: number) => `Pagina ${page}`,
  },
  // ── Phase 2 viewer (Task 10 will extract to i18n catalog) ──────────────
  viewer: {
    pageLabel: (n: number) => `Pagina ${n}`,
    zoomIn: 'Zoom in',
    zoomOut: 'Zoom out',
    zoomReset: 'Reset',
    thumbnailsLabel: 'Pagine',
    closeLabel: 'Chiudi',
    pageOfTotal: (cur: number, total: number) => `${cur} / ${total}`,
  },
  // ── Phase 2 drawer (Task 10 will extract to i18n catalog) ──────────────
  drawer: {
    suggestions: [
      'Come funziona il setup iniziale?',
      'Quali sono le abilità della classe Scout?',
      'Differenza tra base e enhanced effect?',
    ],
    shell: {
      title: 'Ask the Meeple',
      subtitle: 'Knowledge Base',
      closeLabel: 'Chiudi',
      idle: {
        welcomeTitle: 'Chiedimi qualsiasi cosa sui tuoi giochi',
        welcomeBody: 'Cerco nei tuoi PDF e cito le pagine esatte.',
        suggestionsLabel: 'Suggerimenti',
        placeholder: 'Chiedi al Meeple…',
        sendLabel: 'Invia',
      },
      streaming: { statusLabel: 'STREAMING', stopLabel: 'Stop streaming' },
      completed: {
        completedLabel: 'COMPLETED',
        copyLabel: 'Copia',
        regenerateLabel: 'Rigenera',
        inlineCitationAriaLabel: (c: { docId: string; page: number }, n: number) =>
          `Citazione ${n}, documento ${c.docId}, pagina ${c.page}`,
      },
      empty: {
        title: 'Nessun documento nella tua libreria',
        body: 'Carica un PDF dalla libreria per iniziare a chiedere.',
        cta: 'Vai alla libreria',
      },
      error: {
        connection: {
          title: 'Connessione persa',
          body: 'Retry automatico in corso…',
          action: 'Riprova ora',
        },
        timeout: {
          title: 'Risposta lenta',
          body: 'Vuoi continuare ad aspettare?',
          action: 'Continua attesa',
          alt: 'Cancella',
        },
        partial: {
          title: 'Risposta incompleta',
          body: 'Lo stream si è interrotto.',
          action: 'Ripeti query',
        },
        server: {
          title: 'Errore del server',
          body: 'Riprova tra qualche istante.',
          action: 'Riprova',
        },
      },
    },
  },
};

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export function KbGlobaleView(): JSX.Element {
  const router = useRouter();
  const searchParams = useSearchParams();

  // ── URL state (SSOT) ───────────────────────────────────────────────────
  const q = searchParams.get('q')?.trim() ?? '';
  const modeRaw = searchParams.get('mode');
  // SearchMode is currently only 'Semantic'. Validate strictly so future
  // enum extensions are safe.
  const mode: SearchMode | undefined = modeRaw === 'Semantic' ? 'Semantic' : undefined;

  const isHomeBranch = q.length === 0;

  // ── Phase 2: URL params ────────────────────────────────────────────────
  const docIdParam = searchParams.get('docId');
  const pageParam = Number(searchParams.get('page')) || 1;
  const chunkIdParam = searchParams.get('chunkId');
  const askParam = searchParams.get('ask') === '1';

  // ── Hooks (called unconditionally per React rules) ─────────────────────
  // useUserKbDocs: always consumed (home branch data + cached warmup).
  const recent = useUserKbDocs();

  // useGlobalKbSearch: always called but disabled on home branch.
  const search = useGlobalKbSearch({
    query: q,
    mode,
    enabled: !isHomeBranch,
  });

  // useKbDocDetail: always called but gated via enabled. Returns a
  // KbDocEnvelope discriminated union: {status:'ready',doc} | {status:'locked',...}
  const docDetail = useKbDocDetail({ docId: docIdParam, enabled: Boolean(docIdParam) });

  // useKbChunkDetail: resolves chunkId → pageNumber for citation deep-link (#1702).
  // Enabled only when both docId and chunkId are present in URL.
  const chunkQuery = useKbChunkDetail({
    docId: docIdParam,
    chunkId: chunkIdParam,
    enabled: docIdParam != null && chunkIdParam != null,
  });

  // useKbAskStream: FSM 5-state hook for the DrawerShell.
  const askStream = useKbAskStream();

  // ── URL update helpers ─────────────────────────────────────────────────
  const pushUrl = useCallback(
    (newQ: string, newMode: SearchMode) => {
      const params = new URLSearchParams();
      if (newQ) params.set('q', newQ);
      // 'Semantic' is the default — omit from URL to keep it clean.
      if (newMode !== 'Semantic') params.set('mode', newMode);
      const qs = params.toString();
      router.push(qs ? `/knowledge-base/global?${qs}` : '/knowledge-base/global');
    },
    [router]
  );

  const clearUrl = useCallback(() => {
    router.push('/knowledge-base/global');
  }, [router]);

  // ── Phase 2: viewer + drawer URL helpers ──────────────────────────────
  const openViewer = useCallback(
    (result: { docId: string; page: number; chunkId?: string }) => {
      const params = new URLSearchParams(searchParams.toString());
      params.set('docId', result.docId);
      params.set('page', String(result.page));
      if (result.chunkId) {
        params.set('chunkId', result.chunkId);
      } else {
        params.delete('chunkId');
      }
      router.push(`/knowledge-base/global?${params.toString()}`);
    },
    [router, searchParams]
  );

  const closeViewer = useCallback(() => {
    const params = new URLSearchParams(searchParams.toString());
    params.delete('docId');
    params.delete('page');
    params.delete('chunkId'); // prevent stale chunkId persistence (#1702)
    const qs = params.toString();
    router.push(qs ? `/knowledge-base/global?${qs}` : '/knowledge-base/global');
  }, [router, searchParams]);

  const closeDrawer = useCallback(() => {
    const params = new URLSearchParams(searchParams.toString());
    params.delete('ask');
    const qs = params.toString();
    router.push(qs ? `/knowledge-base/global?${qs}` : '/knowledge-base/global');
  }, [router, searchParams]);

  // ── Derived values ─────────────────────────────────────────────────────
  const recentDocs: readonly KbDoc[] = useMemo(
    () => recent.data?.items ?? [],
    [recent.data?.items]
  );

  // ── Phase 2: chunk-level page resolution (#1702) ──────────────────────
  // resolvedPage: uses chunkQuery.data.pageNumber when available, falls back to
  // pageParam from URL. Pure memo — no side effects here (React 19 Strict Mode
  // double-invokes memos, so console.warn goes in a separate useEffect below).
  const resolvedPage = useMemo(() => {
    if (chunkQuery.isSuccess && chunkQuery.data?.pageNumber != null) {
      return chunkQuery.data.pageNumber;
    }
    return pageParam;
  }, [chunkQuery.isSuccess, chunkQuery.data?.pageNumber, pageParam]);

  // Graceful degrade: warn when a chunkId in the URL cannot be resolved.
  useEffect(() => {
    if (chunkQuery.isError && chunkIdParam) {
      console.warn(
        `[KbGlobaleView] chunkId "${chunkIdParam}" not resolvable; falling back to page-level scroll (page=${pageParam}).`
      );
    }
  }, [chunkQuery.isError, chunkIdParam, pageParam]);

  // ── Phase 2: derive viewer props from envelope ─────────────────────────
  // Only mount the viewer when status === 'ready' (locked = doc still processing).
  const isViewerReady = docDetail.data?.status === 'ready';
  const viewerDoc =
    isViewerReady && docDetail.data?.status === 'ready'
      ? {
          id: docDetail.data.doc.id,
          title: docDetail.data.doc.title,
          // KbDocDetail has NO fileUrl field (Task 5 finding).
          // Derived from kbDoc.id === pdf.Id (PdfRetrievalEndpoints.cs:61).
          fileUrl: `/api/v1/pdfs/${docDetail.data.doc.id}/download`,
          // pageCount is nullable upstream; normalize to 1 as sentinel
          // (react-pdf onLoadSuccess will override at render time).
          pageCount: docDetail.data.doc.pageCount ?? 1,
        }
      : null;

  // Map KbCitation (from useKbAskStream state) → KbDocViewerCitation (numbered).
  const viewerCitations: readonly KbDocViewerCitation[] = askStream.state.citations.map((c, i) => ({
    n: i + 1,
    docId: c.docId,
    page: c.page,
    refText: `p.${c.page}`,
    snippet: c.snippet,
  }));

  // ── Retry handlers ─────────────────────────────────────────────────────
  const handleHomeRetry = useCallback(() => {
    void recent.refetch?.();
  }, [recent]);

  const handleResultsRetry = useCallback(() => {
    // The hook's internal state resets when the queryKey changes.
    // Re-pushing the same URL triggers a fresh render + re-enable.
    if (q) {
      const params = new URLSearchParams();
      params.set('q', q);
      if (mode && mode !== 'Semantic') params.set('mode', mode);
      router.push(`/knowledge-base/global?${params.toString()}`);
    }
  }, [q, mode, router]);

  // ── Render ─────────────────────────────────────────────────────────────
  return (
    <div className="flex flex-col gap-6 w-full">
      <HeroSearch
        initialQuery={q}
        initialMode={mode ?? 'Semantic'}
        onSubmit={pushUrl}
        onClear={clearUrl}
        labels={LABELS.hero}
      />

      {isHomeBranch ? (
        <KbHomeDesktop
          recentDocs={recentDocs}
          isLoading={recent.isLoading}
          error={(recent.error as Error | null) ?? null}
          labels={LABELS.home}
          onRetry={handleHomeRetry}
        />
      ) : (
        <KbSearchResultsDesktop
          query={q}
          results={search.results}
          hasMore={search.hasMore}
          isLoading={search.isLoading}
          isFetchingNextPage={search.isFetchingNextPage}
          error={search.error}
          onLoadMore={search.fetchNextPage}
          labels={LABELS.results}
          onRetry={handleResultsRetry}
          onResultClick={r => openViewer({ docId: r.docId, page: r.pageNumber ?? 1 })}
        />
      )}

      {/* Phase 2: PDF viewer — only when envelope is ready */}
      {viewerDoc && (
        <KbDocViewerDesktopLazy
          doc={viewerDoc}
          activePage={resolvedPage}
          citations={viewerCitations}
          labels={LABELS.viewer}
          onPageChange={p => openViewer({ docId: viewerDoc.id, page: p })}
          onClose={closeViewer}
        />
      )}

      {/* Phase 2: Ask drawer */}
      {askParam && (
        <DrawerShellLazy
          state={askStream.state}
          suggestions={LABELS.drawer.suggestions}
          labels={LABELS.drawer.shell}
          onAsk={query => askStream.ask(query)}
          onStop={askStream.stop}
          onReset={askStream.reset}
          onClose={closeDrawer}
          onEmptyCta={() => router.push('/library')}
          onCitationClick={openViewer}
        />
      )}
    </div>
  );
}
