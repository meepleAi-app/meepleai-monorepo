# SP5 Admin — F3-FU-7 `/admin/ai` Re-skin (Plan)

**Date:** 2026-05-31
**Status:** ready for execution
**Issue:** [#1722](https://github.com/meepleAi-app/meepleai-monorepo/issues/1722)
**Design:** [`2026-05-31-sp5-admin-f3-fu-7-admin-ai-reskin-design.md`](../specs/2026-05-31-sp5-admin-f3-fu-7-admin-ai-reskin-design.md)
**Branch:** `feature/issue-1722-f3-fu-7-admin-ai-reskin`
**PR target:** `main-dev` (4 PR stacked, rebase-merge ciascuna)

---

## Ground rules

1. **TDD per ogni componente nuovo.** RED → GREEN → REFACTOR. Mock di api/clients quando l'endpoint non è coperto da fixture.
2. **className-only su PR 1.** Nessun cambio a hooks / state / behavior nelle 9 tab esistenti. Diff atteso: imports + className.
3. **Token-only.** Nessun `bg-zinc-*`, `text-gray-*`, `border-slate-*`. `pnpm lint:tokens` deve restare verde.
4. **Endpoint mancanti = degrade gracefully.** PR 2/3 funzionano con fallback su dati esistenti; sub-task BE tracciati ma **non bloccano** merge.
5. **Rollback granulare.** Ogni PR mergeable e revert-able in autonomia. Se PR 3 ha bug, PR 1+2 restano in prod.
6. **Conventional commit.** `feat(admin-ai): #1722 PR N/4 — <subject>`.

## File structure

```
apps/web/src/
├── app/admin/(dashboard)/ai/
│   ├── layout.tsx                          # NEW (PR 1)
│   ├── page.tsx                            # MOD: rimuove <h1> locale (PR 1)
│   ├── AgentsTab.tsx                       # MOD: token re-skin (PR 1)
│   ├── AiLabTab.tsx                        # MOD: token re-skin (PR 1)
│   ├── DefinitionsTab.tsx                  # MOD: token re-skin (PR 1)
│   ├── LlmConfigTab.tsx                    # MOD: token re-skin only — NO logic (PR 1)
│   ├── ModelsTab.tsx                       # MOD: token re-skin (PR 1)
│   ├── PromptsTab.tsx                      # MOD: token re-skin (PR 1)
│   ├── RagTab.tsx                          # MOD: token re-skin (PR 1), integration trend chart (PR 3)
│   ├── RequestsTab.tsx                     # MOD: token re-skin (PR 1), split panel integration (PR 2), trend chart (PR 3)
│   └── TypologiesTab.tsx                   # MOD: token re-skin (PR 1)
│
└── components/admin/ai/
    ├── AiTopBand.tsx                       # NEW (PR 1)
    ├── AiCrumbs.tsx                        # NEW (PR 1)
    ├── AiTopActions.tsx                    # NEW (PR 1)
    ├── QueryDrillPanel.tsx                 # NEW (PR 2)
    ├── useAiQueryDrill.ts                  # NEW (PR 2)
    ├── AiTrendChart.tsx                    # NEW (PR 3)
    ├── useAiMetricsRange.ts                # NEW (PR 3)
    ├── LatencyBreakdownBar.tsx             # NEW (PR 4)
    └── __tests__/
        ├── AiTopBand.test.tsx              # NEW (PR 1)
        ├── AiCrumbs.test.tsx               # NEW (PR 1)
        ├── QueryDrillPanel.test.tsx        # NEW (PR 2)
        ├── AiTrendChart.test.tsx           # NEW (PR 3)
        └── LatencyBreakdownBar.test.tsx    # NEW (PR 4)
```

## Task decomposition

### PR 1/4 — Layout + token re-skin

- [ ] **Task 1.1** — `AiTopBand.test.tsx` (RED): renders h1 "AI & Agents" + crumb attivo per `?tab=`.
- [ ] **Task 1.2** — `AiTopBand.tsx` + `AiCrumbs.tsx` + `AiTopActions.tsx` (GREEN).
- [ ] **Task 1.3** — `layout.tsx` con `<AiTopBand />` wrapper.
- [ ] **Task 1.4** — `page.tsx`: rimuovere `<h1>` + descrizione locale (assorbita).
- [ ] **Task 1.5** — Token re-skin 9 tab: regex sweep `bg-(amber|rose|violet|emerald|blue)-` → `bg-entity-*`, `text-amber-600` → `text-entity-toolkit`, etc. **Manual review tab-by-tab** (no codemod cieco).
- [ ] **Task 1.6** — `pnpm lint:tokens` + `pnpm typecheck` + `pnpm test src/app/admin/(dashboard)/ai` verdi.
- [ ] **Task 1.7** — Commit `feat(admin-ai): #1722 PR 1/4 — AiTopBand layout + token re-skin` + push + PR a `main-dev`.

### PR 2/4 — QueryDrillPanel

- [ ] **Task 2.1** — `useAiQueryDrill.test.tsx` (RED): hook ritorna query data per id, fallback empty se non trovata.
- [ ] **Task 2.2** — `useAiQueryDrill.ts` (GREEN): wrapper su `getAiRequests` con client filter.
- [ ] **Task 2.3** — `QueryDrillPanel.test.tsx` (RED): renders chunks list o empty state, close button.
- [ ] **Task 2.4** — `QueryDrillPanel.tsx` (GREEN): split right 42% sticky.
- [ ] **Task 2.5** — Integrazione `RequestsTab`: split 58/42 quando `queryId` in URL. Click su row → setter URL.
- [ ] **Task 2.6** — Deep-link test: navigare `/admin/ai?tab=requests&queryId=xxx` apre panel.
- [ ] **Task 2.7** — A11y: `role="region"`, `aria-label`, focus trap.
- [ ] **Task 2.8** — Sub-issue BE `feat(admin-ai): GET /api/v1/admin/ai/queries/{id}/drill` (aperta ma non bloccante).
- [ ] **Task 2.9** — Commit + push + PR `#1722 PR 2/4`.

### PR 3/4 — AiTrendChart

- [ ] **Task 3.1** — `useAiMetricsRange.test.tsx` (RED): mappa range → days, refetch su cambio range.
- [ ] **Task 3.2** — `useAiMetricsRange.ts` (GREEN): wrapper su `getModelPerformance?days=` con polling 10s per Live.
- [ ] **Task 3.3** — `AiTrendChart.test.tsx` (RED): renders 3 path SVG, time picker, empty state.
- [ ] **Task 3.4** — `AiTrendChart.tsx` (GREEN): inline SVG, dual Y axis, time picker.
- [ ] **Task 3.5** — Integrazione `RequestsTab` (sopra tabella) + `RagTab` (sopra quick-links).
- [ ] **Task 3.6** — A11y: tabella `sr-only` per screen reader.
- [ ] **Task 3.7** — Sub-issue BE `feat(admin-ai): GET /api/v1/admin/ai/metrics?range=` (non bloccante).
- [ ] **Task 3.8** — Commit + push + PR `#1722 PR 3/4`.

### PR 4/4 — LatencyBreakdownBar + E2E + docs

- [ ] **Task 4.1** — `LatencyBreakdownBar.test.tsx` (RED): renders 4 segmenti proporzionali, fallback se breakdown null.
- [ ] **Task 4.2** — `LatencyBreakdownBar.tsx` (GREEN): adapter su `WaterfallChart` o re-impl (decisione in task).
- [ ] **Task 4.3** — Integrazione in `QueryDrillPanel`: bar sopra chunks list.
- [ ] **Task 4.4** — E2E `tests/e2e/admin/ai-drill.spec.ts`: navigate → click row → assert panel + bar + chunks.
- [ ] **Task 4.5** — Docs update: `ADMIN_AUDIT.md` (A4 → ✅ full), `SCREENS.md` (A4 row).
- [ ] **Task 4.6** — Commit + push + PR `#1722 PR 4/4`.
- [ ] **Task 4.7** — Issue close + memory entry (se rilevante).

## Gates per ogni PR

Before push:
- [ ] `pnpm typecheck`
- [ ] `pnpm lint` (almeno sui file modificati)
- [ ] `pnpm lint:tokens`
- [ ] Test del PR verdi + sweep cluster (`pnpm vitest run src/components/admin/ai`)
- [ ] Manual: avvia `pnpm dev`, naviga `/admin/ai` con tab modificato

Before merge:
- [ ] Approval reviewer
- [ ] CI green (frontend ci.yml)
- [ ] No regressioni in test sibling (admin-kb, admin-monitor)

## Estimate

- PR 1: ~3-4h (re-skin 9 tab + layout).
- PR 2: ~4-6h (drill panel + integration + a11y + tests).
- PR 3: ~4-6h (SVG chart + range picker + integration).
- PR 4: ~2-3h (bar + integration + E2E + docs).
- **Total: ~13-19h** (2-3 dev-day).

## Open questions

Nessuna che blocchi l'avvio. D-residue tutte risolte (vedi design §5). Se BE drill/metrics endpoints risultano disponibili in corso d'opera, swap fallback → endpoint reale senza riaprire spec.
