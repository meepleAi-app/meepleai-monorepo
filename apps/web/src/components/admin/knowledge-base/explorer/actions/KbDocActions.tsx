'use client';

import { useState } from 'react';

import Link from 'next/link';
import { toast } from 'sonner';

import {
  AdminConfirmationDialog,
  AdminConfirmationLevel,
} from '@/components/ui/admin/admin-confirmation-dialog';
import { useDeleteKbDoc, useReindexDoc } from '@/hooks/queries/useKbDocActions';
import { useKbDocConsumingAgents } from '@/hooks/queries/useKbDocConsumingAgents';
import { api } from '@/lib/api';

import { downloadAsFile } from '../utils/downloadAsFile';

export interface KbDocActionsProps {
  /** Document UUID */
  readonly docId: string;
  /** Original file name — used as typed-confirm phrase on delete */
  readonly fileName: string;
  /** Parent game UUID for cache invalidation; null when unknown / global KB */
  readonly gameId: string | null;
  /** Current processing status of the document */
  readonly processingStatus: 'queued' | 'processing' | 'ready' | 'failed';
}

/** Shared focus-visible ring classes applied to every bare button (a11y). */
const FOCUS_RING =
  'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2';

/**
 * KbDocActions — horizontal action-bar for the KB document detail panel.
 *
 * Five actions:
 *   1. Re-index   — POST reindex; disabled while processing/queued/pending
 *   2. Download   — <a download> anchor (session-cookie auth works)
 *   3. Delete     — Level2 typed-confirm dialog (phrase = fileName)
 *   4. Export JSON— exportDocChunks → downloadAsFile; disabled unless ready
 *   5. Used-by    — Link to the used-by tab
 *
 * Issue #1653 F3-FU-4 Task 6.
 */
export function KbDocActions({ docId, fileName, gameId, processingStatus }: KbDocActionsProps) {
  const [deleteOpen, setDeleteOpen] = useState(false);
  const [exportPending, setExportPending] = useState(false);

  // ── Mutation hooks ──────────────────────────────────────────────────────────
  const reindexMutation = useReindexDoc(docId);
  const deleteMutation = useDeleteKbDoc(gameId);

  // ── Lazy agent count (only when delete dialog is open) ─────────────────────
  const consumingAgentsQuery = useKbDocConsumingAgents({
    docId,
    enabled: deleteOpen,
  });
  const agentCount =
    deleteOpen && !consumingAgentsQuery.isLoading && !consumingAgentsQuery.isError
      ? (consumingAgentsQuery.data?.length ?? null)
      : null;

  // ── Handlers ────────────────────────────────────────────────────────────────
  const handleReindex = () => {
    reindexMutation.mutate(undefined, {
      onSuccess: () => toast.success('Re-index avviato'),
      onError: (err: Error) => toast.error(`Re-index fallito: ${err.message}`),
    });
  };

  const handleDelete = () => {
    deleteMutation.mutate(docId, {
      onSuccess: () => {
        toast.success(`Documento "${fileName}" eliminato`);
        setDeleteOpen(false);
      },
      onError: (err: Error) => toast.error(`Eliminazione fallita: ${err.message}`),
    });
  };

  const handleExportChunks = async () => {
    setExportPending(true);
    try {
      const chunks = await api.pdf.exportDocChunks(docId);
      downloadAsFile(JSON.stringify(chunks, null, 2), `${fileName}-chunks.json`);
    } catch (err) {
      toast.error(
        `Esportazione chunk fallita: ${err instanceof Error ? err.message : String(err)}`
      );
    } finally {
      setExportPending(false);
    }
  };

  // ── Warning message for delete dialog ──────────────────────────────────────
  const deleteWarning: string =
    deleteOpen && consumingAgentsQuery.isLoading
      ? 'Caricamento agent associati…'
      : agentCount === null
        ? 'Verifica degli agent in corso — riprova tra poco.'
        : agentCount > 0
          ? `Referenziato da ${agentCount} agent — verranno scollegati.`
          : 'Nessun agent referenzia questo documento.';

  const isReindexDisabled =
    processingStatus === 'processing' || processingStatus === 'queued' || reindexMutation.isPending;

  return (
    <div className="flex flex-wrap gap-1.5">
      {/* 1. Re-index */}
      <button
        type="button"
        onClick={handleReindex}
        disabled={isReindexDisabled}
        className={`px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70 disabled:opacity-50 disabled:cursor-not-allowed ${FOCUS_RING}`}
      >
        ⟳ Re-index
      </button>

      {/* 2. Download — plain anchor styled as button */}
      {}
      <a
        href={api.pdf.getPdfDownloadUrl(docId)}
        download
        className={`inline-flex items-center px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70 ${FOCUS_RING}`}
      >
        ⤓ Download
      </a>

      {/* 3. Delete */}
      <button
        type="button"
        onClick={() => setDeleteOpen(true)}
        className={`px-3 py-1.5 text-xs font-medium rounded-md bg-destructive text-destructive-foreground hover:bg-destructive/90 ${FOCUS_RING}`}
      >
        🗑 Elimina
      </button>

      {/* 4. Export chunks JSON */}
      <button
        type="button"
        onClick={() => void handleExportChunks()}
        disabled={processingStatus !== 'ready' || exportPending}
        className={`px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70 disabled:opacity-50 disabled:cursor-not-allowed ${FOCUS_RING}`}
      >
        ↧ Export chunks
      </button>

      {/* 5. Used-by link */}
      <Link
        href={`/admin/knowledge-base?doc=${docId}&tab=used-by`}
        className={`inline-flex items-center px-3 py-1.5 text-xs font-medium border border-border rounded-md hover:bg-muted/70 ${FOCUS_RING}`}
      >
        🔗 Agent
      </Link>

      {/* Delete confirmation dialog */}
      <AdminConfirmationDialog
        isOpen={deleteOpen}
        onClose={() => setDeleteOpen(false)}
        onConfirm={handleDelete}
        level={AdminConfirmationLevel.Level2}
        confirmPhrase={fileName}
        title="Elimina documento"
        message={`Stai per eliminare il documento "${fileName}" dalla knowledge base. L'operazione è irreversibile.`}
        warningMessage={deleteWarning}
        isLoading={deleteMutation.isPending}
      />
    </div>
  );
}
