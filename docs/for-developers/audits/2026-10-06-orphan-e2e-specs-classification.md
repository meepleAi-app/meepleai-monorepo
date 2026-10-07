# Spec Playwright orfane: inventario e classificazione

**Data**: 2026-10-06 · **Issue**: [#4092](https://github.com/meepleAi-app/meepleai-monorepo/issues/4092)

Ventuno spec Playwright vivevano fuori da ogni radice raccolta da una config: non erano skippate né
rosse, erano **assenti**. Questo documento registra cosa sono, perché nessuno se n'era accorto, e in
che stato si trovano ora che vengono eseguite.

## Perché nessuno le vedeva

Le config dichiarano radici tutte sotto `./e2e`; i file stavano sotto `./tests/e2e`,
`./__tests__/e2e` e `./src/__tests__/e2e`. Nessun `testDir` puntava là, e **nessun conteggio lo
rivelava**: «392 file raccolti» non dice quanti file esistono.

```bash
# il comando che lo mostra
node scripts/lint-orphan-specs.mjs
```

Le dieci sotto `./tests/e2e` sono state trovate a mano. **Le altre undici le ha trovate il gate**,
la prima volta che l'ho eseguito — in una seconda radice che l'indagine manuale non aveva guardato,
perché aveva cercato solo dove stava il primo lotto. È la ragione per cui questa issue consegna un
gate e non un elenco: l'elenco era incompleto mentre lo si consegnava come completo.

## Il caso che spiega il danno meglio di qualunque argomento

`rag-001-validation.spec.ts` esisteva in **entrambe** le radici. Entrambe le copie nascono dallo
stesso commit (`bcc9ac348`, 2026-01-30) e **entrambe** sono state modificate il 2026-10-05 da #4059
(`d0824577a`): 3 righe nella copia orfana, 16 in quella raccolta. Erano divergenti con correzioni
complementari, e nel verso peggiore:

| | copia raccolta (eseguita) | copia orfana (mai eseguita) |
|---|---|---|
| sorgente dei conteggi vettoriali | `GET localhost:6333/collections/...` — **Qdrant**, dismesso | `GET /api/v1/admin/kb/vector-stats` — l'API attuale |
| commento #4059 su `NEXT_PUBLIC_API_URL` | versione lunga | versione in una riga |

La copia che girava puntava a un servizio che non esiste più; quella corretta non girava. Adottata
l'orfana, eliminata l'altra.

## Classificazione, misurata

Eseguite contro lo stack locale (`baseURL http://localhost:3000`, `PLAYWRIGHT_SKIP_WEB_SERVER=1`).
Le dieci del primo lotto, **119 test**: `27 passati · 92 falliti`.

| file | passati | falliti |
|---|---|---|
| `chat-page.spec.ts` | 15 | 10 |
| `game-catalog.spec.ts` | 4 | 14 |
| `dashboard.spec.ts` | 4 | 7 |
| `admin/dashboard.spec.ts` | 2 | 14 |
| `admin-reports-email.spec.ts` | 2 | 6 |
| `admin-reports.spec.ts` | 0 | 26 |
| `admin/shared-games/manual-creation.spec.ts` | 0 | 5 |
| `admin/shared-games/agent-workflow.spec.ts` | 0 | 4 |
| `admin/ai-lab/agent-workflow.spec.ts` | 0 | 3 |
| `admin/ai-lab/analytics-workflow.spec.ts` | 0 | 3 |

**Causa dei 92 fallimenti**, raggruppata dai messaggi d'errore:

| n | causa | esempio |
|---|---|---|
| 89 | selettore o locator non trovato | `waiting for locator('text=Create Agent')` |
| 2 | codice della spec rotto | `Cannot read properties of undefined (reading 'click')` |
| 1 | asserzione su contenuto di un'altra pagina | attende `"Show password"` in una lista di giochi |

> 🔴 **Correzione del 2026-10-07 (#4098): la tabella sopra raggruppa i messaggi, non le cause, e
> la conclusione che ne traevo era sbagliata.** La riga che diceva «Nessun fallimento per ambiente,
> autenticazione o servizio assente. **È deriva di selettori contro una UI che è cambiata**» è
> **falsa**. Riaprendo i 24 fallimenti residui e guardando l'**istantanea di pagina** invece del
> solo messaggio, **tutte e 24 mostravano la pagina di login**: le spec non si autenticavano, e
> `proxy.ts` rimanda `/admin/**`, `/chat` e `/dashboard` a `/login?from=...`.
>
> Un `toBeVisible` che non trova nulla sulla pagina di login produce **esattamente** il messaggio
> di un selettore derivato. Raggruppare per messaggio d'errore misura la forma del fallimento, non
> la sua causa, e qui le due cose divergevano al 100%: una causa unica travestita da 89 cause
> indipendenti. Un indizio c'era e non l'ho seguito — il test `should display PDF analytics`, che
> asserisce **solo** la URL appena richiesta, era fra i falliti: nessuna deriva di selettori può
> far fallire un test senza selettori.
>
> Lezione riusabile: **per attribuire la causa di un fallimento Playwright, leggi `error-context.md`
> per intero, non la riga `Error:`.** L'istantanea di pagina dice su quale pagina sei; il messaggio
> dice solo che non hai trovato ciò che cercavi.
>
> Conseguenza pratica: sistemata l'autenticazione, i «19 passati» di quel primo lotto sono scesi a
> 19 su 36 ma **cambiando insieme**: diverse asserzioni di NON-visibilità passavano perché la
> pagina di login non contiene nulla di ciò che negavano. Erano **passaggi vacui**, non copertura.
> Contare i verdi senza controllare su quale pagina sono maturati sovrastima la copertura.

Le spec sono state scritte fra il 2025-11-30 e il 2026-05-12 e da allora nessuno le ha eseguite.
L'ultimo commit che le ha toccate si chiama «restyle Gaming Hub to token-first mock fidelity».

## Perché il typecheck non c'entra

Il DoD iniziale della issue chiedeva di far vedere le spec al typechecker «così la prossima deriva
si vede alla build». **Misurato: la premessa è falsa per questi file.** Le ventuno adottate hanno
**zero** errori di tipo e non importano nulla dall'applicazione — solo `@playwright/test` e
`@axe-core/playwright`. La loro deriva vive in selettori e rotte, che sono stringhe: nessun
typechecker la vedrà mai. L'unica rete per quella classe di deriva è **eseguirle**.

Esiste però un debito di tipi reale e separato: **181 errori** nelle spec e2e preesistenti
(`tsc --noEmit` su un tsconfig che includa `e2e/**/*.ts`). Non è questa issue.

## Stato dei gate: adottarle non cambia il colore di nulla

Va detto per non lasciare credere a una copertura che non c'è:

- `test-e2e.yml` è l'unico workflow che esegue la suite Playwright completa. Parte solo su PR verso
  `main`, non è required, e **in 202 run non è mai stato verde** (`gh api .../test-e2e.yml/runs?status=success` → `total_count: 0`).
- `dev-fast.yml` e `dev-async.yml` — i gate dei branch di sviluppo — non eseguono alcun test browser.
- `ci.yml` esegue in browser solo `e2e/smoke.spec.ts` (home, login, redirect) e il gate a11y, che non
  visita nessuna rotta `/admin`.

Quindi i 92 fallimenti non accendono nessun gate oggi. Il valore di questa issue è che da invisibili
sono diventati **visibili e contati**, e che il gate impedisce che il caso si ripeta.

## Cosa resta

1. ~~Riscrivere i selettori dei 92~~ — fatto in #4098, con l'esito in fondo a questo documento.
   Attenzione: «riscrivere i selettori» si è rivelata la diagnosi sbagliata, vedi la correzione
   qui sopra.
2. Classificare le **undici** spec trovate dal gate nella seconda radice: adottate ma non ancora
   eseguite, quindi il loro stato è ignoto.
3. I 181 errori di tipo nelle spec e2e preesistenti.
4. Il motivo per cui `test-e2e.yml` non è mai verde: finché resta tale, qualunque investimento in
   E2E è invisibile.

---

## Esito di #4098 — classificazione e riscrittura

Ogni test che falliva è stato **riscritto contro la UI reale**, **eliminato** col motivo, oppure
**saltato con un motivo classificato** che nomina la issue che lo riattiverà. Nessuno è stato
lasciato rosso senza etichetta.

### Eliminate: tre file, una ragione ciascuno

| file | test | perché |
|---|---|---|
| `admin/dashboard.spec.ts` | 16 | Verificava quattro pagine admin **eliminate di proposito** il 2026-03-08 («delete 4 orphan admin pages with no navigation»). Le etichette che cercava (`Collection Overview`, `User Management`) hanno 0 occorrenze in `src`. La sostituta `/admin/overview` ha già 7 file di test. |
| `game-catalog.spec.ts` | 14 | Verificava i quattro componenti che `PAGE-003` (#1865) aveva creato — `ViewToggle`, `SearchBar`, `Pagination`, `GameGrid` — **tutti e quattro eliminati**. `/games` è oggi l'hub a tab di Asse D (#1924), dove la tab `catalogo` è un `ComingSoonTab`. Nessuna pagina dell'app legge più i parametri `view`+`page` insieme. |
| `chat-page.spec.ts` | 25 | Verificava il design chat precedente a `chat-unified` (#4363/#4364). Il suo `h1` atteso (`MeepleAI Chat`) non esiste: `/chat` mostra `Le tue Chat`. La superficie ha già una directory dedicata e mantenuta, `e2e/chat/` (copy-response, export-conversation, message-regeneration, thread-view-smoke), più `agent-chat-page-rag.spec.ts` (10 test). Eliminarlo non lascia buchi. |

### Riscritte contro la verità a terra

| file | esito |
|---|---|
| `admin/shared-games/manual-creation.spec.ts` | 4 passati + 1 salto dichiarato (il rilevamento duplicati non esiste in quel flusso) |
| `admin/ai-lab/agent-workflow.spec.ts` | 2 passati, 1 saltato per #4102. Rotte corrette: `/admin/agent-definitions*` → `/admin/agents/definitions*`; `/admin/strategies` → `/admin/agents/strategy`, che reindirizza a `/admin/agents/config` |
| `admin/ai-lab/analytics-workflow.spec.ts` | da 3 test a 1, che passa. `/admin/agents/catalog` non esiste; la dashboard reale è `/admin/agents/analytics` (`Analisi Agenti`). I due test su `/admin/analytics/chat` e `/admin/analytics/pdf` sono stati eliminati: oltre alla rotta assente **non asserivano nulla** — `toHaveURL` sulla URL appena richiesta |
| `admin/shared-games/agent-workflow.spec.ts` | da 4 test a 2: 1 passa, 1 saltato per #4103. Il gioco di prova si crea via API |
| `dashboard.spec.ts` | 5 passati, 6 saltati per #4104. Il cookie `mock-session-token` è stato sostituito dal login reale |
| `admin-reports.spec.ts` + `admin-reports-email.spec.ts` | 32 saltati con motivo (`/admin/reports` è un 308 verso `/admin/analytics?tab=reports`, la cui `ReportsTab` rende `EmptyFeatureState`), 2 passati |

### Il nuovo helper: `e2e/_helpers/realAdminAuth.ts`

Le due strade di autenticazione già presenti — `seedAuthSession` e `loginAsAdmin` di
`fixtures/auth.ts` — dipendono **entrambe** da `PLAYWRIGHT_AUTH_BYPASS=true`, che solo il webServer
di `playwright.config.ts` imposta. Contro qualunque altro server (un container locale, `next start`
a mano) il proxy valida il token finto contro il backend e lo rifiuta. L'helper nuovo fa il login
vero via `POST /api/v1/auth/login`, quindi funziona in entrambi i casi.

Due trappole che ha dovuto assorbire, entrambe scoperte misurando:

- **Il banner di consenso cookie** copre la pagina e intercetta i click: senza `seedCookieConsent` il
  primo `click()` va in timeout su un elemento visibile ma coperto, e l'errore non nomina il banner.
- **Il rate limit del login.** Un login per test significa decine di accessi al minuto dallo stesso
  utente: il backend risponde `429` e il test fallisce con «credenziali rifiutate» mentre le
  credenziali sono giuste. L'helper tiene in cache i cookie di sessione **per worker**. Non è
  un'ottimizzazione: senza, il sospetto cade sul segreto invece che sulla frequenza.

### Tre difetti del prodotto, trovati dai test riscritti

Nessuno era noto, e nessun gate li copriva.

| issue | difetto |
|---|---|
| [#4102](https://github.com/meepleAi-app/meepleai-monorepo/issues/4102) | Creare un agent definition dalla UI è **impossibile**: lo schema FE non invia `type`, che il backend impone, e 4 dei 5 modelli cablati nella tendina non sono instradabili. Ogni submit riceve 422 |
| [#4103](https://github.com/meepleAi-app/meepleai-monorepo/issues/4103) | La tab **Agent** della scheda gioco chiama tre endpoint che non esistono sotto `/admin/shared-games`. Il 404 della query è indistinguibile da «nessun agente collegato», quindi la tab sembra funzionante e vuota, per sempre |
| [#4104](https://github.com/meepleAi-app/meepleai-monorepo/issues/4104) | `color-contrast` AA fallito (2.77 contro 4.5:1) sul CTA «+ Nuova» della dashboard in tema scuro, scritto `text-[#fff]` — un valore arbitrario che la regola ESLint sui colori cablati non vede; più i tre landmark a11y che la spec di design della restyle prescrive e che non sono stati implementati |
