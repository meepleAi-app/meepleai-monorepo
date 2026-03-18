/**
 * Sessions Hub Content
 *
 * Issue #5041 — Sessions Redesign Phase 1
 *
 * Renders Active or History tab based on ?tab= search param.
 * Active: banner for in-progress session + "Nuova Sessione" CTA + recent sessions.
 * History: paginated list of past sessions.
 */

'use client';

import { useCallback, useEffect, useState } from 'react';

import { formatDistanceToNow, format } from 'date-fns';
import { it } from 'date-fns/locale';
import { Clock, Crown, History, Loader2, Play, Search, Users } from 'lucide-react';
import Link from 'next/link';
import { useRouter, useSearchParams } from 'next/navigation';

import { MeepleResumeSessionCard } from '@/components/session/MeepleResumeSessionCard';
import { Badge } from '@/components/ui/badge';
import { Button } from '@/components/ui/button';
import { Input } from '@/components/ui/input';
import { api } from '@/lib/api';
import type { LiveSessionSummaryDto } from '@/lib/api/schemas/live-sessions.schemas';
import type { SessionSummaryDto } from '@/lib/api/schemas/session-tracking.schemas';
import { useCardHand } from '@/stores/use-card-hand';

// ========== Status Helpers ==========

const STATUS_CONFIG: Record<string, { label: string; color: string }> = {
  Created: { label: 'Creata', color: 'bg-slate-100 text-slate-700' },
  Setup: { label: 'Setup', color: 'bg-blue-100 text-blue-700' },
  InProgress: { label: 'In corso', color: 'bg-green-100 text-green-700' },
  Paused: { label: 'In pausa', color: 'bg-amber-100 text-amber-700' },
  Completed: { label: 'Completata', color: 'bg-indigo-100 text-indigo-700' },
};

function StatusBadge({ status }: { status: string }) {
  const config = STATUS_CONFIG[status] ?? {
    label: status,
    color: 'bg-muted text-muted-foreground',
  };
  return (
    <Badge variant="outline" className={`${config.color} border-0 text-xs font-medium`}>
      {status === 'InProgress' && (
        <span className="mr-1 h-1.5 w-1.5 rounded-full bg-green-500 animate-pulse inline-block" />
      )}
      {config.label}
    </Badge>
  );
}

// ========== Active Session Banner ==========

function ActiveSessionBanner({ session }: { session: LiveSessionSummaryDto }) {
  const timeAgo = formatDistanceToNow(new Date(session.updatedAt), {
    addSuffix: false,
    locale: it,
  });

  return (
    <Link
      href={`/sessions/${session.id}`}
      className="block rounded-xl border border-green-200 bg-gradient-to-r from-green-50 to-emerald-50 p-4 transition-shadow hover:shadow-md dark:from-green-950/20 dark:to-emerald-950/20 dark:border-green-800"
    >
      <div className="flex items-center justify-between">
        <div className="flex items-center gap-3 min-w-0">
          <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-green-100 dark:bg-green-900/40">
            <Play className="h-5 w-5 text-green-600" />
          </div>
          <div className="min-w-0">
            <div className="flex items-center gap-2">
              <StatusBadge status={session.status} />
              <span className="text-xs text-muted-foreground">{timeAgo}</span>
            </div>
            <h3 className="font-semibold font-quicksand text-foreground truncate">
              {session.gameName}
            </h3>
            <div className="flex items-center gap-2 text-xs text-muted-foreground">
              <Users className="h-3 w-3" />
              <span>{session.playerCount} giocatori</span>
              <span>·</span>
              <span>Turno {session.currentTurnIndex + 1}</span>
            </div>
          </div>
        </div>
        <Button size="sm" className="shrink-0">
          Riprendi
        </Button>
      </div>
    </Link>
  );
}

// ========== Session Card (Recent / History) ==========

function SessionCard({ session }: { session: SessionSummaryDto }) {
  const dateStr = format(new Date(session.sessionDate), 'd MMM yyyy', { locale: it });

  return (
    <Link
      href={`/sessions/${session.id}`}
      className="block rounded-xl border border-border bg-card p-4 transition-shadow hover:shadow-md"
    >
      <div className="flex items-start justify-between gap-3">
        <div className="min-w-0 flex-1">
          <div className="flex items-center gap-2 mb-1">
            {session.gameName && (
              <Badge
                variant="outline"
                className="bg-orange-50 text-orange-700 border-0 text-xs dark:bg-orange-900/20 dark:text-orange-300"
              >
                {session.gameName}
              </Badge>
            )}
            <StatusBadge status={session.status} />
          </div>
          <div className="flex items-center gap-2 text-xs text-muted-foreground mt-2">
            <Clock className="h-3 w-3" />
            <span>{dateStr}</span>
            {session.durationMinutes > 0 && (
              <>
                <span>·</span>
                <span>{session.durationMinutes} min</span>
              </>
            )}
          </div>
          <div className="flex items-center gap-2 text-xs text-muted-foreground mt-1">
            <Users className="h-3 w-3" />
            <span>{session.participantsNames}</span>
          </div>
        </div>
        {session.winnerName && (
          <div className="flex items-center gap-1 rounded-lg bg-amber-50 px-2 py-1 dark:bg-amber-900/20">
            <Crown className="h-3 w-3 text-amber-600" />
            <span className="text-xs font-medium text-amber-700 dark:text-amber-300">
              {session.winnerName}
            </span>
          </div>
        )}
      </div>
    </Link>
  );
}

// ========== New Session CTA ==========

function NewSessionCta() {
  const router = useRouter();

  return (
    <button
      onClick={() => router.push('/sessions/new')}
      className="w-full rounded-xl border-2 border-dashed border-indigo-200 bg-gradient-to-br from-indigo-50 to-violet-50 p-6 text-center transition-all hover:border-indigo-300 hover:shadow-md dark:from-indigo-950/20 dark:to-violet-950/20 dark:border-indigo-800"
    >
      <div className="mx-auto flex h-12 w-12 items-center justify-center rounded-full bg-indigo-100 dark:bg-indigo-900/40 mb-3">
        <Play className="h-6 w-6 text-indigo-600" />
      </div>
      <h3 className="font-semibold font-quicksand text-foreground">Nuova Sessione</h3>
      <p className="mt-1 text-xs text-muted-foreground">Inizia una partita con amici</p>
    </button>
  );
}

// ========== Main Content ==========

export function SessionsContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab') ?? 'active';
  const { drawCard } = useCardHand();

  useEffect(() => {
    drawCard({
      id: 'section-sessions',
      entity: 'session',
      title: 'Sessions',
      href: '/sessions',
    });
  }, [drawCard]);

  const [activeSessions, setActiveSessions] = useState<LiveSessionSummaryDto[]>([]);
  const [historySessions, setHistorySessions] = useState<SessionSummaryDto[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [searchQuery, setSearchQuery] = useState('');

  const loadData = useCallback(async () => {
    setIsLoading(true);
    try {
      if (tab === 'active') {
        const sessions = await api.liveSessions.getActive();
        setActiveSessions(sessions);
      } else {
        const sessions = await api.sessionTracking.getHistory({ limit: 50 });
        setHistorySessions(sessions);
      }
    } catch {
      // Silently handle — empty state will show
    } finally {
      setIsLoading(false);
    }
  }, [tab]);

  useEffect(() => {
    loadData();
  }, [loadData]);

  // Split active sessions by status for differentiated rendering
  const inProgressSessions = activeSessions.filter(s => s.status === 'InProgress');
  const pausedSessions = activeSessions.filter(s => s.status === 'Paused');

  // Filter history by search
  const filteredHistory = searchQuery
    ? historySessions.filter(
        s =>
          s.gameName?.toLowerCase().includes(searchQuery.toLowerCase()) ||
          s.participantsNames?.toLowerCase().includes(searchQuery.toLowerCase()) ||
          s.winnerName?.toLowerCase().includes(searchQuery.toLowerCase())
      )
    : historySessions;

  if (isLoading) {
    return (
      <div className="flex items-center justify-center py-20">
        <Loader2 className="h-6 w-6 animate-spin text-muted-foreground" />
      </div>
    );
  }

  return (
    <div className="space-y-6 container mx-auto px-4 py-6">
      {/* Header */}
      <div className="flex items-center gap-3">
        {tab === 'history' ? (
          <History className="h-6 w-6 text-primary" />
        ) : (
          <Clock className="h-6 w-6 text-primary" />
        )}
        <div>
          <h1 className="text-2xl font-bold font-quicksand text-foreground">
            {tab === 'history' ? 'Storico Sessioni' : 'Sessioni Attive'}
          </h1>
          <p className="text-sm text-muted-foreground">
            {tab === 'history'
              ? 'Revisiona le tue sessioni di gioco passate'
              : 'Gestisci le sessioni di gioco in corso'}
          </p>
        </div>
      </div>

      {/* Active Tab */}
      {tab === 'active' && (
        <div className="space-y-4">
          {/* In-progress sessions — green banner */}
          {inProgressSessions.map(session => (
            <ActiveSessionBanner key={session.id} session={session} />
          ))}

          {/* Paused sessions — amber ResumeSessionCard */}
          {pausedSessions.length > 0 && (
            <div>
              <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
                Partite in pausa
              </h2>
              <div className="space-y-3">
                {pausedSessions.map(session => (
                  <MeepleResumeSessionCard
                    key={session.id}
                    sessionId={session.id}
                    gameName={session.gameName}
                    lastActivityAt={session.updatedAt}
                    playerCount={session.playerCount}
                    sessionCode={session.sessionCode}
                  />
                ))}
              </div>
            </div>
          )}

          {/* New Session CTA */}
          <NewSessionCta />

          {/* All active sessions */}
          {activeSessions.length > 0 && (
            <div>
              <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
                Sessioni recenti
              </h2>
              <div className="space-y-3">
                {activeSessions
                  .filter(s => s.status !== 'InProgress' && s.status !== 'Paused')
                  .map(session => (
                    <Link
                      key={session.id}
                      href={`/sessions/${session.id}`}
                      className="block rounded-xl border border-border bg-card p-4 transition-shadow hover:shadow-md"
                    >
                      <div className="flex items-center justify-between">
                        <div className="min-w-0">
                          <div className="flex items-center gap-2 mb-1">
                            <Badge
                              variant="outline"
                              className="bg-orange-50 text-orange-700 border-0 text-xs dark:bg-orange-900/20 dark:text-orange-300"
                            >
                              {session.gameName}
                            </Badge>
                            <StatusBadge status={session.status} />
                          </div>
                          <div className="flex items-center gap-2 text-xs text-muted-foreground mt-1">
                            <Users className="h-3 w-3" />
                            <span>{session.playerCount} giocatori</span>
                            <span>·</span>
                            <span>{session.sessionCode}</span>
                          </div>
                        </div>
                      </div>
                    </Link>
                  ))}
              </div>
            </div>
          )}

          {/* Empty state */}
          {activeSessions.length === 0 && (
            <div className="rounded-xl border border-border bg-card p-8 text-center">
              <Clock className="mx-auto h-10 w-10 text-muted-foreground/40 mb-3" />
              <h3 className="font-semibold font-quicksand text-foreground">
                Nessuna sessione attiva
              </h3>
              <p className="text-sm text-muted-foreground mt-1">
                Crea una nuova sessione per iniziare a giocare
              </p>
            </div>
          )}
        </div>
      )}

      {/* History Tab */}
      {tab === 'history' && (
        <div className="space-y-4">
          {/* Search */}
          <div className="relative">
            <Search className="absolute left-3 top-1/2 h-4 w-4 -translate-y-1/2 text-muted-foreground" />
            <Input
              placeholder="Cerca per gioco, giocatore..."
              value={searchQuery}
              onChange={e => setSearchQuery(e.target.value)}
              className="pl-9"
            />
          </div>

          {/* Session list */}
          {filteredHistory.length > 0 ? (
            <div className="space-y-3">
              {filteredHistory.map(session => (
                <SessionCard key={session.id} session={session} />
              ))}
            </div>
          ) : (
            <div className="rounded-xl border border-border bg-card p-8 text-center">
              <History className="mx-auto h-10 w-10 text-muted-foreground/40 mb-3" />
              <h3 className="font-semibold font-quicksand text-foreground">
                {searchQuery ? 'Nessun risultato' : 'Nessuna sessione nello storico'}
              </h3>
              <p className="text-sm text-muted-foreground mt-1">
                {searchQuery
                  ? 'Prova a cercare con termini diversi'
                  : 'Le sessioni completate appariranno qui'}
              </p>
            </div>
          )}
        </div>
      )}
    </div>
  );
}
