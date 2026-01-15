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
  const [_isAdding, setIsAdding] = useState(false);

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
   * Add game from SharedGameCatalog to user's personal library
   * Issue #2440: Uses library endpoint instead of games.create (which requires Admin)
   */
  const handleAddFromCatalog = async (gameId: string) => {
    setIsAdding(true);

    try {
      // Add shared game to user's personal library
      await api.library.addGame(gameId);

      toast.success('Gioco aggiunto alla tua libreria!');
      setIsCatalogModalOpen(false);
      router.push('/games');
      router.refresh();
    } catch (error) {
      console.error('Failed to add game from catalog:', error);
      const errorMessage =
        error instanceof Error && error.message.includes('already in')
          ? 'Questo gioco è già nella tua libreria.'
          : "Errore durante l'aggiunta del gioco.";
      toast.error(errorMessage);
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

        {/* BGG Info Dialog - for BGG-only games (not in catalog) */}
        <AlertDialog open={isBggDialogOpen} onOpenChange={setIsBggDialogOpen}>
          <AlertDialogContent>
            <AlertDialogHeader>
              <AlertDialogTitle>Gioco non disponibile</AlertDialogTitle>
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
                    Questo gioco non è ancora nel nostro catalogo. Solo i giochi presenti nel
                    catalogo possono essere aggiunti alla tua libreria personale.
                  </span>
                </div>
                <p className="text-sm text-muted-foreground">
                  Contatta un amministratore se desideri che questo gioco venga aggiunto al
                  catalogo.
                </p>
              </AlertDialogDescription>
            </AlertDialogHeader>
            <AlertDialogFooter>
              <AlertDialogCancel>Chiudi</AlertDialogCancel>
            </AlertDialogFooter>
          </AlertDialogContent>
        </AlertDialog>
      </div>
    </div>
  );
}
