/**
 * GameCard Component (Issue #1830: UI-003)
 *
 * @deprecated Use MeepleCard from '@/components/ui/data-display/meeple-card' directly.
 * This component is a compatibility wrapper that maps legacy GameCard props to MeepleCard.
 * Migration: Issue #3331 - Replace GameCard with MeepleCard
 *
 * Dual-variant card component for game display:
 * - Grid: Compact 160x220px with cover image
 * - List: Full-width with thumbnail and detailed metadata
 *
 * Features:
 * - BGG rating stars (0-10 → 0-5 conversion)
 * - FAQ count badge
 * - Responsive hover animations
 * - Lazy-loaded images with blur placeholder
 * - Quicksand font for titles
 *
 * @see docs/wireframes page 3 "Catalogo Giochi (Hybrid View)"
 * @see MeepleCard for the new universal card component
 */

import React from 'react';

import { Users, Clock, Calendar, HelpCircle } from 'lucide-react';

import { MeepleCard, type MeepleCardVariant, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { Game } from '@/lib/api';

// ============================================================================
// Props Interface
// ============================================================================

/**
 * @deprecated Use MeepleCardProps instead
 */
export interface GameCardProps {
  /** Game data */
  game: Game;
  /** Layout variant */
  variant?: 'grid' | 'list';
  /** Click handler */
  onClick?: () => void;
  /** Show rating stars (default: true if game has rating) */
  showRating?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Utility Functions
// ============================================================================

/**
 * Format player count range
 */
function formatPlayerCount(min: number | null, max: number | null): string {
  if (min === null && max === null) return '';
  if (min === max) return `${min}`;
  return `${min || '?'}–${max || '?'}`;
}

/**
 * Format play time range
 */
function formatPlayTime(min: number | null, max: number | null): string {
  if (min === null && max === null) return '';
  const minTime = min || 0;
  const maxTime = max || 0;
  if (minTime === maxTime) return `${minTime}m`;
  return `${minTime}–${maxTime}m`;
}

// ============================================================================
// GameCard Component (Deprecated Wrapper)
// ============================================================================

/**
 * @deprecated Use MeepleCard directly:
 * ```tsx
 * import { MeepleCard } from '@/components/ui/data-display/meeple-card';
 *
 * <MeepleCard
 *   entity="game"
 *   variant="grid"
 *   title={game.title}
 *   subtitle={game.publisher}
 *   imageUrl={game.imageUrl}
 *   rating={game.averageRating}
 *   ratingMax={10}
 *   onClick={() => {}}
 * />
 * ```
 */
export const GameCard = React.memo(function GameCard({
  game,
  variant = 'grid',
  onClick,
  showRating = true,
  className,
}: GameCardProps) {
  // Build metadata array
  const metadata: MeepleCardMetadata[] = [];

  // Player count
  const playerCount = formatPlayerCount(game.minPlayers, game.maxPlayers);
  if (playerCount) {
    metadata.push({ icon: Users, value: playerCount });
  }

  // Play time
  const playTime = formatPlayTime(game.minPlayTimeMinutes, game.maxPlayTimeMinutes);
  if (playTime) {
    metadata.push({ icon: Clock, value: playTime });
  }

  // Year published
  if (game.yearPublished) {
    metadata.push({ icon: Calendar, value: String(game.yearPublished) });
  }

  // FAQ count (if available)
  // eslint-disable-next-line eqeqeq -- Intentional null/undefined check
  if (game.faqCount != null && game.faqCount > 0) {
    metadata.push({ icon: HelpCircle, value: String(game.faqCount) });
  }

  // Build subtitle
  const subtitleParts: string[] = [];
  if (game.publisher) {
    subtitleParts.push(game.publisher);
  }
  if (game.yearPublished) {
    subtitleParts.push(String(game.yearPublished));
  }
  const subtitle = subtitleParts.length > 0 ? subtitleParts.join(' · ') : undefined;

  // Build badge for catalog/BGG status
  let badge: string | undefined;
  if (game.sharedGameId) {
    badge = 'Catalogo';
  } else if (game.bggId) {
    badge = 'BGG';
  }

  // Map variant (GameCard grid/list → MeepleCard grid/list)
  const meepleVariant: MeepleCardVariant = variant === 'list' ? 'list' : 'grid';

  // Show rating if requested and available
  // eslint-disable-next-line eqeqeq -- Intentional null/undefined check
  const rating = showRating && game.averageRating != null ? game.averageRating : undefined;

  return (
    <MeepleCard
      entity="game"
      variant={meepleVariant}
      title={game.title}
      subtitle={subtitle}
      imageUrl={game.imageUrl ?? undefined}
      rating={rating}
      ratingMax={10}
      metadata={metadata}
      badge={badge}
      onClick={onClick}
      className={className}
      data-testid={`game-card-${game.id}`}
    />
  );
});
