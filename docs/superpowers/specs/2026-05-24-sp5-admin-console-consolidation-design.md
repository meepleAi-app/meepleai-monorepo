# SP5 Admin Console — Consolidamento IA + Re-skin (Design)

**Date:** 2026-05-24
**Status:** draft (pending user review)
**Scope:** Integrazione dei mockup admin SP5 nel codebase MeepleAI **non** come rebuild greenfield, ma come (a) consolidamento dell'Information Architecture admin esistente e (b) re-skin verso il look-and-feel SP5, sopra i ~220 componenti admin e i ~90 `page.tsx` già in produzione. Output di questo documento: il design da cui derivare i plan d'implementazione (`writing-plans`). Nessun codice di prodotto è toccato in questa fase.
**Predecessors:**
- `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md` — audit read-only del codebase vs handoff (verdetto: non-greenfield, handoff sovrastima, 10 decisioni aperte R1-R10).
- `admin-mockups/design_handoff_admin/SCREENS.md` — inventario 18 mockup SP5 + 13 surplus, route attese, endpoint, priorità.
- `admin-mockups/design_handoff_admin/SURPLUS_DESIGN_PROMPTS.md` — prompt-pack + inventario dei 13 mockup "surplus" (funzioni admin reali prive di mockup SP5).
- Colloquio decisioni **R1-R10** (2026-05-24) — tutte risolte; sintetizzate in §3.

---

## 1. Goal

Trasformare l'admin console attuale — funzionalmente ricca ma con **Information Architecture frammentata** (3 pattern di routing coesistenti, funzioni raggiungibili da 2-3 path, ~90 `page.tsx` ma ~20 voci in nav) — in una console **consolidata e ri-skinnata**:

1. **Un path canonico per funzione**, con redirect dai duplicati (decisione R7).
2. **Una sidebar unica a 4 gruppi (A/B/C/D)**, filtrata per ruolo, sostituendo il drawer off-canvas + nav curata attuale.
3. **Re-skin SP5**: token canonici, shell sidebar-persistente (desktop ≥lg) + drawer (mobile), dark default scoped `/admin/*`, density alta.
4. **Riuso massimo** dei componenti esistenti (~33-36 dei 38 "nuovi" del handoff già coperti), creando solo il delta reale.

Il valore: ridurre il carico cognitivo di navigazione, dare coerenza visiva, e preparare il terreno per le ondate di feature senza moltiplicare la confusione di IA (rischio R7 esplicito nell'audit).

## 2. Non-goals

- ❌ **Rebuild greenfield.** Il handoff lo presume; l'audit lo smentisce. Si lavora sul **delta**.
- ❌ **Enforcement dell'hardening di sicurezza.** Step-up 2FA strict, schema audit esteso, impersonate token 15min + claim actor, disambiguazione doppia entity `AuditLog` → **track separato** (§7), non bloccante per il re-skin. In questa fase: solo UI-ready dove serve (R5).
- ❌ **B8 Dev-tools** (`/dev`, scenario/auth-switcher/store-inspector): concetto SP5 assente nel prodotto, P3 dev-only → fuori scope di questa iterazione.
- ❌ **`MobileFallback` "desktop-only gate"**: la shell è responsive (sidebar↔drawer), non serve un gate desktop-only (decisione colloquio: shell ibrida).
- ❌ **Normalizzazione del backend** verso le convenzioni del handoff (`/api/admin`, PATCH, cursor, `/me/...`). Il FE si allinea ai **path reali** (`/api/v1`, PUT, page/limit, `/play-records`); il BE resta as-is (R8).
- ❌ **Content Moderation backend** (gruppo 0% nell'audit). La schermata A3 resta `🟡 partial` finché il BC moderation non esiste; non lo costruiamo in questa iterazione (vedi §5/§9, ondata dedicata o backlog).
- ❌ Modifica del modello ruoli: si **mantengono i 5 ruoli reali** (`user/editor/creator/admin/superadmin`); `premium` resta `UserTier` (R1).

## 3. Decisioni di contesto (fondamenta già fissate)

Sintesi del colloquio R1-R10 (dettaglio e razionale in `ADMIN_AUDIT.md §9`). Sono **input vincolanti** di questo design, non più aperte:

| Rif | Decisione | Effetto sul design |
|-----|-----------|--------------------|
| **R1** | `premium` = `UserTier`, non ruolo | Matrice RBAC su 5 ruoli reali; nessun 4° ruolo `premium`. |
| **R2** | `editor`/`creator` = accesso parziale curato | Sidebar **filtra le voci per ruolo** (§4.4). |
| **R3** | Audit as-is ora | Schema audit esteso + disambiguazione doppia entity → track sicurezza (§7). |
| **R4** | Impersonate riusa as-is | Hardening token 15min + claim actor → track sicurezza (§7). Banner UI può usare `SessionStatusDto`. |
| **R5** | Step-up UI-ready ora, enforcement dopo | Si predispone la UI del modal step-up; il behavior resta shadow finché §7 non lo rende strict. |
| **R7** | Sidebar 4 gruppi A/B/C/D + duplicati → canonico + redirect | Cuore del consolidamento (§4.1, §4.2). |
| **R8** | FE segue i path reali | Contratto API in §8; nessuna modifica BE. |
| **R9** | Dark default scoped admin | `[data-theme="dark"]` applicato solo sotto `/admin/*` (§4.3). |
| **R10** | Sequenza fondamenta → pilota (A1→A2) → ondate; sicurezza track separato | Rollout in §9. |

## 4. Architettura del consolidamento

> **Principio guida (input utente 2026-05-24): l'estetica prima della struttura.** Dei mockup SP5/surplus conta la **parte estetica da mantenere** — palette/token, layout visivo, density, trattamento visivo dei componenti. L'**Information Architecture e il routing** possono invece **divergere dal mockup** quando il consolidamento lo richiede (es. hub `?tab=` al posto di voci top-level separate). Questo principio risolve D-1 e governa tutte le scelte di §4: re-skin fedele al look, IA libera di consolidare.

### 4.1 Information Architecture — sidebar a 4 gruppi

31 schermate totali (18 SP5 + 13 surplus) organizzate in 4 gruppi di navigazione. La sorgente di verità della nav è `admin-mockups/design_handoff_admin/admin-nav.js` (già esteso con i gruppi C/D).

| Gruppo | Etichetta nav | Schermate | Focus |
|--------|---------------|-----------|-------|
| **A** | Admin Console | A1-A9 (10) | Operatività quotidiana: overview, utenti, moderazione, AI/RAG, KB, catalog, config, monitor, notifiche |
| **B** | Power-User Tools | B1-B7 (7, B8 escluso) | Editing avanzato: editor, pipeline, n8n, upload, play-records, versions, private-games |
| **C** | Platform & Operations | C1-C7 (7) | Infra/ops: containers, database-sync, providers, emergency, budget, secrets, alerts |
| **D** | AI Tooling & Data Quality | D1-D6 (6) | Strumenti AI: mechanic-extractor, sandbox, A/B test, prompts, rag-backup, integrations |

### 4.2 Path canonici + redirect (de-duplicazione IA)

**Principio (R7):** ogni funzione ha **un solo path canonico**; i path alternativi diventano **redirect** (Next.js `redirect()` o `next.config.js` `redirects`). Casi di duplicazione noti dall'audit §6 e canonico proposto:

| Funzione | Path oggi (duplicati) | Canonico proposto | Redirect da → a |
|----------|-----------------------|-------------------|-----------------|
| AI/RAG quality (A4) | `/admin/ai?tab=rag` · `/admin/agents/inspector` · `/admin/rag-quality` | `/admin/ai` (hub, tab `rag`/`agents`/`quality`) | `agents/inspector` → `ai?tab=agents`; `rag-quality` → `ai?tab=rag` |
| KB management (A5) | `/admin/content?tab=kb` · `/admin/knowledge-base` | `/admin/knowledge-base` | `content?tab=kb` → `knowledge-base` |
| Content (A3) | `/admin/content` (oggi = gestione giochi/KB, **non** moderation) | `/admin/content` per moderation; gestione-giochi resta sotto i propri path | n/a (A3 partial finché BC moderation assente) |

> **Decisione presa (input utente 2026-05-24):** per A4 si adotta l'**hub `?tab=` esistente** come canonico (pattern dominante, 9 tab già implementati), con redirect dai duplicati — **non** si moltiplicano le voci top-level del mockup. Razionale: conta l'estetica, non la struttura nav (principio §4). I restanti deconflitti path (B1/B2/B6 fuori da `/admin`, vedi §5) si risolvono **per ondata** nei rispettivi plan, applicando lo stesso principio canonico+redirect.

### 4.3 Shell + re-skin

- **Shell ibrida** (decisione colloquio): **sidebar persistente** su desktop (≥lg, breakpoint Tailwind) + **drawer off-canvas** su mobile. Punto di partenza: `apps/web/src/components/layout/AdminShell/AdminShell.tsx` (oggi `TopBar` + `AdminSideDrawer` overlay). Si estende `AdminSideDrawer` a comportamento responsive sidebar↔drawer, **non** si crea una shell nuova.
- **Token**: solo token canonici (`bg-background`, `bg-card`, `bg-muted`, `text-foreground`, `border-border`, entity `bg-entity-*`). Vietato hardcoded color utility (ESLint `local/no-hardcoded-color-utility`, mode error). I mockup SP5/surplus usano già una palette solo-token (`tokens.css` + `admin-base.css`), allineata.
- **Dark default scoped** (R9): `[data-theme="dark"]` forzato sotto `/admin/*` (es. nel layout admin), mantenendo il **default light** del resto del prodotto. Toggle utente disponibile.
- **Density alta**: spacing/typography ridotti admin-wide. Da valutare se via utility scoped o (eventuale) density system — è uno dei ~2-3 componenti realmente nuovi (§6).

### 4.4 RBAC e filtro sidebar per ruolo (R2)

- Gate backend **riusati as-is**: `RequireAdminSession()`, `RequireSuperAdminSession()`, `RequireAdminOrEditorSession()`; policy `RequireSuperAdmin`/`RequireAdminOrAbove`/`RequireEditorOrAbove`. Gerarchia `SuperAdmin(4) > Admin(3) > Editor(2) > Creator(1) > User(0)`.
- **Sidebar filtrata per ruolo**: ogni voce di nav dichiara un `minRole`; la sidebar nasconde le voci sopra il ruolo dell'utente. `editor`/`creator` vedono un sottoinsieme curato (R2). I path `superadmin`-only (es. C2 database-sync, C4 emergency, C6 secrets) restano gated sia in nav sia a livello endpoint.
- Nessun nuovo meccanismo di gate: per i nuovi endpoint admin si standardizza su quello esistente.

## 5. Mappa schermate (31) — stato FE e path canonico

Stato dall'audit §6 (FE: ✅ full · 🟡 partial · ❌ assente). Per C/D il **backend esiste** (aree admin "EXTRA" dell'audit §5); il lavoro FE è skin/consolidamento.

### Gruppo A — Admin Console

| ID | Funzione | Path canonico | Stato FE | P |
|----|----------|---------------|----------|---|
| A1 | Overview | `/admin/overview` | ✅ | P0 |
| A2 | Users + drill | `/admin/users` · `/admin/users/[id]` | 🟡 (wiring delete/impersonate da verificare) | P0 |
| A3 | Content moderation | `/admin/content` | 🟡 (BC moderation 0% → no queue) | P1 |
| A4 | AI/RAG quality | `/admin/ai` (tab) | ✅ | P0 |
| A5 | KB management | `/admin/knowledge-base` | ✅ | P0 |
| A5b | KB upload flow | `/admin/knowledge-base/upload` | ✅ | P0 |
| A6 | Catalog ingestion | `/admin/catalog-ingestion` | ✅ | P1 |
| A7 | Config/Flags | `/admin/config?tab=flags` | ✅ (dirty-bar da verificare) | P1 |
| A8 | Monitor + LiveEventLog | `/admin/monitor` | ✅ (LiveEventLog UI da assemblare) | P1 |
| A9 | Notifications | `/admin/notifications` | 🟡 (template scollegati da compose) | P2 |

### Gruppo B — Power-User Tools

| ID | Funzione | Path canonico (proposto) | Stato FE | P |
|----|----------|--------------------------|----------|---|
| B1 | Editor | `/admin/editor/[type]/[id]` (oggi fuori `/admin`, `?gameId=`) | 🟡 | P1 |
| B2 | Pipeline builder | `/admin/pipeline-builder/[id]` (oggi fuori `/admin`) | 🟡 | P2 |
| B3 | n8n | `/admin/n8n` | 🟡 (no workflow/webhook log) | P2 |
| B4 | Upload avanzato | `/admin/upload` (overlap con A5b) | 🟡 | P1 |
| B5 | Play records | `/play-records` (index mancante) | 🟡 | P2 |
| B6 | Versions | `/admin/versions/[artifact]` (oggi fuori `/admin`) | 🟡 | P1 |
| B7 | Private games | `/private-games` (index + checklist mancanti) | 🟡 | P2 |
| ~~B8~~ | ~~Dev tools~~ | — | ❌ fuori scope | P3 |

### Gruppo C — Platform & Operations (surplus)

| ID | Funzione | Path canonico | Min role | P |
|----|----------|---------------|----------|---|
| C1 | Infra/containers | `/admin/monitor/containers` | admin | P1 |
| C2 | Database-sync | `/admin/database-sync` · `/admin/staging-access` | superadmin | P1 |
| C3 | Providers | `/admin/providers` | admin (rotate=superadmin) | P0 |
| C4 | Emergency | `/admin/llm/emergency` | superadmin + step-up | P1 |
| C5 | Budget | `/admin/business` | admin | P1 |
| C6 | Secrets | `/admin/secrets` | superadmin + step-up | P1 |
| C7 | Alerts | `/admin/monitor` (tab alerts) | admin | P1 |

### Gruppo D — AI Tooling & Data Quality (surplus)

| ID | Funzione | Path canonico | Min role | P |
|----|----------|---------------|----------|---|
| D1 | Mechanic extractor | `/admin/knowledge-base/mechanic-extractor` | admin | P1 (UI base già a PR#547) |
| D2 | Sandbox / debug-chat | `/admin/agents/sandbox` · `/admin/agents/debug-chat` | admin | P2 |
| D3 | A/B testing | `/admin/agents/ab-testing` | admin | P2 |
| D4 | Prompts | `/admin/agents/prompts` | admin | P2 |
| D5 | RAG-backup / seeding | `/admin/rag-backup` · `/admin/seeding` | admin (seed=superadmin) | P2 |
| D6 | Integrations | `/admin/slack` · `/admin/content/email-templates` | admin | P2 |

## 6. Componenti — riuso vs nuovi, collocazione

Dall'audit §7: **~33-36/38 già coperti**. Dettaglio:

- **Riuso diretto (✅):** AdminShell, `TopBar`, `BulkActionBar`, `KPICard`/`KPIStatCard`, `Card`, `Tabs`/`AdminHubTabBar`, `UserRoleBadge` (5 ruoli), `Badge`+status-badge, primitives form, `RetrievedChunkCard`, `WaterfallChart`, `PdfViewerModal`, `PdfStatusTimeline`, `AdminConfirmationDialog`, `AuditTab`/`UserActivityTimeline`, version timeline/diff (`components/diff/`), ReactFlow (`@xyflow/react`, 2 impl.), Tiptap `RichTextEditor`, `FeatureFlagsTab`.
- **Adattamento/estensione (🟡):** `AdminSideDrawer` → sidebar responsive; `DataTable` → +pagination +virtualization (`react-window ^2.2.7`); KPISparkline (wrapper Recharts); EnvPill, StatusDot, QueryDrillDown, ChunkTable, IngestionLog, SyncStatusHero, DirtyStateBar, LiveEventLog (UI su SSE esistente), `DateRangePicker`+presets, PreviewFrame, `setup-checklist`→PublishChecklist, DangerZoneBox.
- **Da creare ex-novo (❌, ~2-3):** **KBTree** (tree-view file/cartelle, per A5), **density system** admin-wide (se confermato in §4.3), eventuale parametrizzazione di `AdminConfirmationDialog` (oggi hardcoda `CONFIRM` → serve "type the resource name").

**Collocazione (freeze de-versioning attivo — NO `components/v2/`):**
- Primitive trasversali → `apps/web/src/components/ui/<primitive>/` (es. `ui/status-dot/`, `ui/sparkline/`; esiste già `ui/admin/`).
- Composizioni admin → `apps/web/src/components/admin/<feature>/`.
- ❌ Vietato `components/admin/v2/`, `components/v2/**`, `components/ui/v2/**`.

## 7. Track sicurezza separato (non bloccante)

Hardening tracciato a parte (R3/R4/R5), **non blocca** re-skin/consolidamento. Backlog ordinato:

1. **Schema audit** (R3): aggiungere `impersonated_user_id`, `before_json`, `after_json`, `step_up_token_id`; allargare/sostituire `Details` (1024 char). **Prima** disambiguare la doppia entity `AuditLog` (`Administration` usata vs `SecurityAudit`) — verifica tecnica "quale è canonica".
2. **Impersonate token** (R4): estendere `Session` con `ImpersonatedByUserId` + `ImpersonatedUntil` + lifetime 15min; banner persistente via `SessionStatusDto`.
3. **Step-up strict** (R5): `LastTotpVerifiedAt` su `Session`; endpoint challenge → `step_up_token`; header `X-StepUp-Token` + validazione; behavior shadow→strict (403 `STEP_UP_REQUIRED`, MaxAge 30→5); coprire trigger (rotate key, emergency shutdown, mass delete >5, change flag prod, promote→superadmin). UI predisposta già in questa fase (R5).

## 8. Contratto API (FE → path reali, R8)

Il FE consuma i path **reali** del backend, senza chiedere normalizzazione:

| Tema | Handoff SP5 | Reale (il FE usa questo) |
|------|-------------|--------------------------|
| Prefisso | `/api/admin/*` | `/api/v1/admin/*` |
| Update | `PATCH` | `PUT` / `POST .../toggle` |
| Paginazione | cursor | page/limit |
| Endpoint personali | `/api/me/play-records`, `/api/me/private-games` | `/play-records`, `/private-games` |
| Sessione | JWT short-lived | cookie + session table (hash, 30gg) |

Gap endpoint reali da colmare per le rispettive schermate (audit §5): SSE `events/live` globale (A8), `force-logout` + `GET .../sessions` (A2), `flag-hallucination` (A4), `kb/tree` + `docs/{id}/chunks` + `idempotency-check` (A5), `DELETE` play-records (B5), `publish-checklist` private-games (B7). Ciascuno è dettagliato nel plan dell'ondata che lo richiede.

## 9. Sequenza di rollout (R10)

```
Fondamenta → Pilota A1 → A2 (stress-test sicurezza) → Ondate per gruppo
```

| Fase | Contenuto | Obiettivo |
|------|-----------|-----------|
| **F0 — Fondamenta** | Shell sidebar-responsive + 4 gruppi nav (filtro ruolo) + token/dark scoped + density; redirect de-duplicazione IA (§4.2) | Valida shell + design-system SP5 senza dipendere da nuovi endpoint/RBAC |
| **F1 — Pilota A1** | Overview re-skin (già full, basso rischio) | Valida il workflow mock SP5 → componente end-to-end |
| **F2 — A2 Users** | Lista (DataTable +pagination +virtualization) + drill + impersonate (banner) + delete typed-confirm + step-up UI + audit timeline | Stress-test di tutti i pattern rischiosi prima di scalare |
| **F3 — Ondata KB/AI** | A5 (KBTree), A5b, A4, D1 mechanic-extractor | Cluster knowledge/AI |
| **F4 — Ondata Ops** | A6, A7, A8 + LiveEventLog, C1/C3/C5/C7 | Operatività/monitoring |
| **F5 — Ondata Tools** | B1, B4, B6 (rientro sotto `/admin`); A9 notifications | Power-user editing |
| **F6 — Ondata avanzata** | B2, B3, B5, B7; C2/C4/C6 (superadmin), D2-D6 | Resto + superadmin-gated |
| **Track ⊥** | Sicurezza (§7) | Parallelo, non bloccante |

A3 Content moderation resta in attesa del BC moderation (fuori scope corrente) → backlog/ondata dedicata.

## 10. Rischi e decisioni residue

| # | Rischio/decisione | Stato |
|---|-------------------|-------|
| D-1 | A4: hub `?tab=` canonico vs route separate 1:1 (§4.2) | ✅ **Risolta**: hub canonico — input utente "conta l'estetica, non la struttura nav" (§4 principio guida) |
| D-2 | Density: utility scoped vs density system dedicato (§4.3) | Da decidere in F0 |
| D-3 | A3 moderation: BC backend assente → schermata resta partial | Accettato (fuori scope) |
| D-4 | Doppia entity `AuditLog` canonica | Verifica tecnica nel track sicurezza (§7) |
| D-5 | Redirect di massa (`agents/inspector`, `rag-quality`, `content?tab=kb`): rischio link rotti interni/esterni | Censire i referrer prima di applicare (plan F0) |
| D-6 | Stato 🟡 di A2/A7/A8: "da verificare" wiring → confermare con lettura mirata in fase di plan | Risolto nei plan d'ondata |

## 11. Riferimenti

- Audit: `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md`
- Inventario schermate + endpoint: `admin-mockups/design_handoff_admin/SCREENS.md`
- Prompt-pack surplus: `admin-mockups/design_handoff_admin/SURPLUS_DESIGN_PROMPTS.md`
- Nav (sorgente di verità gruppi): `admin-mockups/design_handoff_admin/admin-nav.js`
- Mockup HTML: `admin-mockups/design_handoff_admin/admin/sp5-*.html`
- Token: `admin-mockups/design_handoff_admin/admin/tokens.css` + `admin-base.css`
- Freeze de-versioning + token canonici: `CLAUDE.md` (§ AI Assistant Rules)

---

*Design prodotto da sintesi audit + decisioni R1-R10. Nessuna modifica al codice di prodotto. Prossimo passo: review utente di questo documento → `writing-plans` per i plan d'implementazione (a partire da F0 Fondamenta + F1 Pilota A1).*
