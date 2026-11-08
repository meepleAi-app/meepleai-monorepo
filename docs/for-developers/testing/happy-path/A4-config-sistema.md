# Happy Path — A4 · Config & Sistema (admin)

> Catalogo scenari happy-path per l'area **A4 — Config & Sistema admin**. Solo percorso di successo.
> Formato: vedi [`_TEMPLATE.md`](./_TEMPLATE.md). Mappa globale route→area: [`_coverage-map.md`](./_coverage-map.md) sezione A4.
> Strategia: [`2026-07-10-happy-path-testing-program-design.md`](../../../superpowers/specs/2026-07-10-happy-path-testing-program-design.md).

## Intestazione

- **Area**: A4 — Config & Sistema (configurazione di sistema, contenuti, provider LLM, sync DB).
- **Utente**: **admin** (da `infra/secrets/admin.secret`). Alcune azioni richiedono ruolo **superadmin** (Run probe provider, rotate key, staging allowlist) — dove serve è annotato come precondizione; se l'account admin non è superadmin lo step è `⚠️ blocked-env`.
- **Prerequisiti dati (seed `make seed-sp4`)**: tier di default già presenti (`free`/`premium`/…); template email seed (es. `game_night_invitation`, `email_verification`); provider LLM configurati via secret (`openrouter`/`deepseek`/`ollama-local`). n8n e staging-allowlist possono partire vuoti (empty-state legittimo).
- **⚠️ Operazioni sensibili**: `staging-access` (concede accesso reale allo staging) e `database-sync` → **Apply Migrations** (destruttiva, gated da tunnel SSH + typed-confirm). Catalogate come **Flow** ma i loro step distruttivi sono limitati alla parte read-only/non-impattante; l'esecuzione della parte mutante va fatta **con cautela** e su staging va preferibilmente ridotta a **Smoke** (vedi note nei singoli scenari).
- **Ripristino stato**: gli scenari che mutano config globale (Registration Mode) **DEVONO ripristinare lo stato originale** a fine scenario. Le entità create (tier, template, config n8n) usano marcatore `HP-TEST-<data>`.

## Matrice di copertura

| Route | Scenario/i | Tipo |
|-------|-----------|------|
| `admin/(dashboard)/config` (landing, tab General) | A4-01 | Smoke |
| `admin/(dashboard)/config` (tab General → Registration Mode toggle) | A4-02 | Flow |
| `admin/(dashboard)/config/tiers` (edit + persistenza) | A4-03 | Flow |
| `admin/(dashboard)/config/tiers` (ciclo CRUD create→edit; Delete assente in UI) | A4-13 | Flow (CRUD) |
| `admin/(dashboard)/config/n8n` | — | skip: feature n8n in rimozione |
| `admin/(dashboard)/content` (landing) | A4-05 | Smoke |
| `admin/(dashboard)/content/email-templates` (edit + persistenza + anteprima) | A4-06 | Flow |
| `admin/(dashboard)/content/email-templates` (ciclo CRUD create→edit-versione; Delete assente in UI) | A4-14 | Flow (CRUD) |
| `admin/(dashboard)/ai` (landing) | A4-07 | Smoke |
| `admin/(dashboard)/providers` (lista) | A4-08 | Smoke |
| `admin/(dashboard)/providers/[name]` (dettaglio + probe) | A4-09 | Flow |
| `admin/(dashboard)/staging-access` (ciclo add→remove + persistenza) | A4-10 | Flow (CRUD, op. sensibile) |
| `admin/(dashboard)/business` | A4-11 | Smoke |
| `admin/database-sync` (status tunnel + schema compare read-only) | A4-12 | Flow (op. sensibile, parte mutante esclusa) |

**Copertura**: 11 route A4 — 10 coperte + 1 `skip` (`config/n8n`, feature n8n in rimozione); 13 scenari. Doppioni per route: `config` landing (Smoke A4-01 + Flow A4-02 toggle), `config/tiers` (edit A4-03 + ciclo CRUD A4-13), `content/email-templates` (edit+anteprima A4-06 + ciclo CRUD A4-14). Nessuna route `smoke-aggregato`.

**Copertura CRUD & persistenza** (spec §3.1): per ogni entità gestibile in area A4 si dichiara il ciclo di vita coperto dalla UI e la verifica di persistenza via reload.

| Entità | Create | Edit/Save | Delete | Scenari | Persistenza (reload) |
|--------|--------|-----------|--------|---------|----------------------|
| **Tier** (`config/tiers`) | ✅ POST `/admin/tiers` | ✅ PUT `/admin/tiers/{name}` | ❌ **assente in UI** (client solo get/create/update) | A4-13 (ciclo), A4-03 (edit) | ✅ reload post-create e post-edit |
| **Email template** (`content/email-templates`) | ✅ POST `/admin/email-templates` | ✅ PUT `/admin/email-templates/{id}` (nuova versione) | ❌ **assente in UI** (no metodo delete; solo versioning) | A4-14 (ciclo), A4-06 (edit+anteprima) | ✅ reload post-create e post-edit (versione incrementata) |
| **Staging allowlist** (`staging-access`) | ✅ POST `/admin/staging-allowlist` | — (voci immutabili, nessun edit) | ✅ DELETE `/admin/staging-allowlist/{id}` (soft-delete) | A4-10 (ciclo add→remove) | ✅ reload post-add e post-remove |
| **Registration Mode** (`config` General) | — (config singleton, no create) | ✅ toggle | — (no delete) | A4-02 | stato ripristinato S0→S1→S0 (non-distruttivo) |

---

## Scenari

```gherkin
Scenario A4-01 [Smoke]: Config hub carica con tab General di default
  Given sono loggato come admin
  When apro /admin/config
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo l'intestazione "Configurazione"
    And vedo la tab-bar con le tab General · Limits · Feature Flags · Rate Limits · Banner
    And la tab "General" è attiva e mostra la card "Registration Mode"
    And la card "Registration Mode" mostra lo stato corrente ("Invite-only mode" oppure "Public registration enabled")
  Osservabile ✅: heading "Configurazione" + tab-bar con ≥5 tab + card "Registration Mode" con etichetta di stato non-"Loading..."
  Route: admin/(dashboard)/config
  Utente: admin
```

```gherkin
Scenario A4-02 [Flow]: Toggle Registration Mode e ripristino stato originale
  Given sono loggato come admin
    And apro /admin/config (tab General) e leggo lo stato corrente del toggle "Registration Mode" (es. "Invite-only mode")
    And annoto lo stato di partenza S0 (checked = public abilitato, oppure unchecked = invite-only)
  When clicco lo Switch "Toggle public registration" per invertirlo
    And nel dialog di conferma ("Enable Public Registration?" / "Enable Invite-Only Mode?") clicco "Confirm"
  Then compare un toast di successo ("Public registration enabled" oppure "Switched to invite-only mode")
    And l'etichetta di stato della card si aggiorna al nuovo valore S1 (opposto di S0)
  And per ripristinare lo stato: clicco di nuovo lo Switch, confermo nel dialog, e verifico che l'etichetta torni a S0
  Then l'etichetta di stato è di nuovo S0 (stato originale ripristinato)
  Osservabile ✅: toast di successo ad ogni toggle + etichetta card cambia S0→S1→S0 + a fine scenario lo stato è identico a quello di partenza (non-distruttivo)
  Route: admin/(dashboard)/config
  Utente: admin
```

```gherkin
Scenario A4-03 [Flow]: Modifica di un tier esistente con verifica di persistenza
  Given sono loggato come admin
    And esiste almeno un tier seed (es. "free" o "premium")
  When apro /admin/config/tiers
    And attendo il caricamento della tabella "Tier Management" (tiers-table)
    And leggo il Display Name corrente del tier "free" (per ripristinarlo a fine scenario)
    And clicco "Modifica" sulla riga del tier "free" (btn-edit-free)
    And nel dialog "Modifica tier: free" cambio il campo "Display Name" (field-displayName) in "HP-TEST-2026-07-10 Free"
    And clicco "Salva modifiche" (btn-save)
  Then compare il toast "Tier aggiornato"
    And il dialog si chiude
    And la riga del tier "free" (tier-row-free) nella tabella mostra il nuovo Display Name "HP-TEST-2026-07-10 Free"
  When ricarico la pagina /admin/config/tiers (reload di verifica persistenza)
  Then dopo il reload la riga del tier "free" mostra ancora il Display Name "HP-TEST-2026-07-10 Free" (valore persistito nel backend, non solo feedback ottimistico)
  And per ripristinare lo stato: riapro "Modifica" su "free", riporto il Display Name al valore originale e clicco "Salva modifiche"; ricarico e verifico che la cella mostri di nuovo il valore di partenza
  Osservabile ✅: tabella tier renderizzata + toast "Tier aggiornato" + cella Display Name aggiornata in tabella + valore persistito dopo reload + a fine scenario Display Name ripristinato all'originale (non-distruttivo)
  Route: admin/(dashboard)/config/tiers
  Utente: admin
  Nota: modifica additiva reversibile (rinomina display name con marcatore HP-TEST); i limiti numerici non vengono toccati. Il `PUT /api/v1/admin/tiers/{name}` persiste la modifica → il reload conferma la scrittura reale.
```

```gherkin
Scenario A4-05 [Smoke]: Content hub carica con tab Shared Games di default
  Given sono loggato come admin
  When apro /admin/content
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo l'intestazione "Gestione Contenuti"
    And vedo la tab-bar con le tab Shared Games · Knowledge Base
    And la tab "Shared Games" è attiva e mostra contenuto reale (lista/tabella) o empty-state legittimo
  When clicco la tab "Knowledge Base"
  Then la vista cambia e mostra il contenuto della KB tab (effetto visibile a schermo)
  Osservabile ✅: heading "Gestione Contenuti" + tab-bar 2 tab + switch tab produce cambio contenuto visibile
  Route: admin/(dashboard)/content
  Utente: admin
```

```gherkin
Scenario A4-06 [Flow]: Modifica, persistenza e anteprima di un template email
  Given sono loggato come admin
    And esiste almeno un template email seed (es. "game_night_invitation")
  When apro /admin/content/email-templates
    And attendo il caricamento della lista "Template Email" (pannello sinistro)
    And clicco un template dalla lista (es. "game_night_invitation") e annoto la versione corrente (badge "vN")
    And nel pannello destro modifico il campo "Oggetto" aggiungendo il suffisso " HP-TEST-2026-07-10"
    And clicco "Salva bozza"
  Then compare il toast "Bozza salvata" (descrizione "Nuova versione creata con successo.")
  When ricarico la pagina /admin/content/email-templates e riseleziono lo stesso template (reload di verifica persistenza)
  Then la nuova versione risulta persistita: il badge versione è incrementato (v(N+1)) e l'Oggetto contiene il suffisso "HP-TEST-2026-07-10"
    And aprendo "Cronologia" la lista versioni include la nuova bozza appena salvata
  When clicco "Anteprima"
  Then si apre il dialog "Anteprima Email" con un iframe che renderizza l'HTML del template
  Osservabile ✅: lista template renderizzata + editor popolato (Oggetto + Corpo HTML) + toast "Bozza salvata" + dopo reload versione incrementata e Oggetto modificato persistito + dialog anteprima con iframe non-vuoto
  Route: admin/(dashboard)/content/email-templates
  Utente: admin
  Nota: "Salva bozza" (`PUT /api/v1/admin/email-templates/{id}`) crea una NUOVA versione (non pubblica, additiva e reversibile via cronologia) → il reload conferma la persistenza della nuova versione. NON eseguire "Pubblica" nell'happy path per non alterare il template attivo di produzione. La UI NON espone un delete di template/versione (solo create/edit-versione/publish/preview) → nessuno step Delete.
```

```gherkin
Scenario A4-07 [Smoke]: AI hub carica con tab Agents di default
  Given sono loggato come admin
  When apro /admin/ai
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo la tab-bar con le tab Agents · Typologies · Definitions · AI Lab · Prompts · Models · Requests · RAG · Config
    And la tab "Agents" è attiva e mostra la sezione "Agent Catalog"
    And il catalogo mostra le card degli agenti (skeleton → contenuto reale) oppure l'empty-state "No agents found"
  When clicco la tab "Models"
  Then la vista cambia e mostra il contenuto della Models tab (effetto visibile a schermo)
  Osservabile ✅: tab-bar ≥9 tab + sezione "Agent Catalog" con card o empty-state + switch tab produce cambio contenuto visibile
  Route: admin/(dashboard)/ai
  Utente: admin
```

```gherkin
Scenario A4-08 [Smoke]: Lista provider LLM carica con stato e circuit breaker
  Given sono loggato come admin
  When apro /admin/providers
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo la sezione "Provider" con la tabella dei provider
    And la tabella elenca le righe dei provider noti: DeepSeek, OpenRouter, Ollama (locale)
    And la riga "DeepSeek" mostra il tag "primary"
    And ogni riga mostra un chip di stato token (es. "healthy"/"no token") e un chip circuit ("closed"/"open"/"half-open"/"unknown")
  Osservabile ✅: tabella provider con ≥3 righe (deepseek/openrouter/ollama-local) + tag "primary" su DeepSeek + chip stato e circuit renderizzati (non "…" indefinito)
  Route: admin/(dashboard)/providers
  Utente: admin
```

```gherkin
Scenario A4-09 [Flow]: Dettaglio provider e run probe
  Given sono loggato come admin con ruolo superadmin (il pulsante "Run probe" è visibile solo a superadmin; altrimenti compare "Probe richiede privilegi SuperAdmin" → step probe ⚠️ blocked-env)
    And il provider "deepseek" ha il token configurato via secret
  When apro /admin/providers e clicco la riga "DeepSeek" (naviga a /admin/providers/deepseek)
  Then vedo l'intestazione "deepseek" e il link "← Torna alla lista"
    And vedo la card "Quota" con lo stato del provider (valori quota o "Quota tracking non supportato")
  When clicco "Run probe"
  Then il pulsante mostra "Probing…" e poi si risolve con l'esito inline
    And compare l'esito della probe: "✓ autenticato" oppure "✗ fallita" + la latenza in ms
  Osservabile ✅: heading provider + card "Quota" renderizzata + click "Run probe" produce esito inline (autenticato/fallita + latenza ms)
  Route: admin/(dashboard)/providers/[name]
  Utente: admin (superadmin per la probe)
  Nota: la probe è un'operazione read-only diagnostica verso il provider (non muta config). La rotazione chiave (Rotate key) NON fa parte dell'happy path (richiede step-up 2FA + input distruttivo).
```

```gherkin
Scenario A4-10 [Flow]: Staging allowlist — ciclo add → verifica persistenza → remove (add/remove, no edit)
  Given sono loggato come admin con ruolo superadmin (pagina superadmin-only)
  When apro /admin/staging-access
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo l'intestazione "Staging Allowlist" e il form con i campi "Email" e "Note (optional)"
    And vedo la tabella delle voci esistenti oppure l'empty-state "No entries yet"
  When inserisco Email = "hp-test-2026-07-10@meepleai.test", Note = "HP-TEST-2026-07-10 allowlist" e clicco "Add"
  Then compare il toast "Added hp-test-2026-07-10@meepleai.test to staging allowlist"
    And la nuova voce compare nella tabella (colonna Email = l'email di test, colonna Note = "HP-TEST-2026-07-10 allowlist")
  When ricarico la pagina /admin/staging-access (reload di verifica persistenza — Create)
  Then dopo il reload la voce "hp-test-2026-07-10@meepleai.test" è ancora presente in tabella (persistita nel backend)
  When clicco il pulsante Trash sulla riga di test (aria-label "Remove hp-test-2026-07-10@meepleai.test")
    And nel dialog di conferma del browser ("Remove hp-test-2026-07-10@meepleai.test from the staging allowlist?") confermo
  Then compare il toast "Entry removed from staging allowlist"
    And la voce di test sparisce dalla tabella
  When ricarico la pagina /admin/staging-access (reload di verifica persistenza — Delete)
  Then dopo il reload la voce "hp-test-2026-07-10@meepleai.test" resta assente (delete persistito; soft-delete lato backend)
  Osservabile ✅: heading "Staging Allowlist" + form Email/Note + dopo Add: toast di conferma + riga HP-TEST in tabella + voce ancora presente dopo reload · dopo Remove: toast "Entry removed…" + riga sparita + voce ancora assente dopo reload (ciclo add/remove completo e persistito)
  Route: admin/(dashboard)/staging-access
  Utente: admin (superadmin)
  Nota: la UI espone Create (Add) e Delete (Trash) ma NON un edit delle voci (le entry sono immutabili). Il ciclo di vita è quindi add → remove, entrambi con reload di persistenza.
  Nota ⚠️ OPERAZIONE POTENZIALMENTE IMPATTANTE: aggiungere un'email concede accesso reale allo staging (propaga entro ~60s). Usare SOLO un'email marcata HP-TEST e non un indirizzo reale. Il Delete opera esclusivamente sulla voce HP-TEST appena creata. Su staging eseguire con estrema cautela o ridurre a Smoke (solo caricamento lista + form visibile, senza Add/Remove).
```

```gherkin
Scenario A4-11 [Smoke]: Budget & Cost dashboard carica con KPI e simulatore
  Given sono loggato come admin
  When apro /admin/business
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo l'intestazione "Budget & Cost" e i breadcrumb "Admin · Platform & Operations · Budget"
    And vedo la BudgetKpiStrip (KPI di spesa), il grafico CostStackedArea, il BudgetGauge e il CostSimulator
    And vedo il select intervallo (business-range-select) con valore di default "30 giorni"
  When cambio il select intervallo su "7 giorni"
  Then l'URL si aggiorna con ?range=7d e le sezioni collegate al range si ricaricano (effetto visibile a schermo)
  Osservabile ✅: heading "Budget & Cost" + KPI strip + grafico + gauge + simulator renderizzati + cambio range aggiorna URL a ?range=7d
  Route: admin/(dashboard)/business
  Utente: admin
```

```gherkin
Scenario A4-12 [Flow]: Database Sync — status tunnel e confronto schema (read-only)
  Given sono loggato come admin
    And l'ambiente locale dispone del tunnel SSH verso staging (chiave ~/.ssh/meepleai-staging); altrimenti il tunnel resta "Disconnected" → gli step che richiedono connessione sono ⚠️ blocked-env
  When apro /admin/database-sync
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And vedo l'intestazione "Database Sync"
    And vedo il TunnelStatusBanner con lo stato del tunnel (Connected/Disconnected/Connecting) e il pulsante Connect/Disconnect
    And vedo le tab Schema · Data · History con la tab "Schema" attiva
  When (se il tunnel è connesso) resto sulla tab "Schema" e clicco "Refresh"
  Then compare la comparazione delle migration EF Core: tabelle "Common Migrations", "Local Only", "Staging Only" (o il banner "Schemas are in sync")
  Osservabile ✅: heading "Database Sync" + TunnelStatusBanner con stato+pulsante + tab Schema/Data/History + (se connesso) tabelle di confronto migration renderizzate
  Route: admin/database-sync
  Utente: admin
  Nota ⚠️ OPERAZIONE POTENZIALMENTE IMPATTANTE: la parte mutante ("Apply Migrations") è ESCLUSA dall'happy path — è destruttiva, richiede il tunnel e un typed-confirm "APPLY MIGRATIONS". L'happy path si limita a status tunnel + confronto read-only (Refresh / Preview SQL). Non eseguire "Apply Migrations" su staging.
```

```gherkin
Scenario A4-13 [Flow]: Ciclo di vita tier — crea → edita con verifica di persistenza (Delete assente in UI)
  Given sono loggato come admin
    And apro /admin/config/tiers e attendo la tabella "Tier Management" (tiers-table)
  When clicco "Nuovo Tier" (btn-create-tier)
    And nel dialog "Nuovo tier" compilo Nome (field-name) = "hp_test_2026_07_10", Display Name (field-displayName) = "HP-TEST-2026-07-10 Tier", LLM Model Tier (field-llmModelTier) = "standard"
    And lascio i limiti a 0 e le toggle "Session Save" attiva e "Tier default" disattiva
    And clicco "Crea tier" (btn-save)
  Then compare il toast "Tier creato"
    And il dialog si chiude
    And la tabella mostra la nuova riga (tier-row-hp_test_2026_07_10) con Nome "hp_test_2026_07_10" e Display Name "HP-TEST-2026-07-10 Tier"
  When ricarico la pagina /admin/config/tiers (reload di verifica persistenza — Create)
  Then dopo il reload la riga "hp_test_2026_07_10" è ancora presente in tabella (tier persistito nel backend)
  When clicco "Modifica" sulla riga "hp_test_2026_07_10" (btn-edit-hp_test_2026_07_10)
    And nel dialog "Modifica tier: hp_test_2026_07_10" cambio il Display Name in "HP-TEST-2026-07-10 Tier v2"
    And clicco "Salva modifiche" (btn-save)
  Then compare il toast "Tier aggiornato"
    And la cella Display Name della riga mostra "HP-TEST-2026-07-10 Tier v2"
  When ricarico la pagina /admin/config/tiers (reload di verifica persistenza — Edit)
  Then dopo il reload la riga "hp_test_2026_07_10" mostra ancora Display Name "HP-TEST-2026-07-10 Tier v2" (modifica persistita)
  Osservabile ✅: nuova riga tier presente post-create + persistita dopo reload · Display Name aggiornato post-edit + persistito dopo reload
  Route: admin/(dashboard)/config/tiers
  Utente: admin
  Dati creati: tier "hp_test_2026_07_10" ("HP-TEST-2026-07-10 Tier"). Resta nel DB: la UI NON espone un Delete tier.
  Nota ⚠️ OPERAZIONE DELETE ASSENTE IN UI: il client tier (`tierClient.ts`) espone solo get/create/update (`GET|POST /api/v1/admin/tiers`, `PUT /api/v1/admin/tiers/{name}`); non esiste pulsante Elimina né endpoint DELETE. Il ciclo di vita copribile via browser è quindi Create → Edit (nessun Delete inventato). Il tier di test "hp_test_2026_07_10" non è default e resta inerte; per rimuoverlo serve intervento DB (fuori happy path) o `seed-sp4-reset`.
```

```gherkin
Scenario A4-14 [Flow]: Ciclo di vita template email — crea → edita (nuova versione) con verifica di persistenza (Delete assente in UI)
  Given sono loggato come admin
    And apro /admin/content/email-templates e attendo la lista "Template Email"
  When clicco "Nuovo" (apre il dialog "Nuovo Template Email")
    And compilo Nome template = "hp_test_2026_07_10", Lingua = "Italiano" (it), Oggetto = "HP-TEST-2026-07-10 Oggetto", Corpo HTML = "<html><body>HP-TEST-2026-07-10</body></html>"
    And clicco "Crea template"
  Then compare il toast "Template creato" (descrizione «"hp_test_2026_07_10" creato con successo.»)
    And il template appena creato risulta selezionato nel pannello destro (Oggetto e Corpo HTML popolati) con badge "Bozza" e "v1"
  When ricarico la pagina /admin/content/email-templates e cerco/seleziono "hp_test_2026_07_10" nella lista (reload di verifica persistenza — Create)
  Then dopo il reload il template "hp_test_2026_07_10" è presente nella lista e selezionabile (persistito nel backend)
  When nel pannello destro modifico l'Oggetto in "HP-TEST-2026-07-10 Oggetto v2"
    And clicco "Salva bozza"
  Then compare il toast "Bozza salvata"
  When ricarico la pagina, riseleziono "hp_test_2026_07_10" (reload di verifica persistenza — Edit)
  Then il badge versione è incrementato (v2) e l'Oggetto mostra "HP-TEST-2026-07-10 Oggetto v2" (nuova versione persistita)
    And "Cronologia" elenca ≥2 versioni (v1 iniziale + v2 bozza)
  Osservabile ✅: template presente in lista post-create + persistito dopo reload · Oggetto aggiornato + versione incrementata post-edit + persistiti dopo reload · Cronologia con ≥2 versioni
  Route: admin/(dashboard)/content/email-templates
  Utente: admin
  Dati creati: template "hp_test_2026_07_10" (locale it). Resta nel DB: la UI NON espone un Delete template/versione.
  Nota ⚠️ OPERAZIONE DELETE ASSENTE IN UI: il client email-template (`adminContentClient.ts`) espone get/create/update/publish/preview + versions (`GET|POST /api/v1/admin/email-templates`, `PUT …/{id}`, `…/{id}/publish`, `…/{id}/preview`); non esiste metodo delete né pulsante Elimina. Il ciclo di vita copribile è Create → Edit-versione (nessun Delete inventato). "Salva bozza" genera una nuova versione NON attiva (badge "Bozza"): NON cliccare "Pubblica" per non attivare il template di test. Rimozione del template di test: intervento DB (fuori happy path) o `seed-sp4-reset`.
```

---

## Auto-verifica

- **Copertura route**: le 11 route A4 di `_coverage-map.md` sono in matrice → A4-01/02 (config), A4-03 + A4-13 (tiers), `config/n8n` **skip (feature n8n in rimozione)**, A4-05 (content), A4-06 + A4-14 (email-templates), A4-07 (ai), A4-08 (providers), A4-09 (providers/[name]), A4-10 (staging-access), A4-11 (business), A4-12 (database-sync). Nessun buco.
- **Osservabili**: ogni scenario (A4-01 … A4-14) dichiara ≥1 osservabile strutturale a schermo (heading, tab, riga tabella, chip, toast, dialog, cambio URL, badge versione). Nessuna asserzione su testo letterale generato da LLM.
- **Happy path only**: nessuno scenario negativo/errore/edge. I percorsi di errore (endpoint n8n assente, tunnel giù, non-superadmin) sono degradati esplicitamente a `⚠️ blocked-env`, non trattati come fail.
- **Verifica CRUD & persistenza (spec §3.1)**: gli scenari Flow che mutano dati includono un **reload di verifica** che riconferma lo stato dal backend (non solo feedback ottimistico):
  - **Create** persistito dopo reload: A4-13 (tier), A4-14 (email template), A4-10 (staging allowlist).
  - **Edit/Save** persistito dopo reload: A4-03 + A4-13 (tier Display Name), A4-06 + A4-14 (email template — versione incrementata).
  - **Delete** persistito dopo reload: A4-10 (staging allowlist — voce assente dopo remove+reload).
  - **Delete assente in UI, annotato** (nessun Delete inventato): tier (A4-03/A4-13 — client solo get/create/update) e email template (A4-06/A4-14 — solo versioning, no delete). La matrice CRUD sopra dettaglia per-entità.
  - **Marcatore Delete-safe**: i Delete operano solo su entità `HP-TEST-2026-07-10` (A4-10 rimuove esclusivamente la voce allowlist appena creata).
- **Ripristino stato / non-distruttivo**:
  - A4-02 (Registration Mode) ripristina S0 a fine scenario (toggle S0→S1→S0).
  - A4-03 (tier edit) ripristina il Display Name originale a fine scenario.
  - A4-06/A4-13/A4-14 creano/modificano entità marcate `HP-TEST-2026-07-10`; A4-06/A4-14 escludono "Pubblica" (versione resta "Bozza"); A4-10 include cleanup della voce di test (remove nel ciclo). Tier ed email template creati in A4-13/A4-14 restano nel DB (Delete non esposto) → rimozione via `seed-sp4-reset` se serve.
  - A4-09 (probe) e A4-12 (schema compare) sono operazioni read-only; le parti distruttive (rotate key, Apply Migrations) sono esplicitamente escluse.
- **Operazioni sensibili**: A4-10 (staging-access) e A4-12 (database-sync) portano la nota "operazione potenzialmente impattante" con indicazione di cautela e riduzione a Smoke su staging.
- **Utente**: admin in tutti gli scenari; superadmin richiesto (con fallback blocked-env) in A4-09 e A4-10.
