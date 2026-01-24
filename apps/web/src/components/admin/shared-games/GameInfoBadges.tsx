/**
 * Game Info Badges Components - Issue #2372
 *
 * Reusable badge components for displaying game metadata:
 * - PlayersBadge: min-max players count
 * - PlayTimeBadge: playing time in minutes
 * - ComplexityBadge: complexity rating (1-5)
 * - RatingBadge: average rating (0-10)
 * - AgeBadge: minimum age
 */

import { Clock, Star, Users, Brain, UserCheck } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';

// ========== Players Badge ==========

export interface PlayersBadgeProps {
  min: number;
  max: number;
  size?: 'sm' | 'default';
}

/**
 * Badge displaying player count range
 */
export function PlayersBadge({ min, max, size = 'default' }: PlayersBadgeProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5' : 'h-3 w-3';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';

  return (
    <Badge variant="secondary" className={`gap-1 ${badgeClass}`}>
      <Users className={iconClass} />
      {min === max ? min : `${min}-${max}`}
    </Badge>
  );
}

// ========== Play Time Badge ==========

export interface PlayTimeBadgeProps {
  minutes: number;
  size?: 'sm' | 'default';
}

/**
 * Badge displaying playing time in minutes
 */
export function PlayTimeBadge({ minutes, size = 'default' }: PlayTimeBadgeProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5' : 'h-3 w-3';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';

  // Format time display
  let timeDisplay: string;
  if (minutes < 60) {
    timeDisplay = `${minutes} min`;
  } else {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    timeDisplay = mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  }

  return (
    <Badge variant="secondary" className={`gap-1 ${badgeClass}`}>
      <Clock className={iconClass} />
      {timeDisplay}
    </Badge>
  );
}

// ========== Complexity Badge ==========

export interface ComplexityBadgeProps {
  rating: number | null;
  size?: 'sm' | 'default';
}

/**
 * Badge displaying complexity rating (1-5 scale, typically from BGG)
 */
export function ComplexityBadge({ rating, size = 'default' }: ComplexityBadgeProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5' : 'h-3 w-3';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';

  if (rating === null || rating === undefined) {
    return (
      <Badge variant="outline" className={`gap-1 text-muted-foreground ${badgeClass}`}>
        <Brain className={iconClass} />
        N/D
      </Badge>
    );
  }

  // Color based on complexity level
  let colorClass = '';
  if (rating < 2) {
    colorClass = 'bg-green-50 text-green-700 border-green-200';
  } else if (rating < 3) {
    colorClass = 'bg-blue-50 text-blue-700 border-blue-200';
  } else if (rating < 4) {
    colorClass = 'bg-yellow-50 text-yellow-700 border-yellow-200';
  } else {
    colorClass = 'bg-red-50 text-red-700 border-red-200';
  }

  return (
    <Badge variant="outline" className={`gap-1 ${colorClass} ${badgeClass}`}>
      <Brain className={iconClass} />
      {rating.toFixed(1)}
    </Badge>
  );
}

// ========== Rating Badge ==========

export interface RatingBadgeProps {
  rating: number | null;
  size?: 'sm' | 'default';
}

/**
 * Badge displaying average rating (0-10 scale, typically from BGG)
 */
export function RatingBadge({ rating, size = 'default' }: RatingBadgeProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5' : 'h-3 w-3';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';

  if (rating === null || rating === undefined) {
    return (
      <Badge variant="outline" className={`gap-1 text-muted-foreground ${badgeClass}`}>
        <Star className={iconClass} />
        N/D
      </Badge>
    );
  }

  // Color based on rating level
  let colorClass = '';
  if (rating >= 8) {
    colorClass = 'bg-green-50 text-green-700 border-green-200';
  } else if (rating >= 7) {
    colorClass = 'bg-blue-50 text-blue-700 border-blue-200';
  } else if (rating >= 6) {
    colorClass = 'bg-yellow-50 text-yellow-700 border-yellow-200';
  } else {
    colorClass = 'bg-muted text-muted-foreground border-border/50 dark:border-border/70';
  }

  return (
    <Badge variant="outline" className={`gap-1 ${colorClass} ${badgeClass}`}>
      <Star className={iconClass} />
      {rating.toFixed(1)}
    </Badge>
  );
}

// ========== Age Badge ==========

export interface AgeBadgeProps {
  minAge: number;
  size?: 'sm' | 'default';
}

/**
 * Badge displaying minimum age requirement
 */
export function AgeBadge({ minAge, size = 'default' }: AgeBadgeProps) {
  const iconClass = size === 'sm' ? 'h-2.5 w-2.5' : 'h-3 w-3';
  const badgeClass = size === 'sm' ? 'text-xs px-1.5 py-0' : '';

  return (
    <Badge variant="secondary" className={`gap-1 ${badgeClass}`}>
      <UserCheck className={iconClass} />
      {minAge}+
    </Badge>
  );
}
