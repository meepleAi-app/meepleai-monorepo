/**
 * ActiveSessionsSection Component (Issue #2617)
 *
 * Dashboard widget showing active game sessions.
 * Features:
 * - Responsive card grid
 * - Session status badges
 * - Resume/Pause actions
 * - Link to full sessions page
 * - Empty state (hidden if no active sessions)
 *
 * @see docs/04-frontend/wireframes-playful-boardroom.md
 */

'use client';

import Link from 'next/link';
import { useRouter } from 'next/navigation';

import {
  AlertCircle,
  ArrowRight,
  Clock,
  Pause,
  Play,
  PlayCircle,
  Users,
} from 'lucide-react';
import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';

import { Alert, AlertDescription, AlertTitle } from '@/components/ui/feedback/alert';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import {
  Card,
  CardContent,
  CardDescription,
  CardHeader,
  CardTitle,
} from '@/components/ui/data-display/card';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { useActiveSessions, usePauseSession, useResumeSession } from '@/hooks/queries';
import { useGames } from '@/hooks/queries/useGames';
import type { GameSessionDto, Game } from '@/lib/api';
import { toast } from 'sonner';

export interface ActiveSessionsSectionProps {
  /** Number of sessions to display (default: 3) */
  limit?: number;
}

/**
 * Session status badge with appropriate color
 */
function SessionStatusBadge({ status }: { status: string }) {
  const variants: Record<string, 'default' | 'secondary' | 'destructive' | 'outline'> = {
    InProgress: 'default',
    Paused: 'secondary',
    Setup: 'outline',
  };

  const labels: Record<string, string> = {
    InProgress: 'In Corso',
    Paused: 'In Pausa',
    Setup: 'Preparazione',
  };

  return (
    <Badge variant={variants[status] || 'outline'} className="text-xs">
      {labels[status] || status}
    </Badge>
  );
}

/**
 * Single session card for dashboard display
 */
function SessionCard({
  session,
  gameTitle,
  onPause,
  onResume,
  isPausing,
  isResuming,
}: {
  session: GameSessionDto;
  gameTitle: string;
  onPause: () => void;
  onResume: () => void;
  isPausing: boolean;
  isResuming: boolean;
}) {
  const router = useRouter();

  const canPause = session.status === 'InProgress';
  const canResume = session.status === 'Paused';

  const handleNavigate = () => {
    router.push(`/sessions/${session.id}`);
  };

  return (
    <Card
      className="group cursor-pointer transition-all hover:shadow-md"
      onClick={handleNavigate}
      tabIndex={0}
      onKeyDown={e => {
        if (e.key === 'Enter' || e.key === ' ') {
          e.preventDefault();
          handleNavigate();
        }
      }}
      role="button"
      aria-label={`Vai alla sessione di ${gameTitle}`}
    >
      <CardHeader className="pb-2">
        <div className="flex items-start justify-between gap-2">
          <CardTitle className="text-base font-semibold line-clamp-1">{gameTitle}</CardTitle>
          <SessionStatusBadge status={session.status} />
        </div>
        <CardDescription className="flex items-center gap-1 text-xs">
          <Users className="h-3 w-3" aria-hidden="true" />
          {session.playerCount} giocator{session.playerCount !== 1 ? 'i' : 'e'}
        </CardDescription>
      </CardHeader>

      <CardContent className="space-y-3">
        {/* Time info */}
        <div className="flex items-center justify-between text-xs text-muted-foreground">
          <span className="flex items-center gap-1">
            <Clock className="h-3 w-3" aria-hidden="true" />
            {formatDistanceToNow(new Date(session.startedAt), {
              addSuffix: true,
              locale: it,
            })}
          </span>
          {session.durationMinutes > 0 && (
            <span>
              {session.durationMinutes < 60
                ? `${session.durationMinutes}m`
                : `${Math.floor(session.durationMinutes / 60)}h ${session.durationMinutes % 60}m`}
            </span>
          )}
        </div>

        {/* Action buttons */}
        <div
          className="flex gap-2"
          onClick={e => e.stopPropagation()}
          onKeyDown={e => e.stopPropagation()}
        >
          {canResume && (
            <Button
              size="sm"
              variant="default"
              className="flex-1"
              onClick={onResume}
              disabled={isResuming}
              aria-label={`Riprendi sessione di ${gameTitle}`}
            >
              <Play className="h-4 w-4 mr-1" aria-hidden="true" />
              {isResuming ? 'Riprendendo...' : 'Riprendi'}
            </Button>
          )}
          {canPause && (
            <Button
              size="sm"
              variant="outline"
              className="flex-1"
              onClick={onPause}
              disabled={isPausing}
              aria-label={`Metti in pausa sessione di ${gameTitle}`}
            >
              <Pause className="h-4 w-4 mr-1" aria-hidden="true" />
              {isPausing ? 'Pausando...' : 'Pausa'}
            </Button>
          )}
        </div>
      </CardContent>
    </Card>
  );
}

/**
 * ActiveSessionsSection Component
 *
 * Shows active game sessions on the dashboard.
 * Hidden if no active sessions.
 */
export function ActiveSessionsSection({ limit = 3 }: ActiveSessionsSectionProps) {
  const { data: sessionsData, isLoading: sessionsLoading, error: sessionsError } = useActiveSessions(limit);
  const { data: gamesData, isLoading: gamesLoading } = useGames(undefined, undefined, 1, 100);

  const pauseSession = usePauseSession();
  const resumeSession = useResumeSession();

  const isLoading = sessionsLoading || gamesLoading;

  // Create a map of gameId -> title for quick lookup
  const gamesTitleMap: Record<string, string> = {};
  if (gamesData?.games) {
    gamesData.games.forEach((game: Game) => {
      gamesTitleMap[game.id] = game.title;
    });
  }

  const handlePause = async (sessionId: string) => {
    try {
      await pauseSession.mutateAsync(sessionId);
      toast.success('Sessione messa in pausa');
    } catch {
      toast.error('Errore durante la pausa della sessione');
    }
  };

  const handleResume = async (sessionId: string) => {
    try {
      await resumeSession.mutateAsync(sessionId);
      toast.success('Sessione ripresa');
    } catch {
      toast.error('Errore durante la ripresa della sessione');
    }
  };

  // Loading state: Skeleton cards
  if (isLoading) {
    return (
      <section className="space-y-4" aria-label="Sessioni attive">
        <div className="flex items-center justify-between">
          <h2 className="text-xl font-quicksand font-semibold">Partite in Corso</h2>
        </div>

        <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
          {Array.from({ length: limit }).map((_, i) => (
            <Skeleton key={i} className="h-40 w-full" />
          ))}
        </div>
      </section>
    );
  }

  // Error state: Alert with description
  if (sessionsError) {
    const errorMessage = sessionsError instanceof Error ? sessionsError.message : String(sessionsError);

    return (
      <section className="space-y-4" aria-label="Sessioni attive">
        <h2 className="text-xl font-quicksand font-semibold">Partite in Corso</h2>

        <Alert variant="destructive">
          <AlertCircle className="h-4 w-4" />
          <AlertTitle>Errore di Caricamento</AlertTitle>
          <AlertDescription>
            Impossibile caricare le sessioni attive.
            <span className="block mt-2 text-xs opacity-75">{errorMessage}</span>
          </AlertDescription>
        </Alert>
      </section>
    );
  }

  // Empty state: Hide section if no active sessions
  if (!sessionsData?.sessions || sessionsData.sessions.length === 0) {
    return null;
  }

  // Main content: Sessions grid
  return (
    <section className="space-y-4" aria-label="Sessioni attive">
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-2">
          <PlayCircle className="h-5 w-5 text-muted-foreground" />
          <h2 className="text-xl font-quicksand font-semibold">Partite in Corso</h2>
          {sessionsData.total > 0 && (
            <Badge variant="secondary" className="ml-2">
              {sessionsData.total}
            </Badge>
          )}
        </div>
        <Button variant="ghost" size="sm" asChild>
          <Link href="/sessions">
            Vedi Tutte
            <ArrowRight className="ml-2 h-4 w-4" />
          </Link>
        </Button>
      </div>

      <div className="grid grid-cols-1 md:grid-cols-2 lg:grid-cols-3 gap-4">
        {sessionsData.sessions.map(session => (
          <SessionCard
            key={session.id}
            session={session}
            gameTitle={gamesTitleMap[session.gameId] || 'Gioco sconosciuto'}
            onPause={() => handlePause(session.id)}
            onResume={() => handleResume(session.id)}
            isPausing={pauseSession.isPending && pauseSession.variables === session.id}
            isResuming={resumeSession.isPending && resumeSession.variables === session.id}
          />
        ))}
      </div>
    </section>
  );
}
