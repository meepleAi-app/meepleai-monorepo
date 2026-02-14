/**
 * ExportLedgerPanel Component
 * Issue #3724 - Export Ledger (PDF/CSV/Excel)
 *
 * Provides format selection, date range, and optional filters
 * for exporting ledger entries. Downloads the file via blob URL.
 */

'use client';

import { useState } from 'react';

import {
  DownloadIcon,
  FileSpreadsheetIcon,
  FileTextIcon,
  FileIcon,
  Loader2Icon,
} from 'lucide-react';

import { api } from '@/lib/api';
import {
  LEDGER_EXPORT_FORMAT_LABELS,
  LEDGER_EXPORT_FORMAT_EXT,
  LEDGER_ENTRY_TYPE_MAP,
  LEDGER_CATEGORY_MAP,
  type LedgerExportFormat,
} from '@/lib/api/schemas/financial-ledger.schemas';
import { cn } from '@/lib/utils';

const FORMAT_OPTIONS: { value: number; format: LedgerExportFormat; icon: typeof FileTextIcon }[] = [
  { value: 0, format: 'Csv', icon: FileTextIcon },
  { value: 1, format: 'Excel', icon: FileSpreadsheetIcon },
  { value: 2, format: 'Pdf', icon: FileIcon },
];

export function ExportLedgerPanel() {
  const [selectedFormat, setSelectedFormat] = useState(0);
  const [dateFrom, setDateFrom] = useState('');
  const [dateTo, setDateTo] = useState('');
  const [filterType, setFilterType] = useState<number | undefined>(undefined);
  const [filterCategory, setFilterCategory] = useState<number | undefined>(undefined);
  const [exporting, setExporting] = useState(false);
  const [error, setError] = useState<string | null>(null);

  const currentFormat = FORMAT_OPTIONS[selectedFormat]?.format ?? 'Csv';

  const handleExport = async () => {
    setError(null);
    setExporting(true);
    try {
      const blob = await api.admin.exportLedgerEntries({
        format: selectedFormat,
        dateFrom: dateFrom || undefined,
        dateTo: dateTo || undefined,
        type: filterType,
        category: filterCategory,
      });

      // Trigger download via blob URL
      const url = URL.createObjectURL(blob);
      const anchor = document.createElement('a');
      const datePart = dateFrom && dateTo
        ? `${dateFrom.replace(/-/g, '')}-${dateTo.replace(/-/g, '')}`
        : new Date().toISOString().slice(0, 10).replace(/-/g, '');
      anchor.href = url;
      anchor.download = `ledger-export-${datePart}.${LEDGER_EXPORT_FORMAT_EXT[currentFormat]}`;
      document.body.appendChild(anchor);
      anchor.click();
      document.body.removeChild(anchor);
      URL.revokeObjectURL(url);
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Export failed');
    } finally {
      setExporting(false);
    }
  };

  return (
    <div
      className="rounded-xl border border-zinc-200/50 dark:border-zinc-700/50 bg-white/50 dark:bg-zinc-900/50 p-5 space-y-4"
      data-testid="export-ledger-panel"
    >
      <div className="flex items-center gap-2">
        <DownloadIcon className="h-5 w-5 text-zinc-500 dark:text-zinc-400" />
        <h3 className="text-sm font-semibold text-zinc-800 dark:text-zinc-200">
          Export Ledger Data
        </h3>
      </div>

      {/* Format selector */}
      <div>
        <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1.5">Format</label>
        <div className="grid grid-cols-3 gap-2" data-testid="format-selector">
          {FORMAT_OPTIONS.map(({ value, format, icon: Icon }) => (
            <button
              key={value}
              onClick={() => setSelectedFormat(value)}
              className={cn(
                'flex items-center gap-2 rounded-lg border px-3 py-2 text-sm transition-colors',
                selectedFormat === value
                  ? 'border-blue-400 bg-blue-50 text-blue-700 dark:border-blue-500 dark:bg-blue-900/30 dark:text-blue-300'
                  : 'border-zinc-200 dark:border-zinc-700 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800'
              )}
              data-testid={`format-${format.toLowerCase()}`}
            >
              <Icon className="h-4 w-4" />
              {LEDGER_EXPORT_FORMAT_LABELS[format]}
            </button>
          ))}
        </div>
      </div>

      {/* Date range */}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">From</label>
          <input
            type="date"
            value={dateFrom}
            onChange={(e) => setDateFrom(e.target.value)}
            className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-1.5 text-sm"
            data-testid="export-date-from"
          />
        </div>
        <div>
          <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">To</label>
          <input
            type="date"
            value={dateTo}
            onChange={(e) => setDateTo(e.target.value)}
            className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-1.5 text-sm"
            data-testid="export-date-to"
          />
        </div>
      </div>

      {/* Optional filters */}
      <div className="grid grid-cols-2 gap-3">
        <div>
          <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Type</label>
          <select
            value={filterType ?? ''}
            onChange={(e) => setFilterType(e.target.value ? Number(e.target.value) : undefined)}
            className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-1.5 text-sm"
            data-testid="export-filter-type"
          >
            <option value="">All</option>
            {Object.entries(LEDGER_ENTRY_TYPE_MAP).map(([val, label]) => (
              <option key={val} value={val}>{label}</option>
            ))}
          </select>
        </div>
        <div>
          <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Category</label>
          <select
            value={filterCategory ?? ''}
            onChange={(e) => setFilterCategory(e.target.value ? Number(e.target.value) : undefined)}
            className="w-full rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-1.5 text-sm"
            data-testid="export-filter-category"
          >
            <option value="">All</option>
            {Object.entries(LEDGER_CATEGORY_MAP).map(([val, label]) => (
              <option key={val} value={val}>{label}</option>
            ))}
          </select>
        </div>
      </div>

      {/* Error */}
      {error && (
        <div
          className="rounded-lg border border-red-200/50 bg-red-50/50 dark:bg-red-900/10 dark:border-red-700/50 p-3 text-xs text-red-600 dark:text-red-400"
          data-testid="export-error"
        >
          {error}
        </div>
      )}

      {/* Export button */}
      <button
        onClick={handleExport}
        disabled={exporting}
        className={cn(
          'w-full inline-flex items-center justify-center gap-2 rounded-lg px-4 py-2.5 text-sm font-medium text-white transition-colors',
          exporting
            ? 'bg-blue-400 dark:bg-blue-600 cursor-not-allowed'
            : 'bg-blue-500 hover:bg-blue-600 dark:bg-blue-600 dark:hover:bg-blue-500'
        )}
        data-testid="export-btn"
      >
        {exporting ? (
          <>
            <Loader2Icon className="h-4 w-4 animate-spin" />
            Exporting...
          </>
        ) : (
          <>
            <DownloadIcon className="h-4 w-4" />
            Export as {LEDGER_EXPORT_FORMAT_LABELS[currentFormat]}
          </>
        )}
      </button>
    </div>
  );
}
