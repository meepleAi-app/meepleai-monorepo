/**
 * Notification History Page (Issue #4415)
 *
 * Full notification list with filters, tabs, pagination.
 * Linked from NotificationPanel footer "Vedi tutte le notifiche".
 *
 * Features:
 * - Tabs: All / Unread
 * - Filter by notification type
 * - Pagination (20 items per page)
 * - Mark all as read bulk action
 * - Reuses NotificationItem component
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import {
  Bell,
  CheckCheck,
  Filter,
  Loader2,
} from 'lucide-react';

import { NotificationItem } from '@/components/notifications/NotificationItem';
import { CatalogPagination } from '@/components/catalog/CatalogPagination';
import { Button } from '@/components/ui/primitives/button';
import { Separator } from '@/components/ui/navigation/separator';
import type { NotificationDto, NotificationType } from '@/lib/api';
import { cn } from '@/lib/utils';
import {
  useNotificationStore,
  selectNotifications,
} from '@/store/notification/store';

const ITEMS_PER_PAGE = 20;

type TabValue = 'all' | 'unread';

const TYPE_LABELS: Record<NotificationType | 'all', string> = {
  all: 'Tutti',
  pdf_upload_completed: 'PDF completati',
  rule_spec_generated: 'Regole generate',
  processing_failed: 'Errori',
  new_comment: 'Commenti',
  shared_link_accessed: 'Link condivisi',
};

export default function NotificationsPage() {
  const notifications = useNotificationStore(selectNotifications);
  const isFetching = useNotificationStore(state => state.isFetching);
  const error = useNotificationStore(state => state.error);
  const fetchNotifications = useNotificationStore(state => state.fetchNotifications);
  const markAllAsRead = useNotificationStore(state => state.markAllAsRead);

  const [activeTab, setActiveTab] = useState<TabValue>('all');
  const [typeFilter, setTypeFilter] = useState<NotificationType | 'all'>('all');
  const [currentPage, setCurrentPage] = useState(1);

  // Fetch all notifications on mount (no limit for history page)
  useEffect(() => {
    void fetchNotifications({});
  }, [fetchNotifications]);

  // Reset to page 1 when filters change
  useEffect(() => {
    setCurrentPage(1);
  }, [activeTab, typeFilter]);

  // Filtered notifications
  const filtered = useMemo(() => {
    let result: NotificationDto[] = notifications;

    if (activeTab === 'unread') {
      result = result.filter(n => !n.isRead);
    }

    if (typeFilter !== 'all') {
      result = result.filter(n => n.type === typeFilter);
    }

    return result;
  }, [notifications, activeTab, typeFilter]);

  // Pagination
  const totalPages = Math.max(1, Math.ceil(filtered.length / ITEMS_PER_PAGE));
  const paginatedItems = useMemo(() => {
    const start = (currentPage - 1) * ITEMS_PER_PAGE;
    return filtered.slice(start, start + ITEMS_PER_PAGE);
  }, [filtered, currentPage]);

  const hasUnread = notifications.some(n => !n.isRead);
  const unreadCount = notifications.filter(n => !n.isRead).length;

  const handlePageChange = useCallback((page: number) => {
    setCurrentPage(page);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, []);

  return (
    <div className="container max-w-3xl mx-auto py-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-3xl font-bold mb-1">Notifiche</h1>
          <p className="text-muted-foreground text-sm">
            {unreadCount > 0
              ? `${unreadCount} non lette`
              : 'Nessuna notifica non letta'}
          </p>
        </div>
        {hasUnread && (
          <Button
            variant="outline"
            size="sm"
            onClick={() => void markAllAsRead()}
            aria-label="Segna tutte come lette"
          >
            <CheckCheck className="h-4 w-4 mr-1" />
            Segna tutte come lette
          </Button>
        )}
      </div>

      {/* Tabs */}
      <div className="flex gap-1 mb-4" role="tablist" aria-label="Filtro notifiche">
        {(['all', 'unread'] as const).map(tab => (
          <button
            key={tab}
            role="tab"
            aria-selected={activeTab === tab}
            onClick={() => setActiveTab(tab)}
            className={cn(
              'px-4 py-2 text-sm font-medium rounded-lg transition-colors',
              activeTab === tab
                ? 'bg-primary text-primary-foreground'
                : 'text-muted-foreground hover:bg-muted'
            )}
          >
            {tab === 'all' ? 'Tutte' : `Non lette (${unreadCount})`}
          </button>
        ))}
      </div>

      {/* Type filter */}
      <div className="flex items-center gap-2 mb-4 flex-wrap">
        <Filter className="h-4 w-4 text-muted-foreground" />
        {(Object.keys(TYPE_LABELS) as (NotificationType | 'all')[]).map(type => (
          <button
            key={type}
            onClick={() => setTypeFilter(type)}
            className={cn(
              'px-3 py-1 text-xs rounded-full border transition-colors',
              typeFilter === type
                ? 'bg-primary text-primary-foreground border-primary'
                : 'text-muted-foreground border-border hover:bg-muted'
            )}
          >
            {TYPE_LABELS[type]}
          </button>
        ))}
      </div>

      <Separator className="mb-4" />

      {/* Content */}
      {isFetching && notifications.length === 0 && (
        <div className="flex items-center justify-center py-12">
          <Loader2 className="h-8 w-8 animate-spin text-muted-foreground" />
        </div>
      )}

      {error && (
        <div className="flex items-center justify-center py-12 text-destructive text-sm">
          {error}
        </div>
      )}

      {!isFetching && !error && filtered.length === 0 && (
        <div className="flex flex-col items-center justify-center py-12 text-center">
          <Bell className="h-12 w-12 text-muted-foreground/50 mb-2" />
          <p className="text-sm text-muted-foreground">
            {activeTab === 'unread'
              ? 'Nessuna notifica non letta'
              : typeFilter !== 'all'
                ? 'Nessuna notifica di questo tipo'
                : 'Nessuna notifica'}
          </p>
        </div>
      )}

      {!error && paginatedItems.length > 0 && (
        <div className="bg-card rounded-xl border border-border/50 overflow-hidden">
          <div className="divide-y">
            {paginatedItems.map(notification => (
              <NotificationItem key={notification.id} notification={notification} />
            ))}
          </div>
        </div>
      )}

      {/* Pagination */}
      {filtered.length > ITEMS_PER_PAGE && (
        <div className="mt-6">
          <CatalogPagination
            currentPage={currentPage}
            totalPages={totalPages}
            onPageChange={handlePageChange}
            totalResults={filtered.length}
          />
        </div>
      )}
    </div>
  );
}
