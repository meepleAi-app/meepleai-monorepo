/**
 * UserBadges Component (Issue #2747)
 *
 * Container component for user badges section with toggle and navigation.
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 * Milestone: 5 - Gamification
 */

'use client';

import React from 'react';
import Link from 'next/link';

import { useMyBadges } from '@/hooks/queries';
import { BadgeGrid } from './BadgeGrid';
import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/primitives/card';
import { Button } from '@/components/ui/primitives/button';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

export interface UserBadgesProps {
  /** Optional CSS class */
  className?: string;

  /** Show "View All" button (default: true) */
  showViewAll?: boolean;

  /** Maximum badges to display before "View All" (default: 12) */
  maxDisplay?: number;
}

/**
 * User Badges section component
 *
 * @example
 * ```tsx
 * <UserBadges showViewAll maxDisplay={12} />
 * ```
 */
export function UserBadges({
  className,
  showViewAll = true,
  maxDisplay = 12,
}: UserBadgesProps): JSX.Element {
  const { data: badges, isLoading } = useMyBadges();
  const { mutate: toggleDisplay } = useToggleBadgeDisplay();

  const displayedBadges = badges?.slice(0, maxDisplay) ?? [];
  const hasMore = (badges?.length ?? 0) > maxDisplay;

  if (isLoading) {
    return (
      <Card className={cn(className)}>
        <CardHeader>
          <Skeleton className="h-6 w-32" />
        </CardHeader>
        <CardContent>
          <div className="grid grid-cols-2 gap-4 sm:grid-cols-3 md:grid-cols-4">
            {[...Array(8)].map((_, i) => (
              <Skeleton key={i} className="h-32 w-full" />
            ))}
          </div>
        </CardContent>
      </Card>
    );
  }

  return (
    <Card className={cn(className)}>
      <CardHeader>
        <div className="flex items-center justify-between">
          <div>
            <CardTitle>My Badges</CardTitle>
            <p className="text-muted-foreground mt-1 text-sm">
              {badges?.length ?? 0} {(badges?.length ?? 0) === 1 ? 'badge' : 'badges'} earned
            </p>
          </div>

          {showViewAll && hasMore && (
            <Button asChild variant="outline" size="sm">
              <Link href="/badges">View All Badges</Link>
            </Button>
          )}
        </div>
      </CardHeader>

      <CardContent>
        {!badges || badges.length === 0 ? (
          <div className="flex flex-col items-center justify-center py-12">
            <div className="bg-muted mb-4 flex h-16 w-16 items-center justify-center rounded-full">
              <svg
                xmlns="http://www.w3.org/2000/svg"
                viewBox="0 0 20 20"
                fill="currentColor"
                className="text-muted-foreground h-8 w-8"
              >
                <path
                  fillRule="evenodd"
                  d="M5.166 2.621v.858c-1.035.148-2.059.33-3.071.543a.75.75 0 00-.584.859 6.753 6.753 0 006.138 5.6 6.73 6.73 0 002.743 1.346A6.707 6.707 0 019.279 15H8.54c-1.036 0-1.875.84-1.875 1.875V19.5h-.099a2.25 2.25 0 00-2.25 2.25h10.5a2.25 2.25 0 00-2.25-2.25h-.099v-2.625c0-1.036-.84-1.875-1.875-1.875h-.739a6.706 6.706 0 01-1.112-3.173 6.73 6.73 0 002.743-1.347 6.753 6.753 0 006.139-5.6.75.75 0 00-.585-.858 47.077 47.077 0 00-3.07-.543V2.62a.75.75 0 00-.658-.744 49.22 49.22 0 00-6.093-.377c-2.063 0-4.096.128-6.093.377a.75.75 0 00-.657.744zm0 2.629c0 1.196.312 2.32.857 3.294A5.266 5.266 0 013.16 5.337a45.6 45.6 0 012.006-.343v.256zm13.5 0v-.256c.674.1 1.343.214 2.006.343a5.265 5.265 0 01-2.863 3.207 6.72 6.72 0 00.857-3.294z"
                  clipRule="evenodd"
                />
              </svg>
            </div>
            <p className="text-foreground mb-1 font-medium">No badges yet</p>
            <p className="text-muted-foreground text-sm">
              Start contributing to earn your first badge!
            </p>
          </div>
        ) : (
          <BadgeGrid badges={displayedBadges} showHidden={false} />
        )}
      </CardContent>
    </Card>
  );
}

UserBadges.displayName = 'UserBadges';
