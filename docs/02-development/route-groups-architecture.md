# Route Groups Architecture

**Issue**: [#2233](https://github.com/DegrassiAaron/meepleai-monorepo/issues/2233) - Phase 4: Implementa Route Groups e Applica Layout

## 🎯 Overview

MeepleAI utilizza **Next.js Route Groups** per organizzare le pagine dell'app in categorie logiche e applicare automaticamente i layout appropriati. I route groups sono trasparenti alle URL (non modificano i percorsi pubblici).

## 📐 Struttura Route Groups

```
apps/web/src/app/
├── layout.tsx                    ← Root Layout (providers, font, HyperDX)
│
├── (public)/                     ← PublicLayout
│   ├── layout.tsx
│   ├── page.tsx                  → /
│   ├── games/                    → /games/*
│   ├── dashboard/                → /dashboard
│   ├── settings/                 → /settings
│   ├── sessions/                 → /sessions/*
│   ├── giochi/                   → /giochi/*
│   └── board-game-ai/            → /board-game-ai/*
│
├── (chat)/                       ← ChatLayout
│   ├── layout.tsx
│   ├── chat/                     → /chat
│   └── shared/                   → /shared/chat
│
├── (auth)/                       ← AuthLayout
│   ├── layout.tsx
│   ├── login/                    → /login
│   ├── register/                 → /register
│   ├── reset-password/           → /reset-password
│   └── oauth-callback/           → /oauth-callback
│
├── admin/                        ← AdminLayout (pre-esistente)
│   ├── layout.tsx
│   └── ...                       → /admin/*
│
└── api/                          ← Nessun layout
    └── ...                       → /api/*
```

## 🔑 Caratteristiche Chiave

### URL Transparency

I route groups **non modificano le URL pubbliche**:

```bash
# FILE SYSTEM                    → URL PUBBLICA
(public)/games/page.tsx          → /games
(auth)/login/page.tsx            → /login
(chat)/chat/page.tsx             → /chat
```

Le parentesi `(groupName)` sono completamente invisibili agli utenti.

### Layout Auto-Application

Ogni route group ha un `layout.tsx` che applica automaticamente il layout corretto:

| Route Group | Layout Applicato | Caratteristiche |
|-------------|------------------|-----------------|
| **(public)** | `PublicLayout` | Header con navigation, Footer, Responsive container |
| **(chat)** | `ChatLayout` | Sidebar threads, Chat header, Mobile responsive |
| **(auth)** | `AuthLayout` | Minimal header, Centered card, Security notice |
| **admin** | `AdminLayout` | Admin sidebar, Metrics dashboard |

## 📋 Layout Implementation

### (public)/layout.tsx

```tsx
'use client';

import { ReactNode } from 'react';
import { PublicLayout } from '@/components/layouts/PublicLayout';
import { useCurrentUser } from '@/hooks/queries/useCurrentUser';
import { useRouter } from 'next/navigation';

export default function PublicRootLayout({ children }: { children: ReactNode }) {
  const { data: user, isLoading } = useCurrentUser();
  const router = useRouter();

  const handleLogout = async () => {
    const response = await fetch('/api/v1/auth/logout', {
      method: 'POST',
      credentials: 'include',
    });
    if (response.ok) {
      router.push('/login');
      router.refresh();
    }
  };

  if (isLoading) {
    return <div className="min-h-screen bg-background">{children}</div>;
  }

  return (
    <PublicLayout
      user={user ? { name: user.displayName || user.email, email: user.email } : undefined}
      onLogout={handleLogout}
      showNewsletter={false}
    >
      {children}
    </PublicLayout>
  );
}
```

**Caratteristiche**:
- ✅ TanStack Query (`useCurrentUser`) per auth state
- ✅ Loading state per evitare layout shift
- ✅ Logout handler integrato
- ✅ Type-safe user mapping

### (chat)/layout.tsx & (auth)/layout.tsx

```tsx
'use client';

import { ReactNode } from 'react';

export default function ChatRootLayout({ children }: { children: ReactNode }) {
  return <>{children}</>;
}
```

**Rationale**: ChatLayout e AuthLayout hanno props complesse (threads, games, titles) che variano per pagina. Vengono applicati **direttamente nei page components** invece che nel group layout.

## 🚀 Migration Guide

### Adding New Pages

#### Public Page (e.g., `/features`)

```bash
# 1. Create page in (public) group
apps/web/src/app/(public)/features/page.tsx

# 2. PublicLayout applied automatically
# URL: /features
```

#### Auth Page (e.g., `/verify-email`)

```bash
# 1. Create page in (auth) group
apps/web/src/app/(auth)/verify-email/page.tsx

# 2. Apply AuthLayout in page component
export default function VerifyEmailPage() {
  return (
    <AuthLayout title="Verify Email" subtitle="Check your inbox">
      <VerifyEmailForm />
    </AuthLayout>
  );
}

# URL: /verify-email
```

#### Chat Page (e.g., `/threads`)

```bash
# 1. Create page in (chat) group
apps/web/src/app/(chat)/threads/page.tsx

# 2. Apply ChatLayout in page component
export default function ThreadsPage() {
  return (
    <ChatLayout sidebarContent={<ThreadList />} game={selectedGame}>
      <MessageArea />
    </ChatLayout>
  );
}

# URL: /threads
```

## 🔍 Benefits

### Developer Experience

✅ **Visual Organization**: Chiara separazione tra public, auth, chat pages
✅ **Automatic Layout**: No need to wrap each page manually
✅ **Type Safety**: TypeScript enforces correct layout props
✅ **Git History**: `git mv` preserves file history during migration

### Performance

✅ **Code Splitting**: Each route group can lazy-load its layout
✅ **Shared State**: Layout state (user, sidebar) shared across group pages
✅ **Cache Optimization**: TanStack Query caches user data across group

### Maintainability

✅ **Single Source of Truth**: Layout logic centralized in group layout
✅ **Easy Refactoring**: Change layout = update one file
✅ **Clear Boundaries**: Auth vs Public vs Chat separation enforced by structure

## ⚠️ Important Notes

### URL Structure Unchanged

Route groups **DO NOT** create new URL segments:

```bash
✅ CORRECT:
(public)/games → /games

❌ WRONG (not how route groups work):
(public)/games → /(public)/games
```

### Nested Route Groups

You **cannot** nest route groups:

```bash
❌ INVALID:
(public)/(dashboard)/page.tsx

✅ VALID:
(public)/dashboard/page.tsx
```

### API Routes Unaffected

`/api` directory **is not** in a route group and remains unchanged:

```bash
apps/web/src/app/api/v1/[...path]/route.ts → /api/v1/*
```

## 📚 Related Documentation

- [Next.js Route Groups](https://nextjs.org/docs/app/building-your-application/routing/route-groups)
- [PublicLayout Component](../../apps/web/src/components/layouts/PublicLayout.tsx)
- [AuthLayout Component](../../apps/web/src/components/layouts/AuthLayout.tsx)
- [ChatLayout Component](../../apps/web/src/components/layouts/ChatLayout.tsx)

## 🧪 Testing

### TypeScript Validation

```bash
cd apps/web && pnpm typecheck
```

### Production Build

```bash
cd apps/web && pnpm build
```

### E2E Tests

```bash
cd apps/web && pnpm test:e2e
```

## 📝 Changelog

**v1.0 (2025-12-19)** - Initial route groups implementation (#2233)
- Created (public), (chat), (auth) route groups
- Migrated 13 page directories
- Implemented layout wrappers with TanStack Query integration
- Preserved git history with `git mv`
- Zero breaking changes (URL transparency verified)

---

**Last Updated**: 2025-12-19
**Maintainer**: Engineering Team
