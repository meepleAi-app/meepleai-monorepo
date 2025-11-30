# Issue Analysis: Integrazione Layout "Playful Boardroom"

**Date**: 2025-11-30
**Design System**: Opzione A - Playful Boardroom
**Status**: Analysis Complete

---

## 📊 Executive Summary

Analisi delle **100+ issue aperte** per integrarle con il nuovo design system "Playful Boardroom".

**Risultato**:
- ✅ **8 Issue FE-IMP esistenti**: COMPATIBILI, mantenerle con modifiche minori
- ⚠️ **15 Issue BGAI frontend**: AGGIORNARE con nuovi componenti design
- 🆕 **12 Nuove Issue**: Necessarie per implementare layout completo
- ❌ **45 Issue Admin/Infra**: DEFERRED, non impattano layout MVP

---

## 🔍 Analisi Issue Esistenti

### ✅ Issue FE-IMP (001-008): COMPATIBILI

Queste 8 issue sono **architetturali** e COMPATIBILI con qualsiasi design system.

| Issue | Titolo | Status | Action |
|-------|--------|--------|--------|
| FE-IMP-001 | Bootstrap App Router + Shared Providers | KEEP | Aggiungere font Quicksand/Inter in layout |
| FE-IMP-002 | Server Actions per Auth & Export | KEEP | Nessuna modifica necessaria |
| FE-IMP-003 | TanStack Query Data Layer | KEEP | Nessuna modifica necessaria |
| FE-IMP-004 | AuthContext + Edge Middleware | KEEP | Nessuna modifica necessaria |
| FE-IMP-005 | API SDK modulare con Zod | KEEP | Nessuna modifica necessaria |
| FE-IMP-006 | Form System (RHF + Zod) | KEEP | Aggiornare con design tokens Opzione A |
| FE-IMP-007 | Chat Store con Zustand + Streaming | KEEP | Nessuna modifica necessaria |
| FE-IMP-008 | Upload Queue Off-Main-Thread | KEEP | Nessuna modifica necessaria |

**Modifiche Richieste**:
- **FE-IMP-001**: Aggiungere import Google Fonts (Quicksand, Inter) in `app/layout.tsx`
- **FE-IMP-006**: Aggiornare form styling con design tokens Opzione A (beige, orange, purple)

---

### ⚠️ Issue BGAI Frontend: AGGIORNARE

Queste issue richiedono componenti UI → AGGIORNARE acceptance criteria con design Playful Boardroom.

| Issue | Titolo | Update Required |
|-------|--------|-----------------|
| #989 | Base components (Button, Card, Input, Form) | ✅ AGGIORNARE: Usare Opzione A colors, Quicksand headings |
| #1001 | QuestionInputForm component | ✅ AGGIORNARE: Bottom sticky input, meeple icon |
| #1003 | GameSelector dropdown component | ✅ AGGIORNARE: Orange accent, game card design |
| #1004 | Loading and error states (UI/UX) | ✅ AGGIORNARE: Meeple loading animation, bounce effects |
| #1008 | Error handling and retry logic | ✅ AGGIORNARE: Warm error states, beige backgrounds |
| #1013 | PDF viewer integration (react-pdf) | ✅ AGGIORNARE: Styled with Opzione A colors |
| #1014 | Citation click → jump to page | ✅ AGGIORNARE: Orange citation badges |
| #1016 | Complete Italian UI strings | ⚠️ COORDINARE: Tradurre nuovi componenti |
| #1017 | Game catalog page (/board-game-ai/games) | ✅ AGGIORNARE: Hybrid grid/list view, game cards |
| #994 | Frontend build optimization | ✅ COORDINARE: Ottimizzare Quicksand/Inter loading |

**Action Items**:
1. Aggiornare acceptance criteria con reference a `wireframes-playful-boardroom.md`
2. Specificare Shadcn components da usare (Badge, Card, Avatar, etc.)
3. Aggiungere screenshot mockup quando componenti implementati

---

### 🆕 Nuove Issue Necessarie

Componenti/pagine NON coperti dalle issue esistenti.

#### **UI Components (7 nuove issue)**

| ID | Titolo | Descrizione | Priority |
|----|--------|-------------|----------|
| **UI-001** | 🎨 MeepleAvatar Component with States | SVG meeple con 5 stati (idle, thinking, confident, searching, uncertain) + animazioni | P1 |
| **UI-002** | 📱 BottomNav Component (Mobile-First) | Bottom navigation sticky (72px) con 5 tab + active states | P1 |
| **UI-003** | 🎲 GameCard Component (Grid/List variants) | Card component dual-mode: grid (160x220px) + list (full-width) | P1 |
| **UI-004** | 💬 ChatMessage Component (User/AI) | Message bubbles con avatar, confidence badge, citation links | P1 |
| **UI-005** | 🏅 ConfidenceBadge Component | Visual indicator (green/yellow/red) con tooltip explanation | P2 |
| **UI-006** | 📄 CitationLink Component | PDF page reference chip con jump-to-page action | P2 |
| **UI-007** | 🎯 QuickActions Component | Dashboard action cards con icons + hover effects | P2 |

#### **Pages (5 nuove issue)**

| ID | Titolo | Descrizione | Priority |
|----|--------|-------------|----------|
| **PAGE-001** | 🏠 Landing Page (Marketing) | Hero section, features grid, CTA buttons (mobile-first) | P1 |
| **PAGE-002** | 📊 Dashboard Page (Post-Login) | User greeting, recent games, chat history, quick actions | P1 |
| **PAGE-003** | 🎲 Game Catalog Page (Hybrid View) | Grid/List toggle, filters, search, pagination | P1 |
| **PAGE-004** | 💬 Chat Page (Sidebar + Context) | Chat UI con sidebar threads, context chip, streaming | P1 |
| **PAGE-005** | 📖 Game Detail Page (Tabs + Chat) | Hero image, info grid, tabs (overview/FAQ/chat), integrated chat | P2 |

**Totale nuove issue**: 12 (7 components + 5 pages)

---

## 📋 Issue Actions Matrix

### Action: KEEP (Nessuna modifica)

```
FE-IMP-002, FE-IMP-003, FE-IMP-004, FE-IMP-005, FE-IMP-007, FE-IMP-008
```

### Action: UPDATE (Modifica acceptance criteria)

```bash
# Aggiungere reference a wireframes in acceptance criteria
gh issue edit 989 --body "$(cat <<EOF
**UPDATED**: Use Playful Boardroom design system (Opzione A)
- Colors: Primary #F97316 (orange), Secondary #16A34A (green), Accent #A855F7 (purple)
- Typography: Quicksand (headings), Inter (body)
- Reference: docs/04-frontend/wireframes-playful-boardroom.md

**Original Tasks**:
- Button: rounded-lg, py-3 px-6, hover:shadow-lg, active:scale-95
- Card: rounded-2xl, p-6, border-border, hover:shadow-md
- Input: rounded-md, px-3 py-2, focus:ring-primary
- Form: Use FormProvider (RHF + Zod) from FE-IMP-006

**Acceptance**:
- Components match Opzione A colors exactly (verify with design tokens)
- Typography uses Quicksand for headings (font-quicksand class)
- Hover/active states include bounce animations (animate-bounce-subtle)
- WCAG 2.1 AA contrast verified (4.5:1 minimum)
EOF
)"

# Ripetere per issue: 989, 1001, 1003, 1004, 1008, 1013, 1014, 1017
```

### Action: CREATE (Nuove issue)

#### UI-001: MeepleAvatar Component
```markdown
## 🎨 MeepleAvatar Component with States

**Priority**: P1
**Labels**: frontend, component, ui, design-system
**Milestone**: Layout Phase 1

### Description
Implementare componente avatar SVG meeple con 5 stati e animazioni smooth.

### Design Reference
- Wireframes: `docs/04-frontend/wireframes-playful-boardroom.md` (Section: Component Details - AI Avatar States)
- Brainstorm: `docs/04-frontend/improvements/03-brainstorm-ideas.md` (#2.1 AI Avatar & Visual Presence)

### Component Specs
```tsx
interface MeepleAvatarProps {
  state: 'idle' | 'thinking' | 'confident' | 'searching' | 'uncertain';
  size?: 'sm' | 'md' | 'lg'; // 32px, 40px, 48px
  className?: string;
}

const MeepleAvatar: React.FC<MeepleAvatarProps> = ({ state, size = 'md' }) => {
  // SVG meeple icon with state-specific animations
}
```

### Visual States
1. **idle**: Default orange meeple, no animation
2. **thinking**: Pulse animation (opacity 0.7-1.0, 1.5s loop)
3. **confident**: Sparkles overlay, slight glow
4. **searching**: Spinning magnifying glass icon (2s rotation)
5. **uncertain**: Question mark overlay, subtle shake

### Tasks
- [ ] Create SVG meeple base icon (single path, 24x24 viewBox)
- [ ] Implement state animations with CSS keyframes
- [ ] Add size variants (Tailwind classes: w-8 h-8, w-10 h-10, w-12 h-12)
- [ ] Add color variants (primary orange, secondary green)
- [ ] Write Storybook stories for all 5 states
- [ ] Write Jest tests (snapshot + interaction)

### Acceptance Criteria
- ✅ All 5 states render correctly with smooth animations
- ✅ Animations respect `prefers-reduced-motion` (fallback to static)
- ✅ Accessible: aria-label describes current state
- ✅ Storybook story shows all variants in grid
- ✅ Test coverage ≥90%

### Technical Notes
- Use Tailwind `animate-*` utilities where possible
- SVG must be inline (not external file) for color customization
- Primary color: `text-primary` (#F97316)
```

#### UI-002: BottomNav Component
```markdown
## 📱 BottomNav Component (Mobile-First)

**Priority**: P1
**Labels**: frontend, component, navigation, mobile-first
**Milestone**: Layout Phase 1

### Description
Bottom navigation sticky per mobile (<768px) con 5 tab principali.

### Design Reference
- Wireframes: Section "Component Details: Bottom Navigation"
- Mobile height: 72px (`--size-mobile-nav-height`)

### Component Specs
```tsx
interface BottomNavProps {
  currentPath: string;
}

const navItems = [
  { icon: Home, label: 'Home', href: '/dashboard' },
  { icon: Dices, label: 'Giochi', href: '/giochi' },
  { icon: MessageCircle, label: 'Chat', href: '/chat' },
  { icon: Settings, label: 'Config', href: '/settings' },
  { icon: User, label: 'Profilo', href: '/profile' },
];
```

### Visual Design
- Fixed bottom: `z-50`, `shadow-lg`
- Background: `bg-card`, `border-t border-border`
- Active state: `text-primary`, `font-semibold`
- Inactive: `text-muted-foreground`
- Icon: 24x24px
- Label: 10px font-size
- Touch target: 44x44px minimum

### Tasks
- [ ] Create component with 5 nav items
- [ ] Active state logic (current path matching)
- [ ] Hover/active animations (scale, color transition)
- [ ] Hide on desktop (≥768px): `hidden md:hidden`
- [ ] Integrate with App Router (Next.js Link)
- [ ] Storybook story with all routes
- [ ] Playwright E2E test (click navigation)

### Acceptance Criteria
- ✅ Sticky bottom on mobile, hidden on desktop
- ✅ Active tab highlighted with primary orange
- ✅ Smooth transitions (200ms color, transform)
- ✅ Touch targets ≥44px (accessibility)
- ✅ ARIA labels for screen readers
- ✅ Works with keyboard navigation (Tab key)

### Technical Notes
- Use Lucide React icons
- Tailwind utility: `fixed bottom-0 left-0 right-0`
- Z-index: `var(--z-sticky)` (1020)
```

#### UI-003: GameCard Component
```markdown
## 🎲 GameCard Component (Grid/List variants)

**Priority**: P1
**Labels**: frontend, component, ui, games
**Milestone**: Layout Phase 1

### Description
Dual-mode card component: Grid view (compact) + List view (detailed).

### Design Reference
- Wireframes: Page 3 "Catalogo Giochi (Hybrid View)"
- Grid: 2-col mobile, 3-col tablet, 4-col desktop
- List: Full-width horizontal

### Component Specs
```tsx
interface GameCardProps {
  game: Game;
  variant: 'grid' | 'list';
  onSelect?: (gameId: string) => void;
}

type Game = {
  id: string;
  title: string;
  coverImage: string;
  rating: number; // 0-5
  faqCount: number;
  category: string;
  playerCount: string;
};
```

### Visual Design
**Grid Variant** (160x220px mobile):
- aspect-square cover image
- rounded-lg
- shadow-sm, hover:shadow-md
- Title: Quicksand font-semibold 16px
- Rating: Star icons (filled/empty)
- FAQ count badge: "15 FAQ"

**List Variant** (full-width x 80px):
- 48x48px cover (left)
- Title + metadata (center)
- Chevron icon (right)
- border-b separator

### Tasks
- [ ] Create Grid variant component
- [ ] Create List variant component
- [ ] Shared hover/active animations
- [ ] Responsive image loading (next/image)
- [ ] Rating stars component (reusable)
- [ ] Badge component for FAQ count
- [ ] Storybook stories (both variants)
- [ ] Jest tests (render, click handler)

### Acceptance Criteria
- ✅ Both variants render correctly
- ✅ Hover effect: translateY(-4px), shadow-md
- ✅ Active effect: scale(0.97) on press
- ✅ Images lazy-load with blur placeholder
- ✅ Accessible: ARIA labels, keyboard navigable
- ✅ Responsive: grid 2→3→4 cols

### Technical Notes
- Use Next.js Image component
- Transition: 200ms ease-in-out
- Grid gap: 16px (mobile), 24px (desktop)
```

#### PAGE-001: Landing Page
```markdown
## 🏠 Landing Page (Marketing)

**Priority**: P1
**Labels**: frontend, page, marketing, landing
**Milestone**: Layout Phase 1

### Description
Marketing landing page con hero, features, CTA (non-authenticated).

### Design Reference
- Wireframes: "Page 1: Landing Page (Marketing)"
- Mobile-first: 375px base
- Desktop: 2-col layout (hero left, image right)

### Route
- Path: `/` (app/page.tsx)
- Public: No auth required
- SEO: Metadata, og:image

### Page Structure
```tsx
<LandingPage>
  <Navbar sticky /> {/* Logo, Features, Prezzi, Login */}
  <HeroSection>
    <MeepleAvatar size="lg" state="confident" />
    <h1 className="font-quicksand">Il tuo assistente AI per giochi da tavolo</h1>
    <p className="font-inter">Risposte immediate alle regole, in italiano.</p>
    <Button variant="primary">Inizia Gratis →</Button>
    <Button variant="ghost">Scopri di più ↓</Button>
  </HeroSection>
  <FeaturesSection>
    <FeatureCard icon="🤖" title="AI Intelligente" />
    <FeatureCard icon="📚" title="Catalogo Ampio" />
    <FeatureCard icon="📱" title="Mobile-First" />
  </FeaturesSection>
  <HowItWorksSection />
  <Footer />
</LandingPage>
```

### Components Used
- MeepleAvatar (UI-001)
- Button (Shadcn)
- Card (Shadcn)

### Tasks
- [ ] Create app/page.tsx (Server Component wrapper)
- [ ] Implement HeroSection component
- [ ] Implement FeaturesSection (3-col grid)
- [ ] Implement HowItWorksSection (steps 1-2-3)
- [ ] Add responsive breakpoints (mobile → tablet → desktop)
- [ ] Add animations (fadeIn, slideUp)
- [ ] SEO metadata (title, description, og:image)
- [ ] Playwright E2E test (CTA clicks)

### Acceptance Criteria
- ✅ Fully responsive (375px → 1024px+)
- ✅ Lighthouse Performance ≥90
- ✅ Lighthouse Accessibility ≥95
- ✅ CTA buttons link to /register
- ✅ Smooth scroll to sections
- ✅ Animations respect prefers-reduced-motion

### Technical Notes
- Use Next.js metadata API
- Lazy-load hero image (priority="high")
- Font preload: Quicksand, Inter
```

#### PAGE-002: Dashboard Page
```markdown
## 📊 Dashboard Page (Post-Login)

**Priority**: P1
**Labels**: frontend, page, dashboard, authenticated
**Milestone**: Layout Phase 1

### Description
User dashboard con greeting, recent games, chat history, quick actions.

### Design Reference
- Wireframes: "Page 2: Dashboard (Post-Login)"
- Bottom Nav visible (mobile only)

### Route
- Path: `/dashboard` (app/dashboard/page.tsx)
- Protected: Requires auth (middleware redirect)

### Page Structure
```tsx
<DashboardPage>
  <Header>
    <Burger onClick={toggleSidebar} /> {/* Mobile only */}
    <h1>Dashboard</h1>
    <NotificationBell />
    <UserAvatar />
  </Header>

  <Greeting>Ciao, {user.name}! 👋</Greeting>

  <Section title="🎲 Ultimi Giochi">
    <GameCard variant="list" game={catan} />
    <GameCard variant="list" game={wingspan} />
  </Section>

  <Section title="💬 Chat Recenti">
    <ChatHistoryItem />
    <ChatHistoryItem />
  </Section>

  <QuickActions>
    <ActionCard icon="➕" title="Aggiungi Gioco" href="/giochi/nuovo" />
    <ActionCard icon="💬" title="Nuova Chat" href="/chat/nuovo" />
  </QuickActions>

  <BottomNav currentPath="/dashboard" />
</DashboardPage>
```

### Components Used
- GameCard (UI-003)
- QuickActions (UI-007)
- BottomNav (UI-002)

### Tasks
- [ ] Create app/dashboard/page.tsx
- [ ] Implement Greeting component (user name from AuthContext)
- [ ] Implement recent games section (TanStack Query)
- [ ] Implement chat history section (limit 3 recent)
- [ ] Implement QuickActions grid
- [ ] Add loading states (Skeleton components)
- [ ] Add error states (Error boundaries)
- [ ] Playwright E2E test (navigation flow)

### Acceptance Criteria
- ✅ Only accessible when authenticated (middleware redirect)
- ✅ Data loaded via TanStack Query (FE-IMP-003)
- ✅ Loading skeletons shown during fetch
- ✅ Empty states handled gracefully
- ✅ Bottom Nav active on "/dashboard"
- ✅ Quick actions navigate correctly

### Technical Notes
- Use Suspense for streaming
- Prefetch recent games on hover
- Cache: staleTime 5min
```

#### PAGE-003: Game Catalog Page
```markdown
## 🎲 Game Catalog Page (Hybrid View)

**Priority**: P1
**Labels**: frontend, page, games, catalog
**Milestone**: Layout Phase 1

### Description
Game catalog con toggle Grid/List view, filters, search, pagination.

### Design Reference
- Wireframes: "Page 3: Catalogo Giochi (Hybrid View)"
- Grid: 2→3→4 cols responsive
- List: Full-width items

### Route
- Path: `/giochi` (app/giochi/page.tsx)
- Public: Browsable without auth
- Query params: `?view=grid|list&sort=a-z|rating&filter=...`

### Page Structure
```tsx
<GameCatalogPage>
  <Header>
    <BackButton />
    <h1>Giochi</h1>
    <SearchInput />
    <ToggleGroup value={view} onChange={setView}>
      <ToggleItem value="grid" icon={Grid} />
      <ToggleItem value="list" icon={List} />
    </ToggleGroup>
  </Header>

  <FilterBar>
    <Select label="Filtri" options={categories} />
    <Select label="Ordina" options={sortOptions} />
  </FilterBar>

  <GameGrid view={view}>
    {games.map(game => <GameCard variant={view} game={game} />)}
  </GameGrid>

  <Pagination />
  <BottomNav currentPath="/giochi" />
</GameCatalogPage>
```

### Components Used
- GameCard (UI-003)
- BottomNav (UI-002)
- Shadcn: ToggleGroup, Select, Input

### Tasks
- [ ] Create app/giochi/page.tsx
- [ ] Implement view toggle (Grid/List) with URL sync
- [ ] Implement filters (category, player count, duration)
- [ ] Implement sort (A-Z, rating, newest)
- [ ] Implement search with debounce (300ms)
- [ ] Implement pagination (20 games per page)
- [ ] Add loading states (Skeleton grid)
- [ ] Add empty state ("No games found")
- [ ] Playwright E2E test (filter, sort, search)

### Acceptance Criteria
- ✅ View toggle persists in URL (?view=grid)
- ✅ Filters update URL and trigger refetch
- ✅ Search debounced (no request spam)
- ✅ Pagination works (prev/next, page numbers)
- ✅ Grid responsive (2→3→4 cols)
- ✅ List view touch-friendly (44px min height)

### Technical Notes
- Use Next.js searchParams
- TanStack Query with filters as query key
- Infinite scroll (optional Phase 2)
```

#### PAGE-004: Chat Page
```markdown
## 💬 Chat Page (Sidebar + Context)

**Priority**: P1
**Labels**: frontend, page, chat, streaming
**Milestone**: Layout Phase 1

### Description
Chat UI con sidebar threads, context chip, AI streaming responses.

### Design Reference
- Wireframes: "Page 4: Chat AI (con Sidebar + Context)"
- Sidebar: Swipe from left (mobile), persistent (desktop)

### Route
- Path: `/chat` (app/chat/page.tsx)
- Protected: Requires auth
- Dynamic: `/chat/[threadId]`

### Page Structure
```tsx
<ChatPage>
  <Header>
    <Burger onClick={toggleSidebar} />
    <h1>Catan</h1> {/* Current game context */}
    <Menu />
  </Header>

  <Sheet open={sidebarOpen} side="left">
    <ChatThreadList>
      <ThreadItem active />
      <ThreadItem />
      <Button variant="ghost">➕ Nuova Chat</Button>
    </ChatThreadList>
  </Sheet>

  <ContextChip game="Catan">
    <Badge>PDF</Badge>
    <Badge>FAQ 15</Badge>
    <Badge>Wiki</Badge>
    <X onClick={clearContext} />
  </ContextChip>

  <MessageList>
    <ChatMessage role="assistant" state="confident">
      Le risorse si piazzano...
      <CitationLink page={5} />
      <ConfidenceBadge score={0.95} />
    </ChatMessage>
    <ChatMessage role="user">
      E per i deserti?
    </ChatMessage>
    <ChatMessage role="assistant" state="thinking">
      <TypingIndicator />
    </ChatMessage>
  </MessageList>

  <InputArea>
    <AttachButton />
    <Textarea placeholder="Scrivi domanda..." />
    <SendButton />
  </InputArea>

  <ModeToggle>
    <ToggleItem value="fast" icon="⚡">Veloce</ToggleItem>
    <ToggleItem value="complete" icon="🎯">Completa</ToggleItem>
  </ModeToggle>

  <BottomNav currentPath="/chat" />
</ChatPage>
```

### Components Used
- ChatMessage (UI-004)
- MeepleAvatar (UI-001)
- ConfidenceBadge (UI-005)
- CitationLink (UI-006)
- BottomNav (UI-002)
- Shadcn: Sheet, Textarea, Badge

### Tasks
- [ ] Create app/chat/page.tsx
- [ ] Implement sidebar with thread list
- [ ] Implement context chip (game selection)
- [ ] Implement message list (virtualized for performance)
- [ ] Implement streaming hook (useChatStream from FE-IMP-007)
- [ ] Implement input area with auto-resize
- [ ] Implement mode toggle (fast/complete)
- [ ] Add typing indicator animation
- [ ] Playwright E2E test (send message, receive response)

### Acceptance Criteria
- ✅ Sidebar swipeable on mobile, persistent on desktop
- ✅ Messages stream in real-time (SSE)
- ✅ Context chip shows current game
- ✅ Confidence badge shows color-coded score
- ✅ Citation links clickable (future: PDF viewer)
- ✅ Typing indicator animates smoothly
- ✅ Input auto-resizes (max 5 lines)

### Technical Notes
- Use virtual scrolling (react-window) for >100 messages
- SSE endpoint: /api/v1/chat/stream
- Optimistic updates (Zustand store)
```

#### PAGE-005: Game Detail Page
```markdown
## 📖 Game Detail Page (Tabs + Chat)

**Priority**: P2
**Labels**: frontend, page, games, detail
**Milestone**: Layout Phase 2

### Description
Game detail page con hero image, info grid, tabs, integrated chat.

### Design Reference
- Wireframes: "Page 5: Dettaglio Gioco + Chat Integrata"

### Route
- Path: `/giochi/[id]` (app/giochi/[id]/page.tsx)
- Dynamic: Game ID from URL
- Public: Viewable without auth

### Page Structure
```tsx
<GameDetailPage>
  <Header>
    <BackButton />
    <h1>{game.title}</h1>
    <Menu />
    <FavoriteButton />
  </Header>

  <HeroImage src={game.coverImage} alt={game.title} />

  <GameInfo>
    <h1 className="font-quicksand">{game.title}</h1>
    <Rating value={game.rating} />
    <Badge>{game.faqCount} FAQ</Badge>
  </GameInfo>

  <InfoGrid>
    <InfoItem label="Players" value="3-4" />
    <InfoItem label="Time" value="60-120 min" />
    <InfoItem label="Difficulty" value="●●○○○" />
  </InfoGrid>

  <Tabs defaultValue="chat">
    <TabsList>
      <TabsTrigger value="overview">Panoramica</TabsTrigger>
      <TabsTrigger value="faq">FAQ</TabsTrigger>
      <TabsTrigger value="chat">Chat</TabsTrigger>
    </TabsList>

    <TabsContent value="overview">
      <Description>{game.description}</Description>
    </TabsContent>

    <TabsContent value="faq">
      <FAQList questions={game.faqs} />
    </TabsContent>

    <TabsContent value="chat">
      <ChatSection gameId={game.id}>
        {/* Reuse chat components from PAGE-004 */}
        <QuickQuestions>
          <Chip>Come funziona il setup?</Chip>
          <Chip>Regole costruzione</Chip>
        </QuickQuestions>
      </ChatSection>
    </TabsContent>
  </Tabs>

  <InputArea /> {/* Chat input */}
  <BottomNav currentPath="/giochi" />
</GameDetailPage>
```

### Components Used
- GameCard (UI-003) - for related games
- ChatMessage (UI-004) - in chat tab
- BottomNav (UI-002)
- Shadcn: Tabs, Badge, Avatar

### Tasks
- [ ] Create app/giochi/[id]/page.tsx
- [ ] Implement hero image section (aspect-video)
- [ ] Implement info grid (responsive 3-col)
- [ ] Implement tabs (overview, FAQ, chat)
- [ ] Integrate chat section (reuse chat components)
- [ ] Add quick questions chips (FAQ shortcuts)
- [ ] Add related games section (carousel)
- [ ] Playwright E2E test (tab navigation, chat)

### Acceptance Criteria
- ✅ Dynamic game ID loading
- ✅ Hero image responsive (16:9 aspect ratio)
- ✅ Tabs work with keyboard navigation
- ✅ Chat tab functional (send/receive messages)
- ✅ Quick questions clickable (populate input)
- ✅ Related games carousel swipeable

### Technical Notes
- Use Next.js generateStaticParams for popular games
- Prefetch related games
- Chat state isolated per game (Zustand slice)
```

---

## 🗂️ Issue Organization Strategy

### Milestone Structure
```
Milestone: Layout Phase 1 (Weeks 1-4)
├─ UI-001: MeepleAvatar Component (Week 1)
├─ UI-002: BottomNav Component (Week 1)
├─ UI-003: GameCard Component (Week 2)
├─ UI-004: ChatMessage Component (Week 2)
├─ PAGE-001: Landing Page (Week 3)
├─ PAGE-002: Dashboard Page (Week 3)
├─ PAGE-003: Game Catalog Page (Week 4)
└─ PAGE-004: Chat Page (Week 4)

Milestone: Layout Phase 2 (Weeks 5-6)
├─ UI-005: ConfidenceBadge Component
├─ UI-006: CitationLink Component
├─ UI-007: QuickActions Component
└─ PAGE-005: Game Detail Page
```

### Label Strategy
```
Labels per category:
- frontend, component, ui → UI components
- frontend, page → Full pages
- design-system → Design token usage
- mobile-first → Mobile-specific features
- accessibility → A11y focus
- priority-high, priority-medium, priority-low
```

### Dependencies Graph
```
FE-IMP-001 (App Router)
  ↓
FE-IMP-006 (Forms) + UI-001 (MeepleAvatar) + UI-002 (BottomNav)
  ↓
PAGE-001 (Landing) + PAGE-002 (Dashboard)
  ↓
UI-003 (GameCard) → PAGE-003 (Catalog)
  ↓
FE-IMP-007 (Zustand) + UI-004 (ChatMessage)
  ↓
PAGE-004 (Chat)
  ↓
UI-005,006,007 → PAGE-005 (Game Detail)
```

---

## 📊 Priority Matrix

| Priority | Count | Focus |
|----------|-------|-------|
| P1 (High) | 9 issues | Core MVP (Landing, Dashboard, Catalog, Chat, 4 components) |
| P2 (Medium) | 3 issues | Enhanced UX (Game Detail, 3 components) |
| P3 (Low) | 0 issues | - |

**P1 Delivery Target**: 4 weeks (MVP functional)
**P2 Delivery Target**: 2 weeks (Enhanced UX)
**Total**: 6 weeks for complete layout implementation

---

## 🎯 Implementation Roadmap

### Week 1: Foundation
- ✅ FE-IMP-001: App Router setup (font integration)
- 🆕 UI-001: MeepleAvatar Component
- 🆕 UI-002: BottomNav Component

### Week 2: Core Components
- 🆕 UI-003: GameCard Component (Grid/List)
- 🆕 UI-004: ChatMessage Component
- ✅ FE-IMP-006: Form System (updated with Opzione A)

### Week 3: Public Pages
- 🆕 PAGE-001: Landing Page (Marketing)
- 🆕 PAGE-002: Dashboard Page
- ✅ FE-IMP-003: TanStack Query integration

### Week 4: Interactive Pages
- 🆕 PAGE-003: Game Catalog Page
- 🆕 PAGE-004: Chat Page
- ✅ FE-IMP-007: Zustand Chat Store

### Week 5-6: Enhanced UX
- 🆕 UI-005, UI-006, UI-007: Remaining components
- 🆕 PAGE-005: Game Detail Page
- 📝 Storybook documentation
- ✅ Responsive testing + A11y audit

---

## ✅ Next Actions

### Immediate (Do Now)
1. **Update existing BGAI issues** (#989, #1001, #1003, #1004, #1008, #1013, #1014, #1017)
   - Add reference to `wireframes-playful-boardroom.md`
   - Specify Opzione A colors in acceptance criteria
   - Add Shadcn components to use

2. **Create 12 new issues** (UI-001 to PAGE-005)
   - Use templates above
   - Apply labels + milestone
   - Set dependencies

3. **Update FE-IMP-001**
   - Add font integration task (Quicksand, Inter)

4. **Update FE-IMP-006**
   - Specify Opzione A design tokens for forms

### Short-term (This Week)
1. Start Week 1 implementation (FE-IMP-001 + UI-001 + UI-002)
2. Setup Storybook for component showcase
3. Configure Playwright for visual regression

### Medium-term (Next 2 Weeks)
1. Implement core components (UI-003, UI-004)
2. Build public pages (PAGE-001, PAGE-002)
3. Integrate TanStack Query + Zustand

---

## 📝 GitHub Issue Creation Commands

### Batch Create Commands
```bash
# UI Components (7 issues)
gh issue create --title "🎨 [UI-001] MeepleAvatar Component with States" \
  --label "frontend,component,ui,design-system,priority-high" \
  --milestone "Layout Phase 1" \
  --body-file docs/04-frontend/issue-templates/ui-001-meeple-avatar.md

gh issue create --title "📱 [UI-002] BottomNav Component (Mobile-First)" \
  --label "frontend,component,navigation,mobile-first,priority-high" \
  --milestone "Layout Phase 1" \
  --body-file docs/04-frontend/issue-templates/ui-002-bottom-nav.md

# Repeat for UI-003 to UI-007...

# Pages (5 issues)
gh issue create --title "🏠 [PAGE-001] Landing Page (Marketing)" \
  --label "frontend,page,marketing,landing,priority-high" \
  --milestone "Layout Phase 1" \
  --body-file docs/04-frontend/issue-templates/page-001-landing.md

# Repeat for PAGE-002 to PAGE-005...
```

### Update Existing Issues
```bash
# Update BGAI issues with design reference
for issue in 989 1001 1003 1004 1008 1013 1014 1017; do
  gh issue edit $issue --add-label "design-system,playful-boardroom"
  gh issue comment $issue --body "**Design Update**: This component should follow Playful Boardroom design system (Opzione A). Reference: \`docs/04-frontend/wireframes-playful-boardroom.md\`"
done
```

---

## 🎯 Success Metrics

### Code Metrics
- ✅ Test coverage ≥90% (components + pages)
- ✅ Lighthouse Performance ≥90
- ✅ Lighthouse Accessibility ≥95
- ✅ Bundle size <500KB (first load)

### Design Metrics
- ✅ All components match wireframes exactly
- ✅ Color contrast WCAG 2.1 AA (4.5:1)
- ✅ Typography consistent (Quicksand + Inter)
- ✅ Animations smooth (60fps, respects prefers-reduced-motion)

### Functional Metrics
- ✅ Mobile-first responsive (375px → 1024px+)
- ✅ Bottom Nav functional on all pages
- ✅ Chat streaming works (SSE integration)
- ✅ Game catalog filters/search functional

---

**Version**: 1.0
**Date**: 2025-11-30
**Status**: Analysis Complete - Ready for Issue Creation
**Next**: Create 12 new issues + Update 10 existing
