'use client';

import React, { useState, useEffect } from 'react';

import { Loader2, Users, Play, TrendingUp } from 'lucide-react';
import { useParams, useRouter } from 'next/navigation';
import { toast } from 'sonner';

import { Badge } from '@/components/ui/data-display/badge';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Button } from '@/components/ui/primitives/button';
import { getGameTemplateByName, type GameTemplate } from '@/lib/config/game-templates';
import { useSessionStore } from '@/lib/stores/sessionStore';

interface GameDetails {
  id: string;
  name: string;
  imageUrl?: string;
  playerCount: { min: number; max: number };
}

/**
 * Game-Specific Toolkit Landing Page
 *
 * Features:
 * - Game template preview (rounds, categories, rules)
 * - Start game session with template
 * - Recent sessions for this game
 */
export default function GameToolkitLandingPage() {
  const params = useParams();
  const router = useRouter();
  const gameId = params?.gameId as string;

  if (!gameId) {
    throw new Error('Game ID is required');
  }

  const { createSession, isLoading } = useSessionStore();

  const [game, setGame] = useState<GameDetails | null>(null);
  const [template, setTemplate] = useState<GameTemplate | null>(null);
  const [loadingGame, setLoadingGame] = useState(true);
  const [participants, setParticipants] = useState<string[]>(['']);

  // Load game details
  useEffect(() => {
    const fetchGame = async () => {
      try {
        const baseUrl = process.env.NEXT_PUBLIC_API_BASE || '';
        const response = await fetch(`${baseUrl}/api/v1/games/${gameId}`, {
          credentials: 'include',
        });

        if (!response.ok) {
          throw new Error('Game not found');
        }

        const gameData = await response.json();
        setGame(gameData);

        // Try to find template for this game
        const gameTemplate = getGameTemplateByName(gameData.name);
        setTemplate(gameTemplate ?? null);
      } catch (err) {
        toast.error(err instanceof Error ? err.message : 'Failed to load game');
        router.push('/library');
      } finally {
        setLoadingGame(false);
      }
    };

    void fetchGame();
  }, [gameId, router]);

  /**
   * Handle start session
   */
  const handleStartSession = async () => {
    const validParticipants = participants.filter(name => name.trim().length > 0);

    if (validParticipants.length === 0) {
      toast.error('Please add at least one participant');
      return;
    }

    if (game && validParticipants.length < game.playerCount.min) {
      toast.error(`${game.name} requires at least ${game.playerCount.min} players`);
      return;
    }

    if (game && validParticipants.length > game.playerCount.max) {
      toast.error(`${game.name} supports maximum ${game.playerCount.max} players`);
      return;
    }

    try {
      await createSession({
        participants: validParticipants.map(name => ({ displayName: name.trim() })),
        sessionDate: new Date(),
      });

      const activeSession = useSessionStore.getState().activeSession;
      if (activeSession) {
        toast.success(`${game?.name || 'Game'} session started!`);
        router.push(`/library/games/${gameId}/toolkit/${activeSession.id}`);
      }
    } catch (err) {
      toast.error(err instanceof Error ? err.message : 'Failed to start session');
    }
  };

  /**
   * Add participant
   */
  const addParticipant = () => {
    setParticipants([...participants, '']);
  };

  /**
   * Remove participant
   */
  const removeParticipant = (index: number) => {
    setParticipants(participants.filter((_, i) => i !== index));
  };

  /**
   * Update participant
   */
  const updateParticipant = (index: number, value: string) => {
    const updated = [...participants];
    // eslint-disable-next-line security/detect-object-injection -- index is validated function parameter for array update
    updated[index] = value;
    setParticipants(updated);
  };

  // Loading state
  if (loadingGame || !game) {
    return (
      <div className="min-h-screen flex items-center justify-center">
        <Loader2 className="w-8 h-8 animate-spin text-blue-600" />
      </div>
    );
  }

  return (
    <div className="min-h-screen bg-gradient-to-br from-blue-50 via-white to-purple-50 dark:from-gray-900 dark:via-gray-800 dark:to-gray-900">
      <div className="container mx-auto px-4 py-8 max-w-5xl">
        {/* Game Header */}
        <div className="text-center mb-8">
          {template && (
            <div className="inline-flex items-center justify-center w-20 h-20 mb-4 rounded-full bg-gradient-to-br from-blue-500 to-purple-600 text-4xl">
              {template.icon}
            </div>
          )}
          <h1 className="text-4xl font-bold mb-2 bg-gradient-to-r from-blue-600 to-purple-600 bg-clip-text text-transparent">
            {game.name} Toolkit
          </h1>
          <p className="text-lg text-gray-600 dark:text-gray-300">
            Track scores with pre-configured {game.name} template
          </p>
        </div>

        <div className="grid md:grid-cols-2 gap-6 mb-8">
          {/* Template Preview */}
          {template && (
            <Card>
              <CardHeader>
                <CardTitle className="flex items-center gap-2">
                  <TrendingUp className="w-5 h-5" />
                  Game Template
                </CardTitle>
              </CardHeader>
              <CardContent className="space-y-4">
                <div>
                  <p className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">
                    Scoring Categories
                  </p>
                  <div className="flex flex-wrap gap-2">
                    {template.categories.map(category => (
                      <Badge key={category} variant="outline">
                        {category}
                      </Badge>
                    ))}
                  </div>
                </div>

                {template.rounds.length > 0 && (
                  <div>
                    <p className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">
                      Rounds
                    </p>
                    <div className="flex gap-2">
                      {template.rounds.map(round => (
                        <Badge key={round} variant="secondary">
                          Round {round}
                        </Badge>
                      ))}
                    </div>
                  </div>
                )}

                <div>
                  <p className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">
                    Scoring Rules
                  </p>
                  <p className="text-sm text-gray-600 dark:text-gray-400">{template.scoringRules}</p>
                </div>

                <div>
                  <p className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-1">
                    Players
                  </p>
                  <p className="text-sm text-gray-600 dark:text-gray-400">
                    {template.playerCount.min}-{template.playerCount.max} players
                  </p>
                </div>
              </CardContent>
            </Card>
          )}

          {/* Start Session */}
          <Card>
            <CardHeader>
              <CardTitle className="flex items-center gap-2">
                <Users className="w-5 h-5" />
                Start {game.name} Session
              </CardTitle>
            </CardHeader>
            <CardContent className="space-y-4">
              <div>
                <p className="text-sm font-semibold text-gray-700 dark:text-gray-300 mb-2">
                  Players
                </p>
                <div className="space-y-2">
                  {participants.map((name, index) => (
                    <div key={index} className="flex gap-2">
                      <input
                        type="text"
                        value={name}
                        onChange={e => updateParticipant(index, e.target.value)}
                        placeholder={`Player ${index + 1}`}
                        className="flex-1 px-3 py-2 border border-gray-300 dark:border-gray-600 rounded-lg focus:ring-2 focus:ring-blue-500 dark:bg-gray-800 dark:text-white"
                        disabled={isLoading}
                      />
                      {participants.length > 1 && (
                        <Button
                          variant="outline"
                          size="icon"
                          onClick={() => removeParticipant(index)}
                          disabled={isLoading}
                        >
                          ×
                        </Button>
                      )}
                    </div>
                  ))}
                </div>
                <Button
                  variant="ghost"
                  size="sm"
                  onClick={addParticipant}
                  disabled={isLoading}
                  className="mt-2"
                >
                  + Add Player
                </Button>
              </div>

              <Button onClick={handleStartSession} disabled={isLoading} className="w-full">
                <Play className="w-4 h-4 mr-2" />
                {isLoading ? 'Starting...' : `Start ${game.name} Session`}
              </Button>

              {template && (
                <p className="text-xs text-gray-500 dark:text-gray-400 text-center">
                  Template will auto-populate {template.categories.length} categories
                  {template.rounds.length > 0 && ` and ${template.rounds.length} rounds`}
                </p>
              )}
            </CardContent>
          </Card>
        </div>

        {/* Recent Sessions Placeholder */}
        <Card>
          <CardHeader>
            <CardTitle>Recent {game.name} Sessions</CardTitle>
          </CardHeader>
          <CardContent>
            <p className="text-gray-500 dark:text-gray-400 text-center py-4">
              No recent sessions yet. Start your first session above!
            </p>
          </CardContent>
        </Card>
      </div>
    </div>
  );
}
