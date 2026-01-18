# Administration Bounded Context

**Gestione utenti, ruoli, audit logs, analytics, e alerting**

---

## 📋 Responsabilità

- Gestione utenti admin (CRUD, ruoli)
- Audit logs (chi, cosa, quando)
- System analytics (KPI, usage metrics)
- Alerting system (notifiche admin)
- System health monitoring

---

## 🏗️ Domain Model

**AuditLog**:
```csharp
public class AuditLog
{
    public Guid Id { get; private set; }
    public Guid UserId { get; private set; }
    public string Action { get; private set; }       // "Login", "CreateGame", etc.
    public string Entity { get; private set; }       // "User", "Game", etc.
    public Guid? EntityId { get; private set; }
    public DateTime Timestamp { get; private set; }
    public string? IpAddress { get; private set; }
}
```

**SystemAlert**:
```csharp
public class SystemAlert
{
    public Guid Id { get; private set; }
    public AlertSeverity Severity { get; private set; } // Info | Warning | Critical
    public string Message { get; private set; }
    public DateTime CreatedAt { get; private set; }
    public bool IsRead { get; private set; }

    public void MarkAsRead() { }
}
```

---

## 📡 Application Layer

### Commands
- `CreateAuditLogCommand`
- `DismissAlertCommand`
- `UpdateUserRoleCommand`

### Queries
- `GetAuditLogsQuery` (paginated, filterable)
- `GetActiveAlertsQuery`
- `GetSystemStatsQuery` (users count, games count, etc.)

---

## 📊 System Analytics

**KPIs Tracked**:
- Total users (active, inactive)
- Total games (catalog + shared)
- PDF processing stats (success rate, avg latency)
- RAG query metrics (avg confidence, top games)
- API usage (requests/day, top endpoints)

---

## 📂 Code Location

`apps/api/src/Api/BoundedContexts/Administration/`

---

**Status**: ✅ Production
**Last Updated**: 2026-01-18
