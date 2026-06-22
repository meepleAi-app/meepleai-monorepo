# MONITOR_DESIGN_PROMPT.md — Mock per A8 `/admin/monitor` (Issue #1718, F4.1)

> Generato 2026-05-30 per F4.1 (issue #1718). Workflow analogo a `KB_TOOLPAGE_DESIGN_PROMPTS.md` (F3-FU-3): l'utente genera il mock su claude.ai web, poi l'agente lo integra nel codice.

## Contesto

A8 Monitor è oggi un hub consolidato (Issue #5040 + #5053) con `AdminHubTabBar` + **12 tab esistenti**: `alerts · cache (Metrics) · infra · command · testing · mau · containers · logs · grafana · export · email · history`. Pattern URL `?tab=<id>`.

F4.1 aggiunge **un 13° tab "events"** che ospita il nuovo componente **`LiveEventLog`**: stream realtime SSE sopra l'outbox `DomainEventLog` (BE Issue #661, Track S1 sicurezza). Il componente è anche **trasversale**: `sp5-admin-infra.html` (C1) lo cita come `reuse`.

Il backend Outbox esiste già (`DomainEventLogEntity`, `EventTypeRegistry`, `GetActivityFeedQueryHandler`, `ActivityFeedEndpoints`). Eventi emessi: `agent.created`, `chat.session.created`, `kb.doc.indexed`, `session.created`, `session.finalized`, `library.entry.removed`, `library.session.recorded`. Gap = SSE broadcast endpoint `events/live` admin-scoped (Fase 1 dell'issue).

## Decisione header (incorporata nel mock)

Layout `/admin/monitor` possiede la banda di sezione condivisa: header `<h1>Monitor</h1>` + crumbs (`Admin · Monitor · 2 alerts · 1.4k events/h · 14 containers`) + actions (search ⌘K, `⏸ Pause stream`, `⤓ Export ndjson`) + sub-nav `.admin-tabs` (13 tab, con `events` `.active` e badge `count` "live"). La pagina NON ripete un h1 "Monitor"; l'identità della pagina = il tab attivo della sub-nav. Se serve un titolo di contenuto interno, `.admin-panel-head` (h3), non un h1 grande.

**Colore di sezione**: `c-event` (rosso `350 89% 60%`) per coerenza con `sp5-admin-alerts.html` C7 e con la semantica "operations/incidents". `nav-item.active` border-left + glow `hsl(var(--c-event)/.08)`.

## Workflow su claude.ai

1. **claude.ai** → nuova conversazione, **Artifacts attivi**.
2. **Messaggio 1**: incolla il Brief (§1) **+** allega i **5 file** (§2). Claude conferma, attende.
3. **Messaggio 2**: invia il prompt-schermata (§3). Risposta = 1 HTML (`sp5-admin-monitor.html`).
4. Salva l'artifact come `admin-mockups/design_handoff_admin/admin/sp5-admin-monitor.html` e passalo all'agente. L'agente penserà a sostituire `<style>` inline con `<link>`, voce nav, `SCREENS.md`, ecc.

## 1. Brief (Messaggio 1)

```
Sei un designer frontend. Produci UN MOCKUP HTML statico e standalone per la tool-page
"Monitor" della Admin Console di MeepleAI, nello stile SP5 dei 3 CSS allegati
(tokens.css, components.css, admin-base.css). Ti allego anche sp5-admin-infra.html
(pattern .event-log + .live-pill + colonne ts/lvl/msg) e sp5-admin-alerts.html
(pattern banda admin-top + admin-tabs + KPI per gruppo C/operations).

REGOLE FERME:
- Vanilla HTML, un solo file. NIENTE React/framework/build.
- Dark theme default, density alta, desktop-first 1440px. SOLO le CSS variables e le classi
  del design system (admin-shell, admin-top, admin-tabs, admin-table, admin-kpi, admin-panel,
  status-chip, btn-admin, admin-form-row, admin-search, alert-banner, event-log, live-pill,
  ecc.) gia' definite in admin-base.css / sp5-admin-infra.html.
- VIETATO hex hardcoded: usa var(--*) e le utility entita' .e-game/.e-kb/.e-event/...
- Dati realistici e plausibili (nomi giochi, model name bge-m3, job_ID, timestamp).
  Niente Lorem ipsum.
- Per l'anteprima: inlinea i 3 CSS + gli stili .event-log/.live-pill di sp5-admin-infra.html in <style>.

STRUTTURA OBBLIGATORIA (riempi solo la parte "CONTENUTO"):
<!doctype html><html lang="it" data-theme="dark"><head>…<style>/* CSS inline */</style></head>
<body class="admin-page">
  <div class="admin-mobile-fallback"><div class="em">🖥️</div><h2>Console solo desktop</h2><p>≥880px.</p></div>
  <div class="admin-shell">
    <div data-admin-nav-mount></div>
    <main>
      <!-- BANDA DI SEZIONE (uguale per tutte le tab di Monitor) -->
      <header class="admin-top">
        <div>
          <h1>Monitor</h1>
          <div class="crumbs">Admin · Monitor · 2 alerts · 1.4k events/h · 14 containers</div>
        </div>
        <div class="spacer"></div>
        <div class="admin-top-actions">
          <div class="admin-search"><span>🔍</span><input placeholder="Search events, jobs, services…"/><kbd>⌘K</kbd></div>
          <button class="btn-admin sm ghost">⏸ Pause stream</button>
          <button class="btn-admin sm ghost">⤓ Export ndjson</button>
        </div>
      </header>
      <div class="admin-body">
        <!-- SUB-NAV: 13 tab (12 esistenti + nuovo 'events' .active con .count "live")
             alerts · cache · infra · command · testing · mau · containers · logs · grafana
             · export · email · history · EVENTS (.active, .count "live") -->
        <div class="admin-tabs" role="tablist" aria-label="Monitor sezioni">… 13 tab …</div>
        <!-- CONTENUTO SPECIFICO DEL TAB ATTIVO 'events' (vedi §3 sotto) -->
      </div>
    </main>
  </div>
  <script src="admin-nav.js"></script><script>renderAdminNav('monitor');</script>
</body></html>

PRINCIPIO HEADER: la banda "Monitor" + crumbs + sub-nav e' condivisa (vive nel layout).
Il tab "events" e' .active, il resto e' inattivo. La pagina NON ripete un h1 di sezione.
Conferma di aver capito e attendi il prompt-schermata.
```

## 2. File da allegare (5)

```
admin-mockups\design_handoff_admin\admin\tokens.css
admin-mockups\design_handoff_admin\admin\components.css
admin-mockups\design_handoff_admin\admin\admin-base.css
admin-mockups\design_handoff_admin\admin\sp5-admin-infra.html
admin-mockups\design_handoff_admin\admin\sp5-admin-alerts.html
```

## 3. Prompt — Monitor · Events tab (LiveEventLog)

```
Crea il mockup "Monitor · Events" (tab "Events" .active nella sub-nav, badge .count "live"
con dot .running pulsante).

Contesto: admin osserva lo stream realtime dei domain events emessi dall'outbox (agent.created,
kb.doc.indexed, session.created, session.finalized, chat.session.created, library.entry.removed,
library.session.recorded). Il componente LiveEventLog e' il pezzo nuovo della pagina (anche
trasversale: riusato da C1 Infra). Ruolo: admin.

CONTENUTO della .admin-body (dopo la sub-nav), in ordine:

1. **KPI strip** (4× .admin-kpi inline, .e-event accent):
   - "Events/min" valore "247" trend +12% (.e-event) con sparkline ultimi 60 min
   - "Events/h" valore "14.823" trend +3.2% (.e-chat) con sparkline 24h
   - "Errors/min" valore "0.4" stato OK .status-chip.ok (.e-toolkit)
   - "SSE clients" valore "3" sotto-label "2 admin · 1 system" (.e-agent)

2. **Filter bar** (.admin-panel compatta, header = "Filtri" + status .live-pill "● streaming · SSE"):
   - admin-search ampia (placeholder "Filter by aggregateId, userId, payload…")
   - filter-chip group "EventType":
     * agent.created (count 12)
     * kb.doc.indexed (count 8)
     * chat.session.created (count 234)
     * session.created (count 45)
     * session.finalized (count 38)
     * library.entry.removed (count 4)
     * library.session.recorded (count 87)
     * (chip "+ aggiungi filtro" ghost)
   - filter-chip group "EntityType": Agent · ChatSession · PdfDocument · Session · UserLibraryEntry
   - select "Time range": Live · 15m · 1h · 24h · 7d (default = Live)
   - btn-admin.ghost.sm "Clear all filters"

3. **LiveEventLog panel** (.admin-panel, full-width):
   - .admin-panel-head: <h3>Live event stream <span mono small>/api/v1/admin/events/stream</span></h3>
     + .live-pill "● streaming · SSE" + .head-cluster (count "12.487 events · last 60m" + 2 btn ghost ⏸/⤓)
   - .event-log role="log" aria-live="polite" (PATTERN da sp5-admin-infra.html righe 197-215 +
     esempio righe 457-489)
   - Mostra ~12-15 row realistiche miste, NEL FORMATO:
     <span class="ts">14:23:08.412</span> <span class="lvl info|ok|warn|err">LVL</span>
     <span class="msg">eventType <span class="k">aggregateType=</span><span class="v-agent|v-kb|v-game|...">value</span> <span class="k">aggregateId=</span>uuid8 <span class="k">user=</span><span class="v-session">name</span></span>

   Esempi di righe da includere (mescola):
   - `agent.created` lvl info `aggregateType=Agent` `id=ag_8f3a21` `user=alice`
   - `kb.doc.indexed` lvl ok `aggregateType=PdfDocument` `file=wingspan-rulebook-v3.pdf` `pages=96` `duration=4.812s`
   - `chat.session.created` lvl info `aggregateType=ChatSession` `gameId=catan` `model=bge-m3`
   - `session.created` lvl info `aggregateType=Session` `host=bob` `players=4` `game=brass`
   - `session.finalized` lvl ok `aggregateType=Session` `duration=2h14m` `winner=charlie`
   - `library.session.recorded` lvl info `aggregateType=UserLibraryEntry` `playCount=12` `lastPlayed=2026-05-30`
   - `library.entry.removed` lvl warn `aggregateType=UserLibraryEntry` `reason=user-request`
   - una `chat.session.created` lvl err con `error=timeout` `retry=2/3`

4. **Detail drawer placeholder** (.admin-panel side-by-side a destra OR sotto):
   Se preferisci layout 2-col, metti a dx una .admin-panel "Event detail" (350px) con:
   - Hero: EventType in badge .e-event + AggregateType chip + .lvl info
   - Mono key=value pairs: EventId (UUID), AggregateId, UserId, OccurredAt, LoggedAt, PayloadVersion
   - <pre> PayloadJson (truncated 300 char) con btn-admin.ghost.sm "Show full payload"

Realtime: SI - .live-pill "● streaming" e dot .status-chip.running .pulse in cima.
Endpoint (solo realismo nel mock):
- GET /api/v1/admin/events/stream  (SSE, nuovo Fase 1)
- GET /api/v1/admin/events?since=<cursor>&limit=100  (backfill polling)
- GET /api/v1/admin/events/types  (lista tipi per filter chips)

Rispetta SCHELETRO + sub-nav 13 tab + banda Monitor. Inlinea i CSS (tokens + admin-base
+ stili .event-log/.live-pill da sp5-admin-infra.html). NO React, NO build.
```

## Cosa NON disegnare (anti scope-creep)

- Le altre 12 tab di Monitor (`alerts`/`cache`/`infra`/...) NON vanno disegnate in dettaglio: la sub-nav le mostra come inattive (testo + count badge). Il re-skin di quelle tab in Fase 2 dell'issue #1718 e' Tailwind tokens cosmetic, NON richiede mockup dedicati (riusa pattern `sp5-admin-alerts.html` + `sp5-admin-infra.html` esistenti).
- Sub-route esistenti (`/admin/monitor/containers`, `/grafana`, `/logs`, `/mau`, `/operations`, `/service-calls`, `/services`) NON sono in scope: il re-skin Fase 2 le lascia as-is, la migration a tab (se serve) e' ondata F4.4.
- Settings/config dei tipi evento, RBAC granulare per eventType, retention policy editor: NON in scope F4.1.

## 4. Riferimenti tecnici (per l'agente, post-mock)

| Risorsa | Path |
|---|---|
| Spec consolidamento | `docs/superpowers/specs/2026-05-24-sp5-admin-console-consolidation-design.md` (F4 Ondata Ops §9) |
| Audit gap analysis | `admin-mockups/design_handoff_admin/ADMIN_AUDIT.md` (§5-7) |
| Pattern LiveEventLog CSS | `admin-mockups/design_handoff_admin/admin/sp5-admin-infra.html:197-215` (classi) + `:457-489` (esempi) |
| Pattern band admin-top + actions | `admin-mockups/design_handoff_admin/admin/sp5-admin-alerts.html` (header section) |
| Pattern hub tabs 13-elem | `admin-mockups/design_handoff_admin/admin/sp5-admin-kb-subnav.html` (8 tab → estendere a 13) |
| BE Outbox entity | `apps/api/src/Api/Infrastructure/Entities/DomainEventLog/DomainEventLog.cs` |
| BE Event registry | `apps/api/src/Api/Infrastructure/DomainEventLog/EventTypeRegistry.cs` |
| BE Query handler (polling, user-scoped) | `apps/api/src/Api/BoundedContexts/Administration/Application/Queries/ActivityFeed/GetActivityFeedQueryHandler.cs` |
| BE HTTP endpoint | `apps/api/src/Api/Routing/ActivityFeedEndpoints.cs` |
| FE hub monitor | `apps/web/src/app/admin/(dashboard)/monitor/page.tsx` |
| FE LogsTab (NON usare, e' Loki app log) | `apps/web/src/app/admin/(dashboard)/monitor/LogsTab.tsx` |
| Predecessor pattern (re-skin KB) | PR #1664 (F3-FU-3 KB tool-pages) |
| Predecessor pattern (sub-nav) | PR #1649 (F3 KB Explorer) |

## 5. Post-mock — passi dell'agente

1. Sostituisce `<style>` inline con `<link rel="stylesheet" href="./tokens.css">` ecc.
2. Sostituisce `<script src="admin-nav.js"></script><script>renderAdminNav('monitor');</script>` con voce nav esistente (gruppo `monitor` in `admin-nav.js`).
3. Aggiorna `SCREENS.md` (stato A8 da `✅ (12 tab; LiveEventLog ≈ LogsTab)` a `✅ (13 tab; LiveEventLog real, SSE)`).
4. Aggiorna `ADMIN_AUDIT.md` (riga 32: `LiveEventLog 🟡` → `✅ implementato F4.1 PR #....`).
5. Apre design doc `docs/superpowers/specs/2026-05-30-sp5-admin-f4-1-monitor-design.md` (4 fasi dettagliate).
6. Apre plan `docs/superpowers/plans/2026-05-30-sp5-admin-f4-1-monitor.md` (stack di 4 PR).
