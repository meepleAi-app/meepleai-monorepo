/**
 * ActivityItem - Recent Activity Display
 * Issue #3286 - User Dashboard Layout System
 *
 * Features:
 * - Type-based icon and color (game_added, session_played, ai_interaction, share_received)
 * - Relative timestamp formatting
 * - Optional game/user references
 * - Hover animation
 *
 * @example
 * ```tsx
 * <ActivityItem
 *   data={{
 *     id: '1',
 *     type: 'game_added',
 *     title: 'Wingspan aggiunto alla collezione',
 *     timestamp: new Date(),
 *     game: { id: 'g1', name: 'Wingspan' }
 *   }}
 * />
 * ```
 */

'use client';

import { memo } from 'react';
import { motion } from 'framer-motion';
import {
  Plus,
  Dices,
  Bot,
  Share2,
  Users,
  Heart,
  Clock,
  ChevronRight,
  type LucideIcon,
} from 'lucide-react';

import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type ActivityType =
  | 'game_added'
  | 'session_played'
  | 'ai_interaction'
  | 'share_received'
  | 'share_sent'
  | 'group_joined'
  | 'favorite_added'
  | 'loan_started'
  | 'loan_returned';

export interface ActivityData {
  id: string;
  type: ActivityType;
  title: string;
  timestamp: Date;
  details?: string;
  game?: { id: string; name: string; imageUrl?: string };
  user?: { id: string; name: string; avatarUrl?: string };
}

export interface ActivityItemProps {
  data: ActivityData;
  onClick?: () => void;
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

interface ActivityConfig {
  icon: LucideIcon;
  color: string;
  bgColor: string;
}

const ACTIVITY_CONFIG: Record<ActivityType, ActivityConfig> = {
  game_added: {
    icon: Plus,
    color: 'text-green-600 dark:text-green-400',
    bgColor: 'bg-green-500/10',
  },
  session_played: {
    icon: Dices,
    color: 'text-blue-600 dark:text-blue-400',
    bgColor: 'bg-blue-500/10',
  },
  ai_interaction: {
    icon: Bot,
    color: 'text-purple-600 dark:text-purple-400',
    bgColor: 'bg-purple-500/10',
  },
  share_received: {
    icon: Share2,
    color: 'text-primary',
    bgColor: 'bg-primary/10',
  },
  share_sent: {
    icon: Share2,
    color: 'text-primary',
    bgColor: 'bg-primary/10',
  },
  group_joined: {
    icon: Users,
    color: 'text-cyan-600 dark:text-cyan-400',
    bgColor: 'bg-cyan-500/10',
  },
  favorite_added: {
    icon: Heart,
    color: 'text-red-500',
    bgColor: 'bg-red-500/10',
  },
  loan_started: {
    icon: Share2,
    color: 'text-amber-600 dark:text-amber-400',
    bgColor: 'bg-amber-500/10',
  },
  loan_returned: {
    icon: Share2,
    color: 'text-green-600 dark:text-green-400',
    bgColor: 'bg-green-500/10',
  },
};

function formatRelativeTime(date: Date): string {
  const now = new Date();
  const diff = now.getTime() - date.getTime();
  const minutes = Math.floor(diff / (1000 * 60));
  const hours = Math.floor(diff / (1000 * 60 * 60));
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (minutes < 1) return 'Ora';
  if (minutes < 60) return `${minutes} min fa`;
  if (hours < 24) return `${hours}h fa`;
  if (days === 1) return 'Ieri';
  if (days < 7) return `${days} giorni fa`;
  if (days < 30) return `${Math.floor(days / 7)} sett. fa`;
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

// ============================================================================
// ActivityItem Component
// ============================================================================

export const ActivityItem = memo(function ActivityItem({
  data,
  onClick,
  className,
}: ActivityItemProps) {
  const { type, title, timestamp, details, game, user } = data;
  const config = ACTIVITY_CONFIG[type];
  const Icon = config.icon;

  return (
    <motion.article
      whileHover={{ x: 4, backgroundColor: 'hsl(var(--muted) / 0.3)' }}
      transition={{ duration: 0.15 }}
      onClick={onClick}
      className={cn(
        'flex items-start gap-3 rounded-lg p-3 transition-colors',
        onClick && 'cursor-pointer',
        className
      )}
      role={onClick ? 'button' : 'article'}
      tabIndex={onClick ? 0 : undefined}
    >
      {/* Activity Icon */}
      <div
        className={cn(
          'flex h-10 w-10 shrink-0 items-center justify-center rounded-full',
          config.bgColor
        )}
      >
        <Icon className={cn('h-5 w-5', config.color)} />
      </div>

      {/* Content */}
      <div className="min-w-0 flex-1">
        <p className="text-sm font-medium leading-snug">{title}</p>

        {/* Details or metadata */}
        <div className="mt-1 flex flex-wrap items-center gap-x-3 gap-y-1 text-xs text-muted-foreground">
          {/* Timestamp */}
          <span className="flex items-center gap-1">
            <Clock className="h-3 w-3" />
            {formatRelativeTime(timestamp)}
          </span>

          {/* Game reference */}
          {game && (
            <span className="flex items-center gap-1 rounded-md bg-muted px-1.5 py-0.5">
              {game.name}
            </span>
          )}

          {/* User reference */}
          {user && (
            <span className="flex items-center gap-1">
              {user.avatarUrl ? (
                <img
                  src={user.avatarUrl}
                  alt=""
                  className="h-4 w-4 rounded-full object-cover"
                />
              ) : (
                <div className="flex h-4 w-4 items-center justify-center rounded-full bg-primary/20 text-[8px] font-bold text-primary">
                  {user.name[0]}
                </div>
              )}
              {user.name}
            </span>
          )}
        </div>

        {/* Additional details */}
        {details && (
          <p className="mt-1.5 text-xs text-muted-foreground">{details}</p>
        )}
      </div>

      {/* Chevron for clickable items */}
      {onClick && (
        <ChevronRight className="h-5 w-5 shrink-0 text-muted-foreground/50" />
      )}
    </motion.article>
  );
});

export default ActivityItem;
