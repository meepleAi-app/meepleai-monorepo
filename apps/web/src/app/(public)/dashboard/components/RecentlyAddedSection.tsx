/**
 * RecentlyAddedSection Component (Issue #2612)
 *
 * Dashboard widget showing recently added games from user's library.
 * Features:
 * - 1→3→5 column responsive grid
 * - Skeleton loading states
 * - Empty state (hidden if no library entries)
 * - Error handling with alert
 * - "Vedi Tutti" link to full library
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

'use client';

import { AlertCircle, ArrowRight, Library } from 'lucide-react';
import Link from 'next/link';


// Direct import to avoid loading framer-motion through barrel export (UserGameCard)
import { RecentLibraryCard } from '@/components/library/RecentLibraryCard';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useRecentlyAddedGames } from '@/hooks/queries';

export interface RecentlyAddedSectionProps {
  /** Number of games to display (default: 5) */
  limit?: number;
}

/**
 * RecentlyAddedSection Component
 *
 * Shows user's recently added library games in a responsive grid.
 * Hidden if library is empty.
 */
export function RecentlyAddedSection({ limit = 5 }: RecentlyAddedSectionProps) {
  const { data, isLoading, error } = useRecentlyAddedGames(limit);

  // Loading state: Skeleton grid
  if (isLoading) {
    return (
      <section className="space-y-4" aria-label="Recently added games">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-quicksand font-semibold">Aggiunti di Recente</h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-4">
          {Array.from({ length: limit }).map((_, i) => (
            <Skeleton key={i} className="h-52 w-full" />
          ))}
        </div>
      </section>
    );
  }

  // Error state: Alert with description
  if (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);

    return (
      <section className="space-y-4" aria-label="Recently added games">
        <h2 className="text-xl font-quicksand font-semibold">Aggiunti di Recente</h2>

        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Errore di Caricamento</AlertTitle>
          <AlertDescription>
            Impossibile caricare i giochi dalla libreria.
            <span className="block mt-2 text-xs opacity-75">{errorMessage}</span>
          </AlertDescription>
        </Alert>
      </section>
    );
  }

  // Empty state: Hide section if library is empty
  if (!data?.items || data.items.length === 0) {
    return null;
  }

  // Main content: Games grid (1→3→5 responsive)
  return (
    <section className="space-y-4" aria-label="Recently added games">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <Library className="h-5 w-5 text-muted-foreground" />
          <h2 className="text-xl font-quicksand font-semibold">Aggiunti di Recente</h2>
        </div>
        <Button variant="ghost" size="sm" asChild>
          <Link href="/library">
            Vedi Tutti
            <ArrowRight className="ml-2 h-4 w-4" />
          </Link>
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-3 lg:grid-cols-5 gap-4">
        {data.items.map(game => (
          <RecentLibraryCard key={game.id} game={game} />
        ))}
      </div>
    </section>
  );
}
