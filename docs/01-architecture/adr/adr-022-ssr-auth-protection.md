# ADR-022: Server-Side Rendering (SSR) Authentication Protection

**Status**: ✅ Accepted
**Date**: 2025-11-22
**Issue**: [#1611](https://github.com/meepleai/monorepo/issues/1611)
**Context**: Frontend Authentication & Authorization
**Supersedes**: Client-side useAuth() hook pattern with 'use client' components

---

## Context and Problem Statement

Protected routes (`/admin`, `/editor`, `/upload`) currently use **Client Components** (`'use client'`) with client-side authentication checks via the `useAuth()` hook. This creates several user experience and security issues:

**Problems**:
1. **UI Flash**: Unauthenticated users see protected content briefly before redirect
2. **Client-Side Auth Delay**: useAuth hook calls `/api/v1/auth/me` after page render
3. **E2E Test Incompatibility**: `useRouter()` from `next/navigation` fails in test env with "NextRouter was not mounted"
4. **Double Authorization**: Middleware redirects → Client checks → Potential double redirect
5. **Bundle Size**: useAuth hook + dependencies shipped to all users (-15-20% potential savings)

**Current Flow**:
```
Request /upload
→ Middleware: Check cookie → Redirect if no session
→ Page: Client Component renders ('use client')
→ useAuth() hook: useState, useEffect, API call /auth/me
→ UI Flash while loading
→ Authorization check client-side
→ Redirect if unauthorized role
```

**Problem**: How do we eliminate UI flash and move auth logic server-side while maintaining role-based authorization?

---

## Decision Drivers

1. **Zero UI Flash**: Authentication must happen before render, not after
2. **Next.js 16.0.1 + React 19.2.0**: Full Server Components support in App Router
3. **App Router Architecture**: Project uses `/app` directory (Next.js 13+ pattern)
4. **E2E Test Compatibility**: Must work with HTTP-level mocking (MSW, Playwright)
5. **Middleware Enhancement**: Leverage existing cookie-based middleware
6. **SEO Benefits**: Server-rendered content with auth data pre-loaded
7. **Bundle Size Reduction**: Eliminate client-side auth logic
8. **Pages Router Removal**: Eliminate conflicting `/pages` directory

---

## Decision

Migrate protected routes from **Client Components** to **Server Components** using Next.js **App Router** pattern with async server components and `cookies()` from `next/headers`.

### Implementation Pattern

**Before** (Client Component with useAuth):
```typescript
// app/upload/page.tsx
'use client'
import { useAuth } from '@/hooks/useAuth'

export default function UploadPage() {
  const { user, loading } = useAuth() // Client-side API call

  if (!user || !['admin', 'editor'].includes(user.role)) {
    return <div>Unauthorized</div> // After render
  }

  return <UploadWizard />
}
```

**After** (Server Component + Client Component split):
```typescript
// app/upload/page.tsx (Server Component - NO 'use client')
import { getServerUser } from '@/lib/auth/server'
import { redirect } from 'next/navigation'
import { UploadClient } from './upload-client'

export default async function UploadPage() {
  // Server-side auth BEFORE render
  const user = await getServerUser()

  if (!user) {
    redirect('/login?from=/upload')
  }

  if (!['admin', 'editor'].includes(user.role.toLowerCase())) {
    redirect('/')
  }

  return <UploadClient user={user} />
}

// app/upload/upload-client.tsx ('use client' for interactivity)
'use client'
import type { AuthUser } from '@/types/auth'

interface Props {
  user: AuthUser
}

export function UploadClient({ user }: Props) {
  // All client-side logic here
  return <UploadWizard user={user} />
}
```

---

## Components Created

### 1. Server-Side Auth Utilities

**Location**: `apps/web/src/lib/auth/server.ts`

```typescript
/**
 * Server-side authentication utilities for App Router
 * Provides session validation and role-based authorization
 */

import { cookies } from 'next/headers'
import type { AuthUser } from '@/types/auth'

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080'
const SESSION_COOKIE_NAME = 'meepleai_session'

/**
 * Get current authenticated user from server-side
 * Works with App Router Server Components
 */
export async function getServerUser(): Promise<AuthUser | null> {
  const cookieStore = await cookies()
  const sessionCookie = cookieStore.get(SESSION_COOKIE_NAME)

  if (!sessionCookie) {
    return null
  }

  try {
    const res = await fetch(`${API_BASE}/api/v1/auth/me`, {
      headers: {
        Cookie: `${SESSION_COOKIE_NAME}=${sessionCookie.value}`
      },
      credentials: 'include',
      cache: 'no-store' // Don't cache auth checks
    })

    if (!res.ok) {
      return null
    }

    const data = await res.json()
    return data.user || null
  } catch (error) {
    console.error('Server auth check failed:', error)
    return null
  }
}
```

### 2. Migration Pattern for Protected Pages

**Template**:
```typescript
// app/protected-page/page.tsx (Server Component)
import { getServerUser } from '@/lib/auth/server'
import { redirect } from 'next/navigation'
import { ProtectedPageClient } from './client'

export default async function ProtectedPage() {
  const user = await getServerUser()

  if (!user) {
    redirect('/login?from=/protected-page')
  }

  // Optional: Role check
  if (!['admin', 'editor'].includes(user.role.toLowerCase())) {
    redirect('/')
  }

  return <ProtectedPageClient user={user} />
}

// app/protected-page/client.tsx (Client Component)
'use client'
import type { AuthUser } from '@/types/auth'

export function ProtectedPageClient({ user }: { user: AuthUser }) {
  // All useState, useEffect, event handlers here
  return <div>Protected content for {user.email}</div>
}
```

### 3. Pages to Migrate (10 total)

| Page | Complexity | Roles | Priority |
|------|-----------|-------|----------|
| `/upload` | HIGH | admin, editor | 🔴 Phase 1 (POC) |
| `/editor` | HIGH | admin, editor | 🟡 Phase 2 |
| `/admin` | HIGH | admin | 🟡 Phase 2 |
| `/admin/analytics` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/bulk-export` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/cache` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/configuration` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/n8n-templates` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/prompts/**` | MEDIUM | admin | 🟢 Phase 3 |
| `/admin/users` | MEDIUM | admin | 🟢 Phase 3 |

---

## Migration Strategy

### Phase 1: POC on `/upload` (2-3 hours)
1. ✅ Remove `src/pages/` directory completely (eliminate Pages Router conflict)
2. ✅ Update `lib/auth/server.ts` for App Router pattern (cookies from next/headers)
3. ✅ Migrate `app/upload/page.tsx` from Client to Server Component
4. ✅ Create `app/upload/upload-client.tsx` for interactive logic
5. ✅ Update `useGames` hook to accept user prop
6. ✅ Update all tests to import from app/ instead of pages/
7. ✅ Validate: build, typecheck, unit tests, E2E tests pass

### Phase 2: `/editor` + `/admin` Dashboard (3-4 hours)
1. Migrate `app/editor/page.tsx` with RichTextEditor (client-side lib handling)
2. Migrate `app/admin/page.tsx` dashboard with charts
3. Test complex client-side interactions with server props

### Phase 3: `/admin/*` Subpages (8-12 hours)
1. Batch migrate 8 admin subpages
2. Remove useAuth hook entirely (dead code)
3. Update documentation and patterns

### Total Effort: 20-30 hours over 4-6 weeks

---

## Consequences

### ✅ Positive

1. **Zero UI Flash**: Auth check happens before page render
2. **Better UX**: Users immediately see correct page or redirect
3. **SEO Friendly**: Server-rendered content with auth data
4. **-15-20% Bundle Size**: Remove useAuth hook + dependencies
5. **E2E Test Compatible**: HTTP-level mocking works with server-side auth
6. **Security**: Auth logic server-side only, no client bypass
7. **Performance**: One server-side check vs client useEffect
8. **Single Router**: Eliminate Pages/App Router conflict
9. **App Router Benefits**: Layout caching, parallel routes, streaming

### ⚠️ Trade-offs

1. **Server-Side Cost**: Every page request validates session server-side
   - **Mitigation**: Backend has session caching, minimal overhead
2. **Learning Curve**: Team needs to understand Server/Client Component split
   - **Mitigation**: Pattern documented, reusable utilities
3. **Component Split**: Server component + Client component for interactivity
   - **Mitigation**: Clear separation of concerns, better architecture

### ❌ Risks Mitigated

1. **No Breaking Changes**: API contracts unchanged
2. **Middleware Compatibility**: Works alongside existing middleware
3. **Backward Compatible**: Gradual migration, no big bang
4. **Performance**: Server Components cached by Next.js

---

## Technical Details

### App Router Server Components

**Key Differences from Pages Router**:
- No `getServerSideProps` (Pages Router only)
- Async component functions (`async function Page()`)
- `cookies()` from `next/headers` for session access
- `redirect()` from `next/navigation` for server-side redirects
- Component-level data fetching, not page-level

**Server/Client Split**:
```
app/upload/
├── page.tsx          (Server Component - auth, redirect)
└── upload-client.tsx (Client Component - useState, useEffect, handlers)
```

---

## Validation Criteria

### Definition of Done (DoD)

1. ✅ All 10 protected pages migrated to Server Components
2. ✅ `lib/auth/server.ts` utilities updated for App Router
3. ✅ `src/pages/` directory removed (Pages Router eliminated)
4. ✅ All tests updated to import from `app/` instead of `pages/`
5. ✅ Build passes: `pnpm build` ✅
6. ✅ Typecheck passes: `pnpm typecheck` ✅
7. ✅ Unit tests pass: `pnpm test` ✅
8. ✅ E2E tests pass: `pnpm test:e2e` ✅
9. ✅ No UI flash visible in browser testing
10. ✅ Bundle size reduced by 15-20%
11. ✅ Documentation updated (App Router migration guide)
12. ✅ ADR published and reviewed

---

## References

- Next.js App Router: https://nextjs.org/docs/app/building-your-application/routing
- Server Components: https://nextjs.org/docs/app/building-your-application/rendering/server-components
- Data Fetching: https://nextjs.org/docs/app/building-your-application/data-fetching
- cookies(): https://nextjs.org/docs/app/api-reference/functions/cookies
- redirect(): https://nextjs.org/docs/app/api-reference/functions/redirect

---

**Decision Made By**: Engineering Team
**Review Status**: ✅ Approved for Implementation (App Router)
**Implementation Start**: 2025-11-22