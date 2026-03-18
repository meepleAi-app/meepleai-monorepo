'use client';

/**
 * PlayPanel — Play tab panel for AlphaShell.
 *
 * Internal tabs:
 * - Sessioni Attive: sessions with status setup/inProgress/paused
 * - Storico: completed session history
 *
 * Includes a FAB for creating new sessions.
 */

import { useQuery } from '@tanstack/react-query';
import { Gamepad2, History, Plus } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { EmptyStateCard, SkeletonCardGrid } from '@/components/features/common';
import { MeepleCard, entityColors } from '@/components/ui/data-display/meeple-card';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/navigation/tabs';
import { useActiveSessions, sessionsKeys } from '@/hooks/queries/useActiveSessions';
import { useAlphaNav } from '@/hooks/useAlphaNav';
import { api } from '@/lib/api';
import type { PaginatedSessionsResponse } from '@/lib/api/schemas';

/**
 * Hook to fetch session history (completed sessions).
 * Uses the sessionsKeys factory for cache consistency.
 */
function useSessionHistory(limit: number = 20) {
  return useQuery({
    queryKey: sessionsKeys.history({ limit }),
    queryFn: async (): Promise<PaginatedSessionsResponse> => {
      return api.sessions.getHistory({ limit });
    },
    staleTime: 60_000,
  });
}

export function PlayPanel() {
  const router = useRouter();
  const { openDetail } = useAlphaNav();

  const { data: activeSessions, isLoading: activeLoading } = useActiveSessions(20);
  const { data: historySessions, isLoading: historyLoading } = useSessionHistory(20);

  const active = activeSessions?.sessions ?? [];
  const history = historySessions?.sessions ?? [];

  const getStatusLabel = (status: string) => {
    switch (status) {
      case 'Setup':
        return 'Preparazione';
      case 'InProgress':
        return 'In Corso';
      case 'Paused':
        return 'In Pausa';
      case 'Completed':
        return 'Completata';
      default:
        return status;
    }
  };

  return (
    <div className="p-4 sm:p-6 relative">
      <Tabs defaultValue="active" className="w-full">
        <TabsList className="w-full grid grid-cols-2">
          <TabsTrigger value="active">Sessioni Attive</TabsTrigger>
          <TabsTrigger value="history">Storico</TabsTrigger>
        </TabsList>

        {/* Sessioni Attive */}
        <TabsContent value="active">
          {activeLoading ? (
            <SkeletonCardGrid count={3} />
          ) : active.length === 0 ? (
            <EmptyStateCard
              title="Nessuna sessione attiva"
              description="Inizia una nuova sessione di gioco per tracciare punteggi e partite."
              ctaLabel="Nuova Sessione"
              onCtaClick={() => router.push('/sessions/new')}
              icon={Gamepad2}
              entityColor={entityColors.session.hsl}
            />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6 mt-4">
              {active.map(session => (
                <MeepleCard
                  key={session.id}
                  id={session.id}
                  entity="session"
                  variant="list"
                  title={session.notes || `Sessione ${session.id.slice(0, 8)}`}
                  subtitle={`${session.playerCount} giocatori`}
                  badge={getStatusLabel(session.status)}
                  metadata={[
                    {
                      icon: Gamepad2,
                      label: `${session.durationMinutes} min`,
                    },
                  ]}
                  onClick={() => {
                    if (session.status === 'Setup' || session.status === 'Paused') {
                      router.push(`/sessions/live/${session.id}`);
                    } else {
                      openDetail(session.id, 'session');
                    }
                  }}
                />
              ))}
            </div>
          )}
        </TabsContent>

        {/* Storico */}
        <TabsContent value="history">
          {historyLoading ? (
            <SkeletonCardGrid count={4} />
          ) : history.length === 0 ? (
            <EmptyStateCard
              title="Nessuna partita completata"
              description="Completa le tue sessioni di gioco per vederle nello storico."
              ctaLabel="Vai alle Sessioni"
              onCtaClick={() => {
                const tabTrigger = document.querySelector<HTMLButtonElement>(
                  '[data-state][value="active"]'
                );
                tabTrigger?.click();
              }}
              icon={History}
              entityColor={entityColors.session.hsl}
            />
          ) : (
            <div className="grid grid-cols-1 sm:grid-cols-2 gap-6 mt-4">
              {history.map(session => (
                <MeepleCard
                  key={session.id}
                  id={session.id}
                  entity="session"
                  variant="list"
                  title={session.notes || `Sessione ${session.id.slice(0, 8)}`}
                  subtitle={
                    session.winnerName
                      ? `Vincitore: ${session.winnerName}`
                      : `${session.playerCount} giocatori`
                  }
                  badge={getStatusLabel(session.status)}
                  metadata={[
                    {
                      icon: Gamepad2,
                      label: `${session.durationMinutes} min`,
                    },
                  ]}
                  onClick={() => openDetail(session.id, 'session')}
                />
              ))}
            </div>
          )}
        </TabsContent>
      </Tabs>

      {/* FAB: Nuova Sessione */}
      <button
        onClick={() => router.push('/sessions/new')}
        className="fixed bottom-20 right-4 lg:bottom-6 lg:right-6 z-40
                   w-14 h-14 rounded-full flex items-center justify-center
                   text-white shadow-lg hover:shadow-xl
                   transition-all duration-200 hover:scale-105 active:scale-95"
        style={{ backgroundColor: `hsl(${entityColors.session.hsl})` }}
        aria-label="Nuova Sessione"
      >
        <Plus className="w-6 h-6" />
      </button>
    </div>
  );
}
