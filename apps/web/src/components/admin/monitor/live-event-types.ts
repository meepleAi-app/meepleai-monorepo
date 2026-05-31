/**
 * Domain event DTO from BE outbox (matches DomainEventDto in
 * Api.BoundedContexts.Administration.Application.Queries.AdminEvents).
 * Serialized via camelCase per project JsonOptions.
 */
export interface DomainEventDto {
  id: string; // GUID surrogate PK
  eventId: string; // GUID domain event id
  eventType: string; // 'agent.created', 'kb.doc.indexed', etc.
  aggregateType: string | null; // 'Agent', 'ChatSession', 'PdfDocument', 'Session', 'UserLibraryEntry', or null
  aggregateId: string | null; // GUID or null
  userId: string | null; // GUID or null
  payloadJson: string;
  payloadVersion: number;
  occurredAt: string; // ISO timestamp
  loggedAt: string; // ISO timestamp
}

/**
 * Filter passed to broadcaster subscription / polling query.
 * Mirrors EventBroadcastFilter shape from BE Phase 1.
 */
export interface LiveEventFilters {
  eventTypes?: string[];
  aggregateTypes?: string[];
  userId?: string;
  aggregateId?: string;
}

/**
 * Entity color hint for displaying an aggregate type with the right entity utility.
 * Maps AggregateType to entity color name used in Tailwind text-entity-N and bg-entity-N-12 classes.
 */
export type EntityColor =
  | 'agent' // c-agent (yellow)
  | 'kb' // c-kb (teal)
  | 'chat' // c-chat (blue)
  | 'session' // c-session (indigo)
  | 'player' // c-player (purple)
  | 'game' // c-game (orange)
  | 'event' // c-event (red, default for unknown / system)
  | 'toolkit' // c-toolkit (green)
  | 'tool'; // c-tool (cyan)
