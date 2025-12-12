# [Issue #921] Add minimal alert management UI

## 📋 Summary

Implements a **minimal viable alert management UI** for Issue #921, providing operational visibility into the existing alerting system while the full-featured alert configuration is deferred to August 2026+.

**Issue**: #921  
**Type**: Frontend (Minimal Implementation)  
**Status**: Issue DEFERRED to Aug 2026+ (Strategic Priority: Board Game AI)  
**Scope**: Monitoring UI only (no rule builder, no config UI)

---

## 🎯 What Was Implemented

### Features (2-3 hours effort)

- ✅ **Alert Listing Page** (`/admin/alerts`)
  - Table view with active/resolved alerts
  - Severity badges (Critical, Warning, Info) with icons
  - Metadata viewer dialog (labels, annotations, channel delivery status)
  - Resolve button for active alerts

- ✅ **Stats Dashboard**
  - 4 cards: Total, Active, Critical, Warnings
  - Real-time updates

- ✅ **Filtering**
  - Active Only / All (7-day history) toggle

- ✅ **Auto-Refresh**
  - React Query polling (30s interval)
  - Stale time: 25s

- ✅ **Toast Notifications**
  - Success/error messages
  - Auto-dismiss (5s)

- ✅ **Auth Protection**
  - AdminAuthGuard (requires Admin role)

- ✅ **Visual Testing**
  - Storybook stories for Chromatic
  - 5 states: WithActiveAlerts, NoAlerts, MixedAlerts, Loading, Error

### NOT Implemented (Deferred to Aug 2026+)

- ❌ Alert rule builder
- ❌ Multi-channel configuration UI
- ❌ Throttling configuration UI
- ❌ Alert templates
- ❌ Test alert (dry-run)

---

## 🏗️ Technical Implementation

### API Client Layer

**New Files**:
- `apps/web/src/lib/api/clients/alerts.ts` - `AlertsClient` with HttpClient injection
- `apps/web/src/lib/api/schemas/alerts.schemas.ts` - Zod schemas for validation

**Integration**:
- Added `alerts: AlertsClient` to main API client
- Follows existing modular client pattern (auth, games, sessions, etc.)

### UI Components

**New Files**:
- `apps/web/src/app/admin/alerts/client.tsx` (443 LOC) - Main component
- `apps/web/src/app/admin/alerts/page.tsx` (17 LOC) - Server wrapper
- `apps/web/src/app/admin/alerts/client.stories.tsx` (328 LOC) - Storybook stories

**Shadcn/UI Components Used**:
- Table (for alert listing)
- Badge (severity + status)
- Dialog (metadata viewer)
- Card (stats dashboard)
- Alert (error display + toasts)
- Button (resolve action)

### Backend Integration

**Existing Endpoints** (no backend changes):
- `GET /api/v1/admin/alerts?activeOnly={bool}`
- `POST /api/v1/admin/alerts/{alertType}/resolve`

**Backend Features Already Present**:
- AlertingService with multi-channel (Email, Slack, PagerDuty)
- Throttling (1 alert/hour per type)
- Prometheus AlertManager webhook integration
- Alert history (7 days)

---

## 📊 Files Changed

| File | Status | LOC | Description |
|------|--------|-----|-------------|
| `apps/web/src/app/admin/alerts/client.tsx` | New | 443 | Main UI component |
| `apps/web/src/app/admin/alerts/page.tsx` | New | 17 | Server Component |
| `apps/web/src/app/admin/alerts/client.stories.tsx` | New | 328 | Storybook stories |
| `apps/web/src/lib/api/clients/alerts.ts` | New | 56 | API client |
| `apps/web/src/lib/api/schemas/alerts.schemas.ts` | New | 38 | Zod schemas |
| `apps/web/src/lib/api/clients/index.ts` | Updated | +1 | Export |
| `apps/web/src/lib/api/index.ts` | Updated | +5 | Integration |
| `apps/web/src/components/admin/AdminCharts.tsx` | Fixed | -1 | 'use client' fix |

**Total**: 8 files, ~900 LOC added

---

## ✅ Testing

### Manual Testing Checklist

- [ ] Authentication: Requires admin role
- [ ] Data Loading: Alerts fetch from API
- [ ] Filtering: Active/All toggle works
- [ ] Stats: Cards update correctly
- [ ] Table: Displays all alert fields
- [ ] Severity Badges: Correct colors/icons
- [ ] Metadata Dialog: Opens and shows details
- [ ] Resolve Action: Works for active alerts
- [ ] Auto-refresh: Updates every 30s
- [ ] Toast: Success/error messages
- [ ] Empty State: Shows "no alerts" message
- [ ] Error Handling: Displays error Alert
- [ ] Responsive: Mobile/tablet/desktop

### Automated Testing

- ✅ Storybook stories for Chromatic visual regression
- ⏳ Unit tests not created (minimal scope)

---

## 🚀 Deployment

### Prerequisites

- No new environment variables
- No database migrations
- No backend changes
- Uses existing alerting system

### Post-Merge

1. Build passes: ✅ (`pnpm build` successful)
2. No new warnings: ✅
3. Sidebar link: ✅ Already present (`AdminSidebar.tsx` line 76)
4. Route: `/admin/alerts`

---

## 📝 Known Limitations

1. **No Rule Builder**: Cannot create custom alert rules via UI
2. **No Channel Config**: Settings in `appsettings.json` only
3. **No Throttling Config**: Fixed 60min window
4. **No Templates**: No predefined scenarios
5. **No Test Alert**: Cannot trigger dry-run
6. **Limited History**: 7-day window only
7. **No Advanced Filtering**: Basic Active/All only

**Rationale**: Issue #921 deferred to Aug 2026+ for Board Game AI strategic priority.

---

## 🎯 Definition of Done

- [x] Alert listing page (`/admin/alerts`)
- [x] Active/All filter toggle
- [x] Stats cards (total, active, critical, warnings)
- [x] Table with severity badges
- [x] Metadata viewer dialog
- [x] Resolve action
- [x] Auto-refresh (30s)
- [x] Toast notifications
- [x] AdminAuthGuard protection
- [x] Storybook stories
- [x] Build passes
- [x] No new warnings
- [x] Pre-commit hooks pass
- [x] Branch pushed

---

## 🔗 Related Issues

- **Parent**: FASE 4: Advanced Features (#915) - DEFERRED
- **Backend**: AlertingService (OPS-07) - Already implemented
- **Sidebar**: AdminSidebar link (#881) - Already present

---

## 📚 Documentation

- **Summary**: `ISSUE_921_IMPLEMENTATION_SUMMARY.md`
- **Backend**: See `CLAUDE.md` (Observability → Alerting)
- **Storybook**: `pnpm storybook` → Admin/AlertsPageClient

---

## 🔄 Next Steps (Aug 2026+)

When Issue #921 is resumed:
1. Rule builder UI
2. Multi-channel config
3. Throttling config
4. Alert templates
5. Test alert functionality
6. Advanced filtering

---

## 📸 Screenshots

### Alert Listing (Active)
- Stats dashboard with 4 cards
- Table with severity badges (Critical=red, Warning=yellow, Info=blue)
- Resolve buttons for active alerts

### Metadata Dialog
- Full alert details (ID, type, triggered date, resolved date)
- Metadata JSON display
- Channel delivery status (Email ✓, Slack ✓, PagerDuty ✗)

### Empty State
- Green checkmark icon
- "No alerts found" message
- "All systems operational" subtitle

---

**Ready for Review**: ✅  
**Breaking Changes**: None  
**Backward Compatibility**: ✅ (additive only)  
**Performance Impact**: None (lazy-loaded route)

---

**Reviewer Notes**:
- This is a **minimal UI** implementation given the issue's deferred status
- Full-featured alert configuration will be implemented Aug 2026+
- No backend changes required (uses existing endpoints)
- Sidebar link already present (no navigation changes)
