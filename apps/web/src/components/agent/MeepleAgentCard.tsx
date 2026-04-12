/**
 * MeepleAgentCard — Agent entity adapter for MeepleCard.
 *
 * Renders a custom or system agent with nav-footer wired to chat, KB, and config routes.
 * The chat count comes from the optional `chatCount` prop (caller can pass 0 for unknown).
 */

'use client';

import { useMemo } from 'react';

import { useRouter } from 'next/navigation';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildAgentNavItems } from '@/components/ui/data-display/meeple-card/nav-items';

// ============================================================================
// Types
// ============================================================================

export interface AgentSummary {
  id: string;
  name: string;
  description?: string | null;
  iconUrl?: string | null;
  /** Number of KB documents linked. Defaults to 0 if unknown. */
  kbCount?: number;
  /** Number of chat sessions for this agent. Defaults to 0 if unknown. */
  chatCount?: number;
}

export interface MeepleAgentCardProps {
  agent: AgentSummary;
  variant?: MeepleCardVariant;
  onClick?: (agentId: string) => void;
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleAgentCard({
  agent,
  variant = 'grid',
  onClick,
  className,
}: MeepleAgentCardProps) {
  const router = useRouter();

  const navItems = useMemo(
    () =>
      buildAgentNavItems(
        {
          chatCount: agent.chatCount ?? 0,
          kbCount: agent.kbCount ?? 0,
        },
        {
          onChatClick: () => router.push(`/chat?agentId=${agent.id}`),
          onKbClick: () => router.push(`/agents/${agent.id}/sources`),
          onConfigClick: () => router.push(`/agents/${agent.id}/settings`),
        }
      ),
    [agent.chatCount, agent.kbCount, agent.id, router]
  );

  return (
    <MeepleCard
      id={agent.id}
      entity="agent"
      variant={variant}
      title={agent.name}
      subtitle={agent.description ?? undefined}
      imageUrl={agent.iconUrl ?? undefined}
      navItems={navItems}
      onClick={onClick ? () => onClick(agent.id) : undefined}
      className={className}
      data-testid={`agent-card-${agent.id}`}
    />
  );
}

export default MeepleAgentCard;
