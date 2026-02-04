/**
 * WishlistHighlights - Dashboard Widget for Top 5 Wishlist Games
 * Issue #3317 - Implement WishlistHighlights widget
 *
 * Features:
 * - Shows top 5 wishlist games by priority
 * - Priority stars (1-5)
 * - Click navigation to game detail
 * - CTA to manage wishlist
 * - Empty state with explore catalog CTA
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <WishlistHighlights />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Star,
  Heart,
  ChevronRight,
  Sparkles,
} from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export interface WishlistItem {
  id: string;
  gameId: string;
  gameName: string;
  gameImageUrl?: string | null;
  priority: number; // 1-5
  addedAt: string;
}

export interface WishlistHighlightsProps {
  /** Wishlist items data */
  items?: WishlistItem[];
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const MAX_ITEMS = 5;

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_ITEMS: WishlistItem[] = [
  {
    id: 'wish-1',
    gameId: 'game-1',
    gameName: 'Terraforming Mars',
    priority: 5,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 10).toISOString(),
  },
  {
    id: 'wish-2',
    gameId: 'game-2',
    gameName: 'Gloomhaven',
    priority: 4,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 20).toISOString(),
  },
  {
    id: 'wish-3',
    gameId: 'game-3',
    gameName: 'Brass Birmingham',
    priority: 4,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 5).toISOString(),
  },
  {
    id: 'wish-4',
    gameId: 'game-4',
    gameName: 'Spirit Island',
    priority: 3,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 15).toISOString(),
  },
  {
    id: 'wish-5',
    gameId: 'game-5',
    gameName: 'Root',
    priority: 3,
    addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7).toISOString(),
  },
];

// ============================================================================
// Skeleton Component
// ============================================================================

function WishlistHighlightsSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="wishlist-highlights-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8 rounded-lg" />
          <Skeleton className="h-5 w-36" />
        </div>
      </div>

      {/* Items Skeleton */}
      <div className="space-y-2">
        {Array.from({ length: 5 }).map((_, i) => (
          <div
            key={i}
            className="flex items-center justify-between p-2 rounded-lg"
          >
            <div className="flex items-center gap-3">
              <Skeleton className="h-5 w-5 rounded" />
              <Skeleton className="h-4 w-32" />
            </div>
            <div className="flex gap-0.5">
              {Array.from({ length: 5 }).map((_, j) => (
                <Skeleton key={j} className="h-4 w-4" />
              ))}
            </div>
          </div>
        ))}
      </div>

      {/* CTA Skeleton */}
      <Skeleton className="h-9 w-full mt-4 rounded-full" />
    </div>
  );
}

// ============================================================================
// Priority Stars Component
// ============================================================================

function PriorityStars({ priority, size = 'sm' }: { priority: number; size?: 'sm' | 'md' }) {
  const iconSize = size === 'sm' ? 'h-3.5 w-3.5' : 'h-4 w-4';

  return (
    <div className="flex gap-0.5" data-testid="priority-stars">
      {Array.from({ length: 5 }).map((_, i) => (
        <Star
          key={i}
          className={cn(
            iconSize,
            i < priority
              ? 'text-amber-500 fill-amber-500'
              : 'text-muted-foreground/30'
          )}
        />
      ))}
    </div>
  );
}

// ============================================================================
// Wishlist Item Component
// ============================================================================

interface WishlistItemRowProps {
  item: WishlistItem;
  rank: number;
  index: number;
}

function WishlistItemRow({ item, rank, index }: WishlistItemRowProps) {
  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.05 }}
    >
      <Link
        href={`/games/${item.gameId}`}
        className="flex items-center justify-between p-2 rounded-lg hover:bg-muted/50 transition-colors group"
        data-testid={`wishlist-item-${item.id}`}
      >
        <div className="flex items-center gap-3 min-w-0">
          {/* Rank */}
          <span
            className="text-sm font-bold text-muted-foreground w-5 text-center tabular-nums"
            data-testid={`wishlist-rank-${item.id}`}
          >
            {rank}.
          </span>

          {/* Game Name */}
          <span
            className="text-sm font-medium truncate group-hover:text-primary transition-colors"
            data-testid={`wishlist-name-${item.id}`}
          >
            {item.gameName}
          </span>
        </div>

        {/* Priority Stars */}
        <div data-testid={`wishlist-priority-${item.id}`}>
          <PriorityStars priority={item.priority} />
        </div>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="wishlist-highlights-empty"
    >
      <div className="h-12 w-12 rounded-full bg-pink-100 dark:bg-pink-900/30 flex items-center justify-center mb-3">
        <Heart className="h-6 w-6 text-pink-500" />
      </div>
      <p className="text-sm text-muted-foreground mb-1">
        Wishlist vuota
      </p>
      <p className="text-xs text-muted-foreground mb-3">
        Aggiungi giochi che vorresti avere
      </p>
      <Link href="/games/catalog">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="explore-catalog-cta"
        >
          <Sparkles className="h-4 w-4 mr-1" />
          Esplora Catalogo
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// WishlistHighlights Component
// ============================================================================

export function WishlistHighlights({
  items = MOCK_ITEMS,
  isLoading = false,
  className,
}: WishlistHighlightsProps) {
  // Sort by priority (descending) and limit to MAX_ITEMS
  const sortedItems = useMemo(() => {
    return [...items]
      .sort((a, b) => b.priority - a.priority)
      .slice(0, MAX_ITEMS);
  }, [items]);

  if (isLoading) {
    return <WishlistHighlightsSkeleton className={className} />;
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="wishlist-highlights-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-pink-500/20 flex items-center justify-center">
            <Heart className="h-4 w-4 text-pink-600 dark:text-pink-400" />
          </div>
          <h3
            className="font-semibold text-sm"
            data-testid="wishlist-highlights-title"
          >
            Wishlist Highlights
          </h3>
        </div>
      </div>

      {/* Content */}
      {sortedItems.length === 0 ? (
        <EmptyState />
      ) : (
        <>
          {/* Subtitle */}
          <p className="text-xs text-muted-foreground mb-3 flex items-center gap-1">
            <Star className="h-3 w-3 text-amber-500 fill-amber-500" />
            Top {sortedItems.length} Wishlist:
          </p>

          {/* Items List */}
          <div className="space-y-1" data-testid="wishlist-items-list">
            {sortedItems.map((item, index) => (
              <WishlistItemRow
                key={item.id}
                item={item}
                rank={index + 1}
                index={index}
              />
            ))}
          </div>

          {/* CTA */}
          <Link href="/wishlist" className="block mt-4">
            <Button
              variant="outline"
              className="w-full rounded-full"
              data-testid="manage-wishlist-cta"
            >
              Gestisci Wishlist
              <ChevronRight className="h-4 w-4 ml-1" />
            </Button>
          </Link>
        </>
      )}
    </section>
  );
}

export default WishlistHighlights;
