# Game Table Accessibility Test Plan — WCAG 2.1 AA

**Issue**: #217
**Status**: Draft
**Date**: 2026-03-13

## Overview

Accessibility test scenarios for Game Table components targeting WCAG 2.1 AA compliance. Organized by WCAG principle and component.

## Test Matrix

### 1. Perceivable

| WCAG | Criterion | Component | Test | Method |
|------|-----------|-----------|------|--------|
| 1.1.1 | Non-text content | MeepleCard | Game images have alt text with game title | axe-core |
| 1.3.1 | Info & relationships | CardRack | Navigation uses `<nav>` with `aria-label` | DOM inspection |
| 1.3.1 | Info & relationships | QuickView | Tab list uses `role="tablist"`, tabs use `role="tab"` | DOM inspection |
| 1.3.2 | Meaningful sequence | LayoutShell | DOM order matches visual order (TopBar → CardRack → Content → QuickView) | Manual + axe |
| 1.4.3 | Contrast (minimum) | Status badges | Green/gray badges meet 4.5:1 on card backgrounds | Color contrast analyzer |
| 1.4.3 | Contrast (minimum) | MobileTabBar | Active/inactive tab text meets 4.5:1 | Color contrast analyzer |
| 1.4.11 | Non-text contrast | CardRack | Active card border meets 3:1 against adjacent colors | Manual |

### 2. Operable

| WCAG | Criterion | Component | Test | Method |
|------|-----------|-----------|------|--------|
| 2.1.1 | Keyboard | CardRack | Tab focuses each nav item, Enter/Space activates | Playwright |
| 2.1.1 | Keyboard | QuickView tabs | Arrow keys move between tabs, Enter activates | Playwright |
| 2.1.1 | Keyboard | QuickView close | Escape key closes panel | Playwright |
| 2.1.2 | No keyboard trap | QuickView | Tab out of panel moves focus to next element | Playwright |
| 2.1.2 | No keyboard trap | MobileBottomSheet | Escape closes, focus returns to trigger | Playwright |
| 2.4.1 | Bypass blocks | LayoutShell | Skip-to-content link bypasses CardRack+TopBar | Playwright |
| 2.4.3 | Focus order | LayoutShell | Tab order: skip-link → TopBar → CardRack → Content → QuickView | Playwright |
| 2.4.7 | Focus visible | All interactive | Every focusable element has visible focus ring | axe-core + visual |
| 2.4.11 | Focus not obscured | FloatingActionBar | Focused content not hidden behind FAB | Manual |

### 3. Understandable

| WCAG | Criterion | Component | Test | Method |
|------|-----------|-----------|------|--------|
| 3.1.1 | Language | html tag | `lang="it"` attribute present | axe-core |
| 3.2.1 | On focus | CardRack | No navigation triggered on focus alone | Playwright |
| 3.3.1 | Error identification | Game Night form | Validation errors identify the field | Unit test |
| 3.3.2 | Labels | All form inputs | Every input has associated label or aria-label | axe-core |

### 4. Robust

| WCAG | Criterion | Component | Test | Method |
|------|-----------|-----------|------|--------|
| 4.1.2 | Name, role, value | QuickView tabs | Tabs have accessible names matching visible labels | axe-core |
| 4.1.2 | Name, role, value | CardRack items | Nav items have accessible names | axe-core |
| 4.1.3 | Status messages | Activity feed | New events announced via `aria-live="polite"` | DOM inspection |
| 4.1.3 | Status messages | SSE reconnect | Connection status changes announced | DOM inspection |

## Component-Specific Tests

### CardRack

```typescript
// Playwright test outline
test('CardRack keyboard navigation', async ({ page }) => {
  // Tab to first CardRack item
  // Verify focus visible
  // Press Enter → navigates to route
  // Arrow Down → moves focus to next item
  // Arrow Up → moves focus to previous item
});
```

### QuickView

```typescript
test('QuickView focus management', async ({ page }) => {
  // Open QuickView → focus moves to panel
  // Tab through tabs → focus stays within panel
  // Press Escape → panel closes, focus returns to trigger
});
```

### Activity Feed (`aria-live`)

```typescript
test('Activity feed announces new events', async ({ page }) => {
  // Verify feed container has aria-live="polite"
  // Verify new event content is within the live region
  // Verify no aria-live="assertive" (would interrupt user)
});
```

### Mobile Bottom Sheet

```typescript
test('Bottom sheet focus trap', async ({ page }) => {
  // Open bottom sheet → focus moves inside
  // Tab cycles within sheet (focus trap)
  // Escape → closes, focus returns to trigger
  // Body scroll prevented while open
});
```

## Automated Testing

### axe-core Integration

Run axe-core as part of Playwright E2E suite:

```typescript
import AxeBuilder from '@axe-core/playwright';

test('LayoutShell passes axe audit', async ({ page }) => {
  await page.goto('/dashboard');
  const results = await new AxeBuilder({ page })
    .withTags(['wcag2a', 'wcag2aa'])
    .analyze();
  expect(results.violations).toEqual([]);
});
```

### Component-Level (Vitest + axe)

```typescript
import { axe } from 'vitest-axe';

it('QuickView has no a11y violations', async () => {
  const { container } = render(<QuickView />);
  const results = await axe(container);
  expect(results).toHaveNoViolations();
});
```

## Known Issues to Address

| Component | Issue | Fix |
|-----------|-------|-----|
| Radix Progress | Missing `aria-label` | Add explicit `aria-label` prop |
| Activity feed | No `aria-live` region | Add `aria-live="polite"` to feed container |
| QuickView | No focus management on open/close | Implement focus trap + return |
| MobileBottomSheet | Body scroll not prevented | Add `overflow: hidden` to body when open |

## Priority

| Priority | Tests | Effort |
|----------|-------|--------|
| P0 | axe-core audit on all pages (automated) | 1 day |
| P1 | Keyboard navigation (CardRack, QuickView, forms) | 2 days |
| P2 | Focus management (bottom sheets, panel open/close) | 1 day |
| P3 | Color contrast audit (manual verification) | 0.5 days |
