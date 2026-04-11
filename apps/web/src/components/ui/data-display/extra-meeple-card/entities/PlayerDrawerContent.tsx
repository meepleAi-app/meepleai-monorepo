'use client';

import React, { useState } from 'react';

import { BarChart2, Clock, ExternalLink, Gamepad2, Trophy, User, Users } from 'lucide-react';
import { useRouter } from 'next/navigation';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import { DrawerLoadingSkeleton, DrawerErrorState } from '../drawer-states';
import { DrawerActionFooter } from '../DrawerActionFooter';
import { usePlayerDetail } from '../hooks';
import { ENTITY_COLORS, EntityHeader, EntityTabTrigger, StatCard } from '../shared';

import type { DrawerAction } from '../DrawerActionFooter';

// ============================================================================
// PlayerDrawerContent — drawer-specific player detail view
// ============================================================================

interface PlayerDrawerContentProps {
  entityId: string;
}

type PlayerTab = 'profile' | 'stats' | 'history';

export function PlayerDrawerContent({ entityId }: PlayerDrawerContentProps) {
  const { data, loading, error, retry } = usePlayerDetail(entityId);
  const [activeTab, setActiveTab] = useState<PlayerTab>('profile');
  const router = useRouter();
  const colors = ENTITY_COLORS.player;

  if (loading) return <DrawerLoadingSkeleton />;
  if (error) return <DrawerErrorState error={error} onRetry={retry} />;
  if (!data) return <DrawerLoadingSkeleton />;

  const footerActions: DrawerAction[] = [
    {
      icon: BarChart2,
      label: 'Confronta',
      onClick: () => router.push(`/players/${entityId}/compare`),
      variant: 'secondary',
      enabled: data.gamesPlayed > 0,
    },
    {
      icon: ExternalLink,
      label: 'Apri',
      onClick: () => router.push(`/players/${entityId}`),
      variant: 'primary',
      enabled: true,
    },
  ];

  return (
    <div className="flex flex-1 flex-col">
      <EntityHeader
        title={data.displayName}
        imageUrl={data.avatarUrl}
        color={colors.hsl}
        badge={`${Math.round(data.winRate * 100)}%`}
        badgeIcon={<Trophy className="h-3 w-3" />}
      />

      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as PlayerTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="profile"
            icon={User}
            label="Profilo"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="stats"
            icon={Trophy}
            label="Stats"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="history"
            icon={Clock}
            label="Storico"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="profile" className="mt-0">
            <div className="space-y-3">
              <div className="grid grid-cols-3 gap-2">
                <StatCard
                  label="Partite"
                  value={data.gamesPlayed.toString()}
                  icon={Gamepad2}
                  variant="player"
                />
                <StatCard
                  label="Win Rate"
                  value={`${Math.round(data.winRate * 100)}%`}
                  icon={Trophy}
                  variant="player"
                />
                <StatCard
                  label="Sessioni"
                  value={data.totalSessions.toString()}
                  icon={Users}
                  variant="player"
                />
              </div>
              {data.favoriteGame && (
                <div className="rounded-lg bg-purple-50/50 border border-purple-200/40 p-3">
                  <p className="font-nunito text-[10px] text-purple-500 uppercase tracking-wider">
                    Gioco Preferito
                  </p>
                  <p className="font-quicksand text-sm font-bold text-purple-700">
                    {data.favoriteGame}
                  </p>
                </div>
              )}
            </div>
          </TabsContent>

          <TabsContent value="stats" className="mt-0">
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-2">
                <StatCard
                  label="Partite Giocate"
                  value={data.gamesPlayed.toString()}
                  icon={Gamepad2}
                  variant="player"
                />
                <StatCard
                  label="Vittorie"
                  value={`${Math.round(data.winRate * 100)}%`}
                  icon={Trophy}
                  variant="player"
                />
              </div>
              <StatCard
                label="Sessioni Totali"
                value={data.totalSessions.toString()}
                icon={Users}
                variant="player"
              />
            </div>
          </TabsContent>

          <TabsContent value="history" className="mt-0">
            <div className="space-y-2">
              {data.recentGames.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  Nessuna partita recente
                </p>
              ) : (
                data.recentGames.map((g, idx) => (
                  <div
                    key={idx}
                    className="flex items-center justify-between rounded-lg bg-white/50 border border-slate-200/40 p-2.5"
                  >
                    <div>
                      <p className="font-nunito text-xs font-medium text-slate-700">{g.name}</p>
                      <p className="font-nunito text-[10px] text-slate-400">{g.date}</p>
                    </div>
                    <span
                      className={cn(
                        'rounded-full px-2 py-0.5 text-[10px] font-bold font-nunito',
                        g.result === 'win'
                          ? 'bg-green-100 text-green-700'
                          : g.result === 'loss'
                            ? 'bg-red-100 text-red-700'
                            : 'bg-slate-100 text-slate-600'
                      )}
                    >
                      {g.result === 'win' ? 'Vinto' : g.result === 'loss' ? 'Perso' : 'Pareggio'}
                    </span>
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
