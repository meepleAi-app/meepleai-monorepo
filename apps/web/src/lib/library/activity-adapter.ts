/**
 * activity-adapter — Phase 3b (#1593) DTO → rail item mapper.
 *
 * Maps `ActivityItemDto` (BE-3) → `ActivityItem` (rail). Pure function,
 * unit-testable. Two responsibilities:
 *   1. Map `eventType` → `ActivityKind` discriminant (rail's icon/styling driver).
 *   2. Resolve `entityTitle = dto.title ?? fallbacks[kindKey]` so `title=null`
 *      cases (no payload-derived display name) still show meaningful copy.
 *
 * The fallback map is INJECTED by the caller (the hook) which provides
 * i18n-translated strings. Keeping the adapter pure (no i18n IO) keeps it
 * unit-testable with literal strings.
 */

import type { ActivityItem, ActivityKind } from '@/components/features/library/RecentActivityRail';
import type { ActivityItemDto } from '@/lib/api/schemas/activity.schemas';

/**
 * i18n strings injected by the consumer (resolved via useTranslation).
 */
export interface ActivityKindFallbacks {
  readonly agent: string;
  readonly chat: string;
  readonly kbIndexed: string;
  readonly play: string;
  readonly removed: string;
}

function mapEventTypeToKind(eventType: string): ActivityKind {
  switch (eventType) {
    case 'agent.created':
      return 'agent';
    case 'chat.session.created':
      return 'chat';
    case 'kb.doc.indexed':
      return 'kb-indexed';
    case 'session.created':
    case 'session.finalized':
    case 'library.session.recorded':
      return 'play';
    case 'library.entry.removed':
      return 'removed';
    default:
      return 'add';
  }
}

function fallbackForKind(kind: ActivityKind, fallbacks: ActivityKindFallbacks): string {
  switch (kind) {
    case 'agent':
      return fallbacks.agent;
    case 'chat':
      return fallbacks.chat;
    case 'kb-indexed':
      return fallbacks.kbIndexed;
    case 'play':
      return fallbacks.play;
    case 'removed':
      return fallbacks.removed;
    case 'add':
    case 'rating-changed':
      return fallbacks.play;
  }
}

export function toActivityItem(
  dto: ActivityItemDto,
  fallbacks: ActivityKindFallbacks
): ActivityItem {
  const kind = mapEventTypeToKind(dto.eventType);
  const entityTitle = dto.title ?? fallbackForKind(kind, fallbacks);
  return {
    id: dto.id,
    kind,
    entityTitle,
    timestamp: dto.timestamp,
  };
}
