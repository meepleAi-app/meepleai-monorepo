# Happy Path Testing Program — Strategia (browser, locale → staging)

**Data**: 2026-07-10 · **Branch**: main-dev · **Stato**: design approvato, in attesa di piano di esecuzione

## 1. Obiettivo

Verificare che **tutte le funzionalità dell'app** funzionino sui loro **percorsi di successo (happy path)**, eseguendoli con un **browser reale** — prima in **locale** (`localhost`), poi in **staging** (`meepleai.app`) quando la locale conferma. Il programma **verifica e mappa lo stato reale**; non è un lavoro di fix.

Non-obiettivi (esplicitamente fuori scope):
- Test negativi / edge case / validazioni di errore (solo happy path).
- Automazione in CI (nessun codice Playwright prodotto in questo programma).
- Fix dei difetti trovati (i fallimenti diventano issue GitHub, il fix è lavoro separato).
- Performance/load/security testing (domini a sé).

## 2. Perimetro — 220 route, 13 macro-aree

L'app ha 220 `page.tsx`. Le raggruppiamo in 13 macro-aree, ognuna con il proprio catalogo di scenari.

### Lato utente (il prodotto)
| ID | Macro-area | Route chiave |
|----|-----------|--------------|
| **U1** | Accesso & Onboarding | `(public)` pricing/about/contact/legal · `(auth)` register (invite-only + request-access)/login/logout/reset-password/verify-email/setup-account/welcome/verification-pending/accept-invite · `(authenticated)/onboarding` wizard · 2FA |
| **U2** | Catalogo & Discover | `(authenticated)/games` hub (tab discover/catalogo/trending/community) · `/discover` · `(public)/shared-games/[id]` |
| **U3** | Library & Knowledge Base | `/library` · `/library/wishlist` · `/library/private` (+ add/[id]) · `/library/[gameId]` (kb) · `/upload` · `/knowledge-base` (+ global, [id], [id]/pdf) · `/kb/[id]` · `/gamebook` (+ upload) |
| **U4** | Chat RAG & Agenti | `(chat)/chat` · `/library/[gameId]/agent` · `/agents` (+ [id]) · streaming SSE + citazioni live |
| **U5** | Game Night | `/game-nights` (+ new, [id], [id]/live, [id]/summary) · RSVP & inviti · `(public)/join/event/[code]` |
| **U6** | Sessioni & Scoring | `/sessions` (+ new, join, [id], [id]/live, [id]/notes, [id]/scoreboard, [id]/join) · `/play-records` (+ new, [id], [id]/edit, stats) · `/players` (+ [id] e sotto-pagine) · `(public)/join/session/[code]` · scoring polimorfico live |
| **U7** | Toolkit & Gamebook play | `/toolkit` (play/history/stats/templates) · `/toolkits` (+ [id]) · `/hub/toolkits` · `/library/[gameId]/toolbox` · `/library/[gameId]/toolkit` · `/library/[gameId]/play/[campaignId]` (+ encounter/translate) |
| **U8** | Profilo & Notifiche | `/profile` (+ achievements) · `/notifications` (+ preferences) · `/versions` · `/dashboard` |

### Lato admin (tooling)
| ID | Macro-area | Route chiave |
|----|-----------|--------------|
| **A1** | Agenti AI | `admin/agents/*` — builder, config, playground, ab-testing, definitions (+ create/[id]/edit/playground), models, pipeline, sandbox, strategy, templates, inspector, analytics, usage, chat-history, chat-limits, debug, debug-chat, infrastructure |
| **A2** | Knowledge Base admin | `admin/knowledge-base/*` — documents, embedding, queue, rag-pipeline, feedback, games, processing, mechanic-extractor (dashboard/analyses) · `admin/rag-quality` |
| **A3** | Catalogo condiviso | `admin/shared-games/*` — list/all, new, import, wizard, seeding, [id] (+ kb, rag-setup, knowledge-base) · `admin/games/*` (new, [gameId]/phases, [gameId]/agent/test, [gameId]/processing) |
| **A4** | Config & Sistema | `admin/config` (+ tiers, n8n) · `admin/content` (+ email-templates) · `admin/database-sync` · `admin/ai` |
| **A5** | Monitoraggio & Utenti | `admin/monitor/*` (grafana, mau, logs, services, service-calls) · `admin/users/activity` · `admin/analytics` · `admin/ui-library` (+ compositions, [id]) |

> L'inventario route completo e la mappatura scenario↔route verranno prodotti nei singoli cataloghi (Fase A), con esplorazione per area.

## 3. Metodo

- **Catalogo scenari**: ogni happy path è scritto in **Given/When/Then** con **dati concreti** (stile *Specification by Example*, Adzic). Un ID stabile per scenario (`<AreaID>-NN`).
- **Esecuzione browser reale**: gli scenari vengono eseguiti nel browser con **login vero** (nessun `PLAYWRIGHT_AUTH_BYPASS`, nessun mock). Strumento: MCP browser (Playwright MCP / claude-in-chrome).
- **Due livelli di granularità**:
  - **Flow** — flusso transazionale multi-step; l'happy path è l'intero flusso end-to-end (es. crea game night → invita → avvia sessione → segna punteggi → chiudi).
  - **Smoke** — vista prevalentemente read-only (tipico di molte dashboard admin: monitor, analytics, usage). Criterio: **la pagina carica senza errori 4xx/5xx non attesi (Network) né errori JS (Console) · skeleton → contenuto reale (o empty-state legittimo) · l'azione primaria (tab, filtro, apertura dettaglio) produce un effetto visibile a schermo**. Evita di trattare 120 pagine admin come 120 flussi completi.
- **Osservabilità**: ogni scenario dichiara i **criteri di successo osservabili nel browser** (testo presente, elemento visibile, navigazione avvenuta, chip citazione, ecc.). Niente asserzioni non verificabili a schermo.

### 3.1 Verifica CRUD & persistenza dati

Gli scenari *Flow* che **mutano dati** non si fermano al feedback UI ottimistico: devono verificare la **variazione reale e persistente** del dato, eseguita **via browser**:

- **Create** → l'entità appare in lista/dettaglio **e** dopo un **reload** della pagina è ancora presente (persistita nel backend).
- **Save/Edit** → il campo modificato si aggiorna a schermo **e** dopo reload il nuovo valore persiste.
- **Delete** → l'entità sparisce dalla lista **e** dopo reload resta assente.

Per ogni **entità gestibile** dall'utente/admin il corpus include il ciclo completo **Create → Edit → Delete** dove la UI espone l'operazione (in un unico scenario di ciclo di vita, o in scenari collegati). La **verifica di persistenza** (reload / re-navigazione che riconferma lo stato) è l'osservabile che distingue una mutazione reale dal solo feedback ottimistico. I `Delete` operano **solo** su dati marcati `HP-TEST-<data>` (mai su dati seed condivisi o reali). Per le entità con concorrenza `xmin`/`RowVersion` (ADR-060) il salvataggio non deve generare conflitti sull'happy path.

## 4. Prerequisiti & setup

### Locale
- Stack completo (serve l'AI per RAG/chat/toolkit): `cd infra && make dev`.
- Seed dataset: `make seed-sp4` — popola admin + 5 utenti + giochi + PDF indicizzati + agenti + toolkit + library + sessioni + play-records + game night + chat.
- Porte: web `http://localhost:3000` · API `http://localhost:8080` (`/scalar/v1` per gli endpoint).
- Account:
  - **Admin** — da `infra/secrets/admin.secret` (`ADMIN_EMAIL`/`ADMIN_PASSWORD`).
  - **Utente standard** — `marco@meepleai.test` (premium, email-verificato dal seed). Password: default in `seed_password()` (`infra/scripts/seed-sp4/lib/common.sh`) o override `SEED_SP4_PASSWORD`.
  - Altri utenti: `sara|luca|giulia|andrea@meepleai.test` (per scenari multi-utente: inviti, RSVP, sessioni condivise).

### Staging (`meepleai.app`)
- Seed: `make tunnel` (Git Bash) + `make seed-sp4-staging`.
- **Gate email/tier su staging**: il force-verify email + tier=premium via `UPDATE` diretto è **solo locale**. Su staging serve il flusso SSH+SQL separato (vedi `seed-sp4/README.md` e operations manual). Va confermato prima di eseguire le aree che richiedono utenti verificati/premium.

## 5. Formato scenario

### Template
```gherkin
Scenario <AreaID>-NN [Flow|Smoke]: <titolo breve>
  Given <stato/precondizione con dati concreti>
    And <…>
  When <azione utente nel browser>
    And <…>
  Then <esito atteso osservabile>
    And <…>
  Osservabile ✅: <lista concreta di ciò che a schermo conferma il pass>
  Route: <path/i coinvolti>
  Utente: <admin | marco | multi-utente …>
```

### Esempio reale (U4)
```gherkin
Scenario U4-03 [Flow]: Risposta citata su una regola di gioco
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And il gioco "Azul" ha un PDF regole indicizzato (seed KB)
  When apro /library/{azulId}/agent e invio "Quanti punti vale una riga completa?"
  Then entro ~10s vedo la risposta in streaming (SSE)
    And contiene ≥1 citazione cliccabile tipo [Azul, p.N]
    And il click sulla citazione apre il PDF alla pagina citata
  Osservabile ✅: testo risposta non-vuoto + ≥1 chip citazione + apertura PDF
  Route: /library/[gameId]/agent
  Utente: marco
```

### Criterio di pass
Uno scenario è **pass** (✅) solo se **tutti** gli osservabili elencati sono veri a schermo, senza errori Console/Network non attesi. Se un osservabile è vero ma altri no → **fail** (❌). Se l'ambiente impedisce l'esecuzione (dato mancante, servizio giù, gate ambientale) → **blocked-env** (⚠️), che è distinto da fail e non blocca il gate di §7.

## 6. Ordine di esecuzione (grafo dipendenze-dati)

Molte aree utente dipendono da dati creati dall'admin/seed. Ordine:

```
U1 Accesso
  └─► [setup dati: seed-sp4 pre-popola ciò che A3 Catalogo + A2 KB creerebbero a mano]
        └─► U2 Catalogo/Discover ─► U3 Library/KB ─► U4 Chat RAG
              └─► U5 Game Night ─► U6 Sessioni/Scoring
                    └─► U7 Toolkit/Gamebook ─► U8 Profilo/Notifiche
                          └─► A3 Catalogo ─► A2 KB ─► A1 Agenti ─► A4 Config ─► A5 Monitoraggio
```

**Doppia natura di A3/A2**: come *fonte dati* sono già soddisfatte dal `seed-sp4` (giochi + PDF indicizzati pronti), così le aree utente U2-U4 sono testabili senza prima eseguire a mano il flusso admin. Come *cataloghi con propri scenari Flow* (creazione gioco/import/wizard, upload+embedding KB da zero) vengono comunque eseguiti nella **fascia admin** (dopo U8), all'inizio del blocco A. Tutte e 5 le aree admin (A3, A2, A1, A4, A5) hanno una finestra di esecuzione esplicita.

## 7. Flusso locale → staging & gate

Per ogni macro-area, in ordine:
1. **Esecuzione locale** di tutti gli scenari dell'area.
2. **Gate**: l'area passa a staging solo se **tutti gli scenari sono verdi in locale**. Uno scenario fallito → si apre issue, si annota, l'area resta "bloccata su staging" fino a decisione (fix separato o waiver esplicito).
3. **Esecuzione staging** degli stessi scenari.
4. **Delta locale/staging**: ogni divergenza di comportamento locale vs staging è registrata nel report (spesso rivela problemi di config/ambiente, non di codice).

### 7.1 Gestione stato dati & ripetibilità

Gli scenari *Flow* mutano il DB seedato; *Smoke*/read no. Regole per esecuzione ripetibile e non distruttiva:

- **Dati marcati**: ogni entità creata da uno scenario Flow usa un marcatore riconoscibile `HP-TEST-<data>` nel titolo/nome (es. game night "HP-TEST-2026-07-10 Serata Azul"). Rende i dati di test distinguibili e ripetibili senza collidere col seed.
- **Ordine additivo**: preferire scenari che *creano* dati nuovi anziché mutare/distruggere quelli seed, così l'esecuzione non erode le precondizioni degli scenari successivi.
- **Reset tra blocchi**: se lo stato diverge troppo dopo un blocco di aree, `make seed-sp4-reset && make seed-sp4` (locale, idempotente) ripristina una base pulita.
- **Staging safety** — su `meepleai.app` gli scenari Flow creano **dati reali** su ambiente condiviso:
  - Solo dati marcati `HP-TEST-<data>`; **nessun** delete massivo o operazione distruttiva senza conferma esplicita.
  - Cleanup a fine giro: `make seed-sp4-reset-staging` (conferma esplicita) per i dati seed; i dati Flow creati a mano si annotano nel report per rimozione mirata.

## 8. Report & gestione fallimenti

`docs/for-developers/testing/happy-path/RESULTS.md` — una tabella per macro-area:

| ID | Tipo | Locale | Staging | Screenshot | Note / Issue |
|----|------|--------|---------|-----------|--------------|
| U4-03 | Flow | ✅ | ⏭️ | `u4-03-local.png` | — |

Legenda stato: ✅ pass · ❌ fail · ⏭️ non ancora eseguito · ⚠️ pass con riserva.

Fallimenti → **issue GitHub** con: scenario ID, passi per riprodurre, atteso vs osservato, screenshot, ambiente (locale/staging). Nessun fix in questo programma.

## 9. Struttura documentale & ritmo (big-bang)

- **Master di strategia** (questo file): `docs/superpowers/specs/2026-07-10-happy-path-testing-program-design.md`.
- **13 cataloghi scenari**: `docs/for-developers/testing/happy-path/{U1-accesso, U2-catalogo, U3-library-kb, U4-chat-rag, U5-game-night, U6-sessioni-scoring, U7-toolkit-gamebook, U8-profilo-notifiche, A1-agenti, A2-kb-admin, A3-catalogo-condiviso, A4-config-sistema, A5-monitoraggio}.md`.
- **Report esecuzione**: `docs/for-developers/testing/happy-path/RESULTS.md`.

**Ritmo big-bang** (scelto):
- **Fase A — Produzione**: si scrivono **tutti i 13 cataloghi** (~220 scenari) come corpus unico. Ogni area richiede una breve esplorazione (route, componenti, endpoint) per scenari accurati. **Ogni catalogo apre con una matrice di copertura** che mappa ciascuna route dell'area ad ≥1 scenario — o la marca `smoke-aggregato` / `skip: non-user-facing` con motivo — così nessuna delle 220 route resta scoperta silenziosamente. Parallelizzabile per area con subagenti. → **review utente** dell'intero corpus.
- **Fase B — Esecuzione**: dopo l'approvazione dei cataloghi, esecuzione in blocco secondo l'ordine §6 e i gate §7, aggiornando `RESULTS.md`.

## 10. Rischi & note operative

| Rischio | Impatto | Mitigazione |
|---------|---------|-------------|
| Registrazione richiede verifica email (403 `EmailVerificationMiddleware`, no bypass admin) | U1 "register" non completabile in locale senza il token | Confermare all'esecuzione come si recupera il token in locale (SMTP fake/log/Mailhog). Se non recuperabile, marcare lo step come `⚠️ blocked-env` e usare gli utenti seed pre-verificati per il resto. |
| `make dev-core` non avvia l'AI stack | U4/U7 (RAG, toolkit AI) non testabili | Usare `make dev` (full) per l'esecuzione locale. |
| Gate email/tier su staging non applicato dal seed | Aree premium/verified falliscono su staging | Eseguire il flusso SSH+SQL di verifica su staging prima di quelle aree. |
| Contenuti generati da LLM non deterministici | Asserzioni sul testo esatto fragili | Osservabili basati su struttura (presenza risposta, ≥1 citazione, streaming avvenuto), non su testo letterale. |
| 220 scenari = volume alto | Esecuzione lunga | Livello *Smoke* per le viste read-only admin; parallelizzazione della Fase A. |
| BGG asset ban attivo (freeze 2026-06-10) | Scenari che si aspettano cover BGG lato utente falliscono per design | Osservabili usano il placeholder deterministico (`cover-utils.ts`), non asset BGG. |
| Scenari Flow mutano il DB seedato → stato divergente tra esecuzioni | Ripetibilità compromessa, collisioni dati | Dati marcati `HP-TEST-<data>`, ordine additivo, `seed-sp4-reset`+re-seed tra blocchi (§7.1). |
| Esecuzione Flow su staging inquina `meepleai.app` | Dati di test su ambiente condiviso | Solo dati `HP-TEST`, niente delete massivi senza conferma, cleanup `seed-sp4-reset-staging` (§7.1). |
| Ambiente locale non parte o seed fallisce | Fase B bloccata | Pre-flight in Fase B: verificare `:3000`/`:8080` rispondono + `seed-sp4` completato prima di eseguire; se KO, diagnosticare prima di procedere. |

## 11. Definizione di "fatto" del programma

- Tutti i 13 cataloghi scritti e approvati (Fase A).
- Tutti gli scenari eseguiti in locale, con esito registrato in `RESULTS.md`.
- Le aree con locale-verde eseguite anche in staging, con delta registrati.
- Ogni fallimento tracciato come issue GitHub.
