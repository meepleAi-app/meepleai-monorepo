'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Check, X, Search, Filter, Grid3x3, List, Clock } from 'lucide-react';
import Link from 'next/link';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { useToast } from '@/hooks/use-toast';
import { Badge } from '@/components/ui/badge';
import { adminClient } from '@/lib/api/admin-client';

type ViewMode = 'grid' | 'list';

export function SharedGamesBlock() {
  const [viewMode, setViewMode] = useState<ViewMode>('grid');
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const { toast } = useToast();
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['approval-queue', statusFilter, searchQuery],
    queryFn: () =>
      adminClient.getApprovalQueue({
        page: 1,
        pageSize: 6,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        search: searchQuery || undefined,
      }),
  });

  const approveMutation = useMutation({
    mutationFn: (gameId: string) => adminClient.batchApproveGames([gameId]),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['approval-queue'] });
      queryClient.invalidateQueries({ queryKey: ['admin-stats'] });
      toast({
        title: 'Game approved',
        description: 'Game has been published to the catalog',
      });
    },
  });

  const rejectMutation = useMutation({
    mutationFn: (gameId: string) => adminClient.batchRejectGames([gameId]),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['approval-queue'] });
      queryClient.invalidateQueries({ queryKey: ['admin-stats'] });
      toast({
        title: 'Game rejected',
        description: 'Game has been removed from the queue',
        variant: 'destructive',
      });
    },
  });

  return (
    <div className="space-y-6">
      {/* Block Header */}
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3">
          <div className="h-1 w-12 bg-gradient-to-r from-amber-500 to-orange-500 rounded-full" />
          <h2 className="font-quicksand font-bold text-2xl text-slate-900">
            Approval Queue
          </h2>
          <Badge variant="secondary" className="font-nunito">
            {data?.totalCount ?? 0} pending
          </Badge>
        </div>
        <Link
          href="/admin/shared-games/approvals"
          className="font-nunito text-sm text-amber-600 hover:text-amber-700 font-semibold"
        >
          View All →
        </Link>
      </div>

      {/* Filters and View Toggle */}
      <div className="bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 p-4">
        <div className="flex flex-col sm:flex-row gap-4 items-center">
          {/* Search */}
          <div className="flex-1 w-full relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <Input
              placeholder="Search games..."
              value={searchQuery}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
              className="pl-10 bg-white/80 border-amber-200/60 focus:border-amber-400"
            />
          </div>

          {/* Status Filter */}
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[160px] bg-white/80 border-amber-200/60">
              <Filter className="w-4 h-4 mr-2" />
              <SelectValue placeholder="Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Status</SelectItem>
              <SelectItem value="pending">Pending</SelectItem>
              <SelectItem value="urgent">Urgent (7+ days)</SelectItem>
            </SelectContent>
          </Select>

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
        <div className="text-center py-12 bg-white/50 backdrop-blur-sm rounded-xl border border-amber-200/60">
          <p className="font-nunito text-slate-500">No games in approval queue</p>
        </div>
      ) : (
        <div className={viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6' : 'space-y-4'}>
          {data?.items.map((item) => (
            <MeepleCard
              key={item.gameId}
              id={item.gameId}
              entity="game"
              variant={viewMode === 'grid' ? 'grid' : 'list'}
              title={item.title}
              subtitle={`Submitted by ${item.submittedBy}`}
              metadata={[
                { label: `${item.daysPending} days pending` },
                { label: `${item.pdfCount} PDFs` },
              ]}
              actions={[
                {
                  label: 'Approve',
                  primary: true,
                  onClick: () => approveMutation.mutate(item.gameId),
                },
                {
                  label: 'Reject',
                  onClick: () => rejectMutation.mutate(item.gameId),
                },
              ]}
              badge={item.daysPending > 7 ? 'URGENT' : 'PENDING'}
            />
          ))}
        </div>
      )}
    </div>
  );
}
