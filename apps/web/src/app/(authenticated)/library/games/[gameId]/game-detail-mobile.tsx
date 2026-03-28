/**
 * GameDetailMobile — Mobile-first game detail page
 *
 * Vertical scroll layout with hero, action bar, AI section,
 * recent sessions, and game info.
 *
 * Phase 3 Task 5
 */

'use client';

import { useState } from 'react';

import { useQueryClient } from '@tanstack/react-query';
import {
  MessageCircle,
  BookOpen,
  Share2,
  Users,
  Clock,
  BarChart3,
  Loader2,
  Calendar,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { AiReadySection } from '@/components/library/AiReadySection';
import { FavoriteToggle } from '@/components/library/FavoriteToggle';
import { PdfUploadSheet } from '@/components/library/PdfUploadSheet';
import { MobileHeader } from '@/components/ui/navigation/MobileHeader';
import { GlassCard } from '@/components/ui/surfaces/GlassCard';
import { useLibraryGameDetail, libraryKeys } from '@/hooks/queries/useLibrary';

export interface GameDetailMobileProps {
  gameId: string;
}

// ---------------------------------------------------------------------------
// Helpers
// ---------------------------------------------------------------------------

function playerLabel(min: number | null, max: number | null): string | null {
  if (min == null && max == null) return null;
  if (min != null && max != null) return min === max ? `${min}` : `${min}-${max}`;
  return `${min ?? max}`;
}

function formatDate(iso: string): string {
  try {
    return new Date(iso).toLocaleDateString('it-IT', {
      day: 'numeric',
      month: 'short',
      year: 'numeric',
    });
  } catch {
    return iso;
  }
}

// ---------------------------------------------------------------------------
// Component
// ---------------------------------------------------------------------------

export default function GameDetailMobile({ gameId }: GameDetailMobileProps) {
  const router = useRouter();
  const queryClient = useQueryClient();
  const { data: game, isLoading, error } = useLibraryGameDetail(gameId);
  const [uploadOpen, setUploadOpen] = useState(false);

  // -- Loading --
  if (isLoading) {
    return (
      <div className="flex min-h-[60vh] items-center justify-center">
        <Loader2 className="h-8 w-8 animate-spin text-purple-400" />
      </div>
    );
  }

  // -- Error --
  if (error) {
    return (
      <div className="px-4 py-12 text-center">
        <p className="text-sm text-red-400">
          {error instanceof Error ? error.message : 'Errore nel caricamento del gioco.'}
        </p>
        <button onClick={() => router.back()} className="mt-4 text-sm text-purple-400 underline">
          Torna indietro
        </button>
      </div>
    );
  }

  // -- Not found --
  if (!game) {
    return (
      <div className="px-4 py-12 text-center">
        <p className="text-base font-semibold text-[var(--gaming-text-primary)]">
          Gioco non trovato
        </p>
        <p className="mt-1 text-sm text-[var(--gaming-text-secondary)]">
          Questo gioco non è presente nella tua libreria.
        </p>
        <button
          onClick={() => router.push('/library')}
          className="mt-4 text-sm text-purple-400 underline"
        >
          Torna alla Libreria
        </button>
      </div>
    );
  }

  const players = playerLabel(game.minPlayers, game.maxPlayers);
  const subtitle =
    [game.gamePublisher, game.gameYearPublished ? `(${game.gameYearPublished})` : null]
      .filter(Boolean)
      .join(' ') || undefined;

  return (
    <div className="flex flex-col" data-testid="game-detail-mobile">
      {/* Header */}
      <MobileHeader title={game.gameTitle} subtitle={subtitle} onBack={() => router.back()} />

      <div className="flex flex-col gap-4 p-4 pb-24">
        {/* ---- Hero Section ---- */}
        <GlassCard entity="game" className="overflow-hidden">
          {/* Cover image */}
          {game.gameImageUrl && (
            <div className="relative aspect-[16/9] w-full">
              <Image
                src={game.gameImageUrl}
                alt={game.gameTitle}
                fill
                className="object-cover"
                sizes="(max-width: 768px) 100vw, 600px"
                priority
              />
              <div className="absolute inset-0 bg-gradient-to-t from-black/70 to-transparent" />
            </div>
          )}

          <div className="space-y-3 p-4">
            <h2 className="text-xl font-bold text-[var(--gaming-text-primary)]">
              {game.gameTitle}
            </h2>
            {subtitle && <p className="text-sm text-[var(--gaming-text-secondary)]">{subtitle}</p>}

            {/* Mana pips / quick stats */}
            <div className="flex flex-wrap gap-3 text-xs text-[var(--gaming-text-secondary)]">
              {players && (
                <span className="flex items-center gap-1">
                  <Users className="h-3.5 w-3.5" /> {players} giocatori
                </span>
              )}
              {game.playingTimeMinutes != null && (
                <span className="flex items-center gap-1">
                  <Clock className="h-3.5 w-3.5" /> {game.playingTimeMinutes} min
                </span>
              )}
              {game.averageRating != null && (
                <span className="flex items-center gap-1">
                  <BarChart3 className="h-3.5 w-3.5" /> {game.averageRating.toFixed(1)}/10
                </span>
              )}
            </div>
          </div>
        </GlassCard>

        {/* ---- Action Bar ---- */}
        <div className="flex items-center justify-around rounded-xl bg-white/5 py-2">
          <FavoriteToggle gameId={gameId} isFavorite={game.isFavorite} gameTitle={game.gameTitle} />

          <Link
            href={`/chat?gameId=${gameId}`}
            className="flex flex-col items-center gap-0.5 text-[var(--gaming-text-secondary)] transition-colors hover:text-purple-400"
            aria-label="Chat AI"
          >
            <MessageCircle className="h-5 w-5" />
            <span className="text-[10px]">Chat AI</span>
          </Link>

          <button
            onClick={() => setUploadOpen(true)}
            className="flex flex-col items-center gap-0.5 text-[var(--gaming-text-secondary)] transition-colors hover:text-purple-400"
            aria-label="Regole"
          >
            <BookOpen className="h-5 w-5" />
            <span className="text-[10px]">Regole</span>
          </button>

          <button
            onClick={() => {
              if (navigator.share) {
                navigator.share({
                  title: game.gameTitle,
                  url: window.location.href,
                });
              }
            }}
            className="flex flex-col items-center gap-0.5 text-[var(--gaming-text-secondary)] transition-colors hover:text-purple-400"
            aria-label="Condividi"
          >
            <Share2 className="h-5 w-5" />
            <span className="text-[10px]">Condividi</span>
          </button>
        </div>

        {/* ---- AI Ready Section ---- */}
        <AiReadySection
          gameId={gameId}
          hasCustomPdf={game.hasCustomPdf}
          hasRagAccess={game.hasRagAccess}
          onUploadClick={() => setUploadOpen(true)}
        />

        {/* ---- Recent Sessions ---- */}
        {game.recentSessions && game.recentSessions.length > 0 && (
          <GlassCard entity="session" className="p-4">
            <h3 className="mb-3 text-sm font-semibold text-[var(--gaming-text-primary)]">
              Sessioni Recenti
            </h3>
            <ul className="space-y-2">
              {game.recentSessions.slice(0, 3).map(session => (
                <li
                  key={session.id}
                  className="flex items-center justify-between rounded-lg bg-white/5 px-3 py-2 text-sm"
                >
                  <div className="flex items-center gap-2 text-[var(--gaming-text-secondary)]">
                    <Calendar className="h-3.5 w-3.5" />
                    <span>{formatDate(session.playedAt)}</span>
                  </div>
                  <span className="text-xs text-[var(--gaming-text-tertiary)]">
                    {session.durationFormatted}
                  </span>
                </li>
              ))}
            </ul>
          </GlassCard>
        )}

        {/* ---- Description ---- */}
        {game.description && (
          <GlassCard className="p-4">
            <h3 className="mb-2 text-sm font-semibold text-[var(--gaming-text-primary)]">
              Descrizione
            </h3>
            <p className="text-sm leading-relaxed text-[var(--gaming-text-secondary)]">
              {game.description}
            </p>
          </GlassCard>
        )}
      </div>

      {/* ---- PDF Upload Sheet ---- */}
      <PdfUploadSheet
        open={uploadOpen}
        onOpenChange={setUploadOpen}
        gameId={gameId}
        onUploaded={() => {
          queryClient.invalidateQueries({ queryKey: libraryKeys.gameDetail(gameId) });
        }}
      />
    </div>
  );
}
