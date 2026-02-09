/**
 * PlayerStatistics - Cross-Game Statistics Dashboard
 *
 * Features:
 * - Stats cards: Total sessions, Total wins, Win rate
 * - Charts: Sessions per game (bar), Win distribution (pie)
 * - Tables: Game play counts, Average scores by game
 *
 * Issue #3892: Play Records Frontend UI
 */

'use client';

import { Trophy, Target, TrendingUp, BarChart3 } from 'lucide-react';
import {
  BarChart,
  Bar,
  PieChart,
  Pie,
  Cell,
  XAxis,
  YAxis,
  CartesianGrid,
  Tooltip,
  ResponsiveContainer,
} from 'recharts';

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import {
  Table,
  TableBody,
  TableCell,
  TableHead,
  TableHeader,
  TableRow,
} from '@/components/ui/data-display/table';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { usePlayerStatistics } from '@/lib/hooks/use-play-records';

const CHART_COLORS = [
  'hsl(25 95% 45%)', // Orange
  'hsl(262 83% 58%)', // Purple
  'hsl(168 76% 42%)', // Teal
  'hsl(350 89% 60%)', // Rose
  'hsl(220 70% 50%)', // Blue
  'hsl(120 100% 35%)', // Green
  'hsl(45 93% 47%)', // Yellow
];

export function PlayerStatistics() {
  const { data: stats, isLoading, error } = usePlayerStatistics();

  if (isLoading) {
    return (
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        {[...Array(4)].map((_, i) => (
          <Card key={i} className="animate-pulse">
            <CardHeader className="pb-2">
              <div className="h-4 bg-muted rounded w-24" />
            </CardHeader>
            <CardContent>
              <div className="h-8 bg-muted rounded w-16" />
            </CardContent>
          </Card>
        ))}
      </div>
    );
  }

  if (error || !stats) {
    return (
      <Alert variant="destructive">
        <AlertDescription>
          {error instanceof Error ? error.message : 'Failed to load statistics'}
        </AlertDescription>
      </Alert>
    );
  }

  const winRate =
    stats.totalSessions > 0
      ? ((stats.totalWins / stats.totalSessions) * 100).toFixed(1)
      : '0.0';

  // Prepare chart data
  const gamePlayData = Object.entries(stats.gamePlayCounts)
    .map(([game, count]) => ({ game, count }))
    .sort((a, b) => b.count - a.count)
    .slice(0, 10);

  const winDistributionData = Object.entries(stats.gamePlayCounts)
    .map(([game, count]) => ({ name: game, value: count }))
    .sort((a, b) => b.value - a.value)
    .slice(0, 6);

  const avgScoresData = Object.entries(stats.averageScoresByGame)
    .map(([game, avg]) => ({
      game,
      average: Number(avg.toFixed(1)),
    }))
    .sort((a, b) => b.average - a.average);

  return (
    <div className="space-y-6">
      {/* Stats Cards */}
      <div className="grid gap-4 md:grid-cols-2 lg:grid-cols-4">
        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Total Sessions</CardTitle>
            <BarChart3 className="w-4 h-4 text-muted-foreground" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">{stats.totalSessions}</div>
            <p className="text-xs text-muted-foreground mt-1">
              Across all games
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Total Wins</CardTitle>
            <Trophy className="w-4 h-4 text-amber-500" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">{stats.totalWins}</div>
            <p className="text-xs text-muted-foreground mt-1">
              Victory count
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Win Rate</CardTitle>
            <Target className="w-4 h-4 text-green-500" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">{winRate}%</div>
            <p className="text-xs text-muted-foreground mt-1">
              Success percentage
            </p>
          </CardContent>
        </Card>

        <Card>
          <CardHeader className="flex flex-row items-center justify-between pb-2">
            <CardTitle className="text-sm font-medium">Games Played</CardTitle>
            <TrendingUp className="w-4 h-4 text-blue-500" />
          </CardHeader>
          <CardContent>
            <div className="text-3xl font-bold">
              {Object.keys(stats.gamePlayCounts).length}
            </div>
            <p className="text-xs text-muted-foreground mt-1">
              Unique games
            </p>
          </CardContent>
        </Card>
      </div>

      {/* Charts Row */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Sessions Per Game (Bar Chart) */}
        <Card>
          <CardHeader>
            <CardTitle>Sessions Per Game</CardTitle>
            <CardDescription>Top 10 most played games</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <BarChart data={gamePlayData}>
                <CartesianGrid strokeDasharray="3 3" stroke="hsl(var(--border))" />
                <XAxis
                  dataKey="game"
                  tick={{ fill: 'hsl(var(--muted-foreground))' }}
                  angle={-45}
                  textAnchor="end"
                  height={80}
                />
                <YAxis tick={{ fill: 'hsl(var(--muted-foreground))' }} />
                <Tooltip
                  contentStyle={{
                    backgroundColor: 'hsl(var(--card))',
                    border: '1px solid hsl(var(--border))',
                  }}
                />
                <Bar dataKey="count" fill="hsl(25 95% 45%)" />
              </BarChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>

        {/* Win Distribution (Pie Chart) */}
        <Card>
          <CardHeader>
            <CardTitle>Play Distribution</CardTitle>
            <CardDescription>Sessions by game (top 6)</CardDescription>
          </CardHeader>
          <CardContent>
            <ResponsiveContainer width="100%" height={300}>
              <PieChart>
                <Pie
                  data={winDistributionData}
                  cx="50%"
                  cy="50%"
                  labelLine={false}
                  label={({ name, percent }) =>
                    `${name}: ${((percent ?? 0) * 100).toFixed(0)}%`
                  }
                  outerRadius={80}
                  fill="hsl(25 95% 45%)"
                  dataKey="value"
                >
                  {winDistributionData.map((entry, index) => (
                    <Cell
                      key={`cell-${index}`}
                      fill={CHART_COLORS[index % CHART_COLORS.length]}
                    />
                  ))}
                </Pie>
                <Tooltip
                  contentStyle={{
                    backgroundColor: 'hsl(var(--card))',
                    border: '1px solid hsl(var(--border))',
                  }}
                />
              </PieChart>
            </ResponsiveContainer>
          </CardContent>
        </Card>
      </div>

      {/* Tables Row */}
      <div className="grid gap-4 md:grid-cols-2">
        {/* Game Play Counts Table */}
        <Card>
          <CardHeader>
            <CardTitle>Play Counts by Game</CardTitle>
            <CardDescription>All games ranked by sessions</CardDescription>
          </CardHeader>
          <CardContent>
            <Table>
              <TableHeader>
                <TableRow>
                  <TableHead>Game</TableHead>
                  <TableHead className="text-right">Sessions</TableHead>
                </TableRow>
              </TableHeader>
              <TableBody>
                {Object.entries(stats.gamePlayCounts)
                  .sort(([, a], [, b]) => b - a)
                  .map(([game, count]) => (
                    <TableRow key={game}>
                      <TableCell className="font-medium">{game}</TableCell>
                      <TableCell className="text-right">{count}</TableCell>
                    </TableRow>
                  ))}
              </TableBody>
            </Table>
          </CardContent>
        </Card>

        {/* Average Scores Table */}
        <Card>
          <CardHeader>
            <CardTitle>Average Scores by Game</CardTitle>
            <CardDescription>Mean scores across dimensions</CardDescription>
          </CardHeader>
          <CardContent>
            {avgScoresData.length > 0 ? (
              <Table>
                <TableHeader>
                  <TableRow>
                    <TableHead>Game</TableHead>
                    <TableHead className="text-right">Avg Score</TableHead>
                  </TableRow>
                </TableHeader>
                <TableBody>
                  {avgScoresData.map(({ game, average }) => (
                    <TableRow key={game}>
                      <TableCell className="font-medium">{game}</TableCell>
                      <TableCell className="text-right">{average}</TableCell>
                    </TableRow>
                  ))}
                </TableBody>
              </Table>
            ) : (
              <p className="text-sm text-muted-foreground text-center py-8">
                No scoring data available
              </p>
            )}
          </CardContent>
        </Card>
      </div>

      {/* Empty State */}
      {stats.totalSessions === 0 && (
        <Card className="text-center py-12">
          <CardContent>
            <Trophy className="w-16 h-16 mx-auto mb-4 opacity-20" />
            <h3 className="text-lg font-semibold mb-2">No Statistics Yet</h3>
            <p className="text-muted-foreground">
              Start recording your game sessions to see statistics
            </p>
          </CardContent>
        </Card>
      )}
    </div>
  );
}
