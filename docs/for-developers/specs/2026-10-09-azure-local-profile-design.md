# Spec — Profilo `azure-local`: SDK Azure veri contro emulatori, senza un conto Azure

**Data**: 2026-10-09 · **Stato**: approvata in brainstorming (design a sezioni), in attesa di piano di implementazione
**Linea di lavoro**: worktree `D:/Repositories/meepleai-monorepo-azure`, branch d'integrazione `azure/main` (da `origin/main-dev`); feature `feature/azure-<n>-<desc>` con PR verso `azure/main`; `azure/main` → `main-dev` via PR per le astrazioni utili al prodotto.
**Decisioni a monte**: ricerca `docs/for-developers/research/2026-10-08-azure-ai-200-provider-switch-research.md` (profili switchabili, non fork; esercizi AI-200 §8) · ADR-093 (confine IP: qui irrilevante, nessun file esce dalla macchina) · ADR-054 (branch strategy).
**Convenzione**: nessun conteggio volatile in prosa; dove serve una misura c'è il comando. Prezzi: zero per definizione (tutto locale).

---

## 1. Obiettivo e perimetro

Scrivere il codice della "versione Azure" dell'infrastruttura **oggi**, con gli SDK Azure veri, senza iscriversi ad Azure: ogni servizio del dominio AI-200 che ha un emulatore gira in Docker nel profilo compose `azure-local`; quando esisterà una sottoscrizione, il profilo `azure-exam` cambierà solo endpoint e credenziali.

**In scope (prima iterazione)**: workspace e branch; profilo compose con gli emulatori; provider `azureblob` per lo storage; Key Vault e App Configuration come sorgenti `IConfiguration`; evento `PdfUploaded` su Service Bus con consumer Azure Functions; exporter OTLP verso Aspire Dashboard; `infra/azure/` in Bicep validato in CI; egress degli SDK pinnato; test con Testcontainers.
**Fuori scope (seconda iterazione)**: Cosmos DB come store di `ConversationMemory` con change feed; indice vettoriale su Redis 8 per la semantic cache; build multi-arch `linux/amd64`; KEDA/AKS; i punti che iniettano `IAmazonS3` direttamente (restano S3 e sono elencati in §4.1 come debito); Event Grid oltre lo smoke test.

## 2. Emulatori e loro limiti

| Servizio Azure (dominio AI-200) | In `azure-local` | Immagine | Note |
|---|---|---|---|
| Blob Storage | **Azurite** | `mcr.microsoft.com/azure-storage/azurite` | porte 10000–10002; `UseDevelopmentStorage=true` |
| App Configuration | **emulatore ufficiale** | `mcr.microsoft.com/azure-app-configuration/app-configuration-emulator` | porta 8483; auth anonima abilitata via env |
| Key Vault | emulatore di comunità (`AzureKeyVaultEmulator`) | immagine del progetto, porta 4997 (https, certificato self-signed) | il client vuole `DisableChallengeResourceVerification = true` |
| Service Bus | **emulatore ufficiale** | `mcr.microsoft.com/azure-messaging/servicebus-emulator` + `mcr.microsoft.com/azure-sql-edge` | porta 5672; code, topic, sottoscrizioni da `Config.json`; niente entità partizionate né AMQP WebSocket |
| Event Grid | emulatore di comunità (Workleap) | `workleap/eventgridemulator` | solo smoke test in questa iterazione |
| Cosmos DB NoSQL | **emulatore ufficiale vNext** (GA 2026-06, vettoriale, amd64 e arm64) | `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest` | nel compose ma usato solo dalla seconda iterazione |
| Azure Monitor / App Insights | **Aspire Dashboard standalone** (OTLP, in memoria) | `mcr.microsoft.com/dotnet/aspire-dashboard` | UI 18888, OTLP gRPC 18889 |
| Managed Redis | Redis locale esistente | `redis` (già nel compose) | senza indice vettoriale in questa iterazione |
| PostgreSQL Flexible Server | Postgres + pgvector esistente | già nel compose | invariato |
| Azure OpenAI / Foundry | OpenRouter o Ollama/Foundry Local | già nel compose | invariato |
| Container Apps, AKS, ACR, Functions hosting | nessun emulatore | compose + Azure Functions Core Tools in locale | il codice delle Functions è reale, l'hosting no |

Docker locale: `linux/amd64` (verificato); tutte le immagini sono disponibili per questa piattaforma. Le porte sopra sono quelle di default delle immagini: la memoria `port-conflict-multi-checkout` ricorda che un secondo checkout attivo le collide, quindi `compose.azure-local.yml` le espone con prefisso `2xxxx` configurabile (`AZURE_LOCAL_PORT_BASE`, default 20000).

## 3. Profilo compose e Makefile

- Nuovo `infra/compose.azure-local.yml` (override, mai invocato a mano: memoria `never-invoke-docker-compose-by-hand`) con i servizi di §2 e le variabili per l'API; si compone con `docker-compose.yml` e `compose.dev.yml`.
- Target `make dev-azure-local` (core + emulatori + aspire-dashboard) e `make dev-azure-local-down`; `make help` li elenca.
- Variabili iniettate nell'API (tutte con default vuoto = profilo attuale): `MEEPLEAI_PROFILE=azure-local`, `STORAGE_PROVIDER=azureblob`, `AZURE_STORAGE_CONNECTION_STRING`, `AZURE_APPCONFIG_ENDPOINT`, `AZURE_KEYVAULT_URI`, `AZURE_SERVICEBUS_CONNECTION_STRING`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `AZURE_SDK_ALLOW_INSECURE_TLS=true` (solo per l'emulatore Key Vault).
- `infra/azure-local/servicebus-config.json` con la coda `pdf-uploaded` e la sua dead-letter; `infra/azure-local/appconfig-seed.json` con le chiavi di esempio lette dal profilo.

## 4. Codice API

### 4.1 Storage: provider `azureblob`
- `Services/Pdf/AzureBlobStorageService : IBlobStorageService` con `Azure.Storage.Blobs` (container per bucket, SAS per `GetDownloadUrlAsync`, stesse semantiche dell'implementazione S3).
- `BlobStorageServiceFactory`: ramo `"azureblob"` che legge `AZURE_STORAGE_CONNECTION_STRING` (Azurite) o `AZURE_STORAGE_ACCOUNT_URL` + `DefaultAzureCredential` (Azure vero).
- Debito dichiarato: `PdfCoverUploadPipeline`, `BggCoverUploadPipeline`, `CoverR2UploadPipeline`, `S3SeedBlobReader`, `RagBackupStorageService`, `StorageOperationOutboxBackgroundService`, `ReverseStorageMigrationCommandHandler` iniettano `IAmazonS3`: con `azureblob` quei percorsi restano su S3 (o inattivi); l'elenco va in una issue di follow-up.

### 4.2 Secret e configurazione come sorgenti `IConfiguration`
- In `Program.cs`, prima della build: se `AZURE_APPCONFIG_ENDPOINT` è valorizzato, `AddAzureAppConfiguration` (pacchetto `Microsoft.Azure.AppConfiguration.AspNetCore`) con `ConnectionString` per l'emulatore o `DefaultAzureCredential` per Azure; se `AZURE_KEYVAULT_URI` è valorizzato, `AddAzureKeyVault` (`Azure.Extensions.AspNetCore.Configuration.Secrets`).
- Convenzione delle chiavi: le stesse del repo (`Embedding:Provider`, `HybridSearch:*`, …) e i secret con i nomi delle variabili d'ambiente (`OPENROUTER_API_KEY` → secret `OPENROUTER-API-KEY`, mapping `-`↔`_` documentato).
- Nessun codice applicativo cambia: `IProviderCredentialResolver` e `IConfigurationService` continuano a leggere DB → `IConfiguration`.
- Memoria `empty-env-var-is-not-absent-getvalue-throws`: le variabili `AZURE_*` assenti o vuote disattivano la sorgente, mai lanciare.

### 4.3 Messaggistica: `PdfUploaded` su Service Bus
- Nuova astrazione `IIntegrationEventPublisher` (in `SharedKernel`) con `PublishAsync<T>(T @event, CancellationToken)`; implementazione `ServiceBusIntegrationEventPublisher` (`Azure.Messaging.ServiceBus`) e `NullIntegrationEventPublisher` per i profili senza bus.
- Handler post-commit del domain event di upload PDF (il meccanismo outbox esistente: memoria `pdf-indexing-domain-event-bypass` va riletta prima di scegliere il punto di aggancio) che pubblica `PdfUploadedIntegrationEvent { PdfDocumentId, GameId, BlobUri, OccurredAt, ContractVersion = "v1" }` sulla coda `pdf-uploaded`.
- Consumer: progetto `apps/functions/MeepleAI.Functions` (.NET 9 isolated) con `ServiceBusTrigger("pdf-uploaded")` che chiama l'endpoint di indicizzazione dell'API con una chiave di servizio; messaggi malformati o con `ContractVersion` sconosciuto vanno in dead-letter con motivo; retry secondo la policy dell'emulatore; metrica `meepleai_pdf_uploaded_events_total{outcome}` nell'API.
- Idempotenza: l'endpoint di indicizzazione è già protetto dalle due guardie (`ProcessingState` e `ProcessingJob`, memoria `reindex-has-two-independent-guards`): un messaggio duplicato è un no-op.

### 4.4 Telemetria
- `ObservabilityServiceExtensions`: se `OTEL_EXPORTER_OTLP_ENDPOINT` è presente, aggiungere `OpenTelemetry.Exporter.OpenTelemetryProtocol` per trace e metriche accanto a `AddPrometheusExporter` (che resta). Risorsa `service.name=meepleai-api`, `deployment.environment=<MEEPLEAI_PROFILE>`.
- Serilog: sink OTLP opzionale (`Serilog.Sinks.OpenTelemetry`) con la stessa variabile; Seq resta.
- Verifica: una richiesta a `/api/v1/health` compare nell'Aspire Dashboard come trace con lo span `GET /api/v1/health`.

### 4.5 Egress degli SDK
- `HttpClient` nominato `AzureSdk` registrato con `AddHttpClient("AzureSdk").ConfigureSsrfPin("azure-sdk", allowedHostSuffixes: suffixes)`, dove `suffixes` è letto dalla chiave `Azure:Sdk:AllowedHostSuffixes` (firma reale in `SharedKernel/Infrastructure/Http/SsrfPinnedHttpClientBuilderExtensions.cs`); gli SDK ricevono `new HttpClientTransport(httpClient)` nelle loro `ClientOptions`.
- Nel profilo `azure-local` i suffissi ammessi sono gli host degli emulatori (`azurite`, `servicebus-emulator`, `appconfig-emulator`, `keyvault-emulator`, `localhost`); nel profilo Azure, `.blob.core.windows.net`, `.servicebus.windows.net`, `.azconfig.io`, `.vault.azure.net`. La lista è configurazione, non codice; con la chiave assente il client `AzureSdk` non viene registrato e i provider Azure non si attivano (nessun default permissivo).
- `EgressHttpClientPinArchitectureTests`: `AzureSdk` entra in `MustBePinned`.

## 5. `infra/azure/` in Bicep (validato, non deployato)

- `infra/azure/main.bicep` + moduli: `containerapps.bicep` (environment, app `api`, app `web`, min replicas 0), `postgres.bicep` (Flexible Server Burstable B1ms, estensioni `vector` e `pg_diskann` nella allowlist), `redis.bicep` (Managed Redis Balanced B0), `servicebus.bicep` (namespace Basic, coda `pdf-uploaded` con DLQ), `keyvault.bicep`, `appconfig.bicep` (Free), `storage.bicep` (account + container), `monitoring.bicep` (Log Analytics + Application Insights), `functions.bicep` (Flex Consumption).
- `infra/azure/main.bicepparam` per il profilo esame; `azure.yaml` per `azd` con i servizi `api`, `web`, `functions`.
- Workflow `.github/workflows/azure-bicep-lint.yml`: su PR che toccano `infra/azure/**`, installa Bicep CLI, esegue `bicep build` e `bicep lint` su ogni file, fallisce su errori. Nessun `azd provision`.

## 6. Test

| Livello | Cosa | Strumento |
|---|---|---|
| Unit | `AzureBlobStorageService` con `BlobServiceClient` finto; publisher con `ServiceBusSender` finto; mapping chiavi Key Vault | xUnit + Moq |
| Integrazione | upload/download/URL firmato su Azurite; publish + receive + dead-letter su Service Bus emulator; lettura chiavi da App Configuration emulator | Testcontainers (`Testcontainers.Azurite`, `Testcontainers.ServiceBus`) e container generico per App Config |
| Contratto | schema JSON di `PdfUploadedIntegrationEvent` v1 congelato in un fixture condiviso da API e Functions | test da entrambi i lati |
| Architettura | `AzureSdk` in `MustBePinned`; nessun `AddHttpClient` nuovo senza pin | gate esistente |
| Functions | `ServiceBusTrigger` con messaggio valido ⇒ chiamata all'API; messaggio con `ContractVersion` sconosciuto ⇒ dead-letter | test unit del function host (`Microsoft.Azure.Functions.Worker` test harness) |
| Bicep | `bicep build` senza errori | workflow |
| Manuale | `make dev-azure-local`, upload di un PDF, trace nell'Aspire Dashboard, messaggio consumato dalla Function, PDF leggibile da Azurite | checklist nel piano |

Policy del repo: nessun test skippato senza prefisso `PREVISTO:/GUASTO:/DIFETTO:/LIMITE:`; gli emulatori sono servizi di livello L3 (`PREVISTO:` con il comando `make dev-azure-local`), non L1.

## 7. Definizione di fatto della prima iterazione

- [ ] `make dev-azure-local` avvia emulatori e API; `/api/v1/health` sano; `make dev-azure-local-down` pulisce
- [ ] `STORAGE_PROVIDER=azureblob`: upload PDF su Azurite, download via URL firmato, test Testcontainers verdi
- [ ] Chiavi lette da App Configuration emulator e secret da Key Vault emulator senza modifiche al codice applicativo (test di integrazione sulla pipeline `IConfiguration`)
- [ ] Upload PDF ⇒ messaggio `PdfUploaded` v1 su Service Bus emulator ⇒ Function ⇒ indicizzazione avviata; messaggio malformato in dead-letter
- [ ] Trace e metriche visibili nell'Aspire Dashboard; Prometheus invariato nel profilo attuale
- [ ] `infra/azure/**` passa `bicep build` e `bicep lint` in CI
- [ ] Gate di architettura, suite unit backend, `pnpm typecheck && pnpm lint` verdi; nessun test nuovo richiede un conto Azure
- [ ] Issue di follow-up aperte: debito `IAmazonS3`, seconda iterazione (Cosmos, Redis vettoriale, multi-arch, KEDA/AKS)

## 8. Decisioni registrate durante il brainstorming

| Domanda | Scelta |
|---|---|
| Natura della directory | worktree dello stesso repo su `azure/main`, non fork |
| Prima fetta | senza iscrizione ad Azure: emulatori e doppi locali |
| Approccio | A: SDK Azure veri contro emulatori in compose (non Aspire AppHost, non doppi in-process) |
