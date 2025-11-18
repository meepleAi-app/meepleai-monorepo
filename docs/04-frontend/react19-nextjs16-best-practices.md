# React 19 + Next.js 16 - Best Practices & Patterns

**Source**: Migration from React 18 → 19, Next.js 15 → 16 (Issue #823, Completed 2025-11-10)
**Status**: Production-Ready Patterns
**Applies To**: All future frontend development

---

## React 19 Key Changes

### 1. New Compiler (React Forget)
**What**: Automatic memoization compiler
**Impact**: Reduces need for manual `useMemo`, `useCallback`, `React.memo`

**To-Be Pattern**:
```tsx
// ✅ PREFERRED: Let compiler optimize
function Component({ data }: Props) {
  const processed = expensiveOperation(data); // Compiler auto-memoizes
  return <div>{processed}</div>;
}

// ⚠️ LEGACY: Manual memoization (still works, but redundant)
function Component({ data }: Props) {
  const processed = useMemo(() => expensiveOperation(data), [data]);
  return <div>{processed}</div>;
}
```

**Guideline**: Use manual memoization ONLY for:
- Preventing expensive effects
- Maintaining referential equality for dependencies
- Compiler doesn't optimize (edge cases)

---

### 2. Actions (Unified async transitions)
**What**: New `useActionState` hook + Server Actions integration

**To-Be Pattern**:
```tsx
// ✅ Client-side actions
import { useActionState } from 'react';

function Form() {
  const [state, submitAction, isPending] = useActionState(async (prevState, formData) => {
    const response = await fetch('/api/submit', {
      method: 'POST',
      body: formData
    });
    return await response.json();
  }, initialState);

  return (
    <form action={submitAction}>
      <input name="title" />
      <button disabled={isPending}>
        {isPending ? 'Submitting...' : 'Submit'}
      </button>
      {state.error && <p>{state.error}</p>}
    </form>
  );
}
```

**Use Cases**:
- Form submissions with pending states
- Optimistic UI updates
- Server Actions integration (Next.js App Router)

---

### 3. `use()` Hook for Promises/Context
**What**: Read promises and context directly in render

**To-Be Pattern**:
```tsx
import { use } from 'react';

// ✅ Read promise directly (no useEffect needed)
function Component({ dataPromise }: { dataPromise: Promise<Data> }) {
  const data = use(dataPromise); // Suspends until resolved
  return <div>{data.title}</div>;
}

// ✅ Conditional context reading
function Component({ showAdmin }: Props) {
  const adminContext = showAdmin ? use(AdminContext) : null;
  return adminContext ? <AdminPanel /> : <UserPanel />;
}
```

**Benefits**:
- Simpler async data handling
- Works with Suspense
- Conditional context reading (previously impossible)

---

### 4. Document Metadata (Built-in)
**What**: Native metadata via App Router `export const metadata` (no `next/head`)

**To-Be Pattern (Next.js 16 App Router)**:
```tsx
import type { Metadata } from 'next';

export const metadata: Metadata = {
  title: 'MeepleAI · Chat',
  description: 'Ask AI to interpret rulebooks with citations.',
};

export default function Page() {
  return (
    <main className="...">
      <h1>Chat</h1>
      {/* Content */}
    </main>
  );
}

// ⚠️ LEGACY: next/head (still works in pages/, avoid in App Router)
```

**Guideline**: Define metadata via `export const metadata` or `generateMetadata` for App Router routes; only use `next/head` inside `pages/api` fallbacks.

---

## Next.js 16 Key Changes

### 1. Async Request APIs (`cookies()`, `headers()`, `params`, `searchParams`)
**What**: Request APIs now return Promises (breaking change)

**Migration Pattern**:
```tsx
// ❌ OLD (Next.js 15): Synchronous
import { cookies } from 'next/headers';

export default function Page() {
  const cookieStore = cookies(); // Synchronous
  const token = cookieStore.get('token');
  return <div>{token}</div>;
}

// ✅ NEW (Next.js 16): Async
import { cookies } from 'next/headers';

export default async function Page() {
  const cookieStore = await cookies(); // Now async!
  const token = cookieStore.get('token');
  return <div>{token}</div>;
}
```

**Affected Files** (22 files in MeepleAI):
- All admin pages using cookies
- Pages using dynamic params
- Middleware with headers

**Action**: Add `async` to component + `await` before `cookies()/headers()/params()`

---

### 2. Turbopack Stable (Default Dev Server)
**What**: Rust-based bundler replaces Webpack in dev

**Configuration** (`next.config.js`):
```js
// ✅ Enable Turbopack (default in Next.js 16)
module.exports = {
  experimental: {
    turbo: {
      // Turbopack config (rarely needed, good defaults)
    }
  }
}
```

**Benefits**:
- ~10x faster HMR (Hot Module Replacement)
- Faster initial compilation
- Lower memory usage

**Caveats**:
- Some Webpack plugins incompatible (check before migrating)
- Custom loaders may need rewrite

---

### 3. Partial Prerendering (PPR) - Experimental
**What**: Mix static + dynamic content in same page

**To-Be Pattern** (Future enhancement):
```tsx
import { Suspense } from 'react';

export default function Page() {
  return (
    <div>
      {/* Static shell (cached) */}
      <Header />

      {/* Dynamic content (streamed) */}
      <Suspense fallback={<Skeleton />}>
        <DynamicUserData />
      </Suspense>

      {/* Static footer (cached) */}
      <Footer />
    </div>
  );
}
```

**Status**: Experimental in Next.js 16, consider for v17
**Use Case**: Dashboard pages (static layout + dynamic metrics)

---

## Testing Best Practices (React 19 + Next.js 16)

### 1. Component Testing with React 19

**To-Be Pattern** (Jest + React Testing Library):
```tsx
import { render, screen, waitFor } from '@testing-library/react';
import { useActionState } from 'react';

test('Form submission with useActionState', async () => {
  render(<FormComponent />);

  const input = screen.getByLabelText('Title');
  const submit = screen.getByRole('button', { name: 'Submit' });

  await userEvent.type(input, 'Test Title');
  await userEvent.click(submit);

  // Wait for action to complete
  await waitFor(() => {
    expect(screen.getByText('Success')).toBeInTheDocument();
  });
});
```

**Key Changes**:
- `useActionState` testing: Wait for isPending → false
- `use()` hook: Mock promises properly
- Suspense: Use `waitFor` for suspended components

---

### 2. E2E Testing with Next.js 16

**To-Be Pattern** (Playwright):
```typescript
test('Admin page with async cookies', async ({ page }) => {
  await page.goto('/admin/users');

  // Next.js 16: Page is now async (cookies() awaited server-side)
  // No change needed in E2E test - still works!

  await expect(page.locator('h1')).toContainText('User Management');
});
```

**No Changes Needed**: E2E tests unchanged despite async cookies (server-side only)

---

### 3. Mock Service Worker (MSW) with React 19

**To-Be Pattern**:
```tsx
// msw v2 handlers (React 19 compatible)
import { http, HttpResponse } from 'msw';

export const handlers = [
  http.get('/api/users', () => {
    return HttpResponse.json({ users: [/* ... */] });
  }),

  http.post('/api/submit', async ({ request }) => {
    const body = await request.json();
    return HttpResponse.json({ success: true });
  })
];
```

**Upgrade**: MSW v1 → v2 required for React 19 (breaking changes in API)

---

## Package.json Best Practices

### Dependencies (Verified Working)

```json
{
  "dependencies": {
    "react": "^19.0.0",
    "react-dom": "^19.0.0",
    "next": "^16.0.0",

    // React 19 compatible versions
    "@tanstack/react-query": "^5.x",
    "@tiptap/react": "^2.10.0",
    "react-chessboard": "^5.0.0",

    // Next.js 16 compatible
    "@next/bundle-analyzer": "^16.0.0"
  },
  "devDependencies": {
    // Testing (React 19 compatible)
    "@testing-library/react": "^16.0.0",
    "@testing-library/jest-dom": "^6.5.0",
    "@testing-library/user-event": "^14.5.0",

    // Types
    "@types/react": "^19.0.0",
    "@types/react-dom": "^19.0.0"
  }
}
```

**Incompatible Packages** (Check before upgrade):
- Old state management (Redux < v5, MobX < v6)
- Old UI libraries (Material-UI < v6, Ant Design < v5)
- Deprecated Next.js plugins

---

## Common Pitfalls & Solutions

### Pitfall 1: Forgot `async` on Page Components
**Error**: `cookies() is not a function`
**Solution**: Add `async` to component + `await cookies()`

### Pitfall 2: useEffect Cleanup Timing
**Issue**: Cleanup runs differently in React 19 (strict mode double render)
**Solution**: Ensure cleanup is idempotent (can run multiple times)

### Pitfall 3: react-chessboard v4 → v5
**Breaking**: API changes in chessboard component
**Solution**: Update to v5.x (API simplified, better React 19 support)

---

## Future Enhancements (React 19 + Next.js 16)

### 1. App Router Hardening
**Status**: ✅ Migration complete (Issue #1077, Nov 2025)
**Next Steps**:
- Add regression tests for server-only modules (route handlers, streaming loaders)
- Audit cache tags + revalidation per route group
- Document server action patterns (upload workflow, admin prompt editor)

**Benefits**:
- Zero JS for static shells already live
- Streaming hydration for chat/upload
- Simplified provider tree via `AppProviders`

### 2. Suspense for Data Fetching
**Current**: React Query for async data
**Future**: Native Suspense with `use()` hook

**Pattern**:
```tsx
// Wrap async data fetching
<Suspense fallback={<Skeleton />}>
  <UserData promise={fetchUser()} />
</Suspense>
```

### 3. Partial Prerendering (PPR)
**When**: Next.js 17+ (currently experimental)
**Use Case**: Dashboard pages (static shell + dynamic data)

---

## Checklist for Future React/Next.js Work

### Starting New Component
- [ ] Use React 19 features (Actions, use() hook) where appropriate
- [ ] Avoid manual memoization (compiler handles it)
- [ ] Use native `<title>/<meta>` (no next/head)
- [ ] Write tests with React Testing Library v16+

### Creating New Page
- [ ] Mark as `async` if using cookies()/headers()/params()
- [ ] Await request APIs
- [ ] Consider Suspense boundaries for async data
- [ ] Test with Playwright (E2E)

### Reviewing PRs
- [ ] No unnecessary `useMemo`/`useCallback` (compiler optimizes)
- [ ] Async request APIs awaited correctly
- [ ] Tests cover React 19 features (Actions, use())
- [ ] No deprecated Next.js APIs

---

**Knowledge extracted from**:
- codebase-analysis-react19-nextjs16.md
- research_react19_migration_20251110.md
- research_nextjs16_migration_20251110_112519.md
- issue-823-implementation-complete.md

**Status**: Production-ready patterns for all future React 19 + Next.js 16 development
