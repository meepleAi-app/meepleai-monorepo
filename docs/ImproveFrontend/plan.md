# Frontend Improvement Plan - MeepleAI

**Last Updated**: 2025-11-13  
**Status**: 🔵 Proposed – awaiting prioritization  
**Timeline**: ~6-8 settimane (can overlap)  
**Focus**: Next.js 16 features, data layer hardening, consistent UX patterns

---

## 🎯 Objectives

- Sfruttare App Router + Server Components di Next 16 per ridurre il client JS e semplificare l’autenticazione.
- Centralizzare il data fetching con TanStack Query e un SDK tipizzato così da eliminare duplicazioni (`/auth/me`, `/games`, `/chats` sono fetchati più volte).
- Uniformare i form con `react-hook-form` + `zod` e spostare lo stato complesso (chat, upload) in store dedicati per supportare streaming e operazioni lunghe.

## 📦 Deliverable principali

- Nuovo albero `apps/web/app/` con layout condiviso, middleware lato Edge e Server Actions per auth/export.
- `QueryClientProvider` e mutation layer per tutte le chiamate critiche; caching + invalidation automatica.
- SDK API modulare (feature-based) con validazione zod e handling degli errori centralizzato.
- Chat store basato su Zustand (o simile) con slice per giochi, messaggi e feedback.
- Upload queue spostata fuori dal render loop tramite `useSyncExternalStore` + Web Worker/broadcast channel.

## ✅ Success metrics

- -30% codice duplicato per auth/game fetch (misurato via ESLint rule).
- <100 ms TTFB per `/chat` grazie a server-side gating (verificato con Next trace).
- 100% dei form complessi migrati a RHF + schema validation (login/register modal, export modal, admin forms).
- Upload di 10 PDF simultanei mantiene FPS > 50 su laptop di riferimento (profiling dev tools).

---

## 🧭 Workstreams

### WS1 – App Router & Server Actions Foundation
- **Obiettivo**: Introdurre gradualmente `app/` mantenendo parity con `pages/`.
- **Scope**: layout base, `AppProviders`, route `/` e `/chat` come Server Components, server actions per `login/register/export`.
- **Dipendenze**: nessuna (può convivere con Pages router finché completato).
- **Issues**: `FE-IMP-001`, `FE-IMP-002`.

### WS2 – Data Layer con TanStack Query
- **Obiettivo**: Rimpiazzare `useEffect` manuali con `useQuery`/`useMutation`, sfruttando cache e retry.
- **Scope**: provider globale, hook `useCurrentUser`, `useGames`, `useChats`, invalidazioni post-mutation.
- **Dipendenze**: WS1 (AppProviders pronti a ospitare QueryClient).
- **Issues**: `FE-IMP-003`.

### WS3 – Auth & API SDK Modernization
- **Obiettivo**: Eliminare duplicazioni di `AuthUser` e consolidare error handling.
- **Scope**: AuthContext che usa React Query, middleware Edge per redirect, SDK modulare con zod parsing e logging correlation-id.
- **Dipendenze**: WS2 (Query), parziale su WS1 per middleware.
- **Issues**: `FE-IMP-004`, `FE-IMP-005`.

### WS4 – Form Architecture Refresh
- **Obiettivo**: Uniformare login/register modal e `ExportChatModal` su `react-hook-form` + `zod`.
- **Scope**: creare `forms/` shared components, input wrappers accessibili, error mapping, server-action integration.
- **Dipendenze**: WS1 (server actions per form submit), WS3 (SDK).
- **Issues**: `FE-IMP-006`.

### WS5 – Chat State Store & Streaming Readiness
- **Obiettivo**: Spostare il mega `ChatProvider` (>300 righe) su store modulare (Zustand + selectors) predisposto allo streaming.
- **Scope**: slice `session`, `game`, `chat`, `messages`, optimistic updates, SSE/WebSocket hook.
- **Dipendenze**: WS2/WS3 (nuovi hook/API).
- **Issues**: `FE-IMP-007`.

### WS6 – Upload Queue Off-main-thread
- **Obiettivo**: Ridurre rerender e migliorare affidabilità della coda PDF.
- **Scope**: Web Worker o `BroadcastChannel` per progress events, `useSyncExternalStore` per subscribe, metriche e retri centralizzati.
- **Dipendenze**: none hard, ma beneficia del nuovo SDK (WS3).
- **Issues**: `FE-IMP-008`.

---

## 📅 Sequencing (suggerito)

1. **Settimana 1-2**: WS1 (App Router shell) + bootstrap Query provider.
2. **Settimana 2-3**: WS2 migra `/auth/me`, `/games`, `/chats`.
3. **Settimana 3-4**: WS3 (AuthContext + SDK) + Edge middleware.
4. **Settimana 4-5**: WS4 (form migration) in parallelo con WS5 groundwork.
5. **Settimana 5-6**: WS5 (chat store) + SSE scaffolding.
6. **Settimana 6-7**: WS6 (upload queue), QA end-to-end, performance benchmarks.

---

## ⚠️ Risks & Mitigations

- **Compatibilità librerie con React 19/Next 16**: verificare `next-themes`, `framer-motion`, `@tiptap` su App Router prima del rollout – mantenere fallback pages fino al completamento (issues `FE-IMP-001/002` comprendono smoke tests).
- **Debt di test**: nuove architetture richiedono aggiornare Jest + Playwright. Ogni issue include test plan minimo (vedi `issues.md`).
- **Sovrapposizione lavori**: usare feature flag `NEXT_PUBLIC_APP_ROUTER_ENABLED` per abilitare nuove route gradualmente.

---

## 📎 Riferimenti

- Issues dettagliati: [`docs/ImproveFrontend/issues.md`](./issues.md)  
- Stack attuale: `apps/web/package.json`  
- Contesto storico: `docs/planning/frontend-implementation-plan.md`

