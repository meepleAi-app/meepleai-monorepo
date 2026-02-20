/**
 * Game Detail Page (Issue #3522)
 *
 * Public game detail page with:
 * - TOP: MeepleCard (hero, flippable) + MeepleInfoCard (readOnly for public, KB+Social)
 * - BOTTOM: User-specific sections (visible only when authenticated)
 *
 * Data Sources:
 * - Public: SharedGameDetail from shared-games API
 * - Authenticated: Favorite toggle, notes (localStorage)
 */

'use client';

import { useEffect, useState, useCallback, useMemo } from 'react';

import {
  AlertCircle,
  ArrowLeft,
  BookOpen,
  Clock,
  Gauge,
  Heart,
  Loader2,
  MessageCircle,
  Trash2,
  Users,
} from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { toast } from '@/components/layout/Toast';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { MeepleInfoCard } from '@/components/ui/data-display/meeple-info-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import {
  useGameInLibraryStatus,
  useAddGameToLibrary,
  useRemoveGameFromLibrary,
} from '@/hooks/queries';
import { useEntityNavigation } from '@/hooks/useEntityNavigation';
import { api, type SharedGameDetail } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';
import { cn } from '@/lib/utils';

// ============================================================================
// Types & Constants
// ============================================================================

const NOTES_STORAGE_KEY = 'meepleai_game_notes';

interface GameNotes {
  [gameId: string]: string;
}

// ============================================================================
// Main Component
// ============================================================================

export default function GameDetailPage() {
  const params = useParams();
  const gameId = params?.id as string | undefined;
  const { user } = useAuth();
  const isAuthenticated = !!user;

  // Library status & mutations
  const { data: libraryStatus, isLoading: statusLoading } = useGameInLibraryStatus(
    gameId ?? '',
    isAuthenticated && !!gameId
  );
  const addToLibrary = useAddGameToLibrary();
  const removeFromLibrary = useRemoveGameFromLibrary();
  const inLibrary = libraryStatus?.inLibrary ?? false;

  // Core game data
  const [gameDetail, setGameDetail] = useState<SharedGameDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // User-specific state (authenticated users only)
  const [notes, setNotes] = useState('');
  const [isFavorite, setIsFavorite] = useState(false);

  // Entity navigation links (KB, Agents, Chats, Sessions)
  const navigationLinks = useEntityNavigation('game', { id: gameId });

  // Load game data from shared-games API
  useEffect(() => {
    if (!gameId) return;

    const loadGame = async () => {
      setLoading(true);
      setError(null);
      try {
        const data = await api.sharedGames.getById(gameId);
        if (!data) {
          throw new Error('Game not found');
        }
        setGameDetail(data);
      } catch (err) {
        setError('Failed to load game');
        logger.error(
          'Failed to load game details',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadGame', { gameId })
        );
      } finally {
        setLoading(false);
      }
    };

    loadGame();
  }, [gameId]);

  // Load notes from localStorage
  useEffect(() => {
    if (!gameId) return;

    try {
      const savedNotes = localStorage.getItem(NOTES_STORAGE_KEY);
      if (savedNotes) {
        const allNotes: GameNotes = JSON.parse(savedNotes);
        // eslint-disable-next-line security/detect-object-injection
        setNotes(allNotes[gameId] || '');
      }
    } catch (err) {
      logger.error(
        'Failed to load notes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameDetailPage', 'loadNotes', { gameId })
      );
    }
  }, [gameId]);

  // Handlers
  const handleSaveNotes = useCallback(() => {
    if (!gameId) return;

    try {
      const savedNotes = localStorage.getItem(NOTES_STORAGE_KEY);
      const allNotes: GameNotes = savedNotes ? JSON.parse(savedNotes) : {};
      // eslint-disable-next-line security/detect-object-injection
      allNotes[gameId] = notes;
      localStorage.setItem(NOTES_STORAGE_KEY, JSON.stringify(allNotes));
    } catch (err) {
      logger.error(
        'Failed to save notes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameDetailPage', 'saveNotes', { gameId })
      );
    }
  }, [gameId, notes]);

  const toggleFavorite = useCallback(() => {
    setIsFavorite((prev) => !prev);
  }, []);

  const handleAddToLibrary = useCallback(async () => {
    if (!gameId) return;
    try {
      await addToLibrary.mutateAsync({ gameId });
      toast.success('Gioco aggiunto alla collezione!');
    } catch {
      toast.error('Errore durante l\'aggiunta alla collezione');
    }
  }, [gameId, addToLibrary]);

  const handleRemoveFromLibrary = useCallback(async () => {
    if (!gameId) return;
    try {
      await removeFromLibrary.mutateAsync(gameId);
      toast.success('Gioco rimosso dalla collezione');
    } catch {
      toast.error('Errore durante la rimozione dalla collezione');
    }
  }, [gameId, removeFromLibrary]);

  // Build metadata for MeepleCard
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

  // ============================================================================
  // Loading & Error States
  // ============================================================================

  if (loading) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Skeleton className="h-8 w-48 mb-6" />
          <Skeleton className="h-[600px] w-full max-w-2xl mx-auto" />
        </div>
      </div>
    );
  }

  if (error || !gameDetail) {
    return (
      <div className="min-h-screen bg-background py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error || 'Game not found'}</AlertDescription>
          </Alert>
          <Button asChild className="mt-4">
            <Link href="/games">
              <ArrowLeft className="mr-2 h-4 w-4" /> Torna al Catalogo
            </Link>
          </Button>
        </div>
      </div>
    );
  }

  // ============================================================================
  // Main Render
  // ============================================================================

  return (
    <div className="min-h-screen bg-background py-8 px-4">
      <div className="container mx-auto max-w-7xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna al Catalogo
          </Link>
        </Button>

        {/* Cards Section - MeepleCard (hero, flippable) + MeepleInfoCard */}
        <section className="mb-12 flex flex-col items-center justify-center gap-6 lg:flex-row lg:items-start">
          {/* Flippable Game Card */}
          <MeepleCard
            entity="game"
            variant="hero"
            title={gameDetail.title}
            subtitle={
              (gameDetail.publishers && gameDetail.publishers.length > 0
                ? gameDetail.publishers[0].name
                : '') +
              (gameDetail.yearPublished ? ` (${gameDetail.yearPublished})` : '')
            }
            imageUrl={gameDetail.imageUrl || undefined}
            rating={gameDetail.averageRating ?? undefined}
            ratingMax={10}
            metadata={metadata}
            navigateTo={navigationLinks}
            flippable
            flipData={{
              description: gameDetail.description || undefined,
              categories: gameDetail.categories,
              mechanics: gameDetail.mechanics,
              designers: gameDetail.designers,
              publishers: gameDetail.publishers,
              complexityRating: gameDetail.complexityRating,
              minAge: gameDetail.minAge,
            }}
          />

          {/* Info Card - KB & Social (readOnly for public page) */}
          <MeepleInfoCard
            gameId={gameDetail.id}
            gameTitle={gameDetail.title}
            bggId={gameDetail.bggId}
            readOnly={!isAuthenticated}
          />
        </section>

        {/* ========== BOTTOM SECTION: User Info & Actions (authenticated only) ========== */}
        {isAuthenticated && (
          <>
            <div className="grid md:grid-cols-2 gap-6 max-w-4xl mx-auto">
              {/* Left Column: Collection Status & Actions */}
              <Card className="border-l-4 border-l-[hsl(25,95%,38%)] shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl">Azioni Rapide</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <Button asChild className="w-full font-nunito bg-[hsl(25,95%,38%)] hover:bg-[hsl(25,95%,32%)]">
                    <Link href={`/chat/new?game=${gameId}`}>
                      <MessageCircle className="mr-2 h-4 w-4" />
                      💬 Chat con AI
                    </Link>
                  </Button>

                  <Button
                    onClick={toggleFavorite}
                    variant={isFavorite ? 'default' : 'outline'}
                    className="w-full font-nunito"
                  >
                    <Heart
                      className={cn(
                        'mr-2 h-4 w-4',
                        isFavorite && 'fill-white'
                      )}
                    />
                    {isFavorite ? 'Rimuovi dai Preferiti' : 'Aggiungi ai Preferiti'}
                  </Button>

                  {!inLibrary ? (
                    <Button
                      variant="outline"
                      className="w-full font-nunito"
                      onClick={handleAddToLibrary}
                      disabled={addToLibrary.isPending || statusLoading}
                    >
                      {addToLibrary.isPending ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <BookOpen className="mr-2 h-4 w-4" />
                      )}
                      Aggiungi alla Collezione
                    </Button>
                  ) : (
                    <Button
                      variant="destructive"
                      className="w-full font-nunito"
                      onClick={handleRemoveFromLibrary}
                      disabled={removeFromLibrary.isPending || statusLoading}
                    >
                      {removeFromLibrary.isPending ? (
                        <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                      ) : (
                        <Trash2 className="mr-2 h-4 w-4" />
                      )}
                      Rimuovi dalla Collezione
                    </Button>
                  )}
                </CardContent>
              </Card>

              {/* Right Column: Notes */}
              <Card className="border-l-4 border-l-[hsl(262,83%,62%)] shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl">Note Personali</CardTitle>
                </CardHeader>
                <CardContent className="space-y-4">
                  <Textarea
                    placeholder="Scrivi strategie, regole custom, impressioni..."
                    value={notes}
                    onChange={(e) => setNotes(e.target.value)}
                    rows={6}
                    className="resize-none font-nunito"
                  />
                  <Button onClick={handleSaveNotes} className="w-full font-nunito">
                    Salva Note
                  </Button>
                </CardContent>
              </Card>
            </div>

            {/* Statistiche Partite - Placeholder */}
            <div className="mt-6 max-w-4xl mx-auto">
              <Card className="shadow-lg">
                <CardHeader>
                  <CardTitle className="font-quicksand text-xl">Statistiche Partite</CardTitle>
                </CardHeader>
                <CardContent>
                  <Alert>
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription className="font-nunito">
                      Le statistiche verranno visualizzate qui una volta registrate le prime partite.
                    </AlertDescription>
                  </Alert>
                </CardContent>
              </Card>
            </div>
          </>
        )}
      </div>
    </div>
  );
}
