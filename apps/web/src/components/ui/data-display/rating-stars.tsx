/* eslint-disable security/detect-object-injection -- Safe size config Record access */
import React from 'react';

import { Star } from 'lucide-react';

import { cn } from '@/lib/utils';

export interface RatingStarsProps {
  /**
   * Rating value (0-10 from BGG, or 0-5 for display)
   * Automatically converts BGG ratings (0-10) to 5-star scale
   */
  rating: number;
  /**
   * Maximum rating value (default: 10 for BGG)
   */
  maxRating?: number;
  /**
   * Size of the stars
   */
  size?: 'sm' | 'md' | 'lg';
  /**
   * Show half stars for decimal ratings
   */
  showHalfStars?: boolean;
  /**
   * Show rating number next to stars
   */
  showValue?: boolean;
  /**
   * Additional CSS classes
   */
  className?: string;
  /**
   * Color variant
   */
  variant?: 'default' | 'muted';
}

const sizeClasses = {
  sm: 'h-3 w-3',
  md: 'h-4 w-4',
  lg: 'h-5 w-5',
} as const;

const textSizeClasses = {
  sm: 'text-xs',
  md: 'text-sm',
  lg: 'text-base',
} as const;

/**
 * RatingStars - Reusable star rating display component
 *
 * Supports BGG ratings (0-10) automatically converted to 5-star scale
 * Features: half-stars, customizable size, optional value display
 *
 * @example
 * // BGG rating (0-10) with value display
 * <RatingStars rating={7.8} maxRating={10} showValue />
 *
 * // 5-star rating without value
 * <RatingStars rating={4.5} maxRating={5} showHalfStars />
 */
export const RatingStars = React.memo(function RatingStars({
  rating,
  maxRating = 10,
  size = 'md',
  showHalfStars = true,
  showValue = false,
  className,
  variant = 'default',
}: RatingStarsProps) {
  // Convert rating to 5-star scale (BGG 0-10 → 0-5)
  const normalizedRating = (rating / maxRating) * 5;

  // Calculate full stars, half stars, and empty stars
  const fullStars = Math.floor(normalizedRating);
  const hasHalfStar = showHalfStars && normalizedRating % 1 >= 0.5;
  const emptyStars = 5 - fullStars - (hasHalfStar ? 1 : 0);

  const starColor = variant === 'default' ? 'text-yellow-500' : 'text-muted-foreground';
  const emptyColor =
    variant === 'default' ? 'text-muted-foreground/30' : 'text-muted-foreground/20';

  return (
    <div
      className={cn('flex items-center gap-1', className)}
      role="img"
      aria-label={`Rating: ${rating.toFixed(1)} out of ${maxRating}`}
    >
      <div className="flex items-center gap-0.5">
        {/* Full stars */}
        {Array.from({ length: fullStars }).map((_, i) => (
          <Star
            key={`full-${i}`}
            className={cn(sizeClasses[size], starColor)}
            fill="currentColor"
            aria-hidden="true"
          />
        ))}

        {/* Half star */}
        {hasHalfStar && (
          <div className="relative" aria-hidden="true">
            <Star className={cn(sizeClasses[size], emptyColor)} />
            <div className="absolute inset-0 overflow-hidden" style={{ width: '50%' }}>
              <Star className={cn(sizeClasses[size], starColor)} fill="currentColor" />
            </div>
          </div>
        )}

        {/* Empty stars */}
        {Array.from({ length: emptyStars }).map((_, i) => (
          <Star
            key={`empty-${i}`}
            className={cn(sizeClasses[size], emptyColor)}
            aria-hidden="true"
          />
        ))}
      </div>

      {/* Optional rating value */}
      {showValue && (
        <span className={cn(textSizeClasses[size], 'text-muted-foreground font-medium')}>
          {rating.toFixed(1)}
        </span>
      )}
    </div>
  );
});
