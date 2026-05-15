/**
 * Filter predicate set for /game-nights index v2 (Stage 3, refs #1170).
 */

import type { GameNightVM } from './view-model';

export const FILTER_KEYS = ['all', 'organizing', 'invited', 'completed'] as const;
export type FilterKey = (typeof FILTER_KEYS)[number];

export function isFilterKey(value: unknown): value is FilterKey {
  return typeof value === 'string' && (FILTER_KEYS as readonly string[]).includes(value);
}

export function filterEvents(events: readonly GameNightVM[], key: FilterKey): GameNightVM[] {
  switch (key) {
    case 'all':
      return events.slice();
    case 'organizing':
      return events.filter(e => e.role === 'organizer');
    case 'invited':
      return events.filter(e => e.role === 'invited');
    case 'completed':
      return events.filter(e => e.statusKey === 'completed');
  }
}
