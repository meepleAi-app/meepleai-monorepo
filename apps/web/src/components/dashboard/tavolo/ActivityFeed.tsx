'use client';

/**
 * ActivityFeed — Personal activity timeline sidebar component
 * Part of the "Il Tavolo" dashboard redesign
 *
 * Calls GET /api/v1/dashboard/activity-timeline via useActivityTimeline hook.
 * Shows a Discord-style activity log with time-relative labels.
 */

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { MessageSquare, Plus, Gamepad2, FileText, Trophy, Heart } from 'lucide-react';

import { useActivityTimeline } from '@/hooks/useActivityTimeline';
import type { ActivityEventType } from '@/hooks/useActivityTimeline';

// ─── Icon map ────────────────────────────────────────────────────────────────

const EVENT_ICONS: Record<ActivityEventType, typeof MessageSquare> = {
  chat_saved: MessageSquare,
  game_added: Plus,
  session_completed: Gamepad2,
  pdf_uploaded: FileText,
  achievement_unlocked: Trophy,
  wishlist_added: Heart,
};

// ─── Constants ──────────────────────────────────────────────────────────────

const FEED_PARAMS = {
  types: [] as ActivityEventType[],
  search: '',
  skip: 0,
  take: 20,
  order: 'desc' as const,
} as const;

// ─── Component ───────────────────────────────────────────────────────────────

export function ActivityFeed() {
  const { data, isLoading } = useActivityTimeline(FEED_PARAMS);

  if (isLoading) {
    return (
      <div data-testid="feed-skeleton" className="space-y-3">
        {Array.from({ length: 5 }).map((_, i) => (
          <div key={i} className="animate-pulse">
            <div className="mb-1 h-3 w-16 rounded bg-[#21262d]" />
            <div className="h-4 w-full rounded bg-[#21262d]" />
          </div>
        ))}
      </div>
    );
  }

  const items = data?.items ?? [];

  if (items.length === 0) {
    return (
      <div className="flex flex-col items-center justify-center py-8 text-center">
        <MessageSquare className="mb-2 h-8 w-8 text-[#484f58]" />
        <p className="text-xs text-[#484f58]">Nessuna attività recente</p>
      </div>
    );
  }

  return (
    <div className="space-y-0">
      {items.map(item => {
        const Icon = EVENT_ICONS[item.type] ?? MessageSquare;
        return (
          <div
            key={item.id}
            data-testid={`activity-item-${item.id}`}
            className="border-b border-[#21262d] px-0 py-2.5 last:border-b-0"
          >
            <div className="flex items-center gap-1 text-[10px] text-[#484f58]">
              <Icon className="h-3 w-3" />
              {formatDistanceToNow(new Date(item.timestamp), { addSuffix: true, locale: it })}
            </div>
            <p className="text-xs text-[#8b949e]">
              {item.topic && <span>{item.topic} </span>}
              {item.gameName && (
                <span className="font-semibold text-[#f0a030]">{item.gameName}</span>
              )}
            </p>
          </div>
        );
      })}
    </div>
  );
}
