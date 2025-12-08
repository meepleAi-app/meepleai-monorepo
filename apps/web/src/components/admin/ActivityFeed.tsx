/**
 * ActivityFeed Component - Issue #874
 *
 * Displays recent system activity events with severity indicators.
 * Shows last 10 events (user registrations, uploads, errors, alerts).
 */

import { Card, CardHeader, CardTitle, CardContent } from '@/components/ui/card';
import { cn } from '@/lib/utils';
import {
  UserPlusIcon,
  FileUpIcon,
  AlertTriangleIcon,
  AlertCircleIcon,
  CheckCircleIcon,
  XCircleIcon,
  ActivityIcon,
} from 'lucide-react';

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
}

const severityStyles = {
  Info: 'text-blue-600 bg-blue-50',
  Warning: 'text-yellow-600 bg-yellow-50',
  Error: 'text-red-600 bg-red-50',
  Critical: 'text-red-700 bg-red-100',
};

const eventIcons = {
  UserRegistered: UserPlusIcon,
  UserLogin: UserPlusIcon,
  PdfUploaded: FileUpIcon,
  PdfProcessed: CheckCircleIcon,
  AlertCreated: AlertTriangleIcon,
  AlertResolved: CheckCircleIcon,
  GameAdded: ActivityIcon,
  ConfigurationChanged: ActivityIcon,
  ErrorOccurred: XCircleIcon,
  SystemEvent: ActivityIcon,
};

export function ActivityFeed({ events, className, maxEvents = 10 }: ActivityFeedProps) {
  const displayEvents = events.slice(0, maxEvents);

  return (
    <Card className={className}>
      <CardHeader>
        <CardTitle className="text-lg font-semibold">Recent Activity</CardTitle>
      </CardHeader>
      <CardContent className="p-0">
        {displayEvents.length === 0 ? (
          <div className="p-8 text-center text-gray-500">
            <p>No recent activity</p>
          </div>
        ) : (
          <ul className="divide-y divide-gray-100" role="list">
            {displayEvents.map(event => {
              const Icon = eventIcons[event.eventType as keyof typeof eventIcons] || ActivityIcon;
              const severity = event.severity || 'Info';

              return (
                <li key={event.id} className="px-6 py-4 hover:bg-gray-50 transition-colors">
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
                        <time dateTime={event.timestamp}>
                          {new Date(event.timestamp).toLocaleString('it-IT', {
                            year: 'numeric',
                            month: 'short',
                            day: 'numeric',
                            hour: '2-digit',
                            minute: '2-digit',
                          })}
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
        )}
      </CardContent>
    </Card>
  );
}
