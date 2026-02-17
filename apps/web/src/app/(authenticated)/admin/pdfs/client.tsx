'use client';

import { useMemo, useState } from 'react';

import { useQuery, useQueryClient } from '@tanstack/react-query';
import { RefreshCw, Play } from 'lucide-react';
import { toast } from 'sonner';

import {
  AdminConfirmationDialog,
  AdminConfirmationLevel,
} from '@/components/ui/admin/admin-confirmation-dialog';
import { Button } from '@/components/ui/primitives/button';
import { useApiClient } from '@/lib/api/context';
import { isPdfStateTerminal } from '@/types/pdf';
import type { PdfState } from '@/types/pdf';

import { MaintenanceToolbar } from './_components/MaintenanceToolbar';
import { PdfAdminTable } from './_components/PdfAdminTable';
import { PdfBulkActions } from './_components/PdfBulkActions';
import { PdfFilters, defaultFilters, filtersToQueryParams, type PdfFilterState } from './_components/PdfFilters';
import { PdfMiniAnalytics } from './_components/PdfMiniAnalytics';
import { StorageHealthCards } from './_components/StorageHealthCards';
import { useStorageHealth } from './_hooks/useStorageHealth';

export function PdfAdminClient() {
  const apiClient = useApiClient();
  const queryClient = useQueryClient();

  // State
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [sortBy, setSortBy] = useState('uploadedat');
  const [sortOrder, setSortOrder] = useState<'asc' | 'desc'>('desc');
  const [filters, setFilters] = useState<PdfFilterState>(defaultFilters);
  const [page, setPage] = useState(1);
  const [deleteDialog, setDeleteDialog] = useState<{ open: boolean; pdfId: string; fileName: string }>({
    open: false, pdfId: '', fileName: '',
  });
  const [bulkDeleteOpen, setBulkDeleteOpen] = useState(false);
  const [isBulkDeleting, setIsBulkDeleting] = useState(false);

  // Build query params from filters
  const filterParams = useMemo(() => filtersToQueryParams(filters), [filters]);

  // Queries
  const { data: storageHealth, isLoading: healthLoading } = useStorageHealth();

  const { data, isLoading, refetch } = useQuery({
    queryKey: ['admin', 'pdfs', { sortBy, sortOrder, page, ...filterParams, states: filters.states }],
    queryFn: () => apiClient.pdf.getAdminPdfList({
      sortBy,
      sortOrder,
      page,
      pageSize: 50,
      ...filterParams,
      // For multiple states, filter client-side since API supports single state
      state: filters.states.length === 1 && filters.states[0]
        ? filters.states[0].charAt(0).toUpperCase() + filters.states[0].slice(1)
        : undefined,
    }),
    refetchInterval: (query) => {
      // Smart polling: only when there are active (non-terminal) docs
      const items = query.state.data?.items;
      if (!items || items.length === 0) return false;
      const allTerminal = items.every(item => {
        const state = item.processingState.toLowerCase() as PdfState;
        return isPdfStateTerminal(state);
      });
      return allTerminal ? false : 5000;
    },
  });

  const items = useMemo(() => {
    const raw = data?.items || [];
    // Client-side filter for multi-state selection
    if (filters.states.length > 1) {
      return raw.filter(item =>
        filters.states.includes(item.processingState.toLowerCase() as PdfState)
      );
    }
    return raw;
  }, [data?.items, filters.states]);

  const invalidateAll = () => {
    queryClient.invalidateQueries({ queryKey: ['admin', 'pdfs'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'pdf-storage-health'] });
    queryClient.invalidateQueries({ queryKey: ['admin', 'pdf-status-distribution'] });
  };

  // Handlers
  const handleSortChange = (field: string) => {
    if (sortBy === field) {
      setSortOrder(prev => prev === 'asc' ? 'desc' : 'asc');
    } else {
      setSortBy(field);
      setSortOrder('desc');
    }
  };

  const handleProcessPending = async () => {
    try {
      const response = await fetch('/api/v1/admin/pdfs/process-pending', { method: 'POST' });
      const result = await response.json();
      toast.success(`Triggered ${result.triggered} PDF(s) for processing`);
      refetch();
    } catch {
      toast.error('Failed to trigger processing');
    }
  };

  const handleDeleteConfirm = async () => {
    try {
      await apiClient.pdf.adminDeletePdf(deleteDialog.pdfId);
      toast.success(`Deleted ${deleteDialog.fileName}`);
      setDeleteDialog({ open: false, pdfId: '', fileName: '' });
      invalidateAll();
    } catch {
      toast.error('Failed to delete document');
    }
  };

  const handleBulkDeleteConfirm = async () => {
    setIsBulkDeleting(true);
    try {
      const result = await apiClient.pdf.bulkDeletePdfs(Array.from(selectedIds));
      toast.success(`Deleted ${result.successCount} of ${result.totalRequested} document(s)`);
      if (result.failedCount > 0) {
        toast.warning(`${result.failedCount} deletion(s) failed`);
      }
      setSelectedIds(new Set());
      setBulkDeleteOpen(false);
      invalidateAll();
    } catch {
      toast.error('Bulk delete failed');
    } finally {
      setIsBulkDeleting(false);
    }
  };

  const handleReindex = async (pdfId: string, fileName: string) => {
    try {
      await apiClient.pdf.reindexDocument(pdfId);
      toast.success(`Reindexing ${fileName}...`);
      invalidateAll();
    } catch {
      toast.error('Failed to trigger reindex');
    }
  };

  const handleRetry = async (pdfId: string, fileName: string) => {
    try {
      await fetch(`/api/v1/documents/${pdfId}/retry`, { method: 'POST' });
      toast.success(`Retrying ${fileName}...`);
      refetch();
    } catch {
      toast.error('Failed to retry processing');
    }
  };

  return (
      <div className="space-y-6 p-6">
        {/* Header */}
        <div className="flex items-center justify-between">
          <div>
            <h1 className="text-3xl font-bold">PDF Storage Management</h1>
            <p className="text-muted-foreground">
              Monitor processing, storage health, and manage documents
              {data?.total != null && ` (${data.total} total)`}
            </p>
          </div>
          <div className="flex items-center gap-2">
            <MaintenanceToolbar onComplete={invalidateAll} />
            <Button onClick={handleProcessPending} variant="default" size="sm">
              <Play className="h-4 w-4 mr-2" />
              Process Pending
            </Button>
            <Button onClick={() => refetch()} variant="outline" size="sm">
              <RefreshCw className="h-4 w-4 mr-2" />
              Refresh
            </Button>
          </div>
        </div>

        {/* Storage Health Dashboard */}
        <StorageHealthCards data={storageHealth} isLoading={healthLoading} />

        {/* Mini Analytics */}
        <PdfMiniAnalytics />

        {/* Filters */}
        <PdfFilters filters={filters} onFiltersChange={(f) => { setFilters(f); setPage(1); }} />

        {/* Data Table */}
        {isLoading ? (
          <div className="bg-card rounded-lg border p-8 text-center text-muted-foreground">
            Loading documents...
          </div>
        ) : (
          <PdfAdminTable
            items={items}
            selectedIds={selectedIds}
            onSelectionChange={setSelectedIds}
            sortBy={sortBy}
            sortOrder={sortOrder}
            onSortChange={handleSortChange}
            onDelete={(pdfId, fileName) => setDeleteDialog({ open: true, pdfId, fileName })}
            onReindex={handleReindex}
            onRetry={handleRetry}
          />
        )}

        {/* Pagination */}
        {data && data.total > data.pageSize && (
          <div className="flex items-center justify-between">
            <span className="text-sm text-muted-foreground">
              Page {data.page} of {Math.ceil(data.total / data.pageSize)}
            </span>
            <div className="flex gap-2">
              <Button
                variant="outline"
                size="sm"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                Previous
              </Button>
              <Button
                variant="outline"
                size="sm"
                disabled={page >= Math.ceil(data.total / data.pageSize)}
                onClick={() => setPage(p => p + 1)}
              >
                Next
              </Button>
            </div>
          </div>
        )}

        {/* Bulk Actions Bar */}
        <PdfBulkActions
          selectedCount={selectedIds.size}
          onBulkDelete={() => setBulkDeleteOpen(true)}
          isDeleting={isBulkDeleting}
        />

        {/* Delete Confirmation Dialog */}
        <AdminConfirmationDialog
          isOpen={deleteDialog.open}
          onClose={() => setDeleteDialog(prev => ({ ...prev, open: false }))}
          title="Delete Document"
          message={`Are you sure you want to delete "${deleteDialog.fileName}"? This will remove the file, all text chunks, and vector embeddings. This action cannot be undone.`}
          confirmText="Delete"
          level={AdminConfirmationLevel.Level1}
          onConfirm={handleDeleteConfirm}
        />

        {/* Bulk Delete Confirmation */}
        <AdminConfirmationDialog
          isOpen={bulkDeleteOpen}
          onClose={() => setBulkDeleteOpen(false)}
          title="Delete Selected Documents"
          message={`Are you sure you want to delete ${selectedIds.size} document(s)? This will remove all files, text chunks, and vector embeddings. This action cannot be undone.`}
          confirmText={`Delete ${selectedIds.size} Document(s)`}
          level={AdminConfirmationLevel.Level2}
          onConfirm={handleBulkDeleteConfirm}
          isLoading={isBulkDeleting}
        />
      </div>
  );
}
