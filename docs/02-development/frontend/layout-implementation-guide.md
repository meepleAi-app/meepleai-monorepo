# Layout Implementation Guide

**Audience**: Frontend Developers
**Purpose**: Practical guide for implementing pages with MeepleAI layout system
**Prerequisites**: Basic Next.js 16 App Router knowledge

---

## Quick Start

### 1. Choose the Right Layout

| Layout | Use When | Example Pages |
|--------|----------|---------------|
| **PublicLayout** | Marketing, catalog, public info | Homepage, Games, Settings |
| **AuthLayout** | Login, signup, password flows | Login, Register, Reset Password |
| **ChatLayout** | Chat functionality | Chat interface |
| **AdminLayout** | Admin dashboard | Admin pages |
| **Custom** | Unique UX (rare) | Special landing pages |

### 2. Create Page in Correct Route Group

```bash
# Public page
apps/web/src/app/(public)/your-page/page.tsx

# Auth page
apps/web/src/app/(auth)/your-page/page.tsx

# Chat page
apps/web/src/app/(chat)/your-page/page.tsx

# Admin page
apps/web/src/app/admin/your-page/page.tsx
```

### 3. Write Page Component

The route group `layout.tsx` applies the layout automatically - **don't wrap your page content**.

---

## PublicLayout Implementation

### Basic Page

```tsx
// apps/web/src/app/(public)/about/page.tsx
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'About Us - MeepleAI',
  description: 'Learn about MeepleAI',
};

export default function AboutPage() {
  return (
    <>
      {/* No layout wrapper needed - applied by (public)/layout.tsx */}
      {/* No <main> tag needed - PublicLayout provides it */}

      <h1>About MeepleAI</h1>

      <section>
        <h2>Our Mission</h2>
        <p>We help board gamers resolve rules disputes with AI.</p>
      </section>

      <section>
        <h2>Our Team</h2>
        <p>Built by board game enthusiasts for board game enthusiasts.</p>
      </section>
    </>
  );
}
```

**Key Points**:
- ✅ No `<main>` tag (layout provides it)
- ✅ Use semantic HTML (`<section>`, `<h1>`)
- ✅ Export metadata for SEO
- ✅ Server Component by default (no 'use client')

### Page with Custom Container Width

```tsx
// apps/web/src/app/(public)/games/page.tsx
import { PublicLayoutWrapper } from '@/components/layouts';
import { GameGrid } from './components/GameGrid';

export default function GamesPage() {
  return (
    <PublicLayoutWrapper containerWidth="xl">
      <h1 className="text-4xl font-bold mb-8">Games Catalog</h1>
      <GameGrid />
    </PublicLayoutWrapper>
  );
}
```

**When to use `PublicLayoutWrapper`**:
- Need custom container width (default: full)
- Need to pass auth state from client
- Page has client-side logic

### Page with Authentication State

```tsx
// apps/web/src/app/(public)/dashboard/page.tsx
'use client';

import { PublicLayoutWrapper } from '@/components/layouts';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';

export default function DashboardPage() {
  const { data: user } = useCurrentUser();

  return (
    <PublicLayoutWrapper containerWidth="lg">
      <h1>Welcome, {user?.displayName}!</h1>
      {/* Page content */}
    </PublicLayoutWrapper>
  );
}
```

---

## AuthLayout Implementation

### Login Page

```tsx
// apps/web/src/app/(auth)/login/page.tsx
import { AuthLayout } from '@/components/layouts';
import { LoginForm } from '@/components/auth/LoginForm';

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

### Register Page

```tsx
// apps/web/src/app/(auth)/register/page.tsx
import { AuthLayout } from '@/components/layouts';
import { RegisterForm } from '@/components/auth/RegisterForm';

export default function RegisterPage() {
  return (
    <AuthLayout
      title="Create Your Account"
      subtitle="Join thousands of board game enthusiasts"
    >
      <RegisterForm />
    </AuthLayout>
  );
}
```

### Password Reset (Multi-Step)

```tsx
// apps/web/src/app/(auth)/reset-password/page.tsx
'use client';

import { useState } from 'react';
import { AuthLayout } from '@/components/layouts';

export default function ResetPasswordPage() {
  const [step, setStep] = useState<'request' | 'verify' | 'reset'>('request');

  if (step === 'request') {
    return (
      <AuthLayout title="Reset Password" subtitle="Enter your email">
        <RequestResetForm onSuccess={() => setStep('verify')} />
      </AuthLayout>
    );
  }

  if (step === 'verify') {
    return (
      <AuthLayout title="Check Your Email" showBackLink={false}>
        <VerifyEmailMessage />
      </AuthLayout>
    );
  }

  return (
    <AuthLayout title="Set New Password">
      <ResetPasswordForm />
    </AuthLayout>
  );
}
```

**AuthLayout Best Practices**:
- ✅ Use `title` and `subtitle` for context
- ✅ Keep content focused (single form per view)
- ✅ Use `showBackLink={false}` for success states
- ✅ Center card is max-width 28rem (optimal for forms)

---

## ChatLayout Implementation

### Chat Page with Threads

```tsx
// apps/web/src/app/(chat)/chat/page.tsx
'use client';

import { ChatLayout } from '@/components/layouts/ChatLayout';
import { useThreads, useGames } from '@/hooks/queries';
import { useState } from 'react';

export default function ChatPage() {
  const [selectedThreadId, setSelectedThreadId] = useState<string>();
  const { data: threads } = useThreads();
  const { data: games } = useGames();

  const selectedThread = threads?.find(t => t.id === selectedThreadId);

  return (
    <ChatLayout
      threads={threads || []}
      selectedThread={selectedThread}
      games={games?.games || []}
      onThreadChange={setSelectedThreadId}
      onThreadCreate={handleCreateThread}
      onGameChange={handleGameChange}
    >
      {/* Message area content */}
      {selectedThread ? (
        <>
          <MessageList threadId={selectedThread.id} />
          <MessageInput onSend={handleSend} />
        </>
      ) : (
        <EmptyThreadState />
      )}
    </ChatLayout>
  );
}
```

**ChatLayout Props Explained**:
- `threads`: Sidebar thread list
- `selectedThread`: Currently active thread (highlighted)
- `games`: Game selector options
- `onThreadChange`: Callback when user clicks thread
- `onGameChange`: Callback for game selector
- `loading`: Optional loading states for games/title

---

## Common Mistakes to Avoid

### ❌ WRONG: Nested `<main>` Tags

```tsx
// apps/web/src/app/(public)/page.tsx
export default function Page() {
  return (
    <main>  {/* ❌ PublicLayout already provides <main> */}
      <h1>Content</h1>
    </main>
  );
}
```

**Result**: Accessibility violation - duplicate main landmarks

### ✅ CORRECT: No `<main>` Tag

```tsx
export default function Page() {
  return (
    <>  {/* ✅ Let PublicLayout provide <main> wrapper */}
      <h1>Content</h1>
    </>
  );
}
```

---

### ❌ WRONG: Using Wrong Layout

```tsx
// apps/web/src/app/(public)/login/page.tsx
export default function LoginPage() {
  return (
    <PublicLayoutWrapper>  {/* ❌ Too heavy for auth flow */}
      <LoginForm />
    </PublicLayoutWrapper>
  );
}
```

**Result**: Unnecessary navigation, footer distracts from auth task

### ✅ CORRECT: Use AuthLayout

```tsx
// apps/web/src/app/(auth)/login/page.tsx
export default function LoginPage() {
  return (
    <AuthLayout title="Login">  {/* ✅ Focused auth experience */}
      <LoginForm />
    </AuthLayout>
  );
}
```

---

### ❌ WRONG: Hardcoded Header/Footer

```tsx
export default function Page() {
  return (
    <>
      <header>Custom Header</header>  {/* ❌ Breaks consistency */}
      <main>Content</main>
      <footer>Custom Footer</footer>
    </>
  );
}
```

**Result**: Inconsistent UX, duplicate navigation, accessibility issues

### ✅ CORRECT: Use Layout System

```tsx
export default function Page() {
  return (
    <>  {/* ✅ PublicLayout provides header/footer */}
      <h1>Content</h1>
    </>
  );
}
```

---

## Advanced Patterns

### Conditional Layout Based on Auth

```tsx
// apps/web/src/app/(public)/settings/page.tsx
'use client';

import { PublicLayoutWrapper } from '@/components/layouts';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { redirect } from 'next/navigation';

export default function SettingsPage() {
  const { data: user, isLoading } = useCurrentUser();

  // Redirect if not authenticated
  if (!isLoading && !user) {
    redirect('/login?redirect=/settings');
  }

  if (isLoading) {
    return (
      <PublicLayoutWrapper>
        <SettingsSkeleton />
      </PublicLayoutWrapper>
    );
  }

  return (
    <PublicLayoutWrapper>
      {/* User prop automatically passed by PublicLayoutWrapper */}
      <h1>Settings for {user.displayName}</h1>
      <SettingsForm user={user} />
    </PublicLayoutWrapper>
  );
}
```

### Custom Background for Section

```tsx
export default function Page() {
  return (
    <>
      {/* Hero with custom background */}
      <section className="bg-gradient-to-br from-primary/10 to-secondary/10 -mx-4 sm:-mx-6 lg:-mx-8 px-4 sm:px-6 lg:px-8 py-20">
        <h1>Hero Content</h1>
      </section>

      {/* Regular content */}
      <section className="py-8">
        <h2>Standard Content</h2>
      </section>
    </>
  );
}
```

**Note**: Use negative margins to break out of container, then restore padding

---

## Accessibility Checklist

Before shipping a new page:

### Required
- [ ] No `<main>` tag in page (layout provides it)
- [ ] Proper heading hierarchy (h1 → h2 → h3)
- [ ] All images have `alt` text
- [ ] All forms have proper labels
- [ ] Color contrast ≥4.5:1 for normal text
- [ ] Touch targets ≥44x44px for mobile
- [ ] Focus indicators visible
- [ ] Keyboard navigation works

### Testing
- [ ] Run `pnpm test:a11y` (passes)
- [ ] Manual keyboard navigation test
- [ ] Test with screen reader (NVDA/VoiceOver)
- [ ] Test mobile viewport (375px, 768px)
- [ ] Test dark mode

---

## Performance Checklist

### Required
- [ ] Server Component by default (no unnecessary 'use client')
- [ ] Use `next/image` for images (not `<img>`)
- [ ] Lazy load heavy components
- [ ] Avoid layout shift (reserve space for dynamic content)
- [ ] Optimize fonts (preload, font-display: swap)

### Testing
```bash
# Build for production
pnpm build

# Run Lighthouse audit
# (Use Chrome DevTools → Lighthouse)

# Target scores
Performance: ≥90
Accessibility: ≥90
Best Practices: ≥90
SEO: ≥90
```

---

## Dark Mode Implementation

### Using Theme Switcher

All layouts include ThemeSwitcher in header.

### Custom Dark Mode Styles

```tsx
<div className="bg-background text-foreground">
  {/* Automatically adapts to light/dark */}
  <p className="text-muted-foreground">Secondary text</p>

  {/* Custom dark mode styles */}
  <div className="bg-white dark:bg-slate-900">
    <p className="text-slate-900 dark:text-slate-100">Content</p>
  </div>
</div>
```

**Semantic Tokens** (automatically adapt):
- `bg-background` / `text-foreground`
- `bg-card` / `text-card-foreground`
- `bg-muted` / `text-muted-foreground`
- `bg-accent` / `text-accent-foreground`

---

## Mobile-First Responsive Design

### Breakpoint Strategy

```tsx
// Mobile first (default)
<div className="grid grid-cols-1 sm:grid-cols-2 lg:grid-cols-3">
  {/* 1 col mobile, 2 cols tablet, 3 cols desktop */}
</div>
```

**Breakpoints**:
- `sm`: 640px (tablet)
- `md`: 768px (tablet landscape)
- `lg`: 1024px (desktop)
- `xl`: 1280px (wide desktop)
- `2xl`: 1536px (ultra-wide)

### Touch Targets

```tsx
// Minimum 44x44px for mobile
<button className="min-h-[44px] min-w-[44px] touch-target">
  Tap Me
</button>
```

**Utility**: `.touch-target` class available in `design-tokens.css`

---

## Storybook Integration

### Creating Stories for New Components

```tsx
// your-component.stories.tsx
import type { Meta, StoryObj } from '@storybook/react';
import { YourComponent } from './YourComponent';

const meta: Meta<typeof YourComponent> = {
  title: 'Components/YourComponent',
  component: YourComponent,
  parameters: {
    layout: 'fullscreen', // or 'centered' | 'padded'
  },
};

export default meta;
type Story = StoryObj<typeof YourComponent>;

export const Default: Story = {
  args: {
    // Component props
  },
};

export const DarkMode: Story = {
  args: {
    ...Default.args,
  },
  parameters: {
    theme: 'dark',
  },
};

export const Mobile: Story = {
  args: {
    ...Default.args,
  },
  parameters: {
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};
```

### Running Stories

```bash
# Development
pnpm storybook

# Build
pnpm build-storybook

# Visual regression (Chromatic)
pnpm chromatic
```

---

## Testing New Pages

### 1. Unit Tests

```tsx
// your-page.test.tsx
import { render, screen } from '@testing-library/react';
import YourPage from './page';

describe('YourPage', () => {
  it('renders heading', () => {
    render(<YourPage />);
    expect(screen.getByRole('heading', { name: /your page/i })).toBeInTheDocument();
  });

  it('has no accessibility violations', async () => {
    const { container } = render(<YourPage />);
    const results = await axe(container);
    expect(results).toHaveNoViolations();
  });
});
```

### 2. E2E Tests

```tsx
// e2e/your-page.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Your Page', () => {
  test('should load successfully', async ({ page }) => {
    await page.goto('/your-page');
    await expect(page.getByRole('heading', { name: /your page/i })).toBeVisible();
  });

  test('should have no accessibility violations', async ({ page }) => {
    await page.goto('/your-page');

    const results = await new AxeBuilder({ page })
      .withTags(['wcag2a', 'wcag2aa', 'wcag21a', 'wcag21aa'])
      .analyze();

    expect(results.violations).toEqual([]);
  });
});
```

### 3. Run Tests

```bash
# Unit tests
pnpm test your-page

# E2E tests
pnpm test:e2e -- your-page.spec.ts

# Accessibility audit
pnpm test:a11y
```

---

## Debugging Layout Issues

### Issue: Page Content Not Visible

**Check**:
1. Is route in correct group? `(public)` vs `(auth)` vs `(chat)`
2. Does `layout.tsx` exist in route group?
3. Is content wrapped in unnecessary div that hides it?

**Debug**:
```bash
# Check route group structure
ls apps/web/src/app/(public)/

# Verify layout.tsx exists
cat apps/web/src/app/(public)/layout.tsx
```

### Issue: Styling Not Applied

**Check**:
1. Are Tailwind classes spelled correctly?
2. Is component using semantic tokens (`bg-background`) vs hardcoded colors?
3. Is dark mode working? (check ThemeProvider in app/providers.tsx)

**Debug**:
```tsx
// Add temporary className to debug
<div className="bg-red-500">  {/* Should be visible red */}
  {children}
</div>
```

### Issue: Layout Shift on Load

**Check**:
1. Are images using `next/image` with width/height?
2. Are dynamic components using skeleton loaders?
3. Are fonts preloaded?

**Fix**:
```tsx
// Reserve space for dynamic content
{isLoading ? (
  <Skeleton className="h-64 w-full" />  {/* Same height as content */}
) : (
  <DynamicComponent />  {/* Height: h-64 */}
)}
```

---

## Code Review Checklist

### Layout Implementation
- [ ] Correct route group used
- [ ] No nested `<main>` landmarks
- [ ] Semantic HTML5 elements
- [ ] Proper heading hierarchy (h1 → h2 → h3)
- [ ] No hardcoded header/footer

### Accessibility
- [ ] Alt text for all images
- [ ] Labels for all form inputs
- [ ] ARIA attributes where needed
- [ ] Color contrast validated (≥4.5:1)
- [ ] Keyboard navigation tested

### Performance
- [ ] Server Components where possible
- [ ] Images optimized (WebP, lazy load)
- [ ] No layout shift (Skeleton loaders)
- [ ] Code splitting (dynamic imports)

### Dark Mode
- [ ] Uses semantic tokens
- [ ] Tested in both light/dark
- [ ] No hardcoded colors
- [ ] Contrast validated in both modes

---

## FAQ

### Q: When should I create a custom layout?

**A**: Rarely. Use existing layouts first. Only create custom if:
- Page has truly unique UX (marketing landing pages)
- Existing layouts don't fit after trying to adapt
- Design team approved custom layout

**Process**: Discuss with team before implementing custom layout.

### Q: Can I mix layouts on one page?

**A**: No. Each page has one layout determined by its route group.

If you need different layouts, create separate pages:
```
/marketing  → (public) route group → PublicLayout
/app        → (chat) route group → ChatLayout
```

### Q: How do I add a new nav item?

**PublicHeader**: Edit `PublicHeader.tsx` → `NAV_ITEMS` array

**AuthLayout**: No navigation (intentional)

**ChatLayout**: Header actions via props

**AdminLayout**: Edit `AdminSidebar.tsx` → `SIDEBAR_ITEMS` array

### Q: How do I test responsive layouts?

**Unit Tests**: Use `@testing-library/react` with window.matchMedia mocks

**E2E Tests**: Use Playwright viewport settings
```tsx
test('mobile layout', async ({ page }) => {
  await page.setViewportSize({ width: 390, height: 844 });
  await page.goto('/your-page');
  // Assertions
});
```

**Storybook**: Use viewport addon toolbar

### Q: What if I need a footer-less page?

**PublicLayout**: Footer is always included (design decision)

**AuthLayout**: Minimal footer included

**Custom**: Create custom layout for truly unique UX (rare)

---

## Related Guides

- [Layout System Architecture](../../04-frontend/layout-system.md) - High-level architecture
- [Accessibility Testing Guide](../testing/accessibility-testing-guide.md) - A11y testing
- [React 19 Patterns](../testing/react-19-patterns.md) - React 19 best practices
- [Design System](../../04-frontend/design-system.md) - Colors, typography, tokens

---

**Questions?** Create issue with `documentation` or `frontend` label

**Last Updated**: 2025-12-19 (Phase 5)
