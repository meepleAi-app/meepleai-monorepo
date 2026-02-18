'use client';

import { useState, useCallback } from 'react';

import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Search, Filter, Grid3x3, List, CheckSquare, Square } from 'lucide-react';
import Link from 'next/link';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { useDebounce } from '@/hooks/useDebounce';
import { adminClient } from '@/lib/api/admin-client';

type ViewMode = 'grid' | 'list';

export function SharedGamesBlock() {
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const debouncedSearch = useDebounce(searchQuery, 300);
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const queryKey = ['approval-queue', statusFilter, debouncedSearch];

  const { data, isLoading } = useQuery({
    queryKey,
    queryFn: () =>
      adminClient.getApprovalQueue({
        page: 1,
        pageSize: 6,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        search: debouncedSearch || undefined,
      }),
    staleTime: 2 * 60 * 1000,
  });

  const approveMutation = useMutation({
    mutationFn: (gameIds: string[]) => adminClient.batchApproveGames(gameIds),
    onMutate: async (gameIds) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData(queryKey);
      queryClient.setQueryData(queryKey, (old: typeof data) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.filter((item) => !gameIds.includes(item.gameId)),
          totalCount: old.totalCount - gameIds.length,
        };
      });
      return { previous };
    },
    onError: (_err, _gameIds, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      toast({
        title: 'Error',
        description: 'Failed to approve games. Please try again.',
        variant: 'destructive',
      });
    },
    onSuccess: (_data, gameIds) => {
      setSelectedIds((prev) => {
        const next = new Set(prev);
        gameIds.forEach((id) => next.delete(id));
        return next;
      });
      queryClient.invalidateQueries({ queryKey: ['approval-queue'] });
      queryClient.invalidateQueries({ queryKey: ['admin-stats'] });
      toast({
        title: gameIds.length > 1 ? `${gameIds.length} games approved` : 'Game approved',
        description: 'Published to the catalog',
      });
    },
  });

  const rejectMutation = useMutation({
    mutationFn: (gameIds: string[]) => adminClient.batchRejectGames(gameIds),
    onMutate: async (gameIds) => {
      await queryClient.cancelQueries({ queryKey });
      const previous = queryClient.getQueryData(queryKey);
      queryClient.setQueryData(queryKey, (old: typeof data) => {
        if (!old) return old;
        return {
          ...old,
          items: old.items.filter((item) => !gameIds.includes(item.gameId)),
          totalCount: old.totalCount - gameIds.length,
        };
      });
      return { previous };
    },
    onError: (_err, _gameIds, context) => {
      if (context?.previous) {
        queryClient.setQueryData(queryKey, context.previous);
      }
      toast({
        title: 'Error',
        description: 'Failed to reject games. Please try again.',
        variant: 'destructive',
      });
    },
    onSuccess: (_data, gameIds) => {
      setSelectedIds((prev) => {
        const next = new Set(prev);
        gameIds.forEach((id) => next.delete(id));
        return next;
      });
      queryClient.invalidateQueries({ queryKey: ['approval-queue'] });
      queryClient.invalidateQueries({ queryKey: ['admin-stats'] });
      toast({
        title: gameIds.length > 1 ? `${gameIds.length} games rejected` : 'Game rejected',
        description: 'Removed from the queue',
        variant: 'destructive',
      });
    },
  });

  const toggleSelection = useCallback((gameId: string) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (next.has(gameId)) {
        next.delete(gameId);
      } else {
        next.add(gameId);
      }
      return next;
    });
  }, []);

  const toggleSelectAll = useCallback(() => {
    if (!data?.items) return;
    setSelectedIds((prev) => {
      const allIds = data.items.map((item) => item.gameId);
      const allSelected = allIds.every((id) => prev.has(id));
      return allSelected ? new Set() : new Set(allIds);
    });
  }, [data?.items]);

  const hasSelection = selectedIds.size > 0;
  const allSelected = data?.items && data.items.length > 0 && data.items.every((item) => selectedIds.has(item.gameId));

  return (
    <div className="space-y-6">
      {/* Block Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <h2 className="text-xl font-semibold text-foreground">
            Approval Queue
          </h2>
          <Badge variant="secondary">
            {data?.totalCount ?? 0} pending
          </Badge>
        </div>
        <Link
          href="/admin/shared-games/approvals"
          className="text-sm text-primary hover:text-primary/80 font-medium transition-colors"
        >
          View All →
        </Link>
      </div>

      {/* Filters and View Toggle */}
      <div className="bg-card border rounded-lg p-4">
        <div className="flex flex-col sm:flex-row gap-4 items-center">
          {/* Search */}
          <div className="flex-1 w-full relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <Input
              placeholder="Search games..."
              value={searchQuery}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
              className="pl-10"
            />
          </div>

          {/* Status Filter */}
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[160px] bg-background">
              <Filter className="w-4 h-4 mr-2" />
              <SelectValue placeholder="Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Status</SelectItem>
              <SelectItem value="pending">Pending</SelectItem>
              <SelectItem value="urgent">Urgent (7+ days)</SelectItem>
            </SelectContent>
          </Select>

          {/* Select All Toggle */}
          <Button
            size="sm"
            variant="ghost"
            onClick={toggleSelectAll}
            className="h-8 gap-1.5"
            aria-label={allSelected ? 'Deselect all' : 'Select all'}
          >
            {allSelected ? <CheckSquare className="w-4 h-4" /> : <Square className="w-4 h-4" />}
            <span className="text-xs">All</span>
          </Button>

          {/* View Toggle */}
          <div className="flex gap-1 bg-white/80 border border-amber-200/60 rounded-lg p-1">
            <Button
              size="sm"
              variant={viewMode === 'grid' ? 'default' : 'ghost'}
              onClick={() => setViewMode('grid')}
              className="h-8 w-8 p-0"
            >
              <Grid3x3 className="w-4 h-4" />
            </Button>
            <Button
              size="sm"
              variant={viewMode === 'list' ? 'default' : 'ghost'}
              onClick={() => setViewMode('list')}
              className="h-8 w-8 p-0"
            >
              <List className="w-4 h-4" />
            </Button>
          </div>
        </div>
      </div>

      {/* Bulk Actions Bar */}
      {hasSelection && (
        <div className="flex items-center gap-3 bg-amber-50 border border-amber-200 rounded-lg px-4 py-3" data-testid="bulk-actions">
          <span className="text-sm font-medium text-amber-900">
            {selectedIds.size} selected
          </span>
          <Button
            size="sm"
            variant="default"
            onClick={() => approveMutation.mutate(Array.from(selectedIds))}
            disabled={approveMutation.isPending}
          >
            Batch Approve
          </Button>
          <Button
            size="sm"
            variant="destructive"
            onClick={() => rejectMutation.mutate(Array.from(selectedIds))}
            disabled={rejectMutation.isPending}
          >
            Batch Reject
          </Button>
        </div>
      )}

      {/* Games Display - Using MeepleCard */}
      {isLoading ? (
        <div className={viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6' : 'space-y-4'}>
          {[...Array(6)].map((_, i) => (
            <div
              key={i}
              className="h-[280px] bg-white/40 backdrop-blur-sm rounded-xl border border-slate-200/60 animate-pulse"
            />
          ))}
        </div>
      ) : !data || !data.items || data.items.length === 0 ? (
        <div className="text-center py-12 bg-card border rounded-lg">
          <p className="font-nunito text-slate-500">No games in approval queue</p>
        </div>
      ) : (
        <div className={viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6' : 'space-y-4'}>
          {data?.items.map((item) => (
            <div key={item.gameId} className="relative">
              {/* Selection checkbox */}
              <button
                onClick={() => toggleSelection(item.gameId)}
                className="absolute top-3 left-3 z-10 p-1 rounded bg-white/80 border border-slate-200 hover:bg-white transition-colors"
                aria-label={selectedIds.has(item.gameId) ? `Deselect ${item.title}` : `Select ${item.title}`}
              >
                {selectedIds.has(item.gameId) ? (
                  <CheckSquare className="w-4 h-4 text-amber-600" />
                ) : (
                  <Square className="w-4 h-4 text-slate-400" />
                )}
              </button>
              <MeepleCard
                id={item.gameId}
                entity="game"
                variant={viewMode === 'grid' ? 'grid' : 'list'}
                title={item.title}
                subtitle={`Submitted by ${item.submittedByName}`}
                metadata={[
                  { label: `${item.daysPending} days pending` },
                  { label: `${item.pdfCount} PDFs` },
                ]}
                actions={[
                  {
                    label: 'Approve',
                    primary: true,
                    onClick: () => approveMutation.mutate([item.gameId]),
                  },
                  {
                    label: 'Reject',
                    onClick: () => rejectMutation.mutate([item.gameId]),
                  },
                ]}
                badge={item.daysPending > 7 ? 'URGENT' : 'PENDING'}
              />
            </div>
          ))}
        </div>
      )}
    </div>
  );
}
