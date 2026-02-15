/**
 * AchievementsWidget - Dashboard Widget for Recent Achievements
 * Issue #3924 - Frontend: Achievements Widget Component
 *
 * Features:
 * - Fetches from GET /api/v1/achievements/recent via useRecentAchievements hook
 * - Shows last 3 unlocked achievements with colored badges
 * - Badge icon + name + points + unlock date
 * - Celebration animation on new unlock (spring scale + sparkle effect)
 * - Progress bar for next achievement
 * - Rarity-based color coding (common, rare, epic, legendary)
 * - Category icons (collection, gameplay, chat, streak, milestone)
 * - Link: "Vedi Tutti i Badge" -> /achievements
 * - Loading skeleton state
 *
 * @see Issue #3922 - Achievement System & Badge Engine (Backend)
 * @see Epic #3906 - Gamification & Advanced Features
 *
 * @example
 * ```tsx
 * // Auto-fetches via hook
 * <AchievementsWidget />
 *
 * // Props override for testing
 * <AchievementsWidget achievements={mockData} nextProgress={mockProgress} />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { motion, AnimatePresence } from 'framer-motion';
import {
  Trophy,
  Medal,
  BookOpen,
  Gamepad2,
  MessageSquare,
  Flame,
  Target,
  ChevronRight,
  Lock,
  Sparkles,
  Star,
} from 'lucide-react';
import Link from 'next/link';

import { Progress } from '@/components/ui/feedback/progress';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import {
  Tooltip,
  TooltipContent,
  TooltipProvider,
  TooltipTrigger,
} from '@/components/ui/overlays/tooltip';
import { Button } from '@/components/ui/primitives/button';
import { useRecentAchievements } from '@/hooks/useRecentAchievements';
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
  points: number;
  unlockedAt: string;
  icon?: string;
  /** Hint on how to unlock (shown in tooltip for locked badges) */
  unlockHint?: string;
  /** Whether this achievement is locked (not yet unlocked) */
  isLocked?: boolean;
}

export interface NextAchievementProgress {
  achievementName: string;
  current: number;
  target: number;
}

export interface AchievementsWidgetProps {
  /** Achievements data (overrides hook) */
  achievements?: Achievement[];
  /** Next achievement progress (overrides hook) */
  nextProgress?: NextAchievementProgress | null;
  /** Loading state (overrides hook) */
  isLoading?: boolean;
  /** Show new unlock celebration animation */
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

      {/* Progress Skeleton */}
      <div className="mt-4 p-3 rounded-xl border">
        <Skeleton className="h-3 w-40 mb-2" />
        <Skeleton className="h-2 w-full rounded-full" />
        <Skeleton className="h-3 w-16 mt-1" />
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

function RarityBadge({
  rarity,
  isLocked,
  unlockHint,
  achievementName,
}: {
  rarity: AchievementRarity;
  isLocked?: boolean;
  unlockHint?: string;
  achievementName?: string;
}) {
  const colors = RARITY_COLORS[rarity];

  const badge = (
    <span
      className={cn(
        'px-2 py-0.5 rounded-full text-[10px] font-bold uppercase tracking-wider inline-flex items-center gap-1',
        isLocked ? 'bg-muted text-muted-foreground' : colors.bg,
        !isLocked && colors.text
      )}
      data-testid={`rarity-badge-${rarity}`}
    >
      {isLocked && <Lock className="h-2.5 w-2.5" />}
      {RARITY_LABELS[rarity]}
    </span>
  );

  if (isLocked && unlockHint) {
    return (
      <Tooltip>
        <TooltipTrigger asChild>{badge}</TooltipTrigger>
        <TooltipContent side="left" data-testid={`unlock-tooltip-${rarity}`}>
          {achievementName && (
            <p className="font-semibold text-xs mb-1" data-testid="unlock-tooltip-name">{achievementName}</p>
          )}
          <p className="font-medium text-xs" data-testid="unlock-tooltip-hint">{unlockHint}</p>
        </TooltipContent>
      </Tooltip>
    );
  }

  return badge;
}

// ============================================================================
// Celebration Badge (animated sparkle on new unlock)
// ============================================================================

function CelebrationBadge({ achievement }: { achievement: Achievement }) {
  const colors = RARITY_COLORS[achievement.rarity];

  return (
    <motion.div
      initial={{ scale: 0, rotate: -180 }}
      animate={{ scale: 1, rotate: 0 }}
      transition={{ type: 'spring', stiffness: 200, damping: 15 }}
      className={cn(
        'h-10 w-10 rounded-lg flex items-center justify-center relative',
        colors.bg
      )}
      data-testid={`achievement-icon-${achievement.id}`}
    >
      <Medal className={cn('h-5 w-5', colors.text)} />
      {/* Sparkle particles */}
      <motion.div
        className="absolute -top-1 -right-1"
        initial={{ opacity: 0, scale: 0 }}
        animate={{ opacity: [0, 1, 0], scale: [0, 1.2, 0] }}
        transition={{ delay: 0.3, duration: 0.6 }}
      >
        <Sparkles className="h-3 w-3 text-amber-400" />
      </motion.div>
    </motion.div>
  );
}

// ============================================================================
// Achievement Card Component
// ============================================================================

interface AchievementCardProps {
  achievement: Achievement;
  index: number;
  isCelebrating: boolean;
}

function AchievementCard({ achievement, index, isCelebrating }: AchievementCardProps) {
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
        {/* Icon (with celebration animation for first item) */}
        {isCelebrating && index === 0 ? (
          <CelebrationBadge achievement={achievement} />
        ) : (
          <div
            className={cn(
              'h-10 w-10 rounded-lg flex items-center justify-center',
              colors.bg
            )}
            data-testid={`achievement-icon-${achievement.id}`}
          >
            <Medal className={cn('h-5 w-5', colors.text)} />
          </div>
        )}

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
          <div className="flex items-center gap-2 mt-0.5">
            <p className="text-[10px] text-muted-foreground">
              {formatDistanceToNow(new Date(achievement.unlockedAt), { addSuffix: true, locale: it })}
            </p>
            <span
              className={cn('flex items-center gap-0.5 text-[10px] font-medium', colors.text)}
              data-testid={`achievement-points-${achievement.id}`}
            >
              <Star className="h-2.5 w-2.5" />
              {achievement.points} pts
            </span>
          </div>
        </div>

        {/* Rarity Badge */}
        <RarityBadge
          rarity={achievement.rarity}
          isLocked={achievement.isLocked}
          unlockHint={achievement.unlockHint}
          achievementName={achievement.name}
        />
      </Link>
    </motion.div>
  );
}

// ============================================================================
// Next Achievement Progress Component
// ============================================================================

function NextProgressSection({ progress }: { progress: NextAchievementProgress }) {
  const percentage = Math.min(Math.round((progress.current / progress.target) * 100), 100);

  return (
    <div
      className="mt-4 p-3 rounded-xl border border-border/40 bg-muted/30"
      data-testid="next-achievement-progress"
    >
      <div className="flex items-center justify-between mb-2">
        <span className="text-xs font-medium text-muted-foreground flex items-center gap-1">
          <Target className="h-3 w-3 text-amber-500" />
          Prossimo Obiettivo
        </span>
        <span className="text-[10px] text-muted-foreground" data-testid="next-achievement-count">
          {progress.current}/{progress.target}
        </span>
      </div>
      <p className="text-sm font-medium mb-2" data-testid="next-achievement-name">
        {progress.achievementName}
      </p>
      <Progress
        value={percentage}
        className="h-2 [&>div]:bg-amber-500"
        aria-label={`Progresso verso ${progress.achievementName}: ${percentage}%`}
      />
      <p className="text-[10px] text-muted-foreground mt-1" data-testid="next-achievement-percentage">
        {percentage}% completato
      </p>
    </div>
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
  achievements: achievementsProp,
  nextProgress: nextProgressProp,
  isLoading: isLoadingProp,
  hasNewUnlock = false,
  className,
}: AchievementsWidgetProps) {
  // Use hook for data fetching, but allow prop overrides for testing
  const { data: hookData, isLoading: hookLoading } = useRecentAchievements();

  const achievements = useMemo(
    () => achievementsProp ?? hookData?.achievements ?? [],
    [achievementsProp, hookData?.achievements]
  );
  const nextProgress = nextProgressProp !== undefined ? nextProgressProp : hookData?.nextProgress ?? null;
  const isLoading = isLoadingProp ?? hookLoading;

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
    <TooltipProvider>
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
          <AnimatePresence>
            {hasNewUnlock && (
              <motion.span
                initial={{ opacity: 0, scale: 0.5 }}
                animate={{ opacity: 1, scale: 1 }}
                exit={{ opacity: 0, scale: 0.5 }}
                className="flex items-center gap-1 px-1.5 py-0.5 bg-amber-100 dark:bg-amber-900/30 rounded-full"
                data-testid="new-unlock-indicator"
              >
                <Sparkles className="h-3 w-3 text-amber-500" />
                <span className="text-[10px] font-medium text-amber-600 dark:text-amber-400">NEW</span>
              </motion.span>
            )}
          </AnimatePresence>
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
                isCelebrating={hasNewUnlock}
              />
            ))}
          </div>

          {/* Next Achievement Progress */}
          {nextProgress && (
            <NextProgressSection progress={nextProgress} />
          )}

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
    </TooltipProvider>
  );
}

export default AchievementsWidget;
