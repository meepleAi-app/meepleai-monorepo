# Full Feature Audit — 2026-08-26

Verifica che un utente possa eseguire tutte le funzioni previste, provate dalla UI e confermate
su risposta API, stato del DB e log.

Design: [spec](../../specs/2026-08-26-full-feature-audit-design.md) ·
Piano dell'harness: [plan](../../specs/2026-08-26-full-feature-audit-plan.md)

## Stato

| Ondata | Contesti | Stato |
|---|---|---|
| 0 — Harness | — | 🔄 in corso: inventario, risolutore, collettore e report fatti; crawler da eseguire a stack acceso |
| 1 — Accesso | Authentication · SystemConfiguration · Administration · SecurityAudit | ⬜ non iniziata |
| 2 — Contenuti | SharedGameCatalog · GameManagement · UserLibrary · DocumentProcessing | ⬜ non iniziata |
| 3 — Intelligenza | KnowledgeBase · AgentMemory · GameToolkit · KbQuality | ⬜ non iniziata |
| 4 — Gioco | SessionTracking · GameToolbox · Gamification · EntityRelationships | ⬜ non iniziata |
| 5 — Contorno | UserNotifications · BusinessSimulations · DatabaseSync · Testing | ⬜ non iniziata |

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

## Findings

L'ondata 0 costruisce l'harness e non produce findings di prodotto. Ne ha però aperto **uno
sospeso**, da verificare per primo a stack acceso.

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

### 🔍 Sospetto — prefisso `/api/v1` raddoppiato su 4 famiglie di endpoint

**Stato**: da verificare, non confermato.

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
