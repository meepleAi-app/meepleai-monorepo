# Frontend Authorization Protection Gap

**Created**: 2025-11-22
**Priority**: 🟡 MEDIUM
**Discovered By**: E2E RBAC Tests (Issue #1490)
**Category**: Security & Frontend Architecture

---

## 📋 Problem Description

E2E RBAC tests rivelano che alcune route frontend **non hanno protezione client-side**. Backend RBAC funziona correttamente (403 Forbidden), ma frontend permette accesso alle pagine senza redirect/protezione.

**Gap Critici Identificati**:
1. ❌ `/editor` accessibile da User role (dovrebbe essere Editor+)
2. ❌ `/upload` accessibile da User role (dovrebbe essere Editor+)
3. ❌ `/admin/**` accessibile senza autenticazione (dovrebbe redirect a /login)
4. ❌ `/editor` accessibile senza autenticazione (dovrebbe redirect a /login)

---

## 🧪 Test Evidence

```typescript
// Test fallito: User può accedere /editor (dovrebbe essere forbidden)
test('User is forbidden from /editor', async ({ page }) => {
  await setupMockAuthWithForbidden(page, 'User', ['/editor/**']);
  await page.goto('/editor');

  // Expected: redirect o 403
  // Actual: page loads normally ❌
});

// Test fallito: Unauthenticated può accedere /admin
test('Unauthenticated user redirected from /admin', async ({ page }) => {
  // No auth setup
  await page.goto('/admin');

  // Expected: redirect to /login
  // Actual: page loads (poi API 401, ma UX poor) ❌
});
```

**Test Results**: 24/30 passed (80%)
- ✅ Admin-only verification: 9/12 passed (75%)
- ✅ Editor+ verification: 4/6 passed (67%)
- ✅ 403 Forbidden backend: 100% (backend RBAC works)
- ❌ Frontend protection: Missing

---

## 🎯 Root Cause

**Backend**: ✅ RBAC perfetto con `RequireAdminSession()`, `RequireAdminOrEditorSession()`
**Frontend**: ❌ Nessuna route protection middleware/HOC

```typescript
// apps/web/src/app/admin/page.tsx
// ❌ Missing: Role check, redirect logic
export default function AdminDashboard() {
  // Loads page, THEN API calls fail with 403
  // User sees flash of UI before error
}

// ✅ Dovrebbe essere:
export default function AdminDashboard() {
  const { user } = useAuth();

  if (!user || user.role !== 'Admin') {
    redirect('/login?unauthorized=true');
  }

  // ... rest of component
}
```

---

## ✅ Solution Recommendations

### Option 1: Middleware Protection (RECOMMENDED)
**File**: `apps/web/src/middleware.ts`

```typescript
import { NextResponse } from 'next/server';
import type { NextRequest } from 'next/server';

export function middleware(request: NextRequest) {
  const session = request.cookies.get('session-cookie-name');
  const path = request.nextUrl.pathname;

  // Protected routes
  const adminRoutes = ['/admin'];
  const editorRoutes = ['/editor', '/upload'];
  const protectedRoutes = ['/chat', '/settings'];

  // No session → redirect to login for protected routes
  if (!session && [...adminRoutes, ...editorRoutes, ...protectedRoutes].some(r => path.startsWith(r))) {
    return NextResponse.redirect(new URL('/login?unauthorized=true', request.url));
  }

  // TODO: Parse session to check role, redirect if insufficient

  return NextResponse.next();
}

export const config = {
  matcher: ['/admin/:path*', '/editor/:path*', '/upload/:path*', '/chat/:path*', '/settings/:path*']
};
```

**Pros**: Centralizzato, runs prima del render, migliori performance
**Cons**: Richiede session parsing (attualmente in HttpOnly cookie)

---

### Option 2: HOC/Layout Protection
**File**: `apps/web/src/app/admin/layout.tsx`

```typescript
'use client';

import { useAuth } from '@/hooks/useAuth';
import { useRouter } from 'next/navigation';
import { useEffect } from 'react';

export default function AdminLayout({ children }: { children: React.Node }) {
  const { user, loading } = useAuth();
  const router = useRouter();

  useEffect(() => {
    if (!loading && (!user || user.role !== 'Admin')) {
      router.replace('/login?unauthorized=true');
    }
  }, [user, loading, router]);

  if (loading) return <div>Loading...</div>;
  if (!user || user.role !== 'Admin') return null;

  return <>{children}</>;
}
```

**Pros**: Più semplice, no middleware parsing
**Cons**: Flash of content possibile, più codice ripetuto

---

### Option 3: Server Component Protection (BEST FOR APP ROUTER)
**File**: `apps/web/src/app/admin/page.tsx`

```typescript
import { cookies } from 'next/headers';
import { redirect } from 'next/navigation';

async function getSession() {
  const cookieStore = cookies();
  const sessionCookie = cookieStore.get('session-cookie-name');

  if (!sessionCookie) return null;

  // Verify session with API call
  const res = await fetch(`${process.env.NEXT_PUBLIC_API_BASE}/api/v1/auth/me`, {
    headers: { Cookie: `session-cookie-name=${sessionCookie.value}` }
  });

  if (!res.ok) return null;
  return await res.json();
}

export default async function AdminDashboard() {
  const session = await getSession();

  if (!session || session.user.role !== 'Admin') {
    redirect('/login?unauthorized=true');
  }

  return <AdminDashboardClient user={session.user} />;
}
```

**Pros**: No flash, server-side protection, App Router native
**Cons**: Più complesso, richiede async components

---

## 📝 Implementation Checklist

### Phase 1: Quick Win - Layout Protection
- [ ] Create `apps/web/src/app/admin/layout.tsx` with role check
- [ ] Create `apps/web/src/app/editor/layout.tsx` with Editor+ check
- [ ] Create `apps/web/src/app/upload/layout.tsx` with Editor+ check
- [ ] Test E2E RBAC tests pass

### Phase 2: Middleware Enhancement
- [ ] Implement `middleware.ts` with session awareness
- [ ] Add role-based route protection
- [ ] Test performance impact
- [ ] Update E2E tests for redirect behavior

### Phase 3: Server Component Migration
- [ ] Migrate admin pages to server components with session checks
- [ ] Migrate editor/upload pages similarly
- [ ] Remove client-side auth checks (now server-side)
- [ ] Full E2E test suite validation

---

## 🔐 Security Impact

**Current State**:
- 🟡 **Medium Risk**: User può vedere UI flash di admin pages
- 🔴 **Low-Medium**: API 403 previene data breach, ma UX scadente
- ✅ **Backend Protected**: Nessun data leak (API 403 works)

**After Fix**:
- ✅ **No UI Flash**: Immediate redirect prima del render
- ✅ **Better UX**: Clear unauthorized messaging
- ✅ **Defense in Depth**: Frontend + Backend protection

---

## 📊 Test Results Comparison

| Test Scenario | Current | After Fix |
|---------------|---------|-----------|
| Admin can access /admin | ✅ Pass | ✅ Pass |
| User forbidden from /admin | ✅ Pass (API 403) | ✅ Pass (redirect) |
| User forbidden from /editor | ❌ **FAIL** | ✅ Pass (redirect) |
| User forbidden from /upload | ❌ **FAIL** | ✅ Pass (redirect) |
| Unauth redirected from /admin | ❌ **FAIL** | ✅ Pass (redirect) |
| Unauth redirected from /editor | ❌ **FAIL** | ✅ Pass (redirect) |

**Target**: 30/30 (100%) after frontend protection implemented

---

## 🔗 Related Issues

- **Issue #1490**: RBAC E2E Tests (completed, revealed this gap)
- **Issue #1608**: 🛡️ Implement Frontend Route Protection (created 2025-11-22)

---

## ⚠️ Non-Blocking for Production

Questo gap NON è un security vulnerability critico perché:
1. ✅ Backend API completamente protetto con RBAC
2. ✅ Nessun data leak possibile (API returns 403)
3. ⚠️ Solo UX degradata (user vede UI flash + errore)

**Recommendation**: Medium priority fix per migliorare UX, non urgent security patch.

---

**Status**: 🟡 IDENTIFIED - Awaiting Implementation
**Owner**: Frontend Team
**Effort**: 4-6 hours (Layout approach) | 8-12 hours (Middleware approach)
