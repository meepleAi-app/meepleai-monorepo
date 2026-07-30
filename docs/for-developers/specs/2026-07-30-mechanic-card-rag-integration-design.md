# Integrazione — Mechanic Card → Agente RAG player-facing

**Data**: 2026-07-30
**Origine**: spec-panel discussion (panel Fowler / Newman / Hohpe / Adzic / Nygard / Wiegers) + review adversariale multi-agente (4 reviewer: facts / consistency / conventions / completeness) applicata 2026-07-30.
**Stato**: proposto — barra di qualità **decisa** (gate `Published`; trust-flag `certified` **deferito**, D5). Decisioni architetturali D1–D3 da ratificare (candidato ADR-088; il numero è libero, ultimo su disco = adr-087).
**Scope**: BC `SharedGameCatalog` (produttore, [ADR-051](../../for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md)) → BC `KnowledgeBase` (consumatore, agente Q&A `/agents/qa` + `/agents/qa/stream`).
**Decisione governante**: [ADR-051](../../for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md) (IP policy + pipeline AI-first), [ADR-084](../../for-claude/architecture/adr/adr-084-mechanic-validation-canonical-shape.md) (validation shape della card).

---

> **Read-before-code**: questo documento definisce come i **claim approvati e pubblicati** del Mechanic Extractor alimentano le risposte dell'agente RAG al giocatore. Regola non negoziabile: il lato `KnowledgeBase` consuma **solo** il contratto pubblicato (`PublishedMechanicCardDto` / `GetPublishedMechanicCardByGameQuery`), **mai** l'aggregate `MechanicAnalysis` né il suo lifecycle. Qualsiasi dato non presente sul contratto pubblicato (es. `CertificationStatus`) va esposto **producer-side** estendendo il contratto, non leggendo l'aggregate da KB. Leggere prima di toccare `AskQuestionQueryHandler`, `StreamQaQueryHandler` o gli endpoint `SharedGameCatalog`.
>
> **Coordinamento**: la citazione precisa via char-offset **non** è in scope qui — appartiene all'epic [#3403](https://github.com/meepleAi-app/meepleai-monorepo/issues/3403) (`docs/superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md`, stesso giorno). Questo spec emette le citazioni claim-based **direttamente** e fa ride-along su quel canale per l'highlight; **R1 non blocca su #3403** (§7.4–§7.5).

---

## 1. Contesto e problema

Due sottosistemi che **non si parlano**, pur condividendo lo stesso substrato (chunk / PDF / gioco):

- **Mechanic Extractor** (BC `SharedGameCatalog`, ADR-051): pipeline **admin-only** che estrae dal regolamento un grafo `MechanicAnalysis → MechanicClaim → MechanicCitation`, lo sottopone a review umana (`Draft → InReview → Published`) e ne pubblica uno snapshot immutabile `MechanicCard`. Ogni claim è **riformulato player-facing**, ancorato a ≥1 citazione verbatim (`PdfPage` + `Quote` ≤25 parole + `ChunkId`) e superato attraverso guardrail di grounding (cosine claim↔chunk ≥0.65).
- **Agente RAG** (BC `KnowledgeBase`): pipeline **player-facing** — embedding query → hybrid search (vector + keyword, RRF) → rerank cross-encoder → prompt → LLM. `MinScore=0.55`, top-5 chunk.

**Il problema misurato** (memoria `project-rag-tm-answer-quality-fixes`): la query "Setup per N giocatori" su Terraforming Mars **fallisce** perché il retrieval pesca la sezione sbagliata (heading mis-detection multi-colonna IT → "PREPARAZIONE" finisce incollato nel body del chunk "CARTE", che ranka ultimo). Cioè: **il RAG grezzo è a bassa precisione proprio sulle sezioni più richieste** (Setup, Victory, Resources, Phases) — e nel caso peggiore il retrieval non supera nemmeno `MinScore`, uscendo con "This information is not available in the provided rulebook".

Il Mechanic Extractor produce **esattamente** conoscenza verificata su quelle sezioni. Questo spec definisce come iniettarla nella pipeline RAG **senza violare i confini dei bounded context** e **senza degradare i giochi non coperti**.

## 2. Substrato condiviso (perché è fattibile e a basso rischio)

| # | Fatto | Evidenza |
|---|---|---|
| S1 | **`MechanicCitation.ChunkId` → `TextChunkEntity.Id`**: ogni claim è già ancorato agli stessi chunk che il RAG recupera. `ChunkId` è `Guid?` con `ON DELETE SET NULL` (regge il re-index). | `SharedGameCatalog/Domain/Entities/MechanicCitation.cs:44`; `MechanicCitationEntityConfiguration.cs:69` |
| S2 | **`MechanicSection` ≈ intent del giocatore**: l'enum sezione mappa quasi 1:1 sui `GameBookRole` che il RAG già deriva come *role hint*. | `MechanicSection.cs:7-21`; `GameBookRole.cs`; `IIntentClassifierService` |
| S3 | **Il contratto pubblicato esiste già** come anti-corruption boundary: `Published` → `MechanicCard` immutabile → `PublishedMechanicCardDto` servito da `GET /api/v1/games/{gameId}/card`. | `PublishedMechanicCardDto.cs:11-43`; `GetPublishedMechanicCardByGameQueryHandler.cs` |
| S4 | **`KnowledgeBase` fa già letture cross-BC best-effort** (`IHouseRuleMatcher` legge AgentMemory in try/catch; `AutoCreateAgentOnPdfReadyHandler` reagisce a un evento di `DocumentProcessing`). | `AskQuestionQueryHandler.cs:158-171`; `AutoCreateAgentOnPdfReadyHandler.cs` |

## 3. Requisiti (Wiegers) e barra di qualità

- **R1 — Claim come sorgente prioritaria di risposta** *(core di questo spec)*: quando esiste una card pubblicata pertinente all'intent, l'agente risponde **con** i claim approvati come contesto autorevole, prima del RAG grezzo — **anche quando il retrieval è vuoto** (§6.1).
- **R2 — Claim come oracolo di verifica** *(fase 2, §17)*.
- **R3 — Card come fast-path d'overview** *(fase 3, §17)*.

**Barra di qualità (decisa)**: gate su `Published` (claim approvati da un admin + card non soppressa). `Certified` **non** entra nella v1 (D5).

## 4. Decisioni + trade-off

| ID | Decisione | Alternativa scartata | Perché |
|---|---|---|---|
| **D1** | Gate = **`Published`** (card attiva non soppressa), non `Certified`. | Hard-gate `Certified`. | Copertura: pochi giochi sono `Certified`. `Published` = già human-approved (tutti i claim `Approved`). *(scelta utente)* |
| **D2** | **v1 = read-time + cache** del query pubblicato esistente. Proiezione event-driven = evoluzione (§6.3). | Proiezione event-driven subito. | La card è **una riga attiva per gioco** (indice unico filtrato `ux_mechanic_cards_active_per_game`, §6.2), il query esiste già e **applica la suppression gratis**. **Differenziatore reale**: il gap "`Unsuppress` non alza evento" (`MechanicCard.cs:187-204`) è **auto-sanato** da un TTL cache (staleness *bounded*), mentre una proiezione materializzata **non si auto-sana** senza quell'evento (staleness *unbounded*). Più: KISS/YAGNI. |
| **D3** | **Seam A** — iniezione a livello di prompt-assembly, per-handler, **non** reranked, **sopra** gli early-exit (§6.1). | Seam B (terzo arm nella fusione RRF). | I claim sono **autorevoli**: non devono poter essere "reranked away". Precedenza controllata. Retrieval intatto → zero regressione sui giochi scoperti. |
| **D4** | Selezione claim **per `MechanicSection`**, via mapping `GameBookRole` → `MechanicSection` (net-new, §8). | Iniettare l'intera card sempre. | Evita prompt-bloat; allinea alla precisione dell'intent. |
| **D5** | **Trust-flag `certified` DEFERITO** oltre la v1. Quando servirà, il campo va aggiunto **producer-side** al contratto pubblicato (boolean derivato in `SharedGameCatalog` da `MechanicAnalysis.CertificationStatus`, o snapshottato in `MechanicCardContent` con bump `schema_version` 2→3) — **mai** letto da KB sull'aggregate. | Derivare `certified` a read-time da KB via `SourceAnalysisId → MechanicAnalysis`. | ⚠️ **Correzione review (BLOCKER)**: leggere `CertificationStatus` (che vive **solo** su `MechanicAnalysis`) da KB **violerebbe** la regola di confine del banner. `PublishedMechanicCardDto` espone solo `SourceAnalysisId`, nessun campo di certificazione. Il flag non è core a R1 → deferito. |
| **D6** | **Citation bridge (char-offset) deferito all'epic #3403**. Le citazioni dei claim (`PdfId`+`PdfPage`+`Quote` verbatim) sono emesse **direttamente** da questo spec (§7.4); l'highlight di regione arriva quando #3403 chiude il surfacing dei `CharStart/CharEnd`. | Surfacing dei `CharStart/CharEnd` qui. | Evita collisione di scope con lo spec pari-data `2026-07-30-rag-citation-region-grounding-design.md` (§5 SP-A / §6.4). |
| **D7** | **Fail-open verso il RAG grezzo**, **fail-closed sulla suppression** *entro la finestra di staleness cache dichiarata* (§10). | Errore quando manca la card. | "Nessun claim pertinente" è il caso **comune**. La garanzia di suppression è *bounded-staleness* (non assoluta) per via delle cache di risposta (§6.2, §10). |
| **D8** | **Precedenza nel prompt**: `house rule` > `claim ufficiale approvato` > `RAG grezzo`. | Claim sopra le house rule. | Le house rule sono override espliciti del gruppo. |

## 5. Contratto d'integrazione (published contract — verificato)

Il consumatore vede **solo** questo (`Application/DTOs/PublishedMechanicCardDto.cs:11-43`):

```
PublishedMechanicCardDto(
  Guid CardId, Guid SharedGameId, string Title, int Version, DateTime PublishedAt,
  string GameName, string? Publisher, string Language,
  IReadOnlyList<PublishedMechanicCardSectionDto> Sections,
  Guid SourceAnalysisId, int? PublicationYear, string? DocumentName)
  // NB: il campo si chiama SourceAnalysisId sul DTO (l'aggregate lo chiama OriginAnalysisId).

PublishedMechanicCardSectionDto(string Section, IReadOnlyList<PublishedMechanicCardClaimDto> Claims)
  // Section = nome dell'enum MechanicSection ("Setup", "Victory", ...)
PublishedMechanicCardClaimDto(Guid Id, string Claim, IReadOnlyList<PublishedMechanicCardCitationDto> Citations)
PublishedMechanicCardCitationDto(Guid PdfId, int PdfPage, string Quote)
```

Fatti di contratto rilevanti:
- **Suppression enforced dal contratto**: `GetPublishedMechanicCardByGameQuery(SharedGameId)` → `IMechanicCardRepository.GetActiveByGameAsync` → global query filter `!IsSuppressed` (`MechanicCardEntityConfiguration.cs:102`). Card soppressa/assente ⇒ `null` ⇒ 404.
- **Endpoint**: `GET /api/v1/games/{gameId:guid}/card`, `.RequireAuthorization()` (**qualunque utente autenticato** — controllo d'accesso più debole del gate RAG per-gioco: vedi §9).
- **`Certified` NON è nel contratto** (D5).

## 6. Architettura R1

### 6.1 🔴 Control-flow: dove iniettare (correzione BLOCKER review)

Entrambi gli handler **cortocircuitano PRIMA dell'assemblaggio del prompt** su due rami che il seam ingenuo mancherebbe — ed è proprio il caso che R1 deve risolvere:

1. **Response-cache hit** — `AskQuestionQueryHandler.cs:193-228` (semantic cache) e `StreamQaQueryHandler.cs:126-165` (`IAiResponseCacheService`) restituiscono una risposta cachata **prima** di costruire il prompt.
2. **No-relevant-context early return** — `AskQuestionQueryHandler.cs:271-296` ritorna "This information is not available…" quando `searchResults.Count==0`; `StreamQaQueryHandler.cs:205-210` emette `NO_RESULTS` e interrompe.

**Ordinamento richiesto** (per handler), il `card-fetch` deve stare **sopra** l'early-exit no-results:

```
1. CanAccessRagAsync (gate accesso per-gioco)         [invariato — §9]
2. ConsumeQuotaAsync (quota; injection è quota-neutral) [invariato — §9]
3. IntentClassifier.ClassifyIntent → GameBookRole      [invariato]
4. card = IMechanicCardProvider.GetActiveCardAsync(...) [NUOVO, best-effort]  <-- SOPRA gli exit sotto
5. Response-cache lookup:
     chiave DEVE includere il fingerprint card (CardId+Version, o "no-card")  [§6.2]
     → hit valido ⇒ replay; hit con fingerprint diverso ⇒ miss (rigenera)
6. Retrieval + rerank (invariato)
7. Early-exit no-results:
     - se retrieval vuoto MA esiste una sezione-claim pertinente ⇒ NON uscire:
       assembla un prompt "claims-only" (context RAG vuoto, blocco Verified Rules presente)
     - se retrieval vuoto E nessun claim pertinente ⇒ comportamento odierno ("not available")
8. Prompt assembly con blocco Verified Rules (§7) + eventuale context RAG
9. LLM + citazioni (claim + chunk) + metriche
```

Senza lo step 7-modificato, per il caso Terraforming Mars (retrieval sotto `MinScore`) la card **non verrebbe mai iniettata** — R1 sarebbe un no-op sul suo caso motivante.

### 6.2 Provider, cache e invariante single-card

- Nuovo seam **`IMechanicCardProvider`** in `KnowledgeBase/Application/Services/` — unica dipendenza verso il contratto del catalogo. Wrappa `mediator.Send(GetPublishedMechanicCardByGameQuery(...))` + cache-aside + `try/catch` best-effort (mirror `IHouseRuleMatcher`, `AskQuestionQueryHandler.cs:158-171`).
- **Store cache = `IHybridCacheService`** (protezione stampede integrata, per convenzione repo [#2620](https://github.com/meepleAi-app/meepleai-monorepo/issues/2620)), **non** Redis GET/SET nudo (che non dedup-a i miss concorrenti su cold-cache dopo un publish). Chiave `mechanic-card:{sharedGameId}`, **TTL 10 min** (valore concreto; bilancia freschezza vs carico), eviction `RemoveAsync` sulla stessa chiave.
- **Invalidazione**: `INotificationHandler<MechanicCardPublishedEvent>` / `<MechanicCardSuppressedEvent>` (già emessi via outbox `OutboxOnly`) che fa **solo** cache-eviction (pattern `PdfStateChangedCacheInvalidationHandler`). ⚠️ **`Unsuppress` non alza evento** → una card ri-abilitata evince solo a scadenza TTL (staleness *bounded*, mitigazione dichiarata).
- **🔴 Cache di RISPOSTA (non solo del provider)**: le risposte sono cachate anche in `ISemanticResponseCache` (AskQuestion, key su `(GameId, queryVector)`, write `:353-367`) e `IAiResponseCacheService` (StreamQa, TTL hard **86400s**, `:503`). Queste **non** sono invalidate dagli eventi card → **la chiave DEVE includere il fingerprint card** (`CardId+Version` o `"no-card"`, step 5 §6.1); così un publish/suppress produce automaticamente un miss e la risposta viene rigenerata. In alternativa (o in aggiunta) l'handler di invalidazione evince anche le cache di risposta del gioco. La finestra di staleness residua è dichiarata in §10 e coperta da AC-3.
- **Invariante single-card (D2)**: al più una card attiva per gioco è garantito dall'indice unico filtrato `ux_mechanic_cards_active_per_game` (`is_suppressed = false`, `MechanicCardEntityConfiguration.cs:77-80`). `GetActiveByGameAsync` ritorna quindi ≤1 riga. Durante un re-publish (suppress-old + publish-new) una lettura transitoria potrebbe osservare **zero** card attive → **fail-open** al RAG grezzo (benigno). Confermare che suppress-old+publish-new sia transazionale è un follow-up del produttore.
- **Identità gioco (AC-7, non assumere)**: `GetPublishedMechanicCardByGameQuery` prende `SharedGameId`; l'handler RAG lavora con `query.GameId`. La memoria indica `text_chunks.GameId == shared_games.id`, ma `TextChunkEntity` ha **anche** `SharedGameId`. **AC-7 (§12)** verifica la coincidenza; se divergono serve un resolver esplicito (cfr. `reference_scoredata_identity_bridge`).

### 6.3 Evoluzione — proiezione event-driven (fuori v1)

Materializzare un read model in `KnowledgeBase` alimentato da `MechanicCardPublishedEvent`/`SuppressedEvent` (mirror `AutoCreateAgentOnPdfReadyHandler` / `SessionFinalizedEventHandler`). **Prerequisiti**: (a) aggiungere un evento su `MechanicCard.Unsuppress` (altrimenti staleness unbounded, cfr. D2); (b) gestire il version-bump; (c) non confondere `MechanicAnalysisSuppressedEvent` (kill-switch analisi, **non** tocca la card) con `MechanicCardSuppressedEvent`.

## 7. Iniezione nel prompt (D3, D8)

### 7.1 Grounding: i claim come contesto riconosciuto (correzione MAJOR review)

Il system prompt attuale (`DefaultSystemPrompt`, `AskQuestionQueryHandler.cs:31-38`) impone verbatim: *"Answer ONLY using the provided rulebook context."*, *"If the context does not contain the answer, respond EXACTLY with: 'This information is not available in the provided rulebook.'"*, *"Never invent rules … not present in the context."*. Se il blocco claim vive **fuori** dalla sezione `Context:` e il retrieval è vuoto, un LLM letterale rifiuterebbe.

**Decisione**: i claim vanno resi **come parte del contesto autorevole**, non come blocco esterno. Due modifiche congiunte:
1. **System prompt** — aggiungere una riga che riconosce le regole verificate come fonte grounded valida: *"Verified Rules marked [Verified Rule] are human-approved rulebook content and count as provided context; you MAY answer from them."*
2. **User prompt** — il blocco Verified Rules è concatenato **prima** del `Context:` RAG ma **entrambi** sotto l'ombrello "contesto". Ordine: `{houseRulePrefix}{verifiedRulesBlock}Question: … \n\nContext:\n{ragContext}` (su AskQuestion, `:311-321`). Così l'ordinamento resta `house rule → claim → RAG` e il vincolo di grounding **è preservato** (i claim sono context, non invenzione).

### 7.2 Formato di rendering (worked example — correzione MAJOR review)

Template esatto per il blocco (un solo blocco, sezioni ordinate come da §8, cap §16):

```
[Verified Rules — human-approved]
## Setup
[V1] I giocatori ricevono 2 carte progetto ciascuno all'inizio. [Page 3]
[V2] In una partita a 3 giocatori si usa la plancia standard. [Page 3]
## Components
[V3] Ogni giocatore prende un set di 40 cubi. [Page 2]
```

Regole di rendering:
- Numerazione `[V1]`, `[V2]`, … **globale** al blocco (distinta dai `[N]` inline del RAG).
- Ogni claim = `[Vk] {Claim} [Page {PdfPage}]` — la pagina in formato `[Page N]` così il system prompt ("Always cite the page number in brackets, e.g. [Page 3]") la ri-emette naturalmente.
- Header di sezione `## {SectionName}` (nome enum).
- **Nessun `certifiedSuffix`** in v1 (D5): l'header è sempre `[Verified Rules — human-approved]`.
- **Vietato concatenare più `Quote` verbatim** in un estratto lungo (vincolo copyright §16): il rendering usa il **`Claim` riformulato**, non la `Quote`; la `Quote` verbatim viaggia solo nella citazione strutturata (§7.4), non nel testo del prompt.
- Cap per-sezione e budget totale in §16.

### 7.3 Streaming — `StreamQaQueryHandler` (asimmetria dichiarata)

⚠️ `StreamQaQueryHandler` **non** inietta `IHouseRuleMatcher` e **non** traduce (retrieval `Language` hardcoded `"en"`, `:320`); il blocco claim va aggiunto in `BuildLlmPromptsAsync` (`:419-458`, context join `:427-428`). Sullo streaming l'ordine è `verified rules → RAG context` (nessuna house rule).

**Caveat lingua (correzione review)**: il caso motivante (§1) è **italiano**. Su streaming il retrieval è `"en"` e non c'è traduzione → un claim IT verrebbe iniettato accanto a retrieval configurato EN. **Scope v1**: l'iniezione claim su StreamQa è abilitata, ma **il beneficio pieno per corpus non-EN sullo streaming richiede** che StreamQa erediti la gestione lingua di AskQuestion (follow-up). "Copre entrambi i path" ≠ parità funzionale per il caso IT sullo streaming; il path **non-streaming** `/agents/qa` è quello che risolve pienamente il caso TM in v1.

### 7.4 Wiring citazioni claim + sequencing con #3403 (correzione MAJOR review)

Oggi le citazioni sono costruite **solo** dai risultati di retrieval (`AskQuestion:574-579` da `searchResults`; `StreamQa:361-375` da `snippets`; `InlineCitationMatcherService.Match` opera **solo** su quella lista). I claim iniettati **non** sono in `searchResults` → senza codice nuovo la citazione claim non emergerebbe (e AC-1 fallirebbe).

**Meccanismo richiesto**:
- Costruire una `CitationDto` (AskQuestion) / `Snippet` (StreamQa) **direttamente** per ciascuna `PublishedMechanicCardCitationDto` iniettata (`DocumentId=PdfId`, `PageNumber=PdfPage`, snippet = `Quote` verbatim), **taggata `source=claim`**, e appenderla alla lista citazioni **prima** del matcher inline.
- Il matcher inline (`[V1]`→citazione claim) mappa i marker `[Vk]` alle citazioni claim; i `[N]` restano sui chunk RAG.
- **Ancora primaria** della citazione = `PdfPage` + `Quote` (robusta al re-index); `ChunkId` è opportunistica (può essere `NULL`).
- **Sequencing #3403**: **v1 emette le `CitationDto` claim-based direttamente e NON blocca su #3403**; l'highlight di *regione* nel PDF (char-offset) arriva quando #3403 chiude il surfacing di `CharStart/CharEnd` — fino ad allora l'highlight resta best-effort substring (comportamento odierno del viewer).

### 7.5 Coordinamento esplicito con l'epic #3403 (citation region grounding)

I due spec, pari-data, toccano lo **stesso canale citazione**. Confine di proprietà per evitare collisione di scope:

| Superficie | Proprietario |
|---|---|
| Shape ed estensioni dei DTO citazione (`CitationDto`/`Snippet`/`InlineCitationMatch`/`ChunkCitation`), `regions[]`, surfacing `CharStart/CharEnd`, copyright-gating tier-Full, overlay FE (`PdfBBoxOverlay`) | **#3403** ([spec §6.4](../../superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md)) |
| Selezione claim per sezione, iniezione nel prompt, **emissione** di citazioni con provenienza `source=claim` **attraverso** la shape di #3403 | **questo spec** |

**Contratto condiviso (3 punti da allineare — tracciati come DC-4 su #3403):**
1. **Discriminatore di provenienza**: le citazioni acquisiscono un campo `Source` (`retrieval` | `claim`). È un requisito di *questo* spec (osservabilità §11 + rendering `[Vk]`), ma la shape DTO è di #3403 → il campo va aggiunto lì, additivo, non forkato.
2. **DocumentId keying**: le citazioni claim portano già `PdfId` diretto (da `PublishedMechanicCardCitationDto.PdfId`) → sono **già** keyate su `PdfDocumentId`, risolvendo gratis il **DC-1** di #3403 per la loro sotto-classe. Devono usare la convenzione documentId che #3403 fissa (`PdfDocumentId`, non `VectorDocumentId`).
3. **Copyright gating (R3/DA-4 di #3403)**: la `Quote` verbatim del claim è già sanzionata dai guardrail dell'estrattore (ADR-051 T1: ≤25 parole, ≤10 consecutive dalla source) → il **testo** è sicuro anche su `Protected`. Ma l'**highlight geometrico di regione** del claim resta gated: `regions[]` solo per `CopyrightTier=Full`; su `Protected` il claim mostra pagina + `Quote` testuale, nessun rettangolo verbatim. Coerenza copyright preservata.

**Sinergia (non conflitto):**
- La `Quote` del claim è **testo verbatim esatto** (≤25 parole) → è l'input **migliore** per il Pattern-A (highlight testuale) di #3403 **SP0**, più affidabile dello snippet RAG a 150 char troncato. Le citazioni claim alzano l'hit-rate del text-highlight.
- L'`InlineCitationMatcherService` che #3403 estende deve gestire **anche** i marker `[Vk]` dei claim (oggi opera solo su snippet da `searchResults`, §7.4) — è la stessa modifica per entrambi.

**Sequencing (decisione):**
- #3403 **SP-A fissa la shape** di `CitationDto`/`Snippet`; il mio R1 la **consuma**, non forka una shape parallela. → **raccomandato: far atterrare SP-A prima di R1.**
- R1 **non blocca** sul lavoro regione (#3403 SP-B/C/D): emette citazioni claim con la 4-arg `CitationDto` esistente + `source=claim`; l'highlight di regione per i claim arriva quando SP-C/SP-D atterrano (via `ChunkId → text_chunks.bounding_boxes_json`, gated tier-Full).
- **Se R1 atterra prima di SP-A**: usa la 4-arg attuale e introduce **solo** il campo `Source`, coordinandolo con #3403 così che SP-A non lo ridefinisca in conflitto. **Se SP-A atterra prima**: R1 popola anche i campi nuovi dove disponibili (`ChunkId`).
- **Rischio merge**: entrambi toccano `CitationDto`, `SearchResultDto.FromDomain`, `Snippet`, `InlineCitationMatcherService` → chi tocca per primo la shape la estende in modo additivo; l'altro rebasa.

## 8. Routing intent → `MechanicSection` (D4)

`IIntentClassifierService.ClassifyIntent` (regex, sync; `AskQuestion:429` / `StreamQa:309`) restituisce un `GameBookRole` `[Flags]` (`None=0, Tutorial=1, RulesReference=2, Narrative=4, Encounter=8, Lore=16, Setup=32`) — **può essere combinato** (es. `Tutorial|Setup`). Mapping **net-new**:

| Flag `GameBookRole` presente | `MechanicSection` aggiunte |
|---|---|
| `Setup` | `Setup`, `Components` |
| `Tutorial` | `Summary`, `Setup` |
| `RulesReference` (default) | `Mechanics`, `Phases`, `Resources` |
| `Encounter` | `Phases`, `Mechanics` |
| `Narrative` / `Lore` | *(nessuna — coperte solo dal RAG)* |

**Risoluzione multi-flag**: **unione** delle sezioni mappate dai flag presenti, deduplicata; poi cap per-sezione + budget totale (§16). **Fallback** (nessun flag mappa alcuna sezione, es. `Narrative` puro): iniettare solo `Summary` se presente, altrimenti nessun blocco → RAG grezzo. §10 (failure table) e §8 usano **lo stesso** fallback ("Summary se presente, altrimenti RAG"). Usare `GameBookRole` (via `IIntentClassifierService`), **non** `AgentIntent` (`MultiAgentRouter`, tassonomia separata non usata dal path Q&A).

## 9. Access control, quota e tenancy (correzione MAJOR review)

- **Invariante di ordine**: `IMechanicCardProvider.GetActiveCardAsync` **DEVE** essere chiamato **dopo** `_ragAccessService.CanAccessRagAsync` (`AskQuestion:137-145`, `StreamQa:98-106`). L'endpoint card è solo `.RequireAuthorization()` (any-auth); una lettura card **prima** del gate RAG per-gioco **esporrebbe** contenuto curato per un gioco a cui l'utente non ha accesso RAG (privilege-escalation). La card eredita così il gate più stretto.
- **Quota-neutral**: l'iniezione claim **non** aggiunge chiamate LLM → **nessun** `ConsumeQuotaAsync` addizionale (`:175` addebita una volta per domanda). Un implementatore non deve raddoppiare l'addebito.

## 10. Failure mode e fallback (D7)

| Condizione | Comportamento |
|---|---|
| Nessuna card pubblicata | `GetActiveByGameAsync` → `null` → **RAG grezzo** (identico a oggi) |
| Card soppressa (percorso non-cachato) | Global query filter → `null` → **RAG grezzo** (fail-closed) |
| **Card soppressa ma ancora in cache** (provider o risposta) | Claim serviti fino a: eviction evento **o** scadenza TTL (provider 10 min; risposta ≤ fingerprint-miss). **Staleness bounded dichiarata** — coperta da AC-3, mitigata dal fingerprint in chiave (§6.2) |
| Lettura cross-BC fallita/timeout | `try/catch` best-effort → log → **RAG grezzo** |
| `Content` JSONB non deserializzabile | handler ritorna `null` → **RAG grezzo** |
| Re-publish concorrente (zero card transitorio) | **fail-open** → RAG grezzo (benigno) |
| Nessuna sezione matcha l'intent | `Summary` se presente, altrimenti **RAG grezzo** (§8) |
| Card ri-abilitata (`Unsuppress`, no evento) | cache stale fino a TTL — mitigazione, non fix (§6.2) |

**Rischio primario (governance del dato)**: servire contenuto **autorevole ma vecchio**. Il trust dell'utente è alto → sorvegliare `Version`+`PublishedAt`; l'epic estrazione [#3338](https://github.com/meepleAi-app/meepleai-monorepo/issues/3338) + re-index rigenerano le card.

## 11. Osservabilità (correzione review — misurabilità)

- **Segnale deterministico** = *claim-block iniettato* (Seam A sa questo, **non** che l'LLM abbia effettivamente attinto dal claim). Metrica primaria: `claim-block-injected` (bool) — non spacciarla per "risposta sourced-from-claim".
  - **Non-streaming**: prefisso su `RagQueryMetrics.Strategy` (già stringa `hybrid|tier:X`, `AskQuestion:281,376`) → `claim|…` / `rag|…`, confluisce in `IRagQualityTracker.TrackQueryAsync`.
  - **Streaming**: ⚠️ `StreamQaQueryHandler` **non** ha `RagQueryMetrics`/`TrackQueryAsync` — usa solo `MeepleAiMetrics.RecordRagRequest/TokensUsed/ConfidenceScore` (`:288-293`). Serve un **tag/counter dedicato** (`meepleai.rag.answer.source={claim|rag}`) su quel sito, per aggregare entrambi i path.
- **Attribuzione reale (opzionale)**: derivarla dalle **citazioni `source=claim` effettivamente emesse** nella risposta (§7.4), non dall'iniezione.
- **Metrica di successo (baseline, correzione review)**: aggiungere la query `tmars-setup-it` al golden set retrieval ([#3385](https://github.com/meepleAi-app/meepleai-monorepo/issues/3385), gate `rag-smoke`) e **richiedere pass con `source=claim`**. "Regressione zero" = parità (prompt-identity / nessun blocco iniettato) sul sottoinsieme golden **senza card**. Dichiarare baseline pass-rate corrente e target prima del merge.

## 12. Acceptance criteria

> Convenzione: le scenario-label sono `**Scenario**` fuori dal fence; il fence inizia da `Given`. Ogni scenario ha una **Soglia** misurabile. `AC-10` ("tutti i claim `Approved` prima del publish") **appartiene ad ADR-051**, non a questo doc.

**AC-1 (Scenario, Adzic): Setup adattato al numero di giocatori — il caso che oggi ROMPE**
```
Given Terraforming Mars ha una MechanicCard Published non soppressa con claim Section=Setup
And il retrieval RAG NON supera MinScore (caso reale TM)
And il giocatore autenticato con accesso RAG chiede "Setup per 3 giocatori"
When AskQuestionQueryHandler elabora la query
Then il card-fetch avviene sopra l'early-exit no-results (§6.1) e il prompt "claims-only" viene assemblato
And la risposta contiene il passo di setup atteso per 3 giocatori
And emette ≥1 citazione source=claim con [Page N] + Quote verbatim
And la metrica registra claim-block-injected=true
```
**Soglia**: oracolo esplicito — il passo di setup atteso (definito nel fixture per player-count) è presente; ≥1 citazione claim-based. La query entra nel golden set e passa.

**AC-2 (Scenario, Adzic): Sezione non coperta — fallback graceful**
```
Given un gioco ha una card con claim solo per {Summary, Mechanics, Victory}
And il giocatore chiede un caso limite d'interazione carte (nessuna sezione pertinente)
When l'handler elabora la query
Then nessun blocco [Verified Rules] viene iniettato
And l'agente risponde via RAG grezzo con prompt IDENTICO al path senza integrazione
And la metrica registra claim-block-injected=false
```
**Soglia**: **prompt-identity** (stessa stringa di prompt del path RAG puro; non byte-equality dell'output LLM, non deterministico) + `source=rag`.

**AC-3 (Scenario, Adzic): Suppression rispettata entro la finestra dichiarata**
```
Given un gioco con MechanicCard Published servita (claim in risposta)
When la card viene soppressa (MechanicCardSuppressedEvent) o assente
Then dopo eviction evento / miss-fingerprint, nessuna nuova risposta contiene claim di quella card
And il percorso non-cachato non serve MAI claim soppressi (global query filter)
```
**Soglia**: 0 claim soppressi serviti dal percorso non-cachato; dal percorso cachato, 0 **dopo** l'eviction/rigenerazione (finestra ≤ TTL provider 10 min / fingerprint-miss). Nessuna garanzia "istantanea assoluta".

**AC-4 (Scenario, Adzic): Precedenza house rule > claim (comportamentale)**
```
Given una house rule che CONTRADDICE una regola coperta da un claim approvato
When il giocatore chiede di quella regola sul path non-streaming
Then il prompt contiene house rule PRIMA del blocco Verified Rules
And la risposta segue la house rule, non il claim
```
**Soglia**: assertion **comportamentale** (la risposta riflette la house rule) oltre all'ordine testuale nel prompt.

**AC-5 (Scenario, Adzic): Cross-BC read failure — mai abortire la Q&A**
```
Given IMechanicCardProvider lancia (timeout/errore del catalogo)
When AskQuestionQueryHandler elabora la query
Then l'eccezione è catturata e loggata
And l'agente risponde via RAG grezzo
```
**Soglia**: 0 richieste Q&A abortite per fault del layer claim.

**AC-6 (Scenario, Adzic): Access-gate — nessun leak cross-tenant**
```
Given un utente SENZA accesso RAG al gioco G
When invoca /agents/qa su G
Then riceve 403 (CanAccessRagAsync)
And IMechanicCardProvider NON viene mai chiamato per G
```
**Soglia**: card-read count = 0 per utenti senza accesso RAG; 403 restituito.

**AC-7 (Scenario, Adzic): Risoluzione identità gioco**
```
Given query.GameId proveniente dal path RAG
When si costruisce GetPublishedMechanicCardByGameQuery
Then il SharedGameId passato risolve alla stessa entità del gioco RAG
```
**Soglia**: per un gioco con card nota, il provider ritorna la card corretta (nessun mismatch id). Se il test fallisce, il resolver id è prerequisito bloccante.

## 13. Testing (correzione review — convenzione #1555)

Ogni AC mappa a un tipo di test + path (`apps/api/tests/Api.Tests`), con **almeno un test handler-driven** che esercita la pipeline reale (non fixture-only DTO, [#1555](https://github.com/meepleAi-app/meepleai-monorepo/issues/1555)):

| AC | Tipo | Dove | Note |
|---|---|---|---|
| AC-1, AC-2, AC-4 | Unit handler-driven su `AskQuestionQueryHandler` con `IMechanicCardProvider` fake | `Api.Tests/.../KnowledgeBase/` | assert sul **prompt assemblato** + metrica `claim-block-injected` |
| AC-3, AC-6 | Integration Testcontainers (card Published/soppressa reale + gate accesso) | `Api.Tests/Integration/` | seed di una card via fixture builder |
| AC-5 | Unit (provider che lancia) | come AC-1 | best-effort fallback |
| AC-7 | Integration (identità `GameId`↔`SharedGameId`) | `Api.Tests/Integration/` | prerequisito |

**Fixture builder richiesto**: un helper `SeedPublishedMechanicCardAsync(sharedGameId, sections[])` che inserisce `MechanicCardEntity` con `Content` JSONB valido (mirror del pattern `SeedAnalysisAsync` dei test lifecycle del Mechanic Extractor).

## 14. Rollout / feature flag / rollback (correzione review)

- **Feature flag**: `KnowledgeBase:MechanicCardInjection:Enabled` (default **false**), letto via `IOptionsMonitor` (pattern già usato nell'handler, `:58/:128`; semantica config per-ambiente [ADR-062](../../for-claude/architecture/adr/adr-062-config-environment-field-semantics.md)). Il flag guarda la chiamata al provider in **entrambi** gli handler.
- **Enablement graduale**: allowlist di giochi (interni/flagship con card certificate) → tutti i giochi con card.
- **Rollback**: flag off **+** eviction delle cache di risposta del/i gioco/i (il flag da solo non purga le risposte già cachate con claim — §6.2).
- **Stato al merge**: flag **off**; abilitazione post-verifica su gioco pilota con card pubblicata.

## 15. Preconditions & dipendenze (correzione review)

- **Esiste almeno una card `Published`?** R1 è verificabile **solo** se la pipeline admin-review ha pubblicato ≥1 card. Dichiarare all'implementazione il conteggio corrente di card `Published` e se TM ne ha una; se zero, R1 dipende **hard** dal produttore (superfici admin ADR-051 / epic [#3338](https://github.com/meepleAi-app/meepleai-monorepo/issues/3338)). Per dev/test: creare una card pubblicata via flusso admin o `SeedPublishedMechanicCardAsync` (§13).
- **#3403** (citation region): **non** bloccante per R1 (§7.4); solo l'highlight di regione ne dipende.
- **Identità id (AC-7)**: prerequisito del read path.

## 16. Blind spot e budget

- **Budget prompt & troncamento**: cap **per-sezione 6–8 claim** *e* budget totale del blocco Verified Rules. Ordine di troncamento sotto pressione: preservare `house rule` → primi N claim (per `DisplayOrder`) → poi ridurre il context RAG. I claim sono autorevoli (D3) e cedono **dopo** il context RAG. Dichiarare il tetto totale di claim iniettati su tutte le sezioni matchate.
- **Multilingua**: `PublishedMechanicCardDto.Language` = lingua del regolamento. Se `ResponseLanguage != card.Language`, tradurre il **`Claim`** (via `IGenericTranslationService`, come `AskQuestion:391-393` sull'answer) ma **MAI** la `Quote` verbatim. `StreamQa` non traduce → claim in lingua originale sullo streaming (§7.3).
- **FAQ overlap**: `Section=Faq` claim vs FAQ native — stesso routing per v1.
- **Copyright**: rendering usa il `Claim` riformulato, non concatena `Quote` (§7.2; ADR-051 T1).

## 17. Fasi successive

- **Fase 2 — R2 oracolo di verifica**: riuso del grounding guardrail (cosine ≥0.65, `GroundingGuardrail`) come verifica post-hoc della risposta RAG.
- **Fase 3 — R3 fast-path card**: per intent d'overview, servire `PublishedMechanicCardDto` (Summary+Mechanics) bypassando l'LLM.
- **Trust-flag `certified` (D5)**: estensione producer-side del contratto pubblicato.

## 18. Riferimenti

- Produttore: [ADR-051](../../for-claude/architecture/adr/adr-051-mechanic-extractor-ip-policy.md), [ADR-084](../../for-claude/architecture/adr/adr-084-mechanic-validation-canonical-shape.md); superfici admin `docs/superpowers/specs/2026-07-08-issue-526-me-m14-admin-review-core-design.md`, `2026-07-12-issue-532-metrics-dashboard.md`, `2026-07-12-issue-534-auto-suppression.md`, `2026-07-12-issue-535-suppression-notification.md`.
- Consumatore/citazioni: `docs/superpowers/specs/2026-07-30-rag-citation-region-grounding-design.md` ([#3403](https://github.com/meepleAi-app/meepleai-monorepo/issues/3403) — **coordinare**, D6/§7.4); `docs/for-developers/specs/2026-06-14-seed-kb-coverage-evaluation.md` (metrica citation-validity); `docs/frontend/mechanic-card-citations.md` (pagina card standalone, superficie distinta).
- Agente/KB link: `docs/for-developers/specs/2026-07-15-agent-builder-domain-invariants.md` (`AgentDefinition.KbCardIds`).
- Codice — produttore: `PublishedMechanicCardDto.cs:11-43`, `GetPublishedMechanicCardByGameQueryHandler.cs`, `SharedGameCatalogUserEndpoints.cs:30-99`, `MechanicCard.cs:146-204`, `MechanicCardEntityConfiguration.cs:77-102`, `MechanicSection.cs:7-21`, `Domain/Events/MechanicCardPublishedEvent.cs`, `MechanicCardSuppressedEvent.cs`.
- Codice — consumatore: `AskQuestionQueryHandler.cs:31-38,137-145,158-172,193-228,271-296,309-324,331,353-367,383-412`, `StreamQaQueryHandler.cs:98-106,126-165,205-210,264-293,320,419-458,503`, `SearchQueryHandler.cs:101-240`, `IIntentClassifierService.cs`/`IntentClassifierService.cs:13-52`, `GameBookRole.cs`, `SearchResultDto.cs:9-71`, `Observability/Metrics/MeepleAiMetrics.Rag.cs`.

---

> **Prossimo passo suggerito**: aprire una issue di tracking; valutare la ratifica di D1–D3 in **ADR-088 — "Mechanic Cards as RAG Retrieval Source"** (numero libero verificato). Se ADR-088 viene creato, aggiungerne la riga in `docs/for-claude/architecture/adr/README.md` nello stesso PR (la README-indice è stale — evitare di propagarne il drift).
