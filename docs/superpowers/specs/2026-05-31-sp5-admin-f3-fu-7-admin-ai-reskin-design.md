# SP5 Admin — F3-FU-7 `/admin/ai` Full Mockup Compliance (Design)

**Date:** 2026-05-31
**Status:** draft (ready for plan-pack)
**Issue:** [#1722](https://github.com/meepleAi-app/meepleai-monorepo/issues/1722)
**Spec parent:** [`2026-05-24-sp5-admin-console-consolidation-design.md`](./2026-05-24-sp5-admin-console-consolidation-design.md) §5 (A4)
**Predecessor pattern:** PR #1664 (F3-FU-3 KB tool-pages re-skin), epic F4.1 #1718 (A8 Monitor re-skin + LiveEventLog)

---

## 1. Goal

Re-skin **completo** di `/admin/ai` per allinearlo al mockup A4 `admin-mockups/design_handoff_admin/admin/sp5-admin-ai.html`. A differenza di F3-FU-3 (KB tool-pages, re-skin Tailwind-only), il mockup A4 propone **componenti UI ex-novo** che vanno assemblati sopra primitives esistenti:

- **Split-view 58/42** sui tab `rag` e `requests` (metrics aggregate sx + query drill-down dx).
- **AiTrendChart** SVG inline (p50/p95/error con time range picker).
- **LatencyBreakdownBar** segmentata (retrieval / rerank / llm / post).
- **Chunk list con matched highlighting** dentro al drill-down.
- **AiTopBand** (banda shared layout) con h1 + crumbs + actions.

L'IA del hub (`AdminHubTabBar` + 9 tab) resta invariata — il pattern `?tab=` è già canonico (decisione spec parent §4.2).

## 2. Non-goals

- ❌ Refactor logica delle 9 tab esistenti. PR 1 è re-skin token-only (pattern F3-FU-3, niente cambio behavior).
- ❌ Nuovi domini/endpoint backend oltre la **verifica** di quelli esistenti. Se manca un campo (es. `breakdown` su singola request), tracciare in sub-task BE separato senza bloccare il PR FE.
- ❌ Path migration `/admin/rag-quality` → `/admin/ai?tab=rag` (D-residue): redirect 308 va fatto in **un PR separato** post-merge della suite, per non legare la migration FE al re-skin (rollback granulare).
- ❌ Visual regression gate (rimosso 2026-05-20). Sostituto: behavioral test + designer review.
- ❌ Content moderation tab. Fuori scope F3-FU-* (rimane backlog).

## 3. Contesto verificato (audit 2026-05-31)

### 3.1 FE attuale `/admin/ai`

- `apps/web/src/app/admin/(dashboard)/ai/page.tsx` (159 LOC) — root: `AdminHubTabBar` + `<Suspense>` per 9 tab, header h1 locale.
- 9 tab presenti in `apps/web/src/app/admin/(dashboard)/ai/*.tsx`:

  | Tab | LOC | Pattern attuale |
  |-----|-----|-----------------|
  | `AgentsTab` | 100 | List/CRUD |
  | `AiLabTab` | 51 | Placeholder/explorer |
  | `DefinitionsTab` | 52 | List basic |
  | `LlmConfigTab` | 467 | Config form complesso |
  | `ModelsTab` | 119 | List |
  | `PromptsTab` | 117 | List/CRUD |
  | `RagTab` | 64 | **Quick-links hub** a `/admin/agents/{pipeline,debug,strategy,…}` — niente metrics né drill |
  | `RequestsTab` | 194 | Tabella con paginazione, niente drill panel |
  | `TypologiesTab` | 140 | List |

- **Layout assente:** `apps/web/src/app/admin/(dashboard)/ai/layout.tsx` non esiste. Da creare per la banda condivisa.

### 3.2 Primitives disponibili

Verifica codice (grep):

| Componente | Path | Stato |
|-----------|------|-------|
| `RetrievedChunkCard` | `apps/web/src/components/admin/sandbox/RetrievedChunkCard.tsx` | ✅ esiste — riusabile per chunk list |
| `WaterfallChart` | `apps/web/src/components/admin/rag/WaterfallChart.tsx` | ✅ esiste — adapter per `LatencyBreakdownBar` |
| `DebugSidePanel` | `apps/web/src/components/admin/sandbox/DebugSidePanel.tsx` | ✅ esiste — pattern di riferimento per `QueryDrillPanel` (l'audit citava `QueryDrillDown` ma il componente non esiste con quel nome; la primitives effettiva è `DebugSidePanel`) |

⚠️ **Drift dall'audit:** l'issue cita `QueryDrillDown` come primitives disponibile, ma il grep non lo trova. Si usa `DebugSidePanel` come riferimento di pattern (split panel sticky right) — quasi-equivalente.

### 3.3 Backend endpoints

Verifica `apps/web/src/lib/api/clients/admin/adminAnalyticsClient.ts` + `adminAiClient.ts`:

| Endpoint | Status | Coperture mockup |
|----------|--------|-------------------|
| `GET /api/v1/admin/requests?page=&pageSize=&startDate=&endDate=&status=&endpoint=` | ✅ esiste | Lista per RequestsTab. Manca payload chunks/breakdown per drill. |
| `GET /api/v1/admin/model-performance?days=N` | ✅ esiste | Source per AiTrendChart (p50/p95/error per giorno). Granularity solo daily → 1h/Live non coperti. |
| `GET /api/v1/admin/ai-models` (+ varianti `toggle`, `configure`, ...) | ✅ esiste | Models/Config tab — non tocca questa feature. |
| `GET /api/v1/admin/ai/queries/{id}/drill` | ❌ **non esiste** | Drill-down dettagliato (retrieved chunks + breakdown per single query). |
| `GET /api/v1/admin/ai/metrics?range=` | ❌ **non esiste** | Trend con range arbitrari (Live · 1h · 24h · 7d). |

**Decisione:** PR 2/3 implementano FE su **dati esistenti** (`getAiRequests` per drill base, `getModelPerformance?days=` per trend) con fallback graceful per range non coperti (es. "1h/Live → daily approssimato + badge `approx`"). Endpoint nuovi (drill ricco, metrics granulari) sono **sub-task BE** a parte, da aprire in coda alla suite.

## 4. Strategia: 1 issue epic + 4 PR stacked

Branch: `feature/issue-1722-f3-fu-7-admin-ai-reskin`. Pattern S1/S2/S3 e F4.1 #1718. Ogni PR rebase-mergeable in autonomia, rollback granulare.

### PR 1/4 — `AiTopBand` layout + 9 tab token re-skin

**Scope:**
- Creare `apps/web/src/app/admin/(dashboard)/ai/layout.tsx` con `<AiTopBand />` (h1 "AI & Agents" + crumbs dinamici + actions placeholder).
- Creare `apps/web/src/components/admin/ai/{AiTopBand,AiCrumbs,AiTopActions}.tsx` (pattern speculare a `KbTopBand` / `KbCrumbs` di PR #1664).
- Rimuovere `<h1>` locale da `page.tsx` (assorbito da `AiTopBand`).
- Re-skin Tailwind tokens delle 9 tab — **className-only**, niente cambi di logica:
  - Sostituire `bg-card/40 dark:bg-zinc-800/40` → `bg-card/60` (uniformare).
  - Sostituire color utility hardcoded (`text-amber-600`, `text-rose-600`, `text-violet-600`, `text-emerald-600`, ...) con `text-entity-*` token (eslint `local/no-hardcoded-color-utility` deve essere verde).
  - Spacing/density allineato al mockup `sp5-admin-ai.html`.
- Aggiornare test esistenti (`__tests__/page.test.tsx`, eventuali tabs tests) se mutazioni rompono assertions su `<h1>` o classi.

**DoD (Wiegers — testabile):**
- `pnpm typecheck` 0 errori, `pnpm lint` 0 warning sul perimetro modificato.
- `pnpm lint:tokens` 0 violations nei file `ai/**`.
- `AiTopBand` rende `<h1>AI & Agents</h1>` (test).
- Crumbs cambiano in base a `?tab=` (test sui 3 tab più rappresentativi: agents/rag/requests).
- Tab test esistenti restano verdi.

### PR 2/4 — `QueryDrillPanel` split panel

**Scope:**
- Creare `apps/web/src/components/admin/ai/QueryDrillPanel.tsx` — split right 42% sticky.
- Riusa `RetrievedChunkCard` per la lista chunks (se la singola query ha chunks attached) + fallback se assenti.
- Pattern URL: `?tab=requests&queryId={id}` (state in URL per deep-link).
- Hook `useAiQueryDrill(queryId)` su `getAiRequests` (filtro per id, **client-side enrichment**: non blocca su endpoint mancante).
- Integrazione: `RequestsTab` diventa split 58/42 quando `queryId` presente (click su riga → set `queryId`).
- Pattern simile applicato a `RagTab` quando rilevante (ma `RagTab` è hub di quick-links → valutare se split fa senso o se basta tab linkato).

**DoD:**
- Click su riga RequestsTab apre il panel con query id selezionato.
- URL aggiornato con `?queryId=...` (deep-link verificato).
- Close panel pulisce `queryId`.
- A11y: `role="region"` + `aria-label="Query drill-down"`.
- Test unit: drill panel renders chunks o empty-state.
- Endpoint nuovo `/ai/queries/{id}/drill` — se mancante, **tracciato in #1722 sub-task BE**, FE funziona con dati esistenti + badge "limited drill".

### PR 3/4 — `AiTrendChart` SVG p50/p95/error

**Scope:**
- Creare `apps/web/src/components/admin/ai/AiTrendChart.tsx` (SVG inline, niente dipendenze).
- 3 line plot: p50 / p95 / error rate. Asse X temporale, Y dual (latency ms su sx, error % su dx).
- Time range picker: `Live` · `1h` · `24h` · `7d` (state in URL `?range=`).
- Source: `getModelPerformance?days=N` (mapping `7d=7`, `24h=1`, `1h=1` con badge approx, `Live=1` + polling 10s).
- Posizionato in `RagTab` (sopra ai quick-links attuali) e/o `RequestsTab` (sopra alla tabella).
- Empty state se 0 datapoints.

**DoD:**
- Renders 3 line plot per i 4 range con dati mock di test.
- Toggle range cambia query key e refetch.
- A11y: `role="img"` + `aria-label="Latency trend (p50/p95/error)"` + dati tabulati in `<table className="sr-only">` per screen reader.
- Endpoint granulare nuovo — **sub-task BE** tracciato.

### PR 4/4 — `LatencyBreakdownBar` + integration + E2E

**Scope:**
- Creare `apps/web/src/components/admin/ai/LatencyBreakdownBar.tsx`: barra orizzontale segmentata 4 step (retrieval / rerank / llm / post), proporzionale alla latency totale.
- Pattern: adapter sopra `WaterfallChart` esistente (o componente nuovo se l'adapter è più complesso del re-impl da 30 LOC).
- Integrazione dentro `QueryDrillPanel` (PR 2/4): mostrato sopra alla chunk list quando la query ha breakdown.
- Fallback graceful: se breakdown mancante (probabile su endpoint attuale), mostra "breakdown unavailable" + total latency only.
- E2E smoke: navigare `/admin/ai?tab=requests`, click su prima riga, verificare panel apre + breakdown bar/chunks visibili.
- Docs post-merge:
  - Aggiornare `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md` (A4 stato → `✅ full`).
  - Aggiornare `admin-mockups/design_handoff_admin/SCREENS.md` (A4 row).
  - Aggiungere memory entry se emerso pattern interessante.

**DoD:**
- LatencyBreakdownBar renders 4 segmenti con proporzioni corrette (test deterministico con totale 1000ms breakdown 200/100/600/100).
- Integrazione QueryDrillPanel: breakdown visibile sopra chunks.
- E2E `tests/e2e/admin/ai-drill.spec.ts` verde.
- ADMIN_AUDIT.md aggiornato.

## 5. Decisioni di progettazione (D-residue)

| ID | Decisione | Impatto |
|----|-----------|---------|
| D-1 | Time range Live = polling 10s su `days=1` (no SSE in questa iterazione). | PR 3 self-contained, nessun BE work. |
| D-2 | Drill panel SOLO su `RequestsTab` in PR 2. Estensione a `RagTab` valutata in coda — `RagTab` oggi è quick-links hub, drill non ha senso prima di trasformare anche quel tab in tabella. | Riduce scope PR 2. Future: spin-out se utile. |
| D-3 | Path migration `/admin/rag-quality → /admin/ai?tab=rag` (spec parent §4.2): **NON in F3-FU-7**, spin-out in issue separata. | Garantisce rollback granulare; redirect 308 è high-blast-radius e merita un PR dedicato. |
| D-4 | Sub-task BE separati per: `GET /ai/queries/{id}/drill`, `GET /ai/metrics?range=`. Aperti come issue figlie di #1722 dopo merge PR 4/4 (per non bloccare il merge FE). | FE consegna in autonomia; BE in ondata successiva. |
| D-5 | E2E suite scelta: solo smoke (1 test in PR 4/4). Non si aggiungono Playwright fixture pesanti — ridurre flake. | Coerente con cancellazione visual gate 2026-05-20. |

## 6. Risk register (Nygard)

| Rischio | Probabilità | Impatto | Mitigazione |
|---------|-------------|---------|-------------|
| Endpoint drill mancante blocca PR 2 | Alta | Medio | Fallback "limited drill" senza chunks (PR 2 funziona comunque). |
| LlmConfigTab (467 LOC) refactor accidentale | Bassa | Alta | PR 1 className-only review checklist: vietato cambiare hooks/state. |
| WaterfallChart adapter più complesso del previsto | Media | Basso | PR 4 valuta re-impl 30 LOC se adapter > 50 LOC. |
| Time range picker introduce flicker UX | Bassa | Basso | Skeleton stato loading + delay debounce 200ms. |
| `?queryId=` collision con altri filtri URL | Bassa | Medio | Namespace deep-link: prefix `?q.id=` se necessario. |

## 7. Riferimenti

- **Mockup:** `admin-mockups/design_handoff_admin/admin/sp5-admin-ai.html` (25KB, PR #1493 handoff)
- **Audit:** `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md` §7 (primitives disponibili)
- **Spec parent:** `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` §4.2 / §5 (A4)
- **Pattern PR:** #1664 (F3-FU-3 KB re-skin), #1718 (F4.1 Monitor — 4 PR stacked)
- **Hub IA:** Issue #5040 (consolidamento route), #5048 (AI hub migration)
