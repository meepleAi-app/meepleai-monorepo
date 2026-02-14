/**
 * WishlistHighlights - Dashboard Widget for Top 5 Wishlist Games
 * Issue #3920 - Frontend: Wishlist Highlights Component
 *
 * Features:
 * - Shows top 5 wishlist games sorted by priority (HIGH > MEDIUM > LOW)
 * - Game cover thumbnails with lazy loading
 * - Colored priority badges (HIGH=rose, MEDIUM=amber, LOW=emerald)
 * - Target price display
 * - Quick action: "Segna come acquistato" (removes from wishlist)
 * - CTA: "Gestisci Wishlist" links to /wishlist
 * - Empty state with "Aggiungi primo gioco" CTA
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <WishlistHighlights
 *   items={wishlistItems}
 *   onMarkPurchased={(id) => removeFromWishlist(id)}
 * />
 * ```
 */

'use client';

import { useMemo, useCallback } from 'react';

import { motion } from 'framer-motion';
import {
  Heart,
  ChevronRight,
  Plus,
  ShoppingCart,
  Gamepad2,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useWishlistHighlights } from '@/hooks/useWishlistHighlights';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type WishlistPriorityLevel = 'HIGH' | 'MEDIUM' | 'LOW';

export interface WishlistHighlightItem {
  id: string;
  game: {
    id: string;
    name: string;
    coverUrl: string;
  };
  priority: WishlistPriorityLevel;
  targetPrice?: number;
}

export interface WishlistHighlightsProps {
  /** Wishlist items data */
  items?: WishlistHighlightItem[];
  /** Loading state */
  isLoading?: boolean;
  /** Callback when user marks an item as purchased */
  onMarkPurchased?: (itemId: string) => void;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const MAX_ITEMS = 5;

const PRIORITY_ORDER: Record<WishlistPriorityLevel, number> = {
  HIGH: 3,
  MEDIUM: 2,
  LOW: 1,
};

const PRIORITY_CONFIG: Record<
  WishlistPriorityLevel,
  { label: string; className: string }
> = {
  HIGH: {
    label: 'Alta',
    className:
      'bg-rose-100 text-rose-700 dark:bg-rose-900/30 dark:text-rose-400',
  },
  MEDIUM: {
    label: 'Media',
    className:
      'bg-amber-100 text-amber-700 dark:bg-amber-900/30 dark:text-amber-400',
  },
  LOW: {
    label: 'Bassa',
    className:
      'bg-emerald-100 text-emerald-700 dark:bg-emerald-900/30 dark:text-emerald-400',
  },
};

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
          <div key={i} className="flex items-center gap-3 p-2">
            <Skeleton className="h-10 w-10 rounded-lg shrink-0" />
            <div className="flex-1 space-y-1.5">
              <Skeleton className="h-4 w-28" />
              <Skeleton className="h-3 w-16" />
            </div>
            <Skeleton className="h-6 w-12 rounded-full" />
          </div>
        ))}
      </div>

      {/* CTA Skeleton */}
      <Skeleton className="h-9 w-full mt-4 rounded-full" />
    </div>
  );
}

// ============================================================================
// Priority Badge Component
// ============================================================================

function PriorityBadge({ priority }: { priority: WishlistPriorityLevel }) {
  const config = PRIORITY_CONFIG[priority];

  return (
    <span
      className={cn(
        'inline-flex items-center px-2 py-0.5 rounded-full text-[10px] font-semibold uppercase tracking-wide',
        config.className
      )}
      data-testid="priority-badge"
    >
      {config.label}
    </span>
  );
}

// ============================================================================
// Wishlist Item Row Component
// ============================================================================

interface WishlistItemRowProps {
  item: WishlistHighlightItem;
  index: number;
  onMarkPurchased?: (itemId: string) => void;
}

function WishlistItemRow({
  item,
  index,
  onMarkPurchased,
}: WishlistItemRowProps) {
  const handleMarkPurchased = useCallback(
    (e: React.MouseEvent) => {
      e.preventDefault();
      e.stopPropagation();
      onMarkPurchased?.(item.id);
    },
    [item.id, onMarkPurchased]
  );

  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.05 }}
      data-testid={`wishlist-item-${item.id}`}
    >
      <div className="flex items-center gap-3 p-2 rounded-xl hover:bg-muted/30 transition-all duration-200 group">
        {/* Cover Image */}
        <Link
          href={`/games/${item.game.id}`}
          className="relative h-10 w-10 shrink-0 rounded-lg overflow-hidden bg-muted"
          data-testid={`wishlist-cover-${item.id}`}
        >
          {item.game.coverUrl ? (
            <Image
              src={item.game.coverUrl}
              alt={item.game.name}
              fill
              sizes="40px"
              className="object-cover"
              loading="lazy"
            />
          ) : (
            <div className="h-full w-full flex items-center justify-center">
              <Gamepad2 className="h-5 w-5 text-muted-foreground/50" />
            </div>
          )}
        </Link>

        {/* Game Info */}
        <Link
          href={`/games/${item.game.id}`}
          className="flex-1 min-w-0"
        >
          <p
            className="font-medium text-sm truncate group-hover:text-primary transition-colors"
            data-testid={`wishlist-name-${item.id}`}
          >
            {item.game.name}
          </p>
          <div className="flex items-center gap-2 mt-0.5">
            <PriorityBadge priority={item.priority} />
            {item.targetPrice !== undefined && item.targetPrice !== null && (
              <span
                className="text-xs text-muted-foreground"
                data-testid={`wishlist-price-${item.id}`}
              >
                Target: {item.targetPrice.toFixed(2)}
              </span>
            )}
          </div>
        </Link>

        {/* Quick Action: Mark as Purchased */}
        {onMarkPurchased && (
          <button
            onClick={handleMarkPurchased}
            className="shrink-0 p-1.5 rounded-lg text-muted-foreground hover:text-emerald-600 hover:bg-emerald-100 dark:hover:bg-emerald-900/30 transition-colors opacity-0 group-hover:opacity-100 focus:opacity-100"
            title="Segna come acquistato"
            aria-label={`Segna ${item.game.name} come acquistato`}
            data-testid={`wishlist-purchased-${item.id}`}
          >
            <ShoppingCart className="h-4 w-4" />
          </button>
        )}
      </div>
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
      <p className="text-sm text-muted-foreground mb-3">Wishlist vuota</p>
      <Link href="/games/catalog">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="explore-catalog-cta"
        >
          <Plus className="h-4 w-4 mr-1" />
          Aggiungi primo gioco
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// WishlistHighlights Component
// ============================================================================

export function WishlistHighlights({
  items: itemsProp,
  isLoading: isLoadingProp,
  onMarkPurchased,
  className,
}: WishlistHighlightsProps) {
  // Use hook when no data prop is provided
  const { data: hookData, isLoading: hookLoading } = useWishlistHighlights();

  const isLoading = isLoadingProp ?? (itemsProp === undefined && hookLoading);

  const sortedItems = useMemo(() => {
    const items = itemsProp ?? hookData ?? [];
    return [...items]
      .sort(
        (a, b) =>
          PRIORITY_ORDER[b.priority] - PRIORITY_ORDER[a.priority]
      )
      .slice(0, MAX_ITEMS);
  }, [itemsProp, hookData]);

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

        <Link
          href="/wishlist"
          className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors"
          data-testid="view-wishlist-link"
        >
          Gestisci Wishlist
          <ChevronRight className="h-3 w-3" />
        </Link>
      </div>

      {/* Content */}
      {sortedItems.length === 0 ? (
        <EmptyState />
      ) : (
        <div className="space-y-1" data-testid="wishlist-items-list">
          {sortedItems.map((item, index) => (
            <WishlistItemRow
              key={item.id}
              item={item}
              index={index}
              onMarkPurchased={onMarkPurchased}
            />
          ))}
        </div>
      )}
    </section>
  );
}

export default WishlistHighlights;
