'use client';

/**
 * MeepleSessionCard - Game Session adapter using MeepleCard
 * Issue #5003 — Session Card: azioni contestuali per stato e ruolo
 *
 * Adapter component that wraps MeepleCard for GameSession display.
 * Implements the action visibility matrix from Issue #5003:
 *
 * | Action     | Visible when                         | Enabled when       | Tooltip se disabilitato          |
 * |------------|--------------------------------------|--------------------|----------------------------------|
 * | Configura  | isOwner || isAdmin                   | status ≠ completed | "La sessione è completata"       |
 * | Avvia      | isOwner + status = setup             | always             | —                                |
 * | Pausa      | isOwner + status = inProgress        | always             | —                                |
 * | Riprendi   | isOwner + status = paused            | always             | —                                |
 * | Partecipa  | !isParticipant + status ≠ completed  | hasSlots           | "Sessione piena"                 |
 * | Lascia     | isParticipant + !isOwner             | always             | —                                |
 * | Esporta    | status = completed                   | always             | —                                |
 *
 * @example
 * ```tsx
 * <MeepleSessionCard
 *   session={gameSessionDto}
 *   isOwner={currentUser?.id === session.hostId}
 *   isAdmin={hasRole(currentUser, 'Admin')}
 *   isParticipant={isUserInSession(currentUser, session)}
 *   hasSlots={session.playerCount < game.maxPlayers}  // game.maxPlayers from Game entity, not GameSessionDto
 *   onClick={(id) => router.push(`/sessions/${id}`)}
 *   onConfigure={handleConfigure}
 *   onStart={handleStart}
 *   onPause={handlePause}
 *   onResume={handleResume}
 *   onJoin={handleJoin}
 *   onLeave={handleLeave}
 *   onExport={handleExport}
 * />
 * ```
 */

import { Settings, Play, Pause, RefreshCw, UserPlus, LogOut, Download } from 'lucide-react';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';
import type { SessionStatus } from '@/components/ui/data-display/meeple-card-features/session-types';
import { getNavigationLinks } from '@/config/entity-navigation';
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
// Helper Functions
// ============================================================================

/**
 * Map GameSessionDto.status (backend PascalCase) to SessionStatus (frontend camelCase).
 */
function mapSessionStatus(status: string): SessionStatus {
  switch (status) {
    case 'Setup':
      return 'setup';
    case 'InProgress':
      return 'inProgress';
    case 'Paused':
      return 'paused';
    case 'Completed':
      return 'completed';
    default:
      return 'setup';
  }
}

// ============================================================================
// Component
// ============================================================================

export function MeepleSessionCard({
  session,
  variant = 'grid',
  isOwner = false,
  isAdmin = false,
  isParticipant = false,
  hasSlots = true,
  onClick,
  onConfigure,
  onStart,
  onPause,
  onResume,
  onJoin,
  onLeave,
  onExport,
  className,
}: MeepleSessionCardProps) {
  const isOwnerOrAdmin = isOwner || isAdmin;
  const sessionStatus = mapSessionStatus(session.status);
  const isCompleted = sessionStatus === 'completed';
  const isSetup = sessionStatus === 'setup';
  const isInProgress = sessionStatus === 'inProgress';
  const isPaused = sessionStatus === 'paused';

  // ============================================================================
  // Quick Actions Configuration
  // ============================================================================

  const entityQuickActions = [
    // Configura: visible for owner/admin, disabled (with tooltip) if completed
    {
      icon: Settings,
      label: 'Configura',
      onClick: () => onConfigure?.(session.id),
      hidden: !isOwnerOrAdmin,
      disabled: isCompleted,
      disabledTooltip: 'La sessione è completata',
    },
    // Avvia: visible only for owner when in setup state
    {
      icon: Play,
      label: 'Avvia',
      onClick: () => onStart?.(session.id),
      hidden: !isOwner || !isSetup,
    },
    // Pausa: visible only for owner when in progress
    {
      icon: Pause,
      label: 'Pausa',
      onClick: () => onPause?.(session.id),
      hidden: !isOwner || !isInProgress,
    },
    // Riprendi: visible only for owner when paused
    {
      icon: RefreshCw,
      label: 'Riprendi',
      onClick: () => onResume?.(session.id),
      hidden: !isOwner || !isPaused,
    },
    // Partecipa: visible for non-participants when not completed, disabled (with tooltip) if no slots
    {
      icon: UserPlus,
      label: 'Partecipa',
      onClick: () => onJoin?.(session.id),
      hidden: isParticipant || isCompleted,
      disabled: !hasSlots,
      disabledTooltip: 'Sessione piena',
    },
    // Lascia: visible for participants who are not the owner
    {
      icon: LogOut,
      label: 'Lascia',
      onClick: () => onLeave?.(session.id),
      hidden: !isParticipant || isOwner,
    },
    // Esporta: visible only when session is completed
    {
      icon: Download,
      label: 'Esporta',
      onClick: () => onExport?.(session.id),
      hidden: !isCompleted,
    },
  ];

  // ============================================================================
  // Card Data
  // ============================================================================

  const playerLabel = `${session.playerCount} giocator${session.playerCount === 1 ? 'e' : 'i'}`;
  const subtitle = session.winnerName
    ? `${playerLabel} · Vincitore: ${session.winnerName}`
    : playerLabel;

  // ============================================================================
  // Render
  // ============================================================================

  return (
    <MeepleCard
      id={session.id}
      entity="session"
      variant={variant}
      title={`Sessione #${session.id.slice(0, 8)}`}
      subtitle={subtitle}
      sessionStatus={sessionStatus}
      className={className}
      onClick={onClick ? () => onClick(session.id) : undefined}
      // Issue #5003: Quick actions with conditional visibility by status and role
      entityQuickActions={entityQuickActions}
      showInfoButton
      infoHref={`/sessions/${session.id}`}
      infoTooltip="Vai alla sessione"
      // Navigation footer: Game + Players + Agent + Chats links
      navigateTo={getNavigationLinks('session', {
        id: session.id,
        gameId: session.gameId,
      })}
      data-testid={`session-card-${session.id}`}
    />
  );
}

/**
 * MeepleSessionCard Skeleton for loading state
 */
export function MeepleSessionCardSkeleton({ variant = 'grid' }: { variant?: MeepleCardVariant }) {
  return (
    <MeepleCard
      entity="session"
      variant={variant}
      title=""
      loading
      data-testid="session-card-skeleton"
    />
  );
}

export default MeepleSessionCard;
