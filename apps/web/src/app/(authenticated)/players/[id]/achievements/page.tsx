/**
 * Player Achievements Page - /players/[id]/achievements
 *
 * Shows badges earned by the player.
 * Uses api.badges.getMyBadges() (current user's badges).
 *
 * @see Issue #4890
 */

'use client';

import { useState, useEffect } from 'react';

import { ArrowLeft, Lock, Star, Trophy } from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import type { UserBadgeDto } from '@/lib/api/schemas/badges.schemas';
import { cn } from '@/lib/utils';

const TIER_COLORS: Record<string, string> = {
  Bronze: 'bg-amber-100 text-amber-800 border-amber-300',
  Silver: 'bg-gray-100 text-gray-700 border-gray-300',
  Gold: 'bg-yellow-100 text-yellow-800 border-yellow-300',
  Platinum: 'bg-cyan-100 text-cyan-800 border-cyan-300',
  Diamond: 'bg-purple-100 text-purple-800 border-purple-300',
};

export default function PlayerAchievementsPage() {
  const params = useParams();
  const playerId = params?.id as string;
  const playerName = decodeURIComponent(playerId).replace(/-/g, ' ');

  const [badges, setBadges] = useState<UserBadgeDto[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    setLoading(true);
    api.badges
      .getMyBadges()
      .then((data) => setBadges(data))
      .catch((err) => {
        setError(err instanceof Error ? err.message : 'Failed to load badges');
      })
      .finally(() => setLoading(false));
  }, []);

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href={`/players/${playerId}`}>
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to {playerName}
          </Link>
        </Button>

        {/* Header */}
        <div className="flex items-center gap-3 mb-8">
          <Trophy className="h-6 w-6 text-primary" />
          <div>
            <h1 className="text-3xl font-bold font-quicksand">Achievements</h1>
            <p className="text-muted-foreground font-nunito text-sm">
              Badges and milestones earned by {playerName}
            </p>
          </div>
          {!loading && badges.length > 0 && (
            <Badge variant="secondary" className="ml-auto">
              {badges.length} earned
            </Badge>
          )}
        </div>

        {/* Loading */}
        {loading && (
          <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
            {[...Array(6)].map((_, i) => (
              <Skeleton key={i} className="h-36 w-full rounded-xl" />
            ))}
          </div>
        )}

        {/* Error */}
        {error && !loading && (
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">{error}</AlertDescription>
          </Alert>
        )}

        {/* Badges Grid */}
        {!loading && !error && (
          <>
            {badges.length === 0 ? (
              <Card>
                <CardContent className="py-12 flex flex-col items-center gap-4 text-center">
                  <Lock className="h-12 w-12 text-muted-foreground/40" />
                  <div>
                    <p className="font-semibold font-quicksand">No badges yet</p>
                    <p className="text-sm text-muted-foreground font-nunito mt-1">
                      Keep playing to earn your first badge!
                    </p>
                  </div>
                </CardContent>
              </Card>
            ) : (
              <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 gap-4">
                {badges.map((badge) => (
                  <div
                    key={badge.id}
                    className="p-5 rounded-xl border border-primary/30 bg-card shadow-sm hover:shadow-md transition-shadow"
                  >
                    <div className="flex items-start gap-3 mb-3">
                      {badge.iconUrl ? (
                        // eslint-disable-next-line @next/next/no-img-element
                        <img
                          src={badge.iconUrl}
                          alt={badge.name}
                          className="h-10 w-10 rounded-full object-cover shrink-0"
                        />
                      ) : (
                        <div className="h-10 w-10 rounded-full bg-primary/10 flex items-center justify-center shrink-0">
                          <Star className="h-5 w-5 text-primary" />
                        </div>
                      )}
                      <div className="flex-1 min-w-0">
                        <h3 className="font-bold font-quicksand truncate">{badge.name}</h3>
                        <p className="text-sm text-muted-foreground font-nunito line-clamp-2">
                          {badge.description}
                        </p>
                      </div>
                    </div>

                    <div className="flex items-center justify-between gap-2">
                      <Badge
                        variant="outline"
                        className={cn('text-xs', TIER_COLORS[badge.tier])}
                      >
                        {badge.tier}
                      </Badge>
                      <p className="text-xs text-muted-foreground font-nunito">
                        {new Intl.DateTimeFormat('en-US', {
                          month: 'short',
                          day: 'numeric',
                          year: 'numeric',
                        }).format(new Date(badge.earnedAt))}
                      </p>
                    </div>
                  </div>
                ))}
              </div>
            )}
          </>
        )}
      </div>
    </div>
  );
}
