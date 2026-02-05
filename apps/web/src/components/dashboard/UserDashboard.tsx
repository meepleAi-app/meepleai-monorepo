/**
 * UserDashboard - Unified Dashboard with Compact/Extended Mode Toggle
 *
 * A single dashboard component that seamlessly transitions between:
 * - Compact: Single-scroll, focused view with floating ActionBar
 * - Extended: Full-featured view with reorderable sections
 *
 * Design Philosophy:
 * - "Warm Tabletop" aesthetic with wood grain textures
 * - Typography: Playfair Display + Geist Sans
 * - Colors: Amber (wood), Emerald (felt), Stone (linen)
 * - Smooth mode transitions with Framer Motion
 *
 * @example
 * ```tsx
 * <UserDashboard />
 * <UserDashboard defaultMode="extended" />
 * ```
 */

'use client';

import { useState, useMemo, useCallback, useRef, useEffect } from 'react';

import { motion, AnimatePresence, Reorder, LayoutGroup } from 'framer-motion';
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
  Zap,
  Gamepad2,
  Heart,
  Bell,
  FileText,
  Bot,
  Filter,
  Maximize2,
  Minimize2,
  type LucideIcon,
} from 'lucide-react';
import Link from 'next/link';

import { useLayout } from '@/components/layout/LayoutProvider';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

import { ActiveSessionsWidget } from './ActiveSessionsWidget';
import { ActivityFeed } from './ActivityFeed';
import { ActivityItem, type ActivityData } from './ActivityItem';
import { ChatHistorySection } from './ChatHistorySection';
import { DashboardHeader } from './DashboardHeader';
import { DashboardSection, type ViewMode } from './DashboardSection';
import { GameCard, type GameData } from './GameCard';
import { HeroStats } from './HeroStats';
import { LibrarySnapshot } from './LibrarySnapshot';
import { NotificationItem, type NotificationData } from './NotificationItem';
import { QuickActionCard } from './QuickActionCard';
import { QuickActionsGrid } from './QuickActionsGrid';
import { WishlistCard, type WishlistItemData } from './WishlistCard';

// ============================================================================
// Types
// ============================================================================

export type DashboardMode = 'compact' | 'extended';

export interface UserDashboardProps {
  /** Initial dashboard mode */
  defaultMode?: DashboardMode;
  /** Callback when mode changes */
  onModeChange?: (mode: DashboardMode) => void;
  /** User name for greeting */
  userName?: string;
}

/** Section configuration for extended mode */
export interface SectionConfig {
  id: string;
  title: string;
  icon: LucideIcon;
  viewMode: ViewMode;
  collapsed: boolean;
  order: number;
}

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

interface ActionBarAction {
  id: string;
  icon: LucideIcon;
  label: string;
  href?: string;
  onClick?: () => void;
  variant?: 'default' | 'primary';
}

interface QuickActionData {
  icon: LucideIcon;
  title: string;
  description: string;
  onClick: () => void;
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

const MOCK_EXTENDED_ACTIVITIES: ActivityData[] = [
  { id: '1', type: 'game_added', title: 'Wingspan aggiunto alla collezione', timestamp: new Date(Date.now() - 1000 * 60 * 30), game: { id: 'g1', name: 'Wingspan' } },
  { id: '2', type: 'session_played', title: 'Partita a Catan completata', timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2), game: { id: 'g2', name: 'Catan' }, details: 'Vincitore: Mario • 4 giocatori • 90 min' },
  { id: '3', type: 'ai_interaction', title: "Domanda all'AI su Gloomhaven", timestamp: new Date(Date.now() - 1000 * 60 * 60 * 5), game: { id: 'g3', name: 'Gloomhaven' } },
  { id: '4', type: 'share_received', title: 'Luigi ti ha condiviso Terraforming Mars', timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24), game: { id: 'g4', name: 'Terraforming Mars' }, user: { id: 'u1', name: 'Luigi' } },
];

const MOCK_GAMES: GameData[] = [
  { id: 'g1', name: 'Wingspan', imageUrl: '/images/games/wingspan.jpg', rating: 9, playCount: 12, lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3), isFavorite: true, ownershipStatus: 'OWNED' },
  { id: 'g2', name: 'Catan', imageUrl: '/images/games/catan.jpg', rating: 8, playCount: 45, lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 2), isFavorite: false, ownershipStatus: 'OWNED' },
  { id: 'g3', name: 'Gloomhaven', imageUrl: '/images/games/gloomhaven.jpg', rating: 10, playCount: 8, lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7), isFavorite: true, ownershipStatus: 'OWNED', location: 'Scaffale B' },
  { id: 'g4', name: 'Terraforming Mars', imageUrl: '/images/games/terraforming.jpg', rating: 9, playCount: 15, isFavorite: false, ownershipStatus: 'LENT_OUT', location: 'Da Mario' },
  { id: 'g5', name: 'Azul', imageUrl: '/images/games/azul.jpg', rating: 8, playCount: 20, lastPlayedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2), isFavorite: false, ownershipStatus: 'OWNED' },
  { id: 'g6', name: 'Scythe', imageUrl: '/images/games/scythe.jpg', rating: 9, playCount: 6, isFavorite: true, ownershipStatus: 'OWNED' },
];

const MOCK_SHARED_GAMES = {
  sharedByMe: MOCK_GAMES.slice(0, 2),
  sharedWithMe: MOCK_GAMES.slice(2, 4),
  pending: MOCK_GAMES.slice(4, 5),
};

const MOCK_WISHLIST: WishlistItemData[] = [
  { id: 'w1', game: { id: 'wg1', name: 'Ark Nova', imageUrl: '/images/games/arknova.jpg' }, priority: 'HIGH', targetPrice: 55, addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 10), visibility: 'FRIENDS' },
  { id: 'w2', game: { id: 'wg2', name: 'Spirit Island', imageUrl: '/images/games/spirit.jpg' }, priority: 'MEDIUM', notes: 'Espansione Jagged Earth inclusa', addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 30), visibility: 'PRIVATE' },
  { id: 'w3', game: { id: 'wg3', name: 'Brass Birmingham', imageUrl: '/images/games/brass.jpg' }, priority: 'LOW', addedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 60), visibility: 'PUBLIC' },
];

const MOCK_NOTIFICATIONS: NotificationData[] = [
  { id: 'n1', type: 'SHARE', title: 'Mario ti ha condiviso Catan', body: 'Puoi ora accedere alle regole e ai documenti.', status: 'UNREAD', isActionable: true, actions: [{ id: 'accept', label: 'Accetta', actionType: 'ACCEPT' }, { id: 'reject', label: 'Rifiuta', actionType: 'REJECT' }], createdAt: new Date(Date.now() - 1000 * 60 * 15) },
  { id: 'n2', type: 'LOAN', title: 'Promemoria prestito', body: 'Gloomhaven è in prestito a Luigi da 30 giorni.', status: 'UNREAD', isActionable: true, actions: [{ id: 'remind', label: 'Invia promemoria', actionType: 'REMIND' }, { id: 'dismiss', label: 'Ignora', actionType: 'DISMISS' }], createdAt: new Date(Date.now() - 1000 * 60 * 60 * 2) },
];

const MOCK_QUICK_ACTIONS: QuickActionData[] = [
  { icon: Plus, title: 'Aggiungi Gioco', description: 'Cerca o crea un nuovo gioco', onClick: () => {} },
  { icon: FileText, title: 'Carica Regolamento', description: 'Upload PDF o documento', onClick: () => {} },
  { icon: Bot, title: "Chiedi all'AI", description: 'Assistente regole e strategie', onClick: () => {} },
  { icon: Dices, title: 'Nuova Partita', description: 'Registra una sessione di gioco', onClick: () => {} },
];

const DEFAULT_SECTIONS: SectionConfig[] = [
  { id: 'quick-actions', title: 'Azioni Rapide', icon: Zap, viewMode: 'grid', collapsed: false, order: 0 },
  { id: 'recent-activity', title: 'Attività Recente', icon: Clock, viewMode: 'list', collapsed: false, order: 1 },
  { id: 'collection', title: 'La Mia Collezione', icon: Gamepad2, viewMode: 'grid', collapsed: false, order: 2 },
  { id: 'shared-games', title: 'Giochi Condivisi', icon: Users, viewMode: 'grid', collapsed: false, order: 3 },
  { id: 'wishlist', title: 'Lista Desideri', icon: Heart, viewMode: 'grid', collapsed: false, order: 4 },
  { id: 'notifications', title: 'Notifiche', icon: Bell, viewMode: 'list', collapsed: false, order: 5 },
];

const DEFAULT_ACTIONS: ActionBarAction[] = [
  { id: 'add', icon: Plus, label: 'Aggiungi', href: '/library/add', variant: 'primary' },
  { id: 'search', icon: Search, label: 'Cerca', href: '/search' },
  { id: 'session', icon: Dices, label: 'Partita', href: '/toolkit' },
  { id: 'chat', icon: MessageSquare, label: 'Chat AI', href: '/chat' },
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
// Compact Mode Sub-Components
// ============================================================================

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
// Mode Toggle Component
// ============================================================================

function ModeToggle({
  mode,
  onToggle
}: {
  mode: DashboardMode;
  onToggle: () => void;
}) {
  return (
    <motion.button
      onClick={onToggle}
      whileHover={{ scale: 1.05 }}
      whileTap={{ scale: 0.95 }}
      className={cn(
        'flex items-center gap-2 px-3 py-1.5 rounded-xl',
        'text-sm font-medium',
        'bg-stone-100 dark:bg-stone-800',
        'border border-stone-200 dark:border-stone-700',
        'hover:bg-stone-200 dark:hover:bg-stone-700',
        'transition-all duration-200',
        'shadow-sm hover:shadow'
      )}
      aria-label={mode === 'compact' ? 'Espandi dashboard' : 'Comprimi dashboard'}
    >
      <AnimatePresence mode="wait">
        <motion.span
          key={mode}
          initial={{ opacity: 0, rotate: -90 }}
          animate={{ opacity: 1, rotate: 0 }}
          exit={{ opacity: 0, rotate: 90 }}
          transition={{ duration: 0.15 }}
          className="flex items-center gap-1.5"
        >
          {mode === 'compact' ? (
            <>
              <Maximize2 className="h-4 w-4 text-amber-600 dark:text-amber-400" />
              <span className="hidden sm:inline text-muted-foreground">Espandi</span>
            </>
          ) : (
            <>
              <Minimize2 className="h-4 w-4 text-emerald-600 dark:text-emerald-400" />
              <span className="hidden sm:inline text-muted-foreground">Compatta</span>
            </>
          )}
        </motion.span>
      </AnimatePresence>
    </motion.button>
  );
}

// ============================================================================
// Floating ActionBar (Compact Mode)
// ============================================================================

function FloatingActionBar({
  actions = DEFAULT_ACTIONS,
  visible = true
}: {
  actions?: ActionBarAction[];
  visible?: boolean;
}) {
  return (
    <AnimatePresence>
      {visible && (
        <motion.div
          initial={{ y: 100, opacity: 0 }}
          animate={{ y: 0, opacity: 1 }}
          exit={{ y: 100, opacity: 0 }}
          transition={{ type: 'spring', stiffness: 200, damping: 25 }}
          className={cn(
            'fixed bottom-4 left-1/2 -translate-x-1/2 z-50',
            'flex items-center gap-1 p-1.5',
            'rounded-2xl',
            'bg-gradient-to-b from-stone-800 to-stone-900',
            'dark:from-stone-900 dark:to-stone-950',
            'border border-stone-700/50',
            'shadow-[0_8px_32px_rgba(0,0,0,0.35),inset_0_1px_0_rgba(255,255,255,0.1)]',
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
      )}
    </AnimatePresence>
  );
}

// ============================================================================
// Compact Dashboard View
// ============================================================================

function CompactDashboardView({ userName }: { userName: string }) {
  const greeting = useMemo(() => getGreeting(), []);

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
      className="mx-auto max-w-3xl px-4 py-6 space-y-6"
    >
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
    </motion.div>
  );
}

// ============================================================================
// Extended Dashboard View
// ============================================================================

function ExtendedDashboardView() {
  const { responsive, registerActions, clearActions } = useLayout();
  const { isMobile } = responsive;

  const [sections, setSections] = useState<SectionConfig[]>(DEFAULT_SECTIONS);
  const [activeSectionId, setActiveSectionId] = useState<string | null>(null);
  const [isDragging, setIsDragging] = useState(false);
  const [collectionFilter, setCollectionFilter] = useState<'all' | 'mine' | 'accessible' | 'lent'>('all');
  const [sharedTab, setSharedTab] = useState<'byMe' | 'withMe' | 'pending'>('byMe');

  const sectionRefs = useRef<Map<string, HTMLElement>>(new Map());

  const handleViewModeChange = useCallback((sectionId: string, mode: ViewMode) => {
    setSections((prev) =>
      prev.map((s) => (s.id === sectionId ? { ...s, viewMode: mode } : s))
    );
  }, []);

  const handleCollapseToggle = useCallback((sectionId: string) => {
    setSections((prev) =>
      prev.map((s) => (s.id === sectionId ? { ...s, collapsed: !s.collapsed } : s))
    );
  }, []);

  const handleSectionReorder = useCallback((newSections: SectionConfig[]) => {
    setSections(newSections.map((s, i) => ({ ...s, order: i })));
  }, []);

  // Intersection Observer
  useEffect(() => {
    const observers = new Map<string, IntersectionObserver>();

    sections.forEach((section) => {
      const element = sectionRefs.current.get(section.id);
      if (!element) return;

      const observer = new IntersectionObserver(
        (entries) => {
          entries.forEach((entry) => {
            if (entry.isIntersecting && entry.intersectionRatio >= 0.3) {
              setActiveSectionId(section.id);
            }
          });
        },
        { threshold: [0.3, 0.5, 0.7], rootMargin: '-100px 0px -40% 0px' }
      );

      observer.observe(element);
      observers.set(section.id, observer);
    });

    return () => {
      observers.forEach((observer) => observer.disconnect());
    };
  }, [sections]);

  // Register context-aware actions
  useEffect(() => {
    const activeSection = sections.find((s) => s.id === activeSectionId);
    if (!activeSection) {
      clearActions();
      return;
    }

    const sectionActions = getSectionActions(activeSection.id);
    registerActions(sectionActions);

    return () => clearActions();
  }, [activeSectionId, sections, registerActions, clearActions]);

  const filteredGames = useMemo(() => {
    switch (collectionFilter) {
      case 'mine':
        return MOCK_GAMES.filter((g) => g.ownershipStatus === 'OWNED');
      case 'accessible':
        return MOCK_GAMES;
      case 'lent':
        return MOCK_GAMES.filter((g) => g.ownershipStatus === 'LENT_OUT');
      default:
        return MOCK_GAMES;
    }
  }, [collectionFilter]);

  const sharedGames = useMemo(() => {
    switch (sharedTab) {
      case 'byMe':
        return MOCK_SHARED_GAMES.sharedByMe;
      case 'withMe':
        return MOCK_SHARED_GAMES.sharedWithMe;
      case 'pending':
        return MOCK_SHARED_GAMES.pending;
      default:
        return [];
    }
  }, [sharedTab]);

  const renderSectionContent = useCallback(
    (section: SectionConfig) => {
      switch (section.id) {
        case 'quick-actions':
          return (
            <div className="grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-4">
              {MOCK_QUICK_ACTIONS.map((action) => (
                <QuickActionCard
                  key={action.title}
                  icon={action.icon}
                  title={action.title}
                  description={action.description}
                  onClick={action.onClick}
                />
              ))}
            </div>
          );

        case 'recent-activity':
          return (
            <div className="space-y-2">
              <AnimatePresence mode="popLayout">
                {MOCK_EXTENDED_ACTIVITIES.map((activity, index) => (
                  <motion.div
                    key={activity.id}
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, x: -20 }}
                    transition={{ delay: index * 0.05 }}
                  >
                    <ActivityItem data={activity} />
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          );

        case 'collection':
          return (
            <>
              <div className="mb-4 flex gap-2 overflow-x-auto pb-2">
                {(['all', 'mine', 'accessible', 'lent'] as const).map((filter) => (
                  <Button
                    key={filter}
                    variant={collectionFilter === filter ? 'default' : 'outline'}
                    size="sm"
                    onClick={() => setCollectionFilter(filter)}
                    className="shrink-0"
                  >
                    {filter === 'all' && 'Tutti'}
                    {filter === 'mine' && 'Miei'}
                    {filter === 'accessible' && 'Accessibili'}
                    {filter === 'lent' && 'Prestati a me'}
                  </Button>
                ))}
              </div>

              <div
                className={cn(
                  section.viewMode === 'grid'
                    ? 'grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4 xl:grid-cols-5'
                    : 'space-y-3'
                )}
              >
                <AnimatePresence mode="popLayout">
                  {filteredGames.map((game, index) => (
                    <motion.div
                      key={game.id}
                      layout
                      initial={{ opacity: 0, scale: 0.95 }}
                      animate={{ opacity: 1, scale: 1 }}
                      exit={{ opacity: 0, scale: 0.95 }}
                      transition={{ delay: index * 0.03 }}
                    >
                      <GameCard
                        data={game}
                        viewMode={section.viewMode}
                        onAskAI={() => {}}
                        onClick={() => {}}
                      />
                    </motion.div>
                  ))}
                </AnimatePresence>
              </div>
            </>
          );

        case 'shared-games':
          return (
            <>
              <div className="mb-4 flex gap-2 border-b border-border">
                {([
                  { key: 'byMe', label: 'Condivisi da me', count: MOCK_SHARED_GAMES.sharedByMe.length },
                  { key: 'withMe', label: 'Condivisi con me', count: MOCK_SHARED_GAMES.sharedWithMe.length },
                  { key: 'pending', label: 'In attesa', count: MOCK_SHARED_GAMES.pending.length },
                ] as const).map((tab) => (
                  <button
                    key={tab.key}
                    onClick={() => setSharedTab(tab.key)}
                    className={cn(
                      'relative px-4 py-2 text-sm font-medium transition-colors',
                      sharedTab === tab.key
                        ? 'text-primary'
                        : 'text-muted-foreground hover:text-foreground'
                    )}
                  >
                    {tab.label} ({tab.count})
                    {sharedTab === tab.key && (
                      <motion.div
                        layoutId="shared-tab-indicator"
                        className="absolute inset-x-0 -bottom-px h-0.5 bg-primary"
                      />
                    )}
                  </button>
                ))}
              </div>

              <div
                className={cn(
                  section.viewMode === 'grid'
                    ? 'grid grid-cols-2 gap-4 sm:grid-cols-3 lg:grid-cols-4'
                    : 'space-y-3'
                )}
              >
                <AnimatePresence mode="popLayout">
                  {sharedGames.map((game, index) => (
                    <motion.div
                      key={game.id}
                      layout
                      initial={{ opacity: 0, scale: 0.95 }}
                      animate={{ opacity: 1, scale: 1 }}
                      exit={{ opacity: 0, scale: 0.95 }}
                      transition={{ delay: index * 0.03 }}
                    >
                      <GameCard
                        data={game}
                        viewMode={section.viewMode}
                        onAskAI={() => {}}
                        onClick={() => {}}
                      />
                    </motion.div>
                  ))}
                </AnimatePresence>
              </div>
            </>
          );

        case 'wishlist':
          return (
            <div
              className={cn(
                section.viewMode === 'grid'
                  ? 'grid grid-cols-1 gap-4 sm:grid-cols-2 lg:grid-cols-3'
                  : 'space-y-3'
              )}
            >
              <AnimatePresence mode="popLayout">
                {MOCK_WISHLIST.map((item, index) => (
                  <motion.div
                    key={item.id}
                    layout
                    initial={{ opacity: 0, y: 20 }}
                    animate={{ opacity: 1, y: 0 }}
                    exit={{ opacity: 0, x: -20 }}
                    transition={{ delay: index * 0.05 }}
                  >
                    <WishlistCard
                      data={item}
                      viewMode={section.viewMode}
                      onAcquire={() => {}}
                      onClick={() => {}}
                    />
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          );

        case 'notifications':
          return (
            <div className="space-y-2">
              <AnimatePresence mode="popLayout">
                {MOCK_NOTIFICATIONS.map((notification, index) => (
                  <motion.div
                    key={notification.id}
                    initial={{ opacity: 0, x: -20 }}
                    animate={{ opacity: 1, x: 0 }}
                    exit={{ opacity: 0, x: 20 }}
                    transition={{ delay: index * 0.05 }}
                  >
                    <NotificationItem
                      data={notification}
                      onAction={() => {}}
                      onDismiss={() => {}}
                    />
                  </motion.div>
                ))}
              </AnimatePresence>
            </div>
          );

        default:
          return null;
      }
    },
    [collectionFilter, filteredGames, sharedTab, sharedGames]
  );

  return (
    <motion.div
      initial={{ opacity: 0 }}
      animate={{ opacity: 1 }}
      exit={{ opacity: 0 }}
    >
      {/* Dashboard Header */}
      <DashboardHeader />

      {/* Hero Stats Section */}
      <div className="mx-auto max-w-7xl px-4 py-6 sm:px-6 lg:px-8">
        <HeroStats />
      </div>

      {/* Dashboard Widgets */}
      <div className="mx-auto max-w-7xl px-4 pb-6 sm:px-6 lg:px-8 space-y-4">
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <ActiveSessionsWidget />
          <LibrarySnapshot />
        </div>
        <div className="grid grid-cols-1 gap-4 lg:grid-cols-2">
          <ActivityFeed />
          <ChatHistorySection />
        </div>
        <QuickActionsGrid />
      </div>

      {/* Reorderable Sections */}
      <Reorder.Group
        axis="y"
        values={sections}
        onReorder={handleSectionReorder}
        className="space-y-6 px-4 py-6 sm:px-6 lg:px-8"
      >
        <AnimatePresence>
          {sections.map((section) => (
            <Reorder.Item
              key={section.id}
              value={section}
              dragListener={!isMobile}
              onDragStart={() => setIsDragging(true)}
              onDragEnd={() => setIsDragging(false)}
              whileDrag={{ scale: 1.02, boxShadow: '0 10px 30px rgba(139, 90, 60, 0.15)' }}
            >
              <motion.div
                ref={(el) => {
                  if (el) sectionRefs.current.set(section.id, el);
                }}
                layout
                initial={{ opacity: 0, y: 20 }}
                animate={{ opacity: 1, y: 0 }}
                exit={{ opacity: 0, y: -20 }}
              >
                <DashboardSection
                  id={section.id}
                  title={section.title}
                  icon={section.icon}
                  viewMode={section.viewMode}
                  collapsed={section.collapsed}
                  isActive={activeSectionId === section.id}
                  isDragging={isDragging}
                  onViewModeChange={(mode) => handleViewModeChange(section.id, mode)}
                  onCollapseToggle={() => handleCollapseToggle(section.id)}
                  showViewToggle={!['quick-actions', 'recent-activity', 'notifications'].includes(section.id)}
                >
                  {renderSectionContent(section)}
                </DashboardSection>
              </motion.div>
            </Reorder.Item>
          ))}
        </AnimatePresence>
      </Reorder.Group>
    </motion.div>
  );
}

// ============================================================================
// Helper Functions
// ============================================================================

function getSectionActions(sectionId: string) {
  const baseActions = [
    { id: 'search', label: 'Cerca', icon: Search, priority: 1, onClick: () => {} },
    { id: 'filter', label: 'Filtra', icon: Filter, priority: 2, onClick: () => {} },
  ];

  switch (sectionId) {
    case 'collection':
      return [
        { id: 'add', label: 'Aggiungi', icon: Plus, priority: 0, variant: 'primary' as const, onClick: () => {} },
        ...baseActions,
      ];
    case 'shared-games':
      return [
        { id: 'share', label: 'Condividi', icon: Users, priority: 0, variant: 'primary' as const, onClick: () => {} },
        ...baseActions,
      ];
    case 'wishlist':
      return [
        { id: 'add', label: 'Aggiungi', icon: Plus, priority: 0, variant: 'primary' as const, onClick: () => {} },
        ...baseActions,
      ];
    default:
      return baseActions;
  }
}

// ============================================================================
// Main Component
// ============================================================================

export function UserDashboard({
  defaultMode = 'compact',
  onModeChange,
  userName = 'Marco'
}: UserDashboardProps) {
  const [mode, setMode] = useState<DashboardMode>(defaultMode);

  const handleModeToggle = useCallback(() => {
    const newMode = mode === 'compact' ? 'extended' : 'compact';
    setMode(newMode);
    onModeChange?.(newMode);
  }, [mode, onModeChange]);

  return (
    <LayoutGroup>
      <div className="relative min-h-screen pb-24">
        {/* Background Texture */}
        <div
          className={cn(
            'fixed inset-0 -z-10',
            'bg-gradient-to-b from-stone-50 via-amber-50/30 to-stone-100',
            'dark:from-stone-950 dark:via-amber-950/10 dark:to-stone-900'
          )}
        />

        {/* Mode Toggle - Fixed Position */}
        <div className="sticky top-0 z-40 flex justify-end px-4 py-3 sm:px-6">
          <ModeToggle mode={mode} onToggle={handleModeToggle} />
        </div>

        {/* Dashboard Content */}
        <AnimatePresence mode="wait">
          {mode === 'compact' ? (
            <CompactDashboardView key="compact" userName={userName} />
          ) : (
            <ExtendedDashboardView key="extended" />
          )}
        </AnimatePresence>

        {/* Floating ActionBar (only in compact mode) */}
        <FloatingActionBar visible={mode === 'compact'} />
      </div>
    </LayoutGroup>
  );
}

export default UserDashboard;
