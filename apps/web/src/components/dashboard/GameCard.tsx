/**
 * GameCard - Game Display Component
 * Issue #3286 - User Dashboard Layout System
 *
 * Features:
 * - Grid view: Compact card with image, name, rating, play count
 * - List view: Detailed row with all info + quick AI button
 * - Rating badge with color coding (red < 5, yellow < 7, green >= 7)
 * - Favorite heart indicator
 * - Ownership status badge
 * - Hover animations with lift effect
 * - Quick AI assistant button
 *
 * @example
 * ```tsx
 * <GameCard
 *   data={gameData}
 *   viewMode="grid"
 *   onAskAI={() => openAIChat(gameData.id)}
 *   onClick={() => navigateTo(`/games/${gameData.id}`)}
 * />
 * ```
 */

'use client';

import { memo } from 'react';

import { motion } from 'framer-motion';
import { Heart, Bot, MapPin, Calendar, Gamepad2, Share2 } from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import type { ViewMode } from './DashboardSection';

// ============================================================================
// Types
// ============================================================================

export interface GameData {
  id: string;
  name: string;
  imageUrl?: string;
  rating?: number;
  playCount?: number;
  lastPlayedAt?: Date;
  isFavorite?: boolean;
  ownershipStatus?: 'OWNED' | 'LENT_OUT';
  location?: string;
}

export interface GameCardProps {
  data: GameData;
  viewMode: ViewMode;
  onAskAI?: () => void;
  onClick?: () => void;
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

function getRatingColor(rating: number): string {
  if (rating < 5) return 'bg-destructive text-destructive-foreground';
  if (rating < 7) return 'bg-yellow-500 text-white';
  return 'bg-green-500 text-white';
}

function formatLastPlayed(date?: Date): string {
  if (!date) return 'Mai giocato';

  const now = new Date();
  const diff = now.getTime() - date.getTime();
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (days === 0) return 'Oggi';
  if (days === 1) return 'Ieri';
  if (days < 7) return `${days} giorni fa`;
  if (days < 30) return `${Math.floor(days / 7)} settimane fa`;
  if (days < 365) return `${Math.floor(days / 30)} mesi fa`;
  return `${Math.floor(days / 365)} anni fa`;
}

// ============================================================================
// GameCard Component
// ============================================================================

export const GameCard = memo(function GameCard({
  data,
  viewMode,
  onAskAI,
  onClick,
  className,
}: GameCardProps) {
  const { name, imageUrl, rating, playCount, lastPlayedAt, isFavorite, ownershipStatus, location } = data;

  // Grid View (Compact)
  if (viewMode === 'grid') {
    return (
      <motion.article
        whileHover={{ y: -4, boxShadow: '0 8px 25px rgba(139, 90, 60, 0.12)' }}
        transition={{ duration: 0.2 }}
        onClick={onClick}
        className={cn(
          'group relative cursor-pointer overflow-hidden rounded-xl border border-border/60 bg-card',
          'transition-all duration-200',
          className
        )}
        role="button"
        tabIndex={0}
        aria-label={`${name}${rating ? `, valutazione ${rating}` : ''}`}
      >
        {/* Image Container */}
        <div className="relative aspect-[4/3] overflow-hidden bg-muted">
          {imageUrl ? (
            /* eslint-disable-next-line @next/next/no-img-element -- External game image URL */
            <img
              src={imageUrl}
              alt=""
              className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-primary/20 to-primary/5">
              <Gamepad2 className="h-12 w-12 text-primary/40" />
            </div>
          )}

          {/* Rating Badge */}
          {rating !== undefined && (
            <div
              className={cn(
                'absolute right-2 top-2 flex h-7 min-w-7 items-center justify-center rounded-lg px-1.5',
                'text-xs font-bold shadow-md',
                getRatingColor(rating)
              )}
            >
              {rating}
            </div>
          )}

          {/* Favorite Heart */}
          {isFavorite && (
            <div className="absolute left-2 top-2">
              <Heart className="h-5 w-5 fill-red-500 text-red-500 drop-shadow-md" />
            </div>
          )}

          {/* Ownership Badge */}
          {ownershipStatus === 'LENT_OUT' && (
            <div className="absolute bottom-2 left-2 flex items-center gap-1 rounded-md bg-background/90 px-2 py-0.5 text-xs font-medium text-muted-foreground backdrop-blur-sm">
              <Share2 className="h-3 w-3" />
              Prestato
            </div>
          )}

          {/* AI Button (appears on hover) */}
          {onAskAI && (
            <motion.div
              initial={{ opacity: 0, scale: 0.8 }}
              whileHover={{ scale: 1.1 }}
              className="absolute bottom-2 right-2 opacity-0 transition-opacity group-hover:opacity-100"
            >
              <Button
                variant="secondary"
                size="icon"
                className="h-8 w-8 rounded-full bg-primary text-primary-foreground shadow-lg hover:bg-primary/90"
                onClick={(e: React.MouseEvent) => {
                  e.stopPropagation();
                  onAskAI();
                }}
                aria-label={`Chiedi all'AI informazioni su ${name}`}
              >
                <Bot className="h-4 w-4" />
              </Button>
            </motion.div>
          )}
        </div>

        {/* Card Content */}
        <div className="p-3">
          <h3 className="truncate font-quicksand text-sm font-semibold">{name}</h3>
          <div className="mt-1.5 flex items-center gap-3 text-xs text-muted-foreground">
            <span className="flex items-center gap-1">
              <Gamepad2 className="h-3 w-3" />
              {playCount ?? 0}
            </span>
            {location && (
              <span className="flex items-center gap-1 truncate">
                <MapPin className="h-3 w-3 shrink-0" />
                <span className="truncate">{location}</span>
              </span>
            )}
          </div>
        </div>
      </motion.article>
    );
  }

  // List View (Detailed)
  return (
    <motion.article
      whileHover={{ x: 4, backgroundColor: 'hsl(var(--muted) / 0.5)' }}
      transition={{ duration: 0.15 }}
      onClick={onClick}
      className={cn(
        'group flex cursor-pointer items-center gap-4 rounded-xl border border-border/40 bg-card p-3',
        'transition-all duration-200',
        className
      )}
      role="button"
      tabIndex={0}
      aria-label={`${name}${rating ? `, valutazione ${rating}` : ''}`}
    >
      {/* Thumbnail */}
      <div className="relative h-16 w-16 shrink-0 overflow-hidden rounded-lg bg-muted">
        {imageUrl ? (
          /* eslint-disable-next-line @next/next/no-img-element -- External game image URL */
          <img src={imageUrl} alt="" className="h-full w-full object-cover" />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-primary/20 to-primary/5">
            <Gamepad2 className="h-6 w-6 text-primary/40" />
          </div>
        )}
        {isFavorite && (
          <Heart className="absolute right-1 top-1 h-4 w-4 fill-red-500 text-red-500 drop-shadow" />
        )}
      </div>

      {/* Main Info */}
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <h3 className="truncate font-quicksand font-semibold">{name}</h3>
          {rating !== undefined && (
            <span
              className={cn(
                'shrink-0 rounded-md px-1.5 py-0.5 text-xs font-bold',
                getRatingColor(rating)
              )}
            >
              {rating}
            </span>
          )}
          {ownershipStatus === 'LENT_OUT' && (
            <span className="shrink-0 flex items-center gap-1 rounded-md bg-muted px-1.5 py-0.5 text-xs text-muted-foreground">
              <Share2 className="h-3 w-3" />
              Prestato
            </span>
          )}
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
          <span className="flex items-center gap-1">
            <Gamepad2 className="h-3.5 w-3.5" />
            {playCount ?? 0} partite
          </span>
          <span className="flex items-center gap-1">
            <Calendar className="h-3.5 w-3.5" />
            {formatLastPlayed(lastPlayedAt)}
          </span>
          {location && (
            <span className="flex items-center gap-1">
              <MapPin className="h-3.5 w-3.5" />
              {location}
            </span>
          )}
        </div>
      </div>

      {/* AI Button (always visible in list mode) */}
      {onAskAI && (
        <Button
          variant="ghost"
          size="icon"
          className="h-9 w-9 shrink-0 text-muted-foreground hover:bg-primary/10 hover:text-primary"
          onClick={(e: React.MouseEvent) => {
            e.stopPropagation();
            onAskAI();
          }}
          aria-label={`Chiedi all'AI informazioni su ${name}`}
        >
          <Bot className="h-5 w-5" />
        </Button>
      )}
    </motion.article>
  );
});

export default GameCard;
