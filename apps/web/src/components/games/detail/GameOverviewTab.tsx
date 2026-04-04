/**
 * Game Overview Tab Component
 *
 * Displays game metadata and basic information
 */

import React from 'react';

import { Users, Clock, Calendar } from 'lucide-react';

import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Game } from '@/lib/api';

interface GameOverviewTabProps {
  game: Game;
}

export function GameOverviewTab({ game }: GameOverviewTabProps) {
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

            {/* Created At */}
            <div>
              <label className="text-sm font-medium text-muted-foreground">Added to MeepleAI</label>
              <p className="text-base">{new Date(game.createdAt).toLocaleDateString()}</p>
            </div>
          </div>
        </CardContent>
      </Card>
    </div>
  );
}
