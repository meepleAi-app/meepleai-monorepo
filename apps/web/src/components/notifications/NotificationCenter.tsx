/**
 * NotificationCenter Component (Issue #4947)
 *
 * Side drawer (Sheet) showing notifications in two sections:
 * - "NUOVE" (unread) — teal left border + teal background tint
 * - "PRECEDENTI" (read)
 *
 * Special KB-ready notification type (`processing_job_completed`) shows
 * a prominent CTA button linking to the agent config page.
 *
 * Triggered by the NotificationBell in UniversalNavbar.
 */

'use client';

import { useEffect } from 'react';

import {
  Bell,
  BrainCircuit,
  CheckCheck,
  CheckCircle2,
  FileCheck,
  Info,
  AlertTriangle,
  XCircle,
  MessageSquare,
  Link as LinkIcon,
  ChevronRight,
  Settings,
  Shield,
  Trophy,
  Clock,
  Bot,
  BarChart3,
  Zap,
} from 'lucide-react';
import Link from 'next/link';
import { useRouter } from 'next/navigation';

import { Separator } from '@/components/ui/navigation/separator';
import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/navigation/sheet';
import { Button } from '@/components/ui/primitives/button';
import { useTranslation } from '@/hooks/useTranslation';
import type { NotificationDto } from '@/lib/api';
import { cn } from '@/lib/utils';
import { useNotificationStore, selectNotifications } from '@/store/notification/store';

// ============================================================================
// NotificationCenter Props
// ============================================================================

export interface NotificationCenterProps {
  open: boolean;
  onOpenChange: (open: boolean) => void;
}

// ============================================================================
// NotificationCenter Component
// ============================================================================

export function NotificationCenter({ open, onOpenChange }: NotificationCenterProps) {
  const { t } = useTranslation();
  const notifications = useNotificationStore(selectNotifications);
  const isFetching = useNotificationStore(state => state.isFetching);
  const error = useNotificationStore(state => state.error);
  const fetchNotifications = useNotificationStore(state => state.fetchNotifications);
  const markAllAsRead = useNotificationStore(state => state.markAllAsRead);

  // Fetch notifications when drawer opens
  useEffect(() => {
    if (open) {
      void fetchNotifications({ limit: 30 });
    }
  }, [open, fetchNotifications]);

  const unread = notifications.filter(n => !n.isRead);
  const read = notifications.filter(n => n.isRead);
  const hasUnread = unread.length > 0;

  return (
    <Sheet open={open} onOpenChange={onOpenChange}>
      <SheetContent
        side="right"
        className="w-[340px] sm:w-[400px] flex flex-col p-0"
        aria-label={t('notificationCenter.title')}
      >
        {/* Header */}
        <SheetHeader className="flex flex-row items-center justify-between p-4 pb-3 border-b border-border/50">
          <SheetTitle className="text-base font-semibold">
            {t('notificationCenter.title')}
          </SheetTitle>
          {hasUnread && (
            <Button
              variant="ghost"
              size="sm"
              onClick={() => void markAllAsRead()}
              className="h-8 text-xs gap-1 text-muted-foreground"
              aria-label={t('notificationCenter.markAllRead')}
            >
              <CheckCheck className="h-3.5 w-3.5" />
              {t('notificationCenter.markAllRead')}
            </Button>
          )}
        </SheetHeader>

        {/* Scrollable content */}
        <div className="flex-1 overflow-y-auto">
          {/* Loading state */}
          {isFetching && notifications.length === 0 && (
            <div className="flex items-center justify-center p-10 text-sm text-muted-foreground">
              {t('notificationCenter.loading')}
            </div>
          )}

          {/* Error state */}
          {error && !isFetching && (
            <div className="flex items-center justify-center p-10 text-sm text-destructive">
              {error}
            </div>
          )}

          {/* Empty state */}
          {!isFetching && !error && notifications.length === 0 && (
            <div className="flex flex-col items-center justify-center p-10 text-center">
              <Bell className="h-12 w-12 text-muted-foreground/30 mb-3" />
              <p className="text-sm font-medium text-muted-foreground">
                {t('notificationCenter.empty')}
              </p>
              <p className="text-xs text-muted-foreground/60 mt-1">
                {t('notificationCenter.emptySubtext')}
              </p>
            </div>
          )}

          {/* NUOVE section */}
          {unread.length > 0 && (
            <section>
              <div className="px-4 py-2">
                <span className="text-[10px] font-semibold tracking-widest text-teal-600 dark:text-teal-400">
                  {t('notificationCenter.newSection')}
                </span>
              </div>
              <div className="divide-y divide-border/30">
                {unread.map(notification => (
                  <NotificationCenterItem
                    key={notification.id}
                    notification={notification}
                    isUnread
                    onClose={() => onOpenChange(false)}
                  />
                ))}
              </div>
            </section>
          )}

          {/* PRECEDENTI section */}
          {read.length > 0 && (
            <section>
              {unread.length > 0 && <Separator className="my-0" />}
              <div className="px-4 py-2">
                <span className="text-[10px] font-semibold tracking-widest text-muted-foreground/60">
                  {t('notificationCenter.previousSection')}
                </span>
              </div>
              <div className="divide-y divide-border/30">
                {read.map(notification => (
                  <NotificationCenterItem
                    key={notification.id}
                    notification={notification}
                    isUnread={false}
                    onClose={() => onOpenChange(false)}
                  />
                ))}
              </div>
            </section>
          )}
        </div>

        {/* Footer */}
        {!error && notifications.length > 0 && (
          <>
            <Separator />
            <div className="flex items-center justify-between p-2">
              <Link
                href="/notifications"
                onClick={() => onOpenChange(false)}
                className="flex items-center gap-1 p-2 text-sm text-muted-foreground hover:text-primary transition-colors rounded-md hover:bg-accent"
                data-testid="notification-center-view-all"
              >
                {t('notificationCenter.viewAll')}
                <ChevronRight className="h-4 w-4" />
              </Link>
              <Link
                href="/notifications/preferences"
                onClick={() => onOpenChange(false)}
                className="flex items-center gap-1 p-2 text-sm text-muted-foreground hover:text-primary transition-colors rounded-md hover:bg-accent"
                data-testid="notification-center-preferences"
                aria-label={t('notificationCenter.preferences') || 'Preferenze notifiche'}
              >
                <Settings className="h-4 w-4" />
              </Link>
            </div>
          </>
        )}
      </SheetContent>
    </Sheet>
  );
}

// ============================================================================
// NotificationCenterItem — individual notification row
// ============================================================================

interface NotificationCenterItemProps {
  notification: NotificationDto;
  isUnread: boolean;
  onClose: () => void;
}

function NotificationCenterItem({ notification, isUnread, onClose }: NotificationCenterItemProps) {
  const { t } = useTranslation();
  const router = useRouter();
  const markAsRead = useNotificationStore(state => state.markAsRead);

  const isKbReady = notification.type === 'processing_job_completed';

  const handleClick = () => {
    if (!notification.isRead) {
      void markAsRead(notification.id);
    }
    if (notification.link) {
      router.push(notification.link);
      onClose();
    }
  };

  const { icon: SeverityIcon, color } = getSeverityConfig(notification.severity);
  const TypeIcon = isKbReady ? BrainCircuit : getTypeIcon(notification.type);
  const timeAgo = getRelativeTime(new Date(notification.createdAt));

  return (
    <div
      className={cn(
        'relative px-4 py-3 transition-colors',
        isUnread && 'bg-teal-50/60 dark:bg-teal-950/20 border-l-2 border-teal-500',
        !isUnread && 'hover:bg-muted/30'
      )}
    >
      <button
        onClick={handleClick}
        className="w-full text-left"
        aria-label={`Notification: ${notification.title}`}
      >
        <div className="flex gap-3">
          {/* Severity icon */}
          <div className={cn('flex-shrink-0 mt-0.5', color)}>
            <SeverityIcon className="h-4 w-4" />
          </div>

          {/* Content */}
          <div className="flex-1 min-w-0">
            <div className="flex items-center gap-1.5 mb-0.5">
              <TypeIcon className="h-3.5 w-3.5 text-muted-foreground flex-shrink-0" />
              <span className={cn('text-sm truncate', isUnread && 'font-semibold')}>
                {notification.title}
              </span>
            </div>
            <p className="text-xs text-muted-foreground line-clamp-2">{notification.message}</p>
            <p className="text-[10px] text-muted-foreground/60 mt-1">{timeAgo}</p>
          </div>

          {/* Unread dot */}
          {isUnread && (
            <div className="flex-shrink-0 mt-1">
              <div className="h-2 w-2 rounded-full bg-teal-500" aria-label="Unread" />
            </div>
          )}
        </div>
      </button>

      {/* KB-ready special CTA */}
      {isKbReady && notification.link && (
        <div className="mt-2 ml-7">
          <Link
            href={notification.link}
            onClick={() => {
              if (!notification.isRead) void markAsRead(notification.id);
              onClose();
            }}
            className={cn(
              'inline-flex items-center gap-1.5 px-3 py-1.5 rounded-md text-xs font-medium',
              'bg-teal-100 dark:bg-teal-900/40 text-teal-700 dark:text-teal-300',
              'hover:bg-teal-200 dark:hover:bg-teal-800/50 transition-colors'
            )}
            data-testid={`kb-ready-cta-${notification.id}`}
          >
            <BrainCircuit className="h-3.5 w-3.5" />
            {t('notificationCenter.kbReady.cta')}
          </Link>
        </div>
      )}
    </div>
  );
}

// ============================================================================
// Utility Functions (copied from NotificationItem for consistency)
// ============================================================================

function getSeverityConfig(severity: string): {
  icon: React.ComponentType<{ className?: string }>;
  color: string;
} {
  switch (severity) {
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
  switch (type) {
    case 'pdf_upload_completed':
    case 'rule_spec_generated':
    case 'processing_job_completed':
    case 'processing_job_failed':
      return FileCheck;
    case 'new_comment':
      return MessageSquare;
    case 'shared_link_accessed':
    case 'share_request_created':
    case 'share_request_approved':
    case 'share_request_rejected':
    case 'share_request_changes_requested':
      return LinkIcon;
    case 'processing_failed':
      return XCircle;
    case 'badge_earned':
      return Trophy;
    case 'agent_linked':
      return Bot;
    case 'rate_limit_approaching':
    case 'rate_limit_reached':
    case 'cooldown_ended':
      return Zap;
    case 'loan_reminder':
    case 'session_terminated':
      return Clock;
    case 'admin_openrouter_daily_summary':
    case 'admin_openrouter_rpm_alert':
    case 'admin_openrouter_budget_alert':
      return BarChart3;
    case 'admin_circuit_breaker_state_changed':
    case 'admin_new_share_request':
    case 'admin_shared_game_submitted':
      return Shield;
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

  return date.toLocaleDateString();
}
