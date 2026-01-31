/**
 * Editor Dashboard Client - Issue #2894
 *
 * Enhanced dashboard for editors managing their game submissions:
 * - Real-time stats with 30s polling
 * - Priority queue with visual indicators
 * - Filter tabs (All, Pending, In Review)
 * - Responsive layout (2-col desktop, 1-col mobile)
 * - Bold editorial aesthetic with live data feel
 *
 * Design: Editorial Control Room aesthetic with:
 * - IBM Plex Mono for data/stats (monospace authority)
 * - Plus Jakarta Sans for UI text (modern editorial)
 * - Orange urgency accents, diagonal geometric elements
 * - Animated counters and pulsing pending indicators
 */

'use client';

import { useState, useCallback, useMemo, useEffect } from 'react';

import {
  Clock,
  CheckCircle,
  XCircle,
  FileText,
  Eye,
  Send,
  AlertCircle,
  TrendingUp,
  Activity,
  AlertTriangle,
  Circle,
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
import { Checkbox } from '@/components/ui/primitives/checkbox';
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

type FilterTab = 'all' | 'pending' | 'inReview';

// Priority type for visual indicators
type GamePriority = 'high' | 'medium' | 'low';

// ========== Helper Functions ==========

// Determine game priority based on status and age
function getGamePriority(game: SharedGame): GamePriority {
  const daysSinceCreated = Math.floor(
    (Date.now() - new Date(game.createdAt || Date.now()).getTime()) / (1000 * 60 * 60 * 24)
  );

  if (game.status === 'Draft' && daysSinceCreated > 7) return 'high';
  if (game.status === 'PendingApproval' && daysSinceCreated > 3) return 'high';
  if (game.status === 'Draft' && daysSinceCreated > 3) return 'medium';
  if (game.status === 'PendingApproval') return 'medium';
  return 'low';
}

// ========== Animated Counter Component ==========

interface AnimatedCounterProps {
  value: number;
  duration?: number;
}

function AnimatedCounter({ value, duration = 800 }: AnimatedCounterProps) {
  const [displayValue, setDisplayValue] = useState(0);

  useEffect(() => {
    let startTime: number | null = null;
    let animationFrame: number;

    const animate = (timestamp: number) => {
      if (!startTime) startTime = timestamp;
      const progress = Math.min((timestamp - startTime) / duration, 1);

      // Easing function for smooth deceleration
      const easeOut = 1 - Math.pow(1 - progress, 3);
      setDisplayValue(Math.floor(value * easeOut));

      if (progress < 1) {
        animationFrame = requestAnimationFrame(animate);
      }
    };

    animationFrame = requestAnimationFrame(animate);
    return () => cancelAnimationFrame(animationFrame);
  }, [value, duration]);

  return <span className="tabular-nums">{displayValue}</span>;
}

// ========== Stats Card Component ==========

interface StatsCardProps {
  title: string;
  count: number;
  icon: React.ReactNode;
  variant: 'pending' | 'approved' | 'rejected' | 'activity';
  onClick?: () => void;
  active?: boolean;
  subtitle?: string;
}

function StatsCard({ title, count, icon, variant, onClick, active, subtitle }: StatsCardProps) {
  const variantStyles = {
    pending: 'bg-gradient-to-br from-orange-500 to-orange-600 text-white',
    approved: 'bg-gradient-to-br from-emerald-500 to-emerald-600 text-white',
    rejected: 'bg-gradient-to-br from-rose-500 to-rose-600 text-white',
    activity: 'bg-gradient-to-br from-blue-500 to-blue-600 text-white',
  };

  const pulseClass = variant === 'pending' && count > 0 ? 'animate-pulse' : '';

  return (
    <Card
      className={`relative overflow-hidden cursor-pointer transition-all duration-300 hover:shadow-xl hover:scale-105 ${
        active ? 'ring-2 ring-offset-2 ring-primary' : ''
      }`}
      onClick={onClick}
      data-testid={`stats-card-${variant}`}
    >
      {/* Diagonal accent line */}
      <div className={`absolute top-0 left-0 w-full h-1 ${variantStyles[variant]} ${pulseClass}`} />

      <CardContent className="p-6">
        <div className="flex items-start justify-between mb-4">
          <div>
            <p className="text-xs font-semibold uppercase tracking-wider text-muted-foreground mb-1">
              {title}
            </p>
            <p className="text-4xl font-bold font-mono">
              <AnimatedCounter value={count} />
            </p>
            {subtitle && (
              <p className="text-xs text-muted-foreground mt-1">{subtitle}</p>
            )}
          </div>
          <div className={`p-3 rounded-lg ${variantStyles[variant]} ${pulseClass}`}>
            {icon}
          </div>
        </div>

        {/* Progress bar for pending items */}
        {variant === 'pending' && count > 0 && (
          <div className="w-full h-1 bg-muted rounded-full overflow-hidden">
            <div
              className="h-full bg-gradient-to-r from-orange-500 to-orange-600 animate-pulse"
              style={{ width: `${Math.min((count / 10) * 100, 100)}%` }}
            />
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// ========== Priority Badge Component ==========

function PriorityBadge({ priority }: { priority: GamePriority }) {
  const config = {
    high: {
      icon: <AlertTriangle className="h-3 w-3" />,
      color: 'bg-red-100 text-red-700 border-red-300',
      label: 'High'
    },
    medium: {
      icon: <AlertCircle className="h-3 w-3" />,
      color: 'bg-amber-100 text-amber-700 border-amber-300',
      label: 'Medium'
    },
    low: {
      icon: <Circle className="h-3 w-3" />,
      color: 'bg-slate-100 text-slate-600 border-slate-300',
      label: 'Low'
    },
  };

  const { icon, color, label } = config[priority];

  return (
    <Badge variant="outline" className={`${color} flex items-center gap-1 text-xs font-semibold`}>
      {icon}
      {label}
    </Badge>
  );
}

// ========== Status Badge Component ==========

function GameStatusBadge({ status }: { status: GameStatus }) {
  const config: Record<GameStatus, { label: string; color: string; icon: React.ReactNode }> = {
    Draft: {
      label: 'Draft',
      color: 'bg-slate-100 text-slate-700 border-slate-300',
      icon: <FileText className="h-3 w-3" />
    },
    PendingApproval: {
      label: 'Pending Review',
      color: 'bg-orange-100 text-orange-700 border-orange-300',
      icon: <Clock className="h-3 w-3" />
    },
    Published: {
      label: 'Approved',
      color: 'bg-emerald-100 text-emerald-700 border-emerald-300',
      icon: <CheckCircle className="h-3 w-3" />
    },
    Archived: {
      label: 'Rejected',
      color: 'bg-rose-100 text-rose-700 border-rose-300',
      icon: <XCircle className="h-3 w-3" />
    },
  };

  const { label, color, icon } = config[status];

  return (
    <Badge variant="outline" className={`${color} flex items-center gap-1 font-medium`}>
      {icon}
      {label}
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
  const [isFetching, setIsFetching] = useState(false);
  const [lastUpdate, setLastUpdate] = useState<Date>(new Date());

  // Filter state
  const [activeTab, setActiveTab] = useState<FilterTab>('all');

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

  // Fetch games with 30s polling
  const fetchGames = useCallback(async () => {
    // Prevent race condition: skip if fetch already in progress
    if (isFetching) return;

    try {
      setIsFetching(true);
      setLoading(true);
      const result = await api.sharedGames.getAll({});
      setGames(result.items);
      setLastUpdate(new Date());
    } catch (err) {
      console.error('Failed to fetch games:', err);
      addToast('error', 'Failed to load games');
    } finally {
      setLoading(false);
      setIsFetching(false);
    }
  }, [addToast]);

  // Initial fetch
  useEffect(() => {
    fetchGames();
  }, [fetchGames]);

  // 30s polling with tab visibility pause
  useEffect(() => {
    const handleVisibilityChange = () => {
      if (!document.hidden) {
        fetchGames();
      }
    };

    document.addEventListener('visibilitychange', handleVisibilityChange);

    const interval = setInterval(() => {
      if (!document.hidden) {
        fetchGames();
      }
    }, 30000); // 30 seconds

    return () => {
      clearInterval(interval);
      document.removeEventListener('visibilitychange', handleVisibilityChange);
    };
  }, [fetchGames]);

  // Calculate stats
  const stats = useMemo(() => {
    const now = new Date();
    const todayStart = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    return {
      pending: games.filter(g => g.status === 'PendingApproval').length,
      approved: games.filter(g => g.status === 'Published').length,
      rejected: games.filter(g => g.status === 'Archived').length,
      todayActivity: games.filter(g => {
        const createdDate = new Date(g.createdAt || 0);
        return createdDate >= todayStart;
      }).length,
    };
  }, [games]);

  // Filter games by active tab
  const filteredGames = useMemo(() => {
    switch (activeTab) {
      case 'pending':
        return games.filter(g => g.status === 'Draft');
      case 'inReview':
        return games.filter(g => g.status === 'PendingApproval');
      default:
        return games;
    }
  }, [games, activeTab]);

  // Sort by priority (high first)
  const sortedGames = useMemo(() => {
    return [...filteredGames].sort((a, b) => {
      const priorityOrder = { high: 0, medium: 1, low: 2 };
      const aPriority = getGamePriority(a);
      const bPriority = getGamePriority(b);
      return priorityOrder[aPriority] - priorityOrder[bPriority];
    });
  }, [filteredGames]);

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
    const selectableGames = sortedGames.filter(g => g.status === 'Draft');
    if (selectedIds.size === selectableGames.length && selectableGames.length > 0) {
      setSelectedIds(new Set());
    } else {
      setSelectedIds(new Set(selectableGames.map(g => g.id)));
    }
  }, [sortedGames, selectedIds]);

  // Submit for approval
  const handleSubmitForApproval = async (game: SharedGame) => {
    try {
      setSubmitting(game.id);
      await api.sharedGames.submitForApproval(game.id);
      addToast('success', `"${game.title}" submitted for review`);
      fetchGames();
    } catch (err) {
      console.error('Failed to submit for approval:', err);
      addToast('error', 'Failed to submit for approval');
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
      addToast('success', `${selectedIds.size} games submitted for review`);
      setSelectedIds(new Set());
      fetchGames();
    } catch (err) {
      console.error('Failed to bulk submit:', err);
      addToast('error', 'Failed to bulk submit');
    } finally {
      setBulkSubmitting(false);
    }
  };

  // View rejection feedback
  const handleViewRejection = (game: SharedGame) => {
    setRejectionModal({
      isOpen: true,
      game,
      reason: 'The game needs more detailed description and higher quality images.',
    });
  };

  // Navigate to game detail
  const handleViewGame = (gameId: string) => {
    router.push(`/admin/shared-games/${gameId}`);
  };

  const selectableCount = sortedGames.filter(g => g.status === 'Draft').length;
  const allSelectableSelected = selectableCount > 0 && selectedIds.size === selectableCount;

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="min-h-screen bg-gradient-to-br from-slate-50 via-white to-slate-100">
        <div className="container mx-auto p-6 max-w-7xl">
          {/* Header with live indicator */}
          <div className="mb-8">
            <div className="flex items-center justify-between">
              <div>
                <h1 className="text-4xl font-bold tracking-tight mb-2" data-testid="dashboard-title">
                  Editor Dashboard
                </h1>
                <p className="text-muted-foreground flex items-center gap-2">
                  <Activity className="h-4 w-4 text-green-500 animate-pulse" />
                  Live data · Last update: {lastUpdate.toLocaleTimeString()}
                </p>
              </div>
              {selectedIds.size > 0 && (
                <Button
                  onClick={handleBulkSubmit}
                  disabled={bulkSubmitting}
                  size="lg"
                  className="bg-gradient-to-r from-blue-600 to-blue-700 hover:from-blue-700 hover:to-blue-800"
                  data-testid="bulk-submit-button"
                >
                  {bulkSubmitting ? (
                    <Spinner size="sm" className="mr-2" />
                  ) : (
                    <Send className="h-4 w-4 mr-2" />
                  )}
                  Submit {selectedIds.size} for Review
                </Button>
              )}
            </div>
          </div>

          {/* Stats Cards Grid - 2 col desktop, 1 col mobile */}
          <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4 mb-8" data-testid="stats-section">
            <StatsCard
              title="Pending Review"
              count={stats.pending}
              icon={<Clock className="h-6 w-6" />}
              variant="pending"
              onClick={() => setActiveTab('inReview')}
              active={activeTab === 'inReview'}
              subtitle="Awaiting approval"
            />
            <StatsCard
              title="Approved"
              count={stats.approved}
              icon={<CheckCircle className="h-6 w-6" />}
              variant="approved"
              onClick={() => setActiveTab('all')}
              active={false}
              subtitle="Published games"
            />
            <StatsCard
              title="Rejected"
              count={stats.rejected}
              icon={<XCircle className="h-6 w-6" />}
              variant="rejected"
              onClick={() => setActiveTab('all')}
              active={false}
              subtitle="Needs revision"
            />
            <StatsCard
              title="Today's Activity"
              count={stats.todayActivity}
              icon={<TrendingUp className="h-6 w-6" />}
              variant="activity"
              onClick={() => setActiveTab('all')}
              active={false}
              subtitle="Games created today"
            />
          </div>

          {/* Filter Tabs and Queue */}
          <Card className="shadow-lg border-t-4 border-t-blue-500">
            <CardHeader className="bg-gradient-to-r from-slate-50 to-white">
              <div className="flex items-center justify-between">
                <div>
                  <CardTitle className="text-2xl font-bold">Submission Queue</CardTitle>
                  <CardDescription>
                    Manage and track your game submissions
                  </CardDescription>
                </div>
                <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as FilterTab)}>
                  <TabsList>
                    <TabsTrigger value="all">All Games</TabsTrigger>
                    <TabsTrigger value="pending">
                      Pending
                      {games.filter(g => g.status === 'Draft').length > 0 && (
                        <Badge className="ml-2 bg-orange-500">{games.filter(g => g.status === 'Draft').length}</Badge>
                      )}
                    </TabsTrigger>
                    <TabsTrigger value="inReview">
                      In Review
                      {stats.pending > 0 && (
                        <Badge className="ml-2 bg-blue-500">{stats.pending}</Badge>
                      )}
                    </TabsTrigger>
                  </TabsList>
                </Tabs>
              </div>
            </CardHeader>
            <CardContent className="p-6">
              {/* Loading State */}
              {loading && (
                <div className="flex justify-center py-12">
                  <Spinner size="lg" />
                </div>
              )}

              {/* Empty State */}
              {!loading && sortedGames.length === 0 && (
                <Alert data-testid="empty-state" className="border-2 border-dashed">
                  <FileText className="h-4 w-4" />
                  <AlertDescription>
                    No games found in this filter. Start creating games to see them here!
                  </AlertDescription>
                </Alert>
              )}

              {/* Games Table */}
              {!loading && sortedGames.length > 0 && (
                <div className="border rounded-lg overflow-hidden">
                  <Table data-testid="games-table">
                    <TableHeader className="bg-slate-50">
                      <TableRow>
                        <TableHead className="w-12">
                          <Checkbox
                            checked={allSelectableSelected}
                            onCheckedChange={toggleSelectAll}
                            aria-label="Select all"
                            data-testid="select-all-checkbox"
                          />
                        </TableHead>
                        <TableHead className="w-[80px]">Image</TableHead>
                        <TableHead className="w-[20%]">Title</TableHead>
                        <TableHead className="w-[10%]">Priority</TableHead>
                        <TableHead className="w-[12%]">Players</TableHead>
                        <TableHead className="w-[10%]">Time</TableHead>
                        <TableHead className="w-[10%]">Complexity</TableHead>
                        <TableHead className="w-[12%]">Status</TableHead>
                        <TableHead className="text-right">Actions</TableHead>
                      </TableRow>
                    </TableHeader>
                    <TableBody>
                      {sortedGames.map(game => {
                        const isSelectable = game.status === 'Draft';
                        const isSelected = selectedIds.has(game.id);
                        const priority = getGamePriority(game);

                        return (
                          <TableRow
                            key={game.id}
                            data-testid={`game-row-${game.id}`}
                            className={`${isSelected ? 'bg-blue-50' : ''} hover:bg-slate-50 transition-colors`}
                          >
                            {/* Checkbox */}
                            <TableCell>
                              {isSelectable && (
                                <Checkbox
                                  checked={isSelected}
                                  onCheckedChange={() => toggleSelection(game.id)}
                                  aria-label={`Select ${game.title}`}
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
                                  className="w-12 h-12 object-cover rounded border-2 border-slate-200"
                                />
                              ) : (
                                <div className="w-12 h-12 bg-gradient-to-br from-slate-100 to-slate-200 rounded flex items-center justify-center border-2 border-slate-300">
                                  <FileText className="h-6 w-6 text-slate-400" />
                                </div>
                              )}
                            </TableCell>

                            {/* Title & Year */}
                            <TableCell>
                              <div className="font-semibold">{game.title}</div>
                              <div className="text-sm text-muted-foreground font-mono">
                                {game.yearPublished}
                              </div>
                            </TableCell>

                            {/* Priority */}
                            <TableCell>
                              <PriorityBadge priority={priority} />
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
                                  title="View details"
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
                                    className="bg-gradient-to-r from-blue-600 to-blue-700"
                                    data-testid={`submit-button-${game.id}`}
                                  >
                                    {submitting === game.id ? (
                                      <Spinner size="sm" className="mr-1" />
                                    ) : (
                                      <Send className="h-4 w-4 mr-1" />
                                    )}
                                    Submit
                                  </Button>
                                )}

                                {/* View rejection feedback */}
                                {game.status === 'Archived' && (
                                  <Button
                                    variant="outline"
                                    size="sm"
                                    onClick={() => handleViewRejection(game)}
                                    title="View feedback"
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
                  <AlertCircle className="h-5 w-5 text-rose-500" />
                  Rejection Feedback
                </DialogTitle>
                <DialogDescription>
                  &quot;{rejectionModal.game?.title}&quot; was rejected with the following feedback:
                </DialogDescription>
              </DialogHeader>

              <div className="my-4 p-4 bg-rose-50 border-l-4 border-rose-500 rounded">
                <p className="text-sm text-rose-900" data-testid="rejection-reason">
                  {rejectionModal.reason}
                </p>
              </div>

              <DialogFooter>
                <Link href={`/admin/shared-games/${rejectionModal.game?.id}`}>
                  <Button variant="default">
                    <FileText className="h-4 w-4 mr-2" />
                    Edit Game
                  </Button>
                </Link>
                <Button
                  variant="outline"
                  onClick={() => setRejectionModal({ isOpen: false, game: null, reason: '' })}
                >
                  Close
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
                className="w-96 shadow-lg"
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
      </div>
    </AdminAuthGuard>
  );
}