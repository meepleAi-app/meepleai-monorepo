# [A11Y] Accessibility Audit and Fixes

## 🎯 Objective
Achieve WCAG 2.1 AA compliance across all pages.

## 📋 Current Issues
- Missing ARIA live regions for dynamic content
- Loading states not announced to screen readers
- Focus management issues in modals
- Color contrast issues (some text < 4.5:1)
- Keyboard navigation gaps

## ✅ Acceptance Criteria
- [ ] All pages pass axe-core automated tests
- [ ] Focus trapped in modals
- [ ] ARIA live regions for:
   - Chat message received
   - Upload progress
   - Error messages
- [ ] Skip links on all pages
- [ ] Keyboard navigation works for all interactive elements
- [ ] Manual screen reader testing (NVDA/JAWS)

## 🏗️ Implementation
1. Install `@axe-core/react` in development:
   ```tsx
   if (process.env.NODE_ENV === 'development') {
     const axe = require('@axe-core/react');
     axe(React, ReactDOM, 1000);
   }
   ```
2. Add ARIA live regions:
   ```tsx
   <div role="status" aria-live="polite" aria-atomic="true">
     {uploadProgress}%
   </div>
   ```
3. Focus management in modals:
   ```tsx
   const focusTrap = useFocusTrap(modalRef);
   ```
4. Add skip links:
   ```tsx
   <a href="#main" className="sr-only focus:not-sr-only">
     Skip to main content
   </a>
   ```
5. Fix color contrast issues

## 🧪 Testing
- Automated: axe DevTools, Lighthouse
- Manual: Keyboard navigation, NVDA screen reader
- Tools: WAVE, Color Contrast Analyzer

## ⏱️ Effort: **0.5 day** | **Sprint 2** | **Priority**: 🟡 High
