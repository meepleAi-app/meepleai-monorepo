# 🎲 MeepleAI Game Session Toolkit - UI Components

**Production-ready React components for collaborative board game scoring and session management.**

## 🎨 Design Philosophy

**Aesthetic Direction**: *Analog Gaming Digitized*

A tactile, board game-inspired interface that evokes the warmth and nostalgia of tabletop gaming nights:

- **Wood grain textures** - Subtle SVG noise filters create organic surfaces
- **Embossed numbers** - Vintage scoresheet typography with depth
- **Warm color palette** - Amber/orange tones reminiscent of game boxes
- **Physics-based interactions** - Smooth animations that feel like sliding tokens
- **Mobile-first** - Designed for phones held during physical game nights

### Typography
- **Headers**: Bold, clear hierarchy with uppercase tracking
- **Scores**: Monospace tabular numbers for easy reading
- **Labels**: Small caps for categories/rounds

### Color Palette
```css
Primary: #D97706 (Warm Amber) - Game box orange
Accent: #FCD34D (Gold) - Winner highlights
Success: #059669 (Emerald) - Sync states
Danger: #DC2626 (Red) - Negative scores
Neutral: Slate grays - Backgrounds and text
```

### Textures & Effects
- **Wood grain**: SVG noise filter (3-5% opacity)
- **Paper grain**: Subtle repeating gradients for scoresheets
- **Token shadows**: Multi-layered box-shadows for depth
- **Real-time pulse**: Animated rings for collaboration indicators

---

## 📦 Components

### 1. SessionHeader
**Purpose**: Sticky header with session info, join code, and actions

**Props**:
```typescript
interface SessionHeaderProps {
  session: Session;
  onPause?: () => void;
  onFinalize?: () => void;
  onShare?: () => void;
}
```

**Features**:
- Session code with one-click copy
- Status badges (Active/Paused/Finalized)
- Game name/icon display
- Actions dropdown menu
- Responsive layout

**Usage**:
```tsx
<SessionHeader
  session={currentSession}
  onPause={() => handlePause()}
  onFinalize={() => handleFinalize()}
  onShare={() => handleShare()}
/>
```

---

### 2. ParticipantCard
**Purpose**: Display player info with scores, ranks, and real-time status

**Props**:
```typescript
interface ParticipantCardProps {
  participant: Participant;
  variant?: 'compact' | 'full';
}
```

**Variants**:
- **Compact**: Horizontal layout for lists (40px height)
- **Full**: Detailed card with large score display (120px height)

**Features**:
- Avatar with custom colors and initials
- Rank badges (🥇🥈🥉 for top 3, #4+ for others)
- Owner crown indicator
- "Is typing" real-time indicator
- Embossed number styling for scores

**Usage**:
```tsx
{/* Compact variant for lists */}
<ParticipantCard participant={player} variant="compact" />

{/* Full variant for detailed view */}
<ParticipantCard participant={player} variant="full" />
```

---

### 3. ScoreInput
**Purpose**: Mobile-optimized score entry with quick actions

**Props**:
```typescript
interface ScoreInputProps {
  participants: Participant[];
  rounds: number[];
  categories?: string[];
  currentRound?: number;
  onSubmit: (data: ScoreSubmitData) => Promise<void>;
  onUndo?: () => void;
  syncStatus?: SyncStatus;
}
```

**Features**:
- **Participant selector**: Button grid (not dropdown) for fast selection
- **Round/Category selectors**: Optional dropdown menus
- **Score input**: Large number input with validation
- **Quick actions**: -5, -1, +1, +5 buttons (44x44px touch targets)
- **Undo button**: Rollback last entry
- **Sync indicators**: "Saving...", "Synced ✓", "Error"
- **Optimistic UI**: Instant feedback, background sync

**Usage**:
```tsx
<ScoreInput
  participants={players}
  rounds={[1, 2, 3]}
  categories={['Military', 'Science', 'Commerce']}
  currentRound={3}
  onSubmit={handleScoreSubmit}
  onUndo={handleUndo}
  syncStatus="synced"
/>
```

---

### 4. Scoreboard
**Purpose**: Comprehensive score tracking with round breakdown

**Props**:
```typescript
interface ScoreboardProps {
  data: ScoreboardData;
  isRealTime?: boolean;
  variant?: 'full' | 'compact';
}
```

**Variants**:
- **Full**: Detailed table with round/category breakdown
- **Compact**: Simple participant list with totals

**Features**:
- **Leader podium**: Visual highlight for top 3 players
- **Score table**: Scrollable horizontal table with sticky columns
- **Trend indicators**: ↑ ↓ arrows for score momentum
- **Real-time animations**: Pulse effect on score updates
- **Totals calculation**: Automatic sum across rounds/categories
- **Rank updates**: Dynamic ranking based on totals

**Layout**:
- **Mobile**: Single column, stacked cards
- **Tablet**: 2-column grid (scoreboard + input side-by-side)
- **Desktop**: 3-column (participants | scoreboard | tools)

**Usage**:
```tsx
<Scoreboard
  data={{
    participants,
    scores,
    rounds: [1, 2, 3],
    categories: ['Military', 'Science', 'Commerce']
  }}
  isRealTime={true}
  variant="full"
/>
```

---

## 🚀 Installation

```bash
# Components are self-contained in apps/web/src/components/session/
# Import from barrel export:
import {
  SessionHeader,
  ParticipantCard,
  ScoreInput,
  Scoreboard,
  type Participant,
  type ScoreEntry
} from '@/components/session';
```

### Dependencies
- **shadcn/ui**: Badge, Button, Input, Label, Select, DropdownMenu
- **lucide-react**: Icons (Copy, Crown, Trophy, TrendingUp, etc.)
- **Tailwind CSS**: Styling framework

---

## 💻 Demo Page

**Route**: `/toolkit/demo` (authenticated)

**Features**:
- Live interactive demo with all 4 components
- Mock data for 7 Wonders game session
- Real-time score updates with animations
- Responsive layout demonstration
- Design system documentation panel

**Run Demo**:
```bash
cd apps/web
pnpm dev
# Navigate to http://localhost:3000/toolkit/demo
```

---

## 📱 Responsive Behavior

### Breakpoints
```css
sm:  640px  /* Small tablets */
md:  768px  /* Tablets */
lg:  1024px /* Laptops */
xl:  1280px /* Desktops */
```

### Layout Adaptations

**Mobile (< 640px)**:
- Single column layout
- Stacked participant cards
- Bottom sheet for ScoreInput (sticky)
- Full-width buttons (44px min height)
- Horizontal scroll for scoreboard table

**Tablet (640px - 1024px)**:
- 2-column grid (scoreboard + input)
- Participant cards in sidebar
- Larger touch targets maintained

**Desktop (> 1024px)**:
- 3-column layout (participants | scoreboard | tools)
- Hover states enabled
- Optimized for mouse/trackpad interaction

---

## 🎬 Animations

All animations use CSS-only solutions for performance:

```css
/* Score count-up on update */
@keyframes score-count-up {
  0% { transform: scale(1.3) translateY(-10px); opacity: 0; }
  100% { transform: scale(1) translateY(0); opacity: 1; }
}

/* Real-time pulse for collaboration */
@keyframes real-time-pulse {
  0%, 100% { opacity: 1; transform: scale(1); }
  50% { opacity: 0.7; transform: scale(1.05); }
}

/* Rank badge bounce */
@keyframes rank-bounce {
  0%, 100% { transform: translateY(0) scale(1); }
  50% { transform: translateY(-5px) scale(1.1); }
}
```

**Animation Triggers**:
- **Score updates**: Count-up animation + pulse ring
- **Rank changes**: Badge bounce + color transition
- **Typing indicator**: Continuous pulse on participant card
- **Button taps**: Scale transform (0.95) for tactile feedback

---

## ♿ Accessibility

### ARIA Labels
All interactive elements have descriptive labels:
```tsx
<button aria-label="Copy session code">
<button aria-label="Increase score by 5">
<input aria-describedby="score-helper-text">
```

### Keyboard Navigation
- **Tab order**: Logical flow through form elements
- **Enter**: Submit score input
- **Escape**: Close dropdowns
- **Arrow keys**: Navigate select menus

### High Contrast Mode
Compatible with `prefers-contrast: high`:
```css
@media (prefers-contrast: high) {
  .border { border-width: 2px; }
  .text-slate-600 { color: #000; }
}
```

### Touch Targets
All interactive elements meet WCAG 2.1 Level AAA:
- Minimum size: **44x44px**
- Spacing: **8px** between targets
- Visual feedback on tap

---

## 🌓 Dark Mode

All components support dark mode via Tailwind's `dark:` variants:

```tsx
className="bg-white dark:bg-slate-800 text-slate-900 dark:text-slate-100"
```

**Dark Mode Palette**:
- Backgrounds: `slate-900`, `slate-800/50` (with transparency)
- Text: `slate-100`, `slate-400` (reduced contrast)
- Accents: `amber-400` (brightened from `amber-600`)
- Borders: `slate-700`, `amber-900/20` (reduced opacity)

**Texture Adjustments**:
- Wood grain opacity increased to 5% (from 3%)
- Embossed shadows inverted for depth
- Gradient overlays use darker base colors

---

## 🔧 Customization

### Custom Colors
Override default colors in component props or Tailwind config:

```tsx
<ParticipantCard
  participant={{
    ...player,
    avatarColor: '#7C3AED' // Custom purple
  }}
/>
```

### Custom Animations
Import CSS file and apply custom classes:

```tsx
import '@/components/session/session-toolkit.css';

<div className="session-dice-roll">
  {/* Dice component */}
</div>
```

### Typography Override
Use Tailwind's `font-` utilities:

```tsx
<div className="font-serif"> {/* Override default sans */}
  <Scoreboard ... />
</div>
```

---

## 🧪 Testing

### Unit Tests
```bash
cd apps/web
pnpm test src/components/session
```

### E2E Tests (Playwright)
```bash
pnpm test:e2e --grep "Session Toolkit"
```

**Test Coverage**:
- ✅ Score input validation
- ✅ Real-time sync indicators
- ✅ Rank calculation accuracy
- ✅ Responsive breakpoints
- ✅ Dark mode rendering
- ✅ Accessibility (ARIA, keyboard nav)

---

## 📚 Type Definitions

```typescript
// Participant with scoring metadata
interface Participant {
  id: string;
  displayName: string;
  isOwner: boolean;
  isCurrentUser: boolean;
  avatarColor: string;
  totalScore: number;
  rank?: number;
  isTyping?: boolean;
}

// Individual score entry (per round/category)
interface ScoreEntry {
  id: string;
  participantId: string;
  roundNumber: number | null; // null = final score
  category: string | null;    // null = no category
  scoreValue: number;
  timestamp: Date;
  createdBy: string;
}

// Session metadata
interface Session {
  id: string;
  sessionCode: string;
  sessionType: 'Generic' | 'GameSpecific';
  gameName?: string;
  gameIcon?: string;
  sessionDate: Date;
  status: 'Active' | 'Paused' | 'Finalized';
  participantCount: number;
}

// Aggregate scoreboard data
interface ScoreboardData {
  participants: Participant[];
  scores: ScoreEntry[];
  rounds: number[];
  categories: string[];
}

// Real-time sync status
type SyncStatus = 'idle' | 'saving' | 'synced' | 'error';
```

---

## 🎯 Performance

### Optimization Strategies

**Component-Level**:
- `React.memo()` for ParticipantCard (prevent re-renders)
- `useMemo()` for score calculations
- `useCallback()` for event handlers

**Animation**:
- CSS-only animations (no JavaScript)
- `will-change` for transform properties
- `requestAnimationFrame` for scroll effects

**Bundle Size**:
- Tree-shakeable barrel exports
- No external animation libraries
- Inline SVG for textures (no image requests)

**Real-Time Sync**:
- Debounced score updates (300ms)
- Optimistic UI (instant feedback)
- Background sync queue for offline support

---

## 🐛 Troubleshooting

### Common Issues

**1. Wood grain texture not appearing**
- Ensure SVG base64 is properly encoded
- Check `opacity` value isn't too low
- Verify `pointer-events: none` on overlay

**2. Animations not smooth**
- Enable hardware acceleration: `transform: translateZ(0)`
- Reduce `animation-duration` for faster devices
- Check for excessive re-renders with React DevTools

**3. Real-time sync not working**
- Verify SSE endpoint is accessible
- Check `EventSource` browser support
- Inspect network tab for connection drops

**4. Dark mode colors incorrect**
- Ensure `dark:` variant is first in className
- Check Tailwind config has `darkMode: 'class'`
- Verify parent has `dark` class applied

---

## 📖 References

**Design Inspiration**:
- [Splitwise](https://www.splitwise.com) - Collaborative expense tracking
- [Kahoot](https://kahoot.com) - Live scoreboard with rankings
- [Board Game Arena](https://boardgamearena.com) - Game session UI
- [Notion](https://notion.so) - Real-time collaboration indicators

**Technical Resources**:
- [shadcn/ui Documentation](https://ui.shadcn.com)
- [Tailwind CSS Docs](https://tailwindcss.com)
- [React SSE Guide](https://react.dev/reference/react-dom/server/streaming)
- [WCAG 2.1 Guidelines](https://www.w3.org/WAI/WCAG21/quickref/)

---

## 🎨 Design Tokens

```css
:root {
  /* Colors */
  --session-amber-primary: #D97706;
  --session-orange-accent: #EA580C;
  --session-gold-winner: #FCD34D;
  --session-emerald-success: #059669;
  --session-red-danger: #DC2626;

  /* Spacing */
  --session-spacing-xs: 0.25rem;  /* 4px */
  --session-spacing-sm: 0.5rem;   /* 8px */
  --session-spacing-md: 1rem;     /* 16px */
  --session-spacing-lg: 1.5rem;   /* 24px */
  --session-spacing-xl: 2rem;     /* 32px */

  /* Typography */
  --session-font-mono: ui-monospace, 'Cascadia Code', 'Source Code Pro', monospace;
  --session-font-sans: ui-sans-serif, system-ui, -apple-system, sans-serif;

  /* Shadows */
  --session-shadow-sm: 0 1px 2px rgba(0, 0, 0, 0.05);
  --session-shadow-md: 0 4px 6px rgba(0, 0, 0, 0.1);
  --session-shadow-lg: 0 10px 15px rgba(0, 0, 0, 0.1);
  --session-shadow-xl: 0 20px 25px rgba(0, 0, 0, 0.15);

  /* Border Radius */
  --session-radius-sm: 0.375rem;  /* 6px */
  --session-radius-md: 0.5rem;    /* 8px */
  --session-radius-lg: 0.75rem;   /* 12px */
  --session-radius-xl: 1rem;      /* 16px */
  --session-radius-2xl: 1.5rem;   /* 24px */

  /* Transitions */
  --session-transition-fast: 150ms cubic-bezier(0.4, 0, 0.2, 1);
  --session-transition-base: 300ms cubic-bezier(0.4, 0, 0.2, 1);
  --session-transition-slow: 500ms cubic-bezier(0.4, 0, 0.2, 1);
}
```

---

**Last Updated**: 2026-01-29
**Component Version**: 1.0.0
**Compatibility**: Next.js 14+, React 18+, Tailwind CSS 3+
