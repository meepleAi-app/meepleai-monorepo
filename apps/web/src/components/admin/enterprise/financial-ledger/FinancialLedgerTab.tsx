/**
 * FinancialLedgerTab Component
 * Issue #3722 - Manual Ledger CRUD
 *
 * Main tab for the Business > Financial Ledger section.
 * Combines: Summary cards, entries table with filters,
 * create/edit/delete modals.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  PlusIcon,
  RefreshCwIcon,
  FilterIcon,
  XIcon,
  DownloadIcon,
} from 'lucide-react';

import { api } from '@/lib/api';
import type {
  LedgerEntryDto,
  LedgerEntriesResponse,
  LedgerSummary,
  CreateLedgerEntryRequest,
  UpdateLedgerEntryRequest,
  GetLedgerEntriesParams,
} from '@/lib/api/schemas/financial-ledger.schemas';
import {
  LEDGER_ENTRY_TYPE_MAP,
  LEDGER_CATEGORY_MAP,
  LEDGER_SOURCE_MAP,
  type LedgerEntryType,
  type LedgerCategory,
  type LedgerEntrySource,
} from '@/lib/api/schemas/financial-ledger.schemas';
import { cn } from '@/lib/utils';

import { CreateLedgerEntryModal } from './CreateLedgerEntryModal';
import { DeleteLedgerEntryDialog } from './DeleteLedgerEntryDialog';
import { EditLedgerEntryModal } from './EditLedgerEntryModal';
import { LedgerEntriesTable } from './LedgerEntriesTable';
import { ExportLedgerPanel } from './ExportLedgerPanel';
import { LedgerSummaryCards } from './LedgerSummaryCards';

// Deterministic mock data to avoid hydration mismatch
const MOCK_ENTRIES: LedgerEntryDto[] = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    date: '2026-02-01T00:00:00Z',
    type: 0,
    category: 0,
    amount: 299.99,
    currency: 'EUR',
    source: 1,
    description: 'Monthly Pro subscription - Acme Corp',
    metadata: null,
    createdByUserId: '11111111-1111-1111-1111-111111111111',
    createdAt: '2026-02-01T10:00:00Z',
    updatedAt: null,
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    date: '2026-02-03T00:00:00Z',
    type: 1,
    category: 7,
    amount: 45.50,
    currency: 'EUR',
    source: 0,
    description: 'Cloud hosting - February',
    metadata: null,
    createdByUserId: null,
    createdAt: '2026-02-03T05:00:00Z',
    updatedAt: null,
  },
  {
    id: '00000000-0000-0000-0000-000000000003',
    date: '2026-02-05T00:00:00Z',
    type: 0,
    category: 1,
    amount: 150.00,
    currency: 'EUR',
    source: 0,
    description: 'Token purchase - User batch',
    metadata: null,
    createdByUserId: null,
    createdAt: '2026-02-05T14:30:00Z',
    updatedAt: null,
  },
  {
    id: '00000000-0000-0000-0000-000000000004',
    date: '2026-02-07T00:00:00Z',
    type: 1,
    category: 2,
    amount: 12.30,
    currency: 'EUR',
    source: 0,
    description: 'LLM token usage - Daily aggregate',
    metadata: null,
    createdByUserId: null,
    createdAt: '2026-02-07T05:00:00Z',
    updatedAt: null,
  },
  {
    id: '00000000-0000-0000-0000-000000000005',
    date: '2026-02-10T00:00:00Z',
    type: 1,
    category: 5,
    amount: 75.00,
    currency: 'EUR',
    source: 1,
    description: 'Office supplies reimbursement',
    metadata: null,
    createdByUserId: '11111111-1111-1111-1111-111111111111',
    createdAt: '2026-02-10T09:00:00Z',
    updatedAt: null,
  },
];

const MOCK_SUMMARY: LedgerSummary = {
  totalIncome: 449.99,
  totalExpense: 132.80,
  netBalance: 317.19,
  from: '2026-02-01T00:00:00Z',
  to: '2026-02-11T00:00:00Z',
};

const PAGE_SIZE = 20;

export function FinancialLedgerTab() {
  const [entries, setEntries] = useState<LedgerEntriesResponse | null>(null);
  const [summary, setSummary] = useState<LedgerSummary | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [refreshing, setRefreshing] = useState(false);

  // Filters
  const [filterType, setFilterType] = useState<number | undefined>(undefined);
  const [filterCategory, setFilterCategory] = useState<number | undefined>(undefined);
  const [filterSource, setFilterSource] = useState<number | undefined>(undefined);
  const [showFilters, setShowFilters] = useState(false);
  const [page, setPage] = useState(1);

  // Export panel
  const [showExportPanel, setShowExportPanel] = useState(false);

  // Modals
  const [showCreateModal, setShowCreateModal] = useState(false);
  const [editEntry, setEditEntry] = useState<LedgerEntryDto | null>(null);
  const [deleteEntry, setDeleteEntry] = useState<LedgerEntryDto | null>(null);

  const fetchData = useCallback(async () => {
    setError(null);
    try {
      const params: GetLedgerEntriesParams = {
        page,
        pageSize: PAGE_SIZE,
        type: filterType,
        category: filterCategory,
        source: filterSource,
      };

      const [entriesResult, summaryResult] = await Promise.allSettled([
        api.admin.getLedgerEntries(params),
        api.admin.getLedgerSummary({
          dateFrom: new Date(Date.now() - 30 * 24 * 60 * 60 * 1000).toISOString(),
          dateTo: new Date().toISOString(),
        }),
      ]);

      if (entriesResult.status === 'fulfilled') setEntries(entriesResult.value);
      if (summaryResult.status === 'fulfilled') setSummary(summaryResult.value);

      const allFailed = entriesResult.status === 'rejected' && summaryResult.status === 'rejected';
      if (allFailed) {
        setError('Unable to load ledger data. Backend may not be available yet.');
      }
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load ledger data');
    } finally {
      setLoading(false);
      setRefreshing(false);
    }
  }, [page, filterType, filterCategory, filterSource]);

  useEffect(() => {
    fetchData();
  }, [fetchData]);

  const handleRefresh = async () => {
    setRefreshing(true);
    await fetchData();
  };

  const handleCreateEntry = async (request: CreateLedgerEntryRequest) => {
    await api.admin.createLedgerEntry(request);
    await fetchData();
  };

  const handleUpdateEntry = async (id: string, request: UpdateLedgerEntryRequest) => {
    await api.admin.updateLedgerEntry(id, request);
    await fetchData();
  };

  const handleDeleteEntry = async (id: string) => {
    await api.admin.deleteLedgerEntry(id);
    await fetchData();
  };

  const clearFilters = () => {
    setFilterType(undefined);
    setFilterCategory(undefined);
    setFilterSource(undefined);
    setPage(1);
  };

  const hasActiveFilters = filterType !== undefined || filterCategory !== undefined || filterSource !== undefined;

  // Use mock data when API is not available
  const displayEntries = entries ?? {
    entries: MOCK_ENTRIES,
    total: MOCK_ENTRIES.length,
    page: 1,
    pageSize: PAGE_SIZE,
  };

  const displaySummary = summary ?? MOCK_SUMMARY;

  return (
    <div className="space-y-6" data-testid="financial-ledger-tab">
      {/* Action bar */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <button
            onClick={() => setShowFilters(!showFilters)}
            className={cn(
              'inline-flex items-center gap-2 rounded-lg border px-3 py-2 text-sm transition-colors',
              hasActiveFilters
                ? 'border-blue-300 bg-blue-50 text-blue-700 dark:border-blue-600 dark:bg-blue-900/20 dark:text-blue-400'
                : 'border-zinc-200/50 dark:border-zinc-700/50 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800'
            )}
            data-testid="toggle-filters"
          >
            <FilterIcon className="h-4 w-4" />
            Filters
            {hasActiveFilters && (
              <span className="rounded-full bg-blue-500 text-white text-xs px-1.5 py-0.5 font-medium">
                {[filterType, filterCategory, filterSource].filter((f) => f !== undefined).length}
              </span>
            )}
          </button>
          {hasActiveFilters && (
            <button
              onClick={clearFilters}
              className="inline-flex items-center gap-1 rounded-lg px-2 py-1.5 text-xs text-zinc-500 hover:text-zinc-700 dark:hover:text-zinc-300 transition-colors"
              data-testid="clear-filters"
            >
              <XIcon className="h-3 w-3" />
              Clear
            </button>
          )}
        </div>

        <div className="flex items-center gap-2">
          <button
            onClick={handleRefresh}
            disabled={refreshing}
            className="inline-flex items-center gap-2 rounded-lg border border-zinc-200/50 dark:border-zinc-700/50 px-3 py-2 text-sm text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800 transition-colors"
            data-testid="refresh-ledger"
          >
            <RefreshCwIcon className={cn('h-4 w-4', refreshing && 'animate-spin')} />
            Refresh
          </button>
          <button
            onClick={() => setShowExportPanel(!showExportPanel)}
            className={cn(
              'inline-flex items-center gap-2 rounded-lg border px-3 py-2 text-sm transition-colors',
              showExportPanel
                ? 'border-blue-300 bg-blue-50 text-blue-700 dark:border-blue-600 dark:bg-blue-900/20 dark:text-blue-400'
                : 'border-zinc-200/50 dark:border-zinc-700/50 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-50 dark:hover:bg-zinc-800'
            )}
            data-testid="toggle-export"
          >
            <DownloadIcon className="h-4 w-4" />
            Export
          </button>
          <button
            onClick={() => setShowCreateModal(true)}
            className="inline-flex items-center gap-2 rounded-lg bg-emerald-500 hover:bg-emerald-600 px-4 py-2 text-sm font-medium text-white transition-colors"
            data-testid="new-entry-btn"
          >
            <PlusIcon className="h-4 w-4" />
            New Entry
          </button>
        </div>
      </div>

      {/* Filter row */}
      {showFilters && (
        <div className="flex flex-wrap gap-3 rounded-xl border border-zinc-200/50 dark:border-zinc-700/50 bg-zinc-50/50 dark:bg-zinc-800/30 p-4" data-testid="filter-row">
          <div>
            <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Type</label>
            <select
              value={filterType ?? ''}
              onChange={(e) => { setFilterType(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
              className="rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-1.5 text-sm"
              data-testid="filter-type"
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
              onChange={(e) => { setFilterCategory(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
              className="rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-1.5 text-sm"
              data-testid="filter-category"
            >
              <option value="">All</option>
              {Object.entries(LEDGER_CATEGORY_MAP).map(([val, label]) => (
                <option key={val} value={val}>{label}</option>
              ))}
            </select>
          </div>
          <div>
            <label className="block text-xs text-zinc-500 dark:text-zinc-400 mb-1">Source</label>
            <select
              value={filterSource ?? ''}
              onChange={(e) => { setFilterSource(e.target.value ? Number(e.target.value) : undefined); setPage(1); }}
              className="rounded-lg border border-zinc-200 dark:border-zinc-700 bg-white dark:bg-zinc-800 px-3 py-1.5 text-sm"
              data-testid="filter-source"
            >
              <option value="">All</option>
              {Object.entries(LEDGER_SOURCE_MAP).map(([val, label]) => (
                <option key={val} value={val}>{label}</option>
              ))}
            </select>
          </div>
        </div>
      )}

      {/* Export panel */}
      {showExportPanel && <ExportLedgerPanel />}

      {/* Error banner */}
      {error && !loading && (
        <div className="rounded-lg border border-amber-200/50 bg-amber-50/50 dark:bg-amber-900/10 dark:border-amber-700/50 p-4 text-sm text-amber-700 dark:text-amber-300" data-testid="ledger-error">
          {error}
        </div>
      )}

      {/* Summary cards */}
      <LedgerSummaryCards
        totalIncome={displaySummary.totalIncome}
        totalExpense={displaySummary.totalExpense}
        netBalance={displaySummary.netBalance}
        loading={loading}
      />

      {/* Entries table */}
      <LedgerEntriesTable
        entries={displayEntries.entries}
        total={displayEntries.total}
        page={displayEntries.page}
        pageSize={displayEntries.pageSize}
        loading={loading}
        onEdit={(entry) => setEditEntry(entry)}
        onDelete={(entry) => setDeleteEntry(entry)}
        onPageChange={setPage}
      />

      {/* Modals */}
      <CreateLedgerEntryModal
        open={showCreateModal}
        onClose={() => setShowCreateModal(false)}
        onSubmit={handleCreateEntry}
      />
      <EditLedgerEntryModal
        entry={editEntry}
        onClose={() => setEditEntry(null)}
        onSubmit={handleUpdateEntry}
      />
      <DeleteLedgerEntryDialog
        entry={deleteEntry}
        onClose={() => setDeleteEntry(null)}
        onConfirm={handleDeleteEntry}
      />
    </div>
  );
}
