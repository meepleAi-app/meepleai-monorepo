'use client';

/**
 * PlayerExtraMeepleCard — expanded card for Player entities
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 */

import React, { useState } from 'react';

import { Award, Clock, Gamepad2, Trophy, User, Users } from 'lucide-react';

import { Tabs, TabsList, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';

import {
  ENTITY_COLORS,
  EntityHeader,
  EntityTabTrigger,
  StatCard,
  EntityLoadingState,
  EntityErrorState,
} from '../shared';

import type { PlayerDetailData } from '../types';

// ============================================================================
// Types
// ============================================================================

export interface PlayerExtraMeepleCardProps {
  data: PlayerDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type PlayerTab = 'profile' | 'achievements' | 'history';

// ============================================================================
// PlayerExtraMeepleCard
// ============================================================================

export const PlayerExtraMeepleCard = React.memo(function PlayerExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: PlayerExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<PlayerTab>('profile');
  const colors = ENTITY_COLORS.player;

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error) return <EntityErrorState error={error} className={className} testId={testId} />;

  return (
    <div
      className={cn(
        'flex w-[600px] flex-col rounded-2xl overflow-hidden',
        'bg-white/70 backdrop-blur-md shadow-[0_8px_32px_rgba(0,0,0,0.12)] border border-white/20',
        'max-md:w-full',
        className
      )}
      data-testid={testId}
    >
      {/* Header */}
      <EntityHeader
        title={data.displayName}
        imageUrl={data.avatarUrl}
        color={colors.hsl}
        badge={`${Math.round(data.winRate * 100)}%`}
        badgeIcon={<Trophy className="h-3 w-3" />}
      />

      {/* Tabs */}
      <Tabs
        value={activeTab}
        onValueChange={v => setActiveTab(v as PlayerTab)}
        className="flex flex-1 flex-col"
      >
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger
            value="profile"
            icon={User}
            label="Profile"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="achievements"
            icon={Award}
            label="Achievements"
            activeAccent={colors.activeAccent}
          />
          <EntityTabTrigger
            value="history"
            icon={Clock}
            label="Recent"
            activeAccent={colors.activeAccent}
          />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="profile" className="mt-0">
            <div className="space-y-3">
              <div className="grid grid-cols-3 gap-2">
                <StatCard
                  label="Games Played"
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
                  label="Sessions"
                  value={data.totalSessions.toString()}
                  icon={Users}
                  variant="player"
                />
              </div>
              {data.favoriteGame && (
                <div className="rounded-lg bg-purple-50/50 border border-purple-200/40 p-3">
                  <p className="font-nunito text-[10px] text-purple-500 uppercase tracking-wider">
                    Favorite Game
                  </p>
                  <p className="font-quicksand text-sm font-bold text-purple-700">
                    {data.favoriteGame}
                  </p>
                </div>
              )}
            </div>
          </TabsContent>

          <TabsContent value="achievements" className="mt-0">
            <div className="space-y-2">
              {data.achievements.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  No achievements yet
                </p>
              ) : (
                data.achievements.map(a => (
                  <div
                    key={a.id}
                    className="flex items-center gap-2 rounded-lg bg-white/50 border border-slate-200/40 p-2.5"
                  >
                    <span className="text-lg">{a.icon}</span>
                    <span className="font-nunito text-xs font-medium text-slate-700">{a.name}</span>
                  </div>
                ))
              )}
            </div>
          </TabsContent>

          <TabsContent value="history" className="mt-0">
            <div className="space-y-2">
              {data.recentGames.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">
                  No recent games
                </p>
              ) : (
                data.recentGames.map(g => (
                  <div
                    key={`${g.name}-${g.date}`}
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
                      {g.result.charAt(0).toUpperCase() + g.result.slice(1)}
                    </span>
                  </div>
                ))
              )}
            </div>
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
});
