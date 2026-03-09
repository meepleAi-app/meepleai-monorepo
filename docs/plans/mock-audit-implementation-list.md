# Mock/Fake Audit - Implementation List

**Date**: 2026-03-06
**Scope**: Full codebase scan (frontend + backend)

---

## Priority Legend

- **P0 - Critical**: Core feature broken/non-functional, user-facing
- **P1 - High**: Important feature stub, blocks functionality
- **P2 - Medium**: Missing integration, degraded UX
- **P3 - Low**: Nice-to-have, polish items

---

## BACKEND (7 actionable items)

### BE-01 [P0] Trending Games - Mock Data
- **File**: `apps/api/.../SharedGameCatalog/Application/Handlers/GetTrendingGamesQueryHandler.cs:14-26`
- **Problem**: Returns hardcoded list (Wingspan, Terraforming Mars, etc.) with fake scores
- **Implementation**: Create `GameTrendingScores` table, background job to compute trending based on play count/recency, real query

### BE-02 [P0] Session Agent Chat - Placeholder Response
- **File**: `apps/api/.../KnowledgeBase/Application/Handlers/ChatWithSessionAgentCommandHandler.cs:147-162`
- **Problem**: Returns static string "Session agent ready. Full LLM streaming coming in AGT-011" instead of LLM response
- **Implementation**: Integrate HybridLlmService, build proper prompt with game context, implement real streaming

### BE-03 [P0] Session Agent RAG - Stub Response
- **File**: `apps/api/.../SessionTracking/Application/Handlers/ChatCommandHandlers.cs:134-139`
- **Problem**: Returns `[Stub] RAG agent integration pending` instead of real answer
- **Implementation**: Call KnowledgeBase RAG pipeline (vector search + LLM), return real answer with confidence score (Issue #4761)

### BE-04 [P1] Admin Strategy Endpoints - All Placeholder
- **File**: `apps/api/src/Api/Routing/AdminStrategyEndpoints.cs:14-35`
- **Problem**: All 5 CRUD endpoints return empty arrays or echo back IDs
- **Implementation**: Create CQRS handlers, domain entities, repository, validation (Issue #3850)

### BE-05 [P2] System Health Report - Hardcoded Zeros
- **File**: `apps/api/.../Administration/Infrastructure/Services/ReportGeneratorService.SystemHealth.cs:74-75`
- **Problem**: `errorRate = 0.0`, `responseTime = 0.0` always
- **Implementation**: Calculate from LlmRequestLog or Prometheus metrics

### BE-06 [P2] Invite QR Code - SVG Placeholder
- **File**: `apps/api/.../SessionTracking/Application/Handlers/InviteHandlers.cs:152-156`
- **Problem**: Generates placeholder SVG instead of real QR code
- **Implementation**: Add QRCoder NuGet package, encode actual invite URL

### BE-07 [P3] Free Quota Daily Limit - Hardcoded
- **File**: `apps/api/.../KnowledgeBase/Application/Handlers/GetUsageFreeQuotaQueryHandler.cs:20`
- **Problem**: `DefaultDailyLimit = 1000` hardcoded constant
- **Implementation**: Move to appsettings.json or SystemConfiguration table, make configurable per tier

---

## FRONTEND - API/Data Mocks (13 items)

### FE-01 [P0] BGG API - Complete Mock
- **File**: `apps/web/src/lib/api/bgg.ts:10-146`
- **Problem**: Entire module uses `MOCK_GAMES[]` array (5 games) with simulated delays
- **Implementation**: Call real BGG XML API (`/xmlapi2/search`, `/xmlapi2/thing`), parse XML responses, handle rate limits

### FE-02 [P1] RAG Config Store - Fake Save/Load
- **File**: `apps/web/src/stores/ragConfigStore.ts:218,242`
- **Problem**: `saveConfig()` and `loadUserConfig()` use setTimeout instead of API calls
- **Implementation**: Wire to `PUT/GET /api/v1/rag/config` endpoints

### FE-03 [P1] Pipeline Builder Store - Fake Save
- **File**: `apps/web/src/stores/pipelineBuilderStore.ts:107-114`
- **Problem**: `savePipeline()` uses setTimeout, real fetch is commented out
- **Implementation**: Wire to `POST /api/v1/rag/pipelines` endpoint

### FE-04 [P1] Security Page - 2FA Non-Functional
- **File**: `apps/web/src/app/(authenticated)/settings/security/page.tsx:28-157`
- **Problems**:
  - 2FA status hardcoded `useState(false)`
  - Recovery codes hardcoded array
  - QR code shows "QR Code Here" text
  - Download codes not implemented
- **Implementation**: Fetch 2FA status from API, generate real codes on enable, use `qrcode.react` library

### FE-05 [P1] Achievements Page - Mock Data
- **File**: `apps/web/src/app/(authenticated)/profile/achievements/page.tsx:15-56`
- **Problem**: `mockAchievements[]` array with 3 hardcoded achievements
- **Implementation**: Create `useAchievements()` hook, fetch from `/api/v1/achievements`

### FE-06 [P2] Contact Form - No API Call
- **File**: `apps/web/src/app/(public)/contact/page.tsx:61`
- **Problem**: `await new Promise(resolve => setTimeout(resolve, 1500))` instead of API
- **Implementation**: Call `POST /api/v1/contact` to send email via backend

### FE-07 [P2] Settings Page - Privacy + Avatar
- **File**: `apps/web/src/app/(public)/settings/page.tsx:395,770`
- **Problems**:
  - Privacy settings show success toast but don't call API
  - Avatar upload is optimistic-only (no backend persistence)
- **Implementation**: Wire to privacy and avatar upload endpoints when available

### FE-08 [P2] Collection Search - Frontend Only
- **File**: `apps/web/src/app/(authenticated)/library/CollectionPageClient.tsx:142`
- **Problem**: `handleSearchChange()` only sets local state, filters client-side
- **Implementation**: Pass `searchQuery` to `useLibrary(filters)` for server-side search

### FE-09 [P2] KB Tab - Hardcoded PDF State
- **File**: `apps/web/src/components/shared-games/KnowledgeBaseTab.tsx:175`
- **Problem**: `const pdfState: PdfState = 'ready'` always
- **Implementation**: Fetch actual PDF indexing state from document status API

### FE-10 [P2] Library Game Card - Missing Stats
- **File**: `apps/web/src/components/library/MeepleLibraryGameCard.tsx:290-341`
- **Problem**: `formatPlayCount(0)`, `formatWinRate(null)`, `timesPlayed: 0` hardcoded
- **Implementation**: Add playCount/winRate to library API response, wire to card

### FE-11 [P2] Collection Dashboard - State Counts Zero
- **File**: `apps/web/src/components/collection/CollectionDashboard.tsx:629-631`
- **Problem**: `nuovo: 0, inPrestito: 0, wishlist: 0` with TODO comments
- **Implementation**: Extend library stats API to include state breakdown

### FE-12 [P2] SignalR Game State - Not Connected
- **File**: `apps/web/src/hooks/useGameStateSignalR.ts:41,47`
- **Problem**: Hub URL and auth token are TODO placeholders
- **Implementation**: Configure SignalR hub URL from env, get auth token from session

### FE-13 [P3] Impersonation Store - Cookie Security
- **File**: `apps/web/src/store/impersonation/store.ts:101`
- **Problem**: Token in localStorage instead of HttpOnly cookie
- **Implementation**: Migrate to backend Set-Cookie header for proper security

---

## FRONTEND - Unimplemented UI Features (12 items)

### FE-UI-01 [P1] Entity Actions - 5 Stubs
- **File**: `apps/web/src/hooks/use-entity-actions.ts:241-409`
- **Stubs**: Copy session code, trigger download, export chat, open invite modal, RSVP event
- **Implementation**: Implement each action with real clipboard/download/API calls

### FE-UI-02 [P1] Agent Config Modal - Test Agent
- **File**: `apps/web/src/components/library/AgentConfigModal.tsx:146`
- **Problem**: "Test agent" button shows info toast only
- **Implementation**: Call test endpoint when backend provides it

### FE-UI-03 [P1] Agent Info Card - Chat History
- **File**: `apps/web/src/components/agent/AgentInfoCard.tsx:297`
- **Problem**: Chat history section is placeholder
- **Implementation**: Fetch and display real chat history list

### FE-UI-04 [P2] Categories Admin Table - No CRUD
- **File**: `apps/web/src/components/admin/shared-games/categories-table.tsx:30-40`
- **Problem**: Edit, delete, add category all unimplemented (3 TODOs)
- **Implementation**: Add dialogs with API calls for category management

### FE-UI-05 [P2] Session Form - Missing Features
- **File**: `apps/web/src/components/play-records/SessionCreateForm.tsx:304,415`
- **Problems**: Can't load user groups, no dynamic dimension inputs
- **Implementation**: Fetch groups from API, add key-value input for dimensions

### FE-UI-06 [P2] Player Manager - No Autocomplete
- **File**: `apps/web/src/components/play-records/PlayerManager.tsx:194`
- **Problem**: Manual player name entry, no search/autocomplete
- **Implementation**: Add user search with combobox component

### FE-UI-07 [P2] Agent Playground - RAG Filter
- **File**: `apps/web/src/app/admin/(dashboard)/agents/definitions/playground/page.tsx:143,358`
- **Problem**: Can't filter games by RAG-readiness
- **Implementation**: Add `documentCount` to SharedGame API, filter on frontend

### FE-UI-08 [P2] Agent Message - PDF Viewer
- **File**: `apps/web/src/components/agent/AgentMessage.tsx:124`
- **Problem**: PDF references can't be viewed in-app (Issue #4130)
- **Implementation**: Integrate PDF viewer component for source citations

### FE-UI-09 [P2] Shared Games Import - Replace Logic
- **File**: `apps/web/src/app/admin/(dashboard)/shared-games/import/steps/Step4EnrichAndConfirm.tsx:366`
- **Problem**: Can't replace existing games during import (Issue #4167)
- **Implementation**: Add replace confirmation dialog + API call

### FE-UI-10 [P2] Locked Slot - Waitlist
- **File**: `apps/web/src/components/agent/slots/LockedSlotCard.tsx:194`
- **Problem**: Waitlist signup button does nothing
- **Implementation**: Create waitlist API endpoint, wire up signup flow

### FE-UI-11 [P3] Game State Store - Template Management
- **File**: `apps/web/src/lib/stores/game-state-store.ts:184`
- **Problem**: Template not exposed via state endpoint
- **Implementation**: Backend should return template with state response

### FE-UI-12 [P3] Admin Hub Pages - Content Migration
- **Files**: `apps/web/src/app/admin/(dashboard)/{ai,analytics,config,monitor}/page.tsx`
- **Problem**: 4 stub pages with placeholder content
- **Implementation**: Migrate full content with tab-based layout + ActionBar (Issues #505x)

---

## Summary

| Category | P0 | P1 | P2 | P3 | Total |
|----------|----|----|----|----|-------|
| Backend  | 3  | 1  | 2  | 1  | 7     |
| FE Data  | 1  | 4  | 6  | 2  | 13    |
| FE UI    | 0  | 3  | 7  | 2  | 12    |
| **Total**| **4** | **8** | **15** | **5** | **32** |

### Recommended Implementation Order

**Phase 1 - Core Features** (P0):
1. BE-02 + BE-03: Session Agent real LLM/RAG integration
2. FE-01: BGG API real integration
3. BE-01: Trending games real data

**Phase 2 - Important Features** (P1):
4. FE-04: 2FA security page
5. FE-02 + FE-03: RAG config/pipeline persistence
6. BE-04: Admin strategy CRUD
7. FE-05: Achievements real data
8. FE-UI-01: Entity quick actions

**Phase 3 - Polish** (P2+P3):
9. Remaining items by area
