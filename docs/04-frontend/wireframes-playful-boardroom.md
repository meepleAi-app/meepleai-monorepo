# MeepleAI - Mid-Fidelity Wireframes (Opzione A: Playful Boardroom)

**Date**: 2025-11-30
**Design System**: Opzione A - Ludico ed Elegante
**Mobile-First**: 375px base → 768px tablet → 1024px+ desktop
**Bottom Navigation**: Primary actions always accessible

---

## 🎨 Design Tokens Applied

```css
/* Colors */
--primary: #F97316        /* Orange Catan */
--secondary: #16A34A      /* Green Carcassonne */
--accent: #A855F7         /* Purple games */
--background: #F9F7F4     /* Beige chiaro */
--foreground: #2B241F     /* Marrone scuro */

/* Typography */
Headings: Quicksand (rounded, friendly)
Body: Inter (readable, modern)
Code: JetBrains Mono

/* Spacing */
Mobile base: 16px (p-4)
Card padding: 24px (p-6)
Section spacing: 32px (space-y-8)
```

---

## 📱 Page 1: Landing Page (Marketing)

### Mobile (375px)
```
┌─────────────────────────────────────────┐
│ [☰] MeepleAI           [Accedi] [👤]   │ ← Navbar sticky (56px)
├─────────────────────────────────────────┤
│                                         │
│        🎲 [MeepleAI Logo]               │ ← Meeple mascot SVG
│     (200x200px, orange gradient)        │
│                                         │
│   Il tuo assistente AI                  │ ← H1, Quicksand 30px
│   per giochi da tavolo                  │
│                                         │
│   Risposte immediate alle regole,       │ ← Body, Inter 16px
│   in italiano. Sempre con te.           │
│                                         │
│  ┌───────────────────────────────────┐  │
│  │   Inizia Gratis →                 │  │ ← CTA primary
│  │   bg-primary, rounded-lg, py-3    │  │
│  └───────────────────────────────────┘  │
│                                         │
│  [Scopri di più ↓]                      │ ← Ghost button
│                                         │
├─────────────────────────────────────────┤
│  ✨ Caratteristiche                     │ ← Section heading
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ 🤖                               │   │ ← Feature card
│  │ AI Intelligente                  │   │   bg-card, p-6
│  │ Risposte immediate con           │   │   rounded-xl
│  │ citazioni dal manuale            │   │   shadow-sm
│  └─────────────────────────────────┘   │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ 📚                               │   │
│  │ Catalogo Ampio                   │   │
│  │ Migliaia di giochi               │   │
│  │ già disponibili                  │   │
│  └─────────────────────────────────┘   │
│                                         │
│  ┌─────────────────────────────────┐   │
│  │ 📱                               │   │
│  │ Mobile-First                     │   │
│  │ Perfetto durante                 │   │
│  │ le partite                       │   │
│  └─────────────────────────────────┘   │
│                                         │
├─────────────────────────────────────────┤
│  Come funziona                          │ ← Section
│  ─────────────────────                  │
│  1️⃣ Scegli il gioco                    │
│  2️⃣ Fai una domanda                    │
│  3️⃣ Ottieni risposta citata            │
│                                         │
├─────────────────────────────────────────┤
│  [Registrati Ora]  [Accedi]             │ ← Footer CTA
│  Privacy | Termini | API Docs           │
└─────────────────────────────────────────┘
```

### Tablet/Desktop (768px+)
```
┌────────────────────────────────────────────────────────────┐
│ [Logo] MeepleAI        Funzionalità  Prezzi  [Accedi] [👤] │
├────────────────────────────────────────────────────────────┤
│                                                            │
│  ┌──────────────────────┬──────────────────────┐          │
│  │                      │                      │          │
│  │   🎲 MeepleAI        │   [Hero Image]       │ ← 2 col
│  │   (300x300px)        │   Giocatori al       │   layout
│  │                      │   tavolo con phone   │
│  │   Il tuo assistente  │                      │
│  │   AI per giochi      │                      │
│  │   da tavolo          │                      │
│  │                      │                      │
│  │   [Inizia Gratis →]  │                      │
│  │   [Demo Video ▶]     │                      │
│  │                      │                      │
│  └──────────────────────┴──────────────────────┘          │
│                                                            │
│  ✨ Caratteristiche                                       │
│  ┌──────────┬──────────┬──────────┐                       │
│  │ 🤖      │ 📚      │ 📱      │ ← 3-col grid             │
│  │ AI      │ Catalogo│ Mobile   │   gap-6                │
│  │         │         │          │                         │
│  └──────────┴──────────┴──────────┘                       │
│                                                            │
└────────────────────────────────────────────────────────────┘
```

---

## 📱 Page 2: Dashboard (Post-Login)

### Mobile (375px)
```
┌─────────────────────────────────────────┐
│ [≡] Dashboard          [🔔] [👤]        │ ← Top bar (56px)
├─────────────────────────────────────────┤
│  Ciao, Marco! 👋                        │ ← Greeting, p-4
│                                         │
│  🎲 Ultimi Giochi                       │ ← Section heading
│  ─────────────────────                  │   text-lg, font-semibold
│  ┌─────────────────────────────────┐   │
│  │ [IMG]  Catan               [→]  │   │ ← Game card
│  │ ★★★★☆                          │   │   horizontal
│  │ 2h fa • 3 domande               │   │   touch-friendly
│  └─────────────────────────────────┘   │
│  ┌─────────────────────────────────┐   │
│  │ [IMG]  Wingspan            [→]  │   │
│  │ ★★★★★                          │   │
│  │ Ieri • 1 domanda                │   │
│  └─────────────────────────────────┘   │
│                                         │
│  💬 Chat Recenti                        │
│  ─────────────────────                  │
│  • "Come si piazzano i..." (Catan)      │
│  • "Regola movimento..." (Wingspan)     │
│  • "Punteggio finale..." (7 Wonders)    │
│                                         │
│  ┌───────────────────────────────────┐  │
│  │ ➕ Aggiungi Gioco                │  │ ← Quick actions
│  └───────────────────────────────────┘  │   gap-3
│  ┌───────────────────────────────────┐  │
│  │ 💬 Nuova Chat                     │  │
│  └───────────────────────────────────┘  │
│                                         │
├─────────────────────────────────────────┤
│ [🏠] [🎲] [💬] [⚙️] [👤]               │ ← Bottom Nav (72px)
│ Home Giochi Chat Config Profilo         │   sticky, z-50
└─────────────────────────────────────────┘   bg-card, shadow-lg
```

### Component Details: Bottom Navigation
```css
.bottom-nav {
  position: fixed;
  bottom: 0;
  left: 0;
  right: 0;
  height: 72px; /* --size-mobile-nav-height */
  background: hsl(var(--card));
  border-top: 1px solid hsl(var(--border));
  box-shadow: var(--shadow-lg);
  z-index: var(--z-sticky);
  display: flex;
  justify-content: space-around;
  align-items: center;
  padding: 8px 16px;
}

.nav-item {
  display: flex;
  flex-direction: column;
  align-items: center;
  gap: 4px;
  min-width: 44px; /* Touch target */
  min-height: 44px;
  color: hsl(var(--muted-foreground));
  transition: color 200ms;
}

.nav-item-active {
  color: hsl(var(--primary));
  font-weight: 600;
}

.nav-icon {
  width: 24px;
  height: 24px;
}

.nav-label {
  font-size: 10px;
  font-family: Inter;
}
```

---

## 📱 Page 3: Catalogo Giochi (Hybrid View)

### Mobile - Grid View (default)
```
┌─────────────────────────────────────────┐
│ [←] Giochi       [🔍 Search]  [≣/▦]    │ ← Header with toggle
├─────────────────────────────────────────┤
│ [Filtri ▼] [Ordina: A-Z ▼]             │ ← Filters row
├─────────────────────────────────────────┤
│                                         │
│ GRID VIEW (2 colonne):                  │
│ ┌────────────┐ ┌────────────┐          │
│ │ [IMG]      │ │ [IMG]      │          │ ← Game cards
│ │            │ │            │          │   aspect-square
│ │ Catan      │ │ Wingspan   │          │   rounded-lg
│ │ ★★★★☆     │ │ ★★★★★     │          │   shadow-sm
│ │ 15 FAQ     │ │ 8 FAQ      │          │   hover:shadow-md
│ └────────────┘ └────────────┘          │
│ ┌────────────┐ ┌────────────┐          │
│ │ [IMG]      │ │ [IMG]      │          │
│ │ Azul       │ │ 7 Wonders  │          │
│ │ ★★★★☆     │ │ ★★★★☆     │          │
│ │ 12 FAQ     │ │ 20 FAQ     │          │
│ └────────────┘ └────────────┘          │
│                                         │
│ [Load more...]                          │
│                                         │
├─────────────────────────────────────────┤
│ [🏠] [🎲] [💬] [⚙️] [👤]               │ ← Bottom Nav
└─────────────────────────────────────────┘
```

### Mobile - List View (swipe left to toggle)
```
┌─────────────────────────────────────────┐
│ [←] Giochi       [🔍 Search]  [≣/▦]    │
├─────────────────────────────────────────┤
│ [Filtri ▼] [Ordina: A-Z ▼]             │
├─────────────────────────────────────────┤
│                                         │
│ LIST VIEW:                              │
│ ┌─────────────────────────────────────┐ │
│ │ [IMG] Catan                    [→]  │ │ ← List item
│ │ 48x48  ★★★★☆ • 15 FAQ              │ │   p-3
│ │       Strategia • 3-4 player        │ │   border-b
│ └─────────────────────────────────────┘ │
│ ┌─────────────────────────────────────┐ │
│ │ [IMG] Wingspan                 [→]  │ │
│ │       ★★★★★ • 8 FAQ                │ │
│ │       Motore • 1-5 player           │ │
│ └─────────────────────────────────────┘ │
│ ┌─────────────────────────────────────┐ │
│ │ [IMG] Azul                     [→]  │ │
│ │       ★★★★☆ • 12 FAQ               │ │
│ │       Astratto • 2-4 player         │ │
│ └─────────────────────────────────────┘ │
│                                         │
├─────────────────────────────────────────┤
│ [🏠] [🎲] [💬] [⚙️] [👤]               │
└─────────────────────────────────────────┘
```

### Tablet/Desktop (768px+)
```
┌────────────────────────────────────────────────────────────┐
│ Giochi            [Search bar wide]        [Grid] [List]   │
├────────────────────────────────────────────────────────────┤
│ ┌──────────┬──────────┐ [Filtri]                          │
│ │          │          │ ┌─────────────────┐               │
│ │  Sidebar │  Content │ │ Categoria       │               │
│ │  Filters │  Grid    │ │ □ Strategia     │               │
│ │          │          │ │ □ Famiglia      │               │
│ │ 256px    │  Flex-1  │ │ □ Party         │               │
│ │          │          │ │                 │               │
│ │ • Tutto  │  3x grid │ │ Player Count    │               │
│ │ • Nuovi  │  ┌──┬──┬──┐│ │ □ 1-2          │               │
│ │ • Top    │  │  │  │  ││ │ □ 3-4          │               │
│ │ • Popolari│ ├──┼──┼──┤│ │ □ 5+           │               │
│ │          │  │  │  │  ││ └─────────────────┘               │
│ │          │  └──┴──┴──┘│                                   │
│ │          │          │                                   │
│ └──────────┴──────────┘                                   │
└────────────────────────────────────────────────────────────┘
```

---

## 📱 Page 4: Chat AI (con Sidebar + Context)

### Mobile (375px)
```
┌─────────────────────────────────────────┐
│ [☰] Catan             [⋮] [👤]         │ ← Header (burger, menu)
├─────────────────────────────────────────┤
│                                         │
│ [SIDEBAR - Swipe from left overlay]     │
│ ┌──────────────────────────────────┐    │
│ │ 💬 Thread Recenti             [×]│    │ ← Sheet/Drawer
│ │ ───────────────────────────────  │    │   w-80 (320px)
│ │ • Catan - Setup (active)         │    │   bg-card
│ │ • Wingspan - Turni               │    │   shadow-2xl
│ │ • 7 Wonders - Punteggi           │    │
│ │ ───────────────────────────────  │    │
│ │ ➕ Nuova Chat                    │    │
│ └──────────────────────────────────┘    │
│                                         │
│ [MAIN CHAT AREA]                        │
│ ┌─────────────────────────────────────┐ │
│ │ 🎲 Context: Catan              [×] │ │ ← Context chip
│ │ [PDF] [FAQ 15] [Wiki]              │ │   bg-accent/10
│ └─────────────────────────────────────┘ │   border-accent
│                                         │
│ ┌──────────────────────────────────┐    │
│ │ 🤖 MeepleAI                      │    │ ← AI message
│ │ Le risorse si piazzano sui       │    │   bg-muted
│ │ territori adiacenti agli         │    │   rounded-lg
│ │ insediamenti...                  │    │   p-4
│ │                                  │    │
│ │ [📄 Regolamento p.5]             │    │ ← Citation
│ │ Confidence: 95% ●●●●●○           │    │   inline
│ └──────────────────────────────────┘    │
│                                         │
│ ┌──────────────────────────────────┐    │
│ │           Tu 👤                  │    │ ← User message
│ │ E per i deserti?                 │    │   bg-primary/10
│ │                                  │    │   ml-auto
│ └──────────────────────────────────┘    │   rounded-lg
│                                         │
│ ┌──────────────────────────────────┐    │
│ │ 🤖 MeepleAI (typing...)          │    │ ← Typing indicator
│ │ ●●● (animated dots)              │    │
│ └──────────────────────────────────┘    │
│                                         │
│ ─ Scroll to load more ─                 │
│                                         │
├─────────────────────────────────────────┤
│ [📎] [Scrivi domanda...        ] [↑]   │ ← Input sticky
│                                         │   h-16 (64px)
│ [⚡Veloce] [🎯Completa]                │   bg-card
├─────────────────────────────────────────┤   shadow-lg
│ [🏠] [🎲] [💬] [⚙️] [👤]               │
└─────────────────────────────────────────┘
```

### Component Details: AI Avatar States
```
🤖 States:
┌──────────────────────────────┐
│ Default (Idle)               │
│ [Meeple icon, orange]        │
└──────────────────────────────┘

┌──────────────────────────────┐
│ Thinking 🤔                  │
│ [Meeple + dots animation]    │
│ animate-pulse                │
└──────────────────────────────┘

┌──────────────────────────────┐
│ Confident ✨                 │
│ [Meeple + sparkles]          │
│ Confidence ≥85%              │
└──────────────────────────────┘

┌──────────────────────────────┐
│ Searching 🔍                 │
│ [Meeple + magnifying glass]  │
│ animate-spin-slow            │
└──────────────────────────────┘

┌──────────────────────────────┐
│ Uncertain ⚠️                 │
│ [Meeple + question mark]     │
│ Confidence <70%              │
└──────────────────────────────┘
```

---

## 📱 Page 5: Dettaglio Gioco + Chat Integrata

### Mobile (375px)
```
┌─────────────────────────────────────────┐
│ [←] Catan                   [⋮] [★]    │ ← Header
├─────────────────────────────────────────┤
│ [Hero Image - Catan Box]                │ ← aspect-video
│ ┌─────────────────────────────────────┐ │   w-full
│ │                                     │ │   object-cover
│ │   [Catan Cover 16:9]                │ │
│ │                                     │ │
│ └─────────────────────────────────────┘ │
│                                         │
│ 🎲 I Coloni di Catan                    │ ← Title
│ ★★★★☆ (4.2) • 15 FAQ disponibili       │   Quicksand 24px
│                                         │
│ 📊 Info Rapide                          │
│ ┌──────┬──────┬──────┐                  │ ← Grid 3 col
│ │ 3-4  │60-120│ ●●○○○│                  │
│ │Player│ min  │ Diff │                  │
│ └──────┴──────┴──────┘                  │
│                                         │
│ [Tabs]                                  │ ← Shadcn Tabs
│ ┌─────────────────────────────────────┐ │
│ │ Panoramica | FAQ | Chat             │ │
│ ├─────────────────────────────────────┤ │
│ │ [ACTIVE TAB: Chat]                  │ │
│ │                                     │ │
│ │ 💬 Chatta su Catan                  │ │
│ │                                     │ │
│ │ 🤖: Ciao! Chiedi pure su Catan      │ │
│ │                                     │ │
│ │ Domande frequenti:                  │ │
│ │ • Come funziona il setup iniziale?  │ │
│ │ • Regole costruzione strade         │ │
│ │ • Scambio risorse con altri         │ │
│ │                                     │ │
│ └─────────────────────────────────────┘ │
│                                         │
├─────────────────────────────────────────┤
│ [Scrivi domanda...              ]  [↑] │ ← Input
├─────────────────────────────────────────┤
│ [🏠] [🎲] [💬] [⚙️] [👤]               │
└─────────────────────────────────────────┘
```

### Tablet/Desktop (1024px+)
```
┌────────────────────────────────────────────────────────────┐
│ [←] Catan                            [Favoriti] [Condividi]│
├────────────────────────────────────────────────────────────┤
│ ┌──────────────────────┬─────────────────────────────────┐ │
│ │                      │                                 │ │
│ │ [Hero Image]         │ I Coloni di Catan               │ │
│ │ 400x300px            │ ★★★★☆ (4.2) • 15 FAQ           │ │
│ │                      │                                 │ │
│ │                      │ 📊 Info                        │ │
│ │                      │ Players: 3-4                    │ │
│ │                      │ Time: 60-120 min                │ │
│ │                      │ Difficulty: ●●○○○              │ │
│ │                      │                                 │ │
│ │                      │ Descrizione:                    │ │
│ │                      │ Lorem ipsum dolor sit amet...   │ │
│ │                      │                                 │ │
│ └──────────────────────┴─────────────────────────────────┘ │
│                                                            │
│ [Tabs: Panoramica | FAQ | Chat | Regolamento]             │
│ ┌──────────────────────────────────────────────────────┐   │
│ │ [ACTIVE: Chat]                                       │   │
│ │                                                      │   │
│ │ ┌────────────────┬──────────────────────────────┐   │   │
│ │ │ Chat History   │ Current Conversation         │   │   │
│ │ │ (Sidebar)      │ (Main area)                  │   │   │
│ │ │ 280px          │ Flex-1                       │   │   │
│ │ │                │                              │   │   │
│ │ │ • Thread 1     │ 🤖: Ciao! Come posso        │   │   │
│ │ │ • Thread 2     │     aiutarti con Catan?     │   │   │
│ │ │ • Thread 3     │                              │   │   │
│ │ │                │ [Domande frequenti grid]     │   │   │
│ │ │ ➕ Nuovo       │                              │   │   │
│ │ │                │                              │   │   │
│ │ └────────────────┴──────────────────────────────┘   │   │
│ │                                                      │   │
│ │ [Input area with attachments]                        │   │
│ └──────────────────────────────────────────────────────┘   │
└────────────────────────────────────────────────────────────┘
```

---

## 🎨 Component Library Mapping (Shadcn/UI)

### Required Components

| Component | Usage | Variant |
|-----------|-------|---------|
| **Button** | CTAs, actions | primary, secondary, ghost, outline |
| **Card** | Game cards, chat messages, feature cards | default, hover:shadow-md |
| **Badge** | Game tags, confidence, FAQ count | default, secondary, outline |
| **Avatar** | User profile, AI avatar | circular, fallback |
| **Input** | Search, chat input | default, with-icon |
| **Select** | Filters, sorting | default |
| **Tabs** | Game detail sections | default, underline |
| **Sheet** | Mobile sidebar/drawer | left, right |
| **Dialog** | Modals, confirmations | centered |
| **Toggle** | Grid/List view | default |
| **Separator** | Section dividers | horizontal, vertical |
| **Skeleton** | Loading states | default |
| **ScrollArea** | Chat messages, game list | vertical |
| **DropdownMenu** | User menu, game actions | default |

### Custom Components to Build

1. **GameCard** (Grid/List variants)
2. **ChatMessage** (User/AI variants with avatar)
3. **MeepleAvatar** (SVG with states)
4. **BottomNav** (Mobile primary navigation)
5. **ConfidenceBadge** (Visual confidence indicator)
6. **CitationLink** (PDF page reference)
7. **QuickActions** (Dashboard action cards)

---

## 📐 Spacing & Sizing Standards

### Mobile Touch Targets
- Minimum: 44x44px (`--size-touch-min`)
- Buttons: 48px height
- Bottom Nav items: 56x56px area

### Card Sizes
- Game Card Grid: 160x220px (mobile 2-col)
- Game Card List: Full width x 80px height
- Feature Card: Full width x 120px min
- Chat Message: Max 85% width (user), 90% (AI)

### Padding Hierarchy
```css
/* Page container */
padding: var(--space-6); /* 24px */

/* Section */
padding: var(--space-8) var(--space-6); /* 32px 24px */

/* Card */
padding: var(--space-6); /* 24px */

/* Compact (list items) */
padding: var(--space-3); /* 12px */

/* Dense (badges) */
padding: var(--space-2) var(--space-3); /* 8px 12px */
```

---

## 🔄 Animations & Micro-interactions

### Button Press (Playful)
```css
@keyframes bounce-subtle {
  0%, 100% {
    transform: scale(1);
  }
  50% {
    transform: scale(0.97);
  }
}

.btn-primary:active {
  animation: bounce-subtle 150ms ease-in-out;
}
```

### Card Hover
```css
.game-card {
  transition: all 200ms ease-in-out;
}

.game-card:hover {
  transform: translateY(-4px);
  box-shadow: var(--shadow-md);
}
```

### Avatar Thinking State
```css
@keyframes pulse-meeple {
  0%, 100% {
    opacity: 1;
    transform: scale(1);
  }
  50% {
    opacity: 0.7;
    transform: scale(1.05);
  }
}

.meeple-thinking {
  animation: pulse-meeple 1.5s ease-in-out infinite;
}
```

### Typing Indicator
```css
@keyframes dot-bounce {
  0%, 80%, 100% {
    transform: translateY(0);
  }
  40% {
    transform: translateY(-8px);
  }
}

.typing-dot:nth-child(1) {
  animation: dot-bounce 1.4s infinite;
}
.typing-dot:nth-child(2) {
  animation: dot-bounce 1.4s 0.2s infinite;
}
.typing-dot:nth-child(3) {
  animation: dot-bounce 1.4s 0.4s infinite;
}
```

---

## ♿ Accessibility Checklist

- ✅ **Color Contrast**: WCAG 2.1 AA (4.5:1 body, 3:1 large text)
- ✅ **Touch Targets**: 44x44px minimum
- ✅ **Focus Indicators**: 2px ring with offset
- ✅ **ARIA Labels**: All interactive elements
- ✅ **Semantic HTML**: Proper heading hierarchy
- ✅ **Keyboard Navigation**: Tab order logical
- ✅ **Screen Reader**: Live regions for chat
- ✅ **Reduced Motion**: Respect `prefers-reduced-motion`

---

## 📱 Responsive Breakpoints

| Breakpoint | Width | Layout Changes |
|------------|-------|----------------|
| Mobile (base) | 375px - 767px | Single column, bottom nav, stack cards |
| Tablet | 768px - 1023px | 2-3 columns, persistent sidebar option |
| Desktop | 1024px+ | 3-4 columns, always-visible sidebar, wider content |

### Grid Configurations
```css
/* Game Catalog */
mobile: grid-cols-2, gap-4
tablet: grid-cols-3, gap-6
desktop: grid-cols-4, gap-8

/* Feature Cards (Landing) */
mobile: grid-cols-1, gap-6
tablet: grid-cols-2, gap-8
desktop: grid-cols-3, gap-8

/* Chat Layout */
mobile: full-width messages
tablet: max-w-3xl centered
desktop: sidebar-left + chat-main split
```

---

## 🎯 Next Steps

1. ✅ **Wireframes completati**
2. ⏳ **Prototipo Next.js funzionante** (prossimo step)
   - Setup Next.js 15 App Router
   - Implementare 5 pagine core
   - Integrare Shadcn/UI components
   - Aggiungere Google Fonts (Quicksand, Inter)
3. ⏳ **Componenti custom Shadcn/UI**
4. ⏳ **Test responsive** (375px → 1024px+)
5. ⏳ **Documentazione Storybook**

---

**Version**: 1.0
**Date**: 2025-11-30
**Design System**: Opzione A - Playful Boardroom
**Status**: Wireframes Complete ✅
**Next**: Functional Prototype
