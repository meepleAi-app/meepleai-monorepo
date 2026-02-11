/**
 * EditLedgerEntryModal Component
 * Issue #3722 - Manual Ledger CRUD
 *
 * Level 1 edit: description, category, metadata.
 */

'use client';

import { useState, useEffect } from 'react';

import { PencilIcon, XIcon } from 'lucide-react';

import type {
  LedgerEntryDto,
  UpdateLedgerEntryRequest,
  LedgerCategory,
} from '@/lib/api/schemas/financial-ledger.schemas';
import { LEDGER_CATEGORY_MAP } from '@/lib/api/schemas/financial-ledger.schemas';
import { cn } from '@/lib/utils';

export interface EditLedgerEntryModalProps {
  entry: LedgerEntryDto | null;
  onClose: () => void;
  onSubmit: (id: string, request: UpdateLedgerEntryRequest) => Promise<void>;
}

const CATEGORIES: { value: number; label: LedgerCategory }[] = [
  { value: 0, label: 'Subscription' },
  { value: 1, label: 'TokenPurchase' },
  { value: 2, label: 'TokenUsage' },
  { value: 3, label: 'PlatformFee' },
  { value: 4, label: 'Refund' },
  { value: 5, label: 'Operational' },
  { value: 6, label: 'Marketing' },
  { value: 7, label: 'Infrastructure' },
  { value: 8, label: 'Other' },
];

export function EditLedgerEntryModal({ entry, onClose, onSubmit }: EditLedgerEntryModalProps) {
  const [description, setDescription] = useState('');
  const [category, setCategory] = useState(0);
  const [metadata, setMetadata] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (entry) {
      setDescription(entry.description ?? '');
      setCategory(entry.category);
      setMetadata(entry.metadata ?? '');
      setError(null);
    }
  }, [entry]);

  if (!entry) return null;

  const handleSubmit = async () => {
    setSubmitting(true);
    setError(null);
    try {
      const request: UpdateLedgerEntryRequest = {};
      if (description !== (entry.description ?? '')) {
        request.description = description || null;
      }
      if (category !== entry.category) {
        request.category = category;
      }
      if (metadata !== (entry.metadata ?? '')) {
        request.metadata = metadata || null;
      }

      await onSubmit(entry.id, request);
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to update entry');
    } finally {
      setSubmitting(false);
    }
  };

  const currentCategory = LEDGER_CATEGORY_MAP[entry.category] ?? 'Unknown';

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={onClose}
      data-testid="edit-ledger-modal"
    >
      <div
        className="w-full max-w-lg rounded-2xl border bg-white dark:bg-zinc-900 border-zinc-200/50 dark:border-zinc-700/50 p-6 shadow-xl mx-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between mb-5">
          <div className="flex items-center gap-2">
            <PencilIcon className="h-5 w-5 text-blue-500" />
            <h3 className="font-semibold text-zinc-900 dark:text-zinc-100">Edit Ledger Entry</h3>
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1 hover:bg-muted transition-colors"
            data-testid="close-edit-modal"
          >
            <XIcon className="h-4 w-4" />
          </button>
        </div>

        {/* Read-only info */}
        <div className="mb-4 rounded-lg bg-zinc-50 dark:bg-zinc-800/50 p-3 text-sm text-zinc-500 dark:text-zinc-400">
          Editing entry from {new Date(entry.date).toLocaleDateString('en-US', { year: 'numeric', month: 'short', day: 'numeric' })} &mdash; Current category: {currentCategory}
        </div>

        <div className="space-y-4">
          {/* Category */}
          <div>
            <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Category</label>
            <select
              value={category}
              onChange={(e) => setCategory(Number(e.target.value))}
              className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm"
              data-testid="edit-category"
            >
              {CATEGORIES.map((c) => (
                <option key={c.value} value={c.value}>{c.label}</option>
              ))}
            </select>
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Description</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="Entry description"
              maxLength={500}
              rows={2}
              className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm resize-none"
              data-testid="edit-description"
            />
          </div>

          {/* Metadata */}
          <div>
            <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Metadata (optional)</label>
            <textarea
              value={metadata}
              onChange={(e) => setMetadata(e.target.value)}
              placeholder='e.g. {"invoice": "INV-001"}'
              maxLength={4000}
              rows={2}
              className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm resize-none font-mono"
              data-testid="edit-metadata"
            />
          </div>
        </div>

        {/* Error */}
        {error && (
          <div className="mt-4 text-sm text-red-600 dark:text-red-400" data-testid="edit-entry-error">
            {error}
          </div>
        )}

        {/* Actions */}
        <div className="flex justify-end gap-2 mt-5">
          <button
            onClick={onClose}
            className="rounded-lg border border-zinc-200 dark:border-zinc-700 px-4 py-2 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
          >
            Cancel
          </button>
          <button
            onClick={handleSubmit}
            disabled={submitting}
            className={cn(
              'rounded-lg bg-blue-500 hover:bg-blue-600 px-4 py-2 text-sm font-medium text-white transition-colors disabled:opacity-50'
            )}
            data-testid="submit-edit-entry"
          >
            {submitting ? 'Saving...' : 'Save Changes'}
          </button>
        </div>
      </div>
    </div>
  );
}
