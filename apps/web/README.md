# MeepleAI Web Frontend

**Next.js 16 + React 19** - Modern, accessible, Italian-first web application for board game rules assistance.

---

## 📁 Directory Structure

```
apps/web/
├── src/                                 # Application source
│   ├── app/                             # Next.js App Router (server + client routes)
│   │   ├── layout.tsx                   # Root layout (RSC)
│   │   ├── providers.tsx                # Shared client providers (Intl, Theme, Query, Auth)
│   │   ├── page.tsx                     # Landing page (RSC + RSC streaming)
│   │   ├── admin/*                      # Admin dashboard routes (8 nested segments)
│   │   ├── chat/page.tsx                # Chat experience (Zustand-powered)
│   │   ├── upload/page.tsx              # PDF uploads + rulebook matcher
│   │   ├── sessions/{page.tsx,history,[id]/page.tsx}
│   │   └── ... (31 total route segments incl. board-game-ai/, chess/, versions/, n8n/, etc.)
│   ├── pages/api/                       # API routes (health checks, legacy proxy endpoints)
│   ├── components/                      # React components
│   │   ├── ui/                          # Shadcn/UI primitives (CVA variants)
│   │   ├── accessible/                  # WCAG helpers (AccessibleButton, SkipLink, etc.)
│   │   ├── auth/, chat/, games/, pdf/   # Feature-specific component stacks
│   │   └── layout/, modals/, errors/    # Shell, keyboard shortcuts, dialog system
│   ├── store/                           # Zustand stores (chat slices, upload queue, toasts)
│   ├── hooks/                           # Custom hooks (session check, keyboard shortcuts, SSE)
│   ├── lib/
│   │   ├── api/                         # Modular API SDK (core HttpClient + feature clients)
│   │   ├── logger/, telemetry/          # Observability helpers
│   │   └── utils.ts                     # Shared utilities
│   ├── locales/                         # React-intl locale bundles + helpers
│   ├── styles/                          # Tailwind + shared CSS (globals, diff, prism)
│   ├── workers/                         # Web Workers (PDF parsing, upload queue)
│   ├── test-utils/                      # Jest helpers (renderWithProviders, fixtures)
│   └── __tests__/                       # Unit/integration tests (mirrors src/)
├── e2e/                                 # Playwright specs + page objects
├── .storybook/                          # Storybook configuration
├── scripts/                             # Build/dev scripts
├── public/                              # Static assets
├── next.config.js                       # Next.js configuration (App Router defaults)
├── tailwind.config.js                   # Tailwind CSS 4 configuration
├── jest.config.js                       # Jest configuration
├── playwright.config.ts                 # Playwright configuration
├── tsconfig.json                        # TypeScript project refs
└── package.json                         # Dependencies
```

---

## 🚀 Quick Start

### Prerequisites

- Node.js 20+ (LTS)
- pnpm 9+
- Backend API running (http://localhost:8080)

### Install Dependencies

```bash
cd apps/web
pnpm install
```

### Development Server

```bash
# Start dev server (http://localhost:3000)
pnpm dev

# With turbo mode
pnpm turbo dev
```

### Build & Production

```bash
# Build for production
pnpm build

# Start production server
pnpm start

# Preview production build
pnpm preview
```

### Testing

```bash
# Run unit tests (Jest)
pnpm test

# Watch mode
pnpm test:watch

# Coverage report
pnpm test:coverage

# E2E tests (Playwright)
pnpm test:e2e

# E2E UI mode
pnpm test:e2e:ui
```

### Type Checking

```bash
# Type check
pnpm typecheck

# Type check (watch mode)
pnpm typecheck:watch
```

### Linting

```bash
# Lint
pnpm lint

# Lint with auto-fix
pnpm lint:fix
```

### Storybook

```bash
# Start Storybook (http://localhost:6006)
pnpm storybook

# Build Storybook
pnpm build-storybook
```

---

## 🎨 Tech Stack

### Core

- **Framework**: Next.js 16 App Router (React Server Components + streaming)
- **React**: React 19 (with Server Components)
- **TypeScript**: Strict mode enabled
- **Styling**: Tailwind CSS 4 + CSS Modules + CSS Variables

### Application Shell

- **Root Layout**: `src/app/layout.tsx` (server) renders `<AppProviders>` from `src/app/providers.tsx`
- **Shared Providers**: ThemeProvider, IntlProvider, QueryProvider, AuthProvider, Error Boundaries
- **Global UX**: Accessible skip link, keyboard shortcuts modal, session timeout modal via Zustand + TanStack Query

### UI Components

- **Design System**: Shadcn/UI (Radix UI + Tailwind)
- **Components**: 40+ accessible, composable components
  - Button, Input, Select, Checkbox, Switch
  - Dialog, Dropdown Menu, Popover, Tooltip
  - Table, Tabs, Card, Alert, Badge
  - Form, Label, Separator, Skeleton

### State Management

- **Server/Cache State**: TanStack Query (React Query v5) with centralized `QueryProvider`
- **Client State**: Modular Zustand stores (`src/store/*`) for chat, upload queue, command palette
- **Forms**: React Hook Form + Zod validation + controlled inputs

### Testing

- **Unit/Integration**: Jest 29 + React Testing Library
- **E2E**: Playwright
- **Coverage**: 90%+ enforced
- **Mocking**: MSW (Mock Service Worker)

### Development Tools

- **Bundler**: Next.js (Turbopack in dev, Webpack in prod)
- **Linter**: ESLint 9 (with Next.js config)
- **Formatter**: Prettier (via ESLint)
- **Type Checker**: TypeScript 5.6+

---

## 📦 Key Features

### Authentication

**Multi-mode authentication**:
- Cookie-based session (default)
- API key authentication
- OAuth (Google, Discord, GitHub)
- 2FA (TOTP + backup codes)

**Components**:
- `LoginForm` - Email/password login
- `RegisterForm` - User registration
- `TwoFactorPrompt` - 2FA verification
- `OAuthButtons` - Social login buttons

**Pages**:
- `/login` - Login page
- `/register` - Registration page
- `/auth/callback` - OAuth callback handler

### Chat Interface

**RAG-powered Q&A**:
- Streaming responses (SSE)
- Citation support with source highlighting
- Thread management (create, list, delete)
- Hybrid search (vector + keyword)

**Components**:
- `ChatInterface` - Main chat UI
- `MessageList` - Message history
- `MessageInput` - User input (with file upload)
- `CitationPanel` - Source citations
- `ConfidenceIndicator` - Answer confidence (0.0-1.0)

**Pages**:
- `/chat` - Chat interface
- `/chat/[threadId]` - Specific thread

### Games Catalog

**Board game management**:
- Browse games (grid/list view)
- Search & filter
- Game details (rules, players, duration)
- Add/edit games (admin only)

**Components**:
- `GameCard` - Game preview card
- `GameGrid` - Grid layout
- `GameDetail` - Full game details
- `GameForm` - Add/edit game (admin)

**Pages**:
- `/games` - Games catalog
- `/games/[id]` - Game detail page

### Settings Page (SPRINT-1)

**4-tab comprehensive settings**:

1. **Profile** (UI ready, backend pending)
   - Display name, email
   - Password change
   - Avatar upload

2. **Preferences** (mock data)
   - Language (Italian/English)
   - Theme (light/dark/system)
   - Notifications
   - Data retention

3. **Privacy** (fully functional)
   - 2FA management (enable/disable)
   - OAuth linking (Google, Discord, GitHub)
   - Backup codes

4. **Advanced** (placeholders)
   - API keys management
   - Active sessions
   - Account deletion

**Components**:
- `SettingsTabs` - Tab navigation
- `ProfileSettings` - Profile tab content
- `PreferencesSettings` - Preferences tab
- `PrivacySettings` - Privacy tab
- `AdvancedSettings` - Advanced tab

**Pages**:
- `/settings` - Settings page (4 tabs)

### PDF Upload

**3-stage PDF processing**:
- Drag & drop upload
- Quality validation
- Processing status (Unstructured → SmolDocling → Docnet)
- Preview extracted text

**Components**:
- `PdfUploader` - Drag & drop component
- `PdfViewer` - PDF preview
- `ExtractionStatus` - Processing status
- `QualityReport` - Quality metrics display

**Pages**:
- `/documents/upload` - PDF upload page

### Admin Dashboard

**Admin-only features**:
- User management
- Game moderation
- System configuration
- Analytics & metrics

**Components**:
- `AdminLayout` - Admin layout wrapper
- `UserTable` - User management table
- `SystemConfig` - Configuration editor
- `AnalyticsDashboard` - Metrics display

**Pages**:
- `/admin` - Admin dashboard
- `/admin/users` - User management
- `/admin/config` - System configuration

---

## 🔧 Configuration

### Environment Variables

```bash
# .env.local (create from .env.example)

# API Base URL
NEXT_PUBLIC_API_BASE=http://localhost:8080

# Public configuration
NEXT_PUBLIC_APP_NAME="MeepleAI"
NEXT_PUBLIC_APP_LOCALE="it-IT"

# Feature flags
NEXT_PUBLIC_ENABLE_OAUTH=true
NEXT_PUBLIC_ENABLE_2FA=true
```

### API Client Configuration

**lib/api.ts**:
```typescript
const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// Dual authentication: Cookie + API Key
const headers = {
  'Content-Type': 'application/json',
  ...(apiKey && { 'X-API-Key': apiKey })
};

// Include credentials for cookie auth
fetch(url, { credentials: 'include', headers });
```

### Tailwind CSS 4 Configuration

**tailwind.config.js**:
```javascript
module.exports = {
  content: ['./src/**/*.{ts,tsx}'],
  theme: {
    extend: {
      colors: {
        // Shadcn/UI color variables
        border: 'hsl(var(--border))',
        input: 'hsl(var(--input))',
        ring: 'hsl(var(--ring))',
        background: 'hsl(var(--background))',
        foreground: 'hsl(var(--foreground))',
        primary: { /* ... */ },
        secondary: { /* ... */ },
        destructive: { /* ... */ },
        muted: { /* ... */ },
        accent: { /* ... */ },
        popover: { /* ... */ },
        card: { /* ... */ },
      }
    }
  }
};
```

---

## 🧪 Testing

### Test Structure

```
src/__tests__/
├── components/          # Component tests (mirroring src/components/)
├── pages/              # Page-level tests (covering src/app route segments)
├── hooks/              # Hook tests
├── utils/              # Utility tests
├── integration/        # Integration tests
└── fixtures/           # Test data
```

### Testing Stack

- **Jest**: Test runner
- **React Testing Library**: Component testing
- **MSW**: API mocking
- **Playwright**: E2E testing
- **Testing Library User Event**: User interaction simulation

### Test Coverage Requirements

- **Overall**: 90%+ enforced
- **Components**: 90%+
- **Hooks**: 95%+
- **Utils**: 95%+
- **Pages**: 80%+ (integration tests cover the rest)

### Example Component Test

```typescript
// src/components/auth/__tests__/LoginForm.test.tsx
import { render, screen, userEvent } from '@/test-utils';
import { LoginForm } from '../LoginForm';

describe('LoginForm', () => {
  it('submits login credentials', async () => {
    const onSubmit = jest.fn();
    render(<LoginForm onSubmit={onSubmit} />);

    await userEvent.type(screen.getByLabelText(/email/i), 'user@example.com');
    await userEvent.type(screen.getByLabelText(/password/i), 'password123');
    await userEvent.click(screen.getByRole('button', { name: /login/i }));

    expect(onSubmit).toHaveBeenCalledWith({
      email: 'user@example.com',
      password: 'password123'
    });
  });
});
```

### Example E2E Test

```typescript
// e2e/auth/login.spec.ts
import { test, expect } from '@playwright/test';

test('user can login', async ({ page }) => {
  await page.goto('/login');

  await page.fill('input[name="email"]', 'admin@meepleai.dev');
  await page.fill('input[name="password"]', 'Demo123!');
  await page.click('button:has-text("Login")');

  await expect(page).toHaveURL('/chat');
  await expect(page.locator('text=Welcome')).toBeVisible();
});
```

### Running Tests

```bash
# Unit tests
pnpm test                 # Run all tests
pnpm test:watch          # Watch mode
pnpm test:coverage       # Coverage report

# E2E tests
pnpm test:e2e            # Headless mode
pnpm test:e2e:ui         # UI mode (interactive)
pnpm test:e2e:debug      # Debug mode

# Specific test file
pnpm test LoginForm      # Run tests matching "LoginForm"
```

---

## 🎨 Styling & Design

### Tailwind CSS 4

**Utility-first CSS framework**:
```tsx
<button className="px-4 py-2 bg-primary text-primary-foreground rounded-md hover:bg-primary/90">
  Click me
</button>
```

### Shadcn/UI Components

**Install new component**:
```bash
pnpx shadcn@latest add button
pnpx shadcn@latest add dialog
pnpx shadcn@latest add select
```

**Usage**:
```tsx
import { Button } from '@/components/ui/button';
import { Dialog, DialogTrigger, DialogContent } from '@/components/ui/dialog';

<Dialog>
  <DialogTrigger asChild>
    <Button>Open Dialog</Button>
  </DialogTrigger>
  <DialogContent>
    <p>Dialog content</p>
  </DialogContent>
</Dialog>
```

### Dark Mode

**System preference detection**:
```tsx
// Automatic dark mode based on system preference
// CSS variables in src/styles/globals.css:

:root {
  --background: 0 0% 100%;
  --foreground: 222.2 84% 4.9%;
}

.dark {
  --background: 222.2 84% 4.9%;
  --foreground: 210 40% 98%;
}
```

---

## ♿ Accessibility

**WCAG 2.1 AA Compliance**:
- Semantic HTML
- ARIA labels and roles
- Keyboard navigation
- Focus management
- Color contrast (4.5:1 minimum)
- Screen reader support

**Accessible Components**:
- All Shadcn/UI components are accessible by default (Radix UI primitives)
- Custom components follow ARIA best practices
- Focus trap in modals/dialogs
- Skip navigation links

**Testing Accessibility**:
```bash
# Run accessibility tests
pnpm test:a11y

# E2E accessibility checks (Playwright + axe-core)
pnpm test:e2e -- --grep @a11y
```

---

## 🌍 Internationalization

**Italian-first, English support**:

Current: Hardcoded Italian strings
Planned: i18next integration (Q1 2025)

**Example (future)**:
```tsx
import { useTranslation } from '@/hooks/useTranslation';

function Component() {
  const { t } = useTranslation();
  return <h1>{t('welcome')}</h1>;
}
```

---

## 📊 Performance

### Metrics Targets

- **FCP (First Contentful Paint)**: <1.8s
- **LCP (Largest Contentful Paint)**: <2.5s
- **CLS (Cumulative Layout Shift)**: <0.1
- **FID (First Input Delay)**: <100ms
- **TTI (Time to Interactive)**: <3.8s

### Optimization Techniques

- **Code Splitting**: Automatic via Next.js dynamic imports
- **Image Optimization**: Next.js Image component
- **Font Optimization**: next/font with font subsetting
- **Bundle Analysis**: `pnpm analyze`
- **Tree Shaking**: Automatic (ES modules)
- **Minification**: Terser (production builds)

### Bundle Size

```bash
# Analyze bundle
pnpm analyze

# View bundle size report
pnpm build && ls -lh .next/static/chunks/
```

See [BUNDLE_SIZE_ANALYSIS.md](./BUNDLE_SIZE_ANALYSIS.md) for details.

---

## 🐛 Troubleshooting

### Build Errors

```bash
# Clear Next.js cache
rm -rf .next

# Clear node_modules
rm -rf node_modules pnpm-lock.yaml
pnpm install

# Type errors
pnpm typecheck
```

### API Connection Issues

```bash
# Check API base URL
echo $NEXT_PUBLIC_API_BASE

# Test API health
curl http://localhost:8080/health

# Check CORS (browser DevTools Network tab)
# Should see Access-Control-Allow-Origin header
```

### Storybook Issues

```bash
# Clear Storybook cache
rm -rf .storybook/cache

# Rebuild Storybook
pnpm build-storybook

# Check Storybook logs
pnpm storybook --debug
```

---

## 📚 Documentation

### Internal Documentation

- **[Frontend Architecture](../../docs/04-frontend/architecture.md)** - Architecture overview
- **[Design System](../../docs/04-frontend/design-system.md)** - UI components & patterns
- **[Accessibility Standards](../../docs/04-frontend/accessibility-standards.md)** - A11y guidelines
- **[Testing Strategy](../../docs/04-frontend/testing-strategy.md)** - Testing approach
- **[Shadcn/UI Installation](../../docs/04-frontend/shadcn-ui-installation.md)** - Component setup
- **[CLAUDE.md](../../CLAUDE.md)** - Complete development guide

### External Resources

- **Next.js**: https://nextjs.org/docs
- **React 19**: https://react.dev
- **Tailwind CSS 4**: https://tailwindcss.com
- **Shadcn/UI**: https://ui.shadcn.com
- **React Query**: https://tanstack.com/query/latest
- **Playwright**: https://playwright.dev

---

## 🚀 Deployment

### Production Build

```bash
# Build production bundle
pnpm build

# Start production server
pnpm start
```

### Docker Deployment

```bash
# Build Docker image
docker build -t meepleai-web:latest .

# Run container
docker run -p 3000:3000 \
  -e NEXT_PUBLIC_API_BASE="https://api.meepleai.dev" \
  meepleai-web:latest
```

### Vercel Deployment

```bash
# Install Vercel CLI
pnpm add -g vercel

# Deploy
vercel

# Production deployment
vercel --prod
```

**Environment Variables** (Vercel dashboard):
- `NEXT_PUBLIC_API_BASE` - API endpoint URL

See [Frontend Deployment Guide](../../docs/05-operations/deployment/frontend-deployment.md) for details.

---

## 🤝 Contributing

### Code Style

- **TypeScript**: Strict mode enabled, avoid `any`
- **React**: Functional components, hooks
- **Naming**: PascalCase (components), camelCase (functions), kebab-case (files)
- **Imports**: Absolute paths (`@/components/...`)
- **Comments**: JSDoc for complex functions

### Component Development

1. **Create component** in `src/components/[category]/ComponentName.tsx`
2. **Write tests** in `src/components/[category]/__tests__/ComponentName.test.tsx`
3. **Add Storybook story** (optional) in `src/components/[category]/ComponentName.stories.tsx`
4. **Export** from `src/components/[category]/index.ts`
5. **Document** in component JSDoc

### Pull Request Checklist

- [ ] Tests written (90%+ coverage)
- [ ] TypeScript types added
- [ ] ESLint passing (`pnpm lint`)
- [ ] Tests passing (`pnpm test`)
- [ ] E2E tests updated (if applicable)
- [ ] Accessibility tested
- [ ] Storybook story added (if UI component)
- [ ] Documentation updated
- [ ] PR has `[WEB]` prefix

---

**Last Updated**: 2025-11-15
**Maintainer**: Frontend Team
**Test Coverage**: 90.03%
**Bundle Size**: ~850KB (gzipped)
