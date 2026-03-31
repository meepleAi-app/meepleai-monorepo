/**
 * useActivityFeed
 *
 * Aggregates recent play sessions and earned badges into a unified
 * activity feed sorted by timestamp descending.
 */

import { useEffect, useState } from 'react';

import { api } from '@/lib/api';
import type { UserBadgeDto } from '@/lib/api/schemas/badges.schemas';
import type { PlayRecordSummary } from '@/lib/api/schemas/play-records.schemas';

export type ActivityItemType = 'session' | 'achievement';

export interface ActivityItem {
  id: string;
  type: ActivityItemType;
  title: string;
  subtitle?: string;
  timestamp: string;
  iconEmoji: string;
}

export interface UseActivityFeedResult {
  items: ActivityItem[];
  isLoading: boolean;
  error: string | null;
}

function buildSessionItem(s: PlayRecordSummary): ActivityItem {
  return {
    id: `session-${s.id}`,
    type: 'session',
    title: s.gameName,
    subtitle: s.playerCount ? `${s.playerCount} giocatori` : undefined,
    timestamp: s.sessionDate,
    iconEmoji: '🎲',
  };
}

function buildAchievementItem(a: UserBadgeDto): ActivityItem {
  return {
    id: `achievement-${a.id}`,
    type: 'achievement',
    title: a.name,
    subtitle: 'Badge sbloccato',
    timestamp: a.earnedAt,
    iconEmoji: '🏆',
  };
}

export function useActivityFeed(limit = 10): UseActivityFeedResult {
  const [items, setItems] = useState<ActivityItem[]>([]);
  const [isLoading, setIsLoading] = useState(true);
  const [error, setError] = useState<string | null>(null);

  useEffect(() => {
    let sessionsFailed = false;
    let badgesFailed = false;

    const sessionsPromise = api.playRecords
      .getHistory({ page: 1, pageSize: limit })
      .then(data => data.records ?? [])
      .catch(() => {
        sessionsFailed = true;
        return [] as PlayRecordSummary[];
      });

    const badgesPromise = api.badges.getMyBadges().catch(() => {
      badgesFailed = true;
      return [] as UserBadgeDto[];
    });

    Promise.all([sessionsPromise, badgesPromise])
      .then(([sessions, badges]) => {
        if (sessionsFailed && badgesFailed) {
          setError('Errore nel caricamento delle attività');
          return;
        }

        const sessionItems = sessions.map(buildSessionItem);
        const achievementItems = badges.map(buildAchievementItem);

        const all = [...sessionItems, ...achievementItems].sort(
          (a, b) => new Date(b.timestamp).getTime() - new Date(a.timestamp).getTime()
        );

        setItems(all.slice(0, limit));
      })
      .catch(err => setError(err instanceof Error ? err.message : 'Errore'))
      .finally(() => setIsLoading(false));
  }, [limit]);

  return { items, isLoading, error };
}
