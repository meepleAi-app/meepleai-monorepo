/**
 * NotificationPanel Component (Issue #2053)
 *
 * Dropdown panel displaying notification list.
 * Shown when NotificationBell is clicked.
 *
 * Features:
 * - Header with title and "Mark all read" action
 * - Scrollable notification list
 * - Empty state when no notifications
 * - Loading state
 * - Error handling with fallback UI
 */

'use client';

import { useEffect } from 'react';
import { CheckCheck } from 'lucide-react';
import { Button } from '@/components/ui/button';
import { Separator } from '@/components/ui/separator';
import {
  useNotificationStore,
  selectNotifications,
  selectIsLoading,
} from '@/store/notification/store';
import { NotificationItem } from './NotificationItem';

export function NotificationPanel() {
  const notifications = useNotificationStore(selectNotifications);
  const isLoading = useNotificationStore(selectIsLoading);
  const isFetching = useNotificationStore(state => state.isFetching);
  const error = useNotificationStore(state => state.error);
  const fetchNotifications = useNotificationStore(state => state.fetchNotifications);
  const markAllAsRead = useNotificationStore(state => state.markAllAsRead);

  // Fetch notifications on panel open
  useEffect(() => {
    void fetchNotifications({ limit: 50 });
  }, [fetchNotifications]);

  const hasUnread = notifications.some(n => !n.isRead);

  return (
    <div className="flex flex-col max-h-[500px]">
      {/* Header */}
      <div className="flex items-center justify-between p-4">
        <h3 className="font-semibold text-sm">Notifications</h3>
        {hasUnread && (
          <Button
            variant="ghost"
            size="sm"
            onClick={() => void markAllAsRead()}
            className="h-8 text-xs"
            aria-label="Mark all notifications as read"
          >
            <CheckCheck className="h-4 w-4 mr-1" />
            Mark all read
          </Button>
        )}
      </div>

      <Separator />

      {/* Content */}
      <div className="flex-1 overflow-y-auto">
        {/* Loading State */}
        {isFetching && notifications.length === 0 && (
          <div className="flex items-center justify-center p-8 text-muted-foreground text-sm">
            Loading notifications...
          </div>
        )}

        {/* Error State */}
        {error && (
          <div className="flex items-center justify-center p-8 text-destructive text-sm">
            {error}
          </div>
        )}

        {/* Empty State */}
        {!isFetching && !error && notifications.length === 0 && (
          <div className="flex flex-col items-center justify-center p-8 text-center">
            <Bell className="h-12 w-12 text-muted-foreground/50 mb-2" />
            <p className="text-sm text-muted-foreground">No notifications yet</p>
            <p className="text-xs text-muted-foreground/70 mt-1">
              You&apos;ll be notified when uploads complete or comments arrive
            </p>
          </div>
        )}

        {/* Notification List */}
        {!error && notifications.length > 0 && (
          <div className="divide-y">
            {notifications.map(notification => (
              <NotificationItem key={notification.id} notification={notification} />
            ))}
          </div>
        )}
      </div>
    </div>
  );
}

// Bell icon import for empty state
import { Bell } from 'lucide-react';
