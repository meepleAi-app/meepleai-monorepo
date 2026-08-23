# Prefisso e5 e retrieval cross-lingua — misura sul corpus reale

**Data**: 2026-08-17 · **Issue**: [#3737](https://github.com/meepleAi-app/meepleai-monorepo/issues/3737) (prefisso e5), [#3740](https://github.com/meepleAi-app/meepleai-monorepo/issues/3740) (`catan-setup-it`) · **Corpus**: staging, 56.367 chunk / 127 manuali, `intfloat/multilingual-e5-base` (768d, L2-normalizzati)

## 🔴 Esito: entrambe le correzioni sono state revertite (2026-08-18)

**Leggi questo prima del resto.** Le misure qui sotto sono corrette per ciò che misurano, ma **non predicono il gate**, e due correzioni costruite su di esse sono state revertite dopo che il gate le ha bocciate.

| stato di `main-dev` | gioco atteso nei top-3 |
|---|---|
| pre-fix (`f55753cb5`) | **10/11** |
| `#3737` — prefisso `query:` (PR #3741) | **8/11** |
| `#3737 + #3740` — offset di lingua (PR #3743) | 8/11 — *identico*, byte per byte |
| `#3737 + #3740 +` ri-taratura della fusione | **5/11** |

Tre conclusioni, tutte contrarie a quanto questo documento sosteneva:

1. **Il corpus del gate ha la stessa composizione di staging, con granularità diversa** — ⚠️ *corretto il 2026-08-19, vedi § «Cosa ha trovato il primo dump»*. La versione precedente di questo punto affermava che nello snapshot il `lang` fosse **uniforme**, dedotto dal fatto che la correzione per lingua avesse prodotto un output byte-identico. **È falso**: misurato, lo snapshot ha **9840 `en` / 943 `it` / 107 `de`** e gli stessi 13 manuali non inglesi di staging. Differisce la granularità (10.890 chunk contro 56.367), non la composizione.
2. **#3737 è teoricamente corretto e empiricamente dannoso** su quel corpus: −2 query. Codificare una domanda come passaggio resta sbagliato per il model card di e5, ma il 10/11 precedente dipendeva anche da quella codifica.
3. **`absent = 0` in `FuseGlobally` è load-bearing.** Sembrava un artefatto («0 significa rilevanza minima dove il dato è ignoto») e invece rende la fusione **congiuntiva**: per stare in cima serve evidenza da *entrambi* i bracci. Toglierlo — con pesi simmetrici — ha portato 8/11 → 5/11, riempiendo i top-3 di manuali scorrelati. Il commento di #3735 aveva ragione per un motivo che non dichiarava.

**Cosa serve prima di riprovare**: non un'altra ipotesi sui pesi, ma **strumentazione**. Finché il gate non emette l'aggregato per-candidato (`VectorScore`, `KeywordScore`, `lang`, documento) come artifact, ogni taratura è una scommessa da ~45 minuti — e tre scommesse su tre sono andate al ribasso.

## Perché questo documento

#3740 chiede esplicitamente di **misurare prima di ipotizzare**, e nomina tre piste. Questo audit le risolve con i numeri — sul corpus di staging. La parte che regge è l'analisi delle tre piste; la parte che **non** regge è la generalizzazione al corpus del gate, ed è l'errore da cui è nato tutto il resto.

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

**Conclusione (rivista)**: #3737 è un difetto reale, e su **questo** corpus il prefisso corretto migliora nettamente il braccio vettoriale. Corretto in PR [#3741](https://github.com/meepleAi-app/meepleai-monorepo/pull/3741) e poi **revertito**: sul corpus del gate la stessa correzione porta 10/11 → 8/11. Le due affermazioni non sono in contraddizione — misurano corpora diversi, e la seconda è quella che conta perché è la pipeline completa.

### Effetto collaterale misurato: la cache semantica delle risposte

> Vale **se e quando** il prefisso corretto verrà reintrodotto: con il revert non è più in gioco. Resta qui perché è misurato e servirà a chi riprova.

`SemanticResponseCache` (Redis, TTL 24 h) è una cache **query↔query**: confronta il vettore della domanda nuova con quelli delle domande già viste e serve la risposta se la cosine è `>= 0.95`. Cambiare il prefisso delle query cambia quel vettore, quindi le voci scritte prima del fix non combaciano più con le domande di dopo.

Quanto: `cos(passage: X, query: X)` sulla **stessa** domanda sta a cavallo della soglia.

| query | `cos(passage:X, query:X)` | esito con soglia 0.95 |
|---|---|---|
| `catan-setup` | 0.9482 | miss |
| `catan-setup-it` | 0.9351 | miss |
| `wingspan-round-goals` | 0.9367 | miss |
| `dominion-buy-phase` | 0.9603 | **hit** |

Quindi il degrado è **parziale e transitorio**: la maggior parte delle voci pre-fix diventa irraggiungibile, alcune restano raggiungibili — e un hit resta *corretto*, perché è la stessa domanda. Si esaurisce con il TTL di 24 ore, senza intervento e senza risposte errate. Non serve invalidare la cache a mano; se si volesse comunque, `InvalidateGameAsync` esiste.

⚠️ Nota per chi collegherà `CacheSemanticPlugin` al servizio reale (oggi è uno stub hash-based, `// Simulate embedding generation`): è anch'essa una cache query↔query, quindi **entrambe le sponde del confronto devono usare lo stesso prefisso**.

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

### ⚠️ Una discrepanza da non nascondere

Il clustering linguistico è dimostrato **sul braccio vettoriale**, e questo non è ancora tutta la storia del top-3 osservato. La baseline #3739 registra per `catan-setup-it` tre distrattori **inglesi** — star-wars-rebellion, imperial-settlers, cthulhu-death-may-die — mentre il top-10 cosine misurato qui è composto da dieci chunk **italiani**. Le due osservazioni non coincidono, e la differenza è informativa:

- il retrieval prende **top-K per gioco** (`topK: 3` nella fixture), quindi i 13 manuali non inglesi contribuiscono ~39 candidati, non 131: la struttura per-gioco attenuisce già molto il cluster;
- il punteggio finale è `0.7 · cosine_normalizzata + 0.3 · ts_rank_cd_normalizzata`, e il braccio lessicale gira con la config FTS **del gioco** — quindi la query italiana viene stemmata all'inglese. Le sue parole inglesi accidentali (`due` è una parola inglese; `come`, `in`) possono far emergere manuali inglesi che con la domanda non hanno nulla a che fare, e questo è il candidato più probabile per i tre distrattori osservati.

Cioè: per `catan-setup-it` concorrono almeno **due** difetti indipendenti oltre al prefisso — il cluster linguistico sul vettoriale e un braccio lessicale che su una query fuori-lingua matcha rumore. Nessuno dei due è stato quantificato sulla pipeline completa qui, e per farlo onestamente serve il gate, non questo metodo. È la ragione per cui questo audit **non** propone di chiudere #3740: la sua prima voce di DoD («è stabilito quale pista regge, con il dato che la sostiene») è soddisfatta, le altre no.

## Cosa è stato provato, e come è finito

Tre configurazioni misurate col gate, tutte al ribasso. Le riporto per intero perché il valore residuo di questo lavoro è sapere **cosa non funziona e perché**.

### 1. Prefisso `query:` da solo (#3737, PR #3741) — 10/11 → 8/11

Perdono il manuale nominato `wingspan-round-goals-it` e `dominion-buy-phase-it`; `catan-setup` scende da 2 chunk Catan a 1. Migliora `ark-nova-conservation-it`.

Il meccanismo, per come lo si legge nei dati: con il prefisso corretto il vettoriale diventa semanticamente più forte e su una query italiana attrae il **contenuto italiano**, di qualunque gioco. L'unico segnale che conosce il *nome* del gioco è il lessicale, che pesava 0.3.

### 2. Correzione dell'offset di lingua (#3740, PR #3743) — nessun effetto

Output **byte-identico** su tutte le 11 query rispetto alla configurazione 1.

⚠️ **La spiegazione data qui in origine era sbagliata**, e il primo dump l'ha falsificata — vedi § *«Cosa ha trovato il primo dump»*. Non era il corpus a essere monolingua: era il codice a non poterne vedere la lingua.

### 3. Ri-taratura della fusione — 8/11 → 5/11

Pesi simmetrici `0.5/0.5` **più** rimozione della penalità sul segnale assente (normalizzare sul peso dei segnali presenti). Regrediscono anche query EN prima solide: `catan-setup` va a **zero** chunk Catan, `ark-nova-conservation` da 3/3 a 1/3.

La firma è inequivocabile — i top-3 si riempiono di manuali scorrelati (`frostpunk`, `concordia`, `scythe`, `frosthaven`, `mage-knight`, `dune-imperium`): è l'**uncapping degli hit keyword-only**.

> **La lezione, ed è la cosa più utile qui.** `absent = 0` sembrava un artefatto: contribuire 0 asserisce «rilevanza minima possibile» dove il dato reale è «ignoto». Ma è **load-bearing**: rende la fusione di fatto **congiuntiva** — per stare in cima serve evidenza da *entrambi* i bracci — ed è ciò che tiene giù i match lessicali generici, cioè esattamente il difetto che #3735 aveva misurato. Il commento di #3735 aveva ragione per un motivo che non dichiarava, e la sua reticenza è stata scambiata per una svista.

## Cosa ha trovato il primo dump (2026-08-19)

Il primo artifact `rag-fusion-tuning-*` prodotto dal gate (run `32262228377`) ha **falsificato l'unica conclusione di questo audit basata su inferenza invece che su misura**.

### Lo snapshot del gate NON è monolingua

| `lang` | chunk |
|---|---|
| en | 9.840 |
| it | **943** |
| de | **107** |

Tredici manuali non inglesi, **gli stessi di staging**: 7-wonders, agricola, azul, barrage, carcassone, descent, pandemic, root, scacchi-fide, splendor, terraforming-mars, ticket-to-ride (it) · great-western-trail (de). Stessi 127 documenti. Ciò che differisce è la **granularità**: 10.890 chunk contro i 56.367 di staging, perché l'estrattore Docnet produce chunk molto più grossi.

### Perché allora #3743 fu un no-op byte-identico

Non per il corpus. Per un difetto nel codice della correzione stessa:

```sql
-- PgVectorStoreAdapter.SearchWithScoresAsync
SELECT e.id, e.vector_document_id, e.text_content, e.model,
       e.chunk_index, e.page_number, e.role_tags, …, tc."Heading"
```

**`e.lang` non è nella SELECT**, e `Embedding.Language` ha l'inizializzatore `= "en"`. Ogni candidato del braccio vettoriale arrivava quindi alla fusione con `Language = "en"`, qualunque fosse la lingua vera del chunk: un solo gruppo linguistico, media del gruppo uguale alla media globale, offset **esattamente 0**. Il no-op osservato non era una proprietà del corpus, era una colonna mancante mascherata da un default non-null.

**Conseguenza**: l'idea di #3740 non è stata provata e bocciata — **non è mai stata eseguita**. Chi la riprende deve prima aggiungere `e.lang` alla proiezione.

È anche la dimostrazione di cosa serviva lo strumento: questa cosa non era deducibile: era osservabile, e nessuno la stava osservando.

> **Chiuso il 2026-08-22.** Le tre SELECT di lettura dell'adapter (`SearchAsync`, `SearchWithScoresAsync`, `SearchByMultipleGameIdsAsync`) proiettano ora `lang`, e la lingua per candidato arriva fino al campo `l` del dump `[RAG-TUNE]` — prima si conosceva la composizione del *corpus* (via query SQL nel workflow) ma non quella dei *candidati recuperati*, che è la grandezza su cui si ragiona. Un test di integrazione semina tre chunk `en`/`it`/`de` e asserisce il valore riletto su ciascuno dei tre percorsi: prima del fix falliva su tutti e tre con `"en"` ovunque. Nessun consumatore leggeva `Embedding.Language` sul percorso di ricerca, quindi il comportamento di produzione non cambia — cambia solo che ora la lingua è misurabile.

## Perché si è smesso di iterare

Due previsioni sul gate, entrambe smentite: «il prefisso corretto migliora» e «togliere la penalità sull'assenza aiuta». Il ragionamento locale era coerente e i test unit passavano; il gate no. Non esiste, al momento, un modello funzionante di quella pipeline — e ogni ipotesi costa ~45 minuti di CI.

**Quello che manca non è un'altra idea sui pesi, è strumentazione.** Serve che il gate emetta come artifact l'**aggregato per-candidato** prima della fusione: per ogni query, `[documento, VectorScore, KeywordScore, lang, arm]`. Con quel dump la fusione si tara offline in secondi su dati veri, e si risolve anche la domanda rimasta senza risposta — cosa dica davvero la colonna `lang` in quello snapshot.

## Direzioni ancora aperte, non provate

1. **Correggere il metadato `language`** del manifest per i 13 manuali non inglesi (vale su staging; sullo snapshot la colonna è ora letta e riportata per candidato, vedi la nota del 2026-08-22).
2. **Rendere il braccio lessicale consapevole della lingua della query.** Oggi la config FTS viene dal gioco, quindi una domanda italiana viene stemmata all'inglese e le sue parole inglesi accidentali (`due`, `come`, `in`) diventano segnale.
3. **Accettare e documentare** che una query IT su un gioco che esiste solo in EN non sia recuperabile — cioè che `catan-setup-it` resti rosso per costruzione. Va scritto, non subito in silenzio.

## Riproducibilità

Il metodo è tre passaggi (codifica locale → letterale `::vector` → `ORDER BY <=>`) e non richiede infrastruttura dedicata. Serve accesso SSH a staging e il modello in cache locale. Gli script della misura sono volutamente **non** committati: sono usa-e-getta e rifarli è più affidabile che fidarsi di uno script invecchiato contro un corpus che cambia a ogni re-bake.

## Riferimenti

- [#3735](https://github.com/meepleAi-app/meepleai-monorepo/issues/3735) / PR #3738 — il fix del ranking cross-gioco che ha isolato questa query
- [#3739](https://github.com/meepleAi-app/meepleai-monorepo/issues/3739) — la baseline che registra il 10/11
- [#3266](https://github.com/meepleAi-app/meepleai-monorepo/issues/3266) / [#3269](https://github.com/meepleAi-app/meepleai-monorepo/issues/3269) — l'epic della safety-net EN+IT
- [rag-smoke-runbook.md](../operations/rag-smoke-runbook.md) — il gate, e il presupposto «il corpus è in inglese» che questo audit corregge
- [Documentazione e5](https://huggingface.co/intfloat/multilingual-e5-base) — «each input text should start with "query: " or "passage: "»
