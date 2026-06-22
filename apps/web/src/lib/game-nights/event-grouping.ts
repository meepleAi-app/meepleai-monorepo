/**
 * Month-grouping helper for /game-nights index v2 list view (Stage 3, refs #1170).
 *
 * Groups view models into per-month buckets sorted newest-first. Within each
 * bucket items are ordered ASC if the bucket represents the current month or
 * a future month (next-up first), DESC otherwise (recent history first).
 */

import type { GameNightVM } from './view-model';

export interface MonthGroup {
  readonly year: number;
  readonly month: number; // 0-indexed
  readonly items: readonly GameNightVM[];
}

function isPastMonth(year: number, month: number, now: Date): boolean {
  const refYear = now.getFullYear();
  const refMonth = now.getMonth();
  if (year < refYear) return true;
  if (year > refYear) return false;
  return month < refMonth;
}

export function groupByMonth(events: readonly GameNightVM[], now: Date = new Date()): MonthGroup[] {
  if (events.length === 0) return [];

  const buckets = new Map<string, { year: number; month: number; items: GameNightVM[] }>();
  for (const event of events) {
    const key = `${event.year}-${event.month}`;
    let bucket = buckets.get(key);
    if (!bucket) {
      bucket = { year: event.year, month: event.month, items: [] };
      buckets.set(key, bucket);
    }
    bucket.items.push(event);
  }

  const groups: MonthGroup[] = [];
  for (const bucket of buckets.values()) {
    const past = isPastMonth(bucket.year, bucket.month, now);
    const sortedItems = bucket.items.slice().sort((a, b) => {
      const dayDiff = past ? b.day - a.day : a.day - b.day;
      if (dayDiff !== 0) return dayDiff;
      return past ? b.timeLabel.localeCompare(a.timeLabel) : a.timeLabel.localeCompare(b.timeLabel);
    });
    groups.push({ year: bucket.year, month: bucket.month, items: sortedItems });
  }

  groups.sort((a, b) => {
    if (a.year !== b.year) return b.year - a.year;
    return b.month - a.month;
  });

  return groups;
}
