# UI-API Gap Implementation Roadmap

**Planning Date**: 2026-02-11
**Duration**: 3 Sprint (6 settimane)
**Scope**: Colmare gap critici tra 627 endpoint API e 132 route frontend

---

## 🎯 Obiettivi Roadmap

- **Sprint 1-2**: Core user features (Wishlist, Notifications, Play Records)
- **Sprint 3-4**: Security & Admin efficiency (2FA, Bulk Operations, Achievements)
- **Sprint 5-6**: Admin advanced features (User Details, API Keys, Feature Flags)

**Target finale**: Coverage UI da 37% → 75% (+38 punti percentuali)

---

## 📅 Sprint 1 (Settimana 1-2) - Core User Features

**Tema**: Funzionalità essenziali utente con massimo impatto UX

### Issue #1: Notification System UI
**Priority**: 🔴 CRITICAL
**Effort**: 3-5 giorni
**Issue correlata**: #2053

**Scope**:
- [ ] Header notification bell icon con badge count
- [ ] Notification dropdown con lista (SSE real-time)
- [ ] Mark as read (singolo + bulk)
- [ ] Notification preferences page
- [ ] Toast notifications per eventi critici

**Endpoint coperti**:
- `GET /notifications`
- `GET /notifications/unread-count`
- `POST /notifications/{id}/mark-read`
- `POST /notifications/mark-all-read`
- `GET /notifications/preferences`
- `PUT /notifications/preferences`

**Acceptance Criteria**:
- [ ] Bell icon mostra unread count in tempo reale
- [ ] Dropdown lista ultime 10 notifiche
- [ ] Click notifica → mark as read + navigate
- [ ] "Mark all as read" funziona
- [ ] SSE events aggiornano UI automaticamente
- [ ] Mobile responsive

**Technical Notes**:
- Componente: `NotificationBell.tsx` in layout header
- State: Zustand store `useNotificationStore`
- SSE: Hook `useNotificationEvents` per real-time
- API Client: `notificationClient.ts`

---

### Issue #2: Wishlist Management System
**Priority**: 🔴 HIGH
**Effort**: 5-8 giorni
**Issue correlata**: #3917

**Scope**:
- [ ] `/library/wishlist` page con table/grid view
- [ ] "Add to Wishlist" button su MeepleCard
- [ ] Wishlist highlights dashboard widget
- [ ] Remove from wishlist action
- [ ] Sorting & filtering
- [ ] Bulk operations (remove multiple)

**Endpoint coperti**:
- `GET /wishlist`
- `POST /wishlist`
- `GET /wishlist/{id}`
- `PUT /wishlist/{id}`
- `DELETE /wishlist/{id}`
- `GET /wishlist/highlights`

**Acceptance Criteria**:
- [ ] Page `/library/wishlist` mostra tutti i giochi wishlist
- [ ] Heart icon su game card toggle wishlist status
- [ ] Dashboard widget mostra top 3 wishlist highlights
- [ ] Sorting: priority, added date, name
- [ ] Filtering: by complexity, players, playtime
- [ ] Bulk remove con checkbox selection
- [ ] Optimistic UI updates

**Technical Notes**:
- Page: `apps/web/src/app/(protected)/library/wishlist/page.tsx`
- Component: `WishlistButton` (MeepleCard quick action)
- Widget: `WishlistHighlightsWidget` (dashboard)
- State: React Query + optimistic updates
- API Client: `wishlistClient.ts`

---

### Issue #3: Play Records - Complete Actions
**Priority**: 🟡 MEDIUM
**Effort**: 3-4 giorni
**Issue correlata**: #3889-3890

**Scope**:
- [ ] Add/Remove players UI
- [ ] Score tracking table
- [ ] Start session button + workflow
- [ ] Complete session button + summary
- [ ] Session timer display

**Endpoint coperti**:
- `POST /play-records/{id}/players`
- `DELETE /play-records/{id}/players/{playerId}`
- `POST /play-records/{id}/scores`
- `PUT /play-records/{id}/scores/{scoreId}`
- `POST /play-records/{id}/start`
- `POST /play-records/{id}/complete`

**Acceptance Criteria**:
- [ ] "Add Player" dialog con search/select
- [ ] Player list con remove buttons
- [ ] Score table editable (inline editing)
- [ ] "Start Session" button → status = InProgress
- [ ] "Complete Session" button → status = Completed
- [ ] Timer mostra session duration
- [ ] Validation: cannot complete without scores

**Technical Notes**:
- Page: Enhance `apps/web/src/app/(protected)/play-records/[id]/page.tsx`
- Components: `AddPlayerDialog`, `ScoreTracker`, `SessionControls`
- State: React Query mutations
- Validation: Frontend + backend sync

---

## 📅 Sprint 2 (Settimana 3-4) - Security & Gamification

**Tema**: Sicurezza utente e engagement

### Issue #4: Two-Factor Authentication (2FA) Self-Service
**Priority**: 🟡 MEDIUM (HIGH per Enterprise)
**Effort**: 5-7 giorni
**Issue correlata**: New

**Scope**:
- [ ] `/settings/security` page
- [ ] 2FA setup wizard (QR code + backup codes)
- [ ] Enable/Disable 2FA toggle
- [ ] Recovery codes download
- [ ] Admin force-disable (emergency)

**Endpoint coperti**:
- `POST /auth/2fa/setup`
- `POST /auth/2fa/enable`
- `POST /auth/2fa/disable`
- `GET /users/me/2fa/status`
- `POST /auth/2fa/verify`
- `POST /auth/admin/2fa/disable` (admin only)

**Acceptance Criteria**:
- [ ] Setup wizard: QR code display + manual entry
- [ ] Verify code before enabling
- [ ] Generate 10 recovery codes
- [ ] Download recovery codes as PDF/TXT
- [ ] Status indicator: Enabled/Disabled
- [ ] Admin can force-disable for locked users
- [ ] Email confirmation on enable/disable

**Technical Notes**:
- Page: `apps/web/src/app/(protected)/settings/security/page.tsx`
- Components: `TwoFactorSetup`, `QRCodeDisplay`, `RecoveryCodes`
- Library: `qrcode.react` for QR generation
- Security: TOTP algorithm validation

---

### Issue #5: Achievement System Display
**Priority**: 🟡 MEDIUM
**Effort**: 3-4 giorni
**Issue correlata**: #3922

**Scope**:
- [ ] `/profile/achievements` page (or `/badges`)
- [ ] Achievement cards con progress bars
- [ ] Dashboard widget "Recent Achievements"
- [ ] Achievement details modal
- [ ] Filtering: earned, locked, in-progress

**Endpoint coperti**:
- `GET /achievements`
- `GET /achievements/{id}`
- `GET /achievements/recent`
- `GET /achievements/progress`

**Acceptance Criteria**:
- [ ] Page mostra tutti achievements (grid view)
- [ ] Cards: icon, title, description, progress
- [ ] Progress bar per achievements in corso
- [ ] Locked achievements mostrano requirements
- [ ] Dashboard widget: ultime 3 achievements earned
- [ ] Modal dettaglio: stats, date earned, rarity
- [ ] Filtering: All, Earned, Locked, In Progress

**Technical Notes**:
- Page: `apps/web/src/app/(protected)/profile/achievements/page.tsx`
- Components: `AchievementCard`, `AchievementGrid`, `ProgressBar`
- Widget: `RecentAchievementsWidget` (dashboard)
- Icons: Custom achievement icons or lucide-react

---

### Issue #6: Admin User Bulk Operations
**Priority**: 🟡 MEDIUM
**Effort**: 4-6 giorni
**Issue correlata**: New

**Scope**:
- [ ] Bulk actions toolbar su `/admin/users`
- [ ] Checkbox selection (select all, select page)
- [ ] Bulk password reset
- [ ] Bulk role change
- [ ] CSV import/export
- [ ] Confirmation dialogs con preview

**Endpoint coperti**:
- `POST /admin/users/bulk/password-reset`
- `POST /admin/users/bulk/role-change`
- `POST /admin/users/bulk/import`
- `GET /admin/users/bulk/export`

**Acceptance Criteria**:
- [ ] Toolbar appare quando >0 users selected
- [ ] Actions: Password Reset, Change Role, Export, Delete
- [ ] Confirmation dialog mostra affected users count
- [ ] CSV export: all fields, respects filters
- [ ] CSV import: validation + error report
- [ ] Progress indicator per bulk operations
- [ ] Success/error feedback con details

**Technical Notes**:
- Component: `BulkActionsToolbar` (sticky header)
- Dialogs: `BulkPasswordResetDialog`, `BulkRoleChangeDialog`, `ImportDialog`
- CSV: `papaparse` library
- State: Zustand `useUserSelectionStore`

---

## 📅 Sprint 3 (Settimana 5-6) - Admin Advanced Features

**Tema**: Tooling avanzato per amministratori

### Issue #7: Admin User Detail - Complete Tabs
**Priority**: 🟢 LOW-MEDIUM
**Effort**: 6-8 giorni
**Issue correlata**: New

**Scope**:
- [ ] Activity tab (login history, actions log)
- [ ] Library Stats tab (games owned, playtime, records)
- [ ] Badges tab (achievements earned)
- [ ] Role History tab (role changes audit)
- [ ] Sessions tab (active + history, kill sessions)
- [ ] Security tab (2FA status, lockout info)

**Endpoint coperti**:
- `GET /admin/users/{id}/activity`
- `GET /admin/users/{id}/library/stats`
- `GET /admin/users/{id}/badges`
- `GET /admin/users/{id}/role-history`
- `GET /admin/users/{id}/sessions`
- `DELETE /admin/users/{id}/sessions` (kill all)
- `GET /admin/users/{id}/lockout-status`

**Acceptance Criteria**:
- [ ] Tab navigation: Overview, Activity, Library, Badges, Roles, Sessions, Security
- [ ] Activity: Timeline view (last 30 days)
- [ ] Library Stats: Charts (games by status, playtime distribution)
- [ ] Badges: Grid view con progress
- [ ] Role History: Table con change reason, by, date
- [ ] Sessions: Table con device, location, last activity
- [ ] Sessions: "Kill All" button con confirmation
- [ ] Security: 2FA status, lockout countdown, unlock button

**Technical Notes**:
- Tabs: shadcn/ui Tabs component
- Charts: Recharts library
- Timeline: Custom component
- State: Server-side fetching per tab (lazy load)

---

### Issue #8: API Key Management - Advanced Features
**Priority**: 🟢 LOW
**Effort**: 4-5 giorni
**Issue correlata**: New

**Scope**:
- [ ] API key detail page tabs (Usage, Stats, Logs)
- [ ] Rotate key functionality
- [ ] Admin overview stats widget
- [ ] CSV import/export

**Endpoint coperti**:
- `POST /api-keys/{id}/rotate`
- `GET /api-keys/{id}/usage`
- `GET /api-keys/{id}/stats`
- `GET /api-keys/{id}/logs`
- `GET /admin/api-keys/stats`
- `GET /admin/api-keys/bulk/export`
- `POST /admin/api-keys/bulk/import`

**Acceptance Criteria**:
- [ ] Detail page: Overview, Usage, Stats, Logs tabs
- [ ] "Rotate Key" button con confirmation + new key display
- [ ] Usage tab: Chart (requests over time)
- [ ] Stats tab: Total requests, errors, avg latency
- [ ] Logs tab: Request log table (last 100)
- [ ] Admin stats widget: Total keys, active keys, requests today
- [ ] Export: CSV con all keys metadata
- [ ] Import: CSV validation + preview

---

### Issue #9: Feature Flag Tier Configuration
**Priority**: 🟢 LOW
**Effort**: 3-4 giorni
**Issue correlata**: New

**Scope**:
- [ ] Feature flag detail page per-tier toggle
- [ ] Bulk enable/disable per tier
- [ ] Preview affected users count

**Endpoint coperti**:
- `POST /admin/feature-flags/{key}/tier/{tier}/enable`
- `POST /admin/feature-flags/{key}/tier/{tier}/disable`
- `GET /admin/feature-flags/{key}/tiers`

**Acceptance Criteria**:
- [ ] Detail page: Table con tiers (Free, Basic, Pro, Enterprise)
- [ ] Toggle per tier (enable/disable)
- [ ] Preview: "X users affected by this change"
- [ ] Bulk actions: Enable/Disable all tiers
- [ ] Audit log integration

---

## 📅 Sprint Allocation Summary

### Sprint 1 (Week 1-2) - 15 giorni effort
- Issue #1: Notifications (5 giorni)
- Issue #2: Wishlist (8 giorni)
- Buffer: 2 giorni

### Sprint 2 (Week 3-4) - 18 giorni effort
- Issue #3: Play Records (4 giorni)
- Issue #4: 2FA (7 giorni)
- Issue #5: Achievements (4 giorni)
- Issue #6: Bulk Operations (6 giorni) - PARALLEL con #4
- Buffer: 3 giorni

### Sprint 3 (Week 5-6) - 13 giorni effort
- Issue #7: User Detail Tabs (8 giorni)
- Issue #8: API Key Advanced (5 giorni)
- Issue #9: Feature Flag Tiers (4 giorni) - PARALLEL con #8
- Buffer: 2 giorni

---

## 🎯 Metriche Successo

### Coverage Targets

| Sprint | Coverage Target | Gap Reduction | New Features |
|--------|----------------|---------------|--------------|
| Baseline | 37% | - | - |
| Sprint 1 | 50% | -13% | 2 core features |
| Sprint 2 | 65% | -15% | 4 features |
| Sprint 3 | 75% | -10% | 3 advanced features |

### User Impact Metrics

| Feature | Users Affected | Impact Score |
|---------|---------------|--------------|
| Notifications | 100% | ⭐⭐⭐⭐⭐ Critical |
| Wishlist | 80% | ⭐⭐⭐⭐ High |
| Play Records | 60% | ⭐⭐⭐ Medium |
| 2FA | 20% (Enterprise) | ⭐⭐⭐⭐ High (security) |
| Achievements | 70% | ⭐⭐⭐ Medium (engagement) |
| Admin Tools | 5% (admins) | ⭐⭐ Low (efficiency) |

---

## 🔄 Parallelization Opportunities

### Sprint 2 Parallel Tracks
**Track A**: 2FA Implementation (Backend-heavy)
**Track B**: Bulk Operations (Frontend-heavy)
**Duration**: 6-7 giorni (vs 13 giorni sequenziali)
**Savings**: 6 giorni

### Sprint 3 Parallel Tracks
**Track A**: User Detail Tabs (Component-heavy)
**Track B**: API Key + Feature Flags (Configuration-heavy)
**Duration**: 8-9 giorni (vs 17 giorni sequenziali)
**Savings**: 8 giorni

**Total Time Savings**: 14 giorni (2.8 settimane)
**Revised Timeline**: 6 settimane → ~4 settimane con parallelizzazione

---

## 🧪 Testing Strategy

### Per-Issue Testing
- **Unit Tests**: Componenti isolati (target: 90%)
- **Integration Tests**: API client + state management (target: 85%)
- **E2E Tests**: User flows critici (1-2 per issue)
- **Visual Regression**: Playwright screenshots (key pages)

### Sprint-Level Testing
- **Sprint 1**: E2E Notifications + Wishlist flows
- **Sprint 2**: Security testing (2FA), Load testing (bulk ops)
- **Sprint 3**: Admin workflow testing, Performance benchmarks

---

## 🚀 Deployment Strategy

### Per-Sprint Release
- **Sprint 1 End**: Release v2.1.0 (Notifications + Wishlist)
- **Sprint 2 End**: Release v2.2.0 (2FA + Achievements + Bulk Ops)
- **Sprint 3 End**: Release v2.3.0 (Admin Advanced Tools)

### Feature Flags
Tutte le nuove features dietro feature flag per rollout graduale:
- `notifications-ui-enabled`
- `wishlist-system-enabled`
- `2fa-self-service-enabled`
- `achievements-display-enabled`
- `admin-bulk-operations-enabled`

---

## 📋 Dipendenze & Rischi

### Dipendenze Tecniche
- **SSE Infrastructure**: Già implementata (session tracking)
- **File Upload**: Già implementata (CSV import/export)
- **QR Code Generation**: Richiede libreria `qrcode.react`
- **Charts**: Richiede libreria Recharts (già usata)

### Rischi Identificati

| Rischio | Probabilità | Impatto | Mitigazione |
|---------|-------------|---------|-------------|
| SSE scaling issues | Medium | High | Load testing sprint 1 |
| 2FA recovery flow complexity | High | Medium | User testing prototypes |
| Bulk operations performance | Medium | Medium | Pagination + background jobs |
| Mobile UX per admin tools | Medium | Low | Responsive design review |

---

## 🎯 Success Criteria Roadmap

### Sprint 1 Success
- [ ] Notification bell visibile e funzionante (100% utenti)
- [ ] Wishlist utilizzabile con <3 click per add/remove
- [ ] Zero regressioni su features esistenti
- [ ] E2E tests green (notifications + wishlist flows)

### Sprint 2 Success
- [ ] 2FA setup completabile in <5 minuti
- [ ] Achievements visualizzati correttamente
- [ ] Admin può gestire 100+ utenti con bulk ops in <2 minuti
- [ ] Play records workflow completo (start → track → complete)

### Sprint 3 Success
- [ ] Admin user detail fornisce 360° view
- [ ] API key management self-service completo
- [ ] Feature flags configurabili per tier
- [ ] Performance: tutte le pagine <1s load time

---

## 📊 Resource Allocation

### Team Allocation (Esempio 2-person team)

**Developer 1** (Frontend-focused):
- Sprint 1: Notifications UI + Wishlist UI
- Sprint 2: 2FA UI + Achievements UI
- Sprint 3: User Detail Tabs

**Developer 2** (Fullstack):
- Sprint 1: Wishlist integration + Play Records
- Sprint 2: Bulk Operations + 2FA backend
- Sprint 3: API Key Advanced + Feature Flag Tiers

**Parallel Efficiency**: ~70% vs sequential

---

## 🔄 Iterative Refinement

### Sprint Review & Adaptation
- **End Sprint 1**: User feedback su Notifications + Wishlist → adjust Sprint 2
- **End Sprint 2**: Security review 2FA → adjust admin tools priority
- **End Sprint 3**: Analytics review → identify next gap priorities

### Post-Roadmap
Dopo 3 sprint, ri-eseguire gap analysis:
```bash
.\scripts\analyze-ui-api-gaps.ps1
```

**Target**: Coverage >75%, gap critici <10%

---

## 📚 Riferimenti

- [UI-API Gap Analysis](./ui-api-gap-analysis.md) - Analisi completa gap
- [Play Records Implementation](./03-api/bounded-contexts/game-management/play-records.md) - Bounded context
- [Achievement System](./04-features/game-session-toolkit.md) - Gamification spec
- [Admin Dashboard](./04-features/admin-dashboard-enterprise/SPECIFICATION.md) - Admin UI patterns

---

**Approved**: Pending review
**Estimated Duration**: 6 settimane (4 settimane con parallelizzazione)
**Expected Coverage**: 37% → 75%
**Risk Level**: Medium (managed con feature flags)
