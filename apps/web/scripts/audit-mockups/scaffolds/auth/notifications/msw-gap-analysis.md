# notifications — MSW Gap Analysis

**Cross-referenced handler file**: `apps/web/src/__tests__/mocks/handlers/notifications.handlers.ts`

## Endpoint coverage

| Endpoint | Method | Existing handler | Gap | Notes |
|----------|--------|------------------|-----|-------|
| `/api/v1/notifications` | GET | ✅ `notifications.handlers.ts:45-…` | None | List notifications — drives `useNotificationStore.fetchNotifications` |
| `/api/v1/notifications/:id/read` | PUT | ✅ `notifications.handlers.ts:54-…` | None | Mark single notification as read (drawer auto-mark) |
| `/api/v1/notifications/read-all` | PUT | ✅ `notifications.handlers.ts:64-…` | None | Bulk mark all as read (header button) |
| `/api/v1/notifications/preferences` | GET | ⚠️ Verify | ADD if missing | Used by `/notifications/preferences` route |
| `/api/v1/notifications/preferences` | PUT | ⚠️ Verify | ADD if missing | Save channel preferences (email/push/digest) |
| `/api/v1/notifications/test` | POST | ⚠️ Optional | OPTIONAL | Used by preferences "Test notifica" button (settings) |

## Recommended new handlers

```ts
// GET /api/v1/notifications/preferences
http.get(`${API_BASE}/api/v1/notifications/preferences`, () => {
  return HttpResponse.json({
    channels: {
      email: { enabled: true, digest: 'daily' },
      push: { enabled: true },
      inApp: { enabled: true },
    },
    categories: {
      sessions: { email: true, push: true, inApp: true },
      agents: { email: false, push: true, inApp: true },
      events: { email: true, push: true, inApp: true },
      system: { email: false, push: false, inApp: true },
    },
  });
}),

// PUT /api/v1/notifications/preferences
http.put(`${API_BASE}/api/v1/notifications/preferences`, async ({ request }) => {
  const body = await request.json();
  return HttpResponse.json({ success: true, savedAt: new Date().toISOString() });
}),
```

## API contract notes

- `useNotificationStore.fetchNotifications({})` calls `GET /api/v1/notifications`
  expecting `{ items: NotificationDto[], total: number }` shape.
- Drawer auto-mark logic: `openDetail(n)` → `markAsRead(n.id)` if `!n.isRead`
  (page.tsx:225-229). Fixture handler must accept `:id` param and respond 200.
- `markAllAsRead` bulk call from header button (page.tsx:243-251).
- Pagination is client-side (page.tsx:182-186); server returns full list.

## Storybook-specific MSW notes

- Fixture's `mswForState('default')` returns 8 mock notifications spread
  across 4 day groups (Oggi: 2, Ieri: 2, Questa settimana: 2, Precedenti: 2).
  This populates all 5 mockup frames.
- `'empty'` state returns `{ items: [], total: 0 }` — triggers empty-state
  illustration (page.tsx:313-329).
- `'loading'` state holds the response indefinitely — triggers
  `<Loader2 className="animate-spin">` (page.tsx:301-305).
- `'error'` state returns 500 — triggers error banner (page.tsx:307-311).
- Read mutation handlers always return success to avoid Storybook UI
  freezing on optimistic-update rollback.
