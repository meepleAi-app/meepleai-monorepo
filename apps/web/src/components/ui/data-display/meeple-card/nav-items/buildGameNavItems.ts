import type { ConnectionChipProps } from '../types';

export interface GameConnectionsCounts {
  kbCount: number;
  agentCount: number;
  chatCount: number;
  sessionCount: number;
}

export interface GameConnectionsHandlers {
  onKbClick?: () => void;
  onAgentClick?: () => void;
  onChatClick?: () => void;
  onSessionClick?: () => void;
  onKbPlus?: () => void;
  onAgentPlus?: () => void;
  onChatPlus?: () => void;
  onSessionPlus?: () => void;
}

/**
 * Build the canonical 4-slot connection channel for game entity cards.
 *
 * Slots: KB | Agent | Chat | Sessioni
 * - gameId provided → items get href for direct Link navigation
 * - count > 0 → shows count badge
 * - count === 0 → plus indicator wired via onCreate (fires onXxxPlus handler)
 * - missing click handler AND no gameId → slot rendered disabled
 *
 * Step 2 (2026-04-24): renamed from buildGameNavItems to buildGameConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * This is the only builder that legitimately emits BOTH `href` AND `onClick`
 * on the same entry (see spec §1.1 + commit 0 which enabled the combo on
 * ConnectionChip). Old name retained as deprecated re-export until commit 8.
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
      label: 'Agent',
      entityType: 'agent',
      count: counts.agentCount > 0 ? counts.agentCount : undefined,
      disabled: !handlers.onAgentClick && !gameId,
      onClick: handlers.onAgentClick,
      onCreate: counts.agentCount === 0 ? handlers.onAgentPlus : undefined,
      href: gameId ? `/games/${gameId}/agent` : undefined,
    },
    {
      label: 'Chat',
      entityType: 'chat',
      count: counts.chatCount > 0 ? counts.chatCount : undefined,
      disabled: !handlers.onChatClick && !gameId,
      onClick: handlers.onChatClick,
      onCreate: counts.chatCount === 0 ? handlers.onChatPlus : undefined,
      href: gameId ? `/games/${gameId}/chat` : undefined,
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

/**
 * @deprecated Use `buildGameConnections` instead. Will be removed in commit 8 of
 * the Step 2 migration PR.
 */
export const buildGameNavItems = buildGameConnections;

/** @deprecated Use `GameConnectionsCounts` instead. */
export type GameNavCounts = GameConnectionsCounts;

/** @deprecated Use `GameConnectionsHandlers` instead. */
export type GameNavHandlers = GameConnectionsHandlers;
