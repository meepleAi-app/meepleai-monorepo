import type { ConnectionChipProps } from '../types';

export interface ChatConnectionsCounts {
  messageCount: number;
}

export interface ChatConnectionsHandlers {
  onMessagesClick?: () => void;
  onAgentLinkClick?: () => void;
  onSourcesClick?: () => void;
  onArchiveClick?: () => void;
}

/**
 * Build the 4-slot connection channel for chat session entity cards.
 *
 * Slot order matches the contract spec in
 * docs/superpowers/plans/2026-04-08-meeplecard-consumers-completion.md:
 *   0 = Messaggi (count)
 *   1 = Sources (v1 disabled — no sources count in chat schema)
 *   2 = Agente (link to source agent)
 *   3 = Archivia (action)
 */
export function buildChatConnections(
  counts: ChatConnectionsCounts,
  handlers: ChatConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Messaggi',
      entityType: 'chat',
      count: counts.messageCount > 0 ? counts.messageCount : undefined,
      disabled: !handlers.onMessagesClick,
      onClick: handlers.onMessagesClick,
    },
    {
      label: 'Sources',
      entityType: 'kb',
      // v1: not implemented (no sources count in chat schema)
      disabled: !handlers.onSourcesClick,
      onClick: handlers.onSourcesClick,
    },
    {
      label: 'Agente',
      entityType: 'agent',
      disabled: !handlers.onAgentLinkClick,
      onClick: handlers.onAgentLinkClick,
    },
    {
      label: 'Archivia',
      entityType: 'chat',
      disabled: !handlers.onArchiveClick,
      onClick: handlers.onArchiveClick,
    },
  ];
}
