/**
 * LeaderboardTable Component (Issue #2747)
 *
 * Displays top contributors leaderboard with period filters.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

'use client';

import React, { useState } from 'react';

import { useLeaderboard } from '@/hooks/queries';
import { useCurrentUser } from '@/hooks/queries';
import { LeaderboardPeriod, getTierIcon, type LeaderboardEntryDto } from '@/types/badges';
import { cn } from '@/lib/utils';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/primitives/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';

export interface LeaderboardTableProps {
  /** Initial period filter (default: AllTime) */
  period?: LeaderboardPeriod;

  /** Optional CSS class */
  className?: string;
}

/**
 * Leaderboard Table with period filters
 *
 * @example
 * ```tsx
 * <LeaderboardTable period="AllTime" />
 * ```
 */
export function LeaderboardTable({
  period = 'AllTime',
  className,
}: LeaderboardTableProps): JSX.Element {
  const [selectedPeriod, setSelectedPeriod] = useState<LeaderboardPeriod>(period);

  const { data: leaderboard, isLoading } = useLeaderboard(selectedPeriod);
  const { data: currentUser } = useCurrentUser();

  const periods: Array<{ value: LeaderboardPeriod; label: string }> = [
    { value: 'ThisWeek', label: 'This Week' },
    { value: 'ThisMonth', label: 'This Month' },
    { value: 'AllTime', label: 'All Time' },
  ];

  return (
    <Card className={cn(className)}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <svg
              xmlns="http://www.w3.org/2000/svg"
              viewBox="0 0 20 20"
              fill="currentColor"
              className="h-5 w-5"
            >
              <path
                fillRule="evenodd"
                d="M10 18a8 8 0 100-16 8 8 0 000 16zm3.857-9.809a.75.75 0 00-1.214-.882l-3.483 4.79-1.88-1.88a.75.75 0 10-1.06 1.061l2.5 2.5a.75.75 0 001.137-.089l4-5.5z"
                clipRule="evenodd"
              />
            </svg>
            <CardTitle>Top Contributors</CardTitle>
          </div>

          {/* Period Tabs */}
          <div className="bg-muted flex gap-1 rounded-lg p-1">
            {periods.map(({ value, label }) => (
              <button
                key={value}
                type="button"
                onClick={() => setSelectedPeriod(value)}
                className={cn(
                  'rounded-md px-3 py-1 text-sm font-medium transition-colors',
                  selectedPeriod === value
                    ? 'bg-background shadow-sm'
                    : 'text-muted-foreground hover:text-foreground'
                )}
              >
                {label}
              </button>
            ))}
          </div>
        </div>
      </CardHeader>

      <CardContent>
        {isLoading ? (
          <div className="space-y-3">
            {[...Array(5)].map((_, i) => (
              <Skeleton key={i} className="h-16 w-full" />
            ))}
          </div>
        ) : leaderboard && leaderboard.length > 0 ? (
          <div className="space-y-2">
            {leaderboard.map((entry, index) => (
              <LeaderboardRow
                key={entry.userId}
                entry={entry}
                position={index + 1}
                isCurrentUser={currentUser?.id === entry.userId}
              />
            ))}
          </div>
        ) : (
          <div className="flex flex-col items-center justify-center py-8">
            <p className="text-muted-foreground text-sm">No contributors yet</p>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * Internal: Leaderboard row component
 */
interface LeaderboardRowProps {
  entry: LeaderboardEntryDto;
  position: number;
  isCurrentUser: boolean;
}

function LeaderboardRow({ entry, position, isCurrentUser }: LeaderboardRowProps): JSX.Element {
  const positionIcon = getPositionIcon(position);

  return (
    <div
      className={cn(
        'flex items-center gap-4 rounded-lg border p-3 transition-colors',
        isCurrentUser && 'border-primary bg-primary/5'
      )}
    >
      {/* Position */}
      <div
        className={cn(
          'flex h-8 w-8 shrink-0 items-center justify-center rounded-full text-sm font-bold',
          position <= 3 ? 'bg-gradient-to-br text-white' : 'bg-muted text-muted-foreground',
          position === 1 && 'from-yellow-400 to-yellow-600',
          position === 2 && 'from-gray-300 to-gray-400',
          position === 3 && 'from-amber-600 to-amber-800'
        )}
      >
        {positionIcon ? positionIcon : position}
      </div>

      {/* User Avatar */}
      <div className="bg-primary/10 flex h-10 w-10 shrink-0 items-center justify-center rounded-full">
        {entry.avatarUrl ? (
          <img
            src={entry.avatarUrl}
            alt={entry.userName}
            className="h-full w-full rounded-full object-cover"
          />
        ) : (
          <span className="text-primary text-lg font-semibold">
            {entry.userName.charAt(0).toUpperCase()}
          </span>
        )}
      </div>

      {/* User Info */}
      <div className="min-w-0 flex-1">
        <p className="text-foreground truncate text-sm font-medium">
          {entry.userName}
          {isCurrentUser && (
            <span className="text-muted-foreground ml-1 text-xs font-normal">(You)</span>
          )}
        </p>
        <p className="text-muted-foreground text-xs">
          {entry.contributionCount} {entry.contributionCount === 1 ? 'contribution' : 'contributions'}
        </p>
      </div>

      {/* Top Badges */}
      <div className="flex gap-1">
        {entry.topBadges.slice(0, 3).map((badge) => (
          <div
            key={badge.id}
            className="flex h-8 w-8 items-center justify-center rounded-full bg-muted"
            title={badge.name}
          >
            {badge.iconUrl ? (
              <img
                src={badge.iconUrl}
                alt={badge.name}
                className="h-5 w-5 object-contain"
              />
            ) : (
              <span className="text-sm">{getTierIcon(badge.tier)}</span>
            )}
          </div>
        ))}
      </div>
    </div>
  );
}

/**
 * Helper: Get position icon for top 3
 */
function getPositionIcon(position: number): string | null {
  if (position === 1) return '🥇';
  if (position === 2) return '🥈';
  if (position === 3) return '🥉';
  return null;
}

LeaderboardTable.displayName = 'LeaderboardTable';
