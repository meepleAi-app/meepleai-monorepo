# [PERFORMANCE] Optimize Re-renders and Bundle Size

## 🎯 Objective
Reduce unnecessary re-renders, virtualize large lists, and optimize bundle size.

## 📋 Current Issues
- Upload page: Re-renders entire component on state change (20+ useState)
- PDF table: Renders all rows (no virtualization)
- ChatProvider: Excessive re-renders due to large context
- No memoization on expensive computations
- Large bundle size (~450 KB)

## ✅ Acceptance Criteria
- [ ] Memoize expensive components (React.memo)
- [ ] Virtualize PDF table (`react-window`, already installed)
- [ ] Split context providers (see issue #03)
- [ ] Code split routes (`next/dynamic`)
- [ ] Measure improvements with React Profiler
- [ ] Bundle size < 350 KB (measured with `next build`)
- [ ] Initial load < 2s (Lighthouse score)

## 🏗️ Implementation
1. Wrap expensive components in `React.memo`:
   - `<Message>`
   - `<PdfTableRow>`
   - `<GameCard>`
2. Virtualize PDF table:
   ```tsx
   import { FixedSizeList } from 'react-window';
   <FixedSizeList height={600} itemCount={pdfs.length} itemSize={60}>
     {PdfRow}
   </FixedSizeList>
   ```
3. Code split pages:
   ```tsx
   const AdminPage = dynamic(() => import('./admin'), { ssr: false });
   ```
4. Use `useMemo` for derived values
5. Profile with React DevTools Profiler

## 📊 Expected Improvements
- Re-renders: -80% (upload page)
- Bundle size: -100 KB (~20%)
- FCP: -200ms
- TTI: -800ms

## ⏱️ Effort: **1 day** | **Sprint 2** | **Priority**: 🟡 High
