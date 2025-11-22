#!/bin/bash
# Script to create Frontend Improvement GitHub Issues
# Run this script from the repository root: bash scripts/create-frontend-issues.sh

set -e

echo "Creating Frontend Improvement GitHub Issues..."
echo ""

# FE-IMP-001
echo "Creating FE-IMP-001..."
gh issue create \
  --title "FE-IMP-001 — Bootstrap App Router + Shared Providers" \
  --label "frontend,enhancement,architecture,month-6" \
  --body "$(cat <<'EOF'
**Contesto**
Next 16 è già installato ma l'app usa ancora il Pages router. Serve introdurre `app/` con layout condivisi, `AppProviders` (Theme, QueryClient placeholder) e routing parallelo per `/` e `/chat`.

**Scope**
1. Creare `apps/web/app/layout.tsx`, `app/page.tsx` (home) e `app/chat/page.tsx` come Server Components wrapper (possono importare componenti client).
2. Portare `ThemeProvider`, `ErrorBoundary`, `Toaster`, `AccessibleSkipLink` in `app/providers.tsx`.
3. Aggiungere flag `NEXT_PUBLIC_APP_ROUTER_ENABLED` per scegliere se servire route nuove o quelle legacy (utile in QA).
4. Aggiornare `next.config.js` e README con istruzioni migrazione.

**Acceptance**
- `pnpm dev` serve correttamente `/` e `/chat` dal nuovo router quando il flag è attivo.
- Lighthouse mostra -10% JS shipped rispetto alla pagina legacy grazie a server components.
- Playwright smoke (`apps/web/e2e`) aggiornato con toggle flag.

**Milestone**: Next release train (Week 1)

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-001 created"
echo ""

# FE-IMP-002
echo "Creating FE-IMP-002..."
gh issue create \
  --title "FE-IMP-002 — Server Actions per Auth & Export" \
  --label "frontend,auth,server-actions,p1" \
  --body "$(cat <<'EOF'
**Summary**
Implementare Server Actions per `login`, `register`, `logout`, `exportChat` e `extendSession` così da rimuovere fetch manuali e ottenere protezioni CSRF integrate.

**Tasks**
1. Creare file `app/actions/auth.ts` con funzioni server-side che chiamano `api` backend.
2. Aggiornare landing modal e chat export per usare `useActionState`/`useTransition`.
3. Gestire errori tipizzati (es. credenziali errate vs server error).
4. Aggiornare test Jest per mockare azioni via `vi.mock('next/server')`.

**Acceptance**
- Nessuna chiamata `fetch('/api/v1/auth/*')` parte più dal client durante login/register.
- Errori vengono mostrati in-line con messaggi localizzati.
- QA: scenario login/logout, export chat (pdf/txt/md) passa su dev e staging.

**Dipendenze**: FE-IMP-001

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-002 created"
echo ""

# FE-IMP-003
echo "Creating FE-IMP-003..."
gh issue create \
  --title "FE-IMP-003 — TanStack Query Data Layer" \
  --label "frontend,data-layer,tanstack-query,tech-debt" \
  --body "$(cat <<'EOF'
**Summary**
Installare `@tanstack/react-query` (se non già presente) e avvolgere l'app nel `QueryClientProvider` introdotto in FE-IMP-001.

**Tasks**
1. Configurare `QueryClient` con `defaultOptions` (retry, staleTime, error logging).
2. Creare hook `useCurrentUser`, `useGames`, `useChats(gameId)`, `useMessages(chatId)` usando `useQuery`.
3. Sostituire `loadCurrentUser`, `loadGames`, `loadChats`, `loadChatHistory` con i nuovi hook + `useMutation`.
4. Aggiornare unit test (mock `react-query`) e documentare pattern in `docs/frontend/testing-strategy.md`.

**Acceptance**
- Nessun `useEffect(async …)` rimane nei componenti per fetch delle risorse sopra.
- Cache invalidata automaticamente dopo `createChat`, `editMessage`, `deleteMessage`.
- React Profiler mostra -40% render ridondanti in `ChatProvider`.

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-003 created"
echo ""

# FE-IMP-004
echo "Creating FE-IMP-004..."
gh issue create \
  --title "FE-IMP-004 — AuthContext + Edge Middleware" \
  --label "frontend,auth,middleware,quality" \
  --body "$(cat <<'EOF'
**Scope**
1. Implementare `AuthProvider` che fornisce `user`, `status`, `signIn/out` sfruttando `useCurrentUser`.
2. Creare `middleware.ts` per leggere i cookie di sessione e reindirizzare `/chat`, `/upload`, `/admin` verso `/login` se anonimi.
3. Rimuovere duplicazioni di `AuthUser` type nelle pagine e componenti (usare `@/types`).
4. Aggiornare Playwright per testare redirect e flash-free gating.

**Acceptance**
- `ChatPage` non effettua più fetch standalone; consuma il contesto.
- Navigazione anonimo -> `/chat` mostra redirect lato server (nessun contenuto lampeggia).
- Coverage > 80% per middleware logic (unit + e2e).

**Dipendenze**: FE-IMP-003

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-004 created"
echo ""

# FE-IMP-005
echo "Creating FE-IMP-005..."
gh issue create \
  --title "FE-IMP-005 — API SDK modulare con Zod" \
  --label "frontend,api,documentation,tech-debt" \
  --body "$(cat <<'EOF'
**Summary**
Spezzare `apps/web/src/lib/api.ts` in module per feature (`authClient`, `chatClient`, `uploadClient`, ecc.) con validazione `zod` sia in ingresso che uscita.

**Tasks**
1. Definire schema `z.object({ ... })` per risposte note (`AuthResponse`, `Chat`, ecc.).
2. Esporre factory `createApiClient(fetchImpl)` per facilitare mocking nei test.
3. Centralizzare gestione `ApiError` con correlationId, logging, mapping in `docs/frontend/error-handling.md`.
4. Aggiornare import nei componenti/hook + rimuovere tipi duplicati.

**Acceptance**
- Lint non trova più import diretti da `../lib/api` per tutto il codice (solo entry modulare).
- Error boundary riceve errori arricchiti (status, correlationId).
- Documentazione aggiornata (`docs/frontend/architecture.md`).

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-005 created"
echo ""

# FE-IMP-006
echo "Creating FE-IMP-006..."
gh issue create \
  --title "FE-IMP-006 — Form System (RHF + Zod)" \
  --label "frontend,forms,ux,accessibility" \
  --body "$(cat <<'EOF'
**Scope**
Login/Register modal + `ExportChatModal` + future forms condividono `FormProvider`, componenti input, gestione errori.

**Tasks**
1. Creare `apps/web/src/components/forms/` con `Form`, `FormField`, `FormError`, ecc.
2. Definire schemi Zod per `LoginForm`, `RegisterForm`, `ExportChatForm`.
3. Collegare i form alle Server Actions (`FE-IMP-002`) e mostrare errori field-level + summary.
4. Aggiornare test React Testing Library (submit success/error, keyboard nav).

**Acceptance**
- Nessun `useState` per singoli campi rimane nei form target.
- Errori di validazione sono annunciati via aria-live (verificato con jest-axe).
- Docs aggiornate (`docs/frontend/accessibility-standards.md`).

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-006 created"
echo ""

# FE-IMP-007
echo "Creating FE-IMP-007..."
gh issue create \
  --title "FE-IMP-007 — Chat Store con Zustand + Streaming Hook" \
  --label "frontend,chat,state-management,performance" \
  --body "$(cat <<'EOF'
**Summary**
Rimpiazzare l'attuale `ChatProvider` monolitico con uno store modulare e predisporre l'integrazione SSE/WebSocket.

**Tasks**
1. Installare `zustand` (se non presente) con middleware `immer` + `subscribeWithSelector`.
2. Creare slice `session`, `game`, `chat`, `messages`, `ui`.
3. Esporre hook `useChatStore(selector)` e rimuovere `useState` annidati dal vecchio provider.
4. Implementare hook `useChatStream(chatId)` che ascolta SSE (mock per ora) e aggiorna solo il slice interessato.
5. Aggiornare test per selettori e optimistic update.

**Acceptance**
- Componenti `ChatSidebar`, `ChatContent`, `MessageList` leggono solo il selettore necessario (profiling mostra meno rerender).
- Store supporta `undo` per edit/delete tramite history o snapshots.
- Documentato in `docs/frontend/architecture.md`.

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-007 created"
echo ""

# FE-IMP-008
echo "Creating FE-IMP-008..."
gh issue create \
  --title "FE-IMP-008 — Upload Queue Off-Main-Thread" \
  --label "frontend,upload,performance,pdf" \
  --body "$(cat <<'EOF'
**Summary**
Spostare la coda PDF in Web Worker/BroadcastChannel e consumarla tramite `useSyncExternalStore`, evitando rerender massivi e permettendo riprese dopo refresh.

**Tasks**
1. Creare worker `uploadQueue.worker.ts` che gestisce stato, retry/backoff (riusando `retryUtils`).
2. Implementare `UploadQueueStore` condiviso (BroadcastChannel per più tab).
3. Aggiornare `useUploadQueue` per diventare un wrapper su `useSyncExternalStore`.
4. Integrare metriche (succ/fail) e log correlationId centralizzati.
5. Aggiornare docs + test (mock worker con `jest-worker` o `happy-dom` fallback).

**Acceptance**
- Drag&drop di 10 PDF non blocca UI (<5% dropped frames).
- Coda sopravvive a refresh tab (stato recuperato).
- QA script (`apps/web/scripts/run-upload-smoke.ts`) aggiornato con nuovi hook.

**Riferimento**: `docs/ImproveFrontend/plan.md`
EOF
)"

echo "✓ FE-IMP-008 created"
echo ""

echo "=========================================="
echo "All 8 Frontend Improvement issues created!"
echo "=========================================="
