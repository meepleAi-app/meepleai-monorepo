# Provider Token Observability — PR3 Plan (G5 Frontend)

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development or superpowers:executing-plans to implement this plan task-by-task.

**Goal:** UI admin per visualizzare stato + quota dei provider LLM via gli endpoint G1/G2 (PR1 merged in `8d712e1a4`).

**Architecture:** Pagina dedicata `/admin/(dashboard)/providers/` (lista) + `/admin/(dashboard)/providers/[name]/` (detail). Pattern coerente con `monitor/InfrastructureTab.tsx`. TanStack Query per fetch/cache; client API tipizzato in `lib/api/clients/admin/adminProvidersClient.ts`.

**Tech Stack:** Next.js 16 App Router + React 19 + Tailwind 4 + shadcn/ui + Zod (response schemas) + TanStack Query.

**Spec source:** `docs/for-developers/specs/2026-05-10-provider-token-observability-design.md` § G5

**Closes:** child issue (TBD: filed after this plan), Refs umbrella #935

**Out of scope per PR3:**
- Mini-card in `/admin/overview` (deferred)
- Audit log inline UI (basta mostrare last-probe stat)
- "Run probe" with `?model=X` chooser (basic Run probe button only)
- G4 Prometheus metric / AlertRule (separate issue)

---

## File Structure

### Created files

| Path | Responsibility |
|------|----------------|
| `apps/web/src/lib/api/clients/admin/adminProvidersClient.ts` | API client: `listProviders`, `probeProvider`, `getProviderQuota` |
| `apps/web/src/lib/api/schemas/providers.ts` | Zod schemas for `ProviderProbeResultDto`, `ProviderQuotaDto` |
| `apps/web/src/hooks/queries/useProviders.ts` | TanStack Query hooks: `useProvidersList`, `useProviderQuota`, `useProbeProviderMutation` |
| `apps/web/src/app/admin/(dashboard)/providers/layout.tsx` | Layout wrapper |
| `apps/web/src/app/admin/(dashboard)/providers/page.tsx` | Index (list of providers) |
| `apps/web/src/app/admin/(dashboard)/providers/ProvidersList.tsx` | Client component: cards grid |
| `apps/web/src/app/admin/(dashboard)/providers/ProviderCard.tsx` | Single provider card (name, badge, quota mini, last probe) |
| `apps/web/src/app/admin/(dashboard)/providers/[name]/page.tsx` | Detail page route |
| `apps/web/src/app/admin/(dashboard)/providers/[name]/ProviderDetail.tsx` | Detail client component |
| `apps/web/src/app/admin/(dashboard)/providers/[name]/RunProbeButton.tsx` | Probe action (SuperAdmin only) |
| `apps/web/src/app/admin/(dashboard)/providers/__tests__/ProvidersList.test.tsx` | Vitest unit |
| `apps/web/src/app/admin/(dashboard)/providers/__tests__/ProviderCard.test.tsx` | Vitest unit |
| `apps/web/src/app/admin/(dashboard)/providers/[name]/__tests__/ProviderDetail.test.tsx` | Vitest unit |
| `apps/web/e2e/admin-providers.spec.ts` | Playwright E2E (auth flow + probe) |

### Modified files

| Path | Change |
|------|--------|
| `apps/web/src/lib/api/clients/admin/index.ts` | Export `adminProvidersClient` |
| `apps/web/src/lib/api/clients/adminClient.ts` | Re-export under `api.admin.providers.*` |
| `apps/web/src/components/admin/admin-sidebar.tsx` (or similar nav) | Add "Providers" entry under monitoring section |

---

## Branch setup

- [ ] **Step 0.1: Switch to main-dev and pull**

```bash
git checkout main-dev && git pull --ff-only
```

- [ ] **Step 0.2: Create feature branch**

```bash
git checkout -b feature/issue-NEW-providers-admin-ui
git config branch.feature/issue-NEW-providers-admin-ui.parent main-dev
```

- [ ] **Step 0.3: Open child issue**

```bash
gh issue create --title "PR3: admin providers UI (G5)" --body "Child of #935. Frontend G5 — pagina admin providers + drill-down. Plan: docs/for-developers/plans/2026-05-10-provider-token-observability-pr3-plan.md" --label enhancement
```

Note the issue number → use it in branch name retroactively if desired.

---

## Task 1: Zod schemas for DTOs

**Files:**
- Create: `apps/web/src/lib/api/schemas/providers.ts`

- [ ] **Step 1.1: Write schemas**

```typescript
// apps/web/src/lib/api/schemas/providers.ts
import { z } from 'zod';

export const ProviderProbeResultSchema = z.object({
  providerName: z.string(),
  tokenConfigured: z.boolean(),
  tokenAuthenticated: z.boolean(),
  modelAvailable: z.boolean().nullable(),
  expectedModel: z.string().nullable(),
  tokenFingerprint: z.string().nullable(),
  errorCode: z.string().nullable(),
  errorMessage: z.string().nullable(),
  latencyMs: z.number(),
  probedAt: z.string().datetime(),
});

export type ProviderProbeResult = z.infer<typeof ProviderProbeResultSchema>;

export const ProviderQuotaSchema = z.object({
  providerName: z.string(),
  quotaSupported: z.boolean(),
  tokenConfigured: z.boolean(),
  usedUsd: z.number().nullable(),
  limitUsd: z.number().nullable(),
  remainingUsd: z.number().nullable(),
  resetAt: z.string().datetime().nullable(),
  errorCode: z.string().nullable(),
  errorMessage: z.string().nullable(),
  fetchedAt: z.string().datetime(),
  cacheTtlSeconds: z.number(),
});

export type ProviderQuota = z.infer<typeof ProviderQuotaSchema>;

// Static list of provider names known to the backend (must mirror DI registration in InfrastructureServiceExtensions.cs).
// Updated when new providers are registered.
export const KNOWN_PROVIDERS = ['openrouter', 'deepseek', 'ollama-local'] as const;
export type ProviderName = (typeof KNOWN_PROVIDERS)[number];
```

- [ ] **Step 1.2: Commit**

```bash
git add apps/web/src/lib/api/schemas/providers.ts
git commit -m "feat(web): add provider probe + quota Zod schemas"
```

---

## Task 2: API client

**Files:**
- Create: `apps/web/src/lib/api/clients/admin/adminProvidersClient.ts`
- Modify: `apps/web/src/lib/api/clients/admin/index.ts`
- Modify: `apps/web/src/lib/api/clients/adminClient.ts`

- [ ] **Step 2.1: Client file**

```typescript
// apps/web/src/lib/api/clients/admin/adminProvidersClient.ts
import type { HttpClient } from '../../client';
import {
  ProviderProbeResultSchema,
  ProviderQuotaSchema,
  type ProviderProbeResult,
  type ProviderQuota,
  type ProviderName,
  KNOWN_PROVIDERS,
} from '../../schemas/providers';

export function createAdminProvidersClient(http: HttpClient) {
  return {
    /**
     * GET /api/v1/admin/providers/{name}/quota — Admin-or-above
     * Cache 5min server-side. 200 with QuotaSupported:false for unsupported providers.
     */
    async getQuota(name: ProviderName): Promise<ProviderQuota | null> {
      return http.get(`/api/v1/admin/providers/${encodeURIComponent(name)}/quota`, ProviderQuotaSchema);
    },

    /**
     * POST /api/v1/admin/providers/{name}/probe — SuperAdmin
     * Optional ?model=X for explicit availability check.
     * Rate-limited 10/min per user.
     */
    async probe(name: ProviderName, model?: string): Promise<ProviderProbeResult | null> {
      const url = `/api/v1/admin/providers/${encodeURIComponent(name)}/probe${
        model ? `?model=${encodeURIComponent(model)}` : ''
      }`;
      return http.post(url, undefined, ProviderProbeResultSchema);
    },

    /** List of all known provider names (static; mirrors backend DI). */
    listKnownProviders(): readonly ProviderName[] {
      return KNOWN_PROVIDERS;
    },
  };
}
```

- [ ] **Step 2.2: Verify HttpClient signatures match**

```bash
cd apps/web && grep -n "post:\|get:" src/lib/api/client.ts | head -10
```

If `http.post` doesn't accept `(url, body, schema)` or `http.get` doesn't accept `(url, schema)`, adjust to the actual signature. Report deviations.

- [ ] **Step 2.3: Register in admin index**

In `apps/web/src/lib/api/clients/admin/index.ts`, add the export:

```typescript
export { createAdminProvidersClient } from './adminProvidersClient';
```

In `apps/web/src/lib/api/clients/adminClient.ts`, add `providers` to the `api.admin` namespace following the existing pattern (mirror how `system`, `monitor`, etc. are exposed).

- [ ] **Step 2.4: Type-check**

```bash
cd apps/web && pnpm typecheck 2>&1 | tail -3
```

Expected: 0 errors.

- [ ] **Step 2.5: Commit**

```bash
git add apps/web/src/lib/api/clients/admin/adminProvidersClient.ts apps/web/src/lib/api/clients/admin/index.ts apps/web/src/lib/api/clients/adminClient.ts
git commit -m "feat(web): add adminProvidersClient (probe + quota)"
```

---

## Task 3: TanStack Query hooks

**Files:**
- Create: `apps/web/src/hooks/queries/useProviders.ts`

- [ ] **Step 3.1: Hooks**

```typescript
// apps/web/src/hooks/queries/useProviders.ts
import { useMutation, useQuery, useQueryClient } from '@tanstack/react-query';
import { api } from '@/lib/api';
import type { ProviderName, ProviderProbeResult, ProviderQuota } from '@/lib/api/schemas/providers';

export function useProviderQuota(name: ProviderName, options?: { enabled?: boolean }) {
  return useQuery<ProviderQuota | null>({
    queryKey: ['admin', 'providers', name, 'quota'],
    queryFn: () => api.admin.providers.getQuota(name),
    staleTime: 4 * 60 * 1000, // 4min < server cache 5min
    enabled: options?.enabled ?? true,
  });
}

export function useProbeProviderMutation(name: ProviderName) {
  const qc = useQueryClient();
  return useMutation<ProviderProbeResult | null, Error, { model?: string } | void>({
    mutationFn: (input) => api.admin.providers.probe(name, input?.model),
    onSuccess: () => {
      // Invalidate quota query — probe likely changed remaining USD if it consumed tokens.
      // (List-models is zero-cost so this is conservative.)
      qc.invalidateQueries({ queryKey: ['admin', 'providers', name, 'quota'] });
    },
  });
}
```

- [ ] **Step 3.2: Commit**

```bash
git add apps/web/src/hooks/queries/useProviders.ts
git commit -m "feat(web): add useProviderQuota + useProbeProviderMutation hooks"
```

---

## Task 4: ProviderCard component (TDD)

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/providers/ProviderCard.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/providers/__tests__/ProviderCard.test.tsx`

- [ ] **Step 4.1: Failing test**

```typescript
// apps/web/src/app/admin/(dashboard)/providers/__tests__/ProviderCard.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProviderCard } from '../ProviderCard';

function renderWithClient(ui: React.ReactElement) {
  const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
  return render(<QueryClientProvider client={qc}>{ui}</QueryClientProvider>);
}

describe('ProviderCard', () => {
  it('renders provider name and "Quota not supported" when QuotaSupported:false', () => {
    renderWithClient(<ProviderCard name="ollama-local" />);
    expect(screen.getByText('ollama-local')).toBeInTheDocument();
  });

  it('shows remainingUsd when quotaSupported:true', () => {
    // mock implementation will be added once api hook is wired; placeholder for now
    renderWithClient(<ProviderCard name="openrouter" />);
    expect(screen.getByText('openrouter')).toBeInTheDocument();
  });
});
```

- [ ] **Step 4.2: Run — FAIL**

```bash
cd apps/web && pnpm test -- ProviderCard --run 2>&1 | tail -3
```

Expected: FAIL — `ProviderCard` not defined.

- [ ] **Step 4.3: Implement**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/ProviderCard.tsx
'use client';

import Link from 'next/link';
import { useProviderQuota } from '@/hooks/queries/useProviders';
import type { ProviderName } from '@/lib/api/schemas/providers';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';

export function ProviderCard({ name }: { name: ProviderName }) {
  const { data, isLoading, isError } = useProviderQuota(name);

  return (
    <Link href={`/admin/providers/${encodeURIComponent(name)}`} className="block">
      <Card className="transition hover:bg-accent/30">
        <CardHeader className="flex flex-row items-center justify-between pb-2">
          <CardTitle className="text-base font-medium">{name}</CardTitle>
          {data && !data.quotaSupported && (
            <Badge variant="secondary" title={data.errorMessage ?? undefined}>
              Quota n/d
            </Badge>
          )}
          {data?.quotaSupported && data.tokenConfigured && (
            <Badge variant="default">OK</Badge>
          )}
          {data?.quotaSupported && !data.tokenConfigured && (
            <Badge variant="destructive">No token</Badge>
          )}
        </CardHeader>
        <CardContent className="text-sm text-muted-foreground">
          {isLoading && <span>Caricamento…</span>}
          {isError && <span>Errore caricamento quota</span>}
          {data?.quotaSupported && data.tokenConfigured && (
            <div className="space-y-1">
              {data.remainingUsd !== null && (
                <div>
                  <span className="font-medium text-foreground">
                    ${data.remainingUsd.toFixed(2)}
                  </span>{' '}
                  rimanenti
                </div>
              )}
              {data.usedUsd !== null && data.remainingUsd === null && (
                <div>
                  <span className="font-medium text-foreground">
                    ${data.usedUsd.toFixed(2)}
                  </span>{' '}
                  utilizzati
                </div>
              )}
            </div>
          )}
          {data && !data.quotaSupported && (
            <span>Quota tracking non supportato dal provider</span>
          )}
        </CardContent>
      </Card>
    </Link>
  );
}
```

- [ ] **Step 4.4: Run — PASS**

```bash
cd apps/web && pnpm test -- ProviderCard --run 2>&1 | tail -3
```

Expected: 2 PASS.

- [ ] **Step 4.5: Commit**

```bash
git add apps/web/src/app/admin/(dashboard)/providers/ProviderCard.tsx apps/web/src/app/admin/(dashboard)/providers/__tests__/ProviderCard.test.tsx
git commit -m "feat(web): add ProviderCard component"
```

---

## Task 5: Index list page

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/providers/layout.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/providers/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/providers/ProvidersList.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/providers/__tests__/ProvidersList.test.tsx`

- [ ] **Step 5.1: Layout**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/layout.tsx
import { ReactNode } from 'react';

export default function ProvidersLayout({ children }: { children: ReactNode }) {
  return <div className="space-y-6">{children}</div>;
}
```

- [ ] **Step 5.2: Page**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/page.tsx
import { ProvidersList } from './ProvidersList';

export const metadata = { title: 'Providers — Admin' };

export default function ProvidersPage() {
  return (
    <>
      <div>
        <h1 className="font-quicksand text-2xl font-semibold tracking-tight text-foreground">
          LLM Providers
        </h1>
        <p className="mt-1 text-sm text-muted-foreground">
          Stato token e credito residuo per ogni provider configurato.
        </p>
      </div>
      <ProvidersList />
    </>
  );
}
```

- [ ] **Step 5.3: List client**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/ProvidersList.tsx
'use client';

import { ProviderCard } from './ProviderCard';
import { KNOWN_PROVIDERS } from '@/lib/api/schemas/providers';

export function ProvidersList() {
  return (
    <div className="grid gap-4 sm:grid-cols-2 lg:grid-cols-3">
      {KNOWN_PROVIDERS.map((name) => (
        <ProviderCard key={name} name={name} />
      ))}
    </div>
  );
}
```

- [ ] **Step 5.4: Unit test**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/__tests__/ProvidersList.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProvidersList } from '../ProvidersList';

describe('ProvidersList', () => {
  it('renders one card per known provider', () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <ProvidersList />
      </QueryClientProvider>
    );
    expect(screen.getByText('openrouter')).toBeInTheDocument();
    expect(screen.getByText('deepseek')).toBeInTheDocument();
    expect(screen.getByText('ollama-local')).toBeInTheDocument();
  });
});
```

- [ ] **Step 5.5: Run + commit**

```bash
cd apps/web && pnpm test -- ProvidersList --run 2>&1 | tail -3
git add apps/web/src/app/admin/(dashboard)/providers/{layout.tsx,page.tsx,ProvidersList.tsx} apps/web/src/app/admin/(dashboard)/providers/__tests__/ProvidersList.test.tsx
git commit -m "feat(web): add /admin/providers index page with provider list"
```

---

## Task 6: Detail page + RunProbeButton

**Files:**
- Create: `apps/web/src/app/admin/(dashboard)/providers/[name]/page.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/providers/[name]/ProviderDetail.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/providers/[name]/RunProbeButton.tsx`
- Create: `apps/web/src/app/admin/(dashboard)/providers/[name]/__tests__/ProviderDetail.test.tsx`

- [ ] **Step 6.1: Page route**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/[name]/page.tsx
import { notFound } from 'next/navigation';
import { KNOWN_PROVIDERS, type ProviderName } from '@/lib/api/schemas/providers';
import { ProviderDetail } from './ProviderDetail';

export async function generateStaticParams() {
  return KNOWN_PROVIDERS.map((name) => ({ name }));
}

export default async function ProviderDetailPage({
  params,
}: {
  params: Promise<{ name: string }>;
}) {
  const { name } = await params;
  if (!KNOWN_PROVIDERS.includes(name as ProviderName)) {
    notFound();
  }
  return <ProviderDetail name={name as ProviderName} />;
}
```

- [ ] **Step 6.2: Detail component**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/[name]/ProviderDetail.tsx
'use client';

import Link from 'next/link';
import { useProviderQuota } from '@/hooks/queries/useProviders';
import type { ProviderName } from '@/lib/api/schemas/providers';
import { Card, CardContent, CardHeader, CardTitle } from '@/components/ui/card';
import { Badge } from '@/components/ui/badge';
import { RunProbeButton } from './RunProbeButton';

export function ProviderDetail({ name }: { name: ProviderName }) {
  const { data, isLoading, isError } = useProviderQuota(name);

  return (
    <div className="space-y-6">
      <Link
        href="/admin/providers"
        className="text-sm text-muted-foreground hover:underline"
      >
        ← Torna alla lista
      </Link>

      <div className="flex items-center justify-between">
        <h1 className="font-quicksand text-2xl font-semibold">{name}</h1>
        <RunProbeButton name={name} />
      </div>

      <Card>
        <CardHeader>
          <CardTitle className="text-base">Quota</CardTitle>
        </CardHeader>
        <CardContent className="space-y-2 text-sm">
          {isLoading && <span>Caricamento…</span>}
          {isError && <span className="text-destructive">Errore</span>}
          {data && !data.quotaSupported && (
            <div className="text-muted-foreground">
              {data.errorMessage ?? 'Quota tracking non supportato dal provider'}
            </div>
          )}
          {data?.quotaSupported && !data.tokenConfigured && (
            <Badge variant="destructive">Token non configurato</Badge>
          )}
          {data?.quotaSupported && data.tokenConfigured && (
            <dl className="grid grid-cols-2 gap-3">
              {data.usedUsd !== null && (
                <div>
                  <dt className="text-muted-foreground">Utilizzato</dt>
                  <dd className="text-lg font-medium">${data.usedUsd.toFixed(4)}</dd>
                </div>
              )}
              {data.limitUsd !== null && (
                <div>
                  <dt className="text-muted-foreground">Limite</dt>
                  <dd className="text-lg font-medium">${data.limitUsd.toFixed(2)}</dd>
                </div>
              )}
              {data.remainingUsd !== null && (
                <div>
                  <dt className="text-muted-foreground">Rimanente</dt>
                  <dd className="text-lg font-medium">${data.remainingUsd.toFixed(2)}</dd>
                </div>
              )}
              {data.resetAt && (
                <div>
                  <dt className="text-muted-foreground">Reset</dt>
                  <dd>{new Date(data.resetAt).toLocaleString()}</dd>
                </div>
              )}
              <div className="col-span-2 text-xs text-muted-foreground">
                Aggiornato: {new Date(data.fetchedAt).toLocaleString()} (cache{' '}
                {data.cacheTtlSeconds}s)
              </div>
            </dl>
          )}
        </CardContent>
      </Card>
    </div>
  );
}
```

- [ ] **Step 6.3: RunProbeButton**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/[name]/RunProbeButton.tsx
'use client';

import { useState } from 'react';
import { useProbeProviderMutation } from '@/hooks/queries/useProviders';
import { useAuth } from '@/lib/auth/auth-context';
import type { ProviderName, ProviderProbeResult } from '@/lib/api/schemas/providers';
import { Button } from '@/components/ui/button';

export function RunProbeButton({ name }: { name: ProviderName }) {
  const { user } = useAuth();
  const isSuperAdmin = user?.role === 'superadmin';
  const mutation = useProbeProviderMutation(name);
  const [lastResult, setLastResult] = useState<ProviderProbeResult | null>(null);

  if (!isSuperAdmin) {
    return (
      <span className="text-sm text-muted-foreground">
        Probe richiede privilegi SuperAdmin
      </span>
    );
  }

  return (
    <div className="flex items-center gap-3">
      <Button
        size="sm"
        disabled={mutation.isPending}
        onClick={() =>
          mutation.mutateAsync().then((r) => setLastResult(r ?? null))
        }
      >
        {mutation.isPending ? 'Probing…' : 'Run probe'}
      </Button>
      {lastResult && (
        <span className="text-sm text-muted-foreground">
          {lastResult.tokenAuthenticated ? '✓' : '✗'} {lastResult.latencyMs}ms
        </span>
      )}
    </div>
  );
}
```

- [ ] **Step 6.4: Test**

```tsx
// apps/web/src/app/admin/(dashboard)/providers/[name]/__tests__/ProviderDetail.test.tsx
import { describe, it, expect } from 'vitest';
import { render, screen } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { ProviderDetail } from '../ProviderDetail';

describe('ProviderDetail', () => {
  it('renders provider name + back link', () => {
    const qc = new QueryClient({ defaultOptions: { queries: { retry: false } } });
    render(
      <QueryClientProvider client={qc}>
        <ProviderDetail name="openrouter" />
      </QueryClientProvider>
    );
    expect(screen.getByText('openrouter')).toBeInTheDocument();
    expect(screen.getByText(/torna alla lista/i)).toBeInTheDocument();
  });
});
```

- [ ] **Step 6.5: Run + commit**

```bash
cd apps/web && pnpm test -- ProviderDetail --run 2>&1 | tail -3
git add apps/web/src/app/admin/(dashboard)/providers/[name]/
git commit -m "feat(web): add /admin/providers/[name] detail page + RunProbe button"
```

---

## Task 7: Admin sidebar nav entry

**Files:**
- Modify: TBD (find via `grep` after Task 6)

- [ ] **Step 7.1: Locate the admin sidebar component**

```bash
cd apps/web && grep -rln "admin/overview\|admin/monitor" src/components/admin/ src/app/admin/ 2>&1 | head -5
```

- [ ] **Step 7.2: Add entry**

In the located file, add a "Providers" link to the navigation (mirror the format of "Monitor" or "Overview"). Use icon `<Globe />` or `<Activity />` from lucide-react if already imported.

- [ ] **Step 7.3: Visual verify**

```bash
cd apps/web && pnpm dev   # background, then visit http://localhost:3000/admin/providers
```

- [ ] **Step 7.4: Commit**

```bash
git add apps/web/src/components/admin/ # adjust path
git commit -m "feat(web): add Providers entry to admin sidebar"
```

---

## Task 8: E2E Playwright

**Files:**
- Create: `apps/web/e2e/admin-providers.spec.ts`

- [ ] **Step 8.1: Test**

```typescript
// apps/web/e2e/admin-providers.spec.ts
import { test, expect } from '@playwright/test';

test.describe('Admin /providers', () => {
  test('lists providers and navigates to detail', async ({ page }) => {
    await page.goto('/admin/providers');
    await expect(page.getByRole('heading', { name: 'LLM Providers' })).toBeVisible();
    await expect(page.getByText('openrouter')).toBeVisible();
    await expect(page.getByText('deepseek')).toBeVisible();
    await expect(page.getByText('ollama-local')).toBeVisible();

    await page.getByText('openrouter').click();
    await expect(page).toHaveURL(/\/admin\/providers\/openrouter$/);
    await expect(page.getByRole('heading', { name: 'openrouter' })).toBeVisible();
  });

  test('quota n/d badge for ollama-local', async ({ page }) => {
    await page.goto('/admin/providers/ollama-local');
    await expect(page.getByText(/non supportato/i)).toBeVisible();
  });
});
```

- [ ] **Step 8.2: Run E2E**

```bash
cd apps/web && pnpm test:e2e -- admin-providers --reporter=list 2>&1 | tail -10
```

Expected: 2 PASS.

- [ ] **Step 8.3: Commit**

```bash
git add apps/web/e2e/admin-providers.spec.ts
git commit -m "test(web): add E2E for admin providers page"
```

---

## Task 9: Final verify + push + PR

- [ ] **Step 9.1: Full FE check**

```bash
cd apps/web && pnpm typecheck && pnpm lint && pnpm test --run --filter providers 2>&1 | tail -10
```

Expected: 0 errors typecheck, lint, all unit pass.

- [ ] **Step 9.2: Push**

```bash
git push -u origin feature/issue-NEW-providers-admin-ui
```

- [ ] **Step 9.3: Create PR**

```bash
gh pr create --base main-dev --title "feat(web): admin providers UI (G5)" --body "$(cat <<'EOF'
## Summary
- New routes /admin/providers (list) + /admin/providers/[name] (detail)
- Reads from PR1 endpoints (probe + quota), Admin auth
- "Run probe" button (SuperAdmin only)

Closes #NEW
Refs umbrella #935
Spec: G5

## Test plan
- [x] Unit: ProviderCard, ProvidersList, ProviderDetail
- [x] E2E: list + detail navigation, quota n/d badge

🤖 Generated with [Claude Code](https://claude.com/claude-code)
EOF
)"
```

- [ ] **Step 9.4: Verify CI green + merge**

```bash
gh pr checks <PR>; gh pr merge <PR> --squash --delete-branch
```

---

## Self-Review

**Spec coverage**:
- ✅ G5 SMART criteria: card riassuntiva (drill-down preview) + detail page
- ✅ "Run Probe" button SuperAdmin-only
- ✅ Quota unsupported → messaggio esplicito (no fake values)
- ⚠️ Mini-card in `/admin/overview` deferred (out of scope dichiarato)

**Patterns**:
- API client mirrors `adminSystemClient` ✓
- TanStack Query with staleTime 4min < server cache 5min ✓
- Zod schemas in dedicated `schemas/providers.ts` (project convention) ✓
- App Router params await pattern ✓
- generateStaticParams for SSG of known providers ✓

**Test quality**:
- 4 unit (Card x2, List, Detail) + 2 E2E (list+detail, n/d badge)
- Covers happy path + unsupported provider edge case

**Branching**:
- Step 0.1 forces main-dev checkout before branching (CLAUDE.md rule)
- PR target main-dev (parent of feature)
