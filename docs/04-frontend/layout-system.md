# MeepleAI Layout System Architecture

**Status**: Phase 5 Complete (Issue #2234)
**Last Updated**: 2025-12-19
**Version**: 1.0

---

## Overview

MeepleAI uses a hierarchical layout system built with Next.js 16 App Router, providing consistent UX across all application areas while maintaining separation of concerns.

**Architecture**: Route Groups + Layout Components + Shared Primitives

---

## Layout Components

### 1. PublicLayout (Issue #2230)

**Purpose**: Wrapper for public-facing marketing and informational pages

**Location**: `src/components/layouts/PublicLayout.tsx`

**Features**:
- PublicHeader with responsive navigation
- Container width control (sm|md|lg|xl|full)
- PublicFooter with 3-column grid
- Dark mode support
- Min-height flex layout for sticky footer

**Route Group**: `(public)`

**Applied To**:
- `/` - Homepage/Landing
- `/games` - Games catalog
- `/settings` - User settings
- `/giochi/[id]` - Game detail pages

**Usage**:
```tsx
// apps/web/src/app/(public)/layout.tsx
import { PublicLayout } from '@/components/layouts/PublicLayout';

export default function PublicRootLayout({ children }) {
  return (
    <PublicLayout user={user} onLogout={handleLogout}>
      {children}
    </PublicLayout>
  );
}
```

**Props**:
```typescript
interface PublicLayoutProps {
  children: ReactNode;
  user?: PublicUser;
  onLogout?: () => void;
  showNewsletter?: boolean;
  className?: string;
  containerWidth?: 'sm' | 'md' | 'lg' | 'xl' | 'full';
}
```

**Accessibility**:
- ✅ WCAG 2.1 AA compliant (Issue #2234)
- ✅ Semantic `<main>` landmark
- ✅ Skip links for keyboard navigation
- ✅ Focus management

---

### 2. AuthLayout (Issue #2231)

**Purpose**: Specialized layout for authentication pages (login, register, password reset)

**Location**: `src/components/layouts/AuthLayout.tsx`

**Features**:
- Minimal header with back link
- Centered card design (max-width 28rem)
- Gradient background (light/dark mode)
- Optional title and subtitle
- Minimal footer

**Route Group**: `(auth)`

**Applied To**:
- `/login` - Login page
- `/register` - Registration page
- `/reset-password` - Password reset flow

**Usage**:
```tsx
// apps/web/src/app/(auth)/login/page.tsx
import { AuthLayout } from '@/components/layouts';

export default function LoginPage() {
  return (
    <AuthLayout title="Welcome Back" subtitle="Sign in to continue">
      <LoginForm />
    </AuthLayout>
  );
}
```

**Props**:
```typescript
interface AuthLayoutProps {
  children: ReactNode;
  title?: string;
  subtitle?: string;
  showBackLink?: boolean;
  className?: string;
}
```

**Design Philosophy**:
- Separation from PublicLayout (no heavy navigation)
- Focus on authentication task (minimal distraction)
- Consistent branding (MeepleAI logo in header)

---

### 3. ChatLayout (Issue #2232)

**Purpose**: Specialized layout for chat functionality with sidebar threads

**Location**: `src/components/layouts/ChatLayout.tsx`

**Features**:
- Collapsible sidebar (threads list)
- ChatHeader with game selector and actions
- Full-height message area
- Mobile Sheet for threads
- Thread management (create, share, export, delete)

**Route Group**: `(chat)`

**Applied To**:
- `/chat` - Main chat interface
- `/chat/[threadId]` - Specific thread view

**Usage**:
```tsx
// apps/web/src/app/(chat)/chat/page.tsx
import { ChatLayout } from '@/components/layouts/ChatLayout';

export default function ChatPage() {
  return (
    <ChatLayout
      threads={threads}
      selectedThread={selectedThread}
      game={selectedGame}
      games={games}
      onThreadChange={handleThreadChange}
      onGameChange={handleGameChange}
      loading={{ games: gamesLoading, title: false }}
    >
      <MessageList />
      <MessageInput />
    </ChatLayout>
  );
}
```

**Props**:
```typescript
interface ChatLayoutProps {
  children: ReactNode;
  threads: Thread[];
  selectedThread?: Thread;
  game?: Game;
  games: Game[];
  onThreadChange?: (threadId: string) => void;
  onThreadCreate?: () => void;
  onGameChange?: (gameId: string) => void;
  onTitleChange?: (title: string) => void;
  onShare?: () => void;
  onExport?: () => void;
  onDelete?: () => void;
  loading?: { games?: boolean; title?: boolean };
  className?: string;
}
```

**Pattern**: Based on AdminLayout with chat-specific features

---

### 4. AdminLayout

**Purpose**: Admin dashboard with sidebar navigation

**Location**: `src/components/admin/AdminLayout.tsx`

**Features**:
- Persistent sidebar navigation
- Breadcrumbs system
- Header actions support
- Badge notifications
- User info display
- Responsive (collapses on mobile)

**Route Group**: `admin` (no parentheses - affects URL)

**Applied To**:
- `/admin` - Admin dashboard
- `/admin/*` - All admin pages

**Usage**:
```tsx
// apps/web/src/app/admin/page.tsx
import { AdminLayout } from '@/components/admin/AdminLayout';

export default function AdminDashboardPage() {
  return (
    <AdminLayout>
      <DashboardClient />
    </AdminLayout>
  );
}
```

**Props**:
```typescript
interface AdminLayoutProps {
  children: ReactNode;
  user?: { displayName?: string; email: string; role: string };
  badges?: Record<string, number>;
  breadcrumbs?: Array<{ label: string; href?: string }>;
  showBreadcrumbs?: boolean;
  headerActions?: ReactNode;
  className?: string;
}
```

---

## Route Groups Architecture (Issue #2233)

Route groups organize routes without affecting URL structure.

### Route Group Structure

```
apps/web/src/app/
├── (public)/              # Public pages - PublicLayout
│   ├── layout.tsx         # Applies PublicLayout
│   ├── page.tsx           # Homepage /
│   ├── games/             # /games
│   ├── dashboard/         # /dashboard
│   └── settings/          # /settings
├── (auth)/                # Auth pages - AuthLayout
│   ├── layout.tsx         # Applies AuthLayout
│   ├── login/             # /login
│   ├── register/          # /register
│   └── reset-password/    # /reset-password
├── (chat)/                # Chat pages - ChatLayout
│   ├── layout.tsx         # Wrapper (ChatLayout applied in pages)
│   └── chat/              # /chat
└── admin/                 # Admin pages - AdminLayout
    ├── layout.tsx         # No layout wrapper (applied in pages)
    └── */                 # /admin/*
```

### Why Route Groups?

**Benefits**:
1. **Layout Isolation**: Each group has specialized layout
2. **URL Clean**: Parentheses don't appear in URLs
3. **Component Reuse**: Shared layouts without duplication
4. **Performance**: Code splitting by route group
5. **Maintainability**: Clear separation of concerns

**Example**:
```
(public)/games/page.tsx  → URL: /games (not /public/games)
(auth)/login/page.tsx    → URL: /login (not /auth/login)
```

---

## Accessibility Standards (WCAG 2.1 AA)

**Phase 5 (Issue #2234)**: All layouts audited and compliant.

### Requirements Met

**Color Contrast** (1.4.3):
- ✅ Primary colors darkened: 45% lightness (was 53%)
- ✅ Secondary colors darkened: 32% lightness (was 36%)
- ✅ Accent colors darkened: 58% lightness (was 65%)
- ✅ All text meets 4.5:1 minimum contrast ratio

**Landmark Structure** (Best Practice):
- ✅ Single `<main>` landmark per page
- ✅ No nested landmarks
- ✅ Unique aria-labels where needed
- ✅ Semantic HTML5 elements

**Keyboard Navigation** (2.1.1):
- ✅ All interactive elements focusable
- ✅ Logical tab order
- ✅ Visible focus indicators
- ✅ No keyboard traps

**Screen Reader Support** (4.1.2):
- ✅ ARIA labels on all controls
- ✅ Proper heading hierarchy
- ✅ Form labels associated
- ✅ Status announcements

**Testing**:
```bash
# Run accessibility audit
pnpm test:a11y

# Run E2E accessibility tests
pnpm exec playwright test accessibility.spec.ts
```

---

## Performance Optimization

**Core Web Vitals Targets** (Issue #2234):

| Metric | Target | Status |
|--------|--------|--------|
| FCP (First Contentful Paint) | <1.8s | ✅ |
| LCP (Largest Contentful Paint) | <2.5s | ✅ |
| CLS (Cumulative Layout Shift) | <0.1 | ✅ |
| TTI (Time to Interactive) | <3.8s | ✅ |
| TBT (Total Blocking Time) | <200ms | ✅ |

**Optimization Techniques**:
- React Server Components (RSC) for layouts
- Code splitting per route group
- Lazy loading for heavy components
- Font optimization (no FOUT/FOIT)
- Image optimization (WebP, lazy loading)

---

## Dark Mode Support

All layouts support dark mode via `next-themes` integration.

**Theme Provider**: `src/app/providers.tsx`

**CSS Variables**:
```css
:root {
  --background: 30 25% 97%;       /* Light beige */
  --foreground: 30 15% 15%;       /* Dark brown */
  --primary: 25 95% 45%;          /* Orange (WCAG AA) */
  --secondary: 142 76% 32%;       /* Green (WCAG AA) */
  --accent: 271 91% 58%;          /* Purple (WCAG AA) */
}

.dark {
  --background: 30 8% 10%;        /* Dark brown */
  --foreground: 30 5% 95%;        /* Light beige */
  --primary: 25 95% 60%;          /* Lighter orange */
  --secondary: 142 76% 45%;       /* Lighter green */
  --accent: 271 91% 70%;          /* Lighter purple */
}
```

**Toggle Component**: `src/components/layout/ThemeSwitcher.tsx`

---

## Mobile Responsiveness

All layouts are mobile-first with responsive breakpoints:

| Breakpoint | Width | Layout Behavior |
|------------|-------|-----------------|
| **Mobile** | <640px | Single column, bottom nav |
| **Tablet** | 640-1024px | 2-column grids, top nav |
| **Desktop** | ≥1024px | Multi-column, sidebar navigation |

**Touch Targets**: Minimum 44x44px (WCAG 2.1 AA)

**Gestures Supported**:
- Swipe for mobile Sheet navigation
- Tap for interactions
- Pinch-to-zoom allowed (no viewport restrictions)

---

## Storybook Documentation

**Stories Location**: `src/components/layouts/**/*.stories.tsx`

**Coverage** (54 stories total):
- PublicLayout: 13 stories
- PublicHeader: 7 stories
- PublicFooter: 6 stories
- AuthLayout: 14 chromatic stories
- ChatLayout: 14 chromatic stories

**Run Storybook**:
```bash
pnpm storybook          # Dev mode (port 6006)
pnpm build-storybook    # Build static
pnpm chromatic          # Visual regression tests
```

---

## Testing Strategy

### Unit Tests
- Layout component rendering
- Props validation
- Edge cases (empty state, loading)
- Dark mode toggling

**Files**: `src/components/layouts/__tests__/*.test.tsx`

### E2E Tests
- Full user journeys across layouts
- Accessibility compliance (axe-core)
- Responsive behavior
- Cross-browser compatibility

**Files**: `e2e/public-layout.spec.ts`, `e2e/accessibility.spec.ts`

### Visual Regression
- Chromatic integration
- Snapshot testing for all layout states
- Dark/light mode variations
- Responsive viewports

**Files**: `src/components/layouts/__tests__/visual/*.chromatic.test.tsx`

---

## Best Practices

### DO ✅

- Use route groups for layout organization
- Apply layout in `layout.tsx` at route group level
- Avoid nested `<main>` landmarks (PublicLayout provides wrapper)
- Use semantic HTML5 elements
- Ensure color contrast ≥4.5:1
- Test keyboard navigation
- Validate with axe-core

### DON'T ❌

- Don't add `<main>` tag in pages using PublicLayout
- Don't nest landmarks (main inside header/footer)
- Don't use `href="#"` for links (use real paths)
- Don't skip accessibility tests
- Don't use colors without contrast validation
- Don't create duplicate layouts (reuse existing)

---

## Common Patterns

### Page Using PublicLayout

```tsx
// apps/web/src/app/(public)/games/page.tsx
import { PublicLayoutWrapper } from '@/components/layouts';

export default function GamesPage() {
  return (
    <PublicLayoutWrapper containerWidth="xl">
      {/* No <main> tag needed - PublicLayout provides it */}
      <h1>Games Catalog</h1>
      <GameGrid games={games} />
    </PublicLayoutWrapper>
  );
}
```

### Page Using AuthLayout

```tsx
// apps/web/src/app/(auth)/login/page.tsx
import { AuthLayout } from '@/components/layouts';

export default function LoginPage() {
  return (
    <AuthLayout
      title="Welcome Back"
      subtitle="Sign in to continue to MeepleAI"
    >
      <LoginForm />
    </AuthLayout>
  );
}
```

### Page Using ChatLayout

```tsx
// apps/web/src/app/(chat)/chat/page.tsx
import { ChatLayout } from '@/components/layouts/ChatLayout';

export default function ChatPage() {
  return (
    <ChatLayout
      threads={threads}
      selectedThread={selectedThread}
      game={selectedGame}
      games={games}
      onThreadChange={handleThreadChange}
      onGameChange={handleGameChange}
    >
      <MessageList messages={messages} />
      <MessageInput onSend={handleSend} />
    </ChatLayout>
  );
}
```

---

## Performance Metrics

**Measured with Lighthouse** (Issue #2234):

| Layout | Performance | A11y | Best Practices | SEO |
|--------|-------------|------|----------------|-----|
| PublicLayout | 90+ | 90+ | 90+ | 90+ |
| AuthLayout | 95+ | 95+ | 95+ | 90+ |
| ChatLayout | 85+ | 90+ | 90+ | N/A |
| AdminLayout | 88+ | 90+ | 90+ | N/A |

**Bundle Sizes** (gzipped):
- PublicLayout chunk: ~45KB
- AuthLayout chunk: ~28KB
- ChatLayout chunk: ~68KB (includes thread management)
- AdminLayout chunk: ~52KB (includes charts)

---

## Troubleshooting

### Issue: Duplicate `<main>` Landmarks

**Cause**: Page has `<main>` tag when using PublicLayout

**Fix**: Remove `<main>` from page content (layout provides it)

```tsx
// ❌ Wrong
<PublicLayout>
  <main>
    <h1>Content</h1>
  </main>
</PublicLayout>

// ✅ Correct
<PublicLayout>
  <h1>Content</h1>
</PublicLayout>
```

### Issue: Color Contrast Failures

**Cause**: Using brand colors without WCAG validation

**Fix**: Use WCAG AA compliant color tokens

```css
/* globals.css - Phase 5 adjustments */
--primary: 25 95% 45%;        /* Darkened for 4.5:1 contrast */
--secondary: 142 76% 32%;     /* Darkened for 4.5:1 contrast */
--accent: 271 91% 58%;        /* Darkened for 4.5:1 contrast */
```

### Issue: Layout Shift on Load

**Cause**: Dynamic content loading without placeholders

**Fix**: Use skeleton loaders

```tsx
{isLoading ? (
  <Skeleton className="h-64 w-full" />
) : (
  <ContentComponent />
)}
```

---

## Migration Guide

### From Pages Router to App Router

**Step 1**: Move page files
```bash
# Old
pages/games/index.tsx →

# New
apps/web/src/app/(public)/games/page.tsx
```

**Step 2**: Update layout application
```tsx
// Old: pages/_app.tsx wraps everything
<PublicLayout>
  <Component {...pageProps} />
</PublicLayout>

// New: Route group layout
// apps/web/src/app/(public)/layout.tsx
export default function PublicRootLayout({ children }) {
  return <PublicLayout>{children}</PublicLayout>;
}
```

**Step 3**: Remove custom document/app
```bash
# No longer needed with App Router
rm pages/_document.tsx
rm pages/_app.tsx
```

### From Custom Layouts to Standard Layouts

**Audit existing**:
1. Identify layout patterns in pages
2. Map to closest standard layout (Public/Auth/Chat/Admin)
3. Extract custom logic to page components
4. Apply standard layout via route group

**Example**:
```tsx
// Old: Custom layout in every page
export default function MyPage() {
  return (
    <>
      <MyCustomHeader />
      <main>...</main>
      <MyCustomFooter />
    </>
  );
}

// New: Use PublicLayout
export default function MyPage() {
  return (
    // PublicLayout applied via (public)/layout.tsx
    <h1>Page Content</h1>
  );
}
```

---

## Architecture Decisions

### ADR: Route Groups Over Nested Layouts

**Decision**: Use route groups `(public)`, `(auth)`, `(chat)` instead of nested folder structures

**Rationale**:
- Cleaner URLs (no `/public/` prefix)
- Better code splitting
- Explicit layout boundaries
- Easier refactoring

**Alternative Considered**: Nested layouts with URL segments
- Rejected: Pollutes URLs, harder to refactor

### ADR: Layout Components Over HOCs

**Decision**: Use React components for layouts instead of Higher-Order Components

**Rationale**:
- Better TypeScript support
- Easier composition
- Clearer component tree
- Simpler debugging

**Alternative Considered**: `withLayout(Page)` HOC pattern
- Rejected: Complex types, harder to read

### ADR: Single `<main>` Landmark

**Decision**: Layouts provide `<main>` wrapper, pages provide content only

**Rationale**:
- WCAG 2.1 best practice (no duplicate landmarks)
- Consistent landmark structure
- Prevents accessibility violations

**Impact**: Pages must not include `<main>` tags when using PublicLayout

---

## Related Documentation

- [Route Groups Architecture](../02-development/route-groups-architecture.md)
- [Accessibility Testing Guide](../02-development/testing/accessibility-testing-guide.md)
- [Frontend Development Guide](../02-development/frontend/GUIDA-SVILUPPATORE-FRONTEND.md)
- [Design System](./design-system.md)
- [Performance Requirements](./performance-requirements.md)

---

## References

### Issues
- #2230: PublicLayout implementation
- #2231: AuthLayout implementation
- #2232: ChatLayout implementation
- #2233: Route Groups application
- #2234: Phase 5 refinement (this document)

### Pull Requests
- #2236: PublicLayout base components
- #2237: AuthLayout for auth pages
- #2238: ChatLayout system
- #2239: Route Groups implementation

---

**Maintained By**: Frontend Team
**Review Cycle**: Quarterly
**Next Review**: 2026-03-19
