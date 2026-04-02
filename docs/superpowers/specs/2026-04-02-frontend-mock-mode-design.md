# Frontend Mock Mode — Design Spec

**Date**: 2026-04-02
**Status**: Approved
**Scope**: `apps/web/`

## Obiettivo

Permettere lo sviluppo del frontend Next.js 16 senza dipendenze da servizi esterni (backend .NET, database, Redis). Il developer può eseguire `pnpm dev:mock` e navigare l'intera UI con dati realistici.

## Stato Attuale

| Componente | Stato |
|------------|-------|
| MSW `setupServer` (Node tests) | ✅ Già presente in `__tests__/mocks/` |
| MSW handlers (13 domini) | ✅ Già presenti in `__tests__/mocks/handlers/` |
| Storybook + `msw-storybook-addon` | ✅ Già configurato (senza handlers globali) |
| MSW `setupWorker` (browser) | ❌ Mancante |
| Handler browser-safe | ❌ Mancanti (`__tests__/` contiene deps di test) |
| `pnpm dev:mock` script | ❌ Mancante |

## Architettura

```
PRIMA:
  [Browser/Dev] → fetch() → REAL API (localhost:8080) [RICHIESTO]
  [Test/Node]   → fetch() → MSW Node Server → __tests__/mocks/handlers/

DOPO:
  [Browser/Dev + NEXT_PUBLIC_MOCK_MODE=true] → fetch() → MSW ServiceWorker → src/mocks/handlers/
  [Browser/Dev + NEXT_PUBLIC_MOCK_MODE=false] → fetch() → REAL API
  [Test/Node]   → fetch() → MSW Node Server → __tests__/mocks/handlers/ [INVARIATO]
  [Storybook]   → fetch() → MSW ServiceWorker → src/mocks/handlers/
```

## Struttura src/mocks/

```
src/mocks/
├── data/
│   └── factories.ts          # Factory pure (no test deps): createMockGame, createMockUser, etc.
├── handlers/
│   ├── auth.handlers.ts      # POST /api/v1/auth/* — stateless login simulation
│   ├── games.handlers.ts     # CRUD /api/v1/games/* — stateful in-memory store
│   ├── chat.handlers.ts      # GET/POST /api/v1/chat/* + SSE streaming
│   ├── documents.handlers.ts # /api/v1/documents/* + /api/v1/ingest/pdf
│   ├── library.handlers.ts   # /api/v1/library/* — user game library
│   ├── shared-games.handlers.ts  # /api/v1/shared-games/* — community catalog
│   ├── catalog.handlers.ts   # /api/v1/games/categories + mechanics
│   ├── admin.handlers.ts     # /api/v1/admin/* — dashboard stats
│   ├── sessions.handlers.ts  # /api/v1/sessions/* — game sessions
│   ├── game-nights.handlers.ts  # /api/v1/game-nights/*
│   ├── players.handlers.ts   # /api/v1/players/*
│   ├── notifications.handlers.ts  # /api/v1/notifications/*
│   ├── badges.handlers.ts    # /api/v1/badges/*
│   └── index.ts              # Aggregates all handlers
├── browser.ts                # MSW setupWorker (browser)
├── node.ts                   # MSW setupServer (Node.js, alternativo)
└── index.ts                  # Barrel export
```

## Separazione delle dipendenze

| File | Dipendenze test | Browser-safe |
|------|-----------------|--------------|
| `__tests__/fixtures/common-fixtures.ts` | @testing-library/react, vi.fn() | ❌ |
| `src/mocks/data/factories.ts` | Nessuna | ✅ |
| `__tests__/fixtures/sse-test-helpers.ts` | Nessuna | ✅ (riusato via @/) |
| `__tests__/fixtures/mockAdminData.ts` | @/lib/api (solo tipi) | ✅ (riusato via @/) |

## Flusso di avvio MockProvider

```
pnpm dev:mock
  └→ Next.js dev server (NEXT_PUBLIC_MOCK_MODE=true)
       └→ app/layout.tsx renders
            └→ AppProviders renders
                 └→ MockProvider (Client Component)
                      └→ useEffect: import('@/mocks/browser').then(worker.start())
                           └→ MSW ServiceWorker registrato su /mockServiceWorker.js
                                └→ Tutte le fetch() vengono intercettate dagli handler
```

## Storybook Integration

`msw-storybook-addon` è già installato e configurato con `initialize()`. La modifica richiesta è aggiungere i global handlers in `preview.tsx`:

```typescript
import { handlers } from '../src/mocks/handlers';

parameters: {
  msw: { handlers }
}
```

## Prerequisito: MSW Service Worker

MSW richiede `public/mockServiceWorker.js`. Questo file deve essere generato con:
```bash
cd apps/web && npx msw init public/ --save
```

## Script npm

| Script | Comando | Uso |
|--------|---------|-----|
| `pnpm dev` | Next.js dev normale | Con backend attivo |
| `pnpm dev:mock` | Next.js dev + NEXT_PUBLIC_MOCK_MODE=true | Senza backend |

## Contract Validation (Passo 3)

Script `scripts/validate-mock-contracts.ts` che:
1. Importa le factory da `src/mocks/data/factories.ts`
2. Crea istanze di ogni tipo
3. Valida contro gli schemi Zod in `src/lib/api/schemas/`
4. Report di coverage e drift

## Decisioni prese

- **No duplicazione handler**: Gli handler `__tests__/mocks/handlers/` restano invariati. I nuovi in `src/mocks/handlers/` sono la versione browser-safe.
- **No breaking changes ai test**: 13K+ test continuano a usare `__tests__/mocks/` inalterato.
- **Race condition prevention**: `MockProvider` usa `useState(false)` e blocca il render fino a `worker.start()` completato.
- **SSE nel browser**: `sse-test-helpers.ts` è già browser-safe (ReadableStream API standard), riusato via `@/__tests__/`.
