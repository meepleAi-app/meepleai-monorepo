/**
 * ActivityFeed Component - Admin Re-export with Config
 *
 * Re-exports the generic ActivityList from the UI library with admin-specific defaults.
 * The library component contains the full implementation.
 *
 * Migration note:
 * The old ActivityFeed and ActivityTimeline have been consolidated into ActivityList.
 * Use iconMode="severity" for the old ActivityFeed behavior.
 * Use iconMode="category" for the old ActivityTimeline behavior.
 *
 * @module components/admin/ActivityFeed
 * @see Issue #2925 - Component Library extraction
 * @see Issue #2803 - Tech debt consolidation
 */

'use client';

import {
  ActivityList,
  type ActivityListProps,
  type ActivityItem,
} from '@/components/ui/data-display/activity-list';

// Re-export the ActivityEvent type as ActivityFeedEvent for backward compatibility
export type { ActivityItem as ActivityEvent };

export interface ActivityFeedProps extends Omit<ActivityListProps, 'events'> {
  events: ActivityItem[];
}

/**
 * ActivityFeed - Admin activity feed with default admin settings
 *
 * Wraps ActivityList with admin-specific defaults:
 * - viewAllHref points to /admin/activity
 * - Severity-based icons by default
 */
export function ActivityFeed({
  events,
  viewAllHref = '/admin/activity',
  iconMode = 'severity',
  locale = 'en',
  ...props
}: ActivityFeedProps) {
  return (
    <ActivityList
      events={events}
      viewAllHref={viewAllHref}
      iconMode={iconMode}
      locale={locale}
      {...props}
    />
  );
}
