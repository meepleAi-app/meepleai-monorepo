/**
 * MeepleGameWidget - Dashboard Widget using MeepleCard
 * Issue #3334 - MeepleCard Integration with Dashboard
 *
 * Adapter component that wraps MeepleCard for dashboard widget usage.
 * Provides compact and list variants suitable for dashboard context.
 *
 * @example
 * ```tsx
 * // In RecentGamesWidget
 * <MeepleGameWidget
 *   game={recentGame}
 *   variant="compact"
 *   subtitle="Giocato 2 giorni fa"
 *   onClick={() => router.push(`/games/${game.id}`)}
 * />
 *
 * // In library preview
 * <MeepleGameWidget
 *   game={libraryGame}
 *   variant="list"
 *   showRating
 * />
 * ```
 */

'use client';

import { Users, Clock } from 'lucide-react';

import { MeepleCard, type MeepleCardVariant } from '@/components/ui/data-display/meeple-card';

// ============================================================================
// Types
// ============================================================================

export interface DashboardGame {
  id: string;
  title: string;
  imageUrl?: string | null;
  thumbnailUrl?: string | null;
  minPlayers?: number | null;
  maxPlayers?: number | null;
  playingTimeMinutes?: number | null;
  rating?: number | null;
  lastPlayedAt?: string | null;
}

export interface MeepleGameWidgetProps {
  /** Game data */
  game: DashboardGame;
  /** Layout variant (compact or list recommended for dashboard) */
  variant?: Extract<MeepleCardVariant, 'compact' | 'list' | 'grid'>;
  /** Custom subtitle (e.g., "Giocato 2 giorni fa") */
  subtitle?: string;
  /** Show rating stars */
  showRating?: boolean;
  /** Click handler */
  onClick?: () => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function formatPlayers(min: number | null | undefined, max: number | null | undefined): string {
  if (!min && !max) return '';
  if (min === max) return `${min}`;
  return `${min || '?'}-${max || '?'}`;
}

function formatPlaytime(minutes: number | null | undefined): string {
  if (!minutes) return '';
  if (minutes >= 60) {
    const hours = Math.floor(minutes / 60);
    return `${hours}h`;
  }
  return `${minutes}m`;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleGameWidget({
  game,
  variant = 'compact',
  subtitle,
  showRating = false,
  onClick,
  className,
}: MeepleGameWidgetProps) {
  // Build metadata for list variant
  const metadata =
    variant === 'list'
      ? [
          ...(game.minPlayers || game.maxPlayers
            ? [{ icon: Users, value: formatPlayers(game.minPlayers, game.maxPlayers) }]
            : []),
          ...(game.playingTimeMinutes
            ? [{ icon: Clock, value: formatPlaytime(game.playingTimeMinutes) }]
            : []),
        ]
      : undefined;

  return (
    <MeepleCard
      entity="game"
      variant={variant}
      title={game.title}
      subtitle={subtitle}
      imageUrl={game.thumbnailUrl || game.imageUrl || undefined}
      rating={showRating && game.rating ? game.rating : undefined}
      ratingMax={10}
      metadata={metadata}
      onClick={onClick}
      className={className}
      data-testid={`dashboard-game-${game.id}`}
    />
  );
}

/**
 * MeepleGameWidget Skeleton for loading state
 */
export function MeepleGameWidgetSkeleton({
  variant = 'compact',
}: {
  variant?: Extract<MeepleCardVariant, 'compact' | 'list' | 'grid'>;
}) {
  return (
    <MeepleCard
      entity="game"
      variant={variant}
      title=""
      loading
      data-testid="dashboard-game-skeleton"
    />
  );
}

export default MeepleGameWidget;
