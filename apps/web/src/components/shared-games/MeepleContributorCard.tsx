/**
 * MeepleContributorCard Component
 *
 * Displays individual contributor information using the MeepleCard system
 * with entity="player" and variant="list".
 *
 * Migrated from ContributorCard to use the unified MeepleCard design tokens.
 *
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 */

'use client';

import { formatDistanceToNow } from 'date-fns';
import Link from 'next/link';

import { BadgeIcon } from '@/components/badges/BadgeIcon';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import type { GameContributorDto } from '@/lib/api';
import { cn } from '@/lib/utils';

interface MeepleContributorCardProps {
  contributor: GameContributorDto;
  featured?: boolean;
}

export function MeepleContributorCard({
  contributor,
  featured = false,
}: MeepleContributorCardProps) {
  const contributionText = `${contributor.contributionCount} contribution${contributor.contributionCount !== 1 ? 's' : ''}`;
  const timeAgo = formatDistanceToNow(new Date(contributor.firstContributionAt), {
    addSuffix: true,
  });

  return (
    <Link
      href={`/contributors/${contributor.userId}`}
      className={cn('block', featured && 'ring-2 ring-primary/20 rounded-xl')}
    >
      <MeepleCard
        entity="player"
        variant="list"
        title={contributor.userName}
        subtitle={`${contributionText} \u00b7 ${timeAgo}`}
        avatarUrl={contributor.avatarUrl ?? undefined}
        badge={contributor.isPrimaryContributor ? 'Original' : undefined}
        className={cn(featured && 'bg-primary/5')}
        data-testid="meeple-contributor-card"
      />
      {/* Badges Section */}
      {contributor.topBadges.length > 0 && (
        <div className="flex items-center gap-1.5 px-3 pb-2 -mt-1">
          <span className="text-xs text-muted-foreground mr-1">Badges:</span>
          <div className="flex items-center gap-1.5">
            {contributor.topBadges.map(badge => (
              <BadgeIcon key={badge.id} badge={badge} size="sm" />
            ))}
          </div>
        </div>
      )}
    </Link>
  );
}
