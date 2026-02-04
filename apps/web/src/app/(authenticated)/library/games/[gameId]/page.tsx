/**
 * Library Game Detail Page (Issue #3513)
 *
 * Displays comprehensive game details for a game in the user's library.
 * Features:
 * - Flippable game card with front (basic info) and back (detailed info)
 * - Side card for Knowledge Base (PDFs) and Social Links
 * - User section with collection status, labels, stats, and actions
 */

'use client';

import { useParams, useRouter } from 'next/navigation';

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';

import { GameDetailHero } from '@/components/library/game-detail/GameDetailHero';
import { GameSideCard } from '@/components/library/game-detail/GameSideCard';
import { UserActionSection } from '@/components/library/game-detail/UserActionSection';
import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Button } from '@/components/ui/primitives/button';
import { useLibraryGameDetail } from '@/hooks/queries/useLibrary';

export default function LibraryGameDetailPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = params?.gameId as string;

  const { data: gameDetail, isLoading, error } = useLibraryGameDetail(gameId);

  // Handle loading in the loading.tsx file

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
        {/* Cards Section - Game Card + Side Card */}
        <section className="mb-8 flex flex-col items-center justify-center gap-6 lg:flex-row lg:items-start">
          {/* Flippable Game Card */}
          <GameDetailHero gameDetail={gameDetail} />

          {/* Side Card - KB & Social */}
          <GameSideCard gameId={gameId} gameTitle={gameDetail.gameTitle} />
        </section>

        {/* User Actions Section */}
        <UserActionSection gameDetail={gameDetail} />
      </main>
    </div>
  );
}
