/**
 * KbHubContent — orchestrator for `/library/[gameId]/kb` (Issue #1481).
 *
 * Wires the kb-hub presentational components to existing user-side BE endpoints:
 *   - useUserKbStatus    → documentCount, coverageLevel, coverageScore
 *   - useGamePdfs        → PDF list
 *   - useReindexKb       → full re-index mutation
 *   - useRebuildRaptor   → RAPTOR rebuild mutation (Pro tier — backend enforces)
 *   - useDeletePdf       → delete a PDF (Owner endpoint)
 *
 * Deferred props (P83) — chunks, embeddings, lastReindex, raptorLastRebuild,
 * cost history, PDF row status/chunks, RAPTOR tier — are not wired here.
 * Components hide their corresponding UI when the prop is undefined.
 */

'use client';

import { useState, type ReactElement } from 'react';

import { toast } from 'sonner';

import {
  ActionsMenu,
  DeleteDialog,
  EmptyState,
  HubDefault,
  RaptorPanel,
  ReindexModal,
  type ActionsMenuLabels,
  type DeleteDialogLabels,
  type EmptyStateLabels,
  type HubDefaultLabels,
  type KbPdf,
  type PdfAction,
  type PdfStatus,
  type RaptorPanelLabels,
  type ReindexCostRow,
  type ReindexModalLabels,
  type ReindexPhase,
} from '@/components/features/kb-hub';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  useDeletePdf,
  useGamePdfs,
  useReindexKb,
  useRebuildRaptor,
  useUserKbStatus,
} from '@/hooks/queries/useKbHub';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';
import { useTranslation } from '@/hooks/useTranslation';
import type { GamePdfDto } from '@/lib/api/schemas/pdf.schemas';

interface KbHubContentProps {
  readonly gameId: string;
}

/**
 * Static cost breakdown rows for the re-index estimate table.
 * Real per-game estimates require BE schema enrichment (deferred follow-up issue).
 */
const STATIC_REINDEX_COST_ROWS: ReadonlyArray<ReindexCostRow> = [
  { key: 'chunks', label: 'Chunks da re-embed', value: '—', unit: '' },
  { key: 'tokensPerChunk', label: 'Tokens per chunk (avg)', value: '~320', unit: 'tokens' },
  { key: 'tokensTotal', label: 'Tokens totali stimati', value: '—', unit: 'tokens' },
  { key: 'costPer1M', label: 'Costo per 1M tokens', value: '$0.0001', unit: '/1M' },
  { key: 'estimatedCost', label: 'Costo stimato', value: '~$0.45', bold: true },
  { key: 'estimatedDuration', label: 'Durata stimata', value: '3–5 min', bold: true },
];

function formatRelativeDate(_iso: string): string {
  // Lightweight relative formatter — production uses date-fns formatDistance().
  // Replaced with date-fns when wireup expands. For MVP we display the upload date as-is
  // using the runtime locale (no hardcoded it-IT — caller's IntlProvider drives format).
  return new Date(_iso).toLocaleDateString();
}

function formatFileSize(bytes: number): string {
  if (bytes < 1024) return `${bytes} B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(1)} KB`;
  if (bytes < 1024 * 1024 * 1024) return `${(bytes / (1024 * 1024)).toFixed(1)} MB`;
  return `${(bytes / (1024 * 1024 * 1024)).toFixed(1)} GB`;
}

/**
 * F6 #1974 (audit 2026-06-07): map the BE `GamePdfDto.ProcessingState`
 * (Pending|Uploading|Extracting|Chunking|Embedding|Indexing|Ready|Failed)
 * to the FE `PdfStatus` union (ready|indexing|stale|failed) so PdfRow
 * can render a colored status badge per the mockup.
 *
 * Mapping rationale: every transient pipeline phase collapses to
 * "indexing" because the FE only renders one in-progress badge. "Ready"
 * is the only success terminal; "Failed" surfaces explicitly so the
 * user can retry. Unknown values fall back to "indexing" to avoid a
 * confidently-wrong success state when the BE adds a new phase later.
 * The "stale" badge has no BE source today — reserved for a future
 * Issue when re-embedding gates land.
 */
function mapProcessingStateToPdfStatus(state: string): PdfStatus {
  switch (state) {
    case 'Ready':
      return 'ready';
    case 'Failed':
      return 'failed';
    case 'Pending':
    case 'Uploading':
    case 'Extracting':
    case 'Chunking':
    case 'Embedding':
    case 'Indexing':
    default:
      return 'indexing';
  }
}

function mapPdfs(pdfs: ReadonlyArray<GamePdfDto>): ReadonlyArray<KbPdf> {
  return pdfs.map(p => ({
    id: p.id,
    name: p.name,
    sizeFormatted: formatFileSize(p.fileSizeBytes),
    uploadedAtRelative: formatRelativeDate(p.uploadedAt),
    // F6 #1974: wire BE ProcessingState → PdfRow status badge.
    status: mapProcessingStateToPdfStatus(p.processingState),
    // chunks deferred (P83 — BE schema doesn't expose per-doc chunk counts yet)
  }));
}

export function KbHubContent({ gameId }: KbHubContentProps): ReactElement {
  const { t } = useTranslation();
  const statusQuery = useUserKbStatus(gameId);
  const pdfsQuery = useGamePdfs(gameId);
  const reindexMutation = useReindexKb(gameId);
  const _raptorMutation = useRebuildRaptor(gameId);
  const deleteMutation = useDeletePdf(gameId);
  // F4 #1974 (audit 2026-06-07): pull the game title from the library detail
  // hook (shared QueryKey with the layout — TanStack dedups) so the KB hub
  // renders "Catan" instead of the cc1678e8… UUID when the BE status payload
  // omits `gameTitle`. `useLibraryGameDetail` already falls back to the
  // shared-catalog title for games not in the user's library.
  const gameDetailQuery = useLibraryGameDetail(gameId);

  const [reindexOpen, setReindexOpen] = useState(false);
  const [reindexPhase, setReindexPhase] = useState<ReindexPhase>('confirm');
  const [actionsMenuPdf, setActionsMenuPdf] = useState<GamePdfDto | null>(null);
  const [deletePdfTarget, setDeletePdfTarget] = useState<GamePdfDto | null>(null);

  // ─── Loading ──────────────────────────────────────────
  if (statusQuery.isLoading || pdfsQuery.isLoading) {
    return (
      <div data-slot="kb-hub-skeleton" className="container mx-auto max-w-6xl space-y-4 px-4 py-8">
        <Skeleton className="h-32 w-full" />
        <Skeleton className="h-64 w-full" />
        <Skeleton className="h-40 w-full" />
      </div>
    );
  }

  // ─── Error ────────────────────────────────────────────
  if (statusQuery.isError || pdfsQuery.isError) {
    return (
      <div className="container mx-auto max-w-3xl px-4 py-8">
        <Alert variant="destructive" data-slot="kb-hub-error">
          <AlertDescription>{t('pages.library.gameDetail.kb.errors.loadFailed')}</AlertDescription>
        </Alert>
      </div>
    );
  }

  const status = statusQuery.data;
  const pdfs = pdfsQuery.data ?? [];
  // F4 #1974: title resolution order
  //   1. library-detail (preferred — shared with the layout via TanStack dedup)
  //   2. KB status.gameId (only ever the UUID — last resort, signals BE schema
  //      gap rather than a real title; surfaced as the literal id when nothing
  //      else is available so consumers see the bug instead of a silent blank).
  //   3. route param (defensive — should never differ from gameId in BE)
  const gameTitle = gameDetailQuery.data?.gameTitle ?? status?.gameId ?? gameId;

  // #1816 P3-7 Phase 2 — derived from the two independent BE endpoints:
  //   - useGamePdfs returns rows from `pdf_documents` (uploaded files)
  //   - useUserKbStatus.isIndexed reflects pgvector chunk presence (indexed)
  // When at least one file exists but pgvector indexing has not produced any
  // chunk yet, surface the audit-flagged "0 Documenti / Copertura: Nessuna"
  // state as an explicit "Indicizzazione in corso" badge.
  const indexingPending = pdfs.length > 0 && status?.isIndexed === false;

  // ─── Labels (caller-side i18n resolution) ─────────────
  const emptyLabels: EmptyStateLabels = {
    title: t('pages.library.gameDetail.kb.empty.title'),
    description: t('pages.library.gameDetail.kb.empty.description'),
    ctaLabel: t('pages.library.gameDetail.kb.empty.ctaLabel'),
    supportedFormats: t('pages.library.gameDetail.kb.empty.supportedFormats'),
  };

  const hubLabels: HubDefaultLabels = {
    headerSubtitle: t('pages.library.gameDetail.kb.header.titleSuffix'),
    uploadCta: t('pages.library.gameDetail.kb.header.uploadCta'),
    reindexAllCta: t('pages.library.gameDetail.kb.header.reindexAllCta'),
    // F10 #1974: bottom drop-zone CTA — discoverable upload affordance.
    dropZoneCta: t('pages.library.gameDetail.kb.header.dropZoneCta'),
    indexingBadge: t('pages.library.gameDetail.kb.stats.indexingBadge'),
    indexingDescription: t('pages.library.gameDetail.kb.stats.indexingDescription'),
    statsStrip: {
      docs: t('pages.library.gameDetail.kb.stats.docs', { count: status?.documentCount ?? 0 }),
      chunks: t('pages.library.gameDetail.kb.stats.chunksTemplate'),
      embeddings: t('pages.library.gameDetail.kb.stats.embeddingsTemplate'),
      lastReindex: t('pages.library.gameDetail.kb.stats.lastReindexTemplate'),
      coverage: t('pages.library.gameDetail.kb.stats.coverageStripLabel'),
    },
    coverage: {
      None: t('pages.library.gameDetail.kb.stats.coverage.None'),
      Basic: t('pages.library.gameDetail.kb.stats.coverage.Basic'),
      Standard: t('pages.library.gameDetail.kb.stats.coverage.Standard'),
      Complete: t('pages.library.gameDetail.kb.stats.coverage.Complete'),
    },
    columnHeaders: {
      document: t('pages.library.gameDetail.kb.stats.columnHeaders.document'),
      status: t('pages.library.gameDetail.kb.stats.columnHeaders.status'),
      uploaded: t('pages.library.gameDetail.kb.stats.columnHeaders.uploaded'),
    },
    pdfRow: {
      openCta: t('pages.library.gameDetail.kb.pdfRow.openCta'),
      openAria: t('pages.library.gameDetail.kb.pdfRow.openAria'),
      chunksLabel: t('pages.library.gameDetail.kb.pdfRow.chunksTemplate'),
      status: {
        ready: t('pages.library.gameDetail.kb.pdfRow.status.ready'),
        indexing: t('pages.library.gameDetail.kb.pdfRow.status.indexing'),
        stale: t('pages.library.gameDetail.kb.pdfRow.status.stale'),
        failed: t('pages.library.gameDetail.kb.pdfRow.status.failed'),
      },
    },
  };

  // F7 #1974: KbStatsCard labels removed from the user-facing kb page —
  // the same data lives in the HubDefault stats strip. Translation keys
  // under `pages.library.gameDetail.kb.stats.*` stay in the locale files
  // because the KbStatsCard component is still consumed by admin / dev
  // surfaces (see `@/components/features/kb-hub` barrel exports).

  const raptorLabels: RaptorPanelLabels = {
    title: t('pages.library.gameDetail.kb.raptor.title'),
    description: t('pages.library.gameDetail.kb.raptor.description'),
    lockedBadge: t('pages.library.gameDetail.kb.raptor.lockedBadge'),
    activeBadge: t('pages.library.gameDetail.kb.raptor.activeBadge'),
    lockedNote: t('pages.library.gameDetail.kb.raptor.lockedNote'),
    upgradeCta: t('pages.library.gameDetail.kb.raptor.upgradeCta'),
    upgradeLink: t('pages.library.gameDetail.kb.raptor.upgradeLink'),
    rebuildCta: t('pages.library.gameDetail.kb.raptor.rebuildCta'),
    metrics: {
      lastRebuild: t('pages.library.gameDetail.kb.raptor.metrics.lastRebuild'),
      summaries: t('pages.library.gameDetail.kb.raptor.metrics.summaries'),
    },
    estimateLabel: t('pages.library.gameDetail.kb.raptor.estimateLabel'),
    estimateDescription: t('pages.library.gameDetail.kb.raptor.estimateDescription'),
  };

  const reindexLabels: ReindexModalLabels = {
    title: t('pages.library.gameDetail.kb.reindexModal.title'),
    subtitle: t('pages.library.gameDetail.kb.reindexModal.subtitle', {
      gameTitle,
      docCount: status?.documentCount ?? 0,
    }),
    costHeader: t('pages.library.gameDetail.kb.reindexModal.costHeader'),
    description: t('pages.library.gameDetail.kb.reindexModal.description'),
    reindexCta: t('pages.library.gameDetail.kb.reindexModal.reindexCta'),
    cancelCta: t('pages.library.gameDetail.kb.reindexModal.cancelCta'),
    runningTitle: t('pages.library.gameDetail.kb.reindexModal.runningTitle'),
    jobIdLabel: t('pages.library.gameDetail.kb.reindexModal.jobIdLabel'),
    progressTemplate: t('pages.library.gameDetail.kb.reindexModal.progressTemplate'),
    doneTitle: t('pages.library.gameDetail.kb.reindexModal.doneTitle'),
    doneSummaryTemplate: t('pages.library.gameDetail.kb.reindexModal.doneSummaryTemplate'),
    closeCta: t('pages.library.gameDetail.kb.reindexModal.closeCta'),
  };

  const deleteLabels: DeleteDialogLabels = {
    title: t('pages.library.gameDetail.kb.delete.title'),
    subtitlePrefix: t('pages.library.gameDetail.kb.delete.subtitlePrefix'),
    listHeader: t('pages.library.gameDetail.kb.delete.listHeader'),
    warning: t('pages.library.gameDetail.kb.delete.warning'),
    deleteCta: t('pages.library.gameDetail.kb.delete.deleteCta'),
    cancelCta: t('pages.library.gameDetail.kb.delete.cancelCta'),
  };

  const actionsMenuLabels: ActionsMenuLabels = {
    headerSubtitle: t('pages.library.gameDetail.kb.actionsMenu.headerSubtitle'),
    actions: {
      open: {
        label: t('pages.library.gameDetail.kb.actionsMenu.open.label'),
        description: t('pages.library.gameDetail.kb.actionsMenu.open.description'),
        icon: t('pages.library.gameDetail.kb.actionsMenu.open.icon'),
      },
      reindex: {
        label: t('pages.library.gameDetail.kb.actionsMenu.reindex.label'),
        description: t('pages.library.gameDetail.kb.actionsMenu.reindex.description'),
        icon: t('pages.library.gameDetail.kb.actionsMenu.reindex.icon'),
      },
      cost: {
        label: t('pages.library.gameDetail.kb.actionsMenu.cost.label'),
        description: t('pages.library.gameDetail.kb.actionsMenu.cost.description'),
        icon: t('pages.library.gameDetail.kb.actionsMenu.cost.icon'),
      },
      move: {
        label: t('pages.library.gameDetail.kb.actionsMenu.move.label'),
        description: t('pages.library.gameDetail.kb.actionsMenu.move.description'),
        icon: t('pages.library.gameDetail.kb.actionsMenu.move.icon'),
      },
      delete: {
        label: t('pages.library.gameDetail.kb.actionsMenu.delete.label'),
        description: t('pages.library.gameDetail.kb.actionsMenu.delete.description'),
        icon: t('pages.library.gameDetail.kb.actionsMenu.delete.icon'),
      },
    },
  };

  // ─── Action handlers ──────────────────────────────────
  const isEmpty = pdfs.length === 0;
  const game = { title: gameTitle, emoji: '📚' };

  const handleUpload = (): void => {
    // Upload flow is owned by another route; CTA acts as a no-op placeholder for MVP.
    // Real wireup will route to the existing PDF upload page when the link is decided.
  };

  const handlePdfActionTrigger = (pdfId: string): void => {
    const target = pdfs.find(p => p.id === pdfId) ?? null;
    setActionsMenuPdf(target);
  };

  const handleActionSelect = (action: PdfAction): void => {
    const current = actionsMenuPdf;
    setActionsMenuPdf(null);
    if (!current) return;
    if (action === 'delete') {
      setDeletePdfTarget(current);
    }
    // Other actions (open/reindex/cost/move) are deferred to follow-up wiring.
  };

  const handleReindexConfirm = async (): Promise<void> => {
    setReindexPhase('running');
    try {
      await reindexMutation.mutateAsync();
      setReindexPhase('done');
    } catch {
      // Surface the failure to the user; without this the modal closes silently
      // and the operation appears to vanish.
      toast.error(t('pages.library.gameDetail.kb.errors.reindexFailed'));
      setReindexOpen(false);
      setReindexPhase('confirm');
    }
  };

  const handleDeleteConfirm = async (): Promise<void> => {
    if (!deletePdfTarget) return;
    try {
      await deleteMutation.mutateAsync(deletePdfTarget.id);
      // Only dismiss the dialog on success — keep it open on error so the user
      // sees the failure and can decide to retry or cancel.
      setDeletePdfTarget(null);
    } catch {
      toast.error(t('pages.library.gameDetail.kb.errors.deleteFailed'));
    }
  };

  return (
    <div className="container mx-auto max-w-6xl space-y-6 px-4 py-8">
      {isEmpty ? (
        <EmptyState gameTitle={gameTitle} labels={emptyLabels} onUpload={handleUpload} />
      ) : (
        <>
          <HubDefault
            game={game}
            documentCount={status?.documentCount ?? 0}
            coverageLevel={status?.coverageLevel ?? 'None'}
            pdfs={mapPdfs(pdfs)}
            labels={hubLabels}
            onUpload={handleUpload}
            onReindexAll={() => {
              setReindexPhase('confirm');
              setReindexOpen(true);
            }}
            onPdfAction={handlePdfActionTrigger}
            indexingPending={indexingPending}
          />

          {/*
            F7 #1974 (audit 2026-06-07): KbStatsCard removed from the
            primary `/library/[gameId]/kb` layout. The mockup ships the
            same `documentCount + coverage` info inside the HubDefault
            stats strip; rendering a separate card below was pure UI
            redundancy. The component itself stays in the
            `@/components/features/kb-hub` barrel for the admin /
            embedded surfaces that still want a dedicated card.
          */}

          <RaptorPanel tier="free" labels={raptorLabels} />
        </>
      )}

      <ReindexModal
        open={reindexOpen}
        phase={reindexPhase}
        labels={reindexLabels}
        costRows={STATIC_REINDEX_COST_ROWS}
        onConfirm={handleReindexConfirm}
        onClose={() => {
          setReindexOpen(false);
          setReindexPhase('confirm');
        }}
      />

      <DeleteDialog
        open={!!deletePdfTarget}
        pdfName={deletePdfTarget?.name ?? ''}
        labels={deleteLabels}
        onConfirm={handleDeleteConfirm}
        onCancel={() => setDeletePdfTarget(null)}
      />

      {actionsMenuPdf && (
        <ActionsMenu
          pdf={{
            name: actionsMenuPdf.name,
            sizeFormatted: formatFileSize(actionsMenuPdf.fileSizeBytes),
            uploadedAtRelative: formatRelativeDate(actionsMenuPdf.uploadedAt),
          }}
          labels={actionsMenuLabels}
          onSelect={handleActionSelect}
          open
          onOpenChange={open => !open && setActionsMenuPdf(null)}
        >
          <span data-slot="kb-hub-actions-menu-anchor" />
        </ActionsMenu>
      )}
    </div>
  );
}
