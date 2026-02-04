/**
 * AchievementsWidget - Dashboard Widget for Recent Achievements
 * Issue #3321 - Implement AchievementsWidget with badges
 *
 * Features:
 * - Shows last 3 unlocked achievements with colored badges
 * - Rarity-based color coding (common, rare, epic, legendary)
 * - Category icons (collection, gameplay, chat, streak, milestone)
 * - Click navigation to achievement detail
 * - CTA to view all badges
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <AchievementsWidget />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { motion } from 'framer-motion';
import {
  Trophy,
  Medal,
  BookOpen,
  Gamepad2,
  MessageSquare,
  Flame,
  Target,
  ChevronRight,
  Sparkles,
} from 'lucide-react';
import Link from 'next/link';

import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type AchievementCategory = 'collection' | 'gameplay' | 'chat' | 'streak' | 'milestone';
export type AchievementRarity = 'common' | 'rare' | 'epic' | 'legendary';

export interface Achievement {
  id: string;
  name: string;
  description: string;
  category: AchievementCategory;
  rarity: AchievementRarity;
  unlockedAt: string;
  icon?: string;
}

export interface AchievementsWidgetProps {
  /** Achievements data */
  achievements?: Achievement[];
  /** Loading state */
  isLoading?: boolean;
  /** Show new unlock animation */
  hasNewUnlock?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Constants
// ============================================================================

const MAX_ACHIEVEMENTS = 3;

const RARITY_COLORS: Record<AchievementRarity, { bg: string; text: string; border: string }> = {
  common: {
    bg: 'bg-gray-100 dark:bg-gray-800/50',
    text: 'text-gray-600 dark:text-gray-400',
    border: 'border-gray-300 dark:border-gray-600',
  },
  rare: {
    bg: 'bg-blue-100 dark:bg-blue-900/30',
    text: 'text-blue-600 dark:text-blue-400',
    border: 'border-blue-300 dark:border-blue-600',
  },
  epic: {
    bg: 'bg-purple-100 dark:bg-purple-900/30',
    text: 'text-purple-600 dark:text-purple-400',
    border: 'border-purple-300 dark:border-purple-600',
  },
  legendary: {
    bg: 'bg-amber-100 dark:bg-amber-900/30',
    text: 'text-amber-600 dark:text-amber-400',
    border: 'border-amber-300 dark:border-amber-600',
  },
};

const RARITY_LABELS: Record<AchievementRarity, string> = {
  common: 'COMMON',
  rare: 'RARE',
  epic: 'EPIC',
  legendary: 'LEGENDARY',
};

const CATEGORY_ICONS: Record<AchievementCategory, React.ElementType> = {
  collection: BookOpen,
  gameplay: Gamepad2,
  chat: MessageSquare,
  streak: Flame,
  milestone: Target,
};

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_ACHIEVEMENTS: Achievement[] = [
  {
    id: 'ach-1',
    name: 'Giocatore Costante',
    description: '7 giorni di streak',
    category: 'streak',
    rarity: 'rare',
    unlockedAt: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), // 2 hours ago
  },
  {
    id: 'ach-2',
    name: 'Collezionista',
    description: '100+ giochi in collezione',
    category: 'collection',
    rarity: 'epic',
    unlockedAt: new Date(Date.now() - 1000 * 60 * 60 * 24).toISOString(), // 1 day ago
  },
  {
    id: 'ach-3',
    name: 'Esperto AI',
    description: '50+ chat completate',
    category: 'chat',
    rarity: 'common',
    unlockedAt: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3).toISOString(), // 3 days ago
  },
];

// ============================================================================
// Skeleton Component
// ============================================================================

function AchievementsWidgetSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="achievements-widget-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8 rounded-lg" />
          <Skeleton className="h-5 w-36" />
        </div>
      </div>

      {/* Subtitle Skeleton */}
      <Skeleton className="h-4 w-32 mb-3" />

      {/* Items Skeleton */}
      <div className="space-y-3">
        {Array.from({ length: 3 }).map((_, i) => (
          <div
            key={i}
            className="flex items-center gap-3 p-3 rounded-xl border"
          >
            <Skeleton className="h-10 w-10 rounded-lg" />
            <div className="flex-1 space-y-1.5">
              <Skeleton className="h-4 w-32" />
              <Skeleton className="h-3 w-48" />
            </div>
            <Skeleton className="h-5 w-14 rounded-full" />
          </div>
        ))}
      </div>

      {/* CTA Skeleton */}
      <Skeleton className="h-9 w-full mt-4 rounded-full" />
    </div>
  );
}

// ============================================================================
// Category Icon Component
// ============================================================================

function CategoryIcon({ category, className }: { category: AchievementCategory; className?: string }) {
  const Icon = CATEGORY_ICONS[category];
  return <Icon className={className} data-testid={`category-icon-${category}`} />;
}

// ============================================================================
// Rarity Badge Component
// ============================================================================

function RarityBadge({ rarity }: { rarity: AchievementRarity }) {
  const colors = RARITY_COLORS[rarity];

  return (
    <span
      className={cn(
        'px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider',
        colors.bg,
        colors.text
      )}
      data-testid={`rarity-badge-${rarity}`}
    >
      {RARITY_LABELS[rarity]}
    </span>
  );
}

// ============================================================================
// Achievement Card Component
// ============================================================================

interface AchievementCardProps {
  achievement: Achievement;
  index: number;
}

function AchievementCard({ achievement, index }: AchievementCardProps) {
  const colors = RARITY_COLORS[achievement.rarity];

  return (
    <motion.div
      initial={{ opacity: 0, y: 10 }}
      animate={{ opacity: 1, y: 0 }}
      transition={{ delay: index * 0.1 }}
    >
      <Link
        href={`/achievements/${achievement.id}`}
        className={cn(
          'flex items-center gap-3 p-3 rounded-xl border transition-all',
          'hover:shadow-md hover:scale-[1.02]',
          colors.border,
          'bg-gradient-to-r from-transparent to-transparent',
          'hover:from-white/50 hover:to-white/30 dark:hover:from-white/5 dark:hover:to-white/10'
        )}
        data-testid={`achievement-card-${achievement.id}`}
      >
        {/* Icon */}
        <div
          className={cn(
            'h-10 w-10 rounded-lg flex items-center justify-center',
            colors.bg
          )}
          data-testid={`achievement-icon-${achievement.id}`}
        >
          <Medal className={cn('h-5 w-5', colors.text)} />
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="flex items-center gap-2">
            <span
              className="text-sm font-semibold truncate"
              data-testid={`achievement-name-${achievement.id}`}
            >
              {achievement.name}
            </span>
            <CategoryIcon
              category={achievement.category}
              className={cn('h-3.5 w-3.5 flex-shrink-0', colors.text)}
            />
          </div>
          <p
            className="text-xs text-muted-foreground truncate"
            data-testid={`achievement-description-${achievement.id}`}
          >
            {achievement.description}
          </p>
          <p className="text-[10px] text-muted-foreground mt-0.5">
            {formatDistanceToNow(new Date(achievement.unlockedAt), { addSuffix: true, locale: it })}
          </p>
        </div>

        {/* Rarity Badge */}
        <RarityBadge rarity={achievement.rarity} />
      </Link>
    </motion.div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-6 text-center"
      data-testid="achievements-widget-empty"
    >
      <div className="h-12 w-12 rounded-full bg-amber-100 dark:bg-amber-900/30 flex items-center justify-center mb-3">
        <Trophy className="h-6 w-6 text-amber-500" />
      </div>
      <p className="text-sm text-muted-foreground mb-1">
        Nessun achievement sbloccato
      </p>
      <p className="text-xs text-muted-foreground mb-3">
        Completa attività per sbloccare badge
      </p>
      <Link href="/achievements">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="view-achievements-empty-cta"
        >
          <Trophy className="h-4 w-4 mr-1" />
          Vedi Tutti i Badge
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// AchievementsWidget Component
// ============================================================================

export function AchievementsWidget({
  achievements = MOCK_ACHIEVEMENTS,
  isLoading = false,
  hasNewUnlock = false,
  className,
}: AchievementsWidgetProps) {
  // Sort by unlockedAt (most recent first) and limit
  const recentAchievements = useMemo(() => {
    return [...achievements]
      .sort((a, b) => new Date(b.unlockedAt).getTime() - new Date(a.unlockedAt).getTime())
      .slice(0, MAX_ACHIEVEMENTS);
  }, [achievements]);

  if (isLoading) {
    return <AchievementsWidgetSkeleton className={className} />;
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="achievements-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-amber-500/20 flex items-center justify-center">
            <Trophy className="h-4 w-4 text-amber-600 dark:text-amber-400" />
          </div>
          <h3
            className="font-semibold text-sm"
            data-testid="achievements-widget-title"
          >
            Achievements & Badges
          </h3>
          {hasNewUnlock && (
            <span
              className="flex items-center gap-1 px-1.5 py-0.5 bg-amber-100 dark:bg-amber-900/30 rounded-full"
              data-testid="new-unlock-indicator"
            >
              <Sparkles className="h-3 w-3 text-amber-500" />
              <span className="text-[10px] font-medium text-amber-600 dark:text-amber-400">NEW</span>
            </span>
          )}
        </div>
      </div>

      {/* Content */}
      {recentAchievements.length === 0 ? (
        <EmptyState />
      ) : (
        <>
          {/* Subtitle */}
          <p className="text-xs text-muted-foreground mb-3 flex items-center gap-1">
            <Medal className="h-3 w-3 text-amber-500" />
            Progressi Recenti:
          </p>

          {/* Achievements List */}
          <div className="space-y-2" data-testid="achievements-list">
            {recentAchievements.map((achievement, index) => (
              <AchievementCard
                key={achievement.id}
                achievement={achievement}
                index={index}
              />
            ))}
          </div>

          {/* CTA */}
          <Link href="/achievements" className="block mt-4">
            <Button
              variant="outline"
              className="w-full rounded-full"
              data-testid="view-all-badges-cta"
            >
              Vedi Tutti i Badge
              <ChevronRight className="h-4 w-4 ml-1" />
            </Button>
          </Link>
        </>
      )}
    </section>
  );
}

export default AchievementsWidget;
