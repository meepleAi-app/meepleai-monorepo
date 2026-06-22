# F3-FU-6 — KbSubNav Count Badges (Queue / Feedback)

- **Issue**: [#1655](https://github.com/meepleAi-app/meepleai-monorepo/issues/1655)
- **Cluster**: F3 KB Explorer follow-up (chiusura — 5/6 già fatte)
- **Priority**: P3
- **Branch parent**: `main-dev`
- **Date**: 2026-05-30

---

## Background

PR #1649 ha consegnato l'admin Knowledge Base Explorer con `KbSubNav` (8 tab orizzontali). Il commento nel codice di `KbSubNav.tsx` diceva esplicitamente:

> *"Niente badge count in F3.1 (deferred a follow-up)."*

Questa spec definisce quel follow-up, limitato ai tab **Processing Queue** e **Feedback** (gli unici menzionati nel titolo dell'issue). Gli altri 6 tab restano senza badge.

## Goal

Mostrare, accanto al label dei tab *Processing Queue* e *Feedback* del sub-nav `KbSubNav`, un badge numerico che riflette in tempo "quasi reale" la quantità di lavoro/attività pertinente, così che il superadmin sappia at-a-glance se c'è qualcosa che richiede attenzione senza aprire le tab.

## Non-goal

- Badge sugli altri 6 tab (Explorer, Vector Collections, RAG Pipeline, Embedding, Settings, Snapshots).
- Notifiche push / toast su nuovo elemento.
- Drill-down dal badge (resta semplice indicatore numerico).
- SSE/WebSocket real-time push (polling ogni 30s è sufficiente).
- Persistenza "letto/non letto" per feedback (no read model dedicato).

## Semantica dei conteggi

| Tab | Cosa conta | Query |
|---|---|---|
| Processing Queue | Job che richiedono attenzione | `COUNT(processing_jobs) WHERE status IN (Queued, Processing, Failed)` |
| Feedback | Attività feedback ultima settimana | `COUNT(kb_user_feedback) WHERE created_at >= NOW() - INTERVAL '7 days'` |

**Note**:
- *Queue*: include `Failed` perché ammette retry manuale (azione admin). Esclude `Completed`, `Cancelled` (no azione richiesta).
- *Feedback*: count **non monotono** — vecchi feedback escono dalla finestra mobile. Documentato in tooltip nativo (`title`) sul badge: *"Feedback ricevuti negli ultimi 7 giorni"*.

## Architettura

```
KbSubNav (client)
   └── useKbNavCounts() → React Query (poll 30s + refetchOnWindowFocus)
            │
            ▼
   GET /api/v1/admin/kb/nav-counts
            │  MediatR
            ▼
   GetKbNavCountsQueryHandler
       ├── IProcessingJobRepository.CountByStatusesAsync([Queued, Processing, Failed])
       └── IKbUserFeedbackRepository.CountSinceAsync(now - 7d)
           (eseguite in parallelo via Task.WhenAll)
            │
            ▼
   KbNavCountsDto { ProcessingQueue, Feedback7d, AsOf }
```

**Bounded Context placement**: la query vive in `KnowledgeBase.Application.Queries.GetKbNavCounts` perché è una vista *navigation-purpose* del BC KB, anche se aggrega dati da `DocumentProcessing`. Il cross-BC read avviene tramite interfaccia repository — niente entity sharing.

## Backend

### DTO

`apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbNavCountsDto.cs`:

```csharp
namespace Api.BoundedContexts.KnowledgeBase.Application.DTOs;

public sealed record KbNavCountsDto(
    int ProcessingQueue,
    int Feedback7d,
    DateTimeOffset AsOf
);
```

### Query + Handler

`apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/`:

```csharp
public sealed record GetKbNavCountsQuery() : IRequest<KbNavCountsDto>;

internal sealed class GetKbNavCountsQueryHandler
    : IRequestHandler<GetKbNavCountsQuery, KbNavCountsDto>
{
    private static readonly ProcessingJobStatus[] ActiveStatuses =
        [ProcessingJobStatus.Queued, ProcessingJobStatus.Processing, ProcessingJobStatus.Failed];

    private static readonly TimeSpan FeedbackWindow = TimeSpan.FromDays(7);

    private readonly IProcessingJobRepository _jobs;
    private readonly IKbUserFeedbackRepository _feedback;
    private readonly TimeProvider _clock;

    public GetKbNavCountsQueryHandler(
        IProcessingJobRepository jobs,
        IKbUserFeedbackRepository feedback,
        TimeProvider clock)
    {
        _jobs = jobs;
        _feedback = feedback;
        _clock = clock;
    }

    public async Task<KbNavCountsDto> Handle(GetKbNavCountsQuery _, CancellationToken ct)
    {
        var asOf = _clock.GetUtcNow();
        var since = asOf - FeedbackWindow;

        var queueTask = _jobs.CountByStatusesAsync(ActiveStatuses, ct);
        var feedbackTask = _feedback.CountSinceAsync(since, ct);

        await Task.WhenAll(queueTask, feedbackTask).ConfigureAwait(false);

        return new KbNavCountsDto(
            ProcessingQueue: await queueTask.ConfigureAwait(false),
            Feedback7d: await feedbackTask.ConfigureAwait(false),
            AsOf: asOf);
    }
}
```

### Repository extensions (NO migration)

`IProcessingJobRepository`:
```csharp
Task<int> CountByStatusesAsync(
    IReadOnlyList<ProcessingJobStatus> statuses,
    CancellationToken cancellationToken);
```
Impl: `_dbContext.ProcessingJobs.AsNoTracking().Where(j => statuses.Contains(j.Status)).CountAsync(ct)`.

`IKbUserFeedbackRepository`:
```csharp
Task<int> CountSinceAsync(DateTimeOffset since, CancellationToken cancellationToken);
```
Impl: `_dbContext.KbUserFeedbacks.AsNoTracking().Where(f => f.CreatedAt >= since).CountAsync(ct)`.

Entrambi sono `COUNT(*)`. Riusano indici esistenti su `status` e `created_at`. Nessuna migration richiesta.

### Endpoint

In `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` (estendere file esistente):

```csharp
kbGroup.MapGet("/nav-counts", async (IMediator mediator, CancellationToken ct) =>
{
    var counts = await mediator.Send(new GetKbNavCountsQuery(), ct).ConfigureAwait(false);
    return Results.Ok(counts);
})
.WithName("GetKbNavCounts")
.WithSummary("Counts for KbSubNav badges (queue active + feedback last 7d).")
.Produces<KbNavCountsDto>(200);
```

Sotto `RequireAdminSessionFilter` come gli altri endpoint del group → 401/403 automatici.

### DI registration

Handler auto-discovered da MediatR assembly scan. Repository methods aggiunti a implementazioni esistenti — nessuna nuova DI binding.

## Frontend

### Componente badge

`apps/web/src/components/admin/knowledge-base/explorer/KbCountBadge.tsx`:

```tsx
/* eslint-disable local/no-hardcoded-color-utility -- admin KB explorer: amber accent (admin convention) */
'use client';

interface KbCountBadgeProps {
  readonly count: number | undefined;
  readonly loading: boolean;
  readonly tooltip?: string;
  readonly testId?: string;
}

export function KbCountBadge({ count, loading, tooltip, testId }: KbCountBadgeProps): JSX.Element {
  if (loading && count === undefined) {
    return (
      <span
        aria-hidden="true"
        data-testid={testId ? `${testId}-loading` : undefined}
        className="ml-1.5 inline-block h-4 w-6 rounded-full bg-muted animate-pulse"
      />
    );
  }

  const safe = count ?? 0;
  const display = safe > 99 ? '99+' : safe;
  const isActive = safe > 0;

  return (
    <span
      aria-label={`${safe} elementi`}
      title={tooltip}
      data-testid={testId}
      className={[
        'ml-1.5 inline-flex items-center justify-center min-w-[1.5rem] h-4 px-1.5 rounded-full',
        'text-[10px] font-bold tabular-nums leading-none',
        isActive
          ? 'bg-amber-500/15 text-amber-700 dark:text-amber-300'
          : 'bg-muted text-muted-foreground',
      ].join(' ')}
    >
      {display}
    </span>
  );
}
```

### Hook

`apps/web/src/hooks/admin/useKbNavCounts.ts`:

```ts
import { keepPreviousData, useQuery } from '@tanstack/react-query';
import { api } from '@/lib/api';

export interface KbNavCounts {
  queue: number | undefined;
  feedback: number | undefined;
  loading: boolean;
  isError: boolean;
}

export function useKbNavCounts(): KbNavCounts {
  const query = useQuery({
    queryKey: ['admin', 'kb', 'nav-counts'] as const,
    queryFn: ({ signal }) => api.adminKnowledgeBase.getNavCounts({ signal }),
    refetchInterval: 30_000,
    refetchOnWindowFocus: true,
    staleTime: 15_000,
    retry: 1,
    placeholderData: keepPreviousData,
  });

  return {
    queue: query.data?.processingQueue,
    feedback: query.data?.feedback7d,
    loading: query.isLoading,
    isError: query.isError,
  };
}
```

### API client

In `apps/web/src/lib/api/admin-knowledge-base.ts` (estendere modulo esistente):

```ts
export interface KbNavCountsDto {
  processingQueue: number;
  feedback7d: number;
  asOf: string;
}

// ...esistente
getNavCounts: ({ signal }: { signal?: AbortSignal } = {}) =>
  apiFetch<KbNavCountsDto>('/api/v1/admin/kb/nav-counts', { signal }),
```

### Modifica `KbSubNav.tsx`

```tsx
'use client';

import Link from 'next/link';
import { usePathname } from 'next/navigation';

import { KbCountBadge } from './KbCountBadge';
import { useKbNavCounts } from '@/hooks/admin/useKbNavCounts';

const KB_BASE = '/admin/knowledge-base';

interface KbTab {
  readonly label: string;
  readonly href: string;
  readonly kind?: 'queue' | 'feedback';
}

const TABS: ReadonlyArray<KbTab> = [
  { label: 'Explorer', href: KB_BASE },
  { label: 'Vector Collections', href: `${KB_BASE}/vectors` },
  { label: 'Processing Queue', href: `${KB_BASE}/queue`, kind: 'queue' },
  { label: 'RAG Pipeline', href: `${KB_BASE}/pipeline` },
  { label: 'Embedding', href: `${KB_BASE}/embedding` },
  { label: 'Feedback', href: `${KB_BASE}/feedback`, kind: 'feedback' },
  { label: 'Settings', href: `${KB_BASE}/settings` },
  { label: 'Snapshots', href: `${KB_BASE}/snapshots` },
];

const TOOLTIPS: Record<NonNullable<KbTab['kind']>, string> = {
  queue: 'Job attivi (queued, in elaborazione o falliti)',
  feedback: 'Feedback ricevuti negli ultimi 7 giorni',
};

export function KbSubNav() {
  const pathname = usePathname();
  const { queue, feedback, loading } = useKbNavCounts();

  return (
    <nav
      aria-label="Knowledge Base sezioni"
      className="border-b border-border/60 dark:border-zinc-700/60 -mx-6 px-6 mb-6 overflow-x-auto"
    >
      <ul className="flex gap-1 min-w-max">
        {TABS.map(tab => {
          const active = isActive(tab.href, pathname);
          const count = tab.kind === 'queue' ? queue : tab.kind === 'feedback' ? feedback : undefined;
          return (
            <li key={tab.href}>
              <Link
                href={tab.href}
                aria-current={active ? 'page' : undefined}
                className={/* invariato */}
              >
                {tab.label}
                {tab.kind && (
                  <KbCountBadge
                    count={count}
                    loading={loading}
                    tooltip={TOOLTIPS[tab.kind]}
                    testId={`kb-nav-badge-${tab.kind}`}
                  />
                )}
              </Link>
            </li>
          );
        })}
      </ul>
    </nav>
  );
}
```

`isActive` resta identico.

## Error handling

| Caso | Comportamento |
|---|---|
| Errore di rete / 5xx | React Query `isError = true`. Badge → `—` con `aria-label="conteggio non disponibile"`, tinta `muted`. Tab restano clickabili. |
| 401 / 403 | Gestito dal layer API esistente (redirect login). Hook non aggiunge logica. |
| Backend handler — una delle due COUNT fallisce | `Task.WhenAll` propaga la prima eccezione → 5xx. Fail-fast. FE mostra `—` per entrambi i badge. |
| Loading iniziale | Skeleton (`bg-muted animate-pulse`) larghezza fissa per evitare layout shift. |
| `count === 0` autentico | Pill grigia "0" — distinto da loading. |
| Polling concurrent / cambio tab | `placeholderData: keepPreviousData` evita flicker; KbSubNav vive in `layout.tsx` → l'hook persiste. |
| Cancellation | `signal` propagato a `fetch` e CT a EF Core; React Query auto-aborta su unmount o keyChange. |

## Testing

### Backend — Unit (handler con mocks)

`tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQueryHandlerTests.cs`:

1. `Handle_returns_counts_from_both_repositories` — happy path, asserisce DTO + `AsOf` da `FakeTimeProvider`.
2. `Handle_passes_active_statuses_to_processing_repo` — verifica array esatto `[Queued, Processing, Failed]`.
3. `Handle_uses_now_minus_7_days_for_feedback` — `FakeTimeProvider.SetUtcNow`, asserisce `since`.
4. `Handle_runs_count_queries_in_parallel` — uno dei mock blocca con `TaskCompletionSource`, l'altro deve essere già stato chiamato.
5. `Handle_propagates_processing_repo_exception` — fail-fast.
6. `Handle_propagates_cancellation_token` — `OperationCanceledException` su CT pre-cancellato.

### Backend — Integration (Testcontainers Postgres)

`tests/Api.Tests/Integration/BoundedContexts/KnowledgeBase/GetKbNavCountsQueryHandlerIntegrationTests.cs`:

Seed:
- 2 job `Queued`, 1 `Processing`, 1 `Failed`, 3 `Completed`, 1 `Cancelled` → expected `ProcessingQueue = 4`
- 5 feedback `CreatedAt = now - 1..6 days`, 2 feedback `CreatedAt = now - 10 days` → expected `Feedback7d = 5`

Esegue handler reale contro DB reale → asserisce DTO esatto. **Rispetta la regola "acceptance tests must exercise real pipeline"** (memory `feedback_acceptance_tests_must_exercise_real_pipeline.md`).

### Backend — Endpoint integration (HTTP)

In `tests/Api.Tests/Routing/AdminKnowledgeBaseEndpointsTests.cs` (estendere file esistente):

1. `Get_nav_counts_without_session_returns_401`
2. `Get_nav_counts_with_user_session_returns_403`
3. `Get_nav_counts_with_admin_session_returns_200_with_dto`

### Frontend — Unit `KbCountBadge`

`apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCountBadge.test.tsx`:

1. `renders skeleton during loading when count is undefined`
2. `renders 0 pill with muted style when count is 0`
3. `renders 23 pill with amber style when count > 0`
4. `renders "99+" when count > 99`
5. `falls back to 0 when count is undefined after loading`
6. `applies title attribute from tooltip prop`
7. `applies data-testid when testId provided`

### Frontend — Unit `useKbNavCounts`

`apps/web/src/hooks/admin/__tests__/useKbNavCounts.test.ts`:

Mock `api.adminKnowledgeBase.getNavCounts`:
1. `returns queue and feedback from successful fetch`
2. `loading is true before first settle`
3. `isError is true when fetch throws`
4. `configures refetchInterval=30000 and refetchOnWindowFocus=true` (verificato osservando le opzioni passate a `useQuery` via spy)

### Frontend — Estensione `KbSubNav.test.tsx`

Mock `useKbNavCounts` con `vi.mock('@/hooks/admin/useKbNavCounts')`:

- Preserva i 7 test esistenti (regressione zero — i mock di default ritornano `{ queue: undefined, feedback: undefined, loading: false, isError: false }` per non interferire).
- Aggiunge:
  1. `renders queue badge with count from hook`
  2. `renders feedback badge with count from hook`
  3. `does NOT render badge on non-counted tabs` (Explorer, Vectors, Pipeline, Embedding, Settings, Snapshots)
  4. `renders skeleton during initial loading`

### E2E (Playwright)

`apps/web/e2e/admin-kb-navcounts.spec.ts`:

1 smoke test: superadmin login → naviga `/admin/knowledge-base` → asserisce presenza badge `data-testid="kb-nav-badge-queue"` e `kb-nav-badge-feedback` con contenuto numerico (regex `/^\d+\+?$|^—$/`). Nessun assert sul valore esatto (sarebbe flaky).

### Coverage attesa

- Handler: 100% line, 100% branch (6 test coprono tutti i path)
- Badge: 100% (4 stati x 2 dimensioni assert)
- Hook: ≥ 90%
- `KbSubNav` patch: ≥ 95%
- Zero regressioni sui 7 test esistenti di `KbSubNav.test.tsx`

## Performance

- 2 query `COUNT(*)` con indice esistente, parallelizzate. Stima: < 5ms ciascuna su dataset realistico (10k job, 5k feedback).
- Polling 30s × N admin connessi: trascurabile (1 admin tipico → 2 query/min).
- Nessuna cache server-side (aggiunge staleness senza beneficio).
- Nessun SSE/WebSocket overhead.

## Security

- Endpoint sotto `RequireAdminSessionFilter` come gli altri del group.
- Nessun dato sensibile esposto (solo conteggi aggregati).
- Nessun parametro user-controlled → niente SQL injection surface.

## Rollout

- No feature flag necessario: P3, scope frontend additivo, fallback safe (badge non si rompe se endpoint non c'è → `isError` → `—`).
- Deploy senza dipendenze: backend prima (endpoint disponibile), frontend dopo (lo consuma).

## File touched

**Nuovi (10)**:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/DTOs/KbNavCountsDto.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQuery.cs`
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQueryHandler.cs`
- `apps/web/src/components/admin/knowledge-base/explorer/KbCountBadge.tsx`
- `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbCountBadge.test.tsx`
- `apps/web/src/hooks/admin/useKbNavCounts.ts`
- `apps/web/src/hooks/admin/__tests__/useKbNavCounts.test.ts`
- `tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Queries/GetKbNavCounts/GetKbNavCountsQueryHandlerTests.cs`
- `tests/Api.Tests/Integration/BoundedContexts/KnowledgeBase/GetKbNavCountsQueryHandlerIntegrationTests.cs`
- `apps/web/e2e/admin-kb-navcounts.spec.ts`

**Modificati (9)**:
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Domain/Repositories/IKbUserFeedbackRepository.cs` (+1 method)
- `apps/api/src/Api/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/KbUserFeedbackRepository.cs` (+impl)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Domain/Repositories/IProcessingJobRepository.cs` (+1 method)
- `apps/api/src/Api/BoundedContexts/DocumentProcessing/Infrastructure/Persistence/ProcessingJobRepository.cs` (+impl)
- `apps/api/src/Api/Routing/AdminKnowledgeBaseEndpoints.cs` (+endpoint)
- `apps/web/src/lib/api/admin-knowledge-base.ts` (+method + tipo)
- `apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx` (+hook, +badge, +kind)
- `apps/web/src/components/admin/knowledge-base/explorer/__tests__/KbSubNav.test.tsx` (+mock, +4 test)
- `tests/Api.Tests/Routing/AdminKnowledgeBaseEndpointsTests.cs` (+3 test)

## Acceptance criteria

1. Endpoint `GET /api/v1/admin/kb/nav-counts` ritorna 200 + DTO valido per session admin; 401/403 altrimenti.
2. Tab "Processing Queue" mostra badge con conteggio aggiornato di job in `Queued + Processing + Failed`.
3. Tab "Feedback" mostra badge con conteggio aggiornato di feedback creati nelle ultime 7 giorni.
4. Badge sempre presenti su quelle 2 tab (anche con `0`, stile muted); amber quando `> 0`; `99+` quando `> 99`.
5. Aggiornamento automatico ogni 30s + refresh su window focus.
6. Errore di rete → badge `—` invece di crash; tab restano usable.
7. Skeleton durante loading iniziale (no layout shift).
8. Tooltip nativo (`title`) descrive cosa conta ciascun badge.
9. 7 test esistenti di `KbSubNav` continuano a passare.
10. Coverage handler 100%, badge 100%, hook ≥ 90%.
