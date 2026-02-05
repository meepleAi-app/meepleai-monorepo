/**
 * Pending Approvals Client - Issue #2514, Issue #3350
 *
 * Admin interface for reviewing and approving games submitted for publication:
 * - List all games in PendingApproval status
 * - Approve games (PendingApproval → Published)
 * - Reject games with reason (PendingApproval → Draft)
 * - Batch approve/reject multiple games at once (Issue #3350)
 *
 * Backend Integration:
 * - GET /api/v1/admin/shared-games/pending-approvals
 * - POST /api/v1/admin/shared-games/:id/approve-publication
 * - POST /api/v1/admin/shared-games/:id/reject-publication
 * - POST /api/v1/admin/shared-games/batch-approve
 * - POST /api/v1/admin/shared-games/batch-reject
 */

'use client';

import { useState, useCallback, useEffect, useMemo } from 'react';

import {
  CheckCircle,
  XCircle,
  Eye,
  AlertCircle,
  ChevronLeft,
  ChevronRight,
  FileText,
  Clock,
  ArrowLeft,
  CheckSquare,
  Square,
  MinusSquare,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { AdminAuthGuard, PlayersBadge, PlayTimeBadge, ComplexityBadge } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
import { Badge } from '@/components/ui/data-display/badge';
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
  Dialog,
  DialogContent,
  DialogDescription,
  DialogFooter,
  DialogHeader,
  DialogTitle,
} from '@/components/ui/overlays/dialog';
import { Button } from '@/components/ui/primitives/button';
import { Checkbox } from '@/components/ui/primitives/checkbox';
import { Label } from '@/components/ui/primitives/label';
import { Textarea } from '@/components/ui/primitives/textarea';
import { api, type SharedGame } from '@/lib/api';

// ========== Types ==========

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type RejectModalState = {
  isOpen: boolean;
  game: SharedGame | null;
};

type BatchRejectModalState = {
  isOpen: boolean;
  gameIds: string[];
  gameTitles: string[];
};

// ========== Main Component ==========

export function PendingApprovalsClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Data state
  const [games, setGames] = useState<SharedGame[]>([]);
  const [loading, setLoading] = useState(false);

  // Pagination state
  const [page, setPage] = useState(1);
  const [pageSize] = useState(20);
  const [total, setTotal] = useState(0);

  // Selection state (Issue #3350)
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Action states
  const [approving, setApproving] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState(false);
  const [batchApproving, setBatchApproving] = useState(false);
  const [batchRejecting, setBatchRejecting] = useState(false);

  // Modal states
  const [rejectModal, setRejectModal] = useState<RejectModalState>({
    isOpen: false,
    game: null,
  });
  const [rejectReason, setRejectReason] = useState('');

  // Batch reject modal state (Issue #3350)
  const [batchRejectModal, setBatchRejectModal] = useState<BatchRejectModalState>({
    isOpen: false,
    gameIds: [],
    gameTitles: [],
  });
  const [batchRejectReason, setBatchRejectReason] = useState('');

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // Selection helpers (Issue #3350)
  const allSelected = useMemo(() => {
    return games.length > 0 && games.every(game => selectedIds.has(game.id));
  }, [games, selectedIds]);

  const someSelected = useMemo(() => {
    return games.some(game => selectedIds.has(game.id)) && !allSelected;
  }, [games, selectedIds, allSelected]);

  const selectedCount = useMemo(() => {
    return games.filter(game => selectedIds.has(game.id)).length;
  }, [games, selectedIds]);

  const toggleSelectAll = useCallback(() => {
    if (allSelected) {
      // Deselect all on current page
      setSelectedIds(prev => {
        const newSet = new Set(prev);
        games.forEach(game => newSet.delete(game.id));
        return newSet;
      });
    } else {
      // Select all on current page
      setSelectedIds(prev => {
        const newSet = new Set(prev);
        games.forEach(game => newSet.add(game.id));
        return newSet;
      });
    }
  }, [games, allSelected]);

  const toggleSelect = useCallback((gameId: string) => {
    setSelectedIds(prev => {
      const newSet = new Set(prev);
      if (newSet.has(gameId)) {
        newSet.delete(gameId);
      } else {
        newSet.add(gameId);
      }
      return newSet;
    });
  }, []);

  const clearSelection = useCallback(() => {
    setSelectedIds(new Set());
  }, []);

  // Fetch pending approvals
  const fetchPendingApprovals = useCallback(async () => {
    try {
      setLoading(true);
      const result = await api.sharedGames.getPendingApprovals({
        pageNumber: page,
        pageSize,
      });

      setGames(result.items);
      setTotal(result.total);
    } catch (err) {
      console.error('Failed to fetch pending approvals:', err);
      addToast('error', 'Errore nel caricamento delle approvazioni in sospeso');
    } finally {
      setLoading(false);
    }
  }, [page, pageSize, addToast]);

  useEffect(() => {
    fetchPendingApprovals();
  }, [fetchPendingApprovals]);

  // Handle approve
  const handleApprove = async (game: SharedGame) => {
    try {
      setApproving(game.id);
      await api.sharedGames.approvePublication(game.id);
      addToast('success', `"${game.title}" approvato e pubblicato con successo`);
      setSelectedIds(prev => {
        const newSet = new Set(prev);
        newSet.delete(game.id);
        return newSet;
      });
      fetchPendingApprovals();
    } catch (err) {
      console.error('Failed to approve game:', err);
      addToast('error', "Errore nell'approvazione del gioco");
    } finally {
      setApproving(null);
    }
  };

  // Handle reject (opens modal)
  const handleRejectClick = (game: SharedGame) => {
    setRejectModal({ isOpen: true, game });
    setRejectReason('');
  };

  // Confirm reject
  const handleRejectConfirm = async () => {
    const game = rejectModal.game;
    if (!game) return;

    if (!rejectReason.trim()) {
      addToast('error', 'Devi fornire una motivazione per il rifiuto');
      return;
    }

    try {
      setRejecting(true);
      await api.sharedGames.rejectPublication(game.id, rejectReason);
      addToast('info', `"${game.title}" rifiutato e riportato in bozza`);
      setSelectedIds(prev => {
        const newSet = new Set(prev);
        newSet.delete(game.id);
        return newSet;
      });
      setRejectModal({ isOpen: false, game: null });
      setRejectReason('');
      fetchPendingApprovals();
    } catch (err) {
      console.error('Failed to reject game:', err);
      addToast('error', 'Errore nel rifiuto del gioco');
    } finally {
      setRejecting(false);
    }
  };

  // ========== Batch Operations (Issue #3350) ==========

  // Handle batch approve
  const handleBatchApprove = async () => {
    const selectedGameIds = Array.from(selectedIds).filter(id =>
      games.some(g => g.id === id)
    );

    if (selectedGameIds.length === 0) {
      addToast('error', 'Seleziona almeno un gioco da approvare');
      return;
    }

    try {
      setBatchApproving(true);
      const result = await api.sharedGames.batchApprove(selectedGameIds);

      if (result.successCount > 0) {
        addToast(
          'success',
          `${result.successCount} ${result.successCount === 1 ? 'gioco approvato' : 'giochi approvati'} con successo`
        );
      }

      if (result.failureCount > 0) {
        addToast(
          'error',
          `${result.failureCount} ${result.failureCount === 1 ? 'gioco non approvato' : 'giochi non approvati'}: ${result.errors.join(', ')}`
        );
      }

      clearSelection();
      fetchPendingApprovals();
    } catch (err) {
      console.error('Failed to batch approve games:', err);
      addToast('error', "Errore nell'approvazione batch dei giochi");
    } finally {
      setBatchApproving(false);
    }
  };

  // Handle batch reject (opens modal)
  const handleBatchRejectClick = () => {
    const selectedGameIds = Array.from(selectedIds).filter(id =>
      games.some(g => g.id === id)
    );
    const selectedTitles = games
      .filter(g => selectedIds.has(g.id))
      .map(g => g.title);

    if (selectedGameIds.length === 0) {
      addToast('error', 'Seleziona almeno un gioco da rifiutare');
      return;
    }

    setBatchRejectModal({
      isOpen: true,
      gameIds: selectedGameIds,
      gameTitles: selectedTitles,
    });
    setBatchRejectReason('');
  };

  // Confirm batch reject
  const handleBatchRejectConfirm = async () => {
    if (batchRejectModal.gameIds.length === 0) return;

    if (!batchRejectReason.trim()) {
      addToast('error', 'Devi fornire una motivazione per il rifiuto');
      return;
    }

    try {
      setBatchRejecting(true);
      const result = await api.sharedGames.batchReject(
        batchRejectModal.gameIds,
        batchRejectReason
      );

      if (result.successCount > 0) {
        addToast(
          'info',
          `${result.successCount} ${result.successCount === 1 ? 'gioco rifiutato' : 'giochi rifiutati'} e riportati in bozza`
        );
      }

      if (result.failureCount > 0) {
        addToast(
          'error',
          `${result.failureCount} ${result.failureCount === 1 ? 'gioco non rifiutato' : 'giochi non rifiutati'}: ${result.errors.join(', ')}`
        );
      }

      clearSelection();
      setBatchRejectModal({ isOpen: false, gameIds: [], gameTitles: [] });
      setBatchRejectReason('');
      fetchPendingApprovals();
    } catch (err) {
      console.error('Failed to batch reject games:', err);
      addToast('error', 'Errore nel rifiuto batch dei giochi');
    } finally {
      setBatchRejecting(false);
    }
  };

  // Navigate to game detail
  const handleViewGame = (gameId: string) => {
    router.push(`/admin/shared-games/${gameId}`);
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
              <div className="space-y-1">
                <div className="flex items-center gap-3">
                  <Link href="/admin/shared-games">
                    <Button variant="ghost" size="sm">
                      <ArrowLeft className="h-4 w-4 mr-1" />
                      Indietro
                    </Button>
                  </Link>
                  <CardTitle className="flex items-center gap-2">
                    <Clock className="h-5 w-5 text-amber-500" />
                    Approvazioni in Sospeso
                  </CardTitle>
                </div>
                <CardDescription>
                  Rivedi i giochi sottomessi per la pubblicazione. Approva per renderli pubblici o
                  rifiuta con una motivazione.
                </CardDescription>
              </div>
              <Badge variant="secondary" className="text-lg px-4 py-2">
                {total} {total === 1 ? 'gioco' : 'giochi'} in attesa
              </Badge>
            </div>
          </CardHeader>
          <CardContent className="space-y-6">
            {/* Batch Action Bar (Issue #3350) */}
            {selectedCount > 0 && (
              <div className="flex items-center justify-between bg-muted/50 rounded-lg p-4 border">
                <div className="flex items-center gap-3">
                  <Badge variant="outline" className="px-3 py-1">
                    {selectedCount} {selectedCount === 1 ? 'selezionato' : 'selezionati'}
                  </Badge>
                  <Button variant="ghost" size="sm" onClick={clearSelection}>
                    Deseleziona tutto
                  </Button>
                </div>
                <div className="flex items-center gap-2">
                  <Button
                    variant="default"
                    size="sm"
                    onClick={handleBatchApprove}
                    disabled={batchApproving || batchRejecting}
                    className="bg-green-600 hover:bg-green-700"
                  >
                    {batchApproving ? (
                      <Spinner size="sm" className="mr-2" />
                    ) : (
                      <CheckCircle className="h-4 w-4 mr-2" />
                    )}
                    Approva Selezionati
                  </Button>
                  <Button
                    variant="destructive"
                    size="sm"
                    onClick={handleBatchRejectClick}
                    disabled={batchApproving || batchRejecting}
                  >
                    {batchRejecting ? (
                      <Spinner size="sm" className="mr-2" />
                    ) : (
                      <XCircle className="h-4 w-4 mr-2" />
                    )}
                    Rifiuta Selezionati
                  </Button>
                </div>
              </div>
            )}

            {/* Loading State */}
            {loading && (
              <div className="flex justify-center py-12">
                <Spinner size="lg" />
              </div>
            )}

            {/* Empty State */}
            {!loading && games.length === 0 && (
              <Alert>
                <CheckCircle className="h-4 w-4 text-green-500" />
                <AlertDescription>
                  Nessun gioco in attesa di approvazione. Ottimo lavoro!
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
                        {/* Selection checkbox header (Issue #3350) */}
                        <TableHead className="w-[50px]">
                          <button
                            onClick={toggleSelectAll}
                            className="flex items-center justify-center p-1 hover:bg-muted rounded"
                            title={allSelected ? 'Deseleziona tutto' : 'Seleziona tutto'}
                          >
                            {allSelected ? (
                              <CheckSquare className="h-5 w-5 text-primary" />
                            ) : someSelected ? (
                              <MinusSquare className="h-5 w-5 text-primary" />
                            ) : (
                              <Square className="h-5 w-5 text-muted-foreground" />
                            )}
                          </button>
                        </TableHead>
                        <TableHead className="w-[80px]">Immagine</TableHead>
                        <TableHead className="w-[25%]">Titolo</TableHead>
                        <TableHead className="w-[12%]">Giocatori</TableHead>
                        <TableHead className="w-[12%]">Durata</TableHead>
                        <TableHead className="w-[12%]">Complessità</TableHead>
                        <TableHead className="w-[15%]">Sottomesso</TableHead>
                        <TableHead className="w-[20%] text-right">Azioni</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {games.map(game => (
                        <TableRow
                          key={game.id}
                          className={selectedIds.has(game.id) ? 'bg-muted/50' : ''}
                        >
                          {/* Selection checkbox (Issue #3350) */}
                          <TableCell>
                            <Checkbox
                              checked={selectedIds.has(game.id)}
                              onCheckedChange={() => toggleSelect(game.id)}
                              aria-label={`Seleziona ${game.title}`}
                            />
                          </TableCell>

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

                          {/* Complexity */}
                          <TableCell>
                            <ComplexityBadge rating={game.complexityRating ?? 0} />
                          </TableCell>

                          {/* Submitted Date */}
                          <TableCell className="text-sm text-muted-foreground">
                            {game.modifiedAt
                              ? new Date(game.modifiedAt).toLocaleDateString('it-IT', {
                                  day: '2-digit',
                                  month: 'short',
                                  year: 'numeric',
                                  hour: '2-digit',
                                  minute: '2-digit',
                                })
                              : new Date(game.createdAt).toLocaleDateString('it-IT')}
                          </TableCell>

                          {/* Actions */}
                          <TableCell className="text-right">
                            <div className="flex items-center justify-end gap-2">
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => handleViewGame(game.id)}
                                title="Visualizza dettagli"
                              >
                                <Eye className="h-4 w-4" />
                              </Button>
                              <Button
                                variant="default"
                                size="sm"
                                onClick={() => handleApprove(game)}
                                disabled={approving === game.id}
                                className="bg-green-600 hover:bg-green-700"
                              >
                                {approving === game.id ? (
                                  <Spinner size="sm" className="mr-1" />
                                ) : (
                                  <CheckCircle className="h-4 w-4 mr-1" />
                                )}
                                Approva
                              </Button>
                              <Button
                                variant="destructive"
                                size="sm"
                                onClick={() => handleRejectClick(game)}
                                disabled={approving === game.id}
                              >
                                <XCircle className="h-4 w-4 mr-1" />
                                Rifiuta
                              </Button>
                            </div>
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

        {/* Reject Confirmation Modal (Single) */}
        <Dialog
          open={rejectModal.isOpen}
          onOpenChange={isOpen => {
            if (!isOpen) {
              setRejectModal({ isOpen: false, game: null });
              setRejectReason('');
            }
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <XCircle className="h-5 w-5 text-destructive" />
                Rifiuta Pubblicazione
              </DialogTitle>
              <DialogDescription>
                Stai per rifiutare la pubblicazione di &quot;{rejectModal.game?.title}&quot;. Il
                gioco tornerà in stato Bozza e l&apos;editor riceverà una notifica con la tua
                motivazione.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-2 mt-4">
              <Label htmlFor="reject-reason">Motivazione del rifiuto *</Label>
              <Textarea
                id="reject-reason"
                placeholder="Spiega perché il gioco non può essere pubblicato (es. informazioni incomplete, descrizione inadeguata, problemi con le immagini...)"
                value={rejectReason}
                onChange={e => setRejectReason(e.target.value)}
                rows={4}
              />
              <p className="text-xs text-muted-foreground">
                Questa motivazione sarà visibile all&apos;editor che ha sottomesso il gioco.
              </p>
            </div>

            <DialogFooter className="mt-6">
              <Button
                variant="outline"
                onClick={() => {
                  setRejectModal({ isOpen: false, game: null });
                  setRejectReason('');
                }}
                disabled={rejecting}
              >
                Annulla
              </Button>
              <Button
                variant="destructive"
                onClick={handleRejectConfirm}
                disabled={rejecting || !rejectReason.trim()}
              >
                {rejecting ? (
                  <>
                    <Spinner size="sm" className="mr-2" />
                    Rifiutando...
                  </>
                ) : (
                  <>
                    <XCircle className="h-4 w-4 mr-2" />
                    Rifiuta Pubblicazione
                  </>
                )}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Batch Reject Confirmation Modal (Issue #3350) */}
        <Dialog
          open={batchRejectModal.isOpen}
          onOpenChange={isOpen => {
            if (!isOpen) {
              setBatchRejectModal({ isOpen: false, gameIds: [], gameTitles: [] });
              setBatchRejectReason('');
            }
          }}
        >
          <DialogContent>
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <XCircle className="h-5 w-5 text-destructive" />
                Rifiuta {batchRejectModal.gameIds.length} Giochi
              </DialogTitle>
              <DialogDescription>
                Stai per rifiutare la pubblicazione di {batchRejectModal.gameIds.length}{' '}
                {batchRejectModal.gameIds.length === 1 ? 'gioco' : 'giochi'}. Tutti torneranno in
                stato Bozza con la stessa motivazione.
              </DialogDescription>
            </DialogHeader>

            <div className="space-y-4 mt-4">
              {/* Games list preview */}
              <div className="max-h-32 overflow-y-auto bg-muted/50 rounded-md p-3">
                <p className="text-sm font-medium mb-2">Giochi selezionati:</p>
                <ul className="text-sm text-muted-foreground space-y-1">
                  {batchRejectModal.gameTitles.slice(0, 5).map((title, idx) => (
                    <li key={idx} className="truncate">
                      • {title}
                    </li>
                  ))}
                  {batchRejectModal.gameTitles.length > 5 && (
                    <li className="text-muted-foreground/70">
                      ... e altri {batchRejectModal.gameTitles.length - 5}
                    </li>
                  )}
                </ul>
              </div>

              <div className="space-y-2">
                <Label htmlFor="batch-reject-reason">Motivazione comune del rifiuto *</Label>
                <Textarea
                  id="batch-reject-reason"
                  placeholder="Spiega perché questi giochi non possono essere pubblicati..."
                  value={batchRejectReason}
                  onChange={e => setBatchRejectReason(e.target.value)}
                  rows={4}
                />
                <p className="text-xs text-muted-foreground">
                  Questa motivazione sarà applicata a tutti i giochi selezionati.
                </p>
              </div>
            </div>

            <DialogFooter className="mt-6">
              <Button
                variant="outline"
                onClick={() => {
                  setBatchRejectModal({ isOpen: false, gameIds: [], gameTitles: [] });
                  setBatchRejectReason('');
                }}
                disabled={batchRejecting}
              >
                Annulla
              </Button>
              <Button
                variant="destructive"
                onClick={handleBatchRejectConfirm}
                disabled={batchRejecting || !batchRejectReason.trim()}
              >
                {batchRejecting ? (
                  <>
                    <Spinner size="sm" className="mr-2" />
                    Rifiutando...
                  </>
                ) : (
                  <>
                    <XCircle className="h-4 w-4 mr-2" />
                    Rifiuta {batchRejectModal.gameIds.length} Giochi
                  </>
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
              {toast.type === 'success' && <CheckCircle className="h-4 w-4 text-green-500" />}
              {toast.type === 'error' && <AlertCircle className="h-4 w-4" />}
              {toast.type === 'info' && <Clock className="h-4 w-4 text-blue-500" />}
              <AlertDescription>{toast.message}</AlertDescription>
            </Alert>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}
