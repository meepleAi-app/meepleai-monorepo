/**
 * Shared Game Detail Page
 * Issue #2746: Frontend - Contributor Display su SharedGame
 * Epic #2718: Game Sharing from User Library to Shared Catalog
 *
 * Public page showing full details of a shared game from the community catalog.
 * Includes game information, contributors, and community content.
 */

'use client';

import { use } from 'react';
import { useSharedGame } from '@/hooks/queries';
import { ContributorsSection } from '@/components/shared-games/ContributorsSection';
import { Skeleton } from '@/components/ui/primitives/skeleton';
import { Alert, AlertDescription } from '@/components/ui/primitives/alert';
import { AlertCircle } from 'lucide-react';

interface SharedGamePageProps {
  params: Promise<{
    id: string;
  }>;
}

export default function SharedGamePage({ params }: SharedGamePageProps) {
  const { id } = use(params);
  const { data: game, isLoading, error } = useSharedGame(id);

  // Loading state
  if (isLoading) {
    return (
      <div className="container mx-auto px-4 py-8">
        <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
          {/* Main content */}
          <div className="lg:col-span-2 space-y-6">
            <Skeleton className="h-64 w-full" />
            <Skeleton className="h-96 w-full" />
          </div>

          {/* Sidebar */}
          <aside className="space-y-6">
            <Skeleton className="h-48 w-full" />
          </aside>
        </div>
      </div>
    );
  }

  // Error state
  if (error || !game) {
    return (
      <div className="container mx-auto px-4 py-8">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>
            {error?.message || 'Failed to load game details. Please try again.'}
          </AlertDescription>
        </Alert>
      </div>
    );
  }

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="grid grid-cols-1 lg:grid-cols-3 gap-6">
        {/* Main content */}
        <div className="lg:col-span-2 space-y-6">
          {/* Game Header */}
          <div>
            <h1 className="text-3xl font-bold mb-2">{game.title}</h1>
            {game.description && (
              <p className="text-muted-foreground">{game.description}</p>
            )}
          </div>

          {/* Game Details - Placeholder for future expansion */}
          <div className="rounded-lg border p-6">
            <h2 className="text-xl font-semibold mb-4">Game Details</h2>
            <div className="grid grid-cols-2 gap-4">
              {game.minPlayers && game.maxPlayers && (
                <div>
                  <p className="text-sm text-muted-foreground">Players</p>
                  <p className="font-medium">
                    {game.minPlayers === game.maxPlayers
                      ? game.minPlayers
                      : `${game.minPlayers}-${game.maxPlayers}`}
                  </p>
                </div>
              )}
              {game.playingTime && (
                <div>
                  <p className="text-sm text-muted-foreground">Playing Time</p>
                  <p className="font-medium">{game.playingTime} min</p>
                </div>
              )}
              {game.complexity && (
                <div>
                  <p className="text-sm text-muted-foreground">Complexity</p>
                  <p className="font-medium">{game.complexity.toFixed(1)}/5.0</p>
                </div>
              )}
            </div>
          </div>

          {/* Additional sections can be added here:
              - FAQs
              - Errata
              - Community Documents
              - Reviews
          */}
        </div>

        {/* Sidebar */}
        <aside className="space-y-6">
          {/* Contributors Section - NEW */}
          <ContributorsSection gameId={id} />

          {/* Future sidebar sections:
              - Quick Actions (Add to Library, Share, Report)
              - Categories & Mechanics
              - Publisher Info
              - BGG Link
          */}
        </aside>
      </div>
    </div>
  );
}
