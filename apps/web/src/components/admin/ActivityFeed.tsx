/* eslint-disable security/detect-object-injection -- Safe style/icon map Record access */
/**
 * ActivityFeed Component - Issue #884
 *
 * Timeline for recent system activity events with severity indicators.
 * Features:
 * - Timeline layout with event type icons
 * - Relative timestamps (e.g., "5 minuti fa")
 * - Scrollable container (max 10 events)
 * - View All link
 */

import { useMemo } from 'react';

import { ActivityIcon, ArrowRightIcon } from 'lucide-react';
import Link from 'next/link';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import { severityStyles, eventIcons, formatRelativeTimestamp } from './utils/activityUtils';

export interface ActivityEvent {
  id: string;
  eventType: string;
  description: string;
  userId?: string | null;
  userEmail?: string | null;
  entityId?: string | null;
  entityType?: string | null;
  timestamp: string;
  severity?: 'Info' | 'Warning' | 'Error' | 'Critical';
}

export interface ActivityFeedProps {
  events: ActivityEvent[];
  className?: string;
  maxEvents?: number;
  viewAllHref?: string;
  showViewAll?: boolean;
}

export function ActivityFeed({
  events,
  className,
  maxEvents = 10,
  viewAllHref = '/admin/activity',
  showViewAll = true,
}: ActivityFeedProps) {
  const displayEvents = useMemo(() => events.slice(0, maxEvents), [events, maxEvents]);
  const hasMoreEvents = events.length > maxEvents;

  return (
    <Card className={className}>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-lg font-semibold">Recent Activity</CardTitle>
        {showViewAll && (hasMoreEvents || events.length > 0) && (
          <Link
            href={viewAllHref}
            className="inline-flex items-center gap-1 text-sm text-blue-600 hover:text-blue-800 hover:underline transition-colors"
            aria-label="View all activity"
          >
            View All
            <ArrowRightIcon className="h-4 w-4" aria-hidden="true" />
          </Link>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {displayEvents.length === 0 ? (
          <div className="p-8 text-center text-gray-500">
            <ActivityIcon className="h-12 w-12 mx-auto mb-2 text-gray-300" aria-hidden="true" />
            <p>No recent activity</p>
          </div>
        ) : (
          <div className="max-h-[480px] overflow-y-auto" role="region" aria-label="Activity feed">
            <ul className="divide-y divide-gray-100" role="list">
              {displayEvents.map(event => {
                const Icon = eventIcons[event.eventType as keyof typeof eventIcons] || ActivityIcon;
                const severity = event.severity || 'Info';
                const relativeTime = formatRelativeTimestamp(event.timestamp);

                // Background and border for warning/error items (Issue #2849)
                const itemBgClass = severity === 'Warning'
                  ? 'bg-yellow-50 dark:bg-yellow-500/10'
                  : severity === 'Error' || severity === 'Critical'
                  ? 'bg-red-50 dark:bg-red-500/10'
                  : '';

                const borderLeftClass = severity === 'Warning'
                  ? 'border-l-4 border-l-yellow-500'
                  : severity === 'Error' || severity === 'Critical'
                  ? 'border-l-4 border-l-red-500'
                  : 'border-l-4 border-l-transparent';

                return (
                  <li
                    key={event.id}
                    className={cn(
                      'px-6 py-4 transition-all rounded-lg',
                      'hover:bg-meeple-light-orange/30',
                      borderLeftClass,
                      itemBgClass
                    )}
                  >
                    <div className="flex items-start gap-4">
                      <div
                        className={cn(
                          'flex-shrink-0 h-10 w-10 rounded-full flex items-center justify-center',
                          severityStyles[severity]
                        )}
                        aria-label={`${severity} event`}
                      >
                        <Icon className="h-5 w-5" aria-hidden="true" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-gray-900">{event.description}</p>
                        <div className="mt-1 flex items-center gap-4 text-xs text-gray-500">
                          <time
                            dateTime={event.timestamp}
                            title={new Date(event.timestamp).toLocaleString('it-IT')}
                          >
                            {relativeTime}
                          </time>
                          {event.userEmail && (
                            <span className="truncate" title={event.userEmail}>
                              {event.userEmail}
                            </span>
                          )}
                        </div>
                      </div>
                    </div>
                  </li>
                );
              })}
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  );
}