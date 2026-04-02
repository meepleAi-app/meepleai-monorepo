# Issue #4064: Tooltip Accessibility WCAG 2.1 AA

**Epic**: #4068 - MeepleCard Enhancements
**Area**: Smart Tooltip Positioning (2/2)
**Estimate**: 2-3 giorni
**Priority**: P1-High (Accessibility Critical)
**Depends on**: #4063

---

## 📋 Acceptance Criteria

### AC1: Keyboard Navigation
- [ ] Tab to focus trigger element
- [ ] Enter/Space to show tooltip
- [ ] Escape to hide tooltip
- [ ] Tooltip stays visible while focused
- [ ] Focus trap dentro tooltip se contiene interactive elements

### AC2: Screen Reader Support
- [ ] `aria-describedby` links trigger to tooltip
- [ ] Tooltip content announced when shown
- [ ] Role="tooltip" sul tooltip element
- [ ] Live region updates per dynamic content

### AC3: Mobile Touch Support
- [ ] Tap trigger to show tooltip (no hover on mobile)
- [ ] Tap outside to dismiss
- [ ] Long press alternative (optional)
- [ ] Touch-friendly dismiss button (X icon)

### AC4: Contrast & Visibility (WCAG AA)
- [ ] Text contrast ratio ≥ 4.5:1 (normal text)
- [ ] Text contrast ratio ≥ 3:1 (large text 18pt+)
- [ ] Tooltip shadow/border for visibility
- [ ] High contrast mode compatible

### AC5: Focus Management
- [ ] Focus visible indicator (ring outline)
- [ ] Focus returns to trigger on close
- [ ] Focus doesn't get trapped
- [ ] Tab order preserved

---

## 🧪 Accessibility Test Scenarios

### Test 1: Keyboard-Only Navigation
```
GIVEN user navigates with keyboard only
WHEN user tabs to card with tooltip trigger
THEN focus ring visible on trigger

WHEN user presses Enter/Space
THEN tooltip appears
AND tooltip content announced by screen reader

WHEN user presses Escape
THEN tooltip closes
AND focus returns to trigger
```

### Test 2: Screen Reader Compatibility
```
GIVEN user with screen reader (NVDA/JAWS/VoiceOver)
WHEN user focuses tooltip trigger
THEN screen reader announces: "Button, has tooltip, {trigger label}"

WHEN user activates trigger
THEN screen reader announces tooltip content
AND aria-live="polite" region updates
```

### Test 3: Mobile Touch Interaction
```
GIVEN user on mobile device
WHEN user taps tooltip trigger
THEN tooltip shows immediately (no hover delay)

WHEN user taps outside tooltip
THEN tooltip dismisses

WHEN tooltip contains links
THEN links are tappable (min 44x44px touch target)
```

### Test 4: High Contrast Mode
```
GIVEN user enables Windows High Contrast mode
WHEN tooltip is displayed
THEN tooltip border visible
AND text readable
AND colors not lost
```

---

## 🔧 Implementation

### ARIA Attributes
```typescript
<div
  ref={triggerRef}
  role="button"
  tabIndex={0}
  aria-describedby={tooltipId}
  aria-expanded={isOpen}
  onKeyDown={(e) => {
    if (e.key === 'Enter' || e.key === ' ') {
      e.preventDefault();
      setIsOpen(true);
    }
    if (e.key === 'Escape') {
      setIsOpen(false);
    }
  }}
  onClick={handleToggle}
>
  {trigger}
</div>

{isOpen && (
  <div
    ref={tooltipRef}
    id={tooltipId}
    role="tooltip"
    aria-live="polite"
    style={position}
    className="focus-visible:outline-none focus-visible:ring-2 focus-visible:ring-ring"
  >
    {content}
    {/* Mobile dismiss button */}
    <button
      aria-label="Close tooltip"
      onClick={() => setIsOpen(false)}
      className="md:hidden absolute top-1 right-1 p-1"
    >
      <X className="w-4 h-4" />
    </button>
  </div>
)}
```

### Focus Trap (for interactive tooltips)
```typescript
import { useFocusTrap } from '@/hooks/useFocusTrap';

function InteractiveTooltip({ children, content }: TooltipProps) {
  const [isOpen, setIsOpen] = useState(false);
  const tooltipRef = useRef<HTMLDivElement>(null);
  const triggerRef = useRef<HTMLButtonElement>(null);

  // Trap focus if tooltip contains interactive elements
  useFocusTrap(tooltipRef, isOpen, {
    onEscape: () => {
      setIsOpen(false);
      triggerRef.current?.focus(); // Return focus
    }
  });

  return (
    <>
      <button
        ref={triggerRef}
        aria-describedby="tooltip-1"
        aria-expanded={isOpen}
        onClick={() => setIsOpen(!isOpen)}
      >
        {children}
      </button>

      {isOpen && (
        <div ref={tooltipRef} id="tooltip-1" role="tooltip">
          {content}
        </div>
      )}
    </>
  );
}
```

### Mobile Touch Detection
```typescript
const isTouchDevice = () => {
  return 'ontouchstart' in window || navigator.maxTouchPoints > 0;
};

function AdaptiveTooltip(props: TooltipProps) {
  const isTouch = isTouchDevice();

  if (isTouch) {
    // Mobile: tap to show, tap outside to dismiss
    return <TapTooltip {...props} />;
  } else {
    // Desktop: hover + keyboard
    return <HoverTooltip {...props} />;
  }
}
```

---

## ✅ Testing Checklist

### Automated Accessibility Tests
- [ ] axe-core audit passes (0 violations)
- [ ] WAVE tool: no errors
- [ ] Lighthouse accessibility score ≥ 95
- [ ] Pa11y CI tests pass

### Manual Testing
- [ ] Keyboard navigation complete flow
- [ ] Screen reader (NVDA) announces correctly
- [ ] VoiceOver (iOS) compatibility
- [ ] TalkBack (Android) compatibility
- [ ] High contrast mode visibility
- [ ] Focus visible in all states

### WCAG 2.1 AA Checklist
- [ ] 1.3.1 Info and Relationships (Level A)
- [ ] 1.4.3 Contrast (Minimum) (Level AA) - 4.5:1
- [ ] 2.1.1 Keyboard (Level A)
- [ ] 2.1.2 No Keyboard Trap (Level A)
- [ ] 2.4.7 Focus Visible (Level AA)
- [ ] 4.1.2 Name, Role, Value (Level A)
- [ ] 4.1.3 Status Messages (Level AA)

---

## 🔗 Dependencies

**Blocked by**: #4063 (Positioning System)
**Blocks**: None

---

## 📚 Resources

- **WCAG 2.1 Guidelines**: https://www.w3.org/WAI/WCAG21/quickref/
- **ARIA Authoring Practices**: https://www.w3.org/WAI/ARIA/apg/patterns/tooltip/
- **axe DevTools**: https://www.deque.com/axe/devtools/
- **Radix Accessibility**: https://www.radix-ui.com/primitives/docs/overview/accessibility
