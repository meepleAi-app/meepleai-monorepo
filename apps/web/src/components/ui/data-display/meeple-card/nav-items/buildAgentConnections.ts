import type { ConnectionChipProps } from '../types';

export interface AgentConnectionsCounts {
  chatCount: number;
  kbCount: number;
}

export interface AgentConnectionsHandlers {
  onChatClick?: () => void;
  onKbClick?: () => void;
  onMemoryClick?: () => void;
  onConfigClick?: () => void;
}

/**
 * Build the 4-slot connection channel for agent entity cards.
 *
 * Slots: Chat | KB | Memorie (v1 disabled) | Config (action)
 */
export function buildAgentConnections(
  counts: AgentConnectionsCounts,
  handlers: AgentConnectionsHandlers
): ConnectionChipProps[] {
  return [
    {
      label: 'Chat',
      entityType: 'chat',
      count: counts.chatCount > 0 ? counts.chatCount : undefined,
      disabled: !handlers.onChatClick,
      onClick: handlers.onChatClick,
    },
    {
      label: 'KB',
      entityType: 'kb',
      count: counts.kbCount > 0 ? counts.kbCount : undefined,
      disabled: !handlers.onKbClick,
      onClick: handlers.onKbClick,
    },
    {
      label: 'Memorie',
      entityType: 'agent',
      // v1: not implemented (no memory count endpoint)
      disabled: !handlers.onMemoryClick,
      onClick: handlers.onMemoryClick,
    },
    {
      label: 'Config',
      entityType: 'agent',
      // Action button — no count
      disabled: !handlers.onConfigClick,
      onClick: handlers.onConfigClick,
    },
  ];
}
