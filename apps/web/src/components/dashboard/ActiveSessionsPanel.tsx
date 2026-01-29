/**
 * ActiveSessionsPanel Component (Issue #2858)
 *
 * Dashboard widget showing active game sessions with game covers.
 *
 * Features:
 * - Show game cover, title, started time
 * - Continue and Details buttons
 * - Empty state: 'No active sessions'
 * - Link to session detail page
 */

'use client';

import React from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import {
  AlertCircle,
  Clock,
  ExternalLink,
  Gamepad2,
  PlayCircle,
} from 'lucide-react';
import Image from 'next/image';
import Link from 'next/link';

import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/data-display/card';
import { Alert, AlertDescription } from '@/components/ui/feedback/alert';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { useActiveSessions } from '@/hooks/queries';
import { useGames } from '@/hooks/queries/useGames';
import type { GameSessionDto, Game } from '@/lib/api';

export interface ActiveSessionsPanelProps {
  /**
   * Maximum number of sessions to display (default: 3)
   */
  limit?: number;

  /**
   * Optional className for custom styling
   */
  className?: string;
}

/**
 * Single session card with game cover
 */
function SessionCardWithCover({
  session,
  game,
}: {
  session: GameSessionDto;
  game: Game | undefined;
}) {
  const gameTitle = game?.title || 'Gioco sconosciuto';
  const gameImage = game?.imageUrl || game?.iconUrl;

  return (
    <div
      className="flex gap-3 p-3 rounded-lg border bg-card hover:bg-accent/50 transition-colors"
      data-testid={`session-card-${session.id}`}
    >
      {/* Game Cover */}
      <div
        className="relative w-16 h-16 rounded-md overflow-hidden flex-shrink-0 bg-muted"
        data-testid={`session-card-cover-${session.id}`}
      >
        {gameImage ? (
          <Image
            src={gameImage}
            alt={`Cover di ${gameTitle}`}
            fill
            className="object-cover"
            sizes="64px"
          />
        ) : (
          <div className="w-full h-full flex items-center justify-center">
            <Gamepad2 className="h-6 w-6 text-muted-foreground" />
          </div>
        )}
      </div>

      {/* Session Info */}
      <div className="flex-1 min-w-0 space-y-1">
        <p
          className="font-medium text-sm line-clamp-1"
          data-testid={`session-card-title-${session.id}`}
        >
          {gameTitle}
        </p>
        <p
          className="text-xs text-muted-foreground flex items-center gap-1"
          data-testid={`session-card-time-${session.id}`}
        >
          <Clock className="h-3 w-3" aria-hidden="true" />
          {formatDistanceToNow(new Date(session.startedAt), {
            addSuffix: true,
            locale: it,
          })}
        </p>

        {/* Action Buttons */}
        <div className="flex gap-2 pt-1">
          <Button
            size="sm"
            variant="default"
            className="h-7 text-xs"
            asChild
            data-testid={`session-card-continue-${session.id}`}
          >
            <Link href={`/sessions/${session.id}`}>
              <PlayCircle className="h-3 w-3 mr-1" aria-hidden="true" />
              Continua
            </Link>
          </Button>
          <Button
            size="sm"
            variant="outline"
            className="h-7 text-xs"
            asChild
            data-testid={`session-card-details-${session.id}`}
          >
            <Link href={`/games/${session.gameId}`}>
              <ExternalLink className="h-3 w-3 mr-1" aria-hidden="true" />
              Dettagli
            </Link>
          </Button>
        </div>
      </div>
    </div>
  );
}

/**
 * ActiveSessionsPanel Component
 *
 * Widget for displaying active game sessions on the dashboard.
 * Shows game covers, titles, start times with Continue and Details CTAs.
 *
 * @example
 * ```tsx
 * <ActiveSessionsPanel limit={3} />
 * ```
 */
export function ActiveSessionsPanel({ limit = 3, className }: ActiveSessionsPanelProps) {
  const { data: sessionsData, isLoading: sessionsLoading, error: sessionsError } = useActiveSessions(limit);
  const { data: gamesData, isLoading: gamesLoading } = useGames(undefined, undefined, 1, 100);

  const isLoading = sessionsLoading || gamesLoading;

  // Create a map of gameId -> Game for quick lookup
  const gamesMap = React.useMemo(() => {
    const map: Record<string, Game> = {};
    if (gamesData?.games) {
      gamesData.games.forEach((game: Game) => {
        map[game.id] = game;
      });
    }
    return map;
  }, [gamesData?.games]);

  // Loading state
  if (isLoading) {
    return (
      <Card className={className} data-testid="active-sessions-panel">
        <CardHeader>
          <div className="flex items-center gap-2">
            <PlayCircle className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="active-sessions-panel-title">Sessioni Attive</CardTitle>
          </div>
        </CardHeader>
        <CardContent className="space-y-3" data-testid="active-sessions-panel-loading">
          {Array.from({ length: limit }).map((_, i) => (
            <div key={i} className="flex gap-3 p-3 rounded-lg border" data-testid={`active-sessions-panel-skeleton-${i}`}>
              <Skeleton className="w-16 h-16 rounded-md flex-shrink-0" />
              <div className="flex-1 space-y-2">
                <Skeleton className="h-4 w-3/4" />
                <Skeleton className="h-3 w-1/2" />
                <div className="flex gap-2 pt-1">
                  <Skeleton className="h-7 w-20" />
                  <Skeleton className="h-7 w-20" />
                </div>
              </div>
            </div>
          ))}
        </CardContent>
      </Card>
    );
  }

  // Error state
  if (sessionsError) {
    return (
      <Card className={className} data-testid="active-sessions-panel">
        <CardHeader>
          <div className="flex items-center gap-2">
            <PlayCircle className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="active-sessions-panel-title">Sessioni Attive</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          <Alert variant="destructive" className="py-2" data-testid="active-sessions-panel-error">
            <AlertCircle className="h-4 w-4" />
            <AlertDescription className="text-xs" data-testid="active-sessions-panel-error-message">
              Impossibile caricare le sessioni. Riprova più tardi.
            </AlertDescription>
          </Alert>
        </CardContent>
      </Card>
    );
  }

  // Empty state
  if (!sessionsData?.sessions || sessionsData.sessions.length === 0) {
    return (
      <Card className={className} data-testid="active-sessions-panel">
        <CardHeader>
          <div className="flex items-center gap-2">
            <PlayCircle className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="active-sessions-panel-title">Sessioni Attive</CardTitle>
          </div>
        </CardHeader>
        <CardContent>
          <div
            className="flex flex-col items-center justify-center py-6 text-center"
            data-testid="active-sessions-panel-empty"
          >
            <Gamepad2 className="h-10 w-10 text-muted-foreground/50 mb-3" aria-hidden="true" />
            <p className="text-sm text-muted-foreground" data-testid="active-sessions-panel-empty-text">
              Nessuna sessione attiva
            </p>
            <Button
              variant="outline"
              size="sm"
              className="mt-3"
              asChild
              data-testid="active-sessions-panel-start-cta"
            >
              <Link href="/games">
                Inizia una Partita
              </Link>
            </Button>
          </div>
        </CardContent>
      </Card>
    );
  }

  // Success state with sessions
  return (
    <Card className={className} data-testid="active-sessions-panel">
      <CardHeader>
        <div className="flex items-center justify-between">
          <div className="flex items-center gap-2">
            <PlayCircle className="h-5 w-5 text-muted-foreground" />
            <CardTitle data-testid="active-sessions-panel-title">Sessioni Attive</CardTitle>
          </div>
          {sessionsData.total > limit && (
            <Button
              variant="ghost"
              size="sm"
              asChild
              data-testid="active-sessions-panel-view-all"
            >
              <Link href="/sessions">
                Vedi Tutte ({sessionsData.total})
              </Link>
            </Button>
          )}
        </div>
      </CardHeader>
      <CardContent className="space-y-3" data-testid="active-sessions-panel-list">
        {sessionsData.sessions.map((session) => (
          <SessionCardWithCover
            key={session.id}
            session={session}
            game={gamesMap[session.gameId]}
          />
        ))}
      </CardContent>
    </Card>
  );
}
