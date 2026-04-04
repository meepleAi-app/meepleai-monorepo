/**
 * MSW handlers for notification endpoints (browser-safe)
 * Covers: /api/v1/notifications/*
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';

const API_BASE = HANDLER_BASE;

interface Notification {
  id: string;
  type: 'info' | 'warning' | 'success' | 'error';
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  targetUrl?: string;
}

const notifications: Notification[] = [
  {
    id: mockId(1101),
    type: 'info',
    title: 'Benvenuto',
    message: 'Benvenuto su MeepleAI!',
    isRead: false,
    createdAt: '2024-01-15T10:00:00Z',
    targetUrl: '/dashboard',
  },
  {
    id: mockId(1102),
    type: 'success',
    title: 'Badge Ottenuto',
    message: 'Hai ottenuto il badge Prima Partita!',
    isRead: false,
    createdAt: '2024-01-16T10:00:00Z',
    targetUrl: '/badges',
  },
  {
    id: mockId(1103),
    type: 'info',
    title: 'Invito Ricevuto',
    message: 'Alice ti ha invitato a una Game Night',
    isRead: true,
    createdAt: '2024-01-17T10:00:00Z',
    targetUrl: '/game-nights',
  },
];

export const notificationsHandlers = [
  http.get(`${API_BASE}/api/v1/notifications`, () => {
    return HttpResponse.json({
      items: notifications,
      totalCount: notifications.length,
      unreadCount: notifications.filter(n => !n.isRead).length,
    });
  }),

  http.put(`${API_BASE}/api/v1/notifications/:id/read`, ({ params }) => {
    const notif = notifications.find(n => n.id === params.id);
    if (!notif) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    notif.isRead = true;
    return HttpResponse.json(notif);
  }),

  http.put(`${API_BASE}/api/v1/notifications/read-all`, () => {
    notifications.forEach(n => {
      n.isRead = true;
    });
    return HttpResponse.json({ success: true });
  }),

  http.delete(`${API_BASE}/api/v1/notifications/:id`, ({ params }) => {
    const idx = notifications.findIndex(n => n.id === params.id);
    if (idx === -1) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    notifications.splice(idx, 1);
    return HttpResponse.json({ success: true });
  }),

  http.get(`${API_BASE}/api/v1/notifications/preferences`, () => {
    return HttpResponse.json({
      email: true,
      push: false,
      gameNightInvites: true,
      badgeEarned: true,
    });
  }),

  http.put(`${API_BASE}/api/v1/notifications/preferences`, async ({ request }) => {
    const body = await request.json();
    return HttpResponse.json(body);
  }),

  http.get(`${API_BASE}/api/v1/notifications/unread-count`, () => {
    return HttpResponse.json({
      count: notifications.filter(n => !n.isRead).length,
    });
  }),

  // SSE stream endpoint — returns a keep-alive stream in mock mode
  http.get(`${API_BASE}/api/v1/notifications/stream`, () => {
    const encoder = new TextEncoder();
    const stream = new ReadableStream({
      start(controller) {
        // Send a connected event so the client knows the SSE is alive
        controller.enqueue(encoder.encode('event: connected\ndata: {}\n\n'));
        // Keep the stream open (MSW will close it when the request is aborted)
      },
    });
    return new Response(stream, {
      headers: {
        'Content-Type': 'text/event-stream',
        'Cache-Control': 'no-cache',
        Connection: 'keep-alive',
      },
    });
  }),
];

// Helper to reset notification state between tests
export const resetNotificationsState = () => {
  notifications.splice(
    0,
    notifications.length,
    {
      id: mockId(1101),
      type: 'info',
      title: 'Benvenuto',
      message: 'Benvenuto su MeepleAI!',
      isRead: false,
      createdAt: '2024-01-15T10:00:00Z',
      targetUrl: '/dashboard',
    },
    {
      id: mockId(1102),
      type: 'success',
      title: 'Badge Ottenuto',
      message: 'Hai ottenuto il badge Prima Partita!',
      isRead: false,
      createdAt: '2024-01-16T10:00:00Z',
      targetUrl: '/badges',
    },
    {
      id: mockId(1103),
      type: 'info',
      title: 'Invito Ricevuto',
      message: 'Alice ti ha invitato a una Game Night',
      isRead: true,
      createdAt: '2024-01-17T10:00:00Z',
      targetUrl: '/game-nights',
    }
  );
};
