'use client';

/**
 * MeepleChatCard - Chat Session adapter using MeepleCard
 * Issue #5002 — Chat Card: azioni contestuali e slot management
 *
 * Adapter component that wraps MeepleCard for ChatSession display.
 * Implements the action visibility matrix from Issue #5002:
 *
 * | Action         | Visible when              | Enabled when       | Tooltip se disabilitato          |
 * |----------------|---------------------------|--------------------|----------------------------------|
 * | Apri/Continua  | always                    | !isArchived        | "Chat archiviata"                |
 * | Nuova Chat     | isOwner                   | hasGameChatSlot    | "Limite chat raggiunto"          |
 * | Archivia       | isOwner + !isArchived     | always             | —                                |
 * | Riattiva       | isOwner + isArchived      | always             | —                                |
 * | Elimina        | isOwner || isAdmin        | always             | —                                |
 *
 * @example
 * ```tsx
 * <MeepleChatCard
 *   session={chatSessionSummaryDto}
 *   isOwner={currentUser?.id === session.userId}
 *   isAdmin={hasRole(currentUser, 'Admin')}
 *   hasGameChatSlot={gameSlotInfo.hasAvailableSlot}
 *   onClick={(id) => router.push(`/chat/${id}`)}
 *   onNewChat={handleNewChat}
 *   onArchive={handleArchive}
 *   onReactivate={handleReactivate}
 *   onDelete={handleDelete}
 * />
 * ```
 */

import { MessageCircle, PlusCircle, Archive, RefreshCcw, Trash2 } from 'lucide-react';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import type { ChatStatus } from '@/components/ui/data-display/meeple-card-features/ChatStatusBadge';
import { getNavigationLinks } from '@/config/entity-navigation';
import type { ChatSessionSummaryDto } from '@/lib/api/schemas/chat-sessions.schemas';

// ============================================================================
// Types
// ============================================================================

export interface MeepleChatCardProps {
  /** Chat session summary DTO from API (list view) */
  session: ChatSessionSummaryDto;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Whether the current user owns this chat */
  isOwner?: boolean;
  /** Whether the current user is an admin */
  isAdmin?: boolean;
  /** Whether the associated game still has available chat slots */
  hasGameChatSlot?: boolean;
  /** Card click handler and Apri/Continua action — navigate to chat detail */
  onClick?: (sessionId: string) => void;
  /** New chat callback (owner only) */
  onNewChat?: (gameId: string) => void;
  /** Archive chat callback (owner, when !isArchived) */
  onArchive?: (sessionId: string) => void;
  /** Reactivate chat callback (owner, when isArchived) */
  onReactivate?: (sessionId: string) => void;
  /** Delete chat callback (owner/admin) */
  onDelete?: (sessionId: string, title: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

/**
 * Map ChatSessionSummaryDto.isArchived to ChatStatus for MeepleCard display.
 */
function mapChatStatus(session: ChatSessionSummaryDto): ChatStatus {
  return session.isArchived ? 'archived' : 'active';
}

// ============================================================================
// Component
// ============================================================================

export function MeepleChatCard({
  session,
  variant = 'grid',
  isOwner = false,
  isAdmin = false,
  hasGameChatSlot = true,
  onClick,
  onNewChat,
  onArchive,
  onReactivate,
  onDelete,
  className,
}: MeepleChatCardProps) {
  const isOwnerOrAdmin = isOwner || isAdmin;
  const chatStatus = mapChatStatus(session);

  // ============================================================================
  // Quick Actions Configuration
  // ============================================================================

  const entityQuickActions = [
    // Apri/Continua: always visible, disabled (with tooltip) when archived
    {
      icon: MessageCircle,
      label: 'Apri/Continua',
      onClick: () => onClick?.(session.id),
      disabled: session.isArchived,
      disabledTooltip: 'Chat archiviata',
    },
    // Nuova Chat: visible only for owner, disabled (with tooltip) if no slot available
    {
      icon: PlusCircle,
      label: 'Nuova Chat',
      onClick: () => onNewChat?.(session.gameId),
      hidden: !isOwner,
      disabled: !hasGameChatSlot,
      disabledTooltip: 'Limite chat raggiunto',
    },
    // Archivia: visible only for owner when not archived
    {
      icon: Archive,
      label: 'Archivia',
      onClick: () => onArchive?.(session.id),
      hidden: !isOwner || session.isArchived,
    },
    // Riattiva: visible only for owner when archived
    {
      icon: RefreshCcw,
      label: 'Riattiva',
      onClick: () => onReactivate?.(session.id),
      hidden: !isOwner || !session.isArchived,
    },
    // Elimina: visible for owner/admin
    {
      icon: Trash2,
      label: 'Elimina',
      onClick: () => onDelete?.(session.id, session.title ?? 'Chat'),
      hidden: !isOwnerOrAdmin,
    },
  ];

  // ============================================================================
  // Card Data
  // ============================================================================

  const title = session.title ?? 'Chat senza titolo';
  const subtitle = `${session.messageCount} messagg${session.messageCount === 1 ? 'io' : 'i'}${session.gameTitle ? ` · ${session.gameTitle}` : ''}`;

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <MeepleCard
      id={session.id}
      entity="chatSession"
      variant={variant}
      title={title}
      subtitle={subtitle}
      chatStatus={chatStatus}
      chatStats={{ messageCount: session.messageCount, lastMessageAt: session.lastMessageAt ?? undefined }}
      className={className}
      onClick={onClick ? () => onClick(session.id) : undefined}
      // Issue #5002: Quick actions with conditional visibility and slot management
      entityQuickActions={entityQuickActions}
      showInfoButton
      infoHref={`/chat/${session.id}`}
      infoTooltip="Vai alla chat"
      // Navigation footer: Game link
      navigateTo={getNavigationLinks('chatSession', { gameId: session.gameId })}
      data-testid={`chat-card-${session.id}`}
    />
  );
}

/**
 * MeepleChatCard Skeleton for loading state
 */
export function MeepleChatCardSkeleton({
  variant = 'grid',
}: {
  variant?: MeepleCardVariant;
}) {
  return (
    <MeepleCard
      entity="chatSession"
      variant={variant}
      title=""
      loading
      data-testid="chat-card-skeleton"
    />
  );
}

export default MeepleChatCard;
