/**
 * ContributorsSection Component
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 *
 * Main section displaying all contributors for a shared game,
 * with primary contributor highlighted and additional contributors listed.
 */

'use client';

import { Users } from 'lucide-react';
import { useGameContributors } from '@/hooks/queries';
import { ContributorCard } from './ContributorCard';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/primitives/skeleton';

interface ContributorsSectionProps {
  gameId: string;
}

export function ContributorsSection({ gameId }: ContributorsSectionProps) {
  const { data: contributors, isLoading } = useGameContributors(gameId);

  // Loading state
  if (isLoading) {
    return (
      <Card>
        <CardHeader>
          <div className="flex items-center gap-2">
            <Users className="h-5 w-5" />
            <CardTitle>Contributors</CardTitle>
          </div>
          <CardDescription>This game was contributed by the community</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <Skeleton className="h-24 w-full" />
          <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
            <Skeleton className="h-20 w-full" />
            <Skeleton className="h-20 w-full" />
          </div>
        </CardContent>
      </Card>
    );
  }

  // No contributors - should not happen, but handle gracefully
  if (!contributors?.length) {
    return null;
  }

  // Separate primary from additional contributors
  const primaryContributor = contributors.find((c) => c.isPrimaryContributor);
  const additionalContributors = contributors.filter((c) => !c.isPrimaryContributor);

  return (
    <Card>
      <CardHeader>
        <div className="flex items-center gap-2">
          <Users className="h-5 w-5 text-primary" />
          <CardTitle>Contributors</CardTitle>
        </div>
        <CardDescription>This game was contributed by the community</CardDescription>
      </CardHeader>
      <CardContent className="space-y-4">
        {/* Primary Contributor */}
        {primaryContributor && (
          <div>
            <p className="text-sm font-semibold text-muted-foreground mb-2">Original Contributor</p>
            <ContributorCard contributor={primaryContributor} featured />
          </div>
        )}

        {/* Additional Contributors */}
        {additionalContributors.length > 0 && (
          <div>
            <p className="text-sm font-semibold text-muted-foreground mb-2">
              Additional Contributors ({additionalContributors.length})
            </p>
            <div className="grid grid-cols-1 md:grid-cols-2 gap-3">
              {additionalContributors.map((contributor) => (
                <ContributorCard key={contributor.userId} contributor={contributor} />
              ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
