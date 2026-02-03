/**
 * HeroStats - Hero Section with KPI Cards
 * Issue #3308 - Implement HeroStats component with 4 KPI cards
 *
 * Features:
 * - Personalized greeting with user name
 * - Last access timestamp
 * - 4 glassmorphic KPI cards:
 *   - Collection: total + monthly trend
 *   - Played: 30d count + streak indicator
 *   - Chats: 7d conversations
 *   - Wishlist: total + monthly trend
 * - Responsive grid: 2-col mobile, 4-col desktop
 * - Skeleton loading states
 *
 * @example
 * ```tsx
 * <HeroStats
 *   stats={dashboardStats}
 *   isLoading={isLoading}
 * />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { motion } from 'framer-motion';
import { Library, Dices, MessageSquare, Star } from 'lucide-react';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { cn } from '@/lib/utils';

import { KpiCard, KpiCardSkeleton, type KpiColorVariant } from './KpiCard';

// ============================================================================
// Types
// ============================================================================

export interface DashboardStats {
  collection: {
    total: number;
    trend: number;
  };
  played: {
    total: number;
    streak: number;
  };
  chats: {
    total: number;
  };
  wishlist: {
    total: number;
    trend: number;
  };
  lastAccess: string;
  userName: string;
}

export interface HeroStatsProps {
  /** Dashboard statistics data */
  stats?: DashboardStats;
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_STATS: DashboardStats = {
  collection: { total: 127, trend: 3 },
  played: { total: 23, streak: 7 },
  chats: { total: 12 },
  wishlist: { total: 15, trend: 2 },
  lastAccess: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(),
  userName: 'Marco',
};

// ============================================================================
// Helper Functions
// ============================================================================

function formatLastAccess(isoDate: string): string {
  const date = new Date(isoDate);
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMinutes = Math.floor(diffMs / (1000 * 60));
  const diffDays = Math.floor(diffMs / (1000 * 60 * 60 * 24));

  if (diffMinutes < 60) {
    return `${diffMinutes} minuti fa`;
  }

  const isToday = date.toDateString() === now.toDateString();
  const isYesterday =
    date.toDateString() ===
    new Date(now.getTime() - 1000 * 60 * 60 * 24).toDateString();

  const timeStr = date.toLocaleTimeString('it-IT', {
    hour: '2-digit',
    minute: '2-digit',
  });

  if (isToday) {
    return `Oggi alle ${timeStr}`;
  }

  if (isYesterday) {
    return `Ieri alle ${timeStr}`;
  }

  if (diffDays < 7) {
    return `${diffDays} giorni fa`;
  }

  return date.toLocaleDateString('it-IT', {
    day: 'numeric',
    month: 'long',
  });
}

function getGreeting(): string {
  const hour = new Date().getHours();
  if (hour >= 5 && hour < 12) {
    return 'Buongiorno';
  }
  if (hour >= 12 && hour < 18) {
    return 'Buon pomeriggio';
  }
  return 'Buonasera';
}

// ============================================================================
// Skeleton Component
// ============================================================================

function HeroStatsSkeleton({ className }: { className?: string }) {
  return (
    <div className={cn('space-y-6', className)} data-testid="hero-stats-skeleton">
      {/* Greeting Skeleton */}
      <div className="space-y-2">
        <Skeleton className="h-8 w-48" />
        <Skeleton className="h-5 w-72" />
      </div>

      {/* KPI Cards Skeleton */}
      <div className="grid grid-cols-2 gap-4 lg:grid-cols-4">
        {Array.from({ length: 4 }).map((_, i) => (
          <KpiCardSkeleton key={i} />
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// HeroStats Component
// ============================================================================

export function HeroStats({
  stats = MOCK_STATS,
  isLoading = false,
  className,
}: HeroStatsProps) {
  const greeting = useMemo(() => getGreeting(), []);
  const lastAccessFormatted = useMemo(
    () => formatLastAccess(stats.lastAccess),
    [stats.lastAccess]
  );

  if (isLoading) {
    return <HeroStatsSkeleton className={className} />;
  }

  const kpiCards = [
    {
      key: 'collection',
      icon: Library,
      iconColor: 'amber' as KpiColorVariant,
      value: stats.collection.total,
      label: 'Collezione',
      trend: { value: stats.collection.trend, period: 'mese' },
      href: '/library',
    },
    {
      key: 'played',
      icon: Dices,
      iconColor: 'emerald' as KpiColorVariant,
      value: stats.played.total,
      label: 'Giocati 30gg',
      streak:
        stats.played.streak > 0
          ? { count: stats.played.streak, isActive: true }
          : undefined,
      href: '/sessions/history',
    },
    {
      key: 'chats',
      icon: MessageSquare,
      iconColor: 'blue' as KpiColorVariant,
      value: stats.chats.total,
      label: 'Chat AI 7gg',
      href: '/chat',
    },
    {
      key: 'wishlist',
      icon: Star,
      iconColor: 'purple' as KpiColorVariant,
      value: stats.wishlist.total,
      label: 'Wishlist',
      trend: { value: stats.wishlist.trend, period: 'mese' },
      href: '/wishlist',
    },
  ];

  return (
    <section className={cn('space-y-6', className)} data-testid="hero-stats">
      {/* Greeting */}
      <motion.div
        initial={{ opacity: 0, y: -10 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3 }}
        className="space-y-1"
      >
        <h2
          className="font-playfair text-2xl font-bold tracking-tight sm:text-3xl"
          data-testid="hero-greeting"
        >
          {greeting}, {stats.userName}!
        </h2>
        <p className="text-sm text-muted-foreground" data-testid="hero-last-access">
          Bentornato su MeepleAI - Ultimo accesso: {lastAccessFormatted}
        </p>
      </motion.div>

      {/* KPI Cards Grid */}
      <motion.div
        initial={{ opacity: 0, y: 20 }}
        animate={{ opacity: 1, y: 0 }}
        transition={{ duration: 0.3, delay: 0.1 }}
        className="grid grid-cols-2 gap-4 lg:grid-cols-4"
        data-testid="hero-kpi-grid"
      >
        {kpiCards.map((card, index) => (
          <motion.div
            key={card.key}
            initial={{ opacity: 0, y: 20 }}
            animate={{ opacity: 1, y: 0 }}
            transition={{ duration: 0.3, delay: 0.1 + index * 0.05 }}
          >
            <KpiCard
              icon={card.icon}
              iconColor={card.iconColor}
              value={card.value}
              label={card.label}
              trend={card.trend}
              streak={card.streak}
              href={card.href}
              testId={`kpi-${card.key}`}
            />
          </motion.div>
        ))}
      </motion.div>
    </section>
  );
}

export default HeroStats;
