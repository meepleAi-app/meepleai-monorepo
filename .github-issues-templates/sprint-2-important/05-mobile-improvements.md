# [ENHANCEMENT] Mobile-First Responsive Improvements

## 🎯 Objective
Fix mobile UX issues and implement mobile-specific patterns (drawer, touch targets, viewport units).

## 📋 Current Issues
- Chat sidebar: No mobile drawer (fixed width blocks content)
- `height: 100vh` breaks on iOS (browser UI overlaps)
- Touch targets < 44px (WCAG violation)
- Upload page: Forms too wide on small screens
- No mobile navigation patterns

## ✅ Acceptance Criteria
- [ ] Chat sidebar → Sheet/Drawer on mobile (`< 768px`)
- [ ] Replace `h-screen` with `h-dvh` (dynamic viewport height)
- [ ] All buttons min 44x44px (`min-w-11 min-h-11`)
- [ ] Bottom navigation bar on mobile for chat actions
- [ ] Upload page forms responsive (`w-full md:w-auto`)
- [ ] Test on real devices (iOS Safari, Android Chrome)

## 🏗️ Implementation
1. Install Shadcn Sheet component
2. Create `<MobileSidebar>` with Sheet
3. Update ChatContent to toggle Sheet on mobile
4. Replace all `h-screen` → `h-dvh`
5. Audit touch targets, add `touch-target` utility class
6. Add bottom nav for mobile chat

## 📦 Files
- `components/chat/ChatSidebar.tsx`
- `components/chat/MobileSidebar.tsx` (new)
- `components/chat/ChatContent.tsx`
- `components/chat/BottomNav.tsx` (new)
- `styles/design-tokens.css` (add mobile-specific tokens)

## ⏱️ Effort: **1 day** | **Sprint 2** | **Priority**: 🟡 High
