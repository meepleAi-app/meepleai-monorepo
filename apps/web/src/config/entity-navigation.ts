/**
 * Entity Navigation Graph Configuration
 *
 * Centralised map of navigation relationships between MeepleCard entity types.
 * Used by CardNavigationFooter to render destination links.
 *
 * @see Epic #4688 - MeepleCard Navigation System
 * @see Issue #4690 - Navigation Graph Config
 */

import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card';

// ---------------------------------------------------------------------------
// Types
// ---------------------------------------------------------------------------

/** A single navigation target shown in the card navigation footer. */
export interface EntityNavigationTarget {
  /** Destination entity type – drives icon + color */
  entity: MeepleEntityType;
  /** Short label shown below the icon (e.g. "KB", "Agents") */
  label: string;
  /**
   * Key used to pull the relevant id from the source entity data.
   * Falls back to `'id'` when omitted.
   *
   * Example: a ChatSession card needs `gameId` to build the Game link.
   */
  idKey?: string;
  /** Build the destination href from the resolved id value. */
  buildHref: (id: string) => string;
}

/**
 * Minimal bag of ids that the source entity can supply.
 * Every value is optional – if the required `idKey` is missing the link is
 * simply omitted at render time.
 */
export type EntityIdBag = Record<string, string | undefined>;

/** Resolved link ready for rendering. */
export interface ResolvedNavigationLink {
  entity: MeepleEntityType;
  label: string;
  href: string;
}

// ---------------------------------------------------------------------------
// Navigation Graph
// ---------------------------------------------------------------------------

/**
 * The full entity-relationship navigation graph.
 *
 * ```
 * Game ──► KB, Agents, Chats, Sessions
 * Agent ──► Games, KB, Chats, Sessions
 * Document ──► Game, Agent
 * Session ──► Game, Players, Agent, Chats
 * Player ──► Sessions, Games
 * ChatSession ──► Game, Agent, Session
 * ```
 */
export const ENTITY_NAVIGATION_GRAPH: Partial<
  Record<MeepleEntityType, EntityNavigationTarget[]>
> = {
  game: [
    {
      entity: 'document',
      label: 'KB',
      buildHref: (id) => `/games/${id}/knowledge-base`,
    },
    {
      entity: 'agent',
      label: 'Agents',
      buildHref: (id) => `/games/${id}/agents`,
    },
    {
      entity: 'chatSession',
      label: 'Chats',
      buildHref: (id) => `/games/${id}/chats`,
    },
    {
      entity: 'session',
      label: 'Sessions',
      buildHref: (id) => `/games/${id}/sessions`,
    },
  ],

  agent: [
    {
      entity: 'game',
      label: 'Games',
      buildHref: (id) => `/agents/${id}/games`,
    },
    {
      entity: 'document',
      label: 'KB',
      buildHref: (id) => `/agents/${id}/knowledge-base`,
    },
    {
      entity: 'chatSession',
      label: 'Chats',
      buildHref: (id) => `/agents/${id}/chats`,
    },
    {
      entity: 'session',
      label: 'Sessions',
      buildHref: (id) => `/agents/${id}/sessions`,
    },
  ],

  document: [
    {
      entity: 'game',
      label: 'Game',
      idKey: 'gameId',
      buildHref: (id) => `/games/${id}`,
    },
    {
      entity: 'agent',
      label: 'Agent',
      idKey: 'agentId',
      buildHref: (id) => `/agents/${id}`,
    },
  ],

  session: [
    {
      entity: 'game',
      label: 'Game',
      idKey: 'gameId',
      buildHref: (id) => `/games/${id}`,
    },
    {
      entity: 'player',
      label: 'Players',
      buildHref: (id) => `/sessions/${id}/players`,
    },
    {
      entity: 'agent',
      label: 'Agent',
      idKey: 'agentId',
      buildHref: (id) => `/agents/${id}`,
    },
    {
      entity: 'chatSession',
      label: 'Chats',
      buildHref: (id) => `/sessions/${id}/chats`,
    },
  ],

  player: [
    {
      entity: 'session',
      label: 'Sessions',
      buildHref: (id) => `/players/${id}/sessions`,
    },
    {
      entity: 'game',
      label: 'Games',
      buildHref: (id) => `/players/${id}/games`,
    },
  ],

  chatSession: [
    {
      entity: 'game',
      label: 'Game',
      idKey: 'gameId',
      buildHref: (id) => `/games/${id}`,
    },
    {
      entity: 'agent',
      label: 'Agent',
      idKey: 'agentId',
      buildHref: (id) => `/agents/${id}`,
    },
    {
      entity: 'session',
      label: 'Session',
      idKey: 'sessionId',
      buildHref: (id) => `/sessions/${id}`,
    },
  ],
} as const;

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

/**
 * Resolve navigation links for a given entity, filtering out any whose
 * required id is missing from `entityData`.
 *
 * ```ts
 * const links = getNavigationLinks('game', { id: '123' });
 * // [{ entity: 'document', label: 'KB', href: '/games/123/knowledge-base' }, …]
 * ```
 */
export function getNavigationLinks(
  entity: MeepleEntityType,
  entityData: EntityIdBag,
): ResolvedNavigationLink[] {
  // eslint-disable-next-line security/detect-object-injection -- entity comes from typed union
  const targets = ENTITY_NAVIGATION_GRAPH[entity];
  if (!targets) return [];

  const results: ResolvedNavigationLink[] = [];

  for (const target of targets) {
    const key = target.idKey ?? 'id';
    // eslint-disable-next-line security/detect-object-injection -- key is from config
    const value = entityData[key];
    if (!value) continue;

    results.push({
      entity: target.entity,
      label: target.label,
      href: target.buildHref(value),
    });
  }

  return results;
}

/**
 * All navigable entity types (those that have entries in the graph).
 */
export const NAVIGABLE_ENTITIES = Object.keys(
  ENTITY_NAVIGATION_GRAPH,
) as MeepleEntityType[];
