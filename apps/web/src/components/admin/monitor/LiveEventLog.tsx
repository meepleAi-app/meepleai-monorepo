'use client';

/**
 * LiveEventLog — Task 3.4 (Issue #1718, F4.1 Monitor)
 *
 * Virtualized live domain event log that consumes useLiveEvents (Task 3.3),
 * parseEventMessage (Task 3.1), and mapEventLevel (Task 3.2).
 *
 * Layout pattern from mockup sp5-admin-monitor.html § "Live event stream" section:
 *   - admin-panel wrapper (rounded-[10px] border border-border/60 bg-card)
 *   - admin-panel-head: h3, live-pill, head-cluster (event count, pause, export)
 *   - FilterChipsBar: 7 known event types + 5 entity types (hardcoded for Phase 1)
 *   - Virtualized list: react-window v2 List, rowHeight=32, role="log"
 *   - Empty state / Error state / Loading skeleton
 *
 * Non-goals (scope discipline):
 *   - NO detail drawer (Phase 4.x onEventClick wired only as row click callback)
 *   - NO useEventTypes hook wiring to /types endpoint (Phase 4.x)
 *   - NO Export ndjson functionality (placeholder button only)
 */

import React, { useCallback, useMemo, useState } from 'react';

import { List } from 'react-window';

import { mapEventLevel, type EventLevel } from './event-level-mapper';
import { parseEventMessage } from './parse-event-message';
import { useLiveEvents } from './use-live-events';

import type { DomainEventDto } from './live-event-types';

// ============================================================================
// Public types
// ============================================================================

export interface LiveEventLogProps {
  /** Initial event-type filters applied to backfill + SSE subscription. */
  eventTypes?: string[];
  aggregateTypes?: string[];
  userId?: string;
  aggregateId?: string;
  /** Backfill page size. Default 100. */
  initialLimit?: number;
  /** Virtualized list height. Default '70vh'. */
  height?: number | string;
  /** Optional callback when a row is clicked (Phase 4.x detail drawer). */
  onEventClick?: (event: DomainEventDto) => void;
}

// ============================================================================
// Constants
// ============================================================================

/** Hardcoded event-type chips. Phase 4.x: wire to GET /api/v1/admin/events/types */
const KNOWN_EVENT_TYPES = [
  'agent.created',
  'kb.doc.indexed',
  'chat.session.created',
  'session.created',
  'session.finalized',
  'library.entry.removed',
  'library.session.recorded',
] as const;

const KNOWN_AGGREGATE_TYPES = [
  'Agent',
  'ChatSession',
  'PdfDocument',
  'Session',
  'UserLibraryEntry',
] as const;

const ROW_HEIGHT = 32;
const OVERSCAN_COUNT = 10;

// ============================================================================
// Level → colour mapping (mockup .event-log .lvl colours)
// ============================================================================

const LEVEL_CLASS: Record<EventLevel, string> = {
  info: 'text-entity-chat',
  ok: 'text-entity-toolkit',
  warn: 'text-amber-600', // status hue — ESLint-exempt per design doc
  err: 'text-entity-event',
};

const LEVEL_LABEL: Record<EventLevel, string> = {
  info: 'INFO',
  ok: 'OK',
  warn: 'WARN',
  err: 'ERR',
};

// ============================================================================
// Helpers
// ============================================================================

function formatTimestamp(iso: string): string {
  try {
    const d = new Date(iso);
    const hh = d.getUTCHours().toString().padStart(2, '0');
    const mm = d.getUTCMinutes().toString().padStart(2, '0');
    const ss = d.getUTCSeconds().toString().padStart(2, '0');
    const ms = d.getUTCMilliseconds().toString().padStart(3, '0');
    return `${hh}:${mm}:${ss}.${ms}`;
  } catch {
    return iso.slice(11, 23);
  }
}

// ============================================================================
// EventRow — rendered by react-window List
//
// react-window v2 separates "rowProps" (user data, passed through rowProps prop)
// from reserved props (ariaAttributes, index, style — injected by List itself).
//
// The RowComponent signature is:
//   ({ ariaAttributes, index, style, ...rowProps }) => ReactElement | null
//
// EventRowData is what goes through rowProps (no ariaAttributes/index/style).
// ============================================================================

/** User-controlled data forwarded via rowProps (no reserved keys). */
interface EventRowData {
  events: DomainEventDto[];
  onEventClick?: (event: DomainEventDto) => void;
}

/** Full props as received by the row component (reserved keys injected by List). */
interface EventRowProps extends EventRowData {
  ariaAttributes: {
    'aria-posinset': number;
    'aria-setsize': number;
    role: 'listitem';
  };
  index: number;
  style: React.CSSProperties;
}

function EventRow({
  ariaAttributes,
  index,
  style,
  events,
  onEventClick,
}: EventRowProps): React.ReactElement | null {
  const event = events[index];
  if (!event) return null;

  const level = mapEventLevel(event.eventType);
  const { eventType, fragments } = parseEventMessage(event);
  const levelClass = LEVEL_CLASS[level];
  const levelLabel = LEVEL_LABEL[level];

  const handleClick = () => {
    onEventClick?.(event);
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      onEventClick?.(event);
    }
  };

  // Merge react-window's positional style with grid layout
  const mergedStyle: React.CSSProperties = {
    ...style,
    gridTemplateColumns: '96px 60px 1fr',
  };

  return (
    <div
      {...ariaAttributes}
      style={mergedStyle}
      data-level={level}
      onClick={handleClick}
      onKeyDown={handleKeyDown}
      role="listitem"
      tabIndex={onEventClick ? 0 : undefined}
      className={[
        'grid gap-3 px-3.5 py-1 border-b border-border/60 last:border-b-0',
        'text-xs font-mono items-center',
        'hover:bg-muted/50 transition-colors',
        onEventClick ? 'cursor-pointer' : '',
      ]
        .filter(Boolean)
        .join(' ')}
    >
      {/* Timestamp column */}
      <span className="text-muted-foreground truncate">{formatTimestamp(event.loggedAt)}</span>

      {/* Level column */}
      <span className={['font-bold uppercase tracking-wider text-[10px]', levelClass].join(' ')}>
        {levelLabel}
      </span>

      {/* Message column */}
      <span className="truncate text-foreground">
        {/* Event type — bold */}
        <span className="font-bold mr-1">{eventType}</span>

        {/* Fragments: key=value pairs */}
        {fragments.map((f, i) => (
          <span key={i} className="mr-2">
            <span className="text-muted-foreground">{f.key}=</span>
            <span className={f.entityColor ? `text-entity-${f.entityColor} font-bold` : ''}>
              {f.value}
            </span>
          </span>
        ))}
      </span>
    </div>
  );
}

// ============================================================================
// FilterChipsBar
// ============================================================================

interface FilterChipsBarProps {
  activeEventTypes: Set<string>;
  activeAggregateTypes: Set<string>;
  onToggleEventType: (type: string) => void;
  onToggleAggregateType: (type: string) => void;
}

function FilterChipsBar({
  activeEventTypes,
  activeAggregateTypes,
  onToggleEventType,
  onToggleAggregateType,
}: FilterChipsBarProps): React.ReactElement {
  return (
    <div className="px-3.5 py-2.5 border-b border-border/60 space-y-2">
      {/* EventType row */}
      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-[10px] font-bold text-muted-foreground uppercase tracking-wider min-w-[72px]">
          EventType
        </span>
        {KNOWN_EVENT_TYPES.map(type => {
          const isActive = activeEventTypes.has(type);
          return (
            <button
              key={type}
              type="button"
              onClick={() => onToggleEventType(type)}
              aria-pressed={isActive}
              className={[
                'inline-flex items-center gap-1.5 px-2.5 py-[3px] rounded-full border',
                'text-[11.5px] font-bold cursor-pointer whitespace-nowrap',
                'transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
                isActive
                  ? 'bg-entity-event/10 border-entity-event/40 text-entity-event'
                  : 'bg-background border-border text-muted-foreground hover:bg-muted hover:text-foreground',
              ].join(' ')}
            >
              {type}
            </button>
          );
        })}
      </div>

      {/* EntityType row */}
      <div className="flex flex-wrap items-center gap-1.5">
        <span className="text-[10px] font-bold text-muted-foreground uppercase tracking-wider min-w-[72px]">
          EntityType
        </span>
        {KNOWN_AGGREGATE_TYPES.map(type => {
          const isActive = activeAggregateTypes.has(type);
          return (
            <button
              key={type}
              type="button"
              onClick={() => onToggleAggregateType(type)}
              aria-pressed={isActive}
              className={[
                'inline-flex items-center gap-1.5 px-2.5 py-[3px] rounded-full border',
                'text-[11.5px] font-bold cursor-pointer whitespace-nowrap',
                'transition-colors focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
                isActive
                  ? 'bg-entity-event/10 border-entity-event/40 text-entity-event'
                  : 'bg-background border-border text-muted-foreground hover:bg-muted hover:text-foreground',
              ].join(' ')}
            >
              {type}
            </button>
          );
        })}
      </div>
    </div>
  );
}

// ============================================================================
// LiveEventLog
// ============================================================================

export function LiveEventLog({
  eventTypes: initialEventTypes,
  aggregateTypes: initialAggregateTypes,
  userId,
  aggregateId,
  initialLimit = 100,
  height = '70vh',
  onEventClick,
}: LiveEventLogProps): React.JSX.Element {
  // --------------------------------------------------------------------------
  // Local filter state (chips UI layer — applied to hook on change)
  // --------------------------------------------------------------------------
  const [activeEventTypes, setActiveEventTypes] = useState<Set<string>>(
    () => new Set(initialEventTypes ?? [])
  );
  const [activeAggregateTypes, setActiveAggregateTypes] = useState<Set<string>>(
    () => new Set(initialAggregateTypes ?? [])
  );

  const toggleEventType = useCallback((type: string) => {
    setActiveEventTypes(prev => {
      const next = new Set(prev);
      if (next.has(type)) {
        next.delete(type);
      } else {
        next.add(type);
      }
      return next;
    });
  }, []);

  const toggleAggregateType = useCallback((type: string) => {
    setActiveAggregateTypes(prev => {
      const next = new Set(prev);
      if (next.has(type)) {
        next.delete(type);
      } else {
        next.add(type);
      }
      return next;
    });
  }, []);

  // --------------------------------------------------------------------------
  // Hook — derives filter arrays from active-chip sets
  // --------------------------------------------------------------------------
  const eventTypesFilter = useMemo(
    () => (activeEventTypes.size > 0 ? [...activeEventTypes] : undefined),
    [activeEventTypes]
  );
  const aggregateTypesFilter = useMemo(
    () => (activeAggregateTypes.size > 0 ? [...activeAggregateTypes] : undefined),
    [activeAggregateTypes]
  );

  const { events, isLoading, isStreaming, error, pause, resume, refetch } = useLiveEvents({
    eventTypes: eventTypesFilter,
    aggregateTypes: aggregateTypesFilter,
    userId,
    aggregateId,
    initialLimit,
  });

  // --------------------------------------------------------------------------
  // Row props passed through react-window List → EventRow
  // rowProps must satisfy ExcludeForbiddenKeys_2<EventRowData>
  // (no ariaAttributes / index / style — those are injected by List)
  // --------------------------------------------------------------------------
  const rowProps = useMemo<EventRowData>(
    () => ({ events, onEventClick }),

    [events, onEventClick]
  );

  // --------------------------------------------------------------------------
  // Derived header meta
  // --------------------------------------------------------------------------
  const eventCountLabel = `${events.length.toLocaleString()} events · last 60m`;

  // --------------------------------------------------------------------------
  // Render
  // --------------------------------------------------------------------------
  return (
    <section
      className="rounded-[10px] border border-border/60 bg-card overflow-hidden"
      role="region"
      aria-label="Live event stream"
    >
      {/* ── Panel head ───────────────────────────────────────────────────── */}
      <div className="px-3.5 py-2.5 border-b border-border/60 flex items-center gap-2.5 bg-background/60 flex-wrap">
        <h3 className="m-0 text-[13px] font-extrabold text-foreground leading-none">
          Live event stream{' '}
          <span className="font-medium text-muted-foreground text-[11px] font-mono ml-1.5">
            /api/v1/admin/events/stream
          </span>
        </h3>

        {/* Live pill */}
        <span
          className={[
            'inline-flex items-center gap-1.5 px-2.5 py-[2px] rounded-full',
            'text-[10px] font-bold uppercase tracking-widest font-mono',
            'bg-entity-toolkit/12 text-entity-toolkit',
          ].join(' ')}
          aria-label="Streaming via SSE"
        >
          streaming · SSE
        </span>

        {/* Head cluster (right side) */}
        <div className="inline-flex items-center gap-2.5 ml-auto flex-wrap">
          <span className="text-[10px] font-mono text-muted-foreground">{eventCountLabel}</span>

          {/* Pause / Resume button */}
          <button
            type="button"
            onClick={isStreaming ? pause : resume}
            aria-label={isStreaming ? 'Pause stream' : 'Resume stream'}
            className={[
              'inline-flex items-center gap-1.5 px-2 py-[3px] rounded-md border border-border/60',
              'text-[11px] font-bold text-muted-foreground bg-transparent cursor-pointer',
              'hover:bg-muted hover:text-foreground transition-colors',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            ].join(' ')}
          >
            {isStreaming ? '⏸ Pause stream' : '▶ Resume'}
          </button>

          {/* Export ndjson — placeholder, no-op */}
          <button
            type="button"
            onClick={() => {
              /* Phase 4.x: implement ndjson export */
            }}
            aria-label="Export ndjson (not yet implemented)"
            className={[
              'inline-flex items-center gap-1.5 px-2 py-[3px] rounded-md border border-border/60',
              'text-[11px] font-bold text-muted-foreground bg-transparent cursor-pointer',
              'hover:bg-muted hover:text-foreground transition-colors',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            ].join(' ')}
          >
            ⤓ Export ndjson
          </button>
        </div>
      </div>

      {/* ── Filter chips bar ─────────────────────────────────────────────── */}
      <FilterChipsBar
        activeEventTypes={activeEventTypes}
        activeAggregateTypes={activeAggregateTypes}
        onToggleEventType={toggleEventType}
        onToggleAggregateType={toggleAggregateType}
      />

      {/* ── Error banner ─────────────────────────────────────────────────── */}
      {error && (
        <div
          role="alert"
          className="flex items-center justify-between px-3.5 py-2 bg-entity-event/8 border-b border-entity-event/20 text-xs"
        >
          <span className="text-entity-event font-mono font-bold truncate mr-3">
            {error.message}
          </span>
          <button
            type="button"
            onClick={refetch}
            aria-label="Retry loading events"
            className={[
              'shrink-0 inline-flex items-center gap-1 px-2.5 py-1 rounded-md',
              'text-[11px] font-bold border border-entity-event/40 text-entity-event',
              'bg-transparent hover:bg-entity-event/10 transition-colors cursor-pointer',
              'focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring focus-visible:ring-offset-2',
            ].join(' ')}
          >
            ↻ Retry
          </button>
        </div>
      )}

      {/* ── Event log body ───────────────────────────────────────────────── */}
      <div className="bg-muted/20 font-mono text-xs" style={{ height }}>
        {/* Loading state */}
        {isLoading && events.length === 0 && (
          <div className="flex items-center justify-center h-full text-muted-foreground gap-2">
            <span
              className="inline-block w-2 h-2 rounded-full bg-entity-toolkit animate-pulse"
              aria-hidden="true"
            />
            <span>Caricamento eventi…</span>
          </div>
        )}

        {/* Empty state (not loading, no error, no events) */}
        {!isLoading && !error && events.length === 0 && (
          <div className="flex flex-col items-center justify-center h-full gap-2 text-center px-6">
            <span className="inline-flex items-center gap-1.5 px-2.5 py-[3px] rounded-full bg-entity-toolkit/12 text-entity-toolkit text-[10px] font-bold uppercase tracking-widest font-mono">
              ● In ascolto…
            </span>
            <p className="text-[11px] text-muted-foreground">
              0 eventi in 90 giorni — verifica i filtri
            </p>
          </div>
        )}

        {/* Virtualized list */}
        {events.length > 0 && (
          <List<EventRowData>
            rowComponent={EventRow}
            rowCount={events.length}
            rowHeight={ROW_HEIGHT}
            rowProps={rowProps}
            overscanCount={OVERSCAN_COUNT}
            style={{ height: '100%' }}
            role="log"
            aria-live="polite"
            aria-label="Domain event stream"
          />
        )}
      </div>
    </section>
  );
}
