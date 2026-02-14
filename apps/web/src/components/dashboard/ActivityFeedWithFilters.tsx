/**
 * ActivityFeedWithFilters - Activity Feed with Advanced Filters & URL State
 *
 * Issue #3925 - Frontend: Advanced Timeline Filters & Search
 *
 * Integrates ActivityFeedFilters + ActivityFeed with:
 * - URL params sync (/dashboard?filter=sessions&search=catan)
 * - React Query data fetching with filters
 * - Debounced search (300ms)
 * - Page refresh preservation
 *
 * @example
 * ```tsx
 * <ActivityFeedWithFilters />
 * ```
 */

'use client';

import { useState, useEffect, useCallback, useMemo } from 'react';

import { Clock } from 'lucide-react';

import { useActivityTimeline } from '@/hooks/useActivityTimeline';
import { useActivityTimelineParams } from '@/hooks/useActivityTimelineParams';
import { useDebounce } from '@/hooks/useDebounce';
import { cn } from '@/lib/utils';

import { ActivityFeed, type ActivityEvent } from './ActivityFeed';
import { ActivityFeedFilters, type ActivityFilters, type ActivityCounts } from './ActivityFeedFilters';

// ============================================================================
// Types
// ============================================================================

export interface ActivityFeedWithFiltersProps {
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helpers
// ============================================================================

/**
 * Convert API timeline items to ActivityFeed event format
 */
function toActivityEvent(item: {
  id: string;
  type: string;
  gameId?: string;
  gameName?: string;
  sessionId?: string;
  chatId?: string;
  topic?: string;
  timestamp: string;
}): ActivityEvent {
  const type = item.type as ActivityEvent['type'];

  let title: string;
  switch (type) {
    case 'game_added':
      title = `Aggiunto "${item.gameName || 'Gioco'}"`;
      break;
    case 'session_completed':
      title = `Giocato "${item.gameName || 'Sessione'}"`;
      break;
    case 'chat_saved':
      title = `Chat "${item.topic || 'Conversazione'}"`;
      break;
    case 'wishlist_added':
      title = `Wishlist "${item.gameName || 'Gioco'}"`;
      break;
    case 'achievement_unlocked':
      title = `Achievement: "${item.gameName || 'Obiettivo'}"`;
      break;
    default:
      title = item.gameName || item.topic || 'Attività';
  }

  return {
    id: item.id,
    type,
    title,
    entityId: item.gameId || item.sessionId || item.chatId,
    entityType:
      type === 'game_added' ? 'game'
        : type === 'session_completed' ? 'session'
          : type === 'chat_saved' ? 'chat'
            : type === 'wishlist_added' ? 'wishlist'
              : 'achievement',
    timestamp: item.timestamp,
  };
}

// ============================================================================
// Component
// ============================================================================

export function ActivityFeedWithFilters({ className }: ActivityFeedWithFiltersProps) {
  const { params, setTypes, setSearch, clearAll, hasActiveFilters } = useActivityTimelineParams();

  // Local search input state for debounce
  const [searchInput, setSearchInput] = useState(params.search);
  const debouncedSearch = useDebounce(searchInput, 300);

  // Sync debounced search to URL params
  useEffect(() => {
    if (debouncedSearch !== params.search) {
      setSearch(debouncedSearch);
    }
  }, [debouncedSearch, params.search, setSearch]);

  // Sync URL search to local input (e.g., on page load or back/forward)
  useEffect(() => {
    setSearchInput(params.search);
  }, [params.search]);

  // Fetch timeline data with current filters
  const { data, isLoading } = useActivityTimeline({
    types: params.types,
    search: debouncedSearch,
    skip: params.skip,
    take: params.take,
    order: params.order,
  });

  // Convert to ActivityFeed format
  const events: ActivityEvent[] = useMemo(
    () => (data?.items || []).map(toActivityEvent),
    [data?.items]
  );

  // Build filter state for ActivityFeedFilters component
  const filterState: ActivityFilters = useMemo(
    () => ({
      types: params.types,
      search: searchInput,
    }),
    [params.types, searchInput]
  );

  // Compute activity counts from data (placeholder - could come from API)
  const counts: ActivityCounts | undefined = useMemo(() => {
    if (!data?.items) return undefined;
    const items = data.items;
    return {
      game_added: items.filter((i) => i.type === 'game_added').length,
      session_completed: items.filter((i) => i.type === 'session_completed').length,
      chat_saved: items.filter((i) => i.type === 'chat_saved').length,
      wishlist_added: items.filter((i) => i.type === 'wishlist_added').length,
      achievement_unlocked: items.filter((i) => i.type === 'achievement_unlocked').length,
      total: data.totalCount,
    };
  }, [data]);

  // Handle filter changes from ActivityFeedFilters component
  const handleFiltersChange = useCallback(
    (newFilters: ActivityFilters) => {
      // If types changed, update URL
      if (newFilters.types !== filterState.types) {
        setTypes(newFilters.types);
      }
      // Search is handled via local state + debounce
      if (newFilters.search !== searchInput) {
        setSearchInput(newFilters.search);
      }
    },
    [filterState.types, searchInput, setTypes]
  );

  // Handle clear all (reset both URL and local state)
  const handleClearAll = useCallback(() => {
    setSearchInput('');
    clearAll();
  }, [clearAll]);

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="activity-feed-with-filters"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-slate-500/20 flex items-center justify-center">
            <Clock className="h-4 w-4 text-slate-600 dark:text-slate-400" />
          </div>
          <h3 className="font-semibold text-sm" data-testid="activity-feed-title">
            Attività Recente
          </h3>
        </div>
        {hasActiveFilters && (
          <button
            type="button"
            onClick={handleClearAll}
            className="text-xs text-muted-foreground hover:text-destructive transition-colors"
            data-testid="clear-filters-header"
          >
            Reset filtri
          </button>
        )}
      </div>

      {/* Filters */}
      <ActivityFeedFilters
        filters={filterState}
        onFiltersChange={handleFiltersChange}
        counts={counts}
        isLoading={isLoading}
        className="mb-4"
      />

      {/* Feed */}
      <ActivityFeed
        events={events}
        totalCount={data?.totalCount}
        isLoading={isLoading}
        limit={20}
      />
    </section>
  );
}

export default ActivityFeedWithFilters;
