'use client';

/**
 * Wishlist Page (Issue #4114)
 *
 * Displays the current user's wishlist.
 * - Grid of WishlistCard components
 * - Add to Wishlist button with dialog
 * - Empty state when no items
 * - Loading skeleton while fetching
 * - Remove item action
 */

import { Heart, PlusCircle } from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { AddToWishlistDialog } from '@/components/wishlist/AddToWishlistDialog';
import { MeepleWishlistCard } from '@/components/wishlist/MeepleWishlistCard';
import { useRemoveFromWishlist, useWishlist } from '@/hooks/queries/useWishlist';

// ============================================================================
// Skeleton
// ============================================================================

function WishlistSkeleton() {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
      {Array.from({ length: 8 }).map((_, i) => (
        <div key={i} className="rounded-xl border bg-card shadow p-4 space-y-3">
          <div className="flex items-center justify-between">
            <Skeleton className="h-4 w-32" />
            <Skeleton className="h-5 w-16 rounded-full" />
          </div>
          <Skeleton className="h-4 w-24" />
          <Skeleton className="h-4 w-40" />
          <Skeleton className="h-4 w-28" />
        </div>
      ))}
    </div>
  );
}

// ============================================================================
// Empty State
// ============================================================================

function WishlistEmpty({ onAdd }: { onAdd: React.ReactNode }) {
  return (
    <div className="flex flex-col items-center justify-center py-20 gap-4 text-center">
      <Heart className="h-12 w-12 text-muted-foreground/40" />
      <div>
        <p className="text-lg font-semibold text-foreground">Your wishlist is empty</p>
        <p className="text-sm text-muted-foreground mt-1">
          Start adding games you want to play or own.
        </p>
      </div>
      {onAdd}
    </div>
  );
}

// ============================================================================
// Page
// ============================================================================

export default function WishlistPage() {
  const { data: items, isLoading, isError } = useWishlist();
  const { mutate: removeItem } = useRemoveFromWishlist();

  function handleRemove(id: string) {
    removeItem(id);
  }

  const addButton = (
    <AddToWishlistDialog
      trigger={
        <Button>
          <PlusCircle className="h-4 w-4" />
          Add to Wishlist
        </Button>
      }
    />
  );

  return (
    <div className="container mx-auto py-6 space-y-6">
      {/* Header */}
      <div className="flex items-center justify-between">
        <div>
          <h1 className="text-2xl font-bold">My Wishlist</h1>
          {!isLoading && !isError && items && items.length > 0 && (
            <p className="text-sm text-muted-foreground mt-1">
              {items.length} {items.length === 1 ? 'game' : 'games'}
            </p>
          )}
        </div>
        {!isLoading && !isError && items && items.length > 0 && addButton}
      </div>

      {/* Content */}
      {isLoading && <WishlistSkeleton />}

      {isError && (
        <div className="py-10 text-center text-sm text-destructive">
          Failed to load wishlist. Please try again.
        </div>
      )}

      {!isLoading && !isError && items && items.length === 0 && <WishlistEmpty onAdd={addButton} />}

      {!isLoading && !isError && items && items.length > 0 && (
        <div className="grid grid-cols-1 sm:grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-4">
          {items.map(item => (
            <MeepleWishlistCard key={item.id} item={item} onRemove={handleRemove} />
          ))}
        </div>
      )}
    </div>
  );
}
