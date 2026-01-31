/**
 * Badges Page (Issue #2747)
 *
 * Dedicated page for viewing all user badges and leaderboard.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

'use client';

import React from 'react';

import { BadgeGrid } from '@/components/badges';
import { LeaderboardTable } from '@/components/badges';
import { useMyBadges } from '@/hooks/queries';

/**
 * Badges page with full grid and leaderboard
 */
export default function BadgesPage(){
  return (
    <div className="container mx-auto py-8">
      <div className="mb-8">
        <h1 className="text-foreground mb-2 text-3xl font-bold">My Badges</h1>
        <p className="text-muted-foreground">
          View all your earned badges and see how you rank on the leaderboard
        </p>
      </div>

      <div className="grid gap-8 lg:grid-cols-3">
        {/* Badges Grid - 2/3 width */}
        <div className="lg:col-span-2">
          <BadgesSection />
        </div>

        {/* Leaderboard - 1/3 width */}
        <div>
          <LeaderboardTable />
        </div>
      </div>
    </div>
  );
}

/**
 * Client component for badges section
 */
function BadgesSection(){
  'use client';

  const { data: badges, isLoading } = useMyBadges();

  if (isLoading) {
    return (
      <div className="space-y-4">
        <div className="h-8 w-48 animate-pulse rounded bg-muted" />
        <div className="grid grid-cols-2 gap-4 sm:grid-cols-3">
          {[...Array(12)].map((_, i) => (
            <div key={i} className="h-32 animate-pulse rounded bg-muted" />
          ))}
        </div>
      </div>
    );
  }

  return (
    <div className="rounded-lg border bg-card p-6">
      <div className="mb-6">
        <h2 className="text-foreground mb-1 text-xl font-semibold">All Badges</h2>
        <p className="text-muted-foreground text-sm">
          {badges?.length ?? 0} {(badges?.length ?? 0) === 1 ? 'badge' : 'badges'} earned
        </p>
      </div>

      <BadgeGrid badges={badges ?? []} showHidden />
    </div>
  );
}
