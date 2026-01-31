# Agent Page Frontend - Epic & Issue Breakdown

**Epic**: Agent Chat Page - MeepleAI Warm & Friendly UI
**Version**: 1.1 (Design Updated)
**Created**: 2026-01-31
**Updated**: 2026-01-31 (Style aligned with existing MeepleAI aesthetic)
**Status**: Ready for Implementation
**Design Concept**: Warm & Friendly Board Game Aesthetic (Orange/Purple brand, Quicksand/Nunito fonts, subtle animations)

---

## Executive Summary

**Vision**: Mobile-first agent chat interface con design MeepleAI friendly e approachable - warm orange/purple palette, Quicksand/Nunito typography, wood texture backgrounds, glass morphism effects, e gamified UX.

**Scope**: Frontend completo per:
- Agent configuration flow (game/template/model selection)
- Gamified slot management (card-based layout)
- Token quota display con warnings
- Bottom sheet chat interface con SSE streaming
- Contextual action bar (changes based on screen state)

**Design Alignment**:
- ✅ Consistent with MeepleAI brand (Quicksand + Nunito fonts, orange/purple palette)
- ✅ Reuse existing patterns (QuotaStatusBar, UserGameCard, AgentConfigModal)
- ✅ Warm & friendly aesthetic (wood texture, soft shadows, glass morphism on desktop)
- ❌ NO dark tech/cyber-punk (incompatible with MeepleAI brand)

**Dependencies**:
- Backend: `#AGT-001` to `#AGT-010` (Agent Typology + Session Commands) - già implementato al 90%
- Design System: shadcn/ui + Tailwind CSS (già in progetto)

---

## Epic Structure

```
EPIC: Agent Page Frontend (Frontend-only) - 12 issues
├── Sprint 1: Configuration Flow (5 issues) - Week 1
│   ├── #FRONT-001: Base Setup & Routing
│   ├── #FRONT-002: Agent Config Sheet Container
│   ├── #FRONT-003: Game/Template/Model Selectors
│   ├── #FRONT-004: Token Quota & Slot Cards
│   └── #FRONT-005: Contextual Action Bar
│
├── Sprint 2: Chat Interface (4 issues) - Week 2
│   ├── #FRONT-006: Chat Sheet Container
│   ├── #FRONT-007: Message Components & Streaming
│   ├── #FRONT-008: Citations & Confidence Display
│   └── #FRONT-009: Chat Input & SSE Integration
│
├── Sprint 3: Slot Management (2 issues) - Week 3
│   ├── #FRONT-010: Slot Management Page
│   └── #FRONT-011: Upgrade Flow & Premium CTA
│
└── Sprint 4: Testing & Polish (1 issue) - Week 4
    └── #FRONT-012: E2E Tests & Responsive Validation
```

**Total Effort**: 4 settimane (1 Frontend Dev)

---

## Design System Overview (MeepleAI Brand)

### Color Palette (Existing Design Tokens)

**IMPORTANT**: Use existing MeepleAI design tokens from `apps/web/src/styles/design-tokens.css` and `globals.css`

```css
/* Light Mode (Primary) */
:root {
  --primary: 25 95% 38%;            /* #d2691e - MeepleAI Orange */
  --secondary: 142 76% 28%;         /* Green */
  --accent: 271 91% 55%;            /* #8b5cf6 - Purple */
  --background: 30 25% 97%;         /* #f8f6f0 - Warm beige */
  --card: 0 0% 100%;                /* White cards */
  --border: 30 15% 85%;             /* Beige borders */

  --meeple-orange: 25 85% 45%;      /* Brand primary */
  --meeple-purple: 262 83% 62%;     /* Brand accent */
  --meeple-warm-bg: 30 25% 97%;     /* Background */

  /* Status Colors */
  --color-warning: hsl(36, 100%, 50%);   /* Amber/Orange */
  --color-error: hsl(0, 84%, 60%);       /* Red */
  --color-success: hsl(142, 76%, 28%);   /* Green */
}

/* Dark Mode (Desktop) */
.dark {
  --primary: 25 95% 60%;            /* Lighter orange */
  --secondary: 142 76% 45%;         /* Lighter green */
  --accent: 45 93% 56%;             /* Amber */
  --background: 0 0% 10%;           /* #1a1a1a */
  --card: 0 0% 18%;                 /* #2d2d2d */
}
```

### Typography (Existing Fonts)

**IMPORTANT**: Use existing Google Fonts already loaded in `layout.tsx`

```css
/* Fonts from apps/web/src/app/layout.tsx */
--font-quicksand: 'Quicksand', sans-serif;  /* Headings, buttons - weights: 400,500,600,700 */
--font-nunito: 'Nunito', sans-serif;        /* Body text - weights: 300,400,600,700 */

/* Usage */
.heading {
  font-family: var(--font-quicksand);
  font-weight: 700;
}

.body-text {
  font-family: var(--font-nunito);
  font-weight: 400;
  line-height: 1.6;
}

.btn-text {
  font-family: var(--font-quicksand);
  font-weight: 600;
}
```

### Animations (Existing MeepleAI Keyframes)

**IMPORTANT**: Reuse existing animations from `globals.css`, DO NOT create new neon/glow animations

```css
/* From apps/web/tailwind.config.js & globals.css */
@keyframes fadeIn {
  from { opacity: 0; }
  to { opacity: 1; }
}

@keyframes slideUp {
  from { transform: translateY(20px); opacity: 0; }
  to { transform: translateY(0); opacity: 1; }
}

@keyframes pulseMeeple {
  0%, 100% { opacity: 1; }
  50% { opacity: 0.6; }
}

@keyframes bounceSubtle {
  0%, 100% { transform: translateY(0); }
  50% { transform: translateY(-3px); }
}

/* Usage */
.animate-fade-in { animation: fadeIn 0.5s ease-in; }
.animate-slide-up { animation: slideUp 0.6s ease-out; }
.animate-pulse-meeple { animation: pulseMeeple 2s ease-in-out infinite; }
.hover:animate-bounce-subtle { animation: bounceSubtle 0.35s ease-out; }
```

### Background Textures (Existing Pattern)

```css
/* Wood/Paper Texture - from globals.css */
body::before {
  content: '';
  position: fixed;
  inset: 0;
  background-image:
    repeating-linear-gradient(0deg, transparent, transparent 2px, rgba(210, 105, 30, 0.015) 2px, rgba(210, 105, 30, 0.015) 4px),
    repeating-linear-gradient(90deg, transparent, transparent 2px, rgba(139, 90, 60, 0.02) 2px, rgba(139, 90, 60, 0.02) 4px);
  pointer-events: none;
  z-index: 0;
  opacity: 0.6;
}

/* Warm gradient overlay */
body::after {
  content: '';
  position: fixed;
  inset: 0;
  background: radial-gradient(ellipse at 50% 0%, rgba(210, 105, 30, 0.03), transparent 60%);
  pointer-events: none;
  z-index: 0;
}
```

### Component Patterns (Reuse Existing)

```typescript
/* Reuse these existing components - DO NOT recreate */
✅ QuotaStatusBar.tsx          // Token quota display (already implemented!)
✅ AgentConfigModal.tsx        // Agent config modal (already implemented!)
✅ UserGameCard.tsx            // Card pattern with framer-motion
✅ Button (shadcn/ui)          // Styled buttons with variants
✅ Progress (shadcn/ui)        // Progress bars
✅ Sheet (shadcn/ui)           // Bottom sheet / drawer
✅ Dialog (shadcn/ui)          // Modals
```

---

## File Structure & Architecture

```
apps/web/src/
├── app/(authenticated)/
│   ├── library/
│   │   └── games/
│   │       └── [gameId]/
│   │           └── agent/              # NEW: Agent pages
│   │               ├── page.tsx        # Main agent config/chat page
│   │               ├── slots/
│   │               │   └── page.tsx    # Slot management page
│   │               └── loading.tsx
│   │
│   └── agent/                          # NEW: Alternative global route (optional)
│       ├── page.tsx                    # Global agent launcher
│       └── [sessionId]/
│           └── page.tsx                # Direct chat access
│
├── components/agent/                   # NEW: Agent components
│   ├── config/
│   │   ├── AgentConfigSheet.tsx       # Main config container
│   │   ├── GameSelector.tsx           # Game dropdown with PDF filter
│   │   ├── TemplateCarousel.tsx       # Horizontal scroll template picker
│   │   ├── TemplateInfoModal.tsx      # Template details modal
│   │   ├── ModelSelector.tsx          # AI model dropdown (tier-filtered)
│   │   ├── TokenQuotaDisplay.tsx      # Progress bar + warnings
│   │   ├── SlotCards.tsx              # Gamified slot visualization
│   │   └── index.ts                   # Barrel export
│   │
│   ├── chat/
│   │   ├── AgentChatSheet.tsx         # Bottom sheet chat container
│   │   ├── ChatMessage.tsx            # Single message (user/agent)
│   │   ├── ChatMessageList.tsx        # Scrollable message container
│   │   ├── ChatInput.tsx              # Input + send + attachment
│   │   ├── CitationBadge.tsx          # Citation display with page ref
│   │   ├── ConfidenceBar.tsx          # Visual confidence indicator
│   │   ├── TypingIndicator.tsx        # Streaming animation
│   │   └── index.ts
│   │
│   ├── slots/
│   │   ├── SlotManagementPage.tsx     # Full slot manager
│   │   ├── ActiveSlotCard.tsx         # Active agent slot
│   │   ├── LockedSlotCard.tsx         # Upgrade prompt
│   │   ├── SlotUsageStats.tsx         # Usage statistics
│   │   └── index.ts
│   │
│   ├── shared/
│   │   ├── ActionBar.tsx              # Base contextual action bar
│   │   ├── NeonButton.tsx             # Styled button with glows
│   │   ├── NeonProgress.tsx           # Animated neon progress bar
│   │   ├── InfoTooltip.tsx            # Expandable tooltip
│   │   ├── GlowCard.tsx               # Card with neon border glow
│   │   └── index.ts
│   │
│   └── index.ts                        # Main barrel export
│
├── hooks/agent/                        # NEW: Agent hooks
│   ├── useAgentConfig.ts              # Config state management
│   ├── useAgentChat.ts                # Chat SSE streaming
│   ├── useTokenQuota.ts               # Quota tracking
│   ├── useSlotManagement.ts           # Slot CRUD operations
│   └── index.ts
│
├── stores/
│   └── agentStore.ts                  # NEW: Zustand agent store
│
├── lib/agent/                         # NEW: Agent utilities
│   ├── api-client.ts                  # Agent API wrapper
│   ├── sse-handler.ts                 # SSE EventSource wrapper
│   ├── validation.ts                  # Config validation
│   └── types.ts                       # Agent TypeScript types
│
├── styles/
│   ├── agent-theme.css                # NEW: Agent color scheme
│   ├── agent-typography.css           # NEW: Agent fonts
│   └── agent-animations.css           # NEW: Agent animations
│
└── __tests__/agent/                   # NEW: Agent tests
    ├── unit/
    │   ├── AgentConfigSheet.test.tsx
    │   ├── ChatMessage.test.tsx
    │   ├── TokenQuotaDisplay.test.tsx
    │   └── ...
    ├── integration/
    │   ├── agent-config-flow.test.tsx
    │   ├── chat-streaming.test.tsx
    │   └── slot-management.test.tsx
    └── e2e/
        └── agent-full-flow.spec.ts
```

---

## API Integration Patterns

### API Client Setup

```typescript
// apps/web/src/lib/agent/api-client.ts

export class AgentApiClient {
  private baseUrl = '/api/v1';

  // Typologies
  async getTypologies(): Promise<AgentTypology[]> {
    return fetch(`${this.baseUrl}/admin/agent-typologies?status=Approved`)
      .then(r => r.json());
  }

  async getTypologyById(id: string): Promise<AgentTypology> {
    return fetch(`${this.baseUrl}/admin/agent-typologies/${id}`)
      .then(r => r.json());
  }

  // Models (filtered by tier)
  async getAvailableModels(userTier: string): Promise<AIModel[]> {
    return fetch(`${this.baseUrl}/agents/models?tier=${userTier}`)
      .then(r => r.json());
  }

  // User Quota
  async getTokenQuota(): Promise<TokenQuota> {
    return fetch(`${this.baseUrl}/users/me/token-quota`)
      .then(r => r.json());
  }

  // Slot Management
  async getActiveSlots(): Promise<AgentSlot[]> {
    return fetch(`${this.baseUrl}/agents/slots`)
      .then(r => r.json());
  }

  async endSession(sessionId: string): Promise<void> {
    return fetch(`${this.baseUrl}/game-sessions/${sessionId}/agent`, {
      method: 'DELETE'
    }).then(r => r.json());
  }

  // Config
  async saveAgentConfig(gameId: string, config: AgentConfig): Promise<void> {
    return fetch(`${this.baseUrl}/library/games/${gameId}/agent-config`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config)
    }).then(r => r.json());
  }

  async getAgentConfig(gameId: string): Promise<AgentConfig | null> {
    return fetch(`${this.baseUrl}/library/games/${gameId}/agent-config`)
      .then(r => r.status === 404 ? null : r.json());
  }

  // Session Launch
  async launchSessionAgent(sessionId: string, config: AgentConfig): Promise<AgentSession> {
    return fetch(`${this.baseUrl}/game-sessions/${sessionId}/agent/launch`, {
      method: 'POST',
      headers: { 'Content-Type': 'application/json' },
      body: JSON.stringify(config)
    }).then(r => r.json());
  }

  // Chat (returns EventSource for SSE)
  createChatStream(sessionId: string, query: string): EventSource {
    const url = `${this.baseUrl}/game-sessions/${sessionId}/agent/chat?query=${encodeURIComponent(query)}`;
    return new EventSource(url);
  }
}

export const agentApi = new AgentApiClient();
```

### SSE Handler Wrapper

```typescript
// apps/web/src/lib/agent/sse-handler.ts

export interface SSEChunk {
  type: 'chunk' | 'complete' | 'error';
  content?: string;
  metadata?: {
    confidence: number;
    citations: Citation[];
    model: string;
    tokens: number;
  };
  error?: string;
}

export class SSEHandler {
  private eventSource: EventSource | null = null;

  connect(
    url: string,
    onChunk: (chunk: SSEChunk) => void,
    onComplete: (metadata: SSEChunk['metadata']) => void,
    onError: (error: string) => void
  ): void {
    this.eventSource = new EventSource(url);

    this.eventSource.onmessage = (event) => {
      const data: SSEChunk = JSON.parse(event.data);

      if (data.type === 'chunk') {
        onChunk(data);
      } else if (data.type === 'complete') {
        onComplete(data.metadata);
        this.disconnect();
      } else if (data.type === 'error') {
        onError(data.error || 'Unknown error');
        this.disconnect();
      }
    };

    this.eventSource.onerror = () => {
      onError('SSE connection lost');
      this.disconnect();
    };
  }

  disconnect(): void {
    if (this.eventSource) {
      this.eventSource.close();
      this.eventSource = null;
    }
  }
}
```

---

## 🎯 SPRINT 1: Configuration Flow (Week 1)

### Issue #FRONT-001: Base Setup & Routing 🟠

**Type**: Frontend - Infrastructure
**Priority**: P1 High
**Estimate**: 0.5 day
**Assignee**: Frontend Dev 1

**Description**:
Setup base routing, styling system, e Zustand store per Agent page.

**Tasks**:
- [ ] Create routing structure: `/library/games/[gameId]/agent`
- [ ] Create alternative global route: `/agent` (optional)
- [ ] Setup CSS files: `agent-theme.css`, `agent-typography.css`, `agent-animations.css`
- [ ] Import Google Fonts: JetBrains Mono + Chakra Petch
- [ ] Create `agentStore.ts` Zustand store (skeleton)
- [ ] Create `api-client.ts` with base endpoints
- [ ] Create `sse-handler.ts` wrapper class
- [ ] Add barrel exports for clean imports

**File Structure**:
```
apps/web/src/
├── app/(authenticated)/library/games/[gameId]/agent/
│   ├── page.tsx              # "use client" - main agent page
│   ├── loading.tsx
│   └── slots/page.tsx        # Slot management
│
├── styles/
│   ├── agent-theme.css       # Color variables
│   ├── agent-typography.css  # Font imports + classes
│   └── agent-animations.css  # Keyframes
│
├── stores/
│   └── agentStore.ts         # Zustand store
│
└── lib/agent/
    ├── api-client.ts         # AgentApiClient class
    ├── sse-handler.ts        # SSEHandler class
    └── types.ts              # TypeScript interfaces
```

**Acceptance Criteria**:
- ✅ `/library/games/123/agent` route renders placeholder
- ✅ CSS variables accessible globally (test with `var(--agent-accent-cyan)`)
- ✅ Fonts loaded correctly (test JetBrains Mono in DevTools)
- ✅ Zustand store accessible (`useAgentStore()` works)
- ✅ API client instantiable (`agentApi.getTypologies()` compiles)
- ✅ No console errors on page load

**Technical Notes**:
```typescript
// apps/web/src/app/(authenticated)/library/games/[gameId]/agent/page.tsx
"use client";

import { useParams } from 'next/navigation';
import { useAgentStore } from '@/stores/agentStore';

export default function AgentPage() {
  const { gameId } = useParams();
  const store = useAgentStore();

  return (
    <div className="min-h-screen bg-[var(--agent-bg-primary)]">
      <h1 className="agent-heading text-[var(--agent-accent-cyan)]">
        Agent Setup - Game {gameId}
      </h1>
      {/* Placeholder for #FRONT-002 */}
    </div>
  );
}
```

---

### Issue #FRONT-002: Agent Config Sheet Container 🟠

**Type**: Frontend - Component
**Priority**: P1 High
**Estimate**: 1 day
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-001

**Description**:
Creare container principale `AgentConfigSheet` con layout responsivo e gestione stati.

**Tasks**:
- [ ] Create `AgentConfigSheet.tsx` component
- [ ] Implement responsive layout (mobile bottom sheet, desktop modal)
- [ ] Add state machine: 'closed' | 'config' | 'template-info' | 'model-pricing'
- [ ] Integrate with shadcn/ui Sheet component
- [ ] Add header with back button + help icon
- [ ] Add loading states (skeleton loaders)
- [ ] Connect to Zustand store (isConfigOpen state)

**Component Structure**:
```typescript
// apps/web/src/components/agent/config/AgentConfigSheet.tsx
"use client";

import { Sheet, SheetContent, SheetHeader, SheetTitle } from '@/components/ui/sheet';
import { useAgentStore } from '@/stores/agentStore';
import { GameSelector } from './GameSelector';
import { TemplateCarousel } from './TemplateCarousel';
import { ModelSelector } from './ModelSelector';
import { TokenQuotaDisplay } from './TokenQuotaDisplay';
import { SlotCards } from './SlotCards';
import { ActionBar } from '../shared/ActionBar';

export function AgentConfigSheet() {
  const { isConfigOpen, closeConfig, selectedGame, selectedTemplate, selectedModel } = useAgentStore();

  const isConfigComplete = selectedGame && selectedTemplate && selectedModel;

  return (
    <Sheet open={isConfigOpen} onOpenChange={closeConfig}>
      <SheetContent
        side="bottom"
        className="h-[90vh] bg-[var(--agent-bg-primary)]"
      >
        <SheetHeader>
          <SheetTitle className="agent-heading text-[var(--agent-accent-cyan)]">
            AGENT SETUP
          </SheetTitle>
        </SheetHeader>

        <div className="flex flex-col gap-6 py-6 overflow-y-auto">
          <GameSelector />
          <TemplateCarousel />
          <ModelSelector />
          <TokenQuotaDisplay />
          <SlotCards />
        </div>

        <ActionBar
          state="config"
          onCancel={closeConfig}
          onLaunch={() => {/* #FRONT-005 */}}
          isLaunchDisabled={!isConfigComplete}
        />
      </SheetContent>
    </Sheet>
  );
}
```

**Responsive Behavior**:
```typescript
// Mobile (default): Bottom sheet, 90vh height
className="h-[90vh] rounded-t-2xl"

// Tablet: Side sheet, 500px width
@media (min-width: 768px) {
  side="right"
  className="w-[500px] h-full"
}

// Desktop: Centered modal, 600px max-width
@media (min-width: 1024px) {
  className="max-w-[600px] mx-auto"
}
```

**Acceptance Criteria**:
- ✅ Sheet opens/closes smoothly (transition 300ms)
- ✅ Responsive layout works on mobile/tablet/desktop
- ✅ Header displays correctly with back + help icons
- ✅ Child components render in correct order
- ✅ ActionBar pinned to bottom (sticky)
- ✅ No layout shifts during open/close

---

### Issue #FRONT-003: Game/Template/Model Selectors 🟠

**Type**: Frontend - Component
**Priority**: P1 High
**Estimate**: 1.5 days
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-002

**Description**:
Implementare i 3 selector principali: GameSelector, TemplateCarousel, ModelSelector.

**Tasks**:

**GameSelector**:
- [ ] Create `GameSelector.tsx` with shadcn Select
- [ ] Fetch games with `useQuery` → `/library/games?hasPdf=true`
- [ ] Filter: only games in user library + SharedGameCatalog with PDF
- [ ] Display: Game name + "📚 Rulebook available" badge
- [ ] Update Zustand store on selection
- [ ] Add validation: required field, show error if empty

**TemplateCarousel**:
- [ ] Create `TemplateCarousel.tsx` with horizontal scroll
- [ ] Fetch typologies → `/admin/agent-typologies?status=Approved`
- [ ] Render as cards (80px width, icon + name)
- [ ] Implement touch swipe + scroll snap
- [ ] Add info icon → opens `TemplateInfoModal` (#FRONT-003.1)
- [ ] Highlight selected with neon cyan glow
- [ ] Update Zustand store on selection

**ModelSelector**:
- [ ] Create `ModelSelector.tsx` with shadcn Select
- [ ] Fetch models → `/agents/models?tier={userTier}`
- [ ] Filter by user tier (Free: GPT-4o-mini, Llama-3.3 | Premium: +Claude, GPT-4)
- [ ] Display: Model name + tier badge + cost estimate
- [ ] Show cost per query: "~$0.001/query"
- [ ] Update Zustand store on selection
- [ ] Add tooltip explaining tier limits

**TemplateInfoModal** (Sub-component):
- [ ] Create `TemplateInfoModal.tsx` modal component
- [ ] Display: Name, Description, Base Prompt (preview), Default Strategy
- [ ] Show example questions (if available)
- [ ] Add "Got it" button to close
- [ ] Animate in/out with fade + slide

**Component Examples**:
```typescript
// GameSelector.tsx
export function GameSelector() {
  const { data: games } = useQuery({
    queryKey: ['library-games-with-pdf'],
    queryFn: () => agentApi.getGamesWithPdf()
  });

  const { selectedGame, setSelectedGame } = useAgentStore();

  return (
    <GlowCard className="p-4">
      <h3 className="agent-heading text-sm mb-2">🎮 GAME SELECTION</h3>
      <Select value={selectedGame?.id} onValueChange={(id) => {
        const game = games?.find(g => g.id === id);
        setSelectedGame(game);
      }}>
        <SelectTrigger className="bg-[var(--agent-bg-input)] border-[var(--agent-accent-cyan)]">
          <SelectValue placeholder="Select game..." />
        </SelectTrigger>
        <SelectContent>
          {games?.map(game => (
            <SelectItem key={game.id} value={game.id}>
              {game.name}
              <span className="ml-2 text-xs text-[var(--agent-accent-cyan)]">
                📚 Rulebook available
              </span>
            </SelectItem>
          ))}
        </SelectContent>
      </Select>
    </GlowCard>
  );
}
```

```typescript
// TemplateCarousel.tsx
export function TemplateCarousel() {
  const { data: templates } = useQuery({
    queryKey: ['agent-typologies'],
    queryFn: () => agentApi.getTypologies()
  });

  const { selectedTemplate, setSelectedTemplate } = useAgentStore();
  const [isInfoOpen, setIsInfoOpen] = useState(false);

  return (
    <GlowCard className="p-4">
      <div className="flex items-center justify-between mb-2">
        <h3 className="agent-heading text-sm">🤖 AGENT TYPE</h3>
        <button onClick={() => setIsInfoOpen(true)} className="text-[var(--agent-text-secondary)]">
          <InfoIcon className="h-4 w-4" />
        </button>
      </div>

      <div className="flex gap-3 overflow-x-auto snap-x snap-mandatory pb-2">
        {templates?.map(template => (
          <button
            key={template.id}
            onClick={() => setSelectedTemplate(template)}
            className={cn(
              "flex-shrink-0 w-20 h-24 rounded-lg border-2 flex flex-col items-center justify-center",
              "transition-all duration-300",
              selectedTemplate?.id === template.id
                ? "border-[var(--agent-accent-cyan)] shadow-[var(--agent-glow-cyan)] animate-agent-neon-pulse"
                : "border-[var(--agent-bg-tertiary)] hover:border-[var(--agent-text-secondary)]"
            )}
          >
            <span className="text-2xl mb-1">{template.icon}</span>
            <span className="text-xs text-center agent-heading">
              {template.shortName}
            </span>
          </button>
        ))}
      </div>

      <p className="text-sm text-[var(--agent-text-secondary)] mt-2">
        {selectedTemplate?.description}
      </p>

      <TemplateInfoModal
        template={selectedTemplate}
        isOpen={isInfoOpen}
        onClose={() => setIsInfoOpen(false)}
      />
    </GlowCard>
  );
}
```

**Acceptance Criteria**:
- ✅ GameSelector: Populated with games (hasPdf=true only)
- ✅ TemplateCarousel: Horizontal scroll works, snap behavior
- ✅ ModelSelector: Filtered correctly by user tier
- ✅ Selection updates Zustand store immediately
- ✅ TemplateInfoModal: Opens on info icon, displays all details
- ✅ Neon glow animation on selected items
- ✅ Cost estimate accurate ("$0.001/query" format)

---

### Issue #FRONT-004: Token Quota & Slot Cards 🟠

**Type**: Frontend - Component
**Priority**: P1 High
**Estimate**: 1 day
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-002

**Description**:
Implementare `TokenQuotaDisplay` (progress bar + warnings) e `SlotCards` (gamified visualization).

**Tasks**:

**TokenQuotaDisplay**:
- [ ] Create `TokenQuotaDisplay.tsx` component
- [ ] Fetch quota → `useTokenQuota()` hook → `/users/me/token-quota`
- [ ] Display: Progress bar (animated fill) + numeric "450/500"
- [ ] Add countdown timer: "Resets in 2d 14h"
- [ ] Warning state: Show yellow glow + alert icon if >90%
- [ ] Color coding: <70% cyan, 70-90% yellow, >90% red
- [ ] Auto-refresh quota every 30s (React Query refetch)

**SlotCards**:
- [ ] Create `SlotCards.tsx` component
- [ ] Fetch slots → `useSlotManagement()` hook → `/agents/slots`
- [ ] Render active slots as cards (icon + game name + type)
- [ ] Render available slots as empty cards (○ icon)
- [ ] Render locked slots (🔒 + "Upgrade for +3" text)
- [ ] Add upgrade CTA with purple glow
- [ ] Implement horizontal scroll for mobile
- [ ] Show usage: "2 / 5 slots used"

**Component Examples**:
```typescript
// TokenQuotaDisplay.tsx
export function TokenQuotaDisplay() {
  const { data: quota, isLoading } = useTokenQuota();

  if (isLoading) return <Skeleton className="h-20" />;

  const percentage = (quota.used / quota.limit) * 100;
  const warningLevel = percentage > 90 ? 'critical' : percentage > 70 ? 'warning' : 'ok';

  return (
    <GlowCard className="p-4">
      <h3 className="agent-heading text-sm mb-2">📊 TOKEN USAGE THIS WEEK</h3>

      <NeonProgress
        value={percentage}
        color={warningLevel === 'critical' ? 'red' : warningLevel === 'warning' ? 'yellow' : 'cyan'}
        className="h-3 mb-2"
      />

      <div className="flex items-center justify-between text-sm">
        <span className="agent-numbers text-[var(--agent-text-primary)]">
          {quota.used} / {quota.limit}
        </span>
        <span className="text-[var(--agent-text-secondary)]">
          Resets in {formatTimeRemaining(quota.resetAt)}
        </span>
      </div>

      {warningLevel !== 'ok' && (
        <div className="mt-2 flex items-center gap-2 text-xs text-[var(--agent-accent-yellow)]">
          <AlertTriangle className="h-4 w-4" />
          <span>
            {warningLevel === 'critical'
              ? `Only ${quota.limit - quota.used} tokens remaining`
              : 'Approaching token limit'}
          </span>
        </div>
      )}
    </GlowCard>
  );
}
```

```typescript
// SlotCards.tsx
export function SlotCards() {
  const { data: slots, isLoading } = useSlotManagement();

  if (isLoading) return <Skeleton className="h-32" />;

  const activeSlots = slots.filter(s => s.isActive);
  const totalSlots = slots.length;
  const availableSlots = slots.filter(s => !s.isActive && !s.isLocked);
  const lockedSlots = slots.filter(s => s.isLocked);

  return (
    <GlowCard className="p-4">
      <h3 className="agent-heading text-sm mb-2">🎰 ACTIVE AGENT SLOTS</h3>

      <div className="flex gap-3 overflow-x-auto pb-2">
        {/* Active Slots */}
        {activeSlots.map(slot => (
          <div key={slot.id} className="flex-shrink-0 w-20 h-24 rounded-lg border-2 border-[var(--agent-accent-cyan)] bg-[var(--agent-bg-secondary)] p-2 flex flex-col items-center justify-center">
            <div className="w-3 h-3 rounded-full bg-[var(--agent-accent-cyan)] mb-1" />
            <span className="text-xs agent-heading">{slot.typology.shortName}</span>
            <span className="text-xs text-[var(--agent-text-muted)] truncate w-full text-center">
              {slot.game.name.slice(0, 6)}
            </span>
          </div>
        ))}

        {/* Available Slots */}
        {availableSlots.map(slot => (
          <div key={slot.id} className="flex-shrink-0 w-20 h-24 rounded-lg border-2 border-dashed border-[var(--agent-bg-tertiary)] bg-[var(--agent-bg-secondary)] flex items-center justify-center">
            <div className="w-8 h-8 rounded-full border-2 border-[var(--agent-text-muted)]" />
          </div>
        ))}

        {/* Locked Slots */}
        {lockedSlots.map(slot => (
          <div key={slot.id} className="flex-shrink-0 w-20 h-24 rounded-lg border-2 border-[var(--agent-accent-purple)] bg-[var(--agent-bg-secondary)] flex flex-col items-center justify-center opacity-60">
            <Lock className="h-6 w-6 text-[var(--agent-accent-purple)] mb-1" />
            <span className="text-xs text-[var(--agent-text-muted)]">Locked</span>
          </div>
        ))}
      </div>

      <div className="flex items-center justify-between mt-3 text-sm">
        <span className="agent-numbers">{activeSlots.length} / {totalSlots} slots used</span>
        {lockedSlots.length > 0 && (
          <button className="text-[var(--agent-accent-purple)] hover:underline flex items-center gap-1">
            <Sparkles className="h-4 w-4" />
            Upgrade for +{lockedSlots.length}
          </button>
        )}
      </div>
    </GlowCard>
  );
}
```

**Acceptance Criteria**:
- ✅ TokenQuotaDisplay: Progress bar animates on mount
- ✅ TokenQuotaDisplay: Warning shows when >90%
- ✅ TokenQuotaDisplay: Countdown timer updates every second
- ✅ SlotCards: Active slots show correct icon + game
- ✅ SlotCards: Available slots render as empty circles
- ✅ SlotCards: Locked slots show purple lock icon
- ✅ SlotCards: Upgrade CTA visible when locked slots exist
- ✅ Horizontal scroll works on mobile

---

### Issue #FRONT-005: Contextual Action Bar 🟠

**Type**: Frontend - Component
**Priority**: P1 High
**Estimate**: 1 day
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-002, #FRONT-003, #FRONT-004

**Description**:
Implementare `ActionBar` component con comportamento contestuale basato su screen state.

**Tasks**:
- [ ] Create `ActionBar.tsx` base component
- [ ] Implement state-based configuration ('config' | 'chat' | 'slots' | 'template-info')
- [ ] Create `AgentConfigActionBar.tsx` (Cancel + Launch)
- [ ] Create `ChatActionBar.tsx` (Settings + Export + Minimize)
- [ ] Create `SlotActionBar.tsx` (View Usage + Back)
- [ ] Add NeonButton component with glow effects
- [ ] Implement sticky positioning (bottom: 0)
- [ ] Add safe area insets for mobile (iOS notch)
- [ ] Connect to store actions (launch, export, close)

**Action Bar Configuration**:
```typescript
// apps/web/src/components/agent/shared/ActionBar.tsx
type ActionBarState = 'config' | 'chat' | 'slots' | 'template-info';

interface ActionBarProps {
  state: ActionBarState;
  onCancel?: () => void;
  onLaunch?: () => void;
  onSettings?: () => void;
  onExport?: () => void;
  onMinimize?: () => void;
  onBack?: () => void;
  isLaunchDisabled?: boolean;
}

export function ActionBar({ state, ...handlers }: ActionBarProps) {
  return (
    <div className="sticky bottom-0 left-0 right-0 bg-[var(--agent-bg-secondary)] border-t border-[var(--agent-bg-tertiary)] p-4 pb-safe">
      {state === 'config' && (
        <div className="flex gap-3">
          <NeonButton variant="ghost" onClick={handlers.onCancel} className="flex-1">
            Cancel
          </NeonButton>
          <NeonButton
            variant="primary"
            onClick={handlers.onLaunch}
            disabled={handlers.isLaunchDisabled}
            className="flex-1"
            glowColor="cyan"
          >
            Launch 🚀
          </NeonButton>
        </div>
      )}

      {state === 'chat' && (
        <div className="flex gap-2">
          <NeonButton variant="ghost" size="sm" onClick={handlers.onSettings}>
            <Settings className="h-4 w-4 mr-1" />
            Settings
          </NeonButton>
          <NeonButton variant="ghost" size="sm" onClick={handlers.onExport}>
            <Download className="h-4 w-4 mr-1" />
            Export
          </NeonButton>
          <NeonButton variant="ghost" size="sm" onClick={handlers.onMinimize} className="ml-auto">
            Minimize ▼
          </NeonButton>
        </div>
      )}

      {state === 'slots' && (
        <div className="flex gap-3">
          <NeonButton variant="ghost" onClick={handlers.onBack} className="flex-1">
            View Usage
          </NeonButton>
          <NeonButton variant="default" onClick={handlers.onBack} className="flex-1">
            Back
          </NeonButton>
        </div>
      )}

      {state === 'template-info' && (
        <NeonButton variant="primary" onClick={handlers.onBack} className="w-full" glowColor="cyan">
          Got it
        </NeonButton>
      )}
    </div>
  );
}
```

**NeonButton Component**:
```typescript
// apps/web/src/components/agent/shared/NeonButton.tsx
interface NeonButtonProps extends ButtonProps {
  glowColor?: 'cyan' | 'purple' | 'yellow' | 'red';
  variant?: 'primary' | 'ghost' | 'default';
}

export function NeonButton({
  glowColor = 'cyan',
  variant = 'default',
  className,
  ...props
}: NeonButtonProps) {
  const glowClass = {
    cyan: 'hover:shadow-[var(--agent-glow-cyan)]',
    purple: 'hover:shadow-[var(--agent-glow-purple)]',
    yellow: 'hover:shadow-[var(--agent-glow-yellow)]',
    red: 'hover:shadow-[var(--agent-glow-red)]',
  }[glowColor];

  const variantClass = {
    primary: 'bg-[var(--agent-accent-cyan)] text-black font-bold hover:animate-agent-neon-pulse',
    ghost: 'bg-transparent border border-[var(--agent-bg-tertiary)] hover:border-[var(--agent-text-secondary)]',
    default: 'bg-[var(--agent-bg-tertiary)] hover:bg-[var(--agent-bg-input)]',
  }[variant];

  return (
    <Button
      className={cn(
        'agent-heading transition-all duration-300',
        variantClass,
        glowClass,
        className
      )}
      {...props}
    />
  );
}
```

**Acceptance Criteria**:
- ✅ ActionBar changes layout based on state prop
- ✅ Buttons have correct handlers connected
- ✅ Launch button disabled when config incomplete
- ✅ NeonButton shows glow effect on hover
- ✅ Primary button has cyan glow + pulse animation
- ✅ Ghost button has border + subtle hover
- ✅ Safe area insets work on iOS (pb-safe)
- ✅ Sticky positioning works on scroll

---

## 🎯 SPRINT 2: Chat Interface (Week 2)

### Issue #FRONT-006: Chat Sheet Container 🟠

**Type**: Frontend - Component
**Priority**: P1 High
**Estimate**: 1 day
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-005

**Description**:
Creare container `AgentChatSheet` per bottom sheet chat con header e layout.

**Tasks**:
- [ ] Create `AgentChatSheet.tsx` component
- [ ] Implement bottom sheet pattern (shadcn Sheet)
- [ ] Add header: Game name + Agent type + Close button
- [ ] Add model badge + token counter in header
- [ ] Implement swipe-to-minimize gesture (mobile)
- [ ] Add resize handle for manual expand/collapse
- [ ] Connect to Zustand store (isChatOpen state)
- [ ] Add ChatActionBar integration

**Component Structure**:
```typescript
// apps/web/src/components/agent/chat/AgentChatSheet.tsx
"use client";

export function AgentChatSheet() {
  const { isChatOpen, closeChat, currentSession } = useAgentStore();
  const { data: quota } = useTokenQuota();

  return (
    <Sheet open={isChatOpen} onOpenChange={closeChat}>
      <SheetContent
        side="bottom"
        className="h-[80vh] bg-[var(--agent-bg-primary)] rounded-t-2xl"
      >
        {/* Header */}
        <div className="flex items-center justify-between pb-3 border-b border-[var(--agent-bg-tertiary)]">
          <div className="flex items-center gap-2">
            <div className="w-2 h-2 rounded-full bg-[var(--agent-accent-cyan)] animate-agent-neon-pulse" />
            <span className="agent-heading text-sm">
              {currentSession?.game.name} - {currentSession?.typology.name}
            </span>
          </div>
          <button onClick={closeChat} className="text-[var(--agent-text-secondary)]">
            <X className="h-5 w-5" />
          </button>
        </div>

        {/* Sub-header: Model + Quota */}
        <div className="flex items-center gap-3 py-2 text-xs text-[var(--agent-text-secondary)]">
          <span className="flex items-center gap-1">
            <Zap className="h-3 w-3" />
            {currentSession?.model.name}
          </span>
          <Separator orientation="vertical" className="h-3" />
          <span className="agent-numbers">
            {quota.used}/{quota.limit} tokens
          </span>
        </div>

        {/* Chat Content */}
        <div className="flex-1 overflow-hidden">
          <ChatMessageList sessionId={currentSession?.id} />
        </div>

        {/* Input */}
        <ChatInput sessionId={currentSession?.id} />

        {/* Action Bar */}
        <ChatActionBar
          onSettings={() => {/* settings modal */}}
          onExport={() => {/* export chat */}}
          onMinimize={closeChat}
        />
      </SheetContent>
    </Sheet>
  );
}
```

**Acceptance Criteria**:
- ✅ Sheet opens from bottom with smooth transition
- ✅ Header displays game + agent type correctly
- ✅ Model badge + token counter visible
- ✅ Close button closes sheet
- ✅ Swipe-down gesture minimizes (mobile)
- ✅ Resize handle allows manual height adjustment
- ✅ ChatActionBar pinned to bottom

---

### Issue #FRONT-007: Message Components & Streaming 🔴

**Type**: Frontend - Component
**Priority**: P0 Critical
**Estimate**: 1.5 days
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-006

**Description**:
Implementare `ChatMessage`, `ChatMessageList`, `TypingIndicator` con SSE streaming support.

**Tasks**:
- [ ] Create `ChatMessage.tsx` (user/agent variants)
- [ ] Create `ChatMessageList.tsx` (scrollable container)
- [ ] Create `TypingIndicator.tsx` (animated dots + cursor)
- [ ] Implement SSE streaming hook: `useAgentChat()`
- [ ] Add progressive text reveal (chunk-by-chunk)
- [ ] Auto-scroll to bottom on new messages
- [ ] Add timestamps (relative: "2m ago")
- [ ] Add copy message button (hover action)
- [ ] Implement message grouping (same sender)

**SSE Hook**:
```typescript
// apps/web/src/hooks/agent/useAgentChat.ts
export function useAgentChat(sessionId: string) {
  const [messages, setMessages] = useState<ChatMessage[]>([]);
  const [isStreaming, setIsStreaming] = useState(false);
  const [currentChunk, setCurrentChunk] = useState('');

  const sendMessage = useCallback(async (query: string) => {
    // Add user message immediately
    const userMessage: ChatMessage = {
      id: uuid(),
      role: 'user',
      content: query,
      timestamp: new Date()
    };
    setMessages(prev => [...prev, userMessage]);

    // Start streaming
    setIsStreaming(true);
    const agentMessageId = uuid();

    const sseHandler = new SSEHandler();
    sseHandler.connect(
      agentApi.createChatStreamUrl(sessionId, query),
      // onChunk
      (chunk) => {
        setCurrentChunk(prev => prev + chunk.content);
      },
      // onComplete
      (metadata) => {
        const agentMessage: ChatMessage = {
          id: agentMessageId,
          role: 'agent',
          content: currentChunk,
          confidence: metadata.confidence,
          citations: metadata.citations,
          timestamp: new Date()
        };
        setMessages(prev => [...prev, agentMessage]);
        setCurrentChunk('');
        setIsStreaming(false);
      },
      // onError
      (error) => {
        console.error('SSE error:', error);
        setIsStreaming(false);
        // Show error message
      }
    );
  }, [sessionId, currentChunk]);

  return { messages, isStreaming, currentChunk, sendMessage };
}
```

**ChatMessage Component**:
```typescript
// apps/web/src/components/agent/chat/ChatMessage.tsx
interface ChatMessageProps {
  message: ChatMessage;
  isStreaming?: boolean;
}

export function ChatMessage({ message, isStreaming }: ChatMessageProps) {
  const isUser = message.role === 'user';

  return (
    <div
      className={cn(
        "flex flex-col gap-1 mb-4 animate-agent-slide-in-up",
        isUser ? "items-end" : "items-start"
      )}
    >
      {/* Sender Label */}
      <span className="text-xs text-[var(--agent-text-muted)] agent-heading">
        {isUser ? 'YOU' : 'AGENT'} · {formatRelativeTime(message.timestamp)}
      </span>

      {/* Message Bubble */}
      <div
        className={cn(
          "max-w-[80%] rounded-lg p-3",
          isUser
            ? "bg-[var(--agent-accent-cyan)] text-black"
            : "bg-[var(--agent-bg-secondary)] border border-[var(--agent-bg-tertiary)]"
        )}
      >
        <p className="text-sm whitespace-pre-wrap">
          {message.content}
          {isStreaming && <span className="inline-block w-2 h-4 ml-1 bg-current animate-agent-cursor-blink">▊</span>}
        </p>

        {/* Agent Metadata */}
        {!isUser && message.confidence && (
          <div className="mt-2 pt-2 border-t border-[var(--agent-bg-tertiary)]">
            <ConfidenceBar confidence={message.confidence} />
            {message.citations?.map(citation => (
              <CitationBadge key={citation.id} citation={citation} />
            ))}
          </div>
        )}
      </div>

      {/* Copy Button (hover) */}
      <button
        onClick={() => navigator.clipboard.writeText(message.content)}
        className="opacity-0 hover:opacity-100 transition-opacity text-xs text-[var(--agent-text-muted)]"
      >
        Copy
      </button>
    </div>
  );
}
```

**Acceptance Criteria**:
- ✅ User messages right-aligned (cyan background)
- ✅ Agent messages left-aligned (dark background)
- ✅ SSE streaming shows progressive text reveal
- ✅ Typing indicator animates during streaming
- ✅ Auto-scroll to bottom on new message
- ✅ Timestamps show relative time ("2m ago")
- ✅ Copy button appears on hover
- ✅ Message grouping when same sender consecutive

---

### Issue #FRONT-008: Citations & Confidence Display 🟡

**Type**: Frontend - Component
**Priority**: P2 Medium
**Estimate**: 0.5 day
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-007

**Description**:
Implementare `CitationBadge` e `ConfidenceBar` per metadata agent.

**Tasks**:
- [ ] Create `CitationBadge.tsx` (clickable badge)
- [ ] Create `ConfidenceBar.tsx` (visual bar + percentage)
- [ ] Add citation click handler (future: scroll to PDF page)
- [ ] Color-code confidence: >80% cyan, 60-80% yellow, <60% red
- [ ] Add tooltip on citation hover (source details)
- [ ] Implement badge layout (wrap if multiple)

**Components**:
```typescript
// CitationBadge.tsx
export function CitationBadge({ citation }: { citation: Citation }) {
  return (
    <Tooltip>
      <TooltipTrigger asChild>
        <button
          className="inline-flex items-center gap-1 px-2 py-1 rounded text-xs bg-[var(--agent-bg-tertiary)] text-[var(--agent-accent-cyan)] border border-[var(--agent-accent-cyan)] hover:bg-[var(--agent-bg-input)] transition-colors"
          onClick={() => {/* Future: open PDF at page */}}
        >
          <FileText className="h-3 w-3" />
          {citation.source} p.{citation.page}
        </button>
      </TooltipTrigger>
      <TooltipContent>
        <p className="text-xs">{citation.snippet}</p>
      </TooltipContent>
    </Tooltip>
  );
}
```

```typescript
// ConfidenceBar.tsx
export function ConfidenceBar({ confidence }: { confidence: number }) {
  const color = confidence >= 0.8 ? 'cyan' : confidence >= 0.6 ? 'yellow' : 'red';
  const percentage = Math.round(confidence * 100);

  return (
    <div className="flex items-center gap-2 mb-2">
      <span className="text-xs text-[var(--agent-text-muted)]">Confidence:</span>
      <div className="flex-1 h-2 bg-[var(--agent-bg-tertiary)] rounded-full overflow-hidden">
        <div
          className={cn(
            "h-full transition-all duration-500",
            color === 'cyan' && "bg-[var(--agent-accent-cyan)]",
            color === 'yellow' && "bg-[var(--agent-accent-yellow)]",
            color === 'red' && "bg-[var(--agent-accent-red)]"
          )}
          style={{ width: `${percentage}%` }}
        />
      </div>
      <span className="text-xs agent-numbers text-[var(--agent-text-primary)]">
        {percentage}%
      </span>
    </div>
  );
}
```

**Acceptance Criteria**:
- ✅ CitationBadge clickable (logs for now)
- ✅ Citation tooltip shows snippet on hover
- ✅ ConfidenceBar color-coded correctly
- ✅ ConfidenceBar animates fill on mount
- ✅ Multiple citations wrap correctly

---

### Issue #FRONT-009: Chat Input & SSE Integration 🟠

**Type**: Frontend - Component
**Priority**: P1 High
**Estimate**: 1 day
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-007

**Description**:
Implementare `ChatInput` con send, attachment (optional), e SSE trigger integration.

**Tasks**:
- [ ] Create `ChatInput.tsx` component
- [ ] Add textarea with auto-resize (max 5 rows)
- [ ] Add send button (disabled during streaming)
- [ ] Add attachment button (optional, PDF upload future)
- [ ] Trigger `sendMessage()` on Enter key (Shift+Enter = newline)
- [ ] Clear input after send
- [ ] Show loading spinner during streaming
- [ ] Add character limit (1000 chars)

**Component**:
```typescript
// apps/web/src/components/agent/chat/ChatInput.tsx
export function ChatInput({ sessionId }: { sessionId: string }) {
  const [input, setInput] = useState('');
  const { sendMessage, isStreaming } = useAgentChat(sessionId);
  const textareaRef = useRef<HTMLTextAreaElement>(null);

  const handleSend = async () => {
    if (!input.trim() || isStreaming) return;

    await sendMessage(input.trim());
    setInput('');
    textareaRef.current?.focus();
  };

  const handleKeyDown = (e: React.KeyboardEvent) => {
    if (e.key === 'Enter' && !e.shiftKey) {
      e.preventDefault();
      handleSend();
    }
  };

  return (
    <div className="flex items-end gap-2 p-3 bg-[var(--agent-bg-secondary)] border-t border-[var(--agent-bg-tertiary)]">
      {/* Attachment (optional) */}
      <button className="p-2 text-[var(--agent-text-secondary)] hover:text-[var(--agent-text-primary)]">
        <Paperclip className="h-5 w-5" />
      </button>

      {/* Textarea */}
      <textarea
        ref={textareaRef}
        value={input}
        onChange={(e) => setInput(e.target.value)}
        onKeyDown={handleKeyDown}
        placeholder="Type your question..."
        className="flex-1 bg-[var(--agent-bg-input)] text-[var(--agent-text-primary)] rounded-lg px-3 py-2 resize-none focus:outline-none focus:ring-2 focus:ring-[var(--agent-accent-cyan)]"
        rows={1}
        maxLength={1000}
        disabled={isStreaming}
      />

      {/* Character Count */}
      <span className="text-xs text-[var(--agent-text-muted)] absolute bottom-1 right-16">
        {input.length}/1000
      </span>

      {/* Send Button */}
      <NeonButton
        variant="primary"
        size="icon"
        onClick={handleSend}
        disabled={!input.trim() || isStreaming}
        glowColor="cyan"
      >
        {isStreaming ? <Loader2 className="h-5 w-5 animate-spin" /> : <ArrowUp className="h-5 w-5" />}
      </NeonButton>
    </div>
  );
}
```

**Acceptance Criteria**:
- ✅ Textarea auto-resizes up to 5 rows
- ✅ Enter sends message, Shift+Enter adds newline
- ✅ Send button disabled when empty or streaming
- ✅ Input clears after successful send
- ✅ Character counter shows "X/1000"
- ✅ Loading spinner during SSE streaming
- ✅ Attachment button present (disabled for MVP)

---

## 🎯 SPRINT 3: Slot Management (Week 3)

### Issue #FRONT-010: Slot Management Page 🟡

**Type**: Frontend - Component
**Priority**: P2 Medium
**Estimate**: 1.5 days
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-004

**Description**:
Creare pagina completa per gestione slot: active slots, usage stats, end session.

**Tasks**:
- [ ] Create `SlotManagementPage.tsx` full page
- [ ] Create `ActiveSlotCard.tsx` (shows active agent)
- [ ] Add "Open Chat" button (navigates to chat)
- [ ] Add "End Session" button (confirmation dialog)
- [ ] Create `SlotUsageStats.tsx` (usage breakdown)
- [ ] Add tier badge (Free/Premium)
- [ ] Implement end session mutation
- [ ] Add SlotActionBar integration

**File**: `apps/web/src/app/(authenticated)/library/games/[gameId]/agent/slots/page.tsx`

```typescript
// SlotManagementPage.tsx
export default function SlotManagementPage() {
  const { data: slots, isLoading } = useSlotManagement();
  const { mutate: endSession } = useEndSession();

  if (isLoading) return <PageSkeleton />;

  const activeSlots = slots.filter(s => s.isActive);
  const tierInfo = useTierInfo();

  return (
    <div className="min-h-screen bg-[var(--agent-bg-primary)] p-4">
      {/* Header */}
      <div className="flex items-center justify-between mb-6">
        <h1 className="agent-heading text-2xl text-[var(--agent-accent-cyan)]">
          AGENT SLOTS
        </h1>
        <button className="text-[var(--agent-text-secondary)]">
          <HelpCircle className="h-5 w-5" />
        </button>
      </div>

      {/* Tier Info */}
      <GlowCard className="p-4 mb-6">
        <div className="flex items-center justify-between">
          <span className="agent-heading text-sm">Your Plan: {tierInfo.name}</span>
          <Badge variant={tierInfo.isPremium ? 'premium' : 'default'}>
            {tierInfo.name}
          </Badge>
        </div>
        <NeonProgress
          value={(activeSlots.length / slots.length) * 100}
          color={activeSlots.length === slots.length ? 'red' : 'cyan'}
          className="h-2 mt-2"
        />
        <p className="text-sm text-[var(--agent-text-secondary)] mt-2">
          {activeSlots.length} / {slots.length} slots active
        </p>
      </GlowCard>

      {/* Active Slots */}
      <h2 className="agent-heading text-lg mb-4">ACTIVE AGENTS</h2>
      <div className="space-y-4 mb-6">
        {activeSlots.map(slot => (
          <ActiveSlotCard
            key={slot.id}
            slot={slot}
            onEndSession={(id) => endSession(id)}
          />
        ))}
      </div>

      {/* Upgrade CTA */}
      {!tierInfo.isPremium && (
        <LockedSlotCard />
      )}

      {/* Action Bar */}
      <SlotActionBar
        onViewUsage={() => {/* navigate to usage stats */}}
        onBack={() => router.back()}
      />
    </div>
  );
}
```

```typescript
// ActiveSlotCard.tsx
export function ActiveSlotCard({ slot, onEndSession }: ActiveSlotCardProps) {
  const [isEndDialogOpen, setIsEndDialogOpen] = useState(false);

  return (
    <GlowCard className="p-4">
      <div className="flex items-start justify-between">
        <div className="flex items-center gap-3">
          <div className="w-3 h-3 rounded-full bg-[var(--agent-accent-cyan)] animate-agent-neon-pulse" />
          <div>
            <h3 className="agent-heading text-base">{slot.typology.name}</h3>
            <p className="text-sm text-[var(--agent-text-secondary)]">{slot.game.name}</p>
            <p className="text-xs text-[var(--agent-text-muted)]">{slot.model.name}</p>
          </div>
        </div>
        <span className="text-xs text-[var(--agent-text-muted)]">
          Last used: {formatRelativeTime(slot.lastUsedAt)}
        </span>
      </div>

      <div className="flex gap-2 mt-4">
        <NeonButton
          variant="primary"
          size="sm"
          className="flex-1"
          onClick={() => router.push(`/library/games/${slot.game.id}/agent?session=${slot.sessionId}`)}
        >
          Open Chat
        </NeonButton>
        <NeonButton
          variant="ghost"
          size="sm"
          onClick={() => setIsEndDialogOpen(true)}
        >
          End Session
        </NeonButton>
      </div>

      {/* Confirmation Dialog */}
      <AlertDialog open={isEndDialogOpen} onOpenChange={setIsEndDialogOpen}>
        <AlertDialogContent className="bg-[var(--agent-bg-secondary)]">
          <AlertDialogTitle>End Agent Session?</AlertDialogTitle>
          <AlertDialogDescription>
            This will deactivate the agent and free up the slot. Chat history will be preserved.
          </AlertDialogDescription>
          <AlertDialogFooter>
            <AlertDialogCancel>Cancel</AlertDialogCancel>
            <AlertDialogAction onClick={() => onEndSession(slot.id)}>
              End Session
            </AlertDialogAction>
          </AlertDialogFooter>
        </AlertDialogContent>
      </AlertDialog>
    </GlowCard>
  );
}
```

**Acceptance Criteria**:
- ✅ Page displays all active slots correctly
- ✅ Tier badge shows Free/Premium correctly
- ✅ "Open Chat" navigates to chat with session ID
- ✅ "End Session" shows confirmation dialog
- ✅ End session mutation updates UI optimistically
- ✅ SlotActionBar shows correct actions

---

### Issue #FRONT-011: Upgrade Flow & Premium CTA 🟡

**Type**: Frontend - Component
**Priority**: P2 Medium
**Estimate**: 0.5 day
**Assignee**: Frontend Dev 1
**Dependencies**: #FRONT-010

**Description**:
Implementare `LockedSlotCard` con upgrade CTA e pricing modal.

**Tasks**:
- [ ] Create `LockedSlotCard.tsx` component
- [ ] Add premium benefits list (+3 slots, priority models, extended quota)
- [ ] Create upgrade button with purple glow
- [ ] Add pricing modal (future: Stripe integration)
- [ ] Track upgrade click analytics event

**Component**:
```typescript
// LockedSlotCard.tsx
export function LockedSlotCard() {
  const [isPricingOpen, setIsPricingOpen] = useState(false);

  return (
    <>
      <GlowCard className="p-6 border-2 border-[var(--agent-accent-purple)] bg-gradient-to-br from-[var(--agent-bg-secondary)] to-[var(--agent-bg-tertiary)]">
        <div className="flex items-center gap-3 mb-4">
          <Sparkles className="h-6 w-6 text-[var(--agent-accent-purple)]" />
          <h3 className="agent-heading text-lg text-[var(--agent-accent-purple)]">
            PREMIUM TIER
          </h3>
        </div>

        <ul className="space-y-2 mb-6">
          <li className="flex items-center gap-2 text-sm">
            <Check className="h-4 w-4 text-[var(--agent-accent-cyan)]" />
            +3 additional agent slots (5 total)
          </li>
          <li className="flex items-center gap-2 text-sm">
            <Check className="h-4 w-4 text-[var(--agent-accent-cyan)]" />
            Priority model access (GPT-4, Claude-3.5)
          </li>
          <li className="flex items-center gap-2 text-sm">
            <Check className="h-4 w-4 text-[var(--agent-accent-cyan)]" />
            Extended token quota (2000/week)
          </li>
          <li className="flex items-center gap-2 text-sm">
            <Check className="h-4 w-4 text-[var(--agent-accent-cyan)]" />
            Priority support & early features
          </li>
        </ul>

        <NeonButton
          variant="primary"
          className="w-full bg-[var(--agent-accent-purple)] hover:shadow-[var(--agent-glow-purple)]"
          onClick={() => {
            trackEvent('upgrade_cta_clicked', { source: 'slot_management' });
            setIsPricingOpen(true);
          }}
        >
          <Crown className="h-4 w-4 mr-2" />
          Upgrade Now - $9.99/month
        </NeonButton>
      </GlowCard>

      {/* Pricing Modal (future Stripe integration) */}
      <Dialog open={isPricingOpen} onOpenChange={setIsPricingOpen}>
        <DialogContent className="bg-[var(--agent-bg-secondary)]">
          <DialogHeader>
            <DialogTitle className="agent-heading">Upgrade to Premium</DialogTitle>
          </DialogHeader>
          <div className="py-4">
            <p className="text-sm text-[var(--agent-text-secondary)] mb-4">
              Premium tier coming soon! Join the waitlist to get notified.
            </p>
            {/* Future: Stripe pricing table */}
          </div>
        </DialogContent>
      </Dialog>
    </>
  );
}
```

**Acceptance Criteria**:
- ✅ LockedSlotCard shows premium benefits list
- ✅ Upgrade button has purple glow animation
- ✅ Click tracks analytics event
- ✅ Pricing modal opens (placeholder for Stripe)
- ✅ Visual hierarchy emphasizes upgrade value

---

## 🧪 SPRINT 4: Testing & Polish (Week 4)

### Issue #FRONT-012: E2E Tests & Responsive Validation 🔴

**Type**: Testing - E2E
**Priority**: P0 Critical
**Estimate**: 2 days
**Assignee**: Frontend Dev 1 + QA
**Dependencies**: All previous issues

**Description**:
E2E tests completi per flow agent config → chat → slot management + responsive validation.

**Test Plan**:

#### **E2E-FRONT-001: Agent Configuration Flow**
```typescript
// apps/web/__tests__/agent/e2e/agent-config-flow.spec.ts
test('User configures and launches agent successfully', async ({ page }) => {
  // 1. Navigate to library
  await page.goto('/library');

  // 2. Open game detail (with PDF)
  await page.click('[data-testid="game-card-chess"]');

  // 3. Click "Ask Agent" button
  await page.click('[data-testid="ask-agent-button"]');

  // 4. Verify config sheet opens
  await expect(page.locator('[data-testid="agent-config-sheet"]')).toBeVisible();

  // 5. Select game
  await page.locator('[data-testid="game-selector"]').selectOption('Chess');

  // 6. Select template (carousel)
  await page.click('[data-testid="template-card-rules-expert"]');

  // 7. Verify template description updates
  await expect(page.locator('[data-testid="template-description"]')).toContainText('Explains rules');

  // 8. Select model
  await page.locator('[data-testid="model-selector"]').selectOption('GPT-4o-mini');

  // 9. Verify cost estimate shows
  await expect(page.locator('[data-testid="cost-estimate"]')).toContainText('$0.001');

  // 10. Verify token quota displays
  await expect(page.locator('[data-testid="token-quota"]')).toBeVisible();

  // 11. Verify slot cards display
  await expect(page.locator('[data-testid="slot-cards"]')).toBeVisible();

  // 12. Verify launch button enabled
  await expect(page.locator('[data-testid="launch-button"]')).not.toBeDisabled();

  // 13. Launch agent
  await page.click('[data-testid="launch-button"]');

  // 14. Verify chat sheet opens
  await expect(page.locator('[data-testid="agent-chat-sheet"]')).toBeVisible({ timeout: 5000 });

  // 15. Verify header shows game + template
  await expect(page.locator('[data-testid="chat-header"]')).toContainText('Chess - Rules Expert');
});
```

#### **E2E-FRONT-002: Chat Streaming Flow**
```typescript
test('User sends message and receives streamed response', async ({ page }) => {
  // Prerequisite: Agent already configured (use setup helper)
  await setupAgentSession(page, { game: 'Chess', template: 'Rules Expert' });

  // 1. Type message
  await page.locator('[data-testid="chat-input"]').fill('How do pawns move?');

  // 2. Send message
  await page.click('[data-testid="send-button"]');

  // 3. Verify user message appears
  await expect(page.locator('[data-testid="chat-message-user"]').last()).toContainText('How do pawns move?');

  // 4. Verify typing indicator shows
  await expect(page.locator('[data-testid="typing-indicator"]')).toBeVisible();

  // 5. Wait for streaming to start
  await expect(page.locator('[data-testid="chat-message-agent"]').last()).toBeVisible({ timeout: 3000 });

  // 6. Verify streaming cursor animates
  await expect(page.locator('[data-testid="streaming-cursor"]')).toBeVisible();

  // 7. Wait for streaming to complete
  await expect(page.locator('[data-testid="typing-indicator"]')).not.toBeVisible({ timeout: 10000 });

  // 8. Verify agent message contains answer
  const agentMessage = page.locator('[data-testid="chat-message-agent"]').last();
  await expect(agentMessage).toContainText('move');

  // 9. Verify confidence bar displays
  await expect(agentMessage.locator('[data-testid="confidence-bar"]')).toBeVisible();

  // 10. Verify citation badge displays
  await expect(agentMessage.locator('[data-testid="citation-badge"]')).toBeVisible();

  // 11. Click citation
  await agentMessage.locator('[data-testid="citation-badge"]').first().click();
  // Future: Verify PDF opens at page
});
```

#### **E2E-FRONT-003: Slot Management**
```typescript
test('User manages active slots and ends session', async ({ page }) => {
  // Prerequisite: 2 active agent sessions
  await setupMultipleAgentSessions(page, [
    { game: 'Chess', template: 'Rules Expert' },
    { game: 'Catan', template: 'Quick Start' }
  ]);

  // 1. Navigate to slot management
  await page.goto('/library/games/chess/agent/slots');

  // 2. Verify active slots display
  await expect(page.locator('[data-testid="active-slot-card"]')).toHaveCount(2);

  // 3. Verify tier badge
  await expect(page.locator('[data-testid="tier-badge"]')).toContainText('FREE');

  // 4. Verify usage progress bar
  await expect(page.locator('[data-testid="slot-usage-progress"]')).toBeVisible();

  // 5. Click "End Session" on first slot
  await page.locator('[data-testid="active-slot-card"]').first().locator('[data-testid="end-session-button"]').click();

  // 6. Verify confirmation dialog
  await expect(page.locator('[data-testid="end-session-dialog"]')).toBeVisible();

  // 7. Confirm end session
  await page.locator('[data-testid="confirm-end-session"]').click();

  // 8. Verify optimistic UI update (slot count reduces)
  await expect(page.locator('[data-testid="active-slot-card"]')).toHaveCount(1);

  // 9. Verify toast notification
  await expect(page.locator('[data-testid="toast"]')).toContainText('Session ended');

  // 10. Verify usage progress updated
  await expect(page.locator('[data-testid="slot-usage-progress"]')).toHaveAttribute('data-value', '50'); // 1/2 slots
});
```

#### **E2E-FRONT-004: Token Quota Warning**
```typescript
test('User sees quota warning when approaching limit', async ({ page, context }) => {
  // Mock API to return 95% quota usage
  await context.route('**/api/v1/users/me/token-quota', route => {
    route.fulfill({
      status: 200,
      body: JSON.stringify({ used: 475, limit: 500, resetAt: new Date(Date.now() + 2 * 24 * 60 * 60 * 1000) })
    });
  });

  // 1. Open agent config
  await page.goto('/library/games/chess/agent');
  await page.click('[data-testid="ask-agent-button"]');

  // 2. Verify warning displays
  await expect(page.locator('[data-testid="token-quota-warning"]')).toBeVisible();
  await expect(page.locator('[data-testid="token-quota-warning"]')).toContainText('25 tokens until limit');

  // 3. Verify warning icon (yellow)
  await expect(page.locator('[data-testid="warning-icon"]')).toHaveCSS('color', 'rgb(255, 215, 0)'); // --agent-accent-yellow

  // 4. Verify progress bar color (yellow/red)
  const progressBar = page.locator('[data-testid="token-quota-progress"]');
  await expect(progressBar).toHaveClass(/yellow|red/);

  // 5. Verify countdown timer
  await expect(page.locator('[data-testid="quota-reset-timer"]')).toContainText('Resets in');
});
```

#### **E2E-FRONT-005: Responsive Behavior**
```typescript
test.describe('Responsive design validation', () => {
  test('Mobile view - Bottom sheet behavior', async ({ page }) => {
    await page.setViewportSize({ width: 375, height: 667 }); // iPhone SE

    // 1. Open config sheet
    await page.goto('/library/games/chess/agent');
    await page.click('[data-testid="ask-agent-button"]');

    // 2. Verify bottom sheet (90vh height)
    const sheet = page.locator('[data-testid="agent-config-sheet"]');
    await expect(sheet).toHaveCSS('height', '600.3px'); // 90% of 667px

    // 3. Verify rounded top corners
    await expect(sheet).toHaveCSS('border-top-left-radius', '16px');

    // 4. Verify action bar pinned to bottom
    const actionBar = page.locator('[data-testid="action-bar"]');
    await expect(actionBar).toHaveCSS('position', 'sticky');

    // 5. Test swipe-down gesture (if implemented)
    // await sheet.drag({ x: 0, y: 200 });
    // await expect(sheet).not.toBeVisible();
  });

  test('Tablet view - Side sheet behavior', async ({ page }) => {
    await page.setViewportSize({ width: 768, height: 1024 }); // iPad

    await page.goto('/library/games/chess/agent');
    await page.click('[data-testid="ask-agent-button"]');

    // Verify side sheet (right side, 500px width)
    const sheet = page.locator('[data-testid="agent-config-sheet"]');
    await expect(sheet).toHaveCSS('width', '500px');
  });

  test('Desktop view - Modal behavior', async ({ page }) => {
    await page.setViewportSize({ width: 1920, height: 1080 });

    await page.goto('/library/games/chess/agent');
    await page.click('[data-testid="ask-agent-button"]');

    // Verify centered modal (600px max-width)
    const sheet = page.locator('[data-testid="agent-config-sheet"]');
    await expect(sheet).toHaveCSS('max-width', '600px');
  });
});
```

**Unit & Integration Tests Coverage**:
```typescript
// Unit Tests (Vitest)
apps/web/__tests__/agent/unit/
├── AgentConfigSheet.test.tsx       (state management, validation)
├── GameSelector.test.tsx           (dropdown, filtering)
├── TemplateCarousel.test.tsx       (scroll, selection)
├── ModelSelector.test.tsx          (tier filtering)
├── TokenQuotaDisplay.test.tsx      (quota calculation, warnings)
├── SlotCards.test.tsx              (slot rendering, states)
├── ChatMessage.test.tsx            (message variants, timestamps)
├── ChatInput.test.tsx              (input handling, send)
├── ConfidenceBar.test.tsx          (color coding, animation)
├── CitationBadge.test.tsx          (click handler, tooltip)
└── ActionBar.test.tsx              (state-based rendering)

// Integration Tests (React Testing Library + MSW)
apps/web/__tests__/agent/integration/
├── agent-config-flow.test.tsx      (config → launch flow)
├── chat-streaming.test.tsx         (SSE mock, message flow)
├── slot-management.test.tsx        (CRUD operations)
└── quota-tracking.test.tsx         (quota updates, warnings)

// Coverage Targets
- Unit: >85% line coverage
- Integration: >80% critical paths
- E2E: 5 core user journeys
```

**Acceptance Criteria**:
- ✅ 5 E2E tests pass (config, chat, slots, quota, responsive)
- ✅ Unit tests >85% coverage
- ✅ Integration tests >80% coverage
- ✅ Responsive validation: mobile/tablet/desktop
- ✅ SSE streaming tested with mock EventSource
- ✅ No accessibility violations (axe-core)
- ✅ Performance: TTI <3s, FCP <1.5s

**Test Commands**:
```bash
# Unit tests
pnpm test:unit -- __tests__/agent

# Integration tests
pnpm test:integration -- __tests__/agent

# E2E tests
pnpm test:e2e -- agent-*.spec.ts

# Coverage report
pnpm test:coverage -- __tests__/agent

# Accessibility audit
pnpm test:a11y -- /library/games/*/agent
```

---

## 📊 Implementation Timeline

**Week 1: Configuration Flow**
- Day 1: #FRONT-001 (Base setup + routing)
- Day 2-3: #FRONT-002 (Config sheet container)
- Day 3-4: #FRONT-003 (Selectors)
- Day 5: #FRONT-004 (Quota + Slots)
- Day 5: #FRONT-005 (Action bar)

**Week 2: Chat Interface**
- Day 6: #FRONT-006 (Chat container)
- Day 7-8: #FRONT-007 (Messages + streaming)
- Day 8: #FRONT-008 (Citations + confidence)
- Day 9: #FRONT-009 (Chat input + SSE)

**Week 3: Slot Management**
- Day 10-11: #FRONT-010 (Slot page)
- Day 11: #FRONT-011 (Upgrade CTA)

**Week 4: Testing**
- Day 12-13: #FRONT-012 (E2E tests + responsive)
- Day 14: Polish, bug fixes, deployment prep

**Total**: 14 days (3 work weeks) - 1 Frontend Dev

---

## 📦 Dependencies & Prerequisites

### External Dependencies
- ✅ Backend API endpoints (#AGT-001 to #AGT-010) - **90% complete**
- ✅ Design system (shadcn/ui) - **already in project**
- ✅ State management (Zustand) - **already in project**
- ⏳ PDF viewer integration - **future feature**
- ⏳ Stripe payment integration - **future feature**

### Internal Prerequisites
- User must have at least 1 game in library with PDF uploaded
- User must be authenticated
- Browser must support EventSource (SSE) - all modern browsers

---

## 🎯 Success Metrics

**User Experience**:
- ✅ Config flow completion rate >80%
- ✅ Chat engagement >3 messages per session
- ✅ Session duration >5 minutes average

**Technical Performance**:
- ✅ Page load (TTI) <3 seconds
- ✅ SSE connection success rate >95%
- ✅ Mobile responsiveness score >90 (Lighthouse)

**Quality Metrics**:
- ✅ Test coverage >85% (unit + integration)
- ✅ E2E test pass rate 100%
- ✅ Accessibility score >95 (axe-core)
- ✅ Zero critical bugs in production

---

## 🚀 Deployment Checklist

**Pre-Deployment**:
- [ ] All E2E tests passing
- [ ] Performance audit (Lighthouse >90)
- [ ] Accessibility audit (axe-core, no violations)
- [ ] Cross-browser testing (Chrome, Safari, Firefox)
- [ ] Mobile device testing (iOS, Android)
- [ ] API integration validation (staging environment)

**Deployment**:
- [ ] Feature flag enabled (`FEATURE_AGENT_PAGE=true`)
- [ ] Analytics tracking configured
- [ ] Error monitoring (Sentry) configured
- [ ] Performance monitoring (Vercel Analytics)

**Post-Deployment**:
- [ ] Monitor SSE connection errors
- [ ] Track quota warning display rate
- [ ] Monitor upgrade CTA click rate
- [ ] Gather user feedback (first 100 users)

---

**Version**: 1.0
**Status**: Ready for GitHub Issue Creation
**Next Step**: Create GitHub issues with labels and milestones
