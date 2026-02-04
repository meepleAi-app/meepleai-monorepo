/**
 * Shared Games Catalog Admin Client - Issue #3534 (Refactored)
 *
 * Admin dashboard with card grid layout for managing shared game catalog:
 * - Stats cards showing key metrics
 * - Card grid with responsive layout (1-4 columns)
 * - Advanced filters with URL params for shareable links
 * - React Query for data fetching
 * - Role-based permissions (Admin vs Editor)
 *
 * Backend Integration:
 * - GET /api/v1/admin/shared-games (filtered list)
 * - GET /api/v1/admin/shared-games/approval-queue (approval queue)
 * - POST /api/v1/admin/shared-games/:id/documents/:docId/approve
 */

'use client';

import { Suspense, useCallback, useState, useEffect } from 'react';

import { useQuery } from '@tanstack/react-query';
import {
  Plus,
  Trash2,
  AlertCircle,
  RefreshCw,
  LayoutGrid,
  List,
  Clock,
  Upload,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Tabs, TabsList, TabsTrigger } from '@/components/ui/navigation/tabs';
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

import { GameCard, GameCardSkeleton, ImportFromBggModal, SharedGamesFilters, SharedGamesStats } from './_components';
import { useSharedGamesQuery } from './_hooks';

// ========== Types ==========

type ToastMessage = {
  id: string;
  type: 'success' | 'error' | 'info';
  message: string;
};

type ViewMode = 'grid' | 'list';

// ========== Main Component ==========

function SharedGamesClientInner() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // View mode state (persisted in localStorage)
  const [viewMode, setViewMode] = useState<ViewMode>('grid');

  // Modal states
  const [isDeleteModalOpen, setIsDeleteModalOpen] = useState(false);
  const [isImportModalOpen, setIsImportModalOpen] = useState(false);
  const [gameToDelete, setGameToDelete] = useState<SharedGame | null>(null);
  const [deleteReason, setDeleteReason] = useState('');
  const [isDeleting, setIsDeleting] = useState(false);

  // Toast management
  const [toasts, setToasts] = useState<ToastMessage[]>([]);

  // Check if user is admin
  const isAdmin = user?.role?.toLowerCase() === 'admin';

  // Load view mode preference
  useEffect(() => {
    const saved = localStorage.getItem('sharedGames.viewMode');
    if (saved === 'grid' || saved === 'list') {
      setViewMode(saved);
    }
  }, []);

  // Save view mode preference
  const handleViewModeChange = useCallback((mode: ViewMode) => {
    setViewMode(mode);
    localStorage.setItem('sharedGames.viewMode', mode);
  }, []);

  // Use React Query hook for data
  const {
    data: gamesData,
    isLoading,
    error,
    refetch,
    filters,
    setSearch,
    setStatus,
    setSortBy,
    setPage,
    setSubmittedBy,
    totalPages,
  } = useSharedGamesQuery({ defaultPageSize: 20 });

  // Fetch stats data
  const { data: statsData } = useQuery({
    queryKey: ['admin', 'shared-games', 'stats'],
    queryFn: async () => {
      // Fetch pending approvals count
      const pendingResult = await api.sharedGames.getPendingApprovals({ pageSize: 1 });
      const totalResult = await api.sharedGames.getAll({ pageSize: 1 });

      return {
        totalGames: totalResult.total,
        pendingApprovals: pendingResult.total,
        pdfCoveragePercent: 45, // TODO: Implement actual calculation
        avgReviewDays: 3, // TODO: Implement actual calculation
      };
    },
    staleTime: 60000, // 1 minute
  });

  const addToast = useCallback((type: 'success' | 'error' | 'info', message: string) => {
    const id = `toast-${Date.now()}-${Math.random()}`;
    setToasts(prev => [...prev, { id, type, message }]);
    setTimeout(() => {
      setToasts(prev => prev.filter(t => t.id !== id));
    }, 5000);
  }, []);

  // Handle publish
  const handlePublish = async (game: SharedGame) => {
    try {
      await api.sharedGames.publish(game.id);
      addToast('success', `"${game.title}" published successfully`);
      refetch();
    } catch (err) {
      console.error('Failed to publish game:', err);
      addToast('error', 'Failed to publish game');
    }
  };

  // Handle archive
  const handleArchive = async (game: SharedGame) => {
    try {
      await api.sharedGames.archive(game.id);
      addToast('success', `"${game.title}" archived successfully`);
      refetch();
    } catch (err) {
      console.error('Failed to archive game:', err);
      addToast('error', 'Failed to archive game');
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
      addToast('error', 'Please provide a reason for the delete request');
      return;
    }

    try {
      setIsDeleting(true);

      if (isAdmin) {
        await api.sharedGames.delete(gameToDelete.id);
        addToast('success', `"${gameToDelete.title}" deleted successfully`);
      } else {
        await api.sharedGames.requestDelete(gameToDelete.id, { reason: deleteReason });
        addToast('info', 'Delete request submitted for admin approval');
      }

      setIsDeleteModalOpen(false);
      setGameToDelete(null);
      setDeleteReason('');
      refetch();
    } catch (err) {
      console.error('Failed to delete game:', err);
      addToast('error', 'Failed to delete game');
    } finally {
      setIsDeleting(false);
    }
  };

  // Navigate to create page
  const handleCreate = () => {
    router.push('/admin/shared-games/new');
  };

  // Navigate to edit page
  const handleEdit = (game: SharedGame) => {
    router.push(`/admin/shared-games/${game.id}`);
  };

  // Navigate to preview
  const handlePreview = (game: SharedGame) => {
    router.push(`/admin/shared-games/${game.id}`);
  };

  // Pagination
  const canPrevPage = filters.page > 1;
  const canNextPage = filters.page < totalPages;

  const games = gamesData?.items ?? [];

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto space-y-6 p-6 max-w-7xl">
        {/* Header */}
        <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
          <div>
            <h1 className="text-2xl font-bold tracking-tight">Shared Game Catalog</h1>
            <p className="text-muted-foreground">
              Manage the shared game catalog with CRUD operations and BGG integration
            </p>
          </div>
          <div className="flex items-center gap-3">
            {/* Pending Approvals Link (Admin Only) */}
            {isAdmin && statsData && statsData.pendingApprovals > 0 && (
              <Link href="/admin/shared-games/pending-approvals">
                <Button variant="outline" className="relative">
                  <Clock className="h-4 w-4 mr-2" />
                  Approvals
                  <span className="absolute -top-2 -right-2 bg-amber-500 text-white text-xs font-bold rounded-full h-5 w-5 flex items-center justify-center">
                    {statsData.pendingApprovals > 99 ? '99+' : statsData.pendingApprovals}
                  </span>
                </Button>
              </Link>
            )}
            <Button variant="outline" onClick={() => setIsImportModalOpen(true)}>
              <Upload className="h-4 w-4 mr-2" />
              Import from BGG
            </Button>
            <Button onClick={handleCreate}>
              <Plus className="h-4 w-4 mr-2" />
              New Game
            </Button>
          </div>
        </div>

        {/* Stats Cards */}
        <SharedGamesStats
          totalGames={statsData?.totalGames ?? 0}
          pendingApprovals={statsData?.pendingApprovals ?? 0}
          pdfCoveragePercent={statsData?.pdfCoveragePercent ?? 0}
          avgReviewDays={statsData?.avgReviewDays ?? 0}
          isLoading={!statsData}
        />

        {/* Main Content Card */}
        <Card>
          <CardHeader className="pb-4">
            <div className="flex flex-col gap-4 sm:flex-row sm:items-center sm:justify-between">
              <div>
                <CardTitle>Games</CardTitle>
                <CardDescription>
                  {gamesData?.total ?? 0} games in catalog
                </CardDescription>
              </div>
              <div className="flex items-center gap-2">
                {/* View Mode Toggle */}
                <Tabs value={viewMode} onValueChange={(v: string) => handleViewModeChange(v as ViewMode)}>
                  <TabsList className="h-9">
                    <TabsTrigger value="grid" className="px-3">
                      <LayoutGrid className="h-4 w-4" />
                    </TabsTrigger>
                    <TabsTrigger value="list" className="px-3">
                      <List className="h-4 w-4" />
                    </TabsTrigger>
                  </TabsList>
                </Tabs>
                {/* Refresh Button */}
                <Button variant="outline" size="icon" onClick={() => refetch()}>
                  <RefreshCw className="h-4 w-4" />
                  <span className="sr-only">Refresh</span>
                </Button>
              </div>
            </div>
          </CardHeader>

          <CardContent className="space-y-6">
            {/* Filters */}
            <SharedGamesFilters
              search={filters.search}
              status={filters.status}
              sortBy={filters.sortBy}
              submittedBy={filters.submittedBy}
              onSearchChange={setSearch}
              onStatusChange={setStatus}
              onSortChange={setSortBy}
              onSubmitterChange={isAdmin ? setSubmittedBy : undefined}
              isAdmin={isAdmin}
              debounceMs={300}
            />

            {/* Error State */}
            {error && (
              <Alert variant="destructive">
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  Failed to load games. Please try again.
                </AlertDescription>
              </Alert>
            )}

            {/* Loading State */}
            {isLoading && (
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                {Array.from({ length: 8 }).map((_, i) => (
                  <GameCardSkeleton key={i} />
                ))}
              </div>
            )}

            {/* Empty State */}
            {!isLoading && games.length === 0 && (
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  {filters.search || filters.status !== 'all'
                    ? 'No games found matching your filters.'
                    : 'No games in the catalog. Click "New Game" to add one.'}
                </AlertDescription>
              </Alert>
            )}

            {/* Games Grid */}
            {!isLoading && games.length > 0 && viewMode === 'grid' && (
              <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3 xl:grid-cols-4">
                {games.map((game) => (
                  <GameCard
                    key={game.id}
                    game={game}
                    isAdmin={isAdmin}
                    onEdit={handleEdit}
                    onPreview={handlePreview}
                    onPublish={handlePublish}
                    onArchive={handleArchive}
                    onDelete={handleDeleteClick}
                  />
                ))}
              </div>
            )}

            {/* Games List View (fallback to simple list for now) */}
            {!isLoading && games.length > 0 && viewMode === 'list' && (
              <div className="space-y-2">
                {games.map((game) => (
                  <div
                    key={game.id}
                    className="flex items-center justify-between rounded-lg border p-4 hover:bg-muted/50"
                  >
                    <div className="flex items-center gap-4">
                      <div className="h-12 w-12 rounded bg-muted" />
                      <div>
                        <p className="font-medium">{game.title}</p>
                        <p className="text-sm text-muted-foreground">
                          {game.yearPublished} • {game.minPlayers}-{game.maxPlayers} players
                        </p>
                      </div>
                    </div>
                    <Button variant="ghost" size="sm" onClick={() => handleEdit(game)}>
                      View
                    </Button>
                  </div>
                ))}
              </div>
            )}

            {/* Pagination */}
            {totalPages > 1 && (
              <div className="flex items-center justify-between pt-4">
                <div className="text-sm text-muted-foreground">
                  Page {filters.page} of {totalPages}
                </div>
                <div className="flex items-center gap-2">
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage(filters.page - 1)}
                    disabled={!canPrevPage}
                  >
                    Previous
                  </Button>
                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => setPage(filters.page + 1)}
                    disabled={!canNextPage}
                  >
                    Next
                  </Button>
                </div>
              </div>
            )}
          </CardContent>
        </Card>

        {/* Delete Confirmation Modal */}
        <Dialog open={isDeleteModalOpen} onOpenChange={setIsDeleteModalOpen}>
          <DialogContent>
            <DialogHeader>
              <DialogTitle>{isAdmin ? 'Delete Game' : 'Request Deletion'}</DialogTitle>
              <DialogDescription>
                {isAdmin
                  ? `Are you sure you want to delete "${gameToDelete?.title}"? This action cannot be undone.`
                  : `Your deletion request for "${gameToDelete?.title}" will be sent to an admin for approval.`}
              </DialogDescription>
            </DialogHeader>

            {/* Reason (required for Editors) */}
            {!isAdmin && (
              <div className="space-y-2 mt-4">
                <Label htmlFor="delete-reason">Reason *</Label>
                <Textarea
                  id="delete-reason"
                  placeholder="Explain why this game should be deleted..."
                  value={deleteReason}
                  onChange={(e) => setDeleteReason(e.target.value)}
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
                Cancel
              </Button>
              <Button
                variant="destructive"
                onClick={handleDeleteConfirm}
                disabled={isDeleting || (!isAdmin && !deleteReason.trim())}
              >
                {isDeleting ? (
                  <>
                    <Spinner size="sm" className="mr-2" />
                    {isAdmin ? 'Deleting...' : 'Submitting...'}
                  </>
                ) : isAdmin ? (
                  <>
                    <Trash2 className="h-4 w-4 mr-2" />
                    Delete
                  </>
                ) : (
                  'Submit Request'
                )}
              </Button>
            </DialogFooter>
          </DialogContent>
        </Dialog>

        {/* Import from BGG Modal */}
        <ImportFromBggModal
          open={isImportModalOpen}
          onOpenChange={setIsImportModalOpen}
          onSuccess={(gameId) => {
            addToast('success', 'Game imported successfully from BGG');
            refetch();
            // Optionally navigate to the game detail page
            router.push(`/admin/shared-games/${gameId}`);
          }}
        />

        {/* Toast Notifications */}
        <div className="fixed bottom-4 right-4 z-50 space-y-2">
          {toasts.map((toast) => (
            <Alert
              key={toast.id}
              variant={toast.type === 'error' ? 'destructive' : 'default'}
              className="w-96 shadow-lg"
            >
              <AlertDescription>{toast.message}</AlertDescription>
            </Alert>
          ))}
        </div>
      </div>
    </AdminAuthGuard>
  );
}

// Wrap with Suspense for useSearchParams
export function SharedGamesClient() {
  return (
    <Suspense
      fallback={
        <div className="flex h-96 items-center justify-center">
          <Spinner size="lg" />
        </div>
      }
    >
      <SharedGamesClientInner />
    </Suspense>
  );
}
