# Dashboard Collezione-Centrica - Opzione A

**Aesthetic Direction**: **Editorial Gaming** - Mix tra rivista di design e catalogo ludico premium

---

## 🎨 Design Philosophy

### Visual Identity
- **Tone**: Sofisticato ma giocoso, organizzato senza sterilità
- **Typography**:
  - **Display**: Playfair Display (caratteriale, editoriale)
  - **Body**: Geist Sans / Inter (leggibile, moderno)
- **Color Palette**:
  - **Primary**: Burnt Orange `#D97706` (energia, gioco)
  - **Secondary**: Olive Green `#65A30D` (natura, strategia)
  - **Accent**: Deep Blue `#1E40AF` (profondità, competenza)
  - **Neutrals**: Stone (50-900) per warmth
- **Motion**: Microinterazioni fluide, transizioni eleganti, hover states sorprendenti

### Layout Principles
- **Asymmetric Grid**: Elementi che rompono la rigidità del layout
- **Generous White Space**: Respiro tra sezioni dense
- **Visual Hierarchy**: Contrasto dimensionale e cromatico forte
- **Glassmorphism**: Profondità attraverso blur e trasparenze stratificate

### Memorability Factor
**Card giochi con effetto "flip" al hover** che rivelano statistiche dettagliate, con animazione glassmorphic e profondità 3D.

---

## 📐 Layout Structure (Markdown Skeleton)

```markdown
┌─────────────────────────────────────────────────────────────────┐
│ HEADER (Sticky, Backdrop Blur)                                 │
├─────────────────────────────────────────────────────────────────┤
│ [Logo + Nav] [Search Bar (Desktop)] [Profile + Notifications]  │
│              [Search Bar (Mobile - Below)]                      │
└─────────────────────────────────────────────────────────────────┘

┌─────────────────────────────────────────────────────────────────┐
│ STATS OVERVIEW (Glassmorphic Cards, 4-column grid)             │
├────────────┬────────────┬────────────┬────────────────────────┤
│ 📚 Totale  │ 🎲 Giocati │ ⭐ Wishlist│ 📈 Trend              │
│ 127 giochi │ 23 (30gg)  │ 15 giochi  │ +3 questo mese        │
│            │ 🔥 7gg     │ +3 mese    │                        │
└────────────┴────────────┴────────────┴────────────────────────┘

┌────────────┬───────────────────────────────────────────────────┐
│ SIDEBAR    │ MAIN CONTENT (Games Grid/List)                   │
│ (280px)    │                                                   │
│            │ ┌──────────────────────────────────────────────┐ │
│ [Filtri]   │ │ [Toolbar: Filtri Toggle | View Mode Toggle] │ │
│            │ └──────────────────────────────────────────────┘ │
│ Categoria  │                                                   │
│ ☐ Strategia│ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐                │
│ ☐ Party    │ │CARD1│ │CARD2│ │CARD3│ │CARD4│ (Grid 2-4 col)  │
│ ☐ Famiglia │ │ Flip│ │ Flip│ │ Flip│ │ Flip│                │
│            │ └─────┘ └─────┘ └─────┘ └─────┘                │
│ Difficoltà │                                                   │
│ ☐ Facile   │ ┌─────┐ ┌─────┐ ┌─────┐ ┌─────┐                │
│ ☐ Medio    │ │CARD5│ │CARD6│ │CARD7│ │CARD8│                │
│ ☐ Difficile│ └─────┘ └─────┘ └─────┘ └─────┘                │
│            │                                                   │
│ Giocatori  │ [...more cards with infinite scroll/pagination]  │
│ ○ 1-2      │                                                   │
│ ○ 3-4      │                                                   │
│ ○ 5+       │                                                   │
│            │                                                   │
│ [Reset]    │                                                   │
└────────────┴───────────────────────────────────────────────────┘

                                               ┌────────────┐
                                               │ FAB: [+]   │ (Fixed)
                                               │ Add Game   │
                                               └────────────┘
```

---

## 🃏 Component Breakdown

### 1. Header (Sticky)
**Features**:
- Logo + Brand (MeepleAI con Gamepad icon)
- Search Bar prominente con suggerimenti live
- User Profile con dropdown
- Notifications badge con pulse animation

**Responsive**:
- Desktop: Search inline nel header
- Mobile: Search move sotto header, full-width

**Tech**:
```tsx
<header className="sticky top-0 backdrop-blur-xl bg-white/80">
  <Search placeholder="Cerca giochi, editori, designer..." />
  <UserProfile />
</header>
```

---

### 2. Stats Overview (Glassmorphic Cards)
**Layout**: 4-column grid (responsive: 2-col mobile, 4-col desktop)

**Cards**:
1. **Totale Giochi**: 127 giochi (Amber gradient)
2. **Giocati (30gg)**: 23 + Streak 🔥 7 giorni (Emerald gradient)
3. **Wishlist**: 15 + Trend +3 mese (Blue gradient)
4. **Trending**: +3 giochi in crescita (Purple gradient)

**Visual Effects**:
- Glassmorphism: `backdrop-blur-xl` + gradient borders
- Decorative orb: Blurred circle per depth
- Hover: Lift effect (`y: -4px`, `scale: 1.02`)

**Tech**:
```tsx
<StatsCard
  icon={<Gamepad2 />}
  label="Totale Giochi"
  value={127}
  gradient="from-amber-500/20 to-orange-500/20"
  borderColor="border-amber-500/30"
/>
```

---

### 3. Filter Sidebar (Collapsible)
**Sections**:
- **Categoria**: Multi-select badges (Strategia, Party, Famiglia, Astratto, Cooperativo)
- **Difficoltà**: Checkboxes con visual dots (●○○, ●●○, ●●●)
- **Giocatori**: Badge groups (1-2, 3-4, 5+)
- **Stato**: All | Owned | Played | Wishlist
- **Ordinamento**: Recent | Alphabetical | Rating | Duration
- **Reset Filtri**: Clear button

**Responsive**:
- Desktop: Fixed sidebar (280px width)
- Mobile: Bottom sheet modal (Framer Motion slide-up)

**Animation**:
- Collapse: Framer Motion `width` + `opacity` transition
- Badges: Hover color shift (`hover:bg-amber-100`)

**Tech**:
```tsx
<AnimatePresence>
  {sidebarOpen && (
    <motion.aside
      initial={{ width: 0, opacity: 0 }}
      animate={{ width: 'auto', opacity: 1 }}
      exit={{ width: 0, opacity: 0 }}
    >
      <FilterSidebar />
    </motion.aside>
  )}
</AnimatePresence>
```

---

### 4. Game Card (Flip Animation) ⭐ KEY FEATURE
**Front (Default State)**:
- Cover image (full-height, object-cover)
- Gradient overlay (bottom fade: `from-black/80 to-transparent`)
- Title (Playfair Display, white, bold)
- Rating stars (amber-400: ★★★★☆)
- Complexity dots (badge: ●●○)

**Back (Hover State)** - **3D Flip Effect**:
- Glassmorphic background (`from-amber-50 to-orange-50`, blur)
- Stats grid:
  - **Partite giocate**: 23
  - **Durata media**: 90 min
  - **Ultimo gioco**: 15 gen
- Quick Actions:
  - ✓ **Segna Giocato** (Emerald CTA)
  - ⭐ **Wishlist Toggle** (Outline button)

**Animation Details**:
```tsx
<motion.div
  onHoverStart={() => setIsFlipped(true)}
  onHoverEnd={() => setIsFlipped(false)}
  animate={{ rotateY: isFlipped ? 180 : 0 }}
  transition={{ duration: 0.6, type: 'spring' }}
>
  {/* Front: backface-hidden */}
  {/* Back: backface-hidden + rotateY(180deg) */}
</motion.div>
```

**CSS Magic**:
```css
.perspective-1000 { perspective: 1000px; }
.preserve-3d { transform-style: preserve-3d; }
.backface-hidden { backface-visibility: hidden; }
.rotate-y-180 { transform: rotateY(180deg); }
```

**Responsive**:
- Grid view: 1-col (mobile) → 2-col (sm) → 3-col (lg) → 4-col (xl)
- List view: Horizontal layout, no flip (cover + title + stats inline)

---

### 5. Toolbar (Above Grid)
**Left Side**:
- **Filtri Toggle**: Button con badge count (es. "12 giochi")
- **Filter Status Badge**: Active filters indicator

**Right Side**:
- **View Mode Toggle**: Grid icon | List icon (active state styling)

**Tech**:
```tsx
<div className="flex items-center justify-between">
  <Button onClick={toggleSidebar}>
    <Filter /> Filtri
    <Badge>{filteredCount} giochi</Badge>
  </Button>
  <div className="flex gap-2">
    <Button variant={viewMode === 'grid' ? 'default' : 'ghost'}>
      <Grid3x3 />
    </Button>
    <Button variant={viewMode === 'list' ? 'default' : 'ghost'}>
      <List />
    </Button>
  </div>
</div>
```

---

### 6. Floating Action Button (FAB)
**Position**: Fixed bottom-right (24px margin)
**Size**: 56x56px circular
**Visual**:
- Gradient: `from-amber-500 to-orange-600`
- Shadow: `shadow-2xl shadow-amber-500/50` (glowing effect)
- Icon: Plus (+) white

**Interaction**:
- Hover: `scale(1.1)` + shadow intensifies
- Tap: `scale(0.95)` (tactile feedback)
- Click: Opens "Add Game" modal/drawer

**Tech**:
```tsx
<motion.button
  whileHover={{ scale: 1.1 }}
  whileTap={{ scale: 0.95 }}
  className="fixed bottom-6 right-6 h-14 w-14 rounded-full
             bg-gradient-to-br from-amber-500 to-orange-600
             shadow-2xl shadow-amber-500/50"
>
  <Plus />
</motion.button>
```

---

## 🔧 Technical Implementation

### Tech Stack
- **Framework**: Next.js 14 (App Router)
- **Language**: TypeScript
- **Styling**: Tailwind CSS + shadcn/ui
- **Animation**: Framer Motion
- **State**: Zustand (filter store) + TanStack Query (data fetching)

### File Structure
```
apps/web/src/
├── app/(public)/dashboard/
│   ├── collection-dashboard.tsx       # Main component
│   └── components/
│       ├── StatsCard.tsx              # Glassmorphic stats
│       ├── FilterSidebar.tsx          # Filter UI
│       ├── GameCardFlip.tsx           # 3D flip card
│       └── FloatingActionButton.tsx   # FAB
├── lib/stores/
│   └── collection-filters-store.ts    # Zustand store
└── hooks/queries/
    └── useCollectionGames.ts          # TanStack Query hook
```

### Zustand Store (collection-filters-store.ts)
```typescript
interface CollectionFiltersState {
  categories: string[];
  complexity: number[];
  playerCount: string;
  status: 'all' | 'owned' | 'played' | 'wishlist';
  sortBy: 'recent' | 'alphabetical' | 'rating' | 'duration';
  searchQuery: string;
  viewMode: 'grid' | 'list';
  sidebarOpen: boolean;

  // Actions
  toggleCategory: (category: string) => void;
  setComplexity: (levels: number[]) => void;
  setSortBy: (sort: string) => void;
  resetFilters: () => void;
}
```

### API Integration (TanStack Query)
```typescript
const { data: games, isLoading } = useQuery({
  queryKey: ['collection-games', filters],
  queryFn: () => fetchGames({
    categories: filters.categories,
    complexity: filters.complexity,
    search: filters.searchQuery,
    sort: filters.sortBy
  }),
  staleTime: 5 * 60 * 1000 // 5 minutes
});
```

---

## ♿ Accessibility

### Keyboard Navigation
- ✅ Tab order: Header → Search → Filters → Cards → FAB
- ✅ Arrow keys: Navigate grid (optional enhancement)
- ✅ Enter/Space: Activate buttons and checkboxes
- ✅ Escape: Close modals, reset search

### Screen Reader Support
- ✅ ARIA labels on all interactive elements
- ✅ Semantic HTML (`<header>`, `<main>`, `<aside>`)
- ✅ Live regions for filter updates (`aria-live="polite"`)
- ✅ Card descriptions (`aria-describedby` for stats)

### Visual Accessibility
- ✅ Contrast ratios: 4.5:1 minimum (WCAG AA)
- ✅ Focus indicators: Visible ring on all focusable elements
- ✅ Color independence: Icons + text labels (not color-only)
- ✅ Reduced motion: `prefers-reduced-motion` media query

---

## 📱 Responsive Behavior

### Breakpoints (Tailwind)
- **Mobile**: < 640px (sm)
- **Tablet**: 640px - 1024px (sm-md)
- **Desktop**: > 1024px (lg+)

### Layout Adaptations
| Element          | Mobile (< 640px)       | Tablet (640-1024px)    | Desktop (> 1024px)    |
|------------------|------------------------|------------------------|-----------------------|
| **Header Search**| Below header, full-w   | Below header, full-w   | Inline, max-w-2xl     |
| **Stats Grid**   | 2 columns              | 4 columns              | 4 columns             |
| **Sidebar**      | Bottom sheet modal     | Drawer (swipe-in)      | Fixed sidebar (280px) |
| **Games Grid**   | 1-2 columns            | 2-3 columns            | 3-4 columns           |
| **Card Height**  | 280px                  | 320px                  | 360px                 |
| **FAB**          | Bottom-right (56px)    | Bottom-right (56px)    | Bottom-right (56px)   |

---

## 🎬 Animation Choreography

### Page Load Sequence (Staggered Reveals)
1. **Header** (0ms): Fade in + slide down
2. **Stats Cards** (100ms delay each): Scale up + fade in
3. **Sidebar** (400ms): Slide in from left
4. **Game Cards** (500ms + 50ms stagger): Fade up + scale

```tsx
const containerVariants = {
  hidden: { opacity: 0 },
  show: {
    opacity: 1,
    transition: {
      staggerChildren: 0.1
    }
  }
};

const itemVariants = {
  hidden: { opacity: 0, y: 20 },
  show: { opacity: 1, y: 0 }
};
```

### Microinteractions
- **Card Hover**: Lift (`y: -4px`) + shadow intensify (200ms ease-out)
- **Card Flip**: 600ms spring animation with preserve-3d
- **Badge Select**: Background color shift (150ms)
- **FAB Pulse**: Scale breathing animation (2s infinite)
- **Search Focus**: Ring expand + border color shift (200ms)

### Scroll Behavior
- **Infinite Scroll**: Load more cards on 80% viewport intersection
- **Sticky Header**: Maintains position with backdrop-blur
- **Parallax Stats** (optional): Stats cards subtle vertical shift on scroll

---

## 🚀 Performance Optimizations

### Code Splitting
```tsx
const GameCardFlip = lazy(() => import('./components/GameCardFlip'));
// Load after critical content renders
```

### Image Optimization
- **Next.js Image**: Automatic WebP, lazy loading, responsive srcset
- **Blur Placeholder**: LQIP (Low-Quality Image Placeholder)
- **Priority Loading**: First 8 cards get `priority` flag

### Memoization
```tsx
const MemoizedGameCard = React.memo(GameCardFlip, (prev, next) =>
  prev.game.id === next.game.id && prev.viewMode === next.viewMode
);
```

### Virtualization (Large Collections)
```tsx
import { useVirtualizer } from '@tanstack/react-virtual';
// Render only visible cards in viewport
```

---

## 🎨 Design Tokens (CSS Variables)

```css
:root {
  /* Colors - Earthy Palette */
  --color-amber-primary: #D97706;
  --color-emerald-secondary: #65A30D;
  --color-blue-accent: #1E40AF;

  /* Typography */
  --font-display: 'Playfair Display', serif;
  --font-body: 'Geist Sans', 'Inter', sans-serif;

  /* Spacing */
  --spacing-card-gap: 1.5rem; /* 24px */
  --spacing-section: 2rem;    /* 32px */

  /* Effects */
  --glassmorphism-blur: 16px;
  --glassmorphism-bg: rgba(255, 255, 255, 0.7);
  --shadow-elevated: 0 20px 25px -5px rgba(0, 0, 0, 0.1);

  /* Animation */
  --transition-smooth: 300ms cubic-bezier(0.4, 0, 0.2, 1);
  --transition-spring: 600ms cubic-bezier(0.34, 1.56, 0.64, 1);
}
```

---

## 🧪 Testing Strategy

### Unit Tests (Vitest)
- ✅ StatsCard: Renders correct values and gradients
- ✅ FilterSidebar: Toggle logic, reset functionality
- ✅ GameCardFlip: Flip state, quick actions
- ✅ Zustand Store: Filter updates, persistence

### Integration Tests
- ✅ Filter → API → Grid update flow
- ✅ Search → Debounce → Results refresh
- ✅ View mode toggle → Layout change

### E2E Tests (Playwright)
- ✅ User can search and filter games
- ✅ Card flip reveals stats on hover
- ✅ FAB opens add game modal
- ✅ Mobile: Sidebar opens as bottom sheet

### Visual Regression (Chromatic)
- ✅ Snapshot all component states
- ✅ Cross-browser consistency
- ✅ Responsive breakpoint validation

---

## 🔄 Future Enhancements

### Phase 2 (Post-MVP)
- **Advanced Search**: Fuzzy matching, autocomplete suggestions
- **Bulk Actions**: Select multiple cards, batch operations
- **Drag & Drop**: Reorder games, create custom collections
- **Custom Views**: Save filter presets, shareable URLs

### Phase 3 (Premium Features)
- **AI Recommendations**: "Games you might like" section
- **Social Sharing**: Share collection with friends (public URL)
- **Analytics Dashboard**: Play frequency heatmap, genre distribution charts
- **Offline Mode**: PWA with local caching for collection access

---

## 📊 Success Metrics

### User Engagement
- **Time on Page**: > 3 minutes average
- **Card Interactions**: > 5 cards flipped per session
- **Filter Usage**: > 60% of users apply at least one filter
- **Return Rate**: > 40% weekly active users

### Performance
- **LCP (Largest Contentful Paint)**: < 2.5s
- **FID (First Input Delay)**: < 100ms
- **CLS (Cumulative Layout Shift)**: < 0.1

### Accessibility
- **Lighthouse Accessibility Score**: > 95
- **Keyboard Navigation**: 100% of features accessible
- **Screen Reader Compatibility**: Tested with NVDA/VoiceOver

---

## 🎯 Summary

**Opzione A: Dashboard Collezione-Centrica** offre:
- ✨ **Estetica distintiva**: Editorial Gaming con Playfair Display + palette terrosa
- 🃏 **Flip Cards 3D**: Microinterazione memorabile e funzionale
- 🎨 **Glassmorphism**: Profondità visiva senza pesantezza
- ⚡ **Performance**: Code splitting, lazy loading, memoization
- ♿ **Accessibility**: WCAG AA compliant, keyboard + screen reader support
- 📱 **Responsive**: Mobile-first design con adattamenti intelligenti

**Differenziazione competitiva**:
- Nessun altro dashboard di collezione giochi usa flip cards 3D con glassmorphism
- Layout asimmetrico rompe la monotonia delle griglie standard
- Palette terrosa distingue da generici purple/blue corporate

**Implementation Ready**:
- ✅ Componenti completamente implementati in TypeScript
- ✅ Zustand store configurato con persistence
- ✅ Framer Motion animations integrate
- ✅ shadcn/ui components utilizzati
- ✅ Tailwind CSS con design tokens estensibili

---

**Next Steps**:
1. Review design con stakeholder
2. Test usability con utenti beta
3. Integration con API backend
4. Deploy su staging per feedback
5. Iterazione basata su metriche
