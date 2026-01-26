# UserNotifications Bounded Context

**Notifiche in-app, email notifications, push notifications**

---

## 📋 Responsabilità

- Notifiche in-app (alerts, messaggi sistema)
- Email notifications (verification, password reset, alerts)
- Push notifications (browser, mobile - future)
- Notification preferences (enable/disable per type)
- Read/unread status tracking

---

## 🏗️ Domain Model

### Aggregates

**Notification**:
```csharp
public class Notification
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Title { get; private set; }
    public string Message { get; private set; }
    public NotificationType Type { get; private set; }
    public bool IsRead { get; private set; }
    public DateTime CreatedAt { get; private set; }

    public void MarkAsRead() { }
}
```

---

## 📡 Application Layer

### Commands
- `MarkNotificationReadCommand`
- `MarkAllNotificationsReadCommand`
- `UpdateNotificationPreferencesCommand`

### Queries
- `GetNotificationsQuery`
- `GetUnreadCountQuery`

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/UserNotifications/`

---

**Last Updated**: 2026-01-18
**Status**: ✅ Production
