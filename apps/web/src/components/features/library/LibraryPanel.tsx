'use client';

/**
 * LibraryPanel — Library tab panel for AlphaShell.
 *
 * Internal tabs:
 * - I Miei Giochi: User's library (useLibrary)
 * - Catalogo: Shared game catalog (useSharedGames)
 * - Wishlist: User's wishlist (useWishlist)
 *
 * Each sub-tab displays MeepleCard grids or EmptyStateCard for empty states.
 */

import { useState } from 'react';

import { BookOpen, Search, Heart } from 'lucide-react';

import { EmptyStateCard, SkeletonCardGrid } from '@/components/features/common';
import { MeepleCard, entityColors } from '@/components/ui/data-display/meeple-card';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/navigation/tabs';
import { useLibrary } from '@/hooks/queries/useLibrary';
import { useSharedGames } from '@/hooks/queries/useSharedGames';
import { useWishlist } from '@/hooks/queries/useWishlist';
import { useAlphaNav } from '@/hooks/useAlphaNav';

export function LibraryPanel() {
  const { openDetail } = useAlphaNav();
  const [activeTab, setActiveTab] = useState('my-games');
  const [catalogSearch, setCatalogSearch] = useState('');

  const { data: library, isLoading: libraryLoading } = useLibrary({
    page: 1,
    pageSize: 20,
    sortBy: 'addedAt',
    sortDescending: true,
  });

  const { data: catalog, isLoading: catalogLoading } = useSharedGames({
    searchTerm: catalogSearch || undefined,
    page: 1,
    pageSize: 20,
  });

  const { data: wishlist, isLoading: wishlistLoading } = useWishlist();

  const libraryItems = library?.items ?? [];
  const catalogItems = catalog?.items ?? [];
  const wishlistItems = wishlist ?? [];

  return (
    <div className="p-4 sm:p-6">
      <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
        <TabsList className="w-full grid grid-cols-3">
          <TabsTrigger value="my-games">I Miei Giochi</TabsTrigger>
          <TabsTrigger value="catalog">Catalogo</TabsTrigger>
          <TabsTrigger value="wishlist">Wishlist</TabsTrigger>
        </TabsList>

        {/* I Miei Giochi */}
        <TabsContent value="my-games">
          {libraryLoading ? (
            <SkeletonCardGrid count={6} />
          ) : libraryItems.length === 0 ? (
            <EmptyStateCard
              title="La tua libreria e' vuota"
              description="Aggiungi giochi dal catalogo per costruire la tua collezione personale."
              ctaLabel="Esplora Catalogo"
              onCtaClick={() => setActiveTab('catalog')}
              icon={BookOpen}
              entityColor={entityColors.game.hsl}
            />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6 mt-4">
              {libraryItems.map(entry => (
                <MeepleCard
                  key={entry.gameId}
                  id={entry.gameId}
                  entity="game"
                  variant="grid"
                  title={entry.gameTitle ?? 'Gioco'}
                  subtitle={entry.gamePublisher ?? undefined}
                  imageUrl={entry.gameImageUrl ?? entry.gameIconUrl ?? undefined}
                  rating={entry.averageRating ?? undefined}
                  ratingMax={10}
                  onClick={() => openDetail(entry.gameId, 'game')}
                />
              ))}
            </div>
          )}
        </TabsContent>

        {/* Catalogo */}
        <TabsContent value="catalog">
          <div className="mt-4 mb-4">
            <input
              type="text"
              placeholder="Cerca giochi nel catalogo..."
              value={catalogSearch}
              onChange={e => setCatalogSearch(e.target.value)}
              className="w-full px-4 py-2 rounded-lg border border-border bg-background text-foreground
                         placeholder:text-muted-foreground focus:outline-none focus:ring-2 focus:ring-ring"
            />
          </div>
          {catalogLoading ? (
            <SkeletonCardGrid count={6} />
          ) : catalogItems.length === 0 ? (
            <EmptyStateCard
              title="Nessun gioco trovato"
              description="Prova a modificare i criteri di ricerca o esplora il catalogo completo."
              ctaLabel="Cancella Filtri"
              onCtaClick={() => setCatalogSearch('')}
              icon={Search}
              entityColor={entityColors.game.hsl}
            />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6">
              {catalogItems.map(game => (
                <MeepleCard
                  key={game.id}
                  id={game.id}
                  entity="game"
                  variant="grid"
                  title={game.title}
                  subtitle={`${game.minPlayers}-${game.maxPlayers} giocatori`}
                  imageUrl={game.imageUrl ?? game.thumbnailUrl ?? undefined}
                  rating={game.averageRating ?? undefined}
                  ratingMax={10}
                  onClick={() => openDetail(game.id, 'game')}
                />
              ))}
            </div>
          )}
        </TabsContent>

        {/* Wishlist */}
        <TabsContent value="wishlist">
          {wishlistLoading ? (
            <SkeletonCardGrid count={4} />
          ) : wishlistItems.length === 0 ? (
            <EmptyStateCard
              title="La tua wishlist e' vuota"
              description="Aggiungi giochi alla wishlist dal catalogo per non dimenticarli."
              ctaLabel="Esplora Catalogo"
              onCtaClick={() => setActiveTab('catalog')}
              icon={Heart}
              entityColor="350 89% 60%"
            />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4 gap-6 mt-4">
              {wishlistItems.map(item => (
                <MeepleCard
                  key={item.id}
                  id={item.gameId}
                  entity="game"
                  variant="grid"
                  title={item.notes ?? `Gioco ${item.gameId.slice(0, 8)}`}
                  subtitle={`Priorita: ${item.priority}`}
                  status="wishlisted"
                  onClick={() => openDetail(item.gameId, 'game')}
                />
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>
    </div>
  );
}
