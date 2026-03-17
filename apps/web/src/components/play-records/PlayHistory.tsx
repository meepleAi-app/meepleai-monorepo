/**
 * PlayHistory - Paginated Play Records List with Filters
 *
 * Features:
 * - Paginated session list using MeepleCard
 * - Filters: game, status, date range, search
 * - Sort: recent, oldest, game name, duration
 * - Grid/list view toggle
 * - Empty state
 *
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { useState, useEffect } from 'react';

import { Calendar, Filter, Grid3X3, List, Search, X, AlertCircle } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import type { PlayRecordStatus } from '@/lib/api/schemas/play-records.schemas';
import { usePlayHistory } from '@/lib/domain-hooks/usePlayRecords';
import {
  usePlayRecordsStore,
  selectFilters,
  selectHasActiveFilters,
} from '@/lib/stores/play-records-store';

export interface PlayHistoryProps {
  userId?: string; // Optional: filter by specific user (default: current user)
}

export function PlayHistory({ userId: _userId }: PlayHistoryProps) {
  const router = useRouter();
  const [currentPage, setCurrentPage] = useState(1);

  const filters = usePlayRecordsStore(selectFilters);
  const viewMode = usePlayRecordsStore(state => state.viewMode);
  const sortBy = usePlayRecordsStore(state => state.sortBy);
  const sidebarOpen = usePlayRecordsStore(state => state.sidebarOpen);
  const hasActiveFilters = usePlayRecordsStore(selectHasActiveFilters);

  const setFilter = usePlayRecordsStore(state => state.setFilter);
  const resetFilters = usePlayRecordsStore(state => state.resetFilters);
  const setViewMode = usePlayRecordsStore(state => state.setViewMode);
  const setSortBy = usePlayRecordsStore(state => state.setSortBy);
  const toggleSidebar = usePlayRecordsStore(state => state.toggleSidebar);

  // Fetch play history
  const { data, isLoading, error } = usePlayHistory({
    page: currentPage,
    pageSize: 20,
    gameId: filters.gameId,
    status: filters.status === 'all' ? undefined : filters.status,
    dateFrom: filters.dateFrom,
    dateTo: filters.dateTo,
  });

  // Reset to page 1 when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [filters.gameId, filters.status, filters.dateFrom, filters.dateTo]);

  const handleCardClick = (recordId: string) => {
    router.push(`/play-records/${recordId}`);
  };

  const formatDuration = (duration: string | null): string => {
    if (!duration) return 'N/A';
    // Handle .NET TimeSpan format "HH:MM:SS" or "D.HH:MM:SS"
    // eslint-disable-next-line security/detect-unsafe-regex -- anchored regex, no backtracking risk
    const dotNetMatch = duration.match(/^(?:(\d+)\.)?(\d+):(\d+):(\d+)$/);
    if (dotNetMatch) {
      const days = dotNetMatch[1] ? parseInt(dotNetMatch[1]) : 0;
      const hours = parseInt(dotNetMatch[2]) + days * 24;
      const minutes = parseInt(dotNetMatch[3]);
      const h = hours > 0 ? `${hours}h` : '';
      const m = minutes > 0 ? `${minutes}m` : '';
      return [h, m].filter(Boolean).join(' ') || 'N/A';
    }
    // Fallback: ISO 8601 duration (e.g., "PT2H30M")
    // eslint-disable-next-line security/detect-unsafe-regex -- ISO 8601 duration, anchored and safe
    const isoMatch = duration.match(/^PT(?:(\d+)H)?(?:(\d+)M)?$/);
    if (isoMatch) {
      const hours = isoMatch[1] ? `${isoMatch[1]}h` : '';
      const minutes = isoMatch[2] ? `${isoMatch[2]}m` : '';
      return [hours, minutes].filter(Boolean).join(' ') || 'N/A';
    }
    return duration;
  };

  const getStatusColor = (status: string): string => {
    switch (status) {
      case 'Completed':
        return '120 100% 35%'; // Green
      case 'InProgress':
        return '25 95% 45%'; // Orange
      case 'Planned':
        return '220 70% 50%'; // Blue
      case 'Archived':
        return '0 0% 50%'; // Gray
      default:
        return '220 70% 50%';
    }
  };

  return (
    <div className="space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h2 className="text-2xl font-bold">Play History</h2>
          <p className="text-muted-foreground">
            Your recorded game sessions
            {data && ` (${data.totalCount} total)`}
          </p>
        </div>

        {/* View Controls */}
        <div className="flex items-center gap-2">
          <Button
            variant={viewMode === 'grid' ? 'default' : 'outline'}
            size="icon"
            onClick={() => setViewMode('grid')}
            aria-label="Grid view"
          >
            <Grid3X3 className="w-4 h-4" />
          </Button>
          <Button
            variant={viewMode === 'list' ? 'default' : 'outline'}
            size="icon"
            onClick={() => setViewMode('list')}
            aria-label="List view"
          >
            <List className="w-4 h-4" />
          </Button>
          <Button variant="outline" size="icon" onClick={toggleSidebar} aria-label="Toggle filters">
            <Filter className="w-4 h-4" />
          </Button>
        </div>
      </div>

      {/* Filters Bar */}
      <div className={`grid gap-4 ${sidebarOpen ? 'grid-cols-4' : 'grid-cols-1'}`}>
        {sidebarOpen && (
          <>
            <div>
              <Label htmlFor="search" className="text-sm">
                Search
              </Label>
              <div className="relative mt-1">
                <Search className="absolute left-3 top-1/2 -translate-y-1/2 w-4 h-4 text-muted-foreground" />
                <Input
                  id="search"
                  placeholder="Search sessions..."
                  value={filters.searchQuery}
                  onChange={e => setFilter('searchQuery', e.target.value)}
                  className="pl-9"
                />
              </div>
            </div>

            <div>
              <Label htmlFor="status" className="text-sm">
                Status
              </Label>
              <Select
                value={filters.status}
                onValueChange={value => setFilter('status', value as PlayRecordStatus | 'all')}
              >
                <SelectTrigger id="status" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="all">All Sessions</SelectItem>
                  <SelectItem value="Completed">Completed</SelectItem>
                  <SelectItem value="InProgress">In Progress</SelectItem>
                  <SelectItem value="Planned">Planned</SelectItem>
                  <SelectItem value="Archived">Archived</SelectItem>
                </SelectContent>
              </Select>
            </div>

            <div>
              <Label htmlFor="sort" className="text-sm">
                Sort By
              </Label>
              <Select
                value={sortBy}
                onValueChange={value =>
                  setSortBy(value as 'recent' | 'oldest' | 'game' | 'duration')
                }
              >
                <SelectTrigger id="sort" className="mt-1">
                  <SelectValue />
                </SelectTrigger>
                <SelectContent>
                  <SelectItem value="recent">Most Recent</SelectItem>
                  <SelectItem value="oldest">Oldest First</SelectItem>
                  <SelectItem value="game">Game Name</SelectItem>
                  <SelectItem value="duration">Duration</SelectItem>
                </SelectContent>
              </Select>
            </div>

            {hasActiveFilters && (
              <div className="flex items-end">
                <Button variant="ghost" size="sm" onClick={resetFilters} className="w-full">
                  <X className="w-4 h-4 mr-2" />
                  Clear Filters
                </Button>
              </div>
            )}
          </>
        )}
      </div>

      {/* Loading State */}
      {isLoading && (
        <div className="grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-3">
          {[...Array(6)].map((_, i) => (
            <div key={i} className="h-48 bg-muted animate-pulse rounded-lg" />
          ))}
        </div>
      )}

      {/* Error State */}
      {error && (
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {error instanceof Error ? error.message : 'Failed to load play history'}
          </AlertDescription>
        </Alert>
      )}

      {/* Empty State */}
      {!isLoading && !error && data?.records.length === 0 && (
        <div className="text-center py-12 text-muted-foreground">
          <Calendar className="w-16 h-16 mx-auto mb-4 opacity-20" />
          <h3 className="text-lg font-semibold mb-2">No Play Records Yet</h3>
          <p className="mb-4">
            {hasActiveFilters
              ? 'No sessions match your filters'
              : 'Start recording your game sessions'}
          </p>
          {hasActiveFilters && (
            <Button variant="outline" onClick={resetFilters}>
              Clear Filters
            </Button>
          )}
        </div>
      )}

      {/* Records Grid/List */}
      {!isLoading && !error && data && data.records.length > 0 && (
        <div
          className={
            viewMode === 'grid'
              ? 'grid gap-4 grid-cols-1 md:grid-cols-2 lg:grid-cols-3'
              : 'space-y-3'
          }
        >
          {data.records.map(record => (
            <MeepleCard
              key={record.id}
              entity="custom"
              customColor={getStatusColor(record.status)}
              variant={viewMode === 'grid' ? 'grid' : 'list'}
              title={record.gameName}
              subtitle={new Date(record.sessionDate).toLocaleDateString('en-US', {
                month: 'short',
                day: 'numeric',
                year: 'numeric',
              })}
              metadata={[
                { label: `${record.playerCount} players` },
                { label: formatDuration(record.duration) },
                { label: record.status },
              ]}
              badge={record.status}
              onClick={() => handleCardClick(record.id)}
              data-testid={`play-record-${record.id}`}
            />
          ))}
        </div>
      )}

      {/* Pagination */}
      {data && data.totalPages > 1 && (
        <div className="flex items-center justify-center gap-2">
          <Button
            variant="outline"
            size="sm"
            disabled={currentPage === 1}
            onClick={() => setCurrentPage(prev => Math.max(1, prev - 1))}
          >
            Previous
          </Button>
          <span className="text-sm text-muted-foreground">
            Page {currentPage} of {data.totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            disabled={currentPage >= data.totalPages}
            onClick={() => setCurrentPage(prev => Math.min(data.totalPages, prev + 1))}
          >
            Next
          </Button>
        </div>
      )}
    </div>
  );
}
