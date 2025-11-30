# MeepleAI Design System 2.0

**Version**: 2.0 (Strategic Redesign)
**Last Updated**: 2025-11-30
**Status**: In Development
**Aesthetic Direction**: Editorial Playful

---

## 🎨 Design Philosophy

### Brand Concept: "Playful Precision"

MeepleAI transforms the complexity of board game rules into an **intelligent, playful experience**. Our design system balances:

- **Playfulness**: Visual references to board game culture (meeples, dice, tokens)
- **Intelligence**: Precision, reliability, and technological sophistication
- **Accessibility**: Friendly, approachable, Italian-first experience

### Aesthetic Direction: Editorial Playful

**Inspiration**: High-quality board game rulebooks meet modern digital magazine design

**Key Characteristics**:
- Editorial typography with display fonts
- Vivid color palette inspired by board game components
- Asymmetric layouts with grid-breaking elements
- Micro-interactions that reference game mechanics (dice rolls, card flips)
- Generous use of whitespace with controlled density

**Differentiators**:
1. **Distinctive Typography**: DM Serif Display + Plus Jakarta Sans (no generic Inter/Roboto)
2. **Game-Inspired Colors**: Purple (meeple), Amber (game table), Player colors (RBYG)
3. **Playful Animations**: Bounce, wiggle, float effects with game-like feel
4. **Asymmetric Layouts**: Editorial composition avoiding predictable grids
5. **Custom Iconography**: Board game component visual language

---

## 📐 Design Tokens

### Typography System

**Font Families**:
```css
--font-display: 'DM Serif Display', 'Playfair Display', Georgia, serif;
--font-body: 'Plus Jakarta Sans', system-ui, -apple-system, sans-serif;
--font-code: 'JetBrains Mono', 'Fira Code', monospace;
```

**Type Scale** (Fluid Typography):
- Uses `clamp()` for responsive scaling
- Base: 16px (1rem) → 18px (1.125rem) on large screens
- Ratio: ~1.25 (Major Third)
- Range: xs (12px) → 4xl (64px)

**Font Weights**:
- Normal: 400
- Medium: 500 (primary for UI text)
- Semibold: 600 (headings, emphasis)
- Bold: 700 (display text)
- Extrabold: 800 (hero headings)

**Usage Guidelines**:
- **Display Font**: Hero headings, section titles, logo
- **Body Font**: UI text, descriptions, metadata
- **Code Font**: Technical content, API responses, code blocks

### Color System

**Primary - Meeple Purple**:
- Brand color representing AI intelligence + playfulness
- Main: `#a855f7` (500)
- Range: 50 → 950 (10 shades)
- Usage: Primary actions, brand elements, active states

**Secondary - Game Table Amber**:
- Warm, inviting color evoking board game tables
- Main: `#f59e0b` (500)
- Range: 50 → 950
- Usage: Secondary actions, highlights, accent elements

**Accent Colors - Player Tokens**:
- Red: `#ef4444` (Red player)
- Blue: `#3b82f6` (Blue player)
- Green: `#10b981` (Green player)
- Yellow: `#eab308` (Yellow player)
- Usage: Status indicators, categories, data visualization

**Neutral - Dice Ivory & Charcoal**:
- Sophisticated stone palette (not pure gray)
- Range: 0 (white) → 950 (near-black)
- Usage: Backgrounds, text, borders, shadows

**Semantic Colors**:
- Success: Green (`#10b981`)
- Warning: Yellow (`#eab308`)
- Error: Red (`#ef4444`)
- Info: Blue (`#3b82f6`)

**Theme Support**:
- Light theme (default): Clean editorial, like a rulebook
- Dark theme: Cozy game night atmosphere

### Spacing System

**Base Grid**: 8px (0.5rem)

**Scale**:
```
0  → 0
1  → 4px   (0.25rem)
2  → 8px   (0.5rem)  [BASE]
3  → 12px  (0.75rem)
4  → 16px  (1rem)
5  → 24px  (1.5rem)
6  → 32px  (2rem)
8  → 48px  (3rem)
10 → 64px  (4rem)
12 → 96px  (6rem)
16 → 128px (8rem)
20 → 192px (12rem)
```

**Usage Philosophy**:
- Small gaps: 2-3 (8-12px)
- Component padding: 4-5 (16-24px)
- Section spacing: 6-8 (32-48px)
- Large whitespace: 10-16 (64-128px)

### Border Radius

**Scale**:
```
sm   → 6px   (0.375rem) - Buttons, badges
md   → 8px   (0.5rem)   - Inputs, cards
lg   → 12px  (0.75rem)  - Panels, modals
xl   → 16px  (1rem)     - Hero sections
2xl  → 24px  (1.5rem)   - Large cards
full → 9999px           - Pills, avatars
```

**Philosophy**: Soft, playful corners (not sharp or overly rounded)

### Shadows

**Elevation System**:
```css
--shadow-sm: 0 1px 2px rgba(0,0,0,0.05)
--shadow-md: 0 4px 6px rgba(0,0,0,0.1)
--shadow-lg: 0 10px 15px rgba(0,0,0,0.1)
--shadow-xl: 0 20px 25px rgba(0,0,0,0.1)
```

**Dark Theme**: Increased opacity (0.5) for stronger contrast

**Usage**:
- sm: Subtle elevation (buttons, inputs)
- md: Default elevation (cards)
- lg: Hover states, modals
- xl: Popovers, dropdowns

### Transitions & Animations

**Timing Functions**:
```css
--transition-fast:   150ms cubic-bezier(0.4, 0, 0.2, 1)
--transition-base:   250ms cubic-bezier(0.4, 0, 0.2, 1)
--transition-slow:   350ms cubic-bezier(0.4, 0, 0.2, 1)
--transition-bounce: 500ms cubic-bezier(0.68, -0.55, 0.265, 1.55)
```

**Animation Library**:
- **Bounce**: Playful scale on hover (meeple logo, action icons)
- **Float**: Gentle up-down motion (stat cards)
- **Wiggle**: Subtle rotation shake (navigation icons on hover)
- **Slide**: Smooth translation (action cards)
- **Fade-in-up**: Entrance animation (session cards)
- **Gradient-shift**: Subtle hue rotation (hero text gradient)

**Principles**:
- Favor CSS animations over JS for performance
- Use `animation-delay` for staggered reveals
- Apply `will-change` for transform/opacity animations
- Disable animations via `prefers-reduced-motion`

---

## 🧩 Component Library

### Core Components

#### 1. MeepleLogo
**Variants**: `full` | `icon` | `wordmark`
**Sizes**: `sm` | `md` | `lg` | `xl`
**Features**:
- SVG meeple character with gradient fill
- Animated spark indicating AI capabilities
- Hover wiggle effect
- Optional continuous bounce animation

**Usage**:
```tsx
<MeepleLogo variant="full" size="md" animated />
```

#### 2. AppShell (Layout)
**Features**:
- Collapsible sidebar navigation (280px → 80px)
- Icon-based navigation with playful hover states
- Active state with colored accent bar
- User profile dropdown
- Responsive mobile drawer

**Navigation States**:
- Default: Neutral color
- Hover: Background change + icon scale
- Active: Gradient background + accent bar + bold text

#### 3. Stat Card
**Purpose**: Display key metrics with visual appeal

**Features**:
- Large emoji icon with bounce-in animation
- Display font for values
- Decorative gradient circle background
- Hover lift effect

**Variants**:
- Primary (purple decoration)
- Secondary (amber decoration)
- Accent (green decoration)

#### 4. Action Card
**Purpose**: Quick action shortcuts with descriptions

**Features**:
- Icon with gradient background
- Title + description text
- Arrow indicator
- Slide-in entrance animation
- Hover: left accent bar + translateX

#### 5. Session Card
**Purpose**: Game session display with rich metadata

**Features**:
- Large emoji cover with gradient background
- Status badge (active/paused/completed)
- Game title (display font)
- Metadata icons (players, duration)
- Context-aware action button
- Fade-in-up entrance with stagger

**States**:
- Active: Green/Blue gradient
- Paused: Yellow/Amber gradient
- Completed: Neutral gray gradient

#### 6. Tab Navigation (Chips)
**Purpose**: Content filtering with playful switches

**Features**:
- Pill-shaped container
- Individual chip buttons
- Active state: filled with primary color + shadow
- Smooth transition between states

---

## 🎨 Layout Patterns

### 1. Editorial Hero
**Composition**:
- Large display typography (4xl)
- Gradient text for brand emphasis
- Asymmetric two-column grid (60/40 split)
- Stats cards in flexible grid below

**Use Cases**: Dashboard landing, campaign pages

### 2. Asymmetric Content Grid
**Composition**:
- Main content (wider column) + sidebar (sticky quick actions)
- Grid-breaking elements with overlap
- Generous vertical spacing

**Use Cases**: Dashboard, content pages with actions

### 3. Card Grids
**Composition**:
- `auto-fill` with `minmax(320px, 1fr)`
- Staggered entrance animations
- Hover lift effect
- Minimum 3-column on desktop, 1-column mobile

**Use Cases**: Game library, session history, search results

---

## 🚀 Implementation Patterns

### CSS Architecture

**Structure**:
```
styles/
├── design-tokens.css       # CSS variables (this file)
├── globals.css             # Global resets, fonts
├── animations.css          # Keyframe definitions
└── utilities.css           # Utility classes
```

**Component CSS**:
- Use `styled-jsx` for component-scoped styles
- Reference design tokens via CSS variables
- Avoid hardcoded values (use tokens)

### Animation Best Practices

**Performance**:
```css
/* ✅ Good - GPU-accelerated */
.hover-effect {
  transform: translateY(-4px);
  will-change: transform;
}

/* ❌ Bad - Layout thrashing */
.hover-effect {
  margin-top: -4px;
}
```

**Staggered Reveals**:
```css
.card {
  animation: fade-in-up 0.6s ease-out backwards;
  animation-delay: calc(var(--card-index) * 100ms);
}
```

**Accessibility**:
```css
@media (prefers-reduced-motion: reduce) {
  * {
    animation-duration: 0.01ms !important;
    transition-duration: 0.01ms !important;
  }
}
```

### Responsive Strategy

**Breakpoints**:
- Mobile: < 640px
- Tablet: 640px - 1024px
- Desktop: > 1024px
- Large: > 1400px

**Grid Adaptation**:
```css
/* Mobile-first approach */
.grid {
  display: grid;
  grid-template-columns: 1fr;
  gap: var(--space-4);
}

@media (min-width: 640px) {
  .grid {
    grid-template-columns: repeat(2, 1fr);
  }
}

@media (min-width: 1024px) {
  .grid {
    grid-template-columns: repeat(3, 1fr);
    gap: var(--space-6);
  }
}
```

---

## 📊 Design Principles

### 1. Intentional Asymmetry
Break the grid purposefully to create visual interest:
- Hero sections with unequal columns
- Overlapping elements with z-index layering
- Floating decorative shapes

### 2. Playful Micro-Interactions
Every interaction should feel delightful:
- Icon scale/rotate on hover
- Cards lift and tilt slightly
- Smooth color transitions
- Subtle bounce on focus

### 3. Editorial Typography
Typography hierarchy tells a story:
- Display font for impact (headings, values)
- Body font for readability (descriptions, UI)
- Generous line-height for breathing room
- Contrast in font weights for emphasis

### 4. Contextual Color
Colors carry meaning beyond aesthetics:
- Primary (purple) = AI-powered actions
- Secondary (amber) = Discovery/exploration
- Player colors = Categories/status
- Neutral = Content/structure

### 5. Performance-First Animation
Animations enhance, never hinder:
- CSS transforms over layout properties
- `will-change` for known animations
- Respect `prefers-reduced-motion`
- Stagger delays < 150ms for groups

---

## 🎯 Next Steps

### Phase 4: Page Redesigns (Upcoming)
- [ ] Chat/RAG interface with conversational UI
- [ ] Settings page with improved 4-tab structure
- [ ] Game library with advanced filtering
- [ ] Upload flow with drag-and-drop + progress

### Phase 5: Implementation Roadmap
- [ ] Migrate existing pages to new design system
- [ ] Create Storybook documentation
- [ ] Build component testing suite
- [ ] Accessibility audit (WCAG 2.1 AA)
- [ ] Performance optimization (Lighthouse 90+)

---

## 📚 Resources

**Typography**:
- DM Serif Display: https://fonts.google.com/specimen/DM+Serif+Display
- Plus Jakarta Sans: https://fonts.google.com/specimen/Plus+Jakarta+Sans

**Inspiration**:
- Board Game Geek design patterns
- Editorial magazine layouts (Kinfolk, Monocle)
- Modern SaaS dashboard aesthetics (Linear, Notion)

**Tools**:
- Figma: Design prototyping
- Radix UI: Accessible component primitives
- Tailwind CSS 4: Utility-first CSS framework

---

**Maintained by**: Frontend Team
**Review Cycle**: Monthly (design tokens), Quarterly (full system)
**Feedback**: #design-system on internal chat
