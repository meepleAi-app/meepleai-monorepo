'use client';

/**
 * MeepleSessionCard - Game Session adapter using MeepleCard
 * Issue #5003 — Session Card: azioni contestuali per stato e ruolo
 */

import { useMemo } from 'react';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import { buildSessionConnections } from '@/components/ui/data-display/meeple-card/nav-items';
import type { GameSessionDto } from '@/lib/api/schemas/games.schemas';

// ============================================================================
// Types
// ============================================================================

export interface MeepleSessionCardProps {
  /** Game session DTO from API */
  session: GameSessionDto;
  /** Layout variant */
  variant?: MeepleCardVariant;
  /** Whether the current user owns/hosts this session */
  isOwner?: boolean;
  /** Whether the current user is an admin */
  isAdmin?: boolean;
  /** Whether the current user is already a participant */
  isParticipant?: boolean;
  /** Whether the session still has available player slots */
  hasSlots?: boolean;
  /** Card click handler — navigate to session detail page */
  onClick?: (sessionId: string) => void;
  /** Configure callback (owner/admin, when not completed) */
  onConfigure?: (sessionId: string) => void;
  /** Start session callback (owner, when setup) */
  onStart?: (sessionId: string) => void;
  /** Pause session callback (owner, when inProgress) */
  onPause?: (sessionId: string) => void;
  /** Resume session callback (owner, when paused) */
  onResume?: (sessionId: string) => void;
  /** Join session callback (!participant, when not completed + hasSlots) */
  onJoin?: (sessionId: string) => void;
  /** Leave session callback (participant + !owner) */
  onLeave?: (sessionId: string) => void;
  /** Export session callback (when completed) */
  onExport?: (sessionId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleSessionCard({
  session,
  variant = 'grid',
  onClick,
  className,
}: MeepleSessionCardProps) {
  const playerLabel = `${session.playerCount} giocator${session.playerCount === 1 ? 'e' : 'i'}`;
  const subtitle = session.winnerName
    ? `${playerLabel} · Vincitore: ${session.winnerName}`
    : playerLabel;

  const statusBadge =
    session.status === 'Completed'
      ? 'Completata'
      : session.status === 'InProgress'
        ? 'In corso'
        : session.status === 'Paused'
          ? 'In pausa'
          : undefined;

  const connections = useMemo(
    () =>
      buildSessionConnections(
        {
          playerCount: session.playerCount,
          hasNotes: false, // session DTO doesn't expose notes flag
          toolCount: 0,
          photoCount: 0,
        },
        {
          onPlayersClick: onClick ? () => onClick(session.id) : undefined,
        }
      ),
    [session.playerCount, session.id, onClick]
  );

  return (
    <MeepleCard
      id={session.id}
      entity="session"
      variant={variant}
      title={`Sessione #${session.id.slice(0, 8)}`}
      subtitle={subtitle}
      badge={statusBadge}
      connections={connections}
      className={className}
      onClick={onClick ? () => onClick(session.id) : undefined}
      data-testid={`session-card-${session.id}`}
    />
  );
}

/**
 * MeepleSessionCard Skeleton for loading state
 */
export function MeepleSessionCardSkeleton({ variant = 'grid' }: { variant?: MeepleCardVariant }) {
  return (
    <MeepleCard entity="session" variant={variant} title="" data-testid="session-card-skeleton" />
  );
}

export default MeepleSessionCard;
