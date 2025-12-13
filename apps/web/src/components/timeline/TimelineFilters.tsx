/* eslint-disable security/detect-object-injection -- Safe event type config access */
// UI-04: Timeline filters sidebar component
import type { TimelineFilters as TimelineFiltersType } from '@/lib/timeline-types';
import {
  TimelineEventType,
  TimelineEventStatus,
  getEventTypeLabel,
  getEventTypeColor,
} from '@/lib/timeline-types';
import { Input } from '@/components/ui/input';
import { Button } from '@/components/ui/button';
import { cn } from '@/lib/utils';

interface TimelineFiltersProps {
  filters: TimelineFiltersType;
  onFiltersChange: (filters: TimelineFiltersType) => void;
  isCollapsed: boolean;
  onToggleCollapse: () => void;
}

const ALL_EVENT_TYPES: TimelineEventType[] = [
  'message',
  'rag_search',
  'rag_retrieval',
  'rag_generation',
  'rag_complete',
  'error',
];

const ALL_STATUSES: TimelineEventStatus[] = ['pending', 'in_progress', 'success', 'error'];

export function TimelineFilters({
  filters,
  onFiltersChange,
  isCollapsed,
  onToggleCollapse,
}: TimelineFiltersProps) {
  const toggleEventType = (type: TimelineEventType) => {
    const newTypes = new Set(filters.eventTypes);
    if (newTypes.has(type)) {
      newTypes.delete(type);
    } else {
      newTypes.add(type);
    }
    onFiltersChange({ ...filters, eventTypes: newTypes });
  };

  const toggleStatus = (status: TimelineEventStatus) => {
    const newStatuses = new Set(filters.statuses);
    if (newStatuses.has(status)) {
      newStatuses.delete(status);
    } else {
      newStatuses.add(status);
    }
    onFiltersChange({ ...filters, statuses: newStatuses });
  };

  const selectAllEventTypes = () => {
    onFiltersChange({ ...filters, eventTypes: new Set(ALL_EVENT_TYPES) });
  };

  const deselectAllEventTypes = () => {
    onFiltersChange({ ...filters, eventTypes: new Set() });
  };

  const selectAllStatuses = () => {
    onFiltersChange({ ...filters, statuses: new Set(ALL_STATUSES) });
  };

  const deselectAllStatuses = () => {
    onFiltersChange({ ...filters, statuses: new Set() });
  };

  const handleSearchChange = (text: string) => {
    onFiltersChange({ ...filters, searchText: text || undefined });
  };

  if (isCollapsed) {
    return (
      <aside
        aria-label="Timeline filters (collapsed)"
        className="w-[60px] bg-muted border-r flex flex-col items-center p-4"
      >
        <Button
          onClick={onToggleCollapse}
          aria-label="Show filters"
          className="w-full text-xl"
          title="Mostra filtri"
        >
          ☰
        </Button>
        <div
          aria-hidden="true"
          className="mt-4 text-xs text-muted-foreground text-center [writing-mode:vertical-rl] rotate-180"
        >
          Filtri
        </div>
      </aside>
    );
  }

  return (
    <aside
      aria-label="Timeline filters"
      className="w-[280px] bg-gray-50 border-r border-gray-300 flex flex-col overflow-y-auto"
    >
      {/* Header */}
      <div className="p-4 border-b flex justify-between items-center">
        <h3 className="m-0 text-base font-semibold">Filtri Timeline</h3>
        <Button
          onClick={onToggleCollapse}
          aria-label="Hide filters"
          variant="ghost"
          size="sm"
          className="text-base"
          title="Nascondi filtri"
        >
          ✕
        </Button>
      </div>

      <div className="flex-1 overflow-y-auto p-4">
        {/* Search */}
        <div className="mb-6">
          <label htmlFor="timeline-search" className="block mb-2 text-sm font-semibold">
            Cerca
          </label>
          <Input
            id="timeline-search"
            type="text"
            value={filters.searchText || ''}
            onChange={e => handleSearchChange(e.target.value)}
            placeholder="Cerca nei messaggi, citazioni..."
          />
        </div>

        {/* Event Types */}
        <div className="mb-6">
          <div className="flex justify-between items-center mb-2">
            <label className="text-sm font-semibold">Tipo Evento</label>
            <div role="group" aria-label="Event type filter controls" className="flex gap-1">
              <Button
                onClick={selectAllEventTypes}
                aria-label="Select all event types"
                variant="secondary"
                size="sm"
                className="text-xs"
              >
                Tutti
              </Button>
              <Button
                onClick={deselectAllEventTypes}
                aria-label="Deselect all event types"
                variant="ghost"
                size="sm"
                className="text-xs"
              >
                Nessuno
              </Button>
            </div>
          </div>
          {ALL_EVENT_TYPES.map(type => {
            const isChecked = filters.eventTypes.has(type);
            const color = getEventTypeColor(type);
            return (
              <label
                key={type}
                className={cn(
                  'flex items-center gap-2 p-2 mb-1 rounded cursor-pointer text-xs transition-all',
                  isChecked ? 'bg-blue-50' : 'bg-white'
                )}
                style={
                  isChecked
                    ? { borderColor: color, borderWidth: '1px', borderStyle: 'solid' }
                    : { border: '1px solid #dadce0' }
                }
              >
                <input
                  type="checkbox"
                  checked={isChecked}
                  onChange={() => toggleEventType(type)}
                  className="cursor-pointer"
                />
                <div className="w-3 h-3 rounded-full" style={{ background: color }} />
                <span className="flex-1">{getEventTypeLabel(type)}</span>
              </label>
            );
          })}
        </div>

        {/* Statuses */}
        <div className="mb-6">
          <div className="flex justify-between items-center mb-2">
            <label className="text-sm font-semibold">Stato</label>
            <div role="group" aria-label="Status filter controls" className="flex gap-1">
              <Button
                onClick={selectAllStatuses}
                aria-label="Select all statuses"
                variant="secondary"
                size="sm"
                className="text-xs"
              >
                Tutti
              </Button>
              <Button
                onClick={deselectAllStatuses}
                aria-label="Deselect all statuses"
                variant="ghost"
                size="sm"
                className="text-xs"
              >
                Nessuno
              </Button>
            </div>
          </div>
          {ALL_STATUSES.map(status => {
            const isChecked = filters.statuses.has(status);
            const statusLabels: Record<TimelineEventStatus, string> = {
              pending: 'In Attesa',
              in_progress: 'In Corso',
              success: 'Completato',
              error: 'Errore',
            };
            return (
              <label
                key={status}
                className={cn(
                  'flex items-center gap-2 p-2 mb-1 rounded cursor-pointer text-xs transition-all',
                  isChecked ? 'bg-blue-50 border-blue-600' : 'bg-white border-gray-300',
                  'border'
                )}
              >
                <input
                  type="checkbox"
                  checked={isChecked}
                  onChange={() => toggleStatus(status)}
                  className="cursor-pointer"
                />
                <span className="flex-1">{statusLabels[status]}</span>
              </label>
            );
          })}
        </div>

        {/* Reset Button */}
        <Button
          onClick={() =>
            onFiltersChange({
              eventTypes: new Set(ALL_EVENT_TYPES),
              statuses: new Set(ALL_STATUSES),
              searchText: undefined,
            })
          }
          aria-label="Reset all filters"
          variant="ghost"
          className="w-full"
        >
          Ripristina Filtri
        </Button>
      </div>
    </aside>
  );
}
