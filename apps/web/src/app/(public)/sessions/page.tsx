/* eslint-disable security/detect-object-injection -- Safe session data object access */
/**
 * SPRINT-4: Active Sessions Dashboard (Issue #1134)
 *
 * Displays all active game sessions with real-time status updates.
 * Features:
 * - List of active sessions with game info
 * - Status indicators (InProgress, Paused)
 * - Action buttons: Pause/Resume, End Session
 * - Filter by game
 * - Pagination (20 per page)
 * - Empty state handling
 * - Loading and error states
 * - WCAG 2.1 AA accessibility compliance
 */

'use client';

import { useState, useEffect, useRef } from 'react';

import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { ErrorDisplay } from '@/components/errors';
import { LoadingButton } from '@/components/loading/LoadingButton';
import { SessionQuotaBar } from '@/components/sessions/SessionQuotaBar';
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
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { useSessionQuotaWithStatus } from '@/hooks/queries/useSessionQuota';
import { api, GameSessionDto, Game, PaginatedSessionsResponse } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { categorizeError } from '@/lib/errorUtils';
import { logger } from '@/lib/logger';

/**
 * Session status badge component
 */
function SessionStatusBadge({ status }: { status: string }) {
  const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
    InProgress: 'default',
    Paused: 'secondary',
    Setup: 'outline',
  };

  return (
    <Badge variant={variants[status] || 'outline'} aria-label={`Session status: ${status}`}>
      {status}
    </Badge>
  );
}

/**
 * Session action buttons component
 */
function SessionActions({
  session,
  onPause,
  onResume,
  onEnd,
  isLoading,
}: {
  session: GameSessionDto;
  onPause: (id: string) => void;
  onResume: (id: string) => void;
  onEnd: (id: string) => void;
  isLoading: boolean;
}) {
  const canPause = session.status === 'InProgress';
  const canResume = session.status === 'Paused';
  const canEnd = session.status === 'InProgress' || session.status === 'Paused';

  return (
    <div className="flex gap-2" role="group" aria-label="Session actions">
      {canPause && (
        <LoadingButton
          size="sm"
          variant="outline"
          onClick={() => onPause(session.id)}
          isLoading={isLoading}
          loadingText="Pausing..."
          aria-label={`Pause session for ${session.players?.[0]?.playerName || 'game'}`}
        >
          Pause
        </LoadingButton>
      )}
      {canResume && (
        <LoadingButton
          size="sm"
          variant="outline"
          onClick={() => onResume(session.id)}
          isLoading={isLoading}
          loadingText="Resuming..."
          aria-label={`Resume session for ${session.players?.[0]?.playerName || 'game'}`}
        >
          Resume
        </LoadingButton>
      )}
      {canEnd && (
        <LoadingButton
          size="sm"
          variant="destructive"
          onClick={() => onEnd(session.id)}
          isLoading={isLoading}
          loadingText="Ending..."
          aria-label={`End session for ${session.players?.[0]?.playerName || 'game'}`}
        >
          End Session
        </LoadingButton>
      )}
    </div>
  );
}

/**
 * Active Sessions Dashboard Page
 */
export default function ActiveSessionsPage() {
  const router = useRouter();
  const [sessions, setSessions] = useState<GameSessionDto[]>([]);
  const [games, setGames] = useState<Game[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [selectedGame, setSelectedGame] = useState<string>('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const pageSize = 20;

  // Session Quota (Issue #3075)
  const { data: quota, isLoading: quotaLoading } = useSessionQuotaWithStatus();

  // Track if quota warning toast has been shown (Issue #3075 - AC #3)
  const quotaToastShownRef = useRef(false);

  /**
   * Show quota warning toast when approaching limit (Issue #3075 - AC #3)
   * Trigger at 80%+ quota usage (warning or critical level)
   */
  useEffect(() => {
    if (!quota || quotaLoading) return;

    const shouldShowWarning =
      (quota.warningLevel === 'warning' || quota.warningLevel === 'critical') &&
      !quotaToastShownRef.current;

    if (shouldShowWarning) {
      const percentage = quota.percentageUsed;
      toast.warning('Session Quota Warning', {
        description: `You have reached ${percentage}% of your available sessions. ${quota.remainingSlots} slots remaining.`,
        duration: 5000,
      });
      quotaToastShownRef.current = true;
    }

    // Reset toast shown flag when quota improves
    if (quota.warningLevel === 'none' && quotaToastShownRef.current) {
      quotaToastShownRef.current = false;
    }
  }, [quota, quotaLoading]);

  /**
   * Fetch active sessions from API
   */
  const fetchSessions = async () => {
    try {
      setLoading(true);
      setError(null);

      const offset = (currentPage - 1) * pageSize;
      const response: PaginatedSessionsResponse = await api.sessions.getActive(pageSize, offset);

      setSessions(response.sessions || []);
      setTotalPages(Math.ceil((response.total || 0) / pageSize));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load active sessions');
    } finally {
      setLoading(false);
    }
  };

  /**
   * Fetch games for filter dropdown
   */
  const fetchGames = async () => {
    try {
      const response = await api.games.getAll();
      setGames(response.games || []);
    } catch (err) {
      logger.error(
        'Failed to load games for active sessions',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('ActiveSessionsPage', 'fetchGames', { operation: 'fetch_games' })
      );
    }
  };

  /**
   * Initialize component - intentionally only re-runs on currentPage change
   * Note: fetchSessions and fetchGames are recreated on each render but are
   * excluded from dependencies to prevent refetching on unrelated state changes.
   * Functions access stable state via closure and don't require memoization.
   */
  /* eslint-disable react-hooks/exhaustive-deps */
  useEffect(() => {
    fetchSessions();
    fetchGames();
  }, [currentPage]);
  /* eslint-enable react-hooks/exhaustive-deps */

  /**
   * Handle pause session action
   */
  const handlePause = async (sessionId: string) => {
    try {
      setActionLoading(true);
      await api.sessions.pause(sessionId);
      await fetchSessions(); // Refresh list
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to pause session');
    } finally {
      setActionLoading(false);
    }
  };

  /**
   * Handle resume session action
   */
  const handleResume = async (sessionId: string) => {
    try {
      setActionLoading(true);
      await api.sessions.resume(sessionId);
      await fetchSessions(); // Refresh list
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to resume session');
    } finally {
      setActionLoading(false);
    }
  };

  /**
   * Handle end session action
   */
  const handleEnd = async (sessionId: string) => {
    if (!confirm('Are you sure you want to end this session? This action cannot be undone.')) {
      return;
    }

    try {
      setActionLoading(true);
      await api.sessions.end(sessionId);
      await fetchSessions(); // Refresh list
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to end session');
    } finally {
      setActionLoading(false);
    }
  };

  /**
   * Navigate to session details
   */
  const viewSession = (sessionId: string) => {
    router.push(`/sessions/${sessionId}`);
  };

  /**
   * Filter sessions by selected game
   */
  const filteredSessions =
    selectedGame === 'all' ? sessions : sessions.filter(s => s.gameId === selectedGame);

  /**
   * Format duration for display
   */
  const formatDuration = (minutes: number): string => {
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins}m`;
  };

  /**
   * Format date for display
   */
  const formatDate = (dateString: string): string => {
    const date = new Date(dateString);
    return new Intl.DateTimeFormat('en-US', {
      month: 'short',
      day: 'numeric',
      hour: '2-digit',
      minute: '2-digit',
    }).format(date);
  };

  return (
    <div className="container mx-auto py-8 px-4">
      <Card>
        <CardHeader>
          <CardTitle>Active Game Sessions</CardTitle>
          <CardDescription>Manage your currently active and paused game sessions</CardDescription>
        </CardHeader>
        <CardContent>
          {/* Session Quota Bar (Issue #3075) */}
          {quota && !quotaLoading && (
            <div className="mb-6">
              <SessionQuotaBar
                currentSessions={quota.currentSessions}
                maxSessions={quota.maxSessions}
                userTier={quota.userTier}
                remainingSlots={quota.remainingSlots}
                canCreateNew={quota.canCreateNew}
                isUnlimited={quota.isUnlimited}
                compact={false}
                showUpgradeLink={true}
              />
            </div>
          )}

          {/* Filters */}
          <div className="mb-6 flex gap-4 items-center">
            <label htmlFor="game-filter" className="text-sm font-medium">
              Filter by Game:
            </label>
            <select
              id="game-filter"
              value={selectedGame}
              onChange={e => setSelectedGame(e.target.value)}
              className="border rounded px-3 py-2"
              aria-label="Filter sessions by game"
            >
              <option value="all">All Games</option>
              {games.map(game => (
                <option key={game.id} value={game.id}>
                  {game.title}
                </option>
              ))}
            </select>
          </div>

          {/* Error Display */}
          {error && (
            <ErrorDisplay
              error={categorizeError(new Error(error))}
              onRetry={fetchSessions}
              showTechnicalDetails={process.env.NODE_ENV === 'development'}
            />
          )}

          {/* Loading State */}
          {loading && (
            <div
              className="space-y-4"
              role="status"
              aria-live="polite"
              aria-label="Loading sessions"
            >
              {[1, 2, 3].map(i => (
                <Skeleton key={i} className="h-20 w-full" />
              ))}
            </div>
          )}

          {/* Empty State */}
          {!loading && filteredSessions.length === 0 && (
            <div className="text-center py-12" role="status">
              <p className="text-muted-foreground text-lg mb-4">No active sessions found</p>
              <TooltipProvider>
                <Tooltip>
                  <TooltipTrigger asChild>
                    <span>
                      <Button
                        onClick={() => router.push('/games')}
                        disabled={quota && !quota.canCreateNew}
                        aria-label={
                          quota && !quota.canCreateNew
                            ? 'Cannot create new session - quota limit reached'
                            : 'Go to games library to start a session'
                        }
                      >
                        Start a New Session
                      </Button>
                    </span>
                  </TooltipTrigger>
                  {quota && !quota.canCreateNew && (
                    <TooltipContent>
                      <p>You have reached your session limit ({quota.maxSessions})</p>
                    </TooltipContent>
                  )}
                </Tooltip>
              </TooltipProvider>
            </div>
          )}

          {/* Sessions Table */}
          {!loading && filteredSessions.length > 0 && (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Game</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Players</TableHead>
                    <TableHead>Started</TableHead>
                    <TableHead>Duration</TableHead>
                    <TableHead className="text-right">Actions</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {filteredSessions.map(session => (
                    <TableRow
                      key={session.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => viewSession(session.id)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={e => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          viewSession(session.id);
                        }
                      }}
                      aria-label={`View session details for ${session.players?.[0]?.playerName || 'game'}`}
                    >
                      <TableCell className="font-medium">
                        {games.find(g => g.id === session.gameId)?.title || 'Unknown Game'}
                      </TableCell>
                      <TableCell>
                        <SessionStatusBadge status={session.status} />
                      </TableCell>
                      <TableCell>
                        {session.playerCount} player{session.playerCount !== 1 ? 's' : ''}
                      </TableCell>
                      <TableCell>{formatDate(session.startedAt)}</TableCell>
                      <TableCell>{formatDuration(session.durationMinutes)}</TableCell>
                      <TableCell className="text-right" onClick={e => e.stopPropagation()}>
                        <SessionActions
                          session={session}
                          onPause={handlePause}
                          onResume={handleResume}
                          onEnd={handleEnd}
                          isLoading={actionLoading}
                        />
                      </TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {/* Pagination */}
              {totalPages > 1 && (
                <div
                  className="mt-6 flex justify-center gap-2"
                  role="navigation"
                  aria-label="Pagination"
                >
                  <Button
                    variant="outline"
                    onClick={() => setCurrentPage(p => Math.max(1, p - 1))}
                    disabled={currentPage === 1}
                    aria-label="Go to previous page"
                  >
                    Previous
                  </Button>
                  <span className="px-4 py-2 text-sm" aria-current="page">
                    Page {currentPage} of {totalPages}
                  </span>
                  <Button
                    variant="outline"
                    onClick={() => setCurrentPage(p => Math.min(totalPages, p + 1))}
                    disabled={currentPage === totalPages}
                    aria-label="Go to next page"
                  >
                    Next
                  </Button>
                </div>
              )}
            </>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
