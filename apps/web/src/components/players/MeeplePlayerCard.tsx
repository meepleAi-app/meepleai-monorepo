'use client';

/**
 * MeeplePlayerCard - Player adapter using MeepleCard
 * Issue #5004 — Player Card: azioni contestuali + stub pagina profilo
 *
 * Adapter component that wraps MeepleCard for player display.
 * Implements the action visibility matrix from Issue #5004:
 *
 * | Action             | Visible when                              | Enabled when          | Tooltip se disabilitato    |
 * |--------------------|-------------------------------------------|-----------------------|----------------------------|
 * | Info/Dettaglio     | always                                    | always                | —                          |
 * | Configura/Edit     | isCreatedByCurrentUser + !isMeepleAiUser  | always                | —                          |
 * | Invita a Sessione  | isAuthenticated                           | hasActiveSession      | "Nessuna sessione attiva"  |
 *
 * Routing for Info/Detail:
 * - isMeepleAiUser=true  → /users/[userId]
 * - isMeepleAiUser=false → /players/[id]
 *
 * @example
 * ```tsx
 * <MeeplePlayerCard
 *   player={sessionPlayer}
 *   isMeepleAiUser={player.userId !== null}
 *   isCreatedByCurrentUser={playRecord.createdByUserId === currentUser.id}
 *   isAuthenticated={!!currentUser}
 *   hasActiveSession={!!activeSession}
 *   onConfigure={(id) => router.push(`/players/${id}/edit`)}
 *   onInvite={(id) => handleInviteToSession(id)}
 * />
 * ```
 */

import { Settings, UserPlus } from 'lucide-react';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { getNavigationLinks } from '@/config/entity-navigation';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';

// ============================================================================
// Types
// ============================================================================

export interface MeeplePlayerCardProps {
  /** Session player DTO */
  player: SessionPlayer;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Whether the player is a registered MeepleAI user (userId !== null) */
  isMeepleAiUser?: boolean;
  /** Whether the current user created this player record */
  isCreatedByCurrentUser?: boolean;
  /** Whether the viewer is authenticated */
  isAuthenticated?: boolean;
  /** Whether there is an active session to invite the player to */
  hasActiveSession?: boolean;
  /** Card click handler — navigates to player profile */
  onClick?: (playerId: string) => void;
  /** Configure/Edit player callback (owner, non-meeple-user only) */
  onConfigure?: (playerId: string) => void;
  /** Invite to session callback (authenticated users) */
  onInvite?: (playerId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function MeeplePlayerCard({
  player,
  variant = 'grid',
  isMeepleAiUser = false,
  isCreatedByCurrentUser = false,
  isAuthenticated = false,
  hasActiveSession = false,
  onClick,
  onConfigure,
  onInvite,
  className,
}: MeeplePlayerCardProps) {
  // Compute profile href based on player type
  const profileHref = isMeepleAiUser && player.userId
    ? `/users/${player.userId}`
    : `/players/${player.id}`;

  // ============================================================================
  // Quick Actions Configuration
  // ============================================================================

  const entityQuickActions = [
    // Configura/Edit: visible only for owner + non-meeple-user
    {
      icon: Settings,
      label: 'Configura',
      onClick: () => onConfigure?.(player.id),
      hidden: !isCreatedByCurrentUser || isMeepleAiUser,
    },
    // Invita a Sessione: visible for authenticated users, disabled if no active session
    {
      icon: UserPlus,
      label: 'Invita a Sessione',
      onClick: () => onInvite?.(player.id),
      hidden: !isAuthenticated,
      disabled: !hasActiveSession,
      disabledTooltip: 'Nessuna sessione attiva',
    },
  ];

  // ============================================================================
  // Card Data
  // ============================================================================

  const subtitle = isMeepleAiUser ? 'Utente MeepleAI' : 'Giocatore';

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <MeepleCard
      id={player.id}
      entity="player"
      variant={variant}
      title={player.displayName}
      subtitle={subtitle}
      className={className}
      onClick={onClick ? () => onClick(player.id) : undefined}
      // Issue #5004: Quick actions with conditional visibility
      entityQuickActions={entityQuickActions}
      showInfoButton
      infoHref={profileHref}
      infoTooltip="Vai al profilo"
      // Navigation footer
      navigateTo={getNavigationLinks('player', { id: player.id })}
      data-testid={`player-card-${player.id}`}
    />
  );
}

/**
 * MeeplePlayerCard Skeleton for loading state
 */
export function MeeplePlayerCardSkeleton({
  variant = 'grid',
}: {
  variant?: MeepleCardVariant;
}) {
  return (
    <MeepleCard
      entity="player"
      variant={variant}
      title=""
      loading
      data-testid="player-card-skeleton"
    />
  );
}

export default MeeplePlayerCard;
