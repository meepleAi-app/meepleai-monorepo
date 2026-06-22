# SURPLUS_DESIGN_PROMPTS.md — Guida per disegnare i mockup "surplus" con Claude (web)

> **Scopo**: l'audit (`ADMIN_AUDIT.md`) ha rilevato che la console admin reale ha **decine di funzioni senza un mockup SP5**. Per consolidare l'IA (driver scelto: *"Consolidare IA + re-skin"*) serve prima **disegnare quei mockup mancanti**, coerenti con il design system SP5. Questa guida è un **prompt-pack** da usare su **claude.ai** (web) per generarli, uno alla volta, nello stesso formato dei 18 mockup esistenti in `admin/`.
>
> **Relazione con gli altri file del pacchetto**:
> - `ADMIN_AUDIT.md` → da dove viene l'inventario del surplus (§4).
> - `BACKEND_PROMPTS.md` → formato dei prompt (questa guida ne è la versione "design", non "implementazione").
> - `admin/tokens.css` · `admin/components.css` · `admin/admin-base.css` · `admin/admin-nav.js` → il design system da riusare.
> - `WIRING_GUIDE.md` → lo scheletro HTML dei mockup admin.
>
> **Output atteso**: nuovi file `admin/sp5-<id>.html` standalone, indistinguibili per stile dai 18 esistenti, pronti per il passo successivo (traduzione in React durante il consolidamento).

---

## 1. Workflow su claude.ai (per ogni schermata)

1. Apri **claude.ai** → nuova conversazione. Tieni attivi gli **Artifacts** (il mockup apparirà come anteprima live).
2. **Primo messaggio della conversazione**: incolla il **Design Brief** (§3) **+** il contenuto reale dei 3 CSS (`admin/tokens.css`, `admin/components.css`, `admin/admin-base.css`). Servono a Claude per riprodurre il look senza accedere al repo.
3. **Messaggi successivi**: incolla **un prompt-schermata alla volta** (§6/§7). Claude restituisce un singolo file HTML come artifact.
4. **Verifica visiva** nell'anteprima (dark default, density alta, sidebar, tabelle). Itera con follow-up ("rendi la tabella virtualizzabile a vista", "aggiungi stato empty", ecc.).
5. **Salvataggio nel repo**: scarica l'artifact come `admin/sp5-<id>.html`. **Sostituisci** il blocco `<style>` inline con i `<link>` ai 4 asset, esattamente come negli altri mockup (vedi §3 scheletro). Aggiungi la voce in `admin/admin-nav.js`.
6. **Aggiorna** `SCREENS.md` (riga nuova) e marca lo stato qui in §4.

> **Perché inline + poi link**: su claude.ai gli asset esterni non esistono, quindi per *vedere* il mockup Claude deve inlinearne il CSS; nel repo invece i 4 file esistono già, quindi il file finale deve linkarli (handoff omogeneo, niente CSS duplicato).

---

## 2. Convenzioni (valide per tutti i mockup surplus)

- **Vanilla HTML** standalone. **NO** React/Vue/framework, **NO** build. Solo HTML + i 4 asset condivisi.
- **Dark default** (`<html data-theme="dark">`), light come secondaria (`admin-nav.js` gestisce il toggle).
- **Density alta**, **desktop-first 1440px**, **mobile fallback** `<880px` (blocco `.admin-mobile-fallback` già nello scheletro).
- **Solo token/classi del design system** — **NO hex hardcoded**. Usa `var(--*)`, le classi `admin-*` e le utility entità `.e-*`.
- **Dati realistici** (nomi, timestamp, conteggi plausibili), mai "Lorem ipsum" o "TODO".
- **Stati**: includi sempre default; quando pertinente mostra anche loading/empty/error come varianti commentate o sezioni.
- **9 entity color** per badge/chip cross-entity (vedi cheat-sheet §3).
- **Accessibilità**: `aria-label` su nav e azioni, focus visibile, target ≥28px.

---

## 3. DESIGN BRIEF — da incollare UNA volta a inizio conversazione

> Copia da qui fino a fine sezione, e in coda allega il contenuto di `admin/tokens.css`, `admin/components.css`, `admin/admin-base.css`.

```
Sei un designer frontend. Devi produrre MOCKUP HTML statici e standalone per la
"Admin Console" di MeepleAI (un assistente AI per giochi da tavolo), nello stile
del design system "SP5" che ti incollo sotto (3 file CSS: tokens.css, components.css,
admin-base.css).

REGOLE FERME:
- Vanilla HTML, un solo file per schermata. NIENTE React/JS framework/build.
- Riproduci ESATTAMENTE il look del design system: dark theme default, density alta,
  desktop-first 1440px. Usa SOLO le CSS variables e le classi che ti fornisco
  (admin-shell, admin-nav, admin-top, admin-table, admin-kpi, admin-panel, status-chip,
  role-chip, btn-admin, admin-tabs, alert-banner, admin-form-row, bulk-bar, ecc.).
- VIETATO colore hex hardcoded: usa var(--*) e le utility entità .e-game/.e-kb/...
- Dati realistici e plausibili (nomi, date, numeri). Niente Lorem ipsum.
- Per l'anteprima qui su claude.ai: inlinea i 3 CSS in un blocco <style> nell'<head>.
- Includi sempre il blocco mobile-fallback e la struttura shell qui sotto.

SCHELETRO HTML OBBLIGATORIO (riempi solo .admin-body):
<!doctype html>
<html lang="it" data-theme="dark">
<head>
  <meta charset="utf-8"/>
  <meta name="viewport" content="width=device-width, initial-scale=1"/>
  <title>{TITOLO} · MeepleAI Admin</title>
  <!-- nel repo: <link rel="stylesheet" href="tokens.css"> + components.css + admin-base.css -->
  <style>/* su claude.ai: incolla qui i 3 CSS */</style>
</head>
<body class="admin-page">
  <div class="admin-mobile-fallback">
    <div class="em">🖥️</div>
    <h2>Console solo desktop</h2>
    <p>La console admin è ottimizzata per schermi ≥ 880px. Aprila da desktop.</p>
  </div>
  <div class="admin-shell"> <!-- o admin-shell-3col se serve un pannello laterale dx -->
    <div data-admin-nav-mount></div>
    <main>
      <header class="admin-top">
        <div>
          <h1>{TITOLO}</h1>
          <div class="crumbs">Admin › {SEZIONE} › aggiornato 14:23:08</div>
        </div>
        <div class="spacer"></div>
        <div class="admin-top-actions">
          <div class="admin-search"><span>🔍</span><input placeholder="Cerca…"/><kbd>⌘K</kbd></div>
          <button class="btn-admin">⟳ Refresh <kbd>R</kbd></button>
          <button class="btn-admin icon" title="Tema">🌗</button>
        </div>
      </header>
      <div class="admin-body">
        <!-- CONTENUTO SPECIFICO DELLA SCHERMATA QUI -->
      </div>
    </main>
  </div>
  <script src="admin-nav.js"></script>
  <script>renderAdminNav('{NAV_ID}');</script>
</body>
</html>

CHEAT-SHEET design system (classi/variabili più usate):
- Layout: .admin-shell (240px+1fr) · .admin-shell-3col (240px+1fr+360px) · .admin-body
- Topbar: .admin-top h1 + .crumbs + .admin-top-actions · .admin-search (con <kbd>)
- KPI: .admin-kpi (+ .e-game/.e-kb/… per il bordo sx colorato) con
       .admin-kpi-label / .admin-kpi-value / .admin-kpi-trend(.up/.down/.flat) /
       .admin-kpi-spark (svg 70x28) / .admin-kpi-icon
- Pannelli: .admin-panel > .admin-panel-head (h3 + .meta) + .admin-panel-body
- Tabelle: .admin-table (th sticky, .sort-asc/.sort-desc, tr.selected, td .checkbox/.menu)
- Bulk: .bulk-bar (.count + .actions) — mostrala sopra la tabella quando ci sono selezioni
- Tabs: .admin-tabs > .admin-tab(.active) con .count badge
- Chip stato: .status-chip .healthy/.warning/.danger/.muted/.info/.running (dot integrato)
- Chip ruolo: .role-chip .superadmin/.admin/.premium/.user
- Bottoni: .btn-admin (.primary/.danger/.success/.ghost/.icon/.sm) — kbd inline supportato
- Form: .admin-form-row (label 200px + campo) · .admin-input/.admin-select/.admin-textarea
        (.mono per monospazio) · .admin-toggle(.on)
- Alert: .alert-banner .warning/.danger/.info/.success (em + .title + .actions)
- Feed: .activity-feed > .activity-item (.em cerchio + .body + .ts mono)
- Sparkline: <svg><path class="spark"/><path class="spark-fill"/></svg>
- Entità (9): .e-game .e-player .e-session .e-agent .e-kb .e-chat .e-event .e-toolkit .e-tool
  → usa .e-bg / .e-tint / .e-fg / .e-ring per badge e accenti cross-entity
- Mono per dati tecnici (id, timestamp, byte, versioni): font var(--f-mono)

Dato che la nav (admin-nav.js) elenca le voci ufficiali, quando una schermata surplus
appartiene a un gruppo nuovo ("Platform & Operations" o "AI Tooling & Data Quality"),
indicalo nel mockup con la voce attiva corrispondente (NAV_ID indicato in ogni prompt).
Se NAV_ID non esiste ancora in admin-nav.js, useremo un placeholder e lo aggiungeremo dopo.

Conferma di aver capito e attendi il primo prompt-schermata.
```

---

## 4. Inventario schermate surplus (da disegnare)

Derivato da `ADMIN_AUDIT.md` (aree admin reali senza mockup SP5). Nomenclatura continua dopo A (Admin Console) e B (Power Tools): **gruppo C = Platform & Operations**, **gruppo D = AI Tooling & Data Quality**. Gli endpoint reali (prefisso `/api/v1`) servono a rendere i mockup realistici e già "wirable".

### Gruppo C — Platform & Operations

| ID | Schermata | Route reale | Ruolo min | Endpoint reali (riferimento) | Stato mockup |
|----|-----------|-------------|-----------|------------------------------|--------------|
| C1 | Infrastructure & Containers | `/admin/monitor/containers` | admin | `AdminDockerEndpoints`, `AdminInfrastructureEndpoints`, `AdminOperationsEndpoints`, `BatchJobLogsEndpoints` (SSE) | ✅ `sp5-admin-infra.html` |
| C2 | Database Sync & Staging Access | `/admin/database-sync`, `/admin/staging-access` | superadmin | `DatabaseSyncEndpoints`, `Admin/AdminStagingAllowlistEndpoints` | ✅ `sp5-admin-database-sync.html` |
| C3 | LLM Providers & Circuit Breakers | `/admin/providers` | admin (rotate=superadmin) | `AdminProviderEndpoints`, `AdminOpenRouterEndpoints`, `AdminCircuitBreakerEndpoints`, `AdminLlmConfigEndpoints` | ✅ `sp5-admin-providers.html` |
| C4 | LLM Emergency Controls | `/admin/llm/emergency` | superadmin + step-up | `AdminEmergencyControlsEndpoints`, `AdminServiceCallEndpoints` | ✅ `sp5-admin-emergency.html` |
| C5 | Budget & Cost | `/admin/business` | admin | `BudgetEndpoints`, `CostCalculatorEndpoints`, `AdminBusinessStatsEndpoints` | ✅ `sp5-admin-budget.html` |
| C6 | Secrets Vault | `/admin/secrets` | superadmin + step-up | `AdminSecretsEndpoints` | ✅ `sp5-admin-secrets.html` |
| C7 | Alerting Config | `/admin/monitor` (tab alerts) | admin | `AlertConfig*Endpoints`, AlertRules (`Administration/.../AlertRules`) | ✅ `sp5-admin-alerts.html` |

### Gruppo D — AI Tooling & Data Quality

| ID | Schermata | Route reale | Ruolo min | Endpoint reali (riferimento) | Stato mockup |
|----|-----------|-------------|-----------|------------------------------|--------------|
| D1 | Mechanic Extractor | `/admin/knowledge-base/mechanic-extractor` | admin | `AdminMechanicExtractorEndpoints`, `AdminMechanicAnalysesEndpoints`, `AdminMechanicExtractorValidationEndpoints` | ✅ `sp5-admin-mechanic-extractor.html` |
| D2 | Agent Sandbox & Debug | `/admin/agents/sandbox`, `/admin/agents/debug-chat` | admin | `AdminSandboxEndpoints`, `AdminDebugChatEndpoints` (SSE), `AdminAgentTestEndpoints` | ✅ `sp5-admin-sandbox.html` |
| D3 | A/B Testing agenti | `/admin/agents/ab-testing` | admin | `AdminAbTestEndpoints` | ✅ `sp5-admin-ab-testing.html` (nota: 3 hex-contrasto su badge, DS-equivalenti a `.e-bg`) |
| D4 | Prompt Management | `/admin/agents/...prompts` | admin | `PromptManagementEndpoints` | ✅ `sp5-admin-prompts.html` |
| D5 | RAG Backup & Seeding | `/admin/...rag-backup`, seeding | superadmin (seed) | `AdminRagBackupEndpoints`, `RagEnhancementAdminEndpoints`, `AdminSeedingEndpoints` | ✅ `sp5-admin-rag-backup.html` |
| D6 | Integrations: Slack & Email Templates | `/admin/slack`, `/admin/content/email-templates` | admin | `AdminSlackEndpoints`, `AdminEmailTemplateEndpoints` | ✅ `sp5-admin-integrations.html` |

> **Estendibile**: con lo stesso pattern si possono aggiungere `entity-link` (`EntityLinkAdminEndpoints`), `user-tier management` (`Admin/AdminUserTier*Endpoints`, `AdminTierEndpoints`), `categories` (`AdminCategoriesEndpoints`), `ai-model registry` (`AiModelAdminEndpoints`). Aggiungili come C8/D7… quando servono.

---

## 5. Prompt template generico (riusabile per qualsiasi schermata surplus)

> Riempi i `{segnaposto}` con i dati della scheda (§6/§7) e incollalo come messaggio singolo.

```
Crea il mockup HTML standalone della schermata "{TITOLO}" (id: {ID}, NAV_ID: {NAV_ID}).

Contesto: {1-2 frasi su cosa fa la schermata e per chi}.
Ruolo minimo: {ruolo}. Azioni sensibili (mostra il pattern UI, non implementare):
{lista azioni che richiedono confirm/typed-confirm/step-up, se presenti}.

Layout: usa {admin-shell | admin-shell-3col}. La .admin-body contiene, in quest'ordine:
{elenco numerato delle sezioni UI: KPI strip, tabella, pannelli, form, log, timeline…}

Dettagli per sezione:
{per ciascuna sezione: quali componenti del cheat-sheet usare, quali colonne/campi,
quali dati realistici, quali entity-color, stati empty/error se rilevanti}.

Dati realistici da mostrare: {esempi concreti coerenti con MeepleAI}.
Realtime: {se serve, indica un pannello "live" stile LiveEventLog con dot .running}.
Keyboard hint: ⌘K ricerca, R refresh{, altri se pertinenti}.

Riferimento endpoint (solo per realismo dei dati mostrati, NON per codice):
{endpoint reali dalla scheda}.

Rispetta lo SCHELETRO e il CHEAT-SHEET del brief. Inlinea i 3 CSS per l'anteprima.
```

---

## 6. Prompt completi di esempio (2)

### C1 · Infrastructure & Containers

```
Crea il mockup HTML standalone della schermata "Infrastructure & Containers"
(id: C1, NAV_ID: 'infra').

Contesto: pannello operativo per admin/superadmin che monitora i container Docker,
i servizi backend (API, embedding, reranker, postgres, redis, n8n) e i batch job.
Ruolo minimo: admin (restart servizio = superadmin + confirm).
Azioni sensibili: "Restart" su un servizio → btn-admin.danger + ConfirmModal;
"Restart all" → typed-confirm. Mostra solo il pattern UI (bottone + nota "richiede conferma").

Layout: admin-shell. La .admin-body contiene in quest'ordine:
1. KPI strip (4x .admin-kpi): Container attivi (e-toolkit), CPU media %, RAM usata/totale,
   Batch job in coda (e-agent). Ognuna con trend e sparkline.
2. .admin-panel "Servizi" con .admin-table: colonne Servizio | Stato (status-chip
   healthy/warning/danger/running) | Uptime (mono) | CPU% | RAM | Immagine (mono) | azioni
   (.menu con Restart/Logs). Righe: meepleai-api (healthy), embedding-service (healthy),
   reranker-service (warning), postgres (healthy), redis (healthy), n8n (healthy),
   smoldocling (muted/stopped).
3. .admin-panel "Batch jobs (live)" con un mini LiveEventLog: lista mono di righe
   timestamp + livello (ok/info/warn) + messaggio, con un dot .status-chip.running in testa
   "● streaming". Mostra ~8 righe realistiche (es. "reindex game=catan stage=embed 64%").
4. .admin-panel "Risorse" con due barre orizzontali (disco, memoria) usando .e-tint.

Dati realistici: uptime tipo "12d 4h", immagini "ghcr.io/meepleai/api:1.8.2".
Realtime: il pannello Batch jobs ha aspetto "live" (dot pulsante).
Keyboard hint: ⌘K, R.

Riferimento endpoint: GET /api/v1/admin/docker, /admin/infrastructure,
/admin/operations/batch-jobs (+ SSE), POST /admin/operations/restart-service.

Rispetta SCHELETRO + CHEAT-SHEET. Inlinea i 3 CSS per l'anteprima.
```

### D1 · Mechanic Extractor

```
Crea il mockup HTML standalone della schermata "Mechanic Extractor"
(id: D1, NAV_ID: 'mechanic-extractor').

Contesto: tool admin che analizza i PDF dei regolamenti per estrarre le meccaniche di
gioco con un pipeline LLM asincrono e a costo controllato (cost cap + override). Mostra
la lista delle analisi, lo stato di avanzamento, e un drill-down per analisi.
Ruolo minimo: admin.
Azioni sensibili: "Avvia analisi" con cost cap; "Override cap" → conferma.

Layout: admin-shell-3col (lista a sx dentro la body NON serve: usa il 3° col come drill).
In realtà usa admin-shell e metti il drill come .admin-panel a destra in una grid 1fr/360px.
La .admin-body contiene:
1. Barra azioni: select gioco (admin-select) + select PDF + input "Cost cap ($)"
   (admin-input mono) + admin-toggle "Override cap" + btn-admin.primary "Avvia analisi".
2. KPI strip (3x): Analisi totali, Costo medio/analisi ($, e-event), Meccaniche estratte (e-kb).
3. grid 1fr 360px:
   - sx: .admin-panel "Analisi" con .admin-table: colonne Gioco | PDF (mono) | Stato
     (status-chip queued/running/completed/failed) | Sezioni (n) | Costo ($) | Avviata (mono)
     | azioni. Una riga .selected (running, con .status-chip.running).
   - dx: .admin-panel "Dettaglio analisi" (drill): hero col titolo gioco + status-chip;
     .admin-tabs Sezioni · Meccaniche · Log · Validazione; sotto, una lista di "section runs"
     con barra avanzamento (.e-tint) e una tabella meccaniche estratte con confidence
     (status-chip healthy/warning per high/low confidence).
4. Stato empty (commentato) per "nessuna analisi".

Dati realistici: giochi "Catan", "Wingspan"; PDF "wingspan-rules-en.pdf"; meccaniche
"Engine Building", "Set Collection" con confidence 0.92/0.61; costi tipo "$0.43".
Keyboard hint: ⌘K, R.

Riferimento endpoint: GET /api/v1/admin/mechanic-analyses, POST .../mechanic-extractor/run,
GET .../mechanic-extractor/validation.

Rispetta SCHELETRO + CHEAT-SHEET. Inlinea i 3 CSS per l'anteprima.
```

---

## 7. Schede compatte per le restanti schermate

> Incastra ciascuna nel template §5. Tono e densità come gli esempi §6.

- **C2 · Database Sync & Staging Access** (`NAV_ID: 'database-sync'`, superadmin): hero stato sync (ultimo/prossimo run, tunnel up/down con status-chip), timeline run (admin-table: run · direzione · righe · stato · durata mono), pannello "Staging allowlist" (tabella email/ip + btn-admin.danger rimuovi + form aggiungi). Azione sensibile: avviare sync prod→staging (confirm).
- **C3 · LLM Providers & Circuit Breakers** (`NAV_ID: 'providers'`, admin): KPI (provider attivi, richieste 24h, error-rate, costo 24h), tabella provider (OpenRouter/DeepSeek/… stato status-chip, modello default, latenza p95 mono, circuit-breaker open/closed), pannello "Routing chain" (ordine fallback con frecce →), azione "Rotate API key" (superadmin, mostra una volta in modal).
- **C4 · LLM Emergency Controls** (`NAV_ID: 'emergency'`, superadmin+step-up): alert-banner.danger in testa "Zona critica". Grid di "kill-switch" (admin-toggle) per: pausa ingestion, blocco nuove chat, force cache flush, rate-limit reset, emergency shutdown. Ogni switch pericoloso → typed-confirm (es. digita "EMERGENCY"). Mostra il pattern, non il backend.
- **C5 · Budget & Cost** (`NAV_ID: 'budget'`, admin): KPI (spesa oggi/mese, budget residuo, proiezione fine mese), grafico costi per provider (barre/area con sparkline), tabella "cost breakdown" per feature (chat, embedding, reranking, mechanic-extractor), simulatore costo (admin-form-row con input → risultato).
- **C6 · Secrets Vault** (`NAV_ID: 'secrets'`, superadmin+step-up): alert-banner.warning "Modifiche audit-logged". Tabella secret (nome mono, scope, ultima rotazione, stato) — i valori sono SEMPRE mascherati (`••••••`); azioni Reveal (step-up)/Rotate/Delete (typed-confirm). Mai mostrare valori in chiaro nel mockup.
- **C7 · Alerting Config** (`NAV_ID: 'alerts'`, admin): lista regole alert (admin-table: nome · metrica · soglia · finestra · severità status-chip · stato toggle), form "Nuova regola" (admin-form-row: metrica select, operatore, soglia, durata, canale Slack/email), pannello "Alert recenti" (activity-feed con severità colorata).
- **D2 · Agent Sandbox & Debug** (`NAV_ID: 'sandbox'`, admin): admin-shell-3col. Sx config (select agente/gioco, parametri), centro chat di test (bolle + retrieval inline), dx pannello debug (pipeline trace tree con step + latenza, waterfall a segmenti). Pannello "live" con dot .running durante lo stream.
- **D3 · A/B Testing agenti** (`NAV_ID: 'ab-testing'`, admin): KPI (test attivi, vincitore corrente, lift %), tabella esperimenti (nome · variante A/B · metrica · campione · significatività status-chip), grafico confronto due linee, azioni start/stop/promote-winner (confirm).
- **D4 · Prompt Management** (`NAV_ID: 'prompts'`, admin): admin-shell-3col. Sx lista template prompt (filtro per agente), centro editor (textarea mono con evidenza `{{variabili}}`), dx versioni (timeline) + preview. Tabs Edit · Preview · Versioni. Azione "Activate version" (confirm).
- **D5 · RAG Backup & Seeding** (`NAV_ID: 'rag-backup'`, superadmin per seed): KPI (ultimo backup, dimensione indice, giochi indicizzati), tabella backup (data · dimensione · stato · azioni restore), pannello "Seeding" con progress (barra + coda) e bottone "Reindex all" (typed-confirm, costoso). Pannello "live" SSE per il progress.
- **D6 · Integrations: Slack & Email Templates** (`NAV_ID: 'integrations'`, admin): admin-tabs "Slack" · "Email templates". Tab Slack: stato connessione (status-chip), canali configurati (#ops, #alerts), test-message. Tab Email: lista template (admin-table) + editor (textarea mono Handlebars) + PreviewFrame (riquadro 380px) + test-send form.

---

## 8. Checklist per ogni mockup generato

Prima di considerare "fatto" un `sp5-<id>.html`:

- [ ] Importa i 4 asset (`tokens.css`, `components.css`, `admin-base.css`, `admin-nav.js`) via `<link>`/`<script>` (NON `<style>` inline nel file di repo)
- [ ] `data-theme="dark"` di default; toggle funzionante via `admin-nav.js`
- [ ] Blocco `.admin-mobile-fallback` presente; shell nascosta `<880px`
- [ ] Density alta, `font-size: var(--fs-sm)` ereditato da `.admin-page`
- [ ] **Zero hex hardcoded** — solo `var(--*)` / classi `admin-*` / utility `.e-*`
- [ ] Dati realistici (no Lorem ipsum, no "TODO")
- [ ] Azioni sensibili mostrano il pattern (ConfirmModal / typed-confirm / step-up badge)
- [ ] Stati: almeno default; empty/error dove rilevante
- [ ] `renderAdminNav('<NAV_ID>')` con id corretto
- [ ] Voce aggiunta in `admin/admin-nav.js` (nuovo gruppo "Platform & Operations" / "AI Tooling & Data Quality") se il NAV_ID è nuovo
- [ ] Riga aggiunta in `SCREENS.md` e stato aggiornato in §4 di questo file

---

## 9. Dopo i mockup → consolidamento

Una volta disegnato il surplus, si chiude il cerchio del driver *"Consolidare IA + re-skin"*: si ridefinisce la sidebar (gruppi A/B/C/D), si riconcilia la duplicazione IA (AI/RAG/KB su più path → un canonico + redirect) e si pianifica il re-skin verso la shell ibrida sidebar+drawer. Quel passaggio è **implementazione** e va trattato con il flusso design → plan (non in questa guida, che resta sul piano del *design dei mockup*).
