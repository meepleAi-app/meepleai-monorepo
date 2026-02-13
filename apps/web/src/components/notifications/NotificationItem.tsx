/**
 * NotificationItem Component (Issue #2053)
 *
 * Individual notification card in the panel.
 * Displays notification with icon, title, message, timestamp.
 *
 * Features:
 * - Click to mark as read and navigate to link
 * - Visual distinction for unread (bold, background)
 * - Severity-based icon and color
 * - Relative timestamp (e.g., "2 hours ago")
 * - Truncated message with hover tooltip
 */

'use client';

import {
  CheckCircle2,
  Info,
  AlertTriangle,
  XCircle,
  FileCheck,
  MessageSquare,
  Link as LinkIcon,
} from 'lucide-react';
import { useRouter } from 'next/navigation';

import { PdfStatusBadge } from '@/components/pdf';
import type { NotificationDto, NotificationSeverity, NotificationType } from '@/lib/api';
import { cn } from '@/lib/utils';
import { useNotificationStore } from '@/store/notification/store';

export interface NotificationItemProps {
  notification: NotificationDto;
}

export function NotificationItem({ notification }: NotificationItemProps) {
  const router = useRouter();
  const markAsRead = useNotificationStore(state => state.markAsRead);

  const handleClick = () => {
    // Mark as read
    if (!notification.isRead) {
      void markAsRead(notification.id);
    }

    // Navigate to link if exists
    if (notification.link) {
      router.push(notification.link);
    }
  };

  const { icon: Icon, color } = getSeverityConfig(notification.severity);
  const TypeIcon = getTypeIcon(notification.type);

  // Format relative timestamp
  const timeAgo = getRelativeTime(new Date(notification.createdAt));

  // Check if PDF-related notification (Issue #4217)
  const isPdfNotification =
    notification.type === 'pdf_upload_completed' || notification.type === 'processing_failed';

  return (
    <button
      onClick={handleClick}
      className={cn(
        'w-full text-left p-3 hover:bg-muted/50 transition-colors cursor-pointer',
        !notification.isRead && 'bg-muted/30 font-medium'
      )}
      aria-label={`Notification: ${notification.title}`}
      aria-pressed={!notification.isRead}
    >
      <div className="flex gap-3">
        {/* Icon */}
        <div className={cn('flex-shrink-0 mt-0.5', color)}>
          {Icon && <Icon className="h-5 w-5" />}
        </div>

        {/* Content */}
        <div className="flex-1 min-w-0">
          {/* Title with type icon and PDF status badge */}
          <div className="flex items-center gap-2 mb-1 flex-wrap">
            <TypeIcon className="h-4 w-4 text-muted-foreground flex-shrink-0" />
            <span className="text-sm truncate">{notification.title}</span>
            {/* New: PdfStatusBadge for PDF notifications (Issue #4217) */}
            {isPdfNotification && (
              <PdfStatusBadge
                state={notification.type === 'pdf_upload_completed' ? 'ready' : 'failed'}
                variant="compact"
                showIcon={false}
              />
            )}
          </div>

          {/* Message */}
          <p className="text-xs text-muted-foreground line-clamp-2">{notification.message}</p>

          {/* Timestamp */}
          <p className="text-xs text-muted-foreground/70 mt-1">{timeAgo}</p>
        </div>

        {/* Unread indicator */}
        {!notification.isRead && (
          <div className="flex-shrink-0">
            <div className="h-2 w-2 rounded-full bg-blue-500" aria-label="Unread" />
          </div>
        )}
      </div>
    </button>
  );
}

// ============================================================================
// Utility Functions
// ============================================================================

function getSeverityConfig(severity: string): {
  icon: React.ComponentType<{ className?: string }> | null;
  color: string;
} {
  switch (severity as NotificationSeverity) {
    case 'success':
      return { icon: CheckCircle2, color: 'text-green-600 dark:text-green-400' };
    case 'info':
      return { icon: Info, color: 'text-blue-600 dark:text-blue-400' };
    case 'warning':
      return { icon: AlertTriangle, color: 'text-yellow-600 dark:text-yellow-400' };
    case 'error':
      return { icon: XCircle, color: 'text-red-600 dark:text-red-400' };
    default:
      return { icon: Info, color: 'text-muted-foreground' };
  }
}

function getTypeIcon(type: string): React.ComponentType<{ className?: string }> {
  switch (type as NotificationType) {
    case 'pdf_upload_completed':
    case 'rule_spec_generated':
      return FileCheck;
    case 'new_comment':
      return MessageSquare;
    case 'shared_link_accessed':
      return LinkIcon;
    case 'processing_failed':
      return XCircle;
    default:
      return Info;
  }
}

function getRelativeTime(date: Date): string {
  const now = new Date();
  const diffMs = now.getTime() - date.getTime();
  const diffMins = Math.floor(diffMs / 60000);
  const diffHours = Math.floor(diffMins / 60);
  const diffDays = Math.floor(diffHours / 24);

  if (diffMins < 1) return 'Just now';
  if (diffMins < 60) return `${diffMins}m ago`;
  if (diffHours < 24) return `${diffHours}h ago`;
  if (diffDays < 7) return `${diffDays}d ago`;

  // Fallback to date string for older notifications
  return date.toLocaleDateString();
}
