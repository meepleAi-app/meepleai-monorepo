'use client';

import { useState } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { format } from 'date-fns';
import {
  FileTextIcon,
  RefreshCwIcon,
  AlertCircleIcon,
  TrashIcon,
  RotateCcwIcon,
  SearchIcon,
  ChevronLeftIcon,
  ChevronRightIcon,
  HardDriveIcon,
  DatabaseIcon,
  ActivityIcon,
  ShieldCheckIcon,
} from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
  AlertDialogTrigger,
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { createAdminClient } from '@/lib/api/clients/adminClient';
import { HttpClient } from '@/lib/api/core/httpClient';
import type { PdfListItem } from '@/lib/api/schemas/admin-knowledge-base.schemas';

const httpClient = new HttpClient();
const adminClient = createAdminClient({ httpClient });

function formatFileSize(bytes: number): string {
  if (bytes === 0) return '0 B';
  const sizes = ['B', 'KB', 'MB', 'GB'];
  const i = Math.floor(Math.log(bytes) / Math.log(1024));
  return `${(bytes / Math.pow(1024, i)).toFixed(1)} ${sizes[i]}`;
}

function getStatusBadge(status: string) {
  const statusConfig: Record<string, { variant: 'default' | 'secondary' | 'destructive' | 'outline'; className: string }> = {
    Completed: { variant: 'default', className: 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300' },
    Processing: { variant: 'default', className: 'bg-blue-100 text-blue-800 dark:bg-blue-900/30 dark:text-blue-300' },
    Pending: { variant: 'secondary', className: 'bg-amber-100 text-amber-800 dark:bg-amber-900/30 dark:text-amber-300' },
    Failed: { variant: 'destructive', className: '' },
    Extracting: { variant: 'default', className: 'bg-purple-100 text-purple-800 dark:bg-purple-900/30 dark:text-purple-300' },
    Chunking: { variant: 'default', className: 'bg-cyan-100 text-cyan-800 dark:bg-cyan-900/30 dark:text-cyan-300' },
    Embedding: { variant: 'default', className: 'bg-indigo-100 text-indigo-800 dark:bg-indigo-900/30 dark:text-indigo-300' },
  };
  const config = statusConfig[status] ?? { variant: 'outline' as const, className: '' };
  return (
    <Badge variant={config.variant} className={config.className}>
      {status}
    </Badge>
  );
}

export default function DocumentsLibraryPage() {
  const queryClient = useQueryClient();
  const [search, setSearch] = useState('');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [page, setPage] = useState(1);
  const pageSize = 20;

  // Fetch documents
  const { data: pdfsData, isLoading, error, refetch, isRefetching } = useQuery({
    queryKey: ['admin', 'pdfs', { page, pageSize, status: statusFilter, search }],
    queryFn: () => adminClient.getAllPdfs({
      page,
      pageSize,
      state: statusFilter !== 'all' ? statusFilter : undefined,
      sortBy: 'uploadedAt',
      sortOrder: 'desc',
    }),
    staleTime: 30_000,
  });

  // Fetch analytics
  const { data: distribution } = useQuery({
    queryKey: ['admin', 'pdfs', 'distribution'],
    queryFn: () => adminClient.getPdfStatusDistribution(),
    staleTime: 60_000,
  });

  const { data: storageHealth } = useQuery({
    queryKey: ['admin', 'pdfs', 'storage-health'],
    queryFn: () => adminClient.getPdfStorageHealth(),
    staleTime: 60_000,
  });

  // Mutations
  const reindexMutation = useMutation({
    mutationFn: (pdfId: string) => adminClient.reindexPdf(pdfId),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'pdfs'] });
    },
  });

  const bulkDeleteMutation = useMutation({
    mutationFn: (ids: string[]) => adminClient.bulkDeletePdfs(ids),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'pdfs'] });
      setSelectedIds([]);
    },
  });

  const purgeStaleMutation = useMutation({
    mutationFn: () => adminClient.purgeStaleDocuments(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'pdfs'] });
    },
  });

  const cleanupOrphansMutation = useMutation({
    mutationFn: () => adminClient.cleanupOrphans(),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'pdfs'] });
    },
  });

  // Selection
  const [selectedIds, setSelectedIds] = useState<string[]>([]);
  const toggleSelect = (id: string) => {
    setSelectedIds(prev =>
      prev.includes(id) ? prev.filter(x => x !== id) : [...prev, id]
    );
  };
  const toggleSelectAll = () => {
    if (!pdfsData?.items) return;
    if (selectedIds.length === pdfsData.items.length) {
      setSelectedIds([]);
    } else {
      setSelectedIds(pdfsData.items.map(item => item.id));
    }
  };

  const items = pdfsData?.items ?? [];
  const total = pdfsData?.total ?? 0;
  const totalPages = Math.ceil(total / pageSize);

  // Filter items by search client-side (for filename)
  const filteredItems = search
    ? items.filter(item =>
        item.fileName.toLowerCase().includes(search.toLowerCase()) ||
        (item.gameTitle && item.gameTitle.toLowerCase().includes(search.toLowerCase()))
      )
    : items;

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between flex-wrap gap-4">
        <div>
          <h1 className="font-quicksand text-2xl font-bold tracking-tight text-foreground">
            Documents Library
          </h1>
          <p className="text-sm text-muted-foreground mt-1">
            Browse uploaded documents, manage processing, and monitor storage
          </p>
        </div>
        <div className="flex items-center gap-2">
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="outline" size="sm" className="gap-2 text-amber-700 dark:text-amber-400">
                <RotateCcwIcon className="h-3.5 w-3.5" />
                Purge Stale
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Purge Stale Documents</AlertDialogTitle>
                <AlertDialogDescription>
                  This will mark documents stuck in processing for over 24 hours as failed. They can be reindexed later.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={() => purgeStaleMutation.mutate()}>
                  Purge Stale
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>

          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="outline" size="sm" className="gap-2 text-amber-700 dark:text-amber-400">
                <TrashIcon className="h-3.5 w-3.5" />
                Cleanup Orphans
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Cleanup Orphaned Chunks</AlertDialogTitle>
                <AlertDialogDescription>
                  This will delete text chunks that reference non-existent PDF documents. This operation is irreversible.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction onClick={() => cleanupOrphansMutation.mutate()}>
                  Cleanup
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>

          <Button
            variant="outline"
            size="sm"
            onClick={() => refetch()}
            disabled={isRefetching}
            className="gap-2"
          >
            <RefreshCwIcon className={`h-3.5 w-3.5 ${isRefetching ? 'animate-spin' : ''}`} />
            Refresh
          </Button>
        </div>
      </div>

      {/* Analytics Cards */}
      <div className="grid grid-cols-1 md:grid-cols-4 gap-4">
        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-blue-100 dark:bg-blue-900/30 flex items-center justify-center">
                <FileTextIcon className="h-5 w-5 text-blue-600 dark:text-blue-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Total Documents</p>
                <p className="text-xl font-bold">{distribution?.totalDocuments ?? '—'}</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-green-100 dark:bg-green-900/30 flex items-center justify-center">
                <ShieldCheckIcon className="h-5 w-5 text-green-600 dark:text-green-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Completed</p>
                <p className="text-xl font-bold">{distribution?.countByState?.['Completed'] ?? '—'}</p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-purple-100 dark:bg-purple-900/30 flex items-center justify-center">
                <ActivityIcon className="h-5 w-5 text-purple-600 dark:text-purple-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Processing</p>
                <p className="text-xl font-bold">
                  {(distribution?.countByState?.['Processing'] ?? 0) +
                   (distribution?.countByState?.['Extracting'] ?? 0) +
                   (distribution?.countByState?.['Chunking'] ?? 0) +
                   (distribution?.countByState?.['Embedding'] ?? 0) || '—'}
                </p>
              </div>
            </div>
          </CardContent>
        </Card>

        <Card className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md border-slate-200/60 dark:border-zinc-700/40">
          <CardContent className="p-4">
            <div className="flex items-center gap-3">
              <div className="h-10 w-10 rounded-lg bg-slate-100 dark:bg-zinc-700/50 flex items-center justify-center">
                <HardDriveIcon className="h-5 w-5 text-slate-600 dark:text-zinc-400" />
              </div>
              <div>
                <p className="text-sm text-muted-foreground">Storage</p>
                <p className="text-xl font-bold">{storageHealth?.fileStorage?.totalSizeFormatted ?? '—'}</p>
              </div>
            </div>
          </CardContent>
        </Card>
      </div>

      {/* Storage Health Bar */}
      {storageHealth && (
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg border border-slate-200/60 dark:border-zinc-700/40 p-4">
          <div className="flex items-center gap-6 text-sm">
            <div className="flex items-center gap-2">
              <DatabaseIcon className="h-4 w-4 text-blue-500" />
              <span className="text-muted-foreground">PostgreSQL:</span>
              <span className="font-medium">{storageHealth.postgres.totalDocuments} docs, {storageHealth.postgres.totalChunks} chunks</span>
            </div>
            <div className="flex items-center gap-2">
              <DatabaseIcon className="h-4 w-4 text-purple-500" />
              <span className="text-muted-foreground">Qdrant:</span>
              <span className="font-medium">
                {storageHealth.qdrant.isAvailable
                  ? `${storageHealth.qdrant.vectorCount.toLocaleString()} vectors (${storageHealth.qdrant.memoryFormatted})`
                  : 'Unavailable'}
              </span>
            </div>
            <div className="flex items-center gap-2">
              <HardDriveIcon className="h-4 w-4 text-green-500" />
              <span className="text-muted-foreground">Files:</span>
              <span className="font-medium">{storageHealth.fileStorage.totalFiles} ({storageHealth.fileStorage.totalSizeFormatted})</span>
            </div>
            <Badge variant={storageHealth.overallHealth === 'Healthy' ? 'default' : 'destructive'}
              className={storageHealth.overallHealth === 'Healthy' ? 'bg-green-100 text-green-800 dark:bg-green-900/30 dark:text-green-300' : ''}>
              {storageHealth.overallHealth}
            </Badge>
          </div>
        </div>
      )}

      {/* Filters & Bulk Actions */}
      <div className="flex items-center justify-between flex-wrap gap-3">
        <div className="flex items-center gap-3">
          <div className="relative">
            <SearchIcon className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" />
            <Input
              placeholder="Search documents..."
              value={search}
              onChange={(e) => { setSearch(e.target.value); setPage(1); }}
              className="pl-9 w-64 bg-white/70 dark:bg-zinc-800/70"
            />
          </div>
          <Select value={statusFilter} onValueChange={(v) => { setStatusFilter(v); setPage(1); }}>
            <SelectTrigger className="w-40 bg-white/70 dark:bg-zinc-800/70">
              <SelectValue placeholder="Status filter" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Statuses</SelectItem>
              <SelectItem value="Pending">Pending</SelectItem>
              <SelectItem value="Processing">Processing</SelectItem>
              <SelectItem value="Completed">Completed</SelectItem>
              <SelectItem value="Failed">Failed</SelectItem>
            </SelectContent>
          </Select>
        </div>

        {selectedIds.length > 0 && (
          <AlertDialog>
            <AlertDialogTrigger asChild>
              <Button variant="destructive" size="sm" className="gap-2">
                <TrashIcon className="h-3.5 w-3.5" />
                Delete {selectedIds.length} selected
              </Button>
            </AlertDialogTrigger>
            <AlertDialogContent>
              <AlertDialogHeader>
                <AlertDialogTitle>Delete Documents</AlertDialogTitle>
                <AlertDialogDescription>
                  This will permanently delete {selectedIds.length} document(s) and their associated chunks. This action cannot be undone.
                </AlertDialogDescription>
              </AlertDialogHeader>
              <AlertDialogFooter>
                <AlertDialogCancel>Cancel</AlertDialogCancel>
                <AlertDialogAction
                  onClick={() => bulkDeleteMutation.mutate(selectedIds)}
                  className="bg-destructive text-destructive-foreground"
                >
                  Delete
                </AlertDialogAction>
              </AlertDialogFooter>
            </AlertDialogContent>
          </AlertDialog>
        )}
      </div>

      {/* Error State */}
      {error && (
        <div className="bg-red-50 dark:bg-red-900/20 border border-red-200 dark:border-red-800/40 rounded-lg p-4 flex items-center gap-3">
          <AlertCircleIcon className="h-5 w-5 text-red-500 shrink-0" />
          <div className="flex-1">
            <p className="text-sm font-medium text-red-800 dark:text-red-200">Failed to load documents</p>
            <p className="text-xs text-red-600 dark:text-red-400 mt-1">
              {error instanceof Error ? error.message : 'Unknown error'}
            </p>
          </div>
          <Button variant="outline" size="sm" onClick={() => refetch()}>Retry</Button>
        </div>
      )}

      {/* Documents Table */}
      <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-xl border border-slate-200/60 dark:border-zinc-700/40 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full text-sm">
            <thead>
              <tr className="border-b border-slate-200/60 dark:border-zinc-700/40">
                <th className="text-left p-3 w-10">
                  <input
                    type="checkbox"
                    checked={filteredItems.length > 0 && selectedIds.length === filteredItems.length}
                    onChange={toggleSelectAll}
                    className="rounded border-slate-300 dark:border-zinc-600"
                  />
                </th>
                <th className="text-left p-3 font-medium text-muted-foreground">Document</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Game</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Status</th>
                <th className="text-right p-3 font-medium text-muted-foreground">Pages</th>
                <th className="text-right p-3 font-medium text-muted-foreground">Chunks</th>
                <th className="text-right p-3 font-medium text-muted-foreground">Size</th>
                <th className="text-left p-3 font-medium text-muted-foreground">Uploaded</th>
                <th className="text-right p-3 font-medium text-muted-foreground">Actions</th>
              </tr>
            </thead>
            <tbody>
              {isLoading ? (
                Array.from({ length: 5 }).map((_, i) => (
                  <tr key={i} className="border-b border-slate-100 dark:border-zinc-800/60">
                    <td className="p-3"><Skeleton className="h-4 w-4" /></td>
                    <td className="p-3"><Skeleton className="h-4 w-48" /></td>
                    <td className="p-3"><Skeleton className="h-4 w-24" /></td>
                    <td className="p-3"><Skeleton className="h-5 w-20" /></td>
                    <td className="p-3"><Skeleton className="h-4 w-8 ml-auto" /></td>
                    <td className="p-3"><Skeleton className="h-4 w-8 ml-auto" /></td>
                    <td className="p-3"><Skeleton className="h-4 w-16 ml-auto" /></td>
                    <td className="p-3"><Skeleton className="h-4 w-20" /></td>
                    <td className="p-3"><Skeleton className="h-8 w-16 ml-auto" /></td>
                  </tr>
                ))
              ) : filteredItems.length === 0 ? (
                <tr>
                  <td colSpan={9} className="p-8 text-center text-muted-foreground">
                    {search || statusFilter !== 'all'
                      ? 'No documents match your filters'
                      : 'No documents uploaded yet'}
                  </td>
                </tr>
              ) : (
                filteredItems.map((item: PdfListItem) => (
                  <tr key={item.id} className="border-b border-slate-100 dark:border-zinc-800/60 hover:bg-slate-50/50 dark:hover:bg-zinc-700/30">
                    <td className="p-3">
                      <input
                        type="checkbox"
                        checked={selectedIds.includes(item.id)}
                        onChange={() => toggleSelect(item.id)}
                        className="rounded border-slate-300 dark:border-zinc-600"
                      />
                    </td>
                    <td className="p-3">
                      <div className="flex items-center gap-2">
                        <FileTextIcon className="h-4 w-4 text-muted-foreground shrink-0" />
                        <span className="font-medium truncate max-w-[200px]" title={item.fileName}>
                          {item.fileName}
                        </span>
                      </div>
                    </td>
                    <td className="p-3">
                      <span className="text-muted-foreground truncate max-w-[150px] block">
                        {item.gameTitle ?? '—'}
                      </span>
                    </td>
                    <td className="p-3">{getStatusBadge(item.processingState)}</td>
                    <td className="p-3 text-right text-muted-foreground">{item.pageCount ?? '—'}</td>
                    <td className="p-3 text-right text-muted-foreground">{item.chunkCount}</td>
                    <td className="p-3 text-right text-muted-foreground">{formatFileSize(item.fileSizeBytes)}</td>
                    <td className="p-3 text-muted-foreground">
                      {format(new Date(item.uploadedAt), 'MMM d, yyyy')}
                    </td>
                    <td className="p-3 text-right">
                      <div className="flex items-center justify-end gap-1">
                        <Button
                          variant="ghost"
                          size="icon"
                          className="h-7 w-7"
                          onClick={() => reindexMutation.mutate(item.id)}
                          disabled={reindexMutation.isPending}
                          title="Reindex document"
                        >
                          <RotateCcwIcon className="h-3.5 w-3.5" />
                        </Button>
                        <AlertDialog>
                          <AlertDialogTrigger asChild>
                            <Button variant="ghost" size="icon" className="h-7 w-7 text-destructive" title="Delete document">
                              <TrashIcon className="h-3.5 w-3.5" />
                            </Button>
                          </AlertDialogTrigger>
                          <AlertDialogContent>
                            <AlertDialogHeader>
                              <AlertDialogTitle>Delete Document</AlertDialogTitle>
                              <AlertDialogDescription>
                                Delete &quot;{item.fileName}&quot; and all its chunks? This cannot be undone.
                              </AlertDialogDescription>
                            </AlertDialogHeader>
                            <AlertDialogFooter>
                              <AlertDialogCancel>Cancel</AlertDialogCancel>
                              <AlertDialogAction
                                onClick={() => bulkDeleteMutation.mutate([item.id])}
                                className="bg-destructive text-destructive-foreground"
                              >
                                Delete
                              </AlertDialogAction>
                            </AlertDialogFooter>
                          </AlertDialogContent>
                        </AlertDialog>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {totalPages > 1 && (
          <div className="flex items-center justify-between px-4 py-3 border-t border-slate-200/60 dark:border-zinc-700/40">
            <p className="text-sm text-muted-foreground">
              Showing {((page - 1) * pageSize) + 1}–{Math.min(page * pageSize, total)} of {total}
            </p>
            <div className="flex items-center gap-1">
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={page <= 1}
                onClick={() => setPage(p => p - 1)}
              >
                <ChevronLeftIcon className="h-4 w-4" />
              </Button>
              <span className="px-3 text-sm">Page {page} of {totalPages}</span>
              <Button
                variant="outline"
                size="icon"
                className="h-8 w-8"
                disabled={page >= totalPages}
                onClick={() => setPage(p => p + 1)}
              >
                <ChevronRightIcon className="h-4 w-4" />
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
