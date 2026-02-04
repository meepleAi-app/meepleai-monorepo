/**
 * Game Detail Page - Flippable Card Design (Issue #3522)
 *
 * Public game detail page with:
 * - TOP: Flippable 3D card (front: cover/stats, back: description/metadata)
 * - BOTTOM: User-specific sections (visible only when authenticated)
 *
 * Data Sources:
 * - Public: SharedGameDetail from shared-games API
 * - Authenticated: Library status, stats, notes from library API
 *
 * Design: Warm, tactile board game aesthetic with smooth flip animation
 */

'use client';

import { useEffect, useState, useCallback, useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  AlertCircle,
  ArrowLeft,
  RotateCcw,
  Star,
  Users,
  Clock,
  Gauge,
  Tag,
  Cog,
  User as UserIcon,
  Paintbrush,
  Heart,
  BookOpen,
  Trash2,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { useParams } from 'next/navigation';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api, SharedGameDetail } from '@/lib/api';
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

  // Core game data
  const [gameDetail, setGameDetail] = useState<SharedGameDetail | null>(null);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Card flip state
  const [isFlipped, setIsFlipped] = useState(false);

  // User-specific state (authenticated users only)
  const [notes, setNotes] = useState('');
  const [isFavorite, setIsFavorite] = useState(false);

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
  const handleFlip = useCallback(() => {
    setIsFlipped((prev) => !prev);
  }, []);

  const handleSaveNotes = useCallback(() => {
    if (!gameId) return;

    try {
      const savedNotes = localStorage.getItem(NOTES_STORAGE_KEY);
      const allNotes: GameNotes = savedNotes ? JSON.parse(savedNotes) : {};
      // eslint-disable-next-line security/detect-object-injection
      allNotes[gameId] = notes;
      localStorage.setItem(NOTES_STORAGE_KEY, JSON.stringify(allNotes));
      alert('Note salvate con successo!');
    } catch (err) {
      logger.error(
        'Failed to save notes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameDetailPage', 'saveNotes', { gameId })
      );
      alert('Errore nel salvare le note');
    }
  }, [gameId, notes]);

  const toggleFavorite = useCallback(() => {
    setIsFavorite((prev) => !prev);
    // TODO: Persist to backend when authenticated
  }, []);

  // Memoized stats
  const formattedStats = useMemo(() => {
    if (!gameDetail) return null;

    return {
      playerCount:
        gameDetail.minPlayers && gameDetail.maxPlayers
          ? gameDetail.minPlayers === gameDetail.maxPlayers
            ? `${gameDetail.minPlayers}`
            : `${gameDetail.minPlayers}-${gameDetail.maxPlayers}`
          : 'N/A',
      playtime: gameDetail.playingTimeMinutes ? `${gameDetail.playingTimeMinutes} min` : 'N/A',
      complexity: gameDetail.complexityRating
        ? `${gameDetail.complexityRating.toFixed(1)}/5`
        : 'N/A',
      rating: gameDetail.averageRating ? gameDetail.averageRating.toFixed(1) : 'N/A',
    };
  }, [gameDetail]);

  // ============================================================================
  // Loading & Error States
  // ============================================================================

  if (loading) {
    return (
      <div className="min-h-screen bg-[#FAF8F5] py-8 px-4">
        <div className="container mx-auto max-w-7xl">
          <Skeleton className="h-8 w-48 mb-6" />
          <Skeleton className="h-[600px] w-full max-w-2xl mx-auto" />
        </div>
      </div>
    );
  }

  if (error || !gameDetail) {
    return (
      <div className="min-h-screen bg-[#FAF8F5] py-8 px-4">
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
    <div className="min-h-screen bg-[#FAF8F5] py-8 px-4">
      <div className="container mx-auto max-w-7xl">
        {/* Back Button */}
        <Button asChild variant="ghost" className="mb-6 font-nunito">
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" /> Torna al Catalogo
          </Link>
        </Button>

        {/* ========== TOP SECTION: Flippable Game Card ========== */}
        <div className="flex justify-center mb-12">
          <div className="w-full max-w-2xl">
            {/* Flip Button */}
            <div className="flex justify-end mb-4">
              <Button
                onClick={handleFlip}
                variant="outline"
                className="font-nunito shadow-lg hover:shadow-xl transition-all duration-300"
              >
                <RotateCcw className="mr-2 h-4 w-4" />
                {isFlipped ? 'Mostra Fronte' : 'Mostra Retro'}
              </Button>
            </div>

            {/* 3D Flippable Card */}
            <div
              className="relative w-full"
              style={{
                perspective: '1500px',
                minHeight: '600px',
              }}
            >
              <motion.div
                className="relative w-full h-full"
                animate={{ rotateY: isFlipped ? 180 : 0 }}
                transition={{
                  duration: 0.8,
                  type: 'spring',
                  stiffness: 80,
                  damping: 15,
                }}
                style={{
                  transformStyle: 'preserve-3d',
                  minHeight: '600px',
                }}
              >
                {/* ========== FRONT CARD ========== */}
                <motion.div
                  className="absolute inset-0 w-full"
                  style={{
                    backfaceVisibility: 'hidden',
                    WebkitBackfaceVisibility: 'hidden',
                  }}
                >
                  <Card
                    className={cn(
                      'border-l-4 border-l-[hsl(25,95%,38%)] overflow-hidden h-full',
                      'bg-white/95 backdrop-blur-sm shadow-2xl',
                      'hover:shadow-3xl transition-shadow duration-300'
                    )}
                    style={{
                      backgroundImage: `
                        linear-gradient(135deg, rgba(255,255,255,0.95) 0%, rgba(250,248,245,0.98) 100%),
                        url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%23f97316' fill-opacity='0.02'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E")
                      `,
                    }}
                  >
                    {/* Cover Image */}
                    <div className="relative h-80 bg-gradient-to-br from-orange-50 to-purple-50 overflow-hidden">
                      <Image
                        src={gameDetail.imageUrl || '/placeholder-game.svg'}
                        alt={gameDetail.title}
                        fill
                        className="object-cover"
                        sizes="(max-width: 768px) 100vw, 800px"
                        priority
                      />
                      <div className="absolute inset-0 bg-gradient-to-t from-black/40 via-transparent to-transparent" />

                      {/* BGG Badge Overlay */}
                      {gameDetail.bggId && (
                        <Badge
                          variant="secondary"
                          className="absolute top-4 right-4 shadow-lg backdrop-blur-sm bg-white/90"
                        >
                          BGG #{gameDetail.bggId}
                        </Badge>
                      )}
                    </div>

                    {/* Card Content */}
                    <CardContent className="p-8 space-y-6">
                      {/* Title & Publisher */}
                      <div className="space-y-2">
                        <h1 className="font-quicksand text-4xl font-bold text-gray-900 leading-tight">
                          {gameDetail.title}
                        </h1>
                        <div className="flex items-center gap-3 text-sm text-gray-600 font-nunito">
                          {gameDetail.publishers && gameDetail.publishers.length > 0 && (
                            <span>{gameDetail.publishers[0].name}</span>
                          )}
                          {gameDetail.yearPublished && (
                            <>
                              <span>•</span>
                              <span>{gameDetail.yearPublished}</span>
                            </>
                          )}
                        </div>
                      </div>

                      {/* BGG Rating */}
                      {formattedStats && gameDetail.averageRating && (
                        <div className="flex items-center gap-3 py-3 px-4 bg-orange-50/50 rounded-xl border border-orange-100">
                          <Star className="h-6 w-6 text-[hsl(25,95%,38%)] fill-[hsl(25,95%,38%)]" />
                          <div className="flex-1">
                            <div className="font-quicksand text-2xl font-bold text-gray-900">
                              {formattedStats.rating}
                            </div>
                            <div className="text-xs text-gray-600 font-nunito">BGG Rating</div>
                          </div>
                        </div>
                      )}

                      {/* Quick Stats Grid */}
                      {formattedStats && (
                        <div className="grid grid-cols-3 gap-4">
                          {/* Players */}
                          <div className="flex flex-col items-center gap-2 p-4 bg-purple-50/30 rounded-xl">
                            <Users className="h-5 w-5 text-[hsl(262,83%,62%)]" />
                            <div className="font-quicksand font-bold text-lg text-gray-900">
                              {formattedStats.playerCount}
                            </div>
                            <div className="text-xs text-gray-600 font-nunito">Giocatori</div>
                          </div>

                          {/* Duration */}
                          <div className="flex flex-col items-center gap-2 p-4 bg-orange-50/30 rounded-xl">
                            <Clock className="h-5 w-5 text-[hsl(25,95%,38%)]" />
                            <div className="font-quicksand font-bold text-lg text-gray-900">
                              {formattedStats.playtime}
                            </div>
                            <div className="text-xs text-gray-600 font-nunito">Durata</div>
                          </div>

                          {/* Complexity */}
                          {gameDetail.complexityRating && (
                            <div className="flex flex-col items-center gap-2 p-4 bg-purple-50/30 rounded-xl">
                              <Gauge className="h-5 w-5 text-[hsl(262,83%,62%)]" />
                              <div className="font-quicksand font-bold text-lg text-gray-900">
                                {formattedStats.complexity}
                              </div>
                              <div className="text-xs text-gray-600 font-nunito">Complessità</div>
                            </div>
                          )}
                        </div>
                      )}
                    </CardContent>
                  </Card>
                </motion.div>

                {/* ========== BACK CARD ========== */}
                <motion.div
                  className="absolute inset-0 w-full"
                  style={{
                    backfaceVisibility: 'hidden',
                    WebkitBackfaceVisibility: 'hidden',
                    transform: 'rotateY(180deg)',
                  }}
                >
                  <Card
                    className={cn(
                      'border-l-4 border-l-[hsl(262,83%,62%)] overflow-hidden h-full',
                      'bg-white/95 backdrop-blur-sm shadow-2xl'
                    )}
                    style={{
                      backgroundImage: `
                        linear-gradient(135deg, rgba(255,255,255,0.95) 0%, rgba(250,248,245,0.98) 100%),
                        url("data:image/svg+xml,%3Csvg width='60' height='60' viewBox='0 0 60 60' xmlns='http://www.w3.org/2000/svg'%3E%3Cg fill='none' fill-rule='evenodd'%3E%3Cg fill='%239333ea' fill-opacity='0.02'%3E%3Cpath d='M36 34v-4h-2v4h-4v2h4v4h2v-4h4v-2h-4zm0-30V0h-2v4h-4v2h4v4h2V6h4V4h-4zM6 34v-4H4v4H0v2h4v4h2v-4h4v-2H6zM6 4V0H4v4H0v2h4v4h2V6h4V4H6z'/%3E%3C/g%3E%3C/g%3E%3C/svg%3E")
                      `,
                    }}
                  >
                    <CardContent className="p-8 space-y-6 overflow-y-auto max-h-[600px]">
                      {/* Description */}
                      <div className="space-y-3">
                        <h2 className="font-quicksand text-2xl font-bold text-gray-900">
                          Descrizione
                        </h2>
                        <p className="font-nunito text-gray-700 leading-relaxed">
                          {gameDetail.description}
                        </p>
                      </div>

                      {/* Categories */}
                      {gameDetail.categories && gameDetail.categories.length > 0 && (
                        <div className="space-y-3">
                          <div className="flex items-center gap-2">
                            <Tag className="h-5 w-5 text-[hsl(25,95%,38%)]" />
                            <h3 className="font-quicksand text-lg font-semibold text-gray-900">
                              Categorie
                            </h3>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            {gameDetail.categories.map((cat) => (
                              <Badge
                                key={cat.id}
                                variant="secondary"
                                className="bg-orange-100 text-orange-900 hover:bg-orange-200 font-nunito"
                              >
                                {cat.name}
                              </Badge>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Mechanics */}
                      {gameDetail.mechanics && gameDetail.mechanics.length > 0 && (
                        <div className="space-y-3">
                          <div className="flex items-center gap-2">
                            <Cog className="h-5 w-5 text-[hsl(262,83%,62%)]" />
                            <h3 className="font-quicksand text-lg font-semibold text-gray-900">
                              Meccaniche
                            </h3>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            {gameDetail.mechanics.map((mech) => (
                              <Badge
                                key={mech.id}
                                variant="outline"
                                className="border-purple-300 text-purple-900 font-nunito"
                              >
                                {mech.name}
                              </Badge>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Designers */}
                      {gameDetail.designers && gameDetail.designers.length > 0 && (
                        <div className="space-y-3">
                          <div className="flex items-center gap-2">
                            <UserIcon className="h-5 w-5 text-gray-600" />
                            <h3 className="font-quicksand text-lg font-semibold text-gray-900">
                              Designer
                            </h3>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            {gameDetail.designers.map((designer) => (
                              <Badge key={designer.id} variant="secondary" className="font-nunito">
                                {designer.name}
                              </Badge>
                            ))}
                          </div>
                        </div>
                      )}

                      {/* Publishers */}
                      {gameDetail.publishers && gameDetail.publishers.length > 0 && (
                        <div className="space-y-3">
                          <div className="flex items-center gap-2">
                            <Paintbrush className="h-5 w-5 text-gray-600" />
                            <h3 className="font-quicksand text-lg font-semibold text-gray-900">
                              Editori
                            </h3>
                          </div>
                          <div className="flex flex-wrap gap-2">
                            {gameDetail.publishers.map((pub) => (
                              <Badge key={pub.id} variant="secondary" className="font-nunito">
                                {pub.name}
                              </Badge>
                            ))}
                          </div>
                        </div>
                      )}
                    </CardContent>
                  </Card>
                </motion.div>
              </motion.div>
            </div>
          </div>
        </div>

        {/* ========== BOTTOM SECTION: User Info & Actions ========== */}
        {/* TODO: Show only when authenticated - placeholder for now */}
        <div className="grid md:grid-cols-2 gap-6 max-w-4xl mx-auto">
          {/* Left Column: Collection Status & Actions */}
          <Card className="border-l-4 border-l-[hsl(25,95%,38%)] shadow-lg">
            <CardHeader>
              <CardTitle className="font-quicksand text-xl">Azioni Rapide</CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              {/* Favorite Toggle */}
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

              {/* Add to Collection */}
              <Button variant="outline" className="w-full font-nunito">
                <BookOpen className="mr-2 h-4 w-4" />
                Aggiungi alla Collezione
              </Button>

              {/* Remove from Collection */}
              <Button variant="destructive" className="w-full font-nunito">
                <Trash2 className="mr-2 h-4 w-4" />
                Rimuovi dalla Collezione
              </Button>
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
                  Accedi per visualizzare le tue statistiche di gioco
                </AlertDescription>
              </Alert>
            </CardContent>
          </Card>
        </div>
      </div>
    </div>
  );
}
