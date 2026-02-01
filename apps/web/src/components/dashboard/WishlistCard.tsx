/**
 * WishlistCard - Wishlist Item Display
 * Issue #3286 - User Dashboard Layout System
 *
 * Features:
 * - Priority badges with color coding (HIGH=red, MEDIUM=yellow, LOW=green)
 * - Target price display
 * - Visibility indicator (private, friends, public)
 * - Notes preview
 * - Quick acquire action
 * - Grid and List view modes
 *
 * @example
 * ```tsx
 * <WishlistCard
 *   data={wishlistItem}
 *   viewMode="grid"
 *   onAcquire={() => handleAcquire(wishlistItem.id)}
 * />
 * ```
 */

'use client';

import { memo } from 'react';
import { motion } from 'framer-motion';
import {
  Heart,
  ShoppingCart,
  Lock,
  Users,
  Globe,
  Euro,
  Calendar,
  Gamepad2,
  ChevronRight,
} from 'lucide-react';

import { cn } from '@/lib/utils';
import { Button } from '@/components/ui/primitives/button';
import type { ViewMode } from './DashboardSection';

// ============================================================================
// Types
// ============================================================================

export type WishlistPriority = 'HIGH' | 'MEDIUM' | 'LOW';
export type WishlistVisibility = 'PRIVATE' | 'FRIENDS' | 'PUBLIC';

export interface WishlistItemData {
  id: string;
  game: {
    id: string;
    name: string;
    imageUrl?: string;
  };
  priority: WishlistPriority;
  targetPrice?: number;
  notes?: string;
  whereToBuy?: string;
  visibility: WishlistVisibility;
  addedAt: Date;
  isGifted?: boolean;
  giftedBy?: { id: string; name: string };
}

export interface WishlistCardProps {
  data: WishlistItemData;
  viewMode: ViewMode;
  onAcquire?: () => void;
  onClick?: () => void;
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

const PRIORITY_CONFIG: Record<WishlistPriority, { label: string; color: string; bgColor: string }> = {
  HIGH: {
    label: 'Alta',
    color: 'text-red-600 dark:text-red-400',
    bgColor: 'bg-red-500/10 border-red-500/20',
  },
  MEDIUM: {
    label: 'Media',
    color: 'text-yellow-600 dark:text-yellow-400',
    bgColor: 'bg-yellow-500/10 border-yellow-500/20',
  },
  LOW: {
    label: 'Bassa',
    color: 'text-green-600 dark:text-green-400',
    bgColor: 'bg-green-500/10 border-green-500/20',
  },
};

const VISIBILITY_CONFIG: Record<WishlistVisibility, { label: string; icon: typeof Lock }> = {
  PRIVATE: { label: 'Privato', icon: Lock },
  FRIENDS: { label: 'Amici', icon: Users },
  PUBLIC: { label: 'Pubblico', icon: Globe },
};

function formatDate(date: Date): string {
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short', year: 'numeric' });
}

// ============================================================================
// WishlistCard Component
// ============================================================================

export const WishlistCard = memo(function WishlistCard({
  data,
  viewMode,
  onAcquire,
  onClick,
  className,
}: WishlistCardProps) {
  const { game, priority, targetPrice, notes, visibility, addedAt, isGifted } = data;
  const priorityConfig = PRIORITY_CONFIG[priority];
  const visibilityConfig = VISIBILITY_CONFIG[visibility];
  const VisibilityIcon = visibilityConfig.icon;

  // Grid View
  if (viewMode === 'grid') {
    return (
      <motion.article
        whileHover={{ y: -4, boxShadow: '0 8px 25px rgba(139, 90, 60, 0.12)' }}
        transition={{ duration: 0.2 }}
        onClick={onClick}
        className={cn(
          'group relative cursor-pointer overflow-hidden rounded-xl border bg-card',
          priorityConfig.bgColor,
          className
        )}
        role="button"
        tabIndex={0}
        aria-label={`${game.name}, priorità ${priorityConfig.label}`}
      >
        {/* Image Container */}
        <div className="relative aspect-[4/3] overflow-hidden bg-muted">
          {game.imageUrl ? (
            <img
              src={game.imageUrl}
              alt=""
              className="h-full w-full object-cover transition-transform duration-300 group-hover:scale-105"
            />
          ) : (
            <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-primary/20 to-primary/5">
              <Gamepad2 className="h-12 w-12 text-primary/40" />
            </div>
          )}

          {/* Priority Badge */}
          <div
            className={cn(
              'absolute right-2 top-2 flex items-center gap-1 rounded-lg px-2 py-1',
              'text-xs font-bold shadow-md',
              priority === 'HIGH' && 'bg-red-500 text-white',
              priority === 'MEDIUM' && 'bg-yellow-500 text-white',
              priority === 'LOW' && 'bg-green-500 text-white'
            )}
          >
            {priority === 'HIGH' && '🔴'}
            {priority === 'MEDIUM' && '🟡'}
            {priority === 'LOW' && '🟢'}
            {priorityConfig.label}
          </div>

          {/* Wishlist Heart */}
          <div className="absolute left-2 top-2">
            <Heart className="h-5 w-5 fill-pink-500 text-pink-500 drop-shadow-md" />
          </div>

          {/* Gifted Badge */}
          {isGifted && (
            <div className="absolute bottom-2 left-2 rounded-md bg-primary px-2 py-0.5 text-xs font-medium text-primary-foreground shadow-md">
              🎁 Regalato
            </div>
          )}

          {/* Acquire Button (on hover) */}
          {onAcquire && !isGifted && (
            <motion.div
              initial={{ opacity: 0, scale: 0.8 }}
              className="absolute bottom-2 right-2 opacity-0 transition-opacity group-hover:opacity-100"
            >
              <Button
                variant="secondary"
                size="sm"
                className="h-8 rounded-full bg-primary px-3 text-primary-foreground shadow-lg hover:bg-primary/90"
                onClick={(e: React.MouseEvent) => {
                  e.stopPropagation();
                  onAcquire();
                }}
              >
                <ShoppingCart className="mr-1.5 h-3.5 w-3.5" />
                Acquistato
              </Button>
            </motion.div>
          )}
        </div>

        {/* Card Content */}
        <div className="p-3">
          <h3 className="truncate font-quicksand text-sm font-semibold">{game.name}</h3>
          <div className="mt-1.5 flex flex-wrap items-center gap-2 text-xs text-muted-foreground">
            {targetPrice !== undefined && (
              <span className="flex items-center gap-1 font-medium text-foreground">
                <Euro className="h-3 w-3" />
                {targetPrice}
              </span>
            )}
            <span className="flex items-center gap-1">
              <VisibilityIcon className="h-3 w-3" />
              {visibilityConfig.label}
            </span>
          </div>
          {notes && (
            <p className="mt-2 line-clamp-2 text-xs text-muted-foreground">{notes}</p>
          )}
        </div>
      </motion.article>
    );
  }

  // List View
  return (
    <motion.article
      whileHover={{ x: 4, backgroundColor: 'hsl(var(--muted) / 0.5)' }}
      transition={{ duration: 0.15 }}
      onClick={onClick}
      className={cn(
        'group flex cursor-pointer items-center gap-4 rounded-xl border p-3',
        'transition-all duration-200',
        priorityConfig.bgColor,
        className
      )}
      role="button"
      tabIndex={0}
      aria-label={`${game.name}, priorità ${priorityConfig.label}`}
    >
      {/* Thumbnail */}
      <div className="relative h-16 w-16 shrink-0 overflow-hidden rounded-lg bg-muted">
        {game.imageUrl ? (
          <img src={game.imageUrl} alt="" className="h-full w-full object-cover" />
        ) : (
          <div className="flex h-full w-full items-center justify-center bg-gradient-to-br from-primary/20 to-primary/5">
            <Gamepad2 className="h-6 w-6 text-primary/40" />
          </div>
        )}
        <Heart className="absolute right-1 top-1 h-4 w-4 fill-pink-500 text-pink-500 drop-shadow" />
      </div>

      {/* Main Info */}
      <div className="min-w-0 flex-1">
        <div className="flex items-center gap-2">
          <h3 className="truncate font-quicksand font-semibold">{game.name}</h3>
          <span
            className={cn(
              'shrink-0 rounded-md px-1.5 py-0.5 text-xs font-bold',
              priority === 'HIGH' && 'bg-red-500 text-white',
              priority === 'MEDIUM' && 'bg-yellow-500 text-white',
              priority === 'LOW' && 'bg-green-500 text-white'
            )}
          >
            {priorityConfig.label}
          </span>
          {isGifted && (
            <span className="shrink-0 rounded-md bg-primary px-1.5 py-0.5 text-xs font-medium text-primary-foreground">
              🎁 Regalato
            </span>
          )}
        </div>
        <div className="mt-1 flex flex-wrap items-center gap-x-4 gap-y-1 text-sm text-muted-foreground">
          {targetPrice !== undefined && (
            <span className="flex items-center gap-1 font-medium text-foreground">
              <Euro className="h-3.5 w-3.5" />
              Target: {targetPrice}
            </span>
          )}
          <span className="flex items-center gap-1">
            <VisibilityIcon className="h-3.5 w-3.5" />
            {visibilityConfig.label}
          </span>
          <span className="flex items-center gap-1">
            <Calendar className="h-3.5 w-3.5" />
            {formatDate(addedAt)}
          </span>
        </div>
        {notes && (
          <p className="mt-1 line-clamp-1 text-xs text-muted-foreground">{notes}</p>
        )}
      </div>

      {/* Actions */}
      <div className="flex shrink-0 items-center gap-2">
        {onAcquire && !isGifted && (
          <Button
            variant="outline"
            size="sm"
            className="hidden sm:flex"
            onClick={(e: React.MouseEvent) => {
              e.stopPropagation();
              onAcquire();
            }}
          >
            <ShoppingCart className="mr-1.5 h-3.5 w-3.5" />
            Acquistato
          </Button>
        )}
        <ChevronRight className="h-5 w-5 text-muted-foreground/50" />
      </div>
    </motion.article>
  );
});

export default WishlistCard;
