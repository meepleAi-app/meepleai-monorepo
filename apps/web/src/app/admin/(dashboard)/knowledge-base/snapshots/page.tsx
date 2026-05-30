'use client';

/**
 * Admin KB Snapshots Page
 * Lists all RAG backup snapshots. Admin can restore or delete a snapshot.
 * Restore avoids re-processing PDFs from scratch.
 */

import { useState, useCallback } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArchiveIcon,
  RefreshCwIcon,
  AlertCircleIcon,
  Trash2Icon,
  RotateCcwIcon,
  CheckCircle2Icon,
  FileArchiveIcon,
  PlusIcon,
} from 'lucide-react';

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
import { Button } from '@/components/ui/primitives/button';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type {
  KbSnapshotInfo,
  KbImportResult,
} from '@/lib/api/schemas/admin-knowledge-base.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function formatSnapshotDate(dateStr: string | null): string {
  if (!dateStr) return 'Data sconosciuta';
  return new Date(dateStr).toLocaleString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function RestoreResultBanner({ result }: { result: KbImportResult }) {
  const hasErrors = result.errors.length > 0;
  return (
    <div
      className={`rounded-xl border p-4 ${
        hasErrors
          ? 'bg-entity-event/12 border-entity-event/30'
          : 'bg-entity-toolkit/12 border-entity-toolkit/30'
      }`}
    >
      <div className="flex items-start gap-3">
        <CheckCircle2Icon
          className={`h-5 w-5 mt-0.5 ${hasErrors ? 'text-entity-event' : 'text-entity-toolkit'}`}
        />
        <div className="flex-1">
          <p
            className={`text-sm font-semibold ${hasErrors ? 'text-entity-event' : 'text-entity-toolkit'}`}
          >
            {hasErrors ? 'Restore completato con errori' : 'Restore completato con successo'}
          </p>
          <div className="mt-1 text-xs text-muted-foreground space-y-0.5">
            <p>
              Documenti: {result.imported} importati · {result.skipped} già presenti ·{' '}
              {result.failed} falliti
            </p>
            {result.reEmbedded > 0 && <p>{result.reEmbedded} documenti ri-embeddati</p>}
          </div>
          {result.errors.length > 0 && (
            <ul className="mt-2 text-xs text-entity-event space-y-0.5">
              {result.errors.map((e, i) => (
                <li key={i}>• {e}</li>
              ))}
            </ul>
          )}
        </div>
      </div>
    </div>
  );
}

function SnapshotCard({
  snapshot,
  onRestore,
  onDelete,
  isRestoring,
  isDeleting,
}: {
  snapshot: KbSnapshotInfo;
  onRestore: (id: string) => void;
  onDelete: (id: string) => void;
  isRestoring: boolean;
  isDeleting: boolean;
}) {
  const isLatest = snapshot.id === 'latest';

  return (
    <div className="flex flex-col sm:flex-row sm:items-center gap-4 py-4 px-4 hover:bg-muted transition-colors">
      {/* Icon */}
      <div className="flex-shrink-0">
        <div
          className={`h-10 w-10 rounded-lg flex items-center justify-center ${
            isLatest ? 'bg-entity-chat/12' : 'bg-muted'
          }`}
        >
          <FileArchiveIcon
            className={`h-5 w-5 ${isLatest ? 'text-entity-chat' : 'text-muted-foreground'}`}
          />
        </div>
      </div>

      {/* Metadata */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <p className="text-sm font-medium text-foreground font-mono truncate">{snapshot.id}</p>
          {isLatest && (
            <span className="text-xs bg-entity-chat/12 text-entity-chat px-1.5 py-0.5 rounded font-mono font-bold">
              auto
            </span>
          )}
        </div>
        <div className="flex items-center gap-3 mt-0.5 text-xs text-muted-foreground">
          <span>{formatSnapshotDate(snapshot.exportedAt)}</span>
          {snapshot.totalDocuments !== null && (
            <span>
              {snapshot.totalDocuments} {snapshot.totalDocuments === 1 ? 'doc' : 'docs'}
            </span>
          )}
          {snapshot.totalChunks !== null && (
            <span>{snapshot.totalChunks.toLocaleString('it-IT')} chunks</span>
          )}
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2 flex-shrink-0">
        <button
          type="button"
          onClick={() => onRestore(snapshot.id)}
          disabled={isRestoring || isDeleting}
          className="inline-flex items-center gap-1.5 rounded-md border border-border/60 bg-card px-2 py-1 text-[11px] font-quicksand font-bold hover:bg-muted transition-colors disabled:opacity-50 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
        >
          <RotateCcwIcon className={`h-3.5 w-3.5 ${isRestoring ? 'animate-spin' : ''}`} />
          {isRestoring ? 'Restore...' : 'Ripristina'}
        </button>
        {!isLatest && (
          <button
            type="button"
            onClick={() => onDelete(snapshot.id)}
            disabled={isRestoring || isDeleting}
            className="inline-flex items-center gap-1.5 rounded-md border border-entity-event/55 bg-card px-2 py-1 text-[11px] font-quicksand font-bold text-entity-event hover:bg-entity-event/8 transition-colors disabled:opacity-50 disabled:cursor-not-allowed focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2"
          >
            <Trash2Icon className="h-3.5 w-3.5" />
          </button>
        )}
      </div>
    </div>
  );
}

export default function KbSnapshotsPage() {
  const queryClient = useQueryClient();
  const [restoreTarget, setRestoreTarget] = useState<string | null>(null);
  const [deleteTarget, setDeleteTarget] = useState<string | null>(null);
  const [restoreResult, setRestoreResult] = useState<KbImportResult | null>(null);
  const [exportLoading, setExportLoading] = useState(false);
  const [exportError, setExportError] = useState<string | null>(null);

  const { data, isLoading, error, refetch, isRefetching } = useQuery({
    queryKey: ['admin', 'kb-snapshots'],
    queryFn: () => adminClient.listKbSnapshots(),
    staleTime: 30_000,
  });

  const restoreMutation = useMutation({
    mutationFn: (snapshotId: string) =>
      adminClient.importKbSnapshot(`rag-exports/${snapshotId}`, false),
    onSuccess: result => {
      setRestoreResult(result);
      setRestoreTarget(null);
      queryClient.invalidateQueries({ queryKey: ['admin', 'kb-game-statuses'] });
    },
    onError: () => {
      setRestoreTarget(null);
    },
  });

  const deleteMutation = useMutation({
    mutationFn: (snapshotId: string) => adminClient.deleteKbSnapshot(snapshotId),
    onSuccess: () => {
      setDeleteTarget(null);
      queryClient.invalidateQueries({ queryKey: ['admin', 'kb-snapshots'] });
    },
  });

  const handleCreateSnapshot = useCallback(async () => {
    setExportLoading(true);
    setExportError(null);
    try {
      await adminClient.exportKbSnapshot(undefined, false);
      await refetch();
    } catch {
      setExportError('Errore durante la creazione dello snapshot');
    } finally {
      setExportLoading(false);
    }
  }, [refetch]);

  const snapshots = data?.snapshots ?? [];

  return (
    <div className="space-y-6">
      {/* Page toolbar */}
      <div className="flex justify-end gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => refetch()}
          disabled={isRefetching}
          className="gap-2"
        >
          <RefreshCwIcon className={`h-4 w-4 ${isRefetching ? 'animate-spin' : ''}`} />
          Aggiorna
        </Button>
        <Button size="sm" onClick={handleCreateSnapshot} disabled={exportLoading} className="gap-2">
          <PlusIcon className="h-4 w-4" />
          {exportLoading ? 'Creazione...' : 'Nuovo snapshot'}
        </Button>
      </div>

      {/* Export error */}
      {exportError && (
        <div className="flex items-center gap-2 p-3 rounded-xl bg-entity-event/12 border border-entity-event/30 text-sm text-entity-event">
          <AlertCircleIcon className="h-4 w-4 flex-shrink-0" />
          {exportError}
        </div>
      )}

      {/* Restore result banner */}
      {restoreResult && <RestoreResultBanner result={restoreResult} />}

      {/* Info box */}
      <div className="rounded-xl border border-entity-chat/30 bg-entity-chat/12 p-4">
        <p className="text-sm text-entity-chat">
          <strong>Come funziona:</strong> Lo snapshot{' '}
          <code className="font-mono text-xs bg-entity-chat/12 px-1 rounded">latest</code> viene
          aggiornato automaticamente dopo ogni indicizzazione PDF. Gli snapshot con data sono backup
          manuali completi. Ripristina per ricaricare chunks ed embeddings nel database senza
          rielaborare i PDF originali.
        </p>
      </div>

      {/* Snapshots list */}
      <section className="rounded-[10px] border border-border/60 bg-card overflow-hidden">
        <div className="flex items-center gap-2.5 border-b border-border/60 bg-background px-3.5 py-2.5">
          <ArchiveIcon className="h-3.5 w-3.5 text-entity-kb shrink-0" />
          <h2 className="font-quicksand text-[13px] font-extrabold text-foreground">
            Snapshot disponibili
          </h2>
          {snapshots.length > 0 && (
            <span className="font-mono text-[10px] text-muted-foreground ml-auto">
              {snapshots.length} snapshot
            </span>
          )}
        </div>

        {isLoading ? (
          <div className="space-y-2 p-3.5">
            {Array.from({ length: 4 }).map((_, i) => (
              <div key={i} className="h-16 rounded-xl bg-muted animate-pulse" />
            ))}
          </div>
        ) : error ? (
          <div className="flex items-center gap-2 p-4 text-entity-event">
            <AlertCircleIcon className="h-4 w-4" />
            <span className="text-sm">Errore nel caricamento degli snapshot</span>
          </div>
        ) : snapshots.length === 0 ? (
          <div className="flex flex-col items-center gap-3 py-12 text-muted-foreground">
            <ArchiveIcon className="h-10 w-10" />
            <p className="text-sm">Nessuno snapshot disponibile</p>
            <p className="text-xs text-center max-w-xs">
              Gli snapshot vengono creati automaticamente dopo ogni indexing PDF, oppure manualmente
              con il bottone &quot;Nuovo snapshot&quot;.
            </p>
          </div>
        ) : (
          <div className="divide-y divide-border/60">
            {snapshots.map(snapshot => (
              <SnapshotCard
                key={snapshot.id}
                snapshot={snapshot}
                onRestore={id => setRestoreTarget(id)}
                onDelete={id => setDeleteTarget(id)}
                isRestoring={restoreMutation.isPending && restoreTarget === snapshot.id}
                isDeleting={deleteMutation.isPending && deleteTarget === snapshot.id}
              />
            ))}
          </div>
        )}
      </section>

      {/* Restore confirm dialog */}
      <AlertDialog
        open={restoreTarget !== null && !restoreMutation.isPending}
        onOpenChange={open => {
          if (!open) setRestoreTarget(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Ripristina snapshot</AlertDialogTitle>
            <AlertDialogDescription>
              Vuoi ripristinare lo snapshot <strong className="font-mono">{restoreTarget}</strong>?
              <br />
              Questa operazione importerà i chunks e gli embeddings nel database. I documenti già
              presenti verranno saltati.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Annulla</AlertDialogCancel>
            <AlertDialogAction
              onClick={() => restoreTarget && restoreMutation.mutate(restoreTarget)}
            >
              Ripristina
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Delete confirm dialog */}
      <AlertDialog
        open={deleteTarget !== null}
        onOpenChange={open => {
          if (!open) setDeleteTarget(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Elimina snapshot</AlertDialogTitle>
            <AlertDialogDescription>
              Sei sicuro di voler eliminare lo snapshot{' '}
              <strong className="font-mono">{deleteTarget}</strong>?<br />
              Questa operazione è irreversibile.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Annulla</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              onClick={() => deleteTarget && deleteMutation.mutate(deleteTarget)}
            >
              Elimina
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
  );
}
