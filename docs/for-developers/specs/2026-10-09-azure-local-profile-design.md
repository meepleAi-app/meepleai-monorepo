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
| App Configuration | **emulatore ufficiale** | `mcr.microsoft.com/azure-app-configuration/app-configuration-emulator` | porta 8483; auth HMAC via env `Tenant__AccessKeys__0__*` (+ anonima per il seed); l'emulatore verifica la firma (un secret errato dà 401); il provider .NET usa la connection string `Endpoint=…;Id=…;Secret=…` perché rifiuta un bearer su http |
| Key Vault | emulatore di comunità (`AzureKeyVaultEmulator`) | immagine del progetto, porta 4997 (https, certificato self-signed) | **opt-in** (profilo compose `azure-keyvault`, `make dev-azure-local-keyvault`): richiede certificati generati sull'host con `docs/setup.sh` del progetto; non parte con `make dev-azure-local`. Il client vuole `DisableChallengeResourceVerification = true` |
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
- Variabili iniettate nell'API (tutte con default vuoto = profilo attuale): `MEEPLEAI_PROFILE=azure-local`, `STORAGE_PROVIDER=azureblob`, `AZURE_STORAGE_CONNECTION_STRING`, `AZURE_APPCONFIG_CONNECTION_STRING` (HMAC; nel profilo Azure `AZURE_APPCONFIG_ENDPOINT`), `AZURE_APPCONFIG_STARTUP_TIMEOUT_SECONDS` (accorcia il timeout di avvio del provider), `AZURE_KEYVAULT_URI`, `AZURE_KEYVAULT_ANONYMOUS`, `AZURE_SERVICEBUS_CONNECTION_STRING`, `INTERNAL_SERVICE_TOKEN`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `AZURE_SDK_ALLOW_INSECURE_TLS=true` (solo per l'emulatore Key Vault). Le sorgenti Azure vincono sulle variabili d'ambiente.
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
- Le sorgenti girano prima del container DI e non passano dal client pinnato: endpoint da configurazione statica, come le esenzioni OpenRouter/Resend del gate. Emulatore App Configuration: connection string HMAC; la variabile `AZURE_APPCONFIG_STARTUP_TIMEOUT_SECONDS` accorcia il timeout di avvio del provider, così un emulatore assente non blocca l'API a lungo. Key Vault emulator: `StaticTokenCredential` (`AZURE_KEYVAULT_ANONYMOUS=true`). Mapping dei secret: `--` → `:`, `-` → `_`; le chiavi CamelCase con trattino interno non sono esprimibili.
- Memoria `empty-env-var-is-not-absent-getvalue-throws`: le variabili `AZURE_*` assenti o vuote disattivano la sorgente, mai lanciare.

### 4.3 Messaggistica: `PdfUploaded` su Service Bus
- Nuova astrazione `IIntegrationEventPublisher` (in `SharedKernel`) con `PublishAsync<T>(T @event, CancellationToken)`; implementazione `ServiceBusIntegrationEventPublisher` (`Azure.Messaging.ServiceBus`) e `NullIntegrationEventPublisher` per i profili senza bus.
- Handler post-commit del domain event di upload PDF (il meccanismo outbox esistente: memoria `pdf-indexing-domain-event-bypass` va riletta prima di scegliere il punto di aggancio) che pubblica `PdfUploadedIntegrationEvent` sulla coda `pdf-uploaded`. Il payload è `{ eventId, occurredAt, sourceContext, contractVersion, pdfDocumentId, gameId, sharedGameId, filePath }` (non `BlobUri`) con `contractVersion = "v1"`; l'evento parte alla transizione Pending → Uploading, DOPO lo store del blob. Profilo Azure: `AZURE_SERVICEBUS_NAMESPACE` + `DefaultAzureCredential`. Poiché la pubblicazione avviene dentro la transazione dell'outbox, il client Service Bus usa `MaxRetries = 1` e `TryTimeout = 5 s` e il relay un timeout di pubblicazione di 15 s: un bus irraggiungibile non deve tenere aperta la transazione.
- Consumer: progetto `apps/functions/MeepleAI.Functions` (.NET 9 isolated) con `ServiceBusTrigger("pdf-uploaded")` che chiama l'endpoint interno di indicizzazione dell'API con una chiave di servizio (`INTERNAL_SERVICE_TOKEN`); messaggi malformati o con `ContractVersion` sconosciuto vanno in dead-letter con motivo; retry secondo la policy dell'emulatore; metrica `meepleai_pdf_uploaded_events_total{outcome}` (senza unit) nell'API. La Function tratta un 404 VUOTO (token non configurato sull'API) come transitorio, quindi il messaggio viene ritentato; un 404 con `not_found` è invece un esito definitivo (documento inesistente).
- Cosa causa l'evento: il consumer interno indicizza SOLO un PDF inattivo che ne ha bisogno (`Failed`, o `Ready` senza vettori) tramite `IndexPdfIfIdleCommand`; pipeline in corso o documento già indicizzato ⇒ `noop`. Le due guardie non stanno in `IndexPdfCommand`, che ri-indicizza sempre.
- Idempotenza: l'endpoint interno è protetto dalle due guardie (`ProcessingState` e `ProcessingJob`, memoria `reindex-has-two-independent-guards`) tramite `IndexPdfIfIdleCommand`: un messaggio duplicato è un no-op.

### 4.4 Telemetria
- `ObservabilityServiceExtensions`: se `OTEL_EXPORTER_OTLP_ENDPOINT` è presente, aggiungere `OpenTelemetry.Exporter.OpenTelemetryProtocol` per trace e metriche accanto a `AddPrometheusExporter` (che resta). Risorsa `service.name=meepleai-api`, `deployment.environment` resta l'ambiente ASP.NET (dashboard esistenti); il profilo va nell'attributo `meepleai.profile`.
- Serilog: sink OTLP opzionale (`Serilog.Sinks.OpenTelemetry`) con la stessa variabile; Seq resta.
- Verifica: una richiesta a `/api/v1/health` compare nell'Aspire Dashboard come trace con lo span `GET /api/v1/health`.

### 4.5 Egress degli SDK
- Due client nominati: `AzureSdk` pinnato (`MustBePinned`, suffissi da `Azure:Sdk:AllowedHostSuffixes`, rifiuta di partire senza lista) e `AzureSdkLocal` esente (solo `MEEPLEAI_PROFILE=azure-local`: `SsrfPinnedConnect` blocca ogni IP privato e gli emulatori stanno sulla rete compose). Service Bus parla AMQP e non passa da nessuno dei due. Il namespace C# è `Api.Infrastructure.AzureSdk`.
- `HttpClient` nominato `AzureSdk` registrato con `AddHttpClient("AzureSdk").ConfigureSsrfPin("azure-sdk", allowedHostSuffixes: suffixes)`, dove `suffixes` è letto dalla chiave `Azure:Sdk:AllowedHostSuffixes` (firma reale in `SharedKernel/Infrastructure/Http/SsrfPinnedHttpClientBuilderExtensions.cs`); gli SDK ricevono `new HttpClientTransport(httpClient)` nelle loro `ClientOptions`.
- Nel profilo `azure-local` i suffissi ammessi sono gli host degli emulatori (`azurite`, `servicebus-emulator`, `appconfig-emulator`, `keyvault-emulator`, `localhost`); nel profilo Azure, `.blob.core.windows.net`, `.servicebus.windows.net`, `.azconfig.io`, `.vault.azure.net`. La lista è configurazione, non codice; con la chiave assente il client `AzureSdk` non viene registrato e i provider Azure non si attivano (nessun default permissivo).
- `EgressHttpClientPinArchitectureTests`: `AzureSdk` entra in `MustBePinned`.

## 5. `infra/azure/` in Bicep (validato, non deployato)

- `infra/azure/main.bicep` + moduli: `containerapps.bicep` (environment, app `api`, app `web`, min replicas 0), `postgres.bicep` (Flexible Server Burstable B1ms, estensioni `vector` e `pg_diskann` nella allowlist), `redis.bicep` (Managed Redis Balanced B0), `servicebus.bicep` (namespace Basic, coda `pdf-uploaded` con DLQ), `keyvault.bicep`, `appconfig.bicep` (Free), `storage.bicep` (account + container), `monitoring.bicep` (Log Analytics + Application Insights), `functions.bicep` (Flex Consumption).
- `infra/azure/main.bicepparam` per il profilo esame; `azure.yaml` per `azd` con i servizi `api`, `web`, `functions`.
- Workflow `.github/workflows/azure-line.yml` (job `bicep` + `functions-tests`): su PR che toccano `infra/azure/**`, `apps/functions/**` o i fixture di contratto, installa Bicep CLI pinnato a v0.48.1, esegue `bicep build` e `bicep lint` su ogni file, fallisce su errori, ed esegue i test delle Functions. Nessun `azd provision`.

## 6. Test

| Livello | Cosa | Strumento |
|---|---|---|
| Unit | `AzureBlobStorageService` con `BlobServiceClient` finto; publisher con `ServiceBusSender` finto; mapping chiavi Key Vault | xUnit + Moq |
| Integrazione | upload/download/URL firmato su Azurite; publish + receive + dead-letter su Service Bus emulator; lettura chiavi da App Configuration emulator via HMAC (Key Vault: sorgente implementata, verificata con il profilo opt-in nella checklist manuale) | Testcontainers (`Testcontainers.Azurite`, `Testcontainers.ServiceBus`) e container generico per App Config |
| Contratto | schema JSON di `PdfUploadedIntegrationEvent` v1 congelato in un fixture condiviso da API e Functions | test da entrambi i lati |
| Architettura | `AzureSdk` in `MustBePinned`; nessun `AddHttpClient` nuovo senza pin | gate esistente |
| Functions | `ServiceBusTrigger` con messaggio valido ⇒ chiamata all'API; messaggio con `ContractVersion` sconosciuto ⇒ dead-letter | test unit del function host (`Microsoft.Azure.Functions.Worker` test harness) |
| Bicep | `bicep build` senza errori | workflow |
| Manuale | `make dev-azure-local`, upload di un PDF, trace nell'Aspire Dashboard, messaggio consumato dalla Function, PDF leggibile da Azurite | checklist nel piano |

Policy del repo: nessun test skippato senza prefisso `PREVISTO:/GUASTO:/DIFETTO:/LIMITE:`; gli emulatori sono servizi di livello L3 (`PREVISTO:` con il comando `make dev-azure-local`), non L1.

## 7. Definizione di fatto della prima iterazione

- [ ] `make dev-azure-local` avvia emulatori e API; `/api/v1/health` sano; `make dev-azure-local-down` pulisce
- [ ] `STORAGE_PROVIDER=azureblob`: upload PDF su Azurite, download via URL firmato, test Testcontainers verdi
- [ ] Chiavi lette dall'App Configuration emulator con HMAC (test di integrazione); Key Vault opt-in verificato a mano
- [ ] Upload PDF ⇒ messaggio v1 ⇒ Function ⇒ endpoint interno (noop con pipeline in corso; indicizzazione di un PDF Failed); messaggio malformato in dead-letter
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
