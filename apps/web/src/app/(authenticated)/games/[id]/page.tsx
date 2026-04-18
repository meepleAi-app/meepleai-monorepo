/**
 * Game Detail Page - /games/[id]
 *
 * Main landing page for a specific game. Shows Hero MeepleCard with ManaPips
 * and tab navigation to sub-pages (Regole, FAQ, Sessioni, Strategie).
 *
 * @see Issue #4889
 */

'use client';

import { ArrowLeft } from 'lucide-react';
import Link from 'next/link';
import { useParams, useRouter } from 'next/navigation';

import { AddToLibraryButton } from '@/components/library/AddToLibraryButton';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useGameManaPips, buildGameManaPips } from '@/hooks/queries/useGameManaPips';
import { useGameInLibraryStatus } from '@/hooks/queries/useLibrary';
import { useSharedGame } from '@/hooks/queries/useSharedGames';

// ========== Tab Navigation ==========

interface TabLink {
  label: string;
  href: string;
}

function GameTabNav({ gameId }: { gameId: string }) {
  const tabs: TabLink[] = [
    { label: 'Regole', href: `/games/${gameId}/rules` },
    { label: 'FAQ', href: `/games/${gameId}/faqs` },
    { label: 'Sessioni', href: `/games/${gameId}/sessions` },
    { label: 'Strategie', href: `/games/${gameId}/strategies` },
  ];

  return (
    <nav className="rounded-xl border border-border bg-card shadow-sm overflow-hidden">
      <div className="flex">
        {tabs.map((tab, index) => (
          <Link
            key={tab.href}
            href={tab.href}
            className={[
              'flex-1 px-4 py-3 text-center text-sm font-nunito font-medium transition-colors',
              'hover:bg-amber-50 hover:text-amber-700 dark:hover:bg-amber-950/20 dark:hover:text-amber-400',
              index === 0
                ? 'bg-amber-100 text-amber-800 font-semibold dark:bg-amber-950/30 dark:text-amber-300'
                : 'text-muted-foreground',
              index < tabs.length - 1 ? 'border-r border-border' : '',
            ]
              .filter(Boolean)
              .join(' ')}
          >
            {tab.label}
          </Link>
        ))}
      </div>
    </nav>
  );
}

// ========== Loading Skeleton ==========

function GameDetailSkeleton() {
  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        <Skeleton className="h-8 w-48 mb-6" />
        <Skeleton className="h-[400px] w-full max-w-2xl mx-auto mb-8" />
        <Skeleton className="h-14 w-full rounded-xl" />
      </div>
    </div>
  );
}

// ========== Page ==========

export default function GameDetailPage() {
  const params = useParams<{ id: string }>();
  const id = params?.id ?? '';
  const router = useRouter();

  const { data: game, isLoading: gameLoading } = useSharedGame(id);
  const { data: libraryStatus, isLoading: libraryLoading } = useGameInLibraryStatus(id);
  const { data: manaPipsData, isLoading: pipsLoading } = useGameManaPips(id);

  const isLoading = gameLoading || libraryLoading || pipsLoading;

  const manaPips = buildGameManaPips(manaPipsData, {
    onCreateSession: () => {
      if (!game) return;
      router.push(`/sessions/new?gameId=${id}&gameName=${encodeURIComponent(game.title)}`);
    },
  });

  if (isLoading) {
    return <GameDetailSkeleton />;
  }

  if (!game) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-4xl">
          <Alert variant="destructive">
            <AlertDescription className="font-nunito">Gioco non trovato.</AlertDescription>
          </Alert>
          <Button asChild variant="ghost" className="mt-4 font-nunito">
            <Link href="/games">
              <ArrowLeft className="mr-2 h-4 w-4" /> Torna al Catalogo
            </Link>
          </Button>
        </div>
      </div>
    );
  }

  const publisherName = game.publishers?.[0]?.name ?? null;
  const subtitle =
    [publisherName, game.yearPublished ? String(game.yearPublished) : null]
      .filter(Boolean)
      .join(' · ') || 'Gioco da Tavolo';

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-4xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna al Catalogo
          </Link>
        </Button>

        {/* Hero MeepleCard */}
        <section className="mb-8 flex justify-center">
          <MeepleCard
            entity="game"
            variant="hero"
            title={game.title}
            subtitle={subtitle}
            imageUrl={game.imageUrl || undefined}
            rating={game.averageRating ?? undefined}
            ratingMax={10}
            badge={libraryStatus?.inLibrary ? 'In libreria' : undefined}
            manaPips={manaPips}
          />
        </section>

        {/* Library Action */}
        {!libraryStatus?.inLibrary && (
          <div className="mb-6 flex justify-center" data-testid="add-to-library-section">
            <AddToLibraryButton gameId={id} gameTitle={game.title} size="lg" />
          </div>
        )}

        {/* Tab Navigation */}
        <GameTabNav gameId={id} />
      </div>
    </div>
  );
}
