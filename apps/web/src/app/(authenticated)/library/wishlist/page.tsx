/**
 * Wishlist Page (Issue #4114)
 *
 * User wishlist management with:
 * - Grid/Table view toggle
 * - Sorting (priority, date, name, rating)
 * - Filtering (complexity, players, playtime)
 * - Bulk operations
 */

'use client';

import React, { useState } from 'react';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';

export default function WishlistPage() {
  const [viewMode, setViewMode] = useState<'grid' | 'list'>('grid');

  return (
    <div className="container mx-auto py-4">
      <div className="flex items-center justify-end mb-4">
        <div className="flex items-center gap-4">
          {/* View mode toggle */}
          <button
            onClick={() => setViewMode(viewMode === 'grid' ? 'list' : 'grid')}
            className="px-4 py-2 rounded-lg bg-primary text-primary-foreground text-sm"
          >
            {viewMode === 'grid' ? 'List View' : 'Grid View'}
          </button>
        </div>
      </div>

      {/* Wishlist grid */}
      <div
        className={
          viewMode === 'grid' ? 'grid grid-cols-1 md:grid-cols-3 lg:grid-cols-4 gap-4' : 'space-y-2'
        }
      >
        {/* Mock data - replace with actual wishlist */}
        <MeepleCard
          entity="game"
          variant={viewMode}
          title="Sample Game"
          subtitle="Publisher"
          imageUrl="https://via.placeholder.com/300"
          showWishlist
          isWishlisted={true}
          onWishlistToggle={() => {}}
        />
      </div>
    </div>
  );
}
