'use client';

/**
 * Dashboard Bento — Layout B (Bento Command)
 *
 * Desktop dashboard with:
 * - 200px sidebar nav (expanded, within main area)
 * - 12-column bento grid with variable-span widgets
 * - Live session, KPI stats, library, chat preview, leaderboard, trending
 */

import { useEffect } from 'react';

import Link from 'next/link';
import { usePathname, useRouter } from 'next/navigation';

import type { SessionSummaryDto, TrendingGameDto, UserGameDto } from '@/lib/api/dashboard-client';
import { useDashboardStore } from '@/lib/stores/dashboard-store';
import { cn } from '@/lib/utils';

import { BentoWidget, WidgetLabel } from './widgets/BentoWidget';
import { C } from './widgets/dashboard-colors';
import { KpiWidget } from './widgets/KpiWidget';
import { LiveSessionWidget } from './widgets/LiveSessionWidget';

// ─── Helpers ─────────────────────────────────────────────────────────────────

function parsePlayTimeHours(timeSpan: string): string {
  if (!timeSpan) return '—';
  const parts = timeSpan.split(':');
  const hours = parseInt(parts[0] ?? '0', 10);
  const minutes = parseInt(parts[1] ?? '0', 10);
  if (hours === 0) return `${minutes}m`;
  return `${hours}h`;
}

// ─── Library Widget (6×4) ─────────────────────────────────────────────────────

function LibraryWidget({
  games,
  totalCount,
  isLoading,
  error,
  onRetry,
}: {
  games: UserGameDto[];
  totalCount: number;
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  const router = useRouter();

  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      className="flex flex-col gap-0"
      onClick={() => router.push('/library')}
    >
      <WidgetLabel>La Tua Libreria</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {isLoading ? (
          Array.from({ length: 5 }).map((_, i) => (
            <div
              key={i}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 animate-pulse"
            >
              <div className="w-7 h-7 rounded-md bg-muted/60 shrink-0" />
              <div className="flex-1 h-3 rounded bg-muted/60" />
              <div className="w-8 h-3 rounded bg-muted/40" />
            </div>
          ))
        ) : error ? (
          <div className="flex flex-col items-center justify-center flex-1 gap-2 py-4 text-center">
            <p className="text-[11px] text-muted-foreground">Errore nel caricamento giochi</p>
            <button
              type="button"
              onClick={e => {
                e.stopPropagation();
                onRetry();
              }}
              className="text-[10px] font-bold px-3 py-1.5 rounded-lg border border-border hover:bg-muted/30 transition-colors"
            >
              Riprova
            </button>
          </div>
        ) : games.length === 0 ? (
          <p className="text-[11px] text-muted-foreground mt-2">Nessun gioco in libreria ancora</p>
        ) : (
          games.slice(0, 6).map(game => (
            <div
              key={game.id}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0 group/row"
              onClick={e => {
                e.stopPropagation();
                router.push(`/library/${game.id}`);
              }}
            >
              {(game.thumbnailUrl ?? game.imageUrl) ? (
                // eslint-disable-next-line @next/next/no-img-element
                <img
                  src={game.thumbnailUrl ?? game.imageUrl ?? ''}
                  alt={game.title}
                  className="w-7 h-7 rounded-md object-cover shrink-0"
                />
              ) : (
                <div
                  className="w-7 h-7 rounded-md shrink-0 flex items-center justify-center text-sm"
                  style={{ background: `${C.game}22` }}
                >
                  🎲
                </div>
              )}
              <span className="font-quicksand font-semibold text-[11px] flex-1 truncate text-foreground group-hover/row:text-primary transition-colors">
                {game.title}
              </span>
              {game.averageRating !== null && game.averageRating !== undefined && (
                <span
                  className="font-mono text-[9px] font-semibold shrink-0"
                  style={{ color: C.game }}
                >
                  ★ {game.averageRating.toFixed(1)}
                </span>
              )}
            </div>
          ))
        )}
      </div>
      <p className="text-[10px] font-bold mt-2 pt-1" style={{ color: C.game }}>
        Vedi tutti {totalCount} →
      </p>
    </BentoWidget>
  );
}

// ─── Chat Preview Widget (6×4) ────────────────────────────────────────────────

function ChatPreviewWidget() {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={4}
      accentColor={C.chat}
      className="flex flex-col"
      onClick={() => router.push('/chat')}
    >
      <div className="flex items-center justify-between mb-2">
        <WidgetLabel>Chat AI</WidgetLabel>
        <span
          className="text-[9px] font-bold rounded-full px-2 py-0.5"
          style={{ background: `${C.chat}20`, color: C.chat }}
        >
          Regole & Domande
        </span>
      </div>
      <div className="flex-1 flex flex-col items-center justify-center gap-3 text-center px-4">
        <div
          className="w-12 h-12 rounded-2xl flex items-center justify-center text-2xl"
          style={{ background: `${C.chat}15` }}
        >
          💬
        </div>
        <div>
          <p className="font-quicksand font-bold text-sm text-foreground">Chiedi all&apos;AI</p>
          <p className="text-[11px] text-muted-foreground mt-0.5">
            Regole, strategie e suggerimenti per i tuoi giochi da tavolo
          </p>
        </div>
      </div>
      <div
        className="mt-auto pt-2 flex gap-1.5"
        onClick={e => {
          e.stopPropagation();
          router.push('/chat');
        }}
      >
        <div
          className="flex-1 h-7 rounded-lg flex items-center px-2.5 text-[11px] text-muted-foreground/50"
          style={{
            background: 'rgba(255,255,255,0.03)',
            border: '1px solid rgba(255,255,255,0.07)',
          }}
        >
          Fai una domanda…
        </div>
        <button
          type="button"
          className="w-7 h-7 rounded-lg flex items-center justify-center text-white text-sm"
          style={{ background: C.chat }}
          aria-label="Vai alla chat"
          onClick={e => {
            e.stopPropagation();
            router.push('/chat');
          }}
        >
          ↑
        </button>
      </div>
    </BentoWidget>
  );
}

// ─── Leaderboard Widget (6×3) ─────────────────────────────────────────────────

function LeaderboardWidget({ sessions }: { sessions: SessionSummaryDto[] }) {
  const winners = sessions
    .filter(s => s.winnerName)
    .reduce<Record<string, number>>((acc, s) => {
      const key = s.winnerName ?? '';
      acc[key] = (acc[key] ?? 0) + 1;
      return acc;
    }, {});

  const sorted = Object.entries(winners)
    .sort(([, a], [, b]) => b - a)
    .slice(0, 4);

  const medals = ['🥇', '🥈', '🥉', '4️⃣'];
  const avatarColors = [C.game, C.player, C.event, C.session];

  return (
    <BentoWidget colSpan={6} rowSpan={3} accentColor={C.event} className="flex flex-col">
      <WidgetLabel>Classifica Gruppo</WidgetLabel>
      <div className="flex-1 flex flex-col overflow-hidden">
        {sorted.length === 0 ? (
          <p className="text-[11px] text-muted-foreground mt-2">
            Gioca partite con amici per vedere la classifica
          </p>
        ) : (
          sorted.map(([name, wins], i) => (
            <div
              key={name}
              className="flex items-center gap-2 py-1.5 border-b border-border/40 last:border-0"
            >
              <span className="text-sm w-5 text-center shrink-0">{medals[i]}</span>
              <div
                className="w-5 h-5 rounded-full flex items-center justify-center text-[9px] font-bold text-white shrink-0"
                style={{ background: avatarColors[i] }}
              >
                {name[0]?.toUpperCase()}
              </div>
              <span className="flex-1 font-quicksand font-semibold text-[11px] truncate">
                {name}
              </span>
              <span className="font-mono text-[9px] text-muted-foreground shrink-0">
                {wins} vitt.
              </span>
            </div>
          ))
        )}
      </div>
    </BentoWidget>
  );
}

// ─── Trending Widget (6×2) ───────────────────────────────────────────────────

function TrendingWidget({
  games,
  isLoading,
  error,
  onRetry,
}: {
  games: TrendingGameDto[];
  isLoading: boolean;
  error: string | null;
  onRetry: () => void;
}) {
  const router = useRouter();
  return (
    <BentoWidget
      colSpan={6}
      rowSpan={2}
      accentColor={C.kb}
      className="flex flex-col"
      onClick={() => router.push('/games')}
    >
      <WidgetLabel>Popolari questa settimana</WidgetLabel>
      {error ? (
        <div className="flex items-center gap-2 mt-1">
          <p className="text-[10px] text-muted-foreground flex-1">Errore nel caricamento</p>
          <button
            type="button"
            onClick={e => {
              e.stopPropagation();
              onRetry();
            }}
            className="text-[9px] font-bold px-2 py-1 rounded border border-border hover:bg-muted/30 transition-colors"
          >
            Riprova
          </button>
        </div>
      ) : (
        <div className="flex gap-3 mt-1 overflow-hidden">
          {isLoading
            ? Array.from({ length: 5 }).map((_, i) => (
                <div key={i} className="flex flex-col items-center gap-1 shrink-0">
                  <div className="w-9 h-12 rounded-md bg-muted/60 animate-pulse" />
                  <div className="w-9 h-2 rounded bg-muted/40 animate-pulse" />
                </div>
              ))
            : games.slice(0, 6).map(game => (
                <div
                  key={game.gameId}
                  className="flex flex-col items-center gap-1 cursor-pointer shrink-0 group/card"
                  onClick={e => {
                    e.stopPropagation();
                    router.push(`/games/${game.gameId}`);
                  }}
                >
                  {game.thumbnailUrl ? (
                    // eslint-disable-next-line @next/next/no-img-element
                    <img
                      src={game.thumbnailUrl}
                      alt={game.title}
                      className="w-9 h-12 rounded-md object-cover group-hover/card:ring-1 group-hover/card:ring-primary transition-all"
                    />
                  ) : (
                    <div
                      className="w-9 h-12 rounded-md flex items-center justify-center text-lg"
                      style={{ background: 'rgba(255,255,255,0.06)' }}
                    >
                      🎲
                    </div>
                  )}
                  <span className="font-quicksand text-[8px] font-bold text-center w-9 truncate">
                    {game.title}
                  </span>
                </div>
              ))}
        </div>
      )}
    </BentoWidget>
  );
}

// ─── Bento Sidebar ────────────────────────────────────────────────────────────

const SIDEBAR_NAV = [
  { icon: '🏠', label: 'Dashboard', href: '/dashboard' },
  { icon: '📚', label: 'Libreria', href: '/library?tab=collection' },
  { icon: '🎲', label: 'Sessioni', href: '/sessions' },
  { icon: '💬', label: 'Chat AI', href: '/chat' },
  { icon: '📄', label: 'Regole KB', href: '/library?tab=private' },
  { icon: '👥', label: 'Giocatori', href: '/players' },
];

const SIDEBAR_MANAGE = [
  { icon: '📊', label: 'Analytics', href: '/play-records' },
  { icon: '⚙️', label: 'Impostazioni', href: '/settings' },
];

function BentoDashboardSidebar() {
  const pathname = usePathname();

  const isActive = (href: string) => {
    const path = href.split('?')[0];
    if (path === '/dashboard') return pathname === '/dashboard';
    return pathname.startsWith(path);
  };

  return (
    <aside className="hidden lg:flex w-[200px] min-w-[200px] h-full bg-card border-r border-border/40 flex-col py-3 px-2 overflow-y-auto shrink-0">
      <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/40 px-2 pb-1 pt-1">
        Navigazione
      </p>
      {SIDEBAR_NAV.map(item => (
        <Link
          key={item.href}
          href={item.href}
          className={cn(
            'flex items-center gap-2 px-2 py-1.5 rounded-lg font-quicksand text-[12px] font-semibold transition-colors',
            isActive(item.href)
              ? 'text-[hsl(25,95%,45%)] bg-[rgba(245,130,31,0.1)]'
              : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'
          )}
        >
          <span className="text-sm w-5 text-center shrink-0">{item.icon}</span>
          <span>{item.label}</span>
        </Link>
      ))}
      <div className="h-px bg-border/40 my-2 mx-2" />
      <p className="font-mono text-[9px] uppercase tracking-widest text-muted-foreground/40 px-2 pb-1">
        Gestione
      </p>
      {SIDEBAR_MANAGE.map(item => (
        <Link
          key={item.href}
          href={item.href}
          className={cn(
            'flex items-center gap-2 px-2 py-1.5 rounded-lg font-quicksand text-[12px] font-semibold transition-colors',
            isActive(item.href)
              ? 'text-[hsl(25,95%,45%)] bg-[rgba(245,130,31,0.1)]'
              : 'text-muted-foreground hover:text-foreground hover:bg-muted/40'
          )}
        >
          <span className="text-sm w-5 text-center shrink-0">{item.icon}</span>
          <span>{item.label}</span>
        </Link>
      ))}
    </aside>
  );
}

// ─── Dashboard Client ─────────────────────────────────────────────────────────

export function DashboardClient() {
  const {
    stats,
    isLoadingStats,
    fetchStats,
    recentSessions,
    isLoadingSessions,
    sessionsError,
    fetchRecentSessions,
    updateFilters,
    games,
    isLoadingGames,
    gamesError,
    fetchGames,
    totalGamesCount,
    trendingGames,
    isLoadingTrending,
    trendingError,
    fetchTrendingGames,
  } = useDashboardStore();

  useEffect(() => {
    fetchStats();
    fetchRecentSessions(8);
    fetchTrendingGames(6);
    updateFilters({ sort: 'alphabetical', pageSize: 8, page: 1 });
    // eslint-disable-next-line react-hooks/exhaustive-deps -- store actions are stable Zustand references
  }, []);

  useEffect(() => {
    const handleVisibilityChange = () => {
      if (!document.hidden) fetchRecentSessions(8);
    };
    document.addEventListener('visibilitychange', handleVisibilityChange);
    return () => document.removeEventListener('visibilitychange', handleVisibilityChange);
    // fetchRecentSessions is a stable Zustand action reference
    // eslint-disable-next-line react-hooks/exhaustive-deps
  }, []);

  const latestSession = recentSessions[0];

  const monthlyPlays = stats?.monthlyPlays ?? 0;
  const playsChange = stats?.monthlyPlaysChange;
  const weeklyHours = stats ? parsePlayTimeHours(stats.weeklyPlayTime) : '—';
  const totalGames = stats?.totalGames ?? 0;

  return (
    <div className="flex h-full bg-background overflow-hidden">
      <BentoDashboardSidebar />

      {/* Bento grid area */}
      <div className="flex-1 overflow-y-auto p-3.5">
        <div
          className="grid grid-cols-6 lg:grid-cols-12"
          style={{ gridAutoRows: '60px', gap: '8px' }}
        >
          {/* Row 1-2: Live Session (8×2) + Partite (4×2) */}
          <LiveSessionWidget
            session={latestSession}
            isLoading={isLoadingSessions}
            error={sessionsError}
            onRetry={() => fetchRecentSessions(8)}
          />
          <KpiWidget
            label="Partite (mese)"
            value={isLoadingStats ? '…' : monthlyPlays}
            badge={
              playsChange !== null && playsChange !== undefined && playsChange !== 0
                ? `${playsChange > 0 ? '+' : ''}${playsChange}%`
                : undefined
            }
            badgePositive={(playsChange ?? 0) > 0}
            accentColor={C.game}
            colSpan={4}
            tabletColSpan={6}
            rowSpan={2}
            href="/sessions"
          />

          {/* Row 3-6: Library (6×4) + Ore (3×2) + Giochi (3×2) */}
          <LibraryWidget
            games={games}
            totalCount={totalGamesCount || totalGames}
            isLoading={isLoadingGames}
            error={gamesError}
            onRetry={fetchGames}
          />
          <KpiWidget
            label="Ore sett."
            value={isLoadingStats ? '…' : weeklyHours}
            sub="questa settimana"
            accentColor={C.session}
            colSpan={3}
            rowSpan={2}
          />
          <KpiWidget
            label="Giochi in lib."
            value={isLoadingStats ? '…' : totalGames}
            accentColor={C.event}
            colSpan={3}
            rowSpan={2}
            href="/library"
          />

          {/* Row 5-8: Chat AI (6×4) */}
          <ChatPreviewWidget />

          {/* Row 7-9: Leaderboard (6×3) */}
          <LeaderboardWidget sessions={recentSessions} />

          {/* Row 9-10: Trending (6×2) */}
          <TrendingWidget
            games={trendingGames}
            isLoading={isLoadingTrending}
            error={trendingError}
            onRetry={() => fetchTrendingGames(6)}
          />
        </div>
      </div>
    </div>
  );
}
