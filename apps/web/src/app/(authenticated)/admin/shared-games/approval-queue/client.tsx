/**
 * Approval Queue Client Component - Issue #3537
 *
 * Route: /admin/shared-games/approval-queue
 * Features:
 * - List pending approvals with urgency indicators
 * - Bulk selection with checkboxes
 * - Bulk approve/reject actions
 * - Filters: urgency, submitter
 * - Stats cards
 */

'use client';

import { useCallback, useMemo, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  AlertCircle,
  AlertTriangle,
  ArrowLeft,
  Check,
  CheckCircle2,
  Clock,
  ExternalLink,
  Eye,
  Filter,
  Loader2,
  X,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
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
} from '@/components/ui/overlays/alert-dialog-primitives';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api, type SharedGame } from '@/lib/api';

// Urgency threshold in days
const URGENCY_THRESHOLD_DAYS = 7;

// Calculate days pending
function getDaysPending(createdAt: string): number {
  const created = new Date(createdAt);
  const now = new Date();
  const diffTime = Math.abs(now.getTime() - created.getTime());
  return Math.ceil(diffTime / (1000 * 60 * 60 * 24));
}

// Check if item is urgent
function isUrgent(createdAt: string): boolean {
  return getDaysPending(createdAt) >= URGENCY_THRESHOLD_DAYS;
}

// Queue filters type
interface QueueFilters {
  urgency: 'all' | 'urgent';
  sortBy: 'oldest' | 'newest' | 'urgent';
}

// Stats component
function QueueStats({
  total,
  urgentCount,
  isLoading,
}: {
  total: number;
  urgentCount: number;
  isLoading: boolean;
}) {
  if (isLoading) {
    return (
      <div className="grid gap-4 md:grid-cols-3">
        <Skeleton className="h-24" />
        <Skeleton className="h-24" />
        <Skeleton className="h-24" />
      </div>
    );
  }

  return (
    <div className="grid gap-4 md:grid-cols-3">
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Total Pending</CardTitle>
          <Clock className="h-4 w-4 text-muted-foreground" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">{total}</div>
          <p className="text-xs text-muted-foreground">Awaiting review</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Urgent</CardTitle>
          <AlertTriangle className="h-4 w-4 text-destructive" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold text-destructive">{urgentCount}</div>
          <p className="text-xs text-muted-foreground">{'>'}7 days pending</p>
        </CardContent>
      </Card>
      <Card>
        <CardHeader className="flex flex-row items-center justify-between space-y-0 pb-2">
          <CardTitle className="text-sm font-medium">Target SLA</CardTitle>
          <CheckCircle2 className="h-4 w-4 text-green-500" />
        </CardHeader>
        <CardContent>
          <div className="text-2xl font-bold">3 days</div>
          <p className="text-xs text-muted-foreground">Avg review time target</p>
        </CardContent>
      </Card>
    </div>
  );
}

// Individual queue item
function ApprovalQueueItem({
  game,
  isSelected,
  onSelect,
  onPreview,
  onApprove,
  onReject,
  isApproving,
  isRejecting,
}: {
  game: SharedGame;
  isSelected: boolean;
  onSelect: (id: string, selected: boolean) => void;
  onPreview: (game: SharedGame) => void;
  onApprove: (id: string) => void;
  onReject: (id: string) => void;
  isApproving: boolean;
  isRejecting: boolean;
}) {
  const daysPending = getDaysPending(game.createdAt);
  const urgent = isUrgent(game.createdAt);
  const hasImage = game.imageUrl && game.imageUrl.length > 0;

  return (
    <div
      className={`
        flex items-center gap-4 rounded-lg border p-4
        ${urgent ? 'border-destructive/50 bg-destructive/5' : ''}
        ${isSelected ? 'ring-2 ring-primary' : ''}
      `}
    >
      {/* Checkbox */}
      <Checkbox
        checked={isSelected}
        onCheckedChange={(checked) => onSelect(game.id, checked === true)}
      />

      {/* Thumbnail */}
      <div className="relative h-16 w-16 shrink-0 overflow-hidden rounded bg-muted">
        {hasImage && game.thumbnailUrl ? (
          <Image
            src={game.thumbnailUrl}
            alt={game.title}
            fill
            className="object-cover"
            sizes="64px"
          />
        ) : (
          <div className="flex h-full w-full items-center justify-center text-muted-foreground">
            <Clock className="h-6 w-6" />
          </div>
        )}
      </div>

      {/* Info */}
      <div className="flex-1 min-w-0">
        <div className="flex items-center gap-2">
          <h3 className="font-semibold truncate">{game.title}</h3>
          {urgent && (
            <Badge variant="destructive" className="shrink-0">
              <AlertTriangle className="mr-1 h-3 w-3" />
              Urgent
            </Badge>
          )}
        </div>
        <div className="flex items-center gap-3 text-sm text-muted-foreground mt-1">
          <span>{game.yearPublished || 'Unknown Year'}</span>
          {game.bggId && (
            <>
              <span>•</span>
              <a
                href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
                target="_blank"
                rel="noopener noreferrer"
                className="flex items-center gap-1 hover:text-foreground"
              >
                BGG #{game.bggId}
                <ExternalLink className="h-3 w-3" />
              </a>
            </>
          )}
        </div>
        <div className="flex items-center gap-2 mt-1">
          <Badge variant={urgent ? 'destructive' : 'secondary'} className="text-xs">
            <Clock className="mr-1 h-3 w-3" />
            {daysPending} day{daysPending !== 1 ? 's' : ''} pending
          </Badge>
        </div>
      </div>

      {/* Actions */}
      <div className="flex items-center gap-2 shrink-0">
        <Button
          variant="ghost"
          size="sm"
          onClick={() => onPreview(game)}
        >
          <Eye className="h-4 w-4" />
        </Button>
        <Button
          variant="outline"
          size="sm"
          onClick={() => onReject(game.id)}
          disabled={isRejecting || isApproving}
        >
          {isRejecting ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <X className="h-4 w-4" />
          )}
        </Button>
        <Button
          size="sm"
          onClick={() => onApprove(game.id)}
          disabled={isApproving || isRejecting}
        >
          {isApproving ? (
            <Loader2 className="h-4 w-4 animate-spin" />
          ) : (
            <Check className="h-4 w-4" />
          )}
        </Button>
      </div>
    </div>
  );
}

// Bulk action dialog
function BulkActionDialog({
  open,
  onOpenChange,
  action,
  selectedCount,
  onConfirm,
  isPending,
}: {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  action: 'approve' | 'reject';
  selectedCount: number;
  onConfirm: (reason?: string) => void;
  isPending: boolean;
}) {
  const [reason, setReason] = useState('');

  const handleConfirm = () => {
    if (action === 'reject' && !reason.trim()) {
      return;
    }
    onConfirm(action === 'reject' ? reason : undefined);
  };

  return (
    <AlertDialog open={open} onOpenChange={onOpenChange}>
      <AlertDialogContent>
        <AlertDialogHeader>
          <AlertDialogTitle>
            {action === 'approve' ? 'Approve' : 'Reject'} {selectedCount} Game{selectedCount !== 1 ? 's' : ''}
          </AlertDialogTitle>
          <AlertDialogDescription>
            {action === 'approve'
              ? `This will approve ${selectedCount} game${selectedCount !== 1 ? 's' : ''} and make them publicly visible.`
              : `This will reject ${selectedCount} game${selectedCount !== 1 ? 's' : ''} and return them to draft status.`}
          </AlertDialogDescription>
        </AlertDialogHeader>

        {action === 'reject' && (
          <div className="space-y-2 py-4">
            <Label htmlFor="rejection-reason">Rejection Reason (required)</Label>
            <Textarea
              id="rejection-reason"
              placeholder="Please provide a reason for rejection..."
              value={reason}
              onChange={(e) => setReason(e.target.value)}
              rows={3}
            />
          </div>
        )}

        <AlertDialogFooter>
          <AlertDialogCancel disabled={isPending}>Cancel</AlertDialogCancel>
          <AlertDialogAction
            onClick={handleConfirm}
            disabled={isPending || (action === 'reject' && !reason.trim())}
            className={action === 'reject' ? 'bg-destructive hover:bg-destructive/90' : ''}
          >
            {isPending ? (
              <Loader2 className="mr-2 h-4 w-4 animate-spin" />
            ) : action === 'approve' ? (
              <Check className="mr-2 h-4 w-4" />
            ) : (
              <X className="mr-2 h-4 w-4" />
            )}
            {action === 'approve' ? 'Approve' : 'Reject'} All
          </AlertDialogAction>
        </AlertDialogFooter>
      </AlertDialogContent>
    </AlertDialog>
  );
}

// Loading skeleton
function QueueSkeleton() {
  return (
    <div className="space-y-4">
      {[1, 2, 3, 4].map((i) => (
        <div key={i} className="flex items-center gap-4 rounded-lg border p-4">
          <Skeleton className="h-5 w-5" />
          <Skeleton className="h-16 w-16" />
          <div className="flex-1 space-y-2">
            <Skeleton className="h-5 w-48" />
            <Skeleton className="h-4 w-32" />
          </div>
          <div className="flex gap-2">
            <Skeleton className="h-8 w-8" />
            <Skeleton className="h-8 w-8" />
            <Skeleton className="h-8 w-8" />
          </div>
        </div>
      ))}
    </div>
  );
}

// Main component
export function ApprovalQueueClient() {
  const queryClient = useQueryClient();
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());
  const [filters, setFilters] = useState<QueueFilters>({
    urgency: 'all',
    sortBy: 'oldest',
  });
  const [bulkAction, setBulkAction] = useState<'approve' | 'reject' | null>(null);
  const [singleRejectId, setSingleRejectId] = useState<string | null>(null);
  const [previewGame, setPreviewGame] = useState<SharedGame | null>(null);

  // Fetch pending approvals
  const {
    data: pendingData,
    isLoading,
    error,
  } = useQuery({
    queryKey: ['admin', 'approval-queue'],
    queryFn: () => api.sharedGames.getPendingApprovals({ pageSize: 100 }),
    refetchInterval: 30000, // Refresh every 30 seconds
  });

  // Filter and sort items
  const filteredItems = useMemo(() => {
    if (!pendingData?.items) return [];

    let items = [...pendingData.items];

    // Filter by urgency
    if (filters.urgency === 'urgent') {
      items = items.filter((item) => isUrgent(item.createdAt));
    }

    // Sort
    switch (filters.sortBy) {
      case 'oldest':
        items.sort((a, b) => new Date(a.createdAt).getTime() - new Date(b.createdAt).getTime());
        break;
      case 'newest':
        items.sort((a, b) => new Date(b.createdAt).getTime() - new Date(a.createdAt).getTime());
        break;
      case 'urgent':
        items.sort((a, b) => getDaysPending(b.createdAt) - getDaysPending(a.createdAt));
        break;
    }

    return items;
  }, [pendingData?.items, filters]);

  // Calculate stats
  const stats = useMemo(() => {
    if (!pendingData?.items) return { total: 0, urgentCount: 0 };
    return {
      total: pendingData.items.length,
      urgentCount: pendingData.items.filter((item) => isUrgent(item.createdAt)).length,
    };
  }, [pendingData?.items]);

  // Single approve mutation
  const approveMutation = useMutation({
    mutationFn: (id: string) => api.sharedGames.approvePublication(id),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'approval-queue'] });
    },
  });

  // Single reject mutation
  const rejectMutation = useMutation({
    mutationFn: ({ id, reason }: { id: string; reason: string }) =>
      api.sharedGames.rejectPublication(id, reason),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'approval-queue'] });
      setSingleRejectId(null);
    },
  });

  // Bulk approve mutation
  const bulkApproveMutation = useMutation({
    mutationFn: async (ids: string[]) => {
      await Promise.all(ids.map((id) => api.sharedGames.approvePublication(id)));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'approval-queue'] });
      setSelectedIds(new Set());
      setBulkAction(null);
    },
  });

  // Bulk reject mutation
  const bulkRejectMutation = useMutation({
    mutationFn: async ({ ids, reason }: { ids: string[]; reason: string }) => {
      await Promise.all(ids.map((id) => api.sharedGames.rejectPublication(id, reason)));
    },
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['admin', 'approval-queue'] });
      setSelectedIds(new Set());
      setBulkAction(null);
    },
  });

  // Selection handlers
  const handleSelect = useCallback((id: string, selected: boolean) => {
    setSelectedIds((prev) => {
      const next = new Set(prev);
      if (selected) {
        next.add(id);
      } else {
        next.delete(id);
      }
      return next;
    });
  }, []);

  const handleSelectAll = useCallback(() => {
    if (selectedIds.size === filteredItems.length) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(filteredItems.map((item) => item.id)));
    }
  }, [filteredItems, selectedIds.size]);

  // Action handlers
  const handleSingleApprove = useCallback((id: string) => {
    approveMutation.mutate(id);
  }, [approveMutation]);

  const handleSingleReject = useCallback((id: string) => {
    setSingleRejectId(id);
  }, []);

  const handleConfirmSingleReject = useCallback((reason: string) => {
    if (singleRejectId) {
      rejectMutation.mutate({ id: singleRejectId, reason });
    }
  }, [singleRejectId, rejectMutation]);

  const handleBulkConfirm = useCallback((reason?: string) => {
    const ids = Array.from(selectedIds);
    if (bulkAction === 'approve') {
      bulkApproveMutation.mutate(ids);
    } else if (bulkAction === 'reject' && reason) {
      bulkRejectMutation.mutate({ ids, reason });
    }
  }, [selectedIds, bulkAction, bulkApproveMutation, bulkRejectMutation]);

  const isAllSelected = filteredItems.length > 0 && selectedIds.size === filteredItems.length;

  if (error) {
    return (
      <div className="container py-6">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {error instanceof Error ? error.message : 'Failed to load approval queue'}
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="container py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-4">
          <Link href="/admin/shared-games">
            <Button variant="ghost" size="icon">
              <ArrowLeft className="h-5 w-5" />
            </Button>
          </Link>
          <div>
            <h1 className="text-2xl font-bold">Approval Queue</h1>
            <p className="text-sm text-muted-foreground">
              Review and approve pending game submissions
            </p>
          </div>
        </div>
      </div>

      {/* Stats */}
      <QueueStats
        total={stats.total}
        urgentCount={stats.urgentCount}
        isLoading={isLoading}
      />

      {/* Toolbar */}
      <div className="flex items-center justify-between gap-4 flex-wrap">
        <div className="flex items-center gap-2">
          <Checkbox
            checked={isAllSelected}
            onCheckedChange={handleSelectAll}
            disabled={filteredItems.length === 0}
          />
          <span className="text-sm text-muted-foreground">
            {selectedIds.size > 0
              ? `${selectedIds.size} selected`
              : 'Select all'}
          </span>
        </div>

        <div className="flex items-center gap-4">
          {/* Bulk actions */}
          {selectedIds.size > 0 && (
            <div className="flex items-center gap-2">
              <Button
                variant="outline"
                size="sm"
                onClick={() => setBulkAction('reject')}
              >
                <X className="mr-2 h-4 w-4" />
                Reject ({selectedIds.size})
              </Button>
              <Button
                size="sm"
                onClick={() => setBulkAction('approve')}
              >
                <Check className="mr-2 h-4 w-4" />
                Approve ({selectedIds.size})
              </Button>
            </div>
          )}

          {/* Filters */}
          <div className="flex items-center gap-2">
            <Filter className="h-4 w-4 text-muted-foreground" />
            <Select
              value={filters.urgency}
              onValueChange={(value: 'all' | 'urgent') =>
                setFilters((prev) => ({ ...prev, urgency: value }))
              }
            >
              <SelectTrigger className="w-[130px]">
                <SelectValue placeholder="Urgency" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="all">All Items</SelectItem>
                <SelectItem value="urgent">Urgent Only</SelectItem>
              </SelectContent>
            </Select>
            <Select
              value={filters.sortBy}
              onValueChange={(value: 'oldest' | 'newest' | 'urgent') =>
                setFilters((prev) => ({ ...prev, sortBy: value }))
              }
            >
              <SelectTrigger className="w-[140px]">
                <SelectValue placeholder="Sort by" />
              </SelectTrigger>
              <SelectContent>
                <SelectItem value="oldest">Oldest First</SelectItem>
                <SelectItem value="newest">Newest First</SelectItem>
                <SelectItem value="urgent">Most Urgent</SelectItem>
              </SelectContent>
            </Select>
          </div>
        </div>
      </div>

      {/* Queue list */}
      {isLoading ? (
        <QueueSkeleton />
      ) : filteredItems.length === 0 ? (
        <Card>
          <CardContent className="flex flex-col items-center justify-center py-12 text-muted-foreground">
            <CheckCircle2 className="h-12 w-12 mb-4 opacity-50" />
            <p className="text-lg font-medium">No pending approvals</p>
            <p className="text-sm">All caught up! Check back later.</p>
          </CardContent>
        </Card>
      ) : (
        <div className="space-y-3">
          {filteredItems.map((game) => (
            <ApprovalQueueItem
              key={game.id}
              game={game}
              isSelected={selectedIds.has(game.id)}
              onSelect={handleSelect}
              onPreview={setPreviewGame}
              onApprove={handleSingleApprove}
              onReject={handleSingleReject}
              isApproving={approveMutation.isPending && approveMutation.variables === game.id}
              isRejecting={rejectMutation.isPending && rejectMutation.variables?.id === game.id}
            />
          ))}
        </div>
      )}

      {/* Bulk action dialog */}
      {bulkAction && (
        <BulkActionDialog
          open={true}
          onOpenChange={() => setBulkAction(null)}
          action={bulkAction}
          selectedCount={selectedIds.size}
          onConfirm={handleBulkConfirm}
          isPending={bulkApproveMutation.isPending || bulkRejectMutation.isPending}
        />
      )}

      {/* Single reject dialog */}
      <AlertDialog open={!!singleRejectId} onOpenChange={() => setSingleRejectId(null)}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Reject Game</AlertDialogTitle>
            <AlertDialogDescription>
              This will reject the game and return it to draft status.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <div className="space-y-2 py-4">
            <Label htmlFor="single-rejection-reason">Rejection Reason (required)</Label>
            <Input
              id="single-rejection-reason"
              placeholder="Please provide a reason..."
              onKeyDown={(e) => {
                if (e.key === 'Enter' && e.currentTarget.value.trim()) {
                  handleConfirmSingleReject(e.currentTarget.value);
                }
              }}
            />
          </div>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction
              className="bg-destructive hover:bg-destructive/90"
              onClick={(e: React.MouseEvent) => {
                const input = document.getElementById('single-rejection-reason') as HTMLInputElement;
                if (input?.value.trim()) {
                  handleConfirmSingleReject(input.value);
                }
                e.preventDefault();
              }}
              disabled={rejectMutation.isPending}
            >
              {rejectMutation.isPending && <Loader2 className="mr-2 h-4 w-4 animate-spin" />}
              Reject
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Preview - link to detail page */}
      {previewGame && (
        <AlertDialog open={true} onOpenChange={() => setPreviewGame(null)}>
          <AlertDialogContent className="max-w-lg">
            <AlertDialogHeader>
              <AlertDialogTitle>{previewGame.title}</AlertDialogTitle>
              <AlertDialogDescription>
                {previewGame.description?.slice(0, 200)}
                {(previewGame.description?.length || 0) > 200 ? '...' : ''}
              </AlertDialogDescription>
            </AlertDialogHeader>
            <div className="grid grid-cols-2 gap-4 py-4 text-sm">
              <div>
                <span className="text-muted-foreground">Year:</span>{' '}
                {previewGame.yearPublished || 'Unknown'}
              </div>
              <div>
                <span className="text-muted-foreground">Players:</span>{' '}
                {previewGame.minPlayers}-{previewGame.maxPlayers}
              </div>
              <div>
                <span className="text-muted-foreground">Time:</span>{' '}
                {previewGame.playingTimeMinutes} min
              </div>
              <div>
                <span className="text-muted-foreground">Age:</span>{' '}
                {previewGame.minAge}+
              </div>
            </div>
            <AlertDialogFooter>
              <AlertDialogCancel>Close</AlertDialogCancel>
              <Link href={`/admin/shared-games/${previewGame.id}`}>
                <AlertDialogAction>
                  <Eye className="mr-2 h-4 w-4" />
                  View Full Details
                </AlertDialogAction>
              </Link>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      )}
    </div>
  );
}
