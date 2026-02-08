/**
 * Game Sessions Monitoring Client - Issue #3948
 *
 * Admin interface for monitoring game sessions (play records) across all users:
 * - Stats dashboard (active, completed, total hours, most played)
 * - EntityListView with grid/list modes
 * - Search and filter by game, status
 * - Stats calculations from play records data
 *
 * Backend Integration:
 * - Uses existing Play Records API (Issue #3892)
 * - GET /api/v1/game-management/play-records (admin sees all users)
 * - GET /api/v1/game-management/play-records/statistics
 */

'use client';

import { useState, useMemo } from 'react';

import {
  PlayCircle,
  Users,
  Clock,
  Calendar,
  Trophy,
  StopCircle,
  PauseCircle,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { AdminAuthGuard } from '@/components/admin';
import { useAuthUser } from '@/components/auth/AuthProvider';
import { Spinner } from '@/components/loading';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { EntityListView } from '@/components/ui/data-display/entity-list-view';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import type { PlayRecordSummary, PlayRecordStatus } from '@/lib/api/schemas/play-records.schemas';
import { usePlayHistory } from '@/lib/hooks/use-play-records';

// ========== Helpers ==========

/**
 * Format ISO 8601 duration to human readable (PT2H30M → "2h 30m")
 */
function formatDuration(isoDuration: string | null): string {
  if (!isoDuration) return 'N/A';

  // eslint-disable-next-line security/detect-unsafe-regex
  const match = isoDuration.match(/PT(?:(\d+)H)?(?:(\d+)M)?/);
  if (!match) return isoDuration;

  const hours = match[1] ? `${match[1]}h` : '';
  const minutes = match[2] ? `${match[2]}m` : '';

  return [hours, minutes].filter(Boolean).join(' ') || '0m';
}

/**
 * Format relative time (e.g., "2 hours ago", "Yesterday")
 */
function formatRelativeTime(dateStr: string): string {
  const date = new Date(dateStr);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays === 1) return 'Yesterday';
  if (diffDays < 7) return `${diffDays}d ago`;

  return date.toLocaleDateString('en-US', { month: 'short', day: 'numeric' });
}

/**
 * Get status icon
 */
function getStatusIcon(status: PlayRecordStatus) {
  switch (status) {
    case 'InProgress':
      return PlayCircle;
    case 'Completed':
      return StopCircle;
    case 'Planned':
      return Calendar;
    default:
      return PauseCircle;
  }
}

// ========== Main Component ==========

export function GameSessionsClient() {
  const router = useRouter();
  const { user, loading: authLoading } = useAuthUser();

  // Fetch all play records (admin context)
  const { data, isLoading, error } = usePlayHistory({});
  const playRecords = useMemo(() => data?.records || [], [data]);

  // Filter state
  const [statusFilter, setStatusFilter] = useState<PlayRecordStatus | 'All'>('All');

  // Filter play records
  const filteredRecords = useMemo(() => {
    if (!playRecords || playRecords.length === 0) return [];

    let filtered = playRecords;

    // Status filter
    if (statusFilter !== 'All') {
      filtered = filtered.filter((r: PlayRecordSummary) => r.status === statusFilter);
    }

    return filtered;
  }, [playRecords, statusFilter]);

  // Calculate stats
  const stats = useMemo(() => {
    if (!playRecords || playRecords.length === 0) {
      return {
        activeSessions: 0,
        completedToday: 0,
        totalHoursPlayed: 0,
        mostPlayedGame: 'N/A',
        mostPlayedCount: 0,
      };
    }

    const now = new Date();
    const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());

    const active = playRecords.filter((r: PlayRecordSummary) => r.status === 'InProgress').length;
    const completedToday = playRecords.filter((r: PlayRecordSummary) => {
      if (r.status !== 'Completed') return false;
      const sessionDate = new Date(r.sessionDate);
      return sessionDate >= today;
    }).length;

    // Calculate total hours played
    const totalMinutes = playRecords
      .filter((r: PlayRecordSummary) => r.duration)
      .reduce((sum: number, r: PlayRecordSummary) => {
        if (!r.duration) return sum;
        // eslint-disable-next-line security/detect-unsafe-regex
        const match = r.duration.match(/PT(?:(\d+)H)?(?:(\d+)M)?/);
        if (!match) return sum;
        const hours = parseInt(match[1] || '0', 10);
        const mins = parseInt(match[2] || '0', 10);
        return sum + hours * 60 + mins;
      }, 0);

    // Find most played game
    const gameCounts = new Map<string, number>();
    playRecords.forEach((r: PlayRecordSummary) => {
      gameCounts.set(r.gameName, (gameCounts.get(r.gameName) || 0) + 1);
    });
    const mostPlayed = Array.from(gameCounts.entries()).sort((a, b) => b[1] - a[1])[0];

    return {
      activeSessions: active,
      completedToday,
      totalHoursPlayed: Math.round(totalMinutes / 60),
      mostPlayedGame: mostPlayed?.[0] || 'N/A',
      mostPlayedCount: mostPlayed?.[1] || 0,
    };
  }, [playRecords]);

  return (
    <AdminAuthGuard loading={authLoading} user={user}>
      <div className="container mx-auto p-6 max-w-7xl space-y-6">
        {/* Header */}
        <div>
          <h1 className="text-2xl font-bold flex items-center gap-2">
            <PlayCircle className="h-6 w-6 text-primary" />
            Game Sessions Monitoring
          </h1>
          <p className="text-muted-foreground">
            Monitor and analyze gameplay sessions across all users
          </p>
        </div>

        {/* Stats Cards */}
        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-4 gap-4">
          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium flex items-center gap-2">
                <PlayCircle className="h-4 w-4 text-green-600" />
                Active Sessions
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-3xl font-bold text-green-600">{stats.activeSessions}</div>
              <p className="text-xs text-muted-foreground mt-1">Currently playing</p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium flex items-center gap-2">
                <StopCircle className="h-4 w-4 text-blue-600" />
                Completed Today
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-3xl font-bold text-blue-600">{stats.completedToday}</div>
              <p className="text-xs text-muted-foreground mt-1">Sessions finished</p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium flex items-center gap-2">
                <Clock className="h-4 w-4" />
                Total Hours Played
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-3xl font-bold">{stats.totalHoursPlayed}h</div>
              <p className="text-xs text-muted-foreground mt-1">All time</p>
            </CardContent>
          </Card>

          <Card>
            <CardHeader className="pb-3">
              <CardTitle className="text-sm font-medium flex items-center gap-2">
                <Trophy className="h-4 w-4 text-amber-600" />
                Most Played
              </CardTitle>
            </CardHeader>
            <CardContent>
              <div className="text-lg font-semibold truncate">{stats.mostPlayedGame}</div>
              <p className="text-xs text-muted-foreground mt-1">
                {stats.mostPlayedCount} {stats.mostPlayedCount === 1 ? 'session' : 'sessions'}
              </p>
            </CardContent>
          </Card>
        </div>

        {/* Filters */}
        <Card>
          <CardHeader className="pb-4">
            <div className="flex flex-col sm:flex-row sm:items-center gap-4">
              <div className="flex-1">
                <CardTitle>Game Sessions</CardTitle>
                <CardDescription>
                  {filteredRecords.length} {filteredRecords.length === 1 ? 'session' : 'sessions'}
                  {statusFilter !== 'All' && ` (${statusFilter})`}
                </CardDescription>
              </div>
              <div className="flex items-center gap-2">
                <select
                  value={statusFilter}
                  onChange={e => setStatusFilter(e.target.value as PlayRecordStatus | 'All')}
                  className="px-3 py-2 border rounded-md text-sm"
                >
                  <option value="All">All Status</option>
                  <option value="Planned">Planned</option>
                  <option value="InProgress">In Progress</option>
                  <option value="Completed">Completed</option>
                  <option value="Archived">Archived</option>
                </select>
              </div>
            </div>
          </CardHeader>

          <CardContent>
            {/* Error State */}
            {error && (
              <Alert variant="destructive" className="mb-4">
                <AlertDescription>Failed to load game sessions. Please try again.</AlertDescription>
              </Alert>
            )}

            {/* Loading State */}
            {isLoading && (
              <div className="flex justify-center py-12">
                <Spinner size="lg" />
              </div>
            )}

            {/* EntityListView */}
            {!isLoading && !error && (
              <EntityListView
                items={filteredRecords}
                entity="event"
                persistenceKey="admin-game-sessions"
                defaultViewMode="grid"
                availableModes={['grid', 'list']}
                searchable
                searchFields={['gameName']}
                searchPlaceholder="Search by game..."
                renderItem={(record: PlayRecordSummary) => {
                  const StatusIcon = getStatusIcon(record.status);

                  return {
                    id: record.id,
                    title: record.gameName,
                    subtitle: `${record.playerCount} ${record.playerCount === 1 ? 'player' : 'players'}`,
                    metadata: [
                      {
                        icon: StatusIcon,
                        label: 'Status',
                        value: record.status,
                      },
                      {
                        icon: Calendar,
                        label: 'Date',
                        value: formatRelativeTime(record.sessionDate),
                      },
                      {
                        icon: Clock,
                        value: formatDuration(record.duration),
                      },
                      {
                        icon: Users,
                        value: `${record.playerCount} ${record.playerCount === 1 ? 'player' : 'players'}`,
                      },
                    ],
                    actions: [
                      {
                        label: 'View Details',
                        primary: true,
                        onClick: () => router.push(`/admin/game-sessions/${record.id}`),
                      },
                    ],
                    badge: record.status,
                  };
                }}
                onItemClick={(record) => router.push(`/admin/game-sessions/${record.id}`)}
                emptyMessage="No game sessions found"
                loading={isLoading}
              />
            )}
          </CardContent>
        </Card>
      </div>
    </AdminAuthGuard>
  );
}
