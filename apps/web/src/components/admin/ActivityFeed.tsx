/* eslint-disable security/detect-object-injection -- Safe style/icon map Record access */
/**
 * Unified ActivityFeed Component - Issue #2803
 *
 * Consolidated component combining features from ActivityFeed and ActivityTimeline.
 * Eliminates 85%+ code duplication while preserving all functionality.
 *
 * Features:
 * - Dual icon systems: severity-based (default) or category-based
 * - i18n support: English (default) or Italian
 * - Optional staggered entry animation (framer-motion)
 * - Warning/Error highlighting (when using severity icons)
 * - Relative timestamps with locale support
 * - Scrollable container (max 10 events default)
 * - View All link
 *
 * Migration from ActivityTimeline:
 * ```tsx
 * // Old ActivityTimeline usage:
 * <ActivityTimeline events={events} />
 *
 * // New ActivityFeed usage (equivalent):
 * <ActivityFeed events={events} iconMode="category" animated locale="it" />
 * ```
 *
 * @see Issue #2803 - Tech debt consolidation
 * @see Issue #2787 - ActivityTimeline implementation
 * @see Issue #884 - Original ActivityFeed
 * @see Issue #2849 - Warning/Error highlighting
 */

'use client';

import { useMemo } from 'react';

import { ActivityIcon, ArrowRightIcon } from 'lucide-react';
import Link from 'next/link';
import { motion } from 'framer-motion';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import {
  severityStyles,
  eventIcons,
  typeIcons,
  getActivityType,
  formatRelativeTimestamp,
  type ActivityEvent,
} from './utils/activityUtils';

/**
 * i18n strings for UI text
 */
const i18nStrings = {
  en: {
    title: 'Recent Activity',
    emptyState: 'No recent activity',
    viewAll: 'View All',
    ariaLabel: 'View all activity',
    regionLabel: 'Activity feed',
  },
  it: {
    title: 'Attività Recenti',
    emptyState: 'Nessuna attività recente',
    viewAll: 'Vedi tutto',
    ariaLabel: 'Vedi tutte le attività',
    regionLabel: 'Timeline attività',
  },
} as const;

export interface ActivityFeedProps {
  events: ActivityEvent[];
  className?: string;
  maxEvents?: number;
  viewAllHref?: string;
  showViewAll?: boolean;

  /**
   * Icon display mode:
   * - 'severity': Color-coded by severity level (Info/Warning/Error/Critical) with background highlighting
   * - 'category': Semantic categories (user/game/system/ai) with colored icons
   *
   * @default 'severity'
   */
  iconMode?: 'severity' | 'category';

  /**
   * Enable staggered entry animation using framer-motion.
   * Adds smooth fade-in and slide-up effect for each event item.
   *
   * @default false
   */
  animated?: boolean;

  /**
   * UI language for titles, labels, and empty states.
   * Relative timestamps will also use the corresponding date-fns locale.
   *
   * @default 'en'
   */
  locale?: 'en' | 'it';
}

/**
 * Unified activity feed/timeline component with configurable icon modes and i18n.
 *
 * Combines best features from ActivityFeed (severity-based icons, warning/error highlighting)
 * and ActivityTimeline (category-based icons, animation, modern design tokens).
 *
 * Defaults preserve original ActivityFeed behavior for backward compatibility.
 */
export function ActivityFeed({
  events,
  className,
  maxEvents = 10,
  viewAllHref = '/admin/activity',
  showViewAll = true,
  iconMode = 'severity',
  animated = false,
  locale = 'en',
}: ActivityFeedProps) {
  const displayEvents = useMemo(() => events.slice(0, maxEvents), [events, maxEvents]);
  const hasMoreEvents = events.length > maxEvents;
  const strings = i18nStrings[locale];

  // Choose list item wrapper based on animation prop
  const ListItem = animated ? motion.li : 'li';
  const listItemProps = animated
    ? (index: number) => ({
        initial: { opacity: 0, y: 10 },
        animate: { opacity: 1, y: 0 },
        transition: { delay: index * 0.05, duration: 0.3 },
      })
    : (_index: number) => ({});

  return (
    <Card className={className}>
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-lg font-semibold">{strings.title}</CardTitle>
        {showViewAll && (hasMoreEvents || events.length > 0) && (
          <Link
            href={viewAllHref}
            className="inline-flex items-center gap-1 text-sm text-blue-600 hover:text-blue-800 hover:underline transition-colors"
            aria-label={strings.ariaLabel}
          >
            {strings.viewAll}
            <ArrowRightIcon className="h-4 w-4" aria-hidden="true" />
          </Link>
        )}
      </CardHeader>
      <CardContent className="p-0">
        {displayEvents.length === 0 ? (
          <div className="p-8 text-center text-muted-foreground">
            <ActivityIcon className="h-12 w-12 mx-auto mb-2 text-muted-foreground/50" aria-hidden="true" />
            <p>{strings.emptyState}</p>
          </div>
        ) : (
          <div className="max-h-[480px] overflow-y-auto" role="region" aria-label={strings.regionLabel}>
            <ul className="divide-y divide-gray-100" role="list">
              {displayEvents.map((event, index) => {
                // Icon selection based on mode
                let Icon: React.ComponentType<{ className?: string }>;
                let iconColorClass: string;
                let itemBgClass = '';
                let borderLeftClass = '';

                if (iconMode === 'category') {
                  // Category-based icons (ActivityTimeline style)
                  const activityType = getActivityType(event.eventType);
                  const typeConfig = typeIcons[activityType];
                  Icon = typeConfig.icon;
                  iconColorClass = `${typeConfig.colorClass} bg-muted/50 dark:bg-muted/30`;
                } else {
                  // Severity-based icons (ActivityFeed style)
                  Icon = eventIcons[event.eventType as keyof typeof eventIcons] || ActivityIcon;
                  const severity = event.severity || 'Info';
                  iconColorClass = severityStyles[severity];

                  // Warning/Error highlighting (Issue #2849)
                  if (severity === 'Warning') {
                    itemBgClass = 'bg-yellow-50 dark:bg-yellow-500/10';
                    borderLeftClass = 'border-l-4 border-l-yellow-500';
                  } else if (severity === 'Error' || severity === 'Critical') {
                    itemBgClass = 'bg-red-50 dark:bg-red-500/10';
                    borderLeftClass = 'border-l-4 border-l-red-500';
                  } else {
                    borderLeftClass = 'border-l-4 border-l-transparent';
                  }
                }

                const relativeTime = formatRelativeTimestamp(event.timestamp, locale);

                // Hover color based on icon mode
                const hoverClass = iconMode === 'category'
                  ? 'hover:bg-muted/50 dark:hover:bg-muted/30'
                  : 'hover:bg-meeple-light-orange/30';

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
                  <ListItem
                    key={event.id}
                    className={cn(
                      'px-6 py-4 transition-all',
                      hoverClass,
                      borderLeftClass,
                      itemBgClass
                    )}
                    {...listItemProps(index)}
                  >
                    <div className="flex items-start gap-4">
                      <div
                        className={cn(
                          'flex-shrink-0 h-10 w-10 rounded-full flex items-center justify-center',
                          iconColorClass
                        )}
                        aria-label={iconMode === 'category' ? `${getActivityType(event.eventType)} activity` : `${event.severity || 'Info'} event`}
                      >
                        <Icon className="h-5 w-5" aria-hidden="true" />
                      </div>
                      <div className="flex-1 min-w-0">
                        <p className="text-sm font-medium text-foreground">{event.description}</p>
                        <div className="mt-1 flex items-center gap-4 text-xs text-muted-foreground">
                          <time
                            dateTime={event.timestamp}
                            title={new Date(event.timestamp).toLocaleString(locale === 'it' ? 'it-IT' : 'en-US')}
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
                  </ListItem>
                );
              })}
            </ul>
          </div>
        )}
      </CardContent>
    </Card>
  );
}

// Re-export ActivityEvent for convenience
export type { ActivityEvent };
