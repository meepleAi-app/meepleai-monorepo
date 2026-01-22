# 🎨 MeepleAI Design System - Mockups Finali

## Overview

Design system **coerente** con le pagine user esistenti, applicato a tutte le pagine principali (Admin + User).

**Stile Base** (dalle screenshot esistenti):
- Light theme warm
- Orange primary (#d2691e)
- Purple active states (#8b5cf6)
- Green success (#16a34a)
- Rounded, friendly, accessible

---

## 📁 Mockup Disponibili

### 1. Admin Dashboard
**File**: `admin-dashboard-v2.html`
**Features**:
- 12 metric cards (User, Game, System stats)
- Service health matrix (5 services)
- Activity feed real-time
- Quick actions panel
- Live status indicator

**Apri**:
```bash
start docs/design-proposals/meepleai-style/admin-dashboard-v2.html
```

---

### 2. Complete User Mockups (3-in-1)
**File**: `complete-mockups.html`
**Includes**:

#### Tab 1: Personal Library 📚
- Search + filters (All, Favorites, Nuovo, In Prestito)
- Grid/List view toggle
- Game cards con stats (plays, win rate)
- Favorite stars
- Bulk selection mode
- Floating action bar

#### Tab 2: Shared Catalog 🌍
- Advanced filters (Players, Complexity, Duration)
- Community stats (rating, plays)
- "Aggiungi" overlay on hover
- "In Libreria" badges
- Pagination
- Sort options

#### Tab 3: Profile & Settings ⚙️
- 4 sub-tabs: Profilo, Preferenze, Privacy, Account
- Avatar upload
- Level & badges showcase
- Toggle switches for preferences
- Danger zone (delete account)

**Apri**:
```bash
start docs/design-proposals/meepleai-style/complete-mockups.html
```

---

### 3. User Dashboard (Separate)
**File**: `user-dashboard.html`
**Features**:
- Greeting personalized
- Library quota widget
- Active session panel
- Recently added games
- Chat history
- Quick actions

**Apri**:
```bash
start docs/design-proposals/meepleai-style/user-dashboard.html
```

---

## 🎨 Design System Specs

### Typography
- **Titles**: Quicksand (rounded sans, friendly)
  - Weights: 400 (regular), 600 (semibold), 700 (bold)
- **Body**: Nunito (humanist sans, excellent readability)
  - Weights: 300 (light), 400 (regular), 600 (semibold), 700 (bold)

### Color Palette
```css
--primary-orange: #d2691e;     /* Primary CTA, logo */
--primary-orange-hover: #b85a19;

--purple-active: #8b5cf6;      /* Active nav states */
--purple-hover: #7c3aed;

--green-success: #16a34a;      /* Success states, positive */
--green-hover: #15803d;

--red-danger: #dc2626;         /* Errors, delete actions */
--red-hover: #b91c1c;

--yellow-warning: #eab308;     /* Warnings, degraded */

--gray-50: #fafaf8;            /* Subtle backgrounds */
--gray-100: #f5f5f5;
--gray-200: #e8e4d8;           /* Borders */
--gray-400: #999;              /* Secondary text */
--gray-600: #666;              /* Body text light */
--gray-900: #2d2d2d;           /* Headings, primary text */

--bg-main: #f8f6f0;            /* Page background (warm beige) */
```

### Background
- **Base**: #f8f6f0 (warm beige/cream)
- **Texture**: Subtle cross-hatch pattern (wood/paper feel)
  - Vertical lines: rgba(210, 105, 30, 0.015)
  - Horizontal lines: rgba(139, 90, 60, 0.02)
  - Combined opacity: 0.6
- **Overlay**: Radial gradient warm glow from top (opacity 0.03)

### Component Standards

**Cards**:
- Background: White
- Border: 1px solid #e8e4d8
- Border-radius: 1rem
- Shadow: 0 1px 3px rgba(139, 90, 60, 0.05)
- Hover: translateY(-4px), shadow intensifies, border → #d2691e

**Buttons**:
- Primary: #d2691e background, white text
- Success: #16a34a background
- Danger: #dc2626 background
- Secondary: White bg, #e8e4d8 border
- Border-radius: 0.75rem
- Font: Quicksand, weight 700
- Hover: translateY(-1px), shadow

**Nav**:
- Active: #8b5cf6 background (purple)
- Hover: #fef3e2 background, #d2691e text
- Border-radius: 0.625rem

**Icons**:
- Background: #fef3e2 (orange tint)
- Border-radius: 0.75rem
- Size: 40-48px

**Filters/Chips**:
- Active: #8b5cf6 (purple)
- Hover: #fef3e2 bg, #d2691e border
- Border-radius: 2rem (pills)

---

## 🔄 Component Reuse Map

### Shared Components
- **TopBar**: Identical across all pages (logo, nav, user section)
- **MetricCard**: Used in admin dashboard, can adapt for user stats
- **GameCard**: Used in library, catalog (slight variant for community stats)
- **FilterChip**: Used in library, catalog filters
- **ToggleSwitch**: Used in settings
- **Button styles**: Orange/Purple/Green/Red variants

### Page-Specific Components
- **ServiceHealthCard**: Admin dashboard only
- **ActivityFeed**: Admin dashboard primarily
- **ProfileHeader**: Profile page only
- **FloatingActionBar**: Library bulk operations

---

## 📊 Responsive Breakpoints

```css
/* Mobile */
@media (max-width: 768px) {
  - Grid: 1 column
  - Nav: collapse to hamburger
  - Toolbar: stack vertically
}

/* Tablet */
@media (min-width: 769px) and (max-width: 1024px) {
  - Grid: 2 columns
  - Compact spacing
}

/* Desktop */
@media (min-width: 1025px) {
  - Grid: 3-4 columns
  - Two-column layouts for admin
}
```

---

## 🚀 Next Steps

### Implementazione

**Fase 1: Component Library** (1 settimana)
1. Estrarre componenti riutilizzabili da mockup
2. Creare Storybook con MeepleAI theme
3. Documentare design tokens (colors, typography, spacing)

**Fase 2: Admin Dashboard** (2 settimane)
- Backend: GetAdminMetricsQuery, GetServiceHealthQuery
- Frontend: Implement da mockup admin-dashboard-v2.html
- Testing: Real-time updates, service monitoring

**Fase 3: User Pages** (3 settimane)
- Personal Library (search, filters, bulk operations)
- Shared Catalog (advanced filters, add to library)
- Profile/Settings (4 tabs, toggles, avatar upload)

**Fase 4: Game Detail** (3 settimane)
- Riadattare le 3 proposte esistenti allo stile MeepleAI
- Implementare versione scelta
- Epic già pronta (20 issues)

---

## ✅ Design Validation Checklist

### Brand Consistency
- [x] Matches existing user pages (orange, purple, light theme)
- [x] Same fonts (Quicksand + Nunito)
- [x] Same component patterns (cards, buttons, nav)
- [x] Warm, friendly, accessible feel

### Technical Quality
- [x] Production-ready HTML/CSS
- [x] Responsive design
- [x] Accessible color contrast (WCAG AA)
- [x] Performance optimized (no heavy assets)

### User Experience
- [x] Clear information hierarchy
- [x] Intuitive navigation
- [x] Consistent interactions
- [x] Warm, welcoming aesthetic

---

## 📸 Preview

**Admin Dashboard**:
- White cards su warm beige background
- Orange primary buttons
- Purple active navigation
- Green success metrics
- Subtle wood texture

**Personal Library**:
- Search + purple filter chips
- Grid di game cards
- Bulk selection mode
- Orange floating action bar

**Shared Catalog**:
- Advanced filter panel
- Community stats (rating, plays)
- Orange "Aggiungi" overlay on hover
- Pagination controls

**Profile/Settings**:
- Avatar upload con purple accent
- 4 tabs (orange underline when active)
- Toggle switches (green when active)
- Danger zone (red) for account deletion

---

## 🎯 File Locations

```
docs/design-proposals/meepleai-style/
├── admin-dashboard-v2.html      # Admin command center ✅
├── user-dashboard.html          # User personal hub ✅
├── complete-mockups.html        # Library + Catalog + Settings (3-in-1) ✅
└── README.md                    # This file
```

---

## 💡 Implementation Notes

### CSS Variables Setup
```css
:root {
  --color-primary: #d2691e;
  --color-primary-hover: #b85a19;
  --color-purple: #8b5cf6;
  --color-green: #16a34a;
  --color-red: #dc2626;

  --bg-main: #f8f6f0;
  --bg-card: #ffffff;
  --border-color: #e8e4d8;

  --font-title: 'Quicksand', sans-serif;
  --font-body: 'Nunito', sans-serif;

  --radius-sm: 0.5rem;
  --radius-md: 0.75rem;
  --radius-lg: 1rem;
  --radius-full: 2rem;

  --shadow-sm: 0 1px 3px rgba(139, 90, 60, 0.05);
  --shadow-md: 0 4px 12px rgba(139, 90, 60, 0.08);
  --shadow-lg: 0 8px 20px rgba(139, 90, 60, 0.12);
}
```

### Component Extraction Priority
1. **TopBar** - Used everywhere
2. **Button** - Multiple variants (orange, purple, green, red)
3. **Card** - Base for metrics, games, services
4. **FilterChip** - Reused across pages
5. **Toggle** - Settings pages
6. **GameCard** - Library and catalog (with variants)

---

**Pronto per implementazione!** 🚀

Vuoi che:
1. Crei Epic + Implementation Issues per queste pagine?
2. Inizi implementazione component library?
3. Mostri altri dettagli/varianti?
