/**
 * NotificationBell Component (Issue #2053)
 *
 * Bell icon with unread count badge.
 * Opens notification panel on click.
 *
 * Features:
 * - Badge with unread count (0 = hidden, 1-9 = number, 10+ = "9+")
 * - Dropdown panel with notification list
 * - Keyboard accessible (Space/Enter to toggle)
 * - Auto-fetch unread count on mount
 * - SSE real-time updates (Issue #4414)
 */

'use client';

import { useEffect } from 'react';

import { Bell } from 'lucide-react';

import { Badge } from '@/components/ui/data-display/badge';
import {
  DropdownMenu,
  DropdownMenuTrigger,
  DropdownMenuContent,
} from '@/components/ui/navigation/dropdown-menu';
import { Button } from '@/components/ui/primitives/button';
import { useNotificationSSE } from '@/hooks/useNotificationSSE';
import { useNotificationStore, selectUnreadCount } from '@/store/notification/store';

import { NotificationPanel } from './NotificationPanel';

export function NotificationBell() {
  const unreadCount = useNotificationStore(selectUnreadCount);
  const fetchUnreadCount = useNotificationStore(state => state.fetchUnreadCount);

  // SSE real-time notifications (Issue #4414, #4736)
  // Gracefully degrades if /api/v1/notifications/stream endpoint is unavailable
  useNotificationSSE({ enabled: true });

  // Fetch unread count on mount
  useEffect(() => {
    void fetchUnreadCount();
  }, [fetchUnreadCount]);

  // Format badge count (0 = hidden, 1-9 = number, 10+ = "9+")
  const badgeCount = unreadCount === 0 ? null : unreadCount > 9 ? '9+' : unreadCount.toString();

  return (
    <DropdownMenu>
      <DropdownMenuTrigger asChild>
        <Button
          variant="ghost"
          size="icon"
          className="relative"
          aria-label={`Notifications${unreadCount > 0 ? ` (${unreadCount} unread)` : ''}`}
        >
          <Bell className="h-5 w-5" />
          {badgeCount && (
            <Badge
              variant="destructive"
              className="absolute -top-1 -right-1 h-5 min-w-5 px-1 flex items-center justify-center text-xs"
            >
              {badgeCount}
            </Badge>
          )}
        </Button>
      </DropdownMenuTrigger>
      <DropdownMenuContent align="end" className="w-80">
        <NotificationPanel />
      </DropdownMenuContent>
    </DropdownMenu>
  );
}
