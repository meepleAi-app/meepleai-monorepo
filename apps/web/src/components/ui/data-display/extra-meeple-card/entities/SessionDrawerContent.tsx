'use client';

import React, { useState } from 'react';

import { BarChart2, Clock, ExternalLink, Layers, Play, Wrench } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
import { useCascadeNavigationStore } from '@/lib/stores/cascade-navigation-store';

import { DrawerLoadingSkeleton, DrawerErrorState } from '../drawer-states';
import { DrawerActionFooter } from '../DrawerActionFooter';
import { useSessionDetail } from '../hooks';
import { ENTITY_COLORS, EntityHeader, EntityTabTrigger } from '../shared';

import type { DrawerAction } from '../DrawerActionFooter';

// ============================================================================
// SessionDrawerContent — drawer-specific session detail view
// ============================================================================

interface SessionDrawerContentProps {
  entityId: string;
  initialTabId?: string;
}

type SessionTab = 'live' | 'toolkit' | 'timeline';

export function SessionDrawerContent({ entityId, initialTabId }: SessionDrawerContentProps) {
  const { data, loading, error, retry } = useSessionDetail(entityId);
  const validTabs: SessionTab[] = ['live', 'toolkit', 'timeline'];
  const [activeTab, setActiveTab] = useState<SessionTab>(
    validTabs.includes(initialTabId as SessionTab) ? (initialTabId as SessionTab) : 'live'
  );
  const router = useRouter();
  const colors = ENTITY_COLORS.session;
  const pushDrawer = useCascadeNavigationStore(s => s.pushDrawer);

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  const isInProgress = data.status === 'InProgress' || data.status === 'inProgress';
  const isPaused = data.status === 'Paused' || data.status === 'paused';
  const isCompleted = data.status === 'Completed' || data.status === 'completed';

  const statusLabel = isInProgress
    ? 'In corso'
    : isPaused
      ? 'In pausa'
      : isCompleted
        ? 'Completata'
        : 'Setup';

  const footerActions: DrawerAction[] = [
    ...(isInProgress || isPaused
      ? [
          {
            icon: Play,
            label: 'Riprendi',
            onClick: () => router.push(`/sessions/live/${entityId}`),
            variant: 'primary' as const,
            enabled: true,
          },
        ]
      : []),
    ...(isCompleted
      ? [
          {
            icon: BarChart2,
            label: 'Risultati',
            onClick: () => router.push(`/sessions/${entityId}/results`),
            variant: 'secondary' as const,
            enabled: true,
          },
        ]
      : []),
    {
      icon: ExternalLink,
      label: 'Apri',
      onClick: () => router.push(`/sessions/${entityId}`),
      variant: 'primary' as const,
      enabled: true,
    },
  ];

  const hasToolkit = data.toolkit != null;

  return (
    <div className="flex flex-1 flex-col">
      <EntityHeader
        title={data.title}
        imageUrl={data.gameImageUrl}
        color={colors.hsl}
        badge={statusLabel}
        badgeIcon={<Layers className="h-3 w-3" />}
      />

      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as SessionTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="live"
            icon={Play}
            label="Live"
            activeAccent={colors.activeAccent}
          />
          {hasToolkit && (
            <EntityTabTrigger
              value="toolkit"
              icon={Wrench}
              label="Toolkit"
              activeAccent={colors.activeAccent}
            />
          )}
          <EntityTabTrigger
            value="timeline"
            icon={Clock}
            label="Timeline"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          {/* Live tab — player list with scores */}
          <TabsContent value="live" className="mt-0">
            <div className="space-y-2">
              {data.players.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  Nessun giocatore
                </p>
              ) : (
                data.players.map(player => (
                  <div
                    key={player.id}
                    role="button"
                    tabIndex={0}
                    onClick={() => pushDrawer('player', player.id)}
                    onKeyDown={e => {
                      if (e.key === 'Enter' || e.key === ' ') pushDrawer('player', player.id);
                    }}
                    className="flex items-center gap-3 rounded-lg bg-white/50 border border-slate-200/40 p-2.5 cursor-pointer hover:bg-white/80 transition-colors"
                  >
                    {/* Avatar circle */}
                    <div
                      className="h-8 w-8 shrink-0 rounded-full flex items-center justify-center text-white text-xs font-bold font-quicksand"
                      style={{ backgroundColor: player.color }}
                      aria-hidden="true"
                    >
                      {player.displayName.charAt(0).toUpperCase()}
                    </div>

                    {/* Name */}
                    <span className="flex-1 font-nunito text-sm font-medium text-slate-700">
                      {player.displayName}
                    </span>

                    {/* Score */}
                    {player.totalScore != null && (
                      <span className="font-quicksand text-sm font-bold text-indigo-700">
                        {player.totalScore}
                      </span>
                    )}

                    {/* Active indicator */}
                    {player.isActive && (
                      <span
                        className="h-2.5 w-2.5 rounded-full bg-green-500 shrink-0"
                        aria-label="Attivo"
                      />
                    )}
                  </div>
                ))
              )}
            </div>
          </TabsContent>

          {/* Toolkit tab */}
          {hasToolkit && (
            <TabsContent value="toolkit" className="mt-0">
              <div className="space-y-3">
                <div className="rounded-lg bg-indigo-50/50 border border-indigo-200/40 p-3">
                  <p className="font-nunito text-[10px] text-indigo-500 uppercase tracking-wider">
                    Toolkit
                  </p>
                  <p className="font-quicksand text-sm font-bold text-indigo-700">
                    {data.toolkit!.name}
                  </p>
                </div>
                <div className="grid grid-cols-3 gap-2">
                  <div className="rounded-lg bg-white/50 border border-slate-200/40 p-2.5 text-center">
                    <p className="font-quicksand text-lg font-bold text-indigo-700">
                      {data.toolkit!.diceTools.length}
                    </p>
                    <p className="font-nunito text-[10px] text-slate-500">Dadi</p>
                  </div>
                  <div className="rounded-lg bg-white/50 border border-slate-200/40 p-2.5 text-center">
                    <p className="font-quicksand text-lg font-bold text-indigo-700">
                      {data.toolkit!.cardTools.length}
                    </p>
                    <p className="font-nunito text-[10px] text-slate-500">Carte</p>
                  </div>
                  <div className="rounded-lg bg-white/50 border border-slate-200/40 p-2.5 text-center">
                    <p className="font-quicksand text-lg font-bold text-indigo-700">
                      {data.toolkit!.timerTools.length}
                    </p>
                    <p className="font-nunito text-[10px] text-slate-500">Timer</p>
                  </div>
                </div>
              </div>
            </TabsContent>
          )}

          {/* Timeline tab */}
          <TabsContent value="timeline" className="mt-0">
            <div className="space-y-2">
              {data.timeline.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  Nessun evento registrato
                </p>
              ) : (
                data.timeline.map(event => (
                  <div
                    key={event.id}
                    className="flex items-start gap-2.5 rounded-lg bg-white/50 border border-slate-200/40 p-2.5"
                  >
                    <div className="flex-1 min-w-0">
                      <p className="font-nunito text-xs font-medium text-slate-700 truncate">
                        {event.label ?? event.description}
                      </p>
                      <p className="font-nunito text-[10px] text-slate-400">{event.timestamp}</p>
                    </div>
                  </div>
                ))
              )}
            </div>
          </TabsContent>
        </div>
      </Tabs>

      <DrawerActionFooter actions={footerActions} />
    </div>
  );
}
