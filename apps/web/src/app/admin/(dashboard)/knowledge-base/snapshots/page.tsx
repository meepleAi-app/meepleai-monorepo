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

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
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
          ? 'bg-red-50 border-red-200 dark:bg-red-900/20 dark:border-red-800'
          : 'bg-emerald-50 border-emerald-200 dark:bg-emerald-900/20 dark:border-emerald-800'
      }`}
    >
      <div className="flex items-start gap-3">
        <CheckCircle2Icon
          className={`h-5 w-5 mt-0.5 ${hasErrors ? 'text-red-500' : 'text-emerald-500'}`}
        />
        <div className="flex-1">
          <p
            className={`text-sm font-semibold ${hasErrors ? 'text-red-700 dark:text-red-400' : 'text-emerald-700 dark:text-emerald-400'}`}
          >
            {hasErrors ? 'Restore completato con errori' : 'Restore completato con successo'}
          </p>
          <div className="mt-1 text-xs text-slate-600 dark:text-zinc-400 space-y-0.5">
            <p>
              Documenti: {result.imported} importati · {result.skipped} già presenti ·{' '}
              {result.failed} falliti
            </p>
            {result.reEmbedded > 0 && <p>{result.reEmbedded} documenti ri-embeddati</p>}
          </div>
          {result.errors.length > 0 && (
            <ul className="mt-2 text-xs text-red-600 dark:text-red-400 space-y-0.5">
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
    <div className="flex flex-col sm:flex-row sm:items-center gap-4 py-4 px-4 rounded-xl hover:bg-slate-50 dark:hover:bg-zinc-800/50 transition-colors">
      {/* Icon */}
      <div className="flex-shrink-0">
        <div
          className={`h-10 w-10 rounded-lg flex items-center justify-center ${
            isLatest ? 'bg-blue-100 dark:bg-blue-900/30' : 'bg-slate-100 dark:bg-zinc-700/50'
          }`}
        >
          <FileArchiveIcon
            className={`h-5 w-5 ${isLatest ? 'text-blue-600 dark:text-blue-400' : 'text-slate-500 dark:text-zinc-400'}`}
          />
        </div>
      </div>

      {/* Metadata */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2 flex-wrap">
          <p className="text-sm font-medium text-slate-900 dark:text-zinc-100 font-mono truncate">
            {snapshot.id}
          </p>
          {isLatest && (
            <span className="text-xs bg-blue-100 dark:bg-blue-900/30 text-blue-600 dark:text-blue-400 px-1.5 py-0.5 rounded font-medium">
              auto
            </span>
          )}
        </div>
        <div className="flex items-center gap-3 mt-0.5 text-xs text-slate-500 dark:text-zinc-400">
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
        <Button
          variant="outline"
          size="sm"
          onClick={() => onRestore(snapshot.id)}
          disabled={isRestoring || isDeleting}
          className="gap-1.5 text-xs"
        >
          <RotateCcwIcon className={`h-3.5 w-3.5 ${isRestoring ? 'animate-spin' : ''}`} />
          {isRestoring ? 'Restore...' : 'Ripristina'}
        </Button>
        {!isLatest && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => onDelete(snapshot.id)}
            disabled={isRestoring || isDeleting}
            className="gap-1.5 text-xs text-red-500 hover:text-red-600 hover:bg-red-50 dark:hover:bg-red-900/20"
          >
            <Trash2Icon className="h-3.5 w-3.5" />
          </Button>
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
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold text-slate-900 dark:text-zinc-100">Snapshot RAG</h1>
          <p className="text-sm text-slate-500 dark:text-zinc-400 mt-1">
            Gestisci i backup della Knowledge Base. Ripristina uno snapshot per evitare di
            rielaborare i PDF.
          </p>
        </div>
        <div className="flex items-center gap-2">
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
          <Button
            size="sm"
            onClick={handleCreateSnapshot}
            disabled={exportLoading}
            className="gap-2"
          >
            <PlusIcon className="h-4 w-4" />
            {exportLoading ? 'Creazione...' : 'Nuovo snapshot'}
          </Button>
        </div>
      </div>

      {/* Export error */}
      {exportError && (
        <div className="flex items-center gap-2 p-3 rounded-xl bg-red-50 border border-red-200 dark:bg-red-900/20 dark:border-red-800 text-sm text-red-600 dark:text-red-400">
          <AlertCircleIcon className="h-4 w-4 flex-shrink-0" />
          {exportError}
        </div>
      )}

      {/* Restore result banner */}
      {restoreResult && <RestoreResultBanner result={restoreResult} />}

      {/* Info box */}
      <div className="rounded-xl border border-blue-200 dark:border-blue-800 bg-blue-50 dark:bg-blue-900/20 p-4">
        <p className="text-sm text-blue-700 dark:text-blue-300">
          <strong>Come funziona:</strong> Lo snapshot{' '}
          <code className="font-mono text-xs bg-blue-100 dark:bg-blue-900/50 px-1 rounded">
            latest
          </code>{' '}
          viene aggiornato automaticamente dopo ogni indicizzazione PDF. Gli snapshot con data sono
          backup manuali completi. Ripristina per ricaricare chunks ed embeddings nel database senza
          rielaborare i PDF originali.
        </p>
      </div>

      {/* Snapshots list */}
      <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
        <CardHeader className="pb-2">
          <CardTitle className="text-base font-semibold flex items-center gap-2">
            <ArchiveIcon className="h-4 w-4 text-purple-500" />
            Snapshot disponibili
            {snapshots.length > 0 && (
              <span className="ml-auto text-sm font-normal text-slate-400 dark:text-zinc-500">
                {snapshots.length} snapshot
              </span>
            )}
          </CardTitle>
        </CardHeader>
        <CardContent className="px-2">
          {isLoading ? (
            <div className="space-y-2 p-2">
              {Array.from({ length: 4 }).map((_, i) => (
                <div
                  key={i}
                  className="h-16 rounded-xl bg-slate-100 dark:bg-zinc-700/50 animate-pulse"
                />
              ))}
            </div>
          ) : error ? (
            <div className="flex items-center gap-2 p-4 text-red-500 dark:text-red-400">
              <AlertCircleIcon className="h-4 w-4" />
              <span className="text-sm">Errore nel caricamento degli snapshot</span>
            </div>
          ) : snapshots.length === 0 ? (
            <div className="flex flex-col items-center gap-3 py-12 text-slate-400 dark:text-zinc-500">
              <ArchiveIcon className="h-10 w-10" />
              <p className="text-sm">Nessuno snapshot disponibile</p>
              <p className="text-xs text-center max-w-xs">
                Gli snapshot vengono creati automaticamente dopo ogni indexing PDF, oppure
                manualmente con il bottone &quot;Nuovo snapshot&quot;.
              </p>
            </div>
          ) : (
            <div className="divide-y divide-slate-100 dark:divide-zinc-700/50">
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
        </CardContent>
      </Card>

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
              className="bg-red-600 hover:bg-red-700 text-white"
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
