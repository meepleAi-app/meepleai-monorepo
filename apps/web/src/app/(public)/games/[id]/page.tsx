/**
 * Game Preview Page (public)
 *
 * Minimal preview for unauthenticated visitors. Shows a read-only MeepleCard
 * hero and context-sensitive CTA based on auth / library state.
 *
 * Full game experience (zones, drawer, toolkit) is at /library/games/[gameId]
 * for authenticated users.
 */

'use client';

import { useEffect, useState, useCallback, useMemo } from 'react';

import {
  AlertCircle,
  ArrowLeft,
  BookOpen,
  Clock,
  ExternalLink,
  Gauge,
  Loader2,
  Users,
} from 'lucide-react';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { useAuth } from '@/components/auth/AuthProvider';
import { MechanicIcon } from '@/components/icons/mechanics/MechanicIcon';
import { toast } from '@/components/layout/Toast';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useGameInLibraryStatus, useAddGameToLibrary } from '@/hooks/queries';
import { api, type SharedGameDetail } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

// ============================================================================
// Main Component
// ============================================================================

export default function GamePreviewPage() {
  const params = useParams();
  const gameId = params?.id as string | undefined;
  const { user } = useAuth();
  const isAuthenticated = !!user;

  // Library status & add mutation
  const { data: libraryStatus, isLoading: statusLoading } = useGameInLibraryStatus(
    gameId ?? '',
    isAuthenticated && !!gameId
  );
  const addToLibrary = useAddGameToLibrary();
  const inLibrary = libraryStatus?.inLibrary ?? false;

  // Game data
  const [gameDetail, setGameDetail] = useState<SharedGameDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

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
          createErrorContext('GamePreviewPage', 'loadGame', { gameId })
        );
      } finally {
        setLoading(false);
      }
    };

    loadGame();
  }, [gameId]);

  const handleAddToLibrary = useCallback(async () => {
    if (!gameId) return;
    try {
      await addToLibrary.mutateAsync({ gameId });
      toast.success('Gioco aggiunto alla collezione!');
    } catch {
      toast.error("Errore durante l'aggiunta alla collezione");
    }
  }, [gameId, addToLibrary]);

  // Metadata chips for MeepleCard
  const metadata = useMemo(() => {
    if (!gameDetail) return [];
    const items = [];

    if (gameDetail.minPlayers && gameDetail.maxPlayers) {
      const players =
        gameDetail.minPlayers === gameDetail.maxPlayers
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

  // First mechanic icon for cover overlay
  const mechanicIconNode = useMemo(() => {
    if (!gameDetail?.mechanics || gameDetail.mechanics.length === 0) return undefined;
    const firstMechanic = gameDetail.mechanics[0];
    const slug = firstMechanic.name?.toLowerCase().replace(/\s+/g, '-') ?? '';
    return <MechanicIcon mechanic={slug} size={20} />;
  }, [gameDetail]);

  // ============================================================================
  // Loading & Error States
  // ============================================================================

  if (loading) {
    return (
      <div className="min-h-screen bg-[#0d1117] py-8 px-4">
        <div className="max-w-2xl mx-auto">
          <Skeleton className="h-8 w-48 mb-6 bg-white/10" />
          <Skeleton className="h-[520px] w-full bg-white/10 rounded-2xl" />
        </div>
      </div>
    );
  }

  if (error || !gameDetail) {
    return (
      <div className="min-h-screen bg-[#0d1117] py-8 px-4">
        <div className="max-w-2xl mx-auto">
          <Alert variant="destructive">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription>{error || 'Gioco non trovato'}</AlertDescription>
          </Alert>
          <Button asChild variant="ghost" className="mt-4 text-white/70 hover:text-white">
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

  const subtitle =
    (gameDetail.publishers && gameDetail.publishers.length > 0
      ? gameDetail.publishers[0].name
      : '') + (gameDetail.yearPublished ? ` (${gameDetail.yearPublished})` : '');

  return (
    <div className="min-h-screen bg-[#0d1117] py-8 px-4">
      <div className="max-w-2xl mx-auto">
        {/* Back Button */}
        <Button
          asChild
          variant="ghost"
          className="mb-6 font-nunito text-white/70 hover:text-white hover:bg-white/10"
        >
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna al Catalogo
          </Link>
        </Button>

        {/* Hero Card — read-only, not flippable, no action buttons */}
        <div className="flex justify-center mb-8">
          <MeepleCard
            entity="game"
            variant="hero"
            title={gameDetail.title}
            subtitle={subtitle}
            imageUrl={gameDetail.imageUrl || undefined}
            rating={gameDetail.averageRating ?? undefined}
            ratingMax={10}
            metadata={metadata}
            mechanicIcon={mechanicIconNode}
            stateLabel={{ text: 'Catalogo', variant: 'info' }}
          />
        </div>

        {/* Metadata section */}
        <div className="mb-8 space-y-4">
          {gameDetail.description && (
            <p className="text-white/70 font-nunito text-sm leading-relaxed line-clamp-4">
              {gameDetail.description}
            </p>
          )}

          {gameDetail.categories && gameDetail.categories.length > 0 && (
            <div className="flex flex-wrap gap-2">
              {gameDetail.categories.map(cat => (
                <span
                  key={cat.id ?? cat.name}
                  className="text-xs font-nunito px-2 py-1 rounded-full bg-white/10 text-white/60"
                >
                  {cat.name}
                </span>
              ))}
            </div>
          )}

          {gameDetail.designers && gameDetail.designers.length > 0 && (
            <p className="text-white/50 font-nunito text-xs">
              Design:{' '}
              {gameDetail.designers
                .slice(0, 3)
                .map(d => d.name)
                .join(', ')}
            </p>
          )}
        </div>

        {/* CTA Section */}
        <div className="border border-white/10 rounded-2xl p-6 bg-white/5 backdrop-blur-sm space-y-3">
          {!isAuthenticated && (
            <>
              <p className="text-white/70 font-nunito text-sm text-center mb-4">
                Registrati per accedere al tavolo completo con note, sessioni e AI assistant.
              </p>
              <Button
                asChild
                className="w-full font-nunito bg-[hsl(25,95%,38%)] hover:bg-[hsl(25,95%,32%)] text-white"
              >
                <Link href="/register">
                  <ExternalLink className="mr-2 h-4 w-4" />
                  Registrati per il tavolo completo
                </Link>
              </Button>
              <Button
                asChild
                variant="ghost"
                className="w-full font-nunito text-white/60 hover:text-white hover:bg-white/10"
              >
                <Link href="/login">Accedi</Link>
              </Button>
            </>
          )}

          {isAuthenticated && !inLibrary && (
            <>
              <p className="text-white/70 font-nunito text-sm text-center mb-4">
                Aggiungi questo gioco alla tua collezione per accedere al tavolo completo.
              </p>
              <Button
                onClick={handleAddToLibrary}
                disabled={addToLibrary.isPending || statusLoading}
                className="w-full font-nunito bg-[hsl(25,95%,38%)] hover:bg-[hsl(25,95%,32%)] text-white"
              >
                {addToLibrary.isPending ? (
                  <Loader2 className="mr-2 h-4 w-4 animate-spin" />
                ) : (
                  <BookOpen className="mr-2 h-4 w-4" />
                )}
                Aggiungi alla collezione
              </Button>
            </>
          )}

          {isAuthenticated && inLibrary && (
            <>
              <p className="text-white/70 font-nunito text-sm text-center mb-4">
                Questo gioco &egrave; nella tua collezione. Apri il tavolo completo.
              </p>
              <Button
                asChild
                className="w-full font-nunito bg-[hsl(25,95%,38%)] hover:bg-[hsl(25,95%,32%)] text-white"
              >
                <Link href={`/library/games/${gameId}`}>
                  <ExternalLink className="mr-2 h-4 w-4" />
                  Vai al tavolo
                </Link>
              </Button>
            </>
          )}
        </div>
      </div>
    </div>
  );
}
