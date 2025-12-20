/**
 * Game Overview Tab Component
 *
 * Displays game metadata, BGG integration, and basic information
 * Reuses BGG integration logic from GameDetailModal
 */

import React, { useEffect, useState } from 'react';

import { Users, Clock, Calendar, Star, TrendingUp, ExternalLink } from 'lucide-react';
import Image from 'next/image';

import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Card, CardContent, CardDescription, CardHeader, CardTitle } from '@/components/ui/card';
import { Separator } from '@/components/ui/separator';
import { Skeleton } from '@/components/ui/skeleton';
import { Game, BggGameDetails, api } from '@/lib/api';
import { createErrorContext } from '@/lib/errors';
import { logger } from '@/lib/logger';

interface GameOverviewTabProps {
  game: Game;
}

export function GameOverviewTab({ game }: GameOverviewTabProps) {
  const [bggDetails, setBggDetails] = useState<BggGameDetails | null>(null);
  const [loading, setLoading] = useState(false);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    if (!game.bggId) {
      return;
    }

    const fetchBggDetails = async () => {
      setLoading(true);
      setError(null);
      try {
        // eslint-disable-next-line @typescript-eslint/no-non-null-assertion -- bggId checked in useEffect dependency
        const details = await api.bgg.getGameDetails(game.bggId!);
        setBggDetails(details);
      } catch (err) {
        setError('Failed to load BoardGameGeek details');
        logger.error(
          'BGG fetch error',
          err instanceof Error ? err : new Error(String(err)),
          createErrorContext('GameOverviewTab', 'fetchBggDetails', { bggId: game.bggId })
        );
      } finally {
        setLoading(false);
      }
    };

    fetchBggDetails();
  }, [game.bggId]);

  return (
    <div className="space-y-6">
      {/* Basic Game Info Card */}
      <Card>
        <CardHeader>
          <CardTitle>Game Information</CardTitle>
          <CardDescription>Core game details and specifications</CardDescription>
        </CardHeader>
        <CardContent className="space-y-4">
          <div className="grid grid-cols-1 md:grid-cols-2 gap-4">
            {/* Publisher */}
            {game.publisher && (
              <div>
                <label className="text-sm font-medium text-muted-foreground">Publisher</label>
                <p className="text-base">{game.publisher}</p>
              </div>
            )}

            {/* Year Published */}
            {game.yearPublished && (
              <div>
                <label className="text-sm font-medium text-muted-foreground">Year Published</label>
                <div className="flex items-center gap-2 text-base">
                  <Calendar className="h-4 w-4 text-muted-foreground" />
                  <span>{game.yearPublished}</span>
                </div>
              </div>
            )}

            {/* Player Count */}
            {(game.minPlayers !== null || game.maxPlayers !== null) && (
              <div>
                <label className="text-sm font-medium text-muted-foreground">Players</label>
                <div className="flex items-center gap-2 text-base">
                  <Users className="h-4 w-4 text-muted-foreground" />
                  <span>
                    {game.minPlayers === game.maxPlayers
                      ? `${game.minPlayers} players`
                      : `${game.minPlayers || '?'}-${game.maxPlayers || '?'} players`}
                  </span>
                </div>
              </div>
            )}

            {/* Play Time */}
            {(game.minPlayTimeMinutes !== null || game.maxPlayTimeMinutes !== null) && (
              <div>
                <label className="text-sm font-medium text-muted-foreground">Play Time</label>
                <div className="flex items-center gap-2 text-base">
                  <Clock className="h-4 w-4 text-muted-foreground" />
                  <span>
                    {game.minPlayTimeMinutes === game.maxPlayTimeMinutes
                      ? `${game.minPlayTimeMinutes} minutes`
                      : `${game.minPlayTimeMinutes || '?'}-${game.maxPlayTimeMinutes || '?'} minutes`}
                  </span>
                </div>
              </div>
            )}

            {/* BGG ID */}
            {game.bggId && (
              <div>
                <label className="text-sm font-medium text-muted-foreground">
                  BoardGameGeek ID
                </label>
                <div className="flex items-center gap-2">
                  <Badge variant="secondary">{game.bggId}</Badge>
                </div>
              </div>
            )}

            {/* Created At */}
            <div>
              <label className="text-sm font-medium text-muted-foreground">Added to MeepleAI</label>
              <p className="text-base">{new Date(game.createdAt).toLocaleDateString()}</p>
            </div>
          </div>
        </CardContent>
      </Card>

      {/* BGG Details Card */}
      {game.bggId && (
        <Card>
          <CardHeader>
            <CardTitle className="flex items-center gap-2">
              BoardGameGeek Details
              <Badge variant="secondary">BGG</Badge>
            </CardTitle>
            <CardDescription>Information from BoardGameGeek community</CardDescription>
          </CardHeader>
          <CardContent>
            {loading && (
              <div className="space-y-3">
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-full" />
                <Skeleton className="h-4 w-3/4" />
              </div>
            )}

            {error && <div className="text-sm text-destructive">{error}</div>}

            {bggDetails && (
              <div className="space-y-6">
                {/* BGG Image */}
                {bggDetails.imageUrl && (
                  <div className="flex justify-center">
                    <div className="relative max-h-80 w-full rounded-md border overflow-hidden">
                      <Image
                        src={bggDetails.imageUrl}
                        alt={bggDetails.name}
                        width={640}
                        height={320}
                        className="object-contain mx-auto"
                        sizes="(max-width: 768px) 100vw, 640px"
                      />
                    </div>
                  </div>
                )}

                {/* BGG Ratings */}
                <div className="grid grid-cols-2 md:grid-cols-4 gap-4">
                  {bggDetails.averageRating && (
                    <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
                      <Star className="h-5 w-5 text-yellow-500 mb-2" />
                      <div className="text-2xl font-bold">
                        {bggDetails.averageRating.toFixed(2)}
                      </div>
                      <div className="text-xs text-muted-foreground">Average Rating</div>
                    </div>
                  )}

                  {bggDetails.averageWeight && (
                    <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
                      <TrendingUp className="h-5 w-5 text-muted-foreground mb-2" />
                      <div className="text-2xl font-bold">
                        {bggDetails.averageWeight.toFixed(2)}
                      </div>
                      <div className="text-xs text-muted-foreground">Complexity (1-5)</div>
                    </div>
                  )}

                  {bggDetails.usersRated && (
                    <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
                      <Users className="h-5 w-5 text-muted-foreground mb-2" />
                      <div className="text-2xl font-bold">
                        {bggDetails.usersRated.toLocaleString()}
                      </div>
                      <div className="text-xs text-muted-foreground">User Ratings</div>
                    </div>
                  )}

                  {bggDetails.minAge && (
                    <div className="flex flex-col items-center p-4 bg-slate-100 dark:bg-slate-800 rounded-lg">
                      <Users className="h-5 w-5 text-muted-foreground mb-2" />
                      <div className="text-2xl font-bold">{bggDetails.minAge}+</div>
                      <div className="text-xs text-muted-foreground">Minimum Age</div>
                    </div>
                  )}
                </div>

                {/* Description */}
                {bggDetails.description && (
                  <>
                    <Separator />
                    <div>
                      <h4 className="font-semibold mb-3">Description</h4>
                      <div
                        className="text-sm text-muted-foreground prose prose-sm max-w-none dark:prose-invert"
                        dangerouslySetInnerHTML={{ __html: bggDetails.description }}
                      />
                    </div>
                  </>
                )}

                {/* Categories & Mechanics */}
                {(bggDetails.categories.length > 0 || bggDetails.mechanics.length > 0) && (
                  <>
                    <Separator />
                    <div className="space-y-4">
                      {bggDetails.categories.length > 0 && (
                        <div>
                          <h4 className="font-semibold mb-2">Categories</h4>
                          <div className="flex flex-wrap gap-2">
                            {bggDetails.categories.map(category => (
                              <Badge key={category} variant="secondary">
                                {category}
                              </Badge>
                            ))}
                          </div>
                        </div>
                      )}

                      {bggDetails.mechanics.length > 0 && (
                        <div>
                          <h4 className="font-semibold mb-2">Mechanics</h4>
                          <div className="flex flex-wrap gap-2">
                            {bggDetails.mechanics.map(mechanic => (
                              <Badge key={mechanic} variant="outline">
                                {mechanic}
                              </Badge>
                            ))}
                          </div>
                        </div>
                      )}
                    </div>
                  </>
                )}

                {/* Designers & Publishers */}
                {(bggDetails.designers.length > 0 || bggDetails.publishers.length > 0) && (
                  <>
                    <Separator />
                    <div className="grid grid-cols-1 md:grid-cols-2 gap-6">
                      {bggDetails.designers.length > 0 && (
                        <div>
                          <h4 className="font-semibold mb-2">Designers</h4>
                          <ul className="list-disc list-inside text-sm text-muted-foreground space-y-1">
                            {bggDetails.designers.map(designer => (
                              <li key={designer}>{designer}</li>
                            ))}
                          </ul>
                        </div>
                      )}

                      {bggDetails.publishers.length > 0 && (
                        <div>
                          <h4 className="font-semibold mb-2">Publishers</h4>
                          <ul className="list-disc list-inside text-sm text-muted-foreground space-y-1">
                            {bggDetails.publishers.slice(0, 5).map(publisher => (
                              <li key={publisher}>{publisher}</li>
                            ))}
                            {bggDetails.publishers.length > 5 && (
                              <li className="italic">
                                and {bggDetails.publishers.length - 5} more...
                              </li>
                            )}
                          </ul>
                        </div>
                      )}
                    </div>
                  </>
                )}

                {/* BGG Link */}
                <Separator />
                <Button variant="outline" size="sm" asChild className="w-full">
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
          </CardContent>
        </Card>
      )}
    </div>
  );
}
