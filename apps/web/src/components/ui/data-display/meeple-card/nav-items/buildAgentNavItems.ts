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
 *
 * Step 2 (2026-04-24): renamed from buildAgentNavItems to buildAgentConnections.
 * Return shape changed from NavFooterItem[] to ConnectionChipProps[].
 * Old name retained as deprecated re-export until cleanup commit 8.
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

/**
 * @deprecated Use `buildAgentConnections` instead. Will be removed in commit 8
 * of the Step 2 migration PR.
 */
export const buildAgentNavItems = buildAgentConnections;

/** @deprecated Use `AgentConnectionsCounts` instead. Removed in commit 8. */
export type AgentNavCounts = AgentConnectionsCounts;

/** @deprecated Use `AgentConnectionsHandlers` instead. Removed in commit 8. */
export type AgentNavHandlers = AgentConnectionsHandlers;
