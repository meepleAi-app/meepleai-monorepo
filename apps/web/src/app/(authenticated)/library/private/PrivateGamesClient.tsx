/**
 * Private Games Client Component
 * Issue #4052: Browse and manage private games
 *
 * Full CRUD interface for user's private game collection.
 * Features: list, add, edit, delete, search, sort, pagination.
 */

'use client';

import { useCallback, useEffect, useRef, useState } from 'react';

import { zodResolver } from '@hookform/resolvers/zod';
import {
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  Gamepad2,
  Loader2,
  Plus,
  Search,
  SortAsc,
  SortDesc,
} from 'lucide-react';
import { useRouter } from 'next/navigation';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { AddGameDrawer } from '@/app/(authenticated)/library/AddGameDrawer';
import { type AddPrivateGameFormData } from '@/components/library/AddPrivateGameForm';
import { JourneyProgress } from '@/components/library/JourneyProgress';
import { KbStatusBadge } from '@/components/library/KbStatusBadge';
import { LibraryEmptyState } from '@/components/library/LibraryEmptyState';
import { PrivateGameActionsMenu } from '@/components/library/PrivateGameActionsMenu';
import { ProposeGameModal } from '@/components/library/ProposeGameModal';
import { WithdrawProposalButton } from '@/components/library/WithdrawProposalButton';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
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
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { useProposePrivateGame } from '@/hooks/queries/useShareRequests';
import { useTranslation } from '@/hooks/useTranslation';
import { api } from '@/lib/api';
import { ApiError, ValidationError } from '@/lib/api/core/errors';
import type {
  PrivateGameDto,
  GetPrivateGamesParams,
} from '@/lib/api/schemas/private-games.schemas';
import { logger } from '@/lib/logger';

type SortByOption = 'title' | 'createdAt' | 'updatedAt';
type SortDirection = 'asc' | 'desc';

export default function PrivateGamesClient() {
  const router = useRouter();
  const { t } = useTranslation();

  // Data state
  const [games, setGames] = useState<PrivateGameDto[]>([]);
  const [totalCount, setTotalCount] = useState(0);
  const [totalPages, setTotalPages] = useState(0);
  const [hasNextPage, setHasNextPage] = useState(false);
  const [hasPreviousPage, setHasPreviousPage] = useState(false);

  // UI state
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);
  const [page, setPage] = useState(1);
  const [search, setSearch] = useState('');
  const [sortBy, setSortBy] = useState<SortByOption>('createdAt');
  const [sortDirection, setSortDirection] = useState<SortDirection>('desc');

  // Drawer state
  const [addDrawerOpen, setAddDrawerOpen] = useState(false);

  // Dialog state
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [proposeDialogOpen, setProposeDialogOpen] = useState(false);
  const [selectedGame, setSelectedGame] = useState<PrivateGameDto | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  // Propose mutation. #5074: the proposal route of the private game; CreateShareRequest knows only shared games
  // and answered 404 here.
  const proposePrivateGame = useProposePrivateGame();

  const PAGE_SIZE = 12;

  const loadGames = useCallback(
    async (params?: Partial<GetPrivateGamesParams>) => {
      setLoading(true);
      setError(null);
      try {
        const response = await api.library.getPrivateGames({
          page: params?.page ?? page,
          pageSize: PAGE_SIZE,
          search: params?.search ?? (search || undefined),
          sortBy: params?.sortBy ?? sortBy,
          sortDirection: params?.sortDirection ?? sortDirection,
        });
        setGames(response.items);
        setTotalCount(response.totalCount);
        setTotalPages(response.totalPages);
        setHasNextPage(response.hasNextPage);
        setHasPreviousPage(response.hasPreviousPage);
      } catch (err) {
        logger.error('Failed to load private games:', err);
        setError(t('privateGames.loadError'));
      } finally {
        setLoading(false);
      }
    },
    [page, search, sortBy, sortDirection, t]
  );

  // Initial load
  useEffect(() => {
    loadGames();
  }, []); // eslint-disable-line react-hooks/exhaustive-deps

  // Handlers
  const handleSearch = (value: string) => {
    setSearch(value);
    setPage(1);
    loadGames({ search: value || undefined, page: 1 });
  };

  const handleSortChange = (newSortBy: SortByOption) => {
    setSortBy(newSortBy);
    setPage(1);
    loadGames({ sortBy: newSortBy, page: 1 });
  };

  const handleSortDirectionToggle = () => {
    const newDir = sortDirection === 'asc' ? 'desc' : 'asc';
    setSortDirection(newDir);
    setPage(1);
    loadGames({ sortDirection: newDir, page: 1 });
  };

  const handlePageChange = (newPage: number) => {
    setPage(newPage);
    loadGames({ page: newPage });
  };

  const handleEditGame = async (data: AddPrivateGameFormData) => {
    if (!selectedGame) return;
    setIsSubmitting(true);
    try {
      await api.library.updatePrivateGame(selectedGame.id, {
        title: data.title,
        minPlayers: data.minPlayers,
        maxPlayers: data.maxPlayers,
        yearPublished: data.yearPublished ?? null,
        playingTimeMinutes: data.playingTimeMinutes ?? null,
        minAge: data.minAge ?? null,
        complexityRating: data.complexityRating ?? null,
        description: data.description ?? null,
      });
      setEditDialogOpen(false);
      setSelectedGame(null);
      await loadGames();
    } catch (err) {
      logger.error('Failed to update private game:', err);
      throw err;
    } finally {
      setIsSubmitting(false);
    }
  };

  // #5079: why the last deletion was refused, shown in the dialog. It closed on click and the error went only to the
  // log; with a proposal open the game is not deleted (decision of 2026-11-19), and the user is told so.
  const [deleteProblem, setDeleteProblem] = useState<'proposal' | 'withdrawn' | 'failed' | null>(
    null
  );

  const handleDeleteGame = async () => {
    if (!selectedGame) return;
    setIsDeleting(true);
    setDeleteProblem(null);
    try {
      await api.library.deletePrivateGame(selectedGame.id);
      setDeleteDialogOpen(false);
      setSelectedGame(null);
      await loadGames();
    } catch (err) {
      logger.error('Failed to delete private game:', err);
      setDeleteProblem(
        err instanceof ApiError && err.code === 'proposal_already_open' ? 'proposal' : 'failed'
      );
    } finally {
      setIsDeleting(false);
    }
  };

  // #5081: the card's actions menu that opened a dialog. The dialogs are controlled and have no trigger of their own,
  // so on close Radix gives the focus back to nothing: they give it back here, while the button is still on the page.
  const returnFocusRef = useRef<HTMLElement | null>(null);
  const returnFocus = (event: Event) => {
    const trigger = returnFocusRef.current;
    if (trigger?.isConnected) {
      event.preventDefault();
      trigger.focus();
    }
  };

  const openEdit = (game: PrivateGameDto, trigger?: HTMLElement | null) => {
    if (trigger !== undefined) returnFocusRef.current = trigger;
    setSelectedGame(game);
    setEditDialogOpen(true);
  };

  const openDelete = (gameId: string, trigger?: HTMLElement | null) => {
    const game = games.find(g => g.id === gameId);
    if (game) {
      if (trigger !== undefined) returnFocusRef.current = trigger;
      setDeleteProblem(null);
      setSelectedGame(game);
      setDeleteDialogOpen(true);
    }
  };

  const openPropose = (game: PrivateGameDto, trigger?: HTMLElement | null) => {
    if (trigger !== undefined) returnFocusRef.current = trigger;
    setSelectedGame(game);
    setProposeDialogOpen(true);
  };

  const handlePropose = async (gameId: string, notes: string) => {
    await proposePrivateGame.mutateAsync({ privateGameId: gameId, notes: notes || undefined });
    setProposeDialogOpen(false);
    setSelectedGame(null);
  };

  return (
    <div className="container mx-auto py-4 space-y-4">
      {/* Journey Progress Banner — pass the most recently created private game so the later steps
          evaluate. Sort by createdAt desc (independent of the user's display sort order) so the
          banner always tracks the game the user most recently added, not whichever game is first in
          the current display sort (Issue #5217).
          Issue #4138: `agentDefinitionId` non serve piu` — il passo "crea agente" e' uscito. */}
      {(() => {
        const recentGame =
          games.length > 0
            ? [...games].sort((a, b) => b.createdAt.localeCompare(a.createdAt))[0]
            : undefined;
        return <JourneyProgress gameId={recentGame?.id} />;
      })()}

      {/* Compact header — subtitle + add button */}
      <div className="flex items-center justify-between gap-3">
        <p className="text-sm text-muted-foreground">
          {t('privateGames.manageCollection')} ({t('privateGames.subtitle', { count: totalCount })})
        </p>
        <Button size="sm" onClick={() => setAddDrawerOpen(true)} data-testid="add-private-game-btn">
          <Plus className="h-4 w-4 mr-2" />
          {t('privateGames.addGame')}
        </Button>
      </div>

      {/* Search and Sort Controls */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1">
          <Search
            className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground"
            aria-hidden="true"
          />
          <Input
            placeholder={t('privateGames.searchPlaceholder')}
            value={search}
            onChange={e => handleSearch(e.target.value)}
            className="pl-10"
            aria-label="Search private games"
            data-testid="search-input"
          />
        </div>
        <div className="flex gap-2">
          <Select value={sortBy} onValueChange={(v: string) => handleSortChange(v as SortByOption)}>
            <SelectTrigger className="w-[160px]" aria-label="Sort by">
              <SelectValue />
            </SelectTrigger>
            <SelectContent>
              <SelectItem value="createdAt">{t('privateGames.sortDateAdded')}</SelectItem>
              <SelectItem value="updatedAt">{t('privateGames.sortLastUpdated')}</SelectItem>
              <SelectItem value="title">{t('privateGames.sortTitle')}</SelectItem>
            </SelectContent>
          </Select>
          <Button
            variant="outline"
            size="icon"
            onClick={handleSortDirectionToggle}
            aria-label={`Sort ${sortDirection === 'asc' ? 'descending' : 'ascending'}`}
            data-testid="sort-direction-btn"
          >
            {sortDirection === 'asc' ? (
              <SortAsc className="h-4 w-4" />
            ) : (
              <SortDesc className="h-4 w-4" />
            )}
          </Button>
        </div>
      </div>

      {/* Content */}
      {loading ? (
        <div className="flex items-center justify-center py-16" data-testid="loading-state">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
          <span className="ml-3 text-muted-foreground">{t('privateGames.loading')}</span>
        </div>
      ) : error ? (
        <Card data-testid="error-state">
          <CardContent className="flex flex-col items-center py-12">
            <AlertTriangle className="h-12 w-12 text-destructive mb-4" />
            <p className="text-lg font-medium text-destructive">{error}</p>
            <Button variant="outline" className="mt-4" onClick={() => loadGames()}>
              {t('common.refresh')}
            </Button>
          </CardContent>
        </Card>
      ) : games.length === 0 ? (
        search ? (
          // Search returned no results — keep the simple "no results" card
          <Card data-testid="empty-state">
            <CardHeader className="text-center">
              <div className="mx-auto mb-4">
                <Gamepad2 className="h-16 w-16 text-muted-foreground" />
              </div>
              <CardTitle>{t('privateGames.noGamesFound')}</CardTitle>
              <CardDescription>{t('privateGames.noGamesMatchSearch', { search })}</CardDescription>
            </CardHeader>
          </Card>
        ) : (
          // True empty library — show the onboarding empty state (Issue #4945)
          <LibraryEmptyState />
        )
      ) : (
        <>
          {/* Games Grid */}
          <div
            className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
            data-testid="games-grid"
          >
            {games.map(game => (
              <div key={game.id} className="relative">
                <PrivateGameCard
                  game={game}
                  onEdit={openEdit}
                  onDelete={openDelete}
                  onPropose={openPropose}
                  onClick={() => router.push(`/library/private/${game.id}`)}
                />
                {/* KB indexing badge — overlays bottom of card (Issue #4946) */}
                <KbStatusBadge gameId={game.id} />
              </div>
            ))}
          </div>

          {/* Pagination */}
          {totalPages > 1 && (
            <div className="flex items-center justify-center gap-4 pt-4" data-testid="pagination">
              <Button
                variant="outline"
                size="sm"
                disabled={!hasPreviousPage}
                onClick={() => handlePageChange(page - 1)}
                aria-label="Previous page"
              >
                <ChevronLeft className="h-4 w-4 mr-1" />
                {t('common.previous')}
              </Button>
              <span className="text-sm text-muted-foreground">
                {t('privateGames.pageOf', { page, totalPages })}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={!hasNextPage}
                onClick={() => handlePageChange(page + 1)}
                aria-label="Next page"
              >
                {t('common.next')}
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </div>
          )}
        </>
      )}

      {/* Edit Game Dialog */}
      <Dialog
        open={editDialogOpen}
        onOpenChange={(open: boolean) => {
          setEditDialogOpen(open);
          if (!open) setSelectedGame(null);
        }}
      >
        <DialogContent
          className="max-w-lg max-h-[90vh] overflow-y-auto"
          onCloseAutoFocus={returnFocus}
        >
          <DialogHeader>
            <DialogTitle>{t('privateGames.editGame')}</DialogTitle>
            <DialogDescription>
              {t('privateGames.editDescription', { title: selectedGame?.title ?? '' })}
            </DialogDescription>
          </DialogHeader>
          {selectedGame && (
            <EditPrivateGameForm
              game={selectedGame}
              onSubmit={handleEditGame}
              onCancel={() => {
                setEditDialogOpen(false);
                setSelectedGame(null);
              }}
              isSubmitting={isSubmitting}
            />
          )}
        </DialogContent>
      </Dialog>

      {/* Delete Confirmation Dialog */}
      <AlertDialog
        open={deleteDialogOpen}
        onOpenChange={open => {
          setDeleteDialogOpen(open);
          if (!open) {
            setSelectedGame(null);
            setDeleteProblem(null);
          }
        }}
      >
        <AlertDialogContent onCloseAutoFocus={returnFocus}>
          <AlertDialogHeader>
            <AlertDialogTitle>{t('privateGames.deleteGame')}</AlertDialogTitle>
            <AlertDialogDescription>
              {t('privateGames.deleteConfirm', { title: selectedGame?.title ?? '' })}
            </AlertDialogDescription>
          </AlertDialogHeader>
          {deleteProblem === 'proposal' && selectedGame && (
            <div className="space-y-2" data-testid="delete-blocked-by-proposal">
              <p role="alert" className="text-sm text-destructive">
                {t('privateGames.deleteBlockedByProposal')}
              </p>
              <WithdrawProposalButton
                gameId={selectedGame.id}
                onWithdrawn={() => setDeleteProblem('withdrawn')}
              />
            </div>
          )}
          {deleteProblem === 'withdrawn' && (
            <p role="status" className="text-sm text-muted-foreground">
              {t('privateGames.proposalWithdrawn')}
            </p>
          )}
          {deleteProblem === 'failed' && (
            <p role="alert" className="text-sm text-destructive">
              {t('privateGames.deleteFailed')}
            </p>
          )}
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeleting}>{t('common.cancel')}</AlertDialogCancel>
            <AlertDialogAction
              onClick={event => {
                // Radix closes the dialog on click: keep it open until the deletion answers.
                event.preventDefault();
                void handleDeleteGame();
              }}
              disabled={isDeleting}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              data-testid="confirm-delete-btn"
            >
              {isDeleting ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  {t('privateGames.deleting')}
                </>
              ) : (
                t('common.delete')
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>

      {/* Propose Game Modal (Issue #4054) */}
      <ProposeGameModal
        isOpen={proposeDialogOpen}
        onClose={() => {
          setProposeDialogOpen(false);
          setSelectedGame(null);
        }}
        game={selectedGame}
        onPropose={handlePropose}
        onEditGame={game => openEdit(game)}
        onCloseAutoFocus={returnFocus}
      />

      {/* Add Game Drawer — local instance so close stays on /library/private */}
      <AddGameDrawer
        open={addDrawerOpen}
        onClose={() => {
          setAddDrawerOpen(false);
          setPage(1);
          void loadGames({ page: 1 });
        }}
      />
    </div>
  );
}

// ---------------------------------------------------------------------------
// PrivateGameCard — inlined from the former PrivateGameCard.tsx wrapper
// (refactor: remove thin wrapper, Task 12)
// ---------------------------------------------------------------------------

interface PrivateGameCardProps {
  game: PrivateGameDto;
  onEdit?: (game: PrivateGameDto, trigger?: HTMLElement | null) => void;
  onDelete?: (gameId: string, trigger?: HTMLElement | null) => void;
  onPropose?: (game: PrivateGameDto, trigger?: HTMLElement | null) => void;
  onClick?: () => void;
}

function PrivateGameCard({ game, onEdit, onDelete, onPropose, onClick }: PrivateGameCardProps) {
  const subtitle = [
    `${game.minPlayers}-${game.maxPlayers} players`,
    game.yearPublished ? `(${game.yearPublished})` : null,
    game.playingTimeMinutes ? `${game.playingTimeMinutes} min` : null,
  ]
    .filter(Boolean)
    .join(' \u00b7 ');

  const metadata: MeepleCardMetadata[] = [
    { label: `${game.minPlayers}-${game.maxPlayers}` },
    ...(game.playingTimeMinutes ? [{ label: `${game.playingTimeMinutes} min` }] : []),
    ...(game.yearPublished ? [{ label: `${game.yearPublished}` }] : []),
  ];

  // #5081: the actions go in the card's actionsSlot. Passed as `actions` without `showQuickActions`, the grid card
  // drew only its decorative menu placeholder, and none of them was reachable (#1877, 2026-06-04).
  const hasActions = Boolean(onEdit || onPropose || onDelete);

  return (
    <MeepleCard
      entity="game"
      variant="grid"
      title={game.title}
      subtitle={subtitle}
      imageUrl={game.imageUrl || undefined}
      badge="Private"
      metadata={metadata}
      onClick={onClick}
      actionsSlot={
        hasActions ? (
          <PrivateGameActionsMenu
            // eslint-disable-next-line local/prefer-use-game-title -- a private game has no catalog translations: the menu names it as its owner wrote it
            title={game.title}
            onEdit={onEdit ? trigger => onEdit(game, trigger) : undefined}
            onPropose={onPropose ? trigger => onPropose(game, trigger) : undefined}
            onDelete={onDelete ? trigger => onDelete(game.id, trigger) : undefined}
          />
        ) : undefined
      }
      data-testid={`game-card-${game.id}`}
    />
  );
}

/**
 * Edit form wrapper that reuses AddPrivateGameForm with initial values.
 * Provides defaultValues from the selected game for editing.
 */
function EditPrivateGameForm({
  game,
  onSubmit,
  onCancel,
  isSubmitting,
}: {
  game: PrivateGameDto;
  onSubmit: (data: AddPrivateGameFormData) => Promise<void>;
  onCancel: () => void;
  isSubmitting: boolean;
}) {
  return (
    <EditPrivateGameFormInner
      key={game.id}
      game={game}
      onSubmit={onSubmit}
      onCancel={onCancel}
      isSubmitting={isSubmitting}
    />
  );
}

// #5088: an empty optional number is `undefined`, which the schema accepts and the update sends as null. With
// `valueAsNumber` the empty input became NaN: the schema refused it without showing it, and Save did nothing.
const optionalNumber = (value: unknown): number | undefined =>
  value === '' || value == null ? undefined : Number(value);

const EditFormSchema = z
  .object({
    title: z.string().min(1, 'Title is required').max(200, 'Title too long'),
    minPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
    maxPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
    yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
    playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
    minAge: z.number().int().min(0).max(99).nullable().optional(),
    complexityRating: z.number().min(0).max(5).nullable().optional(),
    description: z.string().max(5000, 'Description too long').nullable().optional(),
  })
  .refine(data => data.maxPlayers >= data.minPlayers, {
    message: 'Max players must be >= min players',
    path: ['maxPlayers'],
  });

function EditPrivateGameFormInner({
  game,
  onSubmit,
  onCancel,
  isSubmitting,
}: {
  game: PrivateGameDto;
  onSubmit: (data: AddPrivateGameFormData) => Promise<void>;
  onCancel: () => void;
  isSubmitting: boolean;
}) {
  const { t } = useTranslation();
  // #5079: the server's refusal, shown instead of going only to the log. With a proposal open the description cannot
  // be removed (decision of 2026-11-19): the form says so on the field, and offers to withdraw the proposal.
  const [saveProblem, setSaveProblem] = useState<'proposal' | 'withdrawn' | 'failed' | null>(null);
  const {
    register,
    handleSubmit,
    setError,
    clearErrors,
    formState: { errors },
  } = useForm<AddPrivateGameFormData>({
    resolver: zodResolver(EditFormSchema),
    defaultValues: {
      title: game.title,
      minPlayers: game.minPlayers,
      maxPlayers: game.maxPlayers,
      yearPublished: game.yearPublished ?? undefined,
      playingTimeMinutes: game.playingTimeMinutes ?? undefined,
      minAge: game.minAge ?? undefined,
      complexityRating: game.complexityRating ?? undefined,
      description: game.description ?? undefined,
    },
  });

  const onSubmitForm = async (data: AddPrivateGameFormData) => {
    setSaveProblem(null);
    try {
      await onSubmit(data);
    } catch (error) {
      logger.error('Form submission error:', error);
      const onDescription =
        error instanceof ValidationError &&
        Boolean(error.validationErrors?.Description ?? error.validationErrors?.description);
      if (onDescription) {
        setError('description', {
          type: 'server',
          message: t('privateGames.descriptionNeededByProposal'),
        });
        setSaveProblem('proposal');
      } else {
        setSaveProblem('failed');
      }
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmitForm)} className="space-y-6">
      <div className="space-y-2">
        <Label htmlFor="edit-title">
          {t('privateGameForm.title')} <span className="text-destructive">*</span>
        </Label>
        <Input id="edit-title" {...register('title')} disabled={isSubmitting} />
        {errors.title && <p className="text-sm text-destructive">{errors.title.message}</p>}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="edit-minPlayers">
            {t('privateGameForm.minPlayers')} <span className="text-destructive">*</span>
          </Label>
          <Input
            id="edit-minPlayers"
            type="number"
            min="1"
            max="99"
            {...register('minPlayers', { valueAsNumber: true })}
            disabled={isSubmitting}
          />
          {errors.minPlayers && (
            <p className="text-sm text-destructive">{errors.minPlayers.message}</p>
          )}
        </div>
        <div className="space-y-2">
          <Label htmlFor="edit-maxPlayers">
            {t('privateGameForm.maxPlayers')} <span className="text-destructive">*</span>
          </Label>
          <Input
            id="edit-maxPlayers"
            type="number"
            min="1"
            max="99"
            {...register('maxPlayers', { valueAsNumber: true })}
            disabled={isSubmitting}
          />
          {errors.maxPlayers && (
            <p className="text-sm text-destructive">{errors.maxPlayers.message}</p>
          )}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="edit-yearPublished">{t('privateGameForm.yearPublished')}</Label>
          <Input
            id="edit-yearPublished"
            type="number"
            min="1900"
            max="2100"
            {...register('yearPublished', { setValueAs: optionalNumber })}
            disabled={isSubmitting}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="edit-playingTimeMinutes">{t('privateGameForm.playingTime')}</Label>
          <Input
            id="edit-playingTimeMinutes"
            type="number"
            min="1"
            max="10000"
            {...register('playingTimeMinutes', { setValueAs: optionalNumber })}
            disabled={isSubmitting}
          />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="edit-minAge">{t('privateGameForm.minAge')}</Label>
          <Input
            id="edit-minAge"
            type="number"
            min="0"
            max="99"
            {...register('minAge', { setValueAs: optionalNumber })}
            disabled={isSubmitting}
          />
        </div>
        <div className="space-y-2">
          <Label htmlFor="edit-complexityRating">{t('privateGameForm.complexity')}</Label>
          <Input
            id="edit-complexityRating"
            type="number"
            min="0"
            max="5"
            step="0.1"
            {...register('complexityRating', { setValueAs: optionalNumber })}
            disabled={isSubmitting}
          />
        </div>
      </div>

      <div className="space-y-2">
        <Label htmlFor="edit-description">{t('privateGameForm.description')}</Label>
        <Textarea
          id="edit-description"
          {...register('description')}
          rows={4}
          disabled={isSubmitting}
          aria-invalid={errors.description ? true : undefined}
          aria-describedby={errors.description ? 'edit-description-error' : undefined}
        />
        {errors.description && (
          <p id="edit-description-error" role="alert" className="text-sm text-destructive">
            {errors.description.message}
          </p>
        )}
        {saveProblem === 'proposal' && (
          <WithdrawProposalButton
            gameId={game.id}
            onWithdrawn={() => {
              clearErrors('description');
              setSaveProblem('withdrawn');
            }}
          />
        )}
        {saveProblem === 'withdrawn' && (
          <p role="status" className="text-sm text-muted-foreground">
            {t('privateGames.proposalWithdrawn')}
          </p>
        )}
      </div>

      {saveProblem === 'failed' && (
        <p role="alert" className="text-sm text-destructive">
          {t('privateGames.saveFailed')}
        </p>
      )}

      <div className="flex justify-end gap-3 pt-4">
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          {t('common.cancel')}
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? t('privateGames.saving') : t('privateGames.saveChanges')}
        </Button>
      </div>
    </form>
  );
}
