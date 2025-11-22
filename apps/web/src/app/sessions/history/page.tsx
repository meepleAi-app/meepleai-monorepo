/**
 * SPRINT-4: Session History View (Issue #1134)
 *
 * Displays completed and abandoned game sessions with filtering and statistics.
 * Features:
 * - List of completed/abandoned sessions
 * - Filters: game, date range
 * - Sort by date (newest first)
 * - Session details on click
 * - Statistics: win rates, average duration
 * - Pagination
 * - WCAG 2.1 AA accessibility compliance
 */

'use client';

import { useState, useEffect } from 'react';
import { useRouter } from 'next/navigation';
import { api, GameSessionDto, Game, SessionHistoryFilters } from '@/lib/api';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { Input } from '@/components/ui/input';
import { Label } from '@/components/ui/label';
import { Skeleton } from '@/components/ui/skeleton';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/table';
import { ErrorDisplay } from '@/components/errors';
import { categorizeError } from '@/lib/errorUtils';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';

/**
 * Session status badge component
 */
function SessionStatusBadge({ status }: { status: string }) {
  const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
    Completed: 'default',
    Abandoned: 'destructive',
    InProgress: 'secondary',
    Paused: 'outline'
  };

  return (
    <Badge variant={variants[status] || 'outline'} aria-label={`Session status: ${status}`}>
      {status}
    </Badge>
  );
}

/**
 * Statistics card component
 */
function StatisticsCard({ sessions }: { sessions: GameSessionDto[] }) {
  const totalSessions = sessions.length;
  const completedSessions = sessions.filter(s => s.status === 'Completed').length;
  const abandonedSessions = sessions.filter(s => s.status === 'Abandoned').length;

  const averageDuration = sessions.length > 0
    ? Math.round(sessions.reduce((sum, s) => sum + s.durationMinutes, 0) / sessions.length)
    : 0;

  // Calculate win rates
  const winCounts: { [key: string]: number } = {};
  const totalGames = completedSessions;

  sessions.forEach(session => {
    if (session.winnerName && session.status === 'Completed') {
      winCounts[session.winnerName] = (winCounts[session.winnerName] || 0) + 1;
    }
  });

  const formatDuration = (minutes: number): string => {
    if (minutes < 60) return `${minutes}m`;
    const hours = Math.floor(minutes / 60);
    const mins = minutes % 60;
    return `${hours}h ${mins}m`;
  };

  return (
    <Card className="mb-6">
      <CardHeader>
        <CardTitle>Session Statistics</CardTitle>
        <CardDescription>Overview of your gaming history</CardDescription>
      </CardHeader>
      <CardContent>
        <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
          <div>
            <p className="text-sm text-muted-foreground">Total Sessions</p>
            <p className="text-2xl font-bold">{totalSessions}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Completed</p>
            <p className="text-2xl font-bold text-green-600">{completedSessions}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Abandoned</p>
            <p className="text-2xl font-bold text-red-600">{abandonedSessions}</p>
          </div>
          <div>
            <p className="text-sm text-muted-foreground">Avg Duration</p>
            <p className="text-2xl font-bold">{formatDuration(averageDuration)}</p>
          </div>
        </div>

        {Object.keys(winCounts).length > 0 && (
          <div className="mt-6">
            <h3 className="text-sm font-medium mb-3">Win Rates (Top Players)</h3>
            <div className="space-y-2">
              {Object.entries(winCounts)
                .sort(([, a], [, b]) => b - a)
                .slice(0, 5)
                .map(([player, wins]) => (
                  <div key={player} className="flex justify-between items-center">
                    <span className="text-sm">{player}</span>
                    <div className="flex items-center gap-2">
                      <div className="w-32 bg-muted rounded-full h-2">
                        <div
                          className="bg-primary h-2 rounded-full"
                          style={{ width: `${(wins / totalGames) * 100}%` }}
                          role="progressbar"
                          aria-valuenow={(wins / totalGames) * 100}
                          aria-valuemin={0}
                          aria-valuemax={100}
                          aria-label={`${player} win rate: ${Math.round((wins / totalGames) * 100)}%`}
                        />
                      </div>
                      <span className="text-sm font-medium w-12 text-right">
                        {Math.round((wins / totalGames) * 100)}%
                      </span>
                    </div>
                  </div>
                ))}
            </div>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

/**
 * Session History Page
 */
export default function SessionHistoryPage() {
  const router = useRouter();
  const [sessions, setSessions] = useState<GameSessionDto[]>([]);
  const [games, setGames] = useState<Game[]>([]);
  const [loading, setLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  // Filters
  const [selectedGame, setSelectedGame] = useState<string>('all');
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');

  // Pagination
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const pageSize = 20;

  /**
   * Fetch session history from API
   */
  const fetchHistory = async () => {
    try {
      setLoading(true);
      setError(null);

      const offset = (currentPage - 1) * pageSize;
      const filters: SessionHistoryFilters = {
        limit: pageSize,
        offset
      };

      if (selectedGame !== 'all') {
        filters.gameId = selectedGame;
      }
      if (startDate) {
        filters.startDate = startDate;
      }
      if (endDate) {
        filters.endDate = endDate;
      }

      const response = await api.sessions.getHistory(filters);
      setSessions(response.sessions || []);
      setTotalPages(Math.ceil((response.total || 0) / pageSize));
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to load session history');
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
        'Failed to load games for session history',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('SessionHistoryPage', 'fetchGames', { operation: 'fetch_games' })
      );
    }
  };

  /**
   * Initialize component and refresh on filter changes
   */
  useEffect(() => {
    fetchGames();
  }, []);

  useEffect(() => {
    fetchHistory();
  }, [currentPage, selectedGame, startDate, endDate]);

  /**
   * Reset filters
   */
  const resetFilters = () => {
    setSelectedGame('all');
    setStartDate('');
    setEndDate('');
    setCurrentPage(1);
  };

  /**
   * Navigate to session details
   */
  const viewSession = (sessionId: string) => {
    router.push(`/sessions/${sessionId}`);
  };

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
      year: 'numeric',
      hour: '2-digit',
      minute: '2-digit'
    }).format(date);
  };

  return (
    <div className="container mx-auto py-8 px-4">
      {/* Statistics */}
      {!loading && sessions.length > 0 && (
        <StatisticsCard sessions={sessions} />
      )}

      <Card>
        <CardHeader>
          <CardTitle>Session History</CardTitle>
          <CardDescription>
            View and analyze your past game sessions
          </CardDescription>
        </CardHeader>
        <CardContent>
          {/* Filters */}
          <div className="mb-6 grid grid-cols-1 md:grid-cols-4 gap-4">
            <div>
              <Label htmlFor="game-filter">Game</Label>
              <select
                id="game-filter"
                value={selectedGame}
                onChange={(e) => {
                  setSelectedGame(e.target.value);
                  setCurrentPage(1);
                }}
                className="w-full border rounded px-3 py-2 mt-1"
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

            <div>
              <Label htmlFor="start-date">Start Date</Label>
              <Input
                id="start-date"
                type="date"
                value={startDate}
                onChange={(e) => {
                  setStartDate(e.target.value);
                  setCurrentPage(1);
                }}
                aria-label="Filter sessions from start date"
              />
            </div>

            <div>
              <Label htmlFor="end-date">End Date</Label>
              <Input
                id="end-date"
                type="date"
                value={endDate}
                onChange={(e) => {
                  setEndDate(e.target.value);
                  setCurrentPage(1);
                }}
                aria-label="Filter sessions to end date"
              />
            </div>

            <div className="flex items-end">
              <Button
                variant="outline"
                onClick={resetFilters}
                className="w-full"
                aria-label="Reset all filters"
              >
                Reset Filters
              </Button>
            </div>
          </div>

          {/* Error Display */}
          {error && (
            <ErrorDisplay
              error={categorizeError(new Error(error))}
              onRetry={fetchHistory}
              showTechnicalDetails={process.env.NODE_ENV === 'development'}
            />
          )}

          {/* Loading State */}
          {loading && (
            <div className="space-y-4" role="status" aria-live="polite" aria-label="Loading session history">
              {[1, 2, 3].map(i => (
                <Skeleton key={i} className="h-20 w-full" />
              ))}
            </div>
          )}

          {/* Empty State */}
          {!loading && sessions.length === 0 && (
            <div className="text-center py-12" role="status">
              <p className="text-muted-foreground text-lg mb-4">
                No session history found
              </p>
              {(selectedGame !== 'all' || startDate || endDate) && (
                <Button variant="outline" onClick={resetFilters}>
                  Clear Filters
                </Button>
              )}
            </div>
          )}

          {/* Sessions Table */}
          {!loading && sessions.length > 0 && (
            <>
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Game</TableHead>
                    <TableHead>Status</TableHead>
                    <TableHead>Players</TableHead>
                    <TableHead>Winner</TableHead>
                    <TableHead>Started</TableHead>
                    <TableHead>Completed</TableHead>
                    <TableHead>Duration</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {sessions.map(session => (
                    <TableRow
                      key={session.id}
                      className="cursor-pointer hover:bg-muted/50"
                      onClick={() => viewSession(session.id)}
                      role="button"
                      tabIndex={0}
                      onKeyDown={(e) => {
                        if (e.key === 'Enter' || e.key === ' ') {
                          e.preventDefault();
                          viewSession(session.id);
                        }
                      }}
                      aria-label={`View session details for ${session.winnerName || 'game'}`}
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
                      <TableCell>
                        {session.winnerName || <span className="text-muted-foreground">—</span>}
                      </TableCell>
                      <TableCell>{formatDate(session.startedAt)}</TableCell>
                      <TableCell>
                        {session.completedAt ? formatDate(session.completedAt) : '—'}
                      </TableCell>
                      <TableCell>{formatDuration(session.durationMinutes)}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>

              {/* Pagination */}
              {totalPages > 1 && (
                <div className="mt-6 flex justify-center gap-2" role="navigation" aria-label="Pagination">
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
