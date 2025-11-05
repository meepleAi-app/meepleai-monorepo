Ecco i principali casi d’uso che emergono dall’analisi del progetto MeepleAI.  Ogni caso include un esempio, il flusso delle azioni dell’utente e delle chiamate fra frontend e backend, oltre ai flussi interni al backend e le comunicazioni con altri servizi.

### 1. Autenticazione e gestione utenti

**Scenario**: un nuovo utente si registra, effettua l’accesso e usa l’applicazione.

* **Azioni dell’utente**: dalla home (`/`) l’utente compila un modulo di registrazione/login e invia i dati.
* **Chiamate frontend → backend**:

  1. **Registrazione** – il client chiama `POST /auth/register` passando email, password, nome e ruolo; il backend restituisce l’utente e scadenza della sessione.
  2. **Login** – il client chiama `POST /auth/login` con le credenziali e riceve i dati dell’utente e la scadenza.
  3. **Check sessione** – il client chiama `GET /auth/me` per verificare la sessione.
  4. **Logout** – `POST /auth/logout` invalida la sessione.
* **Flusso interno al backend**: il middleware di autenticazione controlla se la richiesta possiede un’API Key o un cookie di sessione. Nel caso di cookie il middleware interroga la tabella `user_sessions` e restituisce un `ClaimsPrincipal` con il ruolo dell’utente; in caso di mancanza o invalidità il middleware risponde con 401.
* **Integrazioni**: ruoli (Admin, Editor, User) e rate‑limiting differenziati per ruolo; le sessioni sono memorizzate in `user_sessions` e vengono revocate automaticamente da un servizio in background.

### 2. Chat multi‑sessione con agenti RAG (QA, Explain, Setup)

**Scenario**: un utente accede alla chat per porre domande o richiedere spiegazioni sulle regole di un gioco.

* **Azioni dell’utente**: seleziona un gioco (`/games`), sceglie un agente (QA/Explain/Setup), crea una nuova chat o ne riapre una esistente e invia domande.
* **Chiamate frontend → backend**:

  1. `GET /games` recupera la lista di giochi.
  2. `GET /games/{gameId}/agents` restituisce gli agenti disponibili.
  3. `POST /chats` crea una nuova chat per il gioco e l’agente scelto; `GET /chats/{chatId}` recupera la cronologia.
  4. `POST /agents/qa`, `/agents/explain` o `/agents/setup` inviano la domanda/argomento e facoltativamente il `chatId`; la risposta contiene l’answer, eventuali snippet, token counts e id del messaggio.
  5. `POST /agents/feedback` registra un feedback (helpful / not‑helpful) sul messaggio.
* **Flusso interno al backend**:

  * Il servizio di chat (`ChatService`) registra la richiesta e la relazione con il gioco e l’agente.
  * Per le domande, il servizio RAG effettua l’embedding della query, interroga Qdrant per i 5 chunk più rilevanti e costruisce un prompt per LLM; la risposta contiene snippet con pagine e linee del PDF.
  * Per risposte lunghe, il servizio `StreamingQA` invia i dati tramite Server‑Sent Events (SSE), gestendo stati (ricerca, generazione), citazioni e token in streaming. Se la risposta è già in cache (Redis), il flusso indica un cache hit prima di fornire le citazioni e lo stream.
  * Tutte le richieste vengono registrate da `AiRequestLogService` per essere mostrate in dashboard amministrativa.
* **Integrazioni**: servizi di embeddings (`EmbeddingService`) che chiamano l’API di OpenRouter; Qdrant per lo storage vettoriale; Redis per cache; database PostgreSQL per cronologia chat e log delle richieste.

### 3. Caricamento e elaborazione PDF con generazione del RuleSpec

**Scenario**: un editor carica un regolamento in PDF e ottiene una rappresentazione strutturata (RuleSpec) da usare nelle risposte AI.

* **Azioni dell’utente**: dall’interfaccia “Upload PDF” (`/upload`) l’utente sceglie o crea un gioco, seleziona il file e avvia il wizard.
* **Chiamate frontend → backend**:

  1. `GET /games` e `POST /games` per selezionare/creare il gioco.
  2. `POST /ingest/pdf` invia il file e il `gameId`; il server salva il file e crea un record.
  3. Il frontend esegue polling di `GET /pdfs/{pdfId}/text` per monitorare lo stato (`pending`, `processing`, `completed`, `failed`).
  4. Una volta completato, `GET /games/{gameId}/rulespec` restituisce il RuleSpec generato per revisione. L’utente può modificare il JSON nel live editor e salvare le modifiche tramite `PUT /games/{gameId}/rulespec`.
  5. Se abilitato, l’editor può infine indicizzare il PDF in Qdrant chiamando `POST /ingest/pdf/{pdfId}/index`.
* **Flusso interno al backend**:

  * Dopo l’upload, il file viene validato (tipo MIME, dimensione) e salvato su file system; viene creato un record nel DB.
  * Un processo di background (`PdfService`) estrae il testo via Docnet.Core, suddivide in chunk di 512 caratteri con overlap 50, produce un RuleSpec (lista di regole con id, testo e riferimenti a pagina/linea) e lo salva nel DB.
  * L’indicizzazione estrae i chunk, genera embeddings usando OpenRouter e li inserisce in Qdrant; i metadati (numero di chunk, caratteri, modello embedding) vengono salvati.
* **Integrazioni**: sistemi di storage (file system e database), servizi di extraction (Docnet.Core, iText7 per tabelle/diagrammi – funzionalità future), embedding e Qdrant per la ricerca, Redis per caching.

### 4. Editor RuleSpec e gestione versioni

**Scenario**: un admin o editor aggiorna il RuleSpec generato e consulta la cronologia delle versioni.

* **Azioni dell’utente**: apre la pagina `/editor?gameId=…`, modifica il JSON con anteprima e salva; può consultare la pagina `/versions` per vedere lo storico e il diff.
* **Chiamate frontend → backend**:

  1. `GET /games/{gameId}/rulespec` per caricare il RuleSpec corrente.
  2. `PUT /games/{gameId}/rulespec` per salvare le modifiche.
  3. `GET /games/{gameId}/rulespec/history` per la lista versioni; `GET /games/{gameId}/rulespec/versions/{version}` per una versione specifica; `GET /games/{gameId}/rulespec/diff?from=v1&to=v2` per il diff.
* **Flusso interno al backend**: il RuleSpec viene salvato in una tabella versionata; ogni modifica crea una nuova versione con timestamp, userId e commenti. Il diff viene calcolato e restituito al frontend secondo il formato JSON di changes.
* **Integrazioni**: database per storicizzare versioni; editor implementato su front‑end con validazione JSON, undo/redo e preview.

### 5. Dashboard amministrativa e osservabilità

**Scenario**: un amministratore analizza l’utilizzo dell’app, controlla le richieste AI e monitora la salute dei servizi.

* **Azioni dell’utente**: accede alla pagina `/admin` o `/logs`.
* **Chiamate frontend → backend**:

  1. `GET /admin/requests` con parametri (limit, offset, endpoint, userId, gameId, data inizio/fine) per la tabella delle richieste; `GET /admin/stats` per statistiche aggregate.
  2. `GET /health`, `/health/ready` e `/health/live` per verificare lo stato di Postgres, Redis, Qdrant e del servizio API.
  3. `GET /admin/n8n` e relative chiamate CRUD per configurare integrazioni n8n.
* **Flusso interno al backend**:

  * `AiRequestLogService` calcola aggregati (totale richieste, latenza media, token consumati, success rate, breakdown per endpoint e feedback).
  * I controller di health check interrogano i vari servizi (PostgreSQL, Redis, Qdrant) e restituiscono lo stato e la durata delle verifiche.
  * La configurazione n8n viene salvata in DB con chiave API cifrata; l’endpoint `/admin/n8n/{configId}/test` verifica la connessione.
* **Integrazioni**: sistemi di osservabilità come Seq (logging), Jaeger (tracing), Prometheus & Grafana (metriche); l’applicazione esporta log, trace e metriche tramite OpenTelemetry e Serilog.

### 6. Agente scacchi e webhook n8n

**Scenario A – Chat scacchi**: l’utente usa la chat dedicata (in pianificazione) per chiedere consigli su mosse. Il frontend invia `POST /agents/chess` con domanda e posizione FEN opzionale; il backend usa `ChessAgentService` e `ChessKnowledgeService` per generare l’analisi e suggerire mosse.

* **Esempio di risposta**: `answer`, `analysis` (valutazione della posizione), `suggestedMoves`, `sources` e metadati (token, modello, confidenza).
* **Flusso interno**: se `chatId` è fornito, la richiesta viene registrata nella cronologia e loggata; il servizio RAG effettua ricerche nella knowledge base scacchistica (indicizzata con Qdrant) e invia la domanda a LLM.
* **Integrazioni**: Chess knowledge index (`POST /chess/index`, `GET /chess/search`); `AiRequestLogService` per logging.

**Scenario B – Webhook n8n**: un workflow n8n espone un webhook `/webhook/chess` che inoltra la richiesta all’API MeepleAI e ritorna un payload standardizzato.

* **Flusso**:

  1. un client esterno chiama il webhook n8n (`POST /webhook/chess`) con `question` e FEN opzionale;
  2. il nodo di validazione controlla la presenza di `question` e FEN (se presente);
  3. un nodo HTTP invia la richiesta a `POST /agents/chess` includendo la session cookie in header o usando un account di servizio;
  4. n8n trasforma la risposta in un payload standardizzato con metadati e la inoltra al chiamante.
* **Integrazioni**: la configurazione n8n viene gestita via endpoints amministrativi; l’API raccomanda di usare un account di servizio e session cookie in n8n per autenticare le richieste.

### 7. Ricerca vettoriale e caching

**Scenario**: un editor o admin indicizza un PDF e successivamente gli utenti effettuano ricerche RAG.

* **Azioni dell’utente**: dopo l’indicizzazione (`POST /ingest/pdf/{pdfId}/index`) i contenuti diventano ricercabili; le domande degli utenti nelle chat vengono trasformate in embeddings e confrontate con i vettori in Qdrant.
* **Flusso interno**: l’`EmbeddingService` genera embeddings (modello `text‑embedding‑3‑small`, dimensione 1536); `QdrantService` esegue la ricerca top‑k e restituisce i chunk con score > 0,7; `LlmService` genera la risposta usando i chunk come contesto; le risposte identiche vengono memorizzate in cache Redis con chiave `gameId:query:hash` e TTL configurabile; il servizio SSE verifica la cache per ridurre i tempi di risposta.

### 8. Integrazione n8n e automazioni

**Scenario**: l’azienda integra MeepleAI con n8n per orchestrare flussi (es. inviare notifiche a Slack quando viene caricata una nuova regola o generare report periodici).

* **Chiamate**: `GET /admin/n8n`, `POST /admin/n8n` e affini per gestire configurazioni n8n; `POST /webhooks/n8n/chess` (in pianificazione) per ricevere eventi esterni e attivare l’agente scacchi.
* **Flusso interno**: il backend memorizza e cifra le API key n8n; un workflow n8n può autenticarsi come servizio, richiamare gli endpoint dell’API MeepleAI e trasformare le risposte; gli errori e lo stato vengono registrati.

### 9. Osservabilità e salute del sistema

**Scenario**: i DevOps monitorano l’applicazione per garantire affidabilità.

* **Chiamate**: `GET /health`, `/health/ready`, `/health/live` restituiscono lo stato di database, Redis e Qdrant con durata delle verifiche;
  metriche di latenza, token e successo sono raccolte e rese disponibili via Prometheus e visualizzate su Grafana.
* **Flusso interno**: log strutturati con `X-Correlation-Id` vengono inviati a Seq; trace via OpenTelemetry a Jaeger; metriche a Prometheus. L’admin può filtrare i log per userId, endpoint o correlation ID attraverso Seq.

### 10. Altri casi d’uso futuri

Il progetto prevede miglioramenti come: una UI dedicata per la chat scacchi con scacchiera interattiva; caching delle sessioni e risposta RAG in Redis; editor RuleSpec avanzato con drag & drop; streaming SSE per risposte lunghe dell’agente Explain; full‑text search ibrida (vector + PostgreSQL). Questi casi d’uso estenderanno i flussi sopra descritti.

---

In sintesi, MeepleAI combina un frontend Next.js con un backend ASP.NET Core, servizi di embedding, vector search, log e caching. Gli utenti possono autenticarsi, caricare regolamenti, generare e modificare schemi di regole, interrogare agenti AI specializzati e monitorare l’utilizzo.  I flussi interni includono pipeline di upload‑estrazione‑indicizzazione, RAG search con caching e streaming SSE, gestione sessioni duale e integrazione con n8n e strumenti di osservabilità.
