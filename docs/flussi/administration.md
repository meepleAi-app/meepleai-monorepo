# Administration - Flussi API

## Panoramica

Il bounded context Administration gestisce utenti, ruoli, analytics, dashboard, audit, reporting e alerting.

---

## 1. User Management

### CRUD Utenti

| Metodo | Path | Command/Query | Body/Params | Auth |
|--------|------|---------------|-------------|------|
| GET | `/users/search` | `SearchUsersQuery` | `query` | `[S]` |
| GET | `/admin/users` | `GetAllUsersQuery` | `search?, role?, status?, tier?, sortBy?, sortOrder?, page?, limit?` | `[A]` |
| GET | `/admin/users/{userId}` | `GetUserByIdQuery` | — | `[A]` |
| POST | `/admin/users` | `CreateUserCommand` | CreateUserRequest | `[A]` |
| PUT | `/admin/users/{id}` | `UpdateUserCommand` | UpdateUserRequest | `[A]` |
| DELETE | `/admin/users/{id}` | `DeleteUserCommand` | — | `[A]` |

### Gestione Ruoli e Tier

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| PUT | `/admin/users/{id}/tier` | `UpdateUserTierCommand` | UpdateUserTierRequest | `[A]` |
| PATCH | `/admin/users/{userId}/level` | `SetUserLevelCommand` | SetUserLevelRequest | `[A]` |
| GET | `/admin/users/{userId}/role-history` | `GetUserRoleHistoryQuery` | — | `[A]` |

### Operazioni Utente

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/users/{id}/suspend` | `SuspendUserCommand` | `{ reason?, duration? }` | `[A]` |
| POST | `/admin/users/{id}/unsuspend` | `UnsuspendUserCommand` | — | `[A]` |
| POST | `/admin/users/{id}/unlock` | `UnlockAccountCommand` | — | `[A]` |
| POST | `/admin/users/{userId}/reset-password` | `ResetUserPasswordCommand` | ResetUserPasswordRequest | `[A]` |
| POST | `/admin/users/{userId}/send-email` | `SendUserEmailCommand` | `{ subject, body }` | `[A]` |
| GET | `/admin/users/{userId}/lockout-status` | `GetAccountLockoutStatusQuery` | — | `[A]` |

### Impersonation

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/users/{userId}/impersonate` | `ImpersonateUserCommand` | — | `[A]` |
| POST | `/admin/impersonation/end` | `EndImpersonationCommand` | EndImpersonationRequest | `[A]` |

### Bulk Operations

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/users/bulk/password-reset` | `BulkPasswordResetCommand` | BulkPasswordResetRequest | `[A]` |
| POST | `/admin/users/bulk/role-change` | `BulkRoleChangeCommand` | BulkRoleChangeRequest | `[A]` |
| POST | `/admin/users/bulk/import` | `BulkImportUsersCommand` | CSV body | `[A]` |
| GET | `/admin/users/bulk/export` | `BulkExportUsersQuery` | `role?, search?` | `[A]` |

### User Details (Admin View)

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/users/{userId}/activity` | `GetUserActivityQuery` | `actionFilter?, resourceFilter?, startDate?, endDate?, limit?` | `[A]` |
| GET | `/admin/users/{userId}/library/stats` | `GetUserLibraryStatsQuery` | — | `[A]` |
| GET | `/admin/users/{userId}/badges` | `GetUserBadgesQuery` | — | `[A]` |
| GET | `/admin/users/{userId}/ai-usage` | `GetUserDetailedAiUsageQuery` | `days?` | `[A]` |

---

## 2. Analytics e Metriche

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/analytics` | `GetAdminStatsQuery` | `fromDate?, toDate?, days?, gameId?, roleFilter?` | `[A]` |
| GET | `/admin/dashboard/stats` | `GetAdminStatsQuery` | Same as above | `[A]` |
| GET | `/admin/activity` | `GetRecentActivityQuery` | `limit?, since?` | `[A]` |
| POST | `/admin/analytics/export` | `ExportStatsCommand` | ExportDataRequest | `[A]` |
| GET | `/admin/analytics/api-requests` | `GetApiRequestsByDayQuery` | `days?` | `[A]` |
| GET | `/admin/analytics/ai-usage` | `GetAiUsageStatsQuery` | — | `[A]` |

### AI Request Tracking

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/requests` | `GetAiRequestsQuery` | `limit?, offset?, endpoint?, userId?, gameId?, startDate?, endDate?` | `[A]` |
| GET | `/admin/stats` | `GetAiRequestStatsQuery` | `startDate?, endDate?, userId?, gameId?` | `[A]` |
| GET | `/logs` | `GetAiRequestsQuery` | `limit?` | `[S]` |

### Quality Monitoring

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/admin/llm/health` | `GetLlmHealthQuery` | — | `[A]` |
| GET | `/admin/quality/low-responses` | `GetLowQualityResponsesQuery` | `limit?, offset?, startDate?, endDate?` | `[A]` |
| GET | `/admin/quality/report` | `GenerateQualityReportQuery` | `days?` | `[A]` |

### LLM Cost Tracking

| Metodo | Path | Command/Query | Params | Auth |
|--------|------|---------------|--------|------|
| GET | `/llm-costs/report` | `GetLlmCostReportQuery` | `startDate?, endDate?, userId?` | `[A]` |
| GET | `/llm-costs/daily` | `GetLlmCostReportQuery` | `date?` | `[A]` |
| POST | `/llm-costs/check-alerts` | `CheckLlmCostAlertsCommand` | — | `[A]` |

---

## 3. Dashboard

| Metodo | Path | Command/Query | Response | Auth |
|--------|------|---------------|----------|------|
| GET | `/dashboard` | `GetDashboardQuery` | JSON | `[S]` |
| GET | `/dashboard/insights` | `GetDashboardInsightsQuery` | JSON | `[S]` |
| GET | `/dashboard/activity-timeline` | `GetActivityTimelineQuery` | JSON | `[S]` |
| GET | `/dashboard/stream` | `GetDashboardStreamQuery` | SSE | `[S]` |

---

## 4. Reporting

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/admin/reports/generate` | `GenerateReportCommand` | GenerateReportRequest | `[A]` |
| POST | `/admin/reports/schedule` | `ScheduleReportCommand` | ScheduleReportRequest | `[A]` |
| GET | `/admin/reports/scheduled` | `GetScheduledReportsQuery` | — | `[A]` |
| GET | `/admin/reports/executions` | `GetReportExecutionsQuery` | `reportId?, limit?` | `[A]` |
| PUT | `/admin/reports/{reportId}/schedule` | `UpdateReportScheduleCommand` | UpdateScheduleRequest | `[A]` |

---

## 5. Alerting

| Metodo | Path | Command/Query | Body | Auth |
|--------|------|---------------|------|------|
| POST | `/alerts/prometheus` | — (webhook) | Prometheus alert payload | `[P]` |
| GET | `/admin/alerts` | AlertingService | `activeOnly?` | `[A]` |
| POST | `/admin/alerts/{alertType}/resolve` | AlertingService | — | `[A]` |

---

## Flusso Admin: Gestione Utente Completo

```
1. Cerca:    GET /admin/users?search="mario"
2. Dettagli: GET /admin/users/{userId}
3. Activity: GET /admin/users/{userId}/activity
4. AI Usage: GET /admin/users/{userId}/ai-usage
5. Azione:   POST /admin/users/{userId}/suspend { reason }
             oppure
             PUT /admin/users/{userId}/tier { newTier }
             oppure
             POST /admin/users/{userId}/impersonate
```

---

## Stato Test Automatici

**Ultima esecuzione**: 2026-02-15

| Metrica | Valore |
|---------|--------|
| **Test totali** | 885 |
| **Passati** | 885 |
| **Falliti** | 0 |
| **Ignorati** | 0 |
| **Pass Rate** | 100% |
| **Durata** | 8s |

### Copertura per Area

| Area | File Test | Stato |
|------|-----------|-------|
| User CRUD | `CreateUserTests.cs`, `UpdateUserTests.cs`, `DeleteUserTests.cs` | Passato |
| User Operations | `SuspendUserTests.cs`, `UnsuspendTests.cs`, `UnlockAccountTests.cs` | Passato |
| Bulk Operations | `BulkExportTests.cs`, `BulkImportTests.cs`, `BulkPasswordResetTests.cs`, `BulkRoleChangeTests.cs` | Passato |
| Impersonation | `ImpersonateUserTests.cs`, `EndImpersonationTests.cs` | Passato |
| Analytics | `GetAdminStatsTests.cs`, `ExportStatsTests.cs` | Passato |
| Dashboard | `GetDashboardStreamTests.cs`, `GetActivityTimelineTests.cs` | Passato |
| Alert System | `CreateAlertRuleTests.cs`, `SendAlertTests.cs`, `ResolveAlertTests.cs` | Passato |
| Prompt Templates | `CreatePromptTemplateTests.cs`, `ActivateVersionTests.cs` | Passato |
| Quality Monitoring | `GetLowQualityResponsesTests.cs`, `GenerateQualityReportTests.cs` | Passato |
| Reporting | `GenerateReportTests.cs`, `ScheduleReportTests.cs` | Passato |
| Domain Entities | User, AlertRule, PromptTemplate (15 file) | Passato |
| Validators | 15 file di validazione | Passato |

---

*Tutti i path sono relativi a `/api/v1/`*
