/**
 * ActivityFeed - Dashboard Widget for Recent Activity Timeline
 * Issue #3311 - Implement ActivityFeed timeline
 *
 * Features:
 * - Vertical timeline with last 10 activities
 * - Icons for 5 activity types (game_added, session_completed, chat_saved, wishlist_added, achievement_unlocked)
 * - Relative timestamps (Today, Yesterday, date)
 * - Clickable events → navigate to entity
 * - "View All Timeline" CTA → /activity
 * - Empty state with "Inizia a Giocare" CTA
 * - Loading skeleton state
 *
 * @example
 * ```tsx
 * <ActivityFeed />
 * ```
 */

'use client';

import { useMemo } from 'react';
import { motion } from 'framer-motion';
import {
  Library,
  Dices,
  MessageSquare,
  Star,
  Trophy,
  Clock,
  ChevronRight,
  Sparkles,
} from 'lucide-react';
import Link from 'next/link';
import type { LucideIcon } from 'lucide-react';

import { cn } from '@/lib/utils';
import { Skeleton } from '@/components/ui/feedback/skeleton';
import { Button } from '@/components/ui/primitives/button';

// ============================================================================
// Types
// ============================================================================

export type ActivityEventType =
  | 'game_added'
  | 'session_completed'
  | 'chat_saved'
  | 'wishlist_added'
  | 'achievement_unlocked';

export interface ActivityEvent {
  id: string;
  type: ActivityEventType;
  title: string;
  description?: string;
  entityId?: string;
  entityType?: 'game' | 'session' | 'chat' | 'wishlist' | 'achievement';
  timestamp: string;
}

export interface ActivityFeedProps {
  /** Activity events data */
  events?: ActivityEvent[];
  /** Total count of events (for "View All" logic) */
  totalCount?: number;
  /** Maximum events to display */
  limit?: number;
  /** Loading state */
  isLoading?: boolean;
  /** Additional CSS classes */
  className?: string;
}

// ============================================================================
// Mock Data (for development)
// ============================================================================

const MOCK_EVENTS: ActivityEvent[] = [
  {
    id: 'event-1',
    type: 'game_added',
    title: 'Aggiunto "Wingspan"',
    description: 'Aggiunto alla collezione',
    entityId: 'game-1',
    entityType: 'game',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 2).toISOString(), // 2 hours ago
  },
  {
    id: 'event-2',
    type: 'session_completed',
    title: 'Giocato "Catan"',
    description: '4 giocatori • 90 min',
    entityId: 'session-1',
    entityType: 'session',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 5).toISOString(), // 5 hours ago
  },
  {
    id: 'event-3',
    type: 'chat_saved',
    title: 'Chat "Regole Wingspan"',
    description: '12 messaggi',
    entityId: 'chat-1',
    entityType: 'chat',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 28).toISOString(), // Yesterday
  },
  {
    id: 'event-4',
    type: 'wishlist_added',
    title: 'Aggiunto "Terraforming Mars"',
    description: 'Priorità alta',
    entityId: 'wishlist-1',
    entityType: 'wishlist',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 2).toISOString(), // 2 days ago
  },
  {
    id: 'event-5',
    type: 'achievement_unlocked',
    title: 'Achievement: "Streak 7gg"',
    description: 'Giocato per 7 giorni consecutivi',
    entityId: 'achievement-1',
    entityType: 'achievement',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 3).toISOString(), // 3 days ago
  },
  {
    id: 'event-6',
    type: 'session_completed',
    title: 'Giocato "Azul"',
    description: '2 giocatori • 45 min',
    entityId: 'session-2',
    entityType: 'session',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 4).toISOString(), // 4 days ago
  },
  {
    id: 'event-7',
    type: 'game_added',
    title: 'Aggiunto "Gloomhaven"',
    description: 'Aggiunto alla collezione',
    entityId: 'game-2',
    entityType: 'game',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 5).toISOString(), // 5 days ago
  },
  {
    id: 'event-8',
    type: 'chat_saved',
    title: 'Chat "Strategie Catan"',
    description: '8 messaggi',
    entityId: 'chat-2',
    entityType: 'chat',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 6).toISOString(), // 6 days ago
  },
  {
    id: 'event-9',
    type: 'wishlist_added',
    title: 'Aggiunto "Spirit Island"',
    description: 'Priorità media',
    entityId: 'wishlist-2',
    entityType: 'wishlist',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 7).toISOString(), // 7 days ago
  },
  {
    id: 'event-10',
    type: 'achievement_unlocked',
    title: 'Achievement: "Collezionista"',
    description: '10 giochi in collezione',
    entityId: 'achievement-2',
    entityType: 'achievement',
    timestamp: new Date(Date.now() - 1000 * 60 * 60 * 24 * 10).toISOString(), // 10 days ago
  },
];

// ============================================================================
// Activity Type Configuration
// ============================================================================

interface ActivityTypeConfig {
  icon: LucideIcon;
  iconColor: string;
  bgColor: string;
  href: (entityId?: string) => string;
}

const ACTIVITY_TYPE_CONFIG: Record<ActivityEventType, ActivityTypeConfig> = {
  game_added: {
    icon: Library,
    iconColor: 'text-amber-600 dark:text-amber-400',
    bgColor: 'bg-amber-500/20',
    href: (entityId) => entityId ? `/library/${entityId}` : '/library',
  },
  session_completed: {
    icon: Dices,
    iconColor: 'text-emerald-600 dark:text-emerald-400',
    bgColor: 'bg-emerald-500/20',
    href: (entityId) => entityId ? `/toolkit/${entityId}` : '/toolkit',
  },
  chat_saved: {
    icon: MessageSquare,
    iconColor: 'text-blue-600 dark:text-blue-400',
    bgColor: 'bg-blue-500/20',
    href: (entityId) => entityId ? `/chat/${entityId}` : '/chat',
  },
  wishlist_added: {
    icon: Star,
    iconColor: 'text-yellow-600 dark:text-yellow-400',
    bgColor: 'bg-yellow-500/20',
    href: (entityId) => entityId ? `/wishlist/${entityId}` : '/wishlist',
  },
  achievement_unlocked: {
    icon: Trophy,
    iconColor: 'text-purple-600 dark:text-purple-400',
    bgColor: 'bg-purple-500/20',
    href: (entityId) => entityId ? `/achievements/${entityId}` : '/achievements',
  },
};

// ============================================================================
// Helper Functions
// ============================================================================

function formatRelativeTime(isoDate: string): string {
  const date = new Date(isoDate);
  const now = new Date();

  // Check if same day
  const isToday =
    date.getDate() === now.getDate() &&
    date.getMonth() === now.getMonth() &&
    date.getFullYear() === now.getFullYear();

  // Check if yesterday
  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  const isYesterday =
    date.getDate() === yesterday.getDate() &&
    date.getMonth() === yesterday.getMonth() &&
    date.getFullYear() === yesterday.getFullYear();

  if (isToday) {
    return `Oggi ${date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}`;
  }
  if (isYesterday) {
    return `Ieri ${date.toLocaleTimeString('it-IT', { hour: '2-digit', minute: '2-digit' })}`;
  }
  // More than yesterday, show date
  return date.toLocaleDateString('it-IT', {
    day: '2-digit',
    month: '2-digit',
    hour: '2-digit',
    minute: '2-digit',
  });
}

function getDateGroup(isoDate: string): string {
  const date = new Date(isoDate);
  const now = new Date();

  // Check if same day
  const isToday =
    date.getDate() === now.getDate() &&
    date.getMonth() === now.getMonth() &&
    date.getFullYear() === now.getFullYear();

  // Check if yesterday
  const yesterday = new Date(now);
  yesterday.setDate(yesterday.getDate() - 1);
  const isYesterday =
    date.getDate() === yesterday.getDate() &&
    date.getMonth() === yesterday.getMonth() &&
    date.getFullYear() === yesterday.getFullYear();

  if (isToday) return 'Oggi';
  if (isYesterday) return 'Ieri';
  return date.toLocaleDateString('it-IT', { day: '2-digit', month: '2-digit' });
}

// ============================================================================
// Skeleton Component
// ============================================================================

function ActivityFeedSkeleton({ className }: { className?: string }) {
  return (
    <div
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="activity-feed-skeleton"
    >
      {/* Header Skeleton */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <Skeleton className="h-8 w-8 rounded-lg" />
          <Skeleton className="h-5 w-36" />
        </div>
      </div>

      {/* Timeline Skeleton */}
      <div className="space-y-4">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="flex items-start gap-3">
            <Skeleton className="h-8 w-8 rounded-full shrink-0" />
            <div className="flex-1 space-y-1">
              <Skeleton className="h-4 w-40" />
              <Skeleton className="h-3 w-24" />
            </div>
            <Skeleton className="h-3 w-16" />
          </div>
        ))}
      </div>
    </div>
  );
}

// ============================================================================
// Empty State Component
// ============================================================================

function EmptyState() {
  return (
    <div
      className="flex flex-col items-center justify-center py-8 text-center"
      data-testid="activity-feed-empty"
    >
      <div className="h-12 w-12 rounded-full bg-muted/50 flex items-center justify-center mb-3">
        <Sparkles className="h-6 w-6 text-muted-foreground/50" />
      </div>
      <p className="text-sm text-muted-foreground mb-1">
        Nessuna attività recente
      </p>
      <p className="text-xs text-muted-foreground/70 mb-4">
        Inizia ad aggiungere giochi e giocare!
      </p>
      <Link href="/games/catalog">
        <Button
          size="sm"
          variant="outline"
          className="rounded-full"
          data-testid="start-playing-cta"
        >
          <Library className="h-4 w-4 mr-1" />
          Esplora Catalogo
        </Button>
      </Link>
    </div>
  );
}

// ============================================================================
// Event Item Component
// ============================================================================

function EventItem({ event, index }: { event: ActivityEvent; index: number }) {
  const config = ACTIVITY_TYPE_CONFIG[event.type];
  const Icon = config.icon;
  const href = config.href(event.entityId);

  return (
    <motion.div
      initial={{ opacity: 0, x: -10 }}
      animate={{ opacity: 1, x: 0 }}
      transition={{ delay: index * 0.03 }}
      className="relative"
    >
      {/* Timeline connector line */}
      {index > 0 && (
        <div
          className="absolute left-4 -top-4 h-4 w-px bg-border/60"
          aria-hidden="true"
        />
      )}

      <Link
        href={href}
        className="flex items-start gap-3 p-2 rounded-lg hover:bg-muted/30 transition-colors group"
        data-testid={`activity-event-${event.id}`}
      >
        {/* Event Icon */}
        <div
          className={cn(
            'h-8 w-8 rounded-full flex items-center justify-center shrink-0',
            config.bgColor
          )}
          data-testid={`activity-icon-${event.id}`}
        >
          <Icon className={cn('h-4 w-4', config.iconColor)} />
        </div>

        {/* Event Content */}
        <div className="flex-1 min-w-0">
          <p
            className="text-sm font-medium truncate group-hover:text-foreground transition-colors"
            data-testid={`activity-title-${event.id}`}
          >
            {event.title}
          </p>
          {event.description && (
            <p
              className="text-xs text-muted-foreground truncate"
              data-testid={`activity-description-${event.id}`}
            >
              {event.description}
            </p>
          )}
        </div>

        {/* Timestamp */}
        <span
          className="text-xs text-muted-foreground shrink-0"
          data-testid={`activity-time-${event.id}`}
        >
          {formatRelativeTime(event.timestamp)}
        </span>
      </Link>
    </motion.div>
  );
}

// ============================================================================
// ActivityFeed Component
// ============================================================================

export function ActivityFeed({
  events = MOCK_EVENTS,
  totalCount,
  limit = 10,
  isLoading = false,
  className,
}: ActivityFeedProps) {
  const displayEvents = useMemo(() => events.slice(0, limit), [events, limit]);
  const total = totalCount ?? events.length;
  const hasMore = total > limit;
  const isEmpty = events.length === 0;

  if (isLoading) {
    return <ActivityFeedSkeleton className={className} />;
  }

  return (
    <section
      className={cn(
        'rounded-2xl border border-border/60 bg-card/80 backdrop-blur-xl p-4',
        className
      )}
      data-testid="activity-feed-widget"
    >
      {/* Header */}
      <div className="flex items-center justify-between mb-4">
        <div className="flex items-center gap-2">
          <div className="h-8 w-8 rounded-lg bg-slate-500/20 flex items-center justify-center">
            <Clock className="h-4 w-4 text-slate-600 dark:text-slate-400" />
          </div>
          <h3 className="font-semibold text-sm" data-testid="activity-feed-title">
            Attività Recente
          </h3>
        </div>
      </div>

      {/* Content */}
      {isEmpty ? (
        <EmptyState />
      ) : (
        <>
          {/* Timeline */}
          <div className="space-y-1" data-testid="activity-timeline">
            {displayEvents.map((event, index) => (
              <EventItem key={event.id} event={event} index={index} />
            ))}
          </div>

          {/* View All Link */}
          {hasMore && (
            <div className="mt-4 pt-3 border-t border-border/40">
              <Link
                href="/activity"
                className="text-xs text-muted-foreground hover:text-foreground flex items-center gap-1 transition-colors justify-center"
                data-testid="view-all-activity"
              >
                Vedi Tutta la Timeline
                <ChevronRight className="h-3 w-3" />
              </Link>
            </div>
          )}
        </>
      )}
    </section>
  );
}

export default ActivityFeed;
