/**
 * Library Game Detail Page (Issue #3513)
 *
 * Displays comprehensive game details for a game in the user's library.
 * Features:
 * - Flippable MeepleCard (hero) with front (basic info) and back (detailed info)
 * - MeepleInfoCard with Knowledge Base (PDFs), Social Links, and Stats
 * - User section with collection status, labels, and actions
 */

'use client';

import { useMemo } from 'react';

import { ArrowLeft, Clock, Gauge, Users } from 'lucide-react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';

import { JourneyProgress } from '@/components/library/JourneyProgress';
import { UserActionSection } from '@/components/library/game-detail/UserActionSection';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

export default function LibraryGameDetailPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = params?.gameId as string;

  const { data: gameDetail, isLoading, error } = useLibraryGameDetail(gameId);

  // Build metadata chips for MeepleCard
  const metadata = useMemo(() => {
    if (!gameDetail) return [];
    const items = [];

    if (gameDetail.minPlayers && gameDetail.maxPlayers) {
      const players = gameDetail.minPlayers === gameDetail.maxPlayers
        ? `${gameDetail.minPlayers}`
        : `${gameDetail.minPlayers}-${gameDetail.maxPlayers}`;
      items.push({ icon: Users, value: players });
    }

    if (gameDetail.playingTimeMinutes) {
      items.push({ icon: Clock, value: `${gameDetail.playingTimeMinutes} min` });
    }

    if (gameDetail.complexityRating) {
      items.push({ icon: Gauge, value: `${gameDetail.complexityRating.toFixed(1)}/5` });
    }

    return items;
  }, [gameDetail]);

  // Error state
  if (error) {
    return (
      <div className="min-h-screen bg-background">
        <div className="mx-auto max-w-6xl px-4 py-8">
          <Alert variant="destructive">
            <AlertTitle>Errore</AlertTitle>
            <AlertDescription>
              {error instanceof Error ? error.message : 'Si è verificato un errore nel caricamento del gioco.'}
            </AlertDescription>
          </Alert>
          <Button
            variant="outline"
            className="mt-4"
            onClick={() => router.push('/library')}
          >
            <ArrowLeft className="mr-2 h-4 w-4" />
            Torna alla Libreria
          </Button>
        </div>
      </div>
    );
  }

  // Game not found in library
  if (!isLoading && !gameDetail) {
    return (
      <div className="min-h-screen bg-background">
        <div className="mx-auto max-w-6xl px-4 py-8">
          <Alert>
            <AlertTitle>Gioco non trovato</AlertTitle>
            <AlertDescription>
              Questo gioco non è presente nella tua libreria.
            </AlertDescription>
          </Alert>
          <Button
            variant="outline"
            className="mt-4"
            onClick={() => router.push('/library')}
          >
            <ArrowLeft className="mr-2 h-4 w-4" />
            Torna alla Libreria
          </Button>
        </div>
      </div>
    );
  }

  // Loading state handled by loading.tsx, but fallback just in case
  if (isLoading || !gameDetail) {
    return null;
  }

  return (
    <div className="min-h-screen bg-[#FAF8F5]">
      {/* Background gradient decoration */}
      <div
        className="pointer-events-none fixed inset-0"
        style={{
          background: `
            radial-gradient(ellipse at 20% 30%, hsla(25, 40%, 90%, 0.4) 0%, transparent 50%),
            radial-gradient(ellipse at 80% 70%, hsla(262, 30%, 92%, 0.3) 0%, transparent 50%)
          `,
        }}
      />

      {/* Header */}
      <header className="sticky top-0 z-50 border-b border-[rgba(45,42,38,0.08)] bg-[rgba(250,248,245,0.85)] backdrop-blur-md">
        <div className="mx-auto flex max-w-6xl items-center justify-between px-4 py-3 sm:px-6">
          <Link
            href="/library"
            className="group flex items-center gap-2 font-quicksand text-sm font-semibold text-[#6B665C] transition-colors hover:text-[hsl(25,95%,38%)]"
          >
            <ArrowLeft className="h-5 w-5 transition-transform group-hover:-translate-x-1" />
            <span>Torna alla Collezione</span>
          </Link>
          <span className="font-quicksand text-lg font-bold tracking-tight text-[hsl(25,95%,38%)]">
            MeepleAI
          </span>
        </div>
      </header>

      {/* Main Content */}
      <main className="relative z-10 mx-auto max-w-6xl px-4 py-8 sm:px-6">
        {/* Journey Progress Banner */}
        <JourneyProgress gameId={gameId} className="mb-6" />

        {/* Cards Section - MeepleCard (hero, flippable) + MeepleInfoCard */}
        <section className="mb-8 flex flex-col items-center justify-center gap-6 lg:flex-row lg:items-start">
          {/* Flippable Game Card */}
          <MeepleCard
            entity="game"
            variant="hero"
            title={gameDetail.gameTitle}
            subtitle={
              (gameDetail.gamePublisher ?? 'Publisher sconosciuto') +
              (gameDetail.gameYearPublished ? ` (${gameDetail.gameYearPublished})` : '')
            }
            imageUrl={gameDetail.gameImageUrl ?? undefined}
            rating={gameDetail.averageRating ?? undefined}
            ratingMax={10}
            metadata={metadata}
            flippable
            flipData={{
              description: gameDetail.description ?? undefined,
              categories: gameDetail.categories,
              mechanics: gameDetail.mechanics,
              designers: gameDetail.designers,
              publishers: gameDetail.publishers,
              complexityRating: gameDetail.complexityRating,
              minAge: gameDetail.minAge,
            }}
          />

          {/* Info Card - KB, Social & Stats */}
          <MeepleInfoCard
            gameId={gameId}
            gameTitle={gameDetail.gameTitle}
            bggId={gameDetail.bggId}
            showStats
            statsData={{
              timesPlayed: gameDetail.timesPlayed,
              lastPlayed: gameDetail.lastPlayed,
              winRate: gameDetail.winRate,
              avgDuration: gameDetail.avgDuration,
            }}
            recentSessions={gameDetail.recentSessions}
          />
        </section>

        {/* User Actions Section */}
        <UserActionSection gameDetail={gameDetail} />
      </main>
    </div>
  );
}
