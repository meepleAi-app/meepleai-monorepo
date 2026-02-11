/**
 * Wishlist Highlights Widget (Issue #4114)
 * Dashboard widget showing top 3 wishlist priorities
 */

'use client';

import React from 'react';
import Link from 'next/link';
import { Heart, ArrowRight } from 'lucide-react';
import { Button } from '@/components/ui/primitives/button';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';

export function WishlistHighlightsWidget() {
  // TODO: Fetch from API GET /wishlist/highlights
  const highlights = [
    {
      id: '1',
      gameId: 'game-1',
      name: 'Brass: Birmingham',
      publisher: 'Roxley Games',
      priority: 1,
      imageUrl: 'https://via.placeholder.com/300',
    },
    {
      id: '2',
      gameId: 'game-2',
      name: 'Gaia Project',
      publisher: 'Z-Man Games',
      priority: 2,
      imageUrl: 'https://via.placeholder.com/300',
    },
    {
      id: '3',
      gameId: 'game-3',
      name: 'Spirit Island',
      publisher: 'Greater Than Games',
      priority: 3,
      imageUrl: 'https://via.placeholder.com/300',
    },
  ];

  return (
    <div className="bg-card rounded-xl p-6 border border-border/50">
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Heart className="h-5 w-5 text-rose-500 fill-rose-500" />
          <h2 className="text-xl font-bold font-quicksand">Wishlist Highlights</h2>
        </div>
        <Link href="/library/wishlist">
          <Button variant="ghost" size="sm" className="gap-1">
            View All
            <ArrowRight className="h-4 w-4" />
          </Button>
        </Link>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        {highlights.map((item, index) => (
          <div key={item.id} className="relative">
            <div className="absolute -top-2 -left-2 z-10 bg-primary text-primary-foreground w-7 h-7 rounded-full flex items-center justify-center text-sm font-bold">
              {index + 1}
            </div>
            <MeepleCard
              entity="game"
              variant="compact"
              title={item.name}
              subtitle={item.publisher}
              imageUrl={item.imageUrl}
              showWishlist
              isWishlisted={true}
              onWishlistToggle={() => {
                // Remove from wishlist
              }}
              onClick={() => {
                window.location.href = `/games/${item.gameId}`;
              }}
            />
          </div>
        ))}
      </div>
    </div>
  );
}
