/**
 * LedgerEntriesTable Component
 * Issue #3722 - Manual Ledger CRUD
 *
 * Displays paginated ledger entries with type/category/source labels.
 * Supports edit (Level 1) and delete (Level 2, manual only) actions.
 */

'use client';

import { PencilIcon, TrashIcon } from 'lucide-react';

import type { LedgerEntryDto } from '@/lib/api/schemas/financial-ledger.schemas';
import {
  LEDGER_ENTRY_TYPE_MAP,
  LEDGER_CATEGORY_MAP,
  LEDGER_SOURCE_MAP,
} from '@/lib/api/schemas/financial-ledger.schemas';
import { cn } from '@/lib/utils';

export interface LedgerEntriesTableProps {
  entries: LedgerEntryDto[];
  total: number;
  page: number;
  pageSize: number;
  loading: boolean;
  onEdit: (entry: LedgerEntryDto) => void;
  onDelete: (entry: LedgerEntryDto) => void;
  onPageChange: (page: number) => void;
}

function formatDate(dateStr: string): string {
  return new Date(dateStr).toLocaleDateString('en-US', {
    year: 'numeric',
    month: 'short',
    day: 'numeric',
  });
}

function formatCurrency(amount: number, currency: string): string {
  return new Intl.NumberFormat('en-US', {
    style: 'currency',
    currency,
    minimumFractionDigits: 2,
  }).format(amount);
}

export function LedgerEntriesTable({
  entries,
  total,
  page,
  pageSize,
  loading,
  onEdit,
  onDelete,
  onPageChange,
}: LedgerEntriesTableProps) {
  const totalPages = Math.max(1, Math.ceil(total / pageSize));
  const isManual = (entry: LedgerEntryDto) => LEDGER_SOURCE_MAP[entry.source] === 'Manual';

  if (loading) {
    return (
      <div className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 p-6" data-testid="ledger-table-loading">
        <div className="animate-pulse space-y-3">
          {Array.from({ length: 5 }).map((_, i) => (
            <div key={i} className="h-10 bg-muted/20 rounded" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-2xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white dark:bg-zinc-900 overflow-hidden" data-testid="ledger-entries-table">
      <div className="overflow-x-auto">
        <table className="w-full text-sm">
          <thead>
            <tr className="border-b border-zinc-200/50 dark:border-zinc-700/50 bg-zinc-50/50 dark:bg-zinc-800/50">
              <th className="text-left px-4 py-3 font-medium text-zinc-500 dark:text-zinc-400">Date</th>
              <th className="text-left px-4 py-3 font-medium text-zinc-500 dark:text-zinc-400">Type</th>
              <th className="text-left px-4 py-3 font-medium text-zinc-500 dark:text-zinc-400">Category</th>
              <th className="text-right px-4 py-3 font-medium text-zinc-500 dark:text-zinc-400">Amount</th>
              <th className="text-left px-4 py-3 font-medium text-zinc-500 dark:text-zinc-400">Source</th>
              <th className="text-left px-4 py-3 font-medium text-zinc-500 dark:text-zinc-400">Description</th>
              <th className="text-right px-4 py-3 font-medium text-zinc-500 dark:text-zinc-400">Actions</th>
            </tr>
          </thead>
          <tbody>
            {entries.length === 0 ? (
              <tr>
                <td colSpan={7} className="text-center py-8 text-zinc-500 dark:text-zinc-400">
                  No ledger entries found.
                </td>
              </tr>
            ) : (
              entries.map((entry) => {
                const typeName = LEDGER_ENTRY_TYPE_MAP[entry.type] ?? 'Unknown';
                const categoryName = LEDGER_CATEGORY_MAP[entry.category] ?? 'Unknown';
                const sourceName = LEDGER_SOURCE_MAP[entry.source] ?? 'Unknown';
                const isIncome = typeName === 'Income';

                return (
                  <tr
                    key={entry.id}
                    className="border-b border-zinc-100 dark:border-zinc-800 hover:bg-zinc-50/50 dark:hover:bg-zinc-800/30 transition-colors"
                    data-testid={`ledger-entry-${entry.id}`}
                  >
                    <td className="px-4 py-3 text-zinc-700 dark:text-zinc-300">
                      {formatDate(entry.date)}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
                          isIncome
                            ? 'bg-emerald-50 text-emerald-700 dark:bg-emerald-900/20 dark:text-emerald-400'
                            : 'bg-red-50 text-red-700 dark:bg-red-900/20 dark:text-red-400'
                        )}
                      >
                        {typeName}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400">{categoryName}</td>
                    <td className={cn(
                      'px-4 py-3 text-right font-medium',
                      isIncome
                        ? 'text-emerald-600 dark:text-emerald-400'
                        : 'text-red-600 dark:text-red-400'
                    )}>
                      {isIncome ? '+' : '-'}{formatCurrency(entry.amount, entry.currency)}
                    </td>
                    <td className="px-4 py-3">
                      <span
                        className={cn(
                          'inline-flex items-center rounded-full px-2 py-0.5 text-xs font-medium',
                          sourceName === 'Manual'
                            ? 'bg-blue-50 text-blue-700 dark:bg-blue-900/20 dark:text-blue-400'
                            : 'bg-zinc-100 text-zinc-600 dark:bg-zinc-800 dark:text-zinc-400'
                        )}
                      >
                        {sourceName}
                      </span>
                    </td>
                    <td className="px-4 py-3 text-zinc-600 dark:text-zinc-400 max-w-[200px] truncate">
                      {entry.description ?? '—'}
                    </td>
                    <td className="px-4 py-3 text-right">
                      <div className="flex items-center justify-end gap-1">
                        <button
                          onClick={() => onEdit(entry)}
                          className="rounded-md p-1.5 text-zinc-400 hover:text-zinc-600 dark:hover:text-zinc-300 hover:bg-zinc-100 dark:hover:bg-zinc-800 transition-colors"
                          aria-label={`Edit entry ${entry.id}`}
                          data-testid={`edit-entry-${entry.id}`}
                        >
                          <PencilIcon className="h-4 w-4" />
                        </button>
                        {isManual(entry) && (
                          <button
                            onClick={() => onDelete(entry)}
                            className="rounded-md p-1.5 text-zinc-400 hover:text-red-600 dark:hover:text-red-400 hover:bg-red-50 dark:hover:bg-red-900/20 transition-colors"
                            aria-label={`Delete entry ${entry.id}`}
                            data-testid={`delete-entry-${entry.id}`}
                          >
                            <TrashIcon className="h-4 w-4" />
                          </button>
                        )}
                      </div>
                    </td>
                  </tr>
                );
              })
            )}
          </tbody>
        </table>
      </div>

      {/* Pagination */}
      {total > 0 && (
        <div className="flex items-center justify-between px-4 py-3 border-t border-zinc-200/50 dark:border-zinc-700/50">
          <span className="text-sm text-zinc-500 dark:text-zinc-400">
            Showing {((page - 1) * pageSize) + 1}–{Math.min(page * pageSize, total)} of {total}
          </span>
          <div className="flex items-center gap-1">
            <button
              onClick={() => onPageChange(page - 1)}
              disabled={page <= 1}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 disabled:opacity-40 transition-colors"
              data-testid="prev-page"
            >
              Previous
            </button>
            <span className="px-2 text-sm text-zinc-500 dark:text-zinc-400">
              {page} / {totalPages}
            </span>
            <button
              onClick={() => onPageChange(page + 1)}
              disabled={page >= totalPages}
              className="rounded-md px-3 py-1.5 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 disabled:opacity-40 transition-colors"
              data-testid="next-page"
            >
              Next
            </button>
          </div>
        </div>
      )}
    </div>
  );
}
