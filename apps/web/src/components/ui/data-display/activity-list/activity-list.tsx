/* eslint-disable security/detect-object-injection -- Safe style/icon map Record access */
/**
 * ActivityList Component - Generic Activity Feed/Timeline
 *
 * Extracted from Admin ActivityFeed for reuse across the application.
 * Displays a scrollable list of activity events with configurable styling.
 *
 * @module components/ui/data-display/activity-list
 * @see Issue #2925 - Component Library extraction
 *
 * Features:
 * - Dual icon systems: severity-based or category-based
 * - i18n support: English (default) or Italian
 * - Optional staggered entry animation (framer-motion)
 * - Warning/Error highlighting
 * - Relative timestamps with locale support
 * - Scrollable container
 * - View All link
 *
 * @example
 * ```tsx
 * // Basic usage
 * <ActivityList events={events} />
 *
 * // With animations and Italian locale
 * <ActivityList
 *   events={events}
 *   iconMode="category"
 *   animated
 *   locale="it"
 * />
 * ```
 */

'use client';

import { useMemo } from 'react';

import { motion } from 'framer-motion';
import { ActivityIcon, ArrowRightIcon } from 'lucide-react';
import Link from 'next/link';

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/data-display/card';
import { cn } from '@/lib/utils';

import {
  severityStyles,
  eventIcons,
  categoryIcons,
  getActivityCategory,
  formatRelativeTimestamp,
  type ActivityItem,
  type SupportedLocale,
} from './utils';

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
    viewAll: 'Vedi tutte',
    ariaLabel: 'Vedi tutte le attività',
    regionLabel: 'Timeline attività',
  },
};

/**
 * Props for the ActivityList component
 */
export interface ActivityListProps {
  /** Array of activity events to display */
  events: ActivityItem[];
  /** Additional CSS classes */
  className?: string;
  /** Maximum number of events to show (default: 10) */
  maxEvents?: number;
  /** URL for the "View All" link */
  viewAllHref?: string;
  /** Show the "View All" link (default: true) */
  showViewAll?: boolean;
  /** Custom title for the card */
  title?: string;
  /**
   * Icon display mode:
   * - 'severity': Color-coded by severity level (Info/Warning/Error/Critical)
   * - 'category': Semantic categories (user/content/system/ai)
   *
   * @default 'severity'
   */
  iconMode?: 'severity' | 'category';
  /**
   * Enable staggered entry animation using framer-motion
   *
   * @default false
   */
  animated?: boolean;
  /**
   * UI language for titles, labels, and timestamps
   *
   * @default 'en'
   */
  locale?: SupportedLocale;
}

/**
 * ActivityList - A reusable activity feed/timeline component
 *
 * Displays a scrollable list of activity events with configurable icon modes,
 * animations, and i18n support.
 */
export function ActivityList({
  events,
  className,
  maxEvents = 10,
  viewAllHref = '/activity',
  showViewAll = true,
  title: customTitle,
  iconMode = 'severity',
  animated = false,
  locale = 'en',
}: ActivityListProps) {
  const displayEvents = useMemo(() => events.slice(0, maxEvents), [events, maxEvents]);
  const hasMoreEvents = events.length > maxEvents;
  const strings = i18nStrings[locale];
  const title = customTitle ?? strings.title;

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
    <Card className={className} data-testid="activity-list">
      <CardHeader className="flex flex-row items-center justify-between pb-2">
        <CardTitle className="text-lg font-semibold" data-testid="activity-list-title">
          {title}
        </CardTitle>
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
          <div className="p-8 text-center text-muted-foreground" data-testid="activity-list-empty">
            <ActivityIcon className="h-12 w-12 mx-auto mb-2 text-muted-foreground/50" aria-hidden="true" />
            <p data-testid="activity-list-empty-message">{strings.emptyState}</p>
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
                  // Category-based icons
                  const category = getActivityCategory(event.eventType);
                  const categoryConfig = categoryIcons[category];
                  Icon = categoryConfig.icon;
                  iconColorClass = `${categoryConfig.colorClass} bg-muted/50 dark:bg-muted/30`;
                } else {
                  // Severity-based icons
                  Icon = eventIcons[event.eventType] || ActivityIcon;
                  const severity = event.severity || 'Info';
                  iconColorClass = severityStyles[severity];

                  // Warning/Error highlighting
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
                  : 'hover:bg-orange-50';

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
                        aria-label={iconMode === 'category' ? `${getActivityCategory(event.eventType)} activity` : `${event.severity || 'Info'} event`}
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

// Re-export types for convenience
export type { ActivityItem, SupportedLocale };
