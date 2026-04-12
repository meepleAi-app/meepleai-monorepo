import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface GameNavCounts {
  kbCount: number;
  agentCount: number;
  chatCount: number;
  sessionCount: number;
}

export interface GameNavHandlers {
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
 * Build the canonical 4-slot nav-footer for game entity cards.
 *
 * Slots: KB | Agent | Chat | Sessioni
 * - gameId provided → items get href for direct Link navigation
 * - count > 0 → shows count badge
 * - count === 0 → shows plus indicator that fires onPlusClick
 * - missing handler → slot rendered disabled
 */
export function buildGameNavItems(
  counts: GameNavCounts,
  handlers: GameNavHandlers,
  gameId?: string
): NavFooterItem[] {
  return [
    {
      icon: navIcons.kb,
      label: 'KB',
      entity: 'kb',
      count: counts.kbCount > 0 ? counts.kbCount : undefined,
      showPlus: counts.kbCount === 0,
      disabled: !handlers.onKbClick && !gameId,
      onClick: handlers.onKbClick,
      onPlusClick: handlers.onKbPlus,
      href: gameId ? `/games/${gameId}/kb` : undefined,
    },
    {
      icon: navIcons.agent,
      label: 'Agent',
      entity: 'agent',
      count: counts.agentCount > 0 ? counts.agentCount : undefined,
      showPlus: counts.agentCount === 0,
      disabled: !handlers.onAgentClick && !gameId,
      onClick: handlers.onAgentClick,
      onPlusClick: handlers.onAgentPlus,
      href: gameId ? `/games/${gameId}/agent` : undefined,
    },
    {
      icon: navIcons.chat,
      label: 'Chat',
      entity: 'chat',
      count: counts.chatCount > 0 ? counts.chatCount : undefined,
      showPlus: counts.chatCount === 0,
      disabled: !handlers.onChatClick && !gameId,
      onClick: handlers.onChatClick,
      onPlusClick: handlers.onChatPlus,
      href: gameId ? `/games/${gameId}/chat` : undefined,
    },
    {
      icon: navIcons.session,
      label: 'Sessioni',
      entity: 'session',
      count: counts.sessionCount > 0 ? counts.sessionCount : undefined,
      showPlus: counts.sessionCount === 0,
      disabled: !handlers.onSessionClick && !gameId,
      onClick: handlers.onSessionClick,
      onPlusClick: handlers.onSessionPlus,
      href: gameId ? `/games/${gameId}/sessions` : undefined,
    },
  ];
}
