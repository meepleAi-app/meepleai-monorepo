/**
 * SessionDrawerSheet - Session History Drawer for Library Cards
 *
 * Right-side Sheet that displays session history for a game.
 * Shows session list with status, players, duration + "New Session" button.
 * Triggered by clicking "Sessions" in CardNavigationFooter.
 */

'use client';

import { useQuery } from '@tanstack/react-query';
import { Clock, Loader2, Plus, Trophy, Users } from 'lucide-react';

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { api } from '@/lib/api';
import { cn } from '@/lib/utils';

export interface SessionDrawerSheetProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
  gameId: string;
  gameTitle: string;
}

const statusConfig: Record<string, { label: string; dot: string }> = {
  InProgress: { label: 'In corso', dot: 'bg-blue-500' },
  Completed: { label: 'Completata', dot: 'bg-green-500' },
  Abandoned: { label: 'Abbandonata', dot: 'bg-slate-400' },
  Paused: { label: 'In pausa', dot: 'bg-amber-500' },
};

export function SessionDrawerSheet({
  open,
  onOpenChange,
  gameId,
  gameTitle,
}: SessionDrawerSheetProps) {
  const {
    data: sessions = [],
    isLoading: loading,
    error,
  } = useQuery({
    queryKey: ['game-sessions', gameId],
    queryFn: () => api.games.getSessions(gameId),
    enabled: open && !!gameId,
    staleTime: 60 * 1000,
  });

  const handleNewSession = () => {
    onOpenChange(false);
    window.location.href = `/sessions/new?game=${gameId}`;
  };

  const handleOpenSession = (sessionId: string) => {
    onOpenChange(false);
    window.location.href = `/sessions/${sessionId}`;
  };

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent side="right" className={cn('sm:max-w-lg', 'bg-white/80 backdrop-blur-xl')}>
        <SheetHeader className="pb-4 border-b border-border/30">
          <SheetTitle className="font-quicksand text-lg">
            <span className="text-[hsl(240,60%,55%)]">Sessioni</span>{' '}
            <span className="text-card-foreground">{gameTitle}</span>
          </SheetTitle>
          <div className="flex items-center gap-3 mt-1">
            <span className="text-xs text-muted-foreground font-nunito">
              {sessions.length} {sessions.length === 1 ? 'sessione' : 'sessioni'}
            </span>
            <div className="flex-1" />
            <Button
              variant="outline"
              size="sm"
              onClick={handleNewSession}
              className="h-7 gap-1.5 text-xs font-nunito"
            >
              <Plus className="h-3.5 w-3.5" />
              Nuova Sessione
            </Button>
          </div>
        </SheetHeader>

        <div className="mt-4 flex flex-col gap-3 overflow-y-auto max-h-[calc(100vh-180px)]">
          {/* Loading */}
          {loading && sessions.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 gap-2">
              <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
              <p className="text-sm text-muted-foreground font-nunito">Caricamento...</p>
            </div>
          )}

          {/* Error */}
          {error && (
            <div className="flex items-center gap-2 rounded-lg bg-red-50 p-3 text-sm text-red-700">
              Errore nel caricamento delle sessioni
            </div>
          )}

          {/* Empty state */}
          {!loading && !error && sessions.length === 0 && (
            <div className="flex flex-col items-center justify-center py-12 gap-3 text-center">
              <div className="rounded-full bg-muted/50 p-4">
                <Trophy className="h-8 w-8 text-muted-foreground" />
              </div>
              <div>
                <p className="font-quicksand font-semibold text-card-foreground">
                  Nessuna sessione
                </p>
                <p className="text-sm text-muted-foreground font-nunito mt-1">
                  Registra la tua prima partita a questo gioco
                </p>
              </div>
              <Button
                variant="outline"
                size="sm"
                onClick={handleNewSession}
                className="mt-2 gap-1.5"
              >
                <Plus className="h-4 w-4" />
                Nuova Sessione
              </Button>
            </div>
          )}

          {/* Session list */}
          {sessions.map(session => {
            const date = new Date(session.startedAt);
            const status = statusConfig[session.status as keyof typeof statusConfig] ?? {
              label: session.status,
              dot: 'bg-slate-300',
            };

            return (
              <button
                key={session.id}
                type="button"
                onClick={() => handleOpenSession(session.id)}
                className={cn(
                  'flex items-center gap-3 rounded-xl p-3 text-left',
                  'bg-[rgba(45,42,38,0.04)] hover:bg-[rgba(45,42,38,0.06)]',
                  'transition-colors cursor-pointer w-full'
                )}
              >
                <span className={cn('h-2.5 w-2.5 rounded-full flex-shrink-0', status.dot)} />
                <div className="flex-1 min-w-0">
                  <div className="flex items-center justify-between gap-2">
                    <p className="text-sm font-medium text-card-foreground font-nunito">
                      {date.toLocaleDateString('it-IT', {
                        day: '2-digit',
                        month: 'long',
                        year: 'numeric',
                      })}
                    </p>
                    <span
                      className={cn(
                        'rounded px-1.5 py-0.5 text-[10px] font-medium',
                        'bg-muted/50 text-muted-foreground'
                      )}
                    >
                      {status.label}
                    </span>
                  </div>
                  <div className="flex items-center gap-3 mt-1">
                    <span className="flex items-center gap-1 text-xs text-muted-foreground">
                      <Users className="h-3 w-3" />
                      {session.players?.length ?? 0}
                    </span>
                    {session.durationMinutes > 0 && (
                      <span className="flex items-center gap-1 text-xs text-muted-foreground">
                        <Clock className="h-3 w-3" />
                        {session.durationMinutes}min
                      </span>
                    )}
                  </div>
                </div>
              </button>
            );
          })}
        </div>
      </SheetContent>
    </Sheet>
  );
}
