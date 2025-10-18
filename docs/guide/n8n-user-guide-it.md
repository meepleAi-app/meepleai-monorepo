# Guida Utente: Gestione n8n in MeepleAI

**Versione**: 1.0
**Data**: Ottobre 2025
**Target**: Amministratori dell'applicazione MeepleAI

---

## Indice

1. [Introduzione](#introduzione)
2. [Accesso all'Interfaccia Admin](#accesso-allinterfaccia-admin)
3. [Gestione Configurazioni n8n](#gestione-configurazioni-n8n)
4. [Esempio Pratico: Integrare un Sistema Esterno](#esempio-pratico-integrare-un-sistema-esterno)
5. [Test e Verifica](#test-e-verifica)
6. [Troubleshooting](#troubleshooting)
7. [Best Practices](#best-practices)

---

## Introduzione

### Cos'è n8n?

**n8n** è una piattaforma di automazione workflow che permette di creare integrazioni tra MeepleAI e sistemi esterni senza scrivere codice.

### Cosa può fare un Admin?

Come amministratore di MeepleAI, puoi:

- ✅ **Configurare istanze n8n** per connettere sistemi esterni
- ✅ **Testare connessioni** per verificare che tutto funzioni
- ✅ **Gestire webhook** per ricevere richieste da altre applicazioni
- ✅ **Monitorare lo stato** delle integrazioni attive
- ✅ **Attivare/disattivare** configurazioni senza eliminarle

### Caso d'Uso Tipico

**Scenario**: Hai un'applicazione mobile che vuole chiedere spiegazioni sulle regole di un gioco.

**Senza n8n**: L'app mobile deve implementare l'autenticazione MeepleAI, gestire le sessioni, ecc.

**Con n8n**: L'app mobile chiama un webhook pubblico, n8n gestisce l'autenticazione e inoltra la richiesta a MeepleAI.

---

## Accesso all'Interfaccia Admin

### Prerequisiti

- Account admin su MeepleAI
- Accesso all'applicazione web (default: `http://localhost:3000`)

### Passo 1: Login

1. Apri il browser e vai su `http://localhost:3000`
2. Clicca su **"Login"**
3. Inserisci le credenziali admin:
   - **Email**: `admin@meepleai.dev`
   - **Password**: `Demo123!` (o la tua password)
4. Clicca **"Accedi"**

### Passo 2: Accedere alla Pagina n8n

1. Nella barra di navigazione, cerca il link **"n8n"** o vai direttamente su:
   ```
   http://localhost:3000/n8n
   ```

2. Dovresti vedere la pagina di gestione n8n con:
   - Lista delle configurazioni esistenti
   - Pulsante **"Aggiungi Nuova Configurazione"**
   - Stato di ogni configurazione (Attiva/Inattiva)

---

## Gestione Configurazioni n8n

### Visualizzare Configurazioni Esistenti

Nella pagina n8n vedrai una tabella con:

| Nome | Base URL | Webhook URL | Stato | Ultimo Test | Azioni |
|------|----------|-------------|-------|-------------|--------|
| n8n Produzione | http://n8n:5678 | http://n8n:5678/webhook/explain | ✅ Attiva | 2025-10-16 10:30 - OK (120ms) | Test / Modifica / Elimina |

**Colonne**:
- **Nome**: Identificativo della configurazione
- **Base URL**: Indirizzo dell'istanza n8n
- **Webhook URL**: URL del webhook configurato
- **Stato**: Badge verde (Attiva) o grigio (Inattiva)
- **Ultimo Test**: Data/ora e risultato dell'ultimo test di connessione
- **Azioni**: Pulsanti per gestire la configurazione

### Creare una Nuova Configurazione

#### Passo 1: Aprire il Form

Clicca sul pulsante **"Aggiungi Nuova Configurazione"** in alto a destra.

#### Passo 2: Compilare i Campi

```json
┌─────────────────────────────────────────────┐
│  Crea Nuova Configurazione n8n              │
├─────────────────────────────────────────────┤
│                                             │
│  Nome: *                                    │
│  ┌─────────────────────────────────────┐   │
│  │ n8n Produzione                      │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  Base URL: *                                │
│  ┌─────────────────────────────────────┐   │
│  │ http://n8n:5678                     │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  API Key: *                                 │
│  ┌─────────────────────────────────────┐   │
│  │ ••••••••••••••••••••••              │   │
│  └─────────────────────────────────────┘   │
│                                             │
│  Webhook URL: (opzionale)                   │
│  ┌─────────────────────────────────────┐   │
│  │ http://n8n:5678/webhook/explain     │   │
│  └─────────────────────────────────────┘   │
│                                             │
│     [Annulla]  [Salva]                      │
└─────────────────────────────────────────────┘
```

**Campi Obbligatori** (marcati con *):

1. **Nome**: Identificativo univoco (es. "n8n Produzione", "n8n Test")
2. **Base URL**: Indirizzo dell'istanza n8n
   - Ambiente locale: `http://localhost:5678`
   - Docker interno: `http://n8n:5678`
   - Produzione: `https://n8n.tuo-dominio.com`
3. **API Key**: Chiave API di n8n
   - Vai su n8n → Settings → API Keys
   - Copia la chiave generata

**Campi Opzionali**:

4. **Webhook URL**: URL pubblico del webhook (se già configurato)
   - Esempio: `http://n8n:5678/webhook/explain`
   - Puoi lasciarlo vuoto e aggiungerlo dopo

#### Passo 3: Salvare

Clicca **"Salva"**. Se tutto è corretto:
- ✅ Vedrai un messaggio di successo
- ✅ La configurazione apparirà nella lista
- ✅ L'API Key sarà crittografata nel database

### Modificare una Configurazione

1. Clicca sul pulsante **"Modifica"** nella riga della configurazione
2. Il form si aprirà precompilato (esclusa l'API Key)
3. Modifica i campi necessari
4. Clicca **"Salva"**

**Nota**: Se non vuoi cambiare l'API Key, lascia il campo vuoto. Inserisci una nuova chiave solo se vuoi sostituirla.

### Testare la Connessione

Il test verifica che MeepleAI possa comunicare con n8n.

#### Eseguire il Test

1. Clicca sul pulsante **"Testa"** nella riga della configurazione
2. Attendi qualche secondo
3. Vedrai un alert con il risultato:

**Successo**:
```json
✅ Connessione riuscita!
Latenza: 120ms
```

**Errore**:
```
❌ Errore di connessione
Messaggio: Connection refused. Verifica che n8n sia in esecuzione.
```

#### Interpretare i Risultati

- **OK (< 200ms)**: ✅ Ottimo, la connessione è veloce
- **OK (200-500ms)**: ⚠️ Accettabile, potrebbe essere lento sotto carico
- **OK (> 500ms)**: ⚠️ Lento, verifica la rete o le risorse server
- **Errore**: ❌ n8n non è raggiungibile o l'API Key è errata

### Attivare/Disattivare una Configurazione

Puoi disattivare temporaneamente una configurazione senza eliminarla.

**Per disattivare**:
1. Clicca su **"Modifica"**
2. Rimuovi il flag **"Attiva"** (o cambia `isActive` a `false`)
3. Salva

**Effetto**: La configurazione resta visibile ma non viene usata dall'applicazione.

### Eliminare una Configurazione

⚠️ **Attenzione**: L'eliminazione è permanente!

1. Clicca sul pulsante **"Elimina"** nella riga della configurazione
2. Conferma l'operazione nel dialog
3. La configurazione viene rimossa dal database

---

## Esempio Pratico: Integrare un Sistema Esterno

### Scenario

Hai un chatbot su Telegram che vuole offrire spiegazioni sulle regole di giochi da tavolo usando MeepleAI.

**Obiettivo**: Quando un utente chiede "Come si vince a Tris?", il bot Telegram chiama n8n, che a sua volta chiama MeepleAI e restituisce la spiegazione.

### Architettura

```
Utente Telegram
    ↓
Bot Telegram (Python/Node.js)
    ↓ HTTP POST
Webhook n8n (/webhook/explain)
    ↓ Autenticazione + Validazione
MeepleAI API (/api/v1/agents/explain)
    ↓ RAG + LLM
Risposta generata
```

### Passo 1: Configurare n8n su MeepleAI

Segui i passaggi in [Creare una Nuova Configurazione](#creare-una-nuova-configurazione):

- **Nome**: `Bot Telegram Produzione`
- **Base URL**: `http://n8n:5678` (o il tuo URL di produzione)
- **API Key**: La chiave API del tuo n8n
- **Webhook URL**: Lascialo vuoto per ora (lo configureremo dopo)

### Passo 2: Creare il Workflow in n8n

#### 2.1 Accedere a n8n

1. Apri il browser e vai su `http://localhost:5678`
2. Login con le credenziali n8n (default: `admin` / vedi `N8N_BASIC_AUTH_PASSWORD` nel file env)

#### 2.2 Importare il Workflow

**Opzione A: Usa il workflow esistente**

1. In n8n, clicca su **"Workflows"** → **"Import from File"**
2. Seleziona il file:
   ```
   infra/n8n/workflows/agent-explain-orchestrator.json
   ```
3. Clicca **"Import"**

**Opzione B: Creare da zero**

Vedi la documentazione tecnica in `docs/N8N-01-README.md` per i dettagli completi.

#### 2.3 Attivare il Workflow

1. Apri il workflow importato
2. In alto a destra, clicca sul toggle **"Inactive"** → **"Active"**
3. Il workflow è ora in ascolto sul webhook

#### 2.4 Ottenere l'URL del Webhook

1. Nel workflow, clicca sul nodo **"Webhook Trigger"**
2. Nelle proprietà, copia l'URL:
   ```
   http://localhost:5678/webhook/explain
   ```

### Passo 3: Aggiornare la Configurazione MeepleAI

1. Torna su MeepleAI → Pagina n8n
2. Modifica la configurazione **"Bot Telegram Produzione"**
3. Aggiungi il **Webhook URL**: `http://n8n:5678/webhook/explain`
4. Salva
5. Clicca **"Testa"** per verificare la connessione

### Passo 4: Chiamare il Webhook dal Bot Telegram

Nel tuo bot Telegram (esempio Python):

```python
import requests

def get_game_explanation(game_id: str, topic: str):
    """Chiama n8n per ottenere una spiegazione da MeepleAI"""

    webhook_url = "http://localhost:5678/webhook/explain"

    payload = {
        "gameId": game_id,
        "topic": topic
    }

    response = requests.post(webhook_url, json=payload)

    if response.status_code == 200:
        data = response.json()
        return data["data"]["script"]  # Testo della spiegazione
    else:
        return "Errore: impossibile ottenere la spiegazione"

# Esempio d'uso
explanation = get_game_explanation("tic-tac-toe", "winning conditions")
print(explanation)
```

**Output atteso**:
```
# Come si Vince a Tris

Per vincere a Tris, un giocatore deve allineare tre dei propri simboli
(X o O) in una riga, colonna o diagonale sulla griglia 3x3.

Le combinazioni vincenti possibili sono:
- Tre righe orizzontali (in alto, centro, in basso)
- Tre colonne verticali (sinistra, centro, destra)
- Due diagonali (da in alto a sinistra a in basso a destra, e viceversa)

Il primo giocatore che completa una di queste combinazioni vince la partita.
Se la griglia si riempie senza che nessuno abbia vinto, la partita termina in pareggio.

[Regole complete a pagina 1 del regolamento]
```

### Passo 5: Gestire le Risposte nel Bot

```python
def handle_game_question(message):
    """Gestisce domande sui giochi da tavolo"""

    # Estrai il gioco e la domanda dal messaggio
    # Esempio: "Come si vince a Tris?"

    game_id = "tic-tac-toe"  # Mappatura gioco → ID
    topic = "winning conditions"

    # Chiama il webhook
    explanation = get_game_explanation(game_id, topic)

    # Invia la risposta su Telegram
    bot.send_message(
        chat_id=message.chat.id,
        text=explanation,
        parse_mode="Markdown"
    )
```

### Passo 6: Testare l'Integrazione End-to-End

#### Test 1: Chiamata Diretta al Webhook

Usa `curl` per testare il webhook:

```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{
    "gameId": "tic-tac-toe",
    "topic": "winning conditions"
  }'
```

**Risposta Attesa** (200 OK):
```json
{
  "success": true,
  "data": {
    "outline": {
      "mainTopic": "Winning Conditions in Tic-Tac-Toe",
      "sections": [...]
    },
    "script": "# Come si Vince a Tris\n\n...",
    "citations": [
      {
        "text": "A player wins by placing three of their marks in a horizontal, vertical, or diagonal row.",
        "source": "tic-tac-toe-rules.pdf",
        "page": 1,
        "line": 42
      }
    ],
    "estimatedReadingTimeMinutes": 2,
    "metadata": {
      "promptTokens": 512,
      "completionTokens": 256,
      "totalTokens": 768,
      "confidence": 0.95
    }
  },
  "timestamp": "2025-10-16T10:45:00Z",
  "version": "1.0"
}
```json
#### Test 2: Chiamata dal Bot Telegram

1. Avvia il tuo bot Telegram
2. Invia un messaggio: "Come si vince a Tris?"
3. Verifica che il bot risponda con la spiegazione generata

#### Test 3: Monitorare i Log

**Su MeepleAI (Seq)**:
```
http://localhost:8081
```yaml
Cerca:
- **User**: `n8n-service@meepleai.dev`
- **Endpoint**: `/api/v1/agents/explain`
- **Correlation ID**: Per tracciare l'intera richiesta

**Su n8n**:
```
http://localhost:5678
→ Executions
→ Filtra per workflow "Agent Explain"
```json
Vedrai:
- ✅ Esecuzioni riuscite (verde)
- ❌ Esecuzioni fallite (rosso)
- ⏱️ Durata di ogni esecuzione

---

## Test e Verifica

### Test di Connessione Rapido

Dalla pagina n8n di MeepleAI:

1. Clicca **"Testa"** sulla configurazione
2. Verifica il messaggio:
   - ✅ **"Connessione riuscita! Latenza: XXXms"** → Tutto OK
   - ❌ **"Errore di connessione"** → Vedi troubleshooting

### Test Completo del Flusso

#### Comandi di Test

**1. Test Webhook con gameId valido**:
```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{"gameId": "tic-tac-toe", "topic": "rules"}'
```

**Risultato Atteso**: HTTP 200, payload JSON con spiegazione

**2. Test con gameId mancante** (validazione):
```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{"topic": "rules"}'
```

**Risultato Atteso**: HTTP 400, errore di validazione
```json
{
  "success": false,
  "error": "gameId is required",
  "timestamp": "...",
  "version": "1.0"
}
```

**3. Test con gioco senza contenuto**:
```bash
curl -X POST http://localhost:5678/webhook/explain \
  -H "Content-Type: application/json" \
  -d '{"gameId": "nonexistent-game", "topic": "rules"}'
```

**Risultato Atteso**: HTTP 200, messaggio "No relevant information found"

### Verificare lo Stato del Sistema

#### Controllo Servizi Docker

```bash
cd infra
docker compose ps
```

Verifica che siano **Up**:
- `postgres` (database)
- `qdrant` (vector search)
- `redis` (cache)
- `n8n` (workflow automation)
- `api` (MeepleAI backend)

#### Health Check API

```bash
curl http://localhost:8080/health
```

**Risposta Attesa** (200 OK):
```json
{
  "status": "Healthy",
  "results": {
    "postgres": "Healthy",
    "redis": "Healthy",
    "qdrant_http": "Healthy",
    "qdrant_collection": "Healthy"
  }
}
```

#### Health Check n8n

```bash
curl http://localhost:5678/healthz
```

**Risposta Attesa** (200 OK):
```json
{
  "status": "ok"
}
```json
---

## Troubleshooting

### Problema: "Connessione Rifiutata" nel Test

**Sintomo**: Quando clicchi "Testa", vedi:
```
❌ Errore di connessione
Messaggio: Connection refused
```json
**Cause Possibili**:

1. **n8n non è in esecuzione**

   **Soluzione**:
   ```bash
   cd infra
   docker compose up -d n8n
   docker compose logs -f n8n  # Verifica i log
   ```

2. **Base URL errato**

   **Verifica**:
   - Se sei sull'app web (browser): usa `http://localhost:5678`
   - Se sei nell'API (Docker interno): usa `http://n8n:5678`

   **Soluzione**: Modifica la configurazione con l'URL corretto

3. **Porta n8n non esposta**

   **Verifica** `infra/docker-compose.yml`:
   ```yaml
   n8n:
     ports:
       - "5678:5678"  # Deve essere presente
   ```

### Problema: "Unauthorized" o 401 nel Webhook

**Sintomo**: Il webhook restituisce:
```json
{
  "success": false,
  "error": "Unauthorized",
  "timestamp": "..."
}
```json
**Causa**: Il session token di n8n è scaduto o non valido.

**Soluzione**:

1. **Rigenera il session token**:
   ```bash
   cd tools
   pwsh setup-n8n-service-account.ps1
   ```

2. **Aggiorna il workflow n8n**:
   - Vai su n8n → Workflow "Agent Explain"
   - Nodo "Call MeepleAI API"
   - Headers → Cookie: `session=<nuovo_token>`
   - Salva e riattiva il workflow

3. **Verifica il file env**:
   ```bash
   cat infra/env/n8n-service-session.env
   ```
   Dovrebbe contenere:
   ```
   N8N_SERVICE_SESSION=<token_valido>
   ```

### Problema: "gameId is required" anche se inviato

**Sintomo**: Il webhook risponde con errore di validazione ma il payload contiene `gameId`.

**Causa**: Il nodo di validazione JavaScript in n8n potrebbe avere un bug.

**Soluzione**:

1. **Verifica il payload inviato**:
   ```bash
   curl -v -X POST http://localhost:5678/webhook/explain \
     -H "Content-Type: application/json" \
     -d '{"gameId": "tic-tac-toe", "topic": "rules"}'
   ```

   Controlla l'header `Content-Type` nella risposta.

2. **Ispeziona il workflow n8n**:
   - Vai su n8n → Executions
   - Trova l'esecuzione fallita
   - Clicca per vedere i dettagli di ogni nodo
   - Verifica cosa riceve il nodo "Validate Input"

3. **Testa direttamente l'API** (bypassando n8n):
   ```bash
   # Prima ottieni un session token
   curl -X POST http://localhost:8080/api/v1/auth/login \
     -H "Content-Type: application/json" \
     -d '{"email": "admin@meepleai.dev", "password": "Demo123!"}' \
     -c cookies.txt

   # Poi chiama l'API
   curl -X POST http://localhost:8080/api/v1/agents/explain \
     -H "Content-Type: application/json" \
     -b cookies.txt \
     -d '{"gameId": "tic-tac-toe", "topic": "rules"}'
   ```

### Problema: Latenza Alta (> 500ms)

**Sintomo**: Il test di connessione mostra latenze sopra i 500ms.

**Cause Possibili**:

1. **Risorse Docker limitate**

   **Soluzione**:
   - Aumenta CPU/RAM allocate a Docker Desktop
   - Chiudi altri container non necessari

2. **Database lento**

   **Verifica**:
   ```bash
   docker stats postgres
   ```

   Se CPU > 80%, potrebbe essere sovraccarico.

3. **Network Docker lento**

   **Test**:
   ```bash
   docker exec api ping -c 3 n8n
   ```

   La latenza dovrebbe essere < 5ms.

### Problema: Workflow n8n non si Attiva

**Sintomo**: Il toggle "Active" non si attiva o ritorna su "Inactive".

**Causa**: Errore nel workflow (nodo mal configurato).

**Soluzione**:

1. **Controlla gli errori**:
   - In n8n, apri il workflow
   - Guarda se ci sono icone rosse di errore sui nodi
   - Clicca su ogni nodo per vedere i dettagli

2. **Verifica il nodo Webhook**:
   - Deve avere un path univoco (es. `/webhook/explain`)
   - HTTP Method: `POST`
   - Response Mode: `When Last Node Finishes`

3. **Verifica il nodo HTTP Request**:
   - URL: `{{$env.MEEPLEAI_API_URL}}/api/v1/agents/explain`
   - Method: `POST`
   - Headers: `Cookie: session={{$env.N8N_SERVICE_SESSION}}`
   - Body: JSON con `{{$json}}`

4. **Testa manualmente**:
   - Clicca su "Execute Workflow"
   - Poi invia una richiesta al webhook
   - Vedi i risultati di ogni nodo in tempo reale

---

## Best Practices

### Sicurezza

#### 1. Cambia le Password di Default

**Dopo l'installazione**:

```bash
# Modifica il file env
nano infra/env/n8n.env.dev

# Cambia:
N8N_BASIC_AUTH_PASSWORD=TuaPasswordSicura123!
```

**Riavvia n8n**:
```bash
docker compose restart n8n
```json
#### 2. Ruota Periodicamente le API Key

**Frequenza consigliata**: Ogni 90 giorni

**Procedura**:
1. Genera nuova API Key in n8n (Settings → API Keys)
2. Aggiorna configurazione in MeepleAI (pagina n8n → Modifica)
3. Testa la connessione
4. Elimina la vecchia API Key da n8n

#### 3. Usa HTTPS in Produzione

**Non usare HTTP in produzione!**

**Setup minimo**:
- Reverse proxy (nginx/Caddy) con SSL/TLS
- Certificati Let's Encrypt
- Base URL: `https://n8n.tuo-dominio.com`
- Webhook URL: `https://n8n.tuo-dominio.com/webhook/explain`

Vedi `docs/N8N-01-deployment-guide.md` per i dettagli.

#### 4. Limita l'Accesso alla Rete

**Docker Compose** (produzione):
```yaml
n8n:
  networks:
    - meepleai  # Solo rete interna
  # NON esporre la porta pubblica in produzione:
  # ports:
  #   - "5678:5678"  ❌ Commentalo!
```json
Accedi a n8n solo tramite VPN o bastion host.

### Performance

#### 1. Monitora le Esecuzioni

**Frequenza**: Settimanale

**In n8n**:
- Vai su Executions
- Filtra per "Failed"
- Identifica pattern di errore
- Ottimizza i workflow problematici

**In Seq** (`http://localhost:8081`):
- Cerca per `User = "n8n-service@meepleai.dev"`
- Filtra per latenza > 2 secondi
- Identifica endpoint lenti

#### 2. Usa la Cache

MeepleAI ha già cache Redis attivata (AI-05):
- Richieste identiche restituiscono risposte in < 100ms
- TTL: 24 ore
- Nessuna configurazione necessaria

**Verifica cache**:
```bash
# Prima richiesta (cache miss)
time curl -X POST http://localhost:5678/webhook/explain \
  -d '{"gameId": "tic-tac-toe", "topic": "winning"}'
# Tempo: ~1.5s

# Seconda richiesta (cache hit)
time curl -X POST http://localhost:5678/webhook/explain \
  -d '{"gameId": "tic-tac-toe", "topic": "winning"}'
# Tempo: ~0.1s ✅
```json
#### 3. Limita i Timeout

**Nel workflow n8n**:
- Nodo HTTP Request → Settings
- Timeout: 30000 ms (30 secondi max)

Previene hanging indefinito se l'API è lenta.

### Manutenzione

#### 1. Backup delle Configurazioni

**Frequenza**: Prima di ogni modifica importante

**Manuale**:
1. Esporta i workflow n8n (Settings → Export)
2. Salva in `infra/n8n/workflows/backup-YYYYMMDD.json`

**Automatico** (consigliato):
- n8n salva i workflow nel database PostgreSQL
- Backup del database include anche i workflow
- Vedi `docs/database-backup.md`

#### 2. Pulizia Executions Vecchie

**Problema**: n8n accumula cronologia esecuzioni → database cresce

**Soluzione** (in n8n):
- Settings → Executions
- Data Retention: 7 giorni (o meno)
- Older Executions: Delete automatically

#### 3. Aggiornamenti n8n

**Frequenza**: Ogni 2-3 mesi (segui le release notes)

**Procedura**:
```bash
# Backup del database
docker exec postgres pg_dump -U meeple meepleai > backup.sql

# Aggiorna versione in docker-compose.yml
# image: n8nio/n8n:1.114.4 → n8nio/n8n:1.120.0

# Ricostruisci container
docker compose pull n8n
docker compose up -d n8n

# Verifica funzionamento
curl http://localhost:5678/healthz
```

### Organizzazione

#### 1. Naming Convention per Configurazioni

**Pattern consigliato**:
```
<Ambiente>-<Scopo>-<Versione>

Esempi:
- Prod-BotTelegram-v1
- Test-IntegrationTests-v1
- Dev-LocalDevelopment-v1
```json
#### 2. Documentazione dei Workflow

**In n8n**, ogni workflow dovrebbe avere:
- **Nome descrittivo**: "Agent Explain - Production"
- **Note** (clic su "Note" node): Spiegazione del flusso
- **Tags**: Classificazione (production, test, deprecated)

**Esempio Note**:
```markdown
# Agent Explain Workflow

**Scopo**: Fornisce spiegazioni AI sulle regole di giochi da tavolo

**Trigger**: Webhook POST /webhook/explain

**Input**:
- gameId (required): ID del gioco
- topic (optional): Argomento specifico

**Output**:
- success: boolean
- data.script: Spiegazione in Markdown
- data.citations: Riferimenti al regolamento

**Autenticazione**: Service account (n8n-service@meepleai.dev)

**Versione**: 1.0 (2025-10-16)
```

#### 3. Changelog delle Modifiche

**File**: `docs/n8n-changelog.md`

```markdown
# n8n Configuration Changelog

## 2025-10-16 - Aggiunta Bot Telegram

- Creata configurazione "Prod-BotTelegram-v1"
- Base URL: https://n8n.production.com
- Webhook: /webhook/explain
- Testato con successo (latenza: 120ms)

## 2025-10-10 - Rotazione API Key

- Aggiornata API Key per "Prod-BotTelegram-v1"
- Vecchia chiave revocata
- Test OK
```

---

## Risorse Aggiuntive

### Documentazione Tecnica

- **Specifica n8n Integration**: `docs/N8N-01-README.md`
- **Webhook Explain**: `docs/N8N-01-webhook-explain.md`
- **Deployment Guide**: `docs/N8N-01-deployment-guide.md`
- **Database Schema**: `docs/database-schema.md`

### Supporto

- **Issue Tracker**: [GitHub Issues](https://github.com/tuo-repo/meepleai-monorepo/issues)
- **Slack**: #meepleai-support
- **Email**: support@meepleai.dev

### Video Tutorial

- [Come Configurare n8n in 5 Minuti](#) *(coming soon)*
- [Integrare un Bot Telegram con MeepleAI](#) *(coming soon)*
- [Troubleshooting n8n: Errori Comuni](#) *(coming soon)*

---

## Checklist di Verifica

Prima di considerare l'integrazione completata, verifica:

- [ ] Configurazione n8n creata in MeepleAI
- [ ] Test di connessione riuscito (latenza < 200ms)
- [ ] Workflow importato in n8n e attivato
- [ ] Webhook URL aggiunto alla configurazione
- [ ] Test end-to-end riuscito (curl o bot)
- [ ] Log verificati in Seq e n8n Executions
- [ ] Credenziali di default cambiate (password, API key)
- [ ] Documentato il workflow (note in n8n)
- [ ] Backup della configurazione effettuato
- [ ] Monitoraggio impostato (alerts opzionali)

---

**Versione documento**: 1.0
**Ultimo aggiornamento**: 2025-10-16
**Autore**: Team MeepleAI
