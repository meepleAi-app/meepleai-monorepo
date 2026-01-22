/**
 * Shared Games Catalog Admin Client - Issue #2372
 *
 * Admin interface for managing shared game catalog:
 * - List all games with filters (status, search, category, mechanic)
 * - Create new game
 * - Edit existing game
 * - Publish/Archive/Delete games
 * - Role-based permissions (Admin vs Editor)
 *
 * Backend Integration:
 * - GET /api/v1/admin/shared-games (via api.sharedGames.getAll)
 * - POST /api/v1/admin/shared-games (via api.sharedGames.create)
 * - PUT /api/v1/admin/shared-games/:id (via api.sharedGames.update)
 * - POST /api/v1/admin/shared-games/:id/publish
 * - POST /api/v1/admin/shared-games/:id/archive
 * - DELETE /api/v1/admin/shared-games/:id
 */

'use client';

import { useState, useCallback, useEffect } from 'react';

import {
  Plus,
  Trash2,
  Edit,
  Search,
  AlertCircle,
  Archive,
  Eye,
  MoreHorizontal,
  ChevronLeft,
  ChevronRight,
  FileText,
  Clock,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { AdminAuthGuard, GameStatusBadge, PlayersBadge, PlayTimeBadge } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import {
  DropdownMenu,
  DropdownMenuContent,
  DropdownMenuItem,
  DropdownMenuSeparator,
  DropdownMenuTrigger,
} from '@/components/ui/navigation/dropdown-menu';
import {
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
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
import {
  api,
  type SharedGame,
  type GameCategory,
  type GameMechanic,
  type PagedSharedGames,
} from '@/lib/api';

// ========== Types ==========

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type GameStatusFilter = 'all' | '0' | '1' | '2'; // all, Draft, Published, Archived

// ========== Main Component ==========

export function SharedGamesClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Data state
  const [games, setGames] = useState<SharedGame[]>([]);
  const [_categories, setCategories] = useState<GameCategory[]>([]);
  const [_mechanics, setMechanics] = useState<GameMechanic[]>([]);
  const [loading, setLoading] = useState(false);
  const [pendingApprovalsCount, setPendingApprovalsCount] = useState(0);

  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [total, setTotal] = useState(0);

  // Filter state
  const [searchTerm, setSearchTerm] = useState('');
  const [statusFilter, setStatusFilter] = useState<GameStatusFilter>('all');

  // Modal states
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [gameToDelete, setGameToDelete] = useState<SharedGame | null>(null);
  const [deleteReason, setDeleteReason] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // Check if user is admin (for delete permissions)
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  // Fetch reference data (categories, mechanics) and pending approvals count
  useEffect(() => {
    const fetchReferenceData = async () => {
      try {
        const [categoriesResult, mechanicsResult] = await Promise.all([
          api.sharedGames.getCategories(),
          api.sharedGames.getMechanics(),
        ]);
        setCategories(categoriesResult);
        setMechanics(mechanicsResult);
      } catch (err) {
        console.error('Failed to fetch reference data:', err);
        // Non-critical, don't show error toast
      }
    };

    const fetchPendingApprovalsCount = async () => {
      if (isAdmin) {
        try {
          const result = await api.sharedGames.getPendingApprovals({ pageSize: 1 });
          setPendingApprovalsCount(result.total);
        } catch (err) {
          console.error('Failed to fetch pending approvals count:', err);
        }
      }
    };

    fetchReferenceData();
    fetchPendingApprovalsCount();
  }, [isAdmin]);

  // Fetch games
  const fetchGames = useCallback(async () => {
    try {
      setLoading(true);

      const params: { status?: number; page?: number; pageSize?: number } = {
        page,
        pageSize,
      };

      if (statusFilter !== 'all') {
        params.status = parseInt(statusFilter, 10);
      }

      const result: PagedSharedGames = await api.sharedGames.getAll(params);

      // Filter by search term on client-side (for now)
      let filteredItems = result.items;
      if (searchTerm.trim()) {
        const term = searchTerm.toLowerCase();
        filteredItems = result.items.filter(
          game =>
            game.title.toLowerCase().includes(term) || game.description.toLowerCase().includes(term)
        );
      }

      setGames(filteredItems);
      setTotal(result.total);
    } catch (err) {
      console.error('Failed to fetch games:', err);
      addToast('error', 'Errore nel caricamento dei giochi');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, statusFilter, searchTerm, addToast]);

  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  // Handle search
  const handleSearch = () => {
    setPage(1);
    fetchGames();
  };

  // Handle publish
  const handlePublish = async (game: SharedGame) => {
    try {
      await api.sharedGames.publish(game.id);
      addToast('success', `"${game.title}" pubblicato con successo`);
      fetchGames();
    } catch (err) {
      console.error('Failed to publish game:', err);
      addToast('error', 'Errore nella pubblicazione del gioco');
    }
  };

  // Handle archive
  const handleArchive = async (game: SharedGame) => {
    try {
      await api.sharedGames.archive(game.id);
      addToast('success', `"${game.title}" archiviato con successo`);
      fetchGames();
    } catch (err) {
      console.error('Failed to archive game:', err);
      addToast('error', "Errore nell'archiviazione del gioco");
    }
  };

  // Handle delete (opens modal)
  const handleDeleteClick = (game: SharedGame) => {
    setGameToDelete(game);
    setDeleteReason('');
    setIsDeleteModalOpen(true);
  };

  // Confirm delete
  const handleDeleteConfirm = async () => {
    if (!gameToDelete) return;

    // Editors must provide a reason
    if (!isAdmin && !deleteReason.trim()) {
      addToast('error', 'Devi fornire una motivazione per la richiesta di eliminazione');
      return;
    }

    try {
      setIsDeleting(true);

      if (isAdmin) {
        // Admin: immediate deletion
        await api.sharedGames.delete(gameToDelete.id);
        addToast('success', `"${gameToDelete.title}" eliminato con successo`);
      } else {
        // Editor: create delete request for admin approval
        await api.sharedGames.requestDelete(gameToDelete.id, { reason: deleteReason });
        addToast('info', 'Richiesta di eliminazione inviata. Un admin la valuterà.');
      }

      setIsDeleteModalOpen(false);
      setGameToDelete(null);
      setDeleteReason('');
      fetchGames();
    } catch (err) {
      console.error('Failed to delete game:', err);
      addToast('error', "Errore nell'eliminazione del gioco");
    } finally {
      setIsDeleting(false);
    }
  };

  // Navigate to create page
  const handleCreate = () => {
    router.push('/admin/shared-games/new');
  };

  // Navigate to edit page
  const handleEdit = (gameId: string) => {
    router.push(`/admin/shared-games/${gameId}/edit`);
  };

  // Pagination
  const totalPages = Math.ceil(total / pageSize);
  const canPrevPage = page > 1;
  const canNextPage = page < totalPages;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle>Catalogo Giochi Condivisi</CardTitle>
                <CardDescription>
                  Gestisci il catalogo condiviso di giochi da tavolo. Crea, modifica, pubblica e
                  archivia i giochi.
                </CardDescription>
              </div>
              <div className="flex items-center gap-3">
                {/* Pending Approvals Link (Admin Only) */}
                {isAdmin && (
                  <Link href="/admin/shared-games/pending-approvals">
                    <Button variant="outline" className="relative">
                      <Clock className="h-4 w-4 mr-2" />
                      Approvazioni
                      {pendingApprovalsCount > 0 && (
                        <span className="absolute -top-2 -right-2 bg-amber-500 text-white text-xs font-bold rounded-full h-5 w-5 flex items-center justify-center">
                          {pendingApprovalsCount > 99 ? '99+' : pendingApprovalsCount}
                        </span>
                      )}
                    </Button>
                  </Link>
                )}
                <Button onClick={handleCreate}>
                  <Plus className="h-4 w-4 mr-2" />
                  Nuovo Gioco
                </Button>
              </div>
            </div>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Filters */}
            <div className="flex items-center gap-4 flex-wrap">
              {/* Search */}
              <div className="flex items-center gap-2 flex-1 min-w-[300px]">
                <div className="relative flex-1">
                  <Search className="absolute left-3 top-1/2 transform -translate-y-1/2 h-4 w-4 text-muted-foreground" />
                  <Input
                    placeholder="Cerca per titolo o descrizione..."
                    className="pl-10"
                    value={searchTerm}
                    onChange={e => setSearchTerm(e.target.value)}
                    onKeyDown={e => e.key === 'Enter' && handleSearch()}
                  />
                </div>
                <Button variant="secondary" onClick={handleSearch}>
                  Cerca
                </Button>
              </div>

              {/* Status filter */}
              <div className="flex items-center gap-2">
                <Label htmlFor="status-filter" className="shrink-0">
                  Stato:
                </Label>
                <Select
                  value={statusFilter}
                  onValueChange={(value: GameStatusFilter) => {
                    setStatusFilter(value);
                    setPage(1);
                  }}
                >
                  <SelectTrigger id="status-filter" className="w-[150px]">
                    <SelectValue placeholder="Tutti" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Tutti</SelectItem>
                    <SelectItem value="0">Bozza</SelectItem>
                    <SelectItem value="1">Pubblicato</SelectItem>
                    <SelectItem value="2">Archiviato</SelectItem>
                  </SelectContent>
                </Select>
              </div>
            </div>

            {/* Stats */}
            <div className="flex items-center gap-2 text-sm text-muted-foreground">
              <span>
                {total} {total === 1 ? 'gioco' : 'giochi'} totali
              </span>
              {statusFilter !== 'all' && (
                <span className="text-primary">
                  (filtrato per{' '}
                  {statusFilter === '0'
                    ? 'bozze'
                    : statusFilter === '1'
                      ? 'pubblicati'
                      : 'archiviati'}
                  )
                </span>
              )}
            </div>

            {/* Loading State */}
            {loading && (
              <div className="flex justify-center py-12">
                <Spinner size="lg" />
              </div>
            )}

            {/* Empty State */}
            {!loading && games.length === 0 && (
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  {searchTerm || statusFilter !== 'all'
                    ? 'Nessun gioco trovato con i filtri selezionati.'
                    : 'Nessun gioco nel catalogo. Clicca "Nuovo Gioco" per aggiungerne uno.'}
                </AlertDescription>
              </Alert>
            )}

            {/* Games Table */}
            {!loading && games.length > 0 && (
              <>
                <div className="border rounded-lg">
                  <Table>
                    <TableHeader>
                      <TableRow>
                        <TableHead className="w-[80px]">Immagine</TableHead>
                        <TableHead className="w-[25%]">Titolo</TableHead>
                        <TableHead className="w-[15%]">Giocatori</TableHead>
                        <TableHead className="w-[15%]">Durata</TableHead>
                        <TableHead className="w-[15%]">Stato</TableHead>
                        <TableHead className="w-[15%]">Creato</TableHead>
                        <TableHead className="w-[10%] text-right">Azioni</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {games.map(game => (
                        <TableRow key={game.id}>
                          {/* Thumbnail */}
                          <TableCell>
                            {game.thumbnailUrl ? (
                              // eslint-disable-next-line @next/next/no-img-element
                              <img
                                src={game.thumbnailUrl}
                                alt={game.title}
                                className="w-12 h-12 object-cover rounded"
                              />
                            ) : (
                              <div className="w-12 h-12 bg-muted rounded flex items-center justify-center">
                                <FileText className="h-6 w-6 text-muted-foreground" />
                              </div>
                            )}
                          </TableCell>

                          {/* Title & Year */}
                          <TableCell>
                            <div className="font-medium">{game.title}</div>
                            <div className="text-sm text-muted-foreground">
                              {game.yearPublished}
                            </div>
                          </TableCell>

                          {/* Players */}
                          <TableCell>
                            <PlayersBadge min={game.minPlayers} max={game.maxPlayers} />
                          </TableCell>

                          {/* Playing Time */}
                          <TableCell>
                            <PlayTimeBadge minutes={game.playingTimeMinutes} />
                          </TableCell>

                          {/* Status */}
                          <TableCell>
                            <GameStatusBadge status={game.status} />
                          </TableCell>

                          {/* Created Date */}
                          <TableCell className="text-sm text-muted-foreground">
                            {new Date(game.createdAt).toLocaleDateString('it-IT')}
                          </TableCell>

                          {/* Actions */}
                          <TableCell className="text-right">
                            <DropdownMenu>
                              <DropdownMenuTrigger asChild>
                                <Button variant="ghost" size="sm">
                                  <MoreHorizontal className="h-4 w-4" />
                                </Button>
                              </DropdownMenuTrigger>
                              <DropdownMenuContent align="end">
                                <DropdownMenuItem onClick={() => handleEdit(game.id)}>
                                  <Edit className="h-4 w-4 mr-2" />
                                  Modifica
                                </DropdownMenuItem>

                                {/* Publish (only for Draft) */}
                                {game.status === 0 && (
                                  <DropdownMenuItem onClick={() => handlePublish(game)}>
                                    <Eye className="h-4 w-4 mr-2" />
                                    Pubblica
                                  </DropdownMenuItem>
                                )}

                                {/* Archive (only for Published) */}
                                {game.status === 1 && (
                                  <DropdownMenuItem onClick={() => handleArchive(game)}>
                                    <Archive className="h-4 w-4 mr-2" />
                                    Archivia
                                  </DropdownMenuItem>
                                )}

                                <DropdownMenuSeparator />

                                <DropdownMenuItem
                                  onClick={() => handleDeleteClick(game)}
                                  className="text-destructive focus:text-destructive"
                                >
                                  <Trash2 className="h-4 w-4 mr-2" />
                                  {isAdmin ? 'Elimina' : 'Richiedi Eliminazione'}
                                </DropdownMenuItem>
                              </DropdownMenuContent>
                            </DropdownMenu>
                          </TableCell>
                        </TableRow>
                      ))}
                    </TableBody>
                  </Table>
                </div>

                {/* Pagination */}
                {totalPages > 1 && (
                  <div className="flex items-center justify-between">
                    <div className="text-sm text-muted-foreground">
                      Pagina {page} di {totalPages}
                    </div>
                    <div className="flex items-center gap-2">
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => p - 1)}
                        disabled={!canPrevPage}
                      >
                        <ChevronLeft className="h-4 w-4" />
                        Precedente
                      </Button>
                      <Button
                        variant="outline"
                        size="sm"
                        onClick={() => setPage(p => p + 1)}
                        disabled={!canNextPage}
                      >
                        Successivo
                        <ChevronRight className="h-4 w-4" />
                      </Button>
                    </div>
                  </div>
                )}
              </>
            )}
          </CardContent>
        </Card>

        {/* Delete Confirmation Modal */}
        <Dialog open={isDeleteModalOpen} onOpenChange={setIsDeleteModalOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{isAdmin ? 'Elimina Gioco' : 'Richiedi Eliminazione'}</DialogTitle>
              <DialogDescription>
                {isAdmin
                  ? `Sei sicuro di voler eliminare "${gameToDelete?.title}"? Questa azione non può essere annullata.`
                  : `La tua richiesta di eliminazione per "${gameToDelete?.title}" sarà inviata a un amministratore per l'approvazione.`}
              </DialogDescription>
            </DialogHeader>

            {/* Reason (required for Editors) */}
            {!isAdmin && (
              <div className="space-y-2 mt-4">
                <Label htmlFor="delete-reason">Motivazione *</Label>
                <Textarea
                  id="delete-reason"
                  placeholder="Spiega perché questo gioco dovrebbe essere eliminato..."
                  value={deleteReason}
                  onChange={e => setDeleteReason(e.target.value)}
                  rows={3}
                />
              </div>
            )}

            <DialogFooter className="mt-6">
              <Button
                variant="outline"
                onClick={() => {
                  setIsDeleteModalOpen(false);
                  setGameToDelete(null);
                  setDeleteReason('');
                }}
                disabled={isDeleting}
              >
                Annulla
              </Button>
              <Button
                variant="destructive"
                onClick={handleDeleteConfirm}
                disabled={isDeleting || (!isAdmin && !deleteReason.trim())}
              >
                {isDeleting ? (
                  <>
                    <Spinner size="sm" className="mr-2" />
                    {isAdmin ? 'Eliminazione...' : 'Invio...'}
                  </>
                ) : isAdmin ? (
                  'Elimina'
                ) : (
                  'Invia Richiesta'
                )}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Toast Notifications */}
        <div className="fixed bottom-4 right-4 z-50 space-y-2">
          {toasts.map(toast => (
            <Alert
              key={toast.id}
              variant={toast.type === 'error' ? 'destructive' : 'default'}
              className="w-96"
            >
              <AlertDescription>{toast.message}</AlertDescription>
            </Alert>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
