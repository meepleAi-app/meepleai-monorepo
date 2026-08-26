# Full Feature Audit — Design

> **Data**: 2026-08-26 · **Branch**: `feature/full-feature-audit` · **Stato**: design approvato, implementazione non iniziata

## Perché

Nessuno ha mai verificato, con prove alla mano, che un utente riesca davvero a eseguire tutte le
funzioni che il prodotto espone. I 381 spec E2E esistenti girano con `PLAYWRIGHT_AUTH_BYPASS=true`
e risposte mockate: dimostrano che il frontend si comporta bene *dato* un backend ipotetico, non
che lo stack reale funzioni. L'unico precedente di verifica su ambiente vero
([mobile-golden-path-audit](../audits/2026-06-02-mobile-golden-path-audit.md), 2026-06-02) ha
coperto 3 user story e ha trovato 2 P0 in poche ore — indizio che la superficie non coperta
nasconde altro.

Questo audit chiude la domanda per l'intera superficie, con evidenza per ogni elemento e una
misura di copertura leggibile in ogni momento.

## Superficie misurata (2026-08-26)

| Elemento | Conteggio |
|---|---|
| Pagine Next.js (`page.tsx`) | 220 — admin 96 · (authenticated) 83 · (public) 25 · (auth) 10 · (chat) 4 · altre 2 |
| Endpoint API (`Map{Get,Post,Put,Delete,Patch}`) | ~1400 in 224 file sotto `apps/api/src/Api/Routing/` |
| Bounded context | 20 |
| Spec E2E esistenti (mock) | 381 |

## Decisioni

| Decisione | Scelta | Conseguenza |
|---|---|---|
| Scope | Esaustivo per bounded context, in ondate | Serve un inventario e un tracker, non basta una checklist |
| Ambiente | Ibrido: locale `dev-from-snapshot` + spot-check staging | Scritture e azioni distruttive solo in locale |
| Ruoli | Utente finale **e** admin, entrambi in profondità | Ogni rotta va percorsa due volte |
| Findings | Report + issue GitHub + **fix in-session dei P0** | L'audit si interrompe per correggere i bloccanti |
| Esecuzione | Harness esplorativo + ondate mirate | Costo iniziale di costruzione, poi copertura uniforme |

### Cosa questo audit non è

- Non è una suite di regressione: il crawler esplora e riporta, non asserisce. Le asserzioni
  restano nei 381 spec esistenti.
- Non sostituisce i test: un finding produce un'issue e, dove serve, un test di regressione nel
  fix — non una riga in più nel crawler.
- Non tocca staging in scrittura.

## Harness

Quattro componenti indipendenti, ciascuno utile da solo.

### 1. Generatore di inventario — `scripts/audit/build-inventory.ts`

Legge `apps/web/src/app/**/page.tsx` ed estrae le 220 rotte con i loro segmenti dinamici; parsa
`apps/api/src/Api/Routing/**/*.cs` ed estrae metodo, path e file di ogni endpoint; mappa entrambi
sui 20 bounded context.

**Output**: `inventory.csv` (una riga per coppia rotta×ruolo e per endpoint) con colonne
`id, tipo, path, metodo, contesto, ruolo, livello, stato, evidenza, note`.

Lo stato di ogni riga è uno di: `⬜ non coperto` · `✅ verificato` · `⚠️ finding #NNNN` ·
`🚫 non raggiungibile da UI`.

### 2. Risolutore di parametri — `scripts/audit/resolve-params.ts`

Le rotte `[gameId]`, `[threadId]`, `[sessionId]` non si navigano senza id reali. Interroga il
Postgres locale seedato ed estrae un id valido per tipo di entità.

**Output**: `route-params.json`.

Senza questo passaggio il crawler produrrebbe 404 e chiameremmo "rotto" ciò che è solo non
indirizzato.

### 3. Crawler esplorativo — `e2e/audit/crawl.spec.ts` + `playwright.audit.config.ts`

Config separata da `playwright.config.ts`: **niente `PLAYWRIGHT_AUTH_BYPASS`**. Login reale via UI
una volta per ruolo, `storageState` riusato per il resto della passata.

Per ogni rotta × ruolo cattura:

- errori e warning di console;
- richieste HTTP fallite (4xx/5xx), con l'endpoint corrispondente dell'inventario;
- screenshot;
- testo visibile — serve a riconoscere error boundary, stati vuoti e "qualcosa è andato storto",
  difetti reali che non emettono alcun errore tecnico.

### 4. Collettore evidenze — `scripts/audit/collect.ts`

Attorno a ogni azione (del crawler o manuale):

- **Log**: marker temporale, poi `docker logs meepleai-api --since <marker>` filtrato sui livelli
  Error/Fatal. Seq (`meepleai-seq`) è **opzionale**: gira solo sotto il profilo `monitoring` e non
  espone porte sull'host, quindi si interroga via `docker exec` e solo quando il profilo è attivo.
- **DB**: snapshot di `pg_stat_user_tables` prima e dopo, per sapere *se e dove* si è scritto.

Il diff per conteggi è volutamente grossolano: dice "questa azione ha toccato `game_sessions` e
`outbox_messages`". Per le funzioni di livello L2/L3 si legge poi la riga vera.

### Confine automatico/manuale

Il crawler copre navigazione e lettura. **Le mutazioni si eseguono a mano**: creare, modificare e
cancellare con conseguenze reali non si automatizza alla cieca su 1400 endpoint. Le mutazioni
manuali girano però con il collettore attivo, quindi producono la stessa evidenza strutturata.

## Protocollo di verifica

Il livello si assegna per tipo di funzione.

| Livello | Si applica a | Evidenza richiesta |
|---|---|---|
| **L1 — Raggiungibile** | Pagine di sola lettura, viste derivate | Pagina si apre col ruolo giusto · console pulita · nessuna 4xx/5xx · nessun error boundary o stato vuoto sospetto |
| **L2 — Funzionante** | Ogni mutazione, ogni azione con effetto | L1 + risposta 2xx con payload coerente + riga DB attesa + nessun ERROR nei log nella finestra dell'azione |
| **L3 — Corretta ai bordi** | Auth, upload→indicizzazione, chat RAG, scoring live, quota | L2 + almeno un caso negativo (validazione, permesso, risorsa altrui) e un caso limite |

**Regola di chiusura**: un elemento passa a `✅ verificato` solo con l'evidenza del suo livello
allegata al report — screenshot, estratto di risposta, riga DB, riga di log. Nessuno stato
"verificato" senza prova.

### Endpoint senza UI

Molti endpoint non hanno una pagina corrispondente (API interne, callback, admin API). Poiché il
criterio è "un utente lo fa dalla UI", si marcano `🚫 non raggiungibile da UI` e si provano via
chiamata diretta solo quando stanno nel percorso di una funzione che l'utente dovrebbe poter
eseguire.

Un endpoint che implementa una funzione prevista e che **nessuna UI raggiunge è un finding**, non
un elemento da saltare.

## Ondate

L'ordine segue le dipendenze dei dati: ogni ondata produce ciò che serve alla successiva.

| # | Ondata | Bounded context | Perché qui |
|---|---|---|---|
| **0** | Harness | — | Inventario, risolutore, crawler, collettore. Nessun finding di prodotto |
| **1** | Accesso | Authentication · SystemConfiguration · Administration · SecurityAudit | Se l'accesso è rotto l'audit si ferma qui. Registrazione (con toggle invite-only), login, 2FA, OAuth, ruoli, audit log |
| **2** | Contenuti | SharedGameCatalog · GameManagement · UserLibrary · DocumentProcessing | Genera i dati veri: giochi, libreria, PDF caricati e indicizzati |
| **3** | Intelligenza | KnowledgeBase · AgentMemory · GameToolkit · KbQuality | Chat RAG, risposte grounded, house rules, toolkit. Ha senso solo sul corpus dell'ondata 2 |
| **4** | Gioco | SessionTracking · GameToolbox · Gamification · EntityRelationships | GameNight, sessioni live, scoring, achievement |
| **5** | Contorno | UserNotifications · BusinessSimulations · DatabaseSync · Testing | Notifiche/email (Mailpit in locale, Resend su staging), simulazioni, sync, test-support |

Ogni ondata si chiude con: tracker aggiornato, issue aperte, commit. L'audit può fermarsi dopo
qualsiasi ondata e la copertura raggiunta resta leggibile.

## Deliverable

```
docs/for-developers/audits/2026-08-26-full-feature-audit/
├── README.md          report principale, findings per severity, stato copertura
├── inventory.csv      il tracker
├── wave-0-harness.md  … una scheda per ondata, con le evidenze
└── evidence/          screenshot, estratti di risposta, righe DB, righe di log
```

## Severity

| Severity | Definizione | Azione |
|---|---|---|
| **P0** | La funzione non è eseguibile, oppure perde/corrompe dati, oppure espone dati altrui | Fix in-session, PR verso `main-dev`, riverifica live, issue chiusa nella stessa passata |
| **P1** | Eseguibile ma con risultato sbagliato, errore visibile, o effetto non persistito | Issue singola con evidenza completa |
| **P2** | Incoerenza o attrito UX che non blocca | Issue raggruppata per contesto |
| **P3** | Cosmetico, i18n, testo | Riga nel report, batch a fine audit |

## Rischi noti

1. **Snapshot stantio** — lo stack locale è spento; `make dev-from-snapshot` va avviato e lo
   snapshot potrebbe non rispecchiare il corpus attuale. Se accade, l'ondata 3 misura dati non
   rappresentativi e il problema emerge solo lì. Mitigazione: verificare la freschezza dello
   snapshot alla fine dell'ondata 0, non all'ondata 3.
2. **Cloudflare Access su staging** — gli spot-check richiedono `CF_ACCESS_CLIENT_ID/SECRET` o un
   browser già autenticato. Senza, l'ondata 5 (email reali, R2, OAuth) resta parziale: va scritto
   nel report, non aggirato.
3. **Side-effect in navigazione** — alcune rotte scrivono su GET. Il crawler gira **solo in
   locale**, dove lo stato è resettabile.
4. **Granularità del diff DB** — il conteggio per tabella dice dove si è scritto, non se il
   contenuto è corretto. Per L2/L3 si legge la riga vera.

## Criteri di completamento

L'audit è concluso quando:

- ogni riga di `inventory.csv` ha uno stato diverso da `⬜ non coperto`;
- ogni riga `✅ verificato` ha un'evidenza allegata coerente col suo livello;
- ogni finding P0/P1 ha un'issue GitHub aperta (o chiusa, per i P0 corretti in sessione);
- il `README.md` riporta la copertura per contesto e dichiara esplicitamente ciò che è rimasto
  fuori e perché.
