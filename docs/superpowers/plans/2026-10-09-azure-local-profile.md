# Profilo `azure-local` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** far girare l'API MeepleAI con gli SDK Azure veri (Blob, App Configuration, Key Vault, Service Bus, OTLP) contro emulatori in Docker, senza un conto Azure, con un consumer Azure Functions e l'infrastruttura Bicep validata in CI.

**Architecture:** ogni servizio Azure entra dietro un'astrazione già esistente (`IBlobStorageService`, `IConfiguration`, OpenTelemetry) o una nuova e minima (`IIntegrationEventPublisher`); un profilo compose `azure-local` avvia gli emulatori e inietta le variabili; il profilo attuale resta byte-identico quando le variabili `AZURE_*`/`OTEL_*` sono assenti. Gli SDK HTTP passano da due `HttpClient` nominati (uno pinnato per Azure vero, uno esente per la rete compose), scelti a runtime dal profilo.

**Tech Stack:** .NET 9, Azure.Storage.Blobs 12.30.1, Azure.Messaging.ServiceBus 7.21.0, Azure.Identity 1.21.0, Azure.Extensions.AspNetCore.Configuration.Secrets 1.5.2, Microsoft.Azure.AppConfiguration.AspNetCore 8.6.0, OpenTelemetry.Exporter.OpenTelemetryProtocol 1.15.3, Serilog.Sinks.OpenTelemetry 4.2.0, Testcontainers.Azurite/ServiceBus 4.14.0, Azure Functions isolated worker 2.52.0 + Extensions.ServiceBus 5.24.0 + Sdk 2.1.0, Bicep CLI.

**Spec:** `docs/for-developers/specs/2026-10-09-azure-local-profile-design.md` · note di ricerca (versioni, tag, punti di aggancio verificati): `docs/superpowers/plans/2026-10-09-azure-local-profile-plan-notes.md`

## Global Constraints

- Worktree `D:/Repositories/meepleai-monorepo-azure`, branch `feature/azure-1-local-profile-spec` (parent `azure/main`). Ogni comando `dotnet` dei task gira da `apps/api` salvo diversa indicazione; prima di ogni `dotnet test`: `taskkill //IM testhost.exe //F 2>/dev/null || true`. Mai due `dotnet test` in parallelo.
- Versioni NuGet **esattamente** quelle della tabella in Tech Stack; `OpenTelemetry.Exporter.OpenTelemetryProtocol` DEVE essere 1.15.3 (allineata a `OpenTelemetry` 1.15.3 già nel csproj).
- `TreatWarningsAsErrors` è attivo: nessun `// TODO` (SonarAnalyzer S1135 rompe il build — usare `// Follow-up:`), nessun parametro inutilizzato, `ConfigureAwait(false)` nelle librerie come nel codice adiacente, `StringComparison` esplicito.
- Ogni `AddHttpClient(` nel sorgente dell'API deve comparire in `MustBePinned` (con `ConfigureSsrfPin` nella STESSA statement) o in `Exempt` di `apps/api/tests/Api.Tests/Architecture/EgressHttpClientPinArchitectureTests.cs`; il gate legge la statement sorgente, non il runtime. Usare **stringhe letterali** come nome del client nella registrazione.
- Variabili d'ambiente assenti o vuote disattivano una funzionalità, mai `GetValue<bool>` su una variabile che può essere vuota: gating sempre con `string.IsNullOrWhiteSpace`.
- Profilo attuale invariato: con `STORAGE_PROVIDER` diverso da `azureblob` e senza `AZURE_*`/`OTEL_EXPORTER_OTLP_ENDPOINT` nessuna registrazione nuova cambia comportamento (il client `AzureSdk` viene registrato ma non usato).
- Test: `[Trait("Category", TestCategories.Unit)]` per gli unit; gli emulatori sono servizi L3 ⇒ il salto usa `Assert.Skip("PREVISTO: … cd infra && make dev-azure-local …")`, mai `L1Services.FailBecauseUnavailable`. Nessun test nuovo richiede un conto Azure.
- Mai invocare `docker compose` a mano: usare i target del Makefile (anche per la validazione `config`).
- Nessun file `.secret` committato; i valori degli emulatori (chiave Azurite ben nota, `SAS_KEY_VALUE`, password SA dell'emulatore) sono pubblici e vanno in `compose.azure-local.yml` con default `${VAR:-…}`.
- Commit: `feat|fix|docs|test|chore(scope): descrizione` ≤72 caratteri, corpo libero, trailer `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`. Un commit per task, dopo i test verdi.
- Nessun conteggio volatile nella prosa dei documenti (regola di repo): se serve una misura, affiancare il comando.

## Review Focus

1. `STORAGE_PROVIDER=azureblob` senza `AZURE_STORAGE_CONNECTION_STRING` né `AZURE_STORAGE_ACCOUNT_URL`: la factory deve lanciare `InvalidOperationException` con il nome delle due variabili, non restituire lo storage locale in silenzio — test in Task 2 (`Create_AzureBlobWithoutEndpoint_Throws`).
2. `resourceKey` o `fileId` con traversal (`../x`): `StoreAsync`/`RetrieveAsync`/`DeleteAsync` rispondono fallimento senza toccare il container — test in Task 2 (`StoreAsync_TraversalResourceKey_FailsWithoutUpload`).
3. Messaggio Service Bus con `contractVersion` sconosciuta o JSON malformato: la Function lo manda in dead-letter con motivo e NON chiama l'API — test in Task 8 (`Run_UnsupportedContract_DeadLettersWithoutForwarding`).
4. `INTERNAL_SERVICE_TOKEN` assente o vuoto: l'endpoint interno risponde 404 a chiunque, anche con header presente — test in Task 6 (`InvokeAsync_TokenNotConfigured_Returns404`).
5. Bus irraggiungibile durante un upload: l'upload DEVE riuscire comunque e il contatore registra `publish_failed` — test in Task 5 (`Handle_PublisherThrows_SwallowsAndCountsFailure`).

---

### Task 1: Profilo compose `azure-local`, target Makefile, file di configurazione degli emulatori

**Files:**
- Create: `infra/compose.azure-local.yml`
- Create: `infra/azure-local/servicebus-config.json`
- Create: `infra/azure-local/appconfig-seed.json`
- Create: `infra/azure-local/eventgrid-appsettings.json`
- Create: `docs/for-developers/operations/azure-local-profile.md`
- Modify: `infra/Makefile` (dopo il target `dev-down`, riga 18)

**Interfaces:**
- Consumes: nulla.
- Produces: variabili d'ambiente lette dai task successivi — `MEEPLEAI_PROFILE=azure-local`, `STORAGE_PROVIDER=azureblob`, `AZURE_STORAGE_CONNECTION_STRING`, `AZURE_STORAGE_CONTAINER`, `AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT`, `AZURE_APPCONFIG_ENDPOINT`, `AZURE_APPCONFIG_ANONYMOUS`, `AZURE_SERVICEBUS_CONNECTION_STRING`, `AZURE_SERVICEBUS_QUEUE`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `INTERNAL_SERVICE_TOKEN`; nomi di servizio compose `azurite`, `appconfig-emulator`, `servicebus-emulator`, `servicebus-mssql`, `cosmos-emulator`, `aspire-dashboard`, `eventgrid-emulator`, `keyvault-emulator` (profilo `azure-keyvault`).

- [ ] **Step 1: Scrivere `infra/azure-local/servicebus-config.json`**

```json
{
  "UserConfig": {
    "Namespaces": [
      {
        "Name": "sbemulatorns",
        "Queues": [
          {
            "Name": "pdf-uploaded",
            "Properties": {
              "DeadLetteringOnMessageExpiration": true,
              "DefaultMessageTimeToLive": "PT1H",
              "DuplicateDetectionHistoryTimeWindow": "PT20S",
              "ForwardDeadLetteredMessagesTo": "",
              "ForwardTo": "",
              "LockDuration": "PT1M",
              "MaxDeliveryCount": 3,
              "RequiresDuplicateDetection": false,
              "RequiresSession": false
            }
          }
        ],
        "Topics": []
      }
    ],
    "Logging": { "Type": "File" }
  }
}
```

- [ ] **Step 2: Scrivere `infra/azure-local/appconfig-seed.json`** (chiavi di esempio lette dal profilo; i nomi seguono le sezioni già usate dall'API)

```json
{
  "Azure:Profile:Greeting": "hello from app-configuration-emulator",
  "HybridSearch:Enabled": "true"
}
```

- [ ] **Step 3: Scrivere `infra/azure-local/eventgrid-appsettings.json`** (solo smoke: pubblicazione su `http://localhost:<base+6>/pdf-events/api/events` consegnata a una sottoscrizione pull)

```json
{
  "Topics": {
    "pdf-events": [ "pull://smoke-subscription" ]
  }
}
```

- [ ] **Step 4: Scrivere `infra/compose.azure-local.yml`**

```yaml
# Profilo `azure-local` (spec docs/for-developers/specs/2026-10-09-azure-local-profile-design.md).
# Composto SOPRA docker-compose.yml + compose.dev.yml SOLO da `make dev-azure-local`. Avvia gli
# emulatori Azure sulla rete compose e punta l'API a loro con gli SDK Azure veri. Con questo file
# assente il profilo attuale e' byte-identico: ogni funzionalita' e' gated sulle variabili AZURE_*.
#
# Porte sull'host: base AZURE_LOCAL_PORT_BASE (default 20000) per non collidere con un secondo
# checkout (memoria port-conflict-multi-checkout). Mappa:
#   +0 azurite blob · +1 azurite queue · +2 azurite table · +3 appconfig · +4 servicebus amqp
#   +5 servicebus http · +6 eventgrid · +7 cosmos gateway · +8 cosmos explorer · +9 aspire ui
#   +10 aspire otlp grpc · +11 keyvault (profilo azure-keyvault)
#
# Le credenziali qui sotto sono quelle PUBBLICHE degli emulatori (chiave ben nota di Azurite,
# SAS_KEY_VALUE del Service Bus emulator): non sono segreti e non vanno in infra/secrets/.
services:
  api:
    environment:
      MEEPLEAI_PROFILE: "azure-local"
      STORAGE_PROVIDER: "azureblob"
      AZURE_STORAGE_CONNECTION_STRING: "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;"
      AZURE_STORAGE_CONTAINER: "meepleai-uploads"
      # Host di presign raggiungibile dal browser: la firma SAS non copre l'host (#3498 per S3).
      AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT: "http://localhost:${AZURE_LOCAL_PORT_BASE:-20000}"
      AZURE_APPCONFIG_ENDPOINT: "http://appconfig-emulator:8483"
      AZURE_APPCONFIG_ANONYMOUS: "true"
      AZURE_SERVICEBUS_CONNECTION_STRING: "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
      AZURE_SERVICEBUS_QUEUE: "pdf-uploaded"
      OTEL_EXPORTER_OTLP_ENDPOINT: "http://aspire-dashboard:18889"
      OTEL_EXPORTER_OTLP_PROTOCOL: "grpc"
      # Token condiviso con la Function locale (apps/functions): valore di sviluppo, non un segreto.
      INTERNAL_SERVICE_TOKEN: "${INTERNAL_SERVICE_TOKEN:-azure-local-dev-token}"
      # Solo con il profilo azure-keyvault (certificato self-signed dell'emulatore).
      AZURE_SDK_ALLOW_INSECURE_TLS: "${AZURE_SDK_ALLOW_INSECURE_TLS:-false}"
    depends_on:
      azurite:
        condition: service_healthy
      appconfig-emulator:
        condition: service_healthy
      servicebus-emulator:
        condition: service_healthy
      aspire-dashboard:
        condition: service_started

  azurite:
    image: mcr.microsoft.com/azure-storage/azurite:3.37.0
    container_name: meepleai-azurite
    command: ["azurite", "--blobHost", "0.0.0.0", "--queueHost", "0.0.0.0", "--tableHost", "0.0.0.0", "--location", "/data", "--skipApiVersionCheck", "--loose"]
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_BASE:-20000}:10000"
      - "127.0.0.1:${AZURE_LOCAL_PORT_AZURITE_QUEUE:-20001}:10001"
      - "127.0.0.1:${AZURE_LOCAL_PORT_AZURITE_TABLE:-20002}:10002"
    volumes:
      - azurite_data:/data
    healthcheck:
      test: ["CMD-SHELL", "nc -z 127.0.0.1 10000 || exit 1"]
      interval: 5s
      timeout: 3s
      retries: 10
    networks: [meepleai]

  appconfig-emulator:
    image: mcr.microsoft.com/azure-app-configuration/app-configuration-emulator:1.2.0
    container_name: meepleai-appconfig-emulator
    # Il Dockerfile dell'emulatore non imposta l'ambiente ASP.NET: l'auth anonima vive in
    # appsettings.Development.json, quindi va dichiarata qui con la convenzione Section__Key.
    environment:
      Tenant__AnonymousAuthEnabled: "true"
      Tenant__HmacSha256Enabled: "false"
      Authentication__Anonymous__AnonymousUserRole: "Owner"
      Hosting__IPAddress: "0.0.0.0"
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_APPCONFIG:-20003}:8483"
    healthcheck:
      test: ["CMD-SHELL", "wget -qO- http://127.0.0.1:8483/health || exit 1"]
      interval: 5s
      timeout: 3s
      retries: 20
    networks: [meepleai]

  appconfig-seed:
    image: curlimages/curl:8.11.1
    container_name: meepleai-appconfig-seed
    depends_on:
      appconfig-emulator:
        condition: service_healthy
    volumes:
      - ./azure-local/appconfig-seed.json:/seed.json:ro
    # Un PUT per chiave sull'API REST dell'emulatore (api-version 2023-11-01); il file e' una
    # mappa piatta chiave→valore, letta con sed per non dipendere da jq nell'immagine.
    entrypoint: ["/bin/sh", "-c"]
    command:
      - |
        set -eu
        sed -n 's/^ *"\([^"]*\)": *"\([^"]*\)".*/\1\t\2/p' /seed.json | while IFS="$$(printf '\t')" read -r key value; do
          encoded=$$(printf '%s' "$$key" | sed 's/:/%3A/g')
          curl -fsS -X PUT "http://appconfig-emulator:8483/kv/$$encoded?api-version=2023-11-01" \
            -H "Content-Type: application/vnd.microsoft.appconfig.kv+json" \
            -d "{\"value\":\"$$value\"}" > /dev/null
          echo "seeded $$key"
        done
    networks: [meepleai]

  servicebus-mssql:
    image: mcr.microsoft.com/mssql/server:2022-latest
    container_name: meepleai-servicebus-mssql
    environment:
      ACCEPT_EULA: "Y"
      MSSQL_SA_PASSWORD: "${SERVICEBUS_MSSQL_SA_PASSWORD:-AzureLocal!Dev2026}"
    networks: [meepleai]

  servicebus-emulator:
    image: mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1
    container_name: meepleai-servicebus-emulator
    depends_on: [servicebus-mssql]
    volumes:
      - ./azure-local/servicebus-config.json:/ServiceBus_Emulator/ConfigFiles/Config.json:ro
    environment:
      ACCEPT_EULA: "Y"
      SQL_SERVER: servicebus-mssql
      MSSQL_SA_PASSWORD: "${SERVICEBUS_MSSQL_SA_PASSWORD:-AzureLocal!Dev2026}"
      EMULATOR_HTTP_PORT: "5300"
      SQL_WAIT_INTERVAL: "30"
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_SB_AMQP:-20004}:5672"
      - "127.0.0.1:${AZURE_LOCAL_PORT_SB_HTTP:-20005}:5300"
    healthcheck:
      test: ["CMD-SHELL", "curl -fsS http://127.0.0.1:5300/health || exit 1"]
      interval: 10s
      timeout: 5s
      retries: 30
      start_period: 60s
    networks: [meepleai]

  eventgrid-emulator:
    image: workleap/eventgridemulator:latest
    container_name: meepleai-eventgrid-emulator
    volumes:
      - ./azure-local/eventgrid-appsettings.json:/app/appsettings.json:ro
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_EVENTGRID:-20006}:6500"
    networks: [meepleai]

  cosmos-emulator:
    # Nel profilo per la SECONDA iterazione (ConversationMemory): oggi nessun consumer.
    image: mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator:vnext-latest
    container_name: meepleai-cosmos-emulator
    environment:
      PROTOCOL: "https"
      ENABLE_EXPLORER: "true"
      ENABLE_TELEMETRY: "false"
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_COSMOS:-20007}:8081"
      - "127.0.0.1:${AZURE_LOCAL_PORT_COSMOS_EXPLORER:-20008}:1234"
    volumes:
      - cosmos_data:/data
    networks: [meepleai]

  aspire-dashboard:
    image: mcr.microsoft.com/dotnet/aspire-dashboard:13.6.0
    container_name: meepleai-aspire-dashboard
    environment:
      ASPIRE_DASHBOARD_UNSECURED_ALLOW_ANONYMOUS: "true"
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_ASPIRE_UI:-20009}:18888"
      - "127.0.0.1:${AZURE_LOCAL_PORT_ASPIRE_OTLP:-20010}:18889"
    networks: [meepleai]

  keyvault-emulator:
    # OPT-IN (profilo azure-keyvault): richiede emulator.pfx + emulator.crt generati sull'host con
    # docs/setup.sh del progetto james-gould/azure-keyvault-emulator e messi in
    # infra/azure-local/keyvault-certs/ (git-ignorata). Vedi docs/for-developers/operations/azure-local-profile.md.
    image: jamesgoulddev/azure-keyvault-emulator:latest
    container_name: meepleai-keyvault-emulator
    profiles: [azure-keyvault]
    environment:
      Persist: "true"
    volumes:
      - ./azure-local/keyvault-certs:/certs
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_KEYVAULT:-20011}:4997"
    networks: [meepleai]

volumes:
  azurite_data:
  cosmos_data:
```

- [ ] **Step 5: Aggiungere i target al Makefile** (subito dopo `dev-down`, stesso stile `##`)

```makefile
# === Azure local profile (spec 2026-10-09-azure-local-profile-design.md) ===
AZURE_LOCAL_PORT_BASE ?= 20000
AZURE_LOCAL_PORTS := AZURE_LOCAL_PORT_BASE=$(AZURE_LOCAL_PORT_BASE) \
	AZURE_LOCAL_PORT_AZURITE_QUEUE=$$(( $(AZURE_LOCAL_PORT_BASE) + 1 )) \
	AZURE_LOCAL_PORT_AZURITE_TABLE=$$(( $(AZURE_LOCAL_PORT_BASE) + 2 )) \
	AZURE_LOCAL_PORT_APPCONFIG=$$(( $(AZURE_LOCAL_PORT_BASE) + 3 )) \
	AZURE_LOCAL_PORT_SB_AMQP=$$(( $(AZURE_LOCAL_PORT_BASE) + 4 )) \
	AZURE_LOCAL_PORT_SB_HTTP=$$(( $(AZURE_LOCAL_PORT_BASE) + 5 )) \
	AZURE_LOCAL_PORT_EVENTGRID=$$(( $(AZURE_LOCAL_PORT_BASE) + 6 )) \
	AZURE_LOCAL_PORT_COSMOS=$$(( $(AZURE_LOCAL_PORT_BASE) + 7 )) \
	AZURE_LOCAL_PORT_COSMOS_EXPLORER=$$(( $(AZURE_LOCAL_PORT_BASE) + 8 )) \
	AZURE_LOCAL_PORT_ASPIRE_UI=$$(( $(AZURE_LOCAL_PORT_BASE) + 9 )) \
	AZURE_LOCAL_PORT_ASPIRE_OTLP=$$(( $(AZURE_LOCAL_PORT_BASE) + 10 )) \
	AZURE_LOCAL_PORT_KEYVAULT=$$(( $(AZURE_LOCAL_PORT_BASE) + 11 ))
AZURE_LOCAL_COMPOSE := $(COMPOSE) -f compose.dev.yml -f compose.azure-local.yml

dev-azure-local: ## Start core + Azure emulators (azurite, appconfig, servicebus, cosmos, aspire, eventgrid)
	@bash scripts/seed-status.sh --brief || true
	$(AZURE_LOCAL_PORTS) $(AZURE_LOCAL_COMPOSE) up -d

dev-azure-local-keyvault: ## Same as dev-azure-local plus the Key Vault emulator (needs infra/azure-local/keyvault-certs)
	$(AZURE_LOCAL_PORTS) AZURE_SDK_ALLOW_INSECURE_TLS=true $(AZURE_LOCAL_COMPOSE) --profile azure-keyvault up -d

dev-azure-local-down: ## Stop the azure-local profile (emulators included)
	$(AZURE_LOCAL_PORTS) $(AZURE_LOCAL_COMPOSE) --profile azure-keyvault down

dev-azure-local-config: ## Validate the azure-local compose stack (no containers started)
	$(AZURE_LOCAL_PORTS) $(AZURE_LOCAL_COMPOSE) --profile azure-keyvault config --quiet && echo "azure-local compose OK"
```

- [ ] **Step 6: Aggiungere `infra/azure-local/keyvault-certs/` a `.gitignore`** (riga nuova in fondo a `infra/.gitignore` se esiste, altrimenti nel `.gitignore` di radice): `infra/azure-local/keyvault-certs/`

- [ ] **Step 7: Validare lo stack**

Run (da `infra/`, Git Bash): `make dev-azure-local-config`
Expected: `azure-local compose OK` senza warning di variabili non impostate. Se compose segnala `The "…" variable is not set`, la porta corrispondente manca da `AZURE_LOCAL_PORTS`.

- [ ] **Step 8: Scrivere la doc operativa `docs/for-developers/operations/azure-local-profile.md`**

Contenuto minimo, con le sezioni: **Cosa avvia** (tabella servizio → immagine → porta host come funzione di `AZURE_LOCAL_PORT_BASE`), **Comandi** (`make dev-azure-local`, `make dev-azure-local-down`, `make dev-azure-local-config`, `make dev-azure-local-keyvault`), **Key Vault opt-in** (i tre passi: clonare `james-gould/azure-keyvault-emulator`, eseguire `docs/setup.sh`, copiare `emulator.pfx` e `emulator.crt` in `infra/azure-local/keyvault-certs/`; l'API usa TLS insicuro solo con `AZURE_SDK_ALLOW_INSECURE_TLS=true`), **Limiti degli emulatori** (Service Bus: niente entità partizionate né AMQP WebSocket; Cosmos vNext: nessuna autenticazione, solo gateway, DiskANN; App Configuration: auth anonima via env), **Variabili iniettate nell'API** (la lista di Interfaces/Produces con una riga di significato ciascuna). Niente conteggi in prosa.

- [ ] **Step 9: Commit**

```bash
git add infra/compose.azure-local.yml infra/azure-local infra/Makefile docs/for-developers/operations/azure-local-profile.md .gitignore
git commit -m "feat(infra): profilo compose azure-local con emulatori Azure" -m "Azurite, App Configuration, Service Bus (+MSSQL), Cosmos vNext, Aspire Dashboard, Event Grid; Key Vault opt-in. Target make dev-azure-local*." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Client HTTP `AzureSdk`/`AzureSdkLocal`, provider `azureblob` e `AzureBlobStorageService`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Azure/AzureSdkHttpClients.cs`
- Create: `apps/api/src/Api/Services/Pdf/AzureBlobStorageOptions.cs`
- Create: `apps/api/src/Api/Services/Pdf/AzureBlobStorageService.cs`
- Modify: `apps/api/src/Api/Services/Pdf/BlobStorageServiceFactory.cs` (switch in `Create`, nuovo metodo privato)
- Modify: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Egress.cs` (`EgressSinks`)
- Modify: `apps/api/src/Api/Program.cs` (dopo `builder.Services.AddInfrastructureServices(...)`, riga 350)
- Modify: `apps/api/src/Api/Api.csproj` (PackageReference)
- Modify: `apps/api/tests/Api.Tests/Architecture/EgressHttpClientPinArchitectureTests.cs` (`MustBePinned`, `Exempt`)
- Test: `apps/api/tests/Api.Tests/Services/Pdf/AzureBlobStorageServiceTests.cs`
- Test: `apps/api/tests/Api.Tests/Services/Pdf/BlobStorageServiceFactoryAzureTests.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Azure/AzureSdkHttpClientsTests.cs`

**Interfaces:**
- Consumes: `IBlobStorageService` (11 metodi, `Services/Pdf/IBlobStorageService.cs`), `BlobCategory.ToS3Folder()`, `PathSecurity.ValidateIdentifier(string, string)` (`Api.Infrastructure.Security`), `StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file")` (`Api.Helpers`), `SsrfPinnedHttpClientBuilderExtensions.ConfigureSsrfPin(sink, allowAutoRedirect, configureHandler, allowedHostSuffixes)`.
- Produces: `AzureSdkHttpClients.PinnedName = "AzureSdk"`, `LocalName = "AzureSdkLocal"`, `AzureSdkHttpClients.CreateTransport(IServiceProvider) : HttpClientTransport`, `AzureSdkHttpClients.IsLocalProfile(IConfiguration)`, `AzureSdkHttpClients.ReadAllowedHostSuffixes(IConfiguration)`; `AzureBlobStorageOptions`; `AzureBlobStorageService(BlobServiceClient, BlobContainerClient, AzureBlobStorageOptions, ILogger<AzureBlobStorageService>)`; `MeepleAiMetrics.EgressSinks.AzureSdk = "azure_sdk"`.

- [ ] **Step 1: Aggiungere i pacchetti**

Run (da `apps/api/src/Api`):
```bash
dotnet add package Azure.Storage.Blobs --version 12.30.1
dotnet add package Azure.Identity --version 1.21.0
```
Expected: le due righe `<PackageReference>` compaiono in `Api.csproj`; `dotnet build` verde.

- [ ] **Step 2: Scrivere i test del selettore di client** `apps/api/tests/Api.Tests/Infrastructure/Azure/AzureSdkHttpClientsTests.cs`

```csharp
using Api.Infrastructure.Azure;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure.Azure;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class AzureSdkHttpClientsTests
{
    private static IConfiguration Config(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    private static ServiceProvider Build(IConfiguration config)
    {
        var services = new ServiceCollection();
        services.AddSingleton(config);
        services.AddLogging();
        services.AddAzureSdkHttpClients(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void IsLocalProfile_IsCaseInsensitiveAndFalseWhenAbsentOrEmpty()
    {
        AzureSdkHttpClients.IsLocalProfile(Config(("MEEPLEAI_PROFILE", "Azure-Local"))).Should().BeTrue();
        AzureSdkHttpClients.IsLocalProfile(Config(("MEEPLEAI_PROFILE", ""))).Should().BeFalse();
        AzureSdkHttpClients.IsLocalProfile(Config()).Should().BeFalse();
    }

    [Fact]
    public void ReadAllowedHostSuffixes_TrimsAndDropsBlanks()
    {
        var config = Config(
            ("Azure:Sdk:AllowedHostSuffixes:0", " .blob.core.windows.net "),
            ("Azure:Sdk:AllowedHostSuffixes:1", ""),
            ("Azure:Sdk:AllowedHostSuffixes:2", ".vault.azure.net"));

        AzureSdkHttpClients.ReadAllowedHostSuffixes(config)
            .Should().Equal(".blob.core.windows.net", ".vault.azure.net");
    }

    [Fact]
    public void CreateTransport_LocalProfile_UsesLocalClientEvenWithoutSuffixes()
    {
        using var sp = Build(Config(("MEEPLEAI_PROFILE", "azure-local")));

        var transport = AzureSdkHttpClients.CreateTransport(sp);

        transport.Should().NotBeNull();
    }

    [Fact]
    public void CreateTransport_AzureProfileWithoutSuffixes_Throws()
    {
        using var sp = Build(Config(("MEEPLEAI_PROFILE", "azure-exam")));

        var act = () => AzureSdkHttpClients.CreateTransport(sp);

        act.Should().Throw<InvalidOperationException>().WithMessage("*Azure:Sdk:AllowedHostSuffixes*");
    }

    [Fact]
    public void CreateTransport_AzureProfileWithSuffixes_UsesPinnedClient()
    {
        using var sp = Build(Config(
            ("MEEPLEAI_PROFILE", "azure-exam"),
            ("Azure:Sdk:AllowedHostSuffixes:0", ".blob.core.windows.net")));

        var transport = AzureSdkHttpClients.CreateTransport(sp);

        transport.Should().NotBeNull();
    }
}
```

- [ ] **Step 3: Eseguire i test per vederli fallire**

Run (da `apps/api`): `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureSdkHttpClientsTests"`
Expected: errore di compilazione `AzureSdkHttpClients` non esiste.

- [ ] **Step 4: Aggiungere il sink e scrivere `AzureSdkHttpClients`**

In `MeepleAiMetrics.Egress.cs`, dentro `EgressSinks`, dopo `Slack`:
```csharp
        /// <summary>SDK Azure (Blob, Key Vault, App Configuration) nel profilo Azure vero — spec 2026-10-09.</summary>
        public const string AzureSdk = "azure_sdk";
```

`apps/api/src/Api/Infrastructure/Azure/AzureSdkHttpClients.cs`:
```csharp
using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;
using Azure.Core.Pipeline;

namespace Api.Infrastructure.Azure;

/// <summary>
/// I due <see cref="HttpClient"/> nominati che trasportano gli SDK Azure (spec azure-local §4.5).
/// <para>
/// <c>AzureSdk</c> è pinnato con <c>ConfigureSsrfPin</c> e serve il profilo Azure vero: il pin blocca
/// OGNI indirizzo privato (<c>SsrfPolicy.IsBlocked</c>), quindi non può raggiungere gli emulatori sulla
/// rete compose. Per questo esiste <c>AzureSdkLocal</c>, esente dal pin e scelto SOLO quando
/// <c>MEEPLEAI_PROFILE=azure-local</c>. La scelta avviene a runtime in <see cref="CreateTransport"/>:
/// gli SDK ricevono un <see cref="HttpClientTransport"/> e non sanno quale dei due stanno usando.
/// </para>
/// <para>
/// Il client pinnato senza suffissi ammessi non restringerebbe l'host: <see cref="CreateTransport"/>
/// rifiuta quel caso invece di lasciare un default permissivo.
/// </para>
/// </summary>
internal static class AzureSdkHttpClients
{
    public const string PinnedName = "AzureSdk";
    public const string LocalName = "AzureSdkLocal";
    public const string ProfileKey = "MEEPLEAI_PROFILE";
    public const string LocalProfile = "azure-local";
    public const string AllowedHostSuffixesKey = "Azure:Sdk:AllowedHostSuffixes";
    public const string AllowInsecureTlsKey = "AZURE_SDK_ALLOW_INSECURE_TLS";

    public static bool IsLocalProfile(IConfiguration configuration) =>
        string.Equals(configuration[ProfileKey], LocalProfile, StringComparison.OrdinalIgnoreCase);

    public static bool AllowInsecureTls(IConfiguration configuration) =>
        IsLocalProfile(configuration)
        && string.Equals(configuration[AllowInsecureTlsKey], "true", StringComparison.OrdinalIgnoreCase);

    public static IReadOnlyList<string> ReadAllowedHostSuffixes(IConfiguration configuration) =>
        configuration.GetSection(AllowedHostSuffixesKey).Get<string[]>()?
            .Where(s => !string.IsNullOrWhiteSpace(s))
            .Select(s => s.Trim())
            .ToArray() ?? [];

    public static IServiceCollection AddAzureSdkHttpClients(this IServiceCollection services, IConfiguration configuration)
    {
        var suffixes = ReadAllowedHostSuffixes(configuration);
        var allowInsecureTls = AllowInsecureTls(configuration);

        services.AddHttpClient("AzureSdk").ConfigureSsrfPin(MeepleAiMetrics.EgressSinks.AzureSdk, allowedHostSuffixes: suffixes);

        services.AddHttpClient("AzureSdkLocal").ConfigurePrimaryHttpMessageHandler(() =>
        {
            var handler = new SocketsHttpHandler();
            if (allowInsecureTls)
            {
                // Solo per il certificato self-signed del Key Vault emulator, solo nel profilo locale.
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
            }

            return handler;
        });

        return services;
    }

    /// <summary>Il transport da passare nelle <c>ClientOptions</c> di ogni SDK Azure.</summary>
    public static HttpClientTransport CreateTransport(IServiceProvider serviceProvider)
    {
        var configuration = serviceProvider.GetRequiredService<IConfiguration>();
        var factory = serviceProvider.GetRequiredService<IHttpClientFactory>();

        if (IsLocalProfile(configuration))
        {
            return new HttpClientTransport(factory.CreateClient(LocalName));
        }

        if (ReadAllowedHostSuffixes(configuration).Count == 0)
        {
            throw new InvalidOperationException(
                $"{AllowedHostSuffixesKey} must list the Azure endpoint suffixes (for example .blob.core.windows.net) " +
                $"when {ProfileKey} is not '{LocalProfile}': the pinned AzureSdk client refuses to run without a host allow-list.");
        }

        return new HttpClientTransport(factory.CreateClient(PinnedName));
    }
}
```

In `Program.cs`, dopo la riga `builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);`:
```csharp
builder.Services.AddAzureSdkHttpClients(builder.Configuration); // spec azure-local §4.5
```
(aggiungere `using Api.Infrastructure.Azure;` in testa).

Nel gate `EgressHttpClientPinArchitectureTests.cs`, in `MustBePinned`:
```csharp
        ["AzureSdk"] = "Azure SDK (Blob/Key Vault/App Configuration) verso *.windows.net/*.azure.net (spec azure-local §4.5)",
```
e in `Exempt`:
```csharp
        ["AzureSdkLocal"] = "stessi SDK verso gli emulatori sulla rete compose: usato SOLO con MEEPLEAI_PROFILE=azure-local "
            + "(AzureSdkHttpClients.CreateTransport); il pin bloccherebbe gli indirizzi privati degli emulatori",
```

- [ ] **Step 5: Eseguire i test del selettore e il gate**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureSdkHttpClientsTests|FullyQualifiedName~EgressHttpClientPinArchitectureTests"`
Expected: tutti verdi (5 + 3).

- [ ] **Step 6: Scrivere i test del servizio blob** `apps/api/tests/Api.Tests/Services/Pdf/AzureBlobStorageServiceTests.cs`

```csharp
using Api.Services.Pdf;
using Api.Tests.Constants;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class AzureBlobStorageServiceTests
{
    private const string Container = "meepleai-uploads";
    private static readonly Uri ContainerUri = new("http://127.0.0.1:10000/devstoreaccount1/meepleai-uploads");

    private readonly Mock<BlobServiceClient> _service = new();
    private readonly Mock<BlobContainerClient> _container = new();
    private readonly Mock<BlobClient> _blob = new();

    private AzureBlobStorageService CreateSut(string? publicEndpoint = null)
    {
        _container.SetupGet(c => c.Name).Returns(Container);
        _container.SetupGet(c => c.Uri).Returns(ContainerUri);
        _container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_blob.Object);
        _container
            .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(new ETag("x"), DateTimeOffset.UtcNow), Mock.Of<Response>()));

        var options = new AzureBlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = Container,
            PublicBlobEndpoint = publicEndpoint,
            SasExpirySeconds = 600,
        };
        return new AzureBlobStorageService(_service.Object, _container.Object, options, Mock.Of<ILogger<AzureBlobStorageService>>());
    }

    private static Page<BlobItem> PageOf(params string[] names) =>
        Page<BlobItem>.FromValues(names.Select(n => BlobsModelFactory.BlobItem(name: n)).ToList(), null, Mock.Of<Response>());

    private void SetupListing(string prefix, params string[] names) =>
        _container
            .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), prefix, It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<BlobItem>.FromPages([PageOf(names)]));

    [Fact]
    public async Task StoreAsync_WritesCategorizedKeyWithPdfContentType()
    {
        string? uploadedKey = null;
        BlobUploadOptions? uploadOptions = null;
        _container.Setup(c => c.GetBlobClient(It.IsAny<string>()))
            .Callback<string>(k => uploadedKey = k)
            .Returns(_blob.Object);
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobUploadOptions, CancellationToken>((_, o, _) => uploadOptions = o)
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(new ETag("e"), DateTimeOffset.UtcNow, null, null, null, null, 1), Mock.Of<Response>()));
        var sut = CreateSut();
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await sut.StoreAsync(content, "Rule Book.pdf", BlobCategory.Pdf, "game-1");

        result.Success.Should().BeTrue();
        result.FileSizeBytes.Should().Be(3);
        uploadedKey.Should().MatchRegex("^pdfs/game-1/[0-9a-f]{32}_Rule_Book\\.pdf$");
        result.FilePath.Should().Be(uploadedKey);
        result.FileId.Should().HaveLength(32);
        uploadOptions!.HttpHeaders!.ContentType.Should().Be("application/pdf");
    }

    [Fact]
    public async Task StoreAsync_TraversalResourceKey_FailsWithoutUpload()
    {
        var sut = CreateSut();
        using var content = new MemoryStream(new byte[] { 1 });

        var result = await sut.StoreAsync(content, "a.pdf", BlobCategory.Pdf, "../etc");

        result.Success.Should().BeFalse();
        _blob.Verify(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task RetrieveAsync_ResolvesByPrefixAndReturnsStream()
    {
        SetupListing("pdfs/game-1/abc_", "pdfs/game-1/abc_a.pdf");
        var expected = new MemoryStream(new byte[] { 9 });
        _blob.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobDownloadStreamingResult(expected), Mock.Of<Response>()));
        var sut = CreateSut();

        await using var stream = await sut.RetrieveAsync("abc", BlobCategory.Pdf, "game-1");

        stream.Should().BeSameAs(expected);
        _container.Verify(c => c.GetBlobClient("pdfs/game-1/abc_a.pdf"), Times.Once);
    }

    [Fact]
    public async Task RetrieveAsync_NoMatch_ReturnsNull()
    {
        SetupListing("pdfs/game-1/missing_");
        var sut = CreateSut();

        var stream = await sut.RetrieveAsync("missing", BlobCategory.Pdf, "game-1");

        stream.Should().BeNull();
    }

    [Fact]
    public async Task DeleteAsync_DeletesEveryMatchAndReturnsTrue()
    {
        SetupListing("pdfs/game-1/abc_", "pdfs/game-1/abc_a.pdf", "pdfs/game-1/abc_b.pdf");
        _container.Setup(c => c.DeleteBlobIfExistsAsync(It.IsAny<string>(), It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        var sut = CreateSut();

        var deleted = await sut.DeleteAsync("abc", BlobCategory.Pdf, "game-1");

        deleted.Should().BeTrue();
        _container.Verify(c => c.DeleteBlobIfExistsAsync("pdfs/game-1/abc_a.pdf", It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        _container.Verify(c => c.DeleteBlobIfExistsAsync("pdfs/game-1/abc_b.pdf", It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NoMatch_ReturnsFalse()
    {
        SetupListing("pdfs/game-1/abc_");
        var sut = CreateSut();

        (await sut.DeleteAsync("abc", BlobCategory.Pdf, "game-1")).Should().BeFalse();
    }

    [Fact]
    public async Task ExistsAsync_TraversalFileId_ReturnsFalse()
    {
        var sut = CreateSut();

        (await sut.ExistsAsync("../x", BlobCategory.Pdf, "game-1")).Should().BeFalse();
        _container.Verify(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public void GetStoragePath_UsesSameLayoutAsS3()
    {
        var sut = CreateSut();

        sut.GetStoragePath("abc", BlobCategory.GameImage, "game-1", "cover one.png")
            .Should().Be("game-images/game-1/abc_cover_one.png");
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_MissingBlob_ReturnsNull()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));
        var sut = CreateSut();

        (await sut.GetPresignedUrlForRawKeyAsync("covers/x.png")).Should().BeNull();
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_UserDelegationPath_RewritesPublicEndpoint()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        _blob.SetupGet(b => b.CanGenerateSasUri).Returns(false);
        _blob.SetupGet(b => b.Uri).Returns(new Uri("http://azurite:10000/devstoreaccount1/meepleai-uploads/covers/x.png"));
        _service.SetupGet(s => s.AccountName).Returns("devstoreaccount1");
        var key = BlobsModelFactory.UserDelegationKey(
            "oid", "tid", DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1), "b", "2024-11-04",
            Convert.ToBase64String(new byte[32]));
        _service.Setup(s => s.GetUserDelegationKeyAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(key, Mock.Of<Response>()));
        var sut = CreateSut(publicEndpoint: "http://localhost:20000");

        var url = await sut.GetPresignedUrlForRawKeyAsync("covers/x.png", 120);

        url.Should().StartWith("http://localhost:20000/devstoreaccount1/meepleai-uploads/covers/x.png?");
        url.Should().Contain("sig=");
        url.Should().Contain("skoid=oid");
    }

    [Fact]
    public async Task StoreRawKeyAsync_WritesVerbatimKeyWithGivenContentType()
    {
        BlobUploadOptions? uploadOptions = null;
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobUploadOptions, CancellationToken>((_, o, _) => uploadOptions = o)
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContentInfo(new ETag("e"), DateTimeOffset.UtcNow, null, null, null, null, 1), Mock.Of<Response>()));
        var sut = CreateSut();
        using var content = new MemoryStream(new byte[] { 1 });

        var ok = await sut.StoreRawKeyAsync("covers/game-1/cover.png", content, "image/png");

        ok.Should().BeTrue();
        _container.Verify(c => c.GetBlobClient("covers/game-1/cover.png"), Times.Once);
        uploadOptions!.HttpHeaders!.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task RetrieveRawKeyAsync_NotFound_ReturnsNull()
    {
        _blob.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "BlobNotFound"));
        var sut = CreateSut();

        (await sut.RetrieveRawKeyAsync("covers/none.png")).Should().BeNull();
    }
}
```

- [ ] **Step 7: Eseguire i test per vederli fallire**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureBlobStorageServiceTests"`
Expected: errore di compilazione (`AzureBlobStorageService`/`AzureBlobStorageOptions` mancanti).

- [ ] **Step 8: Scrivere `AzureBlobStorageOptions.cs`**

```csharp
namespace Api.Services.Pdf;

/// <summary>Configurazione del provider <c>azureblob</c> (spec azure-local §4.1).</summary>
internal sealed class AzureBlobStorageOptions
{
    /// <summary>Connection string con chiave condivisa (Azurite o account vero). Alternativa a <see cref="AccountUrl"/>.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>URL dell'account (<c>https://&lt;account&gt;.blob.core.windows.net</c>) con <c>DefaultAzureCredential</c>. Alternativa a <see cref="ConnectionString"/>.</summary>
    public string? AccountUrl { get; init; }

    public required string ContainerName { get; init; }

    /// <summary>
    /// Host pubblico con cui riscrivere scheme/host/porta degli URL SAS (come <c>S3_PUBLIC_ENDPOINT</c>, #3498):
    /// la firma SAS copre risorsa e parametri, non l'host, quindi la riscrittura resta valida.
    /// </summary>
    public string? PublicBlobEndpoint { get; init; }

    public int SasExpirySeconds { get; init; } = 3600;
}
```

- [ ] **Step 9: Scrivere `AzureBlobStorageService.cs`**

```csharp
using System.Globalization;
using Api.Helpers;
using Api.Infrastructure.Security;
using Azure;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Api.Services.Pdf;

/// <summary>
/// <see cref="IBlobStorageService"/> su Azure Blob Storage (spec azure-local §4.1). Stesso layout di chiave
/// di <see cref="S3BlobStorageService"/> — <c>{category}/{resourceKey}/{fileId}_{fileName}</c> — e stesse
/// semantiche: risoluzione per prefisso, raw-key senza validazione, HEAD prima della firma, mai un'eccezione
/// oltre il confine del servizio.
/// </summary>
internal sealed class AzureBlobStorageService : IBlobStorageService
{
    private readonly BlobServiceClient _serviceClient;
    private readonly BlobContainerClient _container;
    private readonly AzureBlobStorageOptions _options;
    private readonly ILogger<AzureBlobStorageService> _logger;
    private bool _containerEnsured;

    public AzureBlobStorageService(
        BlobServiceClient serviceClient,
        BlobContainerClient container,
        AzureBlobStorageOptions options,
        ILogger<AzureBlobStorageService> logger)
    {
        _serviceClient = serviceClient;
        _container = container;
        _options = options;
        _logger = logger;
    }

    internal AzureBlobStorageOptions Options => _options;

    public async Task<BlobStorageResult> StoreAsync(Stream stream, string fileName, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        MemoryStream? buffer = null;
        try
        {
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var (uploadStream, length, owned) = await BufferIfNeededAsync(stream, ct).ConfigureAwait(false);
            buffer = owned;

            var fileId = Guid.NewGuid().ToString("N");
            var key = GetKey(fileId, category, resourceKey, SanitizeFileName(fileName));

            await EnsureContainerAsync(ct).ConfigureAwait(false);
            await _container.GetBlobClient(key)
                .UploadAsync(uploadStream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = "application/pdf" } }, ct)
                .ConfigureAwait(false);

            _logger.LogInformation("Stored blob {Key} ({Size} bytes)", key, length);
            return new BlobStorageResult(true, fileId, key, length);
        }
        catch (RequestFailedException ex)
        {
            _logger.LogError(ex, "Azure Blob error storing file in {Category}/{ResourceKey}: {ErrorCode}", category, resourceKey, ex.ErrorCode);
            return new BlobStorageResult(false, null, null, 0, $"Azure Blob error: {ex.Message}");
        }
#pragma warning disable CA1031 // Confine del servizio: nessuna eccezione oltre (stesso pattern di S3BlobStorageService)
        catch (Exception ex)
        {
            _logger.LogError(ex, "Unexpected error storing file in {Category}/{ResourceKey}", category, resourceKey);
            return new BlobStorageResult(false, null, null, 0, ex.Message);
        }
#pragma warning restore CA1031
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<Stream?> RetrieveAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var key = await TryResolveKeyAsync(fileId, category, resourceKey, ct).ConfigureAwait(false);
            if (key is null)
            {
                _logger.LogWarning("Blob not found for {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return null;
            }

            return await DownloadAsync(key, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving blob {FileId} for {Category}/{ResourceKey}", fileId, category, resourceKey);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> DeleteAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct = default)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var matches = new List<string>();
            await foreach (var item in _container.GetBlobsAsync(prefix: GetPrefix(fileId, category, resourceKey), cancellationToken: ct).ConfigureAwait(false))
            {
                matches.Add(item.Name);
            }

            if (matches.Count == 0)
            {
                _logger.LogWarning("Blob not found for deletion: {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return false;
            }

            foreach (var name in matches)
            {
                await _container.DeleteBlobIfExistsAsync(name, cancellationToken: ct).ConfigureAwait(false);
                _logger.LogInformation("Deleted blob {Key}", name);
            }

            return true;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting blob {FileId} for {Category}/{ResourceKey}", fileId, category, resourceKey);
            return false;
        }
#pragma warning restore CA1031
    }

    public string GetStoragePath(string fileId, BlobCategory category, string resourceKey, string fileName)
    {
        PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));
        return GetKey(fileId, category, resourceKey, SanitizeFileName(fileName));
    }

    public async Task<bool> ExistsAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken cancellationToken = default)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));
            return await TryResolveKeyAsync(fileId, category, resourceKey, cancellationToken).ConfigureAwait(false) is not null;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error checking blob existence {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<string?> GetPresignedDownloadUrlAsync(string fileId, BlobCategory category, string resourceKey, int? expirySeconds = null)
    {
        try
        {
            PathSecurity.ValidateIdentifier(fileId, nameof(fileId));
            PathSecurity.ValidateIdentifier(resourceKey, nameof(resourceKey));

            var key = await TryResolveKeyAsync(fileId, category, resourceKey, CancellationToken.None).ConfigureAwait(false);
            if (key is null)
            {
                _logger.LogWarning("Cannot sign URL: blob not found {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
                return null;
            }

            return await BuildReadSasAsync(key, expirySeconds ?? _options.SasExpirySeconds).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing URL for {FileId} in {Category}/{ResourceKey}", fileId, category, resourceKey);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<string?> GetPresignedUrlForRawKeyAsync(string rawKey, int? expirySeconds = null)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return null;
            }

            // HEAD prima della firma: un URL firmato verso un blob assente renderebbe un'immagine rotta.
            var exists = await _container.GetBlobClient(rawKey).ExistsAsync(CancellationToken.None).ConfigureAwait(false);
            if (!exists.Value)
            {
                _logger.LogWarning("Cannot sign URL: raw key not found {Key}", rawKey);
                return null;
            }

            return await BuildReadSasAsync(rawKey, expirySeconds ?? _options.SasExpirySeconds).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error signing URL for raw key {Key}", rawKey);
            return null;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> DeleteRawKeyAsync(string rawKey, CancellationToken ct = default)
    {
        try
        {
            if (string.IsNullOrWhiteSpace(rawKey))
            {
                return false;
            }

            await _container.DeleteBlobIfExistsAsync(rawKey, cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("Deleted blob (raw key) {Key}", rawKey);
            return true;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error deleting raw key {Key}", rawKey);
            return false;
        }
#pragma warning restore CA1031
    }

    public async Task<bool> StoreRawKeyAsync(string rawKey, Stream stream, string contentType, CancellationToken ct = default)
    {
        if (string.IsNullOrWhiteSpace(rawKey) || stream is null)
        {
            return false;
        }

        MemoryStream? buffer = null;
        try
        {
            var (uploadStream, _, owned) = await BufferIfNeededAsync(stream, ct).ConfigureAwait(false);
            buffer = owned;

            await EnsureContainerAsync(ct).ConfigureAwait(false);
            await _container.GetBlobClient(rawKey)
                .UploadAsync(uploadStream, new BlobUploadOptions { HttpHeaders = new BlobHttpHeaders { ContentType = contentType } }, ct)
                .ConfigureAwait(false);

            _logger.LogInformation("Stored blob (raw key) {Key}", rawKey);
            return true;
        }
        catch (OperationCanceledException)
        {
            throw;
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogWarning(ex, "Error storing raw key {Key}", rawKey);
            return false;
        }
#pragma warning restore CA1031
        finally
        {
            if (buffer is not null)
            {
                await buffer.DisposeAsync().ConfigureAwait(false);
            }
        }
    }

    public async Task<Stream?> RetrieveRawKeyAsync(string rawKey, CancellationToken ct = default)
    {
        try
        {
            return await DownloadAsync(rawKey, ct).ConfigureAwait(false);
        }
#pragma warning disable CA1031
        catch (Exception ex)
        {
            _logger.LogError(ex, "Error retrieving raw key {Key}", rawKey);
            return null;
        }
#pragma warning restore CA1031
    }

    // ---------------------------------------------------------------------------------------------

    private static string GetKey(string fileId, BlobCategory category, string resourceKey, string sanitizedFileName)
        => $"{category.ToS3Folder()}/{resourceKey}/{fileId}_{sanitizedFileName}";

    private static string GetPrefix(string fileId, BlobCategory category, string resourceKey)
        => $"{category.ToS3Folder()}/{resourceKey}/{fileId}_";

    private static string SanitizeFileName(string fileName)
        => StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file");

    private async Task<string?> TryResolveKeyAsync(string fileId, BlobCategory category, string resourceKey, CancellationToken ct)
    {
        await foreach (var item in _container.GetBlobsAsync(prefix: GetPrefix(fileId, category, resourceKey), cancellationToken: ct).ConfigureAwait(false))
        {
            return item.Name;
        }

        return null;
    }

    private async Task<Stream?> DownloadAsync(string key, CancellationToken ct)
    {
        try
        {
            var response = await _container.GetBlobClient(key).DownloadStreamingAsync(cancellationToken: ct).ConfigureAwait(false);
            _logger.LogInformation("Retrieved blob {Key}", key);
            return response.Value.Content; // il chiamante dispone lo stream
        }
        catch (RequestFailedException ex) when (ex.Status == 404)
        {
            _logger.LogWarning(ex, "Blob not found {Key}", key);
            return null;
        }
    }

    private async Task EnsureContainerAsync(CancellationToken ct)
    {
        if (_containerEnsured)
        {
            return;
        }

        await _container.CreateIfNotExistsAsync(cancellationToken: ct).ConfigureAwait(false);
        _containerEnsured = true;
    }

    private static async Task<(Stream Stream, long Length, MemoryStream? Owned)> BufferIfNeededAsync(Stream stream, CancellationToken ct)
    {
        if (stream.CanSeek)
        {
            return (stream, stream.Length, null);
        }

        var buffer = new MemoryStream();
        await stream.CopyToAsync(buffer, ct).ConfigureAwait(false);
        buffer.Position = 0;
        return (buffer, buffer.Length, buffer);
    }

    private async Task<string> BuildReadSasAsync(string key, int expirySeconds)
    {
        var blob = _container.GetBlobClient(key);
        var expiresOn = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
        Uri signed;

        if (blob.CanGenerateSasUri)
        {
            // Chiave condivisa (connection string: Azurite o account con chiave).
            signed = blob.GenerateSasUri(BlobSasPermissions.Read, expiresOn);
        }
        else
        {
            // Identità Entra (DefaultAzureCredential): SAS con chiave di delega utente.
            var delegationKey = await _serviceClient
                .GetUserDelegationKeyAsync(DateTimeOffset.UtcNow.AddMinutes(-5), expiresOn, CancellationToken.None)
                .ConfigureAwait(false);
            var sas = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn)
            {
                BlobContainerName = _container.Name,
                BlobName = key,
                Resource = "b",
            };
            var uriBuilder = new BlobUriBuilder(blob.Uri)
            {
                Sas = sas.ToSasQueryParameters(delegationKey.Value, _serviceClient.AccountName),
            };
            signed = uriBuilder.ToUri();
        }

        _logger.LogInformation("Signed read URL for {Key} (expires in {Expiry}s)", key, expirySeconds);
        return AlignWithPublicEndpoint(signed).ToString();
    }

    private Uri AlignWithPublicEndpoint(Uri signed)
    {
        if (string.IsNullOrWhiteSpace(_options.PublicBlobEndpoint)
            || !Uri.TryCreate(_options.PublicBlobEndpoint, UriKind.Absolute, out var publicEndpoint))
        {
            return signed;
        }

        return new UriBuilder(signed) { Scheme = publicEndpoint.Scheme, Host = publicEndpoint.Host, Port = publicEndpoint.Port }.Uri;
    }
}
```

- [ ] **Step 10: Il ramo `azureblob` nella factory**

In `BlobStorageServiceFactory.Create`, nello switch, prima di `"s3"`:
```csharp
            "azureblob" => CreateAzureBlobStorageService(serviceProvider, config),
```
Nuovo metodo privato (dopo `CreateS3StorageService`):
```csharp
    private static IBlobStorageService CreateAzureBlobStorageService(IServiceProvider serviceProvider, IConfiguration config)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<AzureBlobStorageService>>();
        var options = new AzureBlobStorageOptions
        {
            ConnectionString = config["AZURE_STORAGE_CONNECTION_STRING"],
            AccountUrl = config["AZURE_STORAGE_ACCOUNT_URL"],
            ContainerName = config["AZURE_STORAGE_CONTAINER"] is { Length: > 0 } name ? name : "meepleai-uploads",
            PublicBlobEndpoint = config["AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT"],
            SasExpirySeconds = int.TryParse(config["AZURE_STORAGE_SAS_EXPIRY"], CultureInfo.InvariantCulture, out var expiry) ? expiry : 3600,
        };

        var hasConnectionString = !string.IsNullOrWhiteSpace(options.ConnectionString);
        var hasAccountUrl = !string.IsNullOrWhiteSpace(options.AccountUrl);
        if (!hasConnectionString && !hasAccountUrl)
        {
            throw new InvalidOperationException(
                "AZURE_STORAGE_CONNECTION_STRING or AZURE_STORAGE_ACCOUNT_URL is required when STORAGE_PROVIDER=azureblob");
        }

        var clientOptions = new BlobClientOptions { Transport = Api.Infrastructure.Azure.AzureSdkHttpClients.CreateTransport(serviceProvider) };
        var serviceClient = hasConnectionString
            ? new BlobServiceClient(options.ConnectionString, clientOptions)
            : new BlobServiceClient(new Uri(options.AccountUrl!, UriKind.Absolute), new Azure.Identity.DefaultAzureCredential(), clientOptions);
        var container = serviceClient.GetBlobContainerClient(options.ContainerName);

        logger.LogInformation(
            "Initialized Azure Blob storage: endpoint={Endpoint}, container={Container}, publicEndpoint={PublicEndpoint}",
            serviceClient.Uri, options.ContainerName, options.PublicBlobEndpoint ?? serviceClient.Uri.ToString());

        return new AzureBlobStorageService(serviceClient, container, options, logger);
    }
```
Aggiungere `using Azure.Storage.Blobs;` in testa al file.

- [ ] **Step 11: Test della factory** `apps/api/tests/Api.Tests/Services/Pdf/BlobStorageServiceFactoryAzureTests.cs`

```csharp
using Api.Infrastructure.Azure;
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class BlobStorageServiceFactoryAzureTests
{
    private static ServiceProvider Build(params (string Key, string? Value)[] pairs)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddSingleton<IConfiguration>(config);
        services.AddLogging();
        services.AddAzureSdkHttpClients(config);
        return services.BuildServiceProvider();
    }

    [Fact]
    public void Create_AzureBlobWithoutEndpoint_Throws()
    {
        using var sp = Build(("STORAGE_PROVIDER", "azureblob"), ("MEEPLEAI_PROFILE", "azure-local"));

        var act = () => BlobStorageServiceFactory.Create(sp);

        act.Should().Throw<InvalidOperationException>()
            .WithMessage("*AZURE_STORAGE_CONNECTION_STRING*AZURE_STORAGE_ACCOUNT_URL*");
    }

    [Fact]
    public void Create_AzureBlobWithConnectionString_ReturnsAzureServiceWithOptions()
    {
        using var sp = Build(
            ("STORAGE_PROVIDER", "AzureBlob"),
            ("MEEPLEAI_PROFILE", "azure-local"),
            ("AZURE_STORAGE_CONNECTION_STRING", "UseDevelopmentStorage=true"),
            ("AZURE_STORAGE_CONTAINER", "uploads-test"),
            ("AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT", "http://localhost:20000"),
            ("AZURE_STORAGE_SAS_EXPIRY", "120"));

        var service = BlobStorageServiceFactory.Create(sp);

        var azure = service.Should().BeOfType<AzureBlobStorageService>().Subject;
        azure.Options.ContainerName.Should().Be("uploads-test");
        azure.Options.PublicBlobEndpoint.Should().Be("http://localhost:20000");
        azure.Options.SasExpirySeconds.Should().Be(120);
    }

    [Fact]
    public void Create_UnknownProvider_StillFallsBackToLocal()
    {
        using var sp = Build(("STORAGE_PROVIDER", "nope"));

        BlobStorageServiceFactory.Create(sp).Should().BeOfType<BlobStorageService>();
    }
}
```

- [ ] **Step 12: Eseguire tutti i test del task**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureBlobStorageServiceTests|FullyQualifiedName~BlobStorageServiceFactoryAzureTests|FullyQualifiedName~AzureSdkHttpClientsTests|FullyQualifiedName~EgressHttpClientPinArchitectureTests"`
Expected: tutti verdi. Se un `Setup` Moq su un overload dell'SDK non compila, allineare la firma a quella **virtuale** del pacchetto 12.30.1 (`GetBlobsAsync(BlobTraits, BlobStates, string, CancellationToken)`, `DeleteBlobIfExistsAsync(string, DeleteSnapshotsOption, BlobRequestConditions, CancellationToken)`, `UploadAsync(Stream, BlobUploadOptions, CancellationToken)`, `DownloadStreamingAsync(BlobDownloadOptions, CancellationToken)`).

- [ ] **Step 13: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Azure apps/api/src/Api/Services/Pdf apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Egress.cs apps/api/src/Api/Program.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Architecture/EgressHttpClientPinArchitectureTests.cs apps/api/tests/Api.Tests/Services/Pdf apps/api/tests/Api.Tests/Infrastructure/Azure
git commit -m "feat(storage): provider azureblob su Azure.Storage.Blobs" -m "AzureBlobStorageService con lo stesso layout di chiave di S3, SAS con chiave condivisa o delega utente, riscrittura dell'host pubblico. Client AzureSdk pinnato e AzureSdkLocal esente, scelti dal profilo." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Test di integrazione su Azurite (Testcontainers)

**Files:**
- Modify: `apps/api/tests/Api.Tests/Api.Tests.csproj` (PackageReference)
- Modify: `apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs` (costanti, dopo la regione MinIO)
- Test: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/AzureBlobStorageIntegrationTests.cs`

**Interfaces:**
- Consumes: `AzureBlobStorageService`, `AzureBlobStorageOptions` (Task 2).
- Produces: `TestcontainersConfiguration.AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.37.0"`, `AzuriteBlobPort = 10000`.

- [ ] **Step 1: Pacchetto e costanti**

Run (da `apps/api/tests/Api.Tests`): `dotnet add package Testcontainers.Azurite --version 4.14.0`

In `TestcontainersConfiguration.cs`, dopo `MinioTestBucket`:
```csharp
    /// <summary>Azurite (spec azure-local): servizio L3 — la sua assenza è un salto PREVISTO, non un rosso.</summary>
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.37.0";
    public const int AzuriteBlobPort = 10000;
    public const string AzuriteTestContainer = "meepleai-uploads-test";
```

- [ ] **Step 2: Scrivere il test**

```csharp
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.Azurite;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>
/// Ciclo completo Store → Exists → Retrieve → SAS → Delete contro Azurite vero. Gli emulatori sono L3
/// (spec 2026-10-02 dei livelli di dipendenza): senza Docker il test SALTA con il comando per abilitarlo.
/// </summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Feature", "AzureBlobStorage")]
public sealed class AzureBlobStorageIntegrationTests : IAsyncLifetime
{
    private AzuriteContainer? _azurite;
    private string? _unavailableReason;
    private AzureBlobStorageService _sut = null!;
    private string _publicEndpoint = null!;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _azurite = new AzuriteBuilder().WithImage(TestcontainersConfiguration.AzuriteImage).Build();
            await _azurite.StartAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _unavailableReason = ex.Message;
            return;
        }

        var connectionString = _azurite.GetConnectionString();
        var serviceClient = new BlobServiceClient(connectionString);
        var container = serviceClient.GetBlobContainerClient(TestcontainersConfiguration.AzuriteTestContainer);
        _publicEndpoint = $"http://127.0.0.1:{_azurite.GetMappedPublicPort(TestcontainersConfiguration.AzuriteBlobPort)}";

        _sut = new AzureBlobStorageService(
            serviceClient,
            container,
            new AzureBlobStorageOptions
            {
                ConnectionString = connectionString,
                ContainerName = TestcontainersConfiguration.AzuriteTestContainer,
                PublicBlobEndpoint = _publicEndpoint,
                SasExpirySeconds = 300,
            },
            Mock.Of<ILogger<AzureBlobStorageService>>());
    }

    public async ValueTask DisposeAsync()
    {
        if (_azurite is not null)
        {
            await _azurite.DisposeAsync();
        }
    }

    private void SkipIfUnavailable()
    {
        if (_unavailableReason is not null)
        {
            Assert.Skip($"PREVISTO: Azurite non avviato ({_unavailableReason}). Serve Docker; in locale: cd infra && make dev-azure-local");
        }
    }

    [Fact]
    public async Task StoreExistsRetrieveSignDelete_RoundTrip()
    {
        SkipIfUnavailable();
        var payload = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        using var content = new MemoryStream(payload);

        var stored = await _sut.StoreAsync(content, "rulebook.pdf", BlobCategory.Pdf, "game-rt");
        stored.Success.Should().BeTrue(stored.ErrorMessage);

        (await _sut.ExistsAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeTrue();

        await using (var retrieved = await _sut.RetrieveAsync(stored.FileId!, BlobCategory.Pdf, "game-rt"))
        {
            retrieved.Should().NotBeNull();
            using var copy = new MemoryStream();
            await retrieved!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(payload);
        }

        var url = await _sut.GetPresignedDownloadUrlAsync(stored.FileId!, BlobCategory.Pdf, "game-rt");
        url.Should().NotBeNull().And.StartWith(_publicEndpoint);
        using (var http = new HttpClient())
        {
            var bytes = await http.GetByteArrayAsync(new Uri(url!));
            bytes.Should().Equal(payload);
        }

        (await _sut.DeleteAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeTrue();
        (await _sut.ExistsAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeFalse();
    }

    [Fact]
    public async Task RawKey_StoreSignRetrieveDelete_RoundTrip()
    {
        SkipIfUnavailable();
        var payload = new byte[] { 1, 2, 3, 4 };
        using var content = new MemoryStream(payload);

        (await _sut.StoreRawKeyAsync("covers/game-rt/cover.png", content, "image/png")).Should().BeTrue();

        var url = await _sut.GetPresignedUrlForRawKeyAsync("covers/game-rt/cover.png", 60);
        url.Should().NotBeNull().And.Contain("covers/game-rt/cover.png?");

        await using (var retrieved = await _sut.RetrieveRawKeyAsync("covers/game-rt/cover.png"))
        {
            using var copy = new MemoryStream();
            await retrieved!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(payload);
        }

        (await _sut.DeleteRawKeyAsync("covers/game-rt/cover.png")).Should().BeTrue();
        (await _sut.GetPresignedUrlForRawKeyAsync("covers/game-rt/cover.png")).Should().BeNull();
    }
}
```

- [ ] **Step 3: Eseguire**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureBlobStorageIntegrationTests"`
Expected: 2 superati con Docker attivo (prima esecuzione: pull dell'immagine). Senza Docker: 2 salti con prefisso `PREVISTO:`.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/Api.Tests.csproj apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/AzureBlobStorageIntegrationTests.cs
git commit -m "test(storage): round-trip del provider azureblob su Azurite" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 4: Sorgenti `IConfiguration` da App Configuration e Key Vault

**Files:**
- Create: `apps/api/src/Api/Infrastructure/Azure/StaticTokenCredential.cs`
- Create: `apps/api/src/Api/Infrastructure/Azure/EnvironmentStyleKeyVaultSecretManager.cs`
- Create: `apps/api/src/Api/Infrastructure/Azure/AzureConfigurationSources.cs`
- Modify: `apps/api/src/Api/Program.cs` (subito dopo `var builder = WebApplication.CreateBuilder(args);`, PRIMA del blocco `SecretLoader`)
- Modify: `apps/api/src/Api/Api.csproj`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Azure/EnvironmentStyleKeyVaultSecretManagerTests.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Azure/AzureConfigurationSourcesTests.cs`
- Test: `apps/api/tests/Api.Tests/Integration/Infrastructure/AppConfigurationEmulatorIntegrationTests.cs`

**Interfaces:**
- Consumes: `AzureSdkHttpClients.AllowInsecureTls(IConfiguration)` (Task 2).
- Produces: `AzureConfigurationSources.AddAzureConfigurationSources(this ConfigurationManager configuration)`; chiavi `AZURE_APPCONFIG_ENDPOINT`, `AZURE_APPCONFIG_ANONYMOUS`, `AZURE_KEYVAULT_URI`, `AZURE_KEYVAULT_ANONYMOUS`; `StaticTokenCredential`; `EnvironmentStyleKeyVaultSecretManager.GetKey`.

- [ ] **Step 1: Pacchetti**

Run (da `apps/api/src/Api`):
```bash
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore --version 8.6.0
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets --version 1.5.2
```

- [ ] **Step 2: Test del mapping dei nomi dei secret**

```csharp
using Api.Infrastructure.Azure;
using Api.Tests.Constants;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.Azure;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class EnvironmentStyleKeyVaultSecretManagerTests
{
    private static KeyVaultSecret Secret(string name) =>
        SecretModelFactory.KeyVaultSecret(SecretModelFactory.SecretProperties(new Uri($"https://vault.local/secrets/{name}"), name: name), "v");

    [Theory]
    [InlineData("OPENROUTER-API-KEY", "OPENROUTER_API_KEY")]
    [InlineData("Embedding--Provider", "Embedding:Provider")]
    [InlineData("Seq--Api-Key", "Seq:Api_Key")]
    [InlineData("plain", "plain")]
    public void GetKey_MapsDoubleDashToSectionAndDashToUnderscore(string secretName, string expectedKey)
    {
        var sut = new EnvironmentStyleKeyVaultSecretManager();

        sut.GetKey(Secret(secretName)).Should().Be(expectedKey);
    }
}
```

- [ ] **Step 3: Test del gating delle sorgenti**

```csharp
using Api.Infrastructure.Azure;
using Api.Tests.Constants;
using Azure.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Infrastructure.Azure;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class AzureConfigurationSourcesTests
{
    private static ConfigurationManager Manager(params (string Key, string? Value)[] pairs)
    {
        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)));
        return manager;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAzureConfigurationSources_NoEndpoints_AddsNothing(string? value)
    {
        var manager = Manager(("AZURE_APPCONFIG_ENDPOINT", value), ("AZURE_KEYVAULT_URI", value));
        var before = manager.Sources.Count;

        manager.AddAzureConfigurationSources();

        manager.Sources.Count.Should().Be(before);
    }

    [Fact]
    public void AddAzureConfigurationSources_AppConfigEndpoint_AddsOneSourceWithoutConnecting()
    {
        var manager = Manager(("AZURE_APPCONFIG_ENDPOINT", "http://127.0.0.1:9"), ("AZURE_APPCONFIG_ANONYMOUS", "true"));
        var before = manager.Sources.Count;

        // Il provider App Configuration si connette solo al Build/Load: aggiungere la sorgente non apre socket.
        manager.AddAzureConfigurationSources();

        manager.Sources.Count.Should().Be(before + 1);
    }

    [Fact]
    public void AddAzureConfigurationSources_InvalidEndpoint_Throws()
    {
        var manager = Manager(("AZURE_APPCONFIG_ENDPOINT", "not a url"));

        var act = () => manager.AddAzureConfigurationSources();

        act.Should().Throw<InvalidOperationException>().WithMessage("*AZURE_APPCONFIG_ENDPOINT*");
    }

    [Fact]
    public void StaticTokenCredential_ReturnsNonExpiringToken()
    {
        var credential = new StaticTokenCredential("anonymous");

        var token = credential.GetToken(new TokenRequestContext(["https://x/.default"]), CancellationToken.None);

        token.Token.Should().Be("anonymous");
        token.ExpiresOn.Should().BeAfter(DateTimeOffset.UtcNow.AddYears(1));
    }
}
```

- [ ] **Step 4: Eseguire per vedere il fallimento di compilazione**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~EnvironmentStyleKeyVaultSecretManagerTests|FullyQualifiedName~AzureConfigurationSourcesTests"`
Expected: errori di compilazione sui tre tipi mancanti.

- [ ] **Step 5: Implementare i tre tipi**

`StaticTokenCredential.cs`:
```csharp
using Azure.Core;

namespace Api.Infrastructure.Azure;

/// <summary>
/// Credenziale fissa per gli emulatori che accettano l'autenticazione anonima (App Configuration
/// emulator con <c>Tenant:AnonymousAuthEnabled</c>, Key Vault emulator): gli SDK pretendono un
/// <see cref="TokenCredential"/>, l'emulatore ignora il bearer. Mai usata nel profilo Azure vero.
/// </summary>
internal sealed class StaticTokenCredential : TokenCredential
{
    private readonly string _token;

    public StaticTokenCredential(string token)
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(token);
        _token = token;
    }

    public override AccessToken GetToken(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new(_token, DateTimeOffset.MaxValue);

    public override ValueTask<AccessToken> GetTokenAsync(TokenRequestContext requestContext, CancellationToken cancellationToken) =>
        new(GetToken(requestContext, cancellationToken));
}
```

`EnvironmentStyleKeyVaultSecretManager.cs`:
```csharp
using Azure.Extensions.AspNetCore.Configuration.Secrets;
using Azure.Security.KeyVault.Secrets;

namespace Api.Infrastructure.Azure;

/// <summary>
/// Convenzione dei nomi (spec azure-local §4.2): i secret portano i nomi delle variabili d'ambiente
/// che il repo già usa, con <c>-</c> al posto di <c>_</c> perché Key Vault non ammette l'underscore;
/// <c>--</c> resta il separatore di sezione come nel manager di default. Esempi:
/// <c>OPENROUTER-API-KEY</c> → <c>OPENROUTER_API_KEY</c>, <c>Embedding--Provider</c> → <c>Embedding:Provider</c>.
/// </summary>
internal sealed class EnvironmentStyleKeyVaultSecretManager : KeyVaultSecretManager
{
    public override string GetKey(KeyVaultSecret secret) =>
        secret.Name.Replace("--", ":", StringComparison.Ordinal).Replace('-', '_');
}
```

`AzureConfigurationSources.cs`:
```csharp
using Azure.Core;
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Api.Infrastructure.Azure;

/// <summary>
/// Sorgenti <see cref="IConfiguration"/> Azure (spec azure-local §4.2). Girano PRIMA del container DI,
/// quindi non passano dal client <c>AzureSdk</c> pinnato: gli endpoint vengono da configurazione statica
/// (mai da input utente), la stessa condizione con cui il gate di egress esenta OpenRouter o Resend.
/// Con le variabili assenti o vuote non viene aggiunta alcuna sorgente.
/// </summary>
internal static class AzureConfigurationSources
{
    public const string AppConfigEndpointKey = "AZURE_APPCONFIG_ENDPOINT";
    public const string AppConfigAnonymousKey = "AZURE_APPCONFIG_ANONYMOUS";
    public const string KeyVaultUriKey = "AZURE_KEYVAULT_URI";
    public const string KeyVaultAnonymousKey = "AZURE_KEYVAULT_ANONYMOUS";
    private const string AnonymousToken = "anonymous";

    public static ConfigurationManager AddAzureConfigurationSources(this ConfigurationManager configuration)
    {
        var appConfigEndpoint = configuration[AppConfigEndpointKey];
        if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
        {
            var endpoint = ParseAbsoluteUri(appConfigEndpoint, AppConfigEndpointKey);
            var credential = CredentialFor(configuration, AppConfigAnonymousKey);
            configuration.AddAzureAppConfiguration(options => options
                .Connect(endpoint, credential)
                .Select(KeyFilter.Any, LabelFilter.Null));
        }

        var keyVaultUri = configuration[KeyVaultUriKey];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            var vault = ParseAbsoluteUri(keyVaultUri, KeyVaultUriKey);
            var clientOptions = new SecretClientOptions { DisableChallengeResourceVerification = true };
            if (AzureSdkHttpClients.AllowInsecureTls(configuration))
            {
                // Solo profilo azure-local + opt-in esplicito: certificato self-signed del Key Vault emulator.
                var handler = new SocketsHttpHandler();
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
                clientOptions.Transport = new HttpClientTransport(new HttpClient(handler));
            }

            var client = new SecretClient(vault, CredentialFor(configuration, KeyVaultAnonymousKey), clientOptions);
            configuration.AddAzureKeyVault(client, new EnvironmentStyleKeyVaultSecretManager());
        }

        return configuration;
    }

    private static TokenCredential CredentialFor(IConfiguration configuration, string anonymousKey) =>
        string.Equals(configuration[anonymousKey], "true", StringComparison.OrdinalIgnoreCase)
            ? new StaticTokenCredential(AnonymousToken)
            : new DefaultAzureCredential();

    private static Uri ParseAbsoluteUri(string value, string key)
    {
        if (!Uri.TryCreate(value.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{key} must be an absolute URL, got '{value}'");
        }

        return uri;
    }
}
```

In `Program.cs`, subito dopo `var builder = WebApplication.CreateBuilder(args);`:
```csharp
// Spec azure-local §4.2: App Configuration e Key Vault come sorgenti IConfiguration, attive SOLO con
// AZURE_APPCONFIG_ENDPOINT / AZURE_KEYVAULT_URI valorizzate. Vanno prima del SecretLoader, che aggiunge
// soltanto le chiavi ancora assenti.
builder.Configuration.AddAzureConfigurationSources();
```
(`using Api.Infrastructure.Azure;` è già presente da Task 2.)

- [ ] **Step 6: Eseguire i test unit**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~EnvironmentStyleKeyVaultSecretManagerTests|FullyQualifiedName~AzureConfigurationSourcesTests"`
Expected: tutti verdi. Se `SecretModelFactory.SecretProperties` ha un'altra firma nella 4.x di `Azure.Security.KeyVault.Secrets` (dipendenza transitiva), costruire il secret con `new KeyVaultSecret(name, "v")`: `Name` è letto dal costruttore.

- [ ] **Step 7: Test di integrazione contro l'App Configuration emulator**

```csharp
using System.Net.Http.Json;
using System.Text;
using Api.Infrastructure.Azure;
using Api.Tests.Constants;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Feature", "AzureAppConfiguration")]
public sealed class AppConfigurationEmulatorIntegrationTests : IAsyncLifetime
{
    private const string Image = "mcr.microsoft.com/azure-app-configuration/app-configuration-emulator:1.2.0";
    private const int Port = 8483;
    private IContainer? _emulator;
    private string? _unavailableReason;
    private string _endpoint = null!;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _emulator = new ContainerBuilder()
                .WithImage(Image)
                .WithEnvironment("Tenant__AnonymousAuthEnabled", "true")
                .WithEnvironment("Tenant__HmacSha256Enabled", "false")
                .WithEnvironment("Authentication__Anonymous__AnonymousUserRole", "Owner")
                .WithEnvironment("Hosting__IPAddress", "0.0.0.0")
                .WithPortBinding(Port, true)
                .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(Port)))
                .Build();
            await _emulator.StartAsync();
            _endpoint = $"http://127.0.0.1:{_emulator.GetMappedPublicPort(Port)}";
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _unavailableReason = ex.Message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_emulator is not null)
        {
            await _emulator.DisposeAsync();
        }
    }

    [Fact]
    public async Task ConfigurationManager_ReadsSeededKeyFromEmulator()
    {
        if (_unavailableReason is not null)
        {
            Assert.Skip($"PREVISTO: App Configuration emulator non avviato ({_unavailableReason}). Serve Docker; in locale: cd infra && make dev-azure-local");
        }

        using var http = new HttpClient();
        using var body = new StringContent("{\"value\":\"from-emulator\"}", Encoding.UTF8, "application/vnd.microsoft.appconfig.kv+json");
        var put = await http.PutAsync(new Uri($"{_endpoint}/kv/{Uri.EscapeDataString("Azure:Profile:Greeting")}?api-version=2023-11-01"), body);
        put.EnsureSuccessStatusCode();

        var manager = new ConfigurationManager();
        manager.AddInMemoryCollection(new Dictionary<string, string?>
        {
            [AzureConfigurationSources.AppConfigEndpointKey] = _endpoint,
            [AzureConfigurationSources.AppConfigAnonymousKey] = "true",
        });
        manager.AddAzureConfigurationSources();

        manager["Azure:Profile:Greeting"].Should().Be("from-emulator");
    }
}
```

- [ ] **Step 8: Eseguire il test di integrazione**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AppConfigurationEmulatorIntegrationTests"`
Expected: 1 superato. Se il provider risponde 401 contro l'emulatore (il bearer `anonymous` non viene ignorato), riportare BLOCKED con il corpo della risposta: il controller decide tra HMAC e un'altra via; non "aggiustare" il test.

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/Infrastructure/Azure apps/api/src/Api/Program.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Infrastructure/Azure apps/api/tests/Api.Tests/Integration/Infrastructure/AppConfigurationEmulatorIntegrationTests.cs
git commit -m "feat(config): App Configuration e Key Vault come sorgenti IConfiguration" -m "Attive solo con AZURE_APPCONFIG_ENDPOINT / AZURE_KEYVAULT_URI; credenziale fissa per gli emulatori anonimi; nomi dei secret in stile variabile d'ambiente." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: `IIntegrationEventPublisher`, publisher Service Bus ed evento `PdfUploaded` v1

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Application/Interfaces/IIntegrationEventPublisher.cs`
- Create: `apps/api/src/Api/Infrastructure/Messaging/NullIntegrationEventPublisher.cs`
- Create: `apps/api/src/Api/Infrastructure/Messaging/ServiceBusIntegrationEventPublisher.cs`
- Create: `apps/api/src/Api/Infrastructure/Messaging/MessagingServiceExtensions.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/IntegrationEvents/PdfUploadedIntegrationEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PdfUploadedIntegrationEventRelay.cs`
- Create: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.PdfIntegrationEvents.cs`
- Modify: `apps/api/src/Api/Program.cs` (dopo `AddAzureSdkHttpClients`)
- Modify: `apps/api/src/Api/Api.csproj`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Messaging/ServiceBusIntegrationEventPublisherTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/PdfUploadedIntegrationEventRelayTests.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Messaging/MessagingServiceExtensionsTests.cs`

**Interfaces:**
- Consumes: `IntegrationEventBase(string sourceContext)` (`SharedKernel/Domain/Events`), `PdfStateChangedEvent` (`PdfDocumentId`, `PreviousState`, `NewState`), `PdfProcessingState.Pending/Uploading`, `IPdfDocumentRepository.GetByIdAsync(Guid, CancellationToken)`, `PdfDocument.GameId/SharedGameId/FilePath`, `INotificationHandler<T>` (MediatR).
- Produces: `IIntegrationEventPublisher.PublishAsync<TEvent>(TEvent, CancellationToken) where TEvent : IntegrationEventBase`; `PdfUploadedIntegrationEvent(Guid pdfDocumentId, Guid gameId, Guid? sharedGameId, string filePath)` con `ContractVersion = "v1"`, `CurrentContractVersion`; JSON camelCase con chiavi `eventId, occurredAt, sourceContext, contractVersion, pdfDocumentId, gameId, sharedGameId, filePath`; `ServiceBusIntegrationEventPublisher.JsonOptions` (static, usato dal test di contratto in Task 8); `MeepleAiMetrics.RecordPdfUploadedEvent(string outcome)` con `PdfUploadedEventOutcomes.{Published, PublishFailed, Received, Indexed, Noop, NotFound, Rejected, Failed}`; chiavi `AZURE_SERVICEBUS_CONNECTION_STRING`, `AZURE_SERVICEBUS_QUEUE` (default `pdf-uploaded`).

- [ ] **Step 1: Pacchetto**

Run (da `apps/api/src/Api`): `dotnet add package Azure.Messaging.ServiceBus --version 7.21.0`

- [ ] **Step 2: Test del publisher**

```csharp
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Infrastructure.Messaging;
using Api.Tests.Constants;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Infrastructure.Messaging;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class ServiceBusIntegrationEventPublisherTests
{
    [Fact]
    public async Task PublishAsync_SendsJsonEnvelopeWithContractMetadata()
    {
        var sender = new Mock<ServiceBusSender>();
        ServiceBusMessage? sent = null;
        sender.Setup(s => s.SendMessageAsync(It.IsAny<ServiceBusMessage>(), It.IsAny<CancellationToken>()))
            .Callback<ServiceBusMessage, CancellationToken>((m, _) => sent = m)
            .Returns(Task.CompletedTask);
        var sut = new ServiceBusIntegrationEventPublisher(sender.Object, Mock.Of<ILogger<ServiceBusIntegrationEventPublisher>>());
        var pdfId = Guid.NewGuid();
        var gameId = Guid.NewGuid();
        var evt = new PdfUploadedIntegrationEvent(pdfId, gameId, null, "pdfs/g/f_a.pdf");

        await sut.PublishAsync(evt, CancellationToken.None);

        sent.Should().NotBeNull();
        sent!.ContentType.Should().Be("application/json");
        sent.MessageId.Should().Be(evt.EventId.ToString());
        sent.Subject.Should().Be(nameof(PdfUploadedIntegrationEvent));
        sent.ApplicationProperties["contractVersion"].Should().Be("v1");
        using var doc = JsonDocument.Parse(sent.Body.ToString());
        doc.RootElement.GetProperty("pdfDocumentId").GetGuid().Should().Be(pdfId);
        doc.RootElement.GetProperty("gameId").GetGuid().Should().Be(gameId);
        doc.RootElement.GetProperty("sharedGameId").ValueKind.Should().Be(JsonValueKind.Null);
        doc.RootElement.GetProperty("filePath").GetString().Should().Be("pdfs/g/f_a.pdf");
        doc.RootElement.GetProperty("contractVersion").GetString().Should().Be("v1");
        doc.RootElement.GetProperty("sourceContext").GetString().Should().Be("DocumentProcessing");
        doc.RootElement.TryGetProperty("occurredAt", out _).Should().BeTrue();
        doc.RootElement.TryGetProperty("eventId", out _).Should().BeTrue();
    }
}
```

- [ ] **Step 3: Test del relay**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.SharedKernel.Application.Interfaces;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfUploadedIntegrationEventRelayTests
{
    private readonly Mock<IIntegrationEventPublisher> _publisher = new();
    private readonly Mock<IPdfDocumentRepository> _repository = new();
    private readonly PdfUploadedIntegrationEventRelay _sut;

    public PdfUploadedIntegrationEventRelayTests()
    {
        _sut = new PdfUploadedIntegrationEventRelay(_publisher.Object, _repository.Object, Mock.Of<ILogger<PdfUploadedIntegrationEventRelay>>());
    }

    private PdfDocument SeedDocument(Guid id)
    {
        // Builder di test esistente (tests/.../DocumentProcessing/TestHelpers/PdfDocumentBuilder.cs).
        var document = new PdfDocumentBuilder()
            .WithId(id)
            .WithFilePath($"pdfs/{Guid.NewGuid():N}/{Guid.NewGuid():N}_rulebook.pdf")
            .Build();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        return document;
    }

    [Fact]
    public async Task Handle_PendingToUploading_PublishesEventWithDocumentData()
    {
        var pdfId = Guid.NewGuid();
        var document = SeedDocument(pdfId);
        PdfUploadedIntegrationEvent? published = null;
        _publisher.Setup(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .Callback<PdfUploadedIntegrationEvent, CancellationToken>((e, _) => published = e)
            .Returns(Task.CompletedTask);

        await _sut.Handle(new PdfStateChangedEvent(pdfId, PdfProcessingState.Pending, PdfProcessingState.Uploading, Guid.NewGuid()), CancellationToken.None);

        published.Should().NotBeNull();
        published!.PdfDocumentId.Should().Be(pdfId);
        published.GameId.Should().Be(document.GameId);
        published.FilePath.Should().Be(document.FilePath);
        published.ContractVersion.Should().Be("v1");
    }

    [Fact]
    public async Task Handle_OtherTransition_DoesNotPublish()
    {
        await _sut.Handle(new PdfStateChangedEvent(Guid.NewGuid(), PdfProcessingState.Uploading, PdfProcessingState.Extracting, Guid.NewGuid()), CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DocumentMissing_DoesNotPublishAndDoesNotThrow()
    {
        var pdfId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync((PdfDocument?)null);

        var act = () => _sut.Handle(new PdfStateChangedEvent(pdfId, PdfProcessingState.Pending, PdfProcessingState.Uploading, Guid.NewGuid()), CancellationToken.None);

        await act.Should().NotThrowAsync();
        _publisher.Verify(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_PublisherThrows_SwallowsAndCountsFailure()
    {
        var pdfId = Guid.NewGuid();
        SeedDocument(pdfId);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));

        var act = () => _sut.Handle(new PdfStateChangedEvent(pdfId, PdfProcessingState.Pending, PdfProcessingState.Uploading, Guid.NewGuid()), CancellationToken.None);

        // L'upload NON deve fallire perché il bus è giù (Review Focus 5): il relay assorbe e conta.
        await act.Should().NotThrowAsync();
    }
}
```

- [ ] **Step 4: Test della registrazione DI**

```csharp
using Api.Infrastructure.Messaging;
using Api.SharedKernel.Application.Interfaces;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure.Messaging;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class MessagingServiceExtensionsTests
{
    private static ServiceProvider Build(string? connectionString)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["AZURE_SERVICEBUS_CONNECTION_STRING"] = connectionString })
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationEventPublishing(config);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoConnectionString_RegistersNullPublisher(string? value)
    {
        using var sp = Build(value);

        sp.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<NullIntegrationEventPublisher>();
    }

    [Fact]
    public void ConnectionString_RegistersServiceBusPublisher()
    {
        using var sp = Build("Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;");

        // La connessione AMQP è lazy: risolvere il publisher non apre socket.
        sp.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<ServiceBusIntegrationEventPublisher>();
    }
}
```

- [ ] **Step 5: Eseguire per vedere i fallimenti di compilazione**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServiceBusIntegrationEventPublisherTests|FullyQualifiedName~PdfUploadedIntegrationEventRelayTests|FullyQualifiedName~MessagingServiceExtensionsTests"`

- [ ] **Step 6: Implementare**

`SharedKernel/Application/Interfaces/IIntegrationEventPublisher.cs`:
```csharp
using Api.SharedKernel.Domain.Events;

namespace Api.SharedKernel.Application.Interfaces;

/// <summary>
/// Pubblica un integration event verso un sistema esterno al processo (spec azure-local §4.3). I domain
/// event restano su MediatR/outbox; questa porta serve ai consumer fuori dall'API (Azure Functions).
/// </summary>
internal interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IntegrationEventBase;
}
```

`Infrastructure/Messaging/NullIntegrationEventPublisher.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Events;

namespace Api.Infrastructure.Messaging;

/// <summary>Profili senza bus: l'evento viene solo loggato a Debug.</summary>
internal sealed class NullIntegrationEventPublisher : IIntegrationEventPublisher
{
    private readonly ILogger<NullIntegrationEventPublisher> _logger;

    public NullIntegrationEventPublisher(ILogger<NullIntegrationEventPublisher> logger)
    {
        _logger = logger;
    }

    public Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IntegrationEventBase
    {
        _logger.LogDebug("Integration event {EventType} {EventId} not published: no bus configured", typeof(TEvent).Name, integrationEvent.EventId);
        return Task.CompletedTask;
    }
}
```

`Infrastructure/Messaging/ServiceBusIntegrationEventPublisher.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Events;
using Azure.Messaging.ServiceBus;

namespace Api.Infrastructure.Messaging;

internal sealed class ServiceBusIntegrationEventPublisher : IIntegrationEventPublisher
{
    /// <summary>Formato di filo degli integration event: camelCase, enum come stringa. Condiviso con il test di contratto.</summary>
    public static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
        DefaultIgnoreCondition = JsonIgnoreCondition.Never,
    };

    private readonly ServiceBusSender _sender;
    private readonly ILogger<ServiceBusIntegrationEventPublisher> _logger;

    public ServiceBusIntegrationEventPublisher(ServiceBusSender sender, ILogger<ServiceBusIntegrationEventPublisher> logger)
    {
        _sender = sender;
        _logger = logger;
    }

    public async Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IntegrationEventBase
    {
        ArgumentNullException.ThrowIfNull(integrationEvent);

        var message = new ServiceBusMessage(BinaryData.FromObjectAsJson(integrationEvent, JsonOptions))
        {
            ContentType = "application/json",
            MessageId = integrationEvent.EventId.ToString(),
            Subject = typeof(TEvent).Name,
        };
        message.ApplicationProperties["sourceContext"] = integrationEvent.SourceContext;
        if (integrationEvent is IVersionedContract versioned)
        {
            message.ApplicationProperties["contractVersion"] = versioned.ContractVersion;
        }

        await _sender.SendMessageAsync(message, cancellationToken).ConfigureAwait(false);
        _logger.LogInformation("Published {EventType} {EventId} to {Entity}", typeof(TEvent).Name, integrationEvent.EventId, _sender.EntityPath);
    }
}

/// <summary>Un integration event con versione di contratto esplicita sul filo.</summary>
internal interface IVersionedContract
{
    string ContractVersion { get; }
}
```

`Infrastructure/Messaging/MessagingServiceExtensions.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;
using Azure.Messaging.ServiceBus;

namespace Api.Infrastructure.Messaging;

internal static class MessagingServiceExtensions
{
    public const string ConnectionStringKey = "AZURE_SERVICEBUS_CONNECTION_STRING";
    public const string QueueKey = "AZURE_SERVICEBUS_QUEUE";
    public const string DefaultQueue = "pdf-uploaded";

    /// <summary>
    /// Service Bus parla AMQP, non HTTP: il client <c>AzureSdk</c> pinnato non si applica. L'endpoint viene da
    /// configurazione statica (stessa esenzione di OpenRouter/Resend nel gate di egress).
    /// </summary>
    public static IServiceCollection AddIntegrationEventPublishing(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionStringKey];
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            services.AddSingleton<IIntegrationEventPublisher, NullIntegrationEventPublisher>();
            return services;
        }

        var queue = configuration[QueueKey] is { Length: > 0 } q ? q : DefaultQueue;
        services.AddSingleton(_ => new ServiceBusClient(connectionString, new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpTcp }));
        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(queue));
        services.AddSingleton<IIntegrationEventPublisher, ServiceBusIntegrationEventPublisher>();
        return services;
    }
}
```

`BoundedContexts/DocumentProcessing/Application/IntegrationEvents/PdfUploadedIntegrationEvent.cs`:
```csharp
using Api.Infrastructure.Messaging;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;

/// <summary>
/// Contratto v1 (spec azure-local §4.3): un PDF è stato ricevuto dall'API e la pipeline è partita
/// (transizione Pending → Uploading). Chiavi sul filo congelate in
/// <c>tests/fixtures/contracts/pdf-uploaded.v1.json</c>; cambiarle significa una v2.
/// </summary>
internal sealed class PdfUploadedIntegrationEvent : IntegrationEventBase, IVersionedContract
{
    public const string CurrentContractVersion = "v1";

    public string ContractVersion => CurrentContractVersion;
    public Guid PdfDocumentId { get; }
    public Guid GameId { get; }
    public Guid? SharedGameId { get; }
    public string FilePath { get; }

    public PdfUploadedIntegrationEvent(Guid pdfDocumentId, Guid gameId, Guid? sharedGameId, string filePath)
        : base("DocumentProcessing")
    {
        ArgumentException.ThrowIfNullOrWhiteSpace(filePath);
        PdfDocumentId = pdfDocumentId;
        GameId = gameId;
        SharedGameId = sharedGameId;
        FilePath = filePath;
    }
}
```

`BoundedContexts/DocumentProcessing/Application/EventHandlers/PdfUploadedIntegrationEventRelay.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using MediatR;

namespace Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;

/// <summary>
/// Traduce la transizione Pending → Uploading di <see cref="PdfStateChangedEvent"/> nell'integration event
/// <see cref="PdfUploadedIntegrationEvent"/>. Mai un'eccezione verso l'upload: il bus giù non deve far
/// fallire la richiesta dell'utente (Review Focus 5); l'esito finisce nel contatore.
/// </summary>
internal sealed class PdfUploadedIntegrationEventRelay : INotificationHandler<PdfStateChangedEvent>
{
    private readonly IIntegrationEventPublisher _publisher;
    private readonly IPdfDocumentRepository _repository;
    private readonly ILogger<PdfUploadedIntegrationEventRelay> _logger;

    public PdfUploadedIntegrationEventRelay(
        IIntegrationEventPublisher publisher,
        IPdfDocumentRepository repository,
        ILogger<PdfUploadedIntegrationEventRelay> logger)
    {
        _publisher = publisher;
        _repository = repository;
        _logger = logger;
    }

    public async Task Handle(PdfStateChangedEvent notification, CancellationToken cancellationToken)
    {
        if (notification.PreviousState != PdfProcessingState.Pending || notification.NewState != PdfProcessingState.Uploading)
        {
            return;
        }

        try
        {
            var document = await _repository.GetByIdAsync(notification.PdfDocumentId, cancellationToken).ConfigureAwait(false);
            if (document is null)
            {
                _logger.LogWarning("PdfUploaded not published: document {PdfId} not found", notification.PdfDocumentId);
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound);
                return;
            }

            var integrationEvent = new PdfUploadedIntegrationEvent(document.Id, document.GameId, document.SharedGameId, document.FilePath);
            await _publisher.PublishAsync(integrationEvent, cancellationToken).ConfigureAwait(false);
            MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Published);
        }
        catch (OperationCanceledException) when (cancellationToken.IsCancellationRequested)
        {
            throw;
        }
#pragma warning disable CA1031 // Confine: la pubblicazione è best-effort, l'upload non deve fallire
        catch (Exception ex)
        {
            _logger.LogError(ex, "PdfUploaded publish failed for {PdfId}", notification.PdfDocumentId);
            MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.PublishFailed);
        }
#pragma warning restore CA1031
    }
}
```

`Observability/Metrics/MeepleAiMetrics.PdfIntegrationEvents.cs`:
```csharp
// Spec azure-local §4.3: esiti dell'integration event PdfUploaded (API → Service Bus → Function → API).
using System.Diagnostics;
using System.Diagnostics.Metrics;

namespace Api.Observability;

internal static partial class MeepleAiMetrics
{
    /// <summary>Esiti bounded del ciclo PdfUploaded; tag <c>outcome</c>, mai id o host.</summary>
    public static class PdfUploadedEventOutcomes
    {
        public const string Published = "published";
        public const string PublishFailed = "publish_failed";
        public const string NotFound = "not_found";
        public const string Received = "received";
        public const string Indexed = "indexed";
        public const string Noop = "noop";
        public const string Rejected = "rejected";
        public const string Failed = "failed";
    }

    public static readonly Counter<long> PdfUploadedEventsTotal = Meter.CreateCounter<long>(
        name: "meepleai.pdf.uploaded_events.total",
        unit: "events",
        description: "PdfUploaded integration events by outcome (publish side and internal consumer side)");

    public static void RecordPdfUploadedEvent(string outcome) =>
        PdfUploadedEventsTotal.Add(1, new TagList { { "outcome", outcome } });
}
```

Aggiornare l'elenco dei file partial nel commento di testa di `Observability/MeepleAiMetrics.cs` con la riga `MeepleAiMetrics.PdfIntegrationEvents.cs — PdfUploaded integration event outcomes (spec azure-local)`.

In `Program.cs`, dopo `builder.Services.AddAzureSdkHttpClients(builder.Configuration);`:
```csharp
builder.Services.AddIntegrationEventPublishing(builder.Configuration); // spec azure-local §4.3
```
con `using Api.Infrastructure.Messaging;`. Il relay è un `INotificationHandler` nell'assembly dell'API: la registrazione MediatR via scansione dell'assembly lo prende da sola (verificare con `grep -n "RegisterServicesFromAssembly" apps/api/src/Api -r`).

- [ ] **Step 7: Eseguire i test**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServiceBusIntegrationEventPublisherTests|FullyQualifiedName~PdfUploadedIntegrationEventRelayTests|FullyQualifiedName~MessagingServiceExtensionsTests"`
Expected: tutti verdi.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Application/Interfaces/IIntegrationEventPublisher.cs apps/api/src/Api/Infrastructure/Messaging apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/IntegrationEvents apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PdfUploadedIntegrationEventRelay.cs apps/api/src/Api/Observability apps/api/src/Api/Program.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests
git commit -m "feat(messaging): PdfUploaded v1 su Service Bus via IIntegrationEventPublisher" -m "Relay sulla transizione Pending→Uploading, publisher Service Bus e Null, contatore degli esiti." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Endpoint interno `POST /api/v1/internal/pdf-events/uploaded` con token di servizio

**Files:**
- Create: `apps/api/src/Api/Filters/InternalServiceTokenFilter.cs`
- Create: `apps/api/src/Api/Routing/InternalPdfEventsEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs` (dopo la riga `v1Api.MapAdminPdfManagementEndpoints();`)
- Test: `apps/api/tests/Api.Tests/Filters/InternalServiceTokenFilterTests.cs`
- Test: `apps/api/tests/Api.Tests/Routing/InternalPdfEventsEndpointsTests.cs`

**Interfaces:**
- Consumes: `IndexPdfCommand(string PdfId)` → `IndexingResultDto { Success, ErrorCode: PdfIndexingErrorCode? }`, `MeepleAiMetrics.RecordPdfUploadedEvent`, `PdfUploadedIntegrationEvent.CurrentContractVersion` (Task 5), fixture `SharedHostPerTestDatabaseFixture`.
- Produces: header `X-Internal-Service-Token`, chiave `INTERNAL_SERVICE_TOKEN`; richiesta `PdfUploadedEventRequest(Guid EventId, Guid PdfDocumentId, string ContractVersion)`; risposte 200 `{outcome:"indexed"}`, 202 `{outcome:"noop"}`, 404 `{outcome:"not_found"}`, 400 `{outcome:"rejected"}`, 422 `{outcome:"failed"}`; 401/403/404 dal filtro.

- [ ] **Step 1: Test del filtro**

```csharp
using Api.Filters;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.AspNetCore.Http;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Filters;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class InternalServiceTokenFilterTests
{
    private static InternalServiceTokenFilter Filter(string? configured) =>
        new(new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [InternalServiceTokenFilter.ConfigKey] = configured })
            .Build());

    private static async Task<int?> StatusOf(InternalServiceTokenFilter filter, string? header)
    {
        var httpContext = new DefaultHttpContext();
        if (header is not null)
        {
            httpContext.Request.Headers[InternalServiceTokenFilter.HeaderName] = header;
        }

        var context = new DefaultEndpointFilterInvocationContext(httpContext);
        var result = await filter.InvokeAsync(context, _ => ValueTask.FromResult<object?>(Results.Ok("passed")));
        return result is IStatusCodeHttpResult status ? status.StatusCode : null;
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("  ")]
    public async Task InvokeAsync_TokenNotConfigured_Returns404(string? configured)
    {
        (await StatusOf(Filter(configured), "anything")).Should().Be(StatusCodes.Status404NotFound);
    }

    [Fact]
    public async Task InvokeAsync_MissingHeader_Returns401()
    {
        (await StatusOf(Filter("secret-1"), null)).Should().Be(StatusCodes.Status401Unauthorized);
    }

    [Theory]
    [InlineData("wrong")]
    [InlineData("secret-10")]
    public async Task InvokeAsync_WrongToken_Returns403(string header)
    {
        (await StatusOf(Filter("secret-1"), header)).Should().Be(StatusCodes.Status403Forbidden);
    }

    [Fact]
    public async Task InvokeAsync_RightToken_Passes()
    {
        (await StatusOf(Filter("secret-1"), "secret-1")).Should().Be(StatusCodes.Status200OK);
    }
}
```

- [ ] **Step 2: Implementare il filtro**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Api.Filters;

/// <summary>
/// Autenticazione servizio-a-servizio per il gruppo <c>/api/v1/internal</c> (spec azure-local §4.3): un
/// token condiviso in <c>INTERNAL_SERVICE_TOKEN</c>, confrontato a tempo costante. Senza token
/// configurato il gruppo NON esiste (404 per tutti): un deployment che dimentica la variabile non
/// espone un endpoint aperto (Review Focus 4).
/// </summary>
internal sealed class InternalServiceTokenFilter : IEndpointFilter
{
    public const string HeaderName = "X-Internal-Service-Token";
    public const string ConfigKey = "INTERNAL_SERVICE_TOKEN";

    private readonly IConfiguration _configuration;

    public InternalServiceTokenFilter(IConfiguration configuration)
    {
        _configuration = configuration;
    }

    public async ValueTask<object?> InvokeAsync(EndpointFilterInvocationContext context, EndpointFilterDelegate next)
    {
        var expected = _configuration[ConfigKey];
        if (string.IsNullOrWhiteSpace(expected))
        {
            return Results.NotFound();
        }

        if (!context.HttpContext.Request.Headers.TryGetValue(HeaderName, out var provided) || string.IsNullOrEmpty(provided))
        {
            return Results.Unauthorized();
        }

        var expectedBytes = Encoding.UTF8.GetBytes(expected);
        var providedBytes = Encoding.UTF8.GetBytes(provided.ToString());
        if (expectedBytes.Length != providedBytes.Length || !CryptographicOperations.FixedTimeEquals(expectedBytes, providedBytes))
        {
            return Results.StatusCode(StatusCodes.Status403Forbidden);
        }

        return await next(context).ConfigureAwait(false);
    }
}
```

- [ ] **Step 3: Eseguire i test del filtro**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~InternalServiceTokenFilterTests"`
Expected: tutti verdi.

- [ ] **Step 4: Test dell'endpoint con la fixture host condivisa**

```csharp
using System.Net;
using System.Net.Http.Json;
using Api.Filters;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Routing;

public sealed class InternalPdfEventsEndpointsHostFixture(SharedTestcontainersFixture shared)
    : SharedHostPerTestDatabaseFixture(shared, "internal_pdf_events")
{
    public const string Token = "test-internal-token";

    protected override Dictionary<string, string?>? ExtraConfiguration => new()
    {
        [InternalServiceTokenFilter.ConfigKey] = Token,
    };
}

[Collection("Integration-GroupC")]
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class InternalPdfEventsEndpointsTests : IClassFixture<InternalPdfEventsEndpointsHostFixture>, IAsyncLifetime
{
    private const string Route = "/api/v1/internal/pdf-events/uploaded";
    private readonly InternalPdfEventsEndpointsHostFixture _hostFixture;
    private HttpClient _client = null!;

    public InternalPdfEventsEndpointsTests(InternalPdfEventsEndpointsHostFixture hostFixture)
    {
        _hostFixture = hostFixture;
    }

    public async ValueTask InitializeAsync()
    {
        await _hostFixture.BeginTestAsync();
        _client = _hostFixture.Factory.CreateClient();
    }

    public ValueTask DisposeAsync() => ValueTask.CompletedTask;

    private static object Body(Guid pdfId, string version = "v1") => new { eventId = Guid.NewGuid(), pdfDocumentId = pdfId, contractVersion = version };

    [Fact]
    public async Task Post_WithoutToken_Returns401()
    {
        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Unauthorized);
    }

    [Fact]
    public async Task Post_WrongToken_Returns403()
    {
        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, "nope");

        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.Forbidden);
    }

    [Fact]
    public async Task Post_UnknownPdf_Returns404WithOutcome()
    {
        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, InternalPdfEventsEndpointsHostFixture.Token);

        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid()));

        response.StatusCode.Should().Be(HttpStatusCode.NotFound);
        (await response.Content.ReadAsStringAsync()).Should().Contain("not_found");
    }

    [Fact]
    public async Task Post_UnsupportedContract_Returns400WithOutcome()
    {
        _client.DefaultRequestHeaders.Add(InternalServiceTokenFilter.HeaderName, InternalPdfEventsEndpointsHostFixture.Token);

        var response = await _client.PostAsJsonAsync(Route, Body(Guid.NewGuid(), version: "v9"));

        response.StatusCode.Should().Be(HttpStatusCode.BadRequest);
        (await response.Content.ReadAsStringAsync()).Should().Contain("rejected");
    }
}
```

- [ ] **Step 5: Implementare l'endpoint**

`Routing/InternalPdfEventsEndpoints.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Filters;
using Api.Observability;
using MediatR;

namespace Api.Routing;

/// <summary>Corpo accettato dal consumer interno: il sottoinsieme del contratto v1 che l'API deve rileggere.</summary>
internal sealed record PdfUploadedEventRequest(Guid EventId, Guid PdfDocumentId, string? ContractVersion);

/// <summary>
/// Lato API del ciclo PdfUploaded (spec azure-local §4.3): la Function consuma il messaggio dal bus e
/// richiama qui. L'indicizzazione è idempotente per le due guardie esistenti (ProcessingState e
/// ProcessingJob): un duplicato è un <c>noop</c>, non un errore.
/// </summary>
internal static class InternalPdfEventsEndpoints
{
    public static void MapInternalPdfEventsEndpoints(this IEndpointRouteBuilder app)
    {
        var group = app.MapGroup("/internal/pdf-events")
            .WithTags("Internal - PDF events")
            .AddEndpointFilter<InternalServiceTokenFilter>();

        group.MapPost("/uploaded", HandleUploaded)
            .WithName("InternalPdfUploaded")
            .WithSummary("Consumer-side hook for the PdfUploaded v1 integration event (service token)");
    }

    private static async Task<IResult> HandleUploaded(PdfUploadedEventRequest body, IMediator mediator, ILogger<Program> logger, CancellationToken ct)
    {
        MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Received);

        if (!string.Equals(body.ContractVersion, PdfUploadedIntegrationEvent.CurrentContractVersion, StringComparison.Ordinal))
        {
            MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Rejected);
            return Results.BadRequest(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Rejected, error = $"unsupported contractVersion '{body.ContractVersion}'" });
        }

        logger.LogInformation("PdfUploaded {EventId} received for {PdfId}", body.EventId, body.PdfDocumentId);
        var result = await mediator.Send(new IndexPdfCommand(body.PdfDocumentId.ToString()), ct).ConfigureAwait(false);

        if (result.Success)
        {
            MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Indexed);
            return Results.Ok(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Indexed, chunkCount = result.ChunkCount });
        }

        switch (result.ErrorCode)
        {
            case PdfIndexingErrorCode.PdfNotFound:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound);
                return Results.NotFound(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound });
            case PdfIndexingErrorCode.TextExtractionRequired:
                // La pipeline di upload sta ancora estraendo: il messaggio è arrivato prima; nulla da fare.
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Noop);
                return Results.Accepted(value: new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Noop });
            default:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Failed);
                return Results.UnprocessableEntity(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Failed, error = result.ErrorMessage });
        }
    }
}
```

In `Program.cs`, dopo `v1Api.MapAdminPdfManagementEndpoints();`:
```csharp
v1Api.MapInternalPdfEventsEndpoints(); // spec azure-local §4.3: consumer interno del PdfUploaded v1
```

- [ ] **Step 6: Eseguire i test dell'endpoint**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~InternalPdfEventsEndpointsTests"`
Expected: 4 superati (richiede Docker per Postgres/Redis della fixture condivisa). Se un test di routing esistente enumera gli endpoint pubblici (cercare `grep -rn "internal" apps/api/tests/Api.Tests/Architecture/*Endpoint*`), aggiungere il nuovo gruppo alla sua classificazione come «token di servizio».

- [ ] **Step 7: Commit**

```bash
git add apps/api/src/Api/Filters/InternalServiceTokenFilter.cs apps/api/src/Api/Routing/InternalPdfEventsEndpoints.cs apps/api/src/Api/Program.cs apps/api/tests/Api.Tests/Filters apps/api/tests/Api.Tests/Routing/InternalPdfEventsEndpointsTests.cs
git commit -m "feat(api): endpoint interno PdfUploaded con token di servizio" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 7: Test di integrazione sul Service Bus emulator (publish → receive → dead-letter)

**Files:**
- Modify: `apps/api/tests/Api.Tests/Api.Tests.csproj`
- Modify: `apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs`
- Test: `apps/api/tests/Api.Tests/Integration/Infrastructure/ServiceBusEmulatorIntegrationTests.cs`

**Interfaces:**
- Consumes: `ServiceBusIntegrationEventPublisher`, `PdfUploadedIntegrationEvent` (Task 5).
- Produces: `TestcontainersConfiguration.ServiceBusEmulatorImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1"`.

- [ ] **Step 1: Pacchetto e costante**

Run (da `apps/api/tests/Api.Tests`): `dotnet add package Testcontainers.ServiceBus --version 4.14.0`

In `TestcontainersConfiguration.cs`, dopo `AzuriteTestContainer`:
```csharp
    /// <summary>Service Bus emulator (spec azure-local): L3; avvia da sé un MSSQL di appoggio (~1 min).</summary>
    public const string ServiceBusEmulatorImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1";
    /// <summary>Coda presente nel Config.json di default dell'emulatore.</summary>
    public const string ServiceBusEmulatorDefaultQueue = "queue.1";
```

- [ ] **Step 2: Scrivere il test**

```csharp
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Infrastructure.Messaging;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Testcontainers.ServiceBus;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Slow)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Feature", "AzureServiceBus")]
public sealed class ServiceBusEmulatorIntegrationTests : IAsyncLifetime
{
    private ServiceBusContainer? _emulator;
    private string? _unavailableReason;
    private ServiceBusClient _client = null!;

    public async ValueTask InitializeAsync()
    {
        try
        {
            _emulator = new ServiceBusBuilder()
                .WithImage(TestcontainersConfiguration.ServiceBusEmulatorImage)
                .WithAcceptLicenseAgreement(true)
                .Build();
            await _emulator.StartAsync();
            _client = new ServiceBusClient(_emulator.GetConnectionString());
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            _unavailableReason = ex.Message;
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (_client is not null)
        {
            await _client.DisposeAsync();
        }

        if (_emulator is not null)
        {
            await _emulator.DisposeAsync();
        }
    }

    private void SkipIfUnavailable()
    {
        if (_unavailableReason is not null)
        {
            Assert.Skip($"PREVISTO: Service Bus emulator non avviato ({_unavailableReason}). Serve Docker; in locale: cd infra && make dev-azure-local");
        }
    }

    [Fact]
    public async Task Publish_ThenReceive_RoundTripsTheContract()
    {
        SkipIfUnavailable();
        await using var sender = _client.CreateSender(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var receiver = _client.CreateReceiver(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        var publisher = new ServiceBusIntegrationEventPublisher(sender, Mock.Of<ILogger<ServiceBusIntegrationEventPublisher>>());
        var evt = new PdfUploadedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), null, "pdfs/g/f_a.pdf");

        await publisher.PublishAsync(evt, CancellationToken.None);
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

        received.Should().NotBeNull();
        received!.MessageId.Should().Be(evt.EventId.ToString());
        received.ApplicationProperties["contractVersion"].Should().Be("v1");
        using var doc = JsonDocument.Parse(received.Body.ToString());
        doc.RootElement.GetProperty("pdfDocumentId").GetGuid().Should().Be(evt.PdfDocumentId);
        await receiver.CompleteMessageAsync(received);
    }

    [Fact]
    public async Task DeadLetter_LandsInDeadLetterSubQueueWithReason()
    {
        SkipIfUnavailable();
        await using var sender = _client.CreateSender(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var receiver = _client.CreateReceiver(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var deadLetters = _client.CreateReceiver(
            TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue,
            new ServiceBusReceiverOptions { SubQueue = SubQueue.DeadLetter });
        var messageId = Guid.NewGuid().ToString();

        await sender.SendMessageAsync(new ServiceBusMessage("{\"contractVersion\":\"v9\"}") { MessageId = messageId });
        var received = await receiver.ReceiveMessageAsync(TimeSpan.FromSeconds(30));
        received.Should().NotBeNull();
        await receiver.DeadLetterMessageAsync(received!, "unsupported_contract", "contractVersion v9");

        var dead = await deadLetters.ReceiveMessageAsync(TimeSpan.FromSeconds(30));

        dead.Should().NotBeNull();
        dead!.MessageId.Should().Be(messageId);
        dead.DeadLetterReason.Should().Be("unsupported_contract");
        await deadLetters.CompleteMessageAsync(dead);
    }
}
```

- [ ] **Step 3: Eseguire**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServiceBusEmulatorIntegrationTests"`
Expected: 2 superati; il primo avvio scarica l'emulatore e MSSQL (alcuni minuti). Senza Docker: 2 salti `PREVISTO:`.

- [ ] **Step 4: Commit**

```bash
git add apps/api/tests/Api.Tests/Api.Tests.csproj apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs apps/api/tests/Api.Tests/Integration/Infrastructure/ServiceBusEmulatorIntegrationTests.cs
git commit -m "test(messaging): publish, receive e dead-letter sul Service Bus emulator" -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 8: Azure Functions consumer `PdfUploadedConsumer` e contratto condiviso

**Files:**
- Create: `tests/fixtures/contracts/pdf-uploaded.v1.json`
- Create: `apps/functions/MeepleAI.Functions.sln`
- Create: `apps/functions/MeepleAI.Functions/MeepleAI.Functions.csproj`
- Create: `apps/functions/MeepleAI.Functions/Program.cs`
- Create: `apps/functions/MeepleAI.Functions/host.json`
- Create: `apps/functions/MeepleAI.Functions/local.settings.json.example`
- Create: `apps/functions/MeepleAI.Functions/Contracts/PdfUploadedContract.cs`
- Create: `apps/functions/MeepleAI.Functions/Forwarding/IPdfUploadedForwarder.cs`
- Create: `apps/functions/MeepleAI.Functions/Forwarding/PdfUploadedForwarder.cs`
- Create: `apps/functions/MeepleAI.Functions/PdfUploadedConsumer.cs`
- Create: `apps/functions/MeepleAI.Functions.Tests/MeepleAI.Functions.Tests.csproj`
- Create: `apps/functions/MeepleAI.Functions.Tests/ContractFixture.cs`
- Create: `apps/functions/MeepleAI.Functions.Tests/PdfUploadedContractTests.cs`
- Create: `apps/functions/MeepleAI.Functions.Tests/PdfUploadedForwarderTests.cs`
- Create: `apps/functions/MeepleAI.Functions.Tests/PdfUploadedConsumerTests.cs`
- Create: `apps/api/tests/Api.Tests/Contracts/PdfUploadedContractTests.cs`
- Modify: `.gitignore` (radice): aggiungere `apps/functions/**/local.settings.json`

**Interfaces:**
- Consumes: formato di filo di `PdfUploadedIntegrationEvent` e `ServiceBusIntegrationEventPublisher.JsonOptions` (Task 5); endpoint `POST /api/v1/internal/pdf-events/uploaded` + header `X-Internal-Service-Token` (Task 6).
- Produces: fixture `pdf-uploaded.v1.json` (chiavi camelCase congelate); app settings della Function `ServiceBusConnection`, `ServiceBusQueue`, `MeepleAiApiBaseUrl`, `MeepleAiInternalServiceToken`.

- [ ] **Step 1: La fixture di contratto** `tests/fixtures/contracts/pdf-uploaded.v1.json`

```json
{
  "eventId": "3f0f1a2b-4c5d-4e6f-8a9b-0c1d2e3f4a5b",
  "occurredAt": "2026-10-09T10:00:00Z",
  "sourceContext": "DocumentProcessing",
  "contractVersion": "v1",
  "pdfDocumentId": "7b2d9e4a-1c3f-4a5b-9d8e-2f1a3b4c5d6e",
  "gameId": "9a8b7c6d-5e4f-4a3b-8c2d-1e0f9a8b7c6d",
  "sharedGameId": null,
  "filePath": "pdfs/9a8b7c6d5e4f4a3b8c2d1e0f9a8b7c6d/0123456789abcdef0123456789abcdef_rulebook.pdf"
}
```

- [ ] **Step 2: Test di contratto lato API** `apps/api/tests/Api.Tests/Contracts/PdfUploadedContractTests.cs`

```csharp
using System.Text.Json;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Infrastructure.Messaging;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Contracts;

/// <summary>Le chiavi sul filo di PdfUploaded v1 sono congelate nella fixture condivisa con apps/functions.</summary>
[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class PdfUploadedContractTests
{
    private static string FixturePath()
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tests", "fixtures", "contracts", "pdf-uploaded.v1.json")))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("la fixture tests/fixtures/contracts/pdf-uploaded.v1.json deve esistere sopra la cartella di build");
        return Path.Combine(dir!.FullName, "tests", "fixtures", "contracts", "pdf-uploaded.v1.json");
    }

    [Fact]
    public void SerializedEvent_HasExactlyTheFixtureKeys()
    {
        var evt = new PdfUploadedIntegrationEvent(Guid.NewGuid(), Guid.NewGuid(), null, "pdfs/g/f_a.pdf");
        using var actual = JsonDocument.Parse(JsonSerializer.Serialize(evt, ServiceBusIntegrationEventPublisher.JsonOptions));
        using var expected = JsonDocument.Parse(File.ReadAllText(FixturePath()));

        var actualKeys = actual.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);
        var expectedKeys = expected.RootElement.EnumerateObject().Select(p => p.Name).Order(StringComparer.Ordinal);

        actualKeys.Should().Equal(expectedKeys);
        expected.RootElement.GetProperty("contractVersion").GetString().Should().Be(PdfUploadedIntegrationEvent.CurrentContractVersion);
    }
}
```

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~Api.Tests.Contracts.PdfUploadedContractTests"` → Expected: verde (se rosso, le chiavi JSON del Task 5 divergono dalla fixture: correggere il tipo, non la fixture).

- [ ] **Step 3: Progetto Functions**

`apps/functions/MeepleAI.Functions/MeepleAI.Functions.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <AzureFunctionsVersion>v4</AzureFunctionsVersion>
    <OutputType>Exe</OutputType>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
    <RootNamespace>MeepleAI.Functions</RootNamespace>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.Azure.Functions.Worker" Version="2.52.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Sdk" Version="2.1.0" />
    <PackageReference Include="Microsoft.Azure.Functions.Worker.Extensions.ServiceBus" Version="5.24.0" />
  </ItemGroup>
  <ItemGroup>
    <None Update="host.json" CopyToOutputDirectory="PreserveNewest" />
    <None Update="local.settings.json" CopyToOutputDirectory="PreserveNewest" CopyToPublishDirectory="Never" />
  </ItemGroup>
  <ItemGroup>
    <InternalsVisibleTo Include="MeepleAI.Functions.Tests" />
  </ItemGroup>
</Project>
```

`host.json`:
```json
{
  "version": "2.0",
  "logging": { "logLevel": { "default": "Information" } },
  "extensions": { "serviceBus": { "prefetchCount": 0, "autoCompleteMessages": false, "maxConcurrentCalls": 4 } }
}
```

`local.settings.json.example` (il file reale è git-ignorato):
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "UseDevelopmentStorage=true",
    "FUNCTIONS_WORKER_RUNTIME": "dotnet-isolated",
    "ServiceBusConnection": "Endpoint=sb://localhost:20004;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;",
    "ServiceBusQueue": "pdf-uploaded",
    "MeepleAiApiBaseUrl": "http://localhost:8080",
    "MeepleAiInternalServiceToken": "azure-local-dev-token"
  }
}
```

`Program.cs`:
```csharp
using MeepleAI.Functions.Forwarding;
using Microsoft.Extensions.DependencyInjection;
using Microsoft.Extensions.Hosting;

var host = new HostBuilder()
    .ConfigureFunctionsWorkerDefaults()
    .ConfigureServices((context, services) =>
    {
        services.AddHttpClient(PdfUploadedForwarder.HttpClientName, client =>
        {
            var baseUrl = context.Configuration["MeepleAiApiBaseUrl"]
                ?? throw new InvalidOperationException("MeepleAiApiBaseUrl is required");
            client.BaseAddress = new Uri(baseUrl, UriKind.Absolute);
            client.DefaultRequestHeaders.Add(PdfUploadedForwarder.TokenHeader,
                context.Configuration["MeepleAiInternalServiceToken"]
                ?? throw new InvalidOperationException("MeepleAiInternalServiceToken is required"));
            client.Timeout = TimeSpan.FromSeconds(30);
        });
        services.AddSingleton<IPdfUploadedForwarder, PdfUploadedForwarder>();
    })
    .Build();

host.Run();
```

`Contracts/PdfUploadedContract.cs`:
```csharp
using System.Text.Json;
using System.Text.Json.Serialization;

namespace MeepleAI.Functions.Contracts;

/// <summary>Contratto v1 di PdfUploaded, specchio di tests/fixtures/contracts/pdf-uploaded.v1.json.</summary>
public sealed record PdfUploadedEventV1(
    Guid EventId,
    DateTime OccurredAt,
    string SourceContext,
    string ContractVersion,
    Guid PdfDocumentId,
    Guid GameId,
    Guid? SharedGameId,
    string FilePath);

public sealed record ContractParseResult(PdfUploadedEventV1? Event, string? Reason, string? Detail)
{
    public bool IsValid => Event is not null;
}

public static class PdfUploadedContract
{
    public const string SupportedVersion = "v1";
    public const string ReasonMalformed = "malformed";
    public const string ReasonUnsupportedContract = "unsupported_contract";
    public const string ReasonMissingPdfId = "missing_pdf_id";

    private static readonly JsonSerializerOptions JsonOptions = new(JsonSerializerDefaults.Web)
    {
        Converters = { new JsonStringEnumConverter() },
    };

    public static ContractParseResult TryParse(BinaryData body)
    {
        PdfUploadedEventV1? parsed;
        try
        {
            parsed = JsonSerializer.Deserialize<PdfUploadedEventV1>(body, JsonOptions);
        }
        catch (JsonException ex)
        {
            return new ContractParseResult(null, ReasonMalformed, ex.Message);
        }

        if (parsed is null)
        {
            return new ContractParseResult(null, ReasonMalformed, "empty body");
        }

        if (!string.Equals(parsed.ContractVersion, SupportedVersion, StringComparison.Ordinal))
        {
            return new ContractParseResult(null, ReasonUnsupportedContract, $"contractVersion '{parsed.ContractVersion}' is not {SupportedVersion}");
        }

        if (parsed.PdfDocumentId == Guid.Empty)
        {
            return new ContractParseResult(null, ReasonMissingPdfId, "pdfDocumentId is empty");
        }

        return new ContractParseResult(parsed, null, null);
    }
}
```

`Forwarding/IPdfUploadedForwarder.cs`:
```csharp
using MeepleAI.Functions.Contracts;

namespace MeepleAI.Functions.Forwarding;

public enum ForwardOutcome
{
    /// <summary>L'API ha accettato o rifiutato in modo definitivo (2xx, 400, 404, 422): il messaggio si completa.</summary>
    Settled,
    /// <summary>Errore di rete o 5xx: il messaggio si abbandona e Service Bus lo riconsegna fino a MaxDeliveryCount.</summary>
    TransientFailure,
}

public interface IPdfUploadedForwarder
{
    Task<ForwardOutcome> ForwardAsync(PdfUploadedEventV1 integrationEvent, CancellationToken cancellationToken);
}
```

`Forwarding/PdfUploadedForwarder.cs`:
```csharp
using System.Net;
using System.Net.Http.Json;
using MeepleAI.Functions.Contracts;
using Microsoft.Extensions.Logging;

namespace MeepleAI.Functions.Forwarding;

public sealed class PdfUploadedForwarder : IPdfUploadedForwarder
{
    public const string HttpClientName = "MeepleAiApi";
    public const string TokenHeader = "X-Internal-Service-Token";
    public const string Route = "/api/v1/internal/pdf-events/uploaded";

    private readonly IHttpClientFactory _httpClientFactory;
    private readonly ILogger<PdfUploadedForwarder> _logger;

    public PdfUploadedForwarder(IHttpClientFactory httpClientFactory, ILogger<PdfUploadedForwarder> logger)
    {
        _httpClientFactory = httpClientFactory;
        _logger = logger;
    }

    public async Task<ForwardOutcome> ForwardAsync(PdfUploadedEventV1 integrationEvent, CancellationToken cancellationToken)
    {
        var client = _httpClientFactory.CreateClient(HttpClientName);
        var payload = new { eventId = integrationEvent.EventId, pdfDocumentId = integrationEvent.PdfDocumentId, contractVersion = integrationEvent.ContractVersion };

        try
        {
            using var response = await client.PostAsJsonAsync(Route, payload, cancellationToken).ConfigureAwait(false);
            if (response.IsSuccessStatusCode
                || response.StatusCode is HttpStatusCode.BadRequest or HttpStatusCode.NotFound or HttpStatusCode.UnprocessableEntity)
            {
                _logger.LogInformation("PdfUploaded {EventId} forwarded: {Status}", integrationEvent.EventId, (int)response.StatusCode);
                return ForwardOutcome.Settled;
            }

            _logger.LogWarning("PdfUploaded {EventId} forward got {Status}: will retry", integrationEvent.EventId, (int)response.StatusCode);
            return ForwardOutcome.TransientFailure;
        }
        catch (HttpRequestException ex)
        {
            _logger.LogWarning(ex, "PdfUploaded {EventId} forward failed: will retry", integrationEvent.EventId);
            return ForwardOutcome.TransientFailure;
        }
        catch (TaskCanceledException ex) when (!cancellationToken.IsCancellationRequested)
        {
            _logger.LogWarning(ex, "PdfUploaded {EventId} forward timed out: will retry", integrationEvent.EventId);
            return ForwardOutcome.TransientFailure;
        }
    }
}
```

`PdfUploadedConsumer.cs`:
```csharp
using Azure.Messaging.ServiceBus;
using MeepleAI.Functions.Contracts;
using MeepleAI.Functions.Forwarding;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging;

namespace MeepleAI.Functions;

/// <summary>
/// Consumer della coda <c>pdf-uploaded</c> (spec azure-local §4.3). Settlement esplicito: un contratto
/// invalido va in dead-letter con motivo (Review Focus 3), un guasto transitorio si abbandona e Service
/// Bus riconsegna fino a MaxDeliveryCount, un esito definitivo completa il messaggio.
/// </summary>
public sealed class PdfUploadedConsumer
{
    private readonly IPdfUploadedForwarder _forwarder;
    private readonly ILogger<PdfUploadedConsumer> _logger;

    public PdfUploadedConsumer(IPdfUploadedForwarder forwarder, ILogger<PdfUploadedConsumer> logger)
    {
        _forwarder = forwarder;
        _logger = logger;
    }

    [Function("PdfUploadedConsumer")]
    public async Task Run(
        [ServiceBusTrigger("%ServiceBusQueue%", Connection = "ServiceBusConnection", AutoCompleteMessages = false)] ServiceBusReceivedMessage message,
        ServiceBusMessageActions actions,
        CancellationToken cancellationToken)
    {
        var parsed = PdfUploadedContract.TryParse(message.Body);
        if (!parsed.IsValid)
        {
            _logger.LogWarning("Message {MessageId} dead-lettered: {Reason} ({Detail})", message.MessageId, parsed.Reason, parsed.Detail);
            await actions.DeadLetterMessageAsync(message, deadLetterReason: parsed.Reason, deadLetterErrorDescription: parsed.Detail, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        var outcome = await _forwarder.ForwardAsync(parsed.Event!, cancellationToken).ConfigureAwait(false);
        if (outcome == ForwardOutcome.TransientFailure)
        {
            await actions.AbandonMessageAsync(message, cancellationToken: cancellationToken).ConfigureAwait(false);
            return;
        }

        await actions.CompleteMessageAsync(message, cancellationToken).ConfigureAwait(false);
    }
}
```

`apps/functions/MeepleAI.Functions.sln`: creare con `dotnet new sln -n MeepleAI.Functions` da `apps/functions`, poi `dotnet sln add MeepleAI.Functions/MeepleAI.Functions.csproj MeepleAI.Functions.Tests/MeepleAI.Functions.Tests.csproj` (dopo lo Step 4).

- [ ] **Step 4: Progetto di test**

`apps/functions/MeepleAI.Functions.Tests/MeepleAI.Functions.Tests.csproj`:
```xml
<Project Sdk="Microsoft.NET.Sdk">
  <PropertyGroup>
    <TargetFramework>net9.0</TargetFramework>
    <Nullable>enable</Nullable>
    <ImplicitUsings>enable</ImplicitUsings>
    <IsPackable>false</IsPackable>
    <TreatWarningsAsErrors>true</TreatWarningsAsErrors>
  </PropertyGroup>
  <ItemGroup>
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="17.14.1" />
    <PackageReference Include="xunit.v3" Version="2.0.3" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.3" />
    <PackageReference Include="FluentAssertions" Version="7.2.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../MeepleAI.Functions/MeepleAI.Functions.csproj" />
  </ItemGroup>
</Project>
```
Le versioni di `Microsoft.NET.Test.Sdk`, `xunit.v3`, `xunit.runner.visualstudio` e `FluentAssertions` vanno copiate da `apps/api/tests/Api.Tests/Api.Tests.csproj` (stesse versioni del repo, qualunque esse siano): `grep -n "Microsoft.NET.Test.Sdk\|xunit\|FluentAssertions" apps/api/tests/Api.Tests/Api.Tests.csproj`.

`ContractFixture.cs`:
```csharp
namespace MeepleAI.Functions.Tests;

internal static class ContractFixture
{
    public static string PathOf(string fileName)
    {
        var dir = new DirectoryInfo(AppContext.BaseDirectory);
        while (dir is not null && !File.Exists(Path.Combine(dir.FullName, "tests", "fixtures", "contracts", fileName)))
        {
            dir = dir.Parent;
        }

        return dir is null
            ? throw new FileNotFoundException($"tests/fixtures/contracts/{fileName} not found above {AppContext.BaseDirectory}")
            : Path.Combine(dir.FullName, "tests", "fixtures", "contracts", fileName);
    }

    public static string ReadV1() => File.ReadAllText(PathOf("pdf-uploaded.v1.json"));
}
```

`PdfUploadedContractTests.cs`:
```csharp
using FluentAssertions;
using MeepleAI.Functions.Contracts;
using Xunit;

namespace MeepleAI.Functions.Tests;

public sealed class PdfUploadedContractTests
{
    [Fact]
    public void TryParse_SharedFixture_IsValid()
    {
        var result = PdfUploadedContract.TryParse(BinaryData.FromString(ContractFixture.ReadV1()));

        result.IsValid.Should().BeTrue(result.Detail);
        result.Event!.ContractVersion.Should().Be("v1");
        result.Event.SharedGameId.Should().BeNull();
        result.Event.FilePath.Should().EndWith("_rulebook.pdf");
    }

    [Fact]
    public void TryParse_UnsupportedVersion_IsRejected()
    {
        var json = ContractFixture.ReadV1().Replace("\"v1\"", "\"v2\"", StringComparison.Ordinal);

        var result = PdfUploadedContract.TryParse(BinaryData.FromString(json));

        result.IsValid.Should().BeFalse();
        result.Reason.Should().Be(PdfUploadedContract.ReasonUnsupportedContract);
    }

    [Fact]
    public void TryParse_MalformedJson_IsRejected()
    {
        var result = PdfUploadedContract.TryParse(BinaryData.FromString("{not json"));

        result.Reason.Should().Be(PdfUploadedContract.ReasonMalformed);
    }

    [Fact]
    public void TryParse_EmptyPdfId_IsRejected()
    {
        var json = ContractFixture.ReadV1().Replace("7b2d9e4a-1c3f-4a5b-9d8e-2f1a3b4c5d6e", Guid.Empty.ToString(), StringComparison.Ordinal);

        var result = PdfUploadedContract.TryParse(BinaryData.FromString(json));

        result.Reason.Should().Be(PdfUploadedContract.ReasonMissingPdfId);
    }
}
```

`PdfUploadedForwarderTests.cs`:
```csharp
using System.Net;
using FluentAssertions;
using MeepleAI.Functions.Contracts;
using MeepleAI.Functions.Forwarding;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MeepleAI.Functions.Tests;

public sealed class PdfUploadedForwarderTests
{
    private sealed class StubHandler(Func<HttpRequestMessage, HttpResponseMessage> respond) : HttpMessageHandler
    {
        public HttpRequestMessage? LastRequest { get; private set; }

        protected override Task<HttpResponseMessage> SendAsync(HttpRequestMessage request, CancellationToken cancellationToken)
        {
            LastRequest = request;
            return Task.FromResult(respond(request));
        }
    }

    private static (PdfUploadedForwarder Sut, StubHandler Handler) Build(Func<HttpRequestMessage, HttpResponseMessage> respond)
    {
        var handler = new StubHandler(respond);
        var client = new HttpClient(handler) { BaseAddress = new Uri("http://api.test") };
        client.DefaultRequestHeaders.Add(PdfUploadedForwarder.TokenHeader, "t");
        var factory = Substitute.For<IHttpClientFactory>();
        factory.CreateClient(PdfUploadedForwarder.HttpClientName).Returns(client);
        return (new PdfUploadedForwarder(factory, NullLogger<PdfUploadedForwarder>.Instance), handler);
    }

    private static PdfUploadedEventV1 Event() =>
        PdfUploadedContract.TryParse(BinaryData.FromString(ContractFixture.ReadV1())).Event!;

    [Theory]
    [InlineData(HttpStatusCode.OK)]
    [InlineData(HttpStatusCode.Accepted)]
    [InlineData(HttpStatusCode.BadRequest)]
    [InlineData(HttpStatusCode.NotFound)]
    [InlineData(HttpStatusCode.UnprocessableEntity)]
    public async Task ForwardAsync_DefinitiveStatus_IsSettled(HttpStatusCode status)
    {
        var (sut, handler) = Build(_ => new HttpResponseMessage(status));

        var outcome = await sut.ForwardAsync(Event(), CancellationToken.None);

        outcome.Should().Be(ForwardOutcome.Settled);
        handler.LastRequest!.RequestUri!.AbsolutePath.Should().Be(PdfUploadedForwarder.Route);
        handler.LastRequest.Headers.GetValues(PdfUploadedForwarder.TokenHeader).Should().ContainSingle("t");
    }

    [Theory]
    [InlineData(HttpStatusCode.InternalServerError)]
    [InlineData(HttpStatusCode.ServiceUnavailable)]
    public async Task ForwardAsync_ServerError_IsTransient(HttpStatusCode status)
    {
        var (sut, _) = Build(_ => new HttpResponseMessage(status));

        (await sut.ForwardAsync(Event(), CancellationToken.None)).Should().Be(ForwardOutcome.TransientFailure);
    }

    [Fact]
    public async Task ForwardAsync_NetworkFailure_IsTransient()
    {
        var (sut, _) = Build(_ => throw new HttpRequestException("refused"));

        (await sut.ForwardAsync(Event(), CancellationToken.None)).Should().Be(ForwardOutcome.TransientFailure);
    }
}
```

`PdfUploadedConsumerTests.cs`:
```csharp
using Azure.Messaging.ServiceBus;
using FluentAssertions;
using MeepleAI.Functions.Contracts;
using MeepleAI.Functions.Forwarding;
using Microsoft.Azure.Functions.Worker;
using Microsoft.Extensions.Logging.Abstractions;
using NSubstitute;
using Xunit;

namespace MeepleAI.Functions.Tests;

public sealed class PdfUploadedConsumerTests
{
    private readonly IPdfUploadedForwarder _forwarder = Substitute.For<IPdfUploadedForwarder>();
    private readonly ServiceBusMessageActions _actions = Substitute.For<ServiceBusMessageActions>();

    private PdfUploadedConsumer Sut() => new(_forwarder, NullLogger<PdfUploadedConsumer>.Instance);

    private static ServiceBusReceivedMessage Message(string json) =>
        ServiceBusModelFactory.ServiceBusReceivedMessage(body: BinaryData.FromString(json), messageId: "m-1");

    [Fact]
    public async Task Run_ValidContract_ForwardsAndCompletes()
    {
        _forwarder.ForwardAsync(Arg.Any<PdfUploadedEventV1>(), Arg.Any<CancellationToken>()).Returns(ForwardOutcome.Settled);
        var message = Message(ContractFixture.ReadV1());

        await Sut().Run(message, _actions, CancellationToken.None);

        await _forwarder.Received(1).ForwardAsync(Arg.Any<PdfUploadedEventV1>(), Arg.Any<CancellationToken>());
        await _actions.Received(1).CompleteMessageAsync(message, Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_UnsupportedContract_DeadLettersWithoutForwarding()
    {
        var message = Message(ContractFixture.ReadV1().Replace("\"v1\"", "\"v7\"", StringComparison.Ordinal));

        await Sut().Run(message, _actions, CancellationToken.None);

        await _forwarder.DidNotReceiveWithAnyArgs().ForwardAsync(default!, default);
        await _actions.Received(1).DeadLetterMessageAsync(
            message,
            deadLetterReason: PdfUploadedContract.ReasonUnsupportedContract,
            deadLetterErrorDescription: Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_MalformedBody_DeadLettersAsMalformed()
    {
        var message = Message("{oops");

        await Sut().Run(message, _actions, CancellationToken.None);

        await _actions.Received(1).DeadLetterMessageAsync(
            message,
            deadLetterReason: PdfUploadedContract.ReasonMalformed,
            deadLetterErrorDescription: Arg.Any<string>(),
            cancellationToken: Arg.Any<CancellationToken>());
    }

    [Fact]
    public async Task Run_TransientFailure_AbandonsForRedelivery()
    {
        _forwarder.ForwardAsync(Arg.Any<PdfUploadedEventV1>(), Arg.Any<CancellationToken>()).Returns(ForwardOutcome.TransientFailure);
        var message = Message(ContractFixture.ReadV1());

        await Sut().Run(message, _actions, CancellationToken.None);

        await _actions.Received(1).AbandonMessageAsync(message, cancellationToken: Arg.Any<CancellationToken>());
        await _actions.DidNotReceiveWithAnyArgs().CompleteMessageAsync(default!, default);
    }
}
```
Se la firma di `DeadLetterMessageAsync`/`AbandonMessageAsync` nella 5.24.0 ha un parametro `propertiesToModify` prima del motivo, passare `propertiesToModify: null` negli `Received(...)` e nel consumer (named arguments: l'ordine non cambia il resto).

- [ ] **Step 5: Build e test**

Run (da `apps/functions`):
```bash
dotnet new sln -n MeepleAI.Functions
dotnet sln add MeepleAI.Functions/MeepleAI.Functions.csproj MeepleAI.Functions.Tests/MeepleAI.Functions.Tests.csproj
dotnet test
```
Expected: compilazione verde, 11 test superati (4 contratto + 3 forwarder + 4 consumer).

- [ ] **Step 6: Commit**

```bash
git add tests/fixtures/contracts apps/functions apps/api/tests/Api.Tests/Contracts .gitignore
git commit -m "feat(functions): consumer PdfUploaded su Azure Functions isolated" -m "Contratto v1 congelato in tests/fixtures/contracts e verificato da API e Function; dead-letter sui contratti invalidi, abandon sui guasti transitori." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 9: Exporter OTLP per trace, metriche e log

**Files:**
- Create: `apps/api/src/Api/Observability/OtlpExport.cs`
- Modify: `apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs` (`AddOpenTelemetryServices`, `ConfigureTracing`, `ConfigureMetrics`)
- Modify: `apps/api/src/Api/Logging/LoggingConfiguration.cs` (dopo il blocco Seq)
- Modify: `apps/api/src/Api/Api.csproj`
- Test: `apps/api/tests/Api.Tests/Observability/OtlpExportTests.cs`

**Interfaces:**
- Consumes: `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `MEEPLEAI_PROFILE` (Task 1).
- Produces: `OtlpExport.IsEnabled(IConfiguration)`, `OtlpExport.Endpoint(IConfiguration)`; attributo risorsa `meepleai.profile`.

- [ ] **Step 1: Pacchetti**

Run (da `apps/api/src/Api`):
```bash
dotnet add package OpenTelemetry.Exporter.OpenTelemetryProtocol --version 1.15.3
dotnet add package Serilog.Sinks.OpenTelemetry --version 4.2.0
```

- [ ] **Step 2: Test**

```csharp
using Api.Observability;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Observability;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Observability")]
public sealed class OtlpExportTests
{
    private static IConfiguration Config(string? endpoint) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint })
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void IsEnabled_AbsentOrBlank_IsFalse(string? endpoint)
    {
        OtlpExport.IsEnabled(Config(endpoint)).Should().BeFalse();
    }

    [Fact]
    public void IsEnabled_WithEndpoint_IsTrueAndParsesUri()
    {
        var config = Config("http://aspire-dashboard:18889");

        OtlpExport.IsEnabled(config).Should().BeTrue();
        OtlpExport.Endpoint(config).Should().Be(new Uri("http://aspire-dashboard:18889"));
    }

    [Fact]
    public void Endpoint_Invalid_Throws()
    {
        var act = () => OtlpExport.Endpoint(Config("nope"));

        act.Should().Throw<InvalidOperationException>().WithMessage("*OTEL_EXPORTER_OTLP_ENDPOINT*");
    }
}
```

- [ ] **Step 3: Implementare**

`Observability/OtlpExport.cs`:
```csharp
namespace Api.Observability;

/// <summary>
/// Gate unico dell'export OTLP (spec azure-local §4.4): trace e metriche OpenTelemetry e i log Serilog
/// vanno all'endpoint di <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> quando è valorizzato (Aspire Dashboard in
/// locale, Azure Monitor via collector in Azure). Prometheus resta sempre attivo.
/// </summary>
internal static class OtlpExport
{
    public const string EndpointKey = "OTEL_EXPORTER_OTLP_ENDPOINT";
    public const string ProfileKey = "MEEPLEAI_PROFILE";

    public static bool IsEnabled(IConfiguration configuration) => !string.IsNullOrWhiteSpace(configuration[EndpointKey]);

    public static Uri Endpoint(IConfiguration configuration)
    {
        var raw = configuration[EndpointKey];
        if (string.IsNullOrWhiteSpace(raw) || !Uri.TryCreate(raw.Trim(), UriKind.Absolute, out var uri))
        {
            throw new InvalidOperationException($"{EndpointKey} must be an absolute URL, got '{raw}'");
        }

        return uri;
    }

    public static string ProfileName(IConfiguration configuration) =>
        configuration[ProfileKey] is { Length: > 0 } profile ? profile : "default";
}
```

In `ObservabilityServiceExtensions.AddOpenTelemetryServices`: aggiungere `["meepleai.profile"] = OtlpExport.ProfileName(configuration)` nel dizionario degli attributi; cambiare le due chiamate in `.WithTracing(tracing => tracing.ConfigureTracing(OtlpExport.IsEnabled(configuration)))` e `.WithMetrics(metrics => metrics.ConfigureMetrics(OtlpExport.IsEnabled(configuration)))`. Le due firme diventano `ConfigureTracing(this TracerProviderBuilder tracing, bool exportOtlp)` e `ConfigureMetrics(this MeterProviderBuilder metrics, bool exportOtlp)`; in coda a ciascuna catena:

```csharp
        if (exportOtlp)
        {
            // L'exporter legge OTEL_EXPORTER_OTLP_ENDPOINT / OTEL_EXPORTER_OTLP_PROTOCOL dall'ambiente.
            tracing.AddOtlpExporter();
        }

        return tracing;
```
(e lo stesso per `metrics`, mantenendo `.AddPrometheusExporter()` prima). Attenzione: `ConfigureTracing` oggi è una singola espressione `return tracing.SetSampler(...)...;` — spezzarla in assegnazione + `if` + `return`.

In `LoggingConfiguration.ConfigureSerilog`, dopo il blocco Seq:
```csharp
        // Spec azure-local §4.4: log su OTLP accanto a Seq quando l'endpoint è configurato.
        if (OtlpExport.IsEnabled(configuration))
        {
            var otlpEndpoint = OtlpExport.Endpoint(configuration);
            loggerConfig.WriteTo.OpenTelemetry(options =>
            {
                options.Endpoint = otlpEndpoint.ToString();
                options.Protocol = Serilog.Sinks.OpenTelemetry.OtlpProtocol.Grpc;
                options.ResourceAttributes = new Dictionary<string, object>(StringComparer.Ordinal)
                {
                    ["service.name"] = "meepleai-api",
                    ["meepleai.profile"] = OtlpExport.ProfileName(configuration),
                };
            });
        }
```
con `using Api.Observability;`.

- [ ] **Step 4: Test e build**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~OtlpExportTests"` → Expected: 5 superati.
Run: `dotnet build src/Api` → Expected: verde (nessun warning nuovo).

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Observability/OtlpExport.cs apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs apps/api/src/Api/Logging/LoggingConfiguration.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Observability/OtlpExportTests.cs
git commit -m "feat(observability): export OTLP di trace, metriche e log" -m "Attivo solo con OTEL_EXPORTER_OTLP_ENDPOINT; Prometheus e Seq invariati." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: Bicep in `infra/azure/`, workflow CI, emendamenti alla spec e checklist manuale

**Files:**
- Create: `infra/azure/main.bicep`, `infra/azure/main.bicepparam`, `infra/azure/modules/storage.bicep`, `modules/servicebus.bicep`, `modules/keyvault.bicep`, `modules/appconfig.bicep`, `modules/monitoring.bicep`, `modules/postgres.bicep`, `modules/redis.bicep`, `modules/containerapps.bicep`, `modules/functions.bicep`
- Create: `infra/azure/azure.yaml`
- Create: `.github/workflows/azure-line.yml`
- Modify: `docs/for-developers/specs/2026-10-09-azure-local-profile-design.md` (§2 Key Vault, §4.2, §4.5, §7)
- Modify: `docs/for-developers/operations/azure-local-profile.md` (sezione «Verifica manuale»)
- Modify: `CLAUDE.md` (riga nella tabella Quick Reference)

**Interfaces:**
- Consumes: nomi di coda/container dei task precedenti (`pdf-uploaded`, `meepleai-uploads`).
- Produces: workflow `azure-line.yml` (job `bicep`, `functions-tests`).

- [ ] **Step 1: Bicep CLI locale**

Run (Git Bash, nello scratchpad della sessione): `curl -sSL -o bicep.exe https://github.com/Azure/bicep/releases/latest/download/bicep-win-x64.exe && ./bicep.exe --version`
Expected: stampa la versione. Usare `./bicep.exe` (percorso assoluto dello scratchpad) nei passi seguenti; il binario non entra nel repo.

- [ ] **Step 2: Moduli Bicep**

`infra/azure/main.bicep`:
```bicep
targetScope = 'resourceGroup'

@description('Prefisso dei nomi (minuscole, max 10).')
@minLength(3)
@maxLength(10)
param namePrefix string = 'meepleai'

@description('Regione.')
param location string = resourceGroup().location

@description('Profilo applicativo iniettato nell API (MEEPLEAI_PROFILE).')
param profile string = 'azure-exam'

@secure()
@description('Password dell amministratore Postgres.')
param postgresAdminPassword string

@description('Immagine dell API (registry/repo:tag).')
param apiImage string

@description('Immagine del web (registry/repo:tag).')
param webImage string

var suffix = uniqueString(resourceGroup().id)

module monitoring 'modules/monitoring.bicep' = {
  name: 'monitoring'
  params: { namePrefix: namePrefix, location: location }
}

module storage 'modules/storage.bicep' = {
  name: 'storage'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module servicebus 'modules/servicebus.bicep' = {
  name: 'servicebus'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module keyvault 'modules/keyvault.bicep' = {
  name: 'keyvault'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module appconfig 'modules/appconfig.bicep' = {
  name: 'appconfig'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module postgres 'modules/postgres.bicep' = {
  name: 'postgres'
  params: { namePrefix: namePrefix, suffix: suffix, location: location, adminPassword: postgresAdminPassword }
}

module redis 'modules/redis.bicep' = {
  name: 'redis'
  params: { namePrefix: namePrefix, suffix: suffix, location: location }
}

module containerapps 'modules/containerapps.bicep' = {
  name: 'containerapps'
  params: {
    namePrefix: namePrefix
    location: location
    profile: profile
    apiImage: apiImage
    webImage: webImage
    logAnalyticsWorkspaceId: monitoring.outputs.workspaceId
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    storageAccountBlobEndpoint: storage.outputs.blobEndpoint
    serviceBusNamespace: servicebus.outputs.namespaceName
    keyVaultUri: keyvault.outputs.vaultUri
    appConfigEndpoint: appconfig.outputs.endpoint
  }
}

module functions 'modules/functions.bicep' = {
  name: 'functions'
  params: {
    namePrefix: namePrefix
    suffix: suffix
    location: location
    storageAccountName: storage.outputs.accountName
    serviceBusNamespace: servicebus.outputs.namespaceName
    appInsightsConnectionString: monitoring.outputs.appInsightsConnectionString
    apiBaseUrl: containerapps.outputs.apiUrl
  }
}

output apiUrl string = containerapps.outputs.apiUrl
output webUrl string = containerapps.outputs.webUrl
output keyVaultUri string = keyvault.outputs.vaultUri
output appConfigEndpoint string = appconfig.outputs.endpoint
```

`modules/monitoring.bicep`:
```bicep
param namePrefix string
param location string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' = {
  name: '${namePrefix}-logs'
  location: location
  properties: { sku: { name: 'PerGB2018' }, retentionInDays: 30 }
}

resource appInsights 'Microsoft.Insights/components@2020-02-02' = {
  name: '${namePrefix}-insights'
  location: location
  kind: 'web'
  properties: { Application_Type: 'web', WorkspaceResourceId: workspace.id }
}

output workspaceId string = workspace.id
output appInsightsConnectionString string = appInsights.properties.ConnectionString
```

`modules/storage.bicep`:
```bicep
param namePrefix string
param suffix string
param location string

resource account 'Microsoft.Storage/storageAccounts@2023-05-01' = {
  name: toLower('${namePrefix}st${suffix}')
  location: location
  sku: { name: 'Standard_LRS' }
  kind: 'StorageV2'
  properties: { allowBlobPublicAccess: false, minimumTlsVersion: 'TLS1_2', supportsHttpsTrafficOnly: true }
}

resource blobService 'Microsoft.Storage/storageAccounts/blobServices@2023-05-01' = {
  parent: account
  name: 'default'
}

resource uploads 'Microsoft.Storage/storageAccounts/blobServices/containers@2023-05-01' = {
  parent: blobService
  name: 'meepleai-uploads'
  properties: { publicAccess: 'None' }
}

output accountName string = account.name
output blobEndpoint string = account.properties.primaryEndpoints.blob
```

`modules/servicebus.bicep`:
```bicep
param namePrefix string
param suffix string
param location string

resource ns 'Microsoft.ServiceBus/namespaces@2024-01-01' = {
  name: '${namePrefix}-sb-${suffix}'
  location: location
  sku: { name: 'Basic', tier: 'Basic' }
}

resource pdfUploaded 'Microsoft.ServiceBus/namespaces/queues@2024-01-01' = {
  parent: ns
  name: 'pdf-uploaded'
  properties: {
    lockDuration: 'PT1M'
    maxDeliveryCount: 3
    deadLetteringOnMessageExpiration: true
    defaultMessageTimeToLive: 'P1D'
  }
}

output namespaceName string = ns.name
output queueName string = pdfUploaded.name
```

`modules/keyvault.bicep`:
```bicep
param namePrefix string
param suffix string
param location string

resource vault 'Microsoft.KeyVault/vaults@2023-07-01' = {
  name: '${namePrefix}-kv-${take(suffix, 8)}'
  location: location
  properties: {
    tenantId: subscription().tenantId
    sku: { family: 'A', name: 'standard' }
    enableRbacAuthorization: true
    enableSoftDelete: true
    softDeleteRetentionInDays: 7
  }
}

output vaultUri string = vault.properties.vaultUri
```

`modules/appconfig.bicep`:
```bicep
param namePrefix string
param suffix string
param location string

resource store 'Microsoft.AppConfiguration/configurationStores@2024-05-01' = {
  name: '${namePrefix}-appconfig-${suffix}'
  location: location
  sku: { name: 'free' }
  properties: { disableLocalAuth: false }
}

output endpoint string = store.properties.endpoint
```

`modules/postgres.bicep`:
```bicep
param namePrefix string
param suffix string
param location string
@secure()
param adminPassword string

resource server 'Microsoft.DBforPostgreSQL/flexibleServers@2024-08-01' = {
  name: '${namePrefix}-pg-${suffix}'
  location: location
  sku: { name: 'Standard_B1ms', tier: 'Burstable' }
  properties: {
    version: '16'
    administratorLogin: 'meepleai'
    administratorLoginPassword: adminPassword
    storage: { storageSizeGB: 32 }
    backup: { backupRetentionDays: 7, geoRedundantBackup: 'Disabled' }
    highAvailability: { mode: 'Disabled' }
  }
}

resource extensions 'Microsoft.DBforPostgreSQL/flexibleServers/configurations@2024-08-01' = {
  parent: server
  name: 'azure.extensions'
  properties: { value: 'VECTOR,PG_DISKANN,PG_TRGM', source: 'user-override' }
}

resource database 'Microsoft.DBforPostgreSQL/flexibleServers/databases@2024-08-01' = {
  parent: server
  name: 'meepleai'
}

output serverFqdn string = server.properties.fullyQualifiedDomainName
```

`modules/redis.bicep`:
```bicep
param namePrefix string
param suffix string
param location string

resource redis 'Microsoft.Cache/redisEnterprise@2024-10-01' = {
  name: '${namePrefix}-redis-${suffix}'
  location: location
  sku: { name: 'Balanced_B0' }
  properties: { minimumTlsVersion: '1.2' }
}

resource db 'Microsoft.Cache/redisEnterprise/databases@2024-10-01' = {
  parent: redis
  name: 'default'
  properties: { clientProtocol: 'Encrypted', port: 10000, evictionPolicy: 'VolatileLRU', clusteringPolicy: 'EnterpriseCluster' }
}

output hostName string = redis.properties.hostName
```

`modules/containerapps.bicep`:
```bicep
param namePrefix string
param location string
param profile string
param apiImage string
param webImage string
param logAnalyticsWorkspaceId string
param appInsightsConnectionString string
param storageAccountBlobEndpoint string
param serviceBusNamespace string
param keyVaultUri string
param appConfigEndpoint string

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-cae'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: reference(logAnalyticsWorkspaceId, '2023-09-01').customerId
        sharedKey: listKeys(logAnalyticsWorkspaceId, '2023-09-01').primarySharedKey
      }
    }
  }
}

resource api 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-api'
  location: location
  identity: { type: 'SystemAssigned' }
  properties: {
    managedEnvironmentId: environment.id
    configuration: {
      ingress: { external: true, targetPort: 8080, transport: 'http' }
    }
    template: {
      containers: [
        {
          name: 'api'
          image: apiImage
          resources: { cpu: json('0.5'), memory: '1Gi' }
          env: [
            { name: 'MEEPLEAI_PROFILE', value: profile }
            { name: 'STORAGE_PROVIDER', value: 'azureblob' }
            { name: 'AZURE_STORAGE_ACCOUNT_URL', value: storageAccountBlobEndpoint }
            { name: 'AZURE_STORAGE_CONTAINER', value: 'meepleai-uploads' }
            { name: 'AZURE_SERVICEBUS_QUEUE', value: 'pdf-uploaded' }
            { name: 'AZURE_SERVICEBUS_NAMESPACE', value: '${serviceBusNamespace}.servicebus.windows.net' }
            { name: 'AZURE_KEYVAULT_URI', value: keyVaultUri }
            { name: 'AZURE_APPCONFIG_ENDPOINT', value: appConfigEndpoint }
            { name: 'Azure__Sdk__AllowedHostSuffixes__0', value: '.blob.core.windows.net' }
            { name: 'Azure__Sdk__AllowedHostSuffixes__1', value: '.vault.azure.net' }
            { name: 'Azure__Sdk__AllowedHostSuffixes__2', value: '.azconfig.io' }
            { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
          ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 2 }
    }
  }
}

resource web 'Microsoft.App/containerApps@2024-03-01' = {
  name: '${namePrefix}-web'
  location: location
  properties: {
    managedEnvironmentId: environment.id
    configuration: { ingress: { external: true, targetPort: 3000, transport: 'http' } }
    template: {
      containers: [
        {
          name: 'web'
          image: webImage
          resources: { cpu: json('0.25'), memory: '0.5Gi' }
          env: [ { name: 'API_BASE_URL', value: 'https://${api.properties.configuration.ingress.fqdn}' } ]
        }
      ]
      scale: { minReplicas: 0, maxReplicas: 1 }
    }
  }
}

output apiUrl string = 'https://${api.properties.configuration.ingress.fqdn}'
output webUrl string = 'https://${web.properties.configuration.ingress.fqdn}'
output apiPrincipalId string = api.identity.principalId
```

`modules/functions.bicep`:
```bicep
param namePrefix string
param suffix string
param location string
param storageAccountName string
param serviceBusNamespace string
param appInsightsConnectionString string
param apiBaseUrl string

resource plan 'Microsoft.Web/serverfarms@2023-12-01' = {
  name: '${namePrefix}-func-plan'
  location: location
  kind: 'functionapp'
  sku: { tier: 'FlexConsumption', name: 'FC1' }
  properties: { reserved: true }
}

resource app 'Microsoft.Web/sites@2023-12-01' = {
  name: '${namePrefix}-func-${suffix}'
  location: location
  kind: 'functionapp,linux'
  identity: { type: 'SystemAssigned' }
  properties: {
    serverFarmId: plan.id
    functionAppConfig: {
      deployment: {
        storage: {
          type: 'blobContainer'
          value: 'https://${storageAccountName}.blob.${environment().suffixes.storage}/function-releases'
          authentication: { type: 'SystemAssignedIdentity' }
        }
      }
      runtime: { name: 'dotnet-isolated', version: '9.0' }
      scaleAndConcurrency: { maximumInstanceCount: 40, instanceMemoryMB: 2048 }
    }
    siteConfig: {
      appSettings: [
        { name: 'AzureWebJobsStorage__accountName', value: storageAccountName }
        { name: 'APPLICATIONINSIGHTS_CONNECTION_STRING', value: appInsightsConnectionString }
        { name: 'ServiceBusConnection__fullyQualifiedNamespace', value: '${serviceBusNamespace}.servicebus.windows.net' }
        { name: 'ServiceBusQueue', value: 'pdf-uploaded' }
        { name: 'MeepleAiApiBaseUrl', value: apiBaseUrl }
      ]
    }
  }
}

output principalId string = app.identity.principalId
```

`infra/azure/main.bicepparam`:
```bicep
using './main.bicep'

param namePrefix = 'meepleai'
param profile = 'azure-exam'
param apiImage = 'ghcr.io/meepleai-app/meepleai-monorepo/api:latest'
param webImage = 'ghcr.io/meepleai-app/meepleai-monorepo/web:latest'
param postgresAdminPassword = readEnvironmentVariable('POSTGRES_ADMIN_PASSWORD', 'replace-me-at-provision-time')
```

`infra/azure/azure.yaml`:
```yaml
# azd: mai eseguito in CI (solo `bicep build`/`lint`). Per l'esercizio d'esame: `azd provision` manuale.
name: meepleai
infra:
  provider: bicep
  path: .
  module: main
services:
  api:
    project: ../../apps/api/src/Api
    host: containerapp
    language: dotnet
  web:
    project: ../../apps/web
    host: containerapp
    language: js
  functions:
    project: ../../apps/functions/MeepleAI.Functions
    host: function
    language: dotnet
```

- [ ] **Step 3: Validare in locale**

Run (da `infra/azure`): `for f in main.bicep modules/*.bicep; do <scratchpad>/bicep.exe build "$f" --stdout > /dev/null && <scratchpad>/bicep.exe lint "$f"; done`
Expected: nessun errore; i warning `BCP081` (versione API non nota al CLI) sono ammessi, qualsiasi altro warning va risolto. Rimuovere gli eventuali `.json` prodotti accanto ai `.bicep` (`git status` deve mostrare solo i sorgenti).

- [ ] **Step 4: Workflow** `.github/workflows/azure-line.yml`

```yaml
name: Azure Line

on:
  pull_request:
    paths:
      - 'infra/azure/**'
      - 'apps/functions/**'
      - 'tests/fixtures/contracts/**'
      - '.github/workflows/azure-line.yml'
  workflow_dispatch:

concurrency:
  group: azure-line-${{ github.ref }}
  cancel-in-progress: true

permissions:
  contents: read

jobs:
  bicep:
    name: Bicep build + lint (never deploys)
    runs-on: ubuntu-latest
    timeout-minutes: 10
    steps:
      - uses: actions/checkout@v6
      - name: Install Bicep CLI
        run: |
          set -euo pipefail
          curl -sSL -o bicep https://github.com/Azure/bicep/releases/latest/download/bicep-linux-x64
          chmod +x bicep && sudo mv bicep /usr/local/bin/bicep
          bicep --version
      - name: Build and lint every template
        working-directory: infra/azure
        run: |
          set -euo pipefail
          status=0
          for f in main.bicep modules/*.bicep; do
            echo "== $f"
            bicep build "$f" --stdout > /dev/null || status=1
            bicep lint "$f" || status=1
          done
          exit $status

  functions-tests:
    name: Azure Functions unit tests
    runs-on: ubuntu-latest
    timeout-minutes: 15
    steps:
      - uses: actions/checkout@v6
      - uses: actions/setup-dotnet@v5
        with:
          dotnet-version: '9.0.x'
      - name: Test
        working-directory: apps/functions
        run: dotnet test --configuration Release --logger "trx;LogFileName=functions.trx"
      - uses: actions/upload-artifact@v4
        if: ${{ !cancelled() }}
        with:
          name: functions-test-results
          path: apps/functions/**/TestResults/*.trx
```
Verificare la major di `actions/setup-dotnet` usata altrove (`grep -rn "setup-dotnet@" .github/workflows/*.yml | head -1`) e allinearla.

- [ ] **Step 5: Emendamenti alla spec** (`docs/for-developers/specs/2026-10-09-azure-local-profile-design.md`)

- §2, riga Key Vault: sostituire la nota con «**opt-in** (profilo compose `azure-keyvault`, `make dev-azure-local-keyvault`): richiede certificati generati sull'host con `docs/setup.sh` del progetto; non parte con `make dev-azure-local`».
- §4.2: aggiungere «Le sorgenti girano prima del container DI, quindi non passano dal client pinnato: endpoint da configurazione statica, come le esenzioni OpenRouter/Resend del gate. Per gli emulatori anonimi una `StaticTokenCredential` sostituisce `DefaultAzureCredential` (`AZURE_APPCONFIG_ANONYMOUS`/`AZURE_KEYVAULT_ANONYMOUS`).»
- §4.5: riscrivere il primo punto: «Due client nominati: `AzureSdk` pinnato (in `MustBePinned`, suffissi da `Azure:Sdk:AllowedHostSuffixes`, rifiuta di partire senza lista) e `AzureSdkLocal` esente (solo `MEEPLEAI_PROFILE=azure-local`, perché `SsrfPinnedConnect` blocca ogni IP privato e gli emulatori stanno sulla rete compose). Service Bus parla AMQP e non passa da nessuno dei due.»
- §7: la riga Key Vault diventa «Chiavi lette da App Configuration emulator (test di integrazione); Key Vault: sorgente implementata, verificata con il profilo opt-in nella checklist manuale».

- [ ] **Step 6: Checklist manuale** in `docs/for-developers/operations/azure-local-profile.md` (sezione «Verifica manuale», ogni riga con il comando o l'URL)

1. `cd infra && make dev-azure-local` → `curl -fsS http://localhost:8080/api/v1/health`.
2. Login admin, upload di un PDF dalla UI → `docker logs meepleai-api | grep "Stored blob"` mostra la chiave `pdfs/<game>/<fileId>_<nome>.pdf`; `az storage blob list --connection-string "<la connection string di compose.azure-local.yml, con BlobEndpoint=http://localhost:20000/devstoreaccount1>" -c meepleai-uploads -o table` elenca lo stesso blob.
3. `curl -fsS http://localhost:20003/kv?api-version=2023-11-01` mostra le chiavi di `appconfig-seed.json`; `docker logs meepleai-api | grep -i "AppConfiguration"` non riporta errori.
4. `cd apps/functions/MeepleAI.Functions && cp local.settings.json.example local.settings.json && func start` (Azure Functions Core Tools v4) → dopo un upload il log della Function stampa `forwarded: 200` oppure `202`; `curl -s http://localhost:8080/metrics | grep meepleai_pdf_uploaded_events_total`.
5. Messaggio malformato: con il Service Bus Explorer di VS Code (connection string host `Endpoint=sb://localhost:20004;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`) inviare il corpo `{"contractVersion":"v9"}` sulla coda `pdf-uploaded`; il log della Function stampa `dead-lettered: unsupported_contract` e il messaggio compare nella dead-letter della coda.
6. `http://localhost:20009` (Aspire Dashboard): la richiesta a `/api/v1/health` appare in Traces, le metriche `meepleai.*` in Metrics, i log in Structured logs.
7. `make dev-azure-local-down`; `docker ps --filter name=meepleai-` vuoto per i servizi del profilo.

- [ ] **Step 7: Riga in CLAUDE.md** (tabella Quick Reference, dopo «Dev from Snapshot»)

```markdown
| Dev Azure emulators | `make dev-azure-local` | `infra/` — [guide](./docs/for-developers/operations/azure-local-profile.md) |
```

- [ ] **Step 8: Commit**

```bash
git add infra/azure .github/workflows/azure-line.yml docs/for-developers/specs/2026-10-09-azure-local-profile-design.md docs/for-developers/operations/azure-local-profile.md CLAUDE.md
git commit -m "feat(infra): Bicep per il profilo Azure, gate CI azure-line, checklist" -m "bicep build/lint su ogni PR che tocca infra/azure o apps/functions; mai un deploy. Spec emendata: Key Vault opt-in, due client SDK, sorgenti pre-DI." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Chiusura

Dopo il Task 10: suite unit backend completa (`dotnet test tests/Api.Tests --filter "Category=Unit"`), `ArchitectureTests`, `pnpm typecheck && pnpm lint` in `apps/web` (nessun file web toccato: deve restare verde), `make dev-azure-local-config`. Poi `superpowers:finishing-a-development-branch` con base `azure/main`; aprire le issue di follow-up per il debito `IAmazonS3` (spec §4.1) e per la seconda iterazione (Cosmos per ConversationMemory, Redis vettoriale, multi-arch, KEDA/AKS).
