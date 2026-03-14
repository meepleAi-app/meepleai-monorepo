# Issue Resolution Roadmap

**Date**: 2026-03-13 | **Open Issues**: 109 (was 124) | **Branch**: `main-dev`

## Summary

| Category | Count | Status |
|----------|-------|--------|
| Wave 1: Layout & Navigation | 12 | ✅ COMPLETE (closed 2026-03-13) |
| Wave 2: Admin Core | 13 | 🔵 NEXT |
| Wave 3: RAG Dashboard | ~20 | ⏳ PENDING |
| Wave 4: Game Sessions | ~19 | ⏳ PENDING |
| Wave 5: Infrastructure | ~4 | ⏳ PENDING |
| Deferred: Publisher | 18 | 🔻 LOW PRIORITY |
| Deferred: AI Training | 21 | 🔻 LOW PRIORITY |
| Duplicates closed | 2 | ✅ CLOSED (#234, #245) |

## Wave 1: COMPLETE ✅ (Closed 2026-03-13)

All 9 issues + 3 epics closed. Already implemented in `main-dev`.

| Epic | Issues Closed |
|------|--------------|
| #316 Layout Refactor | #343, #344, #345, #346 |
| #314 Desktop UX Polish | #337, #338 |
| #317 Welcome Page Redesign | #348, #349, #350 |
| Standalone | #336 (GameBackContent, merged via PR #384) |

## Wave 2: Admin Core — Invitations, Onboarding, Audit

**Priority**: HIGH | **Effort**: ~3 weeks | **Type**: Backend + Frontend

### Epic #310: Admin User Invitations (4 issues)

| # | Title | Type | Depends On |
|---|-------|------|------------|
| #319 | UserInvitation DDD entity + repositories | Backend | — |
| #320 | Invitation command handlers + endpoints | Backend | #319 |
| #321 | Accept-invite page with password setup | Frontend | #320 |
| #322 | Admin invite UI modal and wiring | Frontend | #320 |

### Epic #311: User Onboarding Wizard (4 issues)

| # | Title | Type | Depends On |
|---|-------|------|------------|
| #323 | Onboarding flags + complete command | Backend | — |
| #324 | Onboarding wizard component (3 steps) | Frontend | #323 |
| #325 | Onboarding middleware guard + page layout | Frontend | #324 |
| #326 | Reminder banner for skipped wizard | Frontend | #325 |

### Epic #313: Admin Audit Log (5 issues)

| # | Title | Type | Depends On |
|---|-------|------|------------|
| #331 | AuditLogEntry entity + repository | Backend | — |
| #332 | ChangeUserRoleCommand + audit events | Backend | #331 |
| #333 | Audit log API endpoints | Backend | #332 |
| #334 | AuditLogTable + admin page | Frontend | #333 |
| #335 | InlineRoleSelect + UserActivityTimeline | Frontend | #334 |

**Execution**: #310 and #311 backend in parallel → frontend. #313 after (logs invitations/onboarding).

## Wave 3: RAG Dashboard & Game Content

**Priority**: HIGH | **Effort**: ~3-4 weeks | **Type**: Full-stack + Testing

### Epic #259: Admin RAG Dashboard (8 issues)

BE: #260 → #262 → FE: #263 → #264 → #265 → #266 → #267 → #268

### Epic #236: Shared Game Content (3 issues)

#119 → #117 → #118

### Epic #251: RAG E2E Testing (7 issues)

#246 + #252 → #248 → #249 + #253 → #254 → #255 + #256 + #257

### Standalone: #269 (notification toast), #347 (entity links)

## Wave 4: Game Host Session Experience

**Priority**: MEDIUM | **Effort**: ~3 weeks

### Epic #280: Game Host Session (7 issues)

#273 → #272 → #274 → #275 → #276 → #277 → #278

### Epic #235: Game Night Sprint 2 (8 issues)

#211–#218 (specs and plans)

### Epic #312: Voice Chat (4 issues)

BE: #327 → FE: #328 → #329 → #330

## Wave 5: Infrastructure & Polish

**Priority**: LOW-MEDIUM | **Effort**: ~2 weeks

- #318 Seeding Architecture (YAML Manifests)
- #124 Admin Infrastructure Panel
- #28 Region-aware routing
- #113 MAU monitoring

## Deferred (Low Priority)

### Publisher Portal (#6): 18 issues

#7–#23 — Entire publisher portal feature

### AI Fine-Tuning (#237, #238): 21 issues

#24, #66–#86 — Training data, fine-tuning prerequisites, evaluation

## Timeline

```
Week 1-3   │ WAVE 2: Admin Core (13 issues)
Week 3-6   │ WAVE 3: RAG Dashboard (20 issues)
Week 6-9   │ WAVE 4: Game Sessions (19 issues)
Week 9-11  │ WAVE 5: Infrastructure (4+ issues)
───────────┤
DEFERRED   │ Publisher (18) + AI Training (21) = 39 issues
```
