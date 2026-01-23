/* eslint-disable security/detect-object-injection -- Safe Record access for type icons */
/**
 * ActivityTimeline Component - Issue #2787
 *
 * Timeline for recent system activity events with modern design system.
 * Replaces ActivityFeed with enhanced UI and category-based colored icons.
 *
 * Features:
 * - Category icons: user (blue), game (green), system (stone), ai (purple)
 * - Relative timestamps in Italian (e.g., "5 minuti fa")
 * - Hover effect (stone-50 background)
 * - Staggered entry animation
 * - Scrollable container (max 10 events)
 * - "Vedi tutto" link to audit page
 */

'use client';

import { useMemo } from 'react';

import { motion } from 'framer-motion';
import { ActivityIcon, ArrowRightIcon } from 'lucide-react';
import Link from 'next/link';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';

import {
  typeIcons,
  getActivityType,
  formatRelativeTimestamp,
  type ActivityEvent,
} from './utils/activityUtils';

export interface ActivityTimelineProps {
  events: ActivityEvent[];
  className?: string;
  maxEvents?: number;
  viewAllHref?: string;
  showViewAll?: boolean;
}

export function ActivityTimeline({
  events,
  className,
  maxEvents = 10,
  viewAllHref = '/admin/audit',
  showViewAll = true,
}: ActivityTimelineProps) {
  const displayEvents = useMemo(() => events.slice(0, maxEvents), [events, maxEvents]);
  const hasMoreEvents = events.length > maxEvents;

  return (
    <Card className={className}>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-lg font-semibold">Attività Recenti</CardTitle>
        {showViewAll && (hasMoreEvents || events.length > 0) && (
          <Link
            href={viewAllHref}
            className="inline-flex items-center gap-1 text-sm text-blue-600 hover:text-blue-800 hover:underline transition-colors"
            aria-label="Vedi tutte le attività"
          >
            Vedi tutto
            <ArrowRightIcon className="h-4 w-4" aria-hidden="true" />
          </Link>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {displayEvents.length === 0 ? (
          <div className="p-8 text-center text-gray-500">
            <ActivityIcon className="h-12 w-12 mx-auto mb-2 text-gray-300" aria-hidden="true" />
            <p>Nessuna attività recente</p>
          </div>
        ) : (
          <div className="max-h-[480px] overflow-y-auto" role="region" aria-label="Timeline attività">
            <ul className="divide-y divide-gray-100" role="list">
              {displayEvents.map((event, index) => {
                const activityType = getActivityType(event.eventType);
                const { icon: Icon, colorClass } = typeIcons[activityType];
                const relativeTime = formatRelativeTimestamp(event.timestamp);

                return (
                  <motion.li
                    key={event.id}
                    initial={{ opacity: 0, y: 10 }}
                    animate={{ opacity: 1, y: 0 }}
                    transition={{ delay: index * 0.05, duration: 0.3 }}
                    className="px-6 py-4 hover:bg-stone-50 transition-colors"
                  >
                    <div className="flex items-start gap-4">
                      <div
                        className={`flex-shrink-0 h-10 w-10 rounded-full flex items-center justify-center ${colorClass} bg-muted/50 dark:bg-muted/30`}
                        aria-label={`${activityType} activity`}
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
                  </motion.li>
                );
              })}
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  );
}
