/**
 * ActivityTimeline - Re-export alias for ActivityFeed
 *
 * Issue #2803: Consolidated into unified ActivityFeed component.
 * This file provides backward compatibility for existing ActivityTimeline imports.
 *
 * Migration:
 * ```tsx
 * // Old:
 * import { ActivityTimeline } from '@/components/admin';
 *
 * // New (recommended):
 * import { ActivityFeed } from '@/components/admin';
 * <ActivityFeed events={events} iconMode="category" animated locale="it" />
 * ```
 *
 * The re-export is configured with ActivityTimeline defaults:
 * - iconMode: 'category' (semantic user/game/system/ai icons)
 * - animated: true (staggered entry animation)
 * - locale: 'it' (Italian labels)
 * - viewAllHref: '/admin/audit' (default audit page)
 *
 * @deprecated Consider migrating to ActivityFeed with explicit props for clarity
 */

import { ActivityFeed, type ActivityEvent } from './ActivityFeed';

export interface ActivityTimelineProps {
  events: ActivityEvent[];
  className?: string;
  maxEvents?: number;
  viewAllHref?: string;
  showViewAll?: boolean;
}

/**
 * Legacy ActivityTimeline wrapper with defaults matching original behavior.
 * Uses unified ActivityFeed component internally.
 */
export function ActivityTimeline({
  events,
  className,
  maxEvents = 10,
  viewAllHref = '/admin/audit',
  showViewAll = true,
}: ActivityTimelineProps): JSX.Element {
  return (
    <ActivityFeed
      events={events}
      className={className}
      maxEvents={maxEvents}
      viewAllHref={viewAllHref}
      showViewAll={showViewAll}
      iconMode="category"
      animated={true}
      locale="it"
    />
  );
}

// Re-export ActivityEvent for convenience
export type { ActivityEvent };
