/* eslint-disable security/detect-object-injection -- Safe severity/icon map Record access */
/**
 * UserActivityItem Component - Issue #911
 *
 * Single user activity item with expandable metadata.
 * Features:
 * - Activity type icon with severity color
 * - Relative timestamp (Italian)
 * - Expandable metadata section
 * - User email and entity info
 */

import { useState } from 'react';

import { ChevronDownIcon, ChevronUpIcon, ActivityIcon } from 'lucide-react';

import { cn } from '@/lib/utils';

import { severityStyles, eventIcons, formatRelativeTimestamp } from './utils/activityUtils';

export interface UserActivityEvent {
  id: string;
  eventType: string;
  description: string;
  userId?: string | null;
  userEmail?: string | null;
  entityId?: string | null;
  entityType?: string | null;
  timestamp: string;
  severity?: 'Info' | 'Warning' | 'Error' | 'Critical';
  metadata?: Record<string, unknown>;
}

export interface UserActivityItemProps {
  event: UserActivityEvent;
  isExpanded?: boolean;
  onToggleExpand?: (eventId: string) => void;
}

export function UserActivityItem({
  event,
  isExpanded = false,
  onToggleExpand,
}: UserActivityItemProps) {
  const [localExpanded, setLocalExpanded] = useState(isExpanded);
  const expanded = onToggleExpand ? isExpanded : localExpanded;

  const handleToggle = () => {
    if (onToggleExpand) {
      onToggleExpand(event.id);
    } else {
      setLocalExpanded(!localExpanded);
    }
  };

  const Icon = eventIcons[event.eventType as keyof typeof eventIcons] || ActivityIcon;
  const severity = event.severity || 'Info';
  const relativeTime = formatRelativeTimestamp(event.timestamp);
  const hasMetadata = event.metadata && Object.keys(event.metadata).length > 0;

  return (
    <li className="px-6 py-4 hover:bg-muted/50 dark:hover:bg-muted/30 transition-colors">
      <div className="flex items-start gap-4">
        {/* Icon */}
        <div
          className={cn(
            'flex-shrink-0 h-10 w-10 rounded-full flex items-center justify-center',
            severityStyles[severity]
          )}
          aria-label={`${severity} event`}
        >
          <Icon className="h-5 w-5" aria-hidden="true" />
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          <div className="flex items-start justify-between gap-2">
            <div className="flex-1 min-w-0">
              <p className="text-sm font-medium text-foreground">{event.description}</p>
              <div className="mt-1 flex items-center gap-4 text-xs text-muted-foreground">
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

            {/* Expand/Collapse Button */}
            {hasMetadata && (
              <button
                onClick={handleToggle}
                className="flex-shrink-0 p-1 rounded hover:bg-muted dark:hover:bg-muted/70 transition-colors"
                aria-expanded={expanded}
                aria-label={expanded ? 'Nascondi dettagli' : 'Mostra dettagli'}
                title={expanded ? 'Nascondi dettagli' : 'Mostra dettagli'}
              >
                {expanded ? (
                  <ChevronUpIcon className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
                ) : (
                  <ChevronDownIcon className="h-5 w-5 text-muted-foreground" aria-hidden="true" />
                )}
              </button>
            )}
          </div>

          {/* Expanded Metadata */}
          {expanded && hasMetadata && (
            <div className="mt-3 pt-3 border-t border-border/50 dark:border-border/30">
              <div className="text-xs">
                <p className="font-semibold text-foreground mb-2">Dettagli:</p>
                <dl className="space-y-1">
                  {event.userId && (
                    <div className="flex gap-2">
                      <dt className="font-medium text-muted-foreground min-w-[80px]">User ID:</dt>
                      <dd className="text-foreground break-all">{event.userId}</dd>
                    </div>
                  )}
                  {event.entityId && (
                    <div className="flex gap-2">
                      <dt className="font-medium text-muted-foreground min-w-[80px]">Entity ID:</dt>
                      <dd className="text-foreground break-all">{event.entityId}</dd>
                    </div>
                  )}
                  {event.entityType && (
                    <div className="flex gap-2">
                      <dt className="font-medium text-muted-foreground min-w-[80px]">Entity Type:</dt>
                      <dd className="text-foreground">{event.entityType}</dd>
                    </div>
                  )}
                  {event.metadata && (
                    <div className="flex gap-2">
                      <dt className="font-medium text-muted-foreground min-w-[80px]">Metadata:</dt>
                      <dd className="flex-1">
                        <pre className="text-foreground bg-muted dark:bg-muted/50 p-2 rounded text-[11px] overflow-x-auto">
                          {JSON.stringify(event.metadata, null, 2)}
                        </pre>
                      </dd>
                    </div>
                  )}
                </dl>
              </div>
            </div>
          )}
        </div>
      </div>
    </li>
  );
}
