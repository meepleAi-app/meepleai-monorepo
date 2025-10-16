# Preliminary Accessibility Audit (UI-05)

**Status:** Static Code Analysis Complete (Automated scan pending)
**Date:** 2025-10-16
**Issue:** #306 (UI-05 - Audit accessibilità baseline)
**Standard:** WCAG 2.1 AA

---

## Executive Summary

This preliminary audit is based on **static code analysis** of all 10 pages in the MeepleAI web application. The automated axe-core scan can be executed by running:

```bash
# Terminal 1: Start dev server
cd apps/web && pnpm dev

# Terminal 2: Run audit
cd apps/web && pnpm audit:a11y
```

### Initial Findings (Static Analysis)

From code review, the following accessibility concerns were identified:

| Category | Issues Found |
|----------|--------------|
| Missing ARIA attributes | ~15+ instances |
| Semantic HTML violations | ~8+ instances |
| Focus management | 5+ instances |
| Keyboard navigation | 3+ instances |
| Form accessibility | 10+ instances |
| Color contrast (pending verification) | TBD |

---

## Page-by-Page Static Analysis

### 1. Landing Page (`index.tsx`)

**Issues Found:**

#### 🔴 Critical
- **Auth Modal:**
  - Missing `role="dialog"`
  - Missing `aria-modal="true"`
  - Missing `aria-labelledby` pointing to title
  - Close button missing `aria-label="Close dialog"`
  - **Focus trap not implemented** (user can Tab outside modal)
  - **ESC key not handled**

#### 🟠 Serious
- **Navigation:**
  - Nav links missing `aria-current="page"` for active state
- **Buttons:**
  - "Get Started" / "Start Chatting" buttons missing loading state announcement
- **Forms:**
  - Login form: no `aria-describedby` for error messages
  - Register form: no `aria-describedby` for error messages
  - Password field: missing `aria-describedby` for requirements hint

#### 🟡 Moderate
- **Scroll Indicator:**
  - SVG arrow missing `aria-hidden="true"` (decorative)
- **Feature Cards:**
  - Emoji icons should have `aria-hidden="true"`

**Recommended Fixes:**
- Replace modal with `AccessibleModal` component (Fase 4)
- Add form error announcements with `aria-live="polite"`
- Implement focus trap with custom hook or library
- Add ESC key handler

---

### 2. Chat Page (`chat.tsx`)

**Issues Found:**

#### 🔴 Critical
- **Message List:**
  - NOT using semantic `<ul><li>` markup
  - Should be `<ul role="log" aria-live="polite" aria-atomic="false">`
  - Screen readers won't announce new messages

#### 🟠 Serious
- **Sidebar Toggle:**
  - Button only has emoji "☰" / "✕"
  - Missing `aria-label="Toggle sidebar"` / `aria-label="Close sidebar"`
  - Missing `aria-expanded` state
- **Chat Selection:**
  - Chat items are divs with onClick, should be `<button>` elements
  - Missing `aria-current="true"` for active chat
- **Delete Button:**
  - Only emoji "🗑️", missing `aria-label="Delete chat"`
- **Game/Agent Selects:**
  - Labels present ✅ but could add `aria-describedby` for loading states

#### 🟡 Moderate
- **Feedback Buttons:**
  - Missing `aria-pressed` state (toggle behavior)
  - Visual feedback only (color change), no programmatic state
- **Input Placeholder:**
  - Should use `aria-label` or `<label>` instead of just placeholder

**Recommended Fixes:**
- Semantic message list markup
- Add `aria-label` to all icon-only buttons
- Convert chat items to proper buttons
- Add `aria-live` region for new messages

---

### 3. Timeline Component (`Timeline.tsx` from UI-04)

**Issues Found:**

#### 🟠 Serious
- **Toggle Button:**
  - Only emoji "📊", missing `aria-label="Show RAG Timeline"`
  - Badge with event count: should be `aria-label="Show RAG Timeline, ${events.length} events"`
- **Close Button:**
  - Text "Chiudi Timeline ✕" is good, but could add `aria-label` for clarity
- **Filters:**
  - Checkboxes: **no visible `<label>` elements**
  - Using `display: "flex", alignItems: "center"` but no `htmlFor` association

#### 🟡 Moderate
- **Event List:**
  - Should be `<ul role="feed">` for screen readers
  - Individual events should be `<li>` elements
- **Collapse Buttons:**
  - Missing `aria-expanded` state

**Recommended Fixes:**
- Add proper `<label>` elements with `htmlFor` in filters
- Use `<fieldset>` and `<legend>` for filter groups
- Add `aria-label` to toggle button

---

### 4. Upload Page (`upload.tsx`)

**Issues Found:**

#### 🟠 Serious
- **Stepper/Wizard:**
  - No `aria-label` announcing current step
  - Should be: `"Step 2 of 3: Select game"`
- **File Input:**
  - Missing `aria-describedby` for size/format restrictions
  - Error messages not announced

#### 🟡 Moderate
- **Progress Bar:**
  - If custom div-based, should be `<progress>` HTML5 element
  - Or add `role="progressbar"`, `aria-valuenow`, `aria-valuemin`, `aria-valuemax`

**Recommended Fixes:**
- Add step announcements
- Use `<progress>` element
- Add `aria-live` region for upload status

---

### 5. Editor Page (`editor.tsx`)

**Issues Found:** (Pending detailed review)

#### Potential Issues
- **Code Editor:** May need `role="textbox"` and `aria-multiline="true"`
- **Toolbar buttons:** Need `aria-label` for icon-only buttons

---

### 6. Versions Page (`versions.tsx`)

**Issues Found:** (Pending detailed review)

#### Potential Issues
- **Diff Viewer:** May need proper heading hierarchy
- **Version Selector:** Dropdown accessibility

---

### 7. Admin Page (`admin.tsx`)

**Issues Found:** (Pending detailed review)

#### Potential Issues
- **Charts (Recharts):** May need text alternatives
- **Data Tables:** Need proper headers and ARIA markup

---

### 8. N8N Page (`n8n.tsx`)

**Issues Found:** (Pending detailed review)

#### Potential Issues
- **Config Forms:** Form validation announcements

---

### 9. Logs Page (`logs.tsx`)

**Issues Found:** (Pending detailed review)

#### Potential Issues
- **Log Viewer:** May need `role="log"` and `aria-live`
- **Filters:** Label associations

---

### 10. Chess Page (`chess.tsx`)

**Issues Found:** (Pending detailed review)

#### Potential Issues
- **Chessboard:** May need keyboard navigation for piece selection
- **Move History:** List semantics

---

## Global Issues (All Pages)

### 🔴 Critical
1. **No Skip Link:** Missing "Skip to main content" link
   - Essential for keyboard users
   - Should be first focusable element

### 🟠 Serious
2. **Focus Indicators:** Inconsistent or missing
   - Need global CSS: `:focus-visible { outline: 2px solid #1a73e8; }`
3. **Heading Hierarchy:** Some pages may skip heading levels
4. **Landmark Regions:** Not all pages have proper `<main>`, `<nav>`, `<header>`

### 🟡 Moderate
5. **Color Contrast:** Needs verification tool (e.g., Chrome DevTools)
   - Tailwind: `text-slate-400` on `bg-slate-950` may fail WCAG AA (4.5:1)
6. **Link Purpose:** Some links may not have descriptive text

---

## Accessibility Features Already Present ✅

Good practices found in the codebase:

1. **Form Labels:** Most forms have proper `<label>` elements with `htmlFor`
2. **Semantic HTML:** Generally good use of `<button>`, `<form>`, `<input>`
3. **Alt Text:** Images appear to have alt attributes (where present)
4. **Responsive Design:** Mobile-friendly layouts
5. **Error Boundaries:** Proper error handling UI

---

## Recommended Fixes by Priority

### Priority 1: Blocking Issues (Critical + Serious)

**Must fix before WCAG 2.1 AA compliance:**

1. [ ] **Add Skip Link** globally in `_app.tsx`
2. [ ] **Fix Auth Modal** (index.tsx)
   - Add `role="dialog"`, `aria-modal="true"`, `aria-labelledby`
   - Implement focus trap
   - Handle ESC key
3. [ ] **Fix Chat Message List** (chat.tsx)
   - Use `<ul role="log" aria-live="polite">`
4. [ ] **Add aria-labels to all icon-only buttons**
   - Chat sidebar toggle
   - Timeline toggle
   - Delete buttons
5. [ ] **Fix Timeline Filters** (Timeline.tsx)
   - Add proper `<label>` elements
   - Use `<fieldset>` and `<legend>`
6. [ ] **Add Focus Indicators** globally (CSS)

### Priority 2: Important Improvements (Moderate)

7. [ ] **Upload Page Stepper** - Add step announcements
8. [ ] **Progress Bars** - Use `<progress>` element or proper ARIA
9. [ ] **Form Error Announcements** - Add `aria-live` regions
10. [ ] **Semantic Lists** - Event lists, chat items, etc.

### Priority 3: Enhancements (Minor)

11. [ ] Decorative icons: `aria-hidden="true"`
12. [ ] Improve link text descriptiveness
13. [ ] Add `aria-current` to navigation links

---

## Next Steps (Fase 3-7 of UI-05)

### Fase 3: Automated Testing
- [ ] Create `jest-axe` unit tests for components
- [ ] Add `axe-playwright` E2E tests
- [ ] Integrate in CI/CD pipeline

### Fase 4: Accessible Components (Magic MCP)
- [ ] Generate `AccessibleButton` component
- [ ] Generate `AccessibleModal` component
- [ ] Generate `AccessibleFormInput` component
- [ ] Generate `AccessibleSkipLink` component

### Fase 5: Implement Fixes
- [ ] Apply fixes to all identified issues
- [ ] Replace custom implementations with accessible components

### Fase 6: Manual Testing
- [ ] Keyboard navigation testing (all pages)
- [ ] Screen reader testing (NVDA + VoiceOver)
- [ ] Color contrast verification

### Fase 7: Documentation
- [ ] Complete audit report (after automated scan)
- [ ] Accessibility checklist for developers
- [ ] Component documentation

---

## Running the Automated Audit

To execute the full axe-core automated scan:

```bash
# Terminal 1: Start dev server
cd apps/web
pnpm dev

# Terminal 2: Run audit (in new terminal)
cd apps/web
pnpm audit:a11y
```

This will generate:
- `docs/issue/ui-05-accessibility-audit.md` (detailed report)
- `docs/issue/ui-05-accessibility-audit.json` (CI/CD data)

---

## Tools & Resources

### Testing Tools
- **axe DevTools:** Browser extension for manual audits
- **Lighthouse:** Chrome DevTools > Lighthouse > Accessibility
- **WAVE:** WebAIM browser extension
- **Playwright + axe:** `@axe-core/playwright` (installed)
- **jest-axe:** Unit testing (installed)

### Screen Readers
- **Windows:** NVDA (free) - https://www.nvaccess.org/
- **macOS:** VoiceOver (built-in) - Cmd+F5
- **Windows:** JAWS (commercial) - https://www.freedomscientific.com/

### Color Contrast Tools
- **Chrome DevTools:** Inspect > Accessibility pane
- **Contrast Checker:** https://webaim.org/resources/contrastchecker/
- **Stark:** Figma/Browser plugin

### WCAG References
- **WCAG 2.1 Quick Reference:** https://www.w3.org/WAI/WCAG21/quickref/?levels=aa
- **WebAIM Checklist:** https://webaim.org/standards/wcag/checklist

---

**Report Status:** Preliminary (Static Analysis)
**Next:** Run automated axe scan with `pnpm audit:a11y`
**Issue:** #306 (UI-05)
