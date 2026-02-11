/**
 * @deprecated Use `MeepleCard` with `flippable` + `flipData` props instead.
 * This component is superseded by the unified MeepleCard flip feature.
 * See: `@/components/ui/data-display/meeple-card`
 *
 * GameDetailHero Component (Issue #3513)
 *
 * Flippable game card with:
 * - Front: Cover image, title, publisher/year, BGG rating, quick stats
 * - Back: Description, categories, mechanics, designers
 *
 * Uses Framer Motion for smooth 3D flip animation.
 * Follows MeepleAI design system with warm beige background and orange accents.
 */

'use client';

import { useState, useMemo, useCallback } from 'react';

import { motion } from 'framer-motion';
import {
  Clock,
  RotateCcw,
  Star,
  Users,
  Gauge,
  Tag,
  Cog,
  User,
  Paintbrush,
} from 'lucide-react';
import Image from 'next/image';

import { Button } from '@/components/ui/primitives/button';
import type { LibraryGameDetail } from '@/hooks/queries/useLibrary';
import { cn } from '@/lib/utils';

export interface GameDetailHeroProps {
  gameDetail: LibraryGameDetail;
}

export function GameDetailHero({ gameDetail }: GameDetailHeroProps) {
  const [isFlipped, setIsFlipped] = useState(false);

  const handleFlip = useCallback(() => {
    setIsFlipped((prev) => !prev);
  }, []);

  const handleKeyDown = useCallback(
    (e: React.KeyboardEvent) => {
      if (e.key === 'Enter' || e.key === ' ') {
        e.preventDefault();
        handleFlip();
      }
    },
    [handleFlip]
  );

  // Memoize formatted stats to avoid recalculation on every render
  const formattedStats = useMemo(
    () => ({
      playerCount:
        gameDetail.minPlayers && gameDetail.maxPlayers
          ? gameDetail.minPlayers === gameDetail.maxPlayers
            ? `${gameDetail.minPlayers}`
            : `${gameDetail.minPlayers}-${gameDetail.maxPlayers}`
          : 'N/A',
      playtime: gameDetail.playingTimeMinutes
        ? `${gameDetail.playingTimeMinutes} min`
        : 'N/A',
      complexity: gameDetail.complexityRating
        ? `${gameDetail.complexityRating.toFixed(1)}/5`
        : 'N/A',
      rating: gameDetail.averageRating
        ? gameDetail.averageRating.toFixed(1)
        : 'N/A',
    }),
    [
      gameDetail.minPlayers,
      gameDetail.maxPlayers,
      gameDetail.playingTimeMinutes,
      gameDetail.complexityRating,
      gameDetail.averageRating,
    ]
  );

  return (
    <div
      className="w-full max-w-[420px] flex-shrink-0 cursor-pointer"
      style={{ perspective: '2000px' }}
      role="button"
      tabIndex={0}
      aria-label={`Carta del gioco ${gameDetail.gameTitle}. Premi Invio o clicca per ${isFlipped ? 'vedere il fronte' : 'vedere i dettagli sul retro'}.`}
      onClick={handleFlip}
      onKeyDown={handleKeyDown}
    >
      <motion.div
        className="relative w-full"
        style={{
          aspectRatio: '3 / 4',
          transformStyle: 'preserve-3d',
        }}
        animate={{ rotateY: isFlipped ? 180 : 0 }}
        transition={{ duration: 0.8, ease: [0.4, 0, 0.2, 1] }}
      >
        {/* Card Front */}
        <div
          className={cn(
            'absolute inset-0 overflow-hidden rounded-3xl',
            'border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]',
            'flex flex-col'
          )}
          style={{
            backfaceVisibility: 'hidden',
            boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
          }}
        >
          {/* Orange left border indicator */}
          <div className="absolute bottom-0 left-0 top-0 w-[5px] rounded-l-3xl bg-gradient-to-b from-[hsl(25,95%,38%)] to-[hsl(25,95%,48%)]" />

          {/* Cover Image */}
          <div className="relative h-1/2 overflow-hidden bg-gradient-to-br from-[#F5F0E8] to-[#FAF8F5]">
            {gameDetail.gameImageUrl ? (
              <Image
                src={gameDetail.gameImageUrl}
                alt={gameDetail.gameTitle}
                fill
                priority
                className="object-cover transition-transform duration-500 hover:scale-105"
                sizes="(max-width: 640px) 100vw, (max-width: 1024px) 50vw, 420px"
              />
            ) : (
              <div className="flex h-full items-center justify-center">
                <span className="text-6xl opacity-30">🎲</span>
              </div>
            )}

            {/* Flip indicator */}
            <div className="absolute bottom-3 right-3 rounded-full bg-white/90 p-2 shadow-md backdrop-blur-sm">
              <RotateCcw className="h-4 w-4 text-[#6B665C]" />
            </div>
          </div>

          {/* Info Section */}
          <div className="flex flex-1 flex-col p-6 pl-8">
            {/* Title */}
            <h1 className="mb-1 line-clamp-2 font-quicksand text-2xl font-bold text-[#2D2A26]">
              {gameDetail.gameTitle}
            </h1>

            {/* Publisher & Year */}
            <p className="mb-3 font-nunito text-sm text-[#6B665C]">
              {gameDetail.gamePublisher ?? 'Publisher sconosciuto'}
              {gameDetail.gameYearPublished && ` (${gameDetail.gameYearPublished})`}
            </p>

            {/* BGG Rating */}
            <div className="mb-4 flex items-center gap-2">
              <div className="flex items-center gap-1.5 rounded-lg bg-[hsla(25,95%,38%,0.1)] px-3 py-1.5">
                <Star className="h-4 w-4 fill-[hsl(25,95%,38%)] text-[hsl(25,95%,38%)]" />
                <span className="font-quicksand text-lg font-bold text-[hsl(25,95%,38%)]">
                  {formattedStats.rating}
                </span>
              </div>
              <span className="font-nunito text-sm text-[#9C958A]">BGG Rating</span>
            </div>

            {/* Quick Stats Grid */}
            <div className="mt-auto grid grid-cols-3 gap-2">
              {/* Players */}
              <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-3 text-center">
                <div className="mb-1 flex items-center justify-center gap-1.5 text-[#6B665C]">
                  <Users className="h-4 w-4" />
                  <span className="font-nunito text-xs">Giocatori</span>
                </div>
                <p className="font-quicksand text-lg font-bold text-[#2D2A26]">
                  {formattedStats.playerCount}
                </p>
              </div>

              {/* Duration */}
              <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-3 text-center">
                <div className="mb-1 flex items-center justify-center gap-1.5 text-[#6B665C]">
                  <Clock className="h-4 w-4" />
                  <span className="font-nunito text-xs">Durata</span>
                </div>
                <p className="font-quicksand text-lg font-bold text-[#2D2A26]">
                  {formattedStats.playtime}
                </p>
              </div>

              {/* Complexity */}
              <div className="rounded-xl bg-[rgba(45,42,38,0.04)] p-3 text-center">
                <div className="mb-1 flex items-center justify-center gap-1.5 text-[#6B665C]">
                  <Gauge className="h-4 w-4" />
                  <span className="font-nunito text-xs">Complessità</span>
                </div>
                <p className="font-quicksand text-lg font-bold text-[#2D2A26]">
                  {formattedStats.complexity}
                </p>
              </div>
            </div>
          </div>
        </div>

        {/* Card Back */}
        <div
          className={cn(
            'absolute inset-0 overflow-hidden rounded-3xl',
            'border border-[rgba(45,42,38,0.08)] bg-[#FFFDF9]',
            'flex flex-col'
          )}
          style={{
            backfaceVisibility: 'hidden',
            transform: 'rotateY(180deg)',
            boxShadow: '0 8px 32px rgba(45, 42, 38, 0.12)',
          }}
        >
          {/* Purple left border indicator for back */}
          <div className="absolute bottom-0 left-0 top-0 w-[5px] rounded-l-3xl bg-gradient-to-b from-[hsl(262,83%,62%)] to-[hsl(262,83%,72%)]" />

          {/* Back content */}
          <div className="flex flex-1 flex-col overflow-y-auto p-6 pl-8">
            {/* Title */}
            <div className="mb-4 flex items-center justify-between">
              <h2 className="font-quicksand text-xl font-bold text-[#2D2A26]">
                Informazioni
              </h2>
              <Button
                variant="ghost"
                size="sm"
                className="text-[#6B665C] hover:text-[hsl(262,83%,62%)]"
                onClick={(e) => {
                  e.stopPropagation();
                  handleFlip();
                }}
              >
                <RotateCcw className="mr-1 h-4 w-4" />
                Gira
              </Button>
            </div>

            {/* Description */}
            {gameDetail.description && (
              <div className="mb-4">
                <h3 className="mb-2 flex items-center gap-2 font-quicksand text-sm font-semibold uppercase tracking-wider text-[#9C958A]">
                  Descrizione
                </h3>
                <p className="line-clamp-4 font-nunito text-sm leading-relaxed text-[#6B665C]">
                  {gameDetail.description}
                </p>
              </div>
            )}

            {/* Categories */}
            {gameDetail.categories && gameDetail.categories.length > 0 && (
              <div className="mb-4">
                <h3 className="mb-2 flex items-center gap-2 font-quicksand text-sm font-semibold uppercase tracking-wider text-[#9C958A]">
                  <Tag className="h-3.5 w-3.5" />
                  Categorie
                </h3>
                <div className="flex flex-wrap gap-1.5">
                  {gameDetail.categories.slice(0, 5).map((cat) => (
                    <span
                      key={cat.id}
                      className="rounded-lg bg-[hsla(262,83%,62%,0.1)] px-2.5 py-1 font-nunito text-xs font-medium text-[hsl(262,83%,62%)]"
                    >
                      {cat.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Mechanics */}
            {gameDetail.mechanics && gameDetail.mechanics.length > 0 && (
              <div className="mb-4">
                <h3 className="mb-2 flex items-center gap-2 font-quicksand text-sm font-semibold uppercase tracking-wider text-[#9C958A]">
                  <Cog className="h-3.5 w-3.5" />
                  Meccaniche
                </h3>
                <div className="flex flex-wrap gap-1.5">
                  {gameDetail.mechanics.slice(0, 5).map((mech) => (
                    <span
                      key={mech.id}
                      className="rounded-lg bg-[hsla(25,95%,38%,0.1)] px-2.5 py-1 font-nunito text-xs font-medium text-[hsl(25,95%,38%)]"
                    >
                      {mech.name}
                    </span>
                  ))}
                </div>
              </div>
            )}

            {/* Designers */}
            {gameDetail.designers && gameDetail.designers.length > 0 && (
              <div className="mb-4">
                <h3 className="mb-2 flex items-center gap-2 font-quicksand text-sm font-semibold uppercase tracking-wider text-[#9C958A]">
                  <User className="h-3.5 w-3.5" />
                  Designer
                </h3>
                <p className="font-nunito text-sm text-[#6B665C]">
                  {gameDetail.designers.map((d) => d.name).join(', ')}
                </p>
              </div>
            )}

            {/* Publishers */}
            {gameDetail.publishers && gameDetail.publishers.length > 0 && (
              <div className="mb-4">
                <h3 className="mb-2 flex items-center gap-2 font-quicksand text-sm font-semibold uppercase tracking-wider text-[#9C958A]">
                  <Paintbrush className="h-3.5 w-3.5" />
                  Editori
                </h3>
                <p className="font-nunito text-sm text-[#6B665C]">
                  {gameDetail.publishers.map((p) => p.name).join(', ')}
                </p>
              </div>
            )}

            {/* Min Age */}
            {gameDetail.minAge && (
              <div className="mt-auto">
                <span className="rounded-lg bg-[rgba(45,42,38,0.04)] px-3 py-1.5 font-nunito text-sm text-[#6B665C]">
                  Età minima: {gameDetail.minAge}+
                </span>
              </div>
            )}
          </div>
        </div>
      </motion.div>

      {/* Flip hint */}
      <p className="mt-3 text-center font-nunito text-sm text-[#9C958A]">
        Clicca per girare la carta
      </p>
    </div>
  );
}
