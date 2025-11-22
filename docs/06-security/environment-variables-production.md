# Variabili d'Ambiente per Produzione - MeepleAI

**Versione**: 1.0
**Ultimo aggiornamento**: 2025-11-22
**Stato**: Production Ready

Questa guida fornisce un elenco completo di tutte le variabili d'ambiente da valorizzare in produzione, organizzate per categoria.

---

## Indice

1. [Database (PostgreSQL)](#1-database-postgresql)
2. [Cache (Redis)](#2-cache-redis)
3. [Vector Database (Qdrant)](#3-vector-database-qdrant)
4. [API Backend (ASP.NET)](#4-api-backend-aspnet)
5. [Frontend Web (Next.js)](#5-frontend-web-nextjs)
6. [AI/ML Services](#6-aiml-services)
7. [PDF Processing Services](#7-pdf-processing-services)
8. [Workflow Automation (n8n)](#8-workflow-automation-n8n)
9. [Observability Stack](#9-observability-stack)
10. [Security & Authentication](#10-security--authentication)
11. [Email & Alerting](#11-email--alerting)
12. [Docker Secrets](#12-docker-secrets)

---

## 1. Database (PostgreSQL)

### `POSTGRES_USER`
- **Descrizione**: Nome utente per accesso al database PostgreSQL
- **Dove**: Variabile d'ambiente Docker, file `infra/env/api.env.prod`
- **Obbligatorio**: ✅ Sì
- **Default**: `meeple`
- **Esempio**: `POSTGRES_USER=meepleai_prod`
- **Note**: Usare un nome utente diverso da quello di default in produzione

### `POSTGRES_DB`
- **Descrizione**: Nome del database PostgreSQL
- **Dove**: Variabile d'ambiente Docker, file `infra/env/api.env.prod`
- **Obbligatorio**: ✅ Sì
- **Default**: `meepleai`
- **Esempio**: `POSTGRES_DB=meepleai_production`
- **Note**: Separare database per staging/production

### `POSTGRES_PASSWORD` (via Docker Secret)
- **Descrizione**: Password per l'utente PostgreSQL
- **Dove**: Docker Secret in `infra/secrets/postgres-password.txt`
- **Obbligatorio**: ✅ Sì (via Docker Secret)
- **Default**: Nessuno (generato)
- **Esempio**: File `postgres-password.txt` contiene: `9x$kL2m#Pq8vR@nT5w`
- **Note**:
  - CRITICO: Usare password complessa (min 16 caratteri, lettere, numeri, simboli)
  - Gestire via Docker Secrets (`POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password`)
  - Rotazione ogni 90 giorni raccomandata
  - Tool: `tools/secrets/rotate-secret.sh postgres-password`

### `POSTGRES_HOST`
- **Descrizione**: Hostname del server PostgreSQL
- **Dove**: Connection string in `ConnectionStrings__Postgres`
- **Obbligatorio**: ✅ Sì
- **Default**: `localhost` (dev), `meepleai-postgres` (Docker)
- **Esempio**: `postgres.meepleai.internal` oppure RDS endpoint
- **Note**: Per managed services (AWS RDS, Azure Database) usare endpoint fornito

### `POSTGRES_PORT`
- **Descrizione**: Porta del server PostgreSQL
- **Dove**: Connection string
- **Obbligatorio**: No
- **Default**: `5432`
- **Esempio**: `POSTGRES_PORT=5432`

### `ConnectionStrings__Postgres`
- **Descrizione**: Connection string completa per EF Core
- **Dove**: Variabile d'ambiente API, `appsettings.Production.json` (override)
- **Obbligatorio**: ✅ Sì (o componenti separati)
- **Default**: Auto-costruita da variabili separate
- **Esempio**:
  ```
  Host=postgres.prod.meepleai.internal;Database=meepleai_production;Username=meepleai_app;Password_File=/run/secrets/postgres-password;Port=5432;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100;SSL Mode=Require;Trust Server Certificate=false
  ```
- **Note**:
  - In produzione SEMPRE usare `SSL Mode=Require`
  - Connection pooling configurato (10-100 connessioni)
  - Preferire `Password_File` (Docker Secret) invece di `Password`

---

## 2. Cache (Redis)

### `REDIS_URL`
- **Descrizione**: URL di connessione a Redis (per cache L2, HybridCache)
- **Dove**: Variabile d'ambiente API in `infra/env/api.env.prod`
- **Obbligatorio**: ✅ Sì (per HybridCache L2)
- **Default**: `localhost:6379` (dev), `redis:6379` (Docker)
- **Esempio**:
  - Docker: `REDIS_URL=redis:6379`
  - Managed: `REDIS_URL=redis.meepleai.cache.windows.net:6380,ssl=true,password=<secret>`
  - ElastiCache: `REDIS_URL=meepleai.abc123.0001.use1.cache.amazonaws.com:6379`
- **Note**:
  - In produzione abilitare SSL/TLS se supportato
  - Per Azure Redis Cache: porta 6380 con SSL
  - Connection timeout configurabile via `StackExchange.Redis`

### `REDIS_PASSWORD`
- **Descrizione**: Password per autenticazione Redis (se richiesta)
- **Dove**: Docker Secret `infra/secrets/redis-password.txt` o variabile d'ambiente
- **Obbligatorio**: ⚠️ Consigliato in produzione
- **Default**: Nessuna (Redis senza auth in dev)
- **Esempio**: `REDIS_PASSWORD=sK9$mP2x@nQ7vT3w`
- **Note**:
  - Configurare `requirepass` in redis.conf
  - Managed services (Azure, AWS) forniscono password automaticamente

### `REDIS_CONFIGURATION`
- **Descrizione**: Stringa di configurazione completa StackExchange.Redis
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: No (alternativa a REDIS_URL)
- **Default**: Costruita da `REDIS_URL`
- **Esempio**:
  ```
  redis:6379,password=<secret>,connectTimeout=5000,connectRetry=3,abortConnect=false,ssl=true
  ```

---

## 3. Vector Database (Qdrant)

### `QDRANT_URL`
- **Descrizione**: URL base per API REST di Qdrant
- **Dove**: Variabile d'ambiente API in `infra/env/api.env.prod`, `appsettings.json`
- **Obbligatorio**: ✅ Sì
- **Default**: `http://localhost:6333` (dev)
- **Esempio**:
  - Docker: `QDRANT_URL=http://qdrant:6333`
  - Qdrant Cloud: `QDRANT_URL=https://xyz-abc123.eu-central.aws.cloud.qdrant.io`
- **Note**:
  - Qdrant Cloud richiede API key (vedere `QDRANT_API_KEY`)
  - Self-hosted: considerare HTTPS con reverse proxy

### `QDRANT_API_KEY`
- **Descrizione**: API key per Qdrant Cloud (managed service)
- **Dove**: Docker Secret `infra/secrets/qdrant-api-key.txt` o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì per Qdrant Cloud, No per self-hosted
- **Default**: Nessuna
- **Esempio**: `QDRANT_API_KEY=qd_abc123xyz456...`
- **Note**: Ottenibile da Qdrant Cloud console

### `QDRANT_COLLECTION_NAME`
- **Descrizione**: Nome della collection Qdrant per vectors
- **Dove**: Configurazione applicazione
- **Obbligatorio**: No (creata automaticamente)
- **Default**: `game_rules`
- **Esempio**: `QDRANT_COLLECTION_NAME=meepleai_rules_prod`

---

## 4. API Backend (ASP.NET)

### `ASPNETCORE_ENVIRONMENT`
- **Descrizione**: Ambiente di esecuzione ASP.NET Core
- **Dove**: Variabile d'ambiente Docker/sistema
- **Obbligatorio**: ✅ Sì
- **Default**: `Development`
- **Esempio**: `ASPNETCORE_ENVIRONMENT=Production`
- **Note**: Valori validi: `Development`, `Staging`, `Production`

### `ASPNETCORE_URLS`
- **Descrizione**: URL di ascolto del server Kestrel
- **Dove**: Variabile d'ambiente Docker
- **Obbligatorio**: ✅ Sì
- **Default**: `http://localhost:5080`
- **Esempio**:
  - HTTP: `ASPNETCORE_URLS=http://+:8080`
  - HTTPS: `ASPNETCORE_URLS=https://+:8080;http://+:8081`
- **Note**: In produzione dietro reverse proxy (Nginx, Traefik) o usare HTTPS diretto

### `ASPNETCORE_HTTPS_PORT`
- **Descrizione**: Porta HTTPS per redirect automatico
- **Dove**: Variabile d'ambiente
- **Obbligatorio**: ⚠️ Consigliato se si usa HTTPS
- **Default**: `443`
- **Esempio**: `ASPNETCORE_HTTPS_PORT=443`

### `ASPNETCORE_Kestrel__Certificates__Default__Path`
- **Descrizione**: Path al certificato SSL/TLS (per HTTPS diretto)
- **Dove**: Variabile d'ambiente o `appsettings.Production.json`
- **Obbligatorio**: ⚠️ Solo se HTTPS diretto (no reverse proxy)
- **Default**: Nessuno
- **Esempio**: `ASPNETCORE_Kestrel__Certificates__Default__Path=/etc/ssl/certs/meepleai.pfx`

### `ASPNETCORE_Kestrel__Certificates__Default__Password`
- **Descrizione**: Password per certificato SSL/TLS
- **Dove**: Docker Secret `infra/secrets/ssl-cert-password.txt`
- **Obbligatorio**: ⚠️ Se certificato protetto da password
- **Default**: Nessuno
- **Esempio**: File contiene: `CertP@ssw0rd123`

### `JWT_ISSUER` / `JwtIssuer`
- **Descrizione**: Issuer per token JWT (per API key validation)
- **Dove**: `appsettings.json` o variabile d'ambiente
- **Obbligatorio**: ✅ Sì
- **Default**: `http://localhost:5080`
- **Esempio**: `JwtIssuer=https://api.meepleai.dev`
- **Note**: Deve corrispondere al dominio pubblico dell'API

### `ALLOW_ORIGIN` / `AllowedOrigins`
- **Descrizione**: Origini consentite per CORS (frontend URLs)
- **Dove**: `appsettings.json` (array) o variabile d'ambiente
- **Obbligatorio**: ✅ Sì
- **Default**: `["http://localhost:3000"]`
- **Esempio**:
  - Singolo: `ALLOW_ORIGIN=https://app.meepleai.dev`
  - Multipli in appsettings.json: `"AllowedOrigins": ["https://app.meepleai.dev", "https://www.meepleai.dev"]`
- **Note**: NON usare `*` in produzione per sicurezza

### `SEQ_URL`
- **Descrizione**: URL per Seq log aggregation
- **Dove**: Variabile d'ambiente API, `appsettings.json`
- **Obbligatorio**: ⚠️ Consigliato per produzione
- **Default**: `http://seq:5341`
- **Esempio**:
  - Docker: `SEQ_URL=http://seq:5341`
  - Cloud: `SEQ_URL=https://logs.meepleai.dev`
- **Note**: Porta 5341 per ingestion HTTP

### `SEQ_API_KEY`
- **Descrizione**: API key per autenticazione Seq
- **Dove**: Docker Secret o variabile d'ambiente
- **Obbligatorio**: ⚠️ Se Seq richiede autenticazione
- **Default**: Nessuna
- **Esempio**: `SEQ_API_KEY=abc123xyz456`

---

## 5. Frontend Web (Next.js)

### `NODE_ENV`
- **Descrizione**: Ambiente Node.js
- **Dove**: Variabile d'ambiente Docker
- **Obbligatorio**: ✅ Sì
- **Default**: `development`
- **Esempio**: `NODE_ENV=production`
- **Note**: Impatta build optimization, logging, error reporting

### `NEXT_PUBLIC_API_BASE`
- **Descrizione**: URL base dell'API backend (pubblico, usato lato client)
- **Dove**: File `infra/env/web.env.prod` o variabile d'ambiente
- **Obbligatorio**: ✅ Sì
- **Default**: `http://localhost:8080`
- **Esempio**:
  - Docker interno: `NEXT_PUBLIC_API_BASE=http://api:8080`
  - Pubblico: `NEXT_PUBLIC_API_BASE=https://api.meepleai.dev`
- **Note**: Prefisso `NEXT_PUBLIC_` espone variabile lato client

### `NEXT_PUBLIC_TENANT_ID`
- **Descrizione**: Identificatore tenant (per multi-tenancy futura)
- **Dove**: File `infra/env/web.env.prod`
- **Obbligatorio**: No
- **Default**: `dev`
- **Esempio**: `NEXT_PUBLIC_TENANT_ID=production`

### `NEXT_PUBLIC_SENTRY_DSN`
- **Descrizione**: Sentry DSN per error tracking frontend
- **Dove**: File `.env.production` in `apps/web/`
- **Obbligatorio**: ⚠️ Consigliato per produzione
- **Default**: Nessuno
- **Esempio**: `NEXT_PUBLIC_SENTRY_DSN=https://abc123@o123456.ingest.sentry.io/987654`
- **Note**: Ottenibile da Sentry.io project settings

### `NEXT_PUBLIC_SENTRY_ENVIRONMENT`
- **Descrizione**: Environment per Sentry (filtrare errori per ambiente)
- **Dove**: File `.env.production`
- **Obbligatorio**: ⚠️ Se si usa Sentry
- **Default**: `development`
- **Esempio**: `NEXT_PUBLIC_SENTRY_ENVIRONMENT=production`

### `SENTRY_AUTH_TOKEN`
- **Descrizione**: Token per upload source maps a Sentry
- **Dove**: Variabile d'ambiente CI/CD o Docker Secret
- **Obbligatorio**: No (solo per source maps)
- **Default**: Nessuno
- **Esempio**: `SENTRY_AUTH_TOKEN=sntrys_abc123xyz456...`

### `SENTRY_ORG` / `SENTRY_PROJECT`
- **Descrizione**: Organization e Project Sentry (per source maps upload)
- **Dove**: File `.env.production` o CI/CD
- **Obbligatorio**: No (solo se si uploadano source maps)
- **Default**: Nessuno
- **Esempio**:
  ```
  SENTRY_ORG=meepleai
  SENTRY_PROJECT=web-frontend
  ```

### Resilience Configuration (Client API)

### `NEXT_PUBLIC_RETRY_ENABLED`
- **Descrizione**: Abilita retry automatico per chiamate API
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `true`
- **Esempio**: `NEXT_PUBLIC_RETRY_ENABLED=true`

### `NEXT_PUBLIC_RETRY_MAX_ATTEMPTS`
- **Descrizione**: Numero massimo tentativi retry
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `3`
- **Esempio**: `NEXT_PUBLIC_RETRY_MAX_ATTEMPTS=5`

### `NEXT_PUBLIC_RETRY_BASE_DELAY`
- **Descrizione**: Delay iniziale tra retry (ms)
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `1000`
- **Esempio**: `NEXT_PUBLIC_RETRY_BASE_DELAY=2000`

### `NEXT_PUBLIC_RETRY_MAX_DELAY`
- **Descrizione**: Delay massimo tra retry (ms, per exponential backoff)
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `10000`
- **Esempio**: `NEXT_PUBLIC_RETRY_MAX_DELAY=30000`

### `NEXT_PUBLIC_CIRCUIT_BREAKER_ENABLED`
- **Descrizione**: Abilita circuit breaker per API calls
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `true`
- **Esempio**: `NEXT_PUBLIC_CIRCUIT_BREAKER_ENABLED=true`

### `NEXT_PUBLIC_CIRCUIT_BREAKER_FAILURE_THRESHOLD`
- **Descrizione**: Numero errori prima di aprire circuito
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `5`
- **Esempio**: `NEXT_PUBLIC_CIRCUIT_BREAKER_FAILURE_THRESHOLD=10`

### `NEXT_PUBLIC_CIRCUIT_BREAKER_SUCCESS_THRESHOLD`
- **Descrizione**: Successi necessari per chiudere circuito
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `2`
- **Esempio**: `NEXT_PUBLIC_CIRCUIT_BREAKER_SUCCESS_THRESHOLD=3`

### `NEXT_PUBLIC_CIRCUIT_BREAKER_TIMEOUT`
- **Descrizione**: Timeout circuito aperto (ms)
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `60000`
- **Esempio**: `NEXT_PUBLIC_CIRCUIT_BREAKER_TIMEOUT=120000`

### `NEXT_PUBLIC_REQUEST_DEDUP_ENABLED`
- **Descrizione**: Abilita deduplicazione richieste identiche
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `true`
- **Esempio**: `NEXT_PUBLIC_REQUEST_DEDUP_ENABLED=true`

### `NEXT_PUBLIC_REQUEST_DEDUP_TTL`
- **Descrizione**: TTL cache deduplicazione (ms)
- **Dove**: File `apps/web/.env.production`
- **Obbligatorio**: No
- **Default**: `100`
- **Esempio**: `NEXT_PUBLIC_REQUEST_DEDUP_TTL=500`

---

## 6. AI/ML Services

### `EMBEDDING_PROVIDER`
- **Descrizione**: Provider per generazione embeddings
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: ✅ Sì
- **Default**: `ollama`
- **Esempio**: `EMBEDDING_PROVIDER=openai` oppure `ollama`
- **Note**: Valori: `ollama` (locale, free), `openai` (cloud, paid)

### `EMBEDDING_MODEL`
- **Descrizione**: Nome modello per embeddings
- **Dove**: Variabile d'ambiente API, `appsettings.json`
- **Obbligatorio**: ✅ Sì
- **Default**: `nomic-embed-text`
- **Esempio**:
  - Ollama: `EMBEDDING_MODEL=nomic-embed-text`
  - OpenAI: `EMBEDDING_MODEL=text-embedding-3-small`

### `OLLAMA_URL`
- **Descrizione**: URL server Ollama (per embeddings e LLM locali)
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: ⚠️ Sì se `EMBEDDING_PROVIDER=ollama`
- **Default**: `http://localhost:11434`
- **Esempio**:
  - Docker: `OLLAMA_URL=http://ollama:11434`
  - Remote: `OLLAMA_URL=https://ollama.meepleai.internal`

### `OPENAI_API_KEY`
- **Descrizione**: API key OpenAI (se si usa OpenAI per embeddings)
- **Dove**: Docker Secret `infra/secrets/openai-api-key.txt` o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì se `EMBEDDING_PROVIDER=openai`
- **Default**: Nessuno
- **Esempio**: `OPENAI_API_KEY=sk-proj-abc123xyz456...`
- **Note**: Ottenibile da https://platform.openai.com/api-keys

### `OPENROUTER_API_KEY`
- **Descrizione**: API key OpenRouter (per LLM chat completions multi-model)
- **Dove**: Docker Secret `infra/secrets/openrouter-api-key.txt`
- **Obbligatorio**: ✅ Sì (core feature: RAG chat)
- **Default**: Nessuno
- **Esempio**: File contiene: `sk-or-v1-abc123xyz456...`
- **Note**:
  - Ottenibile da https://openrouter.ai/keys
  - Gestire via Docker Secret: `OPENROUTER_API_KEY_FILE=/run/secrets/openrouter-api-key`
  - Monitorare crediti/quota

### `LOCAL_EMBEDDING_URL`
- **Descrizione**: URL servizio embedding locale custom (AI-09)
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: No (fallback)
- **Default**: `http://embedding-service:8000`
- **Esempio**: `LOCAL_EMBEDDING_URL=http://embedding:8000`

### `EMBEDDING_FALLBACK_ENABLED`
- **Descrizione**: Abilita fallback tra provider embeddings
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: No
- **Default**: `true`
- **Esempio**: `EMBEDDING_FALLBACK_ENABLED=true`

---

## 7. PDF Processing Services

### Unstructured Service (Stage 1)

### `UNSTRUCTURED_STRATEGY`
- **Descrizione**: Strategia estrazione Unstructured
- **Dove**: Variabile d'ambiente `unstructured-service`
- **Obbligatorio**: No
- **Default**: `fast`
- **Esempio**: `UNSTRUCTURED_STRATEGY=hi_res`
- **Note**: Valori: `fast` (veloce, 80% accuracy), `hi_res` (lento, 95% accuracy)

### `LANGUAGE`
- **Descrizione**: Lingua documento per OCR/estrazione
- **Dove**: Variabile d'ambiente `unstructured-service`
- **Obbligatorio**: No
- **Default**: `ita`
- **Esempio**: `LANGUAGE=ita`

### SmolDocling Service (Stage 2)

### `DEVICE`
- **Descrizione**: Device per inferenza PyTorch (cpu/cuda)
- **Dove**: Variabile d'ambiente `smoldocling-service`
- **Obbligatorio**: ✅ Sì
- **Default**: `cpu`
- **Esempio**: `DEVICE=cuda` (con GPU NVIDIA)
- **Note**: In produzione con GPU usare `cuda` per prestazioni

### `MODEL_NAME`
- **Descrizione**: Nome modello Hugging Face per SmolDocling
- **Dove**: Variabile d'ambiente `smoldocling-service`
- **Obbligatorio**: No
- **Default**: `docling-project/SmolDocling-256M-preview`
- **Esempio**: `MODEL_NAME=docling-project/SmolDocling-256M-preview`

### `TORCH_DTYPE`
- **Descrizione**: Data type per tensori PyTorch
- **Dove**: Variabile d'ambiente `smoldocling-service`
- **Obbligatorio**: No
- **Default**: `bfloat16`
- **Esempio**: `TORCH_DTYPE=float16`
- **Note**: `bfloat16` per GPU moderne, `float32` per CPU

### `IMAGE_DPI`
- **Descrizione**: DPI per conversione PDF → immagini
- **Dove**: Variabile d'ambiente `smoldocling-service`
- **Obbligatorio**: No
- **Default**: `300`
- **Esempio**: `IMAGE_DPI=150`
- **Note**: DPI più alto = migliore qualità ma più lento

### `MAX_FILE_SIZE`
- **Descrizione**: Dimensione massima file PDF (bytes)
- **Dove**: Variabile d'ambiente entrambi i servizi PDF
- **Obbligatorio**: No
- **Default**: `52428800` (50MB)
- **Esempio**: `MAX_FILE_SIZE=104857600` (100MB)

### `QUALITY_THRESHOLD`
- **Descrizione**: Soglia qualità minima estrazione (0.0-1.0)
- **Dove**: Variabile d'ambiente servizi PDF
- **Obbligatorio**: No
- **Default**: `0.80` (Unstructured), `0.70` (SmolDocling)
- **Esempio**: `QUALITY_THRESHOLD=0.85`

---

## 8. Workflow Automation (n8n)

### `N8N_BASIC_AUTH_USER`
- **Descrizione**: Username per autenticazione n8n UI
- **Dove**: Variabile d'ambiente `infra/env/n8n.env.prod`
- **Obbligatorio**: ✅ Sì
- **Default**: `admin`
- **Esempio**: `N8N_BASIC_AUTH_USER=admin_prod`

### `N8N_BASIC_AUTH_PASSWORD`
- **Descrizione**: Password per autenticazione n8n UI
- **Dove**: Docker Secret `infra/secrets/n8n-basic-auth-password.txt`
- **Obbligatorio**: ✅ Sì
- **Default**: Nessuno (generato)
- **Esempio**: File contiene: `N8n!Pr0d@Pass123`
- **Note**: Password forte, rotazione 90 giorni

### `N8N_ENCRYPTION_KEY`
- **Descrizione**: Chiave crittografia per credenziali n8n
- **Dove**: Docker Secret `infra/secrets/n8n-encryption-key.txt`
- **Obbligatorio**: ✅ Sì
- **Default**: Nessuno (generato)
- **Esempio**: File contiene: `a1b2c3d4e5f6a7b8c9d0e1f2a3b4c5d6` (32 bytes hex)
- **Note**:
  - CRITICO: non perdere questa chiave (credenziali irrecuperabili)
  - Backup sicuro richiesto
  - Generare con: `openssl rand -hex 32`

### `DB_POSTGRESDB_HOST`
- **Descrizione**: Host database PostgreSQL per n8n
- **Dove**: Variabile d'ambiente n8n
- **Obbligatorio**: ✅ Sì (se `DB_TYPE=postgresdb`)
- **Default**: `postgres`
- **Esempio**: `DB_POSTGRESDB_HOST=postgres.internal`

### `DB_POSTGRESDB_DATABASE`
- **Descrizione**: Nome database n8n
- **Dove**: Variabile d'ambiente n8n
- **Obbligatorio**: ✅ Sì
- **Default**: `meepleai`
- **Esempio**: `DB_POSTGRESDB_DATABASE=meepleai_n8n`

### `DB_POSTGRESDB_PASSWORD`
- **Descrizione**: Password database PostgreSQL per n8n
- **Dove**: Docker Secret (stesso di `POSTGRES_PASSWORD`)
- **Obbligatorio**: ✅ Sì
- **Default**: Nessuno
- **Esempio**: Letto da `/run/secrets/postgres-password`

### `N8N_HOST`
- **Descrizione**: Hostname pubblico n8n (per webhooks)
- **Dove**: Variabile d'ambiente n8n
- **Obbligatorio**: ✅ Sì
- **Default**: `localhost`
- **Esempio**: `N8N_HOST=workflows.meepleai.dev`

### `N8N_PROTOCOL`
- **Descrizione**: Protocollo per URL webhooks
- **Dove**: Variabile d'ambiente n8n
- **Obbligatorio**: No
- **Default**: `http`
- **Esempio**: `N8N_PROTOCOL=https`

### `WEBHOOK_URL`
- **Descrizione**: URL base per webhook n8n
- **Dove**: Variabile d'ambiente n8n
- **Obbligatorio**: ✅ Sì
- **Default**: `http://localhost:5678/`
- **Esempio**: `WEBHOOK_URL=https://workflows.meepleai.dev/`

### `MEEPLEAI_API_URL`
- **Descrizione**: URL API MeepleAI (per workflow HTTP requests)
- **Dove**: Variabile d'ambiente n8n
- **Obbligatorio**: ✅ Sì
- **Default**: `http://api:8080`
- **Esempio**: `MEEPLEAI_API_URL=https://api.meepleai.dev`

### `N8N_SERVICE_SESSION`
- **Descrizione**: Session token service account n8n
- **Dove**: Variabile d'ambiente n8n
- **Obbligatorio**: ⚠️ Sì per workflow automatici
- **Default**: Nessuno
- **Esempio**: `N8N_SERVICE_SESSION=eyJhbGciOiJIUzI1NiIsInR5cCI6IkpXVCJ9...`
- **Note**: Generare con `tools/setup-n8n-service-account.ps1`

---

## 9. Observability Stack

### Seq (Log Aggregation)

### `SEQ_FIRSTRUN_ADMINPASSWORDHASH`
- **Descrizione**: Hash password admin Seq (primo avvio)
- **Dove**: Variabile d'ambiente Seq
- **Obbligatorio**: ⚠️ Consigliato in produzione
- **Default**: Nessuno (no autenticazione)
- **Esempio**: `SEQ_FIRSTRUN_ADMINPASSWORDHASH=$2a$10$abc123...`
- **Note**: Generare con Seq CLI: `seq hash <password>`

### Prometheus (Metrics)

### `PROMETHEUS_RETENTION_TIME`
- **Descrizione**: Durata retention metriche Prometheus
- **Dove**: Command args Docker Compose (`--storage.tsdb.retention.time`)
- **Obbligatorio**: No
- **Default**: `30d` (dev), `90d` (prod)
- **Esempio**: `--storage.tsdb.retention.time=180d`

### `PROMETHEUS_RETENTION_SIZE`
- **Descrizione**: Dimensione massima storage Prometheus
- **Dove**: Command args Docker Compose (`--storage.tsdb.retention.size`)
- **Obbligatorio**: No
- **Default**: `50GB` (prod)
- **Esempio**: `--storage.tsdb.retention.size=100GB`

### Grafana (Dashboards)

### `GF_SECURITY_ADMIN_USER`
- **Descrizione**: Username admin Grafana
- **Dove**: Variabile d'ambiente Grafana
- **Obbligatorio**: No
- **Default**: `admin`
- **Esempio**: `GF_SECURITY_ADMIN_USER=grafana_admin`

### `GF_SECURITY_ADMIN_PASSWORD`
- **Descrizione**: Password admin Grafana
- **Dove**: Docker Secret `infra/secrets/grafana-admin-password.txt`
- **Obbligatorio**: ✅ Sì
- **Default**: `admin` (CAMBIARE!)
- **Esempio**: File contiene: `Gr@f@na!Pr0d123`
- **Note**: Grafana forza cambio password al primo login se default

### `GF_SERVER_ROOT_URL`
- **Descrizione**: URL pubblico Grafana (per link esterni)
- **Dove**: Variabile d'ambiente Grafana
- **Obbligatorio**: ⚠️ Consigliato
- **Default**: `http://localhost:3001`
- **Esempio**: `GF_SERVER_ROOT_URL=https://metrics.meepleai.dev`

### `GF_USERS_ALLOW_SIGN_UP`
- **Descrizione**: Permetti registrazione utenti Grafana
- **Dove**: Variabile d'ambiente Grafana
- **Obbligatorio**: No
- **Default**: `false`
- **Esempio**: `GF_USERS_ALLOW_SIGN_UP=false`
- **Note**: In produzione SEMPRE `false`

### Jaeger (Tracing)

### `COLLECTOR_OTLP_ENABLED`
- **Descrizione**: Abilita collector OTLP in Jaeger
- **Dove**: Variabile d'ambiente Jaeger
- **Obbligatorio**: ✅ Sì (per OpenTelemetry)
- **Default**: `true`
- **Esempio**: `COLLECTOR_OTLP_ENABLED=true`

### `SPAN_STORAGE_TYPE`
- **Descrizione**: Tipo storage span Jaeger
- **Dove**: Variabile d'ambiente Jaeger
- **Obbligatorio**: ✅ Sì
- **Default**: `badger` (dev), `elasticsearch` (prod consigliato)
- **Esempio**: `SPAN_STORAGE_TYPE=elasticsearch`
- **Note**: Produzione: usare Elasticsearch/Cassandra per persistence

---

## 10. Security & Authentication

### `INITIAL_ADMIN_EMAIL`
- **Descrizione**: Email utente admin iniziale (bootstrap)
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: ✅ Sì (primo avvio)
- **Default**: `admin@meepleai.dev`
- **Esempio**: `INITIAL_ADMIN_EMAIL=admin@production.meepleai.dev`
- **Note**: Creato solo se nessun admin esiste

### `INITIAL_ADMIN_PASSWORD`
- **Descrizione**: Password utente admin iniziale
- **Dove**: Docker Secret `infra/secrets/initial-admin-password.txt`
- **Obbligatorio**: ✅ Sì (primo avvio)
- **Default**: Nessuno (generato)
- **Esempio**: File contiene: `Adm1n!Pr0d@2025`
- **Note**:
  - Requisiti: min 8 caratteri, 1 maiuscola, 1 cifra
  - Cambiare dopo primo login

### `INITIAL_ADMIN_DISPLAY_NAME`
- **Descrizione**: Nome visualizzato admin iniziale
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: No
- **Default**: `System Admin`
- **Esempio**: `INITIAL_ADMIN_DISPLAY_NAME=Production Administrator`

### OAuth Providers

### `GOOGLE_OAUTH_CLIENT_ID`
- **Descrizione**: Client ID Google OAuth 2.0
- **Dove**: Variabile d'ambiente API (sostituita in appsettings.json)
- **Obbligatorio**: ⚠️ Sì per Google OAuth
- **Default**: Nessuno
- **Esempio**: `GOOGLE_OAUTH_CLIENT_ID=123456789-abc.apps.googleusercontent.com`
- **Note**: Ottenibile da Google Cloud Console

### `GOOGLE_OAUTH_CLIENT_SECRET`
- **Descrizione**: Client Secret Google OAuth 2.0
- **Dove**: Docker Secret `infra/secrets/google-oauth-secret.txt` o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì per Google OAuth
- **Default**: Nessuno
- **Esempio**: File contiene: `GOCSPX-abc123xyz456...`
- **Note**: Mantenere segreto, rotazione se compromesso

### `DISCORD_OAUTH_CLIENT_ID`
- **Descrizione**: Client ID Discord OAuth 2.0
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: ⚠️ Sì per Discord OAuth
- **Default**: Nessuno
- **Esempio**: `DISCORD_OAUTH_CLIENT_ID=1234567890123456789`
- **Note**: Ottenibile da Discord Developer Portal

### `DISCORD_OAUTH_CLIENT_SECRET`
- **Descrizione**: Client Secret Discord OAuth 2.0
- **Dove**: Docker Secret o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì per Discord OAuth
- **Default**: Nessuno
- **Esempio**: `DISCORD_OAUTH_CLIENT_SECRET=abc123xyz456_secret`

### `GITHUB_OAUTH_CLIENT_ID`
- **Descrizione**: Client ID GitHub OAuth
- **Dove**: Variabile d'ambiente API
- **Obbligatorio**: ⚠️ Sì per GitHub OAuth
- **Default**: Nessuno
- **Esempio**: `GITHUB_OAUTH_CLIENT_ID=Iv1.abc123xyz456`
- **Note**: Ottenibile da GitHub Developer Settings

### `GITHUB_OAUTH_CLIENT_SECRET`
- **Descrizione**: Client Secret GitHub OAuth
- **Dove**: Docker Secret o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì per GitHub OAuth
- **Default**: Nessuno
- **Esempio**: `GITHUB_OAUTH_CLIENT_SECRET=abc123xyz456secret789...`

### `OAuth__CallbackBaseUrl`
- **Descrizione**: URL base per callback OAuth (redirect URI)
- **Dove**: `appsettings.Production.json` o variabile d'ambiente
- **Obbligatorio**: ✅ Sì se si usa OAuth
- **Default**: `http://localhost:5080`
- **Esempio**: `https://api.meepleai.dev`
- **Note**: Deve essere registrato nelle console OAuth providers

---

## 11. Email & Alerting

### `GMAIL_APP_PASSWORD`
- **Descrizione**: App Password Gmail per invio email alert
- **Dove**: Docker Secret `infra/secrets/gmail-app-password.txt`
- **Obbligatorio**: ⚠️ Sì per email alerts
- **Default**: Nessuno
- **Esempio**: File contiene: `abcdefghijklmnop` (16 caratteri, no spazi)
- **Note**:
  - NON usare password Gmail normale
  - Creare App Password: https://myaccount.google.com/apppasswords
  - Richiede 2FA abilitato

### `SMTP_HOST`
- **Descrizione**: Server SMTP per invio email
- **Dove**: `appsettings.Production.json` (sezione `Alerting.Email`)
- **Obbligatorio**: ⚠️ Sì per email alerts
- **Default**: `smtp.gmail.com`
- **Esempio**:
  - Gmail: `smtp.gmail.com`
  - SendGrid: `smtp.sendgrid.net`
  - AWS SES: `email-smtp.eu-west-1.amazonaws.com`

### `SMTP_PORT`
- **Descrizione**: Porta server SMTP
- **Dove**: `appsettings.Production.json`
- **Obbligatorio**: No
- **Default**: `587` (TLS)
- **Esempio**: `587` (TLS) o `465` (SSL)

### `SMTP_USERNAME`
- **Descrizione**: Username autenticazione SMTP
- **Dove**: `appsettings.Production.json` o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì per la maggior parte dei provider
- **Default**: Nessuno
- **Esempio**:
  - Gmail: email completa `alerts@meepleai.dev`
  - SendGrid: `apikey`

### `SMTP_PASSWORD`
- **Descrizione**: Password autenticazione SMTP
- **Dove**: Docker Secret o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì
- **Default**: Nessuno
- **Esempio**: App Password o API key del provider

### `ALERTING_EMAIL_FROM`
- **Descrizione**: Indirizzo email mittente alert
- **Dove**: `appsettings.Production.json` (sezione `Alerting.Email.From`)
- **Obbligatorio**: ⚠️ Sì per email alerts
- **Default**: `alerts@meepleai.dev`
- **Esempio**: `alerts@meepleai.dev`

### `ALERTING_EMAIL_TO`
- **Descrizione**: Lista destinatari email alert (array)
- **Dove**: `appsettings.Production.json` (sezione `Alerting.Email.To`)
- **Obbligatorio**: ⚠️ Sì per email alerts
- **Default**: `["ops@meepleai.dev"]`
- **Esempio**: `["ops@meepleai.dev", "admin@meepleai.dev"]`

### `SLACK_WEBHOOK_URL`
- **Descrizione**: Webhook URL Slack per notifiche alert
- **Dove**: `appsettings.Production.json` (sezione `Alerting.Slack.WebhookUrl`) o variabile d'ambiente
- **Obbligatorio**: ⚠️ Sì per Slack alerts
- **Default**: Nessuno
- **Esempio**: `https://hooks.slack.com/services/T123/B456/abc123xyz456`
- **Note**: Ottenibile creando Incoming Webhook in Slack workspace

### `PAGERDUTY_INTEGRATION_KEY`
- **Descrizione**: Integration key PagerDuty per alert critici
- **Dove**: `appsettings.Production.json` (sezione `Alerting.PagerDuty.IntegrationKey`)
- **Obbligatorio**: ⚠️ Sì per PagerDuty integration
- **Default**: Nessuno
- **Esempio**: `PAGERDUTY_INTEGRATION_KEY=abc123xyz456def789...`
- **Note**: Ottenibile da PagerDuty service integration

---

## 12. Docker Secrets

MeepleAI usa Docker Secrets per gestire credenziali sensibili. I secret sono file in `infra/secrets/` (gitignored).

### Inizializzazione Secrets

```bash
# Generare tutti i secret necessari
cd infra
./tools/secrets/init-secrets.sh

# Verificare secret creati
./tools/secrets/list-secrets.sh

# Rotazione singolo secret
./tools/secrets/rotate-secret.sh postgres-password
```

### Lista Secret Richiesti

| Secret File | Descrizione | Obbligatorio | Tool Generazione |
|-------------|-------------|--------------|------------------|
| `postgres-password.txt` | Password PostgreSQL | ✅ Sì | `init-secrets.sh` |
| `openrouter-api-key.txt` | API key OpenRouter | ✅ Sì | Manuale (da OpenRouter) |
| `initial-admin-password.txt` | Password admin iniziale | ✅ Sì | `init-secrets.sh` |
| `n8n-encryption-key.txt` | Chiave crittografia n8n | ✅ Sì | `init-secrets.sh` |
| `n8n-basic-auth-password.txt` | Password n8n UI | ✅ Sì | `init-secrets.sh` |
| `grafana-admin-password.txt` | Password admin Grafana | ✅ Sì | `init-secrets.sh` |
| `gmail-app-password.txt` | App Password Gmail | ⚠️ Per email | Manuale (Google) |
| `openai-api-key.txt` | API key OpenAI | ⚠️ Opzionale | Manuale (OpenAI) |
| `google-oauth-secret.txt` | Client Secret Google OAuth | ⚠️ Per OAuth | Manuale (Google Cloud) |
| `discord-oauth-secret.txt` | Client Secret Discord OAuth | ⚠️ Per OAuth | Manuale (Discord) |
| `github-oauth-secret.txt` | Client Secret GitHub OAuth | ⚠️ Per OAuth | Manuale (GitHub) |
| `redis-password.txt` | Password Redis | ⚠️ Consigliato | `init-secrets.sh` |
| `qdrant-api-key.txt` | API key Qdrant Cloud | ⚠️ Per Qdrant Cloud | Manuale (Qdrant) |
| `ssl-cert-password.txt` | Password certificato SSL | ⚠️ Se HTTPS diretto | Manuale |

### Esempio Mount Secret in Docker Compose

```yaml
services:
  api:
    secrets:
      - postgres-password
      - openrouter-api-key
    environment:
      POSTGRES_PASSWORD_FILE: /run/secrets/postgres-password
      OPENROUTER_API_KEY_FILE: /run/secrets/openrouter-api-key

secrets:
  postgres-password:
    file: ./secrets/postgres-password.txt
  openrouter-api-key:
    file: ./secrets/openrouter-api-key.txt
```

---

## Checklist Pre-Produzione

### ✅ Database
- [ ] `POSTGRES_USER` diverso da default
- [ ] `POSTGRES_DB` production-specific
- [ ] `POSTGRES_PASSWORD` forte (16+ caratteri) via Docker Secret
- [ ] Connection string con `SSL Mode=Require`
- [ ] Connection pooling configurato (10-100)
- [ ] Backup automatici configurati

### ✅ API Backend
- [ ] `ASPNETCORE_ENVIRONMENT=Production`
- [ ] `ASPNETCORE_URLS` con HTTPS o dietro reverse proxy
- [ ] `AllowedOrigins` limitate a domini frontend reali
- [ ] `JwtIssuer` corrisponde a URL pubblico
- [ ] `OPENROUTER_API_KEY` valorizzato e con crediti sufficienti
- [ ] `INITIAL_ADMIN_PASSWORD` forte e cambiato dopo primo login

### ✅ Frontend
- [ ] `NODE_ENV=production`
- [ ] `NEXT_PUBLIC_API_BASE` punta a URL API pubblico
- [ ] `NEXT_PUBLIC_SENTRY_DSN` configurato (error tracking)
- [ ] Circuit breaker e retry abilitati

### ✅ Caching & Performance
- [ ] `REDIS_URL` configurato con SSL se managed service
- [ ] `HybridCache.EnableL2Cache=true`
- [ ] `QDRANT_URL` con HTTPS se pubblico

### ✅ Observability
- [ ] `SEQ_URL` configurato o log shipping alternativo
- [ ] Grafana password cambiata da default
- [ ] Prometheus retention configurato (90d+ produzione)
- [ ] Jaeger storage persistente (Elasticsearch)

### ✅ Security
- [ ] Tutti i secret gestiti via Docker Secrets (no plaintext)
- [ ] OAuth client secrets valorizzati se OAuth abilitato
- [ ] SSL/TLS abilitato per tutti i servizi pubblici
- [ ] CORS configurato (no `*`)
- [ ] Rate limiting configurato
- [ ] Security headers abilitati

### ✅ Email & Alerting
- [ ] `GMAIL_APP_PASSWORD` configurato o provider SMTP alternativo
- [ ] `ALERTING_EMAIL_TO` con email operative reali
- [ ] Slack webhook configurato (opzionale)
- [ ] PagerDuty integration per alert critici (opzionale)

### ✅ n8n Workflows
- [ ] `N8N_ENCRYPTION_KEY` generato e salvato in backup sicuro
- [ ] `N8N_BASIC_AUTH_PASSWORD` forte
- [ ] `WEBHOOK_URL` con HTTPS
- [ ] Service account session token configurato

### ✅ PDF Processing
- [ ] `DEVICE=cuda` se disponibile GPU (performance 10x)
- [ ] `MAX_FILE_SIZE` adeguato (100MB+ se necessario)
- [ ] Quality thresholds configurati (0.80/0.70)

---

## Best Practices

### 1. Gestione Secret
- ✅ Usare Docker Secrets o secret manager (AWS Secrets Manager, Azure Key Vault, HashiCorp Vault)
- ✅ Rotazione periodica (90 giorni consigliati)
- ✅ Backup sicuro delle chiavi critiche (encryption keys)
- ❌ MAI committare `.env` files con valori reali
- ❌ MAI loggare valori secret

### 2. Sicurezza
- Usare HTTPS per tutte le comunicazioni esterne
- Abilitare SSL/TLS per database e cache
- Limitare CORS a domini specifici
- Implementare rate limiting
- Monitorare accessi anomali

### 3. Monitoring
- Configurare alerting per:
  - API errors (5xx > 1%)
  - Database connection failures
  - Redis cache failures
  - LLM API quota exhausted
  - Disk space < 20%
- Retention logs: 30-90 giorni
- Dashboards Grafana per metriche chiave

### 4. Performance
- Abilitare HybridCache L2 (Redis)
- Connection pooling per database
- CDN per static assets frontend
- Compressione Brotli/Gzip
- Caching CDN per API responses (se applicabile)

### 5. Disaster Recovery
- Backup database automatici (daily, retention 30d+)
- Backup Qdrant vectors (weekly)
- Backup n8n workflows (git repository)
- Backup configurazioni (Infrastructure as Code)
- Testare restore procedure (quarterly)

---

## Appendici

### A. Esempio Configurazione Completa Produzione

```bash
# File: infra/env/api.env.prod

# Environment
ASPNETCORE_ENVIRONMENT=Production
ASPNETCORE_URLS=https://+:8080

# Database
POSTGRES_USER=meepleai_app
POSTGRES_DB=meepleai_production
POSTGRES_PASSWORD_FILE=/run/secrets/postgres-password
ConnectionStrings__Postgres=Host=postgres.prod.internal;Database=meepleai_production;Username=meepleai_app;Password_File=/run/secrets/postgres-password;SSL Mode=Require;Pooling=true;Minimum Pool Size=10;Maximum Pool Size=100

# Cache & Vectors
REDIS_URL=redis.prod.internal:6379
QDRANT_URL=https://meepleai-prod.cloud.qdrant.io

# AI Services
EMBEDDING_PROVIDER=ollama
OLLAMA_URL=http://ollama.prod.internal:11434
EMBEDDING_MODEL=nomic-embed-text
OPENROUTER_API_KEY_FILE=/run/secrets/openrouter-api-key

# Application
JWT_ISSUER=https://api.meepleai.dev
ALLOW_ORIGIN=https://app.meepleai.dev,https://www.meepleai.dev

# Observability
SEQ_URL=https://logs.meepleai.internal:5341

# Bootstrap
INITIAL_ADMIN_EMAIL=admin@meepleai.dev
INITIAL_ADMIN_PASSWORD_FILE=/run/secrets/initial-admin-password
INITIAL_ADMIN_DISPLAY_NAME=Production Administrator
```

### B. Tool per Validazione Configurazione

```bash
# Validare che tutti i secret richiesti esistano
cd infra
./tools/secrets/validate-secrets.sh

# Output esempio:
# ✅ postgres-password.txt exists
# ✅ openrouter-api-key.txt exists
# ❌ gmail-app-password.txt MISSING (required for email alerts)
# ...
```

### C. Template Docker Compose Produzione

Vedere `infra/compose.prod.yml` per configurazione completa con:
- Resource limits (CPU, RAM)
- Health checks
- Restart policies
- Secrets management
- Network isolation
- Volume persistence

---

## Support & Troubleshooting

### Problema: API non si avvia, errore connection string
**Soluzione**: Verificare che `POSTGRES_PASSWORD_FILE` punti al secret corretto e che il file esista:
```bash
docker exec meepleai-api cat /run/secrets/postgres-password
```

### Problema: n8n perde credenziali salvate dopo restart
**Soluzione**: `N8N_ENCRYPTION_KEY` cambiato o perso. Ripristinare chiave originale da backup.

### Problema: OpenRouter API ritorna 401 Unauthorized
**Soluzione**:
1. Verificare `OPENROUTER_API_KEY` corretto
2. Controllare crediti account: https://openrouter.ai/credits

### Problema: Email alerts non funzionano
**Soluzione**:
1. Verificare `GMAIL_APP_PASSWORD` corretto (16 caratteri, no spazi)
2. Controllare 2FA abilitato su account Gmail
3. Testare credenziali SMTP manualmente

---

**Domande?** Consultare:
- [docs/INDEX.md](../INDEX.md) - Indice completo documentazione
- [docs/06-security/docker-secrets-migration.md](docker-secrets-migration.md) - Migrazione a Docker Secrets
- [SECURITY.md](../../SECURITY.md) - Security policy generale

**Maintainer**: Engineering Team
**Contact**: ops@meepleai.dev
