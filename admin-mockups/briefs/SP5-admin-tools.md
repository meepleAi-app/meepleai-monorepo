# SP5 — Brief Claude Design: Admin + Power-User Tools

> **Preambolo obbligatorio**: leggi `admin-mockups/briefs/_common.md` prima di iniziare.
> Tutti i token, convenzioni, DoD si applicano a questo brief.

## Contesto

MeepleAI ha due superfici di "power use" non coperte dai mockup esistenti:

1. **Admin console** — `apps/web/src/app/admin/(dashboard)/` — operatore piattaforma: overview salute sistema, gestione utenti, content moderation, AI quality, knowledge base ingestion, catalog ingestion (BGG sync), monitor, analytics, notifications templates, config runtime, RAG quality, shared-games community moderation, UI library (showcase interno).
2. **Power-user tools** — editor, pipeline-builder, n8n integrations, upload avanzato, play-records, versioning toolkit/KB, private-games, dev-tools — utenti avanzati che creano contenuto (game design, KB curation, workflow automation).

Questo sub-project chiude il gap: oggi l'admin è un dashboard tabellare generico (materia grezza a `(dashboard)/` con componenti shadcn base) e i power-tools sono tool a sé stanti senza coerenza visiva. Il redesign v2 deve dare identità entity-driven coerente anche qui.

## Fonti di riferimento

- `02-desktop-patterns.html` — **primario**: tutte le pagine admin sono desktop-only (sidebar-detail, split-view, tabs)
- `05-dark-mode.html` — admin e dev-tools hanno heavy dark-mode usage
- `04-design-system.html` — data tables, forms, inputs power-user
- `settings.jsx` — pattern setting sections + tabs (utile per admin config)
- `tokens.css` — tutti i token

## Audience

- **Admin / SuperAdmin**: operatore piattaforma. Conosce l'app, vuole **densità info + azioni bulk**. Meno friction, più scorciatoie keyboard.
- **Power-user / Content creator**: designer giochi, KB curator, automazioni. Vuole **control + history + undo**. Accetta complessità se produttiva.

Non-loggati / utenti base **non atterrano mai qui**. Mobile è optional (desktop-first obbligatorio).

## Schermate da produrre (14)

### Gruppo A — Admin Console (`/admin/*`)

#### A1. Admin Overview — `sp5-admin-overview.{html,jsx}`

**Route target**: `/admin/overview`
**Pattern**: dashboard grid con KPI cards + activity feed + alerts.

**Sezioni**:
- Header minimale: "Overview sistema" + timestamp ultimo refresh + bottone "Refresh all"
- Grid 4 colonne (1440px): 4 KPI cards row 1 — Utenti totali · Partite attive · Agenti live · Storage used
- Row 2 — 2 wide cards: Activity feed (ultimi eventi) + Alerts (health alerts sistema)
- Row 3 — Charts: Uptime 7gg, Requests/min, Error rate, AI cost day
- Sidebar destra (opzionale): "Quick actions" — Impersonate user, Reset rate limit, Force cache flush

**Stati**: default popolato · loading skeleton · degraded (alert banner top) · offline (fallback "Admin API unavailable").

**Componenti v2 da progettare**: `AdminKPICard`, `AdminAlertBanner`, `AdminChartPanel`, `AdminActivityFeed`.

---

#### A2. Admin Users — `sp5-admin-users.{html,jsx}`

**Route target**: `/admin/users`
**Pattern**: sidebar-detail — tabella utenti sinistra (60%), dettaglio utente destra (40%).

**Sezioni**:
- **Tabella**: search + filtri (role, status, last-seen) + bulk-actions bar quando selezionati (enabled/disable/delete/export)
- Colonne: checkbox, avatar+name, email, role chip, joined, last-active, status, menu `⋮` (view, impersonate, change role, delete)
- **Detail pane** (quando riga selezionata):
  - Header utente (avatar grande + name + role chip entity=player)
  - Tabs: Profilo · Sessioni · Agenti · Audit log · Billing
  - Tab Audit log: timeline eventi (impersonazioni, role changes, logins)
  - CTA destruttive in fondo: "Forza logout", "Elimina account" (destructive confirm)

**Stati**: lista default · filtered · bulk-select attivo · detail loading · no-user-selected (empty detail pane con placeholder).

**Componenti v2 da progettare**: `AdminDataTable` (riusa su altre pagine), `BulkActionsBar`, `AdminUserDetail`, `AuditLogTimeline`.

---

#### A3. Admin Content / Games Moderation — `sp5-admin-content.{html,jsx}`

**Route target**: `/admin/content` + `/admin/games` (unifica mockup)
**Pattern**: tabs per tipo contenuto (Games · Shared toolkit · KB · Comments) con tabelle filtrabili.

**Sezioni**:
- Header: tabs tipo contenuto
- Tab Games: tabella giochi con status (pubblicato · in review · rifiutato · bozza), filtri designer/anno/rating, bulk moderation
- Tab Shared toolkit: lista toolkit pubblicati community con install count, rating, report count
- Tab KB: documenti indicizzati, ultimo re-index, quality score
- Tab Comments: flag queue — commenti segnalati con preview + autore + CTA (approva/rimuovi/ban autore)
- Per ogni riga: azioni moderation (approve, reject con reason, request changes)

**Stati**: default · filtered · moderation in-progress (loading su riga) · empty queue (no flagged content).

**Componenti v2 da progettare**: `ModerationQueue`, `ContentStatusChip`, riusa `AdminDataTable`.

---

#### A4. Admin AI / RAG Quality — `sp5-admin-ai.{html,jsx}`

**Route target**: `/admin/ai` + `/admin/rag-quality`
**Pattern**: split-view — left: metriche aggregate; right: query drill-down.

**Sezioni**:
- **Left (60%)**:
  - Row KPI: Query/day, Avg latency, P95 latency, Cost/day, Hallucination rate
  - Chart: latency trend 7gg
  - Table: top-10 worst queries (low rating o alta latency)
  - Table: top-10 agenti per uso
- **Right (40%, query selected)**:
  - Header: query text + timestamp + user + agent
  - Sezione "Retrieval": chunk list (source, similarity score, highlights)
  - Sezione "Response": text generato + rating utente + feedback
  - Sezione "Debug": latency breakdown (retrieval / rerank / LLM / total), token count, cost
  - CTA: "Re-run query" (con current KB) + "Flag hallucination"

**Stati**: default aggregato · query-selected · loading drill-down · no-data (filter empty).

**Componenti v2 da progettare**: `QueryDrillDown`, `RetrievalChunkList`, `LatencyBreakdownBar`, `AdminMetricsPanel`.

---

#### A5. Admin Knowledge Base — `sp5-admin-kb.{html,jsx}`

**Route target**: `/admin/knowledge-base`
**Pattern**: sidebar-detail — sinistra tree KB (per gioco), destra document operations.

**Sezioni**:
- **Left sidebar**: tree espandibile per gioco → documento → chunks
  - Search in top
  - Badge count per gioco (X docs, Y chunks)
- **Right panel** (documento selezionato):
  - Header: filename + gioco associato (EntityChip entity=game) + upload date + indexer version
  - Status: indicizzato/pending/failed (+ timestamp)
  - Actions: Re-index, Delete, Download original, View chunks
  - Tab "Preview": PDF preview inline (usa pattern iframe/pdf.js)
  - Tab "Chunks": tabella chunk con page, section, preview, similarity score (top matches last query)
  - Tab "Ingestion log": stream log ingestion (Unstructured, SmolDocling, embedder timing)

**Stati**: default · document-selected · reindex-in-progress (progress bar) · failed (error banner + retry).

**Componenti v2 da progettare**: `KBTree`, `DocumentViewer`, `IngestionLog`, `ChunkTable`.

---

#### A6. Admin Catalog Ingestion — `sp5-admin-catalog.{html,jsx}`

**Route target**: `/admin/catalog-ingestion`
**Pattern**: hero+body — hero con stato sync corrente, body con history + controlli.

**Sezioni**:
- **Hero**: "Catalog ingestion" + status pill (idle/running/failed) + ultima sync + games importati
- **Controlli**: CTA "Run sync now" + dropdown provider (BGG, CSV, manual) + settings (batch size, rate limit)
- **Body**:
  - Timeline run (sync history) con durata, #imported, #updated, #failed
  - Expand run → log dettagli + errori
  - Section "Queue pending" — giochi in coda per re-sync
  - Section "Failed items" — giochi con sync errors + retry bulk

**Stati**: idle · running (progress bar + live log tail) · failed · success (green banner).

**Componenti v2 da progettare**: `SyncStatusHero`, `SyncRunTimeline`, `LogStream`.

---

#### A7. Admin Config / Feature Flags — `sp5-admin-config.{html,jsx}`

**Route target**: `/admin/config`
**Pattern**: tabs per categoria (General · Features · AI · Integrations · Security).

**Sezioni**:
- Tabs top
- Per ogni flag: label, descrizione, current value (toggle/select/input), environment (dev/staging/prod), last updated, updated by
- **Features tab**: lista feature flags con toggle runtime (es. `Features.PdfUpload`, `Features.AlphaMode`)
- **AI tab**: model selection, temperature default, max tokens, fallback config
- **Integrations tab**: n8n endpoint, BGG API keys, OAuth providers enable/disable
- **Security tab**: rate limit rules, CORS origins, MFA required roles
- Footer sticky: "Changes require apply" bar quando dirty → CTA "Apply" + "Revert"

**Stati**: default · dirty (unsaved) · applying (loading) · applied (toast success) · conflict (optimistic lock error).

**Componenti v2 da progettare**: `ConfigSection`, `FlagRow`, `DirtyStateBar`.

---

#### A8. Admin Monitor / Analytics — `sp5-admin-monitor.{html,jsx}`

**Route target**: `/admin/monitor` + `/admin/analytics` (unifica)
**Pattern**: dashboard grid con charts + event log live.

**Sezioni**:
- Header con time range picker (1h · 24h · 7gg · 30gg · custom)
- Row 1: 6 sparkline KPI (API latency, DB latency, Redis ops, AI tokens/min, PDF ingestion queue, error rate)
- Row 2: Charts grandi (2x2):
  - Request rate by endpoint (stacked area)
  - Error rate breakdown (by route)
  - User signups/session starts trend
  - Game-session duration distribution histogram
- **Live event log** (bottom, collapsible): stream SSE eventi sistema (logins, session start, PDF upload, AI error)

**Stati**: default · time-range selected · no-data · offline (Grafana unreachable) · loading.

**Componenti v2 da progettare**: `TimeRangePicker`, `KPISparkline`, `LiveEventLog`, riusa `AdminChartPanel`.

---

#### A9. Admin Notifications Templates — `sp5-admin-notifications.{html,jsx}`

**Route target**: `/admin/notifications`
**Pattern**: sidebar-detail — lista templates sinistra, editor destra.

**Sezioni**:
- **Left**: lista template per tipo (email, push, in-app) con search
- **Right**:
  - Header: template name + variant (marketing/transactional/system)
  - Tabs: Edit · Preview · Test send · History
  - Tab Edit: campi soggetto/body con variable helper (e.g. `{{user.name}}`, `{{game.title}}`) + visual editor (if email)
  - Tab Preview: rendering light/dark + mobile/desktop preview
  - Tab Test send: input email → invia template a destinatario test
  - Tab History: changelog (chi ha modificato quando)

**Stati**: default · template-selected · editing (dirty) · sending (test) · success toast · error.

**Componenti v2 da progettare**: `TemplateEditor`, `TemplatePreviewFrame`, `VariableHelper`.

---

### Gruppo B — Power-User Tools

#### B1. Editor — `sp5-editor.{html,jsx}`

**Route target**: `/editor` (game content editor)
**Pattern**: 3-col split-view — navigation tree (left) · editor (center) · preview/props (right).

**Sezioni**:
- **Left**: tree game content (rules, FAQ, scenarios, glossary)
- **Center**: rich editor (markdown/structured blocks) — commands, quick insert, history undo/redo
- **Right top**: live preview (come rendering in app)
- **Right bottom**: metadata (tags, linked entities via EntityChip, version, published status)
- Toolbar top: save · publish · preview mobile/desktop · version history · commit message

**Stati**: default editing · dirty (autosave countdown) · conflict (remote change detected) · locked (another editor) · preview mode.

**Componenti v2 da progettare**: `EditorTree`, `BlockEditor`, `PreviewFrame`, `VersionHistoryPanel`.

---

#### B2. Pipeline Builder — `sp5-pipeline-builder.{html,jsx}`

**Route target**: `/pipeline-builder`
**Pattern**: canvas node-based — flow editor per configurare RAG/agent pipelines.

**Sezioni**:
- Toolbar top: "New pipeline", "Templates", "Save", "Test run", "Publish"
- **Canvas center**: node graph (drag-drop) — nodi per step (retrieve, rerank, LLM, postprocess, tool-call)
- **Left sidebar**: palette nodi disponibili (categorizzati: retrieval, processing, output, integrations)
- **Right sidebar** (node-selected): config props (model, parameters, prompt template, retry policy)
- **Bottom panel**: test run output (input → output JSON) + metriche per node (latency, cost)

**Stati**: empty canvas · building · dirty · validating (errori su nodi) · running test · test-complete.

**Componenti v2 da progettare**: `FlowCanvas`, `NodePalette`, `NodeConfigPanel`, `PipelineTestOutput`.

---

#### B3. n8n Integrations — `sp5-n8n.{html,jsx}`

**Route target**: `/n8n`
**Pattern**: hero+body — hero con connection status, body con workflow list + logs.

**Sezioni**:
- **Hero**: "n8n Integrations" + endpoint URL (masked) + connection status pill + CTA "Test connection"
- **Body tab "Workflows"**: lista workflow n8n usati (name, trigger, last-run, success rate) + CTA "Open in n8n"
- **Body tab "Webhooks"**: tabella webhook in entrata (endpoint, origin, last event, auth status)
- **Body tab "Logs"**: stream eventi recenti (tipo, payload preview, status, timestamp)

**Stati**: connected · disconnected (retry CTA) · loading · no-workflows (empty + CTA setup).

**Componenti v2 da progettare**: `IntegrationStatusHero`, `WorkflowTable`, `WebhookTable`, riusa `LogStream`.

---

#### B4. Upload avanzato — `sp5-upload-advanced.{html,jsx}`

**Route target**: `/upload`
**Pattern**: sidebar-detail — left: upload queue + history, right: file processing detail.

**Sezioni**:
- **Left**: dropzone grande in top + queue upload pending + history completed
- **Right** (file-selected):
  - File header: filename, size, type, status (queued/processing/indexed/failed)
  - Processing timeline: Unstructured → SmolDocling → chunker → embedder → indexer (progress per step)
  - Metadata: game association (EntityChip), language, page count, OCR yes/no
  - Actions: retry failed step, download original, view extracted, delete
- Bulk upload: drag multipli, assegnazione gioco, batch settings

**Stati**: empty (dropzone vuota) · queue-active · processing · batch-mode · failed · success.

**Componenti v2 da progettare**: `UploadDropzone`, `UploadQueue`, `ProcessingTimeline`, riusa `AdminDataTable`.

---

#### B5. Play Records — `sp5-play-records.{html,jsx}`

**Route target**: `/play-records`
**Scope**: vista power-user di tutti i play records dell'utente (sessioni salvate storiche con search avanzato + export).

**Pattern**: tabs (List · Calendar · Analytics) + filtri potenti.

**Sezioni**:
- Header: search + chip filters (game, player, date range, winner)
- **Tab List**: tabella play records con sort su ogni colonna, colonne: data, gioco (chip), giocatori, winner, durata, location, notes snippet
- **Tab Calendar**: view mensile con record per giorno
- **Tab Analytics**: charts personali — wins/losses per gioco, avg session duration, top opponents, game played distribution
- CTA: Export CSV, Add record manuale

**Stati**: default · filtered · empty (no records) · loading chart · export-progress.

**Componenti v2 da progettare**: `PlayRecordTable`, `PlayRecordCalendar`, `PersonalStatsChart`.

---

#### B6. Versions (Toolkit/KB) — `sp5-versions.{html,jsx}`

**Route target**: `/versions`
**Scope**: gestione versioning toolkit/KB pubblicati — history, rollback, compare.

**Pattern**: sidebar-detail — left: lista artefatti versionati, right: version timeline + diff.

**Sezioni**:
- **Left**: lista artefatti (toolkit, KB doc) con filter tipo + search
- **Right** (artefatto-selected):
  - Header: artefatto name + tipo chip
  - Timeline versioni (v1.0 → v1.1 → v2.0 → ...) con changelog commit message, autore, data
  - Compare tool: seleziona 2 versioni → diff view side-by-side
  - Actions per version: view, restore, tag, delete
- Sezione "Published" badge sulla versione corrente pubblicata

**Stati**: default · artifact-selected · compare-mode · restoring · empty (no versions yet).

**Componenti v2 da progettare**: `VersionTimeline`, `VersionDiffViewer`, `CompareSelector`.

---

#### B7. Private Games — `sp5-private-games.{html,jsx}`

**Route target**: `/private-games`
**Scope**: giochi personali non shared (game creator mode — design proprio gioco).

**Pattern**: grid card con overlay "private".

**Sezioni**:
- Header: "Miei giochi privati" + CTA "Nuovo gioco" (opens wizard)
- Grid MeepleCard entity=game con badge "Privato" overlay
- Filter: status (draft/ready/beta/published-as-shared)
- Click card → detail privato (modificabile senza review, pre-publish)
- CTA "Pubblica come shared" → workflow publishing con review checklist

**Stati**: default · empty (CTA wizard nuovo gioco) · publishing (validation + submit) · rejected (con feedback).

**Componenti v2 da progettare**: `PrivateGameCard` (variante MeepleCard), `PublishChecklist`.

---

#### B8. Dev Tools — `sp5-dev-tools.{html,jsx}`

**Route target**: `/dev` (solo env=development)
**Scope**: pannello dev interno — scenari MSW, stato auth mock, feature flags runtime, API replay.

**Pattern**: split drawer fisso (sidebar compatta permanente) con sezioni espandibili.

**Sezioni**:
- Sidebar sinistra fissa: nav (Scenarios · Auth · Flags · API · Memory · Cache · Reset)
- **Scenarios**: lista scenari MSW (empty, small-library, power-user, alpha-new, etc.) con radio-select + reload preview
- **Auth**: mock role switcher (guest, user, premium, admin, super-admin) + token viewer
- **Flags**: toggle feature flags lato client (persisted in localStorage)
- **API**: replay log chiamate + mock response editor
- **Memory**: Zustand store snapshot + replay
- **Cache**: React Query cache viewer + invalidation controls
- **Reset**: CTA "Reset all state" con confirm
- Footer: env badge + build hash + "Open in Storybook" link

**Stati**: default · scenario-loading · dirty (flag changed, reload suggested) · reset-confirm.

**Componenti v2 da progettare**: `DevSidebar`, `ScenarioPicker`, `AuthRoleSwitcher`, `FlagRuntimeToggle`, `StoreInspector`.

> Nota: dev-tools NON devono entrare in prod bundle (tree-shaking via contratto isolation). Mockup è solo visuale — implementazione già esiste in `src/dev-tools/` e `src/mocks/`.

---

#### B9. UI Library (showcase interno) — `sp5-ui-library.{html,jsx}` (facoltativo)

**Route target**: `/admin/ui-library`
**Scope**: pagina interna catalogo componenti v2 (simil-Storybook ma inline nell'app).

**Pattern**: sidebar nav categorie + main showcase con props playground.

**Sezioni**:
- Left sidebar: categorie componenti (Data Display · Forms · Feedback · Navigation · Layout · Entity-Specific)
- Main: componente selezionato con:
  - Header: nome + descrizione + import path
  - Preview live (light/dark toggle)
  - Props playground (form per modificare props in tempo reale)
  - Code snippet (TSX copy button)
  - Varianti elencate (default, sizes, states)
  - A11y notes

**Stati**: default · component-selected · playground-dirty · dark-preview.

**Componenti v2 da progettare**: `UILibraryNav`, `PropsPlayground`, `CodeSnippetBlock`.

**Skip se**: esiste già Storybook esterno o il team preferisce strumento dedicato.

---

## Priorità produzione

In quest'ordine (alto impatto → medio):

1. Admin Overview (#A1) + Admin Users (#A2) — quotidiane operazioni
2. Admin AI / RAG Quality (#A4) + Admin KB (#A5) — core business (debugging RAG)
3. Dev Tools (#B8) — velocità iterazione sviluppo (Phase 2 MeepleDev già attivo)
4. Editor (#B1) + Upload avanzato (#B4) — content creation flow
5. Admin Monitor (#A8) + Admin Catalog (#A6) + Admin Config (#A7) — gestione piattaforma
6. Admin Content (#A3) + Admin Notifications (#A9) — moderation + communication
7. Pipeline Builder (#B2) + n8n (#B3) — automazioni avanzate
8. Play Records (#B5) + Versions (#B6) + Private Games (#B7) — power-user personali
9. UI Library (#B9) — nice-to-have, skip se Storybook esterno

## Constraints specifici SP5

- **Desktop-first** obbligatorio. Mobile solo per admin overview + monitor (viewing da smartphone in emergenza). Tutte le altre pagine: fallback "Usa desktop per questa funzione" + link.
- **Density maggiore**: spacing ridotto (`--s-2`/`--s-3` invece di `--s-4`), font body più piccolo (`--fs-sm`), tabelle con righe compatte. Non sacrificare a11y (target 44x44 mantenuto, hit area padded).
- **Keyboard shortcuts**: mostrare hint in tooltip (e.g. `⌘K` search, `g+u` go to users). Documenta in ogni schermo quali shortcut attiva.
- **Dark mode preferita**: molti admin usano dark by default. Light funzionante ma priority a dark per polish.
- **Data tables**: tutte le tabelle admin usano `AdminDataTable` (componente unificato in SP5). Non creare 9 tabelle custom.
- **Entity consistency mantenuta**: anche in admin, ogni reference a gioco/utente/sessione è EntityChip. Non cadere in "solo testo" per density.

## Consegna

Quando finisci una schermata, produci un messaggio di handoff con:
- Nome file creato
- Route target
- Componenti v2 nuovi introdotti
- Screenshot mentali delle varianti light/dark/mobile/desktop (density note esplicita)
- Eventuali dubbi per il reviewer

Ping finale quando tutto SP5 è completo: elenca tutti i file `sp5-*` prodotti e i nuovi componenti v2 da implementare (andranno in `apps/web/src/components/ui/v2/admin/` e `apps/web/src/components/ui/v2/tools/` — sottodirectory dedicate per non inquinare il namespace v2 generico).
