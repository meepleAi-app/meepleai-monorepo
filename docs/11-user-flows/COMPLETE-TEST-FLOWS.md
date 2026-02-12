# MeepleAI - Complete User Test Flows

**Date**: 2026-01-26 | **Purpose**: E2E test flow definitions

---

## Executive Summary

| Category | Existing | Missing | Total |
|----------|----------|---------|-------|
| **Authentication** | 8 | 6 | 14 |
| **Game Discovery** | 5 | 3 | 8 |
| **Library** | 6 | 4 | 10 |
| **AI Chat** | 7 | 5 | 12 |
| **Sessions** | 6 | 5 | 11 |
| **Editor** | 8 | 4 | 12 |
| **Admin** | 10 | 8 | 18 |
| **Errors** | 5 | 15 | 20 |
| **TOTAL** | **55** | **50** | **105** |

---

## 1. Authentication (14 flows)

### Existing ✅ (8)

| ID | Flow | Test File |
|----|------|-----------|
| AUTH-01 | Registration | `auth.spec.ts` |
| AUTH-02 | Login | `auth.spec.ts` |
| AUTH-03 | Logout | `auth.spec.ts` |
| AUTH-04 | OAuth Registration | `auth-oauth-registration.spec.ts` |
| AUTH-05 | OAuth Link/Unlink | `auth-oauth-advanced.spec.ts` |
| AUTH-06 | Password Reset | `auth-password-reset.spec.ts` |
| AUTH-07 | 2FA Enable/Disable | `auth-2fa-complete.spec.ts` |
| AUTH-08 | Session Management | `auth-logout-all-devices.spec.ts` |

### Missing ❌ (6)

**AUTH-09: Email Verification**
```gherkin
Given I registered but haven't verified email
When I click verification link
Then I should see "Email verified" + have full access

Edge: Token expired (24h) | Already used | Rate limit resend (1/min)
```

**AUTH-10: Account Lockout**
```gherkin
Given I enter wrong password 5 times
Then account locked 15min
When I wait 15min + correct password
Then login successful

Edge: Admin unlock | Progressive lockout (15min → 1h → 24h)
```

**AUTH-11: Login Notifications**
```gherkin
Given I have notifications enabled
When I login from new device
Then I receive email with device info + location

Edge: Suspicious login (impossible travel) → Require 2FA
```

**AUTH-12: Device Management**
```gherkin
Given I have 3 active sessions
When I view Settings > Security > Devices
Then I see all sessions with device info
When I revoke one device
Then that session terminates, others remain
```

**AUTH-13: Remember Me**
```gherkin
When I check "Remember me" and login
Then session lasts 30 days
When I login without "Remember me"
Then session expires in 24h
```

**AUTH-14: API Key Lifecycle**
```gherkin
When I create API key with scopes ["read:games"]
And I use key to call API
And I view usage stats
And I rotate key
And I revoke key
Then key no longer works
```

---

## 2. Game Discovery (8 flows)

### Existing ✅ (5)

| ID | Flow | Test File |
|----|------|-----------|
| DISC-01 | Browse Catalog | `game-search-browse.spec.ts` |
| DISC-02 | Search by Name | `game-search-browse.spec.ts` |
| DISC-03 | Filter Category/Players | `game-search-browse.spec.ts` |
| DISC-04 | Game Details | `giochi-game-detail.spec.ts` |
| DISC-05 | BGG Import | `bgg-integration.spec.ts` |

### Missing ❌ (3)

**DISC-06: Autocomplete** → Type "cat" → See ["Catan", "Carcassonne", "Cat Lady"]
**DISC-07: Similar Games** → View "Catan" → See similar mechanics → Add to library
**DISC-08: Advanced Filters** → Filter players "7+" + playtime "<30min" → No results → Suggest broaden

---

## 3. Library Management (10 flows)

### Existing ✅ (6)

| ID | Flow | Test File |
|----|------|-----------|
| LIB-01 | Add Game | `library.spec.ts` |
| LIB-02 | Remove Game | `library.spec.ts` |
| LIB-03 | Mark Favorite | `library.spec.ts` |
| LIB-04 | Bulk Operations | `library-bulk-operations.spec.ts` |
| LIB-05 | Quota Display | `library.spec.ts` |
| LIB-06 | Custom PDF Upload | `pdf-upload-journey.spec.ts` |

### Missing ❌ (4)

**LIB-07: Session Quota**
```gherkin
Given I'm Free tier with 3 active sessions
When I try create new session
Then I see "Limit reached (3/3)" + upgrade prompt

Edge: Session ends → Quota freed | Tier downgrade with sessions over limit
```

**LIB-08: Wishlist** → Add to wishlist → Move to library when owned
**LIB-09: Game Loans** → Mark "Loaned to John" → Send reminder email
**LIB-10: Play History** → View play count + dates + win/loss stats

---

## 4. AI Chat & RAG (12 flows)

### Existing ✅ (7)

| ID | Flow | Test File |
|----|------|-----------|
| CHAT-01 | Ask Question | `chat.spec.ts` |
| CHAT-02 | Streaming | `chat-streaming.spec.ts` |
| CHAT-03 | Citations | `chat-citations.spec.ts` |
| CHAT-04 | Multi-turn | `qa-multi-turn.spec.ts` |
| CHAT-05 | Context Switch | `chat-context-switching.spec.ts` |
| CHAT-06 | Export Chat | `chat-export.spec.ts` |
| CHAT-07 | History | `chat.spec.ts` |

### Missing ❌ (5)

**CHAT-08: Stop Streaming** → Click "Stop" → Keeps partial response → Enable input
**CHAT-09: Feedback** → Thumbs up/down → Optional reason + comment
**CHAT-10: Voice Input** → Microphone → Web Speech API → Transcribe → Submit
**CHAT-11: Share Thread** → Get shareable link → Read-only view for recipients
**CHAT-12: Quick Questions** → Click "How to setup?" → Instant answer → Follow-up enabled

---

## 5. Game Sessions (11 flows)

### Existing ✅ (6)

| ID | Flow | Test File |
|----|------|-----------|
| SESS-01 | Create Session | Partial |
| SESS-02 | Add Players | Partial |
| SESS-03 | State Tracking | Partial |
| SESS-04 | Complete | Partial |
| SESS-05 | History | Partial |
| SESS-06 | Pause/Resume | Partial |

### Missing ❌ (5)

**SESS-07: Limits Enforcement**
```gherkin
Given Free user with 3/3 sessions
When I create new → See "Limit reached" + upgrade prompt

Given Normal user with 10/10 sessions
When I create new → See "Upgrade to Premium for unlimited"
```

**SESS-08: Invite Link** → Generate link → Friend joins as player
**SESS-09: Real-time Sync** → Player 1 updates score → Player 2 sees instantly (SignalR)
**SESS-10: AI Suggestions** → "Suggest Move" → See AI recommendations → Apply
**SESS-11: Statistics** → View 20 sessions → Win rate + avg duration + frequent players

---

## 6. Editor Flows (12 flows)

### Existing ✅ (8)

| ID | Flow | Test File |
|----|------|-----------|
| EDIT-01 | Create Game | `admin-game-creation.spec.ts` |
| EDIT-02 | Edit Game | `admin-games-workflow.spec.ts` |
| EDIT-03 | Upload PDF | `pdf-upload-journey.spec.ts` |
| EDIT-04 | Processing Monitor | `pdf-processing-progress.spec.ts` |
| EDIT-05 | Submit Approval | Partial |
| EDIT-06 | FAQ Management | `game-faq.spec.ts` |
| EDIT-07 | BGG Bulk Import | `bgg-integration.spec.ts` |
| EDIT-08 | Version History | ⚠️ Partial |

### Missing ❌ (4)

**EDIT-09: OCR Validation** → View extracted text + confidence → Correct errors
**EDIT-10: Multi-language PDF** → Upload EN+DE PDF → Extract both → Search works
**EDIT-11: Queue Position** → See "Position 3 of 12" + estimated review time
**EDIT-12: Draft Preview** → Preview as users see it → Edit from preview

---

## 7. Admin Flows (18 flows)

### Existing ✅ (10)

| ID | Flow | Test File |
|----|------|-----------|
| ADM-01 | User List | `admin-users.spec.ts` |
| ADM-02 | Role Assignment | `admin-users.spec.ts` |
| ADM-03 | Approval Queue | Partial |
| ADM-04 | Feature Flags | `admin-configuration.spec.ts` |
| ADM-05 | System Health | `admin-infrastructure.spec.ts` |
| ADM-06 | Alert Config | `admin-alert-config.spec.ts` |
| ADM-07 | Prompt Management | `admin-prompts-management.spec.ts` |
| ADM-08 | Bulk Export | `admin-bulk-export.spec.ts` |
| ADM-09 | Analytics | `admin-analytics.spec.ts` |
| ADM-10 | Audit Log | ⚠️ Partial |

### Missing ❌ (8)

**ADM-11: Session Limits Config** → Configure limits per tier (Free=3, Normal=10, Premium=∞)
**ADM-12: PDF Limits Config** → Max file size, max pages, allowed types
**ADM-13: Tier Feature Flags** → Toggle feature ON/OFF per tier
**ADM-14: AI Usage Analytics** → Token consumption by user/model/time + costs
**ADM-15: User Impersonation** → Impersonate user → See as they see → "Stop Impersonating"
**ADM-16: Locked Accounts** → View locked users + reason → Unlock
**ADM-17: Email Verification Admin** → View unverified → Manually verify or resend
**ADM-18: 2FA Override** → Disable 2FA for locked user → Audit log

---

## 8. Error & Edge Cases (20 flows)

### Existing ✅ (5)

| ID | Flow | Test File |
|----|------|-----------|
| ERR-01 | Network Error | `error-handling.spec.ts` |
| ERR-02 | Session Expiration | `session-expiration.spec.ts` |
| ERR-03 | 404 Not Found | `error-handling.spec.ts` |
| ERR-04 | 403 Forbidden | `rbac-authorization.spec.ts` |
| ERR-05 | Offline Mode | `offline-resilience.spec.ts` |

### Missing ❌ (15)

**ERR-06: Rate Limiting (429)** → See "Too many requests" + countdown → Auto-retry
**ERR-07: Token Expiration Mid-Op** → Session expires during PDF upload → Redirect + preserve upload
**ERR-08: Concurrent Edit** → Two editors → Conflict warning → Merge or overwrite
**ERR-09: PDF Processing Fail** → Corrupted PDF → Specific error → Retry with different file
**ERR-10: Tier Change Over-Quota** → Downgrade Premium→Free with 100 games → Warning → Choose 5 to keep
**ERR-11: OAuth Provider Down** → Google unavailable → "Service unavailable" + alternatives
**ERR-12: SSE Interruption** → Network drops → "Connection lost" + preserve partial + Retry
**ERR-13: Upload Validation** → Wrong file type → "Invalid file type" (before upload)
**ERR-14: Quota Exceeded** → Library 5/5 → "Quota exceeded" + upgrade or remove
**ERR-15: 2FA Recovery** → Lost device → Use backup code → Prompt new 2FA setup
**ERR-16: DB Connection Lost** → "Service unavailable" + show cached data if available
**ERR-17: AI Service Down** → "AI unavailable" + show cached FAQ
**ERR-18: Invalid Deep Link** → Game deleted → "Not found" + suggest similar
**ERR-19: Concurrent Session Race** → At 9/10 limit → Two devices create → Only one succeeds
**ERR-20: Import Conflicts** → BGG import "Catan" (exists) → "Already exists" + merge or skip

---

## 9. Accessibility (3 flows)

**ACC-01: Keyboard-Only** → Complete journey (Login → Browse → Add → Chat → Ask) with Tab+Enter+Esc
**ACC-02: Screen Reader** → Announcements for actions ("Game added") + error messages
**ACC-03: Reduced Motion** → `prefers-reduced-motion` → Disable/simplify animations

---

## 10. Priority Matrix

### P0 - Critical (Must Test Before Release)

| ID | Flow | Reason |
|----|------|--------|
| AUTH-09 | Email Verification | Security |
| AUTH-10 | Account Lockout | Security |
| LIB-07 | Session Quota | New feature |
| SESS-07 | Session Limits | New feature |
| ADM-11 | Limits Config | Admin control |
| ERR-06 | Rate Limiting | Security |
| ERR-14 | Quota Exceeded | UX critical |

### P1 - High (Sprint)

| ID | Flow | Reason |
|----|------|--------|
| AUTH-11 | Login Notifications | Security |
| AUTH-12 | Device Management | Security |
| CHAT-08 | Stop Streaming | UX |
| CHAT-09 | Feedback | Quality |
| ADM-12 | PDF Limits | Admin |
| ADM-13 | Tier Flags | Feature |

### P2 - Medium (Quarter)

DISC-06 (Autocomplete), LIB-08 (Wishlist), CHAT-10 (Voice), ADM-14 (AI Analytics), ERR-08 (Concurrent Edit)

### P3 - Low (When Capacity)

LIB-09 (Loans), CHAT-11 (Share), ADM-15 (Impersonate)

---

## 11. Test File Mapping

```
apps/web/e2e/
├── auth/
│   ├── email-verification.spec.ts      # AUTH-09
│   ├── account-lockout.spec.ts         # AUTH-10
│   ├── login-notifications.spec.ts     # AUTH-11
│   └── device-management.spec.ts       # AUTH-12
├── library/
│   ├── session-quota.spec.ts           # LIB-07
│   ├── wishlist.spec.ts                # LIB-08
│   └── game-loans.spec.ts              # LIB-09
├── chat/
│   ├── stop-streaming.spec.ts          # CHAT-08
│   ├── response-feedback.spec.ts       # CHAT-09
│   └── voice-input.spec.ts             # CHAT-10
├── sessions/
│   ├── session-limits.spec.ts          # SESS-07
│   ├── session-invites.spec.ts         # SESS-08
│   └── realtime-sync.spec.ts           # SESS-09
├── admin/
│   ├── session-limits-config.spec.ts   # ADM-11
│   ├── pdf-limits-config.spec.ts       # ADM-12
│   ├── tier-feature-flags.spec.ts      # ADM-13
│   ├── ai-usage-analytics.spec.ts      # ADM-14
│   └── user-impersonation.spec.ts      # ADM-15
└── errors/
    ├── rate-limiting.spec.ts           # ERR-06
    ├── token-expiration.spec.ts        # ERR-07
    ├── concurrent-edit.spec.ts         # ERR-08
    └── quota-exceeded.spec.ts          # ERR-14
```

---

## 12. Coverage Gap Analysis

| Area | Current | Target | Gap |
|------|---------|--------|-----|
| Authentication | 85% | 100% | 15% |
| Discovery | 90% | 100% | 10% |
| Library | 80% | 100% | 20% |
| AI Chat | 75% | 95% | 20% |
| Sessions | 65% | 95% | 30% |
| Editor | 85% | 95% | 10% |
| Admin | 70% | 95% | 25% |
| Errors | 50% | 90% | 40% |

---

## Next Actions

1. Create GitHub issues for P0/P1 missing flows
2. Implement backend for missing features (email verification, session limits)
3. Create E2E tests per file mapping
4. Update docs as tests pass

---

**Author**: Gap Analysis System | **Version**: 1.0
