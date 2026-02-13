'use client';

import { useState } from 'react';
import { useQuery, useMutation, useQueryClient } from '@tanstack/react-query';
import { Check, X, Eye, Search, Filter, ChevronLeft, ChevronRight } from 'lucide-react';
import { adminClient } from '@/lib/api/admin-client';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/primitives/input';
import { Select, SelectContent, SelectItem, SelectTrigger, SelectValue } from '@/components/ui/select';
import { Checkbox } from '@/components/ui/checkbox';
import { toast } from 'sonner';
import { Badge } from '@/components/ui/data-display/badge';

interface ApprovalQueueItem {
  gameId: string;
  title: string;
  submittedBy: string;
  submittedAt: string;
  daysPending: number;
  pdfCount: number;
}

export function SharedGamesSection() {
  const [page, setPage] = useState(1);
  const [statusFilter, setStatusFilter] = useState<string>('all');
  const [searchQuery, setSearchQuery] = useState('');
  const [selectedGames, setSelectedGames] = useState<Set<string>>(new Set());
  const queryClient = useQueryClient();

  const { data, isLoading } = useQuery({
    queryKey: ['approval-queue', page, statusFilter, searchQuery],
    queryFn: () =>
      adminClient.getApprovalQueue({
        page,
        pageSize: 10,
        status: statusFilter !== 'all' ? statusFilter : undefined,
        search: searchQuery || undefined,
      }),
  });

  const approveMutation = useMutation({
    mutationFn: (gameIds: string[]) => adminClient.batchApproveGames(gameIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['approval-queue'] });
      queryClient.invalidateQueries({ queryKey: ['admin-stats'] });
      setSelectedGames(new Set());
      toast.success('Selected games have been published to the catalog');
    },
  });

  const rejectMutation = useMutation({
    mutationFn: (gameIds: string[]) => adminClient.batchRejectGames(gameIds),
    onSuccess: () => {
      queryClient.invalidateQueries({ queryKey: ['approval-queue'] });
      queryClient.invalidateQueries({ queryKey: ['admin-stats'] });
      setSelectedGames(new Set());
      toast.error('Selected games have been removed from the queue');
    },
  });

  const toggleSelection = (gameId: string) => {
    const newSelection = new Set(selectedGames);
    if (newSelection.has(gameId)) {
      newSelection.delete(gameId);
    } else {
      newSelection.add(gameId);
    }
    setSelectedGames(newSelection);
  };

  const toggleSelectAll = () => {
    if (selectedGames.size === data?.items.length) {
      setSelectedGames(new Set());
    } else {
      setSelectedGames(new Set(data?.items.map((item) => item.gameId) ?? []));
    }
  };

  return (
    <div>
      {/* Section Header */}
      <div className="mb-6 flex items-center gap-2">
        <div className="h-1 w-12 bg-gradient-to-r from-amber-500 to-orange-500 rounded-full" />
        <h2 className="font-quicksand font-bold text-xl text-slate-700">
          Approval Queue
        </h2>
        <Badge variant="secondary" className="ml-2 font-nunito">
          {data?.totalCount ?? 0} pending
        </Badge>
      </div>

      {/* Filters and Actions Bar */}
      <div className="mb-6 bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 p-4">
        <div className="flex flex-col lg:flex-row gap-4">
          {/* Search */}
          <div className="flex-1 relative">
            <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-slate-400" />
            <Input
              placeholder="Search by game title..."
              value={searchQuery}
              onChange={(e: React.ChangeEvent<HTMLInputElement>) => setSearchQuery(e.target.value)}
              className="pl-10 bg-white/80 border-amber-200/60 focus:border-amber-400"
            />
          </div>

          {/* Status Filter */}
          <Select value={statusFilter} onValueChange={setStatusFilter}>
            <SelectTrigger className="w-[180px] bg-white/80 border-amber-200/60">
              <Filter className="w-4 h-4 mr-2" />
              <SelectValue placeholder="Status" />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="all">All Status</SelectItem>
              <SelectItem value="pending">Pending</SelectItem>
              <SelectItem value="urgent">Urgent (7+ days)</SelectItem>
            </SelectContent>
          </Select>

          {/* Bulk Actions */}
          {selectedGames.size > 0 && (
            <div className="flex gap-2 items-center">
              <span className="font-nunito text-sm text-slate-600">
                {selectedGames.size} selected
              </span>
              <Button
                size="sm"
                variant="default"
                onClick={() => approveMutation.mutate(Array.from(selectedGames))}
                disabled={approveMutation.isPending}
                className="bg-emerald-600 hover:bg-emerald-700"
              >
                <Check className="w-4 h-4 mr-1" />
                Approve All
              </Button>
              <Button
                size="sm"
                variant="destructive"
                onClick={() => rejectMutation.mutate(Array.from(selectedGames))}
                disabled={rejectMutation.isPending}
              >
                <X className="w-4 h-4 mr-1" />
                Reject All
              </Button>
            </div>
          )}
        </div>
      </div>

      {/* Table */}
      <div className="bg-white/70 backdrop-blur-md rounded-xl border border-amber-200/60 overflow-hidden">
        <div className="overflow-x-auto">
          <table className="w-full">
            <thead className="bg-gradient-to-r from-amber-100/80 to-orange-100/60 border-b border-amber-200/60">
              <tr>
                <th className="px-4 py-3 text-left w-12">
                  <Checkbox
                    checked={selectedGames.size === data?.items.length && data?.items.length > 0}
                    onCheckedChange={toggleSelectAll}
                  />
                </th>
                <th className="px-4 py-3 text-left font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Game Title
                </th>
                <th className="px-4 py-3 text-left font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Submitted By
                </th>
                <th className="px-4 py-3 text-center font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Days Pending
                </th>
                <th className="px-4 py-3 text-center font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  PDFs
                </th>
                <th className="px-4 py-3 text-right font-quicksand font-semibold text-sm uppercase tracking-wider text-slate-700">
                  Actions
                </th>
              </tr>
            </thead>
            <tbody className="divide-y divide-amber-100/60">
              {isLoading ? (
                [...Array(5)].map((_, i) => (
                  <tr key={i} className="animate-pulse">
                    <td className="px-4 py-4" colSpan={6}>
                      <div className="h-12 bg-slate-200/40 rounded" />
                    </td>
                  </tr>
                ))
              ) : data?.items.length === 0 ? (
                <tr>
                  <td colSpan={6} className="px-4 py-12 text-center">
                    <p className="font-nunito text-slate-500">No games in approval queue</p>
                  </td>
                </tr>
              ) : (
                data?.items.map((item) => (
                  <tr
                    key={item.gameId}
                    className="hover:bg-amber-50/40 transition-colors group"
                  >
                    <td className="px-4 py-4">
                      <Checkbox
                        checked={selectedGames.has(item.gameId)}
                        onCheckedChange={() => toggleSelection(item.gameId)}
                      />
                    </td>
                    <td className="px-4 py-4">
                      <div className="font-nunito font-semibold text-slate-900">
                        {item.title}
                      </div>
                    </td>
                    <td className="px-4 py-4">
                      <div className="font-nunito text-sm text-slate-600">
                        {item.submittedBy}
                      </div>
                      <div className="font-nunito text-xs text-slate-400">
                        {new Date(item.submittedAt).toLocaleDateString()}
                      </div>
                    </td>
                    <td className="px-4 py-4 text-center">
                      <Badge
                        variant={item.daysPending > 7 ? 'destructive' : 'secondary'}
                        className="font-nunito"
                      >
                        {item.daysPending} days
                      </Badge>
                    </td>
                    <td className="px-4 py-4 text-center">
                      <span className="font-nunito text-sm text-slate-700">
                        {item.pdfCount}
                      </span>
                    </td>
                    <td className="px-4 py-4 text-right">
                      <div className="flex items-center justify-end gap-2">
                        <Button size="sm" variant="ghost">
                          <Eye className="w-4 h-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="default"
                          className="bg-emerald-600 hover:bg-emerald-700"
                          onClick={() => approveMutation.mutate([item.gameId])}
                        >
                          <Check className="w-4 h-4" />
                        </Button>
                        <Button
                          size="sm"
                          variant="destructive"
                          onClick={() => rejectMutation.mutate([item.gameId])}
                        >
                          <X className="w-4 h-4" />
                        </Button>
                      </div>
                    </td>
                  </tr>
                ))
              )}
            </tbody>
          </table>
        </div>

        {/* Pagination */}
        {data && data.totalPages > 1 && (
          <div className="px-4 py-3 bg-gradient-to-r from-amber-50/80 to-orange-50/60 border-t border-amber-200/60 flex items-center justify-between">
            <div className="font-nunito text-sm text-slate-600">
              Page {page} of {data.totalPages} ({data.totalCount} total)
            </div>
            <div className="flex gap-2">
              <Button
                size="sm"
                variant="outline"
                onClick={() => setPage((p) => Math.max(1, p - 1))}
                disabled={page === 1}
              >
                <ChevronLeft className="w-4 h-4" />
              </Button>
              <Button
                size="sm"
                variant="outline"
                onClick={() => setPage((p) => Math.min(data.totalPages, p + 1))}
                disabled={page === data.totalPages}
              >
                <ChevronRight className="w-4 h-4" />
              </Button>
            </div>
          </div>
        )}
      </div>
    </div>
  );
}
