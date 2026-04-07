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
import { Clock, History, Loader2, Search } from 'lucide-react';
import { useRouter, useSearchParams } from 'next/navigation';

import { FloatingActionPill } from '@/components/layout/FloatingActionPill';
import { MeepleResumeSessionCard } from '@/components/session/MeepleResumeSessionCard';
import { MeepleCard, type MeepleCardMetadata } from '@/components/ui/data-display/meeple-card';
import { Input } from '@/components/ui/primitives/input';
import { useResponsive } from '@/hooks/useResponsive';
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

// ========== History Metadata Helper ==========

function buildHistoryMeta(session: SessionSummaryDto): MeepleCardMetadata[] {
  const meta: MeepleCardMetadata[] = [
    { label: format(new Date(session.sessionDate), 'd MMM yyyy', { locale: it }) },
  ];
  if (session.durationMinutes > 0) {
    meta.push({ label: `${session.durationMinutes} min` });
  }
  if (session.winnerName) {
    meta.push({ label: session.winnerName });
  }
  return meta;
}

// ========== Main Content ==========

export function SessionsContent() {
  const searchParams = useSearchParams();
  const tab = searchParams.get('tab') ?? 'active';
  const router = useRouter();
  const { drawCard } = useCardHand();
  const { isMobile } = useResponsive();

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
    <div className="space-y-6 container mx-auto px-4 py-6 pb-24">
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
          {/* In-progress sessions — MeepleCard featured/hero */}
          {inProgressSessions.map(session => (
            <MeepleCard
              key={session.id}
              entity="session"
              variant={isMobile ? 'hero' : 'featured'}
              title={session.gameName}
              subtitle={`Turno ${session.currentTurnIndex + 1}`}
              badge={STATUS_CONFIG[session.status]?.label ?? session.status}
              metadata={[
                { label: `${session.playerCount} giocatori` },
                {
                  label: formatDistanceToNow(new Date(session.updatedAt), {
                    addSuffix: false,
                    locale: it,
                  }),
                },
              ]}
              actions={[
                { icon: '▶', label: 'Riprendi', onClick: () => router.push(`/sessions/${session.id}`) },
              ]}
              onClick={() => router.push(`/sessions/${session.id}`)}
            />
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

          {/* Other active sessions (Setup, Created, etc.) */}
          {activeSessions.filter(s => s.status !== 'InProgress' && s.status !== 'Paused').length >
            0 && (
            <div>
              <h2 className="text-sm font-semibold text-muted-foreground uppercase tracking-wide mb-3">
                Sessioni recenti
              </h2>
              {isMobile ? (
                <div className="flex flex-col gap-2">
                  {activeSessions
                    .filter(s => s.status !== 'InProgress' && s.status !== 'Paused')
                    .map(session => (
                      <MeepleCard
                        key={session.id}
                        entity="session"
                        variant="list"
                        title={session.gameName}
                        badge={STATUS_CONFIG[session.status]?.label ?? session.status}
                        metadata={[{ label: `${session.playerCount} giocatori` }]}
                        onClick={() => router.push(`/sessions/${session.id}`)}
                      />
                    ))}
                </div>
              ) : (
                <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                  {activeSessions
                    .filter(s => s.status !== 'InProgress' && s.status !== 'Paused')
                    .map(session => (
                      <MeepleCard
                        key={session.id}
                        entity="session"
                        variant="grid"
                        title={session.gameName}
                        badge={STATUS_CONFIG[session.status]?.label ?? session.status}
                        metadata={[{ label: `${session.playerCount} giocatori` }]}
                        onClick={() => router.push(`/sessions/${session.id}`)}
                      />
                    ))}
                </div>
              )}
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

          {/* Session list — grid on desktop, list on mobile */}
          {filteredHistory.length > 0 ? (
            isMobile ? (
              <div className="flex flex-col gap-2">
                {filteredHistory.map(session => (
                  <MeepleCard
                    key={session.id}
                    entity="session"
                    variant="list"
                    title={session.gameName || 'Sessione'}
                    subtitle={session.participantsNames}
                    badge={STATUS_CONFIG[session.status]?.label ?? session.status}
                    metadata={buildHistoryMeta(session)}
                    onClick={() => router.push(`/sessions/${session.id}`)}
                  />
                ))}
              </div>
            ) : (
              <div className="grid grid-cols-2 md:grid-cols-3 lg:grid-cols-4 gap-3">
                {filteredHistory.map(session => (
                  <MeepleCard
                    key={session.id}
                    entity="session"
                    variant="grid"
                    title={session.gameName || 'Sessione'}
                    subtitle={session.participantsNames}
                    badge={STATUS_CONFIG[session.status]?.label ?? session.status}
                    metadata={buildHistoryMeta(session)}
                    onClick={() => router.push(`/sessions/${session.id}`)}
                  />
                ))}
              </div>
            )
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

      <FloatingActionPill page="sessions" />
    </div>
  );
}
