import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface AgentNavCounts {
  chatCount: number;
  kbCount: number;
}

export interface AgentNavHandlers {
  onChatClick?: () => void;
  onKbClick?: () => void;
  onMemoryClick?: () => void;
  onConfigClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for agent entity cards.
 *
 * Slots: Chat | KB | Memorie (v1 disabled) | Config (action)
 */
export function buildAgentNavItems(
  counts: AgentNavCounts,
  handlers: AgentNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.chat,
      label: 'Chat',
      entity: 'chat',
      count: counts.chatCount > 0 ? counts.chatCount : undefined,
      disabled: !handlers.onChatClick,
      onClick: handlers.onChatClick,
    },
    {
      icon: navIcons.kb,
      label: 'KB',
      entity: 'kb',
      count: counts.kbCount > 0 ? counts.kbCount : undefined,
      disabled: !handlers.onKbClick,
      onClick: handlers.onKbClick,
    },
    {
      icon: navIcons.memory,
      label: 'Memorie',
      entity: 'agent',
      // v1: not implemented (no memory count endpoint)
      disabled: !handlers.onMemoryClick,
      onClick: handlers.onMemoryClick,
    },
    {
      icon: navIcons.config,
      label: 'Config',
      entity: 'agent',
      // Action button — no count
      disabled: !handlers.onConfigClick,
      onClick: handlers.onConfigClick,
    },
  ];
}
