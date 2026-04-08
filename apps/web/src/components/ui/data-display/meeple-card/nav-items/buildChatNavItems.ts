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
 * Slot order matches the contract spec in
 * docs/superpowers/plans/2026-04-08-meeplecard-consumers-completion.md:
 *   0 = Messaggi (count)
 *   1 = Sources (v1 disabled — no sources count in chat schema)
 *   2 = Agente (link to source agent)
 *   3 = Archivia (action)
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
      icon: navIcons.kb,
      label: 'Sources',
      entity: 'kb',
      // v1: not implemented (no sources count in chat schema)
      disabled: !handlers.onSourcesClick,
      onClick: handlers.onSourcesClick,
    },
    {
      icon: navIcons.agent,
      label: 'Agente',
      entity: 'agent',
      disabled: !handlers.onAgentLinkClick,
      onClick: handlers.onAgentLinkClick,
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
