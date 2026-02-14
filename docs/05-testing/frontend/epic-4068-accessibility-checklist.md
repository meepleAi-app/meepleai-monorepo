# Epic #4068: WCAG 2.1 AA Accessibility Checklist

**Target**: 100% WCAG 2.1 AA compliance for all MeepleCard enhancements

---

## Perceivable (Principle 1)

### 1.1 Text Alternatives

- [ ] **1.1.1 Non-text Content (A)**: All icons have text alternatives
  - Tag icons have aria-label on parent
  - Agent status icons have descriptive labels
  - Tooltip triggers have aria-describedby

### 1.3 Adaptable

- [ ] **1.3.1 Info and Relationships (A)**: Structure conveyed programmatically
  - Tag strip uses role="list" and role="listitem"
  - Tooltip uses role="tooltip" with aria-describedby
  - Agent metadata uses semantic HTML (dl/dt/dd for key-value pairs)

- [ ] **1.3.2 Meaningful Sequence (A)**: Reading order matches visual order
  - Tags render top-to-bottom in DOM (matches visual stack)
  - Tab order: card → wishlist → tags → quick actions

- [ ] **1.3.3 Sensory Characteristics (A)**: Instructions don't rely solely on color
  - "Red warning" also has icon (AlertTriangle)
  - Status not identified by color alone (includes text label)

### 1.4 Distinguishable

- [ ] **1.4.1 Use of Color (A)**: Color not sole means of conveying info
  - Collection limits: progress bar has text percentage
  - Agent status: colored dot + text label
  - Tags: icon + label, not color alone

- [ ] **1.4.3 Contrast (Minimum) (AA)**: Text contrast ≥4.5:1 (normal), ≥3:1 (large 18pt+)
  - Tag badges: White text on colored background (tested with contrast checker)
  - Tooltip: Default text on popover background
  - Agent status labels: Sufficient contrast verified

- [ ] **1.4.4 Resize Text (AA)**: Text resizable to 200% without loss of content/functionality
  - Tested with browser zoom 200%
  - Tags stack properly, no overflow hidden
  - Tooltips reposition correctly

- [ ] **1.4.5 Images of Text (AA)**: No images of text (N/A - using SVG icons + live text)

- [ ] **1.4.10 Reflow (AA)**: Content reflows to 320px width without horizontal scroll
  - Mobile: 24px tag strip + card content fits in 320px
  - No horizontal scroll on small screens

- [ ] **1.4.11 Non-text Contrast (AA)**: UI components have ≥3:1 contrast
  - Tag strip border: white/10 on black/20 background
  - Focus ring: 2px solid ring color (high contrast)

- [ ] **1.4.12 Text Spacing (AA)**: No loss of content with spacing overrides
  - Tested with: line-height 1.5, letter-spacing 0.12em, word-spacing 0.16em

- [ ] **1.4.13 Content on Hover/Focus (AA)**: Hover/focus content dismissable, hoverable, persistent
  - Tooltip dismissable: Escape key, tap outside, mouse leave
  - Tooltip hoverable: Can move mouse to tooltip (doesn't disappear)
  - Tooltip persistent: Stays visible until dismissed

---

## Operable (Principle 2)

### 2.1 Keyboard Accessible

- [ ] **2.1.1 Keyboard (A)**: All functionality available via keyboard
  - Tab: Navigate between cards, tags, tooltips
  - Enter/Space: Activate tooltip, select card
  - Escape: Close tooltip, deselect
  - Arrow keys: Navigate within tag overflow tooltip

- [ ] **2.1.2 No Keyboard Trap (A)**: Focus can move away from all components
  - Tooltip: Escape or Tab moves focus out
  - Tag overflow: Can tab away from tooltip
  - No focus traps in any modal/overlay

- [ ] **2.1.4 Character Key Shortcuts (A)**: Single-key shortcuts avoid conflicts (N/A - no single-key shortcuts)

### 2.2 Enough Time

- [ ] **2.2.1 Timing Adjustable (A)**: No time limits on interactions (N/A - no timeouts)

### 2.4 Navigable

- [ ] **2.4.3 Focus Order (A)**: Tab order follows visual order
  - Card → Wishlist → Tags → Quick Actions → Next Card

- [ ] **2.4.6 Headings and Labels (AA)**: Descriptive labels
  - Tooltip triggers: aria-label describes purpose
  - Tag strip: aria-label="Entity tags"
  - Agent status: aria-label="Agent status: Active"

- [ ] **2.4.7 Focus Visible (AA)**: Keyboard focus indicator visible
  - All interactive elements: focus-visible:ring-2 ring-ring
  - Minimum 2px outline, sufficient contrast

---

## Understandable (Principle 3)

### 3.1 Readable

- [ ] **3.1.1 Language of Page (A)**: HTML lang attribute set (app-level, not component)

### 3.2 Predictable

- [ ] **3.2.1 On Focus (A)**: Focus doesn't trigger context change
  - Tooltip shows on Enter/Space, not on focus alone
  - Tab to element doesn't auto-submit forms

- [ ] **3.2.2 On Input (A)**: Input doesn't trigger unexpected context change
  - Checkbox selection doesn't navigate away
  - Form inputs update state predictably

- [ ] **3.2.3 Consistent Navigation (AA)**: Navigation consistent across pages
  - MeepleCard structure same on all pages
  - Tag position consistent (left edge)

- [ ] **3.2.4 Consistent Identification (AA)**: Components identified consistently
  - Wishlist always heart icon
  - Tags always left edge strip
  - Agent status always dot + label

### 3.3 Input Assistance

- [ ] **3.3.1 Error Identification (A)**: Errors identified and described
  - Permission denied: Clear message "Upgrade to Pro for bulk selection"
  - Validation errors: Specific field errors

- [ ] **3.3.2 Labels or Instructions (A)**: Form elements have labels
  - All buttons have accessible names
  - Icons have aria-label

---

## Robust (Principle 4)

### 4.1 Compatible

- [ ] **4.1.1 Parsing (A)**: Valid HTML/ARIA
  - No duplicate IDs
  - ARIA attributes used correctly
  - Roles used according to spec

- [ ] **4.1.2 Name, Role, Value (A)**: UI components have accessible name/role/value
  - Tooltip: role="tooltip", aria-describedby linkage
  - Tags: role="status", descriptive aria-label
  - Checkbox: role="checkbox", aria-checked state

- [ ] **4.1.3 Status Messages (AA)**: Status messages presented to assistive tech
  - Permission check result: aria-live="polite"
  - Upload progress: aria-live="assertive"
  - Collection limit warning: role="status"

---

## Testing Tools & Procedures

### Automated Tools

**axe-core**:
```bash
cd apps/web
pnpm add -D @axe-core/playwright
pnpm exec playwright test --grep @a11y
```

**Lighthouse CI**:
```bash
pnpm add -D @lhci/cli
pnpm exec lhci autorun --collect.url=http://localhost:3000/games
```

**Pa11y**:
```bash
pnpm add -D pa11y-ci
pnpm exec pa11y-ci http://localhost:3000/games
```

### Manual Testing

**Screen Readers**:
- NVDA (Windows): Test on Windows 11 with Firefox
- JAWS (Windows): Test on Windows 11 with Chrome
- VoiceOver (macOS): Test on macOS with Safari
- VoiceOver (iOS): Test on iPhone with Safari
- TalkBack (Android): Test on Android with Chrome

**Keyboard Navigation**:
- Tab through all interactive elements
- Enter/Space activates buttons
- Escape closes overlays
- Arrow keys navigate lists

**High Contrast Mode**:
- Windows High Contrast: Test all 4 themes (high contrast black/white, #1/#2)
- Verify borders visible, colors not lost

### Acceptance Criteria

✅ **Pass**: 0 axe-core violations (errors + warnings)
✅ **Pass**: Lighthouse accessibility score ≥95
✅ **Pass**: Manual screen reader test script completes successfully
✅ **Pass**: All WCAG 2.1 AA checkboxes above marked ✓
✅ **Pass**: High contrast mode renders all components visibly

---

## Remediation Workflow

1. **Run Automated Audit**: `pnpm test:a11y`
2. **Review Violations**: Check axe-core report
3. **Prioritize**: Critical (A) > Important (AA) > Nice-to-have (AAA)
4. **Fix**: Update components to pass checks
5. **Re-test**: Run audit again
6. **Manual Verify**: Test with screen reader
7. **Document**: Update checklist with ✓

---

## Common Accessibility Patterns

### Tooltip Pattern

```tsx
<div
  ref={triggerRef}
  role="button"
  tabIndex={0}
  aria-describedby={tooltipId}
  aria-expanded={isOpen}
  onKeyDown={(e) => {
    if (e.key === 'Enter' || e.key === ' ') setIsOpen(true);
    if (e.key === 'Escape') setIsOpen(false);
  }}
>
  {trigger}
</div>

{isOpen && (
  <div
    id={tooltipId}
    role="tooltip"
    aria-live="polite"
  >
    {content}
  </div>
)}
```

### Tag Strip Pattern

```tsx
<div role="list" aria-label="Entity tags">
  {tags.map(tag => (
    <div key={tag.id} role="listitem">
      <div role="status" aria-label={tag.label}>
        {tag.icon && <Icon aria-hidden="true" />}
        <span>{tag.label}</span>
      </div>
    </div>
  ))}
</div>
```

### Progress Bar Pattern

```tsx
<div role="progressbar" aria-valuenow={percent} aria-valuemin={0} aria-valuemax={100} aria-label="Collection game count">
  <div className="progress-fill" style={{ width: `${percent}%` }} />
  <span className="sr-only">{count} of {max} games ({percent}%)</span>
</div>
```

---

## References

- WCAG 2.1: https://www.w3.org/WAI/WCAG21/quickref/
- ARIA Practices: https://www.w3.org/WAI/ARIA/apg/
- axe-core Rules: https://github.com/dequelabs/axe-core/blob/develop/doc/rule-descriptions.md
- Issue #4180: Tooltip Accessibility WCAG 2.1 AA
