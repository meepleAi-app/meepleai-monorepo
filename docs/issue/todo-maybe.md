1. Refactoring RagService e gestione configurazioni

Titolo: Refactor RagService: estrazione logica condivisa e centralizzazione configurazioni RAG

Descrizione: RagService contiene molta logica duplicata (caricamento dei parametri TopK/MinScore, logging, caching, tracing). Creare classi helper (es. RagConfigProvider, RagExecutionWrapper) per centralizzare la lettura delle configurazioni e la gestione comune (metriche, trace, caching) ed eliminare la ripetizione di codice nei metodi di Q&A, explain e hybrid search.

Attività:

Introdurre RagConfigProvider che gestisca fallback su DB/appsettings/default.

Implementare RagOperationRunner (o middleware) che esegua la pipeline con logging, metriche e gestione errori.

Modificare RagService per utilizzare questi helper in tutti i metodi.

Aggiornare i test unitari per le funzioni di RAG coprendo i nuovi helper.

Test:

Unit: testare RagConfigProvider per i vari scenari di fallback; testare RagOperationRunner verificando che metriche e trace vengano invocati.

Integrazione: assicurarsi che RagService ritorni risultati equivalenti (con e senza refactoring) per i principali endpoint AI.

E2E: tramite test API (/api/v1/agents/qa, /explain, /agents/custom-system-prompt), verificare che le risposte rimangano corrette e che le metriche vengano registrate.

2. Implementazione statistiche Top Questions per AiResponseCache

Titolo: Implementare statistiche Top Questions in AiResponseCacheService

Descrizione: Nel servizio di caching è presente un TODO per raccogliere statistiche sulle domande più frequenti. Implementare una raccolta di contatori per query normalizzate (per esempio, using un contatore in Redis) e fornire un metodo per recuperare la classifica.

Attività:

Definire schema (chiave/value) in Redis o DB per contare le richieste.

Aggiornare AiResponseCacheService per incrementare il contatore per ogni richiesta cache miss/hit.

Aggiungere endpoint interno (o metodo) per leggere le statistiche.

Aggiornare la documentazione.

Test:

Unit: test sul contatore incrementale e su normalizzazione delle query.

Integrazione: simulare richieste di caching e verificare che i contatori vengano aggiornati.

E2E: tramite API QA, generare domande ripetute e verificare che la classifica Top Questions contenga i conteggi corretti.

3. Tracking utilizzo API Key

Titolo: Tracciare utilizzo e quota delle API Key

Descrizione: Completare il TODO in ApiKeyManagementService per monitorare numero di chiamate e quota per ciascuna API key. Salvare i log nel database e fornire endpoint per visualizzarne l’uso.

Attività:

Aggiungere campi usageCount, lastUsedAt nella tabella ApiKey.

Implementare middleware nella pipeline API che, per ogni chiamata autenticata con API key, incrementi il contatore.

Creare endpoint admin /api/v1/admin/api-keys/:id/usage per visualizzare utilizzo e quota.

Aggiornare interfacce di ritorno nelle API.

Test:

Unit: test su incremento contatore e aggiornamento lastUsedAt.

Integrazione: test sulla pipeline API per verificare che la chiamata aggiorni i contatori.

E2E: test admin che visualizza l’utilizzo di una API key (autenticazione, controllo permessi).

4. Integrazione parser PDF in RuleSpecService

Titolo: Integrare parser PDF (Tabula/Camelot) per RuleSpec normalization

Descrizione: Oggi RuleSpecService non normalizza tabelle complesse e rimanda a un TODO. Integrare un parser esterno (Tabula o Camelot) per estrarre le regole e normalizzarle in RuleAtom.

Attività:

Valutare libreria (Tabula o Camelot) da usare server-side (via Docker).

Creare modulo che prenda PDF e restituisca tabelle/righe strutturate.

Collegare il modulo nel flusso PdfStorageService → RuleSpecService.

Aggiornare schema RuleAtom se necessario.

Test:

Unit: test del parser su PDF con tabelle note per verificare la corretta estrazione.

Integrazione: upload di PDF contenente tabelle per verificare che la generazione di RuleSpec includa campi sezione/pagina/linea.

E2E: flusso completo upload→parse→review su UI, verificando la corretta visualizzazione delle regole.

5. Refactoring PdfStorageService: separazione responsabilità

Titolo: Refactor PdfStorageService in moduli indipendenti

Descrizione: PdfStorageService gestisce upload, estrazione, chunking, embedding, indicizzazione e progress. Separare le responsabilità in più servizi (es. PdfValidationService, TextExtractionService, EmbeddingService, IndexingService) e orchestrare con coda o pipeline.

Attività:

Estrarre validazione e salvataggio file in PdfValidationService.

Spostare estrazione testo e segmentazione in TextExtractionService.

Utilizzare EmbeddingService già esistente per generare embedding e QdrantService per indicizzare.

Introdurre orchestratore asincrono (es. coda in processi background) che coordini le fasi, aggiornando lo stato in DB.

Test:

Unit: test di ciascun servizio per validazione, estrazione e indicizzazione.

Integrazione: test sull’orchestratore che verifica il corretto passaggio di stato e il salvataggio in DB.

E2E: upload da UI: verificare che l’avanzamento e la pubblicazione funzionino come prima.

6. Refactoring ChatProvider e gestione stato

Titolo: Modularizzare ChatProvider e implementare state management evoluto

Descrizione: ChatProvider gestisce tutto lo stato della chat (auth, giochi, agenti, messaggi, UI), rendendo la componente monolitica e difficile da testare
github.com
. Dividere la logica in più provider/hook e considerare l’uso di uno state machine o di una libreria (Zustand/Redux) per gestire gli stati complessi.

Attività:

Estrarre AuthProvider per gestire autenticazione e utente attuale.

Creare GameProvider/AgentProvider che gestiscano giochi e agenti.

Creare ChatMessagesProvider per stato delle chat e messaggi, con useReducer o XState per gestire transizioni (invio, modifica, eliminazione).

Aggiornare i componenti ChatSidebar, ChatContent a usare i nuovi hook.

Aggiornare test.

Test:

Unit: test su ciascun reducer/hook (transizioni di stato).

Integrazione: test per verificare che la selezione di gioco/agente carichi correttamente chat e agenti e che l’editing elimini/invalida messaggi come da requisito
github.com
.

E2E: test end‑to‑end sull’intero flusso chat: autenticazione, creazione chat, invio messaggi, editing, cancellazione, esportazione (feature flag abilitata).

7. Refactoring pagina Upload PDF

Titolo: Suddividere la pagina upload.tsx in componenti Step e utilizzare hook di upload

Descrizione: La pagina upload.tsx è molto lunga e mescola logica di stato, chiamate API, polling e UI
github.com
. Suddividerla in sotto‑componenti per ciascuna fase del wizard e spostare la logica di upload/parsing in hook riutilizzabili (useUploadQueue).

Attività:

Creare componenti UploadStep, ParseStep, ReviewStep, PublishStep che ricevano gli state tramite hook.

Estrarre il polling del processing e la gestione della coda di upload in un hook (usePdfProcessing) che restituisca status, progress, error e funzioni retry, cancel.

Integrare MultiFileUpload e UploadQueue spostando la logica di retry e progress nel hook.

Aggiornare test E2E.

Test:

Unit: test per i nuovi hook (usePdfProcessing) simulando diversi stati (pending, processing, failed).

Integrazione: test su ogni Step component verificando passaggio di stato e rendering.

E2E: caricare un PDF e seguire tutto il wizard su interfaccia, includendo upload multipli e retry.

8. Centralizzazione logica embedding provider e dimensioni vettori

Titolo: Centralizzare mappatura provider → dimensione vettore per Qdrant

Descrizione: La dimensione del vettore Qdrant viene determinata dinamicamente in QdrantService e EmbeddingService in base al provider
github.com
. Creare una mappa di configurazione centralizzata e fornire API per leggerla.

Attività:

Definire oggetto di configurazione (es. embeddingProvidersConfig.json) con provider, dimensione, endpoint.

Utilizzare questo oggetto in EmbeddingService e QdrantService.

Aggiungere test per verificare la coerenza di dimensioni.

Test:

Unit: test sulla funzione di lookup; test che un provider sconosciuto generi errore esplicito.

Integrazione: test end‑to‑end di indicizzazione e ricerca su Qdrant con provider diverso.

9. Gestione pesi dinamici HybridSearchService

Titolo: Esporre configurazione dinamica pesi per ricerche ibride

Descrizione: I pesi per vector/keyword search e la costante RRF sono hardcoded in HybridSearchService. Consentire la configurazione a livello di gioco o di query per ottimizzare i risultati.

Attività:

Aggiungere campi vectorWeight, keywordWeight, rrfConstant in DB games o settings.

Estendere API per poter specificare pesi in runtime (opzionale).

Aggiornare HybridSearchService per leggere la configurazione.

Test:

Unit: test per combinazioni di pesi diverse.

Integrazione: verificare che la ricerca ibrida restituisca risultati diversi al cambiare dei pesi.

E2E: test API che consenta l’override dei pesi (se implementato).