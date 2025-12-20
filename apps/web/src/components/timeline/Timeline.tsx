// UI-04: Main Timeline component (multi-pane layout)
import { useState } from 'react';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { TimelineEvent, TimelineFilters, DEFAULT_FILTERS } from '@/lib/timeline-types';

import { TimelineDetails } from './TimelineDetails';
import { TimelineEventList } from './TimelineEventList';
import { TimelineFilters as TimelineFiltersComponent } from './TimelineFilters';

interface TimelineProps {
  events: TimelineEvent[];
  isVisible: boolean;
  onToggleVisibility: () => void;
}

export function Timeline({ events, isVisible, onToggleVisibility }: TimelineProps) {
  const [filters, setFilters] = useState<TimelineFilters>(DEFAULT_FILTERS);
  const [selectedEventId, setSelectedEventId] = useState<string | null>(null);
  const [isFiltersCollapsed, setIsFiltersCollapsed] = useState(false);
  const [isDetailsCollapsed, setIsDetailsCollapsed] = useState(false);

  const selectedEvent = events.find(e => e.id === selectedEventId) || null;

  if (!isVisible) {
    return (
      <Button
        onClick={onToggleVisibility}
        aria-label="Show RAG Timeline"
        className="fixed bottom-6 right-6 rounded-full shadow-md z-[1000] flex items-center gap-2"
        title="Mostra Timeline RAG"
      >
        <span className="text-lg">📊</span>
        <span>Timeline RAG</span>
        {events.length > 0 && (
          <Badge variant="secondary" className="ml-1">
            {events.length}
          </Badge>
        )}
      </Button>
    );
  }

  return (
    <aside
      role="region"
      aria-label="RAG Timeline with conversation events and metrics"
      className="fixed inset-0 bg-white z-[1000] flex flex-col font-sans"
    >
      {/* Timeline Header */}
      <header className="p-4 border-b border-gray-300 flex justify-between items-center bg-white z-10">
        <div className="flex items-center gap-3">
          <span aria-hidden="true" className="text-3xl">
            📊
          </span>
          <div>
            <h1 className="m-0 text-xl font-semibold">Timeline RAG</h1>
            <p className="my-1 text-[13px] text-slate-500">
              Cronologia eventi e metriche conversazione
            </p>
          </div>
        </div>
        <Button
          onClick={onToggleVisibility}
          aria-label="Close RAG Timeline"
          variant="ghost"
          className="flex items-center gap-2"
        >
          <span>Chiudi Timeline</span>
          <span aria-hidden="true" className="text-base">
            ✕
          </span>
        </Button>
      </header>

      {/* Multi-pane Layout */}
      <div className="flex-1 flex overflow-hidden">
        {/* Left: Filters Sidebar */}
        <TimelineFiltersComponent
          filters={filters}
          onFiltersChange={setFilters}
          isCollapsed={isFiltersCollapsed}
          onToggleCollapse={() => setIsFiltersCollapsed(!isFiltersCollapsed)}
        />

        {/* Center: Event List */}
        <div className="flex-1 overflow-y-auto bg-white border-r border-gray-300">
          <TimelineEventList
            events={events}
            filters={filters}
            selectedEventId={selectedEventId}
            onSelectEvent={setSelectedEventId}
          />
        </div>

        {/* Right: Event Details */}
        <TimelineDetails
          event={selectedEvent}
          isCollapsed={isDetailsCollapsed}
          onToggleCollapse={() => setIsDetailsCollapsed(!isDetailsCollapsed)}
        />
      </div>

      {/* Stats Bar (optional) */}
      {events.length > 0 && (
        <footer
          role="status"
          aria-label="Timeline statistics"
          className="py-2 px-4 border-t border-gray-300 bg-gray-50 flex gap-6 text-xs text-slate-500"
        >
          <div>
            <strong className="text-gray-900">{events.length}</strong> eventi totali
          </div>
          <div>
            <strong className="text-gray-900">
              {events.filter(e => e.status === 'success').length}
            </strong>{' '}
            completati
          </div>
          <div>
            <strong className="text-red-600">
              {events.filter(e => e.status === 'error').length}
            </strong>{' '}
            {events.filter(e => e.status === 'error').length === 1 ? 'errore' : 'errori'}
          </div>
          {events.some(e => e.data.metrics?.totalTokens) && (
            <div>
              <strong className="text-gray-900">
                {events
                  .reduce((sum, e) => sum + (e.data.metrics?.totalTokens || 0), 0)
                  .toLocaleString()}
              </strong>{' '}
              token totali
            </div>
          )}
        </footer>
      )}
    </aside>
  );
}
