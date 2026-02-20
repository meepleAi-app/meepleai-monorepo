'use client';

/**
 * Entity Variant ExtraMeepleCards
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 *
 * Expanded card variants for Game, Player, and Collection entities.
 * Each uses entity-specific color schemes from MeepleCard v2 tokens.
 */

import React, { useState } from 'react';
import {
  Award,
  BookOpen,
  Clock,
  Gamepad2,
  HelpCircle,
  Library,
  Loader2,
  AlertCircle,
  Share2,
  Star,
  Trophy,
  User,
  Users,
} from 'lucide-react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';
import type {
  GameDetailData,
  PlayerDetailData,
  CollectionDetailData,
} from './types';

// ============================================================================
// Entity Color Constants (from MeepleCard v2 tokens)
// ============================================================================

const ENTITY_COLORS = {
  game: { hsl: '25 95% 45%', accent: 'text-orange-700', accentBg: 'bg-orange-100', accentBorder: 'border-orange-200/60', activeAccent: 'data-[state=active]:text-orange-700' },
  player: { hsl: '262 83% 58%', accent: 'text-purple-700', accentBg: 'bg-purple-100', accentBorder: 'border-purple-200/60', activeAccent: 'data-[state=active]:text-purple-700' },
  collection: { hsl: '174 60% 40%', accent: 'text-teal-700', accentBg: 'bg-teal-100', accentBorder: 'border-teal-200/60', activeAccent: 'data-[state=active]:text-teal-700' },
} as const;

type EntityVariant = keyof typeof ENTITY_COLORS;

// ============================================================================
// GameExtraMeepleCard
// ============================================================================

export interface GameExtraMeepleCardProps {
  data: GameDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type GameTab = 'details' | 'rules' | 'stats';

export const GameExtraMeepleCard = React.memo(function GameExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: GameExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<GameTab>('details');
  const colors = ENTITY_COLORS.game;

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
        title={data.title}
        subtitle={data.publisher ? `${data.publisher}${data.yearPublished ? ` (${data.yearPublished})` : ''}` : undefined}
        imageUrl={data.imageUrl}
        color={colors.hsl}
        badge={data.averageRating ? `${data.averageRating.toFixed(1)}` : undefined}
        badgeIcon={<Star className="h-3 w-3" />}
      />

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as GameTab)} className="flex flex-1 flex-col">
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger value="details" icon={Gamepad2} label="Details" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="rules" icon={BookOpen} label="Rules & FAQs" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="stats" icon={Trophy} label="Stats" activeAccent={colors.activeAccent} />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="details" className="mt-0">
            <div className="space-y-3">
              {/* Game info grid */}
              <div className="grid grid-cols-3 gap-2">
                {data.minPlayers != null && (
                  <StatCard label="Players" value={`${data.minPlayers}-${data.maxPlayers ?? data.minPlayers}`} icon={Users} variant="game" />
                )}
                {data.playTimeMinutes != null && (
                  <StatCard label="Play Time" value={`${data.playTimeMinutes}m`} icon={Clock} variant="game" />
                )}
                {data.totalPlays != null && (
                  <StatCard label="Total Plays" value={data.totalPlays.toString()} icon={Gamepad2} variant="game" />
                )}
              </div>
              {data.description && (
                <p className="font-nunito text-xs text-slate-600 leading-relaxed">{data.description}</p>
              )}
            </div>
          </TabsContent>

          <TabsContent value="rules" className="mt-0">
            <div className="space-y-3">
              <StatCard label="Documents" value={(data.rulesDocumentCount ?? 0).toString()} icon={BookOpen} variant="game" />
              <StatCard label="FAQs" value={(data.faqCount ?? 0).toString()} icon={HelpCircle} variant="game" />
              <p className="font-nunito text-xs text-slate-400 text-center py-4">
                Rules and FAQs will be displayed here
              </p>
            </div>
          </TabsContent>

          <TabsContent value="stats" className="mt-0">
            <div className="space-y-3">
              {data.averageRating != null && (
                <div className="flex items-center gap-2 rounded-lg bg-orange-50/50 border border-orange-200/40 p-3">
                  <Star className="h-5 w-5 text-orange-500" />
                  <div>
                    <p className="font-quicksand text-lg font-bold text-orange-700">{data.averageRating.toFixed(1)}</p>
                    <p className="font-nunito text-[10px] text-orange-500">Average Rating</p>
                  </div>
                </div>
              )}
              {data.totalPlays != null && (
                <StatCard label="Total Plays" value={data.totalPlays.toString()} icon={Gamepad2} variant="game" />
              )}
            </div>
          </TabsContent>
        </div>
      </Tabs>
    </div>
  );
});

// ============================================================================
// PlayerExtraMeepleCard
// ============================================================================

export interface PlayerExtraMeepleCardProps {
  data: PlayerDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type PlayerTab = 'profile' | 'achievements' | 'history';

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
      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as PlayerTab)} className="flex flex-1 flex-col">
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger value="profile" icon={User} label="Profile" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="achievements" icon={Award} label="Achievements" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="history" icon={Clock} label="Recent" activeAccent={colors.activeAccent} />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="profile" className="mt-0">
            <div className="space-y-3">
              <div className="grid grid-cols-3 gap-2">
                <StatCard label="Games Played" value={data.gamesPlayed.toString()} icon={Gamepad2} variant="player" />
                <StatCard label="Win Rate" value={`${Math.round(data.winRate * 100)}%`} icon={Trophy} variant="player" />
                <StatCard label="Sessions" value={data.totalSessions.toString()} icon={Users} variant="player" />
              </div>
              {data.favoriteGame && (
                <div className="rounded-lg bg-purple-50/50 border border-purple-200/40 p-3">
                  <p className="font-nunito text-[10px] text-purple-500 uppercase tracking-wider">Favorite Game</p>
                  <p className="font-quicksand text-sm font-bold text-purple-700">{data.favoriteGame}</p>
                </div>
              )}
            </div>
          </TabsContent>

          <TabsContent value="achievements" className="mt-0">
            <div className="space-y-2">
              {data.achievements.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">No achievements yet</p>
              ) : (
                data.achievements.map((a) => (
                  <div key={a.id} className="flex items-center gap-2 rounded-lg bg-white/50 border border-slate-200/40 p-2.5">
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
                <p className="font-nunito text-xs text-slate-400 text-center py-8">No recent games</p>
              ) : (
                data.recentGames.map((g) => (
                  <div key={`${g.name}-${g.date}`} className="flex items-center justify-between rounded-lg bg-white/50 border border-slate-200/40 p-2.5">
                    <div>
                      <p className="font-nunito text-xs font-medium text-slate-700">{g.name}</p>
                      <p className="font-nunito text-[10px] text-slate-400">{g.date}</p>
                    </div>
                    <span className={cn(
                      'rounded-full px-2 py-0.5 text-[10px] font-bold font-nunito',
                      g.result === 'win' ? 'bg-green-100 text-green-700' :
                      g.result === 'loss' ? 'bg-red-100 text-red-700' :
                      'bg-slate-100 text-slate-600'
                    )}>
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

// ============================================================================
// CollectionExtraMeepleCard
// ============================================================================

export interface CollectionExtraMeepleCardProps {
  data: CollectionDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type CollectionTab = 'overview' | 'games';

export const CollectionExtraMeepleCard = React.memo(function CollectionExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: CollectionExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<CollectionTab>('overview');
  const colors = ENTITY_COLORS.collection;

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
        title={data.name}
        subtitle={`by ${data.ownerName}`}
        imageUrl={data.imageUrl}
        color={colors.hsl}
        badge={data.gameCount.toString()}
        badgeIcon={<Gamepad2 className="h-3 w-3" />}
      />

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as CollectionTab)} className="flex flex-1 flex-col">
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger value="overview" icon={Library} label="Overview" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="games" icon={Gamepad2} label={`Games (${data.gameCount})`} activeAccent={colors.activeAccent} />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              <div className="grid grid-cols-2 gap-2">
                <StatCard label="Games" value={data.gameCount.toString()} icon={Gamepad2} variant="collection" />
                <StatCard label="Shared" value={data.isShared ? 'Yes' : 'No'} icon={Share2} variant="collection" />
              </div>
              {data.description && (
                <p className="font-nunito text-xs text-slate-600 leading-relaxed">{data.description}</p>
              )}
            </div>
          </TabsContent>

          <TabsContent value="games" className="mt-0">
            <div className="space-y-2">
              {data.games.length === 0 ? (
                <p className="font-nunito text-xs text-slate-400 text-center py-8">No games in collection</p>
              ) : (
                data.games.map((g) => (
                  <div key={g.id} className="flex items-center gap-3 rounded-lg bg-white/50 border border-slate-200/40 p-2.5">
                    {g.imageUrl ? (
                      <img src={g.imageUrl} alt={g.title} className="h-10 w-10 rounded object-cover" />
                    ) : (
                      <div className="flex h-10 w-10 items-center justify-center rounded bg-teal-50">
                        <Gamepad2 className="h-5 w-5 text-teal-400" />
                      </div>
                    )}
                    <p className="font-nunito text-xs font-medium text-slate-700">{g.title}</p>
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

// ============================================================================
// Shared Sub-Components
// ============================================================================

function EntityHeader({
  title,
  subtitle,
  imageUrl,
  color,
  badge,
  badgeIcon,
}: {
  title: string;
  subtitle?: string;
  imageUrl?: string;
  color: string;
  badge?: string;
  badgeIcon?: React.ReactNode;
}) {
  return (
    <div className="relative h-[140px] overflow-hidden">
      {imageUrl ? (
        <div className="absolute inset-0 bg-cover bg-center" style={{ backgroundImage: `url(${imageUrl})` }} />
      ) : (
        <div className="absolute inset-0" style={{ background: `hsl(${color})` }} />
      )}
      <div className="absolute inset-0 bg-gradient-to-b from-black/40 via-black/50 to-black/70" />
      <div className="absolute bottom-0 left-0 right-0 h-1" style={{ background: `hsl(${color})` }} />

      <div className="relative flex h-full flex-col justify-end p-5">
        <div className="flex items-end justify-between">
          <div className="space-y-0.5">
            <h2 className="font-quicksand text-xl font-bold text-white leading-tight line-clamp-2">{title}</h2>
            {subtitle && <p className="font-nunito text-sm text-white/70">{subtitle}</p>}
          </div>
          {badge && (
            <div className="flex items-center gap-1 rounded-full bg-white/20 backdrop-blur-sm px-2.5 py-1">
              {badgeIcon}
              <span className="font-quicksand text-sm font-bold text-white">{badge}</span>
            </div>
          )}
        </div>
      </div>
    </div>
  );
}

function EntityTabTrigger({
  value,
  icon: Icon,
  label,
  activeAccent,
}: {
  value: string;
  icon: React.ComponentType<{ className?: string }>;
  label: string;
  activeAccent: string;
}) {
  return (
    <TabsTrigger
      value={value}
      className={cn(
        'flex items-center gap-1.5 px-3 py-1.5 font-nunito text-xs font-medium',
        'data-[state=active]:bg-white data-[state=active]:shadow-sm',
        activeAccent,
        'transition-all duration-200'
      )}
    >
      <Icon className="h-3.5 w-3.5" />
      <span>{label}</span>
    </TabsTrigger>
  );
}

function StatCard({
  label,
  value,
  icon: Icon,
  variant,
}: {
  label: string;
  value: string;
  icon: React.ComponentType<{ className?: string }>;
  variant: EntityVariant;
}) {
  const colors = ENTITY_COLORS[variant];
  return (
    <div className={cn('rounded-lg border p-2.5', colors.accentBorder, `${colors.accentBg}/30`)}>
      <div className="flex items-center gap-1.5 mb-1">
        <Icon className={cn('h-3.5 w-3.5', colors.accent)} />
        <span className="font-nunito text-[10px] text-slate-500">{label}</span>
      </div>
      <p className={cn('font-quicksand text-sm font-bold', colors.accent)}>{value}</p>
    </div>
  );
}

function EntityLoadingState({ className, testId }: { className?: string; testId?: string }) {
  return (
    <div
      className={cn(
        'flex h-[600px] w-[600px] items-center justify-center rounded-2xl',
        'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
        'max-md:w-full',
        className
      )}
      data-testid={testId}
    >
      <div className="flex flex-col items-center gap-3 text-slate-400">
        <Loader2 className="h-8 w-8 animate-spin" />
        <span className="font-nunito text-sm">Loading...</span>
      </div>
    </div>
  );
}

function EntityErrorState({ error, className, testId }: { error: string; className?: string; testId?: string }) {
  return (
    <div
      className={cn(
        'flex h-[600px] w-[600px] items-center justify-center rounded-2xl',
        'bg-white/70 backdrop-blur-md shadow-lg border border-white/20',
        'max-md:w-full',
        className
      )}
      data-testid={testId}
    >
      <div className="flex flex-col items-center gap-3 text-red-400">
        <AlertCircle className="h-8 w-8" />
        <span className="font-nunito text-sm">{error}</span>
      </div>
    </div>
  );
}
