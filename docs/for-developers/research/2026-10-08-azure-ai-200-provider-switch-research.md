# Ricerca — Portare MeepleAI su Azure come palestra per l'esame AI-200, con switch fra profili (locale · ibrido · Azure)

**Data**: 2026-10-08 · **Tipo**: ricerca (non è una decisione; le decisioni vanno in ADR) · **Profondità**: standard (web + inventario del repo)
**Convenzione**: i prezzi sono listino USD pay-as-you-go di ottobre 2026, regione US salvo nota; dove la pagina ufficiale non espone il valore la fonte è secondaria ed è marcata `(sec.)`. I conteggi su file/test non compaiono: dove serve un numero c'è il comando per misurarlo.

---

## 1. Sommario esecutivo

**AI-200 non è l'esame "chiamo i servizi cognitivi".** È il successore di AZ-204, ribattezzato per l'era AI: misura la *piattaforma sotto* un'applicazione AI — container (ACR, Container Apps, AKS), dati (Cosmos DB NoSQL, **PostgreSQL con pgvector**, Azure Managed Redis), messaggistica (Service Bus, Event Grid, Functions), sicurezza e osservabilità (Key Vault, App Configuration, OpenTelemetry, KQL). L'esame "costruisci l'app AI con Foundry/agenti" è AI-103, che ha sostituito AI-102 (ritirato il 30 giugno 2026). Confidenza alta: lo dice la study guide ufficiale, aggiornata il 2026-05-05.

**MeepleAI è un caso di studio quasi perfetto per AI-200**: già oggi è un'app containerizzata su PostgreSQL + pgvector + Redis, con ricerca ibrida, outbox, OTel e config a runtime. Ogni dominio dell'esame mappa su un componente esistente. La migrazione "come esercizio" è fattibile senza riscrivere il prodotto.

**Lo switch fra profili non parte da zero, ma è disomogeneo**: solo l'LLM si cambia a runtime (DB + fallback chain + kill switch). Embedding, estrattore PDF, storage ed email si scelgono all'avvio da env/`appsettings` e vogliono un riavvio; non esiste alcun client Azure, né `Microsoft.Extensions.AI`, né un exporter OTLP. Il lavoro vero è portare gli altri sottosistemi allo stesso modello dell'LLM, poi aggiungere gli adapter Azure.

**Quattro vincoli decidono il piano** (dettaglio in §5):
1. Le immagini del repo sono **solo `linux/arm64`** (VPS Hetzner Ampere); **Azure Container Apps accetta solo `linux/amd64`**. Serve un build multi-arch prima di qualunque deploy.
2. Il **free trial Azure ha quota 0 sui modelli LLM** di Azure OpenAI: i token si pagano solo passando a pay-as-you-go (i crediti restano spendibili).
3. La colonna vettoriale è **`vector(768)` fissa**: cambiare provider di embedding significa reindicizzare, quindi un "profilo" deve possedere il proprio spazio vettoriale, non solo un endpoint.
4. I servizi Python (embedding/reranker/unstructured, ~10 GB di immagine ciascuno con torch) sono il vero costo su Azure: in un profilo "esame" conviene sostituirli con embedding gestiti (Azure OpenAI o Cohere su Foundry) oppure lasciarli dove sono.

**Economia, in breve** (§7): il profilo "esame" costa tra zero e poche decine di dollari al mese se si usano il free account (12 mesi) o Azure for Students e si spegne ciò che non serve; un profilo Azure sempre acceso con i servizi AI self-hosted supera di molto il VPS Hetzner (~€16/mese di listino per un CAX31).

---

## 2. L'esame AI-200 in sintesi

| Voce | Valore | Fonte |
|---|---|---|
| Nome | AI-200 — *Developing AI Cloud Solutions on Azure* · certificazione *Azure AI Cloud Developer Associate* | [Study guide](https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-200) |
| Cosa sostituisce | AZ-204 (Azure Developer Associate), da luglio 2026 | [aguidetocloud](https://www.aguidetocloud.com/cert-tracker/ai-200/), [examos](https://examos.io/blog/ai-103-vs-ai-200-which-should-you-take) |
| Non confondere con | AI-103 (sostituisce AI-102): app e agenti con Foundry, vision, text, info extraction | [certcrush](https://www.certcrush.app/blog/ai-102-vs-ai-103-which-azure-ai-cert-2026) |
| Punteggio | 700/1000 | study guide |
| Prezzo | $165 USD (listino; in EUR dipende da Pearson VUE al momento della prenotazione) `(sec.)` | [examcert](https://www.examcert.app/blog/azure-ai-200-requirements-2026/) |
| Voucher gratuiti | Microsoft li distribuisce a eventi (AI Skills Fest giugno 2026, Cloud Skills Challenge); AI-200 non risultava nelle liste di giugno, da riverificare a ogni evento | [techcommunity](https://techcommunity.microsoft.com/discussions/skills-hub-discussions/what-are-all-the-ways-to-get-discount-or-free-vouchers-for-microsoft-certificati/4456263) |
| Linguaggio atteso | **Python** è elencato fra le competenze; nessun vincolo sul linguaggio dell'app | study guide |
| Corso ufficiale | AI-200T00, 9 moduli (container hosting · Container Apps · AKS · Cosmos DB · PostgreSQL · Managed Redis · backend services · secrets/config · observability) | [Global Knowledge](https://www.globalknowledge.com/en-be/courses/microsoft/artificial_intelligence/m-ai200/co) |

### Competenze misurate (verbatim dalla study guide, riassunte)

**A. Develop containerized solutions on Azure (20–25%)**
- ACR: build, store, versioning; **ACR Tasks**; deploy su **App Service** con env e secret.
- **Container Apps**: environment, revisioni, **KEDA**; **AKS** con manifest; troubleshooting con log, eventi, connettività end-to-end.

**B. Develop AI solutions by using Azure data management services (25–30%)**
- **Cosmos DB NoSQL**: SDK, query, indexing policy, consistency, RU; **embedding e vector similarity search**; **change feed processor**.
- **Azure Database for PostgreSQL**: SDK, schema e indici, **ridurre il costo compute di pgvector**, dimensionare compute/memoria/storage per i vettori, **RAG con filtro sui metadati**, ottimizzazione delle connessioni.
- **Azure Managed Redis**: cache, expiration, invalidation; **vector indexing** per similarity search.

**C. Connect to and consume Azure services (20–25%)**
- **Service Bus** (code, topic, subscription, **dead-letter**); **Event Grid** (filtri, eventi custom, retry).
- **Azure Functions**: trigger e binding, configurazione e deploy.

**D. Secure, monitor, and troubleshoot Azure solutions (20–25%)**
- **Key Vault** (rotazione, lettura); **App Configuration**.
- **OpenTelemetry SDK** per il tracing distribuito; **KQL** su log e metriche.

Nota: **Azure AI Search, Document Intelligence e Azure OpenAI non compaiono** nella study guide. Sono servizi utili al prodotto, non all'esame; vanno trattati come opzionali (§4, righe "fuori esame").

---

## 3. Mappa: dominio d'esame ↔ componente MeepleAI ↔ servizio Azure ↔ alternativa economica

Legenda colonna "Astrazione oggi": **R** = switch a runtime già esistente · **A** = scelta all'avvio (env/`IOptions`), serve riavvio · **–** = nessuna astrazione, implementazione unica · **B** = astrazione esiste ma bypassata in più punti.

| Dominio | Componente MeepleAI (path) | Servizio Azure (esame) | Alternativa locale / esterna | Astrazione oggi |
|---|---|---|---|---|
| A · container | `apps/api/src/Api/Dockerfile`, `apps/web/Dockerfile`, `.github/workflows/deploy-staging.yml` (GHCR, arm64) | ACR + Container Apps (KEDA) · AKS opzionale | Hetzner CAX31 + compose (attuale) · Docker Desktop | – (pipeline arm64-only) |
| B · Postgres | `PgVectorStoreAdapter.cs`, `HybridSearchService.cs`, `IKeywordSearchService` (FTS), colonna `vector(768)` | Azure Database for PostgreSQL Flexible Server + `vector` + `pg_diskann` | Postgres 16 + pgvector in compose (attuale) | – (una sola impl. di `IVectorStoreAdapter`, SQL raw) |
| B · Cosmos DB | nessuno | Cosmos DB NoSQL (vector policy, change feed) | nessuna (emulatore Cosmos DB in Docker, solo amd64) | – (nuovo, solo esercizio) |
| B · Redis | `InfrastructureServiceExtensions.cs:267-275` (host/port/password, **no TLS**), HybridCache L2 spento, `EmergencyOverrideService` | Azure Managed Redis (Azure Cache for Redis è in ritiro) | Redis in compose (attuale) | A (manca connection string/TLS) |
| B · embedding | `IEmbeddingProvider` + `EmbeddingProviderFactory` (enum: OpenRouter 3-small/large, Ollama, HF bge-m3, External=e5) | Azure OpenAI `text-embedding-3-*` · Cohere embed v3 multilingual su Foundry · `azure_ai` in Postgres | `apps/embedding-service` (e5-base 768, attuale) · Ollama | A |
| C · messaging | outbox: `StorageOperationOutboxBackgroundService`, domain events post-commit (ADR in `docs/for-developers/architecture/domain-events-post-commit-contract.md`) | Service Bus (code + DLQ) · Event Grid (Blob created → indicizzazione) | tabella outbox + `BackgroundService` (attuale) | – (nessun broker) |
| C · Functions | job in-process (`ModelAvailabilityCheckJob`, outbox) | Azure Functions (Service Bus / Timer / Event Grid trigger) | `BackgroundService` (attuale) | – |
| D · secret/config | `infra/secrets/*.secret` + `IProviderCredentialResolver` (DB → env, cache 5 min) + `IConfigurationService` + `IFeatureFlagService` | Key Vault + App Configuration (+ Managed Identity) | file `.secret` + DB (attuale) | R per credenziali e chiavi generiche |
| D · observability | `ObservabilityServiceExtensions.cs:99` → solo `AddPrometheusExporter`; Serilog → Seq | Azure Monitor OTel Distro / exporter OTLP → Application Insights; KQL | Prometheus + Grafana + Seq in compose (attuale) | A (manca OTLP) |
| LLM (fuori esame) | `ILlmClient` (OpenRouter, DeepSeek, Ollama) + `LlmProviderSelector` + `AiModelConfiguration` in DB | Azure OpenAI / Foundry Models (OpenAI-compatible) | OpenRouter (attuale) · Ollama · **Foundry Local** | **R** · B (chiamate dirette a OpenRouter fuori da `ILlmClient`, §4) |
| Reranker (fuori esame) | `ICrossEncoderReranker` → `apps/reranker-service` (bge-reranker-v2-m3) | Semantic ranker di AI Search (fuori esame, Basic+) · Cohere Rerank su Foundry | reranker self-hosted (attuale) · spento (`EnableReranking=false` oggi) | A |
| PDF (fuori esame) | `IPdfTextExtractor` (Docnet, Unstructured, SmolDocling, Orchestrated) keyed services | Document Intelligence (Layout/Read) | servizi Python (attuale) · Docnet puro .NET | A (keyed, aggiungere uno stage è semplice) |
| Storage (fuori esame) | `BlobStorageServiceFactory` (`STORAGE_PROVIDER` = local/s3) | Azure Blob Storage (non parla S3) | locale · R2/MinIO (attuale) | **B** (`IAmazonS3` iniettato direttamente nelle pipeline cover, seed reader, backup, outbox, migrazione) |
| Email (fuori esame) | `EMAIL_PROVIDER` resend/smtp | Azure Communication Services Email (ha relay SMTP) | Mailpit/Resend (attuale) | A (ACS via SMTP = zero codice) |
| SignalR | `Program.cs` `AddSignalR()` senza backplane | Azure SignalR Service | istanza singola (attuale) | – (blocca lo scale-out, rilevante per KEDA) |

Riferimenti verificati durante l'inventario (path relativi ad `apps/api/src/Api/`): `Services/LlmClients/ILlmClient.cs`, `Services/LlmClients/LlmProviderFactory.cs` (switch solo OpenRouter/Ollama), `BoundedContexts/KnowledgeBase/Infrastructure/EmbeddingProviders/EmbeddingProviderFactory.cs`, `Infrastructure/EntityConfigurations/KnowledgeBase/PgVectorEmbeddingEntityConfiguration.cs:35` (`vector(768)`), `Services/Pdf/BlobStorageServiceFactory.cs:19-30`, `Extensions/ObservabilityServiceExtensions.cs:99`, `BoundedContexts/SystemConfiguration/Application/Validators/CreateAiModelCommandValidator.cs:14` (accetta già la stringa `"Azure"` come provider, ma nessun client la gestisce).

---

## 4. Stato delle astrazioni: cosa c'è e cosa manca

### Già pronto da riusare (il modello LLM)
- `ILlmClient.SupportsModel(modelId)` + `LlmProviderSelector` (circuit breaker, health, fallback chain letta dal DB, quota RPD) + `AiModelConfiguration` (provider/model/priority/tier/environment in DB) + kill switch Redis `llm:emergency:force-ollama-only`.
- Credenziali: `IProviderCredentialResolver` risolve DB → env var con cache di 5 minuti e invalidazione pub/sub. Allowlist in `ProviderName.cs` (solo `deepseek`, `openrouter`) e mappa env in `ProviderEnvVarMap.cs`.
- Per aggiungere Azure OpenAI all'LLM basta: un `AzureOpenAiLlmClient`, un valore nell'enum `LlmProvider`, un ramo in `LlmProviderFactory`, le stringhe fisse in `LlmProviderSelector`, `ProviderName` + `ProviderEnvVarMap`.

### Debito che blocca uno switch pulito
- **Chiamate dirette a OpenRouter fuori da `ILlmClient`**: `ChunkTranslationService.cs:38`, `VisionOcrAdapter.cs:28,46`, `OpenRouterService.cs:14`, `OpenRouterUsageService.cs:144`, `ModelAvailabilityCheckJob.cs:217`, `OpenRouterQuotaProvider.cs:20`; lato web `apps/web/src/app/api/chat-proxy/route.ts:47,126`; lato Python `apps/orchestration-service/src/application/arbitro_agent.py:13,58` (`ChatOpenAI` con chiave OpenRouter: basta cambiare `base_url`).
- **`IAmazonS3` iniettato direttamente** in `PdfCoverUploadPipeline`, `BggCoverUploadPipeline`, `CoverR2UploadPipeline`, `S3SeedBlobReader`, `RagBackupStorageService`, `StorageOperationOutboxBackgroundService`, `ReverseStorageMigrationCommandHandler`, più controlli sparsi `STORAGE_PROVIDER == "s3"`. Azure Blob non parla S3: senza un refactor dietro `IBlobStorageService` un provider `azureblob` coprirebbe solo l'upload PDF.
- **Vettori**: `IVectorStoreAdapter` espone il tipo `Pgvector.Vector` e `EnsureCollectionExistsAsync(gameId, vectorDimension = 768)`; ricerca ibrida con fusione RRF in app (`HybridSearch` 0.7/0.3, `FuseGlobally` in `MultiGameHybridSearchService`). Vale il vincolo di #3737: la fusione congiuntiva e i pesi sono tarati sull'e5 `passage:`; un embedding diverso richiede una ritaratura misurata con l'artifact `rag-fusion-tuning`, non a mente.
- **Incoerenze di configurazione trovate**: `appsettings.json` ha `Embedding.Provider = OllamaMxbai` a 1024 dimensioni mentre la colonna è `vector(768)` (staging forza `External` e5-base 768 in `infra/compose.staging.yml:86-93`); il commento in `EmbeddingProviderType.cs:44` dice e5-large 1024 ma il codice usa e5-base 768; `IPdfTextExtractor` (Docnet) è registrato due volte (`ApplicationServiceExtensions.cs:173-174` e `DocumentProcessingServiceExtensions.cs`); `VisionOcrAdapter` è definito ma non registrato nel DI.
- **Redis** si costruisce da `REDIS_HOST/PORT/PASSWORD` senza connection string né TLS: Azure Managed Redis richiede TLS sulla 10000.
- **OTel** esporta solo verso Prometheus; nessun exporter OTLP, nessun Azure Monitor. Serilog → Seq, non Application Insights.
- **Immagini** solo arm64 (`deploy-staging.yml:529,545,735`); Unstructured viene ricompilato sul VPS (`:1213`); SmolDocling non ha immagine su GHCR.

### Microsoft.Extensions.AI: adottarlo o no?
`Microsoft.Extensions.AI` (pacchetti `Microsoft.Extensions.AI.Abstractions` + `Microsoft.Extensions.AI`) definisce `IChatClient` e `IEmbeddingGenerator<TInput,TEmbedding>` con middleware DI (`UseOpenTelemetry`, `UseDistributedCache`, `UseFunctionInvocation`) e provider per OpenAI/Azure OpenAI, Azure AI Inference e Ollama (OllamaSharp implementa l'interfaccia). È GA e documentato su [learn.microsoft.com](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai). Raccomandazione: **non sostituire** `ILlmClient`/`IEmbeddingProvider` (hanno semantica di dominio: costi, tier, purpose query/passage, lingua) ma **implementarli sopra** `IChatClient`/`IEmbeddingGenerator` per i provider nuovi (Azure OpenAI, Foundry, Ollama). Si ottengono gratis telemetria OTel per chiamata (rilevante per il dominio D) e un unico client per "tutto ciò che è OpenAI-compatible", OpenRouter incluso. Confidenza media sui nomi esatti dei pacchetti provider: verificare su NuGet prima dell'implementazione.

---

## 5. Vincoli e trappole scoperti

| # | Vincolo | Evidenza | Impatto sul piano |
|---|---|---|---|
| 1 | **Container Apps accetta solo `linux/amd64`** | Risposta Microsoft Q&A (2024) e feature request ancora aperta su `microsoft/azure-container-apps` #569; nessun annuncio GA/preview trovato per 2025–2026 (confidenza media: riverificare sul roadmap prima di iniziare) | Build **multi-arch** (`linux/amd64,linux/arm64`) in `deploy-staging.yml`. Nota positiva: amd64 è nativo sul runner GitHub, quindi i build torch che oggi impiegano 25–55 min sotto QEMU sarebbero più veloci su amd64. Il mapping `$TARGETARCH` → `dotnet --arch` va esteso (`amd64`→`x64`, già commentato nel Dockerfile). AKS invece supporta arm64. |
| 2 | **Quota 0 sui modelli Azure OpenAI nel free trial** | Microsoft Q&A: «Free trials get $200 but have a 0-quota limit on LLMs»; serve l'upgrade a pay-as-you-go, i crediti restano | Non pianificare l'LLM su Azure finché la sottoscrizione non è PAYG. Per l'esercizio "esame" l'LLM può restare OpenRouter/Ollama/Foundry Local. |
| 3 | **Spazio vettoriale fisso a 768** | `vector(768)` in `PgVectorEmbeddingEntityConfiguration.cs:35` + `EmbeddingDimensionHealthCheck` | Un profilo deve dichiarare provider **e** dimensione e possedere la propria tabella/colonna (o un corpus separato). Cambiare provider = reindicizzare (`VectorReembeddingService` esiste già). `text-embedding-3-small` è 1536, Cohere v3 è 1024, e5-base è 768. |
| 4 | **I servizi Python sono il costo** | immagini ~10 GB (embedding, unstructured), 5,5 GB reranker; torch su CPU | Su Container Apps un replica sempre accesa da 1 vCPU/2 GiB costa ~$63 + ~$16/mese `(sec.)`. Nel profilo esame: embedding gestito (Cohere multilingual $0.10/1M token; `text-embedding-3-small` ~$0.02/1M) e reranker spento (lo è già in `ResilientRetrieval:EnableReranking=false`). |
| 5 | **Azure AI Search non è economico né in esame** | Free: 3 indici, 50 MB, servizio condiviso che può essere cancellato per inattività; Basic ≈ $74–76/mese `(sec.)` | Non usarlo come SSOT. pgvector su Flexible Server copre il dominio B ed è gratuito 12 mesi. Eventuale adapter AI Search = esercizio opzionale fuori esame. |
| 6 | **Document Intelligence F0 legge solo le prime 2 pagine** | 500 pagine/mese, max 2 pagine per documento, 4 MB `(sec.)`; S0 Layout ≈ $10/1000 pagine | Inutile per i regolamenti (decine di pagine) salvo test; con S0 un regolamento da 60 pagine costa ~$0.60. Fuori esame: esercizio opzionale come nuovo stage di `OrchestratedPdfTextExtractor`. |
| 7 | **Azure Cache for Redis è in ritiro** | creazione bloccata per i nuovi clienti dal 2026-04-01; Standard/Premium ritirati il 2028-09-30 | Puntare solo ad **Azure Managed Redis** (B0 1 GB ≈ $13/mese `(sec.)`). |
| 8 | **Cosmos DB free tier solo provisioned** | 1000 RU/s + 25 GB, uno per sottoscrizione, **non** per account serverless; DiskANN richiede ≥1000 vettori per quantizzare | Usare provisioned con free tier per l'esercizio; sotto i 1000 vettori la query fa scan (va bene per imparare, non per misurare). |
| 9 | **SignalR senza backplane** | `Program.cs` `AddSignalR()` | Con KEDA e più repliche le sessioni live si rompono. Serve Azure SignalR Service o backplane Redis **prima** di scalare oltre 1. |
| 10 | **Secret `.secret` + staging source of truth** | `make secrets-sync` tira da staging | Key Vault può diventare la sorgente per il profilo Azure senza toccare gli altri profili: `IProviderCredentialResolver` ha già la catena DB → env; aggiungere Key Vault come terzo anello è coerente. |
| 11 | **Drift nel manuale operativo** | `operations-manual.md` elenca il VPS come CPX41 €29.52; `infra/hetzner/` e la memoria di progetto dicono CAX31 arm64 (listino 2026 ≈ €16 `(sec.)`) | Correggere la tabella costi quando si scrive il confronto economico. |

---

## 6. Design proposto: profili di deployment switchabili

Obiettivo dichiarato: «poter cambiare facilmente tra strumenti Azure, a pagamento, locale con servizi esterni, o altre configurazioni economicamente sensate». Proposta in tre livelli, dal meno al più invasivo.

### 6.1 Profili come composizione di provider, non come ambienti
Un profilo è una tabella `componente → provider`, e **non** coincide con l'ambiente (dev/staging). Quattro profili iniziali:

| Componente | `local` | `hybrid` (oggi) | `azure-exam` | `azure-paid` |
|---|---|---|---|---|
| Hosting | compose | Hetzner + compose | Container Apps (scale-to-zero, min 0) | Container Apps min 1 / AKS |
| Postgres + vettori | pgvector | pgvector | Flexible Server B1ms free + `vector`/`pg_diskann` | Flexible Server GP |
| Cache | Redis | Redis | Managed Redis B0 | Managed Redis B3+ |
| LLM | Ollama / Foundry Local | OpenRouter | OpenRouter (quota 0 su AOAI in trial) | Azure OpenAI / Foundry Models |
| Embedding | e5 locale | e5 self-hosted | Cohere v3 multilingual (1024) o AOAI 3-small (1536) | idem |
| Reranker | off | self-hosted | off | Cohere Rerank / self-hosted su ACA |
| PDF | Docnet | Unstructured | Docnet (+ Document Intelligence opzionale) | Unstructured su ACA |
| Storage | locale | R2 | Blob (Azurite in dev) | Blob |
| Messaging | outbox in-process | outbox in-process | Service Bus Basic + Event Grid | Service Bus Standard |
| Secret/config | `.secret` | `.secret` | Key Vault + App Configuration free | idem |
| Telemetria | Prometheus/Seq | Prometheus/Seq | OTLP → Application Insights | idem |

### 6.2 Meccanismo: tre modifiche strutturali
1. **Factory per-richiesta con `IOptionsMonitor`/`IConfigurationService`** per embedding, PDF, storage, email (oggi la factory gira una volta nel `ConfigureServices`). Ogni factory legge `Providers:<componente>` da DB con fallback env, come già fa `LlmProviderSelector`. Lo switch diventa un toggle in `/admin/config` (pattern `RegistrationMode`).
2. **Spazio vettoriale per profilo**: `EmbeddingProfile { Provider, Model, Dimensions, TableSuffix }`; l'adapter sceglie la tabella/colonna dal profilo attivo; `VectorReembeddingService` popola il nuovo spazio in background; lo switch del profilo di lettura avviene solo a reindex completo (la stessa guardia doppia del reindex corpus, `ProcessingState` + `ProcessingJob`, ADR-086).
3. **Astrazione dei bypass**: `IBlobStorageService` esteso a copertura delle pipeline cover/seed/backup; le chiamate dirette a OpenRouter portate dentro `ILlmClient` (con `SupportsVision` per il Vision OCR) e il `chat-proxy` del web fatto passare dall'API.

### 6.3 Dove usare gli SDK Azure (senza accoppiare il dominio)
- `IChatClient`/`IEmbeddingGenerator` dietro `ILlmClient`/`IEmbeddingProvider` (§4).
- `Azure.Storage.Blobs` dietro `IBlobStorageService` (provider `azureblob`, dev con Azurite).
- `Azure.Messaging.ServiceBus` dietro un `IOutboxTransport` (oggi il trasporto è implicito nel `BackgroundService`).
- `Azure.Extensions.AspNetCore.Configuration.Secrets` + `Microsoft.Azure.AppConfiguration.AspNetCore` come **sorgenti `IConfiguration`** aggiuntive: nessun codice applicativo li vede.
- `Azure.Monitor.OpenTelemetry.AspNetCore` **oppure** exporter OTLP generico verso Application Insights: il secondo mantiene Prometheus/Grafana nel profilo locale senza biforcare il codice.
- Identità: Managed Identity per Postgres (Entra auth), Blob, Key Vault, AOAI; chiave solo dove non c'è alternativa (Cohere serverless).

### 6.4 IaC
`azd` + Bicep in `infra/azure/` (Container Apps, Postgres, Managed Redis, Service Bus, Key Vault, App Configuration, Log Analytics). `azd` supporta anche `azd add` per Postgres/Redis/AOAI con accesso keyless; **.NET Aspire** è un'alternativa che genera il Bicep dall'AppHost, ma introdurrebbe un secondo orchestratore accanto a compose: sconsigliato in prima battuta.

---

## 7. Scenari economici

Prezzi listino USD, ottobre 2026 (regione US, variano per regione; EUR ≈ +10–15% in Europe West nella mia esperienza, non verificato in questa ricerca).

### Gratuito o quasi — cosa dà Microsoft
| Offerta | Cosa include | Fonte |
|---|---|---|
| Azure free account | $200 per 30 giorni + servizi gratuiti per 12 mesi + "always free" | [Azure free FAQ](https://azure.microsoft.com/en-ca/free/free-account-faq/) |
| Azure for Students | $100 per 12 mesi, senza carta, 25+ servizi free (serve email accademica) | [Azure for Students](https://learn.microsoft.com/azure/education-hub/azure-dev-tools-teaching/azure-students-program) |
| Visual Studio Professional/Enterprise | $50 / $150 di credito al mese, solo dev/test | [credit for VS subscribers](https://azure.microsoft.com/pricing/member-offers/credit-for-visual-studio-subscribers/) |
| PostgreSQL Flexible Server | 750 h/mese di B1MS + 32 GB storage + 32 GB backup, 12 mesi | [free account Postgres](https://learn.microsoft.com/en-ie/azure/postgresql/flexible-server/how-to-deploy-on-azure-free-account) |
| Container Apps (sempre) | 180.000 vCPU-s + 360.000 GiB-s + 2 M richieste al mese | [ACA pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/) |
| Cosmos DB free tier (sempre) | 1000 RU/s + 25 GB, uno per sottoscrizione, solo provisioned | [Cosmos free tier](https://learn.microsoft.com/azure/cosmos-db/free-tier) |
| Functions | Consumption 1 M esecuzioni + 400.000 GB-s; Flex 250.000 + 100.000 GB-s | [Functions pricing](https://azure.microsoft.com/en-us/pricing/details/functions/) |
| Event Grid | 100.000 operazioni/mese (Basic) | [Event Grid pricing](https://azure.microsoft.com/en-in/pricing/details/event-grid/) |
| App Configuration | tier Free: 1000 richieste/giorno, 10 MB | [App Configuration pricing](https://azure.microsoft.com/en-us/pricing/details/app-configuration/) |
| Log Analytics / App Insights | 5 GB/mese di ingestione gratis; App Insights 90 giorni di retention gratis | [monitoringcost](https://monitoringcost.com/azure-application-insights-pricing) `(sec.)` |

### Voci che si pagano comunque nel profilo `azure-exam`
| Voce | Ordine di grandezza | Fonte |
|---|---|---|
| Azure Managed Redis B0 (1 GB, no HA) | ≈ $13/mese `(sec.)` | [dragonflydb](https://www.dragonflydb.io/guides/redis-on-azure-service-options-pricing-pros-and-cons) |
| ACR Basic | $0.167/giorno ≈ $5/mese (10 GB inclusi) | [oneuptime](https://oneuptime.com/blog/post/2026-07-23-choose-acr-tier/markdown) `(sec.)` |
| Service Bus Basic | $0.05 per milione di operazioni (code sole); Standard $10/mese base se servono topic | [chakray](https://chakray.com/?p=37683) `(sec.)` |
| Key Vault | $0.03 per 10.000 operazioni su secret | [costbench](https://costbench.com/software/secrets-management/azure-key-vault/) `(sec.)` |
| Blob hot LRS | ≈ $0.018/GB/mese + operazioni | [finout](https://finout.io/blog/cloud-storage-pricing-comparison) `(sec.)` |
| Embedding gestito | Cohere embed v3 multilingual $0.10/1M token; `text-embedding-3-small` ≈ $0.02/1M; `3-large` ≈ $0.13/1M | [llmreference](https://www.llmreference.com/model/cohere-embed-multilingual-v3-0/microsoft-foundry), [wrvishnu](https://www.wrvishnu.com/azure-ai-foundry-pricing-2026/) `(sec.)` |
| LLM (dopo PAYG) | gpt-4o-mini $0.15 in / $0.60 out per 1M | idem `(sec.)` |
| Container Apps oltre il grant | ≈ $63/vCPU/mese attivo, ≈ $7.9/GiB/mese; tariffa idle più bassa per le repliche minime | [nops](https://www.nops.io/blog/azure-container-apps-cost-optimization-guide) `(sec.)` |

Il corpus staging (~56.000 chunk heading-aware, cfr. la riga #3740 nei Known Pitfalls di `CLAUDE.md`) reindicizzato con Cohere costa nell'ordine di qualche decina di centesimi per passata, se i chunk stanno in ~300 token l'uno: misurare con `wc -w` sul dump prima di decidere.

### Quattro scenari
| Scenario | Composizione | Stima mensile | Note |
|---|---|---|---|
| **Locale** | compose + Ollama o Foundry Local | $0 | Foundry Local (GA 2026) espone un endpoint OpenAI-compatible su localhost, solo modelli ONNX: utile come terzo `ILlmClient` oltre a Ollama. |
| **Ibrido attuale** | Hetzner CAX31 + OpenRouter | ≈ €16 VPS + token | Baseline. Il manuale operativo riporta un piano diverso (§5 #11). |
| **Azure-exam** (12 mesi free, spento la notte) | ACA scale-to-zero, Postgres B1ms free, Managed Redis B0, ACR, Service Bus Basic, Key Vault, App Config free, App Insights ≤5 GB, embedding Cohere, LLM OpenRouter | ≈ $20–30 + token | Oltre i 12 mesi aggiungere ~$13–15 per Postgres B1ms `(sec.)`. Richiede PAYG per AOAI; i servizi Python restano fuori. |
| **Azure-paid** sempre acceso | come sopra + API e web min 1 replica + embedding self-hosted 1 vCPU/2 GiB + Postgres GP | ≈ $150–250 | 10× il VPS. Ha senso solo per imparare KEDA/AKS/HA, non come hosting. |

Alternative di hosting sempre acceso, per confronto `(sec.)`: App Service B1 ≈ $55/mese; Container Instances 1 vCPU/2 GB ≈ $0.056/h ≈ $41/mese. Nessuna batte Hetzner; il valore di Azure qui è l'esame, non il prezzo.

---

## 8. Piano di esercizi allineato ai domini AI-200

Ordine pensato per: (a) sbloccare i vincoli prima, (b) coprire i quattro domini con il peso dell'esame, (c) ogni passo lascia il prodotto funzionante nei profili `local`/`hybrid`. Ogni riga è candidata a diventare un'epic con issue figlie; le DoD sono misurabili.

| # | Esercizio | Dominio | Tocca | DoD |
|---|---|---|---|---|
| 0 | **Build multi-arch** `linux/amd64,linux/arm64` per api, web e i tre servizi AI; `$TARGETARCH`→`dotnet --arch` mapping | A | `deploy-staging.yml`, Dockerfile | manifest list su GHCR con entrambe le piattaforme; durata del job confrontata con il baseline (~29 min QEMU → cross-compile) |
| 1 | **Profili di provider** a runtime (§6.2 punto 1) + toggle admin | trasversale | factory embedding/PDF/storage/email | switch senza riavvio dimostrato da un test E2E per componente |
| 2 | **Spazio vettoriale per profilo** + reembedding in background | B | `IVectorStoreAdapter`, migrazione | due profili (e5-768, Cohere-1024) coesistono; gate `rag-fusion-tuning` rieseguito e pesi ritarati con evidenza |
| 3 | **ACR + Container Apps** con `azd`/Bicep in `infra/azure/`; revisioni, env/secret, scale-to-zero | A | nuovo `infra/azure/` | `azd up` da zero porta l'API a rispondere su `/api/v1/health`; `azd down` lascia zero risorse |
| 4 | **Postgres Flexible Server** + `vector` + `pg_diskann` + Entra auth + PgBouncer | B | connection string, indici | query vettoriale con filtro metadati ≤ latenza misurata sul VPS; confronto HNSW vs DiskANN documentato |
| 5 | **Key Vault + App Configuration** come sorgenti `IConfiguration`; Managed Identity ovunque | D | `Program.cs`, `IProviderCredentialResolver` | nessun secret in env sul profilo Azure (`az containerapp show` senza `secrets`); rotazione di una chiave senza riavvio |
| 6 | **OpenTelemetry → Application Insights** via OTLP, mantenendo Prometheus in locale; 5 query KQL salvate nel repo | D | `ObservabilityServiceExtensions` | trace end-to-end upload PDF → indicizzazione visibile in App Insights; KQL che riproduce l'alert `meepleai_bgg_url_attempted_render_total` |
| 7 | **Managed Redis** (TLS, connection string) + HybridCache L2 acceso + **vector index** su Redis per la semantic cache delle query | B | `InfrastructureServiceExtensions` | cache hit misurata; FT.SEARCH KNN su embedding delle query |
| 8 | **Service Bus + Event Grid + Functions**: Blob created → Event Grid → Function → coda Service Bus → worker di indicizzazione con **KEDA** su lunghezza coda; DLQ gestita | C + A | outbox, nuovo worker | upload su Blob avvia l'indicizzazione senza polling; messaggio avvelenato finisce in DLQ con alert |
| 9 | **Cosmos DB NoSQL** come store alternativo di `ConversationMemory` con vector policy + **change feed** verso Redis | B | nuovo repo `IConversationMemoryRepository` | free tier; change feed processor aggiorna la cache |
| 10 | **AKS** con manifest (solo esercizio): stesso stack, cluster arm64 o amd64, `kubectl logs/events` | A | `infra/azure/k8s/` | deploy + troubleshooting documentati; cluster distrutto a fine esercizio |
| 11 | *(fuori esame)* Azure OpenAI/Foundry Models dietro `ILlmClient` via `IChatClient`; Foundry Local come provider locale | – | `LlmProviderFactory`, `ProviderName` | fallback chain DB con `Azure` in testa; kill switch verificato |
| 12 | *(fuori esame)* Azure Blob dietro `IBlobStorageService` + refactor dei bypass `IAmazonS3`; Azurite in compose | – | pipeline cover/seed/backup | `STORAGE_PROVIDER=azureblob` passa le suite di storage (oggi senza gate CI: aggiungerlo) |
| 13 | *(fuori esame, opzionale)* Document Intelligence come stage di `OrchestratedPdfTextExtractor` | – | keyed services | confronto qualità/costo su 3 regolamenti vs Unstructured |

Mappatura con i 9 moduli del corso: 0+3 → moduli 1–2 · 10 → 3 · 9 → 4 · 2+4 → 5 · 7 → 6 · 8 → 7 · 5 → 8 · 6 → 9.

---

## 9. Decisioni aperte (servono all'utente, non alla ricerca)

1. **Quale sottoscrizione**: free account ($200/30 gg + 12 mesi), Azure for Students (serve email accademica) o PAYG subito. Decide se l'LLM su Azure entra nel piano o resta OpenRouter.
2. **Multi-arch o amd64-only per Azure**: multi-arch mantiene il VPS; il costo è tempo di build e cache GHA (già fragile, cfr. `docs/for-developers/specs/2026-05-28-ai-services-ghcr-pipeline-design.md`).
3. **Embedding gestito nel profilo esame**: Cohere multilingual (1024, 512 token di contesto, serverless) o `text-embedding-3-small` (1536, richiede AOAI quindi PAYG). Entrambi comportano la ritaratura di #3737.
4. **Dove far vivere i profili**: DB (`IConfigurationService`, toggle admin) o solo env per profilo. Il primo è coerente con il resto del sistema ma mette un toggle distruttivo (spazio vettoriale) a portata di click.
5. **Aspire sì/no** per l'IaC: comodo per `azd`, ma secondo orchestratore accanto a compose.

---

## 10. Fonti

Ufficiali
- [Study guide AI-200](https://learn.microsoft.com/en-us/credentials/certifications/resources/study-guides/ai-200) (ms.date 2026-04-15, updated 2026-05-05)
- [Azure AI Search — pricing model e tier](https://learn.microsoft.com/azure/search/search-sku-tier) (updated 2026-09-17)
- [Microsoft.Extensions.AI libraries](https://learn.microsoft.com/en-us/dotnet/ai/microsoft-extensions-ai) (updated 2026-09-15)
- [PostgreSQL Flexible Server su free account](https://learn.microsoft.com/en-ie/azure/postgresql/flexible-server/how-to-deploy-on-azure-free-account)
- [pgvector su Flexible Server](https://learn.microsoft.com/en-us/azure/postgresql/extensions/how-to-use-pgvector) · [pg_diskann](https://learn.microsoft.com/ga-ie/azure/postgresql/flexible-server/how-to-use-pgdiskann) · [azure_ai extension](https://learn.microsoft.com/en-us/azure/postgresql/integration/how-to-integrate-azure-ai)
- [Cosmos DB free tier](https://learn.microsoft.com/azure/cosmos-db/free-tier) · [vector index .NET](https://learn.microsoft.com/en-us/azure/cosmos-db/nosql/how-to-dotnet-vector-index-query)
- [Azure OpenAI quotas](https://learn.microsoft.com/en-us/azure/foundry/openai/quotas-limits) · [Q&A quota 0 su free trial](https://learn.microsoft.com/en-us/answers/a/12804121)
- [Container Apps pricing](https://azure.microsoft.com/en-us/pricing/details/container-apps/) · [Q&A solo amd64](https://learn.microsoft.com/en-us/answers/questions/1485305/unable-to-deploy-images-to-azure-container-app) · [feature request arm64 #569](https://github.com/microsoft/azure-container-apps/issues/569)
- [Azure Managed Redis pricing](https://azure.microsoft.com/en-us/pricing/details/managed-redis/) · [ritiro Azure Cache for Redis](https://techcommunity.microsoft.com/blog/azure-managed-redis/-/4458721)
- [Foundry Local GA](https://devblogs.microsoft.com/foundry/foundry-local-ga/) · [architettura](https://learn.microsoft.com/en-us/azure/ai-foundry/foundry-local/concepts/foundry-local-architecture)
- [azd + Aspire su Container Apps](https://learn.microsoft.com/dotnet/aspire/deployment/azure/aca-deployment-azd-in-depth) · [azd compose quickstart](https://learn.microsoft.com/en-in/azure/developer/azure-developer-cli/compose-quickstart)
- [Azure Monitor OpenTelemetry Distro .NET](https://docs.azure.cn/en-us/azure-monitor/app/opentelemetry-enable?tabs=net)
- [Azure for Students](https://learn.microsoft.com/azure/education-hub/azure-dev-tools-teaching/azure-students-program) · [crediti Visual Studio](https://azure.microsoft.com/pricing/member-offers/credit-for-visual-studio-subscribers/)
- [Functions pricing](https://azure.microsoft.com/en-us/pricing/details/functions/) · [Event Grid pricing](https://azure.microsoft.com/en-in/pricing/details/event-grid/) · [App Configuration pricing](https://azure.microsoft.com/en-us/pricing/details/app-configuration/) · [Document Intelligence pricing](https://azure.microsoft.com/it-it/pricing/details/ai-document-intelligence/)

Secondarie (prezzi non esposti dalle pagine ufficiali, o commento)
- [examos — AI-103 vs AI-200](https://examos.io/blog/ai-103-vs-ai-200-which-should-you-take) · [certcrush — AI-102 ritirato](https://www.certcrush.app/blog/ai-102-vs-ai-103-which-azure-ai-cert-2026) · [aguidetocloud](https://www.aguidetocloud.com/cert-tracker/ai-200/) · [Global Knowledge AI-200T00](https://www.globalknowledge.com/en-be/courses/microsoft/artificial_intelligence/m-ai200/co) · [examcert — prezzo](https://www.examcert.app/blog/azure-ai-200-requirements-2026/) · [voucher](https://techcommunity.microsoft.com/discussions/skills-hub-discussions/what-are-all-the-ways-to-get-discount-or-free-vouchers-for-microsoft-certificati/4456263)
- [nops — ACA cost](https://www.nops.io/blog/azure-container-apps-cost-optimization-guide) · [dragonflydb — Redis su Azure](https://www.dragonflydb.io/guides/redis-on-azure-service-options-pricing-pros-and-cons) · [oneuptime — ACR tier](https://oneuptime.com/blog/post/2026-07-23-choose-acr-tier/markdown) · [chakray — messaging](https://chakray.com/?p=37683) · [costbench — Key Vault](https://costbench.com/software/secrets-management/azure-key-vault/) · [finout — storage](https://finout.io/blog/cloud-storage-pricing-comparison) · [monitoringcost — App Insights](https://monitoringcost.com/azure-application-insights-pricing)
- [llmreference — Cohere su Foundry](https://www.llmreference.com/model/cohere-embed-multilingual-v3-0/microsoft-foundry) · [wrvishnu — Foundry pricing 2026](https://www.wrvishnu.com/azure-ai-foundry-pricing-2026/) · [aiproductivity — Document Intelligence](https://aiproductivity.ai/pricing/azure-document-intelligence)
- [cast.ai — container services](https://cast.ai/blog/azure-containers-services-pricing-and-feature-comparison/) · [whtop — Hetzner CAX31](https://www.whtop.com/amp/plans/hetzner.com/144091)
- [adrianbailador — Microsoft.Extensions.AI](https://dev.to/adrianbailador/microsoftextensionsai-one-ichatclient-interface-any-llm-provider-120) · [Q&A — Blob non parla S3](https://learn.microsoft.com/en-us/answers/questions/1512015/compatibility-between-azure-blob-storage-and-s3-pr)

Repo (inventario del 2026-10-08, path in §3–4): ADR correlate `adr-002-multilingual-embedding`, `adr-003b-unstructured-pdf`, `adr-007-hybrid-llm` (§"Easy to add new providers (Anthropic direct, Azure OpenAI)"), `adr-021-auto-configuration-system`, `adr-043-llm-subsystem-nfr`, `adr-050-pgvector-migration`, `adr-054-devops-multi-branch-strategy`; ricerca archiviata `.docs-archive/research/2026-03-07-dotnet-embedding-reranking-feasibility.md` (Semantic Kernel/ONNX, `IEmbeddingGenerator`).
