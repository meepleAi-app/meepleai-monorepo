/**
 * ContributorCard Component
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 *
 * Displays individual contributor information with avatar, stats, and badges.
 */

import { formatDistanceToNow } from 'date-fns';
import { User, Award } from 'lucide-react';
import Link from 'next/link';

import { BadgeIcon } from '@/components/badges/BadgeIcon';
import { Badge } from '@/components/ui/data-display/badge';
import type { GameContributorDto } from '@/lib/api';
import { cn } from '@/lib/utils';

interface ContributorCardProps {
  contributor: GameContributorDto;
  featured?: boolean;
}

export function ContributorCard({ contributor, featured = false }: ContributorCardProps) {
  const contributionText = `${contributor.contributionCount} contribution${contributor.contributionCount !== 1 ? 's' : ''}`;
  const timeAgo = formatDistanceToNow(new Date(contributor.firstContributionAt), {
    addSuffix: true,
  });

  return (
    <Link
      href={`/contributors/${contributor.userId}`}
      className={cn(
        'group block rounded-lg border bg-card text-card-foreground p-4 transition-all duration-200 hover:shadow-md hover:border-primary/50',
        featured && 'ring-2 ring-primary/20 bg-primary/5'
      )}
    >
      {/* Header: Avatar + Name */}
      <div className="flex items-center gap-3 mb-3">
        {/* Avatar */}
        <div className="relative flex-shrink-0">
          {contributor.avatarUrl ? (
            /* eslint-disable-next-line @next/next/no-img-element -- External user-provided avatar URL */
            <img
              src={contributor.avatarUrl}
              alt={contributor.userName}
              className="w-12 h-12 rounded-full object-cover border-2 border-muted"
            />
          ) : (
            <div className="w-12 h-12 rounded-full bg-muted flex items-center justify-center border-2 border-muted">
              <span className="text-lg font-semibold text-muted-foreground">
                {contributor.userName[0].toUpperCase()}
              </span>
            </div>
          )}
          {featured && (
            <div className="absolute -top-1 -right-1 w-5 h-5 bg-primary rounded-full flex items-center justify-center">
              <Award className="h-3 w-3 text-primary-foreground" />
            </div>
          )}
        </div>

        {/* Name + Primary Badge */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2 mb-1">
            <h3 className="font-semibold text-sm truncate group-hover:text-primary transition-colors">
              {contributor.userName}
            </h3>
            {contributor.isPrimaryContributor && (
              <Badge variant="default" className="text-xs px-1.5 py-0 h-5 bg-primary/20 text-primary border-primary/30">
                <Award className="h-3 w-3 mr-0.5" />
                Original
              </Badge>
            )}
          </div>
          {/* Stats Row */}
          <div className="flex items-center gap-1.5 text-xs text-muted-foreground">
            <User className="h-3 w-3" />
            <span>{contributionText}</span>
            <span className="text-muted-foreground/60">•</span>
            <span>{timeAgo}</span>
          </div>
        </div>
      </div>

      {/* Badges Section */}
      {contributor.topBadges.length > 0 && (
        <div className="flex items-center gap-1.5 pt-2 border-t">
          <span className="text-xs text-muted-foreground mr-1">Badges:</span>
          <div className="flex items-center gap-1.5">
            {contributor.topBadges.map((badge) => (
              <BadgeIcon key={badge.id} badge={badge} size="sm" />
            ))}
          </div>
        </div>
      )}
    </Link>
  );
}
