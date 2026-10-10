# Piano — risoluzione dei bloccanti lato utente

**Data**: 2026-10-10 · **Issue**: #4154 #4155 #4156 #4157 #4158 (+ coordinamento con #4138 e #4114)
**Design**: [`2026-10-10-user-use-case-blockers-fix-design.md`](../specs/2026-10-10-user-use-case-blockers-fix-design.md), con le decisioni D1, D2 e D3 già prese.
**Origine**: [`user-use-cases.md`](../user-use-cases.md), § *Verifica nel browser*.

## Regole operative (valgono per ogni issue)

- **Un worktree per issue**, sotto `.claude/worktrees/`, con un branch `feature/issue-<n>-<desc>` creato da `origin/main-dev` e parent `main-dev`. Il checkout `D:\Repositories\meepleai-monorepo-main` è condiviso con altre sessioni: lì non si cambia branch e non si fa commit.
- **Prima il test che fallisce.** Ogni issue ha nel design almeno un test «rosso prima». Si scrive, lo si vede fallire per il motivo atteso, e solo dopo si corregge.
- **Push** con il token di `meepleAi-app` e l'helper azzerato, in background: l'hook pre-push dura più di 3 minuti. **`gh`** con `GH_TOKEN` esplicito, senza cambiare l'account attivo.
- **Verifica nel browser** su immagini `api` e `web` **ricostruite dal commit della PR** (`docker compose -f docker-compose.yml -f compose.dev.yml build api web`, poi `make dev`). Un'immagine di ieri misura codice che non esiste più.
- **Definition of Done**:
  1. test rosso → verde;
  2. suite del contesto toccato verde;
  3. `pnpm typecheck` e `pnpm lint` (se tocca il frontend), oppure `dotnet build` (se tocca il backend);
  4. verifica nel browser dei criteri di accettazione;
  5. PR verso `main-dev`;
  6. code review;
  7. merge;
  8. la riga del caso d'uso aggiornata in `user-use-cases.md`;
  9. la issue chiusa con la DoD spuntata.

## Fasi

### Fase 0 — impostazione (questa PR)

- [ ] Commit di `user-use-cases.md`, della specifica e di questo piano sul branch `feature/user-use-cases-docs`, con PR verso `main-dev`.
- [ ] Su ogni issue, un commento con la decisione presa (D1, D2, D3 dove applicabile) e i criteri di accettazione.
- [ ] Un messaggio alla sessione che lavora su #4138 (PR aperta #4159) per decidere chi corregge #4154.

### Fase 1 — #4154, insieme alla linea #4138

> **Aggiornamento 2026-10-10.**
> - **Coordinamento chiuso**: la sessione di #4138 ha confermato che la correzione la fa questa linea, da `origin/main-dev` (#4159 non tocca gli schemi agente).
> - **Decisione dell'utente**: rimozione dei lettori lato utente invece della correzione dello schema. Il piano diventa:
>   1. backend, expand: il lancio in sessione senza `AgentDefinitionId` usa l'agente di sistema;
>   2. frontend: niente più elenchi agenti in libreria e nel lancio;
>   3. backend, contract: ritiro di `GET /agents?scope=my-library` e `GET /games/{id}/agents`.
> - Il worktree è `D:/Repositories/wt-4154`, senza `+` nel percorso: un `+` fa fallire la build del pre-push. Contiene già i test di contratto dello schema, che restano per i lettori admin.
>
> Il testo che segue è la versione precedente.

**Perché coordinare**: `apps/web/src/lib/api/schemas/agents.schemas.ts` e i client degli agenti sono il terreno di #4138, e la PR #4159 è aperta. Due PR parallele sugli stessi file producono conflitti, e la seconda rischia di reintrodurre ciò che la prima ha tolto.

**Due esiti possibili del coordinamento**, da registrare qui:

- **la sessione #4138 la assorbe** in una delle sue fette: questa fase si riduce a verificare nel browser dopo il merge;
- **la facciamo noi**: worktree da `origin/main-dev` *dopo* il merge di #4159, una PR piccola che tocca solo lo schema, le fixture dei test e il test di contratto.

**Verifica**: il test di contratto sul payload senza `type`; nel browser, `/library` senza errori di validazione in console e l'assistente di `/sessions/{id}/live` che non mostra più l'errore.

### Fase 2 — #4157 e #4158 in parallelo

Sono indipendenti, piccole, e non toccano gli stessi file. Vanno in due worktree e due PR.

| | #4157 preferenze | #4158 ricerca giochi |
|---|---|---|
| strato | backend (+ un controllo sul frontend) | frontend |
| rosso prima | integrazione: un `PUT` parziale oggi dà `422`; dopo deve dare `200` preservando `dataRetentionDays` e `showProfile` | unità: `getAll` chiama `/api/v1/games?search=…&page=…&pageSize=…` |
| correzione | campi nullable in `UpdatePreferencesCommand`, validator condizionale, handler che applica solo i campi presenti, nota nell'OpenAPI | query string verso il server; filtro lato client solo dove il server non filtra; risoluzione per id in `ChatThreadView` e `ChatHistoryDrawer` |
| nel browser | salvare la lingua e ricaricare la pagina | «Dominion» trovato nell'onboarding |
| follow-up da aprire | la lingua salvata che non cambia i testi (`IntlProvider` segue `navigator.language`) | censimento degli altri filtri di `GameFilters`: se nessuno li usa si tolgono, se qualcuno li usa vanno nel backend |

**Vincolo d'ordine**: #4158 va mergiata prima che la Fase 3 tocchi `FirstGameStep`.

### Fase 3 — #4155 (D1 = A: il cancello resta)

Si procede a strati, ciascuno verificabile da solo:

1. **Backend**: il diniego di `RagAccessService` restituisce `errorCode: "rag_ownership_required"` con il `gameId`. Rosso prima: un test di integrazione sul corpo del 403. Non regressione: chi non ha dichiarato riceve ancora 403.
2. **Chat**: `ChatThreadView` riconosce il codice e mostra spiegazione e CTA (`OwnershipDeclarationDialog`); dopo la conferma rinvia la domanda. Rosso prima: un test di componente.
3. **Aggiunta in libreria**: la casella «Possiedo una copia» in `AddGameDrawer`/`CatalogSearchStep` e `FirstGameStep`.
4. **Dettaglio gioco**: la CTA in `/games/[id]` per un gioco in libreria e non dichiarato.
5. **ADR**: registra il cancello (chi passa, perché, dove si dichiara) e il suo rapporto con [`copyright-tier-rag.md`](../../for-claude/architecture/copyright-tier-rag.md).

**Verifica**: uno spec Playwright con login reale (registrazione → aggiunta → dichiarazione → domanda → risposta con citazione). Il gate E2E non gira sulle PR verso `main-dev` (#4032), quindi va lanciato a mano (`gh workflow run`) con un run di controllo su `main-dev`.

### Fase 4 — #4156, coordinata con #4114 (D3: lo Score tab resta dentro)

**Perché coordinare**: #4114 (gli hook SSE `useSessionSync`/`useSessionStream` su `/game-sessions`) e #4156 hanno la stessa radice, un id di `LiveGameSession` passato al namespace di un altro aggregato, e la stessa superficie `/sessions/[id]/live`. #4114 è aperta e non assegnata. **Proposta**: prenderla insieme, nello stesso ramo o in due PR consecutive dallo stesso worktree, con un unico test architetturale che vieta `/game-sessions/` e `api.sessions.getById` sotto `app/(authenticated)/sessions/[id]/`.

Si procede a fette, ciascuna con la sua PR:

1. **Navigazione e letture**: il wizard porta a `/sessions/{id}/live`; riepilogo, `/scoreboard` e `/notes` leggono `LiveGameSession` (`useLiveSession`); il tabellone ordina per totale; si aggiunge il test architetturale.
2. **Lo Score tab su `RoundScores`**: editor giocatore × dimensione × round; scrittura con `POST`/`PUT /api/v1/live-sessions/{id}/scores`; il polimorfico esce dal percorso live; si aggiornano CLAUDE.md (§ *Live-session scoring*) e ADR-089 (riga «Update»).
3. **SSE (#4114)**: gli hook passano su `/live-sessions/{id}/stream`, se #4114 non è già stata chiusa altrove.

**Domanda aperta**, da chiudere prima della fetta 2: i tipi polimorfici (vittoria sì/no, obiettivi, classifica) diventano dimensioni convenzionali o si rinviano? Oggi il wizard non li offre.

**Verifica**: lo scenario Gherkin «percorso completo» del design (crea → segna → ricarica → tabellone → chiudi → riepilogo e partita registrata con gli stessi totali).

## Dipendenze

```
Fase 0 ─► tutte
#4159 (linea #4138) ─► #4154
#4154 ─► #4156 (verifica dell'assistente in /live)
#4158 ─► #4155 (FirstGameStep)
#4114 ⇄ #4156 (stessa superficie: coordinare)
#4157 — indipendente
```

## Tracciamento

| Issue | Branch | PR | Stato |
|---|---|---|---|
| docs | `feature/user-use-cases-docs` | — | in corso |
| #4154 | da decidere col coordinamento | — | in attesa di #4159 / della risposta |
| #4157 | `feature/issue-4157-preferences-partial-update` | — | da iniziare |
| #4158 | `feature/issue-4158-games-getall-server-params` | — | da iniziare |
| #4155 | `feature/issue-4155-declare-ownership-entry-points` | — | dopo #4158 |
| #4156 | `feature/issue-4156-sessions-live-aggregate` | — | dopo #4154; con #4114 |

## Rischi

- **Lavoro concorrente sullo stesso checkout**: lo stato git può cambiare durante qualunque pausa. Dopo ogni attesa si ricontrollano `git branch --show-current`, `git status` e `git log` *nel proprio worktree*.
- **Gate verdi e vuoti**: si chiede sempre il numero di test eseguiti, non solo l'esito.
- **GitGuardian** può impiegare fino a 40 minuti: non è bloccato.
- **Il percorso di #4156 tocca scoring e storico**: un errore lì produce partite registrate con totali sbagliati, in silenzio. Il controllo finale dello scenario confronta i totali della partita registrata con quelli segnati.
