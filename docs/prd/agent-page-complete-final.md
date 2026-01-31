# Agent Page - Complete Final Specification

**Date**: 2026-01-31
**Version**: 2.0 (Split View Edition)
**Status**: ✅ Complete - Ready for Implementation
**Total Issues**: 18 (16 frontend + 2 backend)

---

## 🎨 Design Mockups (3 Total)

### 1. Base Config + Slots (Mobile-First)
**File**: `docs/mockups/agent-page-meepleai-style.html`
```bash
start docs/mockups/agent-page-meepleai-style.html
```
**Content**:
- Agent configuration screen
- Chat interface (bottom sheet)
- Slot management

### 2. Enhanced Chat (Settings + PDF Modal)
**File**: `docs/mockups/agent-page-enhanced-v2.html`
```bash
start docs/mockups/agent-page-enhanced-v2.html
```
**Content**:
- Agent type switcher dropdown
- Settings drawer (slide-in from right)
- PDF viewer modal
- Clickable citations

### 3. Split View Desktop/Tablet ⭐ **NEW**
**File**: `docs/mockups/agent-split-view-desktop.html`
```bash
start docs/mockups/agent-split-view-desktop.html
```
**Content**:
- Vertical split: Chat (left) + PDF (right)
- Draggable resize handle
- Multi-PDF tabs selector
- Real-time citation → PDF sync
- Mobile toggle view

---

## 📊 Complete Issue List (18 Total)

### Frontend Issues (16)

#### Sprint 1: Configuration Flow (5 issues)
| # | Title | Priority | Estimate |
|---|-------|----------|----------|
| #3237 | Base Setup & Routing | P1 High | 0.5d |
| #3238 | Config Sheet Container | P1 High | 1d |
| #3239 | Game/Template/Model Selectors | P1 High | 1.5d |
| #3240 | Token Quota & Slot Cards | P1 High | 1d |
| #3241 | Contextual Action Bar | P1 High | 1d |

**Sprint 1 Total**: 5.5 days

#### Sprint 2: Chat Core + Enhanced (7 issues)
| # | Title | Priority | Estimate |
|---|-------|----------|----------|
| #3242 | Chat Sheet Container | P1 High | 1d |
| #3243 | Message Components & SSE Streaming | P0 Critical | 1.5d |
| #3244 | Citations & Confidence Display | P2 Medium | 0.5d |
| #3245 | Chat Input & SSE Integration | P1 High | 1d |
| #3249 | Agent Type Switcher 🆕 | P1 High | 1d |
| #3250 | Settings Drawer (Runtime Config) 🆕 | P1 High | 1.5d |
| #3251 | PDF Viewer Integration & Citations 🆕 | P0 Critical | 1d |

**Sprint 2 Total**: 7.5 days

#### Sprint 3: Advanced Features (3 issues)
| # | Title | Priority | Estimate |
|---|-------|----------|----------|
| #3246 | Slot Management Page | P2 Medium | 1.5d |
| #3247 | Upgrade Flow & Premium CTA | P2 Medium | 0.5d |
| #3254 | **Split View Layout (Desktop)** 🆕 | P2 Medium | 1.5d |

**Sprint 3 Total**: 3.5 days

#### Sprint 4: Testing (1 issue)
| # | Title | Priority | Estimate |
|---|-------|----------|----------|
| #3248 | E2E Tests & Responsive Validation | P0 Critical | 2d |

**Sprint 4 Total**: 2 days

**Frontend Grand Total**: 16 issues, **18.5 days** (~3.7 weeks for 1 frontend dev)

---

### Backend Issues (2)

| # | Title | Priority | Estimate |
|---|-------|----------|----------|
| #3252 | PATCH Endpoint - Update Agent Typology | P1 High | 1d |
| #3253 | PATCH Endpoint - Update Runtime Config | P1 High | 1.5d |

**Backend Total**: 2 issues, **2.5 days**

---

## 🏗️ Architecture Overview

### Responsive Layout Strategy

```
MOBILE (< 768px):
┌─────────────────┐
│ CHAT VIEW       │ ← Default view
│ [Messages...]   │
│ [Input]         │
└─────────────────┘
      ↕️ Toggle
┌─────────────────┐
│ PDF VIEW        │
│ [PDF Page]      │
│ [Controls]      │
└─────────────────┘

TABLET (768px - 1024px):
┌──────────┬──────────┐
│  CHAT    │   PDF    │
│  (50%)   │   (50%)  │
│          │  [Tabs]  │
│          │  [Page]  │
└──────────┴──────────┘
     ↔️ Resize Handle

DESKTOP (> 1024px):
┌────────────┬─────────┐
│   CHAT     │   PDF   │
│   (55%)    │  (45%)  │
│            │ [Tabs]  │
│ [Citation] ├─────────┤
│     └──────→ Page 5  │ ← Citation link highlight
│            │         │
└────────────┴─────────┘
     ↔️ Draggable (40-70%)

LARGE DESKTOP (> 1440px):
┌───────────┬──────────┐
│   CHAT    │   PDF    │
│   (50%)   │   (50%)  │
│           │  [Tabs]  │
│           │  Zoom+   │
└───────────┴──────────┘
```

### Component Reuse Matrix

| Component | Source | Use In | Modification |
|-----------|--------|--------|--------------|
| **QuotaStatusBar** | `components/library/` | Token quota (#3240) | Extend with timer |
| **AgentConfigModal** | `components/library/` | Config pattern | Reference only |
| **UserGameCard** | `components/library/` | Slot cards (#3246) | Follow pattern |
| **PdfViewerModal** | `components/pdf/` | PDF modal (#3251) | Reuse as-is |
| **PdfViewer** | `components/pdf-viewer/` | Split view (#3254) | Inline mode |
| **Button** | `components/ui/primitives/` | All buttons | shadcn variant |
| **Sheet** | `components/ui/overlays/` | Chat/settings drawers | shadcn |
| **Dialog** | `components/ui/overlays/` | Modals | shadcn |

---

## 🔗 Enhanced Dependencies Graph

```
FRONTEND TRACK:
#3237 (Base) ──┐
               ├──→ #3238 (Config Sheet) ──┐
               │                           ├──→ #3239 (Selectors)
               │                           ├──→ #3240 (Quota)
               │                           └──→ #3241 (Action Bar)
               │
               └──→ #3242 (Chat Sheet) ─────┐
                         │                  ├──→ #3243 (Messages/SSE) ⚠️
                         │                  ├──→ #3244 (Citations)
                         │                  └──→ #3245 (Input)
                         │                           │
                         ├──→ #3249 (Type Switch) ◄──┼── #3252 (Backend)
                         ├──→ #3250 (Settings) ◄─────┼── #3253 (Backend)
                         └──→ #3251 (PDF Modal) ⚠️   │
                                   │                 │
                                   └──→ #3254 (Split View) 🆕
                                              │
#3240 ──→ #3246 (Slots) ──→ #3247 (Upgrade)  │
                                              │
ALL ────────────────────────────────→ #3248 (E2E Tests) ⚠️

BACKEND TRACK (Parallel Week 2):
#3252 (PATCH Typology) ──blocks──→ #3249
#3253 (PATCH Config) ────blocks──→ #3250

LEGEND:
⚠️ = Critical path
🆕 = New in v2.0
◄── = Requires backend
```

---

## 🎯 Critical Path Analysis

**Longest dependency chain**:
```
#3237 (0.5d) → #3238 (1d) → #3239 (1.5d) → #3242 (1d) →
#3243 (1.5d) → #3244 (0.5d) → #3251 (1d) → #3254 (1.5d) →
#3248 (2d)

Total: 10.5 days on critical path
```

**Parallel work opportunities**:
- Week 2: #3249 + #3250 (frontend) parallel to #3252 + #3253 (backend)
- Week 3: #3246 + #3247 (slots) parallel to #3254 (split view)

**Estimated calendar time**:
- Sequential: 21 days (4.2 weeks)
- With 1 frontend + 1 backend parallel: **3.5 weeks**

---

## 📦 New Components Created (v2.0)

### Split View Components (New)
```
apps/web/src/components/agent/split-view/
├── SplitViewLayout.tsx          # Container: chat + PDF + resize
├── PdfPreviewPanel.tsx          # Right panel with multi-PDF tabs
├── PdfTabSelector.tsx           # Tab bar for multiple PDFs
├── ResizeHandle.tsx             # Draggable divider
└── index.ts
```

### Enhanced Chat Components (Updates)
```
apps/web/src/components/agent/chat/
├── ChatHeader.tsx               # ✨ Enhanced: game + type switcher + PDF btn
├── AgentTypeSwitcher.tsx        # 🆕 Dropdown to switch template
├── AgentSettingsDrawer.tsx      # 🆕 Runtime config changes
├── CitationBadge.tsx            # ✨ Enhanced: clickable → PDF viewer
├── PdfViewerIntegration.tsx     # 🆕 PDF modal wrapper
└── ... (existing: messages, input, etc.)
```

---

## 🔌 API Endpoints Status

### ✅ Existing (Backend Ready)
- GET `/admin/agent-typologies?status=Approved` - Available templates
- GET `/agents/models?tier={tier}` - AI models
- GET `/users/me/token-quota` - Token usage
- GET `/agents/slots` - Active slots
- POST `/game-sessions/{id}/agent/launch` - Launch session
- POST `/game-sessions/{id}/agent/chat` - Chat SSE stream
- DELETE `/game-sessions/{id}/agent` - End session
- GET `/admin/shared-games/{id}/documents` - Game PDFs ✅

### ❌ Missing (Need Backend Issues)
- PATCH `/game-sessions/{id}/agent/typology` - Switch agent type (#3252)
- PATCH `/game-sessions/{id}/agent/config` - Update runtime config (#3253)

---

## 🎨 Design System Compliance

### MeepleAI Brand Colors
```css
--primary: hsl(25, 95%, 38%);      /* Orange #d2691e */
--accent: hsl(271, 91%, 55%);      /* Purple #8b5cf6 */
--secondary: hsl(142, 76%, 28%);   /* Green */
--background: hsl(30, 25%, 97%);   /* Warm beige */
```

### Typography
- Headings: **Quicksand** (400, 500, 600, 700)
- Body: **Nunito** (300, 400, 600, 700)

### Effects
- Soft shadows (no neon glows)
- Glass morphism (desktop only)
- Wood/paper texture background
- Subtle animations (fadeIn, slideUp, pulseMeeple)

---

## 📱 User Flows (Complete)

### Flow 1: First-Time Config + Launch
1. User clicks "Ask Agent" on game card
2. Config sheet opens (bottom sheet mobile, modal desktop)
3. Select: Game (Chess) + Template (Rules Expert) + Model (GPT-4o-mini)
4. View quota: 450/500 tokens, warning shown
5. View slots: 2/5 active
6. Click "Launch 🚀"
7. **Mobile**: Chat opens (bottom sheet)
8. **Desktop**: Split view opens (chat left + PDF right)

### Flow 2: Chat with PDF Citations
1. User asks: "Come si muovono i pedoni?"
2. Agent responds with SSE streaming
3. Message shows: Confidence 95%, Citation "Regolamento p.5"
4. User clicks citation badge
5. **Mobile**: PDF modal opens full-screen at page 5
6. **Desktop**: Right panel PDF jumps to page 5 (highlight section)
7. User reads rule, closes PDF (mobile) or keeps open (desktop)
8. User continues chat with PDF visible (desktop only)

### Flow 3: Switch Agent Type During Chat
1. User in chat with "Rules Expert"
2. Clicks agent type dropdown in header
3. Selects "Strategy Coach"
4. Confirmation toast: "Tipo agente cambiato!"
5. Chat history preserved
6. Next query uses Strategy Coach prompt template

### Flow 4: Adjust Settings Runtime
1. User clicks "⚙️ Impostazioni" in action bar
2. Settings drawer slides in from right
3. Changes: GPT-4o-mini → Claude-3.5-Haiku
4. Adjusts temperature: 0.7 → 1.2
5. Changes strategy: Hybrid → Vector Only
6. Clicks "Applica Modifiche"
7. Toast: "Configurazione aggiornata!"
8. Next query uses new config (model + temp + strategy)

### Flow 5: Multi-PDF Navigation (Desktop)
1. User in split view (chat + PDF visible)
2. PDF panel shows 3 tabs: Regolamento, Guida Rapida, Varianti
3. Currently viewing "Regolamento"
4. User clicks "Guida Rapida" tab
5. PDF switches to Quick Start guide (8 pages)
6. Page counter updates: "Pagina 1 di 8"
7. User navigates with arrows or clicks page nav
8. Switch back to "Regolamento" → returns to last viewed page

---

## 🏆 Key Features Summary

### ✅ Implemented in Design

1. **Mobile-First Responsive**
   - Bottom sheet pattern (mobile)
   - Split view (tablet/desktop)
   - Toggle buttons (mobile)
   - Draggable resize (desktop)

2. **Dynamic Agent Configuration**
   - Switch agent type during chat (#3249)
   - Runtime config changes (#3250)
   - Game binding (fixed, cannot change)
   - Settings drawer with advanced options

3. **PDF Integration**
   - Modal viewer (mobile) (#3251)
   - Split view inline (desktop) (#3254)
   - Multi-PDF tabs support
   - Citation → PDF page linking
   - Keyboard navigation

4. **Gamified UX**
   - Slot cards visualization
   - Token quota warnings (color-coded)
   - Progress bars with gradients
   - Upgrade CTAs (premium tier)

5. **Real-Time Features**
   - SSE streaming messages
   - Progressive text reveal
   - Typing indicator
   - Auto-scroll to latest

---

## 📐 Component Architecture

### Top-Level Routes
```
apps/web/src/app/(authenticated)/library/games/[gameId]/agent/
├── page.tsx                     # Main agent page
│   ├─ Mobile: Bottom sheet config → bottom sheet chat
│   └─ Desktop: Modal config → split view (chat + PDF)
│
└── slots/
    └── page.tsx                 # Slot management page
```

### Component Tree
```
<AgentPage>
  ├─ Mobile (< 768px)
  │  ├─ <AgentConfigSheet>       # Bottom sheet
  │  ├─ <AgentChatSheet>         # Bottom sheet
  │  ├─ <PdfViewerModal>         # Full-screen modal
  │  └─ <MobileToggleButtons>    # Chat ↔ PDF
  │
  └─ Desktop (>= 768px)
     ├─ <SplitViewLayout>
     │  ├─ <ChatPanel>           # Left 50-60%
     │  │  ├─ <ChatHeader>       # With type switcher + actions
     │  │  ├─ <ChatMessageList>
     │  │  └─ <ChatInput>
     │  │
     │  ├─ <ResizeHandle>        # Draggable divider
     │  │
     │  └─ <PdfPreviewPanel>     # Right 40-50%
     │     ├─ <PdfTabSelector>   # Multi-PDF tabs
     │     └─ <PdfViewer>        # Inline viewer
     │
     ├─ <AgentSettingsDrawer>    # Slide from right
     └─ <PdfViewerModal>         # Optional full-screen
```

---

## 🧪 Testing Strategy (Updated)

### E2E Scenarios (6 total - #3248 updated)

1. **E2E-FRONT-001**: Config flow → Launch
2. **E2E-FRONT-002**: Chat SSE streaming
3. **E2E-FRONT-003**: Slot management
4. **E2E-FRONT-004**: Token quota warning
5. **E2E-FRONT-005**: Responsive (mobile/tablet/desktop)
6. **E2E-FRONT-006**: 🆕 Citation → PDF sync (click citation, verify PDF jumps)
7. **E2E-FRONT-007**: 🆕 Type switching (change template, verify new prompts)
8. **E2E-FRONT-008**: 🆕 Split view resize (drag handle, verify persists)

### Unit Tests (Enhanced - 14 files)
```
__tests__/agent/unit/
├── AgentConfigSheet.test.tsx
├── GameSelector.test.tsx
├── TemplateCarousel.test.tsx
├── ModelSelector.test.tsx
├── TokenQuotaDisplay.test.tsx
├── SlotCards.test.tsx
├── ChatMessage.test.tsx
├── ChatInput.test.tsx
├── ConfidenceBar.test.tsx
├── CitationBadge.test.tsx          # ✨ Enhanced: test click handler
├── ActionBar.test.tsx
├── AgentTypeSwitcher.test.tsx      # 🆕 New
├── AgentSettingsDrawer.test.tsx    # 🆕 New
└── SplitViewLayout.test.tsx        # 🆕 New
```

### Integration Tests (5 files)
```
__tests__/agent/integration/
├── agent-config-flow.test.tsx
├── chat-streaming.test.tsx
├── slot-management.test.tsx
├── quota-tracking.test.tsx
├── pdf-citation-sync.test.tsx      # 🆕 Test citation → PDF page sync
```

---

## 💾 State Management (Enhanced)

### agentStore.ts (Complete Schema)
```typescript
interface AgentStore {
  // Configuration
  selectedGame: GameData | null;
  selectedTemplate: AgentTypology | null;
  selectedModel: AIModel | null;

  // Active sessions
  activeSessions: Map<string, AgentSession>;
  currentSessionId: string | null;

  // Quota & limits
  tokenQuota: { used: number; limit: number; resetAt: Date };
  slotQuota: { used: number; limit: number; tier: string };

  // UI state
  isConfigOpen: boolean;
  isChatOpen: boolean;
  isSlotsOpen: boolean;
  isSettingsOpen: boolean;           // 🆕 Settings drawer

  // PDF viewer state
  pdfViewerOpen: boolean;             // 🆕 Modal (mobile)
  pdfViewerPage: number | null;       // 🆕 Jump to page
  pdfViewerDocumentId: string | null; // 🆕 Active PDF
  activePdfTabIndex: number;          // 🆕 Multi-PDF tab

  // Split view state (desktop)
  splitViewEnabled: boolean;          // 🆕 Desktop only
  splitRatio: number;                 // 🆕 Chat width % (40-70)

  // Runtime config (settable during chat)
  runtimeConfig: {                    // 🆕 Modifiable settings
    model: AIModel;
    temperature: number;
    maxTokens: number;
    ragStrategy: string;
    ragParams: Record<string, any>;
  };

  // Actions
  setSelectedGame: (game: GameData) => void;
  setSelectedTemplate: (template: AgentTypology) => void;
  setSelectedModel: (model: AIModel) => void;
  launchAgent: () => Promise<void>;
  sendMessage: (sessionId: string, message: string) => Promise<void>;
  endSession: (sessionId: string) => Promise<void>;

  openSettings: () => void;           // 🆕
  closeSettings: () => void;          // 🆕
  updateRuntimeConfig: (config) => Promise<void>; // 🆕

  openPdfViewer: (docId: string, page: number) => void; // 🆕
  closePdfViewer: () => void;         // 🆕
  setActivePdfTab: (index: number) => void; // 🆕

  setSplitRatio: (ratio: number) => void; // 🆕
  toggleSplitView: () => void;        // 🆕

  switchAgentType: (typologyId: string) => Promise<void>; // 🆕
}
```

---

## 🚀 Implementation Roadmap (Updated)

### Phase 1: MVP (Weeks 1-2) - 12 Issues
**Goal**: Basic agent chat funzionante con SSE streaming

**Issues**: #3237-#3245 (original 9) + #3249-#3251 (enhanced 3)

**Deliverables**:
- ✅ Config flow (game/template/model selection)
- ✅ Chat interface con SSE streaming
- ✅ Token quota display con warnings
- ✅ Agent type switcher (change template)
- ✅ PDF viewer modal (citation links)
- ✅ Settings drawer (runtime config)

**Platform**: Mobile + Desktop (chat only, no split view yet)

---

### Phase 2: Enhanced UX (Week 3) - 4 Issues
**Goal**: Slot management + desktop split view

**Issues**: #3246-#3247 (slots) + #3254 (split view)

**Deliverables**:
- ✅ Slot management page (CRUD active agents)
- ✅ Premium upgrade CTA
- ✅ **Split view layout (desktop)** - chat + PDF side-by-side
- ✅ Multi-PDF tabs support
- ✅ Draggable resize handle

**Platform**: All devices (mobile bottom sheet + desktop split view)

---

### Phase 3: Testing & Polish (Week 4) - 1 Issue
**Goal**: Quality assurance e deployment prep

**Issue**: #3248 (E2E tests)

**Deliverables**:
- ✅ E2E tests (8 scenarios)
- ✅ Unit tests >85% coverage
- ✅ Integration tests >80%
- ✅ Responsive validation
- ✅ Accessibility audit
- ✅ Performance validation

---

## 📋 Checklist Before Implementation

### Design Validation
- [x] Review mockup 1: `agent-page-meepleai-style.html`
- [x] Review mockup 2: `agent-page-enhanced-v2.html`
- [x] Review mockup 3: `agent-split-view-desktop.html` ⭐
- [ ] Stakeholder approval on split view layout
- [ ] Validate with actual PDF (test PdfViewerModal)

### Technical Prerequisites
- [x] Backend API 90% ready (existing endpoints work)
- [ ] Backend creates #3252 + #3253 (PATCH endpoints)
- [x] PdfViewerModal.tsx exists and works ✅
- [x] QuotaStatusBar.tsx exists and works ✅
- [x] AgentConfigModal.tsx exists (pattern reference) ✅
- [ ] Test data: At least 1 game with 2-3 PDFs uploaded

### Development Setup
- [ ] Feature flag created: `FEATURE_AGENT_PAGE=true`
- [ ] Storybook setup for agent components (optional)
- [ ] Mock API handlers (MSW) for development
- [ ] Test environment with sample PDFs
- [ ] Analytics events defined (track usage)

---

## 🎯 Success Metrics (Final)

### User Engagement
- Config completion rate >80%
- Chat engagement >3 messages/session
- **Citation click rate >50%** (validates PDF integration value)
- **Settings usage >25%** (power users tune agent)
- **Split view retention >60%** (desktop users keep split open)

### Technical Performance
- Page load (TTI) <3s
- SSE connection success >95%
- **PDF load time <2s** (critical for citation UX)
- **Split view resize <16ms** (60 FPS smooth drag)
- Settings drawer slide <300ms

### Quality Standards
- Test coverage >85% (unit + integration)
- E2E pass rate 100% (8 scenarios)
- Accessibility score >95 (axe-core)
- **PDF keyboard nav works** (arrows, +/-, tab)
- **Resize handle accessible** (keyboard support)

---

## 📚 Documentation Index

1. **Epic v1.1**: `docs/prd/agent-page-frontend-epic.md`
2. **Enhanced Requirements**: `docs/prd/agent-page-enhanced-requirements.md`
3. **Design Update Summary**: `docs/prd/DESIGN_UPDATE_SUMMARY.md`
4. **Issues Created**: `docs/prd/agent-page-issues-created.md`
5. **This Final Spec**: `docs/prd/agent-page-complete-final.md`

### Mockups
1. `docs/mockups/agent-page-meepleai-style.html` (base)
2. `docs/mockups/agent-page-enhanced-v2.html` (enhanced)
3. `docs/mockups/agent-split-view-desktop.html` (split view) ⭐

---

## ✅ COMPLETE - Ready for Sprint Kickoff

**All deliverables**:
- ✅ 18 GitHub issues created (#3237-#3254)
- ✅ 12 original issues updated with design corrections
- ✅ 3 interactive HTML mockups
- ✅ Complete documentation (epic + requirements + specifications)
- ✅ Component reuse identified (saves ~5 days)
- ✅ API gaps identified (2 backend issues)
- ✅ Test plan defined (8 E2E + 14 unit + 5 integration)

**Total Effort**: 21 days effort → **3.5 weeks calendar** (with parallelization)

**View All Issues**:
```bash
gh issue list --label "area/agent" --state open --json number,title,labels
```

**Start Implementation**:
```bash
git checkout -b feature/issue-3237-agent-page-base
# Begin with #3237 → Sprint 1
```

🚀 **Ready to build!**
