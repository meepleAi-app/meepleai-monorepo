/**
 * MeepleAgentCard - Agent adapter using MeepleCard
 * Issue #5000 — Agent Card: azioni contestuali e visibilità condizionale
 *
 * Adapter component that wraps MeepleCard for Agent display.
 * Implements the action visibility matrix from Issue #5000:
 *
 * | Action     | Visible when          | Enabled when |
 * |------------|-----------------------|--------------|
 * | Info       | always                | always       |
 * | Chat       | always                | isActive     |
 * | Configura  | isOwner || isAdmin    | always       |
 * | Elimina    | isOwner || isAdmin    | always       |
 *
 * @example
 * ```tsx
 * <MeepleAgentCard
 *   agent={agentDto}
 *   variant="grid"
 *   isOwner={currentUser?.id === agent.ownerId}
 *   isAdmin={hasRole(currentUser, 'Admin')}
 *   onConfigure={handleConfigure}
 *   onDelete={handleDelete}
 * />
 * ```
 */

'use client';

import { MessageCircle, Settings, Trash2 } from 'lucide-react';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import type { AgentStatus } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import { getNavigationLinks } from '@/config/entity-navigation';
import type { AgentDto } from '@/lib/api';

// ============================================================================
// Types
// ============================================================================

export interface MeepleAgentCardProps {
  /** Agent DTO from API */
  agent: AgentDto;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Whether the current user owns this agent */
  isOwner?: boolean;
  /** Whether the current user is an admin */
  isAdmin?: boolean;
  /** Configure agent callback */
  onConfigure?: (agentId: string, agentName: string) => void;
  /** Delete agent callback */
  onDelete?: (agentId: string, agentName: string) => void;
  /** Selection mode enabled */
  selectionMode?: boolean;
  /** Card is selected */
  isSelected?: boolean;
  /** Selection callback */
  onSelect?: (agentId: string, shiftKey: boolean) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Map AgentDto active/idle flags to AgentStatus for MeepleCard display.
 */
function mapAgentStatus(agent: AgentDto): AgentStatus {
  if (agent.isActive) return 'active';
  return 'idle';
}

// ============================================================================
// Component
// ============================================================================

export function MeepleAgentCard({
  agent,
  variant = 'grid',
  isOwner = false,
  isAdmin = false,
  onConfigure,
  onDelete,
  selectionMode = false,
  isSelected = false,
  onSelect,
  className,
}: MeepleAgentCardProps) {
  const isOwnerOrAdmin = isOwner || isAdmin;
  const agentStatus = mapAgentStatus(agent);

  // ============================================================================
  // Quick Actions Configuration
  // ============================================================================

  const entityQuickActions = [
    // Chat: always visible, disabled with tooltip when !isActive
    {
      icon: MessageCircle,
      label: agent.isActive ? 'Chat' : 'Agente non attivo',
      onClick: () => {
        window.location.href = `/chat/new?agentId=${agent.id}`;
      },
      disabled: !agent.isActive,
    },
    // Configura: visible only for owner/admin
    {
      icon: Settings,
      label: 'Configura',
      onClick: () => onConfigure?.(agent.id, agent.name),
      hidden: !isOwnerOrAdmin,
    },
    // Elimina: visible only for owner/admin
    {
      icon: Trash2,
      label: 'Elimina',
      onClick: () => onDelete?.(agent.id, agent.name),
      hidden: !isOwnerOrAdmin,
    },
  ];

  // ============================================================================
  // Card Data
  // ============================================================================

  const subtitle = `${agent.type} agent`;

  const agentStats = {
    invocationCount: agent.invocationCount,
    lastExecutedAt: agent.lastInvokedAt ?? undefined,
  };

  // ============================================================================
  // Render
  // ============================================================================

  const handleSelect = (id: string, _selected: boolean) => {
    onSelect?.(id, false);
  };

  return (
    <MeepleCard
      id={agent.id}
      entity="agent"
      variant={variant}
      title={agent.name}
      subtitle={subtitle}
      agentStatus={agentStatus}
      agentStats={agentStats}
      className={className}
      onClick={selectionMode && onSelect ? undefined : () => window.location.href = `/agents/${agent.id}`}
      // Issue #5000: Quick actions with conditional visibility
      entityQuickActions={entityQuickActions}
      showInfoButton
      infoHref={`/agents/${agent.id}`}
      infoTooltip="Vai al dettaglio"
      // Navigation footer
      navigateTo={getNavigationLinks('agent', { id: agent.id })}
      // Bulk selection
      selectable={selectionMode}
      selected={isSelected}
      onSelect={handleSelect}
      data-testid={`agent-card-${agent.id}`}
    />
  );
}

/**
 * MeepleAgentCard Skeleton for loading state
 */
export function MeepleAgentCardSkeleton({
  variant = 'grid',
}: {
  variant?: MeepleCardVariant;
}) {
  return (
    <MeepleCard
      entity="agent"
      variant={variant}
      title=""
      loading
      data-testid="agent-card-skeleton"
    />
  );
}

export default MeepleAgentCard;
