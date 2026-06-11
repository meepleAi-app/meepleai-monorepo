/**
 * Notifications page-mock fixtures (DS-17 Phase C-1 — argTypes matrix pattern).
 *
 * Consumed by `notifications` cluster Storybook story con axis matrix:
 *   filter: 'all' | 'sessions' | 'agents' | 'events' | 'system'
 *   state:  'default' | 'empty' | 'loading' | 'error'
 *
 * Stage axis discovery: grep `FILTER|GROUPS|entity` in notifications.jsx
 * (lines 46-57: filter + group definitions).
 *
 * Cross-references handlers in:
 *   apps/web/src/__tests__/mocks/handlers/notifications.handlers.ts
 *
 * Co-located target path:
 *   apps/web/src/__tests__/fixtures/mockup-pilots/auth/notifications.ts
 *
 * Refs: spec docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md,
 *       umbrella #2063, sub-issue #2160.
 */

import { http, HttpResponse } from 'msw';

import type { NotificationDto, NotificationType } from '@/lib/api';

export type NotificationsState = 'default' | 'empty' | 'loading' | 'error';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// ── Notification fixtures (mirror NotificationDto schema) ─────────────────
// Cross-entity sample matches mockup's 8-entry sample (jsx:11-42).
// Timestamps reverse-engineered to populate "Oggi" / "Ieri" / "Questa settimana"
// day groups (page.tsx:122-144).

const now = new Date();
const today = new Date(now.getFullYear(), now.getMonth(), now.getDate());
const yesterday = new Date(today.getTime() - 86_400_000);
const lastWeek = new Date(today.getTime() - 86_400_000 * 4);
const older = new Date(today.getTime() - 86_400_000 * 14);

const baseNotification = (overrides: Partial<NotificationDto>): NotificationDto => ({
  id: 'notif-00000000',
  type: 'session_terminated' as NotificationType,
  title: 'Sample Notification',
  message: 'Sample message body',
  isRead: false,
  createdAt: now.toISOString(),
  link: null,
  ...overrides,
});

export const MOCK_AUTH_NOTIFICATIONS_DEFAULT: NotificationDto[] = [
  baseNotification({
    id: 'notif-01',
    type: 'session_terminated',
    title: 'Marco ha iniziato una sessione',
    message: 'Catan · 4 giocatori · Iniziata 5 min fa',
    createdAt: today.toISOString(),
    isRead: false,
    link: '/sessions/sess-catan-01',
  }),
  baseNotification({
    id: 'notif-02',
    type: 'agent_ready',
    title: 'Agente Regolamento ha risposto',
    message: 'Risposta su "Settlement costs" — clicca per leggere',
    createdAt: today.toISOString(),
    isRead: false,
    link: '/chat/thread-regole-001',
  }),
  baseNotification({
    id: 'notif-03',
    type: 'game_night_invitation',
    title: 'Invito serata gioco ricevuto',
    message: 'Lucia ti ha invitato alla serata di sabato 15 giugno',
    createdAt: yesterday.toISOString(),
    isRead: false,
    link: '/game-nights/gn-2026-06-15',
  }),
  baseNotification({
    id: 'notif-04',
    type: 'document_ready',
    title: 'Nuova house rule aggiunta',
    message: 'Carcassonne: "Tile expansion supplementare"',
    createdAt: yesterday.toISOString(),
    isRead: true,
    link: null,
  }),
  baseNotification({
    id: 'notif-05',
    type: 'badge_earned',
    title: 'Lucia si è unita alla partita',
    message: 'Sessione Wingspan · ora 8 giocatori',
    createdAt: lastWeek.toISOString(),
    isRead: true,
    link: '/sessions/sess-wingspan-04',
  }),
  baseNotification({
    id: 'notif-06',
    type: 'badge_earned',
    title: 'Settimana completata!',
    message: 'Hai giocato 5 partite questa settimana — badge sbloccato',
    createdAt: lastWeek.toISOString(),
    isRead: true,
    link: '/profile?tab=achievements',
  }),
  baseNotification({
    id: 'notif-07',
    type: 'rate_limit_approaching',
    title: 'Limite messaggi AI al 90%',
    message: 'Hai usato 90/100 messaggi del piano Free questo mese',
    createdAt: older.toISOString(),
    isRead: true,
    link: '/settings?section=ai-consent',
  }),
  baseNotification({
    id: 'notif-08',
    type: 'rule_spec_generated',
    title: 'Toolkit "Strategie Catan" aggiornato',
    message: 'Nuova versione disponibile — apri per dettagli',
    createdAt: older.toISOString(),
    isRead: true,
    link: '/toolkits/strategie-catan',
  }),
];

export const MOCK_AUTH_NOTIFICATIONS_EMPTY: NotificationDto[] = [];

// ── MSW handler switcher (state-driven scenarios) ─────────────────────────

export function mswForState(state: NotificationsState) {
  if (state === 'loading') {
    return [http.get(`${API_BASE}/api/v1/notifications`, () => new Promise<Response>(() => {}))];
  }
  if (state === 'error') {
    return [
      http.get(`${API_BASE}/api/v1/notifications`, () =>
        HttpResponse.json({ error: 'Server error' }, { status: 500 })
      ),
    ];
  }
  if (state === 'empty') {
    return [
      http.get(`${API_BASE}/api/v1/notifications`, () =>
        HttpResponse.json({ items: MOCK_AUTH_NOTIFICATIONS_EMPTY, total: 0 })
      ),
      http.put(`${API_BASE}/api/v1/notifications/:id/read`, () =>
        HttpResponse.json({ success: true })
      ),
      http.put(`${API_BASE}/api/v1/notifications/read-all`, () =>
        HttpResponse.json({ success: true, count: 0 })
      ),
    ];
  }
  return [
    http.get(`${API_BASE}/api/v1/notifications`, () =>
      HttpResponse.json({
        items: MOCK_AUTH_NOTIFICATIONS_DEFAULT,
        total: MOCK_AUTH_NOTIFICATIONS_DEFAULT.length,
      })
    ),
    http.put(`${API_BASE}/api/v1/notifications/:id/read`, () =>
      HttpResponse.json({ success: true })
    ),
    http.put(`${API_BASE}/api/v1/notifications/read-all`, () =>
      HttpResponse.json({
        success: true,
        count: MOCK_AUTH_NOTIFICATIONS_DEFAULT.length,
      })
    ),
  ];
}
