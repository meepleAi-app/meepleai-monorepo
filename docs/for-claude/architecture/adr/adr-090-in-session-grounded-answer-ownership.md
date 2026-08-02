# ADR-090 — Ownership della risposta grounded in-sessione: KnowledgeBase owner, SessionTracking consumer

**Date**: 2026-08-02
**Status**: Accepted — ratifica la relazione Customer/Supplier già **in vigore** dopo #3390 Slice 2 e ne fissa la direzione di consolidamento (ritiro della duplicazione controllata).
**Issue**: #3390 (epic "unificare la risposta dell'agente in-sessione dietro un unico contratto RAG grounded") Slice 5 — parte del programma-ombrello #3397; finding C1/C10 dell'audit `docs/for-developers/audits/2026-07-29-in-session-agent-grounding-audit.md`.
**Related**: ADR-089 (SSOT sessione/scoring — dichiara che #3390 possiede la *risposta grounded*, non lo scoring; **prerequisito di lettura**), ADR-083 (convergenza aggregati live; chat-RAG keyed su `LiveGameSession.Id`), #3388 (contratto `groundingStatus`), #3389 (`RetrievalPolicy.LiveSession`).

## Context

L'audit del 2026-07-29 (§3, consenso 5/5) ha diagnosticato che *"il confine è tracciato lungo la costura sbagliata: la modalità di input ha leakato fino a biforcare la Published Language"*. Due bounded context esponevano **due contratti divergenti** per la **stessa** capacità di dominio — *"rispondi alla domanda dell'utente sulle regole"*:

- `KnowledgeBase` → `ChatWithSessionAgentCommandHandler` (RAG con citazioni, path testo, SSE streaming).
- `SessionTracking` → `AskSessionAgentCommandHandler` (LLM multimodale **senza** retrieval, path immagine, non-streaming) — silenziosamente non-grounded.

Gli slice 1–3 di #3390 hanno chiuso il gap **funzionale**:
- **Slice 1** (#3480): osservabilità del grounding (`meepleai.agent.response.grounding`).
- **Slice 2** (#3484): il path immagine ora consuma il retrieval grounded di KnowledgeBase via una query MediatR (`AskGroundedSessionQuery` → `GroundedSessionAnswerDto`), dietro flag `rag.live-image-retrieval`, con budget latenza + fallback.
- **Slice 3** (#3485): query di retrieval derivata dalla vision quando il turno è senza testo.

La relazione **Customer/Supplier** è quindi **già in vigore**: SessionTracking consuma KnowledgeBase via `IMediator.Send`, mai iniettando un servizio KB (regola DDD `CLAUDE.md`: *"Direct service injection (use MediatR)"*), e riceve un DTO pubblico — **non** i modelli `internal` `AssembledPrompt`/`ChunkCitation`. Ciò che resta **non formalizzato** — e che questo ADR fissa — è: (a) la dichiarazione esplicita di **ownership**, e (b) la direzione per ritirare la **duplicazione controllata** che lo Slice 2 ha consapevolmente introdotto (dichiarata nel doc-comment di `AskGroundedSessionQueryHandler`).

Questo ADR non introduce codice: fissa il contratto architetturale, ancorato al codice reale (i `file:line` sotto sono stati letti/scritti in #3390, non inferiti).

## La duplicazione verificata

La generazione della risposta **grounded** esiste oggi in **due** handler, entrambi in `KnowledgeBase`, con lo **stesso** pipeline logico e **due** shape di orchestrazione (streaming vs one-shot):

| Passo del pipeline grounded | `ChatWithSessionAgentCommandHandler` (SSE, path testo) | `AskGroundedSessionQueryHandler` (one-shot, path immagine, #3390 Slice 2) |
|---|---|---|
| Assemble prompt + retrieval (`RetrievalPolicy.LiveSession`, enhancement OFF) | `:246-259` (`AssemblePromptAsync`) | `AssemblePromptAsync(..., RetrievalPolicy.LiveSession)` |
| Resolve copyright tier | `:266-267` (`ICopyrightTierResolver.ResolveAsync`) | idem |
| Generate LLM (text-only) | `_llmService.GenerateCompletionStreamAsync` (stream) | `_llmService.GenerateCompletionAsync` (one-shot) |
| Copyright leak guard (fail-open scan, sanitize fail-closed) | `:503-557` | replicato (sanitize fuori dal try, fix review Slice 2) |
| Map `ChunkCitation` → `CitationDto` (tier-nulling) | `:651-663` | replicato identico |
| Grounding da citation count (#3388) | `:715` (`count > 0 ? Grounded : Ungrounded`) | idem |
| Confidence | `:739` (`RagPromptAssemblyService.ComputeConfidence`) | idem |

**File**: `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`; `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Queries/AskGroundedSessionQueryHandler.cs`. Contratto pubblico consumato da SessionTracking: `AskGroundedSessionQuery.cs` / `GroundedSessionAnswerDto.cs` (`Api.Models.CitationDto` wire shape), invocato da `SessionTracking/Application/Commands/ChatCommandHandlers.cs` (`AskSessionAgentCommandHandler.Handle`) via `_mediator.Send`.

**Rischio concreto della duplicazione** (motivo del ritiro): la review avversariale dello Slice 2 ha trovato un difetto di **sicurezza copyright** — la sanitizzazione del leak dentro il `try` fail-open poteva spedire testo verbatim se il sanitize lanciava. Il fix è stato applicato **solo** in `AskGroundedSessionQueryHandler`; lo stesso pattern nel path SSE è corretto per costruzione (iterator constraints) ma la lezione è generale: un difetto di correttezza sul pipeline grounded va corretto in *N* posti finché la logica è duplicata. Finché coesistono due copie, ogni fix/feature (nuovi enhancement dello Slice 4, copyright, citazioni) rischia di divergere.

## Decisione

### 1. Ownership dichiarata (Customer/Supplier)

`KnowledgeBase` è il **proprietario unico e autoritativo** della capacità *"risposta grounded sul rulebook"* (retrieval + citazioni + `groundingStatus`). Coerente con ADR-089 (tabella responsabilità → SSOT, riga *"Chat-RAG con citazioni"* → `KnowledgeBase`, keyed su `LiveGameSession.Id`).

`SessionTracking` è **consumer** di quella capacità per il path in-sessione (immagine/testo). Vincoli di consumo (già rispettati, qui ratificati):
- Consumo **solo** via `IMediator` (nessuna injection di servizi KB cross-BC).
- Confine di Published Language = **DTO pubblico** (`GroundedSessionAnswerDto`, `Api.Models.CitationDto`). SessionTracking **non** referenzia mai i modelli `internal` di KB (`AssembledPrompt`/`ChunkCitation`).
- SessionTracking possiede sessione, stato-tavolo (vision), persistenza chat-message, budget/fallback e la scelta di path; **non** possiede la generazione grounded.

### 2. Direzione — consolidare il pipeline grounded in un servizio KB condiviso

La generazione grounded (assemble → tier resolve → generate → leak guard → map citazioni → grounding/confidence) va estratta in **un unico servizio di applicazione interno a KnowledgeBase** (working name `IGroundedAnswerService`), consumato da **entrambi** gli handler:
- `ChatWithSessionAgentCommandHandler` (adapter **streaming**: thread/AgentSession, broadcast live, summary — resta owner di questi concern).
- `AskGroundedSessionQueryHandler` (adapter **one-shot**: query dedicata).

Il servizio incapsula il pipeline e i suoi invarianti (LiveSession policy, CRAG web OFF, leak-guard fail-closed sul sanitize, grounding da citation count #3388). I due handler diventano **adapter sottili** sulla forma di consegna (stream di token vs risposta unica), non due implementazioni del pipeline.

**Questo ADR dichiara la direzione, non esegue il refactoring** (vedi Follow-up). Il refactoring è a scope chiuso e a rischio contenuto: il pipeline non-streaming è già isolato in `AskGroundedSessionQueryHandler` (Slice 2), quindi il consolidamento parte da lì; il path SSE ha un vincolo iterator che l'estrazione deve rispettare (la generazione streaming resta nel handler, il servizio espone i passi non-streaming attorno al token-stream).

### 3. Fuori scope

- **Scoring**: ADR-089 è esplicito — #3390 riguarda la *risposta dell'agente*, **non** lo scoring. Questo ADR non tocca `RoundScores`/`ScoreData`/`ScoreEntry`.
- **Rimozione del path multimodale-puro**: il fallback multimodale (quando il retrieval fallisce/scade o l'immagine non è coperta dal rulebook) resta e resta legittimamente `Ungrounded` (#3388). L'ownership collapse **non** elimina il fallback; unifica la sola generazione *grounded*.

## Consequences

### Positive
- Un solo posto possiede il pipeline grounded → un fix di correttezza/sicurezza (es. leak-guard) vive in un punto solo; gli enhancement dello Slice 4 si cablano una volta.
- La Published Language è dichiarata: SessionTracking non può accidentalmente accoppiarsi ai modelli interni di KB.
- Sblocca lo Slice 4 (enhancement live-path) su una superficie unica, non su due copie divergenti.

### Negative / debt
- Il refactoring del servizio condiviso è debito reale, qui **dichiarato ma non eseguito**. Finché non è fatto, la duplicazione controllata resta e va tenuta allineata a mano (un fix sul pipeline grounded va verificato in entrambi gli handler).
- L'estrazione deve accomodare due forme di consegna (streaming vs one-shot) senza rompere il vincolo iterator del path SSE: complessità non banale, da fare con test di parità di comportamento tra i due path.

### Neutral
- Nessun cambiamento di schema, DTO wire o API. Nessun impatto su test esistenti. I due flag di #3390 (`rag.live-image-retrieval`, `rag.live-vision-query-expansion`) restano default OFF: il comportamento in produzione è invariato.

## Follow-up (tracciati, non parte di questo ADR)
- **Refactoring `IGroundedAnswerService`**: estrarre il pipeline grounded condiviso; far diventare i due handler adapter (streaming/one-shot); test di parità di comportamento. Da aprire come issue di cleanup figlia di #3390.
- **Slice 4** (#3390) — enhancement live-path per-enhancement, misurati vs baseline su staging (citation-accuracy → 0.80, no regressione, CRAG web OFF): da cablare **sul servizio consolidato**, non sulle due copie.
- Follow-up minori ereditati dallo Slice 2: `RetrievalBudgetMs` vive nel namespace KB (`SessionAgentOptions`) mentre il budget è concern SessionTracking (mild BC-coupling, LOW); `wikidata-sse.yml` non montato in `docker-compose.yml` dev (pre-esistente).

## References
- Epic #3390; audit `docs/for-developers/audits/2026-07-29-in-session-agent-grounding-audit.md` (finding C1 — doppio backend chat in-session; §3 lente 🏗️ architettura); programma-ombrello #3397.
- PR #3480 (Slice 1), #3484 (Slice 2), #3485 (Slice 3).
- ADR-089 (SSOT sessione/scoring; tabella responsabilità → SSOT, riga Chat-RAG → KnowledgeBase), ADR-083 (chat-RAG keyed su `LiveGameSession.Id`), #3388 (`groundingStatus`), #3389 (`RetrievalPolicy.LiveSession`).
- Codice: `KnowledgeBase/Application/Commands/ChatWithSessionAgentCommandHandler.cs`, `KnowledgeBase/Application/Queries/{AskGroundedSessionQuery,AskGroundedSessionQueryHandler}.cs`, `KnowledgeBase/Application/DTOs/GroundedSessionAnswerDto.cs`, `SessionTracking/Application/Commands/ChatCommandHandlers.cs`.
- CLAUDE.md § Known Pitfalls (pointer a questo ADR).
