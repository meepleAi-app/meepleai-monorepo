# Issue #4063: Tooltip Positioning System

**Epic**: #4068 - MeepleCard Enhancements
**Area**: Smart Tooltip Positioning (1/2)
**Estimate**: 3-4 giorni
**Priority**: P2-Medium

---

## 📋 Acceptance Criteria

### AC1: Viewport Boundary Detection
- [ ] Detect when tooltip would overflow viewport edges
- [ ] Auto-flip positioning: top → bottom, left → right
- [ ] Calculate available space in all 4 directions
- [ ] Choose optimal placement with max visibility

### AC2: Collision Detection
- [ ] Detect overlap with other UI elements (navbar, sidebar)
- [ ] Avoid collision with other active tooltips
- [ ] Smart offset quando collision inevitabile

### AC3: Responsive Positioning
- [ ] Desktop: 4-direction smart positioning (top/bottom/left/right)
- [ ] Mobile: prefer top/bottom (left/right limitate per spazio)
- [ ] Tablet: adaptive based on orientation

### AC4: Performance
- [ ] Position calculation < 16ms (60fps)
- [ ] Debounce scroll/resize events
- [ ] Use `IntersectionObserver` per visibility check

---

## 🎨 Visual Mockup

```
┌─────────────────────────────────────┐
│                              Viewport│
│  ┌──────────┐                       │
│  │ Card     │                       │
│  │  [Hover] │  ← Trigger            │
│  └──────────┘                       │
│       ↓                              │
│  ╔═══════════════╗                  │
│  ║ Tooltip       ║  ← Auto-position │
│  ║ Content here  ║                  │
│  ╚═══════════════╝                  │
│                                     │
│                                     │
│  ┌──────────┐                       │
│  │ Card     │                       │
│  │  [Near   │  ← Near edge          │
│  │   Edge]  │                       │
│  └──────────┘                       │
│       ↑                              │
│  ╔═══════════════╗                  │
│  ║ Tooltip       ║  ← Flipped up    │
│  ║ (auto-flip)   ║                  │
│  ╚═══════════════╝                  │
└─────────────────────────────────────┘
```

---

## 🔧 Implementation

### Positioning Algorithm
```typescript
interface TooltipPosition {
  top?: number;
  bottom?: number;
  left?: number;
  right?: number;
  placement: 'top' | 'bottom' | 'left' | 'right';
}

function calculateOptimalPosition(
  triggerRect: DOMRect,
  tooltipSize: { width: number; height: number },
  viewport: { width: number; height: number }
): TooltipPosition {
  const gap = 8; // spacing between trigger and tooltip

  // Calculate available space in each direction
  const spaceAbove = triggerRect.top;
  const spaceBelow = viewport.height - triggerRect.bottom;
  const spaceLeft = triggerRect.left;
  const spaceRight = viewport.width - triggerRect.right;

  // Determine best vertical placement
  const preferTop = spaceAbove > tooltipSize.height + gap;
  const preferBottom = spaceBelow > tooltipSize.height + gap;

  let placement: TooltipPosition['placement'];
  let position: TooltipPosition = { placement: 'bottom' };

  if (preferTop && spaceAbove >= spaceBelow) {
    placement = 'top';
    position = {
      bottom: viewport.height - triggerRect.top + gap,
      left: triggerRect.left + triggerRect.width / 2 - tooltipSize.width / 2,
      placement: 'top'
    };
  } else if (preferBottom) {
    placement = 'bottom';
    position = {
      top: triggerRect.bottom + gap,
      left: triggerRect.left + triggerRect.width / 2 - tooltipSize.width / 2,
      placement: 'bottom'
    };
  } else if (spaceRight > tooltipSize.width + gap) {
    placement = 'right';
    position = {
      top: triggerRect.top + triggerRect.height / 2 - tooltipSize.height / 2,
      left: triggerRect.right + gap,
      placement: 'right'
    };
  } else {
    placement = 'left';
    position = {
      top: triggerRect.top + triggerRect.height / 2 - tooltipSize.height / 2,
      right: viewport.width - triggerRect.left + gap,
      placement: 'left'
    };
  }

  // Ensure tooltip stays within viewport bounds
  if (position.left !== undefined) {
    position.left = Math.max(gap, Math.min(position.left, viewport.width - tooltipSize.width - gap));
  }
  if (position.top !== undefined) {
    position.top = Math.max(gap, Math.min(position.top, viewport.height - tooltipSize.height - gap));
  }

  return position;
}
```

### useSmartTooltip Hook
```typescript
export function useSmartTooltip() {
  const [position, setPosition] = useState<TooltipPosition | null>(null);
  const triggerRef = useRef<HTMLElement>(null);
  const tooltipRef = useRef<HTMLDivElement>(null);

  useEffect(() => {
    if (!triggerRef.current || !tooltipRef.current) return;

    const updatePosition = () => {
      const triggerRect = triggerRef.current!.getBoundingClientRect();
      const tooltipRect = tooltipRef.current!.getBoundingClientRect();
      const viewport = {
        width: window.innerWidth,
        height: window.innerHeight
      };

      const optimal = calculateOptimalPosition(
        triggerRect,
        { width: tooltipRect.width, height: tooltipRect.height },
        viewport
      );

      setPosition(optimal);
    };

    updatePosition();

    // Update on scroll/resize (debounced)
    const debouncedUpdate = debounce(updatePosition, 100);
    window.addEventListener('scroll', debouncedUpdate, { passive: true });
    window.addEventListener('resize', debouncedUpdate, { passive: true });

    return () => {
      window.removeEventListener('scroll', debouncedUpdate);
      window.removeEventListener('resize', debouncedUpdate);
    };
  }, []);

  return { position, triggerRef, tooltipRef };
}
```

---

## ✅ Testing Checklist

### Unit Tests
- [ ] Calculates correct position when enough space above
- [ ] Flips to bottom when insufficient space above
- [ ] Handles left/right overflow correctly
- [ ] Centers tooltip relative to trigger

### Integration Tests
- [ ] Tooltip repositions on scroll
- [ ] Tooltip repositions on window resize
- [ ] Multiple tooltips don't overlap
- [ ] Tooltip stays within viewport on mobile

### Performance Tests
- [ ] Position calculation < 16ms
- [ ] No jank during scroll
- [ ] Debounced resize handling

### Edge Cases
- [ ] Trigger near top-left corner
- [ ] Trigger near bottom-right corner
- [ ] Very large tooltip content
- [ ] Trigger larger than viewport (mobile)

---

## 🔗 Dependencies

**Blocked by**: None (independent)
**Blocks**: #4064 (needs positioning foundation)

---

## 📚 Resources

- **Floating UI**: https://floating-ui.com/ (reference implementation)
- **Radix Tooltip**: https://www.radix-ui.com/primitives/docs/components/tooltip
- **Popper.js**: https://popper.js.org/ (algorithm reference)
