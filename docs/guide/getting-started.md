# MeepleAI - Guida Pratica per Avvio Locale e Test

Guida step-by-step per avviare MeepleAI in locale e testare tutte le funzionalità disponibili.

## Indice

1. [Setup Iniziale](#setup-iniziale)
2. [Avvio dell'Applicazione](#avvio-dellapplicazione)
3. [Test delle Funzionalità](#test-delle-funzionalità)
4. [Troubleshooting](#troubleshooting)

---

## Setup Iniziale

### Prerequisiti

Assicurati di avere installato:

- **Docker Desktop** (con Docker Compose)
- **.NET 9.0 SDK** - [Download](https://dotnet.microsoft.com/download/dotnet/9.0)
- **Node.js 20+** - [Download](https://nodejs.org/)
- **pnpm 9** - Installa con `npm install -g pnpm@9`
- **Git**

### 1. Clone del Repository

```bash
git clone https://github.com/DegrassiAaron/meepleai-monorepo.git
cd meepleai-monorepo
```

### 2. Configurazione Environment Variables

#### API Environment

```bash
# Copia il template
cp infra/env/api.env.dev.example infra/env/api.env.dev

# Modifica api.env.dev con il tuo editor preferito
# IMPORTANTE: Inserisci una chiave API valida per OPENROUTER_API_KEY
```

Contenuto minimo di `infra/env/api.env.dev`:

```env
POSTGRES_USER=meeple
POSTGRES_PASSWORD=meeplepass
POSTGRES_DB=meepleai
OPENROUTER_API_KEY=sk-or-v1-your-api-key-here
JWT_ISSUER=http://localhost:8080
ALLOW_ORIGIN=http://localhost:3000
N8N_ENCRYPTION_KEY=your-32-byte-encryption-key-here
```

**Dove ottenere la chiave OpenRouter:**
1. Vai su [OpenRouter.ai](https://openrouter.ai/)
2. Crea un account
3. Genera una API key dalla dashboard
4. Inseriscila nel file `.env.dev`

#### Web Environment

```bash
# Copia il template
cp infra/env/web.env.dev.example infra/env/web.env.dev
```

Contenuto di `infra/env/web.env.dev`:

```env
NEXT_PUBLIC_API_BASE=http://localhost:8080
NEXT_PUBLIC_TENANT_ID=dev
```

### 3. Installazione Dipendenze Frontend

```bash
cd apps/web
pnpm install
cd ../..
```

---

## Avvio dell'Applicazione

### Opzione 1: Docker Compose (Consigliato)

Avvia tutti i servizi con un solo comando:

```bash
cd infra
docker compose up -d --build
```

Questo avvia:
- **PostgreSQL** (porta 5432)
- **Qdrant** vector DB (porta 6333)
- **Redis** (porta 6379)
- **n8n** workflow automation (porta 5678)
- **API** backend (porta 8080)
- **Web** frontend (porta 3000)

Verifica che tutti i servizi siano attivi:

```bash
docker compose ps
```

### Opzione 2: Avvio Manuale (per Development)

#### Terminale 1: Servizi di infrastruttura

```bash
cd infra
docker compose up -d postgres qdrant redis n8n
```

#### Terminale 2: API Backend

```bash
cd apps/api/src/Api
dotnet run
```

L'API sarà disponibile su `http://localhost:8080`

#### Terminale 3: Web Frontend

```bash
cd apps/web
pnpm dev
```

Il frontend sarà disponibile su `http://localhost:3000`

### Verifica dei Servizi

Controlla che tutti i servizi siano attivi:

```bash
# API Health Check
curl http://localhost:8080/

# Frontend
curl http://localhost:3000/api/health

# Qdrant
curl http://localhost:6333/healthz

# Redis
docker exec infra-redis-1 redis-cli ping
```

---

## Test delle Funzionalità

### 1. Autenticazione

#### Registrazione Utente

**Via Web UI:**
1. Apri `http://localhost:3000`
2. Se non hai un account, vai alla pagina di registrazione
3. Inserisci email, password e display name

**Via API (curl):**

```bash
# Registrazione
curl -X POST http://localhost:8080/auth/register \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@meepleai.dev",
    "password": "Test123!",
    "displayName": "Test User"
  }' \
  -c cookies.txt

# Verifica sessione
curl http://localhost:8080/auth/me \
  -b cookies.txt
```

#### Login

```bash
curl -X POST http://localhost:8080/auth/login \
  -H "Content-Type: application/json" \
  -d '{
    "email": "test@meepleai.dev",
    "password": "Test123!"
  }' \
  -c cookies.txt
```

#### Utenti Demo Pre-caricati

Il database viene inizializzato con 3 utenti demo (vedi migration `SeedDemoData`):

- **Admin**: `admin@meepleai.dev` / `Demo123!` (Role: Admin)
- **Editor**: `editor@meepleai.dev` / `Demo123!` (Role: Editor)
- **User**: `user@meepleai.dev` / `Demo123!` (Role: User)

---

### 2. Gestione Giochi

#### Visualizza Giochi Disponibili

**Via Web UI:**
1. Vai su `http://localhost:3000`
2. Troverai la lista dei giochi pre-caricati (Tic-Tac-Toe, Chess)

**Via API:**

```bash
curl http://localhost:8080/games \
  -b cookies.txt
```

#### Crea un Nuovo Gioco

```bash
curl -X POST http://localhost:8080/games \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "name": "Catan",
    "publisher": "CATAN GmbH",
    "minPlayers": 3,
    "maxPlayers": 4,
    "minAge": 10,
    "playingTime": "60-120"
  }'
```

---

### 3. Upload e Processing PDF

#### Upload via Web UI

1. Vai su `http://localhost:3000/upload`
2. Seleziona un gioco dalla dropdown
3. Carica un PDF del regolamento
4. Attendi il processing

#### Upload via API

```bash
# Upload PDF
curl -X POST http://localhost:8080/ingest/pdf \
  -F "gameId=game-id-here" \
  -F "file=@/path/to/rulebook.pdf" \
  -b cookies.txt
```

Risposta:
```json
{
  "pdfId": "pdf-id-uuid",
  "gameId": "game-id",
  "fileName": "rulebook.pdf",
  "fileSizeBytes": 1234567,
  "uploadedAt": "2025-10-11T10:30:00Z"
}
```

#### Estrazione Testo

```bash
# Visualizza testo estratto
curl http://localhost:8080/pdfs/{pdfId}/text \
  -b cookies.txt
```

#### Genera RuleSpec da PDF

```bash
# Genera RuleSpec usando AI
curl -X POST http://localhost:8080/ingest/pdf/{pdfId}/rulespec \
  -b cookies.txt
```

Questo processo:
1. Estrae il testo dal PDF
2. Usa l'LLM per generare una RuleSpec v0 strutturata
3. Salva la RuleSpec nel database

#### Indicizza PDF nel Vector DB

```bash
# Indicizza il contenuto per semantic search
curl -X POST http://localhost:8080/ingest/pdf/{pdfId}/index \
  -b cookies.txt
```

Questo processo:
1. Divide il testo in chunks (512 caratteri, overlap 50)
2. Genera embeddings con OpenRouter
3. Salva nel vector database Qdrant

---

### 4. Chat e Q&A con AI

#### Via Web UI

1. Vai su `http://localhost:3000/chat`
2. Seleziona un gioco
3. Fai domande sul regolamento

#### Via API - Agent Q&A

```bash
curl -X POST http://localhost:8080/agents/qa \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "gameId": "game-id-here",
    "question": "Come si vince a Scacchi?",
    "chatId": null
  }'
```

Risposta:
```json
{
  "answer": "Si vince a Scacchi dando scacco matto al Re avversario...",
  "sources": [
    {
      "text": "Il gioco termina quando...",
      "score": 0.95,
      "metadata": { "page": 5 }
    }
  ],
  "chatId": "chat-uuid",
  "messageId": "message-uuid"
}
```

#### Agent Explain

Spiega regole complesse in modo semplificato:

```bash
curl -X POST http://localhost:8080/agents/explain \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "gameId": "game-id-here",
    "topic": "arrocco negli scacchi"
  }'
```

#### Agent Setup Guide

Genera guide di setup passo-passo:

```bash
curl -X POST http://localhost:8080/agents/setup \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "gameId": "game-id-here",
    "playerCount": 4
  }'
```

#### Feedback su Risposte AI

```bash
curl -X POST http://localhost:8080/agents/feedback \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "messageId": "message-uuid",
    "isHelpful": true,
    "comment": "Risposta chiara e completa"
  }'
```

---

### 5. RuleSpec Editor e Versioning

#### Visualizza RuleSpec di un Gioco

**Via Web UI:**
1. Vai su `http://localhost:3000/editor`
2. Seleziona un gioco
3. Visualizza/modifica la RuleSpec JSON

**Via API:**

```bash
curl http://localhost:8080/games/{gameId}/rulespec \
  -b cookies.txt
```

#### Aggiorna RuleSpec

```bash
curl -X PUT http://localhost:8080/games/{gameId}/rulespec \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d @new-rulespec.json
```

Ogni aggiornamento crea automaticamente una nuova versione.

#### Storico Versioni

```bash
# Lista tutte le versioni
curl http://localhost:8080/games/{gameId}/rulespec/history \
  -b cookies.txt

# Ottieni versione specifica
curl http://localhost:8080/games/{gameId}/rulespec/versions/v1.2.0 \
  -b cookies.txt
```

#### Confronta Versioni (Diff)

**Via Web UI:**
1. Vai su `http://localhost:3000/versions`
2. Seleziona gioco e due versioni da confrontare
3. Visualizza le differenze

**Via API:**

```bash
curl "http://localhost:8080/games/{gameId}/rulespec/diff?from=v1.0.0&to=v1.1.0" \
  -b cookies.txt
```

---

### 6. Admin Dashboard

#### Statistiche

**Via Web UI:**
1. Login come admin (`admin@meepleai.dev` / `Demo123!`)
2. Vai su `http://localhost:3000/admin`
3. Visualizza statistiche e metriche

**Via API:**

```bash
# Statistiche generali
curl "http://localhost:8080/admin/stats?startDate=2025-10-01&endDate=2025-10-11" \
  -b cookies.txt

# Log richieste AI
curl "http://localhost:8080/admin/requests?limit=50&offset=0" \
  -b cookies.txt
```

#### Logs

```bash
# Tutti i logs
curl http://localhost:8080/logs \
  -b cookies.txt

# Filtra per endpoint
curl "http://localhost:8080/logs?endpoint=/agents/qa" \
  -b cookies.txt
```

---

### 7. n8n Workflow Automation

#### Accesso n8n UI

1. Apri `http://localhost:5678`
2. Configura workflows per automatizzare processi

#### Configurazione n8n

**Via Web UI:**
1. Login come admin
2. Vai su `http://localhost:3000/n8n`
3. Gestisci configurazioni n8n

**Via API:**

```bash
# Lista configurazioni
curl http://localhost:8080/admin/n8n \
  -b cookies.txt

# Crea nuova configurazione
curl -X POST http://localhost:8080/admin/n8n \
  -H "Content-Type: application/json" \
  -b cookies.txt \
  -d '{
    "name": "PDF Processing Workflow",
    "webhookUrl": "http://n8n:5678/webhook/pdf-process",
    "eventType": "pdf.uploaded",
    "isActive": true
  }'

# Test webhook
curl -X POST http://localhost:8080/admin/n8n/{configId}/test \
  -b cookies.txt
```

---

### 8. Chess Knowledge Base (Funzionalità Speciale)

Per il gioco Chess, c'è un sistema specializzato:

#### Indicizza Conoscenza Scacchistica

```bash
curl -X POST http://localhost:8080/chess/index \
  -b cookies.txt
```

#### Cerca nella Knowledge Base

```bash
curl "http://localhost:8080/chess/search?q=en%20passant&limit=5" \
  -b cookies.txt
```

#### Reset Indice

```bash
curl -X DELETE http://localhost:8080/chess/index \
  -b cookies.txt
```

---

## Troubleshooting

### Problemi Comuni

#### 1. Docker Compose Fallisce

**Problema**: `npm ci` error durante build del container web

**Soluzione**: Il Dockerfile è stato aggiornato per usare pnpm. Assicurati di aver fatto pull delle ultime modifiche:

```bash
git pull origin main
cd infra
docker compose down
docker compose up -d --build
```

#### 2. API Non Si Avvia

**Problema**: Errore di connessione al database

**Soluzione**:
```bash
# Verifica che PostgreSQL sia attivo
docker compose ps postgres

# Controlla logs
docker compose logs postgres

# Riavvia il database
docker compose restart postgres
```

#### 3. Qdrant Connection Failed

**Problema**: `Failed to connect to Qdrant`

**Soluzione**:
```bash
# Verifica Qdrant
curl http://localhost:6333/healthz

# Se non risponde, riavvia
docker compose restart qdrant
```

#### 4. OpenRouter API Errors

**Problema**: `401 Unauthorized` o `Invalid API key`

**Soluzione**:
1. Verifica che `OPENROUTER_API_KEY` sia impostata correttamente in `infra/env/api.env.dev`
2. Controlla che la chiave sia valida su [OpenRouter Dashboard](https://openrouter.ai/keys)
3. Riavvia l'API dopo aver modificato le env vars

#### 5. Frontend Non Si Connette all'API

**Problema**: CORS errors o "Failed to fetch"

**Soluzione**:
1. Verifica che `NEXT_PUBLIC_API_BASE` sia `http://localhost:8080` in `infra/env/web.env.dev`
2. Verifica che l'API sia raggiungibile: `curl http://localhost:8080/`
3. Controlla i logs dell'API per errori CORS

### Comandi Utili per Debug

```bash
# Visualizza logs di tutti i servizi
docker compose logs -f

# Logs di un servizio specifico
docker compose logs -f api
docker compose logs -f web

# Entra in un container
docker compose exec api bash
docker compose exec postgres psql -U meeple -d meepleai

# Riavvia un servizio
docker compose restart api

# Ricostruisci e riavvia
docker compose up -d --build api

# Stop e rimozione completa (inclusi volumi)
docker compose down -v

# Rimozione container orfani
docker compose down --remove-orphans
```

### Verifica Database

```bash
# Connetti al database
docker compose exec postgres psql -U meeple -d meepleai

# Query utili
\dt                           # Lista tabelle
SELECT * FROM games;          # Visualizza giochi
SELECT * FROM users;          # Visualizza utenti
SELECT * FROM user_sessions;  # Visualizza sessioni attive
\q                           # Esci
```

### Reset Completo

Se vuoi ricominciare da zero:

```bash
# Stop e rimozione completa
docker compose down -v

# Rimuovi environment files (opzionale)
rm infra/env/api.env.dev
rm infra/env/web.env.dev

# Riconfigura e riavvia
cp infra/env/api.env.dev.example infra/env/api.env.dev
cp infra/env/web.env.dev.example infra/env/web.env.dev
# Modifica i file .env.dev con le tue chiavi

docker compose up -d --build
```

---

## Link Rapidi

- **Web App**: http://localhost:3000
- **API**: http://localhost:8080
- **API Swagger**: http://localhost:8080/api/docs (se abilitato)
- **n8n**: http://localhost:5678
- **Qdrant Dashboard**: http://localhost:6333/dashboard
- **PostgreSQL**: localhost:5432

---

## Prossimi Passi

1. **Carica il tuo primo regolamento**: Vai su `/upload` e carica un PDF
2. **Testa la chat**: Usa `/chat` per fare domande
3. **Esplora le RuleSpec**: Guarda come sono strutturate in `/editor`
4. **Monitora le metriche**: Accedi come admin e controlla `/admin`
5. **Automatizza con n8n**: Crea workflow personalizzati

---

## Risorse Aggiuntive

- **[CLAUDE.md](../../CLAUDE.md)** - Documentazione completa per sviluppatori
- **[README.md](../../README.md)** - Panoramica del progetto
- **[docs/](../)** - Documentazione tecnica dettagliata
- **[schemas/README.md](../../schemas/README.md)** - Specifiche RuleSpec v0

---

**Buon testing!**
