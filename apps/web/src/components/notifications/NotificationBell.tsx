/**
 * NotificationBell Component (Issue #2053, updated #4947)
 *
 * Bell icon with unread count badge.
 * Opens NotificationCenter Sheet on click (Issue #4947).
 *
 * Features:
 * - Badge with unread count (0 = hidden, 1-9 = number, 10+ = "9+")
 * - Sheet panel with notification list (NUOVE / PRECEDENTI sections)
 * - KB-ready notification CTA (Issue #4947)
 * - Keyboard accessible (Space/Enter to toggle)
 * - Auto-fetch unread count on mount
 * - SSE real-time updates (Issue #4414)
 */

'use client';

import { useEffect, useState } from 'react';

import { Bell } from 'lucide-react';

import { NotificationCenter } from '@/components/layout/Navbar/NotificationCenter';
import { Badge } from '@/components/ui/data-display/badge';
import { Button } from '@/components/ui/primitives/button';
import { useNotificationSSE } from '@/hooks/useNotificationSSE';
import { useNotificationStore, selectUnreadCount } from '@/store/notification/store';

export function NotificationBell() {
  const unreadCount = useNotificationStore(selectUnreadCount);
  const fetchUnreadCount = useNotificationStore(state => state.fetchUnreadCount);
  const [isOpen, setIsOpen] = useState(false);

  // SSE real-time notifications (Issue #4414, #4736, #5005)
  useNotificationSSE({ enabled: true });

  // Fetch unread count on mount
  useEffect(() => {
    void fetchUnreadCount();
  }, [fetchUnreadCount]);

  // Format badge count (0 = hidden, 1-9 = number, 10+ = "9+")
  const badgeCount = unreadCount === 0 ? null : unreadCount > 9 ? '9+' : unreadCount.toString();

  return (
    <>
      <Button
        variant="ghost"
        size="icon"
        className="relative"
        aria-label={`Notifications${unreadCount > 0 ? ` (${unreadCount} unread)` : ''}`}
        aria-expanded={isOpen}
        onClick={() => setIsOpen(true)}
        data-testid="notification-bell-button"
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

      <NotificationCenter open={isOpen} onOpenChange={setIsOpen} />
    </>
  );
}
