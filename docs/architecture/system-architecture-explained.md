<!--
  MeepleAI System Architecture — Spiegazione completa
  Design: Quicksand (headings) + Nunito (body)
  Brand: Orange #d2691e | Purple #8b5cf6 | Green #2d7a4c | Amber #fbbf24
-->

<div align="center">

# MeepleAI — Architettura di Sistema

**Assistente AI per giochi da tavolo** · RAG · Multi-Agent · Living Docs

`v2.0` · `.NET 9` · `Next.js 16` · `Python AI` · `Docker Compose`

[Diagramma PDF](../../claudedocs/MeepleAI-Architecture.pdf)

</div>

---

## Indice

1. [Panoramica](#panoramica)
2. [Client Layer](#-1-client-layer)
3. [Application Layer](#-2-application-layer)
4. [External APIs](#-3-external-apis)
5. [Persistence Layer](#-4-persistence-layer)
6. [AI/ML Services](#-5-aiml-services)
7. [Observability & Automation](#-6-observability--automation)
8. [Mappa connessioni](#mappa-completa-delle-connessioni)
9. [Flussi di dati](#flussi-di-dati-principali)
10. [Audit: box senza connessione nel PDF](#audit-box-senza-connessione-nel-diagramma-pdf)
11. [Docker Compose](#docker-compose-profili-e-deployment)
12. [Glossario](#glossario)

---

## Panoramica

MeepleAI è organizzato come **monorepo** con architettura a microservizi, orchestrato tramite Docker Compose sulla rete bridge `meepleai`. L'architettura si divide in **6 zone funzionali**:

| | Zona | Colore diagramma | Responsabilità |
|---|------|:---:|---|
| **1** | **Client Layer** | 🟠 Ambra | Interfaccia utente, routing, proxy |
| **2** | **Application Layer** | 🔵 Blu | Logica di business — CQRS + DDD |
| **3** | **External APIs** | 🔴 Rosa | Servizi terze parti — LLM, BGG |
| **4** | **Persistence Layer** | 🟣 Viola | Storage — SQL, Vector, Cache, Blob |
| **5** | **AI/ML Services** | 🟢 Teal | Pipeline AI — embedding, reranking, agenti |
| **6** | **Observability** | ⚫ Grigio | Monitoraggio, workflow, alerting |

> **Rete**: tutti i servizi comunicano sulla rete bridge Docker `meepleai`. I nomi di servizio fungono da hostname DNS interni (es. `http://embedding-service:8000`).

---

## 🟠 1. Client Layer

> *Punto di ingresso per gli utenti — browser, PWA mobile*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **Browser** | — | React 19, Tailwind 4, shadcn/ui | SPA renderizzata nel browser. Chat AI, upload PDF, libreria, admin |
| **Next.js 16** | `:3000` | App Router + SSR + Zustand | Framework full-stack. Catch-all proxy verso API, middleware auth |

**Dettagli Next.js:**
- **Proxy catch-all**: `/api/v1/*` → backend .NET `:8080`
- **Middleware auth**: controlla cookie `meepleai_session` prima del rendering
- **Rotte protette**: `/dashboard`, `/chat`, `/upload`, `/admin`, `/editor`, `/settings`
- **Rotte pubbliche**: `/`, `/login`, `/register`
- **Cache sessione**: in-memory, TTL 2 min, max 200 entry
- **Design system**: glassmorphic `bg-white/70 backdrop-blur-md`, font Quicksand (heading) + Nunito (body)

### Connessioni

```
Browser ──renders──▶ Next.js 16
Next.js ──HTTP proxy /api/v1/*──▶ .NET API (:8080)
Next.js ◀──SSE streaming──────── .NET API
Next.js ◀──WebSocket (SignalR)── .NET API
```

---

## 🔵 2. Application Layer

> *Cuore del sistema — tutta la logica di business transita da qui*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **.NET 9 API** | `:8080` | ASP.NET Core 9, MediatR, EF Core, FluentValidation | API backend CQRS + DDD. 13 Bounded Context |

**Regola critica**: gli endpoint usano **SOLO** `IMediator.Send()` — zero iniezione diretta di servizi.

**Resilienza**: Polly per retry (3 tentativi) + circuit breaker verso servizi esterni.

### I 13 Bounded Context

| | Bounded Context | Responsabilità |
|---|----------------|---------------|
| 🔐 | **Authentication** | Login, sessioni, OAuth (Google/Discord/GitHub), 2FA |
| 🎲 | **GameManagement** | Catalogo giochi, sessioni di gioco, FAQ, specifiche |
| 🧠 | **KnowledgeBase** | RAG, agenti AI, chat, ricerca vettoriale |
| 🌐 | **SharedGameCatalog** | Database comunitario giochi con soft-delete |
| 📄 | **DocumentProcessing** | Upload PDF, estrazione testo, chunking |
| 👤 | **Administration** | Utenti, ruoli, audit trail, analytics |
| 📚 | **UserLibrary** | Collezioni, wishlist, storico |
| 🏆 | **Gamification** | Achievement, badge, classifiche |
| 📋 | **SessionTracking** | Note di sessione, punteggi, attività |
| 🔔 | **UserNotifications** | Notifiche, email, push |
| ⚙️ | **WorkflowIntegration** | n8n, webhook, logging |
| 🎛️ | **SystemConfiguration** | Config runtime, feature flag |
| 💼 | **BusinessSimulations** | Registrazioni contabili, scenari, previsioni |

### Connessioni

| Destinazione | Protocollo | Freccia nel PDF | Descrizione |
|-------------|:---------:|:---:|-------------|
| Next.js | HTTP + SSE + WS | ✅ | Risposte, streaming AI, real-time |
| OpenRouter | HTTPS + SSE | ✅ | Inferenza LLM (Claude/GPT) |
| BoardGameGeek | HTTPS + XML | ✅ | Import catalogo giochi |
| PostgreSQL | TCP `:5432` | ✅ | EF Core + pgvector |
| Qdrant | HTTP `:6333` / gRPC `:6334` | ✅ | Ricerca vettoriale |
| Redis | TCP `:6379` | ✅ | HybridCache L2 + sessioni |
| S3 / MinIO | HTTP `:9000` | ✅ | Blob storage PDF |
| Embedding | HTTP POST `/embeddings` | ✅ | Generazione embeddings |
| Reranker | HTTP POST `/rerank` | ✅ | Re-ranking risultati |
| Orchestration | HTTP POST `/execute` | ✅ | Pipeline multi-agente |
| Unstructured | HTTP `:8001` | ✅ | Estrazione PDF Stage 1 |
| SmolDocling | HTTP `:8002` | ⚠️ | Estrazione PDF Stage 2 (implicita in label "S1/S2") |
| Ollama | HTTP `:11434` | ❌ | Fallback LLM locale (solo se OpenRouter down) |
| Prometheus | HTTP `/metrics` scrape | ✅ | Metriche per scraping |
| Mailpit | SMTP `:1025` | ❌ | Email dev (connessione non disegnata) |

---

## 🔴 3. External APIs

> *Servizi terze parti via Internet — LLM e dati giochi*

### Servizi

| Servizio | Porta | Descrizione |
|----------|:-----:|-------------|
| **OpenRouter** | `:443` | Gateway LLM multi-provider (Claude, GPT). HTTPS + SSE streaming |
| **BoardGameGeek** | `:443` | API XML v2 per catalogo giochi da tavolo |

**OpenRouter:**
- Config: `infra/secrets/openrouter.secret`
- Resilienza: circuit breaker + 3 retry con backoff esponenziale (Polly)
- Fallback: OpenRouter down → Ollama locale → messaggio "servizio non disponibile"

**BGG:**
- Config: `infra/secrets/bgg.secret` (opzionale)
- Rate limiting per rispettare i limiti BGG

### Connessioni

| Sorgente | Protocollo | Freccia nel PDF | Descrizione |
|---------|:---------:|:---:|-------------|
| .NET API → OpenRouter | HTTPS + SSE | ✅ | Inferenza LLM |
| .NET API → BGG | HTTPS + XML | ✅ | Import giochi |
| Orchestration → OpenRouter | HTTPS diretto | ✅ | LLM diretto per agenti (bypassa API .NET) |

---

## 🟣 4. Persistence Layer

> *Storage dati — relazionale, vettoriale, cache, blob*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **PostgreSQL 16** | `:5432` | EF Core + pgvector | DB relazionale + vettoriale. Soft delete, audit, concurrency |
| **Qdrant** | `:6333` / `:6334` | Vector DB nativo | Ricerca vettoriale (cosine similarity). Solo via API .NET |
| **Redis 7.4** | `:6379` | HybridCache L2 | Cache distribuita + sessioni + cache semantica |
| **S3 / MinIO** | `:9000` | S3-compatible | Blob storage per PDF. AES256, URL pre-firmati |

**Pattern dati:**
- **Soft delete**: `IsDeleted` + `DeletedAt` (mai cancellazione fisica)
- **Audit trail**: `CreatedAt`, `UpdatedAt`, `CreatedBy`, `UpdatedBy`
- **Concurrency**: `RowVersion` con `DbUpdateConcurrencyException`

**Volumi persistenti:** `postgres_data`, `redis_data`, `qdrant_data`

### Connessioni

| Sorgente | Protocollo | Freccia nel PDF | Descrizione |
|---------|:---------:|:---:|-------------|
| .NET API → PostgreSQL | TCP (EF Core) | ✅ | CRUD, migrations, pgvector |
| .NET API → Qdrant | HTTP / gRPC | ✅ | Ricerca vettoriale |
| .NET API → Redis | TCP | ✅ | Cache L2, sessioni |
| .NET API → S3 | HTTP (S3 API) | ✅ | Upload/download PDF |
| Orchestration → PostgreSQL | TCP (asyncpg) | ✅ | Stato conversazioni (diretto) |
| Orchestration → Redis | TCP (redis.asyncio) | ✅ | Cache intent + regole (diretto) |
| n8n → PostgreSQL | TCP | ✅ | Stato workflow n8n |

> **Nota**: l'Orchestration Service accede a PostgreSQL e Redis **direttamente** tramite `asyncpg` e `redis.asyncio`, bypassando l'API .NET. Necessario per lo stato delle conversazioni multi-agente.

---

## 🟢 5. AI/ML Services

> *Pipeline AI — tutti microservizi Python con FastAPI*

### Servizi

| Servizio | Porta | Modello | Descrizione |
|----------|:-----:|---------|-------------|
| **Embedding** | `:8000` | `intfloat/multilingual-e5-large` (1024 dim) | Genera vettori dal testo. L2 normalized. 5 lingue |
| **Reranker** | `:8003` | `BAAI/bge-reranker-v2-m3` | Riordina risultati per rilevanza. Batch 32, 100 req/min |
| **Orchestration** | `:8004` | LangGraph Multi-Agent | Coordina agenti: Tutor, Arbitro, Decisore |
| **Unstructured** | `:8001` | Unstructured.io | PDF Stage 1 (veloce). Strategia `fast`, italiano |
| **SmolDocling** | `:8002` | `SmolDocling-256M-preview` | PDF Stage 2 (VLM). Max 20 pag, DPI 300, OCR avanzato |
| **Ollama** | `:11434` | Configurabili (llama, mistral) | Fallback LLM locale |

**Orchestration Service — peer bidirezionale dell'API .NET:**

L'API .NET chiama Orchestration via `POST /execute`. L'Orchestration **richiama** l'API .NET via `callback: /api/v1/kb/search` per la ricerca ibrida.

| Connessione diretta | Libreria | Motivo |
|---|---|---|
| → PostgreSQL | `asyncpg` | Stato conversazioni multi-agente |
| → Redis | `redis.asyncio` | Cache intent + regole |
| → OpenRouter | `httpx` | Chiamate LLM dirette per gli agenti |
| → .NET API | HTTP callback | `/api/v1/kb/search` per ricerca ibrida |
| → Embedding | HTTP | Health check |
| → Reranker | HTTP | Health check |

**Risorse Docker (profilo `ai`):**

| Servizio | CPU (limit) | RAM (limit) | CPU (res.) | RAM (res.) |
|----------|:-----------:|:-----------:|:----------:|:----------:|
| Embedding | 2 | 4 GB | 1 | 2 GB |
| Reranker | 2 | 2 GB | 1 | 1 GB |
| Orchestration | 2 | 4 GB | 1 | 2 GB |
| Unstructured | 2 | 2 GB | 1 | 1 GB |
| SmolDocling | 2 | 4 GB | 1 | 2 GB |
| Ollama | 4 | 8 GB | 2 | 4 GB |

### Connessioni

| Sorgente / Destinazione | Freccia nel PDF | Descrizione |
|---|:---:|---|
| .NET API → Embedding | ✅ | `POST /embeddings` |
| .NET API → Reranker | ✅ | `POST /rerank` |
| .NET API → Orchestration | ✅ | `POST /execute` |
| .NET API → Unstructured | ✅ | Estrazione PDF Stage 1 |
| .NET API → SmolDocling | ⚠️ | Stage 2 (label "S1/S2" copre entrambi, freccia solo a Unstructured) |
| Orchestration → PostgreSQL | ✅ | asyncpg diretto |
| Orchestration → Redis | ✅ | redis.asyncio diretto |
| Orchestration → OpenRouter | ✅ | LLM diretto |
| Orchestration ↔ .NET API | ✅ | Callback bidirezionale |
| Orchestration → Embedding/Reranker | ✅ | Health check (linee sottili) |
| .NET API → Ollama | ❌ | Fallback condizionale (vedi audit sotto) |

---

## ⚫ 6. Observability & Automation

> *Monitoraggio, alerting, workflow, strumenti di sviluppo*

### Servizi

| Servizio | Porta | Stack | Descrizione |
|----------|:-----:|-------|-------------|
| **Prometheus** | `:9090` | TSDB metriche | Scrape HTTP da tutti i servizi. Retention 30d |
| **Grafana** | `:3001` | Dashboard | Visualizzazione metriche. Datasource: Prometheus |
| **n8n** | `:5678` | Workflow automation | Webhook, scheduling. Usa PostgreSQL come DB |
| **Mailpit** | `:8025` / `:1025` | SMTP dev | Cattura email in locale. UI web su `:8025` |
| **Alertmanager** | `:9093` | Alert routing | Gestione alert da Prometheus |
| **cAdvisor** | `:8082` | Container metrics | CPU, memoria, I/O dei container Docker |
| **Node Exporter** | `:9100` | Host metrics | Metriche OS (CPU, disco, rete) |

**Target di scraping Prometheus:**
- .NET API `:8080/metrics`
- Embedding `:8000/metrics`
- Reranker `:8003/metrics`
- Orchestration `:8004/metrics`
- Unstructured `:8001/metrics`
- SmolDocling `:8002/metrics`
- cAdvisor `:8082/metrics`
- Node Exporter `:9100/metrics`

### Connessioni

| Sorgente / Destinazione | Freccia nel PDF | Descrizione |
|---|:---:|---|
| Prometheus → .NET API | ✅ | Scrape `/metrics` (edge-route sinistra) |
| Prometheus → AI/ML Services | ❌ | Scrape metriche (non disegnato nel PDF) |
| Grafana → Prometheus | ✅ | Query PromQL (linea interna) |
| .NET API → Mailpit | ❌ | SMTP `:1025` (non disegnato nel PDF) |
| Prometheus → Alertmanager | ❌ | Alert routing (non disegnato nel PDF) |
| n8n → PostgreSQL | ✅ | Stato workflow (linea cross-zona) |
| Prometheus → cAdvisor | ❌ | Metriche container (non nel PDF) |
| Prometheus → Node Exporter | ❌ | Metriche host (non nel PDF) |

---

## Mappa completa delle connessioni

### Connessioni principali (linee solide nel PDF)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 1 | Next.js | .NET API | HTTP proxy | `/api/v1/* catch-all proxy` |
| 2 | .NET API | OpenRouter | HTTPS + SSE | `LLM Inference (SSE stream)` |
| 3 | .NET API | PostgreSQL | TCP (EF Core) | `EF Core + pgvector` |
| 4 | .NET API | Qdrant | HTTP/gRPC | `Vector Search (cosine)` |
| 5 | .NET API | Embedding | HTTP POST | `POST /embeddings` |
| 6 | .NET API | Reranker | HTTP POST | `POST /rerank` |
| 7 | .NET API | Orchestration | HTTP POST | `POST /execute` |

### Connessioni secondarie (linee tratteggiate nel PDF)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 8 | .NET API | BGG | HTTPS + XML | `XML API v2 + rate limit` |
| 9 | .NET API | Redis | TCP | `Cache + Sessions` |
| 10 | .NET API | S3/MinIO | HTTP (S3) | `Blob Storage` |
| 11 | .NET API | Unstructured | HTTP | `PDF Extract (S1/S2)` |

### Connessioni cross-zona dirette (aggiunte in v7)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 12 | Orchestration | PostgreSQL | TCP (asyncpg) | `asyncpg (conversation state)` |
| 13 | Orchestration | Redis | TCP (redis.asyncio) | `redis.asyncio (intent + rule cache)` |
| 14 | Orchestration | OpenRouter | HTTPS | `LLM (direct)` |
| 15 | Orchestration | .NET API | HTTP callback | `callback: /api/v1/kb/search` |
| 16 | n8n | PostgreSQL | TCP | `n8n → PG (workflow state)` |

### Connessioni osservabilità (edge-route nel PDF)

| # | Sorgente | Destinazione | Protocollo | Label nel PDF |
|:-:|---------|-------------|-----------|-------------|
| 18 | Prometheus | .NET API | HTTP scrape | `scrape /metrics` (edge sinistra) |
| 19 | Grafana | Prometheus | HTTP | Linea interna |

### Connessioni intra-zona (linee sottili nel PDF)

| # | Sorgente | Destinazione | Descrizione |
|:-:|---------|-------------|-------------|
| 20 | Orchestration | Embedding | Health check |
| 21 | Orchestration | Reranker | Health check |

---

## Audit: box senza connessione nel diagramma PDF

Alcuni servizi nel PDF non hanno frecce visibili. Ecco perch&eacute;:

| Box nel PDF | Freccia | Motivo |
|---|:---:|---|
| **SmolDocling** | ⚠️ | La freccia "PDF Extract (S1/S2)" va al nodo Unstructured. SmolDocling &egrave; coperto implicitamente dalla label "(S1/S2)" ma non ha una freccia propria. L'API .NET chiama SmolDocling solo se Stage 1 fallisce (qualit&agrave; < 80%) |
| **Ollama** | ❌ | Nessuna freccia. Ollama &egrave; un **fallback condizionale**: viene chiamato dall'API .NET solo se OpenRouter &egrave; irraggiungibile. In condizioni normali non riceve traffico |
| **Mailpit** | ❌ | Nessuna freccia. L'API .NET invia email via SMTP `:1025` a Mailpit, ma questa connessione non &egrave; stata disegnata nel PDF per evitare ulteriore complessit&agrave; visiva |
| **Alertmanager** | ❌ | Nessuna freccia. Prometheus invia alert ad Alertmanager via HTTP, ma la connessione non &egrave; nel PDF |
| **cAdvisor** | — | Non presente nel PDF (solo nel documento). Prometheus scrape le sue metriche su `:8082` |
| **Node Exporter** | — | Non presente nel PDF (solo nel documento). Prometheus scrape le sue metriche su `:9100` |

> Tutti i box "senza freccia" hanno connessioni reali nel sistema. Non sono stati disegnati nel PDF per mantenere leggibilit&agrave; — il diagramma mostra gi&agrave; 21 connessioni.

---

## Flussi di dati principali

### 1. RAG Chat

> Utente fa una domanda → risposta AI con citazioni

```
Browser → Next.js :3000 → .NET API :8080
  1. → Embedding :8000       genera vettore dalla query
  2. → Qdrant :6333          ricerca vettoriale (cosine similarity)
  3. → Reranker :8003        riordina per rilevanza
  4. → OpenRouter :443       genera risposta con contesto (LLM)
  5. ← SSE streaming         risposta → Next.js → Browser
```

### 2. PDF Upload

> Utente carica un regolamento → indicizzazione automatica

```
Browser → Next.js :3000 → .NET API :8080
  1. → S3/MinIO :9000        salva PDF originale
  2. → Unstructured :8001    estrazione testo (Stage 1, veloce)
  3.   se qualità < 80%:
       → SmolDocling :8002   estrazione (Stage 2, VLM/OCR avanzato)
  4. → Chunking              nel .NET API (2000 chars, 200 overlap)
  5. → Embedding :8000       genera vettori per ogni chunk
  6. → Qdrant :6333          salva vettori indicizzati
```

### 3. Multi-Agent

> Richiesta complessa → orchestrazione con pi&ugrave; agenti AI

```
Browser → Next.js → .NET API :8080
  → Orchestration :8004 (LangGraph)
    ├── Scelta agente: Tutor | Arbitro | Decisore
    ├── → .NET API /api/v1/kb/search   callback: ricerca ibrida
    ├── → OpenRouter :443              LLM diretto per agente
    ├── → PostgreSQL :5432             stato conversazione (asyncpg)
    └── → Redis :6379                  cache intent (redis.asyncio)
  ← Risposta → .NET API → Next.js → Browser
```

### 4. Import giochi da BGG

```
.NET API :8080 → BGG API :443 (XML v2)
  → Parse XML → Validate dati
  → PostgreSQL :5432  salva dati gioco
  → Redis :6379       cache risultati
```

### 5. Autenticazione

```
Browser → Next.js middleware → .NET API /api/v1/auth/login
  → PostgreSQL     verifica credenziali
  → Redis          crea sessione
  → Set cookie     meepleai_session (HttpOnly, SameSite=Lax)
  → Next.js        middleware controlla cookie su ogni richiesta
```

### 6. Real-time

```
.NET API :8080 (SignalR Hub) → WebSocket → Next.js → Browser
  Eventi: stato gioco, notifiche, chat live
```

---

## Docker Compose: profili e deployment

| Profilo | Servizi | Uso |
|---------|---------|-----|
| `minimal` | PostgreSQL, Redis, Qdrant, API, Web | Dev base senza AI |
| `dev` | minimal + Prometheus, Grafana, Mailpit, Alertmanager, cAdvisor | Dev con monitoraggio |
| `ai` | dev + Embedding, Reranker, Orchestration, Unstructured, SmolDocling | Dev con AI completa |
| `full` | ai + n8n, Ollama | Stack completo |

**Volumi persistenti:**

| Volume | Servizio | Contenuto |
|--------|----------|-----------|
| `postgres_data` | PostgreSQL | Dati relazionali + vettoriali |
| `redis_data` | Redis | Cache + sessioni |
| `qdrant_data` | Qdrant | Indici vettoriali |
| `grafana_data` | Grafana | Dashboard configurate |

---

## Glossario

| Termine | Significato |
|---------|------------|
| **CQRS** | Command Query Responsibility Segregation — separazione letture/scritture |
| **DDD** | Domain-Driven Design — design guidato dal dominio di business |
| **MediatR** | Libreria .NET per il pattern mediator — disaccoppia handler |
| **RAG** | Retrieval-Augmented Generation — generazione AI potenziata da retrieval |
| **SSE** | Server-Sent Events — streaming unidirezionale server → client |
| **pgvector** | Estensione PostgreSQL per ricerca vettoriale |
| **Cross-encoder** | Modello che compara direttamente query-documento per re-ranking |
| **LangGraph** | Framework Python per orchestrazione di agenti AI |
| **HybridCache** | Cache .NET multi-livello (L1 in-memory + L2 Redis) |
| **Soft delete** | Eliminazione logica (`IsDeleted`) invece di cancellazione fisica |
| **Circuit breaker** | Pattern di resilienza che interrompe chiamate a servizi falliti |
| **asyncpg** | Driver PostgreSQL asincrono per Python (usato dall'Orchestration) |

---

<div align="center">

*Ultimo aggiornamento: 2026-02-19* · *Diagramma: [`MeepleAI-Architecture.pdf`](../../claudedocs/MeepleAI-Architecture.pdf)*

</div>
