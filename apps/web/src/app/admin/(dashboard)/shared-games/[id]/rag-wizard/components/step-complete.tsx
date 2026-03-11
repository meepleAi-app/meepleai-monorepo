/**
 * Step 4: Completo — Success summary
 *
 * Shows green checkmark, count of indexed vs pending documents, and close button.
 */

'use client';

import { CheckCircle2, AlertCircle } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';

import type { FileResult } from './rag-wizard';

// ── Props ───────────────────────────────────────────────────────────────

interface StepCompleteProps {
  results: FileResult[];
  onClose: () => void;
}

// ── Component ───────────────────────────────────────────────────────────

export function StepComplete({ results, onClose }: StepCompleteProps) {
  const succeeded = results.filter(r => r.result && !r.error);
  const failed = results.filter(r => r.error);
  const autoApprovedWithJob = succeeded.filter(r => r.result?.autoApproved && r.result?.processingJobId);
  const autoApprovedNoJob = succeeded.filter(r => r.result?.autoApproved && !r.result?.processingJobId);
  const pendingApproval = succeeded.filter(r => r.result && !r.result.autoApproved);

  return (
    <div className="flex flex-col items-center text-center space-y-6 py-4">
      {/* Main icon */}
      {failed.length === 0 ? (
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-green-100 dark:bg-green-900/30">
          <CheckCircle2 className="h-8 w-8 text-green-600 dark:text-green-400" />
        </div>
      ) : (
        <div className="flex h-16 w-16 items-center justify-center rounded-full bg-amber-100 dark:bg-amber-900/30">
          <AlertCircle className="h-8 w-8 text-amber-600 dark:text-amber-400" />
        </div>
      )}

      {/* Title */}
      <div>
        <h3 className="text-lg font-semibold">
          {failed.length === 0 ? 'Elaborazione completata!' : 'Elaborazione completata con errori'}
        </h3>
        <p className="text-sm text-muted-foreground mt-1">
          {results.length} file processati
        </p>
      </div>

      {/* Stats */}
      <div className="grid grid-cols-2 gap-4 w-full max-w-sm text-sm">
        {autoApprovedWithJob.length > 0 && (
          <div className="rounded-lg border bg-green-50 dark:bg-green-900/20 p-3">
            <p className="text-2xl font-bold text-green-700 dark:text-green-300">
              {autoApprovedWithJob.length}
            </p>
            <p className="text-xs text-green-600 dark:text-green-400">Indicizzati</p>
          </div>
        )}

        {autoApprovedNoJob.length > 0 && (
          <div className="rounded-lg border bg-amber-50 dark:bg-amber-900/20 p-3">
            <p className="text-2xl font-bold text-amber-700 dark:text-amber-300">
              {autoApprovedNoJob.length}
            </p>
            <p className="text-xs text-amber-600 dark:text-amber-400">Approvato (coda non disponibile)</p>
          </div>
        )}

        {pendingApproval.length > 0 && (
          <div className="rounded-lg border bg-amber-50 dark:bg-amber-900/20 p-3">
            <p className="text-2xl font-bold text-amber-700 dark:text-amber-300">
              {pendingApproval.length}
            </p>
            <p className="text-xs text-amber-600 dark:text-amber-400">In attesa approvazione</p>
          </div>
        )}

        {failed.length > 0 && (
          <div className="rounded-lg border bg-red-50 dark:bg-red-900/20 p-3">
            <p className="text-2xl font-bold text-red-700 dark:text-red-300">
              {failed.length}
            </p>
            <p className="text-xs text-red-600 dark:text-red-400">Errori</p>
          </div>
        )}
      </div>

      {/* Failed file details */}
      {failed.length > 0 && (
        <div className="w-full max-w-md space-y-2 text-left">
          <p className="text-xs font-medium text-muted-foreground uppercase tracking-wider">
            File con errori
          </p>
          {failed.map(r => (
            <div
              key={r.fileName}
              className="flex items-start gap-2 rounded-lg border border-red-200 dark:border-red-800 bg-red-50 dark:bg-red-900/20 px-3 py-2 text-sm"
            >
              <AlertCircle className="h-4 w-4 text-red-500 shrink-0 mt-0.5" />
              <div className="min-w-0">
                <p className="font-medium truncate">{r.fileName}</p>
                <p className="text-xs text-red-600 dark:text-red-400">{r.error}</p>
              </div>
            </div>
          ))}
        </div>
      )}

      {/* Close button */}
      <Button onClick={onClose} className="mt-2">
        Chiudi
      </Button>
    </div>
  );
}
