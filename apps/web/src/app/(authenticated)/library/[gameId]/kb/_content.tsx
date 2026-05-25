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
  KbStatsCard,
  RaptorPanel,
  ReindexModal,
  type ActionsMenuLabels,
  type DeleteDialogLabels,
  type EmptyStateLabels,
  type HubDefaultLabels,
  type KbStatsCardLabels,
  type KbPdf,
  type PdfAction,
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

function mapPdfs(pdfs: ReadonlyArray<GamePdfDto>): ReadonlyArray<KbPdf> {
  return pdfs.map(p => ({
    id: p.id,
    name: p.name,
    sizeFormatted: formatFileSize(p.fileSizeBytes),
    uploadedAtRelative: formatRelativeDate(p.uploadedAt),
    // status + chunks deferred (P83 — BE schema doesn't expose them yet)
  }));
}

export function KbHubContent({ gameId }: KbHubContentProps): ReactElement {
  const { t } = useTranslation();
  const statusQuery = useUserKbStatus(gameId);
  const pdfsQuery = useGamePdfs(gameId);
  const reindexMutation = useReindexKb(gameId);
  const _raptorMutation = useRebuildRaptor(gameId);
  const deleteMutation = useDeletePdf(gameId);

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
  const gameTitle = status?.gameId ?? gameId; // BE schema has no title; consumer falls back to id MVP

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

  const statsCardLabels: KbStatsCardLabels = {
    cardTitle: t('pages.library.gameDetail.kb.stats.cardTitle'),
    cardSubtitle: t('pages.library.gameDetail.kb.stats.cardSubtitle'),
    docsLabel: t('pages.library.gameDetail.kb.stats.docsLabel'),
    chunksLabel: t('pages.library.gameDetail.kb.stats.chunksLabel'),
    embeddingsLabel: t('pages.library.gameDetail.kb.stats.embeddingsLabel'),
    lastReindexLabel: t('pages.library.gameDetail.kb.stats.lastReindexLabel'),
    raptorLabel: t('pages.library.gameDetail.kb.stats.raptorLabel'),
    coverageLabel: t('pages.library.gameDetail.kb.stats.coverageLabel'),
    coverage: {
      None: t('pages.library.gameDetail.kb.stats.coverage.None'),
      Basic: t('pages.library.gameDetail.kb.stats.coverage.Basic'),
      Standard: t('pages.library.gameDetail.kb.stats.coverage.Standard'),
      Complete: t('pages.library.gameDetail.kb.stats.coverage.Complete'),
    },
    lifetimeCostLabel: t('pages.library.gameDetail.kb.stats.lifetimeCostLabel'),
    sparklineLabel: t('pages.library.gameDetail.kb.stats.sparklineLabel'),
    sparklineStart: t('pages.library.gameDetail.kb.stats.sparklineStart'),
    sparklineEnd: t('pages.library.gameDetail.kb.stats.sparklineEnd'),
  };

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
          />

          <KbStatsCard
            documentCount={status?.documentCount ?? 0}
            coverageLevel={status?.coverageLevel ?? 'None'}
            coverageScore={status?.coverageScore ?? 0}
            labels={statsCardLabels}
          />

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
