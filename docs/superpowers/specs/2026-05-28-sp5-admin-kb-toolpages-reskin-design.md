# SP5 F3-FU-3 — KB Tool-Pages Re-skin Design

**Issue**: [#1652](https://github.com/meepleAi-app/meepleai-monorepo/issues/1652) (P2, area/frontend)
**Parent epic**: SP5 Admin Console integration (F3.1 Explorer mergiato in PR #1649 `1e03aaa4d`)
**Branch**: `feature/issue-1652-kb-toolpages-reskin` (parent `main-dev`)
**WIP context**: `~/.claude/.../memory/project_sp5_admin_f3_fu3_kb_toolpages_wip.md`

## 1. Cosa fa F3-FU-3 (e cosa non fa)

Re-skin **className/CSS-only** delle 7 KB tool-page del runtime Next.js per allinearle ai mockup `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-*.html`. **Zero refactor logica**: handler, query, hook, contratti API rimangono identici. Le suite test comportamentali esistenti sono il gate.

**In scope** (un'unica branch/PR):
- Banda di sezione condivisa nel layout `/admin/knowledge-base/layout.tsx` (header h1 "Knowledge Base" + crumbs dinamiche + search ⌘K + + Upload PDF).
- Rimozione h1 locali da 7 page/component (assorbiti dalla banda).
- Re-skin del body di 7 surface: vectors, queue, pipeline (2 file componente), embedding, feedback, settings, snapshots.
- Cleanup di 4 `it.skip` con TODO F3-FU-3 in `kb-hub-gaps.test.tsx`.

**Out of scope** (tracked separatamente):
- Funzionalità nuove: ingestion log #1650, used-by tab agents #1651, azioni avanzate + similarity-search #1653, PDF preview #1654, badge count sub-nav wired #1655.
- Refactor handler/query/repo. Aggiunte API. Cambi rotte.
- Visual-regression gate (rimosso 2026-05-20). Sostituto: manual designer review su PR + ESLint `local/no-hardcoded-color-utility` verde + behavioral test verdi.

## 2. Decisione headline — Header semantics

**Problema**: l'issue originale richiedeva "remove local h1 from sub-pages", ma ogni tool-page ha un titolo proprio (Vector Collections, Processing Queue, …). Rimuovere senza compensare = perdita di identità.

**Soluzione**: il layout possiede la banda di sezione condivisa con `h1 "Knowledge Base"` (entità). Le sub-page **NON ripetono un h1**; l'identità della pagina è espressa dalla **sub-nav tab attiva** + dalle **crumbs dinamiche** (es. `Admin · KB · Vector Collections · 9 indici · pgvector 0.7.4 · 18.487 vectors`). Se una pagina necessita un titolo interno per una sotto-sezione, usa `<h3 class="admin-panel-head">`, non un h1 grande.

Rationale:
- Allineato ai mockup: ogni `sp5-admin-kb-*.html` ha la stessa `<header class="admin-top">` con h1 "Knowledge Base" + crumbs page-specific.
- Accessibility: 1 h1 per page (l'h1 della banda), gerarchia heading lineare.
- Sticky: la banda è `position: sticky; top: 0; z-index: 50` → resta visibile durante lo scroll, ed è il punto naturale per shortcut globali (search ⌘K, + Upload).
- F3.1 Explorer page.tsx ha attualmente un `<h1>Knowledge Base</h1>` + sottotitolo locale → da assorbire in layout band, **NO regressione**.

## 3. Inventario surface (7 tool-page)

| # | Surface | Effort | File runtime | Sezioni chiave (da mockup) |
|---|---------|--------|--------------|----------------------------|
| 1 | vectors | LOW | `apps/web/src/app/admin/(dashboard)/knowledge-base/vectors/page.tsx` (418 inline) | KPI 4× (Total Vectors / Games Indexed / Dimensions / Avg Health %) · Semantic search panel · Results expandable · Game breakdown grid. Mockup: `sp5-admin-kb-vectors.html` |
| 2 | embedding | LOW | `embedding/page.tsx` (248 inline) | Service status card · 6 throughput KPI (requests / failures / failure rate / avg duration / total duration / chars). Mockup: `sp5-admin-kb-embedding.html`. **30s refetch invariato.** |
| 3 | feedback | LOW | `apps/web/src/components/admin/knowledge-base/kb-feedback-panel.tsx` | gameId input · outcome filter · list (Utile/Non utile) · pagination. Italian labels invariate. Mockup: `sp5-admin-kb-feedback.html` |
| 4 | snapshots | MEDIUM | `snapshots/page.tsx` (399 inline) | Mockup esistente `sp5-admin-rag-backup.html` (NON ri-generato in questo round). |
| 5 | settings | MEDIUM | `kb-settings.tsx` | 6-card grid read-only (Embedding / Vector DB / Chunking / Cache / Reranker / Storage) · Danger Zone (Rebuild Index, Clear Cache, typed-confirm). Mockup: `sp5-admin-kb-settings.html` |
| 6 | queue | MEDIUM | `components/queue-dashboard-client.tsx` (SSE live) | Alert banner · Control bar (workers, backpressure, drain) · KPI strip · Bulk+filter · 2-col list+detail (40/60) · MetricsDashboard. Mockup: `sp5-admin-kb-queue.html`. **SSE logic invariata.** |
| 7 | pipeline | MED-HIGH | `processing-metrics.tsx` + `rag-pipeline-flow.tsx` | 30s refetch. Stage flow 5 box (Ingest→Extract→Chunk→Embed→Index) · Health summary · Distribution stat · Metric percentile table. Mockup: `sp5-admin-kb-pipeline.html`. **2 file componente.** |

**Ordine di esecuzione**: LOW first (1-3) → MEDIUM (4-6) → MED-HIGH (7) → cleanup + PR. Ogni surface = 1 commit incrementale `refactor(admin-kb): re-skin <surface> (#1652)`.

## 4. Layout band design

### Struttura

Il layout esistente è minimal (`<div className="space-y-6 px-6"><KbSubNav />{children}</div>`). Viene esteso preservando la sub-nav F3.1:

```tsx
// apps/web/src/app/admin/(dashboard)/knowledge-base/layout.tsx
import { KbSubNav } from '@/components/admin/knowledge-base/explorer/KbSubNav';
import { KbTopBand } from '@/components/admin/knowledge-base/explorer/KbTopBand';

export default function KnowledgeBaseLayout({ children }: { children: ReactNode }) {
  return (
    <div className="kb-shell">
      <KbTopBand />
      <div className="kb-body">
        <KbSubNav />
        <div className="kb-content">{children}</div>
      </div>
    </div>
  );
}
```

### Componenti nuovi

**`KbTopBand.tsx`** (nuovo, in `apps/web/src/components/admin/knowledge-base/explorer/`):
- Render statico: `<h1>Knowledge Base</h1>` + slot `<KbCrumbs />` + slot `<KbTopActions />`.
- Sticky `top-0 z-50 backdrop-blur` (Tailwind, NO className raw `bg-white`).
- Border-bottom `border-border-light` (token-bridge).

**`KbCrumbs.tsx`** (nuovo): consuma `usePathname()` e mostra crumbs derivate dalla route:
- `/admin/knowledge-base` → `Admin · KB · Explorer · {totalDocs} docs · {totalChunks} chunks`
- `/admin/knowledge-base/vectors` → `Admin · KB · Vector Collections · {totalVectors} vectors · {dims}d`
- `/admin/knowledge-base/queue` → `Admin · KB · Processing Queue · {active} attivi · {pending} in coda · SSE live`
- … (mapping completo per 8 route)

I counts vivi (es. `totalVectors`, `active`) sono opzionali. Pattern: hook lightweight `useKbBandMeta(pathname)` che ritorna `{ pageLabel, meta?: string }` tramite query già esistenti (es. `useEmbeddingStatus`, `useVectorStats`, `useQueueStatus`). **NON refactor handler**, solo aggregazione client-side dei dati già fetched.

**`KbTopActions.tsx`** (nuovo): contiene `<KbSearchInput />` (⌘K shortcut, già implementato in F3.1 → re-use) + `<Link href="/admin/knowledge-base/upload" class="btn-admin primary">+ Upload PDF</Link>`.

### Mobile

I mockup usano `.admin-mobile-fallback "Console solo desktop"` (viewport < 880px). Il runtime admin Next.js già ha mobile-fallback al livello del `(dashboard)` segment — NO duplicazione. Verifica al task 3.

## 5. Re-skin rules (Nygard, non negoziabili)

1. **className/CSS-only**. Niente refactor di handler, hook, repository, contratti API. Se durante il re-skin emerge un bug logico genuino, **lo si traccia in nuova issue F3-FU-X**, non lo si fix in questo PR.
2. **Token semantic obbligatori**. Pattern admin DS-13c già consolidato (vedi F3.1 PR #1649). Per palette amber/zinc/emerald/rose: `eslint-disable local/no-hardcoded-color-utility` a livello **file** (NON line-level), come F3.1. Per il resto: `bg-card`, `text-foreground`, `bg-muted`, `border-border`, ecc.
3. **Pattern Tailwind v4 + admin tokens**. Il mockup usa CSS variables (`hsl(var(--c-kb))`, `var(--text-muted)`, ecc.). Il runtime usa Tailwind v4 con design-tokens-canonical + token-bridge.css. **Mapping diretto**: `.admin-kpi.e-kb { border-left: 3px solid hsl(var(--c-kb)) }` → `<div class="admin-kpi border-l-[3px] border-l-entity-kb">…</div>` (entity utilities già DS-15 stabili).
4. **Component primitives**: dove esiste un primitive (Card, Badge, …), si usa. Dove il mockup ha pattern admin-specific (`.admin-kpi`, `.admin-panel`, `.admin-tabs`, `.admin-top`), si replica la classe **come className applicata**, **NON** si crea un nuovo wrapper component "AdminKpi" in questo PR (rinviato a futuro F3-FU-X consolidation se serve).
5. **Behavioral test esistenti** = gate. Se cambia un test, deve essere perché il test cercava strutture DOM rimosse (es. h1 locale), non perché il behavior cambia.

## 6. Sketch per-surface (pseudocode → file diff)

### 6.1 vectors (LOW)

**File**: `vectors/page.tsx` (418 inline). Body attuale: KPI inline + ricerca + risultati + breakdown. Re-skin → struttura mockup:

```tsx
<div className="space-y-4">
  {/* KPI strip 4-col */}
  <div className="grid grid-cols-4 gap-3">
    <div className="admin-kpi border-l-[3px] border-l-entity-kb"> {/* totalVectors */} </div>
    <div className="admin-kpi border-l-[3px] border-l-entity-game"> {/* gamesIndexed */} </div>
    <div className="admin-kpi border-l-[3px] border-l-entity-chat"> {/* dimensions */} </div>
    <div className="admin-kpi border-l-[3px] border-l-entity-toolkit"> {/* avgHealth */} </div>
  </div>

  {/* Semantic search panel */}
  <section className="admin-panel">
    <div className="admin-panel-head"><h3>🔬 Ricerca semantica</h3>…</div>
    <div className="admin-panel-body">{/* search row + results table */}</div>
  </section>

  {/* Game breakdown panel */}
  <section className="admin-panel"><div className="admin-panel-head"><h3>📦 Vettori per gioco</h3>…</div>…</section>
</div>
```

I `.admin-kpi`, `.admin-panel`, `.admin-panel-head` sono classi del design system admin già in uso da SP5 Mockup-1 (`sp5-admin-kb.html`). Verifica che esistano nel runtime CSS (file `apps/web/src/styles/admin-base.css` o equivalente) — se mancano, le aggiungiamo nel task 3 al setup layout band.

### 6.2 embedding (LOW)

**File**: `embedding/page.tsx` (248 inline). Body attuale: service status + 6 KPI. Re-skin → 1 service status card + 6-col KPI grid (`grid grid-cols-6 gap-3`). 30s refetch invariato.

### 6.3 feedback (LOW)

**File**: `kb-feedback-panel.tsx`. Body attuale: input + filter + list + pagination. Re-skin → wrap in `admin-panel` + filter row come mockup. Italian labels invariate.

### 6.4 snapshots (MEDIUM)

**File**: `snapshots/page.tsx` (399 inline). Mockup `sp5-admin-rag-backup.html`. Backup list + restore actions + retention policy. Re-skin → admin-panel pattern + status-chip per backup state.

### 6.5 settings (MEDIUM)

**File**: `kb-settings.tsx`. 6 read-only setting card grid → `grid grid-cols-3 gap-3` (or 2 col su viewport stretto, deferred a CSS responsive del mockup). Danger Zone → `admin-panel` con border accent rosso (entity-event token).

### 6.6 queue (MEDIUM)

**File**: `queue-dashboard-client.tsx`. SSE live + 2-col layout 40/60. Alert banner top + control bar + KPI strip + bulk bar + filter chips + list/detail split. Re-skin **mantenendo SSE handler + subscriptions identici**.

### 6.7 pipeline (MED-HIGH)

**File**: `processing-metrics.tsx` + `rag-pipeline-flow.tsx`. Stage flow 5-box (Ingest→Extract→Chunk→Embed→Index) → `flex` con frecce CSS, OR `grid grid-cols-5` con border-right divider. Health summary card + metric percentile table. 30s refetch invariato.

## 7. Criteri di accettazione

**Gate hard** (PR blocker se rosso):
- [ ] `pnpm lint:tokens` → 0 violazioni (`local/no-hardcoded-color-utility` verde).
- [ ] `pnpm test` (Vitest, frontend) → tutto verde, **0 regressioni** rispetto a baseline `main-dev`.
- [ ] `pnpm typecheck` → 0 errori.
- [ ] `pnpm lint` → 0 errori (warning admiti se file-level disable documentato).
- [ ] Header decision applicato: layout band ha 1 `<h1>Knowledge Base</h1>`, sub-page hanno 0 h1.
- [ ] 4 `it.skip` con TODO F3-FU-3 in `kb-hub-gaps.test.tsx` rimossi/riscritti.
- [ ] Manual designer review (Sara) OK su PR.

**Gate soft** (verifica ma non blocker):
- [ ] 7 tool-page navigabili manuali su `http://localhost:3000/admin/knowledge-base/*`, no errori console.
- [ ] Sub-nav 8 tab attiva correttamente per ogni route.
- [ ] Crumbs cambiano per pagina (almeno page label hard-coded; counts live opzionali, fallback a label statico).
- [ ] Sticky band rimane visibile su scroll.

## 8. Out of scope esplicito

- **Funzionalità nuove**: tutte le feature follow-up SP5 F3 (#1650-#1655) restano backlog separato.
- **Refactor logica**: handler, query, hook, repo, API contracts invariati.
- **Visual-regression test**: gate rimosso 2026-05-20 (CLAUDE.md), NON re-introduced in questo PR.
- **Admin-nav.js cambio**: già revertito in setup branch (era un test scrap nei file untracked pre-clear).
- **Componenti primitive admin** (AdminKpi, AdminPanel, AdminTopBand reusable): NON estratti come primitives in questo PR. Replicati come className pattern. Estrazione = futuro F3-FU-X consolidation.
- **i18n**: labels italiane (Utile/Non utile, Pausa, Riprendi, …) invariate. NO migrazione a i18n keys.

## 9. Rischi noti

| Rischio | Mitigazione |
|---------|-------------|
| Classi `.admin-kpi`, `.admin-panel`, `.admin-tabs`, `.admin-top` non esistono ancora nel CSS runtime | Verificare al task 3 (header move). Se mancano, aggiungere come CSS nel layout component, NON come `<style>` inline. |
| Crumbs dinamiche richiedono hook che non esistono per ogni surface | Fallback a label statico hardcoded (es. "Admin · KB · Vector Collections" senza counts). Le counts vengono wired progressivamente. |
| `KbSubNav` esistente di F3.1 potrebbe avere CSS confliggente con la nuova `.admin-tabs` del mockup | Audit visivo durante task 3. Se conflitto, sovrascrivere KbSubNav className come `.admin-tabs` (NO duplicate component). |
| Test E2E che cercano `<h1>Knowledge Base</h1>` su sub-page falliranno | Aggiornare query selector: cercare nella banda, non nel children. Patch nel commit di header-move. |
| Le 30+ entry mock standalone (sp5-admin-*.html) e i 6 nuovi mock condividono `.admin-*` CSS pattern → cambio centralizzato impatta tutti i mock visivi | Mock sono asset di design, non runtime. Cambi al runtime CSS NON propagano ai mock HTML. Risk = zero. |

## 10. Riferimenti

- Issue: [#1652](https://github.com/meepleAi-app/meepleai-monorepo/issues/1652)
- Parent PR (F3.1 Explorer): `1e03aaa4d` (#1649)
- WIP memory: `~/.claude/.../memory/project_sp5_admin_f3_fu3_kb_toolpages_wip.md`
- Mockup design system: `admin-mockups/design_handoff_admin/admin/{tokens.css, components.css, admin-base.css}`
- 6 nuovi mockup: `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-{vectors,queue,pipeline,embedding,feedback,settings}.html` (commit `2a0ab1117`)
- Mockup snapshots esistente: `sp5-admin-rag-backup.html` (pre-existing)
- Prompts generation: `admin-mockups/design_handoff_admin/KB_TOOLPAGE_DESIGN_PROMPTS.md`
- Sub-nav F3.1: `apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx`
- Layout F3.1: `apps/web/src/app/admin/(dashboard)/knowledge-base/layout.tsx`
- Token system: `apps/web/src/styles/design-tokens-canonical.css` + `token-bridge.css`
- ESLint rule: `local/no-hardcoded-color-utility` (mode error since DS-15)
- Visual gate removal: CLAUDE.md "Visual Gate REMOVED 2026-05-20"

---

## ADDENDUM — correzioni post-recon (2026-05-28)

3 agent di ricognizione hanno verificato le assunzioni del doc. Correzioni che prevalgono sulle sezioni sopra:

### A1. CSS classes `.admin-*` NON esistono nel runtime (era rischio #9.1, ora confermato)
TUTTE le classi del mockup (`.admin-kpi`, `.admin-panel`, `.admin-panel-head/body`, `.admin-tabs`, `.admin-tab`, `.admin-top`, `.btn-admin`, `.status-chip`, `.admin-search`, `.admin-form-row`, `.admin-input`, `.admin-select`) sono **mockup-only**. Il runtime usa **Tailwind utilities** (pattern F3.1 `KbSubNav`).

**DECISIONE UTENTE (2026-05-28)**: re-skin con **Tailwind utilities inline** (NO file CSS `.admin-*`, NO nuovi componenti primitives). Coerente DS-16 + ESLint + F3.1. §4 e §5.4 del doc sopra sono **superate** da questa decisione: i mockup HTML sono il riferimento *visivo*, tradotto in utility Tailwind, NON copiato come classi.

### A2. `token-bridge.css` RIMOSSO in DS-16 (§10 era errato)
Non esiste più. I token vivono in `src/styles/design-tokens-canonical.css` (`:root` + `[data-theme="dark"]`) e le utility entity sono generate via `@theme` in globals. **Niente riferimenti a token-bridge nel codice nuovo.**

### A3. Token mapping mockup → Tailwind (verificato uso reale)
| Mockup CSS | Tailwind runtime | Note |
|---|---|---|
| `var(--f-display)` (Quicksand) | `font-quicksand` | headings |
| `var(--f-mono)` (JetBrains) | `font-mono` | KPI label, valori, ID |
| `var(--f-body)` (Nunito) | *(default)* | nessuna classe |
| `var(--text)` | `text-foreground` | + opz. `dark:text-zinc-100` |
| `var(--text-muted)` | `text-muted-foreground` | |
| `var(--bg-card)` | `bg-card` | pattern `bg-card/70 dark:bg-zinc-800/70 backdrop-blur-md` |
| `var(--bg-muted)` | `bg-muted` | |
| `var(--border-light)` | `border-border/60` | NON esiste `border-border-light` utility |
| `hsl(var(--c-kb))` | `text-entity-kb` / `bg-entity-kb` / `border-entity-kb` | + opacity `/12` `/30` |
| `.admin-kpi.e-kb` border-left | `border-l-4 border-l-entity-kb` | accento entità |
| `.status-chip.healthy` | `bg-entity-toolkit/12 text-entity-toolkit` | success=toolkit green |
| `.status-chip.danger` | `bg-entity-event/12 text-entity-event` | danger=event rose |

`dark:bg-zinc-800/70`, `dark:text-zinc-100`, `dark:border-zinc-700/60` ⇒ richiedono `eslint-disable local/no-hardcoded-color-utility` a livello file (pattern admin F3.1, zinc palette).

### A4. Header decision — h1 reali sono DIVERSI da "Knowledge Base"
Ogni surface ha un h1/h2 proprio (NON "Knowledge Base"):
| Surface | Heading attuale | Tag | File:linea | Ha test che lo asserisce? |
|---|---|---|---|---|
| vectors | "Vector Store" | h1 | `vectors/page.tsx` | **SÌ** — `vector-collections.test.tsx:101` `getByText('Vector Store')` |
| embedding | "Embedding Service" | h1 | `embedding/page.tsx` | no |
| feedback | "Feedback KB Utenti" | h2 (in parent page) | `feedback/page.tsx:15` | no |
| snapshots | "Snapshot RAG" | h1 | `snapshots/page.tsx:235` | no |
| settings | "Knowledge Base Settings" | h1 (in wrapper) | `settings/page.tsx:15` | no |
| queue | "Processing Queue" | h1 | `queue/components/queue-dashboard-client.tsx:92` | no |
| pipeline | "RAG Pipeline Overview" | h1 (wrapper) | `pipeline/page.tsx:16` | no |

**Header-move rivisto**: la banda layout ottiene `<h1>Knowledge Base</h1>`. Le sub-page perdono il loro **h1** (diventa al più un `<h2>`/heading di sezione, o sparisce assorbito nelle crumbs/sub-nav). L'unico test impattato è `vector-collections.test.tsx:101` → aggiornare l'assertion (cercare la nuova posizione del titolo, es. crumbs o tab attivo). È un cambio legittimo: il test cercava l'h1 locale che il design rimuove.

### A5. Contenuti testuali INVARIATI (regola rafforzata)
Il re-skin cambia **className + struttura visiva**, NON il testo dei contenuti. Dove il mockup ha IT (`Ricerca semantica`) e il runtime EN (`Semantic Search`), **prevale il runtime** per non rompere i 5 test `VectorStorePage` in `kb-hub-gaps.test.tsx` che asseriscono il heading "Semantic Search". Traduzione IT = follow-up separato (i18n + test). Il mockup guida l'aspetto, non le stringhe.

### A6. Path reali corretti (§3 aveva 1 path errato)
- queue dashboard: `apps/web/src/app/admin/(dashboard)/knowledge-base/queue/components/queue-dashboard-client.tsx` (159 righe, NON `components/admin/...`). SSE via `useQueueSSE`/`useJobSSE` (hooks in `queue/hooks/`), sub-componenti `QueueAlertsBanner`/`QueueControlBar`/`QueueList`/`JobDetailPanel`/`MetricsDashboard`. **Il re-skin del client wrapper tocca solo lo scaffold (header, grid 2-col, spacing); i sub-componenti hanno scope proprio** — valutare per-componente nel task.
- pipeline: 2 componenti `processing-metrics.tsx` (297) + `rag-pipeline-flow.tsx` (363), montati da `pipeline/page.tsx` (31, h1 wrapper).

### A7. Test baseline reali
- `apps/web/__tests__/admin/knowledge-base/vector-collections.test.tsx` (6 test, asserisce "Vector Store")
- `apps/web/src/app/admin/(dashboard)/knowledge-base/__tests__/kb-hub-gaps.test.tsx` (449 righe): **4 `it.skip` TODO F3-FU-3** (da rimuovere) + **VectorStorePage 5 test** (Semantic Search — restano verdi) + **RAGPipelineFlow 4 test** (Stage Drill-Down — restano verdi)
- `apps/web/__tests__/admin/queue/{queue-alerts-banner,queue-filters}.test.tsx` (5+8 test)
- embedding / feedback / settings / snapshots / processing-metrics: **nessun unit test** → gate = typecheck + lint + manual + non-regressione delle suite vicine
- Comando: da `apps/web/` → `pnpm test <path>` (vitest run). Suite admin completa: `pnpm test:admin-dashboard`.

### A8. Mobile già gestito upstream
`AdminShell` + `AdminSidebar` (`hidden lg:flex`). NESSUN mobile fallback da aggiungere nel KB layout. Il `.admin-mobile-fallback` del mockup NON va portato.

### A9. KbSubNav riusabile
`apps/web/src/components/admin/knowledge-base/explorer/KbSubNav.tsx` (75 righe) già fa 8-tab + `usePathname` active (amber). Il re-skin lo **riusa**; eventuale ritocco className per allineare al look `.admin-tabs` del mockup è OK (nessun test diretto su KbSubNav). NON duplicare.

---

**Prossimo step**: writing-plans skill → genera plan task-by-task. ESEGUITO → vedi `docs/superpowers/plans/2026-05-28-sp5-admin-kb-toolpages-reskin.md`.
