# Rotte `/library/games/*` legacy e deriva dei selettori E2E

**Data**: 2026-10-07 · **Origine**: brainstorming su [#4105](https://github.com/meepleAi-app/meepleai-monorepo/issues/4105) · **Stato**: brief, pronto per implementazione

> **Convenzione di questo documento**: dove un conteggio cambierebbe da solo, il documento enumera per **nome** e riporta il comando che rimisura. Nessun totale in prosa.

---

## 1. Da cosa è partito, e perché è cambiato

#4105 chiedeva se il 404 su `/library/games/<id>` fosse accettabile. La risposta è **sì**, e la decisione è chiusa (§2). Ma la verifica ha trovato un difetto adiacente e più grave di quello cercato: **i redirect dei sotto-percorsi — che #1004 aveva esplicitamente conservato come la parte sicura — puntano a id di tab che il validatore rifiuta**, e il risultato è un ritorno silenzioso al tab Info (§3).

È lo stesso modo di guasto che motivava #1004 («silently falls back to a CTA when an unknown `?tab=aiChat` arrives»), sopravvissuto nel punto che quel commento dichiarava di proteggere.

---

## 2. Decisione su #4105: il 404 resta

### Evidenza

| fatto | misura |
|---|---|
| Nessun codice di produzione genera link di **pagina** alla forma vecchia | `grep -rnE "library/games/" apps/web/src --include=*.tsx --include=*.ts \| grep -v "__tests__\|\.test\.\|\.stories\."` → tutte le occorrenze sono il namespace **API** `/api/v1/library/games/{gameId}` |
| Non esiste un ambiente di produzione | nessun segnalibro utente possibile sulla forma vecchia |
| La forma vecchia è stata pubblica fino a `f3702921d` (PR #1037, *closes #871*) | `git log --all -- "apps/web/src/app/**/library/games/**"` |

Il redirect non avrebbe nessun beneficiario: né utenti (non c'è produzione), né codice interno (usa il namespace API, non la rotta di pagina).

### Conseguenze

- La conclusione di #1004 («do not add a catch-all») **resta valida, per un motivo diverso da quello scritto**. La premessa citata (il layout a 4 tab) è caduta; la conclusione sopravvive perché il redirect non serve a nessuno.
- Il commento di `next.config.js` va corretto su **due** punti: la premessa caduta, e l'affermazione «Keep sub-route redirects above (they map specific tab paths)» — che §3 dimostra falsa.
- `apps/web/src/app/(authenticated)/library/[gameId]/layout.tsx:3` rinvia a «Issue #5042», che non esiste nel repository (`gh issue view 5042` → *Could not resolve*). Va corretto a #2158.

---

## 3. 🔴 Il difetto vero: i redirect legacy aprono il tab sbagliato, in silenzio

> **✅ Stato 2026-10-07 — risolto** sul ramo `fix/legacy-tab-redirects`. Le nove destinazioni sono valide, e `apps/web/src/__tests__/next-config-tab-redirects.test.ts` lega ogni `?tab=` prodotto da `next.config.js` a `isGameTabId`. Verificato **per perturbazione in due direzioni**: id invalido nella config → rosso; rinomina di un id in `types.ts` → rosso. Il commento di #1004 e l'header di `layout.tsx` sono corretti nello stesso ramo.
>
> **Reperto collaterale, non risolto**: `#5042` non è un caso isolato. `#5001`, `#5003`, `#5004`, `#5005`, `#5023`, `#5024`, `#5026`–`#5029`, `#5039`, `#5055` e altri compaiono nel codice e **nessuno risolve in questo repository** — `grep -rhoE '#50[0-9]{2}' apps/web/src apps/web/next.config.js | sort -u` li elenca. Sono una numerazione venuta da fuori. L'header di `layout.tsx` ora lo dichiara invece di far inseguire un numero morto; la famiglia merita un trattamento proprio.

### Meccanismo

`apps/web/src/app/(authenticated)/library/[gameId]/page.tsx:70`:

```ts
const initialTab: GameTabId = isGameTabId(tabParam) ? tabParam : 'info';
```

`isGameTabId` (`components/game-detail/tabs/types.ts:68-78`) accetta **solo** gli id di `GAME_TABS`: `info`, `aiChat`, `toolbox`, `houseRules`, `partite`. Qualunque altro valore cade su `info` senza alcun segnale.

### Stato dei redirect in `apps/web/next.config.js`

| sorgente | `?tab=` di destinazione | valido? | esito osservabile |
|---|---|---|---|
| `/library/games/:id/agent` (`:170`) | `agent` | ❌ | apre **Info** |
| `/library/games/:id/toolkit` (`:175`) | `toolkit` | ❌ (valido: `toolbox`) | apre **Info** |
| `/library/games/:id/faqs` (`:179`) | `faq` | ❌ | apre **Info** |
| `/library/games/:id/reviews` (`:181`) | `reviews` | ❌ | apre **Info** |
| `/library/games/:id/rules` (`:186`) | `rules` | ❌ | apre **Info** |
| `/library/games/:id/sessions` (`:191`) | `sessions` | ❌ (valido: `partite`) | apre **Info** |
| `/library/games/:id/strategies` (`:196`) | `strategies` | ❌ | apre **Info** |
| `/games/:id/knowledge-base` (`:322`) · `/games/:id/agents` (`:325`) | `agent` | ❌ | apre **Info** |
| `/library/games/:id/play*` (`:206-217`) | — rotte reali, non tab | ✅ | corretto |

Comando che rienumera sorgenti e destinazioni:

```bash
grep -nE "source: '/library/games|source: '/games/:id/agent|destination: '/library/" apps/web/next.config.js
```

### Nota sul perché è sfuggito

`types.ts:51-54` dichiara gli id stabili e nomina i consumatori da non rompere: «*`?tab=aiChat`/`?tab=toolbox` deep-links + legacy redirects in `agent/page.tsx` + `toolbox/page.tsx`*». Quei due **sono** corretti — `library/[gameId]/agent/page.tsx:20` fa `redirect('/library/${gameId}?tab=aiChat')`. I redirect di `next.config.js` non sono nell'elenco e non sono stati aggiornati. Due livelli di redirect, uno allineato e uno fossile — e i redirect Next **precedono** il routing delle pagine, che è l'avvertimento scritto nel commento di #1004.

### Cosa serve

- [ ] Rimappare i tre che hanno un tab corrispondente: `agent` → `aiChat`, `toolkit` → `toolbox`, `sessions` → `partite`
- [ ] Decidere per i quattro che **non** hanno alcun tab nella superficie a 5 (`faq`, `reviews`, `rules`, `strategies`): puntarli a `/library/:id` senza `?tab=` (stesso esito di oggi, ma dichiarato) oppure rimuovere il redirect. Scegliere **non** lasciarli puntare a un id inesistente, che è il difetto
- [ ] Un test che leghi le destinazioni dei redirect al validatore: ogni `?tab=` prodotto da `next.config.js` deve soddisfare `isGameTabId`. Senza questo legame la deriva si ripete — è già successa una volta in silenzio
- [ ] Correggere il commento di #1004 (premessa caduta **e** l'affermazione sui sotto-percorsi) e l'`Issue #5042` di `layout.tsx:3`

---

## 4. Le sei spec E2E: deriva di selettori, non funzioni assenti

Spec che navigano alla forma **nuda** (`page.goto('/library/games/<id>?session=…')` o `waitForURL`):

`e2e/agent/responsive.spec.ts` · `e2e/agent/quota-warning.spec.ts` · `e2e/agent/chat-streaming.spec.ts` · `e2e/agent-chat.spec.ts` · `e2e/epic-2-agent-system.spec.ts` · `e2e/dashboard-user-journey.spec.ts`

```bash
grep -rnE "library/games/[^/'\"\`]*['\"\`]" apps/web/e2e | grep -vE "api/v1"
```

Sono **raccolte** da Playwright (`testDir: './e2e'`, `testMatch: '**/*.spec.ts'`, `testIgnore: ['**/audit/**']`) e i corpi dei test asseriscono senza condizioni, quindi non possono passare contro un 404. Che stiano fallendo adesso non è osservabile dalle PR verso `main-dev`: il gate E2E non gira su quel target e porta fallimenti preesistenti. Serve un run esplicito **più uno di controllo** su `main-dev` intatto.

### La superficie reale

```
/library/<id>?tab=aiChat
  → GameTabsPanel.tsx:201  (activeTab === 'aiChat')
  → GameAiChatTab.tsx      (tabpanel, inline — NON uno sheet)
  → GameChatTab            (components/features/game-chat)
```

### Mappatura della deriva

| la spec cerca | realtà | verdetto |
|---|---|---|
| `[data-testid="chat-input"]` | `message-input` (`ChatInputBar.tsx`) | rinomina |
| `[data-testid="send-message-button"]` | esiste (`ChatInputBar.tsx:65`) | nessun cambiamento |
| `[data-testid="chat-message"][data-type="user"]` | `chat-bubble` + **`data-role`** (`ChatBubble.tsx:34-35`) | rinomina di testid **e** di attributo |
| `[data-testid="typing-indicator"]` | `typing-dot` (`TypingIndicator.tsx:38`); il wrapper non ha testid | rinomina — o aggiungere un testid al wrapper, che è l'unico hook mancante su un elemento esistente |
| `[data-testid="agent-chat-sheet"]`, `[data-testid="open-chat-button"]` | assenti **per architettura**: la chat è inline in un tab, non uno sheet da aprire | il `beforeEach` va riscritto, non riparato |
| `[data-testid="streaming-cursor"]` | assente; **in `chat-streaming.spec.ts` il locator è dichiarato e mai asserito** | rimuovere il locator morto. Non introdurre un cursore di streaming per compiacere un'asserzione che non esiste |

Comando che rimisura la mappatura:

```bash
for t in agent-chat-sheet open-chat-button chat-input send-message-button chat-message typing-indicator streaming-cursor; do
  printf '%-22s %s\n' "$t" "$(grep -rl "data-testid=\"$t\"" apps/web/src | wc -l)"
done
grep -rhoE 'data-testid="[^"]+"' apps/web/src/components/features/game-chat/ | sort -u
```

### Vincolo sulla riscrittura

**Nessuna UI nuova per far passare un test.** Gli elementi esistono con nomi diversi: la riscrittura è una rimappatura di selettori più la sostituzione della URL. L'unica aggiunta ammessa al codice di produzione è un `data-testid` su un elemento **già renderizzato** e privo di hook stabile (il wrapper di `TypingIndicator` è il solo candidato individuato). `streaming-cursor` e la coppia sheet/open-button **non** vanno implementati: il primo è un locator morto, la seconda descrive un'architettura che non è quella scelta.

### Cosa serve

- [ ] URL: `/library/<id>?tab=aiChat`, non `/library/games/<id>?session=…`. Nota che **`?session=` non è letto da nessuno** sotto `library/[gameId]` (`grep -rn "get('session')" "apps/web/src/app/(authenticated)/library/"` → nessun risultato): qualunque cosa quelle spec intendessero preparare con quel parametro va ottenuta per altra via, o l'intento va dichiarato caduto
- [ ] Selettori rimappati secondo la tabella sopra
- [ ] `beforeEach` riscritto: la chat è inline, non c'è niente da aprire
- [ ] Locator morti rimossi, non implementati
- [ ] `dashboard-user-journey.spec.ts:317` asserisce `waitForURL('**/library/games/game-1')`, cioè si aspetta che **l'app** navighi alla forma vecchia. Nessun codice di produzione lo fa (§2): l'asserzione va corretta alla rotta canonica
- [ ] I blocchi `if (await x.isVisible())` nei `beforeEach` di `responsive`, `quota-warning` e `chat-streaming` vanno via con la riscrittura: nascondono il fallimento invece di misurare

---

## 5. Il gate sui testid orfani

Tre istanze della stessa malformazione in due giorni — lo scaffold #4068 eliminato in #4106, le sei spec di §4, e `meeple-card` in #4110 — e tutte e tre erano invisibili fino a un grep manuale. Un selettore che non esiste rende un test **non eseguibile per costruzione**, e il costo non è il test perso: è il tempo di chi prova a riattivarlo credendo che sia da aggiustare.

### Forma del gate

- [ ] Estrarre i `data-testid` **cercati** da `apps/web/e2e/**` (tutte le forme: `[data-testid="x"]`, `getByTestId('x')`, selettori composti con attributi aggiuntivi)
- [ ] Estrarre i `data-testid` **dichiarati** in `apps/web/src/**`, escludendo `__tests__/**` — l'unica occorrenza di `meeple-card` era un mock Vitest, e contarla avrebbe mascherato il difetto
- [ ] Il gate fallisce quando un testid cercato non è dichiarato. Deve girare in un workflow che si attiva sulle PR verso `main-dev` (`dev-fast.yml`), **non** in `backend-e2e-tests.yml`, che su quel target non gira
- [ ] Prima dell'attivazione: misurare la baseline. Se l'elenco degli orfani non è vuoto — e §4 dice che non lo è — il gate va introdotto insieme alla bonifica, oppure con una lista di eccezioni **nominata e con la issue che la chiude**, come fa l'elenco `Exempt` di `SkipReasonClassArchitectureTests`
- [ ] Verificare il gate **per perturbazione**: aggiungere un testid inesistente a una spec e constatare che il gate diventa rosso. Un gate che non si è visto fallire non è un gate

---

## 6. Ordine proposto

| # | lavoro | dipende da | perché in questo posto |
|---|---|---|---|
| 1 | §3 — redirect con tab id validi + test di legame | — | è l'unico difetto che un utente può incontrare oggi |
| 2 | §2 — commenti corretti, #4105 chiusa | 1 (il commento deve dire la verità sui sotto-percorsi) | chiude la decisione |
| 3 | §5 — gate sui testid, con la baseline misurata | — | dà la lista esatta di ciò che §4 deve sistemare |
| 4 | §4 — riscrittura delle sei spec | 3 (per la lista), 1 (per la URL con il tab giusto) | la più grossa, e l'unica che va verificata con un run E2E più il controllo |

§3 e §5 sono indipendenti e possono procedere in parallelo.
