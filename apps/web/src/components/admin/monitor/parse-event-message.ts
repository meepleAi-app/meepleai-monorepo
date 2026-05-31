import type { DomainEventDto, EntityColor } from './live-event-types';

/**
 * Display fragments for a single LiveEventLog row.
 * Renders as: <eventType> <key>=<value> <key>=<value> ...
 * Each value may have an entity color hint for visual differentiation.
 */
export interface MessageFragment {
  key: string; // 'aggregateType', 'aggregateId', 'user' etc.
  value: string; // displayed value (uuid truncated to 8 chars, dates formatted, etc.)
  entityColor?: EntityColor; // optional: render value with `text-entity-{color}` accent
}

export interface DisplayFragments {
  eventType: string; // e.g. 'agent.created'
  fragments: MessageFragment[];
}

const AGGREGATE_TO_COLOR: Record<string, EntityColor> = {
  Agent: 'agent',
  PdfDocument: 'kb',
  ChatSession: 'chat',
  Session: 'session',
  UserLibraryEntry: 'player',
  // unknown aggregates default to 'event' (gray-red, last-resort)
};

/**
 * Converts a domain event DTO into display fragments suitable for a LiveEventLog row.
 * Pure function — no side effects, no I/O.
 */
export function parseEventMessage(event: DomainEventDto): DisplayFragments {
  const fragments: MessageFragment[] = [];

  if (event.aggregateType) {
    fragments.push({
      key: 'aggregateType',
      value: event.aggregateType,
      entityColor: AGGREGATE_TO_COLOR[event.aggregateType] ?? 'event',
    });
  }

  if (event.aggregateId) {
    fragments.push({
      key: 'aggregateId',
      value: truncateGuid(event.aggregateId),
    });
  }

  if (event.userId) {
    fragments.push({
      key: 'user',
      value: truncateGuid(event.userId),
      entityColor: 'session',
    });
  }

  return {
    eventType: event.eventType,
    fragments,
  };
}

/**
 * Returns the first 8 hex characters of a GUID (dashes stripped).
 * Example: 'a1b2c3d4-e5f6-...' → 'a1b2c3d4'
 */
function truncateGuid(guid: string): string {
  return guid.replace(/-/g, '').slice(0, 8);
}
