'use client';

import React, { useState } from 'react';

import { useQuery } from '@tanstack/react-query';
import { Calendar, Filter, Loader2, Trophy } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { SessionDetailModal } from '@/components/session/SessionDetailModal';
import type { Session } from '@/components/session/types';
import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import {
  Select,
  SelectContent,
  SelectItem,
  SelectTrigger,
  SelectValue,
} from '@/components/ui/overlays/select';
import { Button } from '@/components/ui/primitives/button';
import { Input } from '@/components/ui/primitives/input';
import { Label } from '@/components/ui/primitives/label';
import { api } from '@/lib/api';

/**
 * Toolkit History Page
 *
 * Features:
 * - List finalized sessions
 * - Filters: game, date range, winner
 * - Pagination (20 items/page)
 * - Session detail modal
 */
export default function ToolkitHistoryPage() {
  const router = useRouter();

  const [selectedSession, setSelectedSession] = useState<Session | null>(null);
  const [isModalOpen, setIsModalOpen] = useState(false);

  // Filters
  const [gameFilter, setGameFilter] = useState<string>('');
  const [startDate, setStartDate] = useState<string>('');
  const [endDate, setEndDate] = useState<string>('');

  // Fetch session history from API
  const { data: sessionsData, isLoading } = useQuery({
    queryKey: ['session-history', gameFilter, startDate, endDate],
    queryFn: () =>
      api.sessions.getHistory({
        gameId: gameFilter || undefined,
        startDate: startDate || undefined,
        endDate: endDate || undefined,
        limit: 20,
      }),
  });
  const sessions: Session[] = (sessionsData?.sessions ?? []).map(s => ({
    id: s.id,
    sessionCode: s.id.slice(0, 6).toUpperCase(),
    sessionType: s.gameId ? 'GameSpecific' : 'Generic',
    gameId: s.gameId,
    sessionDate: new Date(s.startedAt),
    status: s.status === 'Completed' ? 'Finalized' : (s.status as Session['status']),
    participantCount: s.playerCount,
  }));

  /**
   * Handle view details
   */
  const handleViewDetails = (session: Session) => {
    setSelectedSession(session);
    setIsModalOpen(true);
  };

  /**
   * Reset filters
   */
  const handleResetFilters = () => {
    setGameFilter('');
    setStartDate('');
    setEndDate('');
  };

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-gray-900">
      <div className="container mx-auto px-4 py-8 max-w-6xl">
        {/* Header */}
        <div className="text-center mb-8">
          <div className="inline-flex items-center justify-center w-16 h-16 mb-4 rounded-full bg-purple-100 dark:bg-purple-900">
            <Calendar className="w-8 h-8 text-purple-600 dark:text-purple-400" />
          </div>
          <h1 className="text-4xl font-bold mb-2 bg-gradient-to-r from-purple-600 to-blue-600 bg-clip-text text-transparent">
            Session History
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-300">
            Review past game sessions and statistics
          </p>
        </div>

        {/* Filters */}
        <Card className="mb-6">
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              <Filter className="w-5 h-5" />
              Filters
            </CardTitle>
          </CardHeader>
          <CardContent>
            <div className="grid md:grid-cols-4 gap-4">
              <div>
                <Label htmlFor="game-filter">Game</Label>
                <Select value={gameFilter} onValueChange={setGameFilter}>
                  <SelectTrigger id="game-filter">
                    <SelectValue placeholder="All games" />
                  </SelectTrigger>
                  <SelectContent>
                    <SelectItem value="all">All games</SelectItem>
                  </SelectContent>
                </Select>
              </div>

              <div>
                <Label htmlFor="start-date">Start Date</Label>
                <Input
                  id="start-date"
                  type="date"
                  value={startDate}
                  onChange={e => setStartDate(e.target.value)}
                />
              </div>

              <div>
                <Label htmlFor="end-date">End Date</Label>
                <Input
                  id="end-date"
                  type="date"
                  value={endDate}
                  onChange={e => setEndDate(e.target.value)}
                />
              </div>

              <div className="flex items-end">
                <Button variant="outline" onClick={handleResetFilters} className="w-full">
                  Reset
                </Button>
              </div>
            </div>
          </CardContent>
        </Card>

        {/* Session List */}
        {isLoading ? (
          <Card>
            <CardContent className="py-12 text-center">
              <Loader2 className="w-8 h-8 mx-auto mb-4 animate-spin text-purple-600" />
              <p className="text-gray-600 dark:text-gray-400">Loading sessions...</p>
            </CardContent>
          </Card>
        ) : sessions.length === 0 ? (
          <Card>
            <CardContent className="py-12 text-center">
              <Calendar className="w-12 h-12 mx-auto mb-4 text-gray-400" />
              <p className="text-gray-600 dark:text-gray-400 mb-4">No sessions found</p>
              <Button onClick={() => router.push('/toolkit')}>Start Your First Session</Button>
            </CardContent>
          </Card>
        ) : (
          <div className="grid md:grid-cols-2 lg:grid-cols-3 gap-4">
            {sessions.map(session => (
              <Card key={session.id} className="hover:shadow-lg transition-shadow">
                <CardHeader>
                  <div className="flex items-center justify-between">
                    <div className="flex items-center gap-2">
                      {session.gameIcon && <span className="text-2xl">{session.gameIcon}</span>}
                      <div>
                        <CardTitle className="text-base">
                          {session.gameName || 'Generic Session'}
                        </CardTitle>
                        <p className="text-sm text-gray-500">{session.sessionCode}</p>
                      </div>
                    </div>
                    <Badge variant={session.status === 'Finalized' ? 'default' : 'secondary'}>
                      {session.status}
                    </Badge>
                  </div>
                </CardHeader>
                <CardContent className="space-y-3">
                  <div className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
                    <Calendar className="w-4 h-4" />
                    <span>{new Date(session.sessionDate).toLocaleDateString('it-IT')}</span>
                  </div>

                  <div className="flex items-center gap-2 text-sm text-gray-600 dark:text-gray-400">
                    <Trophy className="w-4 h-4" />
                    <span>{session.participantCount} players</span>
                  </div>

                  <Button
                    variant="outline"
                    size="sm"
                    onClick={() => handleViewDetails(session)}
                    className="w-full"
                  >
                    View Details
                  </Button>
                </CardContent>
              </Card>
            ))}
          </div>
        )}

        {/* Session Detail Modal */}
        {selectedSession && (
          <SessionDetailModal
            session={selectedSession}
            open={isModalOpen}
            onOpenChange={setIsModalOpen}
          />
        )}
      </div>
    </div>
  );
}
