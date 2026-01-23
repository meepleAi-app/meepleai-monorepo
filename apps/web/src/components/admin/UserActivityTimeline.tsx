/**
 * UserActivityTimeline Component - Issue #911
 *
 * Comprehensive user activity timeline with filtering and pagination.
 * Features:
 * - Vertical timeline layout
 * - Activity type icons
 * - Relative timestamps (Italian)
 * - Expandable metadata
 * - Pagination support
 * - Filter by type and severity
 */

import { useState, useMemo } from 'react';

import { ActivityIcon, FilterIcon } from 'lucide-react';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';

import { UserActivityFilters, UserActivityFiltersState } from './UserActivityFilters';
import { UserActivityItem, UserActivityEvent } from './UserActivityItem';

export interface UserActivityTimelineProps {
  events: UserActivityEvent[];
  className?: string;
  /** Number of items per page (default: 10) */
  pageSize?: number;
  /** Show filters panel (default: true) */
  showFilters?: boolean;
  /** Controlled pagination: current page (1-indexed) */
  currentPage?: number;
  /** Controlled pagination: page change handler */
  onPageChange?: (page: number) => void;
}

/**
 * Simple pagination component
 */
interface PaginationProps {
  currentPage: number;
  totalPages: number;
  onPageChange: (page: number) => void;
  pageSize: number;
  onPageSizeChange: (size: number) => void;
}

function Pagination({
  currentPage,
  totalPages,
  onPageChange,
  pageSize,
  onPageSizeChange,
}: PaginationProps) {
  const PAGE_SIZES = [10, 20, 50];

  return (
    <div className="flex items-center justify-between px-6 py-4 border-t border-gray-200">
      <div className="flex items-center gap-2 text-sm text-muted-foreground">
        <span>Mostra:</span>
        <select
          value={pageSize}
          onChange={e => onPageSizeChange(Number(e.target.value))}
          className="border border-gray-300 rounded px-2 py-1 text-sm"
          aria-label="Items per page"
        >
          {PAGE_SIZES.map(size => (
            <option key={size} value={size}>
              {size}
            </option>
          ))}
        </select>
        <span>per pagina</span>
      </div>

      <div className="flex items-center gap-2">
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(currentPage - 1)}
          disabled={currentPage === 1}
          aria-label="Previous page"
        >
          Precedente
        </Button>
        <span className="text-sm text-muted-foreground">
          Pagina {currentPage} di {totalPages}
        </span>
        <Button
          variant="outline"
          size="sm"
          onClick={() => onPageChange(currentPage + 1)}
          disabled={currentPage === totalPages}
          aria-label="Next page"
        >
          Successiva
        </Button>
      </div>
    </div>
  );
}

export function UserActivityTimeline({
  events,
  className,
  pageSize: initialPageSize = 10,
  showFilters = true,
  currentPage: controlledPage,
  onPageChange: controlledOnPageChange,
}: UserActivityTimelineProps) {
  // Filters state
  const availableEventTypes = useMemo(
    () => Array.from(new Set(events.map(e => e.eventType))),
    [events]
  );

  const [filters, setFilters] = useState<UserActivityFiltersState>({
    eventTypes: new Set(availableEventTypes),
    severities: new Set(['Info', 'Warning', 'Error', 'Critical']),
  });

  const [showFiltersPanel, setShowFiltersPanel] = useState(showFilters);

  // Pagination state (controlled or uncontrolled)
  const [uncontrolledPage, setUncontrolledPage] = useState(1);
  const [pageSize, setPageSize] = useState(initialPageSize);

  const currentPage = controlledPage ?? uncontrolledPage;
  const onPageChange = controlledOnPageChange ?? setUncontrolledPage;

  // Expanded state
  const [expandedEventId, setExpandedEventId] = useState<string | null>(null);

  // Filter events
  const filteredEvents = useMemo(() => {
    return events.filter(event => {
      const typeMatch = filters.eventTypes.has(event.eventType);
      const severityMatch = filters.severities.has(event.severity || 'Info');
      return typeMatch && severityMatch;
    });
  }, [events, filters]);

  // Pagination
  const totalPages = Math.ceil(filteredEvents.length / pageSize);
  const startIndex = (currentPage - 1) * pageSize;
  const endIndex = startIndex + pageSize;
  const paginatedEvents = filteredEvents.slice(startIndex, endIndex);

  // Handle page size change - reset to page 1
  const handlePageSizeChange = (newSize: number) => {
    setPageSize(newSize);
    onPageChange(1);
  };

  return (
    <Card className={className}>
      <CardHeader className="pb-4">
        <div className="flex items-center justify-between">
          <CardTitle className="text-lg font-semibold">User Activity Timeline</CardTitle>
          {showFilters && (
            <Button
              variant="outline"
              size="sm"
              onClick={() => setShowFiltersPanel(!showFiltersPanel)}
              className="flex items-center gap-2"
              aria-expanded={showFiltersPanel}
              aria-label={showFiltersPanel ? 'Nascondi filtri' : 'Mostra filtri'}
            >
              <FilterIcon className="h-4 w-4" aria-hidden="true" />
              {showFiltersPanel ? 'Nascondi Filtri' : 'Mostra Filtri'}
            </Button>
          )}
        </div>

        {/* Filters Panel */}
        {showFiltersPanel && (
          <div className="mt-4 pt-4 border-t border-gray-200">
            <UserActivityFilters
              filters={filters}
              onFiltersChange={setFilters}
              availableEventTypes={availableEventTypes}
            />
          </div>
        )}
      </CardHeader>

      <CardContent className="p-0">
        {/* Empty State */}
        {filteredEvents.length === 0 && (
          <div className="p-8 text-center text-muted-foreground">
            <ActivityIcon className="h-12 w-12 mx-auto mb-2 text-muted-foreground/50" aria-hidden="true" />
            <p className="font-medium">Nessuna attività trovata</p>
            <p className="text-sm mt-1">
              {events.length > 0
                ? 'Prova a modificare i filtri'
                : 'Non ci sono attività da visualizzare'}
            </p>
          </div>
        )}

        {/* Event List */}
        {paginatedEvents.length > 0 && (
          <>
            <div
              className="max-h-[600px] overflow-y-auto"
              role="region"
              aria-label="Activity timeline"
            >
              <ul className="divide-y divide-gray-100" role="list">
                {paginatedEvents.map(event => (
                  <UserActivityItem
                    key={event.id}
                    event={event}
                    isExpanded={expandedEventId === event.id}
                    onToggleExpand={id => setExpandedEventId(expandedEventId === id ? null : id)}
                  />
                ))}
              </ul>
            </div>

            {/* Pagination */}
            {totalPages > 1 && (
              <Pagination
                currentPage={currentPage}
                totalPages={totalPages}
                onPageChange={onPageChange}
                pageSize={pageSize}
                onPageSizeChange={handlePageSizeChange}
              />
            )}
          </>
        )}

        {/* Results Summary */}
        {filteredEvents.length > 0 && (
          <div className="px-6 py-3 bg-muted/50 dark:bg-card border-t border-border/50 dark:border-border/30 text-xs text-muted-foreground">
            Visualizzati {startIndex + 1}-{Math.min(endIndex, filteredEvents.length)} di{' '}
            {filteredEvents.length} {filteredEvents.length === 1 ? 'evento' : 'eventi'}
            {filteredEvents.length < events.length && (
              <span className="ml-2">
                ({events.length - filteredEvents.length} nascosti dai filtri)
              </span>
            )}
          </div>
        )}
      </CardContent>
    </Card>
  );
}
