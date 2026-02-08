/**
 * AddCreditsModal Component
 * Issue #3692 - Token Management System
 *
 * Modal for adding EUR credits to the token balance.
 */

'use client';

import { useState } from 'react';

import { EuroIcon, XIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface AddCreditsModalProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (amount: number, note?: string) => Promise<void>;
}

const QUICK_AMOUNTS = [50, 100, 250, 500, 1000];

export function AddCreditsModal({ open, onClose, onSubmit }: AddCreditsModalProps) {
  const [amount, setAmount] = useState<string>('');
  const [note, setNote] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) return null;

  const handleSubmit = async () => {
    const numAmount = parseFloat(amount);
    if (isNaN(numAmount) || numAmount <= 0) {
      setError('Please enter a valid amount greater than 0');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await onSubmit(numAmount, note || undefined);
      onClose();
      setAmount('');
      setNote('');
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to add credits');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={onClose}
      data-testid="add-credits-modal"
    >
      <div
        className="w-full max-w-md rounded-2xl border bg-white dark:bg-zinc-900 border-zinc-200/50 dark:border-zinc-700/50 p-6 shadow-xl mx-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between mb-5">
          <div className="flex items-center gap-2">
            <EuroIcon className="h-5 w-5 text-amber-500" />
            <h3 className="font-semibold text-zinc-900 dark:text-zinc-100">Add Credits</h3>
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1 hover:bg-muted transition-colors"
            data-testid="close-credits-modal"
          >
            <XIcon className="h-4 w-4" />
          </button>
        </div>

        {/* Quick amounts */}
        <div className="flex flex-wrap gap-2 mb-4">
          {QUICK_AMOUNTS.map((qa) => (
            <button
              key={qa}
              onClick={() => setAmount(String(qa))}
              className={cn(
                'rounded-lg border px-3 py-1.5 text-sm font-medium transition-colors',
                amount === String(qa)
                  ? 'border-amber-500 bg-amber-50 dark:bg-amber-900/20 text-amber-700 dark:text-amber-300'
                  : 'border-zinc-200 dark:border-zinc-700 text-zinc-600 dark:text-zinc-400 hover:border-zinc-300 dark:hover:border-zinc-600'
              )}
              data-testid={`quick-amount-${qa}`}
            >
              &euro;{qa}
            </button>
          ))}
        </div>

        {/* Custom amount input */}
        <div className="mb-4">
          <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">
            Amount (EUR)
          </label>
          <div className="relative">
            <span className="absolute left-3 top-1/2 -translate-y-1/2 text-zinc-400">&euro;</span>
            <input
              type="number"
              value={amount}
              onChange={(e) => { setAmount(e.target.value); setError(null); }}
              placeholder="0.00"
              min="1"
              step="1"
              className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 pl-8 pr-3 py-2 text-sm"
              data-testid="credits-amount-input"
            />
          </div>
        </div>

        {/* Note */}
        <div className="mb-4">
          <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">
            Note (optional)
          </label>
          <input
            type="text"
            value={note}
            onChange={(e) => setNote(e.target.value)}
            placeholder="e.g. Monthly top-up"
            className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm"
            data-testid="credits-note-input"
          />
        </div>

        {/* Error */}
        {error && (
          <div className="mb-4 text-sm text-red-600 dark:text-red-400" data-testid="credits-error">
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
            onClick={handleSubmit}
            disabled={submitting || !amount}
            className="rounded-lg bg-amber-500 hover:bg-amber-600 disabled:opacity-50 px-4 py-2 text-sm font-medium text-white transition-colors"
            data-testid="submit-credits"
          >
            {submitting ? 'Adding...' : `Add \u20AC${amount || '0'}`}
          </button>
        </div>
      </div>
    </div>
  );
}
