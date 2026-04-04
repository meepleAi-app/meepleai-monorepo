'use client';

/**
 * RecentActivitySidebar — Dashboard v2 "Il Tavolo"
 *
 * Compact sidebar section showing up to 5 recent cross-entity activity events.
 */

import { cn } from '@/lib/utils';

// ─── Types ────────────────────────────────────────────────────────────────────

export type ActivityType = 'game' | 'agent' | 'kb' | 'session';

export interface ActivityItem {
  id: string;
  type: ActivityType;
  entityName: string;
  description: string;
  timestamp: string;
}

export interface RecentActivitySidebarProps {
  activities: ActivityItem[];
  loading?: boolean;
  className?: string;
}

// ─── Config ───────────────────────────────────────────────────────────────────

const ACTIVITY_ICONS: Record<ActivityType, string> = {
  game: '🎲',
  agent: '⚡',
  kb: '📜',
  session: '⏳',
};

const ACTIVITY_COLORS: Record<ActivityType, string> = {
  game: 'text-[hsl(25,95%,45%)]',
  agent: 'text-[hsl(38,92%,50%)]',
  kb: 'text-[hsl(174,60%,40%)]',
  session: 'text-[hsl(240,60%,55%)]',
};

// ─── Helpers ──────────────────────────────────────────────────────────────────

function relativeTime(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const mins = Math.floor(diff / 60000);
  if (mins < 1) return 'ora';
  if (mins < 60) return `${mins} min fa`;
  const hours = Math.floor(mins / 60);
  if (hours < 24) return `${hours}h fa`;
  const days = Math.floor(hours / 24);
  if (days === 1) return 'ieri';
  return `${days} giorni fa`;
}

// ─── Component ────────────────────────────────────────────────────────────────

export function RecentActivitySidebar({
  activities,
  loading,
  className,
}: RecentActivitySidebarProps) {
  if (!loading && activities.length === 0) return null;

  return (
    <div data-testid="sidebar-activity" className={cn('', className)}>
      <p className="text-[11px] font-bold uppercase tracking-wide text-muted-foreground mb-2">
        📋 Attività Recenti
      </p>

      {loading ? (
        <>
          <div className="h-[28px] rounded bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
          <div className="h-[28px] rounded bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
          <div className="h-[28px] rounded bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
          <div className="h-[28px] rounded bg-[rgba(200,180,160,0.20)] animate-pulse mb-1.5" />
        </>
      ) : (
        activities.slice(0, 5).map(activity => {
          const icon = ACTIVITY_ICONS[activity.type];
          const entityColor = ACTIVITY_COLORS[activity.type];
          return (
            <div
              key={activity.id}
              className="text-[11px] text-muted-foreground py-[7px] border-b border-[rgba(200,180,160,0.12)] last:border-b-0 leading-relaxed"
            >
              {icon} <span className={cn('font-semibold', entityColor)}>{activity.entityName}</span>{' '}
              {activity.description} · {relativeTime(activity.timestamp)}
            </div>
          );
        })
      )}
    </div>
  );
}
