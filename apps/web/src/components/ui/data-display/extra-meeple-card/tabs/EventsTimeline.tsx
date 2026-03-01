'use client';

/**
 * EventsTimeline - Filterable vertical timeline of session events
 * Issue #4763 - Interactive Cards + Timer + Events Timeline UI + Phase 3 Tests
 */

import React, { useState } from 'react';
import {
  Activity,
  Filter,
  MessageCircle,
  Camera as CameraIcon,
  Zap,
  Trophy,
  RotateCcw,
  Settings,
  ChevronDown,
  ChevronUp,
} from 'lucide-react';
import { cn } from '@/lib/utils';
import type {
  EventsTimelineData,
  EventsTimelineActions,
  EnhancedTimelineEvent,
  SessionEventType,
} from '../types';

interface EventsTimelineProps {
  data?: EventsTimelineData;
  actions?: EventsTimelineActions;
}

/** Event type configuration */
const EVENT_CONFIG: Record<SessionEventType, {
  icon: React.ElementType;
  dot: string;
  text: string;
  label: string;
}> = {
  system: { icon: Settings, dot: 'bg-slate-400', text: 'text-slate-600', label: 'System' },
  turn: { icon: RotateCcw, dot: 'bg-blue-400', text: 'text-blue-600', label: 'Turn' },
  score: { icon: Trophy, dot: 'bg-amber-400', text: 'text-amber-600', label: 'Score' },
  action: { icon: Zap, dot: 'bg-green-400', text: 'text-green-600', label: 'Action' },
  media: { icon: CameraIcon, dot: 'bg-purple-400', text: 'text-purple-600', label: 'Media' },
  phase: { icon: Activity, dot: 'bg-indigo-400', text: 'text-indigo-600', label: 'Phase' },
  snapshot: { icon: CameraIcon, dot: 'bg-teal-400', text: 'text-teal-600', label: 'Snapshot' },
  chat: { icon: MessageCircle, dot: 'bg-rose-400', text: 'text-rose-600', label: 'Chat' },
};

/** Single timeline event item */
function TimelineEventItem({
  event,
  isExpanded,
  onToggle,
  onEventClick,
  onNavigateToSnapshot,
}: {
  event: EnhancedTimelineEvent;
  isExpanded: boolean;
  onToggle: () => void;
  onEventClick?: (event: EnhancedTimelineEvent) => void;
  onNavigateToSnapshot?: (snapshotId: string) => void;
}) {
  const config = EVENT_CONFIG[event.type] ?? EVENT_CONFIG.system;
  const Icon = config.icon;
  const time = new Date(event.timestamp);
  const formattedTime = time.toLocaleTimeString(undefined, {
    hour: '2-digit',
    minute: '2-digit',
    second: '2-digit',
  });

  const hasDetails = event.description || event.snapshotId;

  return (
    <div
      className="relative pl-6"
      data-testid={`timeline-event-${event.id}`}
    >
      {/* Timeline dot */}
      <div className={cn('absolute left-0 top-2 h-3 w-3 rounded-full border-2 border-white', config.dot)} />
      {/* Timeline line */}
      <div className="absolute left-[5px] top-5 bottom-0 w-0.5 bg-slate-200" />

      <div
        role={hasDetails ? 'button' : undefined}
        tabIndex={hasDetails ? 0 : undefined}
        aria-expanded={hasDetails ? isExpanded : undefined}
        className={cn(
          'rounded-lg px-3 py-2 mb-2 transition-colors',
          hasDetails ? 'cursor-pointer hover:bg-slate-50' : '',
          isExpanded ? 'bg-slate-50' : ''
        )}
        onClick={() => {
          if (hasDetails) onToggle();
          onEventClick?.(event);
        }}
        onKeyDown={hasDetails ? (e) => {
          if (e.key === 'Enter' || e.key === ' ') {
            e.preventDefault();
            onToggle();
            onEventClick?.(event);
          }
        } : undefined}
      >
        <div className="flex items-start justify-between gap-2">
          <div className="flex items-start gap-1.5 min-w-0">
            <Icon className={cn('h-3 w-3 mt-0.5 flex-shrink-0', config.text)} />
            <div className="min-w-0">
              <p className={cn('font-nunito text-xs font-medium', config.text)}>
                {event.label}
              </p>
              {event.playerName && (
                <p className="font-nunito text-[10px] text-slate-400">
                  by {event.playerName}
                </p>
              )}
            </div>
          </div>
          <div className="flex items-center gap-1 flex-shrink-0">
            {event.turnNumber != null && (
              <span className="font-nunito text-[10px] text-slate-400">
                T{event.turnNumber}
              </span>
            )}
            <span className="font-nunito text-[10px] text-slate-400 tabular-nums">
              {formattedTime}
            </span>
            {hasDetails && (
              isExpanded
                ? <ChevronUp className="h-3 w-3 text-slate-300" />
                : <ChevronDown className="h-3 w-3 text-slate-300" />
            )}
          </div>
        </div>

        {/* Expanded details */}
        {isExpanded && hasDetails && (
          <div className="mt-1.5 pl-5 space-y-1">
            {event.description && (
              <p className="font-nunito text-[10px] text-slate-500">
                {event.description}
              </p>
            )}
            {event.snapshotId && onNavigateToSnapshot && (
              <button
                onClick={(e) => {
                  e.stopPropagation();
                  onNavigateToSnapshot(event.snapshotId!);
                }}
                className="font-nunito text-[10px] text-indigo-500 hover:text-indigo-700 underline"
                data-testid={`navigate-snapshot-${event.snapshotId}`}
              >
                View snapshot
              </button>
            )}
          </div>
        )}
      </div>
    </div>
  );
}

/** Filter chip button */
function FilterChip({
  type,
  active,
  count,
  onClick,
}: {
  type: SessionEventType;
  active: boolean;
  count: number;
  onClick: () => void;
}) {
  const config = EVENT_CONFIG[type];
  if (count === 0) return null;

  return (
    <button
      onClick={onClick}
      className={cn(
        'flex items-center gap-1 rounded-full px-2 py-0.5 font-nunito text-[10px] font-medium transition-colors',
        active ? `${config.text} bg-white shadow-sm border border-slate-200` : 'text-slate-400 hover:text-slate-600'
      )}
      data-testid={`filter-${type}`}
    >
      <span className={cn('h-1.5 w-1.5 rounded-full', active ? config.dot : 'bg-slate-300')} />
      {config.label}
      <span className="text-[9px] opacity-60">{count}</span>
    </button>
  );
}

export function EventsTimeline({ data, actions }: EventsTimelineProps) {
  const [activeFilters, setActiveFilters] = useState<Set<SessionEventType>>(new Set());
  const [expandedIds, setExpandedIds] = useState<Set<string>>(new Set());

  if (!data || data.events.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-slate-400">
        <Activity className="h-8 w-8 mb-2 opacity-50" />
        <p className="font-nunito text-sm">No events yet</p>
        <p className="font-nunito text-xs mt-1">Events will appear as the session progresses</p>
      </div>
    );
  }

  // Count events by type
  const typeCounts = data.events.reduce<Record<string, number>>((acc, e) => {
    acc[e.type] = (acc[e.type] || 0) + 1;
    return acc;
  }, {});

  // Filter events
  const filteredEvents = activeFilters.size === 0
    ? data.events
    : data.events.filter((e) => activeFilters.has(e.type));

  const toggleFilter = (type: SessionEventType) => {
    setActiveFilters((prev) => {
      const next = new Set(prev);
      if (next.has(type)) {
        next.delete(type);
      } else {
        next.add(type);
      }
      return next;
    });
  };

  const toggleExpanded = (id: string) => {
    setExpandedIds((prev) => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  };

  // Available event types that actually have events
  const availableTypes = (Object.keys(EVENT_CONFIG) as SessionEventType[])
    .filter((type) => (typeCounts[type] ?? 0) > 0);

  return (
    <div className="space-y-3" data-testid="events-timeline">
      {/* Filter bar */}
      <div className="flex items-center gap-2">
        <Filter className="h-3 w-3 text-slate-400 flex-shrink-0" aria-hidden="true" />
        <div className="flex flex-wrap gap-1">
          {availableTypes.map((type) => (
            <FilterChip
              key={type}
              type={type}
              active={activeFilters.size === 0 || activeFilters.has(type)}
              count={typeCounts[type] ?? 0}
              onClick={() => toggleFilter(type)}
            />
          ))}
        </div>
      </div>

      {/* Event count */}
      <p className="font-nunito text-[10px] text-slate-400">
        {filteredEvents.length} of {data.totalEvents} events
      </p>

      {/* Timeline */}
      <div className="relative">
        {filteredEvents.map((event) => (
          <TimelineEventItem
            key={event.id}
            event={event}
            isExpanded={expandedIds.has(event.id)}
            onToggle={() => toggleExpanded(event.id)}
            onEventClick={actions?.onEventClick}
            onNavigateToSnapshot={actions?.onNavigateToSnapshot}
          />
        ))}
      </div>

      {/* Filtered empty state */}
      {filteredEvents.length === 0 && activeFilters.size > 0 && (
        <div className="flex flex-col items-center py-4 text-slate-400">
          <p className="font-nunito text-xs">No events match the selected filters</p>
        </div>
      )}
    </div>
  );
}
