/**
 * GameCatalogGrid - Admin Shared Games Catalog
 * Issue #4909 - Uniform MeepleCard UI across dashboard, /games and admin
 *
 * Replaces mock data with real API data via useSharedGames.
 * Uses MeepleCard directly with admin-specific quick actions.
 * Status badge is integrated in MeepleCard (not external overlay).
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import {
  ArchiveRestore,
  CheckSquare,
  Clock,
  Pencil,
  Share2,
  Trash2,
  Upload,
  Users,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { BulkActionBar } from '@/components/admin/BulkActionBar';
import { MeepleCard } from '@/components/ui/data-display/meeple-card';
import { Sheet, SheetContent, SheetTitle } from '@/components/ui/navigation/sheet';
import {
  AlertDialog,
  AlertDialogAction,
  AlertDialogCancel,
  AlertDialogContent,
  AlertDialogDescription,
  AlertDialogFooter,
  AlertDialogHeader,
  AlertDialogTitle,
} from '@/components/ui/overlays/alert-dialog-primitives';
import type { ResolvedNavigationLink } from '@/config/entity-navigation';
import { sharedGamesKeys } from '@/hooks/queries';
import { api } from '@/lib/api';
import type { SharedGame } from '@/lib/api';
import type { GameStatus } from '@/lib/api/schemas/shared-games.schemas';

import { AdminSharedGameCardContainer } from './AdminSharedGameCardContainer';

// ============================================================================
// Helpers
// ============================================================================

function formatPlayers(min: number | null | undefined, max: number | null | undefined): string {
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

function AdminGameCard({
  game,
  onPublish,
  onArchive,
  onDelete,
  onOpenExtraCard,
}: AdminGameCardProps) {
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
      entity: 'kb',
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

export interface GameCatalogGridProps {
  searchQuery?: string;
  categoryFilter?: string;
  statusFilter?: string;
  playersFilter?: string;
}

export function GameCatalogGrid({
  searchQuery = '',
  categoryFilter = 'all',
  statusFilter = 'all',
  playersFilter = 'all',
}: GameCatalogGridProps) {
  const queryClient = useQueryClient();
  const { data, isLoading } = useQuery({
    queryKey: [...sharedGamesKeys.all, 'admin-list'],
    queryFn: () => api.sharedGames.getAll({ pageSize: 100 }),
    staleTime: 2 * 60 * 1000,
  });

  const allGames = data?.items ?? [];

  // Client-side filtering
  const games = allGames.filter(game => {
    // Search filter (title, description)
    if (searchQuery) {
      const q = searchQuery.toLowerCase();
      const titleMatch = game.title?.toLowerCase().includes(q);
      const descMatch = game.description?.toLowerCase().includes(q);
      if (!titleMatch && !descMatch) return false;
    }

    // Status filter
    if (statusFilter !== 'all') {
      const statusMap: Record<string, string> = {
        published: 'Published',
        pending: 'PendingApproval',
        draft: 'Draft',
        archived: 'Archived',
      };
      if (game.status !== statusMap[statusFilter]) return false;
    }

    // Players filter
    if (playersFilter !== 'all') {
      const min = game.minPlayers ?? 0;
      const max = game.maxPlayers ?? 99;
      if (playersFilter === '1-2' && min > 2) return false;
      if (playersFilter === '3-4' && (max < 3 || min > 4)) return false;
      if (playersFilter === '5+' && max < 5) return false;
    }

    // Category filter — not available in list view DTO (SharedGame),
    // only in SharedGameDetail. Skip for now until API supports server-side filtering.

    return true;
  });

  // Selection state for bulk actions
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Clear selection when filters change to avoid bulk actions on hidden games
  useEffect(() => {
    setSelectedIds(new Set());
  }, [searchQuery, categoryFilter, statusFilter, playersFilter]);

  const toggleSelection = useCallback((id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) next.delete(id);
      else next.add(id);
      return next;
    });
  }, []);

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

  // Confirmation dialog state for destructive bulk actions
  const [confirmAction, setConfirmAction] = useState<{
    type: 'publish' | 'archive' | 'delete';
    ids: string[];
  } | null>(null);

  // Bulk action mutations (proper lifecycle, isPending, error handling)
  const bulkPublishMutation = useMutation({
    mutationFn: (ids: string[]) =>
      Promise.all(ids.map(id => api.sharedGames.publish(id).catch(() => id))),
    onSuccess: (results, ids) => {
      const failed = results.filter(r => typeof r === 'string');
      queryClient.invalidateQueries({ queryKey: sharedGamesKeys.lists() });
      setSelectedIds(new Set());
      toast.success(`${ids.length - failed.length} giochi pubblicati`);
      if (failed.length > 0) toast.error(`${failed.length} pubblicazioni fallite`);
    },
    onError: () => toast.error('Errore nella pubblicazione'),
  });

  const bulkArchiveMutation = useMutation({
    mutationFn: (ids: string[]) =>
      Promise.all(ids.map(id => api.sharedGames.archive(id).catch(() => id))),
    onSuccess: (results, ids) => {
      const failed = results.filter(r => typeof r === 'string');
      queryClient.invalidateQueries({ queryKey: sharedGamesKeys.lists() });
      setSelectedIds(new Set());
      toast.success(`${ids.length - failed.length} giochi archiviati`);
      if (failed.length > 0) toast.error(`${failed.length} archiviazioni fallite`);
    },
    onError: () => toast.error("Errore nell'archiviazione"),
  });

  const bulkDeleteMutation = useMutation({
    mutationFn: (ids: string[]) =>
      Promise.all(ids.map(id => api.sharedGames.delete(id).catch(() => id))),
    onSuccess: (results, ids) => {
      const failed = results.filter(r => typeof r === 'string');
      queryClient.invalidateQueries({ queryKey: sharedGamesKeys.lists() });
      setSelectedIds(new Set());
      toast.success(`${ids.length - failed.length} giochi eliminati`);
      if (failed.length > 0) toast.error(`${failed.length} eliminazioni fallite`);
    },
    onError: () => toast.error("Errore nell'eliminazione"),
  });

  const isBulkPending =
    bulkPublishMutation.isPending || bulkArchiveMutation.isPending || bulkDeleteMutation.isPending;

  const handleBulkPublish = useCallback(() => {
    bulkPublishMutation.mutate([...selectedIds]);
  }, [selectedIds, bulkPublishMutation]);

  const handleBulkArchive = useCallback(() => {
    setConfirmAction({ type: 'archive', ids: [...selectedIds] });
  }, [selectedIds]);

  const handleBulkDelete = useCallback(() => {
    setConfirmAction({ type: 'delete', ids: [...selectedIds] });
  }, [selectedIds]);

  const executeConfirmedAction = () => {
    if (!confirmAction) return;
    if (confirmAction.type === 'archive') bulkArchiveMutation.mutate(confirmAction.ids);
    if (confirmAction.type === 'delete') bulkDeleteMutation.mutate(confirmAction.ids);
    setConfirmAction(null);
  };

  const published = allGames.filter(g => g.status === 'Published').length;
  const draft = allGames.filter(g => g.status === 'Draft').length;
  const isFiltered =
    searchQuery || categoryFilter !== 'all' || statusFilter !== 'all' || playersFilter !== 'all';

  return (
    <div className="space-y-6">
      {/* Bulk Action Bar */}
      <BulkActionBar
        selectedCount={selectedIds.size}
        totalCount={games.length}
        itemLabel="giochi"
        itemLabelSingular="gioco"
        onClearSelection={() => setSelectedIds(new Set())}
        actions={[
          {
            id: 'publish',
            label: 'Pubblica',
            icon: Share2,
            variant: 'default',
            onClick: handleBulkPublish,
            disabled: isBulkPending,
          },
          {
            id: 'archive',
            label: 'Archivia',
            icon: ArchiveRestore,
            variant: 'outline',
            onClick: handleBulkArchive,
            disabled: isBulkPending,
          },
          {
            id: 'delete',
            label: 'Elimina',
            icon: Trash2,
            variant: 'destructive',
            onClick: handleBulkDelete,
            disabled: isBulkPending,
          },
        ]}
      />

      {/* Confirmation Dialog for destructive bulk actions */}
      <AlertDialog
        open={!!confirmAction}
        onOpenChange={open => {
          if (!open) setConfirmAction(null);
        }}
      >
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>
              {confirmAction?.type === 'delete'
                ? `Eliminare ${confirmAction.ids.length} giochi?`
                : `Archiviare ${confirmAction?.ids.length} giochi?`}
            </AlertDialogTitle>
            <AlertDialogDescription>
              {confirmAction?.type === 'delete'
                ? 'Questa azione è irreversibile. I giochi selezionati verranno rimossi permanentemente dal catalogo.'
                : "I giochi selezionati verranno spostati nell'archivio e non saranno più visibili agli utenti."}
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel>Annulla</AlertDialogCancel>
            <AlertDialogAction onClick={executeConfirmedAction}>
              {confirmAction?.type === 'delete' ? 'Elimina' : 'Archivia'}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

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
          {isFiltered ? (
            <>
              <p className="text-lg mb-2">Nessun gioco corrisponde ai filtri</p>
              <p className="text-sm">Prova a modificare i criteri di ricerca.</p>
            </>
          ) : (
            <>
              <p className="text-lg mb-2">Nessun gioco nel catalogo</p>
              <p className="text-sm">Aggiungi il primo gioco al catalogo condiviso.</p>
            </>
          )}
        </div>
      ) : (
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-6">
          {games.map(game => (
            <div key={game.id} className="relative">
              {/* Selection checkbox */}
              <button
                onClick={e => {
                  e.stopPropagation();
                  toggleSelection(game.id);
                }}
                className={`absolute top-2 left-2 z-10 flex h-6 w-6 items-center justify-center rounded border transition-colors ${
                  selectedIds.has(game.id)
                    ? 'bg-primary border-primary text-primary-foreground'
                    : 'bg-white/80 dark:bg-zinc-800/80 border-slate-300 dark:border-zinc-600 hover:border-primary'
                }`}
                aria-label={`Seleziona ${game.title}`}
              >
                {selectedIds.has(game.id) && <CheckSquare className="h-4 w-4" />}
              </button>
              <AdminGameCard
                game={game}
                onPublish={handlePublish}
                onArchive={handleArchive}
                onDelete={handleDelete}
                onOpenExtraCard={handleOpenExtraCard}
              />
            </div>
          ))}
        </div>
      )}

      {/* ExtraCard Sheet */}
      <Sheet
        open={!!sheetGameId}
        onOpenChange={open => {
          if (!open) setSheetGameId(null);
        }}
      >
        <SheetContent side="right" className="w-[640px] sm:max-w-[640px] p-0 overflow-y-auto">
          <SheetTitle className="sr-only">Dettaglio gioco</SheetTitle>
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
