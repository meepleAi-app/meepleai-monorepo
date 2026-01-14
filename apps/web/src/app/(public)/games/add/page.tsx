/**
 * Add Game Page (Issue #2373: Phase 4)
 *
 * Integrates SharedGameSearch for catalog-first game discovery.
 * Users can add games from:
 * 1. SharedGameCatalog (preferred - enriched data)
 * 2. BoardGameGeek (fallback - basic data)
 *
 * @see claudedocs/shared-game-catalog-spec.md (Section: User-Facing)
 */

'use client';

import { useState } from 'react';

import { Loader2 } from 'lucide-react';
import dynamic from 'next/dynamic';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { SharedGameSearch, type SharedGameSearchResult } from '@/components/shared-games';

// ISSUE #2374 Phase 5: Lazy load heavy modal component for bundle optimization
// SharedGameDetailModal is only used when user selects catalog game (not BGG)
// Impact: ~50KB initial bundle reduction
const SharedGameDetailModal = dynamic(
  () =>
    import('@/components/shared-games').then(mod => ({
      default: mod.SharedGameDetailModal,
    })),
  {
    loading: () => (
      <div className="flex items-center justify-center p-8">
        <Loader2 className="w-8 h-8 animate-spin text-primary" />
      </div>
    ),
    ssr: false, // Modal doesn't need SSR (user interaction only)
  }
);

import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/alert-dialog';
import { Badge } from '@/components/ui/badge';
import { api } from '@/lib/api';

export default function AddGamePage() {
  const router = useRouter();
  const [selectedGame, setSelectedGame] = useState<SharedGameSearchResult | null>(null);
  const [isCatalogModalOpen, setIsCatalogModalOpen] = useState(false);
  const [isBggDialogOpen, setIsBggDialogOpen] = useState(false);
  const [isAdding, setIsAdding] = useState(false);

  /**
   * Handle game selection from search results
   */
  const handleGameSelect = (game: SharedGameSearchResult) => {
    setSelectedGame(game);
    if (game.source === 'catalog') {
      setIsCatalogModalOpen(true);
    } else {
      // BGG-only game - show simple confirmation
      setIsBggDialogOpen(true);
    }
  };

  /**
   * Add game from SharedGameCatalog (preferred)
   * Links the new game to the catalog entry for enriched data
   */
  const handleAddFromCatalog = async (gameId: string) => {
    setIsAdding(true);

    try {
      // Get full game details from catalog
      const catalogGame = await api.sharedGames.getById(gameId);

      if (!catalogGame) {
        toast.error('Gioco non trovato nel catalogo.');
        return;
      }

      // Extract publisher name from first publisher object
      const publisherName = catalogGame.publishers?.[0]?.name ?? null;

      // Create game in user's collection with catalog link
      await api.games.create({
        title: catalogGame.title,
        publisher: publisherName,
        yearPublished: catalogGame.yearPublished,
        minPlayers: catalogGame.minPlayers,
        maxPlayers: catalogGame.maxPlayers,
        minPlayTimeMinutes: catalogGame.playingTimeMinutes,
        maxPlayTimeMinutes: catalogGame.playingTimeMinutes,
        iconUrl: catalogGame.thumbnailUrl,
        imageUrl: catalogGame.imageUrl,
        bggId: catalogGame.bggId ?? null,
        sharedGameId: catalogGame.id, // Link to catalog!
      });

      toast.success('Gioco aggiunto con successo!');
      setIsCatalogModalOpen(false);
      router.push('/games');
      router.refresh();
    } catch (error) {
      console.error('Failed to add game from catalog:', error);
      toast.error("Errore durante l'aggiunta del gioco.");
    } finally {
      setIsAdding(false);
    }
  };

  /**
   * Add game from BGG (fallback)
   * No catalog link, just BGG data
   */
  const handleAddFromBgg = async () => {
    if (!selectedGame?.bggId) return;

    setIsAdding(true);

    try {
      // Get full details from BGG
      const details = await api.bgg.getGameDetails(selectedGame.bggId);

      // Create game in user's collection (no catalog link)
      await api.games.create({
        title: details.name,
        publisher: details.publishers?.[0] ?? null,
        yearPublished: details.yearPublished,
        minPlayers: details.minPlayers,
        maxPlayers: details.maxPlayers,
        minPlayTimeMinutes: details.minPlayTime ?? details.playingTime ?? null,
        maxPlayTimeMinutes: details.maxPlayTime ?? details.playingTime ?? null,
        iconUrl: details.thumbnailUrl,
        imageUrl: details.imageUrl,
        bggId: details.bggId,
        // No sharedGameId - BGG only
      });

      toast.success('Gioco aggiunto con successo!');
      setIsBggDialogOpen(false);
      router.push('/games');
      router.refresh();
    } catch (error) {
      console.error('Failed to add game from BGG:', error);
      toast.error("Errore durante l'aggiunta del gioco.");
    } finally {
      setIsAdding(false);
    }
  };

  return (
    <div className="container mx-auto px-4 py-8">
      <div className="max-w-2xl mx-auto">
        <div className="mb-8">
          <h1 className="text-3xl font-bold mb-2">Aggiungi Gioco</h1>
          <p className="text-muted-foreground">
            Cerca un gioco nel nostro catalogo o su BoardGameGeek per aggiungerlo alla tua
            collezione.
          </p>
        </div>

        {/* SharedGameSearch with catalog-first approach */}
        <SharedGameSearch
          onSelect={handleGameSelect}
          placeholder="Cerca un gioco..."
          autoFocus
          showFilters
          className="mb-8"
        />

        {/* Info about catalog vs BGG */}
        <div className="mt-8 p-4 rounded-lg bg-muted/50 text-sm text-muted-foreground">
          <p className="font-medium mb-2">💡 Come funziona?</p>
          <ul className="list-disc list-inside space-y-1">
            <li>
              <strong>Catalogo</strong>: Giochi verificati con FAQ, regole e dati arricchiti
            </li>
            <li>
              <strong>BGG</strong>: Giochi da BoardGameGeek con dati base
            </li>
          </ul>
        </div>

        {/* Catalog Detail Modal - for catalog games with rich content */}
        {selectedGame?.source === 'catalog' && (
          <SharedGameDetailModal
            open={isCatalogModalOpen}
            onClose={() => setIsCatalogModalOpen(false)}
            gameId={selectedGame.id}
            onAddToCollection={handleAddFromCatalog}
          />
        )}

        {/* BGG Confirmation Dialog - for BGG-only games */}
        <AlertDialog open={isBggDialogOpen} onOpenChange={setIsBggDialogOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Aggiungi gioco da BGG</AlertDialogTitle>
              <AlertDialogDescription className="space-y-3">
                <div className="flex items-center gap-3 mt-2">
                  {selectedGame?.thumbnailUrl && (
                    <img
                      src={selectedGame.thumbnailUrl}
                      alt={selectedGame.title}
                      className="w-16 h-16 rounded object-cover"
                    />
                  )}
                  <div>
                    <p className="font-semibold text-foreground">{selectedGame?.title}</p>
                    {selectedGame?.yearPublished && (
                      <p className="text-sm text-muted-foreground">
                        ({selectedGame.yearPublished})
                      </p>
                    )}
                  </div>
                </div>
                <div className="flex items-center gap-2">
                  <Badge variant="outline">BGG</Badge>
                  <span className="text-sm">
                    Questo gioco verrà aggiunto con i dati base di BoardGameGeek.
                  </span>
                </div>
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel disabled={isAdding}>Annulla</AlertDialogCancel>
              <AlertDialogAction onClick={handleAddFromBgg} disabled={isAdding}>
                {isAdding ? (
                  <>
                    <Loader2 className="w-4 h-4 mr-2 animate-spin" />
                    Aggiungendo...
                  </>
                ) : (
                  'Aggiungi alla Collezione'
                )}
              </AlertDialogAction>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </div>
  );
}
