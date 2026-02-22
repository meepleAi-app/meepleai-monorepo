'use client';

/**
 * Entity Variant ExtraMeepleCards
 * Issue #4762 - ExtraMeepleCard: Media Tab + AI Tab + Other Entity Types
 * Issue #5026 - AgentExtraMeepleCard (Epic #5023)
 *
 * Expanded card variants for Game, Player, Collection, and Agent entities.
 * Each uses entity-specific color schemes from MeepleCard v2 tokens.
 */

import React, { useState } from 'react';
import {
  Award,
  Bot,
  BookOpen,
  Calendar,
  Clock,
  ExternalLink,
  FileText,
  Gamepad2,
  Hash,
  HelpCircle,
  Library,
  Loader2,
  AlertCircle,
  MessageCircle,
  MessageSquare,
  Settings,
  Share2,
  Star,
  Trophy,
  User,
  Users,
  Zap,
} from 'lucide-react';
import { Tabs, TabsList, TabsTrigger, TabsContent } from '@/components/ui/navigation/tabs';
import { cn } from '@/lib/utils';
import { AgentStatusBadge } from '@/components/ui/data-display/meeple-card-features/AgentStatusBadge';
import { AgentStatsDisplay } from '@/components/ui/data-display/meeple-card-features/AgentStatsDisplay';
import { AgentModelInfo } from '@/components/ui/data-display/meeple-card-features/AgentModelInfo';
import { DocumentStatusBadge } from '@/components/ui/data-display/meeple-card-features/DocumentStatusBadge';
import { ChatStatusBadge } from '@/components/ui/data-display/meeple-card-features/ChatStatusBadge';
import type {
  GameDetailData,
  PlayerDetailData,
  CollectionDetailData,
  AgentDetailData,
  ChatDetailData,
  ChatDetailMessage,
  ChatThreadPreview,
  KbDocumentPreview,
} from './types';

// ============================================================================
// Entity Color Constants (from MeepleCard v2 tokens)
// ============================================================================

const ENTITY_COLORS = {
  game:       { hsl: '25 95% 45%',   accent: 'text-orange-700', accentBg: 'bg-orange-100', accentBorder: 'border-orange-200/60', activeAccent: 'data-[state=active]:text-orange-700' },
  player:     { hsl: '262 83% 58%',  accent: 'text-purple-700', accentBg: 'bg-purple-100', accentBorder: 'border-purple-200/60', activeAccent: 'data-[state=active]:text-purple-700' },
  collection: { hsl: '174 60% 40%',  accent: 'text-teal-700',   accentBg: 'bg-teal-100',   accentBorder: 'border-teal-200/60',   activeAccent: 'data-[state=active]:text-teal-700'   },
  agent:      { hsl: '220 70% 55%',  accent: 'text-blue-700',   accentBg: 'bg-blue-100',   accentBorder: 'border-blue-200/60',   activeAccent: 'data-[state=active]:text-blue-700'   },
  chat:       { hsl: '262 83% 58%',  accent: 'text-violet-700', accentBg: 'bg-violet-100', accentBorder: 'border-violet-200/60', activeAccent: 'data-[state=active]:text-violet-700' },
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
// AgentExtraMeepleCard
// Issue #5026 — Agent detail card with 4 tabs (Epic #5023)
// ============================================================================

export interface AgentExtraMeepleCardProps {
  data: AgentDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type AgentTab = 'overview' | 'stats' | 'history' | 'kb';

export const AgentExtraMeepleCard = React.memo(function AgentExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: AgentExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<AgentTab>('overview');
  const { threads, loading: threadsLoading } = useAgentThreads(data.id);
  const { docs, loading: docsLoading } = useAgentKbDocs(data.gameId);
  const colors = ENTITY_COLORS.agent;

  // Derive AgentStatus from flags
  const agentStatus = !data.isActive ? 'error' : data.isIdle ? 'idle' : 'active';

  // Derive model name from strategyParameters or strategyName
  const modelName = (data.strategyParameters?.model as string | undefined)
    ?? data.strategyName
    ?? 'Unknown';

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error)   return <EntityErrorState error={error} className={className} testId={testId} />;

  return (
    <div
      className={cn(
        'flex w-[600px] flex-col rounded-2xl overflow-hidden',
        'bg-white/70 backdrop-blur-md shadow-[0_8px_32px_rgba(0,0,0,0.12)] border border-white/20',
        'max-md:w-full',
        className,
      )}
      data-testid={testId}
    >
      {/* Header */}
      <EntityHeader
        title={data.name}
        subtitle={data.type ? `Tipo: ${data.type}` : undefined}
        color={colors.hsl}
        badge={data.invocationCount > 0 ? data.invocationCount.toString() : undefined}
        badgeIcon={<Zap className="h-3 w-3" />}
      />

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as AgentTab)} className="flex flex-1 flex-col">
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger value="overview" icon={Bot}           label="Overview" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="stats"    icon={Zap}           label="Stats"    activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="history"  icon={MessageSquare} label="History"  activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="kb"       icon={FileText}      label="KB"       activeAccent={colors.activeAccent} />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">

          {/* ── Overview Tab ─────────────────────────────────────── */}
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              {/* Status + Model row */}
              <div className="flex items-center justify-between gap-2 rounded-lg bg-blue-50/50 border border-blue-200/40 p-3">
                <AgentStatusBadge status={agentStatus} showLabel size="md" />
                <AgentModelInfo modelName={modelName} showIcon size="md" />
              </div>

              {/* Linked game chip */}
              {data.gameName && (
                <div className={cn(
                  'flex items-center gap-2 rounded-lg border p-3',
                  colors.accentBorder, `${colors.accentBg}/30`,
                )}>
                  <Gamepad2 className={cn('h-4 w-4 shrink-0', colors.accent)} aria-hidden="true" />
                  <div className="flex-1 min-w-0">
                    <p className="font-nunito text-[10px] text-slate-500 uppercase tracking-wider">Gioco collegato</p>
                    <p className={cn('font-quicksand text-sm font-bold truncate', colors.accent)}>
                      {data.gameName}
                    </p>
                  </div>
                </div>
              )}

              {/* Quick actions */}
              <div className="grid grid-cols-2 gap-2">
                <a
                  href={`/chat/new?agentId=${data.id}`}
                  className={cn(
                    'flex items-center justify-center gap-1.5 rounded-lg border py-2.5 px-3',
                    'bg-blue-600 border-blue-700 text-white',
                    'font-nunito text-xs font-medium',
                    'transition-colors duration-150 hover:bg-blue-700',
                    'focus:outline-none focus:ring-2 focus:ring-blue-500 focus:ring-offset-1',
                  )}
                  data-testid="agent-action-start-chat"
                >
                  <MessageCircle className="h-3.5 w-3.5" aria-hidden="true" />
                  Avvia Chat
                </a>
                {data.gameId && (
                  <a
                    href={`/library/games/${data.gameId}/agent`}
                    className={cn(
                      'flex items-center justify-center gap-1.5 rounded-lg border py-2.5 px-3',
                      'bg-white border-blue-200 text-blue-700',
                      'font-nunito text-xs font-medium',
                      'transition-colors duration-150 hover:bg-blue-50',
                      'focus:outline-none focus:ring-2 focus:ring-blue-400 focus:ring-offset-1',
                    )}
                    data-testid="agent-action-configure"
                  >
                    <Settings className="h-3.5 w-3.5" aria-hidden="true" />
                    Configura
                  </a>
                )}
              </div>
            </div>
          </TabsContent>

          {/* ── Stats Tab ─────────────────────────────────────────── */}
          <TabsContent value="stats" className="mt-0">
            <div className="space-y-3">
              <AgentStatsDisplay
                stats={{
                  invocationCount: data.invocationCount,
                  lastExecutedAt: data.lastInvokedAt ?? undefined,
                }}
                layout="vertical"
              />
              {data.invocationCount === 0 && (
                <p className="font-nunito text-xs text-slate-400 text-center py-4">
                  Nessuna conversazione ancora
                </p>
              )}
            </div>
          </TabsContent>

          {/* ── History Tab ───────────────────────────────────────── */}
          <TabsContent value="history" className="mt-0">
            {threadsLoading ? (
              <div className="flex justify-center py-8">
                <Loader2 className="h-5 w-5 animate-spin text-blue-400" />
              </div>
            ) : threads.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <MessageSquare className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400">
                  Nessun thread di chat
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                {threads.map((thread) => (
                  <ThreadItem key={thread.id} thread={thread} accentColor={colors.accent} />
                ))}
              </div>
            )}
          </TabsContent>

          {/* ── KB Tab ────────────────────────────────────────────── */}
          <TabsContent value="kb" className="mt-0">
            {docsLoading ? (
              <div className="flex justify-center py-8">
                <Loader2 className="h-5 w-5 animate-spin text-blue-400" />
              </div>
            ) : !data.gameId ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <FileText className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400">
                  Nessun gioco collegato a questo agente
                </p>
              </div>
            ) : docs.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <FileText className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400 mb-2">
                  Nessun documento KB
                </p>
                <a
                  href={`/library/games/${data.gameId}/agent`}
                  className="font-nunito text-xs text-blue-600 underline hover:text-blue-700"
                >
                  Aggiungi documento
                </a>
              </div>
            ) : (
              <div className="space-y-2">
                {docs.map((doc) => (
                  <KbDocItem key={doc.id} doc={doc} />
                ))}
              </div>
            )}
          </TabsContent>

        </div>
      </Tabs>
    </div>
  );
});

// ── Agent sub-components ───────────────────────────────────────────────────

function ThreadItem({
  thread,
  accentColor,
}: {
  thread: ChatThreadPreview;
  accentColor: string;
}) {
  const date = new Date(thread.createdAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });
  const preview = thread.firstMessagePreview.length > 80
    ? `${thread.firstMessagePreview.slice(0, 80)}…`
    : thread.firstMessagePreview;

  return (
    <div className="flex items-start gap-3 rounded-lg bg-white/50 border border-slate-200/40 p-2.5">
      <MessageSquare className="h-4 w-4 mt-0.5 shrink-0 text-slate-400" aria-hidden="true" />
      <div className="flex-1 min-w-0 space-y-0.5">
        <div className="flex items-center justify-between gap-2">
          <p className="font-nunito text-[10px] text-slate-400">{date}</p>
          <p className="font-nunito text-[10px] text-slate-400">{thread.messageCount} msg</p>
        </div>
        {preview && (
          <p className="font-nunito text-xs text-slate-600 leading-relaxed">{preview}</p>
        )}
      </div>
      <a
        href={`/chat/${thread.id}`}
        className={cn('shrink-0 font-nunito text-[10px] font-medium', accentColor, 'hover:underline')}
        aria-label={`Vai al thread del ${date}`}
      >
        <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
      </a>
    </div>
  );
}

function KbDocItem({ doc }: { doc: KbDocumentPreview }) {
  const date = new Date(doc.uploadedAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });

  return (
    <div className="flex items-center gap-3 rounded-lg bg-white/50 border border-slate-200/40 p-2.5">
      <FileText className="h-4 w-4 shrink-0 text-slate-400" aria-hidden="true" />
      <div className="flex-1 min-w-0">
        <p className="font-nunito text-xs font-medium text-slate-700 truncate">{doc.fileName}</p>
        <p className="font-nunito text-[10px] text-slate-400">{date}</p>
      </div>
      <DocumentStatusBadge status={doc.status} size="sm" />
    </div>
  );
}

// ── Agent data-fetching hooks  ──────────────────────────────────────────────

function useAgentThreads(agentId: string) {
  const [threads, setThreads] = React.useState<ChatThreadPreview[]>([]);
  const [loading, setLoading] = React.useState(true);

  React.useEffect(() => {
    if (!agentId) return;
    const controller = new AbortController();

    (async () => {
      try {
        const res = await fetch(`/api/v1/chat/threads?agentId=${agentId}`, { signal: controller.signal });
        if (!res.ok) return;
        const json = (await res.json()) as unknown[];
        setThreads(mapThreads(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
      } finally {
        setLoading(false);
      }
    })();

    return () => { controller.abort(); };
  }, [agentId]);

  return { threads, loading };
}

function useAgentKbDocs(gameId: string | undefined) {
  const [docs, setDocs] = React.useState<KbDocumentPreview[]>([]);
  const [loading, setLoading] = React.useState(!!gameId);

  React.useEffect(() => {
    if (!gameId) { setLoading(false); return; }
    const controller = new AbortController();

    (async () => {
      try {
        const res = await fetch(`/api/v1/library/games/${gameId}/documents`, { signal: controller.signal });
        if (!res.ok) return;
        const json = (await res.json()) as unknown[];
        setDocs(mapKbDocs(json));
      } catch (err) {
        if (err instanceof Error && err.name === 'AbortError') return;
      } finally {
        setLoading(false);
      }
    })();

    return () => { controller.abort(); };
  }, [gameId]);

  return { docs, loading };
}

function mapThreads(json: unknown[]): ChatThreadPreview[] {
  return json.map((raw) => {
    const t = raw as Record<string, unknown>;
    const messages = Array.isArray(t.messages) ? (t.messages as Record<string, unknown>[]) : [];
    const firstMsg = messages[0];
    const preview = typeof firstMsg?.content === 'string' ? firstMsg.content : '';
    return {
      id: String(t.id ?? ''),
      createdAt: String(t.createdAt ?? t.startedAt ?? new Date().toISOString()),
      messageCount: messages.length,
      firstMessagePreview: preview,
    };
  });
}

function mapKbDocs(json: unknown[]): KbDocumentPreview[] {
  return json.map((raw) => {
    const d = raw as Record<string, unknown>;
    const statusMap: Record<string, KbDocumentPreview['status']> = {
      indexed: 'indexed', processing: 'processing', failed: 'failed', none: 'none',
    };
    const rawStatus = String(d.status ?? 'none').toLowerCase();
    return {
      id: String(d.id ?? ''),
      fileName: String(d.fileName ?? d.name ?? 'Documento'),
      uploadedAt: String(d.uploadedAt ?? d.createdAt ?? new Date().toISOString()),
      status: statusMap[rawStatus] ?? 'none',
    };
  });
}

// ============================================================================
// ChatExtraMeepleCard
// ============================================================================

export interface ChatExtraMeepleCardProps {
  data: ChatDetailData;
  loading?: boolean;
  error?: string;
  className?: string;
  'data-testid'?: string;
}

type ChatTab = 'overview' | 'messages' | 'context';

export const ChatExtraMeepleCard = React.memo(function ChatExtraMeepleCard({
  data,
  loading,
  error,
  className,
  'data-testid': testId,
}: ChatExtraMeepleCardProps) {
  const [activeTab, setActiveTab] = useState<ChatTab>('overview');
  const colors = ENTITY_COLORS.chat;

  if (loading) return <EntityLoadingState className={className} testId={testId} />;
  if (error)   return <EntityErrorState error={error} className={className} testId={testId} />;

  const startDate = new Date(data.startedAt).toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    year: '2-digit',
  });

  return (
    <div
      className={cn(
        'flex w-[600px] flex-col rounded-2xl overflow-hidden',
        'bg-white/70 backdrop-blur-md shadow-[0_8px_32px_rgba(0,0,0,0.12)] border border-white/20',
        'max-md:w-full',
        className,
      )}
      data-testid={testId}
    >
      {/* Header */}
      <EntityHeader
        title={data.agentName ?? 'Chat'}
        subtitle={`Thread · ${data.messageCount} messaggi`}
        color={colors.hsl}
      />

      {/* Tabs */}
      <Tabs value={activeTab} onValueChange={(v) => setActiveTab(v as ChatTab)} className="flex flex-1 flex-col">
        <TabsList className="mx-4 mt-3 h-10 w-auto justify-start gap-1 bg-slate-100/80 rounded-lg p-1">
          <EntityTabTrigger value="overview"  icon={MessageCircle} label="Overview" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="messages"  icon={MessageSquare} label="Messaggi" activeAccent={colors.activeAccent} />
          <EntityTabTrigger value="context"   icon={Settings}      label="Contesto" activeAccent={colors.activeAccent} />
        </TabsList>

        <div className="flex-1 overflow-y-auto px-4 py-3">

          {/* ── Overview Tab ─────────────────────────────────────── */}
          <TabsContent value="overview" className="mt-0">
            <div className="space-y-3">
              {/* Status + model row */}
              <div className={cn(
                'flex items-center justify-between gap-2 rounded-lg border p-3',
                colors.accentBorder, `${colors.accentBg}/30`,
              )}>
                <ChatStatusBadge status={data.status} showLabel size="md" />
                {data.agentModel && (
                  <span className={cn(
                    'inline-flex items-center gap-1 rounded-full px-2.5 py-1',
                    colors.accentBg, colors.accent,
                    'font-nunito text-[10px] font-semibold',
                  )}>
                    <Bot className="h-3 w-3" aria-hidden="true" />
                    {data.agentModel}
                  </span>
                )}
              </div>

              {/* Game context chip */}
              {data.gameName && (
                <div className={cn(
                  'flex items-center gap-2 rounded-lg border p-3',
                  colors.accentBorder, `${colors.accentBg}/30`,
                )}>
                  <Gamepad2 className={cn('h-4 w-4 shrink-0', colors.accent)} aria-hidden="true" />
                  <div className="flex-1 min-w-0">
                    <p className="font-nunito text-[10px] text-slate-500 uppercase tracking-wider">Gioco di contesto</p>
                    <p className={cn('font-quicksand text-sm font-bold truncate', colors.accent)}>
                      {data.gameName}
                    </p>
                  </div>
                </div>
              )}

              {/* Metadata stat cards */}
              <div className="grid grid-cols-3 gap-2">
                <StatCard label="Inizio"    value={startDate}                  icon={Calendar} variant="chat" />
                {data.durationMinutes != null && (
                  <StatCard label="Durata"  value={`${data.durationMinutes}m`} icon={Clock}    variant="chat" />
                )}
                <StatCard label="Messaggi" value={data.messageCount.toString()} icon={Hash}    variant="chat" />
              </div>

              {/* CTA */}
              <a
                href={`/chat/${data.id}`}
                className={cn(
                  'flex items-center justify-center gap-1.5 rounded-lg border py-2.5 px-3 w-full',
                  'bg-violet-600 border-violet-700 text-white',
                  'font-nunito text-xs font-medium',
                  'transition-colors duration-150 hover:bg-violet-700',
                  'focus:outline-none focus:ring-2 focus:ring-violet-500 focus:ring-offset-1',
                )}
                data-testid="chat-action-resume"
              >
                <MessageCircle className="h-3.5 w-3.5" aria-hidden="true" />
                Riprendi Chat
              </a>
            </div>
          </TabsContent>

          {/* ── Messages Tab ──────────────────────────────────────── */}
          <TabsContent value="messages" className="mt-0">
            {data.messages.length === 0 ? (
              <div className="flex flex-col items-center gap-2 py-8 text-center">
                <MessageSquare className="h-8 w-8 text-slate-300" aria-hidden="true" />
                <p className="font-nunito text-xs text-slate-400">
                  Nessun messaggio in questo thread
                </p>
              </div>
            ) : (
              <div className="space-y-2">
                {data.messages.map((msg) => (
                  <ChatBubble key={msg.id} message={msg} />
                ))}
                {/* Footer CTA */}
                <div className="pt-2">
                  <a
                    href={`/chat/${data.id}`}
                    className={cn(
                      'flex items-center justify-center gap-1.5 rounded-lg border py-2 px-3 w-full',
                      'bg-white border-slate-200 text-violet-700',
                      'font-nunito text-xs font-medium',
                      'transition-colors duration-150 hover:bg-violet-50',
                      'focus:outline-none focus:ring-2 focus:ring-violet-400 focus:ring-offset-1',
                    )}
                    data-testid="chat-action-view-full"
                  >
                    <ExternalLink className="h-3.5 w-3.5" aria-hidden="true" />
                    Vedi conversazione completa
                  </a>
                </div>
              </div>
            )}
          </TabsContent>

          {/* ── Context Tab ───────────────────────────────────────── */}
          <TabsContent value="context" className="mt-0">
            <div className="space-y-3">
              {/* Compact game card */}
              {data.gameName && (
                <div className="flex items-center gap-3 rounded-lg bg-white/60 border border-slate-200/40 p-3">
                  {data.gameThumbnailUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={data.gameThumbnailUrl}
                      alt={data.gameName}
                      className="h-10 w-10 rounded-lg object-cover shrink-0"
                    />
                  ) : (
                    <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-orange-100">
                      <Gamepad2 className="h-5 w-5 text-orange-600" aria-hidden="true" />
                    </div>
                  )}
                  <div className="min-w-0">
                    <p className="font-nunito text-[10px] text-slate-400 uppercase tracking-wider">Gioco</p>
                    <p className="font-quicksand text-sm font-semibold text-slate-700 truncate">{data.gameName}</p>
                  </div>
                </div>
              )}

              {/* Compact agent card */}
              {data.agentName && (
                <div className="flex items-center gap-3 rounded-lg bg-white/60 border border-slate-200/40 p-3">
                  <div className="flex h-10 w-10 shrink-0 items-center justify-center rounded-lg bg-blue-100">
                    <Bot className="h-5 w-5 text-blue-600" aria-hidden="true" />
                  </div>
                  <div className="min-w-0">
                    <p className="font-nunito text-[10px] text-slate-400 uppercase tracking-wider">Agente</p>
                    <p className="font-quicksand text-sm font-semibold text-slate-700 truncate">{data.agentName}</p>
                    {data.agentModel && (
                      <p className="font-nunito text-[10px] text-slate-400 truncate">{data.agentModel}</p>
                    )}
                  </div>
                </div>
              )}

              {/* Session parameters */}
              {(data.temperature != null || data.maxTokens != null || data.systemPrompt) && (
                <div className="rounded-lg bg-white/60 border border-slate-200/40 p-3 space-y-2">
                  <p className="font-nunito text-[10px] text-slate-400 uppercase tracking-wider">Parametri sessione</p>
                  {data.temperature != null && (
                    <div className="flex items-center justify-between">
                      <span className="font-nunito text-xs text-slate-500">Temperatura</span>
                      <span className="font-quicksand text-xs font-semibold text-slate-700">{data.temperature}</span>
                    </div>
                  )}
                  {data.maxTokens != null && (
                    <div className="flex items-center justify-between">
                      <span className="font-nunito text-xs text-slate-500">Max token</span>
                      <span className="font-quicksand text-xs font-semibold text-slate-700">{data.maxTokens}</span>
                    </div>
                  )}
                  {data.systemPrompt && (
                    <div className="space-y-1">
                      <p className="font-nunito text-[10px] text-slate-400">System prompt</p>
                      <p className="font-nunito text-xs text-slate-600 leading-relaxed">
                        {data.systemPrompt.length > 150
                          ? `${data.systemPrompt.slice(0, 150)}…`
                          : data.systemPrompt}
                      </p>
                    </div>
                  )}
                </div>
              )}

              {/* Empty state */}
              {!data.gameName && !data.agentName && data.temperature == null && data.maxTokens == null && !data.systemPrompt && (
                <div className="flex flex-col items-center gap-2 py-8 text-center">
                  <Settings className="h-8 w-8 text-slate-300" aria-hidden="true" />
                  <p className="font-nunito text-xs text-slate-400">
                    Nessun contesto disponibile
                  </p>
                </div>
              )}
            </div>
          </TabsContent>

        </div>
      </Tabs>
    </div>
  );
});

// ── Chat sub-components ────────────────────────────────────────────────────

function ChatBubble({ message }: { message: ChatDetailMessage }) {
  const isUser = message.role === 'user';
  const text = message.content.length > 200
    ? `${message.content.slice(0, 200)}…`
    : message.content;

  const diffMs = Date.now() - new Date(message.createdAt).getTime();
  const diffMin = Math.floor(diffMs / 60_000);
  const diffHr  = Math.floor(diffMin / 60);
  const diffDay = Math.floor(diffHr  / 24);
  const relTime = diffMin < 1  ? 'ora'
    : diffMin < 60 ? `${diffMin}m fa`
    : diffHr  < 24 ? `${diffHr}h fa`
    : `${diffDay}g fa`;

  return (
    <div className={cn('flex gap-2', isUser ? 'flex-row-reverse' : 'flex-row')}>
      {/* Avatar */}
      <div className={cn(
        'flex h-7 w-7 shrink-0 items-center justify-center rounded-full',
        isUser ? 'bg-violet-100 text-violet-700' : 'bg-slate-100 text-slate-500',
      )}>
        {isUser
          ? <User className="h-3.5 w-3.5" aria-hidden="true" />
          : <Bot  className="h-3.5 w-3.5" aria-hidden="true" />
        }
      </div>

      {/* Bubble */}
      <div className={cn(
        'max-w-[75%] rounded-2xl px-3 py-2',
        isUser
          ? 'bg-violet-100 border border-violet-200/60'
          : 'bg-white/80 border border-slate-200/40',
      )}>
        <p className="font-nunito text-xs text-slate-700 leading-relaxed">{text}</p>
        <p className="font-nunito text-[10px] text-slate-400 mt-1">{relTime}</p>
      </div>
    </div>
  );
}

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
