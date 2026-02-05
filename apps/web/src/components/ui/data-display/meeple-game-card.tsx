/**
 * MeepleGameCard - Frosted Glass Game Card Component
 *
 * Modern glass morphism card with 3D flip animation for board games.
 * Based on B1 Frosted Glass design from meeple-card-glass-variations.
 *
 * Features:
 * - Frosted glass effect with soft blur
 * - 3D flip animation (Framer Motion)
 * - Front: Cover image, title, rating, stats
 * - Back: Description, categories, mechanics, designers
 * - MeepleAI design tokens (orange accent, Quicksand/Nunito)
 * - Accessible (keyboard nav, ARIA labels)
 *
 * @module components/ui/data-display/meeple-game-card
 */

'use client';

import { useState, useCallback, useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Clock,
  Users,
  Gauge,
  Tag,
  Cog,
  User,
  RotateCcw,
  Star,
  Heart,
  Plus,
} from 'lucide-react';
import Image from 'next/image';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface MeepleGameCardProps {
  /** Game ID */
  id: string;
  /** Game title */
  title: string;
  /** Publisher name */
  publisher?: string;
  /** Year published */
  year?: number;
  /** Cover image URL */
  imageUrl?: string;
  /** BGG average rating (0-10) */
  rating?: number;
  /** Minimum players */
  minPlayers?: number;
  /** Maximum players */
  maxPlayers?: number;
  /** Playing time in minutes */
  playingTimeMinutes?: number;
  /** Complexity rating (0-5) */
  complexity?: number;
  /** Game description */
  description?: string;
  /** Categories list */
  categories?: Array<{ id: string; name: string }>;
  /** Mechanics list */
  mechanics?: Array<{ id: string; name: string }>;
  /** Designers list */
  designers?: Array<{ id: string; name: string }>;
  /** Badge text (e.g., "New", "Top Rated") */
  badge?: string;
  /** Whether user owns this game */
  isOwned?: boolean;
  /** Whether game is in wishlist */
  isWishlist?: boolean;
  /** Whether game is favorited */
  isFavorite?: boolean;
  /** Called when card is clicked */
  onCardClick?: () => void;
  /** Called when "Add to Library" is clicked */
  onAddToLibrary?: () => void;
  /** Called when "Details" is clicked */
  onViewDetails?: () => void;
  /** Additional class names */
  className?: string;
}

// ============================================================================
// Component
// ============================================================================

export function MeepleGameCard({
  id: _id,
  title,
  publisher,
  year,
  imageUrl,
  rating,
  minPlayers,
  maxPlayers,
  playingTimeMinutes,
  complexity,
  description,
  categories = [],
  mechanics = [],
  designers = [],
  badge,
  isOwned,
  isWishlist,
  isFavorite,
  onCardClick: _onCardClick,
  onAddToLibrary,
  onViewDetails,
  className,
}: MeepleGameCardProps) {
  const [isFlipped, setIsFlipped] = useState(false);

  const handleFlip = useCallback(() => {
    setIsFlipped((prev) => !prev);
  }, []);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleFlip();
      }
    },
    [handleFlip]
  );

  // Format stats
  const formattedStats = useMemo(
    () => ({
      playerCount:
        minPlayers && maxPlayers
          ? minPlayers === maxPlayers
            ? `${minPlayers}`
            : `${minPlayers}-${maxPlayers}`
          : null,
      playtime: playingTimeMinutes ? `${playingTimeMinutes}m` : null,
      complexityDisplay: complexity ? `${complexity.toFixed(1)}/5` : null,
      ratingDisplay: rating ? rating.toFixed(1) : null,
    }),
    [minPlayers, maxPlayers, playingTimeMinutes, complexity, rating]
  );

  // Generate star rating
  const stars = useMemo(() => {
    if (!rating) return [];
    const normalizedRating = Math.round((rating / 10) * 5);
    return Array.from({ length: 5 }, (_, i) => i < normalizedRating);
  }, [rating]);

  return (
    <div
      className={cn('w-full max-w-[340px] cursor-pointer', className)}
      style={{ perspective: '2000px' }}
      role="button"
      tabIndex={0}
      aria-label={`${title}. Premi Invio o clicca per ${isFlipped ? 'vedere il fronte' : 'vedere i dettagli'}.`}
      onClick={handleFlip}
      onKeyDown={handleKeyDown}
    >
      <motion.div
        className="relative w-full"
        style={{
          aspectRatio: '3 / 4',
          transformStyle: 'preserve-3d',
        }}
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.6, ease: [0.4, 0, 0.2, 1] }}
      >
        {/* ================================================================
            FRONT FACE - Frosted Glass
            ================================================================ */}
        <div
          className={cn(
            'absolute inset-0 overflow-hidden rounded-[28px]',
            'flex flex-col',
            // Frosted glass effect
            'bg-white/[0.08] backdrop-blur-[40px]',
            // Soft border
            'border border-white/15',
            // Shadows
            'shadow-[0_8px_32px_rgba(0,0,0,0.2),inset_0_0_0_1px_rgba(255,255,255,0.05)]',
            // Hover state
            'transition-all duration-500 ease-out',
            'hover:translate-y-[-12px] hover:scale-[1.02]',
            'hover:shadow-[0_24px_64px_rgba(0,0,0,0.3),inset_0_0_0_1px_rgba(255,255,255,0.1),0_0_60px_rgba(198,93,33,0.15)]'
          )}
          style={{
            backfaceVisibility: 'hidden',
          }}
        >
          {/* Soft top gradient accent */}
          <div className="pointer-events-none absolute inset-x-0 top-0 z-10 h-[100px] bg-gradient-to-b from-white/[0.12] to-transparent" />

          {/* Entity Badge */}
          {badge && (
            <span className="absolute left-4 top-4 z-20 rounded-2xl border border-white/25 bg-white/20 px-3.5 py-2 font-quicksand text-[0.6rem] font-bold uppercase tracking-[0.12em] text-white shadow-[0_4px_12px_rgba(0,0,0,0.15)] backdrop-blur-[10px]">
              {badge}
            </span>
          )}

          {/* Status indicators */}
          <div className="absolute right-4 top-4 z-20 flex gap-2">
            {isOwned && (
              <span className="rounded-lg border border-emerald-400/30 bg-emerald-500/20 px-2 py-1 font-quicksand text-[0.55rem] font-bold uppercase tracking-wider text-emerald-300 backdrop-blur-sm">
                Owned
              </span>
            )}
            {isWishlist && (
              <span className="rounded-lg border border-purple-400/30 bg-purple-500/20 px-2 py-1 font-quicksand text-[0.55rem] font-bold uppercase tracking-wider text-purple-300 backdrop-blur-sm">
                Wishlist
              </span>
            )}
            {isFavorite && (
              <Heart className="h-5 w-5 fill-rose-400 text-rose-400 drop-shadow-[0_0_8px_rgba(251,113,133,0.6)]" />
            )}
          </div>

          {/* Cover Image */}
          <div className="relative aspect-[16/10] overflow-hidden">
            {imageUrl ? (
              <Image
                src={imageUrl}
                alt={title}
                fill
                className="object-cover brightness-95 transition-transform duration-700 ease-out group-hover:scale-[1.08]"
                sizes="(max-width: 640px) 100vw, 340px"
              />
            ) : (
              <div className="flex h-full items-center justify-center bg-gradient-to-br from-slate-700 to-slate-900">
                <span className="text-6xl opacity-30">🎲</span>
              </div>
            )}
            {/* Vignette overlay */}
            <div className="pointer-events-none absolute inset-0 bg-[radial-gradient(ellipse_at_center,transparent_30%,rgba(0,0,0,0.3)_100%),linear-gradient(180deg,transparent_50%,rgba(15,23,42,0.8)_100%)]" />

            {/* Flip indicator */}
            <div className="absolute bottom-3 right-3 rounded-full bg-white/20 p-2 backdrop-blur-sm">
              <RotateCcw className="h-4 w-4 text-white/80" />
            </div>
          </div>

          {/* Content */}
          <div className="flex flex-1 flex-col p-6">
            {/* Title */}
            <h3 className="mb-1 line-clamp-2 font-quicksand text-xl font-bold tracking-tight text-white">
              {title}
            </h3>

            {/* Subtitle */}
            <p className="mb-4 font-nunito text-sm text-white/60">
              {publisher}
              {year && ` · ${year}`}
            </p>

            {/* Rating */}
            {rating && (
              <div className="mb-4 flex items-center gap-1">
                {stars.map((filled, i) => (
                  <Star
                    key={i}
                    className={cn(
                      'h-4 w-4',
                      filled
                        ? 'fill-[#E07A3A] text-[#E07A3A] drop-shadow-[0_0_4px_rgba(224,122,58,0.5)]'
                        : 'fill-transparent text-white/15'
                    )}
                  />
                ))}
                <span className="ml-2 rounded-lg bg-white/10 px-2.5 py-1 font-quicksand text-sm font-bold text-white">
                  {formattedStats.ratingDisplay}
                </span>
              </div>
            )}

            {/* Metadata chips */}
            <div className="mb-5 flex flex-wrap gap-2">
              {formattedStats.playerCount && (
                <span className="flex items-center gap-1.5 rounded-[10px] border border-white/10 bg-white/[0.08] px-3 py-2 font-quicksand text-[0.7rem] font-semibold text-white/85 transition-all hover:translate-y-[-2px] hover:bg-white/15">
                  <Users className="h-3.5 w-3.5" />
                  {formattedStats.playerCount}
                </span>
              )}
              {formattedStats.playtime && (
                <span className="flex items-center gap-1.5 rounded-[10px] border border-white/10 bg-white/[0.08] px-3 py-2 font-quicksand text-[0.7rem] font-semibold text-white/85 transition-all hover:translate-y-[-2px] hover:bg-white/15">
                  <Clock className="h-3.5 w-3.5" />
                  {formattedStats.playtime}
                </span>
              )}
              {formattedStats.complexityDisplay && (
                <span className="flex items-center gap-1.5 rounded-[10px] border border-white/10 bg-white/[0.08] px-3 py-2 font-quicksand text-[0.7rem] font-semibold text-white/85 transition-all hover:translate-y-[-2px] hover:bg-white/15">
                  <Gauge className="h-3.5 w-3.5" />
                  {formattedStats.complexityDisplay}
                </span>
              )}
            </div>

            {/* Action Buttons */}
            <div className="mt-auto flex gap-3 border-t border-white/[0.08] pt-4">
              <Button
                variant="default"
                size="sm"
                className="flex-1 rounded-[14px] bg-gradient-to-br from-[hsl(25,95%,45%)] to-[hsl(25,85%,40%)] font-quicksand text-sm font-bold text-white shadow-[0_4px_20px_rgba(198,93,33,0.4)] transition-all hover:translate-y-[-3px] hover:shadow-[0_8px_30px_rgba(198,93,33,0.5)]"
                onClick={(e) => {
                  e.stopPropagation();
                  onAddToLibrary?.();
                }}
              >
                <Plus className="mr-1.5 h-4 w-4" />
                Add
              </Button>
              <Button
                variant="ghost"
                size="sm"
                className="flex-1 rounded-[14px] border border-white/15 bg-white/10 font-quicksand text-sm font-bold text-white/90 transition-all hover:translate-y-[-2px] hover:bg-white/[0.18]"
                onClick={(e) => {
                  e.stopPropagation();
                  onViewDetails?.();
                }}
              >
                Details
              </Button>
            </div>
          </div>
        </div>

        {/* ================================================================
            BACK FACE - Info Panel
            ================================================================ */}
        <div
          className={cn(
            'absolute inset-0 overflow-hidden rounded-[28px]',
            'flex flex-col',
            // Frosted glass effect
            'bg-white/[0.08] backdrop-blur-[40px]',
            // Soft border with purple accent
            'border border-purple-400/30',
            // Shadows
            'shadow-[0_8px_32px_rgba(0,0,0,0.2),inset_0_0_0_1px_rgba(255,255,255,0.05)]'
          )}
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
          }}
        >
          {/* Purple accent line at top */}
          <div className="h-1 w-full bg-gradient-to-r from-purple-500 to-purple-400" />

          {/* Content */}
          <div className="flex flex-1 flex-col overflow-y-auto p-6">
            {/* Header */}
            <div className="mb-4 flex items-center justify-between">
              <h3 className="font-quicksand text-lg font-bold text-white">
                {title}
              </h3>
              <button
                className="flex items-center gap-1 rounded-lg px-2 py-1 font-quicksand text-xs font-semibold text-white/70 transition-colors hover:bg-white/10 hover:text-purple-300"
                onClick={(e) => {
                  e.stopPropagation();
                  handleFlip();
                }}
              >
                <RotateCcw className="h-3.5 w-3.5" />
                Flip
              </button>
            </div>

            {/* Description */}
            {description && (
              <div className="mb-4">
                <h4 className="mb-2 flex items-center gap-2 font-quicksand text-xs font-semibold uppercase tracking-wider text-white/50">
                  Description
                </h4>
                <p className="line-clamp-4 font-nunito text-sm leading-relaxed text-white/70">
                  {description}
                </p>
              </div>
            )}

            {/* Categories */}
            {categories.length > 0 && (
              <div className="mb-4">
                <h4 className="mb-2 flex items-center gap-2 font-quicksand text-xs font-semibold uppercase tracking-wider text-white/50">
                  <Tag className="h-3.5 w-3.5" />
                  Categories
                </h4>
                <div className="flex flex-wrap gap-1.5">
                  {categories.slice(0, 4).map((cat) => (
                    <span
                      key={cat.id}
                      className="rounded-lg bg-purple-500/20 px-2.5 py-1 font-nunito text-xs font-medium text-purple-200"
                    >
                      {cat.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Mechanics */}
            {mechanics.length > 0 && (
              <div className="mb-4">
                <h4 className="mb-2 flex items-center gap-2 font-quicksand text-xs font-semibold uppercase tracking-wider text-white/50">
                  <Cog className="h-3.5 w-3.5" />
                  Mechanics
                </h4>
                <div className="flex flex-wrap gap-1.5">
                  {mechanics.slice(0, 4).map((mech) => (
                    <span
                      key={mech.id}
                      className="rounded-lg bg-[hsl(25,95%,45%)]/20 px-2.5 py-1 font-nunito text-xs font-medium text-orange-200"
                    >
                      {mech.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Designers */}
            {designers.length > 0 && (
              <div className="mb-4">
                <h4 className="mb-2 flex items-center gap-2 font-quicksand text-xs font-semibold uppercase tracking-wider text-white/50">
                  <User className="h-3.5 w-3.5" />
                  Designers
                </h4>
                <p className="font-nunito text-sm text-white/70">
                  {designers.map((d) => d.name).join(', ')}
                </p>
              </div>
            )}

            {/* Stats summary */}
            <div className="mt-auto grid grid-cols-3 gap-2 border-t border-white/10 pt-4">
              {formattedStats.playerCount && (
                <div className="rounded-lg bg-white/5 p-2 text-center">
                  <Users className="mx-auto mb-1 h-4 w-4 text-white/50" />
                  <p className="font-quicksand text-sm font-bold text-white">
                    {formattedStats.playerCount}
                  </p>
                  <p className="font-nunito text-[0.6rem] text-white/40">
                    Players
                  </p>
                </div>
              )}
              {formattedStats.playtime && (
                <div className="rounded-lg bg-white/5 p-2 text-center">
                  <Clock className="mx-auto mb-1 h-4 w-4 text-white/50" />
                  <p className="font-quicksand text-sm font-bold text-white">
                    {formattedStats.playtime}
                  </p>
                  <p className="font-nunito text-[0.6rem] text-white/40">
                    Time
                  </p>
                </div>
              )}
              {formattedStats.ratingDisplay && (
                <div className="rounded-lg bg-white/5 p-2 text-center">
                  <Star className="mx-auto mb-1 h-4 w-4 text-[#E07A3A]" />
                  <p className="font-quicksand text-sm font-bold text-white">
                    {formattedStats.ratingDisplay}
                  </p>
                  <p className="font-nunito text-[0.6rem] text-white/40">
                    Rating
                  </p>
                </div>
              )}
            </div>
          </div>
        </div>
      </motion.div>

      {/* Flip hint */}
      <p className="mt-3 text-center font-nunito text-xs text-white/40">
        Click to flip
      </p>
    </div>
  );
}

// ============================================================================
// Skeleton
// ============================================================================

export function MeepleGameCardSkeleton({
  className,
}: {
  className?: string;
}) {
  return (
    <div
      className={cn(
        'w-full max-w-[340px] animate-pulse rounded-[28px]',
        'bg-white/[0.05] backdrop-blur-[20px]',
        'border border-white/10',
        className
      )}
      style={{ aspectRatio: '3 / 4' }}
    >
      {/* Image skeleton */}
      <div className="aspect-[16/10] rounded-t-[28px] bg-white/[0.08]" />

      {/* Content skeleton */}
      <div className="space-y-4 p-6">
        <div className="h-6 w-3/4 rounded bg-white/[0.1]" />
        <div className="h-4 w-1/2 rounded bg-white/[0.08]" />
        <div className="flex gap-2">
          <div className="h-5 w-16 rounded bg-white/[0.08]" />
          <div className="h-5 w-12 rounded bg-white/[0.08]" />
          <div className="h-5 w-14 rounded bg-white/[0.08]" />
        </div>
        <div className="flex gap-2">
          <div className="h-8 w-24 rounded bg-white/[0.08]" />
          <div className="h-8 w-16 rounded bg-white/[0.08]" />
          <div className="h-8 w-16 rounded bg-white/[0.08]" />
        </div>
        <div className="flex gap-3 pt-4">
          <div className="h-10 flex-1 rounded-[14px] bg-white/[0.1]" />
          <div className="h-10 flex-1 rounded-[14px] bg-white/[0.08]" />
        </div>
      </div>
    </div>
  );
}

export default MeepleGameCard;
