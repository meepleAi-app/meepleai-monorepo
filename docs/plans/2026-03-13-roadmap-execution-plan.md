# MeepleAI Roadmap Execution Plan — Q1-Q2 2026

> **For agentic workers:** REQUIRED: Use superpowers:subagent-driven-development (if subagents available) or superpowers:executing-plans to implement this plan. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Execute the MeepleAI roadmap across 5 sprints, delivering Game Night flow, Admin Infrastructure, Layout/UX improvements, and Admin Content features in ~10 weeks. Publisher Portal and AI Training are deferred to backlog.

**Architecture:** CQRS + DDD bounded contexts on .NET 9 backend, Next.js 16 frontend. All endpoints use MediatR. Feature branches merge to parent branch (`main-dev` or `frontend-dev`).

**Tech Stack:** .NET 9, PostgreSQL 16, Redis, Qdrant, SignalR, Next.js 16, React 19, Tailwind 4, Zustand, React Query

**Date:** 2026-03-13 | **Source:** `rdmap.md` + GitHub open issues

---

## Dashboard Summary

| #  | Epic / Track | Open | Priority | Status |
|----|-------------|------|----------|--------|
| ~~1~~ | ~~#279 API Startup Bugs~~ | 0 | ✅ DONE | Closed 2026-03-13 |
| 2  | [#284 Game Night Improvvisata](#sprint-1a-game-night-improvvisata) | 13 | 🔴 CRITICAL | Ph 2 next |
| 3  | [#315 Game Night E2E Flow](#sprint-1b-game-night-e2e-flow) | 4 | 🔴 CRITICAL | New |
| ~~4~~ | [#124 Admin Infra Panel](#sprint-2a-admin-infra-phase-2) | **6** | 🔴 CRITICAL | **Ph 1+2 done, Ph 3 next** |
| 5  | [#316 Layout Refactor](#sprint-1c-layout-refactor) | 4 | 🟡 IMPORTANT | New |
| 6  | [#317 Welcome Page Redesign](#sprint-1c-welcome-page-redesign) | 3 | 🟡 IMPORTANT | New |
| 7  | [#310 Admin User Invitations](#sprint-2b-admin-user-invitations) | 4 | 🟡 IMPORTANT | New |
| 8  | [#311 Onboarding Wizard](#sprint-2b-onboarding-wizard) | 4 | 🟡 IMPORTANT | New |
| 9  | [#313 Admin Audit Log](#sprint-2c-admin-audit-log) | 5 | 🟡 IMPORTANT | New |
| 10 | [#235 Game Night Sprint 2](#sprint-3a-game-night-sprint-2) | 8 | 🟡 IMPORTANT | Ready |
| 11 | [#236 Admin Shared Game Content](#sprint-3b-admin-shared-content) | 4 | 🟡 IMPORTANT | Ready |
| 12 | [#259 Admin RAG Dashboard](#sprint-3b-admin-rag-dashboard) | 9 | 🟡 IMPORTANT | Ready |
| 13 | [#314 Desktop UX Polish](#sprint-3c-desktop-ux-polish) | 3 | 🟢 RECOMMENDED | New |
| 14 | [#318 Hybrid Layered Seeding](#sprint-3c-hybrid-layered-seeding) | 4 | 🟢 RECOMMENDED | New (replaces #234) |
| 15 | [#312 Voice-to-Text & TTS](#sprint-4-voice-to-text--tts) | 4 | 🟢 RECOMMENDED | New |
| 16 | [#245/#251 RAG E2E Tests](#sprint-4-rag-e2e-tests) | **~6** | 🟢 RECOMMENDED | Ready (#247,#250 closed) |
| 17 | [#280 Game Host Session](#sprint-4-game-host-session) | ~8 | 🟢 RECOMMENDED | Blocked by #284 Ph3 |
| 18 | [#347 Entity Links + RAG Builder](#standalone-issues) | 1 | 🟢 RECOMMENDED | New |
| 19 | [#269 Notification Toast](#standalone-issues) | 1 | 🟢 RECOMMENDED | Standalone |
| — | **LOW PRIORITY (deferred)** | | | |
| 20 | [#6 Publisher Portal](#low-priority-deferred) | 17 | 🔵 LOW | User-deferred |
| 21 | [#237 Fine-Tuning Prerequisites](#low-priority-deferred) | 14 | 🔵 LOW | User-deferred |
| 22 | [#238 Fact Extraction Pipeline](#low-priority-deferred) | 6 | 🔵 LOW | Blocked by #237 |
| — | Standalone: #24, #28 | 2 | 🔵 LOW | Future |

**Total: ~122 open issues across 22 tracks (6 closed since v3)**

---

## Execution Overview

```
WEEK 1          WEEK 2-3           WEEK 3-4          WEEK 5-6          WEEK 7-8          WEEK 9-10
─────────────── ────────────────── ────────────────── ────────────────── ────────────────── ──────────
Track A: #279✅→ #284 Ph2 ────────→ #284 Ph3 ────────→ #284 Ph4-5 ─────→ #280 Game Host
Track B:         #315 GN Flow ───→ #316 Layout ──────→ #317 Welcome ───→ #314 UX Polish
Track C: #124Ph2✅ ───────────────→ #124 Ph3 ────────→ #124 Ph4 ────────→ #245 E2E Tests
Track D:                            #310 Invitations → #311 Onboarding → #313 Audit Log
Track E:                            #235 Specs ──────→ #235 Impl+Qual ─→ #236 + #259
Track F:                                                                  #318 Seeding      #312 Voice
```

**Critical path:** #284 Ph2 → #284 Ph3 → #284 Ph4-5 → #280
**Completed:** #279 ✅ | #124 Ph1+Ph2 ✅ (6 remaining: Ph3+Ph4)

---

## Sprint 0: Blockers (Day 1) — ✅ COMPLETE

### Issue #279 — API Startup Bugs — ✅ CLOSED (2026-03-13)

**Branch:** `fix/issue-279-startup-bugs` — merged
**Resolution:** All 3 bugs fixed (SeedOrchestrator crash, duplicate endpoint, Postgres connectivity)

- [x] Diagnose + fix Postgres connectivity
- [x] Verify all 3 fixes
- [x] PR #355 merged, issue closed

---

## Sprint 1A: Game Night Improvvisata — Phase 2 (Week 1-2)

### Branch: `game-night-improvvisata` (existing)

All Phase 2 work goes on the existing `game-night-improvvisata` branch.

---

### Issue #294 — TierDefinition Admin CRUD Endpoints

**Area:** Backend | **Effort:** ~1 day | **Parallel with:** #295

- [ ] **Step 1: Write failing tests for TierDefinition CRUD**
- [ ] **Step 2: Implement domain + command/query handlers**
- [ ] **Step 3: Run tests to verify pass**
- [ ] **Step 4: Wire endpoints** (`POST/PUT/GET /api/v1/admin/tiers`)
- [ ] **Step 5: Commit** `feat(admin): TierDefinition CRUD endpoints (#294)`

---

### Issue #295 — User Usage Endpoint & Redis Tracking

**Area:** Backend | **Effort:** ~1.5 days | **Parallel with:** #294

- [ ] **Step 1: Write failing tests**
- [ ] **Step 2: Implement Redis tracking service** (atomic counters per user)
- [ ] **Step 3: Implement query handler + endpoint** (`GET /api/v1/users/me/usage`)
- [ ] **Step 4: Run tests, commit** `feat(admin): user usage endpoint with Redis tracking (#295)`

---

### Issue #296 — Frontend Usage Dashboard & Quota UI

**Area:** Frontend | **Effort:** ~1.5 days | **Depends on:** #294, #295

- [ ] **Step 1: Implement API client + hooks** (Zod schema, `useUserUsage()`)
- [ ] **Step 2: Build QuotaProgressBar component** (thresholds: green/amber/red)
- [ ] **Step 3: Build UsageDashboard page** (4-panel layout)
- [ ] **Step 4: Run tests, commit** `feat(frontend): usage dashboard with quota UI (#296)`

---

### Issue #297 — Pricing Page

**Area:** Frontend | **Effort:** ~1 day | **Depends on:** #294

- [ ] **Step 1: Build PricingCard component** (Free, Premium, Contributor)
- [ ] **Step 2: Build TierComparison table** (responsive)
- [ ] **Step 3: Build pricing page** (SSR-friendly)
- [ ] **Step 4: Tests, commit** `feat(frontend): pricing page with tier comparison (#297)`

---

### Phase 2 Completion Gate

- [ ] All 4 issues passing tests
- [ ] Update issues #294-#297 on GitHub: close with commit reference
- [ ] Update rdmap.md: Mark Phase 2 as ✅

---

## Sprint 1B: Game Night E2E Flow (Week 2-3)

### Epic #315 — Complete End-to-End Experience

**Branch:** feature branch from `frontend-dev`
**Parent:** `frontend-dev`

These issues complete the private game → session → AI chat flow on frontend.

| Issue | Title | Depends On |
|-------|-------|------------|
| #339 | Wire private game detail page as Smart Hub | — |
| #340 | Implement session creation and resumption flow | #339 |
| #341 | Connect LiveSessionView to real agent chat SSE | #340 |
| #342 | Add activation checklist to private game detail | #339 |

- [ ] **#339: Wire private game detail as Smart Hub** (~1d)
  - Connect private game detail page with all sub-sections
  - Route navigation between game info, documents, sessions

- [ ] **#340: Session creation/resumption flow** (~1d, after #339)
  - Create new session from private game detail
  - Resume existing session with state recovery

- [ ] **#341: Connect LiveSessionView to agent chat SSE** (~1d, after #340)
  - Wire SSE streaming to live session view
  - Handle reconnection and error states

- [ ] **#342: Activation checklist on private game detail** (~0.5d, parallel with #340)
  - Show progress checklist: game created → PDF uploaded → indexed → agent ready

- [ ] **PR to `frontend-dev`**, close #339-#342

---

## Sprint 1C: Layout & Welcome (Week 2-3, parallel)

### Epic #316 — Layout Refactor: MiniNav Removal

**Branch:** from `frontend-dev` | **Effort:** ~2-3 days

| Issue | Title |
|-------|-------|
| #343 | Relocate Logo and NotificationCenter, delete deprecated Navbar components |
| #344 | Migrate MiniNav tabs to SidebarContextNav and remove MiniNav |
| #345 | Add DesktopBreadcrumb and SearchTrigger to TopNavbar |
| #346 | Remove GlobalSearch, consolidate to CommandPalette |

- [ ] **#343: Relocate Logo + NotificationCenter** (~0.5d)
- [ ] **#344: Migrate MiniNav → SidebarContextNav** (~1d, after #343)
- [ ] **#345: DesktopBreadcrumb + SearchTrigger** (~0.5d, parallel with #344)
- [ ] **#346: Remove GlobalSearch → CommandPalette** (~0.5d, after #344)
- [ ] **PR to `frontend-dev`**, close #343-#346

---

### Epic #317 — Welcome Page Redesign

**Branch:** from `frontend-dev` | **Effort:** ~2 days
**Note:** PR #243 already merged the landing page redesign. These issues handle cleanup.

| Issue | Title |
|-------|-------|
| #348 | Create WelcomeHero, HowItWorksSteps, SocialProofBar, WelcomeCTA components |
| #349 | Wire new landing page components and update SEO metadata |
| #350 | Remove old landing components and consolidate to 4-section design |

- [ ] **#348: Create new landing components** (~1d)
- [ ] **#349: Wire components + SEO** (~0.5d, after #348)
- [ ] **#350: Remove old landing components** (~0.5d, after #349)
- [ ] **PR to `frontend-dev`**, close #348-#350

---

## Sprint 1 Completion Gate (end Week 3)

- [x] **#279** merged (PR #355) — ✅ 2026-03-13
- [ ] **#284 Phase 2** complete (4 issues)
- [ ] **#315** complete (4 issues)
- [ ] **#316** complete (4 issues)
- [ ] **#317** complete (3 issues)
- [ ] All PRs merged to parent branches

---

## Sprint 2A: Admin Infra — Phase 2 (Week 3-4) — ✅ COMPLETE

### Branch: `feature/issue-124-admin-infra-ph2` from `frontend-dev` (current branch)

**Phase 1: Operations Console** — ✅ COMPLETE (PR #205, issues #127-#130 closed)

**Phase 2: Service Dashboard** — ✅ COMPLETE (issues #132-#136 closed 2026-03-13)

| Issue | Title | Status |
|-------|-------|--------|
| #132 | Enhanced ServiceHealthMatrix — auto-refresh, uptime %, trends | ✅ Closed |
| #133 | Service Restart UI — confirmation, cooldown, feedback | ✅ Closed |
| #134 | Grafana Dashboard Embed — iframe selector (14 dashboards) | ✅ Closed |
| #135 | DB Stats Overview — connection pool, top tables, vacuum | ✅ Closed |
| #136 | Service Dashboard tests — E2E for all Ph2 components | ✅ Closed |

- [x] All 5 issues implemented and closed
- [ ] **PR to `frontend-dev`** — pending merge of current branch

---

## Sprint 2B: Admin User Management (Week 3-5)

### Epic #310 — Admin User Invitations & Onboarding Flow

**Branch:** from `main-dev` | **Effort:** ~3 days

| Issue | Title | Area |
|-------|-------|------|
| #319 | Implement UserInvitation DDD entity and repositories | Backend |
| #320 | Create invitation command handlers and endpoints | Backend |
| #321 | Build accept-invite page with password setup | Frontend |
| #322 | Add admin invite UI modal and wiring | Frontend |

- [ ] **#319: UserInvitation entity** (~0.5d)
- [ ] **#320: Invitation commands + endpoints** (~1d, after #319)
- [ ] **#321: Accept-invite page** (~1d, after #320)
- [ ] **#322: Admin invite UI modal** (~0.5d, parallel with #321)
- [ ] **PR to `main-dev`**, close #319-#322

---

### Epic #311 — User Onboarding Wizard

**Branch:** from `main-dev` | **Effort:** ~2.5 days | **After:** #310

| Issue | Title | Area |
|-------|-------|------|
| #323 | Add onboarding flags to User entity and complete command | Backend |
| #324 | Implement onboarding wizard component with 3 steps | Frontend |
| #325 | Create onboarding middleware guard and page layout | Frontend |
| #326 | Build reminder banner for skipped wizard | Frontend |

- [ ] **#323: Onboarding flags backend** (~0.5d)
- [ ] **#324: Onboarding wizard component** (~1d, after #323)
- [ ] **#325: Middleware guard + layout** (~0.5d, after #324)
- [ ] **#326: Reminder banner** (~0.5d, parallel with #325)
- [ ] **PR to `main-dev`**, close #323-#326

---

## Sprint 2C: Admin Audit Log (Week 4-5, parallel)

### Epic #313 — Admin Audit Log & User Activity Tracking

**Branch:** from `main-dev` | **Effort:** ~3 days

| Issue | Title | Area |
|-------|-------|------|
| #331 | Create AuditLogEntry entity and repository with event handlers | Backend |
| #332 | Implement ChangeUserRoleCommand and audit event capture | Backend |
| #333 | Build audit log API endpoints with filtering and export | Backend |
| #334 | Create AuditLogTable and admin audit page | Frontend |
| #335 | Build InlineRoleSelect and UserActivityTimeline components | Frontend |

- [ ] **#331: AuditLogEntry entity** (~0.5d)
- [ ] **#332: ChangeUserRoleCommand + audit capture** (~0.5d, after #331)
- [ ] **#333: Audit log API endpoints** (~1d, after #331)
- [ ] **#334: AuditLogTable + admin page** (~0.5d, after #333)
- [ ] **#335: InlineRoleSelect + UserActivityTimeline** (~0.5d, parallel with #334)
- [ ] **PR to `main-dev`**, close #331-#335

---

## Sprint 2D: Game Night Phase 3 — Multi-Device (Week 4-5)

### Branch: `game-night-improvvisata` (continuing)

| Issue | Title | Depends On |
|-------|-------|------------|
| #298 | Invite & Join Session Flow | Phase 2 ✅ |
| #299 | SignalR Session Sub-Groups & Agent Toggle | Phase 2 ✅ |
| #300 | Score Proposal Flow | #299 |
| #301 | Multi-Device Session Frontend | #298, #299 |
| #302 | Session Multi-Device Tests | #301 |

- [ ] **#298: Invite & Join** (~1.5d) — shareable invite code, join page
- [ ] **#299: SignalR sub-groups** (~1.5d, parallel with #298)
- [ ] **#300: Score Proposal** (~1d, after #299)
- [ ] **#301: Multi-Device Frontend** (~1.5d, after #298 + #299)
- [ ] **#302: Multi-Device Tests** (~1d, after #301)
- [ ] Close #298-#302

---

## Sprint 2 Completion Gate (end Week 5)

- [x] **#124 Phase 2** complete (5 issues) — ✅ 2026-03-13
- [ ] **#310** complete (4 issues)
- [ ] **#311** complete (4 issues)
- [ ] **#313** complete (5 issues)
- [ ] **#284 Phase 3** complete (5 issues)
- [ ] All PRs merged

---

## Sprint 3A: Game Night Sprint 2 + Phase 4-5 (Week 5-7)

### Branch: `game-night-improvvisata` — Phase 4 & 5

| Issue | Title | Depends On |
|-------|-------|------------|
| #303 | Degraded Agent (BGG-only, Pre-PDF) | Phase 2 ✅ |
| #305 | Language Detection VO Extension & Python Integration | Phase 0 ✅ |
| #306 | Language Intelligence Frontend & Cross-Language RAG | #305 |

- [ ] **#303: Degraded Agent** (~1d, parallel)
- [ ] **#305: Language Detection VO** (~1.5d, parallel)
- [ ] **#306: Language Intelligence Frontend** (~1d, after #305)
- [ ] Close #303, #305, #306 — **Game Night Improvvisata COMPLETE** 🎉

### Branch: `feature/issue-235-game-night-s2` from `main-dev`

**Spec Phase** (parallel)

| Issue | Title |
|-------|-------|
| #211 | API contracts for Sprint 2 endpoints |
| #212 | Behavioral examples (Given/When/Then) |
| #213 | SSE operational requirements |

- [ ] **#211-#213: Specs** (~1.5d total, all parallel)

**Implementation Phase** (after specs)

| Issue | Title | Depends On |
|-------|-------|------------|
| #214 | QuickView dual-context resolution | #211, #212 |
| #215 | Responsive test matrix | #211 |
| #216 | Server-side timer | #213 |

- [ ] **#214: QuickView** (~2d)
- [ ] **#215: Responsive test matrix** (~1d, parallel with #214)
- [ ] **#216: Server-side timer** (~1.5d, parallel with #214)

**Quality Phase**

| Issue | Title | Depends On |
|-------|-------|------------|
| #217 | Accessibility test plan (WCAG 2.1 AA) | #214-#216 |
| #218 | Performance budget | #216 |

- [ ] **#217: Accessibility plan** (~1d)
- [ ] **#218: Performance budget** (~0.5d, parallel)
- [ ] **PR to `main-dev`**, close #211-#218

---

## Sprint 3B: Admin Content (Week 5-7, parallel)

### Epic #236 — Admin Shared Game Content

**Branch:** from `main-dev` | **Effort:** ~3-4 days

| Issue | Title | Type |
|-------|-------|------|
| #119 | Per-SharedGame document overview endpoint | Backend |
| #117 | Bulk PDF upload for shared games | Backend + AI |
| #118 | Guided wizard for catalog population | Frontend |
| #113 | MAU monitoring — ActiveAiUsers | Frontend + AI |

- [ ] **#119 → #117 → #118** (sequential)
- [ ] **#113** (independent, parallel)
- [ ] **PR to `main-dev`**, close #113, #117-#119

---

### Epic #259 — Admin RAG Dashboard

**Branch:** from `main-dev` | **Effort:** ~5-7 days | **Parallel with #236**

| Phase | Issues | Type |
|-------|--------|------|
| Backend | #260, #261, #262 | Parallel |
| API Client | #263 | After backend |
| Hooks | #264 | After #263 |
| Components | #265, #266, #267 | Parallel, after #264 |
| Dashboard | #268 | After all above |

- [ ] **#260-#262: Backend queries** (~2d, parallel)
- [ ] **#263: Zod schemas + API client** (~0.5d)
- [ ] **#264: React Query hooks** (~0.5d)
- [ ] **#265-#267: UI components** (~2d, parallel)
- [ ] **#268: Unified dashboard page** (~1d)
- [ ] **PR to `main-dev`**, close #260-#268

---

## Sprint 3C: Polish & Infra (Week 7-8, parallel)

### Epic #314 — Desktop UX Polish

**Branch:** from `frontend-dev` | **Effort:** ~2 days

| Issue | Title |
|-------|-------|
| #336 | Redesign GameBackContent with enriched header and contextual actions |
| #337 | Add sidebar left border indicator and tooltip styling |
| #338 | Implement View Transitions API hook and CSS animations |

- [ ] **#336-#338** (parallel, ~2d total)
- [ ] **PR to `frontend-dev`**, close #336-#338

---

### Epic #318 — Hybrid Layered Seeding (replaces #234)

**Branch:** from `main-dev` | **Effort:** ~3 days

| Issue | Title |
|-------|-------|
| #351 | Implement SeedOrchestrator with advisory lock and DI scopes |
| #352 | Create YAML-driven CatalogSeeder for shared games |
| #353 | Refactor existing seeders into composable layers (Core, Catalog, LivedIn) |
| #354 | Add dump/restore scripts for production-to-staging data migration |

- [ ] **#351: SeedOrchestrator** (~1d)
- [ ] **#352: YAML CatalogSeeder** (~0.5d, after #351)
- [ ] **#353: Refactor seeders** (~1d, after #351)
- [ ] **#354: Dump/restore scripts** (~0.5d, independent)
- [ ] **PR to `main-dev`**, close #351-#354, close #234 (superseded by #318)

---

### Admin Infra — Phase 3: Log Viewer + Docker (Week 7-8)

**Branch:** from `frontend-dev` | ⚠️ Security-sensitive

| Issue | Title | Depends On |
|-------|-------|------------|
| #137 | Docker Socket Proxy setup | — (infra) |
| #138 | IDockerProxyService backend | #137 |
| #139 | Container management API | #138 |
| #140 | Log Viewer page (HyperDX + audit) | Phase 2 ✅ |
| #141 | Container Logs UI | #139, #140 |
| #142 | Log Viewer + Container tests | #140, #141 |

- [ ] **#137 → #138 → #139** (sequential backend)
- [ ] **#140** (parallel with backend chain)
- [ ] **#141** (after #139 + #140)
- [ ] **#142** (tests, after #141)
- [ ] **PR to `frontend-dev`**, close #137-#142

---

## Sprint 3 Completion Gate (end Week 8)

- [ ] **#284** all phases complete (**Game Night Improvvisata DONE**)
- [ ] **#235** complete (8 issues)
- [ ] **#236** complete (4 issues)
- [ ] **#259** complete (9 issues)
- [ ] **#314** complete (3 issues)
- [ ] **#318** complete (4 issues)
- [ ] **#124 Phase 3** complete (6 issues)

---

## Sprint 4: Extended Features (Week 8-10)

### Admin Infra — Phase 4: Container Dashboard

| Issue | Title | Depends On |
|-------|-------|------------|
| #143 | Container Dashboard page | #139 |
| #144 | Container Management tests | #143 |
| #145 | Restart All (dependency-ordered) | #143 |

- [ ] **#143 → #144, #145** (~2d)
- [ ] Close #143-#145 — **Admin Infra Panel COMPLETE** 🎉

---

### Epic #312 — Voice-to-Text & TTS in Chat

**Branch:** from `main-dev` | **Effort:** ~4 days

| Issue | Title | Area |
|-------|-------|------|
| #327 | Implement Whisper transcription service with tier gating | Backend |
| #328 | Create WhisperProvider for cloud-based speech recognition | Frontend |
| #329 | Build VoiceChatButton and wire to chat input | Frontend |
| #330 | Add text-to-speech output with language auto-detection | Frontend |

- [ ] **#327: Whisper backend** (~1d)
- [ ] **#328: WhisperProvider frontend** (~1d, after #327)
- [ ] **#329: VoiceChatButton** (~1d, after #328)
- [ ] **#330: TTS output** (~1d, parallel with #329)
- [ ] **PR to `main-dev`**, close #327-#330

---

### RAG E2E Tests (#245/#251)

**Effort:** ~3-4 days | **After:** #236 + #259

| Issue | Title |
|-------|-------|
| #246 | Add Descent rulebook PDF to data/rulebook |
| #248 | Add Descent to YAML seeding manifests |
| #249 | E2E integration test: admin game → PDF → RAG flow |
| #252 | Add Descent rulebook PDF to test data |
| #253 | E2E test: Admin shared game + RAG full flow |
| #254 | Add BGG search autocomplete to New Game form |
| #255 | Cross-link New Game and Import Game flows |
| #256 | Deprecate RAG Wizard in favor of RagSetupClient |
| #257 | Validate Import Wizard E2E (BGG search flow) |

- [ ] **#246, #248, #252** (test data, parallel)
- [ ] **#249, #253** (E2E tests, after data)
- [ ] **#254, #255, #256, #257** (enhancements, parallel)
- [ ] Close all — **RAG E2E COMPLETE**

---

### Epic #280 — Game Host Session Experience

**Effort:** ~6-8 days | **After:** #284 Phase 3

| Issue | Title |
|-------|-------|
| #272 | Card/Deck Simulation Tool |
| #273 | Phase/Round Tracker Tool |
| #274 | Voice → Session Notes Integration |
| #275 | Voice → AI Agent Commands |
| #276 | Session Diary / Timeline |
| #277 | Turn Summary AI Feature |
| #278 | Session Checkpoint / Deep Save |

- [ ] **#272-#273** (toolkit, parallel)
- [ ] **#274-#275** (voice features, parallel, after #312 if possible)
- [ ] **#276-#278** (session features, after toolkit)
- [ ] Close all — **Game Host COMPLETE**

---

### Standalone Issues

- [ ] **#347: Entity links + RAG pipeline builder** (backend gaps, ~1d)
- [ ] **#269: Notification toast** (processing completion, ~0.5d)

---

## Low Priority (Deferred)

> **User decision:** Publisher Portal and AI Training are explicitly deferred to backlog. They remain tracked but will NOT be scheduled in the current 10-week plan.

### #6 — Publisher Portal & Test Campaigns (17 issues)

**Effort:** ~12-15 days | **Area:** full-stack | **Status:** 🔵 DEFERRED

Backend chain (12 sequential): #7 → #8 → #9 → #10 → #11 → #12 → #13 → #14 → #15 → #16 → #17 → #18
Frontend (3): #19 → #20 → #21
Tests (2): #22, #23

### #237 — Fine-Tuning Prerequisites (14 issues)

**Effort:** ~10-15 days (research/analysis) | **Area:** AI strategy | **Status:** 🔵 DEFERRED

Gate 1: Legal (#67, #70) | Gate 2: Economic (#69) | Gate 3: Evaluation (#68, #72, #71)
Gate 4: Infrastructure (#74, #75, #73) | Gate 5: Production (#76, #77, #78, #79) | Umbrella: #66

GO/NO-GO decision after Gates 1-3. If NO-GO, gates 4-5 are cancelled.

### #238 — Fact Extraction Pipeline (6 issues)

**Effort:** ~5-7 days | **Area:** AI/Python | **Status:** 🔵 BLOCKED by #237 GO decision

#81 → #82 → #83 → #84 → #85 → #86

### Other Backlog

| Issue | Title | Notes |
|-------|-------|-------|
| #24 | Fine-Tuned Rulebook Analysis Model | Requires #237 + #238 |
| #28 | Region-aware routing strategy interface | No deps |

---

## Git Branch Strategy

```
main
 └── main-dev
      ├── fix/issue-279-startup-bugs (PR #355) → PR to main-dev
      ├── game-night-improvvisata          → PR to main-dev (existing)
      │    (Phases 2-5 work here)
      ├── feature/issue-235-game-night-s2  → PR to main-dev
      ├── feature/issue-236-admin-shared   → PR to main-dev
      ├── feature/issue-259-rag-dashboard  → PR to main-dev
      ├── feature/issue-310-invitations    → PR to main-dev
      ├── feature/issue-311-onboarding     → PR to main-dev
      ├── feature/issue-313-audit-log      → PR to main-dev
      ├── feature/issue-312-voice-tts      → PR to main-dev
      ├── feature/issue-318-seeding        → PR to main-dev
      └── frontend-dev
           ├── feature/issue-124-admin-ph2 → PR to frontend-dev (current!)
           ├── feature/issue-124-admin-ph3 → PR to frontend-dev
           ├── feature/issue-124-admin-ph4 → PR to frontend-dev
           ├── feature/issue-315-gn-flow   → PR to frontend-dev
           ├── feature/issue-316-layout    → PR to frontend-dev
           ├── feature/issue-317-welcome   → PR to frontend-dev
           └── feature/issue-314-ux-polish → PR to frontend-dev
```

**Rule:** Always set parent: `git config branch.<name>.parent <parent-branch>`

---

## Risk Register

| Risk | Impact | Mitigation |
|------|--------|------------|
| ~~#279 blocks all dev~~ | ~~🔴 High~~ | ✅ RESOLVED — merged 2026-03-13 |
| #284 Phase 3 SignalR complexity | 🟡 Medium | Start with invite flow, incrementally add real-time |
| #124 Phase 3 Docker security | 🟡 Medium | Isolated Docker Socket Proxy, read-only by default |
| #316 Layout refactor breaks existing pages | 🟡 Medium | Thorough E2E tests before merge, parallel with cleanup |
| Branch conflicts between tracks | 🟡 Medium | Frequent rebases, small PRs, clear BC boundaries |
| Many new frontend epics overlap (#314-#317) | 🟡 Medium | Sequential merge order: #316 first (structural), then #317, then #314 |
| #312 Voice requires AI service integration | 🟢 Low | Tier-gated, can be feature-flagged |

---

## Parallelism Opportunities

| Week | Parallel Tracks | Total |
|------|----------------|-------|
| 1 | #279 + #284 Ph2 | 2 |
| 2-3 | #315 + #316 + #317 + #284 Ph2 finish | 4 |
| 3-4 | #124 Ph2 + #310 + #235 Specs | 3 |
| 4-5 | #284 Ph3 + #311 + #313 + #235 Impl | 4 |
| 5-7 | #284 Ph4-5 + #236 + #259 + #235 Quality | 4 |
| 7-8 | #314 + #318 + #124 Ph3 | 3 |
| 8-10 | #124 Ph4 + #312 + #245 E2E + #280 | 4 |

---

## Daily Standup Checklist

For each working day:
1. Which issue am I working on? (single issue focus)
2. Is the branch correct? (check parent)
3. Are tests passing before I start? (`dotnet test` / `pnpm test`)
4. Commit at each step completion
5. Update issue on GitHub when done (close + PR reference)

---

## Changelog

- **2026-03-13 (v4)**: Updated with latest closures: #279 CLOSED (Sprint 0 done), #132-#136 ALL CLOSED (Admin Infra Ph2 done), #247/#250 closed (RAG E2E partial), #171 CI/CD epic closed. Open count reduced from ~128 to ~122. Admin Infra reduced from 11 to 6 remaining (Ph3+Ph4 only).
- **2026-03-13 (v3)**: Major update — added 9 new epics (#310-#318) with 35 sub-issues. Reorganized into 5 sprints over 10 weeks. Publisher (#6, 17 issues) and AI Training (#237/#238, 20 issues) deferred to LOW priority per user request. Total open: ~128 issues across 22 tracks. Added parallelism table. Updated branch strategy with new feature branches.
- **2026-03-13 (v2)**: Closed #127-#130 (Phase 1 already done in PR #205). Reduced #124 from 15 to 11 remaining. Created PR #355 for #279.
- **2026-03-13 (v1)**: Initial roadmap. Purged closed epics. Added all existing epics. Created execution sequence.
- **2026-03-12**: Initial roadmap created from rdmap.md.
