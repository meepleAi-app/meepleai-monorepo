# User Notifications - Flussi API

## Panoramica

Il bounded context User Notifications gestisce le notifiche in-app e le preferenze di notifica degli utenti.

---

## 1. Notifiche

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/notifications` | `GetNotificationsQuery` | `unreadOnly?, limit?` | `[S]` |
| GET | `/notifications/unread-count` | `GetUnreadCountQuery` | — | `[S]` |
| POST | `/notifications/{notificationId}/mark-read` | `MarkNotificationReadCommand` | — | `[S]` |
| POST | `/notifications/mark-all-read` | `MarkAllNotificationsReadCommand` | — | `[S]` |

---

## 2. Preferenze Notifiche

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| GET | `/notifications/preferences` | `GetNotificationPreferencesQuery` | — | `[S]` |
| PUT | `/notifications/preferences` | `UpdateNotificationPreferencesCommand` | Preferences object | `[S]` |

---

## Flusso Notifiche

```
1. Check:     GET /notifications/unread-count
                    │
                    ▼ { count: 3 }
                    │
2. Leggi:     GET /notifications?unreadOnly=true
                    │
                    ▼ [notifica1, notifica2, notifica3]
                    │
3. Segna:     POST /notifications/{id}/mark-read
                    │
                    ▼ oppure
                    │
              POST /notifications/mark-all-read
```

---

## Flusso Preferenze

```
GET /notifications/preferences
       │
       ▼ { emailNotifications: true, pushNotifications: false, ... }
       │
PUT /notifications/preferences { emailNotifications: false }
       │
       ▼ 200 OK
```

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 147 |
| **Passati** | 147 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 1s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| Notifications | `GetNotificationsTests.cs`, `MarkNotificationReadTests.cs` | Passato |
| Preferences | `GetNotificationPreferencesTests.cs`, `UpdatePreferencesTests.cs` | Passato |
| Event Handlers | Email sending, push notifications (3 file) | Passato |
| Domain Entities | Notification aggregate (5 file) | Passato |
| Validators | 2 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
