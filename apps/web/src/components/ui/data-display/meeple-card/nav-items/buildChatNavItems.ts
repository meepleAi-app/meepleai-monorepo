import { navIcons } from './icons';

import type { NavFooterItem } from '../types';

export interface ChatNavCounts {
  messageCount: number;
}

export interface ChatNavHandlers {
  onMessagesClick?: () => void;
  onAgentLinkClick?: () => void;
  onSourcesClick?: () => void;
  onArchiveClick?: () => void;
}

/**
 * Build the 4-slot nav-footer for chat session entity cards.
 *
 * Slots: Messaggi | Agente (link) | Sources (v1 disabled) | Archivia (action)
 */
export function buildChatNavItems(
  counts: ChatNavCounts,
  handlers: ChatNavHandlers
): NavFooterItem[] {
  return [
    {
      icon: navIcons.messages,
      label: 'Messaggi',
      entity: 'chat',
      count: counts.messageCount > 0 ? counts.messageCount : undefined,
      disabled: !handlers.onMessagesClick,
      onClick: handlers.onMessagesClick,
    },
    {
      icon: navIcons.agent,
      label: 'Agente',
      entity: 'agent',
      disabled: !handlers.onAgentLinkClick,
      onClick: handlers.onAgentLinkClick,
    },
    {
      icon: navIcons.kb,
      label: 'Sources',
      entity: 'kb',
      // v1: not implemented (no sources count in chat schema)
      disabled: !handlers.onSourcesClick,
      onClick: handlers.onSourcesClick,
    },
    {
      icon: navIcons.archive,
      label: 'Archivia',
      entity: 'chat',
      disabled: !handlers.onArchiveClick,
      onClick: handlers.onArchiveClick,
    },
  ];
}
