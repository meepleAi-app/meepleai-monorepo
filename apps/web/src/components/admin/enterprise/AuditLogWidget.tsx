/**
 * AuditLogWidget Component
 * Issue #3691 - Audit Log System
 *
 * Compact widget for the enterprise sidebar footer showing
 * the 5 most recent audit log actions with quick-access link.
 */

'use client';

import { useEffect, useState } from 'react';

import {
  AlertTriangleIcon,
  CheckCircleIcon,
  ClockIcon,
  ShieldAlertIcon,
  ShieldCheckIcon,
} from 'lucide-react';
import Link from 'next/link';

import { api } from '@/lib/api';
import type { AuditLogEntry } from '@/lib/api/schemas';
import { cn } from '@/lib/utils';

const WIDGET_POLL_INTERVAL = 60000; // 60s for widget (less frequent than full viewer)

function getRelativeTime(dateStr: string): string {
  const diff = Date.now() - new Date(dateStr).getTime();
  const minutes = Math.floor(diff / 60000);
  if (minutes < 1) return 'just now';
  if (minutes < 60) return `${minutes}m ago`;
  const hours = Math.floor(minutes / 60);
  if (hours < 24) return `${hours}h ago`;
  const days = Math.floor(hours / 24);
  return `${days}d ago`;
}

function ResultIcon({ result }: { result: string }) {
  switch (result) {
    case 'Success':
      return <CheckCircleIcon className="h-3 w-3 text-green-500 shrink-0" />;
    case 'Error':
      return <AlertTriangleIcon className="h-3 w-3 text-red-500 shrink-0" />;
    case 'Denied':
      return <ShieldAlertIcon className="h-3 w-3 text-yellow-500 shrink-0" />;
    default:
      return <ClockIcon className="h-3 w-3 text-zinc-400 shrink-0" />;
  }
}

export function AuditLogWidget({ collapsed }: { collapsed?: boolean }) {
  const [entries, setEntries] = useState<AuditLogEntry[]>([]);
  const [loading, setLoading] = useState(true);

  useEffect(() => {
    let cancelled = false;

    async function fetchRecent() {
      try {
        const result = await api.admin.getAuditLogs({ limit: 5, offset: 0 });
        if (!cancelled) setEntries(result.entries);
      } catch {
        // Widget should not show errors - fail silently
      } finally {
        if (!cancelled) setLoading(false);
      }
    }

    fetchRecent();
    const interval = setInterval(fetchRecent, WIDGET_POLL_INTERVAL);
    return () => {
      cancelled = true;
      clearInterval(interval);
    };
  }, []);

  // Collapsed: just show the audit log link icon
  if (collapsed) {
    return (
      <Link
        href="/admin/audit-log"
        className="flex items-center justify-center p-2 text-zinc-600 dark:text-zinc-400 hover:bg-zinc-100 dark:hover:bg-zinc-800 rounded-lg transition-colors"
        data-testid="enterprise-audit-log-btn"
      >
        <ShieldCheckIcon className="w-4 h-4" />
      </Link>
    );
  }

  return (
    <div data-testid="audit-log-widget">
      {/* Recent actions list */}
      <div className="space-y-0.5 mb-2">
        {loading ? (
          <div className="space-y-1">
            {Array.from({ length: 3 }).map((_, i) => (
              <div key={i} className="h-5 bg-muted/30 rounded animate-pulse" />
            ))}
          </div>
        ) : entries.length === 0 ? (
          <p className="text-xs text-zinc-400 dark:text-zinc-500 py-1">No recent actions</p>
        ) : (
          entries.map((entry) => (
            <div
              key={entry.id}
              className="flex items-center gap-1.5 text-xs py-0.5 group"
              data-testid={`audit-widget-entry-${entry.id}`}
            >
              <ResultIcon result={entry.result} />
              <span className={cn(
                'truncate flex-1',
                'text-zinc-600 dark:text-zinc-400',
                'group-hover:text-zinc-900 dark:group-hover:text-zinc-200'
              )}>
                {entry.action}
              </span>
              <span className="text-[10px] text-zinc-400 dark:text-zinc-500 shrink-0 tabular-nums">
                {getRelativeTime(entry.createdAt)}
              </span>
            </div>
          ))
        )}
      </div>

      {/* View all link */}
      <Link
        href="/admin/audit-log"
        className="flex items-center gap-2 text-sm rounded-lg transition-colors w-full px-3 py-2 bg-zinc-100 dark:bg-zinc-800 hover:bg-zinc-200 dark:hover:bg-zinc-700 text-zinc-700 dark:text-zinc-300"
        data-testid="enterprise-audit-log-btn"
      >
        <ShieldCheckIcon className="w-4 h-4" />
        Audit Log
      </Link>
    </div>
  );
}
