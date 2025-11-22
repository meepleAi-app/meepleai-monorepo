/**
 * Game Detail Page - 4 Tabs (Issue #855)
 *
 * Full-page view with:
 * - Overview: Game info + BGG details (reuses GameDetailModal logic)
 * - Rules: Placeholder for GetRuleSpecsQuery (backend TODO)
 * - Sessions: Game session history (uses api.sessions.getHistory)
 * - Notes: User notes (localStorage-based)
 *
 * DDD Integration:
 * - Frontend only (backend queries in separate issue)
 * - Consumes existing Game + Session APIs
 * - Designed for future GameManagement.Application queries
 */

'use client';


import React, { useState, useEffect } from 'react';
import { useRouter, useParams } from 'next/navigation';
import Link from 'next/link';
import { Game, BggGameDetails, GameSessionDto, api } from '@/lib/api';
import { Tabs, TabsContent, TabsList, TabsTrigger } from '@/components/ui/tabs';
import { logger } from '@/lib/logger';
import { createErrorContext } from '@/lib/errors';
import { Button } from '@/components/ui/button';
import { Badge } from '@/components/ui/badge';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Textarea } from '@/components/ui/textarea';
import { Alert, AlertDescription } from '@/components/ui/alert';
import {
  Users,
  Clock,
  Calendar,
  Star,
  TrendingUp,
  ExternalLink,
  ArrowLeft,
  AlertCircle,
  StickyNote,
  PlayCircle,
  BookOpen
} from 'lucide-react';

// LocalStorage key for notes
const NOTES_STORAGE_KEY = 'meepleai_game_notes';

interface GameNotes {
  [gameId: string]: string;
}

export default function GameDetailPage() {
  const router = useRouter();
  const params = useParams();
  const id = params?.id as string | undefined;
  const gameId = typeof id === 'string' ? id : null;

  // State
  const [game, setGame] = useState<Game | null>(null);
  const [bggDetails, setBggDetails] = useState<BggGameDetails | null>(null);
  const [sessions, setSessions] = useState<GameSessionDto[]>([]);
  const [notes, setNotes] = useState('');
  const [loading, setLoading] = useState(true);
  const [bggLoading, setBggLoading] = useState(false);
  const [sessionsLoading, setSessionsLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);
  const [bggError, setBggError] = useState<string | null>(null);
  const [activeTab, setActiveTab] = useState('overview');

  // Load game data
  useEffect(() => {
    if (!gameId) return;

    const loadGame = async () => {
      setLoading(true);
      setError(null);
      try {
        const gameData = await api.games.getById(gameId);
        setGame(gameData);
      } catch (err) {
        setError('Failed to load game');
        logger.error(
          'Failed to load game details',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadGame', { gameId, operation: 'fetch_game' })
        );
      } finally {
        setLoading(false);
      }
    };

    loadGame();
  }, [gameId]);

  // Load BGG details when game loads
  useEffect(() => {
    if (!game?.bggId) {
      setBggDetails(null);
      return;
    }

    const loadBggDetails = async () => {
      setBggLoading(true);
      setBggError(null);
      try {
        const details = await api.bgg.getGameDetails(game.bggId!);
        setBggDetails(details);
      } catch (err) {
        setBggError('Failed to load BGG details');
        logger.error(
          'Failed to load BGG details',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadBggDetails', { bggId: game.bggId, operation: 'fetch_bgg_details' })
        );
      } finally {
        setBggLoading(false);
      }
    };

    loadBggDetails();
  }, [game]);

  // Load sessions when tab is activated
  useEffect(() => {
    if (!gameId || activeTab !== 'sessions') return;

    const loadSessions = async () => {
      setSessionsLoading(true);
      try {
        const response = await api.sessions.getHistory({
          gameId,
          limit: 50
        });
        setSessions(response.sessions);
      } catch (err) {
        logger.error(
          'Failed to load game sessions',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameDetailPage', 'loadSessions', { gameId, operation: 'fetch_sessions' })
        );
        setSessions([]);
      } finally {
        setSessionsLoading(false);
      }
    };

    loadSessions();
  }, [gameId, activeTab]);

  // Load notes from localStorage
  useEffect(() => {
    if (!gameId) return;

    try {
      const savedNotes = localStorage.getItem(NOTES_STORAGE_KEY);
      if (savedNotes) {
        const allNotes: GameNotes = JSON.parse(savedNotes);
        setNotes(allNotes[gameId] || '');
      }
    } catch (err) {
      logger.error(
        'Failed to load game notes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameDetailPage', 'loadNotes', { gameId, operation: 'load_notes' })
      );
    }
  }, [gameId]);

  // Save notes to localStorage
  const handleSaveNotes = () => {
    if (!gameId) return;

    try {
      const savedNotes = localStorage.getItem(NOTES_STORAGE_KEY);
      const allNotes: GameNotes = savedNotes ? JSON.parse(savedNotes) : {};
      allNotes[gameId] = notes;
      localStorage.setItem(NOTES_STORAGE_KEY, JSON.stringify(allNotes));
      alert('Notes saved successfully!');
    } catch (err) {
      logger.error(
        'Failed to save game notes',
        err instanceof Error ? err : new Error(String(err)),
        createErrorContext('GameDetailPage', 'handleSaveNotes', { gameId, operation: 'save_notes' })
      );
      alert('Failed to save notes');
    }
  };

  if (loading) {
    return (
      <div className="container mx-auto p-6 max-w-6xl">
        <Skeleton className="h-8 w-64 mb-6" />
        <Skeleton className="h-96 w-full" />
      </div>
    );
  }

  if (error || !game) {
    return (
      <div className="container mx-auto p-6 max-w-6xl">
        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertDescription>{error || 'Game not found'}</AlertDescription>
        </Alert>
        <Button asChild className="mt-4">
          <Link href="/games">
            <ArrowLeft className="mr-2 h-4 w-4" /> Back to Games
          </Link>
        </Button>
      </div>
    );
  }

  return (
      <div className="container mx-auto p-6 max-w-6xl">
        {/* Header */}
        <div className="mb-6">
          <Button asChild variant="ghost" className="mb-4">
            <Link href="/games">
              <ArrowLeft className="mr-2 h-4 w-4" /> Back to Games
            </Link>
          </Button>

          <div className="flex items-start justify-between gap-4">
            <div>
              <h1 className="text-3xl font-bold mb-2">{game.title}</h1>
              {game.publisher && (
                <p className="text-muted-foreground">{game.publisher}</p>
              )}
            </div>
            {game.bggId && (
              <Badge variant="secondary">BGG #{game.bggId}</Badge>
            )}
          </div>

          {/* Basic Info */}
          <div className="flex flex-wrap gap-4 text-sm mt-4">
            {(game.minPlayers !== null || game.maxPlayers !== null) && (
              <div className="flex items-center gap-2">
                <Users className="h-4 w-4 text-muted-foreground" />
                <span>
                  {game.minPlayers === game.maxPlayers
                    ? `${game.minPlayers} players`
                    : `${game.minPlayers || '?'}-${game.maxPlayers || '?'} players`}
                </span>
              </div>
            )}

            {(game.minPlayTimeMinutes !== null || game.maxPlayTimeMinutes !== null) && (
              <div className="flex items-center gap-2">
                <Clock className="h-4 w-4 text-muted-foreground" />
                <span>
                  {game.minPlayTimeMinutes === game.maxPlayTimeMinutes
                    ? `${game.minPlayTimeMinutes} min`
                    : `${game.minPlayTimeMinutes || '?'}-${game.maxPlayTimeMinutes || '?'} min`}
                </span>
              </div>
            )}

            {game.yearPublished && (
              <div className="flex items-center gap-2">
                <Calendar className="h-4 w-4 text-muted-foreground" />
                <span>{game.yearPublished}</span>
              </div>
            )}
          </div>
        </div>

        {/* Tabs */}
        <Tabs value={activeTab} onValueChange={setActiveTab} className="w-full">
          <TabsList className="grid w-full grid-cols-4">
            <TabsTrigger value="overview">
              <Star className="mr-2 h-4 w-4" />
              Overview
            </TabsTrigger>
            <TabsTrigger value="rules">
              <BookOpen className="mr-2 h-4 w-4" />
              Rules
            </TabsTrigger>
            <TabsTrigger value="sessions">
              <PlayCircle className="mr-2 h-4 w-4" />
              Sessions
            </TabsTrigger>
            <TabsTrigger value="notes">
              <StickyNote className="mr-2 h-4 w-4" />
              Notes
            </TabsTrigger>
          </TabsList>

          {/* Overview Tab */}
          <TabsContent value="overview" className="space-y-4">
            {game.bggId && (
              <>
                {bggLoading && (
                  <div className="space-y-3">
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-full" />
                    <Skeleton className="h-4 w-3/4" />
                  </div>
                )}

                {bggError && (
                  <Alert variant="destructive">
                    <AlertCircle className="h-4 w-4" />
                    <AlertDescription>{bggError}</AlertDescription>
                  </Alert>
                )}

                {bggDetails && (
                  <div className="space-y-4">
                    {/* BGG Image */}
                    {bggDetails.imageUrl && (
                      <div className="flex justify-center">
                        <img
                          src={bggDetails.imageUrl}
                          alt={bggDetails.name}
                          className="max-h-96 rounded-md object-contain"
                        />
                      </div>
                    )}

                    {/* BGG Ratings */}
                    <Card>
                      <CardHeader>
                        <CardTitle>Ratings & Stats</CardTitle>
                      </CardHeader>
                      <CardContent className="grid grid-cols-2 gap-4 text-sm">
                        {bggDetails.averageRating && (
                          <div className="flex items-center gap-2">
                            <Star className="h-4 w-4 text-yellow-500" />
                            <span>Rating: {bggDetails.averageRating.toFixed(2)}</span>
                          </div>
                        )}

                        {bggDetails.averageWeight && (
                          <div className="flex items-center gap-2">
                            <TrendingUp className="h-4 w-4 text-muted-foreground" />
                            <span>Complexity: {bggDetails.averageWeight.toFixed(2)}/5</span>
                          </div>
                        )}

                        {bggDetails.usersRated && (
                          <div className="text-muted-foreground col-span-2">
                            {bggDetails.usersRated.toLocaleString()} ratings
                          </div>
                        )}
                      </CardContent>
                    </Card>

                    {/* Description */}
                    {bggDetails.description && (
                      <Card>
                        <CardHeader>
                          <CardTitle>Description</CardTitle>
                        </CardHeader>
                        <CardContent>
                          <div
                            className="prose prose-sm max-w-none text-muted-foreground"
                            dangerouslySetInnerHTML={{ __html: bggDetails.description }}
                          />
                        </CardContent>
                      </Card>
                    )}

                    {/* Categories & Mechanics */}
                    {(bggDetails.categories.length > 0 || bggDetails.mechanics.length > 0) && (
                      <Card>
                        <CardHeader>
                          <CardTitle>Categories & Mechanics</CardTitle>
                        </CardHeader>
                        <CardContent className="space-y-3">
                          {bggDetails.categories.length > 0 && (
                            <div>
                              <h4 className="font-semibold mb-2 text-sm">Categories</h4>
                              <div className="flex flex-wrap gap-2">
                                {bggDetails.categories.map((category) => (
                                  <Badge key={category} variant="secondary">
                                    {category}
                                  </Badge>
                                ))}
                              </div>
                            </div>
                          )}

                          {bggDetails.mechanics.length > 0 && (
                            <div>
                              <h4 className="font-semibold mb-2 text-sm">Mechanics</h4>
                              <div className="flex flex-wrap gap-2">
                                {bggDetails.mechanics.map((mechanic) => (
                                  <Badge key={mechanic} variant="outline">
                                    {mechanic}
                                  </Badge>
                                ))}
                              </div>
                            </div>
                          )}
                        </CardContent>
                      </Card>
                    )}

                    {/* Designers & Publishers */}
                    {(bggDetails.designers.length > 0 || bggDetails.publishers.length > 0) && (
                      <Card>
                        <CardHeader>
                          <CardTitle>Credits</CardTitle>
                        </CardHeader>
                        <CardContent className="grid grid-cols-2 gap-4 text-sm">
                          {bggDetails.designers.length > 0 && (
                            <div>
                              <h4 className="font-semibold mb-1">Designers</h4>
                              <ul className="list-disc list-inside text-muted-foreground">
                                {bggDetails.designers.slice(0, 5).map((designer) => (
                                  <li key={designer}>{designer}</li>
                                ))}
                              </ul>
                            </div>
                          )}

                          {bggDetails.publishers.length > 0 && (
                            <div>
                              <h4 className="font-semibold mb-1">Publishers</h4>
                              <ul className="list-disc list-inside text-muted-foreground">
                                {bggDetails.publishers.slice(0, 5).map((publisher) => (
                                  <li key={publisher}>{publisher}</li>
                                ))}
                              </ul>
                            </div>
                          )}
                        </CardContent>
                      </Card>
                    )}

                    {/* BGG Link */}
                    <Button
                      variant="outline"
                      asChild
                      className="w-full"
                    >
                      <a
                        href={`https://boardgamegeek.com/boardgame/${game.bggId}`}
                        target="_blank"
                        rel="noopener noreferrer"
                      >
                        View on BoardGameGeek
                        <ExternalLink className="ml-2 h-4 w-4" />
                      </a>
                    </Button>
                  </div>
                )}
              </>
            )}

            {!game.bggId && (
              <Alert>
                <AlertCircle className="h-4 w-4" />
                <AlertDescription>
                  No BoardGameGeek data available for this game.
                </AlertDescription>
              </Alert>
            )}
          </TabsContent>

          {/* Rules Tab */}
          <TabsContent value="rules">
            <Card>
              <CardHeader>
                <CardTitle>Game Rules</CardTitle>
                <CardDescription>
                  Rules specification and reference
                </CardDescription>
              </CardHeader>
              <CardContent>
                <Alert>
                  <BookOpen className="h-4 w-4" />
                  <AlertDescription>
                    Rules integration coming soon. Backend query (GetRuleSpecsQuery) will be
                    implemented in a separate issue.
                  </AlertDescription>
                </Alert>
                <div className="mt-4 text-sm text-muted-foreground">
                  <p>Future features:</p>
                  <ul className="list-disc list-inside mt-2 space-y-1">
                    <li>View rule specifications</li>
                    <li>Search rules by keyword</li>
                    <li>Version history</li>
                    <li>PDF rule document viewer</li>
                  </ul>
                </div>
              </CardContent>
            </Card>
          </TabsContent>

          {/* Sessions Tab */}
          <TabsContent value="sessions" className="space-y-4">
            <Card>
              <CardHeader>
                <CardTitle>Play Sessions</CardTitle>
                <CardDescription>
                  Game session history and statistics
                </CardDescription>
              </CardHeader>
              <CardContent>
                {sessionsLoading && (
                  <div className="space-y-3">
                    <Skeleton className="h-16 w-full" />
                    <Skeleton className="h-16 w-full" />
                    <Skeleton className="h-16 w-full" />
                  </div>
                )}

                {!sessionsLoading && sessions.length === 0 && (
                  <Alert>
                    <PlayCircle className="h-4 w-4" />
                    <AlertDescription>
                      No play sessions recorded yet. Start a new session to track your games!
                    </AlertDescription>
                  </Alert>
                )}

                {!sessionsLoading && sessions.length > 0 && (
                  <div className="space-y-3">
                    {sessions.map((session) => (
                      <Card key={session.id}>
                        <CardHeader className="pb-3">
                          <div className="flex items-start justify-between">
                            <div>
                              <Badge variant={session.status === 'Completed' ? 'default' : 'secondary'}>
                                {session.status}
                              </Badge>
                              <CardDescription className="mt-2">
                                {new Date(session.startedAt).toLocaleDateString()} •{' '}
                                {session.durationMinutes} min
                              </CardDescription>
                            </div>
                            {session.winnerName && (
                              <Badge variant="outline">
                                Winner: {session.winnerName}
                              </Badge>
                            )}
                          </div>
                        </CardHeader>
                        <CardContent>
                          <div className="flex items-center gap-2 text-sm">
                            <Users className="h-4 w-4 text-muted-foreground" />
                            <span>
                              {session.playerCount} players:{' '}
                              {session.players.map(p => p.playerName).join(', ')}
                            </span>
                          </div>
                          {session.notes && (
                            <p className="text-sm text-muted-foreground mt-2">
                              Notes: {session.notes}
                            </p>
                          )}
                        </CardContent>
                      </Card>
                    ))}
                  </div>
                )}

                <Separator className="my-4" />

                <Button asChild className="w-full">
                  <Link href="/sessions">View All Sessions</Link>
                </Button>
              </CardContent>
            </Card>
          </TabsContent>

          {/* Notes Tab */}
          <TabsContent value="notes">
            <Card>
              <CardHeader>
                <CardTitle>Your Notes</CardTitle>
                <CardDescription>
                  Personal notes about {game.title}
                </CardDescription>
              </CardHeader>
              <CardContent className="space-y-4">
                <Textarea
                  placeholder="Write your notes about strategies, house rules, or game impressions..."
                  value={notes}
                  onChange={(e) => setNotes(e.target.value)}
                  rows={10}
                  className="resize-none"
                />
                <div className="flex justify-between items-center">
                  <p className="text-xs text-muted-foreground">
                    Notes are saved locally in your browser
                  </p>
                  <Button onClick={handleSaveNotes}>
                    <StickyNote className="mr-2 h-4 w-4" />
                    Save Notes
                  </Button>
                </div>
              </CardContent>
            </Card>
          </TabsContent>
        </Tabs>
      </div>
  );
}
