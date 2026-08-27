# Full Feature Audit — 2026-08-26

Verifica che un utente possa eseguire tutte le funzioni previste, provate dalla UI e confermate
su risposta API, stato del DB e log.

Design: [spec](../../specs/2026-08-26-full-feature-audit-design.md) ·
Piano dell'harness: [plan](../../specs/2026-08-26-full-feature-audit-plan.md)

## Stato

| Ondata | Contesti | Stato |
|---|---|---|
| 0 — Harness | — | ✅ completata: inventario, crawler, collettore, report, prima passata |
| 1A — Identità e accessi | Authentication · SecurityAudit · Administration/utenti | 🔄 blocco utenti completo — [scheda](./wave-1a-identita-accessi.md) |
| 1B — Configurazione e operazioni | SystemConfiguration · Administration/operazioni | 🔄 letture complete, mutazioni sicure eseguite — [scheda](./wave-1b-configurazione-operazioni.md) |
| 2 — Contenuti | SharedGameCatalog · GameManagement · UserLibrary · DocumentProcessing | 🔄 letture complete, ciclo upload PDF verificato — [scheda](./wave-2-contenuti.md) |
| 3 — Intelligenza | KnowledgeBase · AgentMemory · GameToolkit · KbQuality | 🔄 sonde sistematiche — [scheda](./wave-3-5-sonde-sistematiche.md) |
| 4 — Gioco | SessionTracking · GameToolbox · Gamification · EntityRelationships | 🔄 sonde sistematiche — [scheda](./wave-3-5-sonde-sistematiche.md) |
| 5 — Contorno | UserNotifications · BusinessSimulations · DatabaseSync · Testing | 🔄 sonde sistematiche — [scheda](./wave-3-5-sonde-sistematiche.md) |

## Come si esegue

Prerequisiti: stack locale avviato (`cd infra && make dev-from-snapshot`), frontend su :3000,
variabili `AUDIT_USER_EMAIL` / `AUDIT_USER_PASSWORD` / `AUDIT_ADMIN_EMAIL` / `AUDIT_ADMIN_PASSWORD`
esportate (credenziali admin in `infra/secrets/admin.secret`).

```bash
cd apps/web
pnpm audit:inventory   # rigenera inventory.csv dal codice
pnpm audit:crawl       # passata del crawler (setup + audit-user + audit-admin)
pnpm audit:report      # aggiorna gli stati e genera la scheda dell'ondata
```

## Legenda degli stati

| Stato | Significato |
|---|---|
| `⬜ non coperto` | Mai visitato |
| `✅ verificato` | Evidenza presente e coerente col livello (L1/L2/L3) |
| `⚠️ finding da triagare` | Anomalia non bloccante: console o richieste secondarie in errore |
| `⚠️ finding da aprire` | Guasto: HTTP ≥ 400, nessuna risposta, o marker di errore nella pagina |
| `🚫 non raggiungibile da UI` | Endpoint senza pagina corrispondente |

## Copertura

`inventory.csv` contiene **1725 righe**: 220 rotte moltiplicate per i ruoli che possono
percorrerle, più 1381 endpoint. Distribuzione per contesto rigenerabile con:

```bash
cd apps/web && node -e "
const fs=require('fs');
const rows=fs.readFileSync('../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv','utf8').trim().split('\n').slice(1).map(l=>l.split(','));
const per={};
for(const c of rows){ (per[c[4]] ??= {})[c[7]] = ((per[c[4]]||{})[c[7]]||0)+1; }
console.table(per);
"
```

## Perché l'ondata 1 è divisa in due

Nella prima stesura l'ondata 1 pesava 611 righe, il 35% del tracker, perché `Administration` da
sola ne valeva 520. Il problema non era la sua dimensione: era che **raccoglieva gli scarti**.
Tutto ciò che stava sotto `/admin` senza una regola propria vi cadeva per fallback — `admin/kb`,
`admin/mechanic-extractor`, `admin/pdfs`, `admin/feature-flags` — mentre `EntityRelationships` e
`SecurityAudit` risultavano a **zero righe** pur avendo endpoint propri (`/admin/entity-links`,
`/admin/audit-log`), assorbiti dallo stesso fallback.

Corretta l'attribuzione, Administration scende a 272 righe e ogni contesto riceve ciò che gli
spetta. Il resto si divide per funzione:

| Sotto-ondata | Contenuto | Righe |
|---|---|---|
| **1A — Identità e accessi** | Authentication (67) · SecurityAudit (2) · Administration → `users`, `impersonation`, `access-requests`, `invitations`, `staging-allowlist` | ~204 |
| **1B — Configurazione e operazioni** | SystemConfiguration (64) · Administration → `queue`, `operations`, `infrastructure`, `monitor`, `analytics`, `resources`, `system`, `storage`, `cache`, `events`, `event-outbox`, `test`, `playground` | ~201 |

Effetto della riattribuzione sugli altri contesti: KnowledgeBase 160 → 254, SharedGameCatalog
161 → 181, SystemConfiguration 24 → 64, BusinessSimulations 4 → 37, UserNotifications 28 → 44,
DocumentProcessing 55 → 71, KbQuality 6 → 20.

## L'ambiente locale, come è davvero

Rilevato il 2026-08-26 con `make dev`. Quasi nulla corrispondeva alle assunzioni ovvie, e ogni
scostamento avrebbe prodotto un fallimento silenzioso.

| Assunzione naturale | Realtà |
|---|---|
| Database `meepleai` | **`meepleai_staging`**, anche in locale |
| Colonne in snake_case | **Dipende dalla tabella**: `users."Id"`, `users."Email"` in PascalCase quotato; `shared_games.id` in snake_case; `shared_games` mescola le due |
| Tabella `games` | Non esiste: il catalogo è **`shared_games`** (162 righe). Non esistono `agents` né `chat_threads` |
| `make dev` avvia tutto | Il container **`web` resta in stato `Created`** e va avviato a mano (`docker start meepleai-web`) |
| Gli utenti seed sono utilizzabili | `test@meepleai.com` aveva `EmailVerified = false`: login 200, poi **403 su ogni richiesta** |
| `admin.secret` apre l'admin | La password di `admin@meepleai.app` **non corrisponde** al DB locale (400). Funziona `badsworm@gmail.com` con `SEED_BADSWORM_PASSWORD` |

### Contenuto disponibile

| Tabella | Righe | Effetto sull'audit |
|---|---|---|
| `shared_games` | 162 | `[gameId]` risolvibile |
| `pdf_documents` | 135 | ondata 2 percorribile |
| `vector_documents` | 126 | ondata 3 misurabile, ma su corpus ridotto |
| `user_library_entries` | 12 | libreria popolata |
| `users` | 8 | 2 superadmin, 6 utenti |
| `chat_sessions` · `game_sessions` · `agent_sessions` | **0** | `[threadId]`, `[sessionId]`, `[agentId]` non risolvibili: le rotte che li usano restano da visitare finché le ondate 3 e 4 non creano i dati |

### Modifiche fatte all'ambiente locale

Per sbloccare l'audit ho eseguito una sola scrittura, reversibile e circoscritta al DB locale:

```sql
UPDATE users SET "EmailVerified" = true, "EmailVerifiedAt" = now() WHERE "EmailVerified" = false;
-- ha toccato 1 riga: test@meepleai.com
```

### ⚠️ Gli artifact del crawler contengono credenziali in chiaro

Lo snapshot d'errore di Playwright (`error-context.md`) registra il **valore** dei campi password.
`apps/web/audit-results/` è in `.gitignore` e non deve mai uscire dalla macchina.

## Prima passata del crawler — 2026-08-26

346 test, **275 rotte visitate**, 13,6 minuti. Dettaglio in [`wave-0-harness.md`](./wave-0-harness.md).

| Esito | Rotte |
|---|---|
| ✅ verificato | 222 |
| ⚠️ da triagare | 51 |
| ⚠️ da aprire | 2 |
| saltate (parametro non risolvibile) | 68 |

Le 68 saltate dipendono dalle tabelle vuote (`chat_sessions`, `game_sessions`, `agent_sessions`):
diventeranno visitabili quando le ondate 3 e 4 creeranno i dati. Non sono un difetto del prodotto
né dell'harness, ma **non sono copertura**: restano `⬜ non coperto` nel tracker.

Gli screenshot dei problemi restano in `apps/web/audit-results/` e **non sono committati** (13 MB
per 53 immagini): il segnale utile — URL fallite e marker — è nel report testuale.

### Limiti noti dell'harness

1. **`networkidle` non convive con le connessioni persistenti.** Il crawler attende
   `waitUntil: 'networkidle'`, che su una pagina con SSE o SignalR — entrambi usati dal prodotto —
   non si verifica mai: la rotta va in timeout e risulta fallita senza essere rotta. È accaduto su
   `/admin/knowledge-base/games` (1 test su 346). Prima dell'ondata 1 conviene passare a
   `domcontentloaded` più un'attesa esplicita, altrimenti ogni pagina live verrà segnalata a torto.
2. **Il crawler non esercita le mutazioni.** Copre navigazione e lettura: create, update e delete
   restano manuali, come previsto dal design.

### Come leggere i 404 con un id nell'URL

23 fallimenti su `/api/v1/private-games/{id}` e 9 su `/api/v1/library/games/{id}` derivano
dall'id che il crawler inietta: è un id di `shared_games`, e quelle rotte si aspettano un id di
altro tipo. **Non sono findings** finché non li si prova con un id del tipo giusto — è il rischio
che il piano prevedeva, e va tenuto presente prima di aprire issue su questa categoria.

## Come leggere questo audit

**Che cosa significa "verificato".** Tre livelli, dichiarati per riga nel tracker:

| Livello | Significa |
|---|---|
| **L1** | L'endpoint esiste, autorizza e valida. Le mutazioni sondate in massa stanno qui: sono state chiamate con corpo vuoto e id inesistente, quindi si sa che rispondono correttamente a una richiesta malformata — non che facciano la cosa giusta con una valida |
| **L2** | La funzione produce l'effetto atteso, confermato su risposta API, riga di database o email |
| **L3** | Come L2, più almeno un caso negativo o limite |

Una riga L1 **non è** una funzione verificata. È una funzione che non esplode.

**Che cosa NON è stato provato**, e non per dimenticanza:

- **71 mutazioni irreversibili** — riavvii di servizi, migrazione dello storage, rotazione delle chiavi API, cancellazione di backup, revoca di sessioni. Vanno provate su un ambiente sacrificabile.
- **86 righe** che richiedono entità inesistenti in questo ambiente (collezioni di documenti, campagne, job di coda).
- **OAuth**, disattivato in locale (`oauthEnabled: false`): verificabile solo su staging.

**Quanto fidarsi dei numeri.** Durante la sessione un terzo di una passata è risultato invalido: la
sessione era caduta e ogni 401 veniva letto come «autorizzazione applicata», marcando 232 righe
come verificate senza provarle. Sono state rifatte, e la sonda ora si ferma dopo cinque 401
consecutivi. Un audit automatico sbaglia in silenzio: i controlli su come sbaglia contano quanto i
controlli su ciò che misura.

**Il residuo di 96 righe "da triagare" è in larga parte artefatto dei parametri.** Il crawler
naviga le rotte con id reali, ma un id solo non può essere del tipo giusto per tutte: navigare
`/library/private/[id]` con l'id di un gioco **condiviso** produce un 404 legittimo, e
`getPrivateGame` non ha fallback. Allineare il `gameId` a un gioco presente sia in libreria sia con
KB ha dimezzato i 404 su `library/games` (28 → 14); i 33 su `private-games` restano e **non sono
difetti**. Chi rilegge il tracker deve distinguerli: l'evidenza a fianco di ogni riga serve a
questo.

**I falsi positivi sono documentati come tali.** Diverse segnalazioni iniziali erano difetti dei
criteri, non del prodotto: `/library` che comunica lo stato vuoto con i contatori invece che con
una frase, endpoint chiamati senza i parametri obbligatori, un endpoint "Superadmin only"
classificato come self-service dal parser. Sono riportati nelle schede delle ondate perché
distinguere ciò che non è un difetto vale quanto elencare ciò che lo è.

## Findings aperti

| # | Cosa | Severità | Stato |
|---|---|---|---|
| [#3831](https://github.com/meepleAi-app/meepleai-monorepo/issues/3831) | 4 famiglie di endpoint admin irraggiungibili: prefisso `/api/v1` raddoppiato | P0 | corretto — [PR #3832](https://github.com/meepleAi-app/meepleai-monorepo/pull/3832) |
| [#3840](https://github.com/meepleAi-app/meepleai-monorepo/issues/3840) | Un'impersonificazione avviata non si può terminare (rotta registrata due volte) | P0 | corretto — [PR #3841](https://github.com/meepleAi-app/meepleai-monorepo/pull/3841) |
| [#3846](https://github.com/meepleAi-app/meepleai-monorepo/issues/3846) | Upload PDF: risponde 200 e l'elaborazione fallisce, senza segnale all'utente | P1 | aperto |
| [#3847](https://github.com/meepleAi-app/meepleai-monorepo/issues/3847) | 30 endpoint rispondono 500 su richiesta malformata | P1 | aperto |
| [#3843](https://github.com/meepleAi-app/meepleai-monorepo/issues/3843) | 7 endpoint in 500: query concorrenti sullo stesso DbContext | P1 | aperto |
| [#3839](https://github.com/meepleAi-app/meepleai-monorepo/issues/3839) | 9 endpoint in 500: LINQ non traducibile o DbSet non mappato | P1 | aperto |
| [#3849](https://github.com/meepleAi-app/meepleai-monorepo/issues/3849) | Creare una sessione senza `participants` dà 500 (validator senza `NotNull`) | P1 | aperto |
| [#3842](https://github.com/meepleAi-app/meepleai-monorepo/issues/3842) | Il cambio di tier rifiuta ogni valore | P1 | aperto |
| [#3833](https://github.com/meepleAi-app/meepleai-monorepo/issues/3833) | 500 su `tables/top`: colonna SQL inesistente | P1 | aperto |
| [#3834](https://github.com/meepleAi-app/meepleai-monorepo/issues/3834) | 500 su `mechanic-extractor/thresholds`: riga di seed assente | P1 | aperto |
| [#3835](https://github.com/meepleAi-app/meepleai-monorepo/issues/3835) | `/toolkit/stats` chiama l'API senza il prefisso `/api/v1` | P1 | aperto |
| [#3844](https://github.com/meepleAi-app/meepleai-monorepo/issues/3844) | Disabilitare un feature flag per tier fallisce (inserisce invece di aggiornare) | P1 | aperto |
| [#3848](https://github.com/meepleAi-app/meepleai-monorepo/issues/3848) | Il campo di ricerca di `/games` è readonly ma invita a cercare | P2 | aperto |
| [#3838](https://github.com/meepleAi-app/meepleai-monorepo/issues/3838) | L'audit di sicurezza registra solo i login | P2 | aperto |
| [#3845](https://github.com/meepleAi-app/meepleai-monorepo/issues/3845) | 500 su `contribution-stats` e `cache/metrics` | P2 | aperto |
| [#3836](https://github.com/meepleAi-app/meepleai-monorepo/issues/3836) | 4 difetti minori (404 client, pagina card, a11y onboarding) | P2 | aperto |
| [#3855](https://github.com/meepleAi-app/meepleai-monorepo/issues/3855) | L'agente altera le house rule e le attribuisce a pagine del manuale che non le contengono | P1 | aperto |
| [#3854](https://github.com/meepleAi-app/meepleai-monorepo/issues/3854) | Toolkit: due strutture parallele, la creazione scrive dove l'aggiunta di strumenti non legge | P1 | aperto |
| [#3853](https://github.com/meepleAi-app/meepleai-monorepo/issues/3853) | 9 pagine admin scartano risposte valide: schema Zod più stretto del contratto backend | P2 | aperto |
| [#3850](https://github.com/meepleAi-app/meepleai-monorepo/issues/3850) | Il messaggio "informazione non disponibile" è in inglese in conversazioni italiane | P3 | aperto |

## Cosa funziona, verificato con prove

Vale la pena dirlo con la stessa precisione dei difetti:

- **Autenticazione**: accesso, uscita con revoca della sessione nel database, password errata senza cookie, nessuna enumerazione degli utenti, 403 su tutti i confini di autorizzazione provati.
- **Invito e registrazione chiusa**: ciclo completo da richiesta ad approvazione fino all'email di invito.
- **Recupero password**: con email inesistente la risposta è indistinguibile, senza email inviate né token creati.
- **RAG**: risposta in 11 secondi con citazione testuale dal regolamento e riferimento alla pagina; su domanda fuori contesto dichiara di non sapere invece di inventare.
- **Interfaccia**: le pagine principali di utente e amministratore caricano, elencano e navigano senza errori di rete.

## Findings aperti (dettaglio storico)

Oltre al P0 già confermato, la passata ne ha prodotti altri, tutti **verificati con richieste
dirette** e riproducibili senza dipendere dagli id iniettati.

| # | Endpoint / rotta | Esito | Causa accertata | Severità | Issue |
|---|---|---|---|---|---|
| 1 | `/api/v1/resources/database/tables/top` | 500 | `Npgsql 42703: column "tablename" does not exist` — `GetTopTablesBySizeQueryHandler.cs:32-34` interroga `pg_stat_user_tables` usando `tablename`, colonna che appartiene a `pg_tables`; lì si chiama `relname` | P1 | [#3833](https://github.com/meepleAi-app/meepleai-monorepo/issues/3833) |
| 2 | `/api/v1/admin/mechanic-extractor/thresholds` | 500 | `CertificationThresholdsConfig singleton row (Id=1) is missing` — riga di seed assente. Stesso schema dei seed persi negli squash di migration già visti su questo repo | P1 | [#3834](https://github.com/meepleAi-app/meepleai-monorepo/issues/3834) |
| 3 | `/toolkit/stats` → `/game-sessions/session-statistics` | 404 | Il client chiama **senza il prefisso `/api/v1`**: con il prefisso l'endpoint risponde 200 | P1 | [#3835](https://github.com/meepleAi-app/meepleai-monorepo/issues/3835) |
| 4 | `/api/v1/admin/openrouter/usage/requests` | 404 | Il frontend chiama un endpoint che non esiste | P2 | [#3836](https://github.com/meepleAi-app/meepleai-monorepo/issues/3836) |
| 5 | `/api/v1/badges/my-badges` | 404 | Idem | P2 | [#3836](https://github.com/meepleAi-app/meepleai-monorepo/issues/3836) |
| 6 | `/games/[id]/card` | marker `not-found` | Pagina che si presenta come "non trovato" con HTTP 200, per entrambi i ruoli | P2 | [#3836](https://github.com/meepleAi-app/meepleai-monorepo/issues/3836) |
| 7 | `/onboarding` | a11y | `DialogContent` senza `DialogTitle`: contenuto inaccessibile agli screen reader | P2 | [#3836](https://github.com/meepleAi-app/meepleai-monorepo/issues/3836) |

**Non è un finding**: `/api/v1/admin/catalog/seeds` risponde 503 con
`"Catalog seed pipeline disabled"` — è un flag disattivato, cioè comportamento previsto. Elencato
qui perché compariva 10 volte fra i fallimenti e sarebbe stato facile scambiarlo per un guasto.

### Il primo P0, confermato

### 🔍 Candidato P1 — un utente non verificato viene rimbalzato al login senza spiegazione

**Stato**: meccanismo confermato dai log; l'impatto sull'utente reale va verificato in ondata 1.

Con un utente autenticato ma con `EmailVerified = false`, il backend risponde 403 e **spiega cosa
fare**, con tanto di endpoint per rinviare la mail:

```
[WRN] Blocking request for unverified user … (grace period ended: never set)
      Api.Middleware.EmailVerificationMiddleware → /api/v1/auth/me → 403
```

```json
{ "error": "Email verification required",
  "verificationStatus": { "resendEndpoint": "/api/v1/auth/email/resend" } }
```

Il frontend però traduce quel 403 in un redirect a `/login?from=…`: l'utente torna al modulo di
accesso, accede di nuovo, e viene rimbalzato ancora. Non vede mai il messaggio, benché esista una
pagina `/verification-pending` fatta apposta.

Da accertare in ondata 1: se il proxy distingue 401 da 403 e dove va intercettato il caso.

### 🚨 P0 CONFERMATO — prefisso `/api/v1` raddoppiato: 4 famiglie di endpoint irraggiungibili

**Stato**: confermato, corretto e in revisione — issue [#3831](https://github.com/meepleAi-app/meepleai-monorepo/issues/3831), PR [#3832](https://github.com/meepleAi-app/meepleai-monorepo/pull/3832).

| Path chiamato dal frontend | Esito | Path realmente montato | Esito |
|---|---|---|---|
| `/api/v1/admin/reports/generate` | **404** | `/api/v1/api/v1/admin/reports/generate` | 405 (esiste, è POST) |
| `/api/v1/admin/alert-configuration` | **404** | `/api/v1/api/v1/admin/alert-configuration` | **200** |
| `/api/v1/permissions/me` | **404** | `/api/v1/api/v1/permissions/me` | **200** |

Comando di riproduzione (con cookie di sessione admin):

```bash
curl -s -o /dev/null -w "%{http_code}\n" -H "Cookie: meepleai_session=…" \
  http://localhost:8080/api/v1/admin/alert-configuration      # 404
curl -s -o /dev/null -w "%{http_code}\n" -H "Cookie: meepleai_session=…" \
  http://localhost:8080/api/v1/api/v1/admin/alert-configuration # 200
```

Nessun test backend copre questi path, il che spiega perché la regressione sia sopravvissuta.

Quattro file sono registrati su `v1Api` (che è già `app.MapGroup("/api/v1")`) ma dichiarano al
proprio interno un gruppo con il prefisso completo:

| File | Gruppo dichiarato | Registrazione |
|---|---|---|
| `Routing/ReportingEndpoints.cs:19` | `/api/v1/admin/reports` | `Program.cs:1037` |
| `Routing/AlertConfigurationEndpoints.cs` | `/api/v1/admin/alert-configuration` | `Program.cs:966` |
| `Routing/LlmAnalyticsEndpoints.cs` | `/api/v1/admin/llm` | `Program.cs:932` |
| `Routing/PermissionRoutes.cs` | `/api/v1/permissions` | — |

ASP.NET annida i prefissi dei gruppi, quindi il path effettivo sarebbe
`/api/v1/api/v1/admin/reports/...`. Il frontend chiama il path singolo
(`lib/api/alert-config.api.ts:20`, `lib/api/clients/admin/adminMonitorClient.ts:135`) e **nessun
test backend copre questi path**.

Se confermato è P0: funzioni admin irraggiungibili. Se smentito, questa sezione va cancellata.
La verifica è una chiamata ai due path con lo stack acceso.

## Domande aperte per l'ondata 1

1. **L'ondata 1 è sbilanciata**: 611 righe contro le 438 dell'ondata 2 e le 52 dell'ondata 5,
   perché `Administration` da sola vale 520 righe. Va spezzata o messa in conto su più sessioni.
2. **Due contesti hanno zero righe**: `EntityRelationships` e `SecurityAudit` sono fra i 20
   bounded context dichiarati ma non espongono né pagine né endpoint riconoscibili. Da accertare
   in ondata 5: se non hanno superficie raggiungibile è un finding, non una casella vuota.
