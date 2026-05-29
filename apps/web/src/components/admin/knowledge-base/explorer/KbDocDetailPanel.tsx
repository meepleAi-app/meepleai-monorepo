/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber/emerald/rose chip palette + zinc dark (admin convention, DS-13c scope deferred to DS-16) */
'use client';

import { useSearchParams } from 'next/navigation';

import { useKbChunksList } from '@/hooks/queries/useKbChunksList';
import { useKbDocDetail } from '@/hooks/queries/useKbDocDetail';

import { KbDocActions } from './actions/KbDocActions';
import { IngestionPanel } from './ingestion/IngestionPanel';
import { KbDocDetailTabs, type KbDocTabKey } from './KbDocDetailTabs';
import { KbChunkSearch } from './search/KbChunkSearch';
import { UsedByPanel } from './used-by/UsedByPanel';

import type { SelectedDocMeta } from './explorer-types';

export interface KbDocDetailPanelProps {
  readonly docId: string | null;
  /** Metadata carried from the tree selection (title + gameId). Available even
   *  when the document is locked (HTTP 423, no body). T8 will consume this to
   *  render the action-bar in all states. */
  readonly selectedDocMeta?: SelectedDocMeta | null;
}

function formatDate(iso: string): string {
  const d = new Date(iso);
  return d.toLocaleDateString('it-IT', { year: 'numeric', month: '2-digit', day: '2-digit' });
}

function processingChipClass(status: string): string {
  switch (status) {
    case 'ready':
      return 'bg-emerald-500/10 text-emerald-700 dark:text-emerald-300 border-emerald-500/30';
    case 'processing':
    case 'queued':
      return 'bg-amber-500/10 text-amber-700 dark:text-amber-300 border-amber-500/30';
    case 'failed':
      return 'bg-rose-500/10 text-rose-700 dark:text-rose-300 border-rose-500/30';
    default:
      return 'bg-muted text-muted-foreground border-border';
  }
}

/**
 * KbDocDetailPanel — pannello destro dell'esploratore. Mostra hero + lista
 * chunk del documento selezionato. Costruito da zero (i componenti
 * `features/kb-detail/*` sono stub post-pivot 2026-05-10, non riusabili).
 *
 * 4 stati:
 *   - docId null     → placeholder "Seleziona un documento"
 *   - loading        → skeleton
 *   - locked (423)   → banner "Documento in elaborazione"
 *   - ready (200)    → hero + lista chunk infinite-cursor
 */

export function KbDocDetailPanel({ docId, selectedDocMeta }: KbDocDetailPanelProps) {
  const searchParams = useSearchParams();
  const activeTab: KbDocTabKey = (() => {
    const tab = searchParams?.get('tab');
    if (tab === 'ingestion') return 'ingestion';
    if (tab === 'used-by') return 'used-by';
    return 'overview';
  })();

  const detailQuery = useKbDocDetail({ docId: docId ?? undefined, enabled: docId !== null });
  const chunksQuery = useKbChunksList({
    docId: docId ?? undefined,
    limit: 50,
    enabled: docId !== null && detailQuery.data?.status === 'ready',
  });

  if (docId === null) {
    return (
      <div
        className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 p-8 text-center text-sm text-muted-foreground min-h-[200px] flex flex-col items-center justify-center gap-2"
        data-testid="kb-doc-detail-empty"
      >
        <span aria-hidden="true" className="text-3xl">
          📄
        </span>
        <p>Seleziona un documento dall&apos;alberatura a sinistra.</p>
      </div>
    );
  }

  if (detailQuery.isLoading) {
    return (
      <div
        data-testid="kb-doc-detail-loading"
        className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 p-6 animate-pulse min-h-[200px]"
      >
        <div className="h-6 w-2/3 bg-muted rounded mb-4" />
        <div className="h-4 w-1/2 bg-muted rounded mb-2" />
        <div className="h-4 w-1/3 bg-muted rounded" />
      </div>
    );
  }

  const envelope = detailQuery.data;

  if (envelope?.status === 'locked') {
    // Derive display name + gameId from selectedDocMeta (the tree passes these
    // even when the HTTP 423 body is null) — fall back to docId if unavailable.
    const lockedTitle = selectedDocMeta?.title ?? docId;
    const lockedGameId = selectedDocMeta?.gameId ?? null;

    // The Used-by tab is independent of doc readiness (it only needs the docId
    // to query agents whose KbCardIds contain it). Allow it to render during
    // processing — addresses the carry-forward gap flagged on PR #1668 (#1650).
    if (activeTab === 'used-by') {
      return (
        <div className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 overflow-hidden">
          <KbDocDetailTabs docId={docId} activeTab={activeTab} />
          <UsedByPanel docId={docId} />
        </div>
      );
    }

    // For all other tabs (overview / ingestion) while locked: render the full
    // shell so the action-bar (Re-index, Delete) is reachable — critical for
    // `failed` docs. The body shows the processing notice instead of content.
    return (
      <div className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 overflow-hidden">
        <KbDocDetailTabs docId={docId} activeTab={activeTab} />

        {/* Slim hero — title from meta + status chip */}
        <header className="p-5 border-b border-border/60 dark:border-zinc-700/60 bg-gradient-to-b from-amber-500/5 to-transparent">
          <div className="flex items-start gap-4">
            <span aria-hidden="true" className="text-3xl">
              📄
            </span>
            <div className="flex-1 min-w-0">
              <h2 className="font-quicksand font-bold text-lg truncate">{lockedTitle}</h2>
              <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground font-mono">
                <span
                  className={`inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${processingChipClass(envelope.processingStatus)}`}
                >
                  {envelope.processingStatus}
                </span>
              </div>
            </div>
          </div>

          {/* Action-bar — reachable for failed docs */}
          <div className="mt-4">
            <KbDocActions
              docId={docId}
              fileName={lockedTitle}
              gameId={lockedGameId}
              processingStatus={envelope.processingStatus}
            />
          </div>
        </header>

        {/* Processing notice in the body */}
        <div className="p-6">
          <div className="rounded-lg border border-amber-500/30 bg-amber-500/5 p-4">
            <h3 className="font-quicksand font-bold text-base text-amber-700 dark:text-amber-300 mb-1">
              Documento in elaborazione
            </h3>
            <p className="text-sm text-muted-foreground">
              Stato corrente: <span className="font-mono">{envelope.processingStatus}</span>. Il
              pannello sarà disponibile quando l&apos;indicizzazione sarà completa.
            </p>
          </div>
        </div>
      </div>
    );
  }

  if (!envelope || envelope.status !== 'ready') {
    return null; // nothing else to render (404 or unknown)
  }

  const doc = envelope.doc;
  const chunkPages = chunksQuery.data?.pages ?? [];
  const chunks = chunkPages.flatMap(p => p.items);

  return (
    <div className="border border-border/60 dark:border-zinc-700/60 rounded-lg bg-card/80 dark:bg-zinc-900/80 overflow-hidden">
      <KbDocDetailTabs docId={doc.id} activeTab={activeTab} />

      {activeTab === 'ingestion' && (
        <IngestionPanel docId={doc.id} chunkCount={doc.chunkCount} pageCount={doc.pageCount ?? 0} />
      )}

      {activeTab === 'used-by' && <UsedByPanel docId={doc.id} />}

      {activeTab === 'overview' && (
        <>
          {/* Hero */}
          <header className="p-5 border-b border-border/60 dark:border-zinc-700/60 bg-gradient-to-b from-amber-500/5 to-transparent">
            <div className="flex items-start gap-4">
              <span aria-hidden="true" className="text-3xl">
                📄
              </span>
              <div className="flex-1 min-w-0">
                <h2 className="font-quicksand font-bold text-lg truncate">{doc.title}</h2>
                <div className="mt-1 flex flex-wrap items-center gap-2 text-xs text-muted-foreground font-mono">
                  <span>{doc.gameName ?? 'KB globale'}</span>
                  <span aria-hidden="true">·</span>
                  <span>{doc.docType}</span>
                  <span aria-hidden="true">·</span>
                  <span>uploaded {formatDate(doc.uploadedAt)}</span>
                  <span
                    className={`ml-auto inline-flex items-center px-2 py-0.5 text-[10px] font-semibold rounded-full border ${processingChipClass(doc.processingStatus)}`}
                  >
                    {doc.processingStatus}
                  </span>
                </div>
              </div>
            </div>

            <dl className="mt-4 grid grid-cols-3 gap-2">
              <Stat label="Chunks" value={doc.chunkCount.toLocaleString('it-IT')} />
              <Stat label="Pagine" value={doc.pageCount?.toLocaleString('it-IT') ?? '—'} />
              <Stat label="Lingua" value={doc.language} />
            </dl>

            {/* Action-bar */}
            <div className="mt-4">
              <KbDocActions
                docId={doc.id}
                fileName={doc.title}
                gameId={doc.gameId}
                processingStatus={doc.processingStatus}
              />
            </div>
          </header>

          {/* Chunk similarity search */}
          <section className="px-4 pt-4">
            <KbChunkSearch docId={doc.id} chunkCount={doc.chunkCount} />
          </section>

          {/* Chunks */}
          <section className="p-4">
            <h3 className="font-quicksand font-semibold text-sm mb-2">Chunks</h3>
            <ul className="divide-y divide-border/60 dark:divide-zinc-700/60">
              {chunks.map(c => (
                <li key={c.id} className="py-2.5">
                  <div className="flex items-center gap-2 text-[10.5px] font-mono text-muted-foreground mb-0.5">
                    <code>c-{c.position.toString().padStart(4, '0')}</code>
                    {c.pageNumber !== null && <span>· p. {c.pageNumber}</span>}
                    {c.headingPath.length > 0 && (
                      <>
                        <span aria-hidden="true">·</span>
                        <span className="truncate" data-testid="kb-chunk-heading">
                          {c.headingPath.join(' › ')}
                        </span>
                      </>
                    )}
                  </div>
                  <p className="text-[12.5px] text-foreground leading-snug line-clamp-2">
                    {c.snippet}
                  </p>
                </li>
              ))}
            </ul>
            {chunksQuery.hasNextPage && (
              <button
                type="button"
                onClick={() => chunksQuery.fetchNextPage()}
                disabled={chunksQuery.isFetchingNextPage}
                className="mt-3 w-full text-center text-xs font-medium text-amber-700 dark:text-amber-300 border border-border/60 dark:border-zinc-700/60 rounded-md py-2 hover:bg-muted/70 disabled:opacity-60"
              >
                {chunksQuery.isFetchingNextPage ? 'Caricamento…' : 'Carica altri'}
              </button>
            )}
          </section>
        </>
      )}
    </div>
  );
}

function Stat({ label, value }: { label: string; value: string }) {
  return (
    <div className="bg-muted/40 dark:bg-zinc-800/40 border border-border/40 rounded-md px-2.5 py-1.5">
      <dt className="font-mono text-[9.5px] uppercase tracking-wider text-muted-foreground">
        {label}
      </dt>
      <dd className="font-quicksand text-[15px] font-bold">{value}</dd>
    </div>
  );
}
