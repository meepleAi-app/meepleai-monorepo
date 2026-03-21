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
  /** Route navigation — optional when onClick is provided. */
  href?: string;
  /** Callback for sheet/modal — alternative to href. */
  onClick?: () => void;
}

// ---------------------------------------------------------------------------
// Navigation Graph
// ---------------------------------------------------------------------------

/**
 * The full entity-relationship navigation graph.
 *
 * ```
 * Game ──► KB/Agent tab, Agent tab, Chats, Sessions
 * Agent ──► Library, Library, Chats, Sessions
 * KB ──► Game (library), Agent
 * Session ──► Game (library), Players, Agent, Chats
 * Player ──► Sessions, Library
 * ChatSession ──► Game (library), Agent, Session
 * ```
 *
 * URLs updated for Epic #5033 route consolidation (Issue #5055):
 * - Game detail pages → /library/[gameId] (was /games/[gameId])
 * - KB / Agent views → /library/[gameId]?tab=agent (was /games/[gameId]/knowledge-base)
 * - Chat lists → /chat (was /games/[gameId]/chats)
 * - Session lists → /sessions (was /games/[gameId]/sessions)
 */
export const ENTITY_NAVIGATION_GRAPH: Partial<Record<MeepleEntityType, EntityNavigationTarget[]>> =
  {
    game: [
      {
        entity: 'kb',
        label: 'KB',
        buildHref: id => `/library/${id}?tab=agent`,
      },
      {
        entity: 'agent',
        label: 'Agents',
        buildHref: id => `/library/${id}?tab=agent`,
      },
      {
        entity: 'chatSession',
        label: 'Chats',
        buildHref: _id => `/chat`,
      },
      {
        entity: 'session',
        label: 'Sessions',
        buildHref: _id => `/sessions`,
      },
    ],

    agent: [
      {
        entity: 'game',
        label: 'Library',
        buildHref: _id => `/library`,
      },
      {
        entity: 'kb',
        label: 'KB',
        buildHref: _id => `/library`,
      },
      {
        entity: 'chatSession',
        label: 'Chats',
        buildHref: _id => `/chat`,
      },
      {
        entity: 'session',
        label: 'Sessions',
        buildHref: _id => `/sessions`,
      },
    ],

    kb: [
      {
        entity: 'game',
        label: 'Game',
        idKey: 'gameId',
        buildHref: id => `/library/${id}`,
      },
      {
        entity: 'agent',
        label: 'Agent',
        idKey: 'agentId',
        buildHref: id => `/agents/${id}`,
      },
    ],

    session: [
      {
        entity: 'game',
        label: 'Game',
        idKey: 'gameId',
        buildHref: id => `/library/${id}`,
      },
      {
        entity: 'player',
        label: 'Players',
        buildHref: id => `/sessions/${id}/players`,
      },
      {
        entity: 'agent',
        label: 'Agent',
        idKey: 'agentId',
        buildHref: id => `/agents/${id}`,
      },
      {
        entity: 'chatSession',
        label: 'Chats',
        buildHref: id => `/sessions/${id}/chats`,
      },
    ],

    player: [
      {
        entity: 'session',
        label: 'Sessions',
        buildHref: id => `/players/${id}/sessions`,
      },
      {
        entity: 'game',
        label: 'Library',
        buildHref: _id => `/library`,
      },
    ],

    chatSession: [
      {
        entity: 'game',
        label: 'Game',
        idKey: 'gameId',
        buildHref: id => `/library/${id}`,
      },
      {
        entity: 'agent',
        label: 'Agent',
        idKey: 'agentId',
        buildHref: id => `/agents/${id}`,
      },
      {
        entity: 'session',
        label: 'Session',
        idKey: 'sessionId',
        buildHref: id => `/sessions/${id}`,
      },
    ],

    event: [
      {
        entity: 'game',
        label: 'Games',
        buildHref: _id => `/library`,
      },
      {
        entity: 'session',
        label: 'Sessions',
        buildHref: _id => `/sessions`,
      },
    ],

    toolkit: [
      {
        entity: 'game',
        label: 'Game',
        idKey: 'gameId',
        buildHref: id => `/library/${id}`,
      },
      {
        entity: 'agent',
        label: 'Agent',
        idKey: 'agentId',
        buildHref: id => `/agents/${id}`,
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
 * // [{ entity: 'kb', label: 'KB', href: '/library/123?tab=agent' }, …]
 * ```
 */
export function getNavigationLinks(
  entity: MeepleEntityType,
  entityData: EntityIdBag
): ResolvedNavigationLink[] {
  const targets = ENTITY_NAVIGATION_GRAPH[entity];
  if (!targets) return [];

  const results: ResolvedNavigationLink[] = [];

  for (const target of targets) {
    const key = target.idKey ?? 'id';
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
export const NAVIGABLE_ENTITIES = Object.keys(ENTITY_NAVIGATION_GRAPH) as MeepleEntityType[];
