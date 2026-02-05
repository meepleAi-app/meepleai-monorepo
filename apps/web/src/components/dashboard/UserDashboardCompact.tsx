/**
 * UserDashboardCompact - Refined User Dashboard with Tabletop Elegance
 * Issue #3306 - User Dashboard Hub Core - MVP
 *
 * A condensed, elegant dashboard that prioritizes quick access
 * and visual warmth without overwhelming the user.
 *
 * Design Philosophy:
 * - "Warm Tabletop" aesthetic with wood grain textures
 * - Compact single-scroll layout (no endless sections)
 * - Floating action bar with game piece metaphor
 * - Typography: Playfair Display + Geist Sans
 * - Colors: Amber (wood), Emerald (felt), Stone (linen)
 *
 * @example
 * ```tsx
 * <UserDashboardCompact />
 * ```
 */

'use client';

import { useState, useMemo } from 'react';

import { motion } from 'framer-motion';
import {
  Library,
  Dices,
  MessageSquare,
  Star,
  Plus,
  Search,
  TrendingUp,
  Clock,
  ChevronRight,
  Sparkles,
  Users,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

interface StatCard {
  id: string;
  icon: LucideIcon;
  value: number;
  label: string;
  trend?: number;
  href: string;
  color: 'amber' | 'emerald' | 'blue' | 'purple';
}

interface QuickGame {
  id: string;
  name: string;
  imageUrl?: string;
  lastPlayed?: Date;
  playCount: number;
}

interface RecentActivity {
  id: string;
  type: 'played' | 'added' | 'shared' | 'chat';
  title: string;
  subtitle?: string;
  timestamp: Date;
}

// ============================================================================
// Mock Data
// ============================================================================

const MOCK_STATS: StatCard[] = [
  { id: 'collection', icon: Library, value: 127, label: 'Giochi', trend: 3, href: '/library', color: 'amber' },
  { id: 'played', icon: Dices, value: 23, label: 'Partite 30gg', trend: 5, href: '/sessions', color: 'emerald' },
  { id: 'chats', icon: MessageSquare, value: 12, label: 'Chat AI', href: '/chat', color: 'blue' },
  { id: 'wishlist', icon: Star, value: 15, label: 'Wishlist', trend: 2, href: '/wishlist', color: 'purple' },
];

const MOCK_QUICK_GAMES: QuickGame[] = [
  { id: '1', name: 'Wingspan', playCount: 12, lastPlayed: new Date(Date.now() - 86400000 * 2) },
  { id: '2', name: 'Catan', playCount: 45, lastPlayed: new Date(Date.now() - 3600000 * 2) },
  { id: '3', name: 'Gloomhaven', playCount: 8, lastPlayed: new Date(Date.now() - 86400000 * 7) },
  { id: '4', name: 'Terraforming Mars', playCount: 15 },
];

const MOCK_ACTIVITIES: RecentActivity[] = [
  { id: '1', type: 'played', title: 'Catan completato', subtitle: '4 giocatori • 90 min', timestamp: new Date(Date.now() - 3600000 * 2) },
  { id: '2', type: 'added', title: 'Wingspan aggiunto', timestamp: new Date(Date.now() - 86400000) },
  { id: '3', type: 'chat', title: 'Domanda su Gloomhaven', subtitle: "Regole sugli attacchi dell'AI", timestamp: new Date(Date.now() - 86400000 * 2) },
];

// ============================================================================
// Utility Functions
// ============================================================================

function getGreeting(): string {
  const hour = new Date().getHours();
  if (hour >= 5 && hour < 12) return 'Buongiorno';
  if (hour >= 12 && hour < 18) return 'Buon pomeriggio';
  return 'Buonasera';
}

function formatRelativeTime(date: Date): string {
  const now = new Date();
  const diff = now.getTime() - date.getTime();
  const minutes = Math.floor(diff / 60000);
  const hours = Math.floor(diff / 3600000);
  const days = Math.floor(diff / 86400000);

  if (minutes < 60) return `${minutes}m fa`;
  if (hours < 24) return `${hours}h fa`;
  if (days === 1) return 'Ieri';
  if (days < 7) return `${days}gg fa`;
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

const colorMap = {
  amber: {
    bg: 'bg-amber-500/15 dark:bg-amber-500/20',
    icon: 'text-amber-600 dark:text-amber-400',
    border: 'border-amber-500/30',
    glow: 'shadow-amber-500/20',
  },
  emerald: {
    bg: 'bg-emerald-500/15 dark:bg-emerald-500/20',
    icon: 'text-emerald-600 dark:text-emerald-400',
    border: 'border-emerald-500/30',
    glow: 'shadow-emerald-500/20',
  },
  blue: {
    bg: 'bg-blue-500/15 dark:bg-blue-500/20',
    icon: 'text-blue-600 dark:text-blue-400',
    border: 'border-blue-500/30',
    glow: 'shadow-blue-500/20',
  },
  purple: {
    bg: 'bg-purple-500/15 dark:bg-purple-500/20',
    icon: 'text-purple-600 dark:text-purple-400',
    border: 'border-purple-500/30',
    glow: 'shadow-purple-500/20',
  },
};

// ============================================================================
// Sub-Components
// ============================================================================

/** Compact stat card with trend indicator */
function StatCardCompact({ stat, index }: { stat: StatCard; index: number }) {
  const colors = colorMap[stat.color];
  const Icon = stat.icon;

  return (
    <motion.div
      initial={{ opacity: 0, y: 20 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.05 }}
    >
      <Link
        href={stat.href}
        className={cn(
          'group relative flex flex-col gap-1 p-4 rounded-2xl',
          'border backdrop-blur-sm',
          'transition-all duration-300',
          'hover:scale-[1.02] hover:shadow-lg',
          colors.bg,
          colors.border,
          `hover:${colors.glow}`
        )}
      >
        <div className="flex items-center justify-between">
          <Icon className={cn('h-5 w-5', colors.icon)} />
          {stat.trend !== undefined && stat.trend > 0 && (
            <span className="flex items-center gap-0.5 text-xs text-emerald-600 dark:text-emerald-400">
              <TrendingUp className="h-3 w-3" />
              +{stat.trend}
            </span>
          )}
        </div>
        <div className="mt-1">
          <span className="font-heading text-2xl font-bold tracking-tight">
            {stat.value}
          </span>
        </div>
        <span className="text-xs text-muted-foreground">{stat.label}</span>
      </Link>
    </motion.div>
  );
}

/** Game thumbnail with play count */
function QuickGameCard({ game, index }: { game: QuickGame; index: number }) {
  return (
    <motion.div
      initial={{ opacity: 0, scale: 0.9 }}
      animate={{ opacity: 1, scale: 1 }}
      transition={{ delay: 0.2 + index * 0.05 }}
    >
      <Link
        href={`/library/${game.id}`}
        className={cn(
          'group relative flex flex-col items-center gap-2 p-3',
          'rounded-xl transition-all duration-200',
          'hover:bg-amber-500/10 dark:hover:bg-amber-500/20'
        )}
      >
        {/* Game Cover Placeholder */}
        <div
          className={cn(
            'relative h-16 w-16 rounded-lg overflow-hidden',
            'bg-gradient-to-br from-stone-200 to-stone-300',
            'dark:from-stone-700 dark:to-stone-800',
            'ring-2 ring-stone-300/50 dark:ring-stone-600/50',
            'group-hover:ring-amber-500/50 transition-all',
            'shadow-md group-hover:shadow-lg'
          )}
        >
          {game.imageUrl ? (
            // eslint-disable-next-line @next/next/no-img-element
            <img src={game.imageUrl} alt={game.name} className="h-full w-full object-cover" />
          ) : (
            <div className="flex h-full w-full items-center justify-center">
              <Dices className="h-6 w-6 text-stone-400 dark:text-stone-500" />
            </div>
          )}
          {/* Play count badge */}
          <div className="absolute -bottom-1 -right-1 flex h-5 min-w-5 items-center justify-center rounded-full bg-emerald-500 px-1 text-[10px] font-bold text-white shadow-sm">
            {game.playCount}
          </div>
        </div>
        <span className="text-xs font-medium text-center line-clamp-2 max-w-[80px]">
          {game.name}
        </span>
        {game.lastPlayed && (
          <span className="text-[10px] text-muted-foreground">
            {formatRelativeTime(game.lastPlayed)}
          </span>
        )}
      </Link>
    </motion.div>
  );
}

/** Activity item in timeline */
function ActivityRow({ activity, index }: { activity: RecentActivity; index: number }) {
  const typeConfig = {
    played: { icon: Dices, color: 'text-emerald-500' },
    added: { icon: Plus, color: 'text-amber-500' },
    shared: { icon: Users, color: 'text-blue-500' },
    chat: { icon: Sparkles, color: 'text-purple-500' },
  };
  const config = typeConfig[activity.type];
  const Icon = config.icon;

  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: 0.3 + index * 0.05 }}
      className="flex items-start gap-3 py-2"
    >
      <div className={cn('mt-0.5 p-1.5 rounded-lg bg-muted/50', config.color)}>
        <Icon className="h-3.5 w-3.5" />
      </div>
      <div className="flex-1 min-w-0">
        <p className="text-sm font-medium truncate">{activity.title}</p>
        {activity.subtitle && (
          <p className="text-xs text-muted-foreground truncate">{activity.subtitle}</p>
        )}
      </div>
      <span className="text-xs text-muted-foreground whitespace-nowrap">
        {formatRelativeTime(activity.timestamp)}
      </span>
    </motion.div>
  );
}

// ============================================================================
// ActionBar Component (Floating Game Piece Style)
// ============================================================================

interface ActionBarAction {
  id: string;
  icon: LucideIcon;
  label: string;
  href?: string;
  onClick?: () => void;
  variant?: 'default' | 'primary';
}

const DEFAULT_ACTIONS: ActionBarAction[] = [
  { id: 'add', icon: Plus, label: 'Aggiungi', href: '/library/add', variant: 'primary' },
  { id: 'search', icon: Search, label: 'Cerca', href: '/search' },
  { id: 'session', icon: Dices, label: 'Partita', href: '/toolkit' },
  { id: 'chat', icon: MessageSquare, label: 'Chat AI', href: '/chat' },
];

function DashboardActionBar({ actions = DEFAULT_ACTIONS }: { actions?: ActionBarAction[] }) {
  return (
    <motion.div
      initial={{ y: 100, opacity: 0 }}
      animate={{ y: 0, opacity: 1 }}
      transition={{ delay: 0.5, type: 'spring', stiffness: 200, damping: 25 }}
      className={cn(
        'fixed bottom-4 left-1/2 -translate-x-1/2 z-50',
        'flex items-center gap-1 p-1.5',
        // Wood grain inspired styling
        'rounded-2xl',
        'bg-gradient-to-b from-stone-800 to-stone-900',
        'dark:from-stone-900 dark:to-stone-950',
        'border border-stone-700/50',
        'shadow-[0_8px_32px_rgba(0,0,0,0.35),inset_0_1px_0_rgba(255,255,255,0.1)]',
        // Subtle wood texture
        'before:absolute before:inset-0 before:rounded-2xl',
        'before:bg-[url("data:image/svg+xml,%3Csvg viewBox=\'0 0 200 200\' xmlns=\'http://www.w3.org/2000/svg\'%3E%3Cfilter id=\'noise\'%3E%3CfeTurbulence type=\'fractalNoise\' baseFrequency=\'0.85\' numOctaves=\'4\' stitchTiles=\'stitch\'/%3E%3C/filter%3E%3Crect width=\'100%25\' height=\'100%25\' filter=\'url(%23noise)\' opacity=\'0.04\'/%3E%3C/svg%3E")]',
        'before:opacity-50 before:pointer-events-none'
      )}
      role="toolbar"
      aria-label="Azioni rapide"
    >
      {actions.map((action) => {
        const Icon = action.icon;
        const isPrimary = action.variant === 'primary';

        const buttonContent = (
          <motion.button
            key={action.id}
            whileHover={{ scale: 1.08 }}
            whileTap={{ scale: 0.95 }}
            className={cn(
              'relative flex items-center gap-2 px-4 py-2.5 rounded-xl',
              'transition-all duration-200',
              'focus:outline-none focus:ring-2 focus:ring-amber-500/50',
              isPrimary
                ? [
                    'bg-gradient-to-br from-amber-500 to-amber-600',
                    'text-white font-semibold',
                    'shadow-[0_4px_12px_rgba(245,158,11,0.4)]',
                    'hover:shadow-[0_6px_20px_rgba(245,158,11,0.5)]',
                  ]
                : [
                    'text-stone-300 hover:text-white',
                    'hover:bg-stone-700/50',
                  ]
            )}
            onClick={action.onClick}
          >
            <Icon className={cn('h-4 w-4', isPrimary && 'drop-shadow-sm')} />
            <span className="text-sm hidden sm:inline">{action.label}</span>
          </motion.button>
        );

        if (action.href) {
          return (
            <Link key={action.id} href={action.href}>
              {buttonContent}
            </Link>
          );
        }
        return buttonContent;
      })}
    </motion.div>
  );
}

// ============================================================================
// Main Dashboard Component
// ============================================================================

export function UserDashboardCompact() {
  const [userName] = useState('Marco');
  const greeting = useMemo(() => getGreeting(), []);

  return (
    <div className="relative min-h-screen pb-24">
      {/* Subtle Background Texture */}
      <div
        className={cn(
          'fixed inset-0 -z-10',
          'bg-gradient-to-b from-stone-50 via-amber-50/30 to-stone-100',
          'dark:from-stone-950 dark:via-amber-950/10 dark:to-stone-900'
        )}
      />

      {/* Main Content */}
      <div className="mx-auto max-w-3xl px-4 py-6 space-y-6">
        {/* Hero Section - Greeting + Stats */}
        <section className="space-y-4">
          <motion.div
            initial={{ opacity: 0, y: -10 }}
            animate={{ opacity: 1, y: 0 }}
            className="space-y-1"
          >
            <h1 className="font-heading text-2xl font-bold tracking-tight sm:text-3xl">
              {greeting}, {userName}!
            </h1>
            <p className="text-sm text-muted-foreground flex items-center gap-1.5">
              <Clock className="h-3.5 w-3.5" />
              Ultimo accesso: 2 ore fa
            </p>
          </motion.div>

          {/* Stats Grid */}
          <div className="grid grid-cols-2 gap-3 sm:grid-cols-4">
            {MOCK_STATS.map((stat, index) => (
              <StatCardCompact key={stat.id} stat={stat} index={index} />
            ))}
          </div>
        </section>

        {/* Quick Access Section */}
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="font-heading text-lg font-semibold flex items-center gap-2">
              <Dices className="h-4 w-4 text-amber-600 dark:text-amber-400" />
              Giochi Rapidi
            </h2>
            <Link
              href="/library"
              className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-0.5 transition-colors"
            >
              Vedi tutti
              <ChevronRight className="h-3 w-3" />
            </Link>
          </div>

          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.15 }}
            className={cn(
              'rounded-2xl p-2',
              'bg-card/60 backdrop-blur-sm',
              'border border-border/50'
            )}
          >
            <div className="grid grid-cols-4 sm:grid-cols-4">
              {MOCK_QUICK_GAMES.map((game, index) => (
                <QuickGameCard key={game.id} game={game} index={index} />
              ))}
            </div>
          </motion.div>
        </section>

        {/* Recent Activity Section */}
        <section className="space-y-3">
          <div className="flex items-center justify-between">
            <h2 className="font-heading text-lg font-semibold flex items-center gap-2">
              <Clock className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
              Attività Recente
            </h2>
            <Link
              href="/activity"
              className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-0.5 transition-colors"
            >
              Cronologia
              <ChevronRight className="h-3 w-3" />
            </Link>
          </div>

          <motion.div
            initial={{ opacity: 0 }}
            animate={{ opacity: 1 }}
            transition={{ delay: 0.25 }}
            className={cn(
              'rounded-2xl p-4',
              'bg-card/60 backdrop-blur-sm',
              'border border-border/50',
              'divide-y divide-border/30'
            )}
          >
            {MOCK_ACTIVITIES.map((activity, index) => (
              <ActivityRow key={activity.id} activity={activity} index={index} />
            ))}
          </motion.div>
        </section>

        {/* AI Insight Card (Compact) */}
        <motion.section
          initial={{ opacity: 0, y: 20 }}
          animate={{ opacity: 1, y: 0 }}
          transition={{ delay: 0.4 }}
        >
          <Link
            href="/chat"
            className={cn(
              'group block p-4 rounded-2xl',
              'bg-gradient-to-br from-purple-500/10 via-blue-500/10 to-purple-500/10',
              'dark:from-purple-500/20 dark:via-blue-500/20 dark:to-purple-500/20',
              'border border-purple-500/20',
              'hover:border-purple-500/40 transition-all duration-300',
              'hover:shadow-lg hover:shadow-purple-500/10'
            )}
          >
            <div className="flex items-center gap-3">
              <div className="p-2 rounded-xl bg-purple-500/20">
                <Sparkles className="h-5 w-5 text-purple-600 dark:text-purple-400" />
              </div>
              <div className="flex-1 min-w-0">
                <h3 className="font-semibold text-sm">Suggerimento AI</h3>
                <p className="text-xs text-muted-foreground truncate">
                  Basandoti sulle tue ultime partite, potresti provare Spirit Island!
                </p>
              </div>
              <ChevronRight className="h-4 w-4 text-muted-foreground group-hover:translate-x-1 transition-transform" />
            </div>
          </Link>
        </motion.section>
      </div>

      {/* Floating Action Bar */}
      <DashboardActionBar />
    </div>
  );
}

export default UserDashboardCompact;
