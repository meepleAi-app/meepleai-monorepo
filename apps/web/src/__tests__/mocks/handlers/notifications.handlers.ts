/**
 * MSW handlers for notification endpoints
 *
 * Covers: /api/v1/notifications/* routes
 * - List notifications, mark read, preferences
 */

import { http, HttpResponse } from 'msw';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

interface Notification {
  id: string;
  type: 'info' | 'warning' | 'success' | 'error';
  title: string;
  message: string;
  isRead: boolean;
  createdAt: string;
  targetUrl?: string;
}

let notifications: Notification[] = [
  {
    id: 'notif-1',
    type: 'info',
    title: 'Welcome',
    message: 'Welcome to MeepleAI!',
    isRead: false,
    createdAt: '2024-01-15T10:00:00Z',
    targetUrl: '/dashboard',
  },
  {
    id: 'notif-2',
    type: 'success',
    title: 'Badge Earned',
    message: 'You earned the First Game badge!',
    isRead: false,
    createdAt: '2024-01-16T10:00:00Z',
    targetUrl: '/badges',
  },
];

export const notificationsHandlers = [
  // GET /api/v1/notifications
  http.get(`${API_BASE}/api/v1/notifications`, () => {
    return HttpResponse.json({
      items: notifications,
      totalCount: notifications.length,
      unreadCount: notifications.filter(n => !n.isRead).length,
    });
  }),

  // PUT /api/v1/notifications/:id/read - Mark as read
  http.put(`${API_BASE}/api/v1/notifications/:id/read`, ({ params }) => {
    const notif = notifications.find(n => n.id === params.id);
    if (!notif) {
      return HttpResponse.json({ error: 'Notification not found' }, { status: 404 });
    }
    notif.isRead = true;
    return HttpResponse.json(notif);
  }),

  // PUT /api/v1/notifications/read-all - Mark all as read
  http.put(`${API_BASE}/api/v1/notifications/read-all`, () => {
    notifications.forEach(n => (n.isRead = true));
    return HttpResponse.json({ success: true });
  }),
];

export const resetNotificationsState = () => {
  notifications = [
    {
      id: 'notif-1',
      type: 'info',
      title: 'Welcome',
      message: 'Welcome to MeepleAI!',
      isRead: false,
      createdAt: '2024-01-15T10:00:00Z',
      targetUrl: '/dashboard',
    },
    {
      id: 'notif-2',
      type: 'success',
      title: 'Badge Earned',
      message: 'You earned the First Game badge!',
      isRead: false,
      createdAt: '2024-01-16T10:00:00Z',
      targetUrl: '/badges',
    },
  ];
};

export const addNotification = (notif: Partial<Notification>) => {
  const newNotif: Notification = {
    id: `notif-${Date.now()}`,
    type: notif.type || 'info',
    title: notif.title || 'Notification',
    message: notif.message || '',
    isRead: false,
    createdAt: new Date().toISOString(),
    ...notif,
  };
  notifications.push(newNotif);
  return newNotif;
};
