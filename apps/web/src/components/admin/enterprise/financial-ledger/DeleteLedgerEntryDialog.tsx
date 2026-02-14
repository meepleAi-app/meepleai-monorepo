/**
 * DeleteLedgerEntryDialog Component
 * Issue #3722 - Manual Ledger CRUD
 *
 * Level 2 delete confirmation dialog for manual ledger entries only.
 */

'use client';

import { useState } from 'react';

import { AlertTriangleIcon, XIcon } from 'lucide-react';

import type { LedgerEntryDto } from '@/lib/api/schemas/financial-ledger.schemas';
import {
  LEDGER_ENTRY_TYPE_MAP,
  LEDGER_CATEGORY_MAP,
} from '@/lib/api/schemas/financial-ledger.schemas';

export interface DeleteLedgerEntryDialogProps {
  entry: LedgerEntryDto | null;
  onClose: () => void;
  onConfirm: (id: string) => Promise<void>;
}

function formatCurrency(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}

export function DeleteLedgerEntryDialog({ entry, onClose, onConfirm }: DeleteLedgerEntryDialogProps) {
  const [deleting, setDeleting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!entry) return null;

  const handleConfirm = async () => {
    setDeleting(true);
    setError(null);
    try {
      await onConfirm(entry.id);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to delete entry');
    } finally {
      setDeleting(false);
    }
  };

  const typeName = LEDGER_ENTRY_TYPE_MAP[entry.type] ?? 'Unknown';
  const categoryName = LEDGER_CATEGORY_MAP[entry.category] ?? 'Unknown';

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={onClose}
      data-testid="delete-ledger-dialog"
    >
      <div
        className="w-full max-w-md rounded-2xl border bg-white dark:bg-zinc-900 border-zinc-200/50 dark:border-zinc-700/50 p-6 shadow-xl mx-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Warning icon + title */}
        <div className="flex items-center gap-3 mb-4">
          <div className="rounded-full bg-red-50 dark:bg-red-900/20 p-2.5">
            <AlertTriangleIcon className="h-5 w-5 text-red-600 dark:text-red-400" />
          </div>
          <div>
            <h3 className="font-semibold text-zinc-900 dark:text-zinc-100">Delete Ledger Entry</h3>
            <p className="text-sm text-zinc-500 dark:text-zinc-400">This action cannot be undone.</p>
          </div>
          <button
            onClick={onClose}
            className="ml-auto rounded-md p-1 hover:bg-muted transition-colors"
            data-testid="close-delete-dialog"
          >
            <XIcon className="h-4 w-4" />
          </button>
        </div>

        {/* Entry details */}
        <div className="rounded-lg bg-zinc-50 dark:bg-zinc-800/50 p-4 mb-4 text-sm space-y-1">
          <p className="text-zinc-700 dark:text-zinc-300">
            <span className="text-zinc-500 dark:text-zinc-400">Type:</span> {typeName}
          </p>
          <p className="text-zinc-700 dark:text-zinc-300">
            <span className="text-zinc-500 dark:text-zinc-400">Category:</span> {categoryName}
          </p>
          <p className="text-zinc-700 dark:text-zinc-300">
            <span className="text-zinc-500 dark:text-zinc-400">Amount:</span> {formatCurrency(entry.amount, entry.currency)}
          </p>
          {entry.description && (
            <p className="text-zinc-700 dark:text-zinc-300">
              <span className="text-zinc-500 dark:text-zinc-400">Description:</span> {entry.description}
            </p>
          )}
        </div>

        {/* Error */}
        {error && (
          <div className="mb-4 text-sm text-red-600 dark:text-red-400" data-testid="delete-entry-error">
            {error}
          </div>
        )}

        {/* Actions */}
        <div className="flex justify-end gap-2">
          <button
            onClick={onClose}
            className="rounded-lg border border-zinc-200 dark:border-zinc-700 px-4 py-2 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleConfirm}
            disabled={deleting}
            className="rounded-lg bg-red-500 hover:bg-red-600 px-4 py-2 text-sm font-medium text-white transition-colors disabled:opacity-50"
            data-testid="confirm-delete-entry"
          >
            {deleting ? 'Deleting...' : 'Delete Entry'}
          </button>
        </div>
      </div>
    </div>
  );
}
