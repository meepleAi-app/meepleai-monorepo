/**
 * Private Games Client Component
 * Issue #4052: Browse and manage private games
 *
 * Full CRUD interface for user's private game collection.
 * Features: list, add, edit, delete, search, sort, pagination.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import {
  AlertTriangle,
  ChevronLeft,
  ChevronRight,
  Loader2,
  Plus,
  Search,
  SortAsc,
  SortDesc,
  Gamepad2,
} from 'lucide-react';

import { zodResolver } from '@hookform/resolvers/zod';
import { useForm } from 'react-hook-form';
import { z } from 'zod';

import { PrivateGameCard } from '@/components/library/PrivateGameCard';
import { AddPrivateGameForm, type AddPrivateGameFormData } from '@/components/library/AddPrivateGameForm';
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
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api } from '@/lib/api';
import type { PrivateGameDto, GetPrivateGamesParams } from '@/lib/api/schemas/private-games.schemas';

type SortByOption = 'title' | 'createdAt' | 'updatedAt';
type SortDirection = 'asc' | 'desc';

export default function PrivateGamesClient() {
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

  // Dialog state
  const [addDialogOpen, setAddDialogOpen] = useState(false);
  const [editDialogOpen, setEditDialogOpen] = useState(false);
  const [deleteDialogOpen, setDeleteDialogOpen] = useState(false);
  const [selectedGame, setSelectedGame] = useState<PrivateGameDto | null>(null);
  const [isSubmitting, setIsSubmitting] = useState(false);
  const [isDeleting, setIsDeleting] = useState(false);

  const PAGE_SIZE = 12;

  const loadGames = useCallback(async (params?: Partial<GetPrivateGamesParams>) => {
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
      console.error('Failed to load private games:', err);
      setError('Impossibile caricare i giochi privati. Riprova.');
    } finally {
      setLoading(false);
    }
  }, [page, search, sortBy, sortDirection]);

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

  const handleAddGame = async (data: AddPrivateGameFormData) => {
    setIsSubmitting(true);
    try {
      await api.library.addPrivateGame({
        source: 'Manual',
        title: data.title,
        minPlayers: data.minPlayers,
        maxPlayers: data.maxPlayers,
        yearPublished: data.yearPublished ?? null,
        playingTimeMinutes: data.playingTimeMinutes ?? null,
        minAge: data.minAge ?? null,
        complexityRating: data.complexityRating ?? null,
        description: data.description ?? null,
        imageUrl: data.imageUrl || null,
      });
      setAddDialogOpen(false);
      await loadGames();
    } catch (err) {
      console.error('Failed to add private game:', err);
      throw err;
    } finally {
      setIsSubmitting(false);
    }
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
        imageUrl: data.imageUrl || null,
      });
      setEditDialogOpen(false);
      setSelectedGame(null);
      await loadGames();
    } catch (err) {
      console.error('Failed to update private game:', err);
      throw err;
    } finally {
      setIsSubmitting(false);
    }
  };

  const handleDeleteGame = async () => {
    if (!selectedGame) return;
    setIsDeleting(true);
    try {
      await api.library.deletePrivateGame(selectedGame.id);
      setDeleteDialogOpen(false);
      setSelectedGame(null);
      await loadGames();
    } catch (err) {
      console.error('Failed to delete private game:', err);
    } finally {
      setIsDeleting(false);
    }
  };

  const openEdit = (game: PrivateGameDto) => {
    setSelectedGame(game);
    setEditDialogOpen(true);
  };

  const openDelete = (gameId: string) => {
    const game = games.find(g => g.id === gameId);
    if (game) {
      setSelectedGame(game);
      setDeleteDialogOpen(true);
    }
  };

  return (
    <div className="container mx-auto py-8 space-y-6">
      {/* Header */}
      <div className="flex flex-col sm:flex-row items-start sm:items-center justify-between gap-4">
        <div>
          <h1 className="text-3xl font-bold">Private Games</h1>
          <p className="text-muted-foreground mt-1">
            Manage your personal game collection ({totalCount} {totalCount === 1 ? 'game' : 'games'})
          </p>
        </div>
        <Button onClick={() => setAddDialogOpen(true)} data-testid="add-private-game-btn">
          <Plus className="h-4 w-4 mr-2" />
          Add Game
        </Button>
      </div>

      {/* Search and Sort Controls */}
      <div className="flex flex-col sm:flex-row gap-3">
        <div className="relative flex-1">
          <Search className="absolute left-3 top-1/2 -translate-y-1/2 h-4 w-4 text-muted-foreground" aria-hidden="true" />
          <Input
            placeholder="Search games..."
            value={search}
            onChange={(e) => handleSearch(e.target.value)}
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
              <SelectItem value="createdAt">Date Added</SelectItem>
              <SelectItem value="updatedAt">Last Updated</SelectItem>
              <SelectItem value="title">Title</SelectItem>
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
          <span className="ml-3 text-muted-foreground">Loading games...</span>
        </div>
      ) : error ? (
        <Card data-testid="error-state">
          <CardContent className="flex flex-col items-center py-12">
            <AlertTriangle className="h-12 w-12 text-destructive mb-4" />
            <p className="text-lg font-medium text-destructive">{error}</p>
            <Button variant="outline" className="mt-4" onClick={() => loadGames()}>
              Retry
            </Button>
          </CardContent>
        </Card>
      ) : games.length === 0 ? (
        <Card data-testid="empty-state">
          <CardHeader className="text-center">
            <div className="mx-auto mb-4">
              <Gamepad2 className="h-16 w-16 text-muted-foreground" />
            </div>
            <CardTitle>
              {search ? 'No Games Found' : 'No Private Games Yet'}
            </CardTitle>
            <CardDescription>
              {search
                ? `No games matching "${search}". Try a different search term.`
                : 'Start building your personal collection by adding your first private game.'}
            </CardDescription>
          </CardHeader>
          {!search && (
            <CardContent className="text-center">
              <Button onClick={() => setAddDialogOpen(true)}>
                <Plus className="h-4 w-4 mr-2" />
                Add Your First Game
              </Button>
            </CardContent>
          )}
        </Card>
      ) : (
        <>
          {/* Games Grid */}
          <div
            className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4"
            data-testid="games-grid"
          >
            {games.map((game) => (
              <PrivateGameCard
                key={game.id}
                game={game}
                onEdit={openEdit}
                onDelete={openDelete}
              />
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
                Previous
              </Button>
              <span className="text-sm text-muted-foreground">
                Page {page} of {totalPages}
              </span>
              <Button
                variant="outline"
                size="sm"
                disabled={!hasNextPage}
                onClick={() => handlePageChange(page + 1)}
                aria-label="Next page"
              >
                Next
                <ChevronRight className="h-4 w-4 ml-1" />
              </Button>
            </div>
          )}
        </>
      )}

      {/* Add Game Dialog */}
      <Dialog open={addDialogOpen} onOpenChange={setAddDialogOpen}>
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Add Private Game</DialogTitle>
            <DialogDescription>
              Add a new game to your personal collection.
            </DialogDescription>
          </DialogHeader>
          <AddPrivateGameForm
            onSubmit={handleAddGame}
            onCancel={() => setAddDialogOpen(false)}
            isSubmitting={isSubmitting}
          />
        </DialogContent>
      </Dialog>

      {/* Edit Game Dialog */}
      <Dialog open={editDialogOpen} onOpenChange={(open: boolean) => {
        setEditDialogOpen(open);
        if (!open) setSelectedGame(null);
      }}>
        <DialogContent className="max-w-lg max-h-[90vh] overflow-y-auto">
          <DialogHeader>
            <DialogTitle>Edit Game</DialogTitle>
            <DialogDescription>
              Update the details of &quot;{selectedGame?.title}&quot;.
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
      <AlertDialog open={deleteDialogOpen} onOpenChange={(open) => {
        setDeleteDialogOpen(open);
        if (!open) setSelectedGame(null);
      }}>
        <AlertDialogContent>
          <AlertDialogHeader>
            <AlertDialogTitle>Delete Game</AlertDialogTitle>
            <AlertDialogDescription>
              Are you sure you want to delete &quot;{selectedGame?.title}&quot;? This action cannot be undone.
            </AlertDialogDescription>
          </AlertDialogHeader>
          <AlertDialogFooter>
            <AlertDialogCancel disabled={isDeleting}>Cancel</AlertDialogCancel>
            <AlertDialogAction
              onClick={handleDeleteGame}
              disabled={isDeleting}
              className="bg-destructive text-destructive-foreground hover:bg-destructive/90"
              data-testid="confirm-delete-btn"
            >
              {isDeleting ? (
                <>
                  <Loader2 className="h-4 w-4 mr-2 animate-spin" />
                  Deleting...
                </>
              ) : (
                'Delete'
              )}
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </div>
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

const EditFormSchema = z.object({
  title: z.string().min(1, 'Title is required').max(200, 'Title too long'),
  minPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
  maxPlayers: z.number().int().min(1, 'Min 1 player').max(99, 'Max 99 players'),
  yearPublished: z.number().int().min(1900).max(2100).nullable().optional(),
  playingTimeMinutes: z.number().int().min(1).max(10000).nullable().optional(),
  minAge: z.number().int().min(0).max(99).nullable().optional(),
  complexityRating: z.number().min(0).max(5).nullable().optional(),
  description: z.string().max(5000, 'Description too long').nullable().optional(),
  imageUrl: z.string().url('Invalid URL').nullable().optional().or(z.literal('')),
}).refine((data) => data.maxPlayers >= data.minPlayers, {
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
  const {
    register,
    handleSubmit,
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
      imageUrl: game.imageUrl ?? undefined,
    },
  });

  const onSubmitForm = async (data: AddPrivateGameFormData) => {
    try {
      await onSubmit(data);
    } catch (error) {
      console.error('Form submission error:', error);
    }
  };

  return (
    <form onSubmit={handleSubmit(onSubmitForm)} className="space-y-6">
      <div className="space-y-2">
        <Label htmlFor="edit-title">
          Title <span className="text-destructive">*</span>
        </Label>
        <Input id="edit-title" {...register('title')} disabled={isSubmitting} />
        {errors.title && <p className="text-sm text-destructive">{errors.title.message}</p>}
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="edit-minPlayers">Min Players <span className="text-destructive">*</span></Label>
          <Input id="edit-minPlayers" type="number" min="1" max="99" {...register('minPlayers', { valueAsNumber: true })} disabled={isSubmitting} />
          {errors.minPlayers && <p className="text-sm text-destructive">{errors.minPlayers.message}</p>}
        </div>
        <div className="space-y-2">
          <Label htmlFor="edit-maxPlayers">Max Players <span className="text-destructive">*</span></Label>
          <Input id="edit-maxPlayers" type="number" min="1" max="99" {...register('maxPlayers', { valueAsNumber: true })} disabled={isSubmitting} />
          {errors.maxPlayers && <p className="text-sm text-destructive">{errors.maxPlayers.message}</p>}
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="edit-yearPublished">Year Published</Label>
          <Input id="edit-yearPublished" type="number" min="1900" max="2100" {...register('yearPublished', { valueAsNumber: true })} disabled={isSubmitting} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="edit-playingTimeMinutes">Playing Time (min)</Label>
          <Input id="edit-playingTimeMinutes" type="number" min="1" max="10000" {...register('playingTimeMinutes', { valueAsNumber: true })} disabled={isSubmitting} />
        </div>
      </div>

      <div className="grid grid-cols-2 gap-4">
        <div className="space-y-2">
          <Label htmlFor="edit-minAge">Min Age</Label>
          <Input id="edit-minAge" type="number" min="0" max="99" {...register('minAge', { valueAsNumber: true })} disabled={isSubmitting} />
        </div>
        <div className="space-y-2">
          <Label htmlFor="edit-complexityRating">Complexity (0-5)</Label>
          <Input id="edit-complexityRating" type="number" min="0" max="5" step="0.1" {...register('complexityRating', { valueAsNumber: true })} disabled={isSubmitting} />
        </div>
      </div>

      <div className="space-y-2">
        <Label htmlFor="edit-description">Description</Label>
        <Textarea id="edit-description" {...register('description')} rows={4} disabled={isSubmitting} />
      </div>

      <div className="space-y-2">
        <Label htmlFor="edit-imageUrl">Image URL</Label>
        <Input id="edit-imageUrl" type="url" {...register('imageUrl')} disabled={isSubmitting} />
      </div>

      <div className="flex justify-end gap-3 pt-4">
        <Button type="button" variant="outline" onClick={onCancel} disabled={isSubmitting}>
          Cancel
        </Button>
        <Button type="submit" disabled={isSubmitting}>
          {isSubmitting ? 'Saving...' : 'Save Changes'}
        </Button>
      </div>
    </form>
  );
}
