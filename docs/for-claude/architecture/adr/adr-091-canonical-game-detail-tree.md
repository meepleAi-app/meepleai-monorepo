# ADR-091 — Albero canonico del dettaglio gioco: `/games/[id]`

**Date**: 2026-09-07
**Status**: Accepted — decisione del committente, presa nell'ambito dell'Epic #3916. Questo ADR la registra, ne misura l'impatto e fissa i vincoli di esecuzione.
**Issue**: #3925 (UX-06a). Esecuzione in #3927 (spostamento route), #3928 (codemod link), #3929 (redirect).
**Related**: #3916 (Epic web UI/UX redesign) · #3934 (spike sorgente regolamento, che aggiunge la decisione §6) · #3938 (stessa classe di problema su Settings) · piano `docs/for-developers/audits/2026-09-07-web-ux-glass-redesign-plan.md` §M.1.

---

## Context

Due alberi di route descrivono **la stessa entità**:

| Albero | Route | Modello di navigazione interna |
|---|---|---|
| `/games/[id]` | index, `card`, `faqs`, `rules`, `sessions` — **5** | **sotto-route** |
| `/library/[gameId]` | index, `agent`, `kb`, `play`, `play/[campaignId]`, `play/[campaignId]/encounter`, `play/[campaignId]/translate`, `toolbox`, `toolkit`, `toolkit/[sessionId]` — **10** | **query param** (`?tab=agent`) |

Le CTA saltano fra i due: `/games/[id]/rules` ha un back-link verso `/library/[gameId]`.

Esiste già una regola ESLint dedicata, `local/no-game-detail-orphan-routes`: il problema è **noto e presidiato, non risolto**.

### Una migrazione inversa già avvenuta, il cui razionale non è recuperabile

`apps/web/src/config/entity-navigation.ts:67-71` documenta che il dettaglio gioco **era** su `/games/[gameId]` ed è stato spostato:

```
* URLs updated for Epic #5033 route consolidation (Issue #5055):
* - Game detail pages → /library/[gameId] (was /games/[gameId])
* - KB / Agent views → /library/[gameId]?tab=agent (was /games/[gameId]/knowledge-base)
```

**#5033, #5055 e #4889 non esistono su GitHub** (la issue più alta del repository è #3948): appartengono a un sistema di tracking precedente. Il razionale di quella consolidazione **non è recuperabile dalle issue**.

Questa decisione inverte quindi una scelta passata senza poterne leggere le motivazioni. È un rischio reale, registrato in §Rischi, e non un dettaglio: se quella migrazione correggeva un problema concreto, questa lo reintroduce.

---

## Decision

### 1. `/games/[id]` è l'albero canonico del dettaglio gioco

Il dettaglio di un gioco appartiene al **gioco**, non alla collezione di chi lo possiede. `/library` risponde a «cosa ho», `/games` a «cos'è»; la scheda di un gioco è la seconda domanda. Un gioco visto dal catalogo e lo stesso gioco visto dalla propria libreria non devono essere due pagine.

### 2. `/library` resta, come indice della collezione personale

Si sposta **solo il dettaglio**. `/library`, `/library/private`, `/library/wishlist` non si toccano.

### 3. Le 10 sotto-route si spostano sotto `/games/[id]`

```
/library/[gameId]                                → /games/[id]
/library/[gameId]/agent                          → /games/[id]/agent
/library/[gameId]/kb                             → /games/[id]/kb
/library/[gameId]/play                           → /games/[id]/play
/library/[gameId]/play/[campaignId]              → /games/[id]/play/[campaignId]
/library/[gameId]/play/[campaignId]/encounter    → …/encounter
/library/[gameId]/play/[campaignId]/translate    → …/translate
/library/[gameId]/toolbox                        → /games/[id]/toolbox
/library/[gameId]/toolkit                        → /games/[id]/toolkit
/library/[gameId]/toolkit/[sessionId]            → /games/[id]/toolkit/[sessionId]
```

Il segmento dinamico si uniforma su **`[id]`**, quello dell'albero canonico. È un cambio di tipo visibile a `tsc`: il typecheck verde è il segnale che la migrazione è completa.

### 4. Il modello di navigazione interna è **sotto-route**, non query param

I due alberi usano modelli diversi: `/games/[id]/faqs` contro `/library/[gameId]?tab=agent`. Vince il **sotto-route**, per tre ragioni:

1. è già il modello dell'albero canonico;
2. le 10 route in migrazione sono già sotto-route sul filesystem — è `entity-navigation.ts` a costruire href con `?tab=`;
3. una sotto-route ha un `layout.tsx` proprio e uno stato di caricamento proprio; un tab in query string no.

`?tab=` resta legittimo dove distingue **viste della stessa risorsa** senza dati propri.

> Nota: la stessa domanda si ripresenta su `/profile?tab=settings` in #3938. Le due decisioni vanno prese con lo stesso criterio, ma restano indipendenti.

### 5. Redirect di cortesia, non 301 permanenti

**L'app non è distribuita**: nessun utente reale, nessun link esterno, nessun ranking da preservare. Bastano redirect che coprano i 10 pattern preservando i segmenti dinamici. L'ADR non fissa una data di rimozione: la fissi #3929 se lo ritiene.

### 6. Il lettore del regolamento non è una sotto-route nuova

Aggiunta dopo lo spike #3934, che ha stabilito un fatto rilevante: **esiste già un lettore di documenti funzionante** a `/knowledge-base/[id]` — due colonne, ricerca full-text, superficie utente — mentre `/games/[id]/rules` mostra `RuleSpec.atoms`, cioè le regole estratte, non il regolamento.

Sono due sorgenti distinte e complementari:

| Sorgente | Cos'è | Dove va |
|---|---|---|
| `RuleSpec` / `RuleAtom` | regole estratte, versionate, con `Section`/`PageNumber` | superficie di **ispezione**, non il lettore |
| `TextChunk` (via `/kb-docs/{id}/chunks`) | il regolamento leggibile, con `headingPath` | il **lettore** |

Decisione: `/games/[id]/rules` **elenca i documenti** del gioco e rimanda al lettore; non diventa un terzo lettore. L'indirizzo del lettore (`/knowledge-base/[id]` o una sotto-route del gioco) è deciso da #3935, non qui — ma **un solo lettore** è vincolo di questo ADR.

---

## Impatto misurato

Comandi riproducibili, eseguiti su `main-dev` @ `708c9e0cf`. **Riesegui prima di stimare**: questi numeri invecchiano.

```bash
# Link entranti verso il dettaglio in libreria
grep -rn '/library/\${' apps/web/src --include=*.tsx --include=*.ts | wc -l   # 90 occorrenze
grep -rl '/library/\${' apps/web/src --include=*.tsx --include=*.ts | wc -l   # 61 file
```

> Il piano §M.1 riportava 117 su 84: quel conteggio usava un pattern più largo, che includeva anche le occorrenze letterali di `/library/[gameId]`. **90 su 61** è la misura dei link costruiti a runtime, cioè quelli che il codemod deve toccare.

### La navigazione è in parte centralizzata — e questo riduce molto #3928

Due file concentrano la costruzione degli href e vanno migrati **per primi**:

- `src/lib/navigation/index.ts` — API tipizzata: `libraryGame(gameId)`, `libraryGameTab(gameId, tab)`, tipo `LibraryGameTab`
- `src/config/entity-navigation.ts` — `ENTITY_NAVIGATION_GRAPH`, 6 occorrenze, il grafo di navigazione fra entità

Ogni consumatore che passa da qui si aggiorna senza essere toccato. Il codemod sui restanti 59 file va fatto **dopo**, e sarà più piccolo di quanto il conteggio grezzo suggerisca.

⚠️ Non tutte le occorrenze di `/library/` sono dettaglio: alcune puntano all'**indice** della collezione, che non si sposta. Un `sed` cieco le romperebbe. Il discrimine è il segmento dinamico: `/library/${id}` è dettaglio, `/library` è indice.

---

## Consequences

**Positive**

- Un solo albero: le CTA smettono di saltare, e `no-game-detail-orphan-routes` può essere ritirata o riscritta come guardia contro la regressione.
- Il dettaglio gioco diventa raggiungibile allo stesso indirizzo dal catalogo e dalla libreria.
- Il modello di navigazione interna si uniforma su sotto-route.

**Negative**

- 10 route spostate e ~90 link da riscrivere: cambiamento ampio, benché meccanico.
- Si inverte una consolidazione passata di cui non conosciamo il razionale (§Rischi).
- I bookmark interni verso `/library/[gameId]` passano per un redirect finché non si aggiornano.

**Neutre**

- Nessun impatto backend: gli endpoint sono indipendenti dal path del frontend.
- Nessun impatto SEO: l'app non è distribuita.

---

## Rischi

| # | Rischio | Mitigazione |
|---|---|---|
| R1 | **La migrazione precedente correggeva un problema che non conosciamo** e che questa reintroduce | Nessuna mitigazione preventiva possibile: il razionale non è recuperabile. Mitigazione reattiva: se durante #3927 emerge un motivo strutturale per cui il dettaglio stava sotto `/library` (es. una dipendenza dal contesto «gioco posseduto»), **fermarsi e riaprire questo ADR** invece di forzare |
| R2 | Due segmenti dinamici con nome diverso nello stesso ramo sono un errore di build in App Router | La migrazione va fatta in un solo passaggio, non incrementale (vincolo su #3927) |
| R3 | Un codemod cieco su `/library/` rompe i link all'indice della collezione | Discriminare per segmento dinamico; rivedere a mano i casi ambigui; contare atteso vs prodotto nel PR |
| R4 | Il dettaglio gioco assume oggi il contesto «gioco in libreria» (quota, possesso, campagne) | Da verificare in #3927: se le sotto-route `play`/`toolkit` dipendono dall'appartenenza alla libreria, il gate va spostato con loro, non perso |

---

## Alternatives considered

**`/library/[gameId]` come canonico** — ha 10 route contro 5, quindi meno lavoro di migrazione, ed è dove la consolidazione precedente aveva portato le cose. Scartata: `/library` significa «la mia collezione», e il dettaglio di un gioco esiste anche per un gioco che non possiedo — è la superficie che il catalogo pubblico deve poter linkare.

**Lasciare due alberi con responsabilità distinte** (catalogo vs libreria) — scartata: è lo stato attuale, e produce le CTA che saltano. Se due pagine mostrano la stessa entità, la differenza fra loro diventa un dettaglio implementativo che l'utente subisce.

---

## Note di esecuzione

Ordine obbligato: **#3927** (sposta route, un solo passaggio) → **#3928** (migra prima `lib/navigation` e `entity-navigation`, poi il resto) → **#3929** (redirect).

Aggiornare `entity-navigation.ts:67-71`: quel commento descriverà una migrazione invertita, e lasciarlo com'è renderebbe il prossimo lettore più confuso di adesso.
