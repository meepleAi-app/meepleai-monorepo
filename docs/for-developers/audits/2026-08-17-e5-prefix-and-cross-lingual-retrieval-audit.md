# Prefisso e5 e retrieval cross-lingua — misura sul corpus reale

**Data**: 2026-08-17 · **Issue**: [#3737](https://github.com/meepleAi-app/meepleai-monorepo/issues/3737) (prefisso e5), [#3740](https://github.com/meepleAi-app/meepleai-monorepo/issues/3740) (`catan-setup-it`) · **Corpus**: staging, 56.367 chunk / 127 manuali, `intfloat/multilingual-e5-base` (768d, L2-normalizzati)

## Perché questo documento

#3740 chiede esplicitamente di **misurare prima di ipotizzare**, e nomina tre piste. Questo audit le risolve con i numeri, e nel farlo ridimensiona #3737 al rialzo: l'effetto del prefisso è molto più grande della stima nella issue, ma **non** è la causa di #3740.

## Metodo

I vettori del corpus **non** sono stati ricalcolati: restano quelli indicizzati. Cambia solo la codifica della query.

1. Le query canoniche di `infra/fixtures/rag-canonical-queries.json` sono state codificate in locale con `intfloat/multilingual-e5-base`, una volta con prefisso `passage:` (comportamento pre-fix) e una con `query:`, `normalize_embeddings=True` come il servizio.
2. Ogni vettore è stato passato a Postgres su staging come letterale `::vector` e ordinato con `<=>` (cosine) contro `pgvector_embeddings`, con join su `vector_documents` → `pdf_documents` per risolvere il nome del manuale.
3. Per ogni query è stato misurato il **rango del miglior chunk del manuale atteso**, sul corpus intero e restringendo a `lang='en'`.

⚠️ **Limite dichiarato.** Questa è la misura del **braccio vettoriale**, non della pipeline completa. Il top-3 finale di `/ask/global` esce da `MultiGameHybridSearchService.FuseGlobally`, che combina la cosine (peso 0.7) con `ts_rank_cd` (peso 0.3) dopo un top-K per gioco. I ranghi qui **non** sono i ranghi finali. Sono comunque il segnale che — per la motivazione scritta in `FuseGlobally` stesso — distingue un manuale da un altro: le parole di una query di regolamento compaiono in ogni manuale, il lessicale da solo non discrimina il gioco.

## Risultato 1 — il prefisso (#3737)

Rango del miglior chunk del manuale atteso, ordinamento cosine sul corpus intero:

| query | lingua | `passage:` (pre-fix) | `query:` (corretto) |
|---|---|---|---|
| `catan-setup` | en | 10 | **1** |
| `ark-nova-conservation-it` | it | 15 | **1** |
| `dominion-buy-phase-it` | it | 30 | 25 |
| `catan-setup-it` | it | 353 | 132 |

Sul top-10 globale della query EN il ribaltamento è totale: con `passage:` **nessun** chunk di Catan compare (i primi sono frostpunk, res-arcana, el-grande, nemesis); con `query:` Catan occupa i ranghi 1, 2, 3 e 7.

La stima nella issue — «+0.018 di similarità, **non cambia l'ordine**» — era corretta *sul suo campione*: quattro passaggi scelti a mano. Su 56.367 il margine in più riordina l'insieme. È la differenza fra misurare il margine e misurare il rango.

Lo stesso vale per `seven-wonders-military-it`: con `passage:` i primi due sono iss-vanguard e voidfall e 7 Wonders arriva al rango 9; con `query:` **tutti e dieci** i primi risultati sono `7-wonders_rulebook.pdf`.

**Conclusione**: #3737 è un difetto reale e sottostimato. Corretto in PR [#3745](https://github.com/meepleAi-app/meepleai-monorepo/pull/3745).

## Risultato 2 — le tre piste di #3740

### Pista 1 — il prefisso: regge, ma non basta

Necessaria e insufficiente. `catan-setup-it` passa da rango 353 a 132: un miglioramento di 2,7×, che però lascia 131 chunk davanti al manuale giusto. Il prefisso **non** spiega #3740.

### Pista 2 — il campo `language` della richiesta: esclusa

Escluso per lettura del codice, in tre passaggi verificabili:

1. `GlobalKbAskRequest.Language` diventa `CrossGameStreamQaQuery.AgentLanguage` (`KnowledgeBaseEndpoints.cs`, `AgentLanguage: request.Language ?? "it"`).
2. `AgentLanguage` è consumato **solo** da `AssembleFromContextAsync` — è la lingua della risposta dell'LLM, non della ricerca. `MultiGameHybridSearchService` non contiene la parola `language`.
3. Il vettore della query nasce in `HybridSearchService.ExecuteVectorSearchAsync`, che chiama l'overload **senza** `language`. E anche se lo ricevesse: il servizio Python **valida** `language` contro la lista dei supportati e poi non lo usa.

Il campo non può quindi né filtrare né pesare i chunk. La pista è chiusa.

### Pista 3 — traduzione / espansione della query: inesistente

Sul percorso cross-gioco non c'è traduzione. L'unica espansione è **lessicale** (`ExpandTermsToTsQuery` / `ExpandHeadingMatchTerms`) e la sua tabella di sinonimi è scelta dalla config FTS **del gioco**, non dalla lingua della query — quindi non tocca il vettore e non è sensibile alla lingua della domanda.

## Risultato 3 — la causa vera: clustering linguistico

Il corpus **non è tutto inglese**, contrariamente al presupposto di #3740 e del runbook RAG smoke:

| `lang` | chunk |
|---|---|
| en | 51.505 |
| it | 4.332 |
| de | 530 |

I 13 manuali non inglesi sono PDF nativamente in quella lingua (`is_translation = false`): root, scacchi-fide, descent, barrage, agricola, pandemic, 7-wonders, azul, terraforming-mars, ticket-to-ride, carcassone, splendor (it) e great-western-trail (de). Sono nel manifest `dev.yml`, che li dichiara tutti `language: en` — **il metadato del manifest è sbagliato per questi giochi**, mentre il `lang` per-chunk è stato rilevato all'ingest.

Nello spazio di `multilingual-e5` la lingua del testo è una componente dominante. Su un corpus mixed-language una query italiana ha per vicini i chunk **italiani**, di qualunque gioco. Il top-10 di `catan-setup-it` con il prefisso corretto è: root, barrage, scacchi-fide ×5, descent — dieci chunk italiani, zero pertinenti.

La verifica decisiva è restringere il corpus a `lang='en'`:

| query (it) | `passage:` + solo en | `query:` + solo en |
|---|---|---|
| `catan-setup-it` | 133 | **1** |
| `dominion-buy-phase-it` | 7 | **1** |
| `ark-nova-conservation-it` | 9 | **1** |

Con il prefisso corretto **e** i distrattori italiani fuori, il manuale atteso è al rango 1 in tutti e tre i casi. Le due variabili sono entrambe necessarie, e sono indipendenti.

Questo spiega anche perché `wingspan-round-goals-it` e `seven-wonders-military-it` «già funzionano» mentre Catan no: Wingspan e 7 Wonders hanno contenuto nella lingua della query (7-wonders ha 263 chunk `it`), Catan, Dominion e Ark Nova sono **solo** in inglese. Non è che «l'italiano non funziona»: è che l'italiano funziona *troppo bene* verso i manuali italiani sbagliati.

## Cosa resta aperto

#3737 rimuove una delle due cause. La seconda — il clustering linguistico su corpus mixed-language — è un cambiamento di ranking e va misurato sulla pipeline completa, non sul solo braccio vettoriale: va quindi verificato con il gate RAG smoke, non con questo metodo. Direzioni candidate, in ordine di invasività:

1. **Normalizzare il segnale vettoriale per lingua** in `FuseGlobally` (min-max dentro ogni gruppo `lang`) invece che sull'intero aggregato. Cancella esattamente l'offset di lingua e lascia decidere al lessicale — dove «Catan» è il termine discriminante. È la direzione suggerita dalla tabella `solo lang=en` qui sopra.
2. **Correggere il metadato** `language` del manifest per i 13 manuali non inglesi. Non risolve il ranking da solo, ma oggi qualunque logica che si fidi di quel campo sta leggendo un dato falso.
3. **Accettare e documentare** che una query IT su un gioco che esiste solo in EN non è recuperabile a corpus misto — cioè che `catan-setup-it` resta rosso per costruzione. Va scritto, non subito in silenzio: il gate esiste per non far passare questo caso inosservato.

⚠️ Nessuna delle tre è stata applicata qui. Applicarne una senza passare dal gate significherebbe sostituire una misura con un'ipotesi, che è precisamente ciò che #3740 chiede di non fare.

## Riproducibilità

Il metodo è tre passaggi (codifica locale → letterale `::vector` → `ORDER BY <=>`) e non richiede infrastruttura dedicata. Serve accesso SSH a staging e il modello in cache locale. Gli script della misura sono volutamente **non** committati: sono usa-e-getta e rifarli è più affidabile che fidarsi di uno script invecchiato contro un corpus che cambia a ogni re-bake.

## Riferimenti

- [#3735](https://github.com/meepleAi-app/meepleai-monorepo/issues/3735) / PR #3738 — il fix del ranking cross-gioco che ha isolato questa query
- [#3739](https://github.com/meepleAi-app/meepleai-monorepo/issues/3739) — la baseline che registra il 10/11
- [#3266](https://github.com/meepleAi-app/meepleai-monorepo/issues/3266) / [#3269](https://github.com/meepleAi-app/meepleai-monorepo/issues/3269) — l'epic della safety-net EN+IT
- [rag-smoke-runbook.md](../operations/rag-smoke-runbook.md) — il gate, e il presupposto «il corpus è in inglese» che questo audit corregge
- [Documentazione e5](https://huggingface.co/intfloat/multilingual-e5-base) — «each input text should start with "query: " or "passage: "»
