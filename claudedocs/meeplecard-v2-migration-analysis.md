# MeepleCard v2 Migration Analysis

**Date**: 2026-02-17
**Scope**: Complete migration from current MeepleCard to v2 design (mockup approved)
**Approach**: Big Bang (all 78 files at once)

## Design Comparison

### Visual Differences

| Aspect | Current | v2 Mockup | Action Required |
|--------|---------|-----------|-----------------|
| **Shadows** | Standard Tailwind | Warm-toned custom shadows | Add --shadow-warm-* tokens |
| **Backdrop** | None | blur(12px) + saturate(180%) | Add backdrop-filter |
| **Hover transform** | -translate-y-1 | -translate-y-2 (6px) | Update transform values |
| **Entity accent** | 4px static | 4px → 6px on hover | Already implemented ✅ |
| **Gradient overlay** | Basic linear | Entity-colored gradients | Add dynamic gradients |
| **Shimmer effect** | None | Animated on hover | Already in globals.css ✅ |
| **Glow outline** | None | Entity-colored outline on hover | Add outline-2 + color |
| **Aspect ratios** | All 7/10 | Game: 7/10, Chat: 4/3 | Conditional aspect ratio |

### Typography

| Element | Current | v2 | Status |
|---------|---------|-----|--------|
| Display font | Quicksand | Quicksand | ✅ Match |
| Body font | Inter/Nunito | Nunito | ⚠️ Verify usage |
| Title size | 1rem/1.05rem | 1.05rem | Minor adjust |
| Subtitle | 0.875rem | 0.82rem | Minor adjust |

### Color System

**Current**: Entity colors defined in component (entityColors object)
**v2**: CSS variables for reusability

```css
/* Need to add to globals.css */
:root {
  --e-game: 25, 95%, 45%;
  --e-player: 262, 83%, 58%;
  --e-session: 240, 60%, 55%;
  --e-agent: 38, 92%, 50%;
  --e-document: 210, 40%, 55%;
  --e-chat: 220, 80%, 55%;
  --e-event: 350, 89%, 60%;
}
```

### Animations

**Missing from current** (need to add to tailwind.config.js):

```javascript
keyframes: {
  'pulse-glow': {
    '0%, 100%': { boxShadow: '0 0 0 0 currentColor' },
    '50%': { boxShadow: '0 0 12px 4px currentColor' },
  },
  'unread-bounce': {
    '0%, 100%': { transform: 'scale(1)' },
    '30%': { transform: 'scale(1.2)' },
    '60%': { transform: 'scale(0.95)' },
  },
  'live-pulse': {
    '0%, 100%': { opacity: '1' },
    '50%': { opacity: '0.4' },
  },
  'spin-slow': {
    '0%': { transform: 'rotate(0deg)' },
    '100%': { transform: 'rotate(360deg)' },
  },
}
```

## Component Structure Changes

### Image Placeholder Strategy

**Current**: Single placeholder SVG for all entities
**v2**: Entity-specific placeholders

| Entity | v2 Placeholder | Implementation |
|--------|----------------|----------------|
| Game (no image) | Gradient + DiceIcon 3D | Create DiceIcon component |
| Chat | Gradient + 💬 emoji | Text emoji fallback |
| Agent | Gradient + 🤖 emoji | Text emoji fallback |
| Player | Gradient + avatar initials | Generate initials |
| Event | Gradient + 🏆 emoji | Text emoji fallback |

**Decision**: Create `DiceIcon3D` component for game placeholders, use emoji for others.

### Cover Component Updates

**Changes needed** in `CoverImage` subcomponent:

```typescript
// v2 gradient overlays (entity-colored)
const gradientOverlay =
  variant === 'hero'
    ? `linear-gradient(180deg, transparent 0%, hsla(${color}, 0.1) 30%, hsla(${color}, 0.6) 70%, hsla(${color}, 0.9) 100%)`
    : variant === 'featured'
      ? `linear-gradient(180deg, transparent 40%, hsla(${color}, 0.15) 70%, hsla(${color}, 0.4) 100%)`
      : `linear-gradient(180deg, transparent 50%, hsl(var(--card)) 100%)`;

// Shimmer already implemented ✅
// Add placeholder logic based on entity type
```

### Feature Components Updates

**ChatStatusBadge** (`meeple-card-features/ChatStatusBadge.tsx`):
- v2 uses more vibrant colors with live-pulse animation for "active" status
- Add dot indicator for active state

**ChatUnreadBadge** (`meeple-card-features/ChatUnreadBadge.tsx`):
- Current: positioned absolute top-right
- v2: Same position but with unread-bounce animation on mount

**AgentStatusBadge** (`meeple-card-features/AgentStatusBadge.tsx`):
- Add live-pulse animation for "active" status
- Update color palette to match v2

## CSS Tokens to Add

### Shadow System (Warm-toned)

```css
:root {
  /* Light mode */
  --shadow-warm-sm: 0 1px 3px rgba(180,130,80,0.06), 0 1px 2px rgba(180,130,80,0.04);
  --shadow-warm-md: 0 4px 12px rgba(180,130,80,0.08), 0 2px 4px rgba(180,130,80,0.04);
  --shadow-warm-lg: 0 10px 30px rgba(180,130,80,0.12), 0 4px 8px rgba(180,130,80,0.06);
  --shadow-warm-xl: 0 20px 50px rgba(180,130,80,0.16), 0 8px 16px rgba(180,130,80,0.08);
  --shadow-warm-2xl: 0 25px 60px rgba(180,130,80,0.2), 0 12px 24px rgba(180,130,80,0.1);
}

.dark {
  /* Dark mode - stronger shadows */
  --shadow-warm-sm: 0 1px 3px rgba(0,0,0,0.2);
  --shadow-warm-md: 0 4px 12px rgba(0,0,0,0.3);
  --shadow-warm-lg: 0 10px 30px rgba(0,0,0,0.4);
  --shadow-warm-xl: 0 20px 50px rgba(0,0,0,0.5);
  --shadow-warm-2xl: 0 25px 60px rgba(0,0,0,0.6);
}
```

### Entity Color Variables

```css
:root {
  --e-game: 25, 95%, 45%;
  --e-player: 262, 83%, 58%;
  --e-session: 240, 60%, 55%;
  --e-agent: 38, 92%, 50%;
  --e-document: 210, 40%, 55%;
  --e-chat: 220, 80%, 55%;
  --e-event: 350, 89%, 60%;
}
```

## File Impact Analysis

### Core Component
- `meeple-card.tsx` (1205 lines) — **Major refactor**

### Feature Components (8 files)
1. `ChatStatusBadge.tsx` — Update colors + add live-pulse
2. `ChatUnreadBadge.tsx` — Add unread-bounce animation
3. `AgentStatusBadge.tsx` — Update colors + add live-pulse
4. `QuickActionsMenu.tsx` — Minor style updates
5. `StatusBadge.tsx` — Color updates
6. `TagStrip.tsx` — Verify compatibility
7. `FlipCard.tsx` — Verify flip transition
8. `DragHandle.tsx` — No changes needed

### Consumer Files (78 total)

**By Category**:
- Games pages: ~15 files
- Library pages: ~12 files
- Agent pages: ~8 files
- Chat pages: ~10 files
- Admin/Dashboard: ~8 files
- Components: ~15 files
- Tests: ~10 files

**Props Changes**: None expected (backward compatible)
**Visual Changes**: All cards will render with v2 styles automatically

### Test Files (8 files)

1. `meeple-card.test.tsx` — Update snapshots
2. `meeple-card.a11y.test.tsx` — Verify contrast ratios
3. `meeple-card.snapshot.test.tsx` — Regenerate all snapshots
4. `meeple-card-agent.test.tsx` — Update agent-specific tests
5. `meeple-card-chat.test.tsx` — Update chat-specific tests
6. `meeple-card-contexts.test.tsx` — Verify context handling
7. `meeple-card-improvements.test.tsx` — Update improvement tests
8. `meeple-card-permissions.test.tsx` — No changes expected

## New Components Needed

### DiceIcon3D Component

**Path**: `components/ui/icons/dice-icon-3d.tsx`

**Specs**:
- SVG isometric dice (3 visible faces)
- Purple/pink gradient coloring
- Subtle rotation on parent hover
- Size prop: sm (40px), md (80px), lg (120px)
- Responsive and accessible

**Usage**:
```tsx
<DiceIcon3D size="md" className="opacity-60" />
```

## Implementation Checklist

### Phase 1: Foundation (Tasks 1-3)
- [x] Task 1: Analysis complete
- [ ] Task 2: Add CSS tokens to globals.css
- [ ] Task 3: Create DiceIcon3D component

### Phase 2: Core Component (Tasks 4-5)
- [ ] Task 4: Update meeple-card.tsx
  - Add conditional aspect ratios
  - Update gradient overlays
  - Add placeholder logic
  - Update hover effects
- [ ] Task 5: Update feature components (8 files)

### Phase 3: Consumer Migration (Task 6)
- [ ] Batch 1: Game pages (~15 files)
- [ ] Batch 2: Library pages (~12 files)
- [ ] Batch 3: Agent pages (~8 files)
- [ ] Batch 4: Chat pages (~10 files)
- [ ] Batch 5: Admin/Dashboard (~8 files)
- [ ] Batch 6: Other components (~15 files)

### Phase 4: Cleanup (Tasks 7-9)
- [ ] Task 7: Update test suite (8 files)
- [ ] Task 8: Remove legacy components
- [ ] Task 9: Final QA + visual validation

## Risk Mitigation

### Performance
- **Concern**: Backdrop-filter can be GPU-intensive
- **Mitigation**: Use will-change: transform on hover only
- **Testing**: Lighthouse performance audit required

### Accessibility
- **Concern**: New contrast ratios with entity colors
- **Mitigation**: Verify WCAG AA compliance (4.5:1)
- **Testing**: axe-core audit on all variants

### Visual Regression
- **Concern**: 78 files = high regression risk
- **Mitigation**: Batch testing per entity type with checkpoints
- **Testing**: Screenshot comparison before/after

## Effort Breakdown

| Phase | Files | Lines Changed | Estimated Time |
|-------|-------|---------------|----------------|
| Foundation | 3 | ~500 | 4-6h |
| Core Component | 9 | ~1500 | 6-8h |
| Consumer Migration | 78 | ~2000 | 8-12h |
| Cleanup & QA | 8+ | ~500 | 4-6h |
| **Total** | **98+** | **~4500** | **22-32h** |

## Next Actions

1. ✅ Complete Task 1 (this document)
2. ▶️ Start Task 2: Add CSS tokens
3. ▶️ Start Task 3: Create DiceIcon3D
4. Continue with Tasks 4-9 sequentially

---

**Status**: Task 1 complete — Ready for implementation phase
