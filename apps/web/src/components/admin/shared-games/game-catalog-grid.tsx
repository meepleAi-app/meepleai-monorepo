/**
 * GameCatalogGrid - Admin Shared Games Catalog
 * Issue #4909 - Uniform MeepleCard UI across dashboard, /games and admin
 *
 * Replaces mock data with real API data via useSharedGames.
 * Uses MeepleCard directly with admin-specific quick actions.
 * Status badge is integrated in MeepleCard (not external overlay).
 */

'use client';

import { useCallback, useState } from 'react';

import { ArchiveRestore, Clock, Pencil, Share2, Trash2, Upload, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useMutation, useQueryClient } from '@tanstack/react-query';

import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Sheet, SheetContent } from '@/components/ui/navigation/sheet';
import { sharedGamesKeys, useSharedGames } from '@/hooks/queries';
import { api } from '@/lib/api';
import type { SharedGame } from '@/lib/api';
import type { ResolvedNavigationLink } from '@/config/entity-navigation';
import type { GameStatus } from '@/lib/api/schemas/shared-games.schemas';

import { AdminSharedGameCardContainer } from './AdminSharedGameCardContainer';

// ============================================================================
// Helpers
// ============================================================================

function formatPlayers(
  min: number | null | undefined,
  max: number | null | undefined,
): string {
  if (!min && !max) return 'N/A';
  if (min === max) return `${min}`;
  return `${min ?? '?'}-${max ?? '?'}`;
}

function formatPlaytime(minutes: number | null | undefined): string {
  if (!minutes) return 'N/A';
  if (minutes >= 60) {
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return mins > 0 ? `${hours}h ${mins}m` : `${hours}h`;
  }
  return `${minutes}m`;
}

const STATUS_LABELS: Record<GameStatus, string> = {
  Draft: 'Bozza',
  PendingApproval: 'In Attesa',
  Published: 'Pubblicato',
  Archived: 'Archiviato',
};

// ============================================================================
// Admin Game Card
// ============================================================================

interface AdminGameCardProps {
  game: SharedGame;
  onPublish: (id: string) => void;
  onArchive: (id: string) => void;
  onDelete: (id: string) => void;
  onOpenExtraCard: (id: string) => void;
}

function AdminGameCard({ game, onPublish, onArchive, onDelete, onOpenExtraCard }: AdminGameCardProps) {
  const router = useRouter();

  const metadata = [
    { icon: Users, value: formatPlayers(game.minPlayers, game.maxPlayers) },
    { icon: Clock, value: formatPlaytime(game.playingTimeMinutes) },
  ];

  const quickActions = [
    {
      icon: Pencil,
      label: 'Modifica',
      onClick: () => router.push(`/admin/shared-games/${game.id}`),
    },
    game.status === 'Published'
      ? {
          icon: ArchiveRestore,
          label: 'Archivia',
          onClick: () => onArchive(game.id),
        }
      : {
          icon: Upload,
          label: 'Pubblica',
          onClick: () => onPublish(game.id),
        },
    {
      icon: Trash2,
      label: 'Elimina',
      onClick: () => onDelete(game.id),
    },
    {
      icon: Share2,
      label: 'Condividi',
      onClick: () => {
        navigator.clipboard?.writeText(`${window.location.origin}/games/${game.id}`);
      },
    },
  ];

  const navigateTo: ResolvedNavigationLink[] = [
    {
      entity: 'document',
      label: 'Info',
      onClick: () => onOpenExtraCard(game.id),
    },
  ];

  return (
    <MeepleCard
      id={game.id}
      entity="game"
      variant="grid"
      title={game.title}
      subtitle={game.yearPublished ? String(game.yearPublished) : undefined}
      imageUrl={game.imageUrl || undefined}
      rating={game.averageRating || undefined}
      ratingMax={10}
      metadata={metadata}
      badge={STATUS_LABELS[game.status]}
      onClick={() => router.push(`/admin/shared-games/${game.id}`)}
      entityQuickActions={quickActions}
      navigateTo={navigateTo}
      showInfoButton
      infoHref={`/admin/shared-games/${game.id}`}
      infoTooltip="Dettaglio admin"
      data-testid={`admin-game-card-${game.id}`}
    />
  );
}

// ============================================================================
// GameCatalogGrid
// ============================================================================

export function GameCatalogGrid() {
  const queryClient = useQueryClient();
  const { data, isLoading } = useSharedGames({ pageSize: 50 });
  const games = data?.items ?? [];

  // ExtraCard sheet state
  const [sheetGameId, setSheetGameId] = useState<string | null>(null);

  const publishMutation = useMutation({
    mutationFn: (id: string) => api.sharedGames.publish(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: sharedGamesKeys.lists() }),
  });

  const archiveMutation = useMutation({
    mutationFn: (id: string) => api.sharedGames.archive(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: sharedGamesKeys.lists() }),
  });

  const deleteMutation = useMutation({
    mutationFn: (id: string) => api.sharedGames.delete(id),
    onSuccess: () => queryClient.invalidateQueries({ queryKey: sharedGamesKeys.lists() }),
  });

  const handlePublish = useCallback((id: string) => publishMutation.mutate(id), [publishMutation]);
  const handleArchive = useCallback((id: string) => archiveMutation.mutate(id), [archiveMutation]);
  const handleDelete = useCallback((id: string) => deleteMutation.mutate(id), [deleteMutation]);
  const handleOpenExtraCard = useCallback((id: string) => setSheetGameId(id), []);

  const published = games.filter((g) => g.status === 'Published').length;
  const draft = games.filter((g) => g.status === 'Draft').length;

  return (
    <div className="space-y-6">
      {/* Stats Summary */}
      <div className="grid grid-cols-1 md:grid-cols-3 gap-4">
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Totale</div>
          <div className="text-2xl font-bold text-slate-900 dark:text-zinc-100">
            {isLoading ? '—' : games.length}
          </div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Pubblicati</div>
          <div className="text-2xl font-bold text-green-600 dark:text-green-400">
            {isLoading ? '—' : published}
          </div>
        </div>
        <div className="bg-white/70 dark:bg-zinc-800/70 backdrop-blur-md rounded-lg p-4 border border-slate-200/50 dark:border-zinc-700/50">
          <div className="text-sm text-slate-600 dark:text-zinc-400">Bozze</div>
          <div className="text-2xl font-bold text-amber-600 dark:text-amber-400">
            {isLoading ? '—' : draft}
          </div>
        </div>
      </div>

      {/* Game Grid */}
      {isLoading ? (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {[...Array(6)].map((_, i) => (
            <MeepleCard
              key={i}
              entity="game"
              variant="grid"
              title=""
              loading
              data-testid="admin-game-card-skeleton"
            />
          ))}
        </div>
      ) : games.length === 0 ? (
        <div className="text-center py-12 text-muted-foreground">
          <p className="text-lg mb-2">Nessun gioco nel catalogo</p>
          <p className="text-sm">Aggiungi il primo gioco al catalogo condiviso.</p>
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {games.map((game) => (
            <AdminGameCard
              key={game.id}
              game={game}
              onPublish={handlePublish}
              onArchive={handleArchive}
              onDelete={handleDelete}
              onOpenExtraCard={handleOpenExtraCard}
            />
          ))}
        </div>
      )}

      {/* ExtraCard Sheet */}
      <Sheet open={!!sheetGameId} onOpenChange={(open) => { if (!open) setSheetGameId(null); }}>
        <SheetContent side="right" className="w-[640px] sm:max-w-[640px] p-0 overflow-y-auto">
          {sheetGameId && (
            <AdminSharedGameCardContainer
              gameId={sheetGameId}
              onClose={() => setSheetGameId(null)}
            />
          )}
        </SheetContent>
      </Sheet>
    </div>
  );
}
