/* eslint-disable security/detect-object-injection -- Safe session data object access */
/**
 * SPRINT-4: Active Sessions Dashboard (Issue #1134)
 * Issue #4863: Migrated to EntityListView with grid/list/table views
 *
 * Displays all active game sessions with MeepleCard entity="session"
 * and EntityListView view mode switching.
 *
 * Features:
 * - EntityListView with grid/list/table toggle
 * - MeepleCard entity="session" (indigo accents)
 * - Status filter (All, InProgress, Paused, Setup)
 * - Search by game name
 * - Quick actions: Pause/Resume/End Session
 * - Session quota bar (Issue #3075)
 * - Pagination (20 per page)
 * - WCAG 2.1 AA accessibility compliance
 */

'use client';

import { useState, useEffect, useRef, useMemo, useCallback } from 'react';

import { Calendar, Clock, Dice6, Pause, Play, Square, Users } from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { ErrorDisplay } from '@/components/errors';
import { SessionQuotaBar } from '@/components/sessions/SessionQuotaBar';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  EntityListView,
  type TableColumnConfig,
  type FilterConfig,
  type SortOption,
} from '@/components/ui/data-display/entity-list-view';
import type { MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
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

import { SessionsNavConfig } from './NavConfig';
import { createErrorContext } from '@/lib/errors';
import { categorizeError } from '@/lib/errorUtils';
import { logger } from '@/lib/logger';

// ============================================================================
// Types
// ============================================================================

interface SessionWithGame extends GameSessionDto {
  gameTitle: string;
  gameImageUrl?: string;
}

// ============================================================================
// Helpers
// ============================================================================

function formatDuration(minutes: number): string {
  if (minutes < 60) return `${minutes}m`;
  const hours = Math.floor(minutes / 60);
  const mins = minutes % 60;
  return `${hours}h ${mins}m`;
}

function formatDate(dateString: string): string {
  const date = new Date(dateString);
  return new Intl.DateTimeFormat('en-US', {
    month: 'short',
    day: 'numeric',
    hour: '2-digit',
    minute: '2-digit',
  }).format(date);
}

// ============================================================================
// Table columns for table view
// ============================================================================

const tableColumns: TableColumnConfig[] = [
  {
    id: 'title',
    header: 'Game',
    accessorKey: 'title',
  },
  {
    id: 'status',
    header: 'Status',
    accessorKey: 'badge',
    cell: (value) => {
      const status = String(value ?? '');
      const variants: Record<string, 'default' | 'secondary' | 'outline'> = {
        InProgress: 'default',
        Paused: 'secondary',
        Setup: 'outline',
      };
      return (
        <Badge variant={variants[status] || 'outline'}>
          {status}
        </Badge>
      );
    },
  },
  {
    id: 'players',
    header: 'Players',
    accessorKey: 'meta_0',
    cell: (value) => (
      <span className="text-sm">{String(value ?? '—')}</span>
    ),
  },
  {
    id: 'started',
    header: 'Started',
    accessorKey: 'meta_1',
    cell: (value) => (
      <span className="text-sm text-muted-foreground">{String(value ?? '—')}</span>
    ),
  },
  {
    id: 'duration',
    header: 'Duration',
    accessorKey: 'meta_2',
    cell: (value) => (
      <span className="font-mono text-sm">{String(value ?? '—')}</span>
    ),
  },
];

// ============================================================================
// Sort options
// ============================================================================

const sortOptions: SortOption<SessionWithGame>[] = [
  {
    value: 'newest',
    label: 'Newest',
    icon: Calendar,
    compareFn: (a, b) => new Date(b.startedAt).getTime() - new Date(a.startedAt).getTime(),
  },
  {
    value: 'oldest',
    label: 'Oldest',
    icon: Calendar,
    compareFn: (a, b) => new Date(a.startedAt).getTime() - new Date(b.startedAt).getTime(),
  },
  {
    value: 'duration',
    label: 'Duration',
    icon: Clock,
    compareFn: (a, b) => b.durationMinutes - a.durationMinutes,
  },
  {
    value: 'players',
    label: 'Players',
    icon: Users,
    compareFn: (a, b) => b.playerCount - a.playerCount,
  },
];

// ============================================================================
// Filter config
// ============================================================================

const statusFilter: FilterConfig<SessionWithGame> = {
  type: 'select' as const,
  id: 'status',
  label: 'Status',
  field: 'status',
  options: [
    { label: 'All', value: '' },
    { label: 'In Progress', value: 'InProgress' },
    { label: 'Paused', value: 'Paused' },
    { label: 'Setup', value: 'Setup' },
  ],
};

// ============================================================================
// Main Component
// ============================================================================

export default function ActiveSessionsPage() {
  const router = useRouter();
  const [sessions, setSessions] = useState<GameSessionDto[]>([]);
  const [games, setGames] = useState<Game[]>([]);
  const [loading, setLoading] = useState(true);
  const [actionLoading, setActionLoading] = useState<string | null>(null);
  const [error, setError] = useState<string | null>(null);
  const [currentPage, setCurrentPage] = useState(1);
  const [totalPages, setTotalPages] = useState(1);
  const pageSize = 20;

  // Session Quota (Issue #3075)
  const { data: quota, isLoading: quotaLoading } = useSessionQuotaWithStatus();

  // Track if quota warning toast has been shown (Issue #3075 - AC #3)
  const quotaToastShownRef = useRef(false);

  /**
   * Show quota warning toast when approaching limit (Issue #3075 - AC #3)
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

    if (quota.warningLevel === 'none' && quotaToastShownRef.current) {
      quotaToastShownRef.current = false;
    }
  }, [quota, quotaLoading]);

  /**
   * Fetch active sessions from API
   */
  const fetchSessions = useCallback(async () => {
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
  }, [currentPage]);

  /**
   * Fetch games for enrichment
   */
  const fetchGames = useCallback(async () => {
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
  }, []);

  useEffect(() => {
    fetchSessions();
    fetchGames();
  }, [fetchSessions, fetchGames]);

  // Enrich sessions with game titles
  const enrichedSessions = useMemo<SessionWithGame[]>(() => {
    return sessions.map(s => {
      const game = games.find(g => g.id === s.gameId);
      return {
        ...s,
        gameTitle: game?.title || 'Unknown Game',
        gameImageUrl: game?.imageUrl || undefined,
      };
    });
  }, [sessions, games]);

  // Session actions
  const handlePause = async (sessionId: string) => {
    try {
      setActionLoading(sessionId);
      await api.sessions.pause(sessionId);
      await fetchSessions();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to pause session');
    } finally {
      setActionLoading(null);
    }
  };

  const handleResume = async (sessionId: string) => {
    try {
      setActionLoading(sessionId);
      await api.sessions.resume(sessionId);
      await fetchSessions();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to resume session');
    } finally {
      setActionLoading(null);
    }
  };

  const handleEnd = async (sessionId: string) => {
    if (!confirm('Are you sure you want to end this session? This action cannot be undone.')) {
      return;
    }

    try {
      setActionLoading(sessionId);
      await api.sessions.end(sessionId);
      await fetchSessions();
    } catch (err) {
      setError(err instanceof Error ? err.message : 'Failed to end session');
    } finally {
      setActionLoading(null);
    }
  };

  /**
   * renderItem: transforms SessionWithGame to MeepleCard props
   */
  const renderItem = useCallback(
    (session: SessionWithGame) => {
      const playerLabel = `${session.playerCount} player${session.playerCount !== 1 ? 's' : ''}`;

      const metadata: MeepleCardMetadata[] = [
        { icon: Users, label: playerLabel },
        { icon: Calendar, label: formatDate(session.startedAt) },
        { icon: Clock, label: formatDuration(session.durationMinutes) },
      ];

      const quickActions: Array<{
        icon: typeof Pause;
        label: string;
        onClick: () => void;
        destructive?: boolean;
      }> = [];

      if (session.status === 'InProgress') {
        quickActions.push({
          icon: Pause,
          label: 'Pause',
          onClick: () => handlePause(session.id),
        });
      }
      if (session.status === 'Paused') {
        quickActions.push({
          icon: Play,
          label: 'Resume',
          onClick: () => handleResume(session.id),
        });
      }
      if (session.status === 'InProgress' || session.status === 'Paused') {
        quickActions.push({
          icon: Square,
          label: 'End Session',
          onClick: () => handleEnd(session.id),
          destructive: true,
        });
      }

      return {
        id: session.id,
        title: session.gameTitle,
        subtitle: `${playerLabel} · ${formatDuration(session.durationMinutes)}`,
        imageUrl: session.gameImageUrl,
        badge: session.status,
        metadata,
        quickActions: quickActions.length > 0 ? quickActions : undefined,
      };
    },
    [actionLoading]
  );

  return (
    <div className="container mx-auto py-8 px-4">
      <SessionsNavConfig />
      <Card>
        <CardHeader className="flex flex-row items-start justify-between">
          <div>
            <CardTitle>Active Game Sessions</CardTitle>
            <CardDescription>Manage your currently active and paused game sessions</CardDescription>
          </div>
          <Link href="/toolkit">
            <Button variant="outline" size="sm" className="gap-2">
              <Dice6 className="w-4 h-4" />
              Go to Toolkit
            </Button>
          </Link>
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
          {!loading && enrichedSessions.length === 0 && (
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

          {/* Sessions EntityListView */}
          {!loading && enrichedSessions.length > 0 && (
            <>
              <EntityListView
                items={enrichedSessions}
                entity="session"
                persistenceKey="active-sessions"
                renderItem={renderItem}
                availableModes={['grid', 'list', 'table']}
                defaultViewMode="table"
                tableColumns={tableColumns}
                searchable
                searchPlaceholder="Search by game name..."
                searchFields={['gameTitle']}
                sortOptions={sortOptions}
                defaultSort="newest"
                filters={[statusFilter]}
                onItemClick={(session) => router.push(`/sessions/${session.id}`)}
                emptyMessage="No sessions match your filters"
                data-testid="sessions-list-view"
              />

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
