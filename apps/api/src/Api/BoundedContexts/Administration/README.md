# Administration Bounded Context

## Responsabilità

Gestisce amministrazione utenti, analytics, audit trail, alerting e monitoraggio del sistema.

## Funzionalità Principali

- **User Management**: Gestione utenti (CRUD, roles, permissions)
- **Analytics & Stats**: Metriche di sistema e dashboard amministrativa
- **Audit Trail**: Tracciamento tutte le azioni critiche
- **Alerting**: Notifiche automatiche per eventi critici (email, Slack, PagerDuty)
- **System Health**: Monitoraggio salute del sistema
- **Reports**: Generazione report periodici

## Struttura

### Domain/
Logica di business pura e modelli di dominio:
- **Entities/**: Aggregates principali (User, AuditLog, Alert, SystemMetric)
- **ValueObjects/**: Oggetti valore immutabili (UserId, AlertSeverity, MetricValue)
- **Services/**: Domain services per logica complessa
  - AlertingDomainService (threshold evaluation)
  - AuditLogDomainService (sensitive data filtering)
- **Events/**: Domain events (UserCreated, AlertTriggered, AuditLogCreated, etc.)

### Application/
Orchestrazione e casi d'uso (CQRS pattern con MediatR):
- **Commands/**: Operazioni di scrittura
  - CreateUser, UpdateUser, DeleteUser
  - AssignRole, RevokeRole
  - CreateAlert, AcknowledgeAlert, ResolveAlert
  - CreateAuditLog
- **Queries/**: Operazioni di lettura
  - GetAllUsers, GetUserById, SearchUsers
  - GetSystemStats, GetDashboardMetrics
  - GetAuditLogs, GetAuditLogsByUser
  - GetAlerts, GetActiveAlerts
  - GetAnalyticsReport
- **DTOs/**: Data Transfer Objects per le risposte
- **Validators/**: FluentValidation validators per validare i comandi
- **EventHandlers/**: Gestori di domain events
- **Interfaces/**: Contratti per repositories e servizi
- **Services/**: Application services per orchestrazione
  - AdminStatsService (retained for complex aggregations)
  - AlertingService (retained for multi-channel notifications)

### Infrastructure/
Implementazioni concrete e adattatori:
- **Repositories/**: Implementazioni EF Core (UserRepository, AuditLogRepository, AlertRepository)
- **Services/**: Implementazioni concrete di servizi
  - EmailAlertService: Email notifications
  - SlackAlertService: Slack webhook integration
  - PagerDutyAlertService: PagerDuty integration
- **Adapters/**: Adattatori per servizi di terze parti

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura) e Queries (lettura)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Domain-Driven Design**: Aggregates, Value Objects, Domain Events
- **Repository Pattern**: Astrazione dell'accesso ai dati
- **Observer Pattern**: Alert subscriptions

## User Management

### Roles (RBAC)
- **Admin**: Full system access
- **Editor**: Can manage games, upload PDFs, edit content
- **User**: Basic user, can ask questions, view games

### Permissions
- Manage Users
- Manage Games
- Upload PDFs
- Manage Configuration
- View Analytics
- Manage Alerts

### User Lifecycle
```
Create User
    ↓
Assign Role(s)
    ↓
Set Permissions (optional, granular)
    ↓
User Active
    ↓
Deactivate/Delete User
    ↓
Audit Log Created
```

## Analytics & Dashboard

### System Stats
- **Users**:
  - Total users
  - Active users (last 30 days)
  - New users (this month)
  - Users by role
- **Games**:
  - Total games
  - Games with PDFs
  - Most popular games (by questions asked)
- **Documents**:
  - Total PDFs uploaded
  - PDFs processed
  - Total indexed documents
  - Average quality score
- **Knowledge Base**:
  - Total questions asked
  - Average confidence score
  - Total chat threads
  - Average response time
- **Performance**:
  - Average API response time
  - Cache hit rate
  - Database query performance
  - Qdrant query latency

### Dashboard Endpoint
```bash
GET /api/v1/admin/dashboard

Response:
{
  "users": { "total": 1234, "active": 567, ... },
  "games": { "total": 89, "withPdfs": 45, ... },
  "documents": { "totalPdfs": 123, "processed": 120, ... },
  "knowledgeBase": { "questionsAsked": 5678, "avgConfidence": 0.82, ... },
  "performance": { "avgResponseTimeMs": 120, "cacheHitRate": 0.85, ... }
}
```

## Audit Trail

### Audited Actions
- User created, updated, deleted
- Role assigned, revoked
- Configuration changed
- PDF uploaded, deleted
- Sensitive data accessed
- Authentication events (login, logout, 2FA)
- API key created, revoked

### Audit Log Entity
```
AuditLog
├── Id (Guid)
├── Action (string: "UserCreated", "ConfigChanged", etc.)
├── UserId (Guid - who performed action)
├── TargetEntityType (string: "User", "Game", "PdfDocument", etc.)
├── TargetEntityId (Guid)
├── Timestamp (DateTime)
├── IpAddress (string)
├── UserAgent (string)
├── Changes (JSON - before/after values for updates)
└── Metadata (JSON - additional context)
```

### Retention Policy
- Keep audit logs for **7 years** (compliance)
- Archive to cold storage after 1 year
- Sensitive data masked in logs

## Alerting (OPS-07)

### Alert Types
- **Performance**: High latency, low cache hit rate
- **Error**: High error rate, service down
- **Security**: Failed login attempts, unauthorized access
- **Resource**: High CPU, memory, disk usage
- **Business**: Low confidence scores, PDF processing failures

### Alert Severity
- **Critical**: Immediate action required (PagerDuty)
- **Warning**: Action needed soon (Slack, Email)
- **Info**: Informational (Email only)

### Alert Channels
- **Email**: All severities
- **Slack**: Warning, Critical
- **PagerDuty**: Critical only (on-call rotation)

### Alert Configuration
```json
{
  "Alerts": {
    "Email": {
      "Enabled": true,
      "Recipients": ["admin@meepleai.dev"]
    },
    "Slack": {
      "Enabled": true,
      "WebhookUrl": "${SLACK_WEBHOOK_URL}",
      "Channel": "#alerts"
    },
    "PagerDuty": {
      "Enabled": true,
      "ApiKey": "${PAGERDUTY_API_KEY}",
      "ServiceKey": "${PAGERDUTY_SERVICE_KEY}"
    },
    "Thresholds": {
      "HighLatency": 1000,        // ms
      "ErrorRate": 0.05,          // 5%
      "LowConfidence": 0.70       // RAG confidence
    }
  }
}
```

### Alert Flow
```
Threshold Exceeded (e.g., error rate >5%)
    ↓
AlertingDomainService.EvaluateThreshold()
    ↓
CreateAlertCommand
    ↓
Domain Event: AlertTriggered
    ↓
EventHandler → Send Notifications
    ├─→ EmailAlertService
    ├─→ SlackAlertService
    └─→ PagerDutyAlertService (if Critical)
         ↓
Alert Stored in Database
```

## API Endpoints

### User Management
```
GET    /api/v1/admin/users              → GetAllUsersQuery
GET    /api/v1/admin/users/{id}         → GetUserByIdQuery
POST   /api/v1/admin/users              → CreateUserCommand
PUT    /api/v1/admin/users/{id}         → UpdateUserCommand
DELETE /api/v1/admin/users/{id}         → DeleteUserCommand
POST   /api/v1/admin/users/{id}/roles   → AssignRoleCommand
DELETE /api/v1/admin/users/{id}/roles/{role} → RevokeRoleCommand
```

### Analytics
```
GET    /api/v1/admin/dashboard          → GetDashboardMetricsQuery
GET    /api/v1/admin/stats              → GetSystemStatsQuery
GET    /api/v1/admin/analytics/report   → GetAnalyticsReportQuery
```

### Audit
```
GET    /api/v1/admin/audit              → GetAuditLogsQuery
GET    /api/v1/admin/audit/user/{id}    → GetAuditLogsByUserQuery
GET    /api/v1/admin/audit/{id}         → GetAuditLogByIdQuery
```

### Alerts
```
GET    /api/v1/admin/alerts             → GetAlertsQuery
GET    /api/v1/admin/alerts/active      → GetActiveAlertsQuery
POST   /api/v1/admin/alerts             → CreateAlertCommand
PUT    /api/v1/admin/alerts/{id}/ack    → AcknowledgeAlertCommand
PUT    /api/v1/admin/alerts/{id}/resolve→ ResolveAlertCommand
```

## Database Entities

Vedi `Infrastructure/Entities/Administration/`:
- `User`: Utente con roles e permissions
- `AuditLog`: Audit trail entry
- `Alert`: Alert con severity, status, acknowledgment
- `SystemMetric`: Metriche di sistema time-series

## Performance Optimizations

- **Analytics Caching**: Dashboard stats cached for 5 minutes
- **Audit Log Pagination**: Max 100 records per page
- **Alert Batching**: Alerts grouped and sent every 5 minutes (except Critical)
- **Metrics Aggregation**: Pre-computed hourly/daily aggregates

## Testing

- Unit tests per domain logic, threshold evaluation
- Integration tests con Testcontainers (PostgreSQL)
- Mock alert services per test isolati
- E2E tests per admin dashboard
- Test coverage: >90%

## Monitoring & Observability

### Health Checks
- `/health/live`: Liveness probe
- `/health/ready`: Readiness probe (includes DB, Redis, Qdrant checks)

### Metrics (Prometheus)
- `meepleai_users_total`
- `meepleai_active_users`
- `meepleai_questions_asked_total`
- `meepleai_pdf_uploads_total`
- `meepleai_alerts_triggered_total{severity="critical|warning|info"}`
- `meepleai_api_request_duration_seconds`

### Logs (Serilog → Seq)
- Structured logging
- Correlation IDs for request tracing
- Sensitive data masking

### Traces (OpenTelemetry → Jaeger)
- W3C Trace Context propagation
- Distributed tracing across services

## Dipendenze

- **EF Core**: Persistence
- **MediatR**: CQRS orchestration
- **FluentValidation**: Input validation
- **Serilog**: Structured logging
- **Prometheus.NET**: Metrics
- **OpenTelemetry**: Tracing
- **Polly**: Retry policies per alert notifications

## Security

- **RBAC**: Role-Based Access Control
- **Audit Everything**: All admin actions logged
- **Sensitive Data**: Masked in logs (passwords, API keys)
- **IP Whitelisting**: Optional for admin endpoints
- **Rate Limiting**: Admin endpoints have separate limits

## Legacy Services Retained

- **AdminStatsService**: Retained for complex aggregations (not simple CRUD)
- **AlertingService**: Retained for multi-channel orchestration (infrastructure service)

Both are infrastructure/orchestration services, not application services.

## Note di Migrazione

Questo context è stato completamente migrato alla nuova architettura DDD/CQRS. Il legacy `UserManagementService` (243 linee) è stato rimosso. `AdminStatsService` e `AlertingService` sono mantenuti come infrastructure services.

## Related Documentation

- `docs/06-security/audit-trail.md`
- `docs/02-development/admin-dashboard.md`
- `docs/01-architecture/overview/system-architecture.md` (Administration section)
- `SECURITY.md`
