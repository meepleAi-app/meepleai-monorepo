# UI ↔ API Gap Analysis

**Data analisi**: 2026-02-11
**Scope**: Identificare endpoint API senza UI corrispondente

---

## 📊 Executive Summary

| Metrica | Valore | Note |
|---------|--------|------|
| **Endpoint API totali** | 627 | Da 82 file di routing |
| **Route frontend** | 132 | Next.js App Router |
| **Coverage UI completa** | ~37% | ~230 endpoint con UI |
| **Coverage UI parziale** | ~12% | ~75 endpoint UI incompleta |
| **Gap critici** | ~40 | ~51% endpoint senza UI |

**Verdict**: Coverage accettabile per MVP, ma gap significativi nelle funzionalità utente core.

---

## 🔴 HIGH Priority Gaps (Core User Features)

### 1. Wishlist Management System
**Issue correlata**: #3917 (già tracciata)

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/wishlist` | GET | ❌ Missing | Create `/library/wishlist` page |
| `/wishlist` | POST | ❌ Missing | Add "Add to Wishlist" button (game cards) |
| `/wishlist/{id}` | PUT | ❌ Missing | Edit wishlist item modal |
| `/wishlist/{id}` | DELETE | ❌ Missing | Remove from wishlist button |
| `/wishlist/highlights` | GET | ❌ Missing | Dashboard widget "Wishlist Highlights" |

**Impact**: Wishlist è feature richiesta dagli utenti, attualmente non utilizzabile.

**Raccomandazione**: Priorità 1 - Implementare in Sprint corrente

---

### 2. Notification System
**Issue correlata**: #2053 (Upload Notifications)

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/notifications` | GET | ❌ Missing | Header notification bell icon |
| `/notifications/unread-count` | GET | ❌ Missing | Badge count on bell icon |
| `/notifications/{id}/mark-read` | POST | ❌ Missing | Click notification to mark read |
| `/notifications/mark-all-read` | POST | ❌ Missing | "Mark all read" button in dropdown |

**Impact**: Gli utenti non vedono notifiche di upload PDF, processing completati, errori.

**Raccomandazione**: Priorità 1 - Critical UX issue

---

### 3. Play Records - Actions Mancanti
**Issue correlata**: #3889-3890 (Play Records Implementation)

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/play-records/{id}/players` | POST | ❌ Missing | "Add Player" button in edit page |
| `/play-records/{id}/scores` | POST | ❌ Missing | Score tracking table in detail page |
| `/play-records/{id}/start` | POST | ❌ Missing | "Start Session" button in detail |
| `/play-records/{id}/complete` | POST | ❌ Missing | "Complete Session" button in detail |

**Impact**: Play Records parzialmente utilizzabile - mancano azioni core.

**Raccomandazione**: Priorità 2 - Complete existing feature

---

### 4. Achievement System
**Issue correlata**: #3922 (Gamification)

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/achievements` | GET | ❌ Missing | Create `/profile/achievements` page |
| `/achievements/recent` | GET | ❌ Missing | Dashboard widget "Recent Achievements" |

**Impact**: Sistema gamification invisibile agli utenti.

**Raccomandazione**: Priorità 2 - Gamification incomplete without UI

---

## 🟡 MEDIUM Priority Gaps (Admin & Self-Service)

### 5. Admin User Management - Bulk Operations

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/admin/users/bulk/password-reset` | POST | ❌ Missing | Bulk actions toolbar |
| `/admin/users/bulk/role-change` | POST | ❌ Missing | Bulk role change modal |
| `/admin/users/bulk/import` | POST | ❌ Missing | CSV import button |
| `/admin/users/bulk/export` | GET | ❌ Missing | CSV export button |

**Raccomandazione**: Priorità 3 - Admin efficiency enhancement

---

### 6. Admin User Detail - Missing Tabs

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/admin/users/{id}/activity` | GET | ❌ Missing | Add "Activity" tab |
| `/admin/users/{id}/library/stats` | GET | ❌ Missing | Add "Library Stats" tab |
| `/admin/users/{id}/badges` | GET | ❌ Missing | Add "Badges" tab |
| `/admin/users/{id}/role-history` | GET | ❌ Missing | Add "Role History" tab |
| `/admin/users/{id}/lockout-status` | GET | ❌ Missing | Add to "Security" section |
| `/admin/users/{id}/sessions` | DELETE | ❌ Missing | "Kill all sessions" button |

**Raccomandazione**: Priorità 3 - Enhanced admin UX

---

### 7. Admin User Actions

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/admin/users/{id}/suspend` | POST | ❌ Missing | Add to action menu |
| `/admin/users/{id}/unsuspend` | POST | ❌ Missing | Add to action menu |
| `/admin/users/{id}/unlock` | POST | ❌ Missing | Add to lockout section |
| `/admin/users/{id}/reset-password` | POST | ❌ Missing | Quick actions dropdown |
| `/admin/users/{id}/send-email` | POST | ❌ Missing | Quick actions dropdown |
| `/admin/users/{id}/impersonate` | POST | ❌ Missing | Debug actions (⚠️ danger) |

**Raccomandazione**: Priorità 3 - Complete admin tooling

---

### 8. Two-Factor Authentication (2FA)

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/auth/2fa/setup` | POST | ❌ Missing | Create `/settings/security` page |
| `/auth/2fa/enable` | POST | ❌ Missing | Enable 2FA button |
| `/auth/2fa/disable` | POST | ❌ Missing | Disable 2FA button |
| `/users/me/2fa/status` | GET | ❌ Missing | 2FA status display |
| `/auth/admin/2fa/disable` | POST | ❌ Missing | Admin force-disable (emergency) |

**Raccomandazione**: Priorità 2 - Security feature critical for Enterprise tier

---

### 9. API Key Advanced Features

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/api-keys/{id}/rotate` | POST | ❌ Missing | "Rotate Key" button |
| `/api-keys/{id}/usage` | GET | ❌ Missing | Usage stats tab |
| `/api-keys/{id}/stats` | GET | ❌ Missing | Analytics tab |
| `/api-keys/{id}/logs` | GET | ❌ Missing | Request logs tab |
| `/admin/api-keys/stats` | GET | ❌ Missing | Admin overview stats widget |
| `/admin/api-keys/bulk/export` | GET | ❌ Missing | Export button |
| `/admin/api-keys/bulk/import` | POST | ❌ Missing | Import button |

**Raccomandazione**: Priorità 4 - Advanced admin features

---

## 🟢 LOW Priority Gaps (System/Internal)

### 10. Share Links Management

| Endpoint | Metodo | UI Status | Action Required |
|----------|--------|-----------|-----------------|
| `/share-links` | POST | ❌ Missing | Chat UI "Share" button |
| `/share-links/{id}` | DELETE | ❌ Missing | Manage shared links page |
| `/shared/thread/comment` | POST | ❌ Missing | Public shared page comments |

**Raccomandazione**: Priorità 5 - Nice-to-have feature enhancement

---

## 📋 Implementation Roadmap

### Sprint 1-2 (Immediate)
**Focus**: Core user features con impatto UX massimo

- [ ] **Wishlist System** (#3917)
  - Page: `/library/wishlist`
  - Components: WishlistButton, WishlistTable, WishlistFilters
  - Estimated: 5-8 giorni

- [ ] **Notification System** (#2053)
  - Components: NotificationBell, NotificationDropdown, NotificationList
  - Integration: Layout header + SSE events
  - Estimated: 3-5 giorni

- [ ] **Play Records Actions** (#3889-3890)
  - Enhance: `/play-records/[id]` page
  - Components: AddPlayerDialog, ScoreTracker, SessionControls
  - Estimated: 3-4 giorni

### Sprint 3-4 (Short-term)
**Focus**: Security & Admin efficiency

- [ ] **2FA Self-Service** (New issue)
  - Page: `/settings/security`
  - Components: TwoFactorSetup, QRCodeDisplay, BackupCodes
  - Estimated: 5-7 giorni

- [ ] **Achievement Display** (#3922)
  - Page: `/profile/achievements` or `/badges`
  - Components: AchievementCard, AchievementGrid, ProgressBars
  - Estimated: 3-4 giorni

- [ ] **Admin Bulk Operations** (New issue)
  - Enhance: `/admin/users` page
  - Components: BulkActionsToolbar, BulkRoleChangeDialog, ImportExportButtons
  - Estimated: 4-6 giorni

### Sprint 5-6 (Medium-term)
**Focus**: Admin advanced features

- [ ] **User Detail Tabs** (New issue)
  - Tabs: Activity, Library Stats, Badges, Role History, Sessions
  - Estimated: 6-8 giorni

- [ ] **API Key Advanced Features** (New issue)
  - Tabs: Usage, Stats, Logs
  - Actions: Rotate, Export, Import
  - Estimated: 4-5 giorni

---

## 🎯 Quick Wins (< 2 giorni ciascuno)

Implementazioni veloci con alto impatto:

1. **Notification Bell Icon** (1 giorno)
   - Header component + unread count badge
   - Dropdown con lista notifiche
   - Mark as read functionality

2. **Wishlist Toggle Button** (1 giorno)
   - Add MeepleCard quick action
   - Heart icon toggle
   - Optimistic UI update

3. **Play Records Start/Complete** (1.5 giorni)
   - Add buttons to detail page
   - Status workflow UI
   - Success feedback

4. **Achievement Badge** (0.5 giorni)
   - Dashboard widget "Recent Achievements"
   - Simple card display

---

## 📊 Coverage Analysis by Bounded Context

| Bounded Context | Endpoints | UI Coverage | Gap % | Priority |
|----------------|-----------|-------------|-------|----------|
| **UserLibrary** | ~45 | Partial (60%) | 40% | HIGH |
| **Administration** | ~180 | Good (70%) | 30% | MEDIUM |
| **Authentication** | ~25 | Good (75%) | 25% | MEDIUM |
| **GameManagement** | ~80 | Excellent (85%) | 15% | LOW |
| **KnowledgeBase** | ~60 | Excellent (90%) | 10% | LOW |
| **SessionTracking** | ~40 | Partial (55%) | 45% | HIGH |
| **UserNotifications** | ~15 | None (0%) | 100% | **CRITICAL** |

**Findings**:
- **KnowledgeBase** e **GameManagement**: Ottima coverage (85-90%)
- **UserNotifications**: Coverage 0% - sistema invisibile agli utenti ⚠️
- **UserLibrary**: Gap significativi (wishlist, achievements)
- **Administration**: Buona base, mancano bulk operations e tab avanzate

---

## 🤔 Discovery Questions

Per prioritizzare ulteriormente:

1. **Wishlist**: È feature richiesta da utenti? Analytics mostra interesse?
2. **Notifications**: Qual è l'impatto UX attuale? Gli utenti si lamentano di feedback mancante?
3. **2FA**: Quanti utenti Enterprise necessitano autenticazione forte?
4. **Achievements**: Sistema gamification è strategico per retention?
5. **Admin Bulk Ops**: Quanto tempo risparmia agli admin? Quanti utenti gestiscono?

---

## 📝 Next Steps

Vuoi che:

**A)** Creo issue su GitHub per i top 5 gap (Wishlist, Notifications, Play Records, 2FA, Achievements)

**B)** Genero spec tecniche dettagliate per 1-2 gap prioritari

**C)** Implemento subito un "Quick Win" (es. Notification Bell in 1 giorno)

**D)** Faccio analisi più approfondita su bounded context specifico

Quale approccio preferisci?
