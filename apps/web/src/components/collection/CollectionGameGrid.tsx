'use client';

import { AnimatePresence, motion } from 'framer-motion';
import { Library, Plus } from 'lucide-react';
import Link from 'next/link';

import { GameCarousel, type CarouselGame } from '@/components/ui/data-display/game-carousel';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import { COLLECTION_TEST_IDS } from '@/lib/test-ids';
import { cn } from '@/lib/utils';

import type { ViewMode } from './CollectionToolbar';

// ============================================================================
// Types
// ============================================================================

export interface CollectionGame {
  id: string;
  title: string;
  thumbnailUrl?: string;
  imageUrl?: string;
  yearPublished?: number;
  addedAt: string;
  playCount: number;
  hasPdf: boolean;
  hasActiveChat: boolean;
  chatCount: number;
  status: 'owned' | 'wishlisted' | 'borrowed';
}

// ============================================================================
// Sub-Components
// ============================================================================

function GameGridSkeleton({ count = 8 }: { count?: number }) {
  return (
    <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4">
      {Array.from({ length: count }).map((_, i) => (
        <div key={i} className="rounded-xl border bg-card/80 overflow-hidden">
          <Skeleton className="aspect-[4/3]" />
          <div className="p-3 space-y-2">
            <Skeleton className="h-4 w-3/4" />
            <Skeleton className="h-3 w-1/2" />
            <div className="grid grid-cols-2 gap-2">
              <Skeleton className="h-3 w-full" />
              <Skeleton className="h-3 w-full" />
            </div>
          </div>
        </div>
      ))}
    </div>
  );
}

function EmptyState({ hasFilters }: { hasFilters: boolean }) {
  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      className="flex flex-col items-center justify-center py-16 text-center"
      data-testid={COLLECTION_TEST_IDS.emptyState}
    >
      <div className="rounded-full bg-muted/50 p-6 mb-4">
        <Library className="h-12 w-12 text-muted-foreground" />
      </div>
      <h3 className="font-heading text-xl font-semibold mb-2">
        {hasFilters ? 'Nessun gioco trovato' : 'La tua collezione è vuota'}
      </h3>
      <p className="text-muted-foreground max-w-md mb-6">
        {hasFilters
          ? "Prova a modificare i filtri o cerca qualcos'altro."
          : 'Inizia ad aggiungere i tuoi giochi da tavolo preferiti per costruire la tua collezione.'}
      </p>
      {!hasFilters && (
        <Button asChild size="lg" className="gap-2">
          <Link href="/library">
            <Plus className="h-5 w-5" />
            Aggiungi il tuo primo gioco
          </Link>
        </Button>
      )}
    </motion.div>
  );
}

// ============================================================================
// Exported Component
// ============================================================================

interface CollectionGameGridProps {
  games: CollectionGame[];
  carouselGames: CarouselGame[];
  viewMode: ViewMode;
  isLoading: boolean;
  hasError: boolean;
  hasActiveFilters: boolean;
  totalCount?: number;
  totalPages?: number;
  page: number;
  onPageChange: (page: number) => void;
}

export function CollectionGameGrid({
  games,
  carouselGames,
  viewMode,
  isLoading,
  hasError,
  hasActiveFilters,
  totalCount,
  totalPages,
  page,
  onPageChange,
}: CollectionGameGridProps) {
  const { t } = useTranslation();

  if (isLoading) {
    return <GameGridSkeleton count={8} />;
  }

  if (hasError) {
    return (
      <div className="flex flex-col items-center justify-center py-16 text-center">
        <p className="text-destructive mb-4">Errore nel caricamento della collezione.</p>
        <Button onClick={() => window.location.reload()}>Riprova</Button>
      </div>
    );
  }

  if (games.length === 0) {
    return <EmptyState hasFilters={hasActiveFilters} />;
  }

  return (
    <section aria-label="Game collection">
      {/* Results count */}
      <div className="flex items-center justify-between mb-4">
        <p className="text-sm text-muted-foreground">
          {totalCount === 1 ? '1 gioco' : `${totalCount || 0} giochi`}
          {hasActiveFilters && ' trovati'}
        </p>
      </div>

      {/* Game Grid / Carousel */}
      {viewMode === 'carousel' ? (
        <GameCarousel
          games={carouselGames}
          flippable={false}
          showDots
          data-testid="collection-carousel"
        />
      ) : (
        <AnimatePresence mode="wait">
          <motion.div
            key={`${viewMode}-${page}`}
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            exit={{ opacity: 0 }}
            transition={{ duration: 0.2 }}
            className={cn(
              viewMode === 'grid'
                ? 'grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-4'
                : 'flex flex-col gap-3'
            )}
            data-testid={COLLECTION_TEST_IDS.grid}
          >
            {games.map(game => (
              <MeepleCard
                key={game.id}
                id={game.id}
                entity="game"
                title={game.title}
                imageUrl={game.imageUrl}
                variant={viewMode === 'list' ? 'list' : 'grid'}
                status={game.status}
                metadata={
                  [
                    { label: t('collection.year'), value: game.yearPublished?.toString() },
                    { label: t('collection.plays'), value: game.playCount.toString() },
                  ].filter(m => m.value) as Array<{ label: string; value: string }>
                }
              />
            ))}
          </motion.div>
        </AnimatePresence>
      )}

      {/* Pagination */}
      {totalPages && totalPages > 1 && (
        <div className="flex items-center justify-center gap-2 mt-8">
          <Button
            variant="outline"
            size="sm"
            onClick={() => onPageChange(Math.max(1, page - 1))}
            disabled={page === 1}
          >
            Precedente
          </Button>
          <span className="text-sm text-muted-foreground px-4">
            Pagina {page} di {totalPages}
          </span>
          <Button
            variant="outline"
            size="sm"
            onClick={() => onPageChange(Math.min(totalPages, page + 1))}
            disabled={page === totalPages}
          >
            Successiva
          </Button>
        </div>
      )}
    </section>
  );
}
