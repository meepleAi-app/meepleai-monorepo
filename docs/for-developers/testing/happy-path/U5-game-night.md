# Happy Path — U5 · Game Night

> Catalogo scenari happy-path per l'area **U5 (Game Night)**. Solo percorso di successo. Formato Given/When/Then per `_TEMPLATE.md`. Keyword scenario in inglese, prosa in italiano.

## Intestazione

- **Area**: U5 — Game Night (`(authenticated)/game-nights/**` + `(public)/game-nights/shared/[token]`).
- **Prerequisiti dati (seed `make seed-sp4`)**:
  - Utenti verificati/premium: `marco@meepleai.test`, `sara@meepleai.test`, `luca@meepleai.test`, `giulia@meepleai.test`, `andrea@meepleai.test`. Password default `Sp4-Seed-Pwd!2026` (o override `SEED_SP4_PASSWORD`) — vedi `infra/scripts/seed-sp4/lib/common.sh:114`.
  - Admin: da `infra/secrets/admin.secret`.
  - Game night pre-seedate (`data.json:events[]`, create da `90-events.sh` con `POST /game-nights` → `POST /game-nights/{id}/publish`):
    - `Serata da Marco` — owner marco, partecipanti tutti e 5, giochi Azul/Wingspan/7 Wonders, **published**.
    - `Game Night Club` — owner marco, partecipanti marco/sara/andrea, giochi Brass/Spirit Island, **published**.
    - `Torneo 7 Wonders` — owner luca, 5 partecipanti, **NON published** (resta Draft).
    - `Capodanno Ludico` — owner giulia, published, data 2025-12-31 (passata → archivio).
    - `Strategici di gennaio` — owner andrea, published, data 2026-01-18.
  - ⚠️ **Nota date seed**: `90-events.sh` forza `scheduledAt` a NOW+4/8/12… giorni (il BE richiede `scheduledAt >= NOW+1h`), quindi le date effettive divergono dal mock e sono tutte future al momento del seed. Le game night con date lontane nel passato (Capodanno, Strategici) potrebbero comparire come completate/archivio o non nel calendario del mese corrente. Osservabili basati su struttura, non sulla data letterale.
- **Utenti usati**: `marco` (host/organizzatore per i Flow di creazione), `sara` (secondo utente / invitato per RSVP e join). Scenari multi-utente richiedono due sessioni browser separate.
- **Dati creati (marcati)**: ogni game night creata da uno scenario Flow usa il prefisso `HP-TEST-<data>` nel titolo (es. `HP-TEST-2026-07-10 Serata Azul`).
- **Modello di dominio di riferimento**: `docs/for-developers/specs/2026-06-04-gamenight-session-domain-model.md`. Flow a 5 fasi tag→invii→pending→confermato (invarianti #16/#17); transition `Draft → Published` via `Publish()`; `Published → InProgress` alla prima sessione (invariante #15); max 1 sessione live per game night (invariante #10 → `MAX_LIVE_SESSIONS_EXCEEDED` 409). Mapping demo↔backend: "tagged" = create Draft (no notifica), "invited" = `publish` (email + notifica), `GameNightEvent` aggregate.

### Nota semantica: tag → "Invia inviti" → pending

Nel modello di dominio la fase "tag silente" (player aggiunti senza notifica) corrisponde alla **creazione come Draft**; la fase "Invia inviti" corrisponde alla **pubblicazione** (`POST /api/v1/game-nights/{id}/publish`), che genera gli inviti/RSVP e invia le email/notifiche. Nella UI attuale (`GameNightDetailView`) il pulsante che innesca gli inviti è **"Pubblica"** (`data-testid="publish-game-night"`), visibile solo all'host e solo quando la game night è in stato **Draft**. La label testuale "Invia inviti" del modello di dominio è quindi realizzata dal pulsante Pubblica. Il wizard `/game-nights/new` invia opzionalmente `invitedUserIds`: il BE (`CreateGameNightCommandHandler` → `gameNight.PreInvite(...)`) crea comunque la game night in stato **Draft** con gli invitati pre-caricati (nessun auto-publish); la pubblicazione resta un passo esplicito. Quindi la Draft creata in U5-02 è sempre una sorgente Draft valida per U5-04. Se in futuro il BE cambiasse e la creazione con invitati producesse già una Published, U5-04 si esegue partendo da `Torneo 7 Wonders` (seed Draft) come sorgente Draft alternativa.

### Nota CRUD: operazioni esposte in UI sull'entità Game Night (per U5-15)

Verificato nel codice (`apps/web/src/lib/api/clients/gameNightsClient.ts`, `GameNightDetailView.tsx`, `GameNightEditDrawer.tsx`, `GameNightForm.tsx`) quali operazioni CRUD la UI espone sulla **game night** stessa:

| Operazione | Endpoint | Trigger UI | Visibile quando |
|-----------|----------|-----------|-----------------|
| **Create** | `POST /api/v1/game-nights` | wizard `/game-nights/new` (U5-02) | qualsiasi utente premium |
| **Edit** | `PUT /api/v1/game-nights/{id}` | pulsante **"Modifica"** → deep-link `?action=edit` → `GameNightEditDrawer` (`GameNightForm`: campi `#title`, `#description`, `#scheduledAt`, `#location`, `#maxPlayers`) → submit **"Salva modifiche"** → toast **"Serata aggiornata"** (`gameNightDetail.editDrawer.savedToast`) | pulsante visibile **solo su Draft** (host); il drawer `?action=edit` è montabile dall'organizzatore anche via deep-link su stati non-terminali (IDOR-guard: mai per un non-organizzatore) |
| **Delete/Annulla** | `POST /api/v1/game-nights/{id}/cancel` | pulsante **"Annulla serata"** (variante destructive, icona XCircle) → toast **"Serata annullata"** (`gameNightDetail.actor.host.cancelledToast`) | pulsante visibile **solo su Published** (host) — condizione `!isDraft && !isCancelled && !isCompleted` |

**⚠️ Nessun hard-delete.** Non esiste alcun endpoint/pulsante che *rimuove* fisicamente una game night dalla lista. L'operazione "Delete" del ciclo CRUD è realizzata dal **cancel** (soft-delete: `status → Cancelled`). Conseguenza sulla verifica di persistenza: dopo il cancel + reload la game night **resta presente nella lista** con la status-pill **"Annullata"** (`gameNightsIndex.status.cancelled`) e il dettaglio mostra il `GameNightCancelledBanner` (titolo "Serata cancellata"). L'osservabile di persistenza del delete è quindi **"presente ma Annullata"**, non "assente dalla lista".

**⚠️ Sequenza CRUD obbligata.** Il pulsante "Modifica" è **Draft-only** e il pulsante "Annulla serata" è **Published-only**: sulla stessa game night NON si può passare direttamente da Edit a Cancel. Il ciclo completo su una singola entità è quindi **Create (Draft) → Edit (su Draft) → Pubblica → Annulla (su Published)**. U5-15 esegue esattamente questa sequenza.

**⚠️ Modifica/rimozione invitato non esposta.** Post-creazione la UI di dettaglio **non** espone controlli per aggiungere o rimuovere un invitato: la label i18n `kickGuest`/"Rimuovi" esiste nel bundle ma non è cablata ad alcun handler, e `GameNightRsvpRow` renderizza il roster in sola lettura. L'unico punto di tagging invitati è lo step 3 del wizard di creazione (U5-02). Il metodo client `invite(id, userIds)` (`POST /{id}/invite`) esiste ma non ha trigger nella UI di dettaglio. Nessuno scenario copre "modifica invitato" perché l'operazione non è esposta.

---

## Matrice di copertura

| Route | Liv. atteso | Scenario/i | Note |
|-------|-------------|------------|------|
| `(authenticated)/game-nights` | Smoke | **U5-01** (smoke list + view/filter) · toccata anche da U5-02/03/04 | Lista calendario+list, filtri, "+ Nuova" |
| `(authenticated)/game-nights/new` | Flow | **U5-02** (crea game night via wizard) | Wizard 4 step, submit → redirect detail |
| `(authenticated)/game-nights/[id]` | Flow | **U5-03** (detail Draft host) · **U5-04** (pubblica) · **U5-05** (RSVP guest) · **U5-06** (voting tab) · **U5-07** (smoke detail published) · **U5-08** (avvia sessione da detail) · **U5-15** (ciclo CRUD crea→edita→annulla + reload) | Detail status-branched (Draft/Published/Completed/Cancelled) |
| `(authenticated)/game-nights/[id]/live` | Flow | **U5-09** (smoke live read-only) · **U5-10** (organizer avvia prossimo gioco) · **U5-11** (completa partita + concludi serata) | Hub live read-only + CTA organizer |
| `(authenticated)/game-nights/[id]/summary` | Smoke | **U5-12** (smoke summary read) · **U5-13** (genera share-link) | KPI/MVP recap + share + gallery foto |
| `(public)/game-nights/shared/[token]` | Smoke | **U5-14** (pagina pubblica via token) | Summary read-only anonimo, nessuna CTA |
| `(public)/join/event/[code]` | Flow | **coperto in U6** | Join-by-code RSVP anonimo — catalogato in U6 con gli altri `join/*` (referenziato da U5-04b) |

**Copertura**: 6 route dell'area U5 (mappa `_coverage-map.md` §U5) → tutte mappate ad ≥1 scenario. `join/event/[code]` è concettualmente U5 ma catalogato in U6 (nota mappa) — qui referenziato, non duplicato.

**Smoke-aggregato**: nessuno. **Skip**: nessuno.

---

## Scenari

### U5-01 [Smoke]: Lista game night — calendario, filtri e vista lista

```gherkin
Scenario U5-01 [Smoke]: Lista game night — calendario, filtri e vista lista
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And il seed ha creato game night di cui marco è owner/partecipante (es. "Serata da Marco")
  When apro /game-nights
    And la vista calendario carica (default view=calendar)
    And clicco il toggle vista "Lista" nell'header
    And clicco un filtro diverso nella pill-bar (es. "Organizzate")
  Then la pagina carica senza errori 4xx/5xx (Network) né errori JS (Console)
    And lo skeleton lascia posto al contenuto reale (griglia calendario o lista raggruppata per mese)
    And il toggle vista aggiorna l'URL a ?view=list e mostra la lista (section data-testid="game-nights-list")
    And il click sul filtro aggiorna l'URL a ?filter=organizing e la lista si restringe di conseguenza
    And è presente il pulsante header "+ Nuova" (ctaNew)
  Osservabile ✅: header con contatore mese + CTA "+ Nuova" · URL ?view=list dopo toggle · section [data-testid="game-nights-list"] con ≥1 card (o empty-state legittimo se il filtro azzera) · URL ?filter=organizing dopo click filtro · nessun errore Console/Network
  Route: /game-nights
  Utente: marco
```

### U5-02 [Flow]: Crea una game night con il wizard a 4 step

```gherkin
Scenario U5-02 [Flow]: Crea una game night con il wizard a 4 step
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And marco ha giochi in libreria (seed: Azul, Catan, Wingspan…)
  When apro /game-nights/new
    And digito il titolo "HP-TEST-2026-07-10 Serata Azul" nel campo titolo (data-slot="game-night-create-title-input")
    And allo step 1 (Quando) scelgo una data/ora futura (≥ oggi+1h)
    And avanzo con "Avanti" allo step 2 (Dove) e scelgo un tipo location (es. "Casa mia")
    And avanzo allo step 3 (Chi) e taggo sara come partecipante (ricerca giocatore)
    And avanzo allo step 4 (Cosa) e seleziono "Azul" dalla libreria
    And clicco il pulsante di submit finale ("Crea"/nav.submit)
  Then la creazione va a buon fine (POST /api/v1/game-nights → 201 con id)
    And compare un toast di successo (gameNightCreate.submit.successToast)
    And vengo reindirizzato a /game-nights/{nuovoId}
    And la pagina di dettaglio mostra il titolo "HP-TEST-2026-07-10 Serata Azul"
  Osservabile ✅: avanzamento step con URL ?step=2→3→4 · toast successo · redirect a /game-nights/{id} (URL cambia) · hero dettaglio col titolo HP-TEST · POST /api/v1/game-nights = 201 (Network)
  Route: /game-nights/new → /game-nights/[id]
  Utente: marco
```

### U5-03 [Flow]: Dettaglio di una Draft — vista host con azioni Pubblica/Modifica

```gherkin
Scenario U5-03 [Flow]: Dettaglio di una Draft — vista host con azioni Pubblica/Modifica
  Given sono loggato come marco@meepleai.test (host)
    And esiste una game night in stato Draft di cui marco è organizzatore
        (creata da U5-02 se atterra in Draft, oppure una game night appena creata senza invitati)
  When apro /game-nights/{draftId}
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo l'hero con titolo, stato "Bozza" e riga organizzato-da marco
    And essendo host su una Draft vedo la action-row con "Modifica" e "Pubblica" (data-testid="publish-game-night")
    And vedo il layout di planning legacy (GameNightPlanningLayout) con i giochi disponibili
  Osservabile ✅: hero con titolo + badge stato "Bozza" · pulsante [data-testid="publish-game-night"] visibile · link "Modifica" (?action=edit) · planning layout renderizzato · nessun errore Console/Network
  Route: /game-nights/[id]
  Utente: marco
```

### U5-04 [Flow]: Pubblica una game night (tag → "Invia inviti" → pending)

```gherkin
Scenario U5-04 [Flow]: Pubblica una game night (tag → "Invia inviti" → pending)
  Given sono loggato come marco@meepleai.test (host)
    And esiste una game night Draft di cui marco è organizzatore, con almeno sara taggata
        (fonte: la Draft creata in U5-02, oppure il seed "Torneo 7 Wonders" — owner luca, quindi in tal caso eseguire loggato come luca)
  When apro /game-nights/{draftId}
    And clicco "Pubblica" (data-testid="publish-game-night")
  Then la pubblicazione va a buon fine (POST /api/v1/game-nights/{id}/publish → 200/204)
    And compare un toast di conferma pubblicazione (gameNightDetail.actor.host.publishedToast)
    And il dettaglio si aggiorna allo stato Published (l'hero non mostra più "Bozza")
    And le RSVP dei giocatori taggati passano a Pending (invito inviato — invariante #16/#17)
  Osservabile ✅: toast "pubblicata"/publishedToast · POST /api/v1/game-nights/{id}/publish = 200/204 (Network) · il pulsante "Pubblica" scompare (non più Draft) · roster RSVP visibile con status Pending · nessun errore Console/Network
  Route: /game-nights/[id]
  Utente: marco (o luca se si usa "Torneo 7 Wonders")
```

> **U5-04b (riferimento, coperto in U6)**: dopo la pubblicazione l'invitato può confermare l'RSVP anche da fuori-app tramite il link pubblico `/join/event/{code}` (endpoint anonimo `GET/POST /api/v1/game-nights/invitations/{token}`). Questo flusso è catalogato in **U6** con gli altri `join/*` — non ripetuto qui.

### U5-05 [Flow]: RSVP di un invitato su una game night pubblicata

```gherkin
Scenario U5-05 [Flow]: RSVP di un invitato su una game night pubblicata
  Given sono loggato come sara@meepleai.test (invitata, non organizzatrice)
    And esiste una game night Published in cui sara è tra gli invitati
        (seed "Serata da Marco" — sara è partecipante — oppure la game night pubblicata in U5-04)
  When apro /game-nights/{publishedId}
    And nella barra RSVP (GameNightRsvpActionBar, visibile ai soli guest) clicco "Partecipo" (accept)
  Then l'RSVP viene inviato (POST /api/v1/game-nights/{id}/rsvp con response=Accepted → 200/204)
    And compare un toast di conferma (gameNightDetail.rsvp.confirmedToast)
    And nel roster la riga di sara mostra lo status "Confermato" (RsvpRow)
  Osservabile ✅: barra RSVP guest visibile con Partecipo/Forse/Non partecipo · toast conferma · POST /api/v1/game-nights/{id}/rsvp = 200/204 (Network) · riga roster sara → stato Confermato/Accepted · nessun errore Console/Network
  Route: /game-nights/[id]
  Utente: sara
```

### U5-06 [Flow]: Votazione candidati sui giochi (tab Votazione)

```gherkin
Scenario U5-06 [Flow]: Votazione candidati sui giochi (tab Votazione)
  Given sono loggato come sara@meepleai.test (partecipante)
    And esiste una game night Published con più giochi candidati (seed "Serata da Marco": Azul/Wingspan/7 Wonders)
  When apro /game-nights/{publishedId}
    And clicco la tab "Votazione" (data-testid="tab-voting", URL diventa ?tab=voting)
    And esprimo un voto su un gioco candidato (VotingPanel)
  Then la pagina mostra il pannello di votazione con i giochi candidati
    And il voto viene registrato (POST /api/v1/game-nights/{id}/votes → 200/204)
    And il conteggio voti del candidato si aggiorna (vote tally rifetchata)
  Osservabile ✅: tab-strip Dettagli|Votazione con aria-current sulla tab attiva · URL ?tab=voting · VotingPanel con card candidati · POST /api/v1/game-nights/{id}/votes = 200/204 (Network) · conteggio voto aggiornato a schermo · nessun errore Console/Network
  Route: /game-nights/[id]?tab=voting
  Utente: sara
```

### U5-07 [Smoke]: Dettaglio di una game night pubblicata — vista completa

```gherkin
Scenario U5-07 [Smoke]: Dettaglio di una game night pubblicata — vista completa
  Given sono loggato come marco@meepleai.test
    And esiste la game night Published "Serata da Marco" (seed) di cui marco è organizzatore
  When apro /game-nights/{publishedId}
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And vedo l'hero con titolo, badge stato "In programma"/Published, riga organizzato-da e riga capacità/accettati
    And vedo la tab-strip Dettagli | Votazione
    And vedo il roster dei partecipanti (sezione RsvpRow) con ≥1 riga
    And essendo host vedo le azioni sessione ("Aggiungi partita" / "Concludi serata")
  Osservabile ✅: hero completo (titolo + stato + organizzato-da + capacità) · tab Dettagli/Votazione · sezione roster con ≥1 GameNightRsvpRow · pulsanti sessione host visibili · GET /api/v1/game-nights/{id} + /rsvps = 200 (Network) · nessun errore Console/Network
  Route: /game-nights/[id]
  Utente: marco
```

### U5-08 [Flow]: Avvia una partita da una game night pubblicata (→ in-progress)

```gherkin
Scenario U5-08 [Flow]: Avvia una partita da una game night pubblicata (→ in-progress)
  Given sono loggato come marco@meepleai.test (host)
    And esiste una game night Published di cui marco è organizzatore (seed "Serata da Marco" o la creata in U5-02+U5-04)
    And non c'è ancora nessuna sessione in corso per quella serata
  When apro /game-nights/{publishedId}
    And clicco "Aggiungi partita" (data-testid="game-night-add-partita")
    And nel GamePickerDialog scelgo un gioco della line-up e avvio la partita
  Then una nuova sessione viene creata per la serata (invariante #15: Published → InProgress alla prima sessione)
    And la game night ha ora una sessione tracciata (GameNightSessionsList mostra ≥1 elemento)
  Osservabile ✅: dialog game-picker si apre · dopo la conferma la lista sessioni (GameNightSessionsList) mostra ≥1 partita · request di start sessione = 2xx (Network) · nessun errore Console/Network
  Route: /game-nights/[id]
  Utente: marco
```

> Nota invariante #10 (max 1 live): con una sessione già live, il pulsante "Aggiungi partita" è disabilitato (`disabled={hasActiveSession}`). Lo scenario happy path avvia la **prima** partita, quindi nessun blocco. Il ramo bloccato (409 `MAX_LIVE_SESSIONS_EXCEEDED`) è fuori scope happy-path.

### U5-09 [Smoke]: Hub live read-only di una game night pubblicata

```gherkin
Scenario U5-09 [Smoke]: Hub live read-only di una game night pubblicata
  Given sono loggato come marco@meepleai.test (organizzatore)
    And esiste una game night Published/InProgress di cui marco è organizzatore (seed "Serata da Marco" o creata in U5)
  When apro /game-nights/{publishedId}/live
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And lo skeleton "Caricamento serata live" lascia posto all'hub live (NightLiveHub)
    And vedo l'header serata + la line-up dei giochi pianificati (plannedGames) + il conteggio giocatori confermati
    And il pulsante "← Torna alla serata" riporta a /game-nights/{id}
  Osservabile ✅: hub live renderizzato (header + line-up pianificata + contatore giocatori) · GET /api/v1/game-nights/{id}/live = 200 (Network) · pulsante back funzionante · nessun errore Console/Network
  Route: /game-nights/[id]/live
  Utente: marco
```

### U5-10 [Flow]: Organizzatore avvia il prossimo gioco dal live

```gherkin
Scenario U5-10 [Flow]: Organizzatore avvia il prossimo gioco dal live
  Given sono loggato come marco@meepleai.test (organizzatore)
    And apro l'hub live di una game night Published/InProgress con almeno un gioco pianificato non ancora avviato
    And nessun gioco è attualmente "live" (status ≠ live)
  When apro /game-nights/{publishedId}/live
    And clicco la CTA fissa in basso "▶ Avvia: {titolo gioco}" (organizer-only, WS1)
  Then la partita viene avviata (POST /api/v1/game-nights/{id}/sessions → 200/201)
    And il read model live si aggiorna (refetch, no flip ottimistico) mostrando il gioco corrente come live
  Osservabile ✅: CTA "▶ Avvia: {gioco}" visibile (solo organizer) · POST /api/v1/game-nights/{id}/sessions = 2xx (Network) · l'hub live riflette il gioco corrente/stato live dopo il refetch · nessun errore Console/Network
  Route: /game-nights/[id]/live
  Utente: marco
```

### U5-11 [Flow]: Completa la partita live e concludi la serata

```gherkin
Scenario U5-11 [Flow]: Completa la partita live e concludi la serata
  Given sono loggato come marco@meepleai.test (organizzatore)
    And una game night InProgress di marco ha una partita attualmente live (avviata in U5-10)
  When apro /game-nights/{publishedId}/live
    And clicco "🏁 Completa partita" e nel WinnerPickerModal confermo (con o senza vincitore)
  Then la partita live viene completata (POST /complete della sessione → 2xx)
    And con nessun gioco più live e line-up esaurita compare la CTA "🏁 Concludi serata"
    And clicco "🏁 Concludi serata" (finalize night, POST /complete della serata)
  Then la serata passa a Completed
    And vengo reindirizzato al riepilogo /game-nights/{id}/summary (LD-14: router.replace su nightStatus Completed)
  Osservabile ✅: modale winner-picker si apre · completamento partita = 2xx (Network) · CTA "🏁 Concludi serata" compare quando non c'è più live · dopo la conclusione redirect a /game-nights/{id}/summary (URL cambia) · nessun errore Console/Network
  Route: /game-nights/[id]/live → /game-nights/[id]/summary
  Utente: marco
```

> Nota: il completamento della singola partita e la conclusione della serata sono CTA mutuamente esclusive per stato (per-sessione live-only vs night-level a line-up esaurita). Lo scenario esercita entrambe in sequenza sull'happy path. Se lo stato della game night non consente ancora `POST /complete` (es. sessione non ancora avviata), la conclusione serata non è disponibile → marcare `⚠️ blocked-env` sul solo passo finale.

### U5-12 [Smoke]: Riepilogo di una game night — KPI, MVP e recap partite

```gherkin
Scenario U5-12 [Smoke]: Riepilogo di una game night — KPI, MVP e recap partite
  Given sono loggato come marco@meepleai.test
    And esiste una game night con almeno una sessione (Completed dopo U5-11, o una game night seed con partite)
  When apro /game-nights/{id}/summary
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And il messaggio di loading lascia posto alla NightSummaryView
    And vedo la griglia KPI (KPIStatGrid) + eventuale MVP + il recap per-gioco (PerGameRecapRow)
    And vedo la sezione galleria foto (GameNightPhotoGallery), anche vuota (empty-state legittimo)
  Osservabile ✅: NightSummaryView renderizzata (header serata + KPI grid) · almeno una riga recap partita (o empty-state coerente se 0 partite) · sezione galleria foto presente · GET /api/v1/game-nights/{id}/summary = 200 (Network) · nessun errore Console/Network
  Route: /game-nights/[id]/summary
  Utente: marco
```

### U5-13 [Flow]: Genera il link di condivisione del riepilogo

```gherkin
Scenario U5-13 [Flow]: Genera il link di condivisione del riepilogo
  Given sono loggato come marco@meepleai.test (organizzatore della game night)
    And apro il riepilogo /game-nights/{id}/summary di una serata di cui marco è organizzatore
  When clicco l'azione "Condividi" (onShare) nella NightSummaryView
  Then viene generato un token di condivisione (POST /api/v1/game-nights/{id}/share-token → 200 con shareToken)
    And l'URL pubblico {origin}/game-nights/shared/{shareToken} viene copiato negli appunti
    And compare il toast di conferma copia (ShareSuccessToast, gameNightDetail.summary.shareCopied)
  Osservabile ✅: pulsante Condividi visibile (solo organizer) · POST /api/v1/game-nights/{id}/share-token = 200 (Network) · toast ShareSuccessToast "link copiato" visibile · nessun errore Console/Network
  Route: /game-nights/[id]/summary
  Utente: marco
```

### U5-14 [Smoke]: Pagina pubblica del riepilogo condiviso via token

```gherkin
Scenario U5-14 [Smoke]: Pagina pubblica del riepilogo condiviso via token
  Given ho un token di condivisione valido di una game night (generato in U5-13)
    And apro il browser SENZA sessione autenticata (contesto pubblico/anonimo)
  When navigo su /game-nights/shared/{token}
  Then la pagina carica senza errori 4xx/5xx né errori JS
    And il possesso del token autorizza la lettura (GET /api/v1/game-nights/shared/{token}/summary → 200)
    And vedo il riepilogo read-only (NightSummaryView) con KPI + recap partite
    And NON sono presenti azioni Condividi/Archivia/naviga-lista (vista sola lettura)
    And la galleria foto compare solo se ci sono foto (altrimenti assente, legittimo)
  Osservabile ✅: NightSummaryView read-only renderizzata senza login · GET /api/v1/game-nights/shared/{token}/summary = 200 (Network) · nessun pulsante Condividi/Archivia a schermo · nessun errore Console/Network
  Route: /game-nights/shared/[token]
  Utente: anonimo (nessuna sessione)
```

### U5-15 [Flow]: Ciclo CRUD game night — crea → edita → annulla, con reload di persistenza

> Ciclo di vita completo sull'entità **game night** (spec §3.1, pattern `_TEMPLATE.md` § "Pattern ciclo CRUD"). Verifica la **persistenza reale** dopo ogni mutazione con un **reload** della pagina, non il solo feedback ottimistico. Opera esclusivamente su una game night marcata `HP-TEST-<data>` creata dallo scenario stesso. **Nota chiave**: non esiste hard-delete — la fase "Delete" è realizzata dal **cancel** (soft-delete `status → Cancelled`), quindi l'osservabile di persistenza del delete è "la serata resta in lista ma è **Annullata**", non "sparisce dalla lista" (vedi § Nota CRUD).

```gherkin
Scenario U5-15 [Flow]: Ciclo CRUD game night — crea → edita → annulla, con reload di persistenza
  Given sono loggato come marco@meepleai.test (premium, verificato)
    And marco ha giochi in libreria (seed: Azul, Catan, Wingspan…) e sara è un giocatore taggabile

  # ── CREATE ──────────────────────────────────────────────────────────
  When apro /game-nights/new
    And digito il titolo "HP-TEST-2026-07-10 Ciclo CRUD" nel campo (data-slot="game-night-create-title-input")
    And allo step 1 (Quando) scelgo una data/ora futura (≥ oggi+1h)
    And avanzo allo step 2 (Dove) e scelgo un tipo location (es. "Casa mia")
    And avanzo allo step 3 (Chi) e taggo sara come partecipante
    And avanzo allo step 4 (Cosa) e seleziono "Azul"
    And clicco il submit finale ("Crea"/nav.submit)
  Then la creazione va a buon fine (POST /api/v1/game-nights → 201 con id) e vengo reindirizzato a /game-nights/{nuovoId}
    And il dettaglio mostra il titolo "HP-TEST-2026-07-10 Ciclo CRUD" e lo stato "Bozza" (creata come Draft, PreInvite)
    And RICARICO /game-nights/{nuovoId}: la serata è ancora presente col titolo HP-TEST e stato "Bozza" (persistita nel backend)

  # ── EDIT (su Draft) ─────────────────────────────────────────────────
  When essendo host su una Draft clicco "Modifica" (?action=edit) e si apre il GameNightEditDrawer
    And nel form cambio il campo Titolo in "HP-TEST-2026-07-10 Ciclo CRUD (modificata)" e/o il campo Luogo
    And clicco "Salva modifiche"
  Then l'update va a buon fine (PUT /api/v1/game-nights/{id} → 200/204)
    And compare il toast "Serata aggiornata" (gameNightDetail.editDrawer.savedToast) e il drawer si chiude (URL torna a /game-nights/{id})
    And l'hero riflette il titolo/luogo aggiornato a schermo
    And RICARICO /game-nights/{id}: il nuovo titolo/luogo persiste (valore modificato ancora presente dopo reload)

  # ── PUBLISH (transizione richiesta per abilitare l'annulla) ──────────
  When clicco "Pubblica" (data-testid="publish-game-night")
  Then la pubblicazione va a buon fine (POST /api/v1/game-nights/{id}/publish → 200/204) e lo stato passa a Published (l'hero non mostra più "Bozza")

  # ── DELETE = ANNULLA (su Published) ─────────────────────────────────
  When clicco "Annulla serata" (pulsante destructive, icona XCircle — visibile solo su Published)
  Then l'annullamento va a buon fine (POST /api/v1/game-nights/{id}/cancel → 200/204)
    And compare il toast "Serata annullata" (gameNightDetail.actor.host.cancelledToast)
    And il dettaglio mostra il GameNightCancelledBanner ("Serata cancellata") e lo stato diventa "Annullata"
    And RICARICO /game-nights/{id}: lo stato "Annullata" + banner persistono (cancel persistito, soft-delete)
    And apro /game-nights (lista): la serata HP-TEST compare ancora ma con la status-pill "Annullata" (NON rimossa — è soft-delete, non hard-delete)
  Osservabile ✅:
    - CREATE: redirect /game-nights/{id} + titolo HP-TEST + stato "Bozza" · post-reload ancora presente · POST /game-nights = 201 (Network)
    - EDIT: toast "Serata aggiornata" + titolo/luogo aggiornato nell'hero · post-reload il nuovo valore persiste · PUT /game-nights/{id} = 2xx (Network)
    - PUBLISH: POST /game-nights/{id}/publish = 2xx · hero non più "Bozza"
    - DELETE/ANNULLA: toast "Serata annullata" + banner "Serata cancellata" + stato "Annullata" · post-reload stato "Annullata" persiste · in lista la serata resta presente con pill "Annullata" · POST /game-nights/{id}/cancel = 2xx (Network)
    - nessun errore Console/Network in tutto il ciclo
  Route: /game-nights/new → /game-nights/[id] → /game-nights (lista)
  Utente: marco
  Dati creati: "HP-TEST-2026-07-10 Ciclo CRUD" (poi "… (modificata)") → lasciata in stato Cancelled a fine ciclo (soft-delete; nessun hard-delete disponibile)
```

> **Nota concorrenza `xmin` (ADR-060 / #2703)**: l'update PUT è protetto da optimistic-concurrency. Sull'happy path a singola sessione (nessun edit concorrente) il salvataggio non genera 409 `concurrent_edit`. Se comparisse il toast "Modifiche aggiornate" (`gameNightDetail.errors.concurrentEdit`), lo stato del dato è divergente per interferenza esterna → marcare il solo passo Edit come `⚠️ blocked-env`, non `fail`.
>
> **Nota assenza hard-delete e invitato**: il ciclo non copre "rimozione fisica" (non esiste in UI) né "modifica/rimozione invitato" (non esposta post-creazione — vedi § Nota CRUD). Sono operazioni assenti, non omesse: non vanno inventate.

---

## Auto-verifica (author checklist)

- **Copertura route**: tutte le 6 route U5 della mappa (`game-nights`, `game-nights/new`, `game-nights/[id]`, `game-nights/[id]/live`, `game-nights/[id]/summary`, `game-nights/shared/[token]`) compaiono nella matrice con ≥1 scenario. `join/event/[code]` referenziato ma correttamente rinviato a U6 (nessuna duplicazione). ✅
- **Osservabili**: ogni scenario (U5-01 … U5-15) dichiara ≥1 `Osservabile ✅` strutturale (elemento/testid/URL/Network status), nessuna asserzione su testo letterale generato da LLM (non applicabile in U5). ✅
- **Solo happy path**: nessuno scenario negativo/errore/edge. I rami bloccati (409 max-live, RSVP rifiutato 409/410, errori auth 401/403, 409 `concurrent_edit`) sono citati solo come note fuori scope. ✅
- **Copertura CRUD & persistenza (spec §3.1)**: l'entità **game night** ha il ciclo di vita completo in **U5-15** (Create → Edit → Delete/Annulla), ognuna con **reload di verifica** della persistenza. Le operazioni realmente esposte in UI (create/edit/cancel) sono tutte coperte; l'assenza di hard-delete e di modifica-invitato è annotata esplicitamente (§ Nota CRUD), non inventata. Il "Delete" è il **cancel** soft (persistenza verificata come "presente ma Annullata", non "assente"). ✅
- **Dati marcati**: le entità create (U5-02, U5-15 e a valle) usano il prefisso `HP-TEST-<data>`. Il `Delete` di U5-15 opera solo sulla game night HP-TEST creata dallo scenario (mai su dati seed). Gli scenari read/smoke riusano dati seed additivi senza distruggerli. ✅
- **Conteggio scenari**: 15 scenari — Flow: U5-02, U5-03, U5-04, U5-05, U5-06, U5-08, U5-10, U5-11, U5-13, U5-15 (10). Smoke: U5-01, U5-07, U5-09, U5-12, U5-14 (5). ✅
```