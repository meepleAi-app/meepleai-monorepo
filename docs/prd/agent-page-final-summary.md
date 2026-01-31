# Agent Page - Final Summary & Complete Issue List

**Date**: 2026-01-31
**Status**: Complete - Ready for Implementation
**Total Issues**: 17 (12 frontend + 3 enhanced + 2 backend)

---

## 🎨 Design Evolution

### Iteration 1: Neon Brutalist Terminal ❌
- Industrial cyber-punk aesthetic
- Dark mode, neon glows, brutal typography
- **Rejected**: Incompatible with MeepleAI brand

### Iteration 2: MeepleAI Warm & Friendly ✅
- Warm beige background with wood texture
- Orange/purple brand colors
- Quicksand + Nunito fonts
- Soft shadows, glass morphism
- **Approved**: Aligned with existing design system

### Iteration 3: Enhanced v2 (Final) ✅
- **Added**: PDF viewer integration with citation links
- **Added**: Agent type switcher (change template during chat)
- **Added**: Settings drawer (runtime config changes)
- **Added**: Game binding (fixed, cannot change)

---

## 📋 Complete Issue List

### Frontend Issues (15 total)

#### Sprint 1: Configuration Flow (5 issues) - Week 1
| # | Title | Priority | Est | Status |
|---|-------|----------|-----|--------|
| [#3237](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3237) | Base Setup & Routing | P1 | 0.5d | Open |
| [#3238](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3238) | Config Sheet Container | P1 | 1d | Open |
| [#3239](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3239) | Game/Template/Model Selectors | P1 | 1.5d | Open |
| [#3240](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3240) | Token Quota & Slot Cards | P1 | 1d | Open |
| [#3241](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3241) | Contextual Action Bar | P1 | 1d | Open |

#### Sprint 2: Chat Interface (7 issues) - Week 2-3
| # | Title | Priority | Est | Status |
|---|-------|----------|-----|--------|
| [#3242](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3242) | Chat Sheet Container | P1 | 1d | Open |
| [#3243](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3243) | Message Components & SSE Streaming | P0 | 1.5d | Open |
| [#3244](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3244) | Citations & Confidence Display | P2 | 0.5d | Open |
| [#3245](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3245) | Chat Input & SSE Integration | P1 | 1d | Open |
| [#3249](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3249) | **Agent Type Switcher** 🆕 | P1 | 1d | Open |
| [#3250](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3250) | **Settings Drawer (Runtime Config)** 🆕 | P1 | 1.5d | Open |
| [#3251](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3251) | **PDF Viewer Integration** 🆕 | P0 | 1d | Open |

#### Sprint 3: Slot Management (2 issues) - Week 3
| # | Title | Priority | Est | Status |
|---|-------|----------|-----|--------|
| [#3246](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3246) | Slot Management Page | P2 | 1.5d | Open |
| [#3247](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3247) | Upgrade Flow & Premium CTA | P2 | 0.5d | Open |

#### Sprint 4: Testing (1 issue) - Week 4
| # | Title | Priority | Est | Status |
|---|-------|----------|-----|--------|
| [#3248](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3248) | E2E Tests & Responsive Validation | P0 | 2d | Open |

**Frontend Subtotal**: 15 issues, **16 days** (3.2 weeks for 1 dev)

---

### Backend Issues (2 new)

| # | Title | Priority | Est | Status |
|---|-------|----------|-----|--------|
| [#3252](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3252) | PATCH Endpoint - Update Typology | P1 | 1d | Open |
| [#3253](https://github.com/DegrassiAaron/meepleai-monorepo/issues/3253) | PATCH Endpoint - Update Runtime Config | P1 | 1.5d | Open |

**Backend Subtotal**: 2 issues, **2.5 days**

---

## 📐 Complete File Structure

```
apps/web/src/
├── app/(authenticated)/library/games/[gameId]/agent/
│   ├── page.tsx                        # Main agent page (config + chat)
│   ├── loading.tsx
│   └── slots/
│       └── page.tsx                    # Slot management
│
├── components/agent/
│   ├── config/
│   │   ├── AgentConfigSheet.tsx        # Config container
│   │   ├── GameSelector.tsx            # Game dropdown (with PDF filter)
│   │   ├── TemplateCarousel.tsx        # Horizontal template picker
│   │   ├── TemplateInfoModal.tsx       # Template details modal
│   │   ├── ModelSelector.tsx           # AI model dropdown (tier-filtered)
│   │   ├── TokenQuotaDisplay.tsx       # ✅ REUSE QuotaStatusBar.tsx
│   │   ├── SlotCards.tsx               # Gamified slot visualization
│   │   └── index.ts
│   │
│   ├── chat/
│   │   ├── AgentChatSheet.tsx          # Bottom sheet container
│   │   ├── ChatHeader.tsx              # 🆕 Header with game + type switcher + PDF/settings
│   │   ├── AgentTypeSwitcher.tsx       # 🆕 Dropdown to switch template (#3249)
│   │   ├── ChatMessageList.tsx         # Scrollable messages
│   │   ├── ChatMessage.tsx             # User/agent message bubbles
│   │   ├── CitationBadge.tsx           # 🆕 Clickable → PDF viewer (#3251)
│   │   ├── ConfidenceBar.tsx           # Visual confidence indicator
│   │   ├── TypingIndicator.tsx         # SSE streaming animation
│   │   ├── ChatInput.tsx               # Input + send + attachment
│   │   ├── AgentSettingsDrawer.tsx     # 🆕 Runtime config changes (#3250)
│   │   ├── PdfViewerIntegration.tsx    # 🆕 PDF modal wrapper (#3251)
│   │   └── index.ts
│   │
│   ├── slots/
│   │   ├── SlotManagementPage.tsx      # Full slot manager
│   │   ├── ActiveSlotCard.tsx          # ✅ Follow UserGameCard.tsx pattern
│   │   ├── LockedSlotCard.tsx          # Premium upgrade CTA
│   │   ├── SlotUsageStats.tsx          # Usage breakdown
│   │   └── index.ts
│   │
│   └── shared/
│       ├── ActionBar.tsx               # Contextual action bar (config/chat/slots states)
│       └── index.ts
│
├── hooks/agent/
│   ├── useAgentConfig.ts               # Config state management
│   ├── useAgentChat.ts                 # Chat SSE streaming
│   ├── useTokenQuota.ts                # Quota tracking
│   ├── useSlotManagement.ts            # Slot CRUD operations
│   ├── useAgentTypology.ts             # 🆕 Typology switching (#3249)
│   ├── useAgentSettings.ts             # 🆕 Runtime config updates (#3250)
│   ├── usePdfViewer.ts                 # 🆕 PDF viewer state (#3251)
│   └── index.ts
│
├── stores/
│   └── agentStore.ts                   # 🆕 Enhanced with PDF + settings state
│
├── lib/agent/
│   ├── api-client.ts                   # Agent API wrapper
│   ├── sse-handler.ts                  # SSE EventSource wrapper
│   ├── validation.ts                   # Config validation
│   └── types.ts                        # TypeScript interfaces
│
└── styles/
    └── (use existing globals.css + design-tokens.css)
```

---

## 🔗 Updated Dependencies Graph

```
FRONTEND TRACK:
#3237 (Base Setup)
  ├─→ #3238 (Config Sheet)
  │     ├─→ #3239 (Selectors)
  │     ├─→ #3240 (Quota & Slots) ─┐
  │     └─→ #3241 (Action Bar) ←───┤
  │                                 │
  └─→ #3242 (Chat Sheet) ───────────┤
        ├─→ #3243 (Messages) ───────┤
        │     ├─→ #3244 (Citations)─┼─→ #3251 (PDF Integration) 🆕
        │     └─→ #3245 (Input)     │
        │                            │
        ├─→ #3249 (Type Switcher) 🆕│ (requires #3252 backend)
        └─→ #3250 (Settings Drawer) 🆕 (requires #3253 backend)
                                     │
  #3240 ─→ #3246 (Slot Page) ────────┤
             └─→ #3247 (Upgrade)     │
                                     │
  All ──────────→ #3248 (E2E Tests) ←┘

BACKEND TRACK:
#3252 (PATCH typology) ──blocks──→ #3249 (Type Switcher)
#3253 (PATCH config) ────blocks──→ #3250 (Settings Drawer)
```

---

## 🎯 Critical Path (Longest Dependency Chain)

```
#3237 (0.5d)
  → #3238 (1d)
    → #3239 (1.5d)
      → #3242 (1d)
        → #3243 (1.5d)
          → #3244 (0.5d)
            → #3251 (1d) 🆕 PDF Integration
              → #3248 (2d) E2E Tests

Total Critical Path: 9.5 days
```

**Parallel Work Opportunities**:
- Week 2: #3249 (Type Switcher) + #3250 (Settings) + backend (#3252, #3253)
- Week 3: #3246 (Slots) + #3247 (Upgrade) while chat is being finalized

---

## 📦 Design Assets

### Mockups (HTML Interattivi)

1. **Base MeepleAI Style** ✅
   - File: `docs/mockups/agent-page-meepleai-style.html`
   - Content: Config + Chat + Slots screens
   - Features: Template carousel, quota display, slot cards

2. **Enhanced v2 (Final)** ✅
   - File: `docs/mockups/agent-page-enhanced-v2.html`
   - Content: Enhanced chat with settings drawer + PDF viewer
   - Features: Agent type switcher, settings panel, clickable citations, PDF modal

**View**:
```bash
# Enhanced mockup (recommended)
start docs/mockups/agent-page-enhanced-v2.html

# Base mockup (config/slots reference)
start docs/mockups/agent-page-meepleai-style.html
```

### Documentation

1. **Epic Document**: `docs/prd/agent-page-frontend-epic.md` (v1.1)
2. **Enhanced Requirements**: `docs/prd/agent-page-enhanced-requirements.md`
3. **Design Update Summary**: `docs/prd/DESIGN_UPDATE_SUMMARY.md`
4. **This Final Summary**: `docs/prd/agent-page-final-summary.md`

---

## 🆕 Enhanced Features Added

### 1. Agent Type Switcher (#3249)
**What**: Dropdown in chat header to switch from "Rules Expert" → "Strategy Coach"
**Why**: User needs different expertise during same game session
**How**: PATCH /agent/typology endpoint, preserves chat history

### 2. Settings Drawer (#3250)
**What**: Slide-in panel to change model, temperature, RAG strategy during chat
**Why**: User wants to tune agent behavior without restarting session
**How**: PATCH /agent/config endpoint, applies to next query

### 3. PDF Viewer Integration (#3251)
**What**: Clickable citations open PDF viewer at specific page
**Why**: User validates agent response with primary source (rulebook)
**How**: Reuse PdfViewerModal.tsx, pass initialPage from citation

### 4. Game Binding (Design Pattern)
**What**: Game is FIXED for agent session (cannot switch game)
**Why**: Agent trained on specific game rulebook, RAG context specific
**How**: Game badge with "🔒" icon + disabled state + tooltip

---

## 🔧 Backend Requirements (New)

### Endpoints to Create

**1. PATCH /game-sessions/{id}/agent/typology** (#3252)
```
Request: { "agentSessionId": "uuid", "newTypologyId": "uuid" }
Response: 204 No Content
Effect: Updates AgentSession.TypologyId, reloads prompt template
```

**2. PATCH /game-sessions/{id}/agent/config** (#3253)
```
Request: {
  "agentSessionId": "uuid",
  "modelType": "gpt-4o",
  "temperature": 0.7,
  "maxTokens": 2000,
  "ragStrategy": "HybridSearch",
  "ragParams": { "topK": 8, "minScore": 0.75 }
}
Response: 204 No Content
Effect: Updates session config, applies to next chat query
```

### Domain Methods to Add

```csharp
// AgentSession.cs
public void UpdateTypology(Guid newTypologyId) { ... }
public void UpdateConfig(AgentConfig newConfig) { ... }
```

---

## ✅ Components to Reuse (DO NOT RECREATE)

| Component | Location | Use For | Status |
|-----------|----------|---------|--------|
| **QuotaStatusBar.tsx** | `components/library/` | Token quota display (#3240) | ✅ Exists |
| **AgentConfigModal.tsx** | `components/library/` | Config pattern reference | ✅ Exists |
| **UserGameCard.tsx** | `components/library/` | Slot card pattern (#3246) | ✅ Exists |
| **PdfViewerModal.tsx** | `components/pdf/` | PDF viewer (#3251) | ✅ Exists |
| **PdfViewer.tsx** | `components/pdf-viewer/` | PDF rendering | ✅ Exists |
| **Button** | `components/ui/primitives/` | All buttons | ✅ Exists |
| **Sheet** | `components/ui/overlays/` | Drawers, bottom sheets | ✅ Exists |
| **Progress** | `components/ui/feedback/` | Progress bars | ✅ Exists |

---

## 🎯 Implementation Priority

### Week 1: Foundation
- Day 1: #3237 Base Setup
- Day 2: #3238 Config Sheet + #3239 Selectors
- Day 3: #3240 Quota/Slots + #3241 Action Bar

### Week 2: Chat Core (Frontend + Backend Parallel)
**Frontend**:
- Day 4: #3242 Chat Container
- Day 5-6: #3243 Messages + SSE Streaming ⚠️ Critical
- Day 6: #3244 Citations + #3245 Input

**Backend** (Parallel):
- Day 5-6: #3252 PATCH Typology + #3253 PATCH Config

### Week 3: Enhanced Features
- Day 7: #3249 Type Switcher (frontend) - depends on #3252 backend
- Day 8-9: #3250 Settings Drawer (frontend) - depends on #3253 backend
- Day 9: #3251 PDF Integration
- Day 10-11: #3246 Slots + #3247 Upgrade

### Week 4: Testing & Polish
- Day 12-13: #3248 E2E Tests
- Day 14: Bug fixes, deployment prep

**Total**: 14 days (≈3 weeks) - 1 Frontend + 1 Backend dev in parallel

---

## 🚀 Next Actions

### Immediate (Before Starting Implementation)
- [ ] Review both mockups in browser:
  - `docs/mockups/agent-page-meepleai-style.html` (base)
  - `docs/mockups/agent-page-enhanced-v2.html` (enhanced) ⭐
- [ ] Validate PdfViewerModal works with test PDF
- [ ] Verify backend API endpoints exist:
  - ✅ GET `/admin/shared-games/{id}/documents`
  - ❌ PATCH `/agent/typology` (create #3252)
  - ❌ PATCH `/agent/config` (create #3253)

### Week 1 Sprint Planning
- [ ] Assign #3237 to frontend dev
- [ ] Setup Storybook for agent components (optional)
- [ ] Create feature flag: `FEATURE_AGENT_PAGE=true`
- [ ] Setup test environment with mock PDFs

### Coordination
- [ ] Frontend dev reviews all 15 frontend issues
- [ ] Backend dev takes #3252 + #3253 (Week 2)
- [ ] QA plans E2E test scenarios (#3248)
- [ ] Design validates mockups before implementation

---

## 📊 Success Metrics (Updated)

### User Experience
- Config flow completion >80%
- Chat engagement >3 messages/session
- **Citation click rate >40%** (validates PDF integration value)
- **Settings usage >20%** (power users tune agent)

### Technical Performance
- Page load (TTI) <3s
- SSE connection success >95%
- **PDF load time <2s** (critical for citation UX)
- Settings drawer slide-in <300ms

### Quality
- Test coverage >85% (unit + integration)
- E2E pass rate 100%
- Accessibility score >95 (axe-core)
- **PDF viewer keyboard navigation works** (accessibility)

---

## 📝 All Issues Created

**View all agent issues**:
```bash
gh issue list --label "area/agent" --state open
```

**Direct links**:
- Frontend: #3237-#3251 (15 issues)
- Backend: #3252-#3253 (2 issues)

**Total**: 17 issues, ~18.5 days effort (frontend 16d + backend 2.5d in parallel = ~3.5 weeks calendar time)

---

## ✅ Completato

- [x] Design concept aggiornato (Neon → MeepleAI style)
- [x] Mockup HTML creato (base + enhanced)
- [x] 12 issue frontend originali create
- [x] 3 issue frontend enhanced create
- [x] 2 issue backend create
- [x] 12 issue originali aggiornate con design comment
- [x] Documentazione completa (epic, requirements, summary)
- [x] Componenti esistenti identificati per reuse
- [x] Backend API gap analysis completata

**Status**: Pronto per implementation sprint! 🚀
