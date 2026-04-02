# Epic #4068 - Gap Analysis & Completion Plan

**Generated**: 2026-02-13
**Branch**: feature/issue-4177-permission-model
**Overall Completion**: 45% (weighted)

---

## 🎯 Issue Status Matrix

| Issue | % Complete | Critical Gaps | Effort to Complete |
|-------|-----------|---------------|-------------------|
| #4177 Permission Model | 89% | Migration file only | 1-2h |
| #4178 Permission Hooks | 71% | Gate components, tests | 4-6h |
| #4179 MeepleCard Integration | **0%** | ENTIRE ISSUE | 2-3 days ⚠️ |
| #4186 Tooltip Positioning | 25% | Full implementation | 3-4 days ⚠️ |
| #4180 Tooltip A11y | **0%** | ENTIRE ISSUE | 2-3 days ⚠️ |
| #4181 Vertical Tags | **100%** | None - COMPLETE ✅ | 0h |
| #4182 Tag Integration | **0%** | ENTIRE ISSUE | 2 days ⚠️ |
| #4183 Collection Limits | 25% | UI components | 2-3 days |
| #4184 Agent Metadata | 38% | StatusBadge, capabilities | 1-2 days |
| #4185 Testing & Docs | 60% | E2E, A11y, visual tests | 2-3 days |

**Total Remaining Effort**: 15-23 days (3-4.5 weeks) at 1 developer

---

## 🚨 Critical Blocking Issues (Must Complete First)

### Issue #4179 - MeepleCard Permission Integration
**Status**: ❌ 0% - ZERO IMPLEMENTATION
**Blocks**: Entire permission system UX
**Effort**: 2-3 days

#### Missing Components (ALL)
1. **Import & Use Hook**
   ```tsx
   // MISSING in meeple-card.tsx
   import { usePermissions } from '@/contexts/PermissionContext';
   const { canAccess, tier, role } = usePermissions();
   ```

2. **Feature Conditional Logic** (NOT IMPLEMENTED)
   ```tsx
   const showWishlist = props.showWishlist && canAccess('wishlist');
   const enableDrag = props.draggable && canAccess('drag-drop');
   const enableSelect = props.selectable && canAccess('bulk-select');
   ```

3. **Quick Actions Filtering** (NOT IMPLEMENTED)
   ```tsx
   const filteredActions = props.quickActions?.filter(action => {
     if (action.adminOnly && !isAdmin(role)) return false;
     if (action.requiresTier && !hasTier(action.minTier)) return false;
     return true;
   });
   ```

4. **Tier Badge Display** (NOT IMPLEMENTED)
   ```tsx
   {tier && <TierBadge tier={tier} />}
   ```

5. **Upgrade Prompts** (NOT IMPLEMENTED)
   ```tsx
   {!canAccess('feature') && <UpgradePrompt requiredTier="Pro" />}
   ```

6. **Permission Override Prop** (NOT IMPLEMENTED)
   ```tsx
   // MeepleCardProps missing
   permissions?: { tier: UserTier; role: UserRole; canAccess: (f: string) => boolean };
   ```

#### Files to Modify
- `apps/web/src/components/ui/data-display/meeple-card.tsx` (ADD permission integration)
- `apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx` (ADD filtering)
- `apps/web/src/components/ui/feedback/tier-badge.tsx` (CREATE new component)
- `apps/web/src/components/ui/feedback/upgrade-prompt.tsx` (CREATE new component)

#### Tests Required
- [ ] Free tier: limited features test
- [ ] Pro tier: all features test
- [ ] Admin role: admin actions test
- [ ] Permission override prop test

---

### Issue #4186 - Tooltip Positioning System
**Status**: ❌ 25% - Only stub tests exist
**Blocks**: #4180 (Accessibility)
**Effort**: 3-4 days

#### What Exists
- `lib/tooltip/__tests__/positioning.test.ts` - 2 basic tests (23 lines)

#### Missing Implementation (ALL)
1. **calculateOptimalPosition() Function** (SPEC exists, CODE missing)
   - Algorithm in spec (lines 78-137) NOT implemented
   - Viewport boundary detection logic
   - Auto-flip top/bottom, left/right
   - Space calculation in 4 directions

2. **useSmartTooltip() Hook** (SPEC exists, CODE missing)
   - Position state management
   - Debounced scroll/resize handlers
   - IntersectionObserver integration
   - Performance optimization (<16ms)

3. **SmartTooltip Component** (NOT CREATED)
   - Wrapper around base Tooltip
   - Auto-positioning integration
   - Responsive behavior

4. **Collision Detection** (NOT IMPLEMENTED)
   - Detect navbar/sidebar overlap
   - Multi-tooltip conflict resolution

#### Files to Create
- `apps/web/src/lib/tooltip/positioning.ts` - Core algorithm (200-300 lines)
- `apps/web/src/hooks/useSmartTooltip.ts` - Hook implementation (150-200 lines)
- `apps/web/src/components/ui/overlays/smart-tooltip.tsx` - Component (100-150 lines)

#### Tests Required
- [ ] All 4 viewport corners (edge cases)
- [ ] Performance: <16ms calculation benchmark
- [ ] No jank during scroll/resize
- [ ] Multiple tooltips no overlap
- [ ] Very large content handling

---

### Issue #4180 - Tooltip Accessibility WCAG 2.1 AA
**Status**: ❌ 0% - NOT STARTED
**Blocks**: Quality gate, compliance
**Effort**: 2-3 days
**Depends on**: #4186

#### Missing Implementation (ALL)
1. **Keyboard Navigation**
   - Tab focus handling
   - Enter/Space activation
   - Escape dismissal
   - Focus trap for interactive tooltips

2. **Screen Reader Support**
   - `aria-describedby` linkage
   - `role="tooltip"` attribute
   - Live region updates
   - Proper announcements

3. **Mobile Touch**
   - Tap activation (no hover)
   - Tap-outside dismiss
   - Touch-friendly close button
   - 44x44px minimum touch targets

4. **WCAG AA Compliance**
   - Contrast checking (4.5:1 / 3:1)
   - High contrast mode support
   - Focus indicators
   - Color independence

5. **Testing Tools Integration**
   - axe-core audit setup
   - Lighthouse A11y tests
   - WAVE validation
   - Manual screen reader testing

#### Files to Create/Modify
- Enhance `apps/web/src/components/ui/overlays/tooltip.tsx` with A11y
- Create `apps/web/src/lib/a11y/tooltip-a11y.ts` utilities
- Add `apps/web/src/__tests__/a11y/tooltip.a11y.test.tsx`

#### Tests Required
- [ ] Keyboard-only navigation flow
- [ ] Screen reader announcements (manual)
- [ ] Mobile touch interactions
- [ ] Contrast ratio validation
- [ ] Focus management edge cases
- [ ] axe-core: 0 violations

---

### Issue #4182 - Tag Integration in MeepleCard
**Status**: ❌ 0% - TagStrip exists but NOT integrated
**Blocks**: Tag system UX
**Effort**: 2 days

#### What Exists
- ✅ `TagStrip.tsx` component (complete, tested)
- ✅ `TagBadge.tsx`, `TagOverflow.tsx` sub-components
- ✅ `presets.ts` entity-specific tags
- ❌ NO integration in MeepleCard

#### Missing Integration
1. **MeepleCardProps Extension**
   ```tsx
   // MISSING in MeepleCardProps
   tags?: Tag[];
   maxVisibleTags?: number;
   showTagStrip?: boolean;
   ```

2. **TagStrip Rendering in MeepleCard**
   ```tsx
   // MISSING in meeple-card.tsx
   import { TagStrip } from '@/components/ui/tags/TagStrip';

   {showTagStrip && tags && (
     <TagStrip
       tags={tags}
       maxVisible={maxVisibleTags ?? 3}
       variant={isMobile ? 'mobile' : isTablet ? 'tablet' : 'desktop'}
       position="left"
     />
   )}
   ```

3. **Variant-Specific Behavior**
   - Grid: vertical strip left edge ✅ (ready)
   - List: horizontal strip top ❌ (needs implementation)
   - Compact: minimal tag display ❌ (needs implementation)
   - Featured/Hero: prominent tags ❌ (needs implementation)

4. **Animation Integration**
   - Staggered fade-in (50ms delay per tag)
   - Smooth transitions

#### Files to Modify
- `apps/web/src/components/ui/data-display/meeple-card.tsx` (ADD TagStrip import + rendering)
- `apps/web/src/components/ui/tags/TagStrip.tsx` (ADD variant-specific layouts)

#### Tests Required
- [ ] All 5 variants render tags
- [ ] No layout breaking
- [ ] Overflow tooltip works
- [ ] Animations smooth
- [ ] Responsive behavior correct

---

## 📊 Completion Sequence (Critical Path)

### Phase 1: Foundation (3-4 days)
**Parallel Track A**: Tooltip System
1. #4186 - Tooltip Positioning (3-4 days)
   - Implement `calculateOptimalPosition()`
   - Create `useSmartTooltip()` hook
   - Build `SmartTooltip` component
   - Write positioning tests

**Parallel Track B**: Complete Partials (2-3 days)
2. #4177 - Create DB migration (1-2h)
3. #4178 - Gate components + tests (4-6h)
4. #4183 - Collection Limit UI (2 days)
5. #4184 - AgentStatusBadge + capabilities (1-2 days)

### Phase 2: Integration (4-5 days)
**Sequential** (depends on Phase 1):
6. #4180 - Tooltip A11y (2-3 days) - requires #4186 ✅
7. #4182 - Tag Integration (2 days) - requires #4181 ✅ (already done)
8. #4179 - MeepleCard Permission (2-3 days) - requires #4178 ✅

### Phase 3: Quality Gate (2-3 days)
**Sequential** (depends on Phase 2):
9. #4185 - Testing & Validation
   - E2E test suite (1 day)
   - A11y audit (axe-core) (4h)
   - Visual regression (Chromatic) (4h)
   - Performance benchmarks (4h)

**Total Timeline**: 9-12 days (2-2.5 weeks) with parallelization

---

## 🛠️ Implementation Checklist by File

### Backend Files to Complete
- [ ] `apps/api/src/Api/Migrations/YYYYMMDDHHMMSS_AddUserTierAndRole.cs`
  - Add `Tier` column to User table
  - Add `Role` column to User table
  - Create indexes on tier, role
  - Set default values

### Frontend Files to Create
```
apps/web/src/
├── components/ui/
│   ├── feedback/
│   │   ├── tier-badge.tsx (NEW - 50 lines)
│   │   ├── upgrade-prompt.tsx (NEW - 80 lines)
│   │   └── __tests__/
│   │       ├── tier-badge.test.tsx (NEW)
│   │       └── upgrade-prompt.test.tsx (NEW)
│   ├── gates/
│   │   ├── permission-gate.tsx (NEW - 40 lines)
│   │   ├── tier-gate.tsx (NEW - 35 lines)
│   │   ├── role-gate.tsx (NEW - 35 lines)
│   │   └── __tests__/
│   │       ├── permission-gate.test.tsx (NEW)
│   │       ├── tier-gate.test.tsx (NEW)
│   │       └── role-gate.test.tsx (NEW)
│   └── overlays/
│       └── smart-tooltip.tsx (NEW - 150 lines)
├── lib/
│   ├── tooltip/
│   │   ├── positioning.ts (NEW - 250 lines)
│   │   └── collision-detection.ts (NEW - 150 lines)
│   └── a11y/
│       └── tooltip-a11y.ts (NEW - 100 lines)
├── hooks/
│   ├── useSmartTooltip.ts (NEW - 180 lines)
│   ├── useFocusTrap.ts (NEW - 120 lines)
│   └── __tests__/
│       ├── usePermissions.test.tsx (NEW)
│       └── useSmartTooltip.test.tsx (NEW)
└── __tests__/
    ├── e2e/
    │   └── epic-4068-permission-flows.spec.ts (NEW - 400 lines)
    └── a11y/
        └── tooltip.a11y.test.tsx (NEW - 200 lines)
```

### Frontend Files to Modify
- [ ] `apps/web/src/components/ui/data-display/meeple-card.tsx`
  - Import usePermissions hook (line ~60)
  - Add permission prop to interface (line ~128)
  - Add conditional rendering logic (lines ~600-650)
  - Filter quick actions by permission (line ~750)
  - Add tier badge display (line ~800)

- [ ] `apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx`
  - Add permission filtering logic
  - Add upgrade prompt for locked actions

- [ ] `apps/web/src/components/ui/tags/TagStrip.tsx`
  - Add list variant (horizontal layout)
  - Add compact variant (minimal)
  - Add featured/hero variants

---

## 🔍 Detailed Gap Analysis by Issue

### #4179 - MeepleCard Permission Integration (CRITICAL)

**Why it's blocking**: This is the USER-FACING integration. Without it, users can't see/use the permission system.

**Missing Implementation**:

1. **Hook Integration** (NOT DONE)
   ```tsx
   // CURRENT (meeple-card.tsx line 500+)
   export function MeepleCard(props: MeepleCardProps) {
     // NO permission hook usage

   // NEEDED
   export function MeepleCard(props: MeepleCardProps) {
     const { canAccess, tier, role, isAdmin } = usePermissions();
     const effectivePerms = props.permissions ?? { canAccess, tier, role, isAdmin };
   ```

2. **Props Extension** (NOT DONE)
   ```tsx
   // CURRENT MeepleCardProps (line 128-200)
   export interface MeepleCardProps {
     // ... existing props
     showWishlist?: boolean;
     selectable?: boolean;
     draggable?: boolean;
     // NO permission prop
   }

   // NEEDED
   export interface MeepleCardProps {
     // ... existing props
     permissions?: PermissionOverride; // For testing
   }

   interface PermissionOverride {
     tier: UserTier;
     role: UserRole;
     canAccess: (feature: string) => boolean;
     isAdmin: () => boolean;
   }
   ```

3. **Conditional Feature Rendering** (NOT DONE)
   ```tsx
   // CURRENT (meeple-card.tsx lines 600-700)
   {props.showWishlist && <WishlistButton />}
   {props.selectable && <Checkbox />}
   {props.draggable && <DragHandle />}

   // NEEDED
   {props.showWishlist && canAccess('wishlist') && <WishlistButton />}
   {props.selectable && canAccess('bulk-select') && <Checkbox />}
   {props.draggable && canAccess('drag-drop') && <DragHandle />}
   ```

4. **Quick Actions Permission Filter** (NOT DONE)
   ```tsx
   // CURRENT: No filtering
   <QuickActionsMenu actions={props.quickActions} />

   // NEEDED
   const filteredActions = props.quickActions?.filter(action => {
     if (action.adminOnly) return isAdmin();
     if (action.requiresTier) return hasTier(action.minTier);
     return true;
   });
   <QuickActionsMenu
     actions={filteredActions}
     onLockedClick={(action) => showUpgradePrompt(action.minTier)}
   />
   ```

5. **Tier Badge Component** (NOT CREATED)
   - File: `apps/web/src/components/ui/feedback/tier-badge.tsx`
   - Display user tier (Free/Normal/Pro/Enterprise)
   - Color-coded (gray/blue/purple/gold)
   - Positioned top-right corner card

6. **Upgrade Prompt Component** (NOT CREATED)
   - File: `apps/web/src/components/ui/feedback/upgrade-prompt.tsx`
   - Shows when locked feature clicked
   - CTA: "Upgrade to Pro"
   - Modal or toast notification

**Test Coverage Needed**:
- `__tests__/meeple-card-permissions.test.tsx` (NEW - 300+ lines)
  - Free tier: wishlist visible, bulk-select hidden
  - Pro tier: all features visible
  - Admin: admin actions visible
  - Permission override works

---

### #4186 - Tooltip Positioning System

**Why it's blocking**: Foundation for #4180 (A11y). Without smart positioning, tooltips break UX.

**Missing Implementation**:

1. **Core Positioning Algorithm** (SPEC line 78-137, CODE missing)
   ```typescript
   // FILE: apps/web/src/lib/tooltip/positioning.ts (CREATE)

   export interface TooltipPosition {
     top?: number;
     bottom?: number;
     left?: number;
     right?: number;
     placement: 'top' | 'bottom' | 'left' | 'right';
   }

   export function calculateOptimalPosition(
     triggerRect: DOMRect,
     tooltipSize: { width: number; height: number },
     viewportSize: { width: number; height: number }
   ): TooltipPosition {
     // Algorithm from spec (140 lines of logic)
     // 1. Calculate space in 4 directions
     // 2. Prefer vertical (top/bottom) over horizontal
     // 3. Auto-flip if insufficient space
     // 4. Clamp to viewport bounds
     // 5. Return position object
   }
   ```

2. **Smart Tooltip Hook** (SPEC line 140-180, CODE missing)
   ```typescript
   // FILE: apps/web/src/hooks/useSmartTooltip.ts (CREATE)

   export function useSmartTooltip(options?: SmartTooltipOptions) {
     const [position, setPosition] = useState<TooltipPosition | null>(null);
     const triggerRef = useRef<HTMLElement>(null);
     const tooltipRef = useRef<HTMLDivElement>(null);

     useEffect(() => {
       // Debounced position calculator (100ms)
       const updatePosition = debounce(() => {
         if (!triggerRef.current || !tooltipRef.current) return;

         const triggerRect = triggerRef.current.getBoundingClientRect();
         const tooltipRect = tooltipRef.current.getBoundingClientRect();
         const viewport = { width: window.innerWidth, height: window.innerHeight };

         const newPos = calculateOptimalPosition(triggerRect, tooltipRect, viewport);
         setPosition(newPos);
       }, 100);

       // Listen scroll/resize
       window.addEventListener('scroll', updatePosition);
       window.addEventListener('resize', updatePosition);

       return () => {
         window.removeEventListener('scroll', updatePosition);
         window.removeEventListener('resize', updatePosition);
       };
     }, []);

     return { position, triggerRef, tooltipRef };
   }
   ```

3. **SmartTooltip Component** (NOT CREATED)
   ```tsx
   // FILE: apps/web/src/components/ui/overlays/smart-tooltip.tsx (CREATE)

   export function SmartTooltip({ children, content, ...props }: SmartTooltipProps) {
     const { position, triggerRef, tooltipRef } = useSmartTooltip();

     return (
       <TooltipProvider>
         <Tooltip>
           <TooltipTrigger ref={triggerRef}>{children}</TooltipTrigger>
           <TooltipContent
             ref={tooltipRef}
             style={position ? {
               top: position.top,
               bottom: position.bottom,
               left: position.left,
               right: position.right
             } : undefined}
             data-placement={position?.placement}
           >
             {content}
           </TooltipContent>
         </Tooltip>
       </TooltipProvider>
     );
   }
   ```

4. **Collision Detection** (NOT IMPLEMENTED)
   ```typescript
   // FILE: apps/web/src/lib/tooltip/collision-detection.ts (CREATE)

   export function detectCollisions(
     tooltipRect: DOMRect,
     excludeSelectors: string[] = ['.navbar', '.sidebar', '[role="tooltip"]']
   ): DOMRect[] {
     // Query all potential collision elements
     // Check intersection
     // Return colliding elements
   }

   export function adjustForCollisions(
     position: TooltipPosition,
     collisions: DOMRect[]
   ): TooltipPosition {
     // Offset position to avoid collisions
     // Maintain viewport bounds
   }
   ```

5. **Performance Optimization** (NOT IMPLEMENTED)
   - IntersectionObserver for visibility
   - RequestAnimationFrame for smooth updates
   - Memoization for position calculations
   - Benchmark: <16ms target

**Test Coverage Needed**:
- `__tests__/lib/tooltip/positioning.test.ts` (EXPAND from 23 lines to 300+)
  - Edge test: top-left corner
  - Edge test: top-right corner
  - Edge test: bottom-left corner
  - Edge test: bottom-right corner
  - Performance: benchmark <16ms
  - Scroll/resize: no jank
  - Multi-tooltip: no overlap
  - Large content: handled gracefully

---

### #4180 - Tooltip Accessibility WCAG 2.1 AA

**Why it's blocking**: P1-High compliance requirement. Can't ship without A11y.

**Missing Implementation**:

1. **Keyboard Navigation Enhancement**
   ```tsx
   // ENHANCE: apps/web/src/components/ui/overlays/tooltip.tsx

   function AccessibleTooltip({ children, content }: TooltipProps) {
     const [isOpen, setIsOpen] = useState(false);
     const triggerId = useId();
     const tooltipId = useId();

     const handleKeyDown = (e: KeyboardEvent) => {
       if (e.key === 'Enter' || e.key === ' ') {
         e.preventDefault();
         setIsOpen(prev => !prev);
       }
       if (e.key === 'Escape' && isOpen) {
         setIsOpen(false);
         // Return focus to trigger
         document.getElementById(triggerId)?.focus();
       }
     };

     return (
       <div
         id={triggerId}
         role="button"
         tabIndex={0}
         aria-describedby={isOpen ? tooltipId : undefined}
         aria-expanded={isOpen}
         onKeyDown={handleKeyDown}
       >
         {children}
         {isOpen && (
           <div id={tooltipId} role="tooltip" aria-live="polite">
             {content}
           </div>
         )}
       </div>
     );
   }
   ```

2. **Mobile Touch Support**
   ```tsx
   // ADD to tooltip.tsx

   const isMobile = useMediaQuery('(max-width: 768px)');

   const handleTouch = (e: TouchEvent) => {
     if (isMobile) {
       e.preventDefault();
       setIsOpen(true);
     }
   };

   const handleTouchOutside = (e: TouchEvent) => {
     if (!tooltipRef.current?.contains(e.target)) {
       setIsOpen(false);
     }
   };

   // Mobile dismiss button
   {isMobile && (
     <button
       className="absolute top-1 right-1 p-2 min-w-[44px] min-h-[44px]"
       onClick={() => setIsOpen(false)}
       aria-label="Close tooltip"
     >
       <X className="w-4 h-4" />
     </button>
   )}
   ```

3. **Focus Trap for Interactive Tooltips**
   ```tsx
   // FILE: apps/web/src/hooks/useFocusTrap.ts (CREATE)

   export function useFocusTrap(containerRef: RefObject<HTMLElement>, isActive: boolean) {
     useEffect(() => {
       if (!isActive || !containerRef.current) return;

       const container = containerRef.current;
       const focusableElements = container.querySelectorAll(
         'a, button, input, select, textarea, [tabindex]:not([tabindex="-1"])'
       );

       if (focusableElements.length === 0) return;

       const firstElement = focusableElements[0] as HTMLElement;
       const lastElement = focusableElements[focusableElements.length - 1] as HTMLElement;

       const handleTabKey = (e: KeyboardEvent) => {
         if (e.key !== 'Tab') return;

         if (e.shiftKey && document.activeElement === firstElement) {
           e.preventDefault();
           lastElement.focus();
         } else if (!e.shiftKey && document.activeElement === lastElement) {
           e.preventDefault();
           firstElement.focus();
         }
       };

       container.addEventListener('keydown', handleTabKey);
       return () => container.removeEventListener('keydown', handleTabKey);
     }, [isActive, containerRef]);
   }
   ```

4. **Contrast Validation**
   ```tsx
   // FILE: apps/web/src/lib/a11y/contrast.ts (CREATE)

   export function meetsWCAGAA(
     foreground: string,
     background: string,
     isLargeText: boolean = false
   ): boolean {
     const contrast = calculateContrastRatio(foreground, background);
     return isLargeText ? contrast >= 3 : contrast >= 4.5;
   }

   // Integrate in tooltip.tsx
   const tooltipClasses = cn(
     'bg-popover text-popover-foreground',
     // Ensure WCAG AA contrast
     'shadow-lg border border-border'
   );
   ```

5. **A11y Testing Suite**
   ```tsx
   // FILE: apps/web/src/__tests__/a11y/tooltip.a11y.test.tsx (CREATE)

   import { axe, toHaveNoViolations } from 'jest-axe';
   expect.extend(toHaveNoViolations);

   describe('Tooltip Accessibility', () => {
     it('has no axe violations', async () => {
       const { container } = render(<MeepleCard showWishlist />);
       const results = await axe(container);
       expect(results).toHaveNoViolations();
     });

     it('keyboard navigation works', () => {
       render(<MeepleCard />);
       const trigger = screen.getByRole('button');
       trigger.focus();
       fireEvent.keyDown(trigger, { key: 'Enter' });
       expect(screen.getByRole('tooltip')).toBeVisible();
       fireEvent.keyDown(trigger, { key: 'Escape' });
       expect(screen.queryByRole('tooltip')).not.toBeInTheDocument();
     });

     // + 15 more A11y tests
   });
   ```

**Implementation Steps**:
1. Add `permissions?: PermissionOverride` to MeepleCardProps
2. Import `usePermissions` hook in meeple-card.tsx
3. Add permission check logic before rendering features
4. Filter quick actions by permission
5. Add TierBadge component and integrate
6. Add UpgradePrompt component for locked features
7. Write permission integration tests (8+ scenarios)

**Estimated Lines of Code**: ~200 lines added to meeple-card.tsx + 150 lines new components

---

### #4186 - Tooltip Positioning System

**Missing Implementation**:

**Core Algorithm** (250 lines):
```typescript
// apps/web/src/lib/tooltip/positioning.ts

export interface ViewportBounds {
  top: number;
  right: number;
  bottom: number;
  left: number;
}

export interface SpaceAvailable {
  above: number;
  below: number;
  left: number;
  right: number;
}

export function calculateOptimalPosition(
  trigger: DOMRect,
  tooltip: { width: number; height: number },
  viewport: { width: number; height: number } = {
    width: window.innerWidth,
    height: window.innerHeight
  }
): TooltipPosition {
  const GAP = 8; // px spacing

  // Step 1: Calculate available space in each direction
  const space: SpaceAvailable = {
    above: trigger.top,
    below: viewport.height - trigger.bottom,
    left: trigger.left,
    right: viewport.width - trigger.right
  };

  // Step 2: Determine if tooltip fits in each direction
  const fitsAbove = space.above >= tooltip.height + GAP;
  const fitsBelow = space.below >= tooltip.height + GAP;
  const fitsLeft = space.left >= tooltip.width + GAP;
  const fitsRight = space.right >= tooltip.width + GAP;

  // Step 3: Prefer vertical placement (top/bottom)
  let placement: TooltipPosition['placement'];
  let position: Partial<TooltipPosition> = {};

  if (fitsBelow && space.below >= space.above) {
    // Place below
    placement = 'bottom';
    position.top = trigger.bottom + GAP;
    position.left = trigger.left + trigger.width / 2 - tooltip.width / 2;
  } else if (fitsAbove) {
    // Place above
    placement = 'top';
    position.bottom = viewport.height - trigger.top + GAP;
    position.left = trigger.left + trigger.width / 2 - tooltip.width / 2;
  } else if (fitsRight && space.right >= space.left) {
    // Place right
    placement = 'right';
    position.left = trigger.right + GAP;
    position.top = trigger.top + trigger.height / 2 - tooltip.height / 2;
  } else if (fitsLeft) {
    // Place left
    placement = 'left';
    position.right = viewport.width - trigger.left + GAP;
    position.top = trigger.top + trigger.height / 2 - tooltip.height / 2;
  } else {
    // Fallback: place below with best effort
    placement = 'bottom';
    position.top = trigger.bottom + GAP;
    position.left = trigger.left;
  }

  // Step 4: Clamp to viewport bounds
  if (position.left !== undefined) {
    position.left = Math.max(
      GAP,
      Math.min(position.left, viewport.width - tooltip.width - GAP)
    );
  }
  if (position.top !== undefined) {
    position.top = Math.max(
      GAP,
      Math.min(position.top, viewport.height - tooltip.height - GAP)
    );
  }

  return { ...position, placement } as TooltipPosition;
}

// Performance optimized version with memoization
export const calculateOptimalPositionMemo = memoize(
  calculateOptimalPosition,
  (trigger, tooltip, viewport) =>
    `${trigger.top}-${trigger.left}-${tooltip.width}x${tooltip.height}`
);
```

**Hook Implementation** (180 lines):
```typescript
// apps/web/src/hooks/useSmartTooltip.ts

export interface UseSmartTooltipOptions {
  enabled?: boolean;
  debounceMs?: number;
  repositionOnScroll?: boolean;
  repositionOnResize?: boolean;
}

export function useSmartTooltip(options: UseSmartTooltipOptions = {}) {
  const {
    enabled = true,
    debounceMs = 100,
    repositionOnScroll = true,
    repositionOnResize = true
  } = options;

  const [position, setPosition] = useState<TooltipPosition | null>(null);
  const [isVisible, setIsVisible] = useState(false);
  const triggerRef = useRef<HTMLElement>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);
  const updateTimeoutRef = useRef<NodeJS.Timeout>();

  const updatePosition = useCallback(() => {
    if (!enabled || !triggerRef.current || !tooltipRef.current) return;

    const startTime = performance.now();

    const triggerRect = triggerRef.current.getBoundingClientRect();
    const tooltipRect = tooltipRef.current.getBoundingClientRect();
    const viewport = {
      width: window.innerWidth,
      height: window.innerHeight
    };

    const newPosition = calculateOptimalPosition(
      triggerRect,
      { width: tooltipRect.width, height: tooltipRect.height },
      viewport
    );

    const elapsed = performance.now() - startTime;
    if (elapsed > 16) {
      console.warn(`Tooltip positioning slow: ${elapsed.toFixed(2)}ms`);
    }

    setPosition(newPosition);
  }, [enabled]);

  const debouncedUpdate = useMemo(
    () => debounce(updatePosition, debounceMs),
    [updatePosition, debounceMs]
  );

  // Initial position calculation
  useEffect(() => {
    if (isVisible) {
      updatePosition();
    }
  }, [isVisible, updatePosition]);

  // Scroll listener
  useEffect(() => {
    if (!repositionOnScroll || !isVisible) return;

    window.addEventListener('scroll', debouncedUpdate, { passive: true });
    return () => window.removeEventListener('scroll', debouncedUpdate);
  }, [repositionOnScroll, isVisible, debouncedUpdate]);

  // Resize listener
  useEffect(() => {
    if (!repositionOnResize || !isVisible) return;

    window.addEventListener('resize', debouncedUpdate);
    return () => window.removeEventListener('resize', debouncedUpdate);
  }, [repositionOnResize, isVisible, debouncedUpdate]);

  // Intersection Observer for performance
  useEffect(() => {
    if (!enabled || !triggerRef.current) return;

    const observer = new IntersectionObserver(
      (entries) => {
        const [entry] = entries;
        if (!entry.isIntersecting && isVisible) {
          setIsVisible(false);
        }
      },
      { threshold: 0 }
    );

    observer.observe(triggerRef.current);
    return () => observer.disconnect();
  }, [enabled, isVisible]);

  return {
    position,
    isVisible,
    setIsVisible,
    triggerRef,
    tooltipRef,
    updatePosition
  };
}
```

**Component Integration** (100 lines):
```tsx
// apps/web/src/components/ui/overlays/smart-tooltip.tsx

export function SmartTooltip({
  children,
  content,
  side = 'auto',
  delayDuration = 300,
  ...props
}: SmartTooltipProps) {
  const {
    position,
    isVisible,
    setIsVisible,
    triggerRef,
    tooltipRef
  } = useSmartTooltip({
    enabled: side === 'auto',
    debounceMs: 100
  });

  // If side is specified, use default Tooltip
  if (side !== 'auto') {
    return (
      <Tooltip delayDuration={delayDuration}>
        <TooltipTrigger>{children}</TooltipTrigger>
        <TooltipContent side={side} {...props}>
          {content}
        </TooltipContent>
      </Tooltip>
    );
  }

  // Smart positioning mode
  return (
    <TooltipProvider delayDuration={delayDuration}>
      <Tooltip open={isVisible} onOpenChange={setIsVisible}>
        <TooltipTrigger ref={triggerRef}>{children}</TooltipTrigger>
        <TooltipContent
          ref={tooltipRef}
          style={position ? {
            position: 'fixed',
            top: position.top,
            bottom: position.bottom,
            left: position.left,
            right: position.right
          } : undefined}
          data-placement={position?.placement}
          {...props}
        >
          {content}
        </TooltipContent>
      </Tooltip>
    </TooltipProvider>
  );
}
```

**Test Suite** (300+ lines):
```tsx
// apps/web/src/__tests__/lib/tooltip/positioning.test.ts (EXPAND)

describe('calculateOptimalPosition', () => {
  describe('Viewport Edge Cases', () => {
    it('top-left corner - places bottom-right', () => {
      const trigger = new DOMRect(10, 10, 100, 40);
      const tooltip = { width: 200, height: 100 };
      const viewport = { width: 1920, height: 1080 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('bottom');
      expect(pos.top).toBeGreaterThan(trigger.bottom);
      expect(pos.left).toBeGreaterThanOrEqual(8); // Gap
    });

    it('bottom-right corner - places top-left', () => {
      const trigger = new DOMRect(1800, 1000, 100, 40);
      const tooltip = { width: 200, height: 100 };
      const viewport = { width: 1920, height: 1080 };

      const pos = calculateOptimalPosition(trigger, tooltip, viewport);

      expect(pos.placement).toBe('top');
      expect(pos.bottom).toBeDefined();
      expect(pos.left).toBeLessThanOrEqual(viewport.width - tooltip.width - 8);
    });

    // + 8 more edge case tests
  });

  describe('Performance', () => {
    it('calculates position in <16ms', () => {
      const trigger = new DOMRect(500, 500, 100, 40);
      const tooltip = { width: 200, height: 100 };

      const start = performance.now();
      calculateOptimalPosition(trigger, tooltip);
      const elapsed = performance.now() - start;

      expect(elapsed).toBeLessThan(16);
    });
  });

  describe('Collision Detection', () => {
    it('detects navbar collision', () => {
      // Setup DOM with navbar
      // Calculate position
      // Verify offset to avoid collision
    });
  });
});
```

---

### #4182 - Tag Integration in MeepleCard

**Why simple but critical**: TagStrip is 100% ready, just needs wiring into MeepleCard.

**Missing Implementation**:

1. **Props Addition** (5 lines)
   ```tsx
   // meeple-card.tsx line 128-200 (MeepleCardProps interface)

   export interface MeepleCardProps {
     // ... existing props

     /** Feature: Tag Strip (#4181, #4182) */
     tags?: Tag[];
     maxVisibleTags?: number;
     showTagStrip?: boolean;
   }
   ```

2. **Import Statement** (1 line)
   ```tsx
   // meeple-card.tsx line ~65
   import { TagStrip } from '@/components/ui/tags/TagStrip';
   ```

3. **Rendering Logic** (15 lines)
   ```tsx
   // meeple-card.tsx line ~800 (after VerticalTagStack)

   {/* Tag Strip (left edge for grid/featured, top for list) */}
   {showTagStrip && tags && tags.length > 0 && (
     <TagStrip
       tags={tags}
       maxVisible={maxVisibleTags ?? 3}
       variant={
         variant === 'compact' ? 'mobile' :
         variant === 'list' ? 'tablet' :
         'desktop'
       }
       position={variant === 'list' ? 'top' : 'left'}
     />
   )}
   ```

4. **Variant-Specific Layouts** (50 lines in TagStrip.tsx)
   ```tsx
   // TagStrip.tsx - ADD layout variants

   const layoutStyles = {
     vertical: 'flex-col gap-1', // Existing (grid/featured)
     horizontal: 'flex-row gap-2', // NEW (list variant)
     minimal: 'flex-row gap-1' // NEW (compact variant)
   };

   const layout = position === 'top' ? 'horizontal' :
                  variant === 'mobile' ? 'minimal' :
                  'vertical';
   ```

5. **Animation Integration** (Already exists via stagger)
   ```tsx
   // Already in TagStrip.tsx line 30
   <div style={{ animationDelay: `${i * 50}ms` }}>
   ```

**Implementation Steps**:
1. Extend MeepleCardProps interface (5 min)
2. Import TagStrip component (1 min)
3. Add conditional rendering logic (15 min)
4. Update TagStrip for list/compact variants (2h)
5. Write integration tests (4h)

**Estimated Effort**: 6-8 hours (1 day)

---

## 🎯 Priority Matrix

### Must Complete (Blocking)
1. **#4179** - MeepleCard Permission Integration (2-3 days) - USER-FACING
2. **#4186** - Tooltip Positioning (3-4 days) - FOUNDATION
3. **#4180** - Tooltip A11y (2-3 days) - COMPLIANCE
4. **#4182** - Tag Integration (1 day) - POLISH

### Should Complete (Quality)
5. **#4178** - Gate components (4-6h)
6. **#4183** - Collection Limits UI (2 days)
7. **#4184** - Agent StatusBadge (1 day)
8. **#4177** - DB Migration (1-2h)

### Final Gate
9. **#4185** - E2E, A11y audit, Visual regression (2-3 days)

---

## 📐 Lines of Code Estimate

| Task | Existing | Needed | Total |
|------|----------|--------|-------|
| #4177 Migration | 0 | 80 | 80 |
| #4178 Gates | 0 | 300 | 300 |
| #4179 Integration | 0 | 350 | 350 |
| #4186 Positioning | 23 | 550 | 573 |
| #4180 A11y | 0 | 400 | 400 |
| #4182 Tag Wire | 0 | 70 | 70 |
| #4183 Limits UI | 0 | 250 | 250 |
| #4184 AgentStatus | 23 | 180 | 203 |
| #4185 Tests | 0 | 900 | 900 |

**Total New Code**: ~3,100 lines to add
**Total Implementation**: ~4,100 lines (including existing 1K)

---

## 🧪 Testing Strategy

### Unit Tests (Day 1-2)
- Permission hooks tests (200 lines)
- Gate components tests (150 lines)
- Tooltip positioning tests (300 lines)
- Tag integration tests (100 lines)

### Integration Tests (Day 3)
- Permission API integration (150 lines)
- Tag + Permission integration (100 lines)
- Tooltip + MeepleCard integration (100 lines)

### E2E Tests (Day 4-5)
- Permission flows (Free/Normal/Pro/Enterprise) (400 lines)
- Tooltip edge cases (viewport corners) (200 lines)
- Tag overflow scenarios (100 lines)

### A11y Tests (Day 6)
- axe-core audit (automated)
- Keyboard navigation (manual + automated)
- Screen reader testing (manual: NVDA/VoiceOver)
- Contrast validation (automated)

### Visual Regression (Day 7)
- Chromatic snapshots (all variants × all entities)
- Responsive breakpoints (mobile/tablet/desktop)

---

## 💾 Files Checklist

### To Create (23 new files)
- [ ] `apps/api/src/Api/Migrations/..._AddUserTierRole.cs`
- [ ] `apps/web/src/components/ui/gates/permission-gate.tsx`
- [ ] `apps/web/src/components/ui/gates/tier-gate.tsx`
- [ ] `apps/web/src/components/ui/gates/role-gate.tsx`
- [ ] `apps/web/src/components/ui/feedback/tier-badge.tsx`
- [ ] `apps/web/src/components/ui/feedback/upgrade-prompt.tsx`
- [ ] `apps/web/src/components/ui/feedback/agent-status-badge.tsx`
- [ ] `apps/web/src/components/ui/overlays/smart-tooltip.tsx`
- [ ] `apps/web/src/lib/tooltip/positioning.ts`
- [ ] `apps/web/src/lib/tooltip/collision-detection.ts`
- [ ] `apps/web/src/lib/a11y/tooltip-a11y.ts`
- [ ] `apps/web/src/lib/a11y/contrast.ts`
- [ ] `apps/web/src/hooks/useSmartTooltip.ts`
- [ ] `apps/web/src/hooks/useFocusTrap.ts`
- [ ] `apps/web/src/__tests__/hooks/usePermissions.test.tsx`
- [ ] `apps/web/src/__tests__/hooks/useSmartTooltip.test.tsx`
- [ ] `apps/web/src/__tests__/components/meeple-card-permissions.test.tsx`
- [ ] `apps/web/src/__tests__/a11y/tooltip.a11y.test.tsx`
- [ ] `apps/web/src/__tests__/e2e/epic-4068-permission-flows.spec.ts`
- [ ] `tests/Api.Tests/.../GetUserPermissionsIntegrationTests.cs` (if missing)
- [ ] 3 gate component test files

### To Modify (4 existing files)
- [ ] `apps/web/src/components/ui/data-display/meeple-card.tsx` (ADD 200 lines)
- [ ] `apps/web/src/components/ui/data-display/meeple-card-quick-actions.tsx` (ADD 50 lines)
- [ ] `apps/web/src/components/ui/tags/TagStrip.tsx` (ADD 40 lines variants)
- [ ] `apps/web/src/components/ui/overlays/tooltip.tsx` (ENHANCE 100 lines A11y)

---

## ⚡ Quick Wins (< 1 day each)

1. **#4177 Migration** (1-2h) - Just create migration file
2. **#4182 Tag Integration** (6-8h) - Simple wiring, component ready
3. **#4184 AgentStatusBadge** (1 day) - Straightforward component

Complete these first for momentum.

---

## 🏁 Definition of Done - Epic Level

Current Status: **2/9 criteria met (22%)**

- [ ] Tutte le 10 issue complete e merged → **4/10 complete**
- [ ] WCAG 2.1 AA compliance verificata → **NOT TESTED**
- [ ] E2E tests per tutti i flussi utente → **NOT IMPLEMENTED**
- [x] Documentazione aggiornata → **✅ EXCEPTIONAL (600K+ tokens)**
- [ ] Storybook stories per tutte le feature → **PARTIAL (TagStrip only)**
- [ ] Performance benchmarks soddisfatti → **NOT BENCHMARKED**
- [ ] Visual regression tests passed → **NOT IMPLEMENTED**
- [ ] PR merged to parent branch → **NOT MERGED (in WIP branch)**
- [ ] Epic closed → **OPEN**

---

**Next**: See completion plan in next section.
