# Happy Path — A5 Monitoraggio & Utenti (admin)

> Catalogo scenari happy-path per l'area **A5 — Monitoraggio & Utenti admin**.
> Formato: [`_TEMPLATE.md`](./_TEMPLATE.md) · Mappa route→area: [`_coverage-map.md`](./_coverage-map.md) (sezione A5).
> Solo **happy path**. Osservabili **strutturali** (heading, tab attivo, riga tabella, chip stato, toast), mai testo esatto generato.

## Prerequisiti dati (seed)

- Stack completo: `cd infra && make dev` (serve lo stack di monitoring per grafana/logs/containers/service-calls).
- `make seed-sp4` — popola admin + 5 utenti (`marco|sara|luca|giulia|andrea@meepleai.test`, ruolo `User`) + giochi + PDF indicizzati.
- **Utente**: **admin** (da `infra/secrets/admin.secret`). L'intera area `/admin/(dashboard)/**` è protetta da `RequireRole allowedRoles={['Admin']}` (`admin/(dashboard)/layout.tsx`) → login admin obbligatorio per ogni scenario. Se il cookie `meepleai_view_mode === 'user'` il layout reindirizza a `/` prima di renderizzare la shell admin: assicurarsi di essere in view-mode admin.

## Note di ambiente (dashboard read-only)

Molte pagine `/admin/monitor/**` sono dashboard **read-only/embed** dipendenti dallo stack di osservabilità. Con `make dev` (full) lo stack è previsto attivo; se manca, il criterio Smoke resta comunque soddisfatto tramite **empty-state legittimo** (es. "No containers found. Make sure Docker Socket Proxy is running.", "Loki log aggregation not available."). Un empty-state esplicito **non** è un fail. Casi che possono richiedere `⚠️ blocked-env`:

- **Grafana** (`/admin/monitor/grafana`, e i tab "Grafana" di monitor/logs): richiedono `NEXT_PUBLIC_GRAFANA_URL` configurato e un'istanza Grafana raggiungibile. Se assente, la pagina mostra "Grafana Not Configured" → lo Smoke passa sul messaggio di non-configurazione; l'embed effettivo dell'iframe è `blocked-env` senza lo stack.
- **Containers / Container logs** (`/admin/monitor/containers`, tab "Container Logs" di logs): richiedono il Docker Socket Proxy. Senza, empty-state legittimo → Smoke pass.

---

## Matrice di copertura

| # | Route | Scenario | Liv. |
|---|-------|----------|------|
| 1 | `admin/page.tsx` (redirect/landing) | A5-01 | Smoke |
| 2 | `admin/(dashboard)/overview` | A5-02 | Smoke |
| 3 | `admin/(dashboard)/overview/activity` | A5-03 | Smoke |
| 4 | `admin/(dashboard)/overview/system` | A5-04 | Smoke |
| 5 | `admin/(dashboard)/monitor` | A5-05 | Smoke |
| 6 | `admin/(dashboard)/monitor/grafana` | A5-06 | Smoke |
| 7 | `admin/(dashboard)/monitor/mau` | A5-07 | Smoke |
| 8 | `admin/(dashboard)/monitor/logs` | A5-08 | Smoke |
| 9 | `admin/(dashboard)/monitor/services` | A5-09 | Smoke |
| 10 | `admin/(dashboard)/monitor/service-calls` | A5-10 | Smoke |
| 11 | `admin/(dashboard)/monitor/operations` | A5-11 | Smoke |
| 12 | `admin/(dashboard)/monitor/containers` | A5-12 | Smoke |
| 13 | `admin/(dashboard)/monitor/wikidata-dead-letters` | A5-13 | Smoke |
| 14 | `admin/(dashboard)/analytics` | A5-14 | Smoke |
| 15 | `admin/(dashboard)/users` | A5-15 | Smoke |
| 16 | `admin/(dashboard)/users/[id]` | A5-16 | Smoke |
| 17 | `admin/(dashboard)/users/activity` | A5-17 | Smoke |
| 18 | `admin/(dashboard)/users/access-requests` | A5-18 | Flow |
| 19 | `admin/(dashboard)/users/invitations` | A5-19 | Flow |
| 20 | `admin/(dashboard)/users/roles` | A5-20 | Smoke |
| 21 | `admin/(dashboard)/users/[id]` (tab Role) | A5-21 | Flow |
| 22 | `admin/(dashboard)/notifications/compose` | A5-22 | Flow |
| 23 | `admin/(dashboard)/ui-library` | A5-23 | Smoke |
| 24 | `admin/(dashboard)/ui-library/[id]` | A5-24 | Smoke |
| 25 | `admin/(dashboard)/ui-library/compositions` | A5-25 | Smoke |
| 26 | `admin/(dashboard)/ui-library/compositions/[id]` | A5-26 | Smoke |
| 19 | `admin/(dashboard)/users/invitations` (ciclo CRUD) | A5-27 | Flow |
| 21 | `admin/(dashboard)/users/[id]` (tab Role — persistenza) | A5-28 | Flow |

> `admin/(dashboard)/business` compare nella sezione A5 della mappa ma è rimandato esplicitamente ad **A4** (`→ vedi A4`): non è coperto qui.
> `users/[id]` compare due volte nell'inventario route: A5-16 copre la vista di dettaglio read-only (Overview), A5-21/A5-28 coprono il flusso Flow di cambio ruolo (tab Role). Tutti mappano alla stessa route.
> `users/invitations` è coperta da A5-19 (invio singolo) e A5-27 (ciclo CRUD crea→revoca con verifica persistenza): stessa route, focus complementari.

**Conteggio**: 28 scenari · 22 Smoke · 6 Flow. Tutte le 25 route dell'area sono coperte (nessun `smoke-aggregato`, nessun `skip`). A5-27/A5-28 rafforzano la copertura CRUD delle entità utenti/inviti (spec §3.1) senza aggiungere route nuove.

---

## Scenari

### Landing & Overview

```gherkin
Scenario A5-01 [Smoke]: Redirect landing admin → Overview
  Given sono loggato come admin (view-mode admin)
  When apro /admin
  Then vengo reindirizzato a /admin/overview
    And la pagina Overview si carica dentro la shell admin (sidebar admin visibile)
  Osservabile ✅: URL finale = /admin/overview + heading "Overview sistema" visibile + nessun errore Console/Network
  Route: admin/page.tsx → admin/(dashboard)/overview
  Utente: admin
```

```gherkin
Scenario A5-02 [Smoke]: Overview sistema con KPI e riepiloghi
  Given sono loggato come admin
    And il seed ha popolato giochi e utenti
  When apro /admin/overview
  Then lo skeleton (card pulsanti) lascia il posto ai dati reali
    And vedo la riga KPI (totale giochi, totale utenti, approvazioni in sospeso)
    And vedo le card riepilogo "Azioni rapide" nella sidebar
  When clicco il bottone "Refresh"
  Then i dati vengono ricaricati senza errori
  Osservabile ✅: heading "Overview sistema" + riga KPI con numeri (≥0) + card LibrarySummary/UsersSummary + bottone Refresh produce refetch (nessun errore)
  Route: admin/(dashboard)/overview
  Utente: admin
```

```gherkin
Scenario A5-03 [Smoke]: Activity Log admin (timeline)
  Given sono loggato come admin
  When apro /admin/overview/activity
  Then vedo la timeline eventi admin (dati placeholder legittimi, con banner informativo)
  When cambio il filtro "Activity type" da "All Activities" a "Users"
  Then la timeline si filtra mostrando solo le voci categoria "users"
  Osservabile ✅: heading "Activity Log" + timeline con ≥1 voce + il cambio filtro riduce visibilmente le voci mostrate
  Route: admin/(dashboard)/overview/activity
  Utente: admin
  Nota: pagina a dati placeholder (audit API non ancora collegata) — banner "Showing placeholder data" è atteso, non è un errore.
```

```gherkin
Scenario A5-04 [Smoke]: System Health (servizi + metriche API)
  Given sono loggato come admin
    And lo stack è avviato con make dev
  When apro /admin/overview/system
  Then "Loading..." lascia il posto alla griglia servizi (o empty-state "No services found")
    And se disponibili vedo la card stato complessivo e le metriche API (24h)
  Osservabile ✅: heading "System Health" + griglia servizi popolata OPPURE empty-state legittimo "No services found" + nessun errore Console/Network non atteso
  Route: admin/(dashboard)/overview/system
  Utente: admin
```

### Monitor (dashboard read-only / embed)

```gherkin
Scenario A5-05 [Smoke]: Monitor hub — tab bar e switch tab
  Given sono loggato come admin
  When apro /admin/monitor
  Then vedo la tab bar del monitor (Alerts, Metrics, Infrastructure, MAU, Containers, Logs, Grafana, …)
    And il tab di default "Alerts" carica il proprio contenuto (skeleton → contenuto)
  When clicco un altro tab (es. "Metrics")
  Then il contenuto del tab cambia (skeleton di transizione → contenuto reale)
  Osservabile ✅: tab bar con le etichette visibili + il click su un tab cambia visibilmente il contenuto + nessun errore Console/Network non atteso
  Route: admin/(dashboard)/monitor
  Utente: admin
```

```gherkin
Scenario A5-06 [Smoke]: Grafana dashboards (picker + embed)
  Given sono loggato come admin
  When apro /admin/monitor/grafana
  Then se NEXT_PUBLIC_GRAFANA_URL è configurato vedo il picker per categorie (Application, Infrastructure, AI Services, Security)
    And selezionando una dashboard card compaiono i controlli time-range (15m/1h/6h/24h/7d) e l'iframe si carica
  Osservabile ✅: heading "Grafana Dashboards" + picker categorie visibile + selezione dashboard mostra i bottoni time-range
  Route: admin/(dashboard)/monitor/grafana
  Utente: admin
  Nota ambiente: senza NEXT_PUBLIC_GRAFANA_URL la pagina mostra "Grafana Not Configured" → lo Smoke passa sul messaggio di non-configurazione; l'embed effettivo dell'iframe è ⚠️ blocked-env senza lo stack di monitoring.
```

```gherkin
Scenario A5-07 [Smoke]: MAU Monitoring Dashboard (KPI + tabella)
  Given sono loggato come admin
  When apro /admin/monitor/mau
  Then lo skeleton delle KPI card lascia il posto ai valori (Total Active Users, AI Chat Users, PDF Upload Users, Agent Users)
  When clicco il bottone periodo "90d"
  Then le KPI card e la tabella (Date/Total/AI Chat/PDF Upload) si ricaricano per il nuovo periodo
  Osservabile ✅: heading "MAU Monitoring Dashboard" + 4 KPI card con valori + il cambio periodo produce un refetch visibile
  Route: admin/(dashboard)/monitor/mau
  Utente: admin
```

```gherkin
Scenario A5-08 [Smoke]: Log Viewer (tab + filtro applicazione)
  Given sono loggato come admin
  When apro /admin/monitor/logs
  Then vedo i tab "Application Logs" / "Container Logs" / "Container Errors" con il tab Application attivo
    And la tabella application (Time/Level/Source/Message) mostra righe OPPURE l'empty-state "No log entries found."
  When digito un termine nel campo di ricerca e clicco "Apply"
  Then la tabella si aggiorna con il filtro applicato
  Osservabile ✅: heading "Log Viewer" + tab bar visibile + il click su "Apply" produce un refetch della tabella (righe o empty-state legittimo)
  Route: admin/(dashboard)/monitor/logs
  Utente: admin
  Nota ambiente: tab "Container Logs" senza Docker Proxy → empty-state legittimo; tab "Container Errors" senza Loki → messaggio "Loki log aggregation not available." (entrambi Smoke pass, non fail).
```

```gherkin
Scenario A5-09 [Smoke]: Service Dashboard (health + auto-refresh)
  Given sono loggato come admin
    And lo stack è avviato con make dev
  When apro /admin/monitor/services
  Then vedo il banner stato complessivo ("System Healthy/Degraded/…" + conteggio servizi) e la riga metriche (API Requests/Avg Latency/Error Rate/LLM Cost)
  When clicco il bottone "Pause" dell'auto-refresh
  Then il countdown si ferma (auto-refresh in pausa) senza errori
  Osservabile ✅: heading "Service Dashboard" + banner stato complessivo con conteggio + il toggle Pause produce un effetto visibile sul countdown
  Route: admin/(dashboard)/monitor/services
  Utente: admin
```

```gherkin
Scenario A5-10 [Smoke]: Service Call History (summary + filtro tabella)
  Given sono loggato come admin
  When apro /admin/monitor/service-calls
  Then vedo le summary card per periodo (default 24h) OPPURE empty-state "No service call data for this period."
    And la tabella storico (Time/Service/Method/URL/Status/Latency) è visibile o mostra "No service calls found."
  When cambio il periodo (es. "7d") e clicco "Apply" sul filtro
  Then summary e tabella si ricaricano
  Osservabile ✅: heading "Service Call History" + summary card o empty-state legittimo + il filtro produce un refetch visibile
  Route: admin/(dashboard)/monitor/service-calls
  Utente: admin
```

```gherkin
Scenario A5-11 [Smoke]: Operations Console (4 tab)
  Given sono loggato come admin
  When apro /admin/monitor/operations
  Then vedo la tab bar con "Resources" / "Queue" / "Emergency" / "Audit" e il tab "Resources" attivo di default
  When clicco il tab "Audit"
  Then il contenuto del tab cambia (skeleton di transizione → contenuto)
  Osservabile ✅: heading "Operations Console" + 4 tab visibili + il click su un tab cambia visibilmente il contenuto
  Route: admin/(dashboard)/monitor/operations
  Utente: admin
```

```gherkin
Scenario A5-12 [Smoke]: Infrastructure & Containers (KPI + eventi live)
  Given sono loggato come admin
  When apro /admin/monitor/containers
  Then vedo la striscia KPI con sparkline e la dashboard container (griglia o empty-state)
    And vedo la sezione "Eventi live" con l'indicatore stato SSE
  Osservabile ✅: heading "Infrastructure & Containers" + striscia KPI + sezione "Eventi live" con indicatore SSE + nessun errore Console/Network non atteso
  Route: admin/(dashboard)/monitor/containers
  Utente: admin
  Nota ambiente: senza Docker Socket Proxy la griglia container mostra empty-state legittimo (Smoke pass).
```

```gherkin
Scenario A5-13 [Smoke]: Wikidata dead-letters (tabella + filtro)
  Given sono loggato come admin
  When apro /admin/monitor/wikidata-dead-letters
  Then vedo la tabella dead-letters (Game/Reason/Details/Dead-lettered/Retries/Action) con l'indicatore SSE e il conteggio "X dead-letters matching filter"
    And se non ci sono record vedo l'empty-state "No dead-letters match the current filter."
  When cambio il filtro "Reason" (es. da "Any reason" a una causa specifica)
  Then la tabella si ricarica per il nuovo filtro
  Osservabile ✅: heading "Wikidata enrichment — dead-letters" + tabella o empty-state legittimo + il cambio filtro produce un refetch visibile
  Route: admin/(dashboard)/monitor/wikidata-dead-letters
  Utente: admin
```

### Analytics

```gherkin
Scenario A5-14 [Smoke]: Analitiche — hub a 5 tab
  Given sono loggato come admin
  When apro /admin/analytics
  Then vedo la tab bar "Overview" / "AI Usage" / "Audit Log" / "Reports" / "API Keys" con "Overview" attivo (?tab=overview)
  When clicco il tab "AI Usage"
  Then l'URL diventa ?tab=ai-usage e il contenuto del tab cambia (skeleton → contenuto)
  Osservabile ✅: heading "Analitiche" + tab bar a 5 voci + il click aggiorna il tab attivo e il query param + nessun errore Console/Network
  Route: admin/(dashboard)/analytics
  Utente: admin
```

### Utenti

```gherkin
Scenario A5-15 [Smoke]: Lista utenti + inviti in sospeso
  Given sono loggato come admin
    And il seed ha creato 5 utenti (marco/sara/luca/giulia/andrea)
  When apro /admin/users
  Then vedo la tabella utenti (colonne Utente/Ruolo/Stato/Azioni) popolata dagli utenti seed
    And eventuali inviti in sospeso compaiono in cima con riga ambra
  When digito "marco" nel campo "Cerca per nome o email..."
  Then la tabella si filtra sull'utente corrispondente
  Osservabile ✅: heading "Utenti" + ≥1 riga utente (es. marco@meepleai.test) + il filtro ricerca riduce visibilmente le righe
  Route: admin/(dashboard)/users
  Utente: admin
```

```gherkin
Scenario A5-16 [Smoke]: Dettaglio utente (Overview)
  Given sono loggato come admin
    And esiste l'utente seed marco@meepleai.test
  When apro /admin/users da /admin/users e clicco sulla riga di "Marco R." (oppure navigo a /admin/users/{marcoId})
  Then si apre la pagina di dettaglio con header (nome, badge ruolo, badge stato, email, data iscrizione)
    And il tab "Overview" mostra le card Info Account / Utilizzo / Libreria e Attività
  When clicco il tab "Activity Log"
  Then compare la tabella audit del singolo utente (Timestamp/Action/Resource/Result/Details)
  Osservabile ✅: heading = nome utente + tab Overview/Role/Activity Log visibili + il cambio tab mostra un contenuto diverso
  Route: admin/(dashboard)/users/[id]
  Utente: admin
```

```gherkin
Scenario A5-17 [Smoke]: Audit Log utenti (filtro)
  Given sono loggato come admin
  When apro /admin/users/activity
  Then vedo la tabella audit (Timestamp/User/Action/Resource/IP Address/Result) e il conteggio "X registrazioni trovate"
    And il pannello filtri mostra Search User / Action Type / Date Range e il bottone "Esporta CSV"
  When cambio il filtro "Action Type" (es. da "All Actions" a "Login")
  Then la tabella si ricarica per il filtro selezionato
  Osservabile ✅: heading "Audit Log" + tabella o empty-state legittimo + il cambio filtro produce un refetch visibile
  Route: admin/(dashboard)/users/activity
  Utente: admin
```

```gherkin
Scenario A5-18 [Flow]: Approva una richiesta di accesso in sospeso
  Given sono loggato come admin
    And esiste ≥1 richiesta di accesso in stato "Pending"
      (precondizione dati: creabile inviando una request-access da /register con reg. invite-only,
       usando un'email marcata HP-TEST-2026-07-10, es. hp-test-2026-07-10-access@meepleai.test)
  When apro /admin/users/access-requests
    And nella riga della richiesta Pending clicco "Approva"
  Then la richiesta viene approvata (POST approveAccessRequest)
    And il badge di stato della riga passa a "Approved" (verde) e i bottoni azione spariscono
    And le KPI in cima (In attesa / Approvati) si aggiornano
  Osservabile ✅: heading "Richieste di Accesso" + toast di conferma + badge riga → "Approved" + KPI aggiornate
  Route: admin/(dashboard)/users/access-requests
  Utente: admin
  Nota: se nessuna richiesta Pending è presente e non è possibile crearne una in locale, marcare ⚠️ blocked-env; lo Smoke di caricamento (tabella + KPI + empty-state "Nessuna richiesta di accesso") resta comunque verificabile.
```

```gherkin
Scenario A5-19 [Flow]: Invia un invito utente
  Given sono loggato come admin
  When apro /admin/users/invitations
    And clicco "Invita Utente"
  Then si apre il dialog invito (campo email + selettore ruolo)
  When inserisco l'email HP-TEST-2026-07-10 (es. hp-test-2026-07-10-invite@meepleai.test), scelgo il ruolo "user" e confermo
  Then l'invito viene creato (POST sendInvitation)
    And compare un toast di conferma e una nuova riga invito "Pending" in tabella
    And le KPI (Totale / In attesa) si aggiornano
  Osservabile ✅: heading "Inviti" + toast conferma + nuova riga "Pending" con l'email HP-TEST + KPI aggiornate
  Route: admin/(dashboard)/users/invitations
  Utente: admin
  Cleanup: revocare l'invito HP-TEST creato ("Revoke" sulla riga) a fine giro, per ripetibilità.
```

```gherkin
Scenario A5-20 [Smoke]: Ruoli & Permessi (matrice)
  Given sono loggato come admin
  When apro /admin/users/roles
  Then vedo la matrice permessi (righe = permessi, colonne = Admin/Editor/User/Anonymous con conteggi)
    And le celle mostrano check/❌ per ciascun ruolo (es. "Manage Users" spuntato solo per Admin)
  Osservabile ✅: heading "Ruoli e Permessi" + matrice con ≥1 riga permesso + colonne ruolo con conteggi
  Route: admin/(dashboard)/users/roles
  Utente: admin
  Nota: pagina di riferimento a matrice statica (nessuna API, nessuna mutazione) → livello Smoke. Il briefing di area indicava "gestisci roles" come Flow, ma il cambio ruolo effettivo è un flusso su /admin/users/[id] (tab Role) → coperto da A5-21.
```

```gherkin
Scenario A5-21 [Flow]: Cambia il ruolo di un utente (tab Role del dettaglio)
  Given sono loggato come admin
    And esiste l'utente seed luca@meepleai.test (ruolo iniziale "User")
  When apro /admin/users/{lucaId} e seleziono il tab "Role"
    And apro il dropdown ruolo, scelgo un ruolo diverso (es. "editor") e (opzionale) inserisco una motivazione
    And clicco "Change Role" e confermo nel dialog
  Then il ruolo viene aggiornato (changeUserRole)
    And compare un toast di conferma
    And la tabella "Role History" acquisisce una nuova riga e il badge ruolo nell'header si aggiorna
  Osservabile ✅: toast di conferma + badge ruolo header aggiornato + nuova riga in "Role History"
  Route: admin/(dashboard)/users/[id]
  Utente: admin
  Cleanup: riportare luca al ruolo "User" a fine scenario, per non erodere le precondizioni degli altri cataloghi (utenti seed = ruolo User).
```

### Cicli CRUD & persistenza (utenti/inviti)

```gherkin
Scenario A5-27 [Flow]: Ciclo CRUD invito — crea → revoca con verifica persistenza
  Given sono loggato come admin
    And apro /admin/users/invitations
  # --- CREATE ---
  When clicco "Invita Utente", inserisco l'email HP-TEST-2026-07-10 (es. hp-test-2026-07-10-crud@meepleai.test),
       scelgo il ruolo "User" e confermo "Send Invitation"
  Then l'invito viene creato (POST /api/v1/admin/users/invite)
    And compare un toast di conferma e una nuova riga "Pending" con l'email HP-TEST in tabella
    And le KPI (Totale / In attesa) si incrementano
  When ricarico la pagina (reload di /admin/users/invitations)
  Then l'invito HP-TEST è ancora presente in tabella con stato "Pending" (persistito nel backend, non solo feedback ottimistico)
  # --- DELETE (revoca) ---
  When nella riga dell'invito HP-TEST clicco "Revoke" e confermo nel dialog
  Then l'invito viene revocato (DELETE /api/v1/admin/users/invitations/{id})
    And compare un toast di conferma e la riga esce dall'elenco "Pending" (stato → "Revoked" o riga rimossa dal filtro corrente)
    And le KPI (In attesa ↓, Revocati ↑) si aggiornano
  When ricarico la pagina e applico il filtro stato "In attesa" (Pending)
  Then l'invito HP-TEST NON compare più tra i Pending (revoca persistita); filtrando su "Revocati" l'invito compare con stato "Revoked"
  Osservabile ✅: invito HP-TEST presente post-create+reload (Pending) · assente dai Pending post-revoke+reload (persistito come Revoked) · toast su ciascuna operazione · KPI coerenti
  Route: admin/(dashboard)/users/invitations
  Utente: admin
  Dati creati: invito "hp-test-2026-07-10-crud@meepleai.test" (revocato a fine ciclo → stato terminale, non ripetibile sullo stesso indirizzo se già presente)
  Nota operazioni: l'entità Invitation espone Create (POST /invite) + Delete/revoca (DELETE /invitations/{id}) + Resend (POST /invitations/{id}/resend). NON esiste un'operazione di edit in-place di un invito nella UI (nessun "modifica invito") → il ciclo è Create → Delete, senza step Edit (nessun Edit inventato).
```

```gherkin
Scenario A5-28 [Flow]: Persistenza edit ruolo utente — cambia → reload conferma → ripristina
  Given sono loggato come admin
    And esiste l'utente seed giulia@meepleai.test (ruolo iniziale "User")
  # --- EDIT (persistenza) ---
  When apro /admin/users/{giuliaId} e seleziono il tab "Role"
    And apro il dropdown "New Role", scelgo "editor", clicco "Change Role" e confermo nel dialog
  Then il ruolo viene aggiornato (PUT /api/v1/admin/users/{id}/role)
    And compare un toast/riga di conferma, il badge ruolo nell'header diventa "Editor" e "Role History" acquisisce una nuova riga (User → Editor)
  When ricarico la pagina (reload di /admin/users/{giuliaId})
  Then il badge ruolo nell'header è ancora "Editor" e la riga (User → Editor) è ancora in "Role History" (edit persistito, non solo feedback ottimistico)
  # --- RESTORE (non distruttivo) ---
  When nel tab "Role" seleziono di nuovo "user", clicco "Change Role" e confermo
  Then il ruolo torna "User" (nuova riga Editor → User in "Role History")
  When ricarico la pagina
  Then il badge ruolo nell'header è di nuovo "User" (stato iniziale ripristinato e persistito)
  Osservabile ✅: badge header "Editor" post-edit+reload · riga (User→Editor) in Role History · badge header "User" post-restore+reload · Role History con entrambe le transizioni
  Route: admin/(dashboard)/users/[id]
  Utente: admin
  Dati modificati: giulia@meepleai.test (User → Editor → User) — stato finale = iniziale, non distruttivo, non erode le precondizioni degli altri cataloghi (utenti seed = ruolo User)
  Nota operazioni: l'entità User espone in UI solo l'edit del ruolo (PUT /users/{id}/role), sia inline in lista (InlineRoleSelect) sia nel tab Role del dettaglio (ChangeRoleCard). Le operazioni Create utente, Delete utente e Suspend/Unsuspend ESISTONO nell'API client (`api.admin.createUser` → POST /users, `deleteUser` → DELETE /users/{id}, `suspendUser`/`unsuspendUser` → POST /users/{id}/suspend|unsuspend) ma NON sono cablate in alcuna UI dell'area utenti: il badge "Sospeso/Attivo" è di sola lettura, non c'è pulsante Sospendi/Elimina/Nuovo utente. Perciò il ciclo CRUD user in-browser copre solo Edit (ruolo); Create/Delete/Suspend restano non testabili via browser fino a quando la UI non li espone (annotazione, nessun pulsante inventato).
```

### Notifiche

```gherkin
Scenario A5-22 [Flow]: Componi e invia una notifica manuale
  Given sono loggato come admin
  When apro /admin/notifications/compose
  Then vedo il form (card Channels / Recipients / Message) e il pannello "PREVIEW" a destra
  When compilo "Title" = "HP-TEST 2026-07-10 Avviso" e "Body" con un testo di prova
    And lascio almeno un canale selezionato (default "inapp") e destinatari = "all" (default)
    And clicco "Send Notification"
  Then la notifica viene inviata (POST /api/v1/admin/notifications/send)
    And compare un toast di successo ("Notification sent to N recipient(s)")
    And il form si resetta (Title/Body svuotati, canali → default, destinatari → all)
  Osservabile ✅: heading "Compose Notification" + il preview riflette il titolo digitato + toast di successo dopo l'invio + form resettato
  Route: admin/(dashboard)/notifications/compose
  Utente: admin
  Nota: invio reale ai destinatari (cap 100). Usare il marcatore HP-TEST nel titolo e destinatari "all" solo su seed locale; su staging valutare l'impatto prima di eseguire.
```

### UI Library

```gherkin
Scenario A5-23 [Smoke]: UI Library — griglia componenti + filtro
  Given sono loggato come admin
  When apro /admin/ui-library
  Then vedo la griglia dei componenti del registry con la barra di ricerca/filtro e la sidebar filtri
    And la sezione "Compositions" (teaser) è visibile senza filtri attivi
  When digito un termine nella ricerca (es. "card")
  Then la griglia si filtra sui componenti corrispondenti
  Osservabile ✅: heading "UI Library" + griglia con ≥1 card componente + il filtro ricerca riduce visibilmente le card
  Route: admin/(dashboard)/ui-library
  Utente: admin
```

```gherkin
Scenario A5-24 [Smoke]: Dettaglio componente UI Library
  Given sono loggato come admin
  When apro /admin/ui-library/meeple-card (id reale dal COMPONENT_REGISTRY)
  Then si apre la vista di dettaglio del componente con anteprima live e documentazione
    And l'heading corrisponde al nome del componente (es. "MeepleCard")
    And è presente il link "back" verso la UI Library
  Osservabile ✅: heading = nome componente + area di anteprima renderizzata + link back visibile + nessun 404
  Route: admin/(dashboard)/ui-library/[id]
  Utente: admin
  Dati: id di esempio validi — meeple-card, badge, avatar, data-table, accordion.
```

```gherkin
Scenario A5-25 [Smoke]: UI Library — Compositions (griglia scene)
  Given sono loggato come admin
  When apro /admin/ui-library/compositions
  Then vedo la griglia delle composizioni (nome, badge area, descrizione, conteggio componenti)
    And è presente il link back "UI Library"
  When clicco su una card composizione (es. "Entity Cards")
  Then navigo al dettaglio della composizione
  Osservabile ✅: heading "Compositions" + griglia con ≥1 card composizione + il click apre il dettaglio (navigazione avvenuta)
  Route: admin/(dashboard)/ui-library/compositions
  Utente: admin
```

```gherkin
Scenario A5-26 [Smoke]: Dettaglio composizione UI Library
  Given sono loggato come admin
  When apro /admin/ui-library/compositions/entity-cards (id reale dal COMPOSITIONS registry)
  Then si apre il dettaglio con l'heading = nome composizione + badge area, la sezione "Scene" con anteprima
    And la sezione "Components in this Composition" elenca i chip cliccabili dei componenti (es. "meeple-card")
    And è presente il link back "Compositions"
  When clicco un chip componente (es. "meeple-card")
  Then navigo al dettaglio di quel componente (/admin/ui-library/meeple-card)
  Osservabile ✅: heading = nome composizione + sezione "Scene" renderizzata + chip componenti cliccabili + il click su un chip naviga al dettaglio componente
  Route: admin/(dashboard)/ui-library/compositions/[id]
  Utente: admin
  Dati: id di esempio validi — entity-cards, rag-pipeline, game-management, agent-builder, user-admin.
```

---

## Auto-verifica

- **Copertura**: tutte le 25 route dell'area A5 (mappa `_coverage-map.md`) compaiono nella matrice → mappate ad ≥1 scenario. `admin/(dashboard)/business` è escluso per rimando esplicito ad A4. Nessuna route resta scoperta, nessun `smoke-aggregato`, nessun `skip`. A5-27/A5-28 non aggiungono route: riusano `users/invitations` e `users/[id]` per la copertura CRUD.
- **Osservabili**: ogni scenario dichiara ≥1 `Osservabile ✅` strutturale (heading, tab attivo, riga/chip, toast, navigazione, badge post-reload), mai testo LLM letterale.
- **Solo happy path**: nessuno scenario negativo/errore/edge; gli empty-state citati sono esiti legittimi dello Smoke, non fallimenti.
- **Flow**: 6 scenari transazionali con effetto verificabile — A5-18 (approva access-request, con precondizione dati e fallback blocked-env), A5-19 (invia invito, con cleanup), A5-21 (cambia ruolo, con cleanup), A5-22 (invia notifica), A5-27 (ciclo CRUD invito crea→revoca con reload di persistenza), A5-28 (persistenza edit ruolo con reload + ripristino non distruttivo). Le entità/azioni create usano il marcatore `HP-TEST-2026-07-10`.
- **CRUD & persistenza (spec §3.1)**: gli scenari-ciclo verificano la mutazione reale via **reload di verifica**, non il solo feedback ottimistico —
  - **Invitation** (`/admin/users/invitations`): ciclo **Create → Delete/revoca** (A5-27). Delete/revoca **disponibile** (DELETE /invitations/{id}). **Edit assente** in UI (nessuna modifica in-place di un invito) → il ciclo è Create→Delete, senza step Edit inventato.
  - **User role** (`/admin/users/[id]` tab Role): **Edit** con persistenza (A5-28), ripristinato allo stato iniziale (non distruttivo).
  - **User** (create/disabilita/elimina): API presenti (`createUser`/`deleteUser`/`suspendUser`/`unsuspendUser`) ma **non esposte in alcuna UI** dell'area utenti (badge stato read-only) → Create/Delete/Suspend user **non testabili via browser**, annotato in A5-28 (nessun pulsante inventato).
  - **Access-request** (`/admin/users/access-requests`): approva/rifiuta (A5-18); nessun delete tipico per questa entità (coerente con lo spec).
  - Distruttività: i Delete operano **solo** su dati `HP-TEST-2026-07-10` (invito creato dallo scenario); l'edit ruolo opera su utente seed ma **ripristina** lo stato → utenti seed restano al ruolo "User".
- **Utente**: admin per tutti gli scenari (area protetta `RequireRole['Admin']`).
- **Ambiente**: le dipendenze dallo stack di osservabilità (Grafana, Docker Proxy, Loki) sono documentate come `blocked-env`/empty-state legittimo, non come fail.
