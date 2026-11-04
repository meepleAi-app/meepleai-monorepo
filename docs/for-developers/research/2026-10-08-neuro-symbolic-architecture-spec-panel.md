# Spec panel — «Architettura Tecnologica Integrata per MeepleAI» (neuro-simbolica)

**Data**: 2026-10-08 · **Modalità**: critique · **Focus**: architecture + requirements · **Vincolo dato dall'utente**: restare sul profilo ibrido attuale (Hetzner CAX31 + OpenRouter), fase pre-Azure, «miglioramenti senza costi eccessivi».
**Pannello**: Fowler (lead architettura), Newman (confini dei servizi), Hohpe (integrazione), Nygard (esercizio e guasti), Wiegers (qualità dei requisiti), Adzic (esempi eseguibili), Crispin (verificabilità).
**Materiale**: il documento incollato dall'utente (LlamaParse/Unstructured Hi-Res → Neo4j GraphRAG + ontologia attiva → Clingo ASP → LangGraph → XState) confrontato con un inventario del repo eseguito oggi. Path relativi alla root del monorepo; `API` = `apps/api/src/Api`.
**Convenzione**: nessun conteggio volatile in prosa; dove serve una misura c'è il comando. I prezzi sono listino di ottobre 2026 e vanno in `(sec.)` se da fonte secondaria.

---

## 0. Verdetto in una pagina

Il documento descrive una **meta di arrivo** plausibile, ma la presenta come se MeepleAI partisse da zero. In realtà il repo ha già una versione minima di ogni blocco, e il valore sta nel farle crescere, non nel sostituirle:

| Blocco proposto | Cosa c'è già | Giudizio del pannello |
|---|---|---|
| Parsing layout-aware (LlamaParse / Unstructured Hi-Res) | Unstructured self-hosted con `strategy = fast` di default, `hi_res` solo per le regioni immagine (~200 s), cascata Unstructured → SmolDocling → Docnet, chunking heading-aware, `TableChunkIndexer` | **Migliorare ciò che c'è**: `hi_res` selettivo sulle pagine con tabelle costa solo CPU. LlamaParse è un'opzione a credito con un problema di IP da decidere prima |
| GraphRAG su Neo4j + ontologia attiva | triple LLM-estratte in `game_entity_relations` (entità `Game, Mechanic, Component, Phase, Action, Rule`; relazioni `HasMechanic … Overrides`), ma **nessun traversal**: `GraphRetrievalService` prende le top-N per confidenza e le inietta nel prompt | **Non aggiungere Neo4j ora**: un secondo database sul VPS a disco pieno per un grafo che non viene attraversato. Prima il traversal a 2 hop in Postgres (`WITH RECURSIVE`) e la risoluzione delle entità, poi misurare |
| Clingo / ASP + xclingo | nessun modello del tabellone; l'Arbitro Python valida con `re.match` sulla notazione scacchistica e **salta i `Constraint`** (#3668); l'Arbitro C# usa keyword "cannot/must not" | **Il solver è gratis, la formalizzazione no**: il costo è scrivere i programmi ASP per ogni gioco. Pilota su un gioco solo, dopo aver reso i claim delle mechanic card "regole con priorità" |
| LangGraph + checkpointer | `orchestration-service` è già LangGraph (`langgraph>=0.2.45`), ma è *publish-only*, non parte su staging, l'intent è a keyword, `enable_checkpointing` è dichiarato e **non cablato**; esiste un **secondo Arbitro in C#** | **Decidere un proprietario**: due arbitri in due linguaggi violano ADR-090. Cablare il checkpointer su Postgres è a costo zero |
| FSM di gioco + XState | `LiveGameSession` è già una FSM a 5 stati con invarianti; `UpdateGameState(JsonDocument)` è senza schema; XState è già dipendenza ma usata solo nella dashboard; ADR-071 (XState per la live) è *Proposed* | **Validare lo stato, non cambiare libreria**: `StateTemplateDefinition.SchemaJson` di GameToolkit esiste e nessuno lo applica |
| Loss function `L_totale` e reward binaria | MeepleAI non addestra modelli | **Rimuovere**: è materiale da paper, non da specifica |

Punteggi di qualità del documento (giudizio del pannello, scala 0–10): chiarezza 7 · completezza 5 · verificabilità **3** · coerenza interna 6 · aderenza al repo **2**.

---

## 1. Analisi dei requisiti (Wiegers, Adzic, Crispin)

**WIEGERS — Requisiti non misurabili.** Il documento promette «output privo di allucinazioni», «arbitrare qualsiasi regolamento senza margine d'errore», «eliminazione totale degli stati impossibili». Nessuna di queste frasi è un requisito: non dice quanto, misurato come, su quale insieme. Il repo ha già un bersaglio dichiarato e non misurato: il README di `tests/llm-eval/golden-set/` fissa «hallucination rate ≤ 3%», ma non esiste alcuna misura automatica di faithfulness (niente RAGAS/deepeval); l'unica rilevazione è a runtime (ADR-006, Layer 4).
- ❌ CRITICO — sostituire ogni «senza errori» con un bersaglio sul golden set: *recall@5 delle citazioni di pagina*, *tasso di verdetti dell'Arbitro contraddetti da un revisore*, *tasso di risposte con citazione mancante*. Le metriche esistono già in `DatasetEvaluationService` (Recall@5/10, nDCG@10, MRR, CitationAccuracy) e in KbQuality (`QualityBand`).
- 📝 Il bersaglio ≤ 3% va reso misurabile o tolto: un numero che nessun gate produce invecchia come la prosa.

**ADZIC — Nessun esempio eseguibile.** La frase chiave («modificatori applicabili a un'azione in presenza di particolari condizioni di stato e di illuminazione») è l'unico scenario e non è legato a un gioco del catalogo. Prima di qualunque grafo serve una **fetta multi-hop nel golden set**: domande la cui risposta richiede di collegare due sezioni del manuale (eccezione di una carta + regola generale), con citazioni attese su entrambe le pagine. Oggi `tests/evaluation-datasets/meepleai-en-seed.json` ha `relevant_chunk_ids` vuoto e `qa-questions.jsonl` ha `primary_pages: []`: il golden set non può ancora distinguere un sistema che attraversa relazioni da uno che non lo fa.
- 📝 Given/When/Then minimo per ogni blocco (esempi in §5). Senza questi, «GraphRAG aiuta» e «ASP serve» restano opinioni.

**CRISPIN — Verificabilità.** Il documento non dice come si testa un programma ASP, un'ontologia o una transizione di LangGraph. Nel repo il precedente utile è il gate `rag-fusion-tuning` (`infra/fixtures/rag-golden-baseline.json` + `rag-canonical-queries.json`, bench offline `infra/scripts/rag-fusion-bench.py`): ogni blocco nuovo va aggiunto **come braccio di quel gate**, con baseline prima e dopo. Comandi per dimensionare il golden set attuale:

```bash
wc -l tests/llm-eval/golden-set/qa-questions.jsonl tests/llm-eval/golden-set/translation-paragraphs.jsonl
python -c "import json;print(len(json.load(open('tests/evaluation-datasets/meepleai-en-seed.json',encoding='utf-8'))['samples']))"
python -c "import json;print(len(json.load(open('infra/fixtures/rag-canonical-queries.json',encoding='utf-8'))['queries']))"
```

---

## 2. Analisi architetturale (Fowler, Newman, Hohpe, Nygard)

### 2.1 Ingestione

**FOWLER.** Il confronto LlamaParse/Unstructured è corretto ma ignora che Unstructured è **già in casa** e che la sua modalità `hi_res` è usata solo in un passaggio dedicato (`UnstructuredPdfTextExtractor.cs:345-354`, #3435). La leva a costo zero è il **selettore di strategia** (`ExtractionStrategySelector.cs:24`, default `Fast`): `hi_res` per le pagine che Docnet riconosce come tabellari o multi-colonna, `fast` per il resto. Costo: CPU sul VPS (il passaggio `hi_res` oggi dura ~200 s per documento), nessun euro.

**NYGARD.** LlamaParse Agentic costa 10 crediti/pagina, con 10.000 crediti/mese gratuiti e $1,25 per 1.000 crediti oltre `(sec.)`: un regolamento da 60 pagine vale ~$0,75. Il prezzo non è il problema. Il problema è che **manda il PDF integrale a un terzo**: ADR-051 ha appena spostato la Mechanic Extractor su un modello «AI-first con citation enforcement» dopo una fase «l'AI non legge mai il PDF»; ADR-059 fissa la postura legale del catalogo. Inviare manuali coperti da copyright a LlamaCloud è una decisione IP, non tecnica, e va presa in una ADR prima di scrivere un adapter. Se passa, l'adapter è un nuovo `IPdfTextExtractor` keyed, come già previsto dalla cascata.

**HOHPE.** Le `parsing_instructions` («separa flavor da regole») sono una scorciatoia: il repo ha già `RoleClassifierService` (regex sugli heading + fallback LLM) con i flag `Tutorial, RulesReference, Narrative, Encounter, Lore, Setup` su chunk ed embedding (`RoleTags`). Manca la **granularità di frase** e la classe «esempio di gioco», che nei manuali inquina il retrieval delle regole. Un classificatore a livello di chunk con un modello piccolo via OpenRouter costa centesimi per manuale e si misura sul golden set: è un miglioramento pre-Azure, non una dipendenza nuova.

### 2.2 Rappresentazione della conoscenza

**FOWLER — il grafo esiste, ma è un elenco.** `EntityExtractor.cs:23` estrae già triple con un vocabolario controllato (`Overrides` compreso, che è esattamente la relazione defettibile del documento), `GameEntityRelationEntity` le persiste con `Confidence`, il flag `rag.enhancement.graph-traversal` esiste. Ma `GraphRetrievalService.cs:27-35` **non attraversa nulla**: ordina per confidenza e inietta un blocco `[Knowledge Graph]` nel prompt. Il documento chiede Neo4j per fare traversal a 2–3 hop. Prima di cambiare motore, va fatto il traversal nel motore che c'è:

```sql
-- 2 hop dalle entità riconosciute nella domanda, tutto in Postgres
WITH RECURSIVE walk(src, rel, tgt, depth, conf) AS (
  SELECT source_entity, relation, target_entity, 1, confidence
  FROM game_entity_relations WHERE game_id = @gameId AND source_entity = ANY(@seeds)
  UNION ALL
  SELECT r.source_entity, r.relation, r.target_entity, w.depth + 1, LEAST(w.conf, r.confidence)
  FROM game_entity_relations r JOIN walk w ON r.source_entity = w.tgt
  WHERE w.depth < 2 AND r.game_id = @gameId
)
SELECT * FROM walk ORDER BY conf DESC LIMIT 40;
```

Il repo usa già `WITH RECURSIVE` per l'albero dei chunk (`SearchKbChunksHandler.cs:159`). I semi (`@seeds`) si risolvono con `pg_trgm`, che è **già installato** (`infra/init/postgres-init.sql`) e mai usato nel codice applicativo.

**NEWMAN — Neo4j è un secondo sistema di record.** Community Edition: GPLv3, un solo database utente, heap + page cache + 1 GB di margine. Il VPS è all'80% di disco con tre immagini AI intoccabili (vedi `infra/hetzner/`). Aggiungere un database con la propria persistenza, backup e disaster recovery per un grafo che oggi non si attraversa è il classico costo di esercizio nascosto. Apache AGE porterebbe Cypher dentro Postgres (PG16 supportato), ma **non è nella allowlist di Azure Flexible Server**: sceglierlo ora contraddice il profilo Azure del report gemello. La CTE ricorsiva è portabile ovunque.

**HOHPE — l'ontologia «attiva» è risoluzione delle entità.** Il documento ha ragione sul problema (nodi duplicati, «fase del turno» contro «turno del giocatore»), ma la soluzione non è un prodotto: è (1) un vocabolario chiuso per i tipi, che c'è già nell'enum; (2) una **forma canonica** del nome (lowercase, singolare, lingua) e una tabella `game_entities` con alias; (3) una fusione a soglia `pg_trgm` con revisione umana per i casi ambigui, nello stesso pannello admin in cui oggi si approvano i claim. La misura: numero di entità distinte prima e dopo la canonicalizzazione su un gioco campione.

**Quando Neo4j avrebbe senso**: solo se, con il traversal in Postgres acceso, la fetta multi-hop del golden set migliora **e** la latenza P95 del braccio grafo supera il budget. Fino a quel momento è un'ipotesi.

### 2.3 Validazione deterministica

**FOWLER — il solver non è il collo di bottiglia, la formalizzazione sì.** `clingo` 5.8 è su PyPI, gratis, leggero: starebbe in `orchestration-service` domani. Ma un programma ASP va **scritto per ogni gioco** (fatti, regole, priorità, eccezioni), e la letteratura 2025 mostra che chiedere a un LLM di scrivere ASP è inaffidabile («Can LLMs Solve ASP Problems?», arXiv 2507.19749). Chi mantiene i programmi, con che revisione, con che test? Il documento non lo dice. Il repo ha già la risposta organizzativa: il **review gate umano per claim** della Mechanic Extractor (ADR-051 §7). La via a basso costo è far crescere il claim fino a diventare una regola formale:

| Oggi (`MechanicCardContent` v2) | Passo intermedio (ancora dati, non codice) |
|---|---|
| `Claim{Section, Claim, Citations[], Validations[]}` | `+ Kind (Rule | Exception | Example | Flavor)` · `+ Priority` · `+ Overrides[] → claimId` · `+ Trigger (fase/azione/carta)` |

Con questi quattro campi i claim diventano **regole defettibili come dati**, revisionabili dall'admin, iniettabili nel prompt in ordine di priorità (oggi la precedenza è house rule > claim > RAG grezzo, `VerifiedRulesRenderer.cs`), e **compilabili** in ASP o in un valutatore più semplice quando ne varrà la pena.

**NYGARD — oggi la «validazione» è una regex.** `rule_engine.py:257` fa `re.match` sulla notazione algebrica, `:252-254` dichiara «Constraint NOT evaluated: no board model exists», e `arbitro_agent.py:192` restituisce una confidence **fissa** (0,95 se valido, 0,90 se no). Qualunque cosa si chiami «deterministica» nel documento deve prima passare da qui: un cliente che legge `confidence: 0.95` crede a una misura che non esiste. Primo intervento, gratuito: confidence derivata da ciò che è stato davvero valutato (nessuna regola valutata ⇒ `null`, non 0,95).

**Sulla letteratura citata.** BoardgameQA (NeurIPS 2023) misura il ragionamento defettibile a profondità 1–3 e lo stato dell'arte 2025 non è ASP ma **LLM-ASPIC+** (argomentazione formale: 87,1% su BoardgameQA-2, 82,6% su BoardgameQA-3). Il documento usa il benchmark per motivare Clingo, ma il benchmark non lo dimostra. Per MeepleAI è una buona notizia: un framework di argomentazione lavora proprio su «regola + eccezione + priorità», cioè sul passo intermedio della tabella sopra, senza grounding né esplosione degli stati.

**Le formule di perdita vanno tolte.** `L_totale = L_standard + L_vincoli + L_esperto` e la reward binaria descrivono l'addestramento o il reinforcement di un modello. MeepleAI usa modelli via API (ADR-007) e non addestra nulla; la sezione non produce un requisito e confonde il lettore sul perimetro.

### 2.4 Orchestrazione e stato

**NEWMAN — due Arbitri, due linguaggi, un solo proprietario.** Python: `arbitro_agent.py` (LangGraph, lineare, regex). C#: `ArbitroAgentService.cs` + `MoveValidationDomainService.cs` (keyword) + `AskArbiterCommandHandler` (verdetto RAG con confidence `avg(score) × (citazioni > 0 ? 1 : 0,5)`). ADR-090 dice che la risposta grounded è di `KnowledgeBase` e che `SessionTracking` la consuma via `IMediator`. Il documento mette LangGraph al centro senza dire cosa succede all'Arbitro C#. Decisione da prendere in una ADR, con due esiti onesti:
- *A*: l'Arbitro Python diventa un **servizio di validazione puro** (stato + mossa → verdetto + regole applicate), chiamato dal C#; la generazione della spiegazione resta nel C# che ha già citazioni e multi-layer validation (ADR-006).
- *B*: l'Arbitro Python viene ritirato e la validazione formale nasce nel C#.
Finché non si decide, ogni lavoro su uno dei due è a rischio di essere buttato.

**HOHPE — il checkpointer è un flag senza implementazione.** `settings.py:73` espone `enable_checkpointing = True`; nessun nodo lo usa. Il documento ha ragione che senza checkpoint non c'è ripristino; la correzione costa un pacchetto (`langgraph-checkpoint-postgres`) sul Postgres già presente, nessun servizio nuovo. Nello stesso passo: l'intent classifier a keyword (`orchestrator.py:152-170`, commentato «placeholder») va misurato prima di essere sostituito, perché un router sbagliato costa più di un nodo lento.

**FOWLER — la FSM esiste, manca lo schema dello stato.** `LiveGameSession` è già la quintupla del documento: stati `Created, Setup, InProgress, Paused, Completed`, transizioni con precondizioni (`Start` richiede almeno un giocatore attivo, `AdvanceTurn` richiede `InProgress` e `< MaxTurns`), eventi di dominio, concorrenza `xmin`. Il buco è `UpdateGameState(JsonDocument?)` (`:828`): JSON libero, nessuna legalità. GameToolkit ha `StateTemplateDefinition.SchemaJson` che nessuno applica. Validare il JSON di stato contro lo schema del toolkit del gioco **prima** di persisterlo è il primo mattone della «eliminazione degli stati impossibili», costa una libreria JSON Schema e nessuna infrastruttura. Il contatore di iterazioni del documento è una riga.

**Sul client.** XState è già una dipendenza (`xstate`, `@xstate/react`) usata solo da `DashboardEngine.ts`; la live session ha una FSM derivata pura (`session-live-state.ts`) e uno store Zustand. ADR-071 (XState per la live) è ancora *Proposed*. Portare XState nella live senza chiudere ADR-071 è un cambio di libreria senza requisito: priorità bassa, nessun costo evitato.

### 2.5 Libro-game

Il documento promette di «identificare vicoli ciechi e loop narrativi» col grafo. Il modello attuale non ha nodi: `GameBook` ha `ParagraphScheme` e `KbSourceDocId`, i numeri di paragrafo arrivano da `RegexParagraphNumberExtractor`, la campagna (`GamebookCampaignSession`) traccia solo titolo ed esito. Il piano SP6 è UI e dichiara fuori scope la navigazione v2. Qui il grafo serve davvero, ma è un grafo **piccolo e deterministico** (paragrafo → scelta → paragrafo, con requisiti): tabella `gamebook_edges` in Postgres, estrazione dei rimandi («vai al 112») con regex più LLM per le condizioni, e una CTE ricorsiva per raggiungibilità e cicli. Nessun Neo4j, e un esempio Given/When/Then si scrive subito (§5).

---

## 3. Analisi di esercizio (Nygard, Hightower in assenza: nota dal report gemello)

- **Disco e memoria sul VPS**: tre immagini AI (~26 GB) sono il costo fisso; Neo4j aggiungerebbe heap + page cache + persistenza. Qualunque blocco nuovo deve vivere **dentro** Postgres o `orchestration-service`, oppure attendere il profilo Azure.
- **`orchestration-service` non è osservato**: non parte su staging (opt-in `compose.staging.tutor.yml`), quindi nessun log, metrica o incident l'ha mai esercitato. Prima di caricarlo di responsabilità (ASP, checkpoint, router), va acceso su staging con health e metriche nel Prometheus esistente, e con la deadline per chunk già usata nello streaming (#3601).
- **Costo per domanda**: ogni hop del grafo e ogni chiamata al solver sta nel percorso sincrono della chat. Budget da fissare prima: P95 del braccio grafo ≤ budget del braccio lessicale attuale, misurato dal dump `[RAG-TUNE]`.
- **Modalità di guasto da specificare**: solver che non termina (timeout e verdetto `Unknown`, mai `Valid` per default), grafo vuoto per un gioco nuovo (fallback silenzioso al RAG, con metrica), entità non risolta (nessun seme ⇒ braccio grafo spento per quella domanda).

---

## 4. Roadmap pre-Azure a basso costo (ordinata)

Costo in euro: tutto a **0 €/mese** salvo le righe segnate; «sforzo» S/M/L è una stima qualitativa del pannello, non una misura.

| # | Intervento | Blocco del documento | Sforzo | Misura di esito |
|---|---|---|---|---|
| 1 | **Fetta multi-hop nel golden set** (domande regola + eccezione, due pagine attese) e riempimento di `primary_pages`/`relevant_chunk_ids` | tutti | S | il gate `rag-fusion-tuning` ha una baseline multi-hop |
| 2 | **Confidence onesta nell'Arbitro Python** (`null` se nessuna regola valutata) + checkpointer Postgres cablato + servizio acceso su staging con metriche | LangGraph | S | nessun verdetto con confidence fissa; checkpoint visibile in tabella |
| 3 | **ADR «un solo Arbitro»** (esito A o B di §2.4) | LangGraph / ASP | S (decisione) | ADR accettata; codice dell'altro arbitro marcato deprecato |
| 4 | **Traversal a 2 hop in Postgres** su `game_entity_relations` + risoluzione entità con `pg_trgm` e forma canonica; dietro il flag esistente `rag.enhancement.graph-traversal` | GraphRAG | M | delta su (1) e P95 del braccio grafo nel dump `[RAG-TUNE]` |
| 5 | **`hi_res` selettivo** per pagine tabellari/multi-colonna nel `ExtractionStrategySelector` | Ingestione | S | citazioni di tabella corrette su un campione; tempo di indicizzazione per documento |
| 6 | **Classificazione chunk `Rule / Exception / Example / Flavor`** con modello piccolo, salvata accanto a `RoleTags`, usata come filtro nel retrieval | Ingestione / ontologia | M (centesimi per manuale) | precisione del classificatore su un campione etichettato; delta recall@5 sulle domande di regola |
| 7 | **Claim → regola defettibile come dati**: `Kind`, `Priority`, `Overrides[]`, `Trigger` in `MechanicCardContent` v3 (additivo, review umana invariata) | ASP / ontologia | M | claim con `Overrides` presenti nei giochi campione; prompt ordinato per priorità |
| 8 | **Schema dello stato live**: validazione JSON Schema di `UpdateGameState` contro `StateTemplateDefinition.SchemaJson` + contatore di iterazioni | FSM | S | stato illegale rifiutato con 400 e test di dominio |
| 9 | **Grafo del libro-game in Postgres** (`gamebook_edges`, estrazione rimandi, CTE per raggiungibilità e cicli) | GraphRAG (libro-game) | M | vicoli ciechi e loop elencati per un libro campione |
| 10 | **Pilota ASP su un gioco** del golden set, programma scritto a mano dai claim del punto 7, confronto con LLM-ASPIC+ in lettura; `clingo` dentro `orchestration-service` | ASP | M | tasso di verdetti corretti sulla fetta (1) contro l'Arbitro attuale |
| 11 | *(decisione IP, poi eventuale)* LlamaParse come stage keyed opzionale | Ingestione | S dopo ADR | confronto tabelle su 3 manuali; costo in crediti misurato |
| — | Neo4j · XState nella live · Apache AGE | — | — | **non ora**: nessuna misura li richiede e tutti hanno un costo di esercizio o una contraddizione col profilo Azure |

I punti 1–3 non toccano l'architettura e sbloccano tutto il resto; senza il punto 1 nessun altro punto è dimostrabile.

---

## 5. Esempi eseguibili minimi (Adzic)

```gherkin
# Multi-hop (punto 1 e 4)
Given il gioco "X" indicizzato, con la regola generale a p. 7 e l'eccezione della carta "Y" a p. 15
When l'utente chiede "posso fare Z se ho giocato Y?"
Then la risposta cita p. 15 e p. 7, in quest'ordine di precedenza
And il dump [RAG-TUNE] mostra il braccio grafo con un cammino Rule -Overrides-> Rule

# Confidence onesta (punto 2)
Given una mossa per un gioco senza regole Constraint valutabili
When l'Arbitro Python restituisce il verdetto
Then confidence è assente (null) e applied_rule_ids è vuoto

# Stato live (punto 8)
Given un toolkit con StateTemplateDefinition.SchemaJson che richiede "round" intero ≥ 1
When il client invia UpdateGameState con "round": "due"
Then la API risponde 400 e lo stato persistito è invariato

# Libro-game (punto 9)
Given un libro con i paragrafi 12 → 40, 40 → 12 e 41 senza uscite
When viene costruito il grafo
Then 41 è segnalato come vicolo cieco e {12, 40} come ciclo
```

---

## 6. Cosa resta aperto (decisioni dell'utente)

1. **Proprietario dell'Arbitro** (Python o C#): condiziona i punti 2, 3, 10.
2. **Postura IP su parser esterni**: condiziona il punto 11 e qualunque uso di LlamaCloud/Document Intelligence (stesso tema nel report gemello).
3. **Budget di latenza** per il braccio grafo e per il solver nel percorso della chat.
4. **Gioco pilota** per i punti 7 e 10 (uno dei cinque del golden set, preferibilmente con eccezioni da carte).

---

## 7. Fonti

- Documento incollato dall'utente (2026-10-08) con la sua bibliografia: BoardgameQA ([arXiv 2306.07934](https://arxiv.org/abs/2306.07934v1), [NeurIPS 2023](https://papers.neurips.cc/paper_files/paper/2023/file/7adce80e86aa841490e6307109094de5-Paper-Datasets_and_Benchmarks.pdf)); Unstructured «4 PDF Parsing Strategies»; LlamaParse docs; Neo4j GraphRAG/ontologie; xclingo ([arXiv 2009.10242](https://arxiv.org/abs/2009.10242)); Clingo[DL]/clingcon; LangGraph.
- Verifiche di oggi: [LlamaParse tiers e prezzi](https://developers.llamaindex.ai/llamaparse/general/pricing/index.md) `(sec. per i valori)`; [clingo su PyPI](https://pypi.org/project/clingo/) (5.8.0); [Apache AGE](https://age.apache.org/) (PG16 supportato); LLM-ASPIC+ ([crossref 10.3233/FAIA250981](https://api.crossref.org/works/10.3233%2FFAIA250981)); [«Can LLMs Solve ASP Problems?»](https://arxiv.org/pdf/2507.19749); limiti Neo4j Community ([forum](https://community.neo4j.com/t/limitation-of-neo4j-community-edition/74547)); rassegne GraphRAG vs vector 2026 ([tianpan](https://tianpan.co/blog/2026-04-19-graphrag-vs-vector-rag-architecture-decision), [memx](https://memx.app/blog/graphrag-vs-vector-rag-when-graphs-win/)) `(sec.)`.
- Repo: `apps/orchestration-service/src/application/{arbitro_agent,orchestrator,tutor_agent}.py`, `rule_engine.py`, `config/settings.py`; `API/BoundedContexts/KnowledgeBase/Domain/Services/Enhancements/EntityExtractor.cs`, `.../GraphRetrievalService.cs`, `.../RagPromptAssemblyService.cs`; `API/BoundedContexts/GameManagement/Domain/Entities/LiveGameSession.cs`, `.../Services/MoveValidationDomainService.cs`; `API/BoundedContexts/SharedGameCatalog/Domain/ValueObjects/MechanicCardContent.cs`; `apps/unstructured-service/src/config/settings.py`, `infrastructure/unstructured_adapter.py`; `API/BoundedContexts/DocumentProcessing/Infrastructure/External/{UnstructuredPdfTextExtractor,ExtractionStrategySelector}.cs`; `infra/init/postgres-init.sql`; ADR-006, 007, 051, 059, 071, 083, 084, 088, 090; `docs/for-developers/plans/2026-05-06-sp6-libro-game-migration.md`; `tests/llm-eval/`, `tests/evaluation-datasets/`, `infra/fixtures/rag-*.json`.
