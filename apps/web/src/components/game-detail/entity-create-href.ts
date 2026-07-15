import type { MeepleEntityType } from '@/components/ui/data-display/meeple-card/types';

/**
 * Maps a ConnectionBar entity type to the "create new {entity}" target URL for a
 * given game. Extracted as a pure function so the per-entity route mapping is
 * unit-testable in isolation — it guards the bug class where a wrong param
 * silently turns the ConnectionBar "+" (empty pip) create action into a dead click.
 *
 * Returns `null` for entity types that have no game-scoped create surface (the
 * caller then performs no navigation).
 *
 * Route targets:
 *   - agent   → full-page agent creation under the game
 *   - kb      → full-page KB (PDF upload) under the game
 *   - chat    → new-chat wizard, prefilled with the game (`?game=`)
 *   - session → new-session wizard, prefilled with the game (`?gameId=`)
 */
export function getEntityCreateHref(entityType: MeepleEntityType, gameId: string): string | null {
  switch (entityType) {
    case 'agent':
      return `/library/${gameId}/agent`;
    case 'kb':
      return `/library/${gameId}/kb`;
    case 'chat':
      return `/chat/new?game=${gameId}`;
    case 'session':
      return `/sessions/new?gameId=${gameId}`;
    default:
      return null;
  }
}
