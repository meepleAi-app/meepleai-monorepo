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

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import type { SessionPlayer } from '@/lib/api/schemas/play-records.schemas';
import { buildPlayerCardProps } from '@/lib/card-mappers';

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
  // Map player to card props for consistency
  const mapperProps = buildPlayerCardProps(player);

  // Compute profile href based on player type (unused until navigation wiring)
  const _profileHref =
    isMeepleAiUser && player.userId ? `/users/${player.userId}` : `/players/${player.id}`;

  // ============================================================================
  // Quick Actions Configuration
  // ============================================================================

  const actions = [
    // Configura/Edit: visible only for owner + non-meeple-user
    ...(isCreatedByCurrentUser && !isMeepleAiUser
      ? [
          {
            icon: '⚙️',
            label: 'Configura',
            onClick: () => onConfigure?.(player.id),
          },
        ]
      : []),
    // Invita a Sessione: visible for authenticated users, disabled if no active session
    ...(isAuthenticated
      ? [
          {
            icon: '➕',
            label: 'Invita a Sessione',
            onClick: () => onInvite?.(player.id),
            disabled: !hasActiveSession,
          },
        ]
      : []),
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
      title={mapperProps.title ?? player.displayName}
      subtitle={subtitle}
      className={className}
      onClick={onClick ? () => onClick(player.id) : undefined}
      actions={actions.length > 0 ? actions : undefined}
      data-testid={`player-card-${player.id}`}
    />
  );
}

/**
 * MeeplePlayerCard Skeleton for loading state
 */
export function MeeplePlayerCardSkeleton({ variant = 'grid' }: { variant?: MeepleCardVariant }) {
  return (
    <MeepleCard entity="player" variant={variant} title="..." data-testid="player-card-skeleton" />
  );
}

export default MeeplePlayerCard;
