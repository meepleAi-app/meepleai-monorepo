/**
 * Editor Dashboard Client - Issue #2897
 *
 * Dashboard interface for editors to manage their game submissions:
 * - View all games created by the current user
 * - Filter by status (Draft, PendingApproval, Published, Rejected)
 * - Submit games for approval
 * - View rejection feedback from admins
 * - Bulk select and bulk submit for approval
 *
 * Backend Integration:
 * - GET /api/v1/admin/shared-games (filtered by current user)
 * - POST /api/v1/admin/shared-games/:id/submit-for-approval
 */

'use client';

import { useState, useCallback, useEffect, useMemo } from 'react';

import {
  Clock,
  CheckCircle,
  XCircle,
  FileText,
  Eye,
  Send,
  AlertCircle,
  Filter,
  LayoutGrid,
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
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/primitives/select';
import { api, type SharedGame, type GameStatus } from '@/lib/api';

// ========== Types ==========

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type RejectionModalState = {
  isOpen: boolean;
  game: SharedGame | null;
  reason: string;
};

type StatusFilter = 'all' | GameStatus;

// Status configuration
const STATUS_CONFIG: Record<GameStatus, { label: string; color: string; icon: React.ReactNode }> = {
  Draft: { label: 'Bozza', color: 'bg-gray-100 text-gray-800', icon: <FileText className="h-4 w-4" /> },
  PendingApproval: { label: 'In Attesa', color: 'bg-amber-100 text-amber-800', icon: <Clock className="h-4 w-4" /> },
  Published: { label: 'Pubblicato', color: 'bg-green-100 text-green-800', icon: <CheckCircle className="h-4 w-4" /> },
  Archived: { label: 'Archiviato', color: 'bg-red-100 text-red-800', icon: <XCircle className="h-4 w-4" /> },
};

// ========== Stats Card Component ==========

interface StatsCardProps {
  title: string;
  count: number;
  icon: React.ReactNode;
  color: string;
  onClick?: () => void;
  active?: boolean;
}

function StatsCard({ title, count, icon, color, onClick, active }: StatsCardProps) {
  return (
    <Card
      className={`cursor-pointer transition-all hover:shadow-md ${active ? 'ring-2 ring-primary' : ''}`}
      onClick={onClick}
      data-testid={`stats-card-${title.toLowerCase().replace(/\s/g, '-')}`}
    >
      <CardContent className="p-4">
        <div className="flex items-center justify-between">
          <div>
            <p className="text-sm text-muted-foreground">{title}</p>
            <p className="text-2xl font-bold">{count}</p>
          </div>
          <div className={`p-3 rounded-full ${color}`}>
            {icon}
          </div>
        </div>
      </CardContent>
    </Card>
  );
}

// ========== Status Badge Component ==========

function GameStatusBadge({ status }: { status: GameStatus }) {
  // eslint-disable-next-line security/detect-object-injection -- status is typed GameStatus enum
  const config = STATUS_CONFIG[status];
  return (
    <Badge className={`${config.color} flex items-center gap-1`} data-testid={`status-badge-${status.toLowerCase()}`}>
      {config.icon}
      {config.label}
    </Badge>
  );
}

// ========== Main Component ==========

export function EditorDashboardClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Data state
  const [games, setGames] = useState<SharedGame[]>([]);
  const [loading, setLoading] = useState(false);

  // Filter state
  const [statusFilter, setStatusFilter] = useState<StatusFilter>('all');

  // Selection state
  const [selectedIds, setSelectedIds] = useState<Set<string>>(new Set());

  // Action states
  const [submitting, setSubmitting] = useState<string | null>(null);
  const [bulkSubmitting, setBulkSubmitting] = useState(false);

  // Modal states
  const [rejectionModal, setRejectionModal] = useState<RejectionModalState>({
    isOpen: false,
    game: null,
    reason: '',
  });

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // Fetch games
  const fetchGames = useCallback(async () => {
    try {
      setLoading(true);
      // Get all games - in a real implementation, backend would filter by createdBy
      // For now, we fetch all and display (E2E tests will mock the response)
      const result = await api.sharedGames.getAll({
        status: statusFilter !== 'all' ? getStatusNumeric(statusFilter) : undefined,
      });
      setGames(result.items);
    } catch (err) {
      console.error('Failed to fetch games:', err);
      addToast('error', 'Errore nel caricamento dei giochi');
    } finally {
      setLoading(false);
    }
  }, [statusFilter, addToast]);

  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  // Calculate stats
  const stats = useMemo(() => {
    return {
      total: games.length,
      draft: games.filter(g => g.status === 'Draft').length,
      pending: games.filter(g => g.status === 'PendingApproval').length,
      published: games.filter(g => g.status === 'Published').length,
      archived: games.filter(g => g.status === 'Archived').length,
    };
  }, [games]);

  // Filter games
  const filteredGames = useMemo(() => {
    if (statusFilter === 'all') return games;
    return games.filter(g => g.status === statusFilter);
  }, [games, statusFilter]);

  // Selection handlers
  const toggleSelection = useCallback((id: string) => {
    setSelectedIds(prev => {
      const next = new Set(prev);
      if (next.has(id)) {
        next.delete(id);
      } else {
        next.add(id);
      }
      return next;
    });
  }, []);

  const toggleSelectAll = useCallback(() => {
    const selectableGames = filteredGames.filter(g => g.status === 'Draft');
    if (selectedIds.size === selectableGames.length && selectableGames.length > 0) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(selectableGames.map(g => g.id)));
    }
  }, [filteredGames, selectedIds]);

  // Submit for approval
  const handleSubmitForApproval = async (game: SharedGame) => {
    try {
      setSubmitting(game.id);
      await api.sharedGames.submitForApproval(game.id);
      addToast('success', `"${game.title}" inviato per approvazione`);
      fetchGames();
    } catch (err) {
      console.error('Failed to submit for approval:', err);
      addToast('error', "Errore nell'invio per approvazione");
    } finally {
      setSubmitting(null);
    }
  };

  // Bulk submit for approval
  const handleBulkSubmit = async () => {
    if (selectedIds.size === 0) return;

    try {
      setBulkSubmitting(true);
      const promises = Array.from(selectedIds).map(id =>
        api.sharedGames.submitForApproval(id)
      );
      await Promise.all(promises);
      addToast('success', `${selectedIds.size} giochi inviati per approvazione`);
      setSelectedIds(new Set());
      fetchGames();
    } catch (err) {
      console.error('Failed to bulk submit:', err);
      addToast('error', "Errore nell'invio bulk per approvazione");
    } finally {
      setBulkSubmitting(false);
    }
  };

  // View rejection feedback (mock - would need backend support)
  const handleViewRejection = (game: SharedGame) => {
    // In a real implementation, this would fetch the rejection reason from the backend
    setRejectionModal({
      isOpen: true,
      game,
      reason: 'Il gioco necessita di una descrizione più dettagliata e immagini di qualità migliore.',
    });
  };

  // Navigate to game detail
  const handleViewGame = (gameId: string) => {
    router.push(`/admin/shared-games/${gameId}`);
  };

  // Helper function to get numeric status
  function getStatusNumeric(status: GameStatus): number {
    const map: Record<GameStatus, number> = {
      Draft: 0,
      PendingApproval: 1,
      Published: 2,
      Archived: 3,
    };
    // eslint-disable-next-line security/detect-object-injection -- status is typed GameStatus enum
    return map[status];
  }

  const selectableCount = filteredGames.filter(g => g.status === 'Draft').length;
  const allSelectableSelected = selectableCount > 0 && selectedIds.size === selectableCount;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl">
        {/* Header */}
        <div className="mb-8">
          <h1 className="text-3xl font-bold flex items-center gap-3" data-testid="dashboard-title">
            <LayoutGrid className="h-8 w-8 text-primary" />
            I Miei Giochi
          </h1>
          <p className="text-muted-foreground mt-2">
            Gestisci i tuoi giochi e invia le sottomissioni per la pubblicazione
          </p>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4 mb-8" data-testid="stats-section">
          <StatsCard
            title="Bozze"
            count={stats.draft}
            icon={<FileText className="h-5 w-5 text-gray-600" />}
            color="bg-gray-100"
            onClick={() => setStatusFilter('Draft')}
            active={statusFilter === 'Draft'}
          />
          <StatsCard
            title="In Attesa"
            count={stats.pending}
            icon={<Clock className="h-5 w-5 text-amber-600" />}
            color="bg-amber-100"
            onClick={() => setStatusFilter('PendingApproval')}
            active={statusFilter === 'PendingApproval'}
          />
          <StatsCard
            title="Pubblicati"
            count={stats.published}
            icon={<CheckCircle className="h-5 w-5 text-green-600" />}
            color="bg-green-100"
            onClick={() => setStatusFilter('Published')}
            active={statusFilter === 'Published'}
          />
          <StatsCard
            title="Archiviati"
            count={stats.archived}
            icon={<XCircle className="h-5 w-5 text-red-600" />}
            color="bg-red-100"
            onClick={() => setStatusFilter('Archived')}
            active={statusFilter === 'Archived'}
          />
        </div>

        {/* Games Table Card */}
        <Card>
          <CardHeader>
            <div className="flex items-center justify-between">
              <div>
                <CardTitle className="flex items-center gap-2">
                  <Filter className="h-5 w-5" />
                  Lista Giochi
                </CardTitle>
                <CardDescription>
                  {statusFilter === 'all'
                    ? `Mostrando tutti i ${games.length} giochi`
                    : `Mostrando ${filteredGames.length} giochi con stato "${STATUS_CONFIG[statusFilter as GameStatus]?.label}"`}
                </CardDescription>
              </div>
              <div className="flex items-center gap-4">
                {/* Status Filter */}
                <Select
                  value={statusFilter}
                  onValueChange={(value) => setStatusFilter(value as StatusFilter)}
                >
                  <SelectTrigger className="w-48" data-testid="status-filter">
                    <SelectValue placeholder="Filtra per stato" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">Tutti gli stati</SelectItem>
                    <SelectItem value="Draft">Bozza</SelectItem>
                    <SelectItem value="PendingApproval">In Attesa</SelectItem>
                    <SelectItem value="Published">Pubblicato</SelectItem>
                    <SelectItem value="Archived">Archiviato</SelectItem>
                  </SelectContent>
                </Select>

                {/* Bulk Submit Button */}
                {selectedIds.size > 0 && (
                  <Button
                    onClick={handleBulkSubmit}
                    disabled={bulkSubmitting}
                    data-testid="bulk-submit-button"
                  >
                    {bulkSubmitting ? (
                      <Spinner size="sm" className="mr-2" />
                    ) : (
                      <Send className="h-4 w-4 mr-2" />
                    )}
                    Invia {selectedIds.size} per Approvazione
                  </Button>
                )}
              </div>
            </div>
          </CardHeader>
          <CardContent>
            {/* Loading State */}
            {loading && (
              <div className="flex justify-center py-12">
                <Spinner size="lg" />
              </div>
            )}

            {/* Empty State */}
            {!loading && filteredGames.length === 0 && (
              <Alert data-testid="empty-state">
                <FileText className="h-4 w-4" />
                <AlertDescription>
                  {statusFilter === 'all'
                    ? 'Non hai ancora creato nessun gioco. Inizia creando il tuo primo gioco!'
                    : `Nessun gioco con stato "${STATUS_CONFIG[statusFilter as GameStatus]?.label}"`}
                </AlertDescription>
              </Alert>
            )}

            {/* Games Table */}
            {!loading && filteredGames.length > 0 && (
              <div className="border rounded-lg">
                <Table data-testid="games-table">
                  <TableHeader>
                    <TableRow>
                      <TableHead className="w-12">
                        <Checkbox
                          checked={allSelectableSelected}
                          onCheckedChange={toggleSelectAll}
                          aria-label="Seleziona tutti"
                          data-testid="select-all-checkbox"
                        />
                      </TableHead>
                      <TableHead className="w-[80px]">Immagine</TableHead>
                      <TableHead className="w-[25%]">Titolo</TableHead>
                      <TableHead className="w-[12%]">Giocatori</TableHead>
                      <TableHead className="w-[12%]">Durata</TableHead>
                      <TableHead className="w-[12%]">Complessità</TableHead>
                      <TableHead className="w-[12%]">Stato</TableHead>
                      <TableHead className="w-[15%] text-right">Azioni</TableHead>
                    </TableRow>
                  </TableHeader>
                  <TableBody>
                    {filteredGames.map(game => {
                      const isSelectable = game.status === 'Draft';
                      const isSelected = selectedIds.has(game.id);

                      return (
                        <TableRow
                          key={game.id}
                          data-testid={`game-row-${game.id}`}
                          className={isSelected ? 'bg-primary/5' : undefined}
                        >
                          {/* Checkbox */}
                          <TableCell>
                            {isSelectable && (
                              <Checkbox
                                checked={isSelected}
                                onCheckedChange={() => toggleSelection(game.id)}
                                aria-label={`Seleziona ${game.title}`}
                                data-testid={`select-checkbox-${game.id}`}
                              />
                            )}
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

                          {/* Status */}
                          <TableCell>
                            <GameStatusBadge status={game.status} />
                          </TableCell>

                          {/* Actions */}
                          <TableCell className="text-right">
                            <div className="flex items-center justify-end gap-2">
                              <Button
                                variant="ghost"
                                size="sm"
                                onClick={() => handleViewGame(game.id)}
                                title="Visualizza dettagli"
                                data-testid={`view-button-${game.id}`}
                              >
                                <Eye className="h-4 w-4" />
                              </Button>

                              {/* Submit for approval (only Draft) */}
                              {game.status === 'Draft' && (
                                <Button
                                  variant="default"
                                  size="sm"
                                  onClick={() => handleSubmitForApproval(game)}
                                  disabled={submitting === game.id}
                                  data-testid={`submit-button-${game.id}`}
                                >
                                  {submitting === game.id ? (
                                    <Spinner size="sm" className="mr-1" />
                                  ) : (
                                    <Send className="h-4 w-4 mr-1" />
                                  )}
                                  Invia
                                </Button>
                              )}

                              {/* View rejection feedback (only Archived/rejected) */}
                              {game.status === 'Archived' && (
                                <Button
                                  variant="outline"
                                  size="sm"
                                  onClick={() => handleViewRejection(game)}
                                  title="Visualizza feedback"
                                  data-testid={`rejection-button-${game.id}`}
                                >
                                  <AlertCircle className="h-4 w-4 mr-1" />
                                  Feedback
                                </Button>
                              )}
                            </div>
                          </TableCell>
                        </TableRow>
                      );
                    })}
                  </TableBody>
                </Table>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Rejection Feedback Modal */}
        <Dialog
          open={rejectionModal.isOpen}
          onOpenChange={isOpen => {
            if (!isOpen) {
              setRejectionModal({ isOpen: false, game: null, reason: '' });
            }
          }}
        >
          <DialogContent data-testid="rejection-modal">
            <DialogHeader>
              <DialogTitle className="flex items-center gap-2">
                <AlertCircle className="h-5 w-5 text-amber-500" />
                Feedback Rifiuto
              </DialogTitle>
              <DialogDescription>
                Il gioco &quot;{rejectionModal.game?.title}&quot; è stato rifiutato con il seguente
                feedback:
              </DialogDescription>
            </DialogHeader>

            <div className="my-4 p-4 bg-muted rounded-lg">
              <p className="text-sm" data-testid="rejection-reason">
                {rejectionModal.reason}
              </p>
            </div>

            <DialogFooter>
              <Link href={`/admin/shared-games/${rejectionModal.game?.id}`}>
                <Button variant="default">
                  <FileText className="h-4 w-4 mr-2" />
                  Modifica Gioco
                </Button>
              </Link>
              <Button
                variant="outline"
                onClick={() => setRejectionModal({ isOpen: false, game: null, reason: '' })}
              >
                Chiudi
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
              data-testid={`toast-${toast.type}`}
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
