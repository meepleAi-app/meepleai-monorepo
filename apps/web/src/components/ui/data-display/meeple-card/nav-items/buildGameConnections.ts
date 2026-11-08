import type { ConnectionChipProps } from '../types';

export interface GameConnectionsCounts {
  kbCount: number;
  chatCount: number;
  sessionCount: number;
}

export interface GameConnectionsHandlers {
  onKbClick?: () => void;
  onChatClick?: () => void;
  onSessionClick?: () => void;
  onKbPlus?: () => void;
  onChatPlus?: () => void;
  onSessionPlus?: () => void;
}

/**
 * Build the canonical 3-slot connection channel for game entity cards.
 *
 * Slots: KB | Chat | Sessioni
 *
 * Issue #4138: there was a fourth slot, Agent, between KB and Chat. With a single
 * system agent (ADR-094) it showed the same global agent on every card, so it said
 * nothing about the game. Chat is the way into that agent.
 * - gameId provided → items get href for direct Link navigation
 * - count > 0 → shows count badge
 * - count === 0 → plus indicator wired via onCreate (fires onXxxPlus handler)
 * - missing click handler AND no gameId → slot rendered disabled
 *
 * Note: this is the only builder that legitimately emits BOTH `href` AND
 * `onClick` on the same entry (see spec §1.1) — ConnectionChip renders as a
 * Link and onClick fires on left-click while href preserves middle-click
 * semantics.
 */
export function buildGameConnections(
  counts: GameConnectionsCounts,
  handlers: GameConnectionsHandlers,
  gameId?: string
): ConnectionChipProps[] {
  return [
    {
      label: 'KB',
      entityType: 'kb',
      count: counts.kbCount > 0 ? counts.kbCount : undefined,
      disabled: !handlers.onKbClick && !gameId,
      onClick: handlers.onKbClick,
      onCreate: counts.kbCount === 0 ? handlers.onKbPlus : undefined,
      href: gameId ? `/games/${gameId}/kb` : undefined,
    },
    {
      label: 'Chat',
      entityType: 'chat',
      count: counts.chatCount > 0 ? counts.chatCount : undefined,
      disabled: !handlers.onChatClick && !gameId,
      onClick: handlers.onChatClick,
      onCreate: counts.chatCount === 0 ? handlers.onChatPlus : undefined,
      // ADR-061: /games/{id}/chat removed (was orphan). Redirect to chat
      // creation flow with game pre-selected via query filter.
      href: gameId ? `/chat/new?gameId=${gameId}` : undefined,
    },
    {
      label: 'Sessioni',
      entityType: 'session',
      count: counts.sessionCount > 0 ? counts.sessionCount : undefined,
      disabled: !handlers.onSessionClick && !gameId,
      onClick: handlers.onSessionClick,
      onCreate: counts.sessionCount === 0 ? handlers.onSessionPlus : undefined,
      href: gameId ? `/games/${gameId}/sessions` : undefined,
    },
  ];
}
