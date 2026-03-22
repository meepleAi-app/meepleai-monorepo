'use client';

import { AlertTriangle, CalendarDays, HelpCircle, Gamepad2, Bell } from 'lucide-react';
import Link from 'next/link';

const PRIORITY_ORDER = { high: 0, medium: 1, info: 2 } as const;

const ICON_MAP = {
  session: Gamepad2,
  event: CalendarDays,
  faq: HelpCircle,
  alert: AlertTriangle,
  notification: Bell,
} as const;

const PRIORITY_STYLES = {
  high: 'border-l-red-400 bg-red-50/50 dark:bg-red-950/10',
  medium: 'border-l-amber-400 bg-amber-50/50 dark:bg-amber-950/10',
  info: 'border-l-blue-400 bg-blue-50/50 dark:bg-blue-950/10',
} as const;

export interface ActivityItem {
  id: string;
  priority: 'high' | 'medium' | 'info';
  message: string;
  href: string;
  icon: keyof typeof ICON_MAP;
}

interface ActivityFeedProps {
  items: ActivityItem[];
  isSessionMode?: boolean;
}

export function ActivityFeed({ items, isSessionMode }: ActivityFeedProps) {
  if (isSessionMode) return null;

  const sorted = [...items].sort((a, b) => PRIORITY_ORDER[a.priority] - PRIORITY_ORDER[b.priority]);

  if (sorted.length === 0) {
    return (
      <p className="text-sm text-muted-foreground font-nunito text-center py-4">
        Nessuna attività recente
      </p>
    );
  }

  return (
    <div className="space-y-2" data-testid="activity-feed">
      {sorted.map(item => {
        const Icon = ICON_MAP[item.icon] ?? Bell;
        return (
          <Link
            key={item.id}
            href={item.href}
            data-testid={`activity-item-${item.id}`}
            className={`flex items-center gap-3 rounded-xl border-l-4 px-4 py-3 transition-colors hover:bg-accent/50 ${PRIORITY_STYLES[item.priority]}`}
          >
            <Icon className="h-4 w-4 shrink-0 text-muted-foreground" />
            <span className="font-nunito text-sm text-foreground">{item.message}</span>
          </Link>
        );
      })}
    </div>
  );
}
