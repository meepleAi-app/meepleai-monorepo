# Admin Dashboard User Guide

> **Status**: 🚧 Planned (UI not yet implemented)
> **Related Issue**: [#2464 - Admin Dashboard UI Implementation](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2464), [#2571 - Admin Dashboard Enhancements](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2571)
> **Last Updated**: 2026-01-17

---

## ⚠️ Implementation Status

**Admin Dashboard UI is currently in development**. This document serves as a placeholder and specification for the planned admin interface.

**Expected Completion**: Q1 2026 (Issue #2464)

**Tracking Issues**:
- [#2464 - Admin Dashboard UI Implementation](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2464)
- [#2571 - Admin Dashboard Enhancements](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2571)

---

## Planned Features

### 1. Service Health Monitoring

**Overview**: Real-time dashboard showing status of all MeepleAI services.

**Planned UI Components**:
- **Service Status Grid**: Visual cards for each service (postgres, redis, qdrant, etc.)
- **Status Indicators**: 🟢 Healthy, 🟡 Degraded, 🔴 Unhealthy
- **Real-time Updates**: Auto-refresh every 30 seconds via SSE
- **Historical Uptime**: Service uptime % over 24h/7d/30d
- **Dependency Graph**: Visual representation of service dependencies

**Planned API Integration**:
```typescript
// Planned implementation
interface ServiceHealth {
  serviceName: string;
  status: 'Healthy' | 'Degraded' | 'Unhealthy';
  description: string;
  isCritical: boolean;
  timestamp: string;
}

async function getServiceHealth(): Promise<ServiceHealth[]> {
  const response = await fetch('/api/v1/health');
  const data = await response.json();
  return data.checks;
}
```

**Planned Mockup** (Placeholder):
```
┌─────────────────────────────────────────────────────┐
│ MeepleAI Admin Dashboard - Service Health          │
├─────────────────────────────────────────────────────┤
│                                                     │
│  Overall Status: 🟢 Healthy                         │
│  Last Updated: 2026-01-17 10:00:00                  │
│                                                     │
│  CRITICAL SERVICES:                                 │
│  ┌──────────────┬──────────┬────────────────────┐  │
│  │ PostgreSQL   │ 🟢 Healthy│ Connected          │  │
│  │ Redis        │ 🟢 Healthy│ Connected          │  │
│  │ Qdrant       │ 🟢 Healthy│ Connected          │  │
│  │ Embedding    │ 🟢 Healthy│ Ready              │  │
│  └──────────────┴──────────┴────────────────────┘  │
│                                                     │
│  NON-CRITICAL SERVICES:                             │
│  ┌──────────────┬──────────┬────────────────────┐  │
│  │ OpenRouter   │ 🟡 Degraded│ Using fallback    │  │
│  │ BGG API      │ 🟢 Healthy│ Connected          │  │
│  └──────────────┴──────────┴────────────────────┘  │
└─────────────────────────────────────────────────────┘
```

---

### 2. Game Management

**Overview**: Approve, configure, and manage games in shared catalog.

**Planned Features**:
- **Pending Approvals**: Review user-submitted games
- **Game Configuration**: Edit game metadata (name, publisher, year, player count)
- **Soft Delete Management**: View deleted games, restore if needed
- **Bulk Operations**: Approve/reject multiple games at once

**Planned API Endpoints**:
```
GET    /api/v1/admin/games/pending           # List pending approvals
POST   /api/v1/admin/games/{id}/approve      # Approve game
POST   /api/v1/admin/games/{id}/reject       # Reject game
PATCH  /api/v1/admin/games/{id}              # Update game metadata
POST   /api/v1/admin/games/{id}/restore      # Restore soft-deleted game
```

---

### 3. AI Model Management

**Overview**: Configure AI models, monitor costs, and manage API quotas.

**Planned Features**:
- **Model Configuration**: Select default models (OpenRouter, local Ollama)
- **Cost Tracking**: View API usage and costs per model
- **Quota Management**: Set daily/monthly spending limits
- **Model Testing**: Test model responses in sandbox

**Planned Metrics**:
- Total tokens used (input + output)
- Average cost per request
- Most used models
- Peak usage hours

---

### 4. User Management

**Overview**: Manage user accounts, roles, and permissions.

**Planned Features**:
- **User List**: View all registered users
- **Role Assignment**: Assign admin/user roles
- **Account Actions**: Disable/enable accounts, reset passwords
- **Activity Logs**: View user login history, actions

**Planned API Endpoints**:
```
GET    /api/v1/admin/users                   # List users
PATCH  /api/v1/admin/users/{id}/role         # Update role
POST   /api/v1/admin/users/{id}/disable      # Disable account
POST   /api/v1/admin/users/{id}/enable       # Enable account
GET    /api/v1/admin/users/{id}/activity     # Activity logs
```

---

## Temporary Workarounds (Until UI Implemented)

### Service Health Monitoring

**Use Health Check API**:
```bash
# Check all services
curl http://localhost:8080/api/v1/health | jq

# Check specific service
curl -s http://localhost:8080/api/v1/health | \
    jq '.checks[] | select(.serviceName=="postgres")'

# Filter only unhealthy services
curl -s http://localhost:8080/api/v1/health | \
    jq '.checks[] | select(.status!="Healthy")'
```

### Game Management

**Use API Endpoints Directly**:
```bash
# List pending games
curl http://localhost:8080/api/v1/admin/games/pending \
    -H "Authorization: Bearer $ADMIN_JWT"

# Approve game
curl -X POST http://localhost:8080/api/v1/admin/games/{gameId}/approve \
    -H "Authorization: Bearer $ADMIN_JWT"

# Update game metadata
curl -X PATCH http://localhost:8080/api/v1/admin/games/{gameId} \
    -H "Authorization: Bearer $ADMIN_JWT" \
    -H "Content-Type: application/json" \
    -d '{"name":"Catan","publisher":"Catan Studio","yearPublished":1995}'
```

### User Management

**Use Database Queries** (Development Only):
```bash
# Connect to PostgreSQL
docker compose exec postgres psql -U meepleai -d meepleai_db

# List users
SELECT id, email, display_name, role, created_at FROM users;

# Update user role to admin
UPDATE users SET role = 'Admin' WHERE email = 'user@example.com';

# Disable user account
UPDATE users SET is_active = false WHERE email = 'user@example.com';
```

---

## Development Timeline

**Phase 1: Core Infrastructure** (Q1 2026)
- [ ] Admin layout and navigation (Issue #2464)
- [ ] Service health dashboard (Issue #2464)
- [ ] Authentication and RBAC (Issue #2464)

**Phase 2: Game Management** (Q1 2026)
- [ ] Game approval workflow UI (Issue #2571)
- [ ] Game metadata editing (Issue #2571)
- [ ] Soft delete management (Issue #2571)

**Phase 3: AI & Monitoring** (Q2 2026)
- [ ] AI model configuration UI
- [ ] Cost tracking dashboard
- [ ] Alert configuration

**Phase 4: User Management** (Q2 2026)
- [ ] User list and search
- [ ] Role management UI
- [ ] Activity logs viewer

---

## Contributing

Once the admin dashboard UI is implemented, this guide will be updated with:
- Screenshots of each admin interface
- Step-by-step task walkthroughs
- Keyboard shortcuts and power user tips
- Common workflows and best practices

**Want to contribute?** Check the tracking issues:
- [#2464 - Admin Dashboard UI](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2464)
- [#2571 - Admin Enhancements](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2571)

---

## Related Documentation

- **Health Check API**: [docs/03-api/health-check-api.md](../03-api/health-check-api.md)
- **Health Check System**: [docs/04-deployment/health-checks.md](../04-deployment/health-checks.md)
- **Auto-Configuration**: [docs/04-deployment/auto-configuration-guide.md](../04-deployment/auto-configuration-guide.md)

---

**Maintained by**: MeepleAI Frontend Team
**Status**: 🚧 Planned (documentation will be completed post-UI implementation)
