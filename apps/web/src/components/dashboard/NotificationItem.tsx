/**
 * NotificationItem - Notification Display with Inline Actions
 * Issue #3286 - User Dashboard Layout System
 *
 * Features:
 * - Type-based icon and color (SHARE, LOAN, GROUP, SESSION, WISHLIST, AI, SYSTEM)
 * - Unread/Read state indicator
 * - Inline actionable buttons (Accept, Reject, View, Remind, Dismiss)
 * - Relative timestamp
 * - Swipe to dismiss (mobile)
 *
 * @example
 * ```tsx
 * <NotificationItem
 *   data={notification}
 *   onAction={(actionId) => handleAction(notification.id, actionId)}
 *   onDismiss={() => handleDismiss(notification.id)}
 * />
 * ```
 */

'use client';

import { memo, useState } from 'react';

import { motion, PanInfo, useMotionValue, useTransform } from 'framer-motion';
import {
  Share2,
  BookOpen,
  Users,
  Dices,
  Heart,
  Bot,
  Bell,
  Check,
  X,
  ExternalLink,
  Clock as ClockIcon,
  Trash2,
  ChevronRight,
  type LucideIcon,
} from 'lucide-react';

import { Button } from '@/components/ui/primitives/button';
import { cn } from '@/lib/utils';

// ============================================================================
// Types
// ============================================================================

export type NotificationType = 'SHARE' | 'LOAN' | 'GROUP' | 'SESSION' | 'WISHLIST' | 'AI' | 'SYSTEM';
export type NotificationStatus = 'UNREAD' | 'READ' | 'ACTIONED';
export type NotificationActionType = 'ACCEPT' | 'REJECT' | 'VIEW' | 'REMIND' | 'EXTERNAL_LINK' | 'DISMISS';

export interface NotificationAction {
  id: string;
  label: string;
  actionType: NotificationActionType;
  icon?: LucideIcon;
}

export interface NotificationData {
  id: string;
  type: NotificationType;
  title: string;
  body?: string;
  imageUrl?: string;
  deepLink?: string;
  status: NotificationStatus;
  isActionable: boolean;
  actions?: NotificationAction[];
  createdAt: Date;
  relatedEntityType?: string;
  relatedEntityId?: string;
}

export interface NotificationItemProps {
  data: NotificationData;
  onAction?: (actionId: string) => void;
  onDismiss?: () => void;
  onClick?: () => void;
  className?: string;
}

// ============================================================================
// Helper Functions
// ============================================================================

interface NotificationConfig {
  icon: LucideIcon;
  color: string;
  bgColor: string;
}

const NOTIFICATION_CONFIG: Record<NotificationType, NotificationConfig> = {
  SHARE: {
    icon: Share2,
    color: 'text-blue-600 dark:text-blue-400',
    bgColor: 'bg-blue-500/10',
  },
  LOAN: {
    icon: BookOpen,
    color: 'text-amber-600 dark:text-amber-400',
    bgColor: 'bg-amber-500/10',
  },
  GROUP: {
    icon: Users,
    color: 'text-cyan-600 dark:text-cyan-400',
    bgColor: 'bg-cyan-500/10',
  },
  SESSION: {
    icon: Dices,
    color: 'text-green-600 dark:text-green-400',
    bgColor: 'bg-green-500/10',
  },
  WISHLIST: {
    icon: Heart,
    color: 'text-pink-600 dark:text-pink-400',
    bgColor: 'bg-pink-500/10',
  },
  AI: {
    icon: Bot,
    color: 'text-purple-600 dark:text-purple-400',
    bgColor: 'bg-purple-500/10',
  },
  SYSTEM: {
    icon: Bell,
    color: 'text-muted-foreground',
    bgColor: 'bg-muted',
  },
};

const ACTION_ICONS: Record<NotificationActionType, LucideIcon> = {
  ACCEPT: Check,
  REJECT: X,
  VIEW: ChevronRight,
  REMIND: ClockIcon,
  EXTERNAL_LINK: ExternalLink,
  DISMISS: X,
};

function formatRelativeTime(date: Date): string {
  const now = new Date();
  const diff = now.getTime() - date.getTime();
  const minutes = Math.floor(diff / (1000 * 60));
  const hours = Math.floor(diff / (1000 * 60 * 60));
  const days = Math.floor(diff / (1000 * 60 * 60 * 24));

  if (minutes < 1) return 'Ora';
  if (minutes < 60) return `${minutes} min fa`;
  if (hours < 24) return `${hours}h fa`;
  if (days === 1) return 'Ieri';
  if (days < 7) return `${days} giorni fa`;
  return date.toLocaleDateString('it-IT', { day: 'numeric', month: 'short' });
}

// ============================================================================
// NotificationItem Component
// ============================================================================

export const NotificationItem = memo(function NotificationItem({
  data,
  onAction,
  onDismiss,
  onClick,
  className,
}: NotificationItemProps) {
  const { type, title, body, status, isActionable, actions, createdAt, deepLink } = data;
  // eslint-disable-next-line security/detect-object-injection -- type is from typed NotificationType union
  const config = NOTIFICATION_CONFIG[type];
  const Icon = config.icon;

  const isUnread = status === 'UNREAD';

  // Swipe to dismiss
  const x = useMotionValue(0);
  const background = useTransform(
    x,
    [-100, 0],
    ['rgb(239, 68, 68)', 'rgb(255, 255, 255)']
  );
  const [isDismissing, setIsDismissing] = useState(false);

  const handleDragEnd = (_: unknown, info: PanInfo) => {
    if (info.offset.x < -100 && onDismiss) {
      setIsDismissing(true);
      setTimeout(() => onDismiss(), 200);
    }
  };

  return (
    <motion.div
      className="relative overflow-hidden rounded-xl"
      animate={isDismissing ? { x: -400, opacity: 0 } : {}}
      transition={{ duration: 0.2 }}
    >
      {/* Swipe Background */}
      <motion.div
        className="absolute inset-y-0 right-0 flex w-24 items-center justify-center rounded-r-xl bg-destructive"
        style={{ background }}
      >
        <Trash2 className="h-5 w-5 text-white" />
      </motion.div>

      {/* Main Card */}
      <motion.article
        drag="x"
        dragConstraints={{ left: -100, right: 0 }}
        dragElastic={0.1}
        onDragEnd={handleDragEnd}
        style={{ x }}
        onClick={onClick}
        className={cn(
          'relative flex items-start gap-3 rounded-xl border bg-card p-4 transition-colors',
          isUnread && 'border-primary/30 bg-primary/5',
          !isUnread && 'border-border/40',
          onClick && 'cursor-pointer hover:bg-muted/50',
          className
        )}
        role={onClick ? 'button' : 'article'}
        tabIndex={onClick ? 0 : undefined}
      >
        {/* Unread Indicator */}
        {isUnread && (
          <div className="absolute left-1.5 top-1/2 h-2 w-2 -translate-y-1/2 rounded-full bg-primary" />
        )}

        {/* Notification Icon */}
        <div
          className={cn(
            'flex h-10 w-10 shrink-0 items-center justify-center rounded-full',
            config.bgColor
          )}
        >
          <Icon className={cn('h-5 w-5', config.color)} />
        </div>

        {/* Content */}
        <div className="min-w-0 flex-1">
          <div className="flex items-start justify-between gap-2">
            <div className="min-w-0 flex-1">
              <p className={cn('text-sm leading-snug', isUnread && 'font-semibold')}>
                {title}
              </p>
              {body && (
                <p className="mt-1 line-clamp-2 text-sm text-muted-foreground">{body}</p>
              )}
            </div>
            {/* Timestamp */}
            <span className="shrink-0 text-xs text-muted-foreground">
              {formatRelativeTime(createdAt)}
            </span>
          </div>

          {/* Action Buttons */}
          {isActionable && actions && actions.length > 0 && (
            <div className="mt-3 flex flex-wrap gap-2">
              {actions.map((action) => {
                const ActionIcon = ACTION_ICONS[action.actionType];
                const isPrimary = action.actionType === 'ACCEPT';
                const isDestructive = action.actionType === 'REJECT';

                return (
                  <Button
                    key={action.id}
                    variant={isPrimary ? 'default' : isDestructive ? 'outline' : 'ghost'}
                    size="sm"
                    className={cn(
                      'h-8 text-xs',
                      isDestructive && 'text-destructive hover:bg-destructive/10 hover:text-destructive'
                    )}
                    onClick={(e: React.MouseEvent) => {
                      e.stopPropagation();
                      if (action.actionType === 'EXTERNAL_LINK' && deepLink) {
                        window.open(deepLink, '_blank');
                      } else {
                        onAction?.(action.id);
                      }
                    }}
                  >
                    <ActionIcon className="mr-1.5 h-3.5 w-3.5" />
                    {action.label}
                  </Button>
                );
              })}
            </div>
          )}
        </div>
      </motion.article>
    </motion.div>
  );
});

export default NotificationItem;
