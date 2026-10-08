# Classificazione degli orfani `data-testid` nella suite E2E

**Data**: 2026-10-08 · **Origine**: bonifica guidata dalla baseline del gate [#4120](https://github.com/meepleAi-app/meepleai-monorepo/issues/4120) · **Stato**: audit, decisione non presa

> **Convenzione**: ogni misura porta il comando che la riproduce. I numeri sono esiti del passaggio del 2026-10-08 su `feature/issue-4120-orphan-testid-gate`, non affermazioni sul presente.

---

## 1. Perché questo documento esiste

Dopo la prima fetta di bonifica (le schede, −137 sedi) il passo ovvio sarebbe continuare a rinominare selettori. **Non lo è**, e il motivo è il reperto di questo audit: gli orfani residui appartengono a categorie che vogliono trattamenti opposti — due all apertura di questo audit, cinque alla fine, e trattarli allo stesso modo trasformerebbe test morti in test sottilmente sbagliati.

Far risolvere un selettore non rende significativo un test scritto contro una UI immaginata. Lo rende eseguibile — e quindi rosso, o peggio verde per il motivo sbagliato.

## 2. Le categorie (due all apertura, cinque alla fine — vedi §3-ter e seguenti)

### A — Deriva di strumentazione: la funzione esiste, l'hook no

L'elemento è renderizzato, ma nessuno gli ha mai messo un `data-testid`, oppure ne porta uno diverso. È il caso che la prima fetta ha chiuso: `MeepleCard` esponeva `'data-testid'` nel contratto e tutte le varianti lo propagavano, ma **nessun chiamante lo passava**.

Esempi verificati come esistenti:

| cluster | componente | testid dichiarati |
|---|---|---|
| ~~citazioni in chat~~ | ~~`features/game-chat/CitationChip.tsx`~~ | ❌ **componente sbagliato, e non e` categoria A**: vedi §3-ter |
| preview PDF (`current-page` 11, `zoom-in`/`zoom-out`/`zoom-level` 7+6+7) | i componenti esistono | nessuno fra quelli cercati |
| slot agente (`slot-card` 10) | i componenti esistono | nessuno fra quelli cercati |
| banner offline (`offline-banner` 12) | `features/gamebook/OfflineBanner.tsx` | nessun `data-testid`, ma **`data-slot="offline-banner"`** — risolto senza toccare la produzione, vedi §3-bis |

**Trattamento ammesso**: aggiungere un `data-testid` a un elemento **già renderizzato**, come fatto per `MeepleCard`. Mai introdurre UI per far passare un'asserzione.

### B — Funzione assente: la spec descrive una UI che non c'è

Qui il selettore non è il problema: è il sintomo. La spec è non eseguibile per costruzione, e lo resterebbe anche con il selettore giusto.

**Caso dimostrato — `e2e/admin/shared-games-bulk-import.spec.ts` (54 sedi, il secondo file più contaminato):**

```bash
# la rotta che visita
grep -n "page.goto" apps/web/e2e/admin/shared-games-bulk-import.spec.ts
#   /admin/games/import/bulk  →  redirige a /admin/content?tab=games&section=import

# i testid che cerca, contro il codice di produzione
for t in bulk-import-uploader json-textarea bulk-import-preview total-count valid-count \
         duplicate-count preview-table confirm-import bulk-import-results; do
  printf '%-24s %s\n' "$t" "$(grep -rl "data-testid=\"$t\"" apps/web/src --include=*.tsx | grep -v __tests__ | wc -l)"
done
# tutti 0
```

Nessun componente di bulk import esiste: `grep -rln "bulk-import\|BulkImport" apps/web/src` restituisce un dialog di import BGG, una storia di glossario e dei client API — niente con textarea JSON, uploader, tabella di preview e conteggi.

**Altro caso**: `dev-panel` (15 sedi) e `dev-badge` (6) — `grep -rln DevPanel apps/web/src` → **nessun file**.

**Trattamento**: skip con motivo classificato (`DIFETTO:` o `PREVISTO:` secondo il gate `SkipReasonClassArchitectureTests`) più una issue che registri l'intento, oppure eliminazione con l'intento registrato — il precedente è lo scaffold #4068, eliminato in #4106.

## 3. Un primo taglio misurabile: le rotte

Una spec le cui rotte non esistono **né come pagina né come sorgente di redirect** è quasi certamente di categoria B.

```bash
# il classificatore vive nello scratchpad della sessione; la misura è riproducibile
# confrontando i `page.goto` di ogni spec con le rotte di src/app e le `source:` di next.config.js
```

| sedi orfane | rotte visitate | assenti | file |
|---|---|---|---|
| 61 | 1 | **1** | `e2e/editor/dashboard.spec.ts` → `/editor/dashboard` |
| 33 | 8 | 3 | `e2e/epic-2-agent-system.spec.ts` → `/library/games/azul`, `/admin/strategies/create`, `/library/games/simple-game` |
| 15 | 1 | **1** | `e2e/collection-dashboard.spec.ts` → `/dashboard/collection` |
| 14 | 3 | **3** | `e2e/visual-docs/editor-flows/content-management.visual.spec.ts` → `/admin/games/game-1/{faq,quick-questions,errata}` |
| 14 | 1 | **1** | `e2e/visual-docs/editor-flows/document-management.visual.spec.ts` → `/admin/games/game-1/documents` |
| 12 | 5 | 1 | `e2e/epic-4-pdf-status.spec.ts` → `/admin/processing-queue` |

**145 sedi su 1103** stanno in spec le cui rotte sono **tutte** assenti.

### ⚠️ È un limite inferiore, non la misura

Due ragioni, e la seconda è dimostrata:

1. **Il classificatore ignora i redirect solo se li riconosce.** La prima stesura dava 261 invece di 145, perché non confrontava le URL con le `source:` di `next.config.js` — e i redirect Next precedono il routing, quindi `/agent/slots` atterra benissimo su `?tab=slots`. Corretto.
2. **Una rotta che risolve non garantisce che la funzione ci sia.** `shared-games-bulk-import` ha rotta valida (via redirect) e funzione inesistente: il classificatore la segna `0 assenti`, e sbaglia. Il controllo sulle rotte non sostituisce il controllo sui componenti.

Nota a margine: `e2e/epic-2-agent-system.spec.ts` visita `/library/games/azul`, cioè la **forma nuda** di #4105 — quella che risponde 404 di proposito. È una delle sedi che la decisione di #4105 lascia da correggere.

## 3-bis. Una terza via: il repository ha DUE convenzioni di selettore

Scoperto il 2026-10-08 mentre si strumentavano le citazioni: `CitationChip.tsx` non ha `data-testid` ma porta `data-slot="citation-chip"`.

| convenzione | file di produzione | valori distinti |
|---|---|---|
| `data-testid` | 696 | — |
| `data-slot` | 368 | **965** |

Alcune spec usano già la seconda (`e2e/a11y/*`). Quindi per un orfano `data-testid` con un `data-slot` omonimo **la correzione non tocca il codice di produzione**: basta puntare la spec all'attributo che c'è già.

**L'ipotesi è stata in gran parte smentita**: solo **5 dei 682** id orfani hanno un `data-slot` esattamente omonimo. Ma quei cinque valevano **22 sedi** e sono stati chiusi senza una riga di produzione:

`offline-banner` · `typing-indicator` · `achievement-card` · `chat-info-panel` · `game-detail-kb-doc-list`

Una trappola trovata convertendo: due asserzioni componevano `achievement-card` con `data-status`, che **non esiste** — l'attributo vero è `data-unlocked={unlocked || undefined}` (`AchievementsCarousel.tsx:132`). Convertire il solo nome avrebbe lasciato due selettori comunque morti.

`--list` ora segnala quando un orfano ha un `data-slot` omonimo, con l'avvertenza che `getByTestId()` non può puntarlo: serve `locator()`.

### Il buco speculare, misurato e non chiuso

Un `[data-slot="x"]` inesistente **non** è controllato dal gate. Misurato: 11 sedi, di cui solo 6 reali.

| id | sedi | stato |
|---|---|---|
| `mobile-body-tab` | 4 | slot **legacy rimosso**: due test unit asseriscono che sia assente (`MobileBody.test.tsx:92`, `SessionLiveView.test.tsx:769`), e `e2e/a11y/session-live.spec.ts` lo interroga ancora |
| `game-detail-tabs` | 2 | assente; gli slot reali sono `agent-detail-tabs`, `player-detail-tabs`, `shared-game-detail-tabs`, `toolkit-detail-tabs` |
| `...` | 3 | **falso positivo**: `data-slot="..."` citato dentro un commento |
| `test-banner-reconnecting`, `test-banner-failed` | 2 | **falsi positivi**: la spec se li inietta con `document.createElement` + `setAttribute` |

Allargare il gate costerebbe due esclusioni (commenti, DOM iniettato dalla spec) per sei sedi reali: per ora non le vale, ed è il limite 6 dichiarato nell'intestazione dello script.

> ⚠️ Nota di metodo: verificando `mobile-body-tab` ho prima concluso che lo script sbagliasse, perché un `grep` mostrava lo slot presente. Era il **grep** a sbagliare — includeva `__tests__/`. Le due occorrenze sono asserzioni di assenza in test unit. Una sonda di verifica va ristretta con gli stessi filtri dello strumento che verifica.

## 3-ter. ⚠️ Correzione a §2: le citazioni NON sono categoria A

Nella prima stesura di questo audit le citazioni erano l'esempio principale di categoria A, sulla base di «`features/game-chat/CitationChip.tsx` esiste». **Era il componente sbagliato.**

`e2e/chat-citations.spec.ts` guida `/chat`, cioè la chat **unificata**, dove le citazioni le rende `src/components/chat-unified/CitationBlock.tsx`. E quel componente implementa un design **diverso** da quello che la spec descrive:

| la spec assume | la realtà (`CitationBlock.tsx`) |
|---|---|
| `message-citations` > `citation-list` > `citation-card[]` (tre livelli) | un solo contenitore `citation-block` con chip indicizzate `citation-chip-${i}` |
| un header `📚 Fonti (2)` col conteggio | nessun header, nessun conteggio — `grep -rn "Fonti" src/components/chat-unified/` → nulla |
| card con testo `Pag. 10` | chip con testo **`Pagina {page}`** (`:56`) |
| un collassabile globale: `citations-header` apre/chiude `citations-content` | espansione **per singola chip** (`setExpanded(...)`, `citation-expanded-${i}`) |

Non è deriva di strumentazione: è un **design sostituito**. Far risolvere i selettori lascerebbe comunque rosse le asserzioni sul testo (`Pag. 10` vs `Pagina 10`) e il secondo test resterebbe intraducibile.

**Conseguenza sulla stima**: la categoria A è **più piccola di come sembrava**, e ogni cluster va verificato sul componente che la spec guida davvero — non sul primo componente dal nome simile. Era la stessa lezione del classificatore per rotte, ripetuta su un altro asse.

## 3-quater. 🔴 La categoria peggiore: verde e cieco

In `chat-citations.spec.ts` **due test su quattro passano per il motivo sbagliato**:

```ts
// e2e/chat-citations.spec.ts:111 e :127
await expect(userMessage.getByTestId('message-citations')).not.toBeVisible();
await expect(page.getByTestId('message-citations')).not.toBeVisible();
```

`message-citations` non esiste in produzione, quindi «non è visibile» è vero **per costruzione**. Quei due test passerebbero anche se i messaggi utente mostrassero citazioni: non misurano nulla, e sono **verdi**.

È peggio di un test rosso. Un rosso si vede; questo si conta fra i passati.

### Misura

Sedi orfane dentro un'asserzione negativa sulla stessa riga (`.not.`, `toHaveCount(0)`, `toBeNull()`): **11, in 7 file**.

| sedi | file |
|---|---|
| 5 | `e2e/editor/dashboard.spec.ts` |
| 1 ciascuno | `admin/admin-workflow-actions.spec.ts` · `agent/agent-rag-flow.spec.ts` · `auth/auth-complete.spec.ts` · `auth-registration-dashboard.spec.ts` · `chat-citations.spec.ts` · `pdf-indexing-flow.spec.ts` |

⚠️ **È una sottostima**, e il motivo è il paragrafo seguente: `message-citations` non compare in questa misura perché il gate lo classifica *indimostrabile*, non orfano.

## 3-quinquies. 🔴 Le false assoluzioni: 144 id invisibili

Il gate non accusa un id coperto da un pattern dinamico (limite 1). Ma quei pattern nascono dalla **testa** e dalla **coda** di un template, e alcuni sono genericissimi:

| id cercato | sedi | assolto da | il pattern nasce da |
|---|---|---|---|
| `message-citations` | 3 | `message-*` | un `data-testid={`message-${…}`}` che rende i **messaggi di chat**, non contenitori di citazioni |
| `chess-message-input` | 7 | `*-input` | qualunque template che finisca in `-input` |
| `game-title` | 6 | `*-title` | idem per `-title` |
| `quota-warning` | 5 | `quota-*` | — |
| `filter-chip-favorites` | 5 | `filter-chip-*`, `filter-*` | — |

L'assoluzione è **letteralmente corretta**: `message-${x}` *può* produrre `message-citations`. Ma è spesso implausibile, e finora era **silenziosa** — l'id non veniva né accusato né mostrato. Totale in quello stato: **144**.

`--list` ora stampa la sezione *Indimostrabili* con il pattern che assolve ciascun id, perché un'assoluzione che non si vede non si può contestare. Il verdetto del gate non cambia: resta non accusatorio, come deve essere per non venire disattivato.

> **Lezione di metodo, la terza di questo audit**: avevo blindato il gate contro le false *accuse* (la soppressione dei pattern dinamici) e non avevo guardato il lato opposto. Una misura conservativa non è neutra: sposta l'errore, non lo elimina. Chi la progetta deve rendere visibile anche ciò che assolve.

## 3-sexies. Tre convenzioni di indirizzamento, non una

Dopo `data-slot` (§3-bis) è emersa la terza: **`id`**. `RegisterForm.tsx` porta `id="register-email"` e `id="register-password"`, cioè esattamente i nomi che `e2e/auth-email-registration-flow.spec.ts` cerca come `data-testid`.

| convenzione | dichiarazioni letterali in produzione | orfani con un omonimo | sedi |
|---|---|---|---|
| `data-testid` | — (è quella che il gate verifica) | — | — |
| `data-slot` | 965 | 5 | 22 → **chiuse** |
| `id` | 355 | 12 | 29 |

`--list` segnala entrambe. Per un campo di form l'`id` è anche il selettore *migliore*: è quello a cui punta `<label for>`, quindi è legato all'accessibilità e non a una convenzione di test.

### ⚠️ Un suggerimento non è un permesso: la trappola della metrica

Gli `id` del grappolo auth **non** sono stati convertiti, e il motivo è il reperto più utile di questa sezione.

`auth-email-registration-flow.spec.ts` riempie email e password — che hanno un `id` omonimo — e poi fa:

```ts
const confirmPasswordInput = page.locator('[data-testid="register-confirm-password"]');
await expect(confirmPasswordInput).toBeVisible();
```

**`RegisterForm` non ha un campo di conferma password.** I suoi campi sono `email`, `password`, `honeypot`, `termsAccepted` (`grep -nE 'name="' RegisterForm.tsx`), e la spec non accetta mai i termini che il form richiede.

Quindi convertire `register-email` e `register-password` a `#id` avrebbe fatto **scendere 14 sedi dal gate lasciando la spec rotta**: il fallimento si sarebbe spostato di una riga, e il conteggio avrebbe detto che le cose vanno meglio. È truccare la metrica.

**Regola che ne segue**: prima si stabilisce che la spec *possa* passare, poi si ripuntano i selettori. L'ordine inverso compra un numero e perde l'informazione.

Il messaggio di `--list` porta questa avvertenza accanto al suggerimento, con questo caso come esempio.

### Nota su `auth-email-registration-flow.spec.ts`: non eliminata

Ha due test. Il primo (712 righe di flusso completo) non può passare per quanto sopra; **il secondo è indipendente** — va su `/dashboard` e verifica la trasmissione degli header, senza selettori orfani. Eliminare il file perderebbe un test funzionante, e il primo descrive una funzione che **esiste**: la registrazione c'è, è il modello di form della spec a essere sbagliato. Va riscritto contro il form reale, e quella è una decisione su come riscriverlo — non un'eliminazione.

## 3-septies. 🔴 La classe più grande, e non sono i selettori: 777 blocchi ciechi

Registrata in **#4127**. Classificando gli orfani per decidere cosa strumentare, `e2e/pdf-viewer-modal.spec.ts` sembrava un caso di «manca il testid `dialog`». Non lo era.

```ts
const citationList = page.getByTestId('citation-list');
if (await citationList.isVisible({ timeout: 10000 }).catch(() => false)) {
  … tutte le asserzioni del test …
} else {
  console.log('No citations returned from query');   // e il test PASSA
}
```

475 righe, 18 test, **36** blocchi di questa forma. Se l'elemento non c'è il corpo non gira, il test passa, e il `console.log` fa sembrare un salto legittimo per mancanza di dati. Il `.catch(() => false)` isola anche gli errori: un selettore malformato e un elemento assente diventano indistinguibili.

| misura (2026-10-08) | valore |
|---|---|
| blocchi condizionati sulla presenza di un elemento | **777** |
| file coinvolti | **121** |
| con un `.catch()` che ingoia gli errori | **292** |

⚠️ **777 è una superficie, non un conteggio di difetti.** Condizionare su un elemento davvero opzionale è legittimo. Il difetto è più stretto: quando il blocco contiene le **uniche** asserzioni del test, quel test non ha un esito «non applicabile» — ha un **verde**.

### Perché è più grande degli orfani, e li spiega

Il gate misura 952 sedi con selettori inesistenti: è il sottoinsieme **dimostrabile**. I blocchi ciechi non dipendono dalla validità del selettore — e un corpo condizionato non gira nemmeno quando l'elemento **esiste ma non è su quella pagina**.

Gli orfani sono in parte un **sintomo**: una spec il cui corpo non gira mai non ha modo di accorgersi che i suoi selettori sono morti.

## 3-octies. ⚠️ Correzione a §3-ter: `citation-list` esiste, altrove

Nella tabella di §3-ter avevo elencato `message-citations > citation-list > citation-card[]` come «struttura che la spec assume», implicando che nessuno dei tre esistesse. **`citation-list` è dichiarato**:

```
src/components/features/kb-globale/DrawerCompleted.tsx:67
  <ol className="space-y-1 mb-3" data-testid="citation-list">
```

È il drawer di **KB-globale**, non la chat. Quindi una spec che guida `/chat` non lo troverà mai — ma il gate non lo segnala, perché l'id *esiste*. È il **limite 2** dichiarato nell'intestazione dello script («verifica l'esistenza, non la raggiungibilità») con un'istanza concreta, e la prima che ne mostra il costo: è proprio quell'id a tenere sempre falso il condizionale di `pdf-viewer-modal.spec.ts`.

La conclusione di §3-ter **non cambia**: il design delle citazioni della chat è stato sostituito, e lo dicono gli altri quattro confronti (header `📚 Fonti (2)` assente, `Pag. N` contro `Pagina N`, un solo livello di contenitore, espansione per chip invece che globale). Ma il confronto va letto con questa correzione: uno dei tre nomi non era assente, era **in un'altra feature**.

> Lezione, la quarta di questo audit: avevo verificato due dei tre nomi e dedotto il terzo dalla forma della tabella. Una tabella con tre righe invita a trattarle come un blocco; vanno verificate una per una.

## 4. Cosa serve decidere, prima di continuare

- [ ] **Per la categoria B**: skip classificato o eliminazione? Esiste il precedente dell'eliminazione (#4068 → #4106) e quello dello skip con `DIFETTO:` (#4114). La scelta cambia cosa resta leggibile: un file eliminato lascia solo l'issue, uno skippato lascia la struttura
- [ ] **Per la categoria A**: fino a che punto è lecito strumentare? Aggiungere un testid a un elemento esistente è additivo, ma **rende eseguibile una spec che non ha mai girato** — e il resto delle sue asserzioni non è stato verificato contro la UI vera. Va deciso se la strumentazione arriva con la revisione della spec o prima
- [ ] **Chi classifica i cluster residui**: la tabella sopra copre i primi per peso; i rimanenti vanno passati con la stessa sonda (il componente esiste? i testid fratelli esistono?) prima di toccarli
- [ ] Le sedi di `/library/games/<id>` nuda in `epic-2-agent-system` vanno corrette insieme alle altre di #4105

## 5. Cosa NON serve decidere

Il gate #4120 è già in posizione e blocca il peggioramento, con la baseline per file come inventario. Nessuna di queste decisioni è un prerequisito per mergiare la PR del gate: la bonifica può procedere a fette, e ogni calo si registra con `pnpm lint:orphan-testids --update`.
