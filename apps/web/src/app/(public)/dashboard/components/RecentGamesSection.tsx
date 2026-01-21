/**
 * RecentGamesSection Component - Dashboard Recent Games Grid
 *
 * Issue #1836: PAGE-002 - Dashboard Page
 *
 * Displays user's most recently played or added games in a responsive grid.
 * Uses existing GameCard component (UI-003) with grid variant.
 *
 * Features:
 * - 2x3 grid on mobile/tablet (6 games max)
 * - Skeleton loading states (pulse animation)
 * - Error states with retry
 * - Empty state for no games
 * - Link to full games catalog
 *
 * Dependencies:
 * - UI-003: GameCard component ✅
 * - TanStack Query useGames hook ✅
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

'use client';

import React from 'react';

import { AlertCircle, ArrowRight, Gamepad2 } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { GameCard } from '@/components/games/GameCard';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import type { Game } from '@/lib/api';

export interface RecentGamesSectionProps {
  /** Games data from useGames hook */
  games?: Game[];
  /** Loading state from TanStack Query */
  isLoading: boolean;
  /** Error from TanStack Query */
  error: unknown;
}

/**
 * RecentGamesSection Component
 */
export function RecentGamesSection({ games, isLoading, error }: RecentGamesSectionProps) {
  const router = useRouter();
  // Loading state: Skeleton grid
  if (isLoading) {
    return (
      <section className="space-y-4" aria-label="Recent games">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-quicksand font-semibold">Giochi Recenti</h2>
        </div>

        <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
          {Array.from({ length: 6 }).map((_, i) => (
            <Skeleton key={i} className="h-64 w-full" />
          ))}
        </div>
      </section>
    );
  }

  // Error state: Alert with description
  if (error) {
    const errorMessage = error instanceof Error ? error.message : String(error);

    return (
      <section className="space-y-4" aria-label="Recent games">
        <h2 className="text-xl font-quicksand font-semibold">Giochi Recenti</h2>

        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Errore di Caricamento</AlertTitle>
          <AlertDescription>
            Impossibile caricare i giochi recenti. Riprova più tardi.
            <span className="block mt-2 text-xs opacity-75">{errorMessage}</span>
          </AlertDescription>
        </Alert>
      </section>
    );
  }

  // Empty state: No games yet
  if (!games || games.length === 0) {
    return (
      <section className="space-y-4" aria-label="Recent games">
        <h2 className="text-xl font-quicksand font-semibold">Giochi Recenti</h2>

        <div className="flex flex-col items-center justify-center py-12 px-4 border-2 border-dashed border-muted rounded-lg">
          <Gamepad2 className="h-12 w-12 text-muted-foreground mb-4" />
          <h3 className="text-lg font-semibold mb-2">Nessun Gioco Ancora</h3>
          <p className="text-muted-foreground text-center mb-4 max-w-md">
            Aggiungi i tuoi giochi da tavolo preferiti per iniziare a chattare con l'AI.
          </p>
          <Button asChild>
            <Link href="/games">
              Esplora Catalogo
              <ArrowRight className="ml-2 h-4 w-4" />
            </Link>
          </Button>
        </div>
      </section>
    );
  }

  // Main content: Games grid (2x3 responsive)
  return (
    <section className="space-y-4" aria-label="Recent games">
      <div className="flex items-center justify-between">
        <h2 className="text-xl font-quicksand font-semibold">Giochi Recenti</h2>
        <Button variant="ghost" size="sm" asChild>
          <Link href="/games">
            Vedi Tutti
            <ArrowRight className="ml-2 h-4 w-4" />
          </Link>
        </Button>
      </div>

      <div className="grid grid-cols-2 md:grid-cols-3 gap-4">
        {games.slice(0, 6).map(game => (
          <GameCard
            key={game.id}
            game={game}
            variant="grid"
            onClick={() => router.push(`/giochi/${game.id}`)}
          />
        ))}
      </div>
    </section>
  );
}
