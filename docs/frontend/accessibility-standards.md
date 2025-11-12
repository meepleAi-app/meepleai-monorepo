# Accessibility Standards

**Standard**: WCAG 2.1 Level AA (Minimum)
**Stretch Goal**: WCAG 2.1 AAA for core journeys (chat, upload)
**Status**: Production
**Last Updated**: 2025-01-15

---

## Compliance Target

**Minimum**: WCAG 2.1 AA
**Validation**:
- Automated: jest-axe (zero critical violations)
- Manual: Screen reader testing (NVDA, JAWS) quarterly
- E2E: Playwright accessibility checks in CI

---

## Key Requirements

### 1. Keyboard Navigation
- All interactive elements accessible via keyboard
- Visible focus indicators
- Logical tab order
- Skip links for main content

### 2. Screen Reader Support
- Semantic HTML (`<nav>`, `<main>`, `<article>`)
- ARIA labels for dynamic content
- Live regions for notifications
- Alt text for images

### 3. Color & Contrast
- Minimum contrast ratio: 4.5:1 (text), 3:1 (UI components)
- No reliance on color alone for information
- Dark mode support with proper contrast

### 4. Responsive & Zoom
- Mobile responsive (320px minimum width)
- 200% zoom support without horizontal scroll
- Touch targets minimum 44x44px

---

## Implementation

### Automated Testing (jest-axe)

```typescript
import { axe, toHaveNoViolations } from 'jest-axe';

expect.extend(toHaveNoViolations);

test('ChatInterface has no accessibility violations', async () => {
  const { container } = render(<ChatInterface />);
  const results = await axe(container);
  expect(results).toHaveNoViolations();
});
```

### Manual Testing Checklist

**Quarterly Screen Reader Audit**:
- [ ] Navigation works with NVDA (Windows)
- [ ] Forms announce errors properly
- [ ] Chat messages read in order
- [ ] Upload progress announced
- [ ] Modal focus trapped correctly

---

## Acceptance Criteria

- [ ] Zero critical axe violations in CI
- [ ] 100% keyboard navigable
- [ ] Screen reader compatible (NVDA tested)
- [ ] Contrast ratios pass AA
- [ ] Mobile touch targets ≥44px

---

**References**:
- [WCAG 2.1](https://www.w3.org/WAI/WCAG21/quickref/)
- [WebAIM](https://webaim.org/)
- [Accessible Components](https://www.radix-ui.com/)

---

**Maintained by**: Frontend Team
**Review Frequency**: Quarterly
