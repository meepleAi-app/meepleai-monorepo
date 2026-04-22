/**
 * Notification History Page (Issue #4415)
 *
 * Full notification list migrated to Claude Design v1 with NotificationCard (M6 Task 11).
 *
 * Features:
 * - Filters bar: all | sessions | agents | events | system (entity-colored pills)
 * - Grouped by day: Oggi / Ieri / Questa settimana / Precedenti
 * - Detail opens in Drawer
 * - Pagination (20 items per page)
 * - Mark all as read bulk action
 * - Empty state with illustration
 */

'use client';

import { useCallback, useEffect, useMemo, useState } from 'react';

import { formatDistanceToNow } from 'date-fns';
import { it } from 'date-fns/locale';
import { Bell, CheckCheck, Loader2 } from 'lucide-react';

import { CatalogPagination } from '@/components/catalog/CatalogPagination';
import { Btn } from '@/components/ui/v2/btn';
import {
  Drawer,
  DrawerContent,
  DrawerHeader,
  DrawerTitle,
  DrawerDescription,
} from '@/components/ui/v2/drawer';
import type { EntityType } from '@/components/ui/v2/entity-tokens';
import { NotificationCard } from '@/components/ui/v2/notification-card';
import type { NotificationDto, NotificationType } from '@/lib/api';
import { cn } from '@/lib/utils';
import { useNotificationStore, selectNotifications } from '@/stores/notification/store';

const ITEMS_PER_PAGE = 20;

// ─── Filter categories (Claude Design v1 spec) ────────────────────
type FilterKey = 'all' | 'sessions' | 'agents' | 'events' | 'system';

interface FilterDef {
  readonly key: FilterKey;
  readonly label: string;
  readonly entity: EntityType;
  readonly types: readonly NotificationType[] | null; // null = all
}

const FILTERS: readonly FilterDef[] = [
  { key: 'all', label: 'Tutte', entity: 'game', types: null },
  {
    key: 'sessions',
    label: 'Sessioni',
    entity: 'session',
    types: ['session_terminated'],
  },
  {
    key: 'agents',
    label: 'Agenti',
    entity: 'agent',
    types: ['agent_ready', 'rule_spec_generated'],
  },
  {
    key: 'events',
    label: 'Serate',
    entity: 'event',
    types: [
      'game_night_invitation',
      'game_night_rsvp_received',
      'game_night_published',
      'game_night_cancelled',
      'game_night_reminder',
    ],
  },
  {
    key: 'system',
    label: 'Sistema',
    entity: 'toolkit',
    types: [
      'document_ready',
      'document_processing_failed',
      'shared_link_accessed',
      'share_request_created',
      'share_request_approved',
      'share_request_rejected',
      'share_request_changes_requested',
      'rate_limit_approaching',
      'rate_limit_reached',
      'cooldown_ended',
      'badge_earned',
      'loan_reminder',
    ],
  },
];

// Legacy "Tutte / Non lette" tab filter (preserved from previous page)
type TabValue = 'all' | 'unread';

// ─── Type → EntityType mapping for NotificationCard border color ─
function mapTypeToEntity(type: NotificationType): EntityType {
  if (type.startsWith('game_night_')) return 'event';
  if (type === 'agent_ready' || type === 'rule_spec_generated') return 'agent';
  if (type === 'session_terminated') return 'session';
  if (type === 'badge_earned') return 'player';
  if (type.startsWith('share_request_') || type === 'shared_link_accessed') return 'kb';
  if (type === 'document_ready' || type === 'document_processing_failed') return 'kb';
  return 'toolkit';
}

// ─── Day grouping ────────────────────────────────────────────────
type GroupKey = 'oggi' | 'ieri' | 'settimana' | 'precedenti';
const GROUP_LABELS: Record<GroupKey, string> = {
  oggi: 'Oggi',
  ieri: 'Ieri',
  settimana: 'Questa settimana',
  precedenti: 'Precedenti',
};
const GROUP_ORDER: readonly GroupKey[] = ['oggi', 'ieri', 'settimana', 'precedenti'];

function startOfDay(d: Date): number {
  return new Date(d.getFullYear(), d.getMonth(), d.getDate()).getTime();
}

function groupKey(createdAt: string): GroupKey {
  const now = new Date();
  const today = startOfDay(now);
  const msPerDay = 86_400_000;
  const t = startOfDay(new Date(createdAt));
  const delta = today - t;
  if (delta <= 0) return 'oggi';
  if (delta === msPerDay) return 'ieri';
  if (delta < msPerDay * 7) return 'settimana';
  return 'precedenti';
}

// Relative timestamp (short), using date-fns (already a dep)
function formatRelative(iso: string): string {
  return formatDistanceToNow(new Date(iso), { addSuffix: true, locale: it });
}

export default function NotificationsPage() {
  const notifications = useNotificationStore(selectNotifications);
  const isFetching = useNotificationStore(state => state.isFetching);
  const error = useNotificationStore(state => state.error);
  const fetchNotifications = useNotificationStore(state => state.fetchNotifications);
  const markAllAsRead = useNotificationStore(state => state.markAllAsRead);
  const markAsRead = useNotificationStore(state => state.markAsRead);

  const [activeTab, setActiveTab] = useState<TabValue>('all');
  const [filter, setFilter] = useState<FilterKey>('all');
  const [currentPage, setCurrentPage] = useState(1);
  const [detail, setDetail] = useState<NotificationDto | null>(null);

  useEffect(() => {
    void fetchNotifications({});
  }, [fetchNotifications]);

  useEffect(() => {
    setCurrentPage(1);
  }, [activeTab, filter]);

  // Apply tab + filter
  const filtered = useMemo(() => {
    let result: NotificationDto[] = notifications;
    if (activeTab === 'unread') result = result.filter(n => !n.isRead);
    const f = FILTERS.find(x => x.key === filter);
    const types = f?.types;
    if (types) result = result.filter(n => types.includes(n.type));
    return result;
  }, [notifications, activeTab, filter]);

  const totalPages = Math.max(1, Math.ceil(filtered.length / ITEMS_PER_PAGE));
  const paginatedItems = useMemo(() => {
    const start = (currentPage - 1) * ITEMS_PER_PAGE;
    return filtered.slice(start, start + ITEMS_PER_PAGE);
  }, [filtered, currentPage]);

  // Group page items by day
  const grouped = useMemo(() => {
    const map = new Map<GroupKey, NotificationDto[]>();
    for (const n of paginatedItems) {
      const k = groupKey(n.createdAt);
      const arr = map.get(k) ?? [];
      arr.push(n);
      map.set(k, arr);
    }
    return GROUP_ORDER.flatMap(k => {
      const items = map.get(k);
      if (!items || items.length === 0) return [];
      return [{ key: k, label: GROUP_LABELS[k], items }];
    });
  }, [paginatedItems]);

  // Per-filter unread counts
  const filterCounts = useMemo(() => {
    const counts: Partial<Record<FilterKey, number>> = {};
    for (const f of FILTERS) {
      const types = f.types;
      counts[f.key] = types
        ? notifications.filter(n => !n.isRead && types.includes(n.type)).length
        : notifications.filter(n => !n.isRead).length;
    }
    return counts;
  }, [notifications]);

  const hasUnread = notifications.some(n => !n.isRead);
  const unreadCount = notifications.filter(n => !n.isRead).length;

  const handlePageChange = useCallback((page: number) => {
    setCurrentPage(page);
    window.scrollTo({ top: 0, behavior: 'smooth' });
  }, []);

  const openDetail = useCallback(
    (n: NotificationDto) => {
      setDetail(n);
      if (!n.isRead) void markAsRead(n.id);
    },
    [markAsRead]
  );

  return (
    <div className="container max-w-3xl mx-auto py-8">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <div>
          <h1 className="text-3xl font-bold mb-1">Notifiche</h1>
          <p className="text-muted-foreground text-sm">
            {unreadCount > 0 ? `${unreadCount} non lette` : 'Nessuna notifica non letta'}
          </p>
        </div>
        {hasUnread && (
          <Btn
            variant="outline"
            size="sm"
            onClick={() => void markAllAsRead()}
            leftIcon={<CheckCheck className="h-4 w-4" aria-hidden="true" />}
            aria-label="Segna tutte come lette"
          >
            Segna tutte come lette
          </Btn>
        )}
      </div>

      {/* Legacy Tutte / Non lette tabs (roles preserved for existing tests) */}
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

      {/* Filters bar (Claude Design v1: entity-colored outline pills) */}
      <div
        className="flex items-center gap-2 mb-4 flex-wrap"
        role="tablist"
        aria-label="Categoria notifiche"
      >
        {FILTERS.map(f => {
          const active = filter === f.key;
          const count = filterCounts[f.key] ?? 0;
          return (
            <Btn
              key={f.key}
              entity={f.entity}
              variant={active ? 'primary' : 'outline'}
              size="sm"
              onClick={() => setFilter(f.key)}
              aria-pressed={active}
            >
              {f.label}
              {count > 0 && <span className="ml-1 font-mono text-xs opacity-80">({count})</span>}
            </Btn>
          );
        })}
      </div>

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
        <div className="flex flex-col items-center justify-center py-16 text-center gap-3">
          <div
            className="flex h-24 w-24 items-center justify-center rounded-full bg-muted/60"
            aria-hidden="true"
          >
            <Bell className="h-10 w-10 text-muted-foreground/60" />
          </div>
          <p className="text-sm text-muted-foreground">
            {activeTab === 'unread'
              ? 'Nessuna notifica non letta'
              : filter !== 'all'
                ? 'Nessuna notifica di questo tipo'
                : 'Nessuna notifica'}
          </p>
        </div>
      )}

      {!error && grouped.length > 0 && (
        <div className="flex flex-col gap-5">
          {grouped.map(g => (
            <section key={g.key} aria-labelledby={`notif-group-${g.key}`}>
              <h2
                id={`notif-group-${g.key}`}
                className="mb-2 font-mono text-xs uppercase tracking-wider text-muted-foreground"
              >
                {g.label}
              </h2>
              <div className="flex flex-col gap-2">
                {g.items.map(n => (
                  <NotificationCard
                    key={n.id}
                    entity={mapTypeToEntity(n.type)}
                    title={n.title}
                    body={n.message}
                    timestamp={formatRelative(n.createdAt)}
                    unread={!n.isRead}
                    onClick={() => openDetail(n)}
                  />
                ))}
              </div>
            </section>
          ))}
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

      {/* Detail drawer */}
      <Drawer
        open={detail !== null}
        onOpenChange={open => {
          if (!open) setDetail(null);
        }}
        entity={detail ? mapTypeToEntity(detail.type) : undefined}
      >
        <DrawerContent>
          {detail && (
            <>
              <DrawerHeader>
                <DrawerTitle>{detail.title}</DrawerTitle>
                <DrawerDescription>{formatRelative(detail.createdAt)}</DrawerDescription>
              </DrawerHeader>
              <div className="px-4 pb-6 text-sm leading-relaxed text-foreground/90">
                {detail.message}
              </div>
              {detail.link && (
                <div className="px-4 pb-6">
                  <Btn
                    variant="primary"
                    entity={mapTypeToEntity(detail.type)}
                    fullWidth
                    onClick={() => {
                      if (detail.link) window.location.assign(detail.link);
                    }}
                  >
                    Apri
                  </Btn>
                </div>
              )}
            </>
          )}
        </DrawerContent>
      </Drawer>
    </div>
  );
}
