# Flussi utente MeepleAI

## Panoramica ruoli e permessi
- **Anonimo**: può registrarsi, effettuare login e avviare il flusso di recupero password.
- **Utente autenticato (ruolo `User`)**: può accedere alla chat multi-sessione, scegliere giochi e agenti, inviare messaggi, feedback e caricare PDF per i giochi a cui ha accesso.
- **Utente con privilegi (`Editor` o `Admin`)**: eredita i flussi dell'utente base e in aggiunta può revisionare RuleSpec e gestire versioni.
- **Amministratore (`Admin`)**: oltre ai flussi precedenti può consultare la dashboard operativa e configurare integrazioni n8n.
- **Servizi esterni (n8n, webhook)**: possono orchestrare i flussi di Q&A e Explain tramite endpoint dedicati.

## Flussi per utenti anonimi

### 1. Registrazione nuovo account
1. Apre `pages/index.tsx` con il form di registrazione/login.
2. Compila i campi richiesti (nome, email, password) e seleziona eventuale ruolo.
3. Invia il form → `POST /auth/register`.
4. Riceve conferma e sessione autenticata (cookie HttpOnly).

### 2. Login
1. Apre il form di autenticazione.
2. Inserisce credenziali esistenti.
3. Invia il form → `POST /auth/login`.
4. In caso di successo viene reindirizzato alla dashboard/chat iniziale.

### 3. Recupero password – richiesta
1. Dalla pagina di login seleziona "Forgot Password?" (`/reset-password`).
2. Inserisce l'email e invia → `POST /auth/password-reset/request`.
3. Riceve messaggio di conferma e attende la mail con token.

### 4. Recupero password – reset con token
1. Segue il link dell'email che apre `/reset-password?token=...`.
2. L'app verifica il token → `GET /auth/password-reset/verify`.
3. Compila la nuova password (validazione live e indicatore di forza).
4. Conferma → `PUT /auth/password-reset/confirm`.
5. L'app tenta l'auto-login → `POST /auth/login`; in caso di successo reindirizza a `/chat`.

## Flussi per utenti autenticati (ruolo `User`)

### 5. Validazione sessione automatica
1. Al caricamento, il client invia `GET /auth/me`.
2. Se la sessione è valida, aggiorna l'utente in stato locale.
3. Se scaduta, redirige alla pagina di login.

### 6. Navigazione giochi e chat
1. Accede a `/chat`.
2. Seleziona un gioco dall'elenco (recuperato via `GET /games`).
3. L'app carica gli agenti disponibili per il gioco → `GET /games/{gameId}/agents`.
4. L'utente seleziona un agente e apre/crea una chat (`GET /chats`, `POST /chats`).

### 7. Conversazione con agente QA
1. Invia domanda testuale → `POST /agents/qa` (con `chatId`).
2. Visualizza streaming della risposta con snippet e citazioni.
3. Può interrompere lo streaming o attendere la chiusura evento `Complete`.

### 8. Conversazione con agente Explain
1. Dal pannello agente sceglie "Explain".
2. Richiede spiegazione su un topic → `POST /agents/explain`.
3. Riceve outline, script e citazioni; può salvarle nella chat attiva.

### 9. Conversazione con agente Setup
1. Seleziona l'agente "Setup".
2. Avvia guida di setup → `POST /agents/setup`.
3. Riceve lista di step con tempi stimati e può seguire la checklist.

### 10. Conversazione con agente Chess (quando disponibile)
1. Accede alla sezione Chess (feature in rollout).
2. Inserisce domanda e posizione FEN → `POST /agents/chess`.
3. Riceve analisi specifica con mosse consigliate.

### 11. Gestione chat multi-sessione
1. Visualizza la sidebar con tutte le chat (`GET /chats`).
2. Può rinominare o eliminare una chat (`DELETE /chats/{chatId}`).
3. Se non esistono chat per il gioco selezionato, la UI crea una chat al primo messaggio.

### 12. Feedback sulle risposte
1. Dopo una risposta AI, seleziona 👍/👎.
2. Invia feedback → `POST /agents/feedback` con `messageId` e stato.
3. La UI aggiorna le metriche locali e mostra conferma.

## Flussi gestione PDF e RuleSpec (`User`, `Editor`, `Admin`)

> **Ruoli**
> - Upload e monitoraggio PDF: `User`, `Editor`, `Admin`.
> - Revisione e pubblicazione RuleSpec, gestione versioni: `Editor`, `Admin`.

### 13. Selezione o creazione gioco per ingestione
1. Accede a `/upload`.
2. Seleziona un gioco esistente (`GET /games`) oppure crea nuovo gioco → `POST /games` (solo `Editor`/`Admin`).
3. Conferma la scelta per proseguire nel wizard.

### 14. Upload PDF regole
1. Step "Upload": seleziona file (validazione MIME, dimensione, pagine).
2. Invia il file → `POST /ingest/pdf` con metadati (disponibile a `User`, `Editor`, `Admin`).
3. Visualizza stato iniziale `pending` nella lista.

### 15. Monitoraggio estrazione PDF
1. Il client effettua polling → `GET /pdfs/{pdfId}/text`.
2. Aggiorna UI in base a `processing`, `completed` o `failed`.
3. In caso di errore può riprovare l'upload o mostrare il messaggio di failure.

### 16. Revisione RuleSpec generato
1. A estrazione completata, il wizard carica RuleSpec → `GET /games/{gameId}/rulespec`.
2. L'utente verifica testo, pagine e metadata.
3. Può modificare manualmente i dati prima della pubblicazione.

### 17. Pubblicazione RuleSpec
1. Dopo la revisione conferma le modifiche.
2. Invia il JSON aggiornato → `PUT /games/{gameId}/rulespec`.
3. Riceve conferma e versione incrementata.

### 18. Gestione upload esistenti
1. Visualizza tabella PDF associati al gioco → `GET /games/{gameId}/pdfs`.
2. Può riavviare la pipeline su un PDF fallito o eliminarlo (se supportato).
3. Mantiene storico caricamenti con stato e timestamp.

### 19. Modifica avanzata RuleSpec
1. Accede a `/editor?gameId=...`.
2. Il sistema carica RuleSpec corrente → `GET /games/{gameId}/rulespec`.
3. Effettua modifiche nel doppio pannello con validazione live.
4. Usa undo/redo per navigare fra versioni temporanee.
5. Salva → `PUT /games/{gameId}/rulespec` (auto-save su blur).

### 20. Storico versioni e diff
1. Apre `/versions?gameId=...`.
2. Elenca versioni disponibili → `GET /games/{gameId}/rulespec/history`.
3. Seleziona due versioni da confrontare → `GET /games/{gameId}/rulespec/diff`.
4. Analizza cambiamenti (added/modified/deleted).

## Flussi esclusivi amministratore (`Admin`)

### 21. Monitoraggio richieste AI
1. Apre `/admin`.
2. Recupera statistiche aggregate → `GET /admin/stats`.
3. Visualizza tabella richieste → `GET /admin/requests` con filtri (endpoint, user, game, date).
4. Esporta CSV con i metadati correnti.

### 22. Analisi feedback e prestazioni
1. Applica filtri per endpoint specifico (QA, Explain, Setup).
2. Valuta success rate, latenza media e token utilizzati.
3. Identifica richieste problematiche tramite correlation ID.

### 23. Configurazione integrazioni n8n
1. Accede a `/n8n`.
2. Elenca configurazioni → `GET /admin/n8n`.
3. Crea nuova configurazione → `POST /admin/n8n` (nome, base URL, API key cifrata, webhook).
4. Testa connessione → `POST /admin/n8n/{configId}/test`.
5. Aggiorna o elimina configurazioni esistenti (`PUT`/`DELETE`).

### 24. Verifica stato piattaforma
1. Per diagnosi apre endpoint `/health`, `/health/ready`, `/health/live`.
2. Controlla stato di Postgres, Redis, Qdrant.
3. Usa correlation ID dai log Seq per correlare problemi.

## Flussi orchestrati da servizi esterni (n8n)

### 25. Webhook Q&A orchestrato
1. Workflow n8n riceve richiesta esterna.
2. Autentica verso MeepleAI (`/auth/login` con account di servizio).
3. Invoca `POST /agents/qa` con `chatId` opzionale.
4. Riceve risposta e la inoltra al chiamante.

### 26. Webhook Explain orchestrato
1. Trigger esterno attiva workflow Explain.
2. Workflow invoca `POST /agents/explain` con `gameId` e `topic`.
3. Gestisce errori e ritenta secondo logica n8n.
4. Consegna output formattato al sistema esterno.

### 27. Monitoraggio e manutenzione workflow
1. Operatore importa/attiva workflow JSON da `infra/init/n8n`.
2. Utilizza interfaccia n8n per abilitare/disabilitare flussi.
3. Verifica log di esecuzione, request ID e metriche.

## Flussi operativi trasversali

### 28. Osservabilità e logging
1. Ogni chiamata API restituisce `X-Correlation-Id`.
2. Gli operatori usano Seq per aggregare log e filtrare per ID, userId, endpoint.
3. In caso di errore seguono il flusso end-to-end (richiesta → AI → risposta).

### 29. Controlli di carico e QA
1. Team QA esegue test E2E Playwright per il flusso di upload e chat.
2. Load test tramite workflow GitHub Actions (`load-test.yml`).
3. Valutano metriche e regressioni prima della release.

### 30. Aggiornamento contenuti e roadmap
1. Product manager consulta `docs/roadmap.md` e `docs/features.md`.
2. Pianifica nuovi flussi (es. Chess agent UI, multi-gioco) inserendo task nel backlog.
3. Coordina con dev per evolvere i flussi esistenti seguendo principi SOLID/DRY.
