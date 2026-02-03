/**
 * Pending Approvals Client - Issue #2514
 *
 * Admin interface for reviewing and approving games submitted for publication:
 * - List all games in PendingApproval status
 * - Approve games (PendingApproval → Published)
 * - Reject games with reason (PendingApproval → Draft)
 *
 * Backend Integration:
 * - GET /api/v1/admin/shared-games/pending-approvals
 * - POST /api/v1/admin/shared-games/:id/approve-publication
 * - POST /api/v1/admin/shared-games/:id/reject-publication
 */

'use client';

import { useState, useCallback, useEffect } from 'react';

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

  // Action states
  const [approving, setApproving] = useState<string | null>(null);
  const [rejecting, setRejecting] = useState(false);

  // Modal states
  const [rejectModal, setRejectModal] = useState<RejectModalState>({
    isOpen: false,
    game: null,
  });
  const [rejectReason, setRejectReason] = useState('');

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
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
    if (!rejectModal.game) return;

    if (!rejectReason.trim()) {
      addToast('error', 'Devi fornire una motivazione per il rifiuto');
      return;
    }

    try {
      setRejecting(true);
      await api.sharedGames.rejectPublication(rejectModal.game.id, rejectReason);
      addToast('info', `"${rejectModal.game.title}" rifiutato e riportato in bozza`);
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

        {/* Reject Confirmation Modal */}
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
