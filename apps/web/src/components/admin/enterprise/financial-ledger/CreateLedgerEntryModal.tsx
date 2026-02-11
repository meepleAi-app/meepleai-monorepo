/**
 * CreateLedgerEntryModal Component
 * Issue #3722 - Manual Ledger CRUD
 *
 * Form modal for creating a new manual ledger entry.
 * Fields: date, type (Income/Expense), category, amount, currency, description.
 */

'use client';

import { useState } from 'react';

import { PlusIcon, XIcon } from 'lucide-react';

import type { CreateLedgerEntryRequest, LedgerEntryType, LedgerCategory } from '@/lib/api/schemas/financial-ledger.schemas';
import { cn } from '@/lib/utils';

export interface CreateLedgerEntryModalProps {
  open: boolean;
  onClose: () => void;
  onSubmit: (request: CreateLedgerEntryRequest) => Promise<void>;
}

const ENTRY_TYPES: { value: number; label: LedgerEntryType }[] = [
  { value: 0, label: 'Income' },
  { value: 1, label: 'Expense' },
];

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

export function CreateLedgerEntryModal({ open, onClose, onSubmit }: CreateLedgerEntryModalProps) {
  const [date, setDate] = useState(() => new Date().toISOString().slice(0, 10));
  const [type, setType] = useState(0);
  const [category, setCategory] = useState(0);
  const [amount, setAmount] = useState('');
  const [currency, setCurrency] = useState('EUR');
  const [description, setDescription] = useState('');
  const [submitting, setSubmitting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  if (!open) return null;

  const resetForm = () => {
    setDate(new Date().toISOString().slice(0, 10));
    setType(0);
    setCategory(0);
    setAmount('');
    setCurrency('EUR');
    setDescription('');
    setError(null);
  };

  const handleSubmit = async () => {
    const numAmount = parseFloat(amount);
    if (isNaN(numAmount) || numAmount <= 0) {
      setError('Please enter a valid amount greater than 0');
      return;
    }
    if (!date) {
      setError('Please select a date');
      return;
    }

    setSubmitting(true);
    setError(null);
    try {
      await onSubmit({
        date: new Date(date).toISOString(),
        type,
        category,
        amount: numAmount,
        currency,
        description: description || null,
      });
      resetForm();
      onClose();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to create entry');
    } finally {
      setSubmitting(false);
    }
  };

  return (
    <div
      className="fixed inset-0 z-50 flex items-center justify-center bg-black/50 backdrop-blur-sm"
      onClick={onClose}
      data-testid="create-ledger-modal"
    >
      <div
        className="w-full max-w-lg rounded-2xl border bg-white dark:bg-zinc-900 border-zinc-200/50 dark:border-zinc-700/50 p-6 shadow-xl mx-4"
        onClick={(e) => e.stopPropagation()}
      >
        {/* Header */}
        <div className="flex items-center justify-between mb-5">
          <div className="flex items-center gap-2">
            <PlusIcon className="h-5 w-5 text-emerald-500" />
            <h3 className="font-semibold text-zinc-900 dark:text-zinc-100">New Ledger Entry</h3>
          </div>
          <button
            onClick={onClose}
            className="rounded-md p-1 hover:bg-muted transition-colors"
            data-testid="close-create-modal"
          >
            <XIcon className="h-4 w-4" />
          </button>
        </div>

        <div className="space-y-4">
          {/* Date */}
          <div>
            <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Date</label>
            <input
              type="date"
              value={date}
              onChange={(e) => { setDate(e.target.value); setError(null); }}
              max={new Date().toISOString().slice(0, 10)}
              className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm"
              data-testid="entry-date"
            />
          </div>

          {/* Type + Category row */}
          <div className="grid grid-cols-2 gap-3">
            <div>
              <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Type</label>
              <select
                value={type}
                onChange={(e) => setType(Number(e.target.value))}
                className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm"
                data-testid="entry-type"
              >
                {ENTRY_TYPES.map((t) => (
                  <option key={t.value} value={t.value}>{t.label}</option>
                ))}
              </select>
            </div>
            <div>
              <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Category</label>
              <select
                value={category}
                onChange={(e) => setCategory(Number(e.target.value))}
                className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm"
                data-testid="entry-category"
              >
                {CATEGORIES.map((c) => (
                  <option key={c.value} value={c.value}>{c.label}</option>
                ))}
              </select>
            </div>
          </div>

          {/* Amount + Currency row */}
          <div className="grid grid-cols-3 gap-3">
            <div className="col-span-2">
              <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Amount</label>
              <input
                type="number"
                value={amount}
                onChange={(e) => { setAmount(e.target.value); setError(null); }}
                placeholder="0.00"
                min="0.01"
                step="0.01"
                className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm"
                data-testid="entry-amount"
              />
            </div>
            <div>
              <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Currency</label>
              <select
                value={currency}
                onChange={(e) => setCurrency(e.target.value)}
                className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm"
                data-testid="entry-currency"
              >
                <option value="EUR">EUR</option>
                <option value="USD">USD</option>
                <option value="GBP">GBP</option>
              </select>
            </div>
          </div>

          {/* Description */}
          <div>
            <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Description (optional)</label>
            <textarea
              value={description}
              onChange={(e) => setDescription(e.target.value)}
              placeholder="e.g. Monthly hosting fee"
              maxLength={500}
              rows={2}
              className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-2 text-sm resize-none"
              data-testid="entry-description"
            />
          </div>
        </div>

        {/* Error */}
        {error && (
          <div className="mt-4 text-sm text-red-600 dark:text-red-400" data-testid="create-entry-error">
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
            disabled={submitting || !amount}
            className={cn(
              'rounded-lg px-4 py-2 text-sm font-medium text-white transition-colors disabled:opacity-50',
              type === 0
                ? 'bg-emerald-500 hover:bg-emerald-600'
                : 'bg-red-500 hover:bg-red-600'
            )}
            data-testid="submit-create-entry"
          >
            {submitting ? 'Creating...' : 'Create Entry'}
          </button>
        </div>
      </div>
    </div>
  );
}
