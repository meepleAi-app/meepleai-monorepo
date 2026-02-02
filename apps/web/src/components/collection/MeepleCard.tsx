/**
 * MeepleCard Component - Issue #3476
 *
 * Reusable game card component for collection display
 *
 * Features:
 * - Game image with fallback
 * - Title, year, players, time metadata
 * - Status badges (PDF, Active Chat, Play Count)
 * - Hover effects and click actions
 * - Responsive design
 *
 * @example
 * ```tsx
 * <MeepleCard
 *   game={gameData}
 *   onPlay={(id) => console.log('Play:', id)}
 * />
 * ```
 */

'use client';

import { motion } from 'framer-motion';
import {
  FileText,
  MessageSquare,
  Users,
  Clock,
  TrendingUp,
  Star,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';
import type { MeepleCardProps } from '@/types/collection';

// ============================================================================
// Helper Functions
// ============================================================================

function formatPlayTime(minutes?: number): string {
  if (!minutes) return 'N/A';
  if (minutes < 60) return `${minutes}min`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return mins > 0 ? `${hours}h ${mins}min` : `${hours}h`;
}

function formatPlayers(min?: number, max?: number): string {
  if (!min && !max) return 'N/A';
  if (min === max) return `${min}`;
  return `${min || '?'}-${max || '?'}`;
}

function formatComplexity(complexity?: number): string {
  if (!complexity) return 'N/A';
  return complexity.toFixed(1);
}

// ============================================================================
// MeepleCard Component
// ============================================================================

export function MeepleCard({
  game,
  onPlay,
  onViewChat,
  onViewPdf,
  className,
}: MeepleCardProps) {
  const {
    id,
    title,
    thumbnailUrl,
    imageUrl,
    yearPublished,
    minPlayers,
    maxPlayers,
    playingTime,
    complexity,
    rating,
    playCount,
    hasPdf,
    hasActiveChat,
    chatCount,
    lastPlayedAt,
  } = game;

  const handlePlayClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onPlay?.(id);
  };

  const handleChatClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onViewChat?.(id);
  };

  const handlePdfClick = (e: React.MouseEvent) => {
    e.preventDefault();
    e.stopPropagation();
    onViewPdf?.(id);
  };

  const imageSource = thumbnailUrl || imageUrl || '/images/game-placeholder.png';
  const lastPlayedLabel = lastPlayedAt
    ? `Giocato ${new Date(lastPlayedAt).toLocaleDateString('it-IT', { day: 'numeric', month: 'short' })}`
    : 'Mai giocato';

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ duration: 0.2 }}
      className={cn(className)}
      data-testid={`meeple-card-${id}`}
    >
      <Link
        href={`/games/${id}`}
        className="group block h-full"
      >
        <div className="h-full rounded-xl border border-border/60 bg-card/80 backdrop-blur-xl overflow-hidden hover:border-border hover:shadow-lg transition-all duration-200">
          {/* Image Section */}
          <div className="relative aspect-[4/3] bg-muted/20 overflow-hidden">
            <Image
              src={imageSource}
              alt={title}
              fill
              className="object-cover group-hover:scale-105 transition-transform duration-300"
              sizes="(max-width: 768px) 100vw, (max-width: 1200px) 50vw, 33vw"
              priority={false}
            />

            {/* Status Badges - Top Right */}
            <div className="absolute top-2 right-2 flex flex-col gap-1">
              {hasPdf && (
                <button
                  onClick={handlePdfClick}
                  className="h-8 w-8 rounded-lg bg-blue-500/90 hover:bg-blue-600 text-white flex items-center justify-center backdrop-blur-sm transition-colors"
                  title="Visualizza PDF"
                  data-testid={`pdf-badge-${id}`}
                >
                  <FileText className="h-4 w-4" />
                </button>
              )}
              {hasActiveChat && (
                <button
                  onClick={handleChatClick}
                  className="h-8 w-8 rounded-lg bg-emerald-500/90 hover:bg-emerald-600 text-white flex items-center justify-center backdrop-blur-sm transition-colors"
                  title={`${chatCount} chat attive`}
                  data-testid={`chat-badge-${id}`}
                >
                  <MessageSquare className="h-4 w-4" />
                </button>
              )}
            </div>

            {/* Rating Badge - Top Left */}
            {rating && (
              <div className="absolute top-2 left-2 h-8 px-2 rounded-lg bg-amber-500/90 text-white flex items-center gap-1 backdrop-blur-sm">
                <Star className="h-3 w-3 fill-current" />
                <span className="text-xs font-semibold">{rating.toFixed(1)}</span>
              </div>
            )}

            {/* Play Count Badge - Bottom Left */}
            {playCount > 0 && (
              <div className="absolute bottom-2 left-2 h-7 px-2 rounded-lg bg-black/60 text-white flex items-center gap-1 backdrop-blur-sm">
                <TrendingUp className="h-3 w-3" />
                <span className="text-xs font-medium">{playCount} partite</span>
              </div>
            )}
          </div>

          {/* Content Section */}
          <div className="p-3 space-y-2">
            {/* Title & Year */}
            <div>
              <h3
                className="font-semibold text-sm line-clamp-2 group-hover:text-primary transition-colors"
                title={title}
                data-testid={`game-title-${id}`}
              >
                {title}
              </h3>
              {yearPublished && (
                <p className="text-xs text-muted-foreground">
                  {yearPublished}
                </p>
              )}
            </div>

            {/* Metadata Grid */}
            <div className="grid grid-cols-2 gap-2 text-xs text-muted-foreground">
              <div className="flex items-center gap-1" title="Giocatori">
                <Users className="h-3 w-3" />
                <span>{formatPlayers(minPlayers, maxPlayers)}</span>
              </div>
              <div className="flex items-center gap-1" title="Durata">
                <Clock className="h-3 w-3" />
                <span>{formatPlayTime(playingTime)}</span>
              </div>
            </div>

            {/* Last Played & Complexity */}
            <div className="flex items-center justify-between text-xs">
              <span className="text-muted-foreground/70">{lastPlayedLabel}</span>
              {complexity && (
                <span className="text-muted-foreground" title="Complessità">
                  ⚙️ {formatComplexity(complexity)}
                </span>
              )}
            </div>

            {/* Actions */}
            <div className="pt-2">
              <Button
                size="sm"
                variant="outline"
                className="w-full h-8"
                onClick={handlePlayClick}
                data-testid={`play-button-${id}`}
              >
                <TrendingUp className="h-3 w-3 mr-1" />
                Segna Partita
              </Button>
            </div>
          </div>
        </div>
      </Link>
    </motion.div>
  );
}

export default MeepleCard;
