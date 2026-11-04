# Profilo `azure-local` — Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** far girare l'API MeepleAI con gli SDK Azure veri (Blob, App Configuration, Key Vault, Service Bus, OTLP) contro emulatori in Docker, senza un conto Azure, con un consumer Azure Functions e l'infrastruttura Bicep validata in CI.

**Architecture:** ogni servizio Azure entra dietro un'astrazione già esistente (`IBlobStorageService`, `IConfiguration`, OpenTelemetry) o una nuova e minima (`IIntegrationEventPublisher`); un profilo compose `azure-local` avvia gli emulatori e inietta le variabili; il profilo attuale resta byte-identico quando le variabili `AZURE_*`/`OTEL_*` sono assenti. Gli SDK HTTP passano da due `HttpClient` nominati (uno pinnato per Azure vero, uno esente per la rete compose), scelti a runtime dal profilo.

**Tech Stack:** .NET 9, Azure.Storage.Blobs 12.30.1, Azure.Messaging.ServiceBus 7.21.0, Azure.Identity 1.21.0, Azure.Extensions.AspNetCore.Configuration.Secrets 1.5.2, Microsoft.Azure.AppConfiguration.AspNetCore 8.6.0, OpenTelemetry.Exporter.OpenTelemetryProtocol 1.15.3, Serilog.Sinks.OpenTelemetry 4.2.0, Testcontainers.Azurite/ServiceBus 4.14.0, Azure Functions isolated worker 2.52.0 + Extensions.ServiceBus 5.24.0 + Sdk 2.1.0, Bicep CLI v0.48.1.

**Spec:** `docs/for-developers/specs/2026-10-09-azure-local-profile-design.md` · note di ricerca: `docs/superpowers/plans/2026-10-09-azure-local-profile-plan-notes.md` · revisione indipendente del piano v1 (38 rilievi, tutti recepiti o registrati nel ledger): scratchpad `azure-plan-review.md`

## Global Constraints

- Worktree `D:/Repositories/meepleai-monorepo-azure`, branch `feature/azure-1-local-profile-spec` (parent `azure/main`). Il worktree ha `.git` come **file**: i gate di architettura che cercano la radice con `Directory.Exists(".git")` falliscono finché il Task 0 non li corregge — il Task 0 è prerequisito di ogni «Expected: verde» successivo.
- Comandi `dotnet` da `apps/api` salvo diversa indicazione; prima di ogni `dotnet test`: `taskkill //IM testhost.exe //F 2>/dev/null || true`. Mai due `dotnet test` in parallelo.
- Versioni NuGet **esattamente** quelle del Tech Stack; `OpenTelemetry.Exporter.OpenTelemetryProtocol` DEVE essere 1.15.3. Pacchetti di test: le versioni del repo (`Microsoft.NET.Test.Sdk` 18.0.1, `xunit.v3` 3.2.1, `xunit.runner.visualstudio` 3.1.5, `FluentAssertions` 8.8.0).
- `TreatWarningsAsErrors`: nessun `// TODO` (S1135 — usare `// Follow-up:`), `ConfigureAwait(false)` nel codice di libreria, `StringComparison` esplicito, nessun parametro inutilizzato.
- Namespace nuovo `Api.Infrastructure.AzureSdk` (NON `Api.Infrastructure.Azure`: ombreggerebbe la radice `Azure` degli SDK dentro `Api.Infrastructure.*`).
- Layering DDD: Application non importa Infrastructure; i contratti condivisi stanno in `SharedKernel`.
- Ogni `AddHttpClient(` nel sorgente dell'API va in `MustBePinned` (con `ConfigureSsrfPin` nella STESSA statement) o in `Exempt` di `EgressHttpClientPinArchitectureTests.cs`; nome del client come **stringa letterale**. Ogni nuovo `CryptographicOperations.FixedTimeEquals` va nell'inventario `ConstantTimeSites` di `ConstantTimeComparisonArchitectureTests.cs`.
- Variabili assenti o vuote disattivano una funzionalità (`string.IsNullOrWhiteSpace`), mai `GetValue<bool>` su una variabile che può essere vuota.
- Profilo attuale invariato: senza `STORAGE_PROVIDER=azureblob`, `AZURE_*`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `INTERNAL_SERVICE_TOKEN` nessuna registrazione nuova cambia comportamento.
- Test: `[Trait("Category", TestCategories.Unit)]` per gli unit; gli emulatori sono L3 ⇒ con Docker assente `Assert.Skip("PREVISTO: … cd infra && make dev-azure-local …")`, con Docker presente ma container non avviato `Assert.Skip("GUASTO: <errore osservato>")`; mai `L1Services.FailBecauseUnavailable`. Un container per CLASSE (`IClassFixture`), mai per metodo: `methodTimeout` 180 s copre `InitializeAsync`.
- Mai `docker compose` a mano: target del Makefile. Nessun `.secret` committato; i valori degli emulatori sono pubblici e stanno nel compose con default `${VAR:-…}`.
- Commit: `feat|fix|docs|test|chore(scope): descrizione` ≤72 caratteri, trailer `Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>`, un commit per task con i file elencati uno per uno (mai una cartella intera dei test).
- Nessun conteggio volatile nella prosa dei documenti.

## Review Focus

1. `STORAGE_PROVIDER=azureblob` senza `AZURE_STORAGE_CONNECTION_STRING` né `AZURE_STORAGE_ACCOUNT_URL`: `InvalidOperationException` con i due nomi — Task 2 `Create_AzureBlobWithoutEndpoint_Throws`.
2. `resourceKey`/`fileId` con traversal su `StoreAsync`, `RetrieveAsync`, `DeleteAsync`, `ExistsAsync`: fallimento senza toccare il container — Task 2 `StoreAsync_TraversalResourceKey_FailsWithoutUpload`, `RetrieveAsync_TraversalFileId_ReturnsNullWithoutListing`, `DeleteAsync_TraversalFileId_ReturnsFalseWithoutListing`, `ExistsAsync_TraversalFileId_ReturnsFalse`.
3. Messaggio Service Bus con `contractVersion` sconosciuta o JSON malformato: dead-letter con motivo, API mai chiamata — Task 8 `Run_UnsupportedContract_DeadLettersWithoutForwarding`, `Run_MalformedBody_DeadLettersAsMalformed`.
4. `INTERNAL_SERVICE_TOKEN` assente o vuoto: 404 per tutti — Task 6 `InvokeAsync_TokenNotConfigured_Returns404`.
5. Bus irraggiungibile durante un upload: l'upload riesce e il contatore registra `publish_failed` — Task 5 `Handle_PublisherThrows_SwallowsAndCountsFailure` (asserisce la misura via `MeterListener`).

---

### Task 0: Radice del repo riconosciuta anche nei worktree (gate di architettura)

**Files:**
- Modify: `apps/api/tests/Api.Tests/Architecture/SourceScanner.cs`
- Modify (sostituzione meccanica di una riga ciascuno): `apps/api/tests/Api.Tests/Architecture/ConstantTimeComparisonArchitectureTests.cs`, `EgressHttpClientPinArchitectureTests.cs`, `L1NeverSkippedArchitectureTests.cs`, `NonCriticalHealthCheckStatusArchitectureTests.cs`, `PaidAiGuardCoverageArchitectureTests.cs`, `PerClassHostFixtureBreakEvenArchitectureTests.cs`, `SkipReasonClassArchitectureTests.cs`, `TestSeedPersistenceArchitectureTests.cs`, `apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Architecture/VersionDriftPreventionTests.cs`, `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/OllamaChatModelConfigurationTests.cs`, `apps/api/tests/Api.Tests/E2E/Infrastructure/E2EServiceProbeTests.cs`, `apps/api/tests/Api.Tests/Infrastructure/PdfCorpus.cs`
- Test: `apps/api/tests/Api.Tests/Architecture/SourceScannerRepoRootTests.cs`

**Interfaces:**
- Produces: `SourceScanner.IsRepoRoot(string path) : bool` (vero se `path/.git` è una directory O un file).

- [ ] **Step 1: Test**

```csharp
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Architecture;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Architecture")]
public sealed class SourceScannerRepoRootTests : IDisposable
{
    private readonly string _root = Path.Combine(Path.GetTempPath(), "repo-root-" + Guid.NewGuid().ToString("N"));

    public SourceScannerRepoRootTests() => Directory.CreateDirectory(_root);

    public void Dispose() => Directory.Delete(_root, recursive: true);

    [Fact]
    public void IsRepoRoot_GitDirectory_IsTrue()
    {
        Directory.CreateDirectory(Path.Combine(_root, ".git"));

        SourceScanner.IsRepoRoot(_root).Should().BeTrue();
    }

    [Fact]
    public void IsRepoRoot_GitFile_IsTrue()
    {
        // Un worktree (`git worktree add`) ha `.git` come FILE: "gitdir: <path>".
        File.WriteAllText(Path.Combine(_root, ".git"), "gitdir: D:/somewhere/.git/worktrees/x\n");

        SourceScanner.IsRepoRoot(_root).Should().BeTrue();
    }

    [Fact]
    public void IsRepoRoot_NoGit_IsFalse()
    {
        SourceScanner.IsRepoRoot(_root).Should().BeFalse();
    }
}
```

- [ ] **Step 2: Eseguire per vedere il fallimento di compilazione**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~SourceScannerRepoRootTests"`

- [ ] **Step 3: Implementare e sostituire i dodici locator**

In `SourceScanner.cs` (classe `internal static class SourceScanner`), aggiungere:
```csharp
    /// <summary>
    /// Vero se <paramref name="path"/> è la radice del repo: contiene <c>.git</c> come directory (checkout
    /// normale) oppure come file (worktree creato con <c>git worktree add</c>, che scrive "gitdir: …").
    /// I gate che risalivano con <c>Directory.Exists(".git")</c> non trovavano mai la radice in un worktree.
    /// </summary>
    public static bool IsRepoRoot(string path) =>
        Directory.Exists(Path.Combine(path, ".git")) || File.Exists(Path.Combine(path, ".git"));
```

Nei dodici file elencati sostituire la riga
```csharp
        while (dir is not null && !Directory.Exists(Path.Combine(dir.FullName, ".git")))
```
con
```csharp
        while (dir is not null && !SourceScanner.IsRepoRoot(dir.FullName))
```
(aggiungendo `using Api.Tests.Architecture;` dove il namespace del file è diverso). Verifica: `grep -rn 'Path.Combine(dir.FullName, ".git")' apps/api/tests/Api.Tests` → nessuna occorrenza; `grep -rln "SourceScanner.IsRepoRoot" apps/api/tests/Api.Tests | wc -l` → 13 (12 + SourceScanner stesso) oppure 12 se lo scanner non si auto-usa.

- [ ] **Step 4: Eseguire tutti i gate di architettura NEL worktree**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~Architecture"`
Expected: tutti verdi (prima del task fallivano con «the test binary must live inside the meepleai-monorepo repo»).

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/Architecture/SourceScanner.cs apps/api/tests/Api.Tests/Architecture/SourceScannerRepoRootTests.cs apps/api/tests/Api.Tests/Architecture/ConstantTimeComparisonArchitectureTests.cs apps/api/tests/Api.Tests/Architecture/EgressHttpClientPinArchitectureTests.cs apps/api/tests/Api.Tests/Architecture/L1NeverSkippedArchitectureTests.cs apps/api/tests/Api.Tests/Architecture/NonCriticalHealthCheckStatusArchitectureTests.cs apps/api/tests/Api.Tests/Architecture/PaidAiGuardCoverageArchitectureTests.cs apps/api/tests/Api.Tests/Architecture/PerClassHostFixtureBreakEvenArchitectureTests.cs apps/api/tests/Api.Tests/Architecture/SkipReasonClassArchitectureTests.cs apps/api/tests/Api.Tests/Architecture/TestSeedPersistenceArchitectureTests.cs apps/api/tests/Api.Tests/BoundedContexts/GameToolkit/Architecture/VersionDriftPreventionTests.cs apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/OllamaChatModelConfigurationTests.cs apps/api/tests/Api.Tests/E2E/Infrastructure/E2EServiceProbeTests.cs apps/api/tests/Api.Tests/Infrastructure/PdfCorpus.cs
git commit -m "test(arch): i gate trovano la radice del repo anche in un worktree" -m ".git è un file nei worktree: dodici locator usavano Directory.Exists e non risalivano mai alla radice." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 1: Profilo compose `azure-local`, target Makefile, file di configurazione degli emulatori

**Files:**
- Create: `infra/compose.azure-local.yml`
- Create: `infra/azure-local/servicebus-config.json`
- Create: `infra/azure-local/appconfig-seed.json`
- Create: `infra/azure-local/eventgrid-appsettings.json`
- Create: `infra/azure-local/wait-and-seed.sh`
- Create: `docs/for-developers/operations/azure-local-profile.md`
- Modify: `infra/Makefile` (dopo il target `dev-down`, riga 18)
- Modify: `infra/.gitignore` (esiste già: pattern relativi a `infra/`)

**Interfaces:**
- Produces: variabili lette dai task successivi — `MEEPLEAI_PROFILE=azure-local`, `STORAGE_PROVIDER=azureblob`, `AZURE_STORAGE_CONNECTION_STRING`, `AZURE_STORAGE_CONTAINER`, `AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT`, `AZURE_APPCONFIG_CONNECTION_STRING`, `AZURE_KEYVAULT_URI`, `AZURE_KEYVAULT_ANONYMOUS`, `AZURE_SDK_ALLOW_INSECURE_TLS`, `AZURE_SERVICEBUS_CONNECTION_STRING`, `AZURE_SERVICEBUS_QUEUE`, `OTEL_EXPORTER_OTLP_ENDPOINT`, `OTEL_EXPORTER_OTLP_PROTOCOL`, `INTERNAL_SERVICE_TOKEN`; servizi compose `azurite`, `appconfig-emulator`, `servicebus-emulator`, `servicebus-mssql`, `azure-local-init`, `cosmos-emulator`, `aspire-dashboard`, `eventgrid-emulator`, `keyvault-emulator` (profilo `azure-keyvault`); credenziali HMAC dell'App Configuration emulator `Id=azure-local-dev`, `Secret=YXp1cmUtbG9jYWwtZGV2LXNlY3JldC0wMTIzNDU2Nzg5YWJjZGVm` (base64 di `azure-local-dev-secret-0123456789abcdef`, valore pubblico di sviluppo).

- [ ] **Step 1: `infra/azure-local/servicebus-config.json`**

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

- [ ] **Step 2: `infra/azure-local/appconfig-seed.json`** (solo una chiave di prova: le sorgenti Azure vengono aggiunte DOPO le variabili d'ambiente e **vincono** su di esse per le chiavi coincidenti — non seminare chiavi reali dell'API)

```json
{
  "Azure:Profile:Greeting": "hello from app-configuration-emulator"
}
```

- [ ] **Step 3: `infra/azure-local/eventgrid-appsettings.json`**

```json
{
  "Topics": {
    "pdf-events": [ "pull://smoke-subscription" ]
  }
}
```

- [ ] **Step 4: `infra/azure-local/wait-and-seed.sh`** (gira nel container `azure-local-init`, immagine `curlimages/curl`: le immagini .NET degli emulatori non portano `curl`/`wget`, quindi la readiness si sonda da fuori)

```sh
#!/bin/sh
# Attende gli emulatori e semina l'App Configuration emulator. Esce 0 solo quando tutto e' pronto:
# `api` dipende da questo container con condition: service_completed_successfully.
set -eu

wait_http() { # $1 url, $2 descrizione, $3 accetta-qualsiasi-status (1) o solo 2xx (0)
  i=0
  while [ "$i" -lt 120 ]; do
    if [ "$3" = "1" ]; then
      curl -s -o /dev/null "$1" && { echo "ready: $2"; return 0; }
    else
      curl -fsS -o /dev/null "$1" && { echo "ready: $2"; return 0; }
    fi
    i=$((i + 1)); sleep 2
  done
  echo "timeout waiting for $2 ($1)" >&2; return 1
}

wait_http "http://azurite:10000/" "azurite blob" 1          # risponde 400 senza auth: basta la connessione
wait_http "http://appconfig-emulator:8483/health" "app configuration emulator" 0
wait_http "http://servicebus-emulator:5300/health" "service bus emulator" 0

# Seed: un PUT anonimo per chiave (api-version 2023-11-01); il file e' una mappa piatta chiave->valore.
sed -n 's/^ *"\([^"]*\)": *"\([^"]*\)".*/\1\t\2/p' /seed.json | while IFS="$(printf '\t')" read -r key value; do
  encoded=$(printf '%s' "$key" | sed 's/:/%3A/g')
  curl -fsS -o /dev/null -X PUT "http://appconfig-emulator:8483/kv/$encoded?api-version=2023-11-01" \
    -H "Content-Type: application/vnd.microsoft.appconfig.kv+json" \
    -d "{\"value\":\"$value\"}"
  echo "seeded $key"
done
```

- [ ] **Step 5: `infra/compose.azure-local.yml`**

```yaml
# Profilo `azure-local` (spec docs/for-developers/specs/2026-10-09-azure-local-profile-design.md).
# Composto SOPRA docker-compose.yml + compose.dev.yml SOLO dai target `make dev-azure-local*`.
# Avvia gli emulatori Azure sulla rete compose e punta l'API a loro con gli SDK Azure veri. Senza
# questo file il profilo attuale e' byte-identico: ogni funzionalita' e' gated sulle variabili AZURE_*.
#
# Porte sull'host: AZURE_LOCAL_PORT_BASE (default 20000) e le derivate calcolate dal Makefile, per
# non collidere con un secondo checkout. +0 azurite blob · +1 queue · +2 table · +3 appconfig ·
# +4 servicebus amqp · +5 servicebus http · +6 eventgrid · +7 cosmos · +8 cosmos explorer ·
# +9 aspire ui · +10 aspire otlp grpc · +11 keyvault (profilo azure-keyvault).
#
# Le credenziali sono quelle PUBBLICHE degli emulatori (chiave ben nota di Azurite, SAS_KEY_VALUE del
# Service Bus emulator, Id/Secret HMAC di sviluppo): non sono segreti e non vanno in infra/secrets/.
services:
  api:
    environment:
      MEEPLEAI_PROFILE: "azure-local"
      STORAGE_PROVIDER: "azureblob"
      AZURE_STORAGE_CONNECTION_STRING: "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://azurite:10000/devstoreaccount1;"
      AZURE_STORAGE_CONTAINER: "meepleai-uploads"
      # Host di presign raggiungibile dal browser: la firma SAS non copre l'host.
      AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT: "http://localhost:${AZURE_LOCAL_PORT_BASE:-20000}"
      # HMAC (spec §4.2): il provider .NET rifiuta un bearer su http, la connection string no.
      AZURE_APPCONFIG_CONNECTION_STRING: "Endpoint=http://appconfig-emulator:8483;Id=azure-local-dev;Secret=YXp1cmUtbG9jYWwtZGV2LXNlY3JldC0wMTIzNDU2Nzg5YWJjZGVm"
      # Key Vault: vuote di default; `make dev-azure-local-keyvault` le valorizza.
      AZURE_KEYVAULT_URI: "${AZURE_KEYVAULT_URI:-}"
      AZURE_KEYVAULT_ANONYMOUS: "${AZURE_KEYVAULT_ANONYMOUS:-}"
      AZURE_SDK_ALLOW_INSECURE_TLS: "${AZURE_SDK_ALLOW_INSECURE_TLS:-false}"
      AZURE_SERVICEBUS_CONNECTION_STRING: "Endpoint=sb://servicebus-emulator;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"
      AZURE_SERVICEBUS_QUEUE: "pdf-uploaded"
      OTEL_EXPORTER_OTLP_ENDPOINT: "http://aspire-dashboard:18889"
      OTEL_EXPORTER_OTLP_PROTOCOL: "grpc"
      # Token condiviso con la Function locale (apps/functions): valore di sviluppo, non un segreto.
      INTERNAL_SERVICE_TOKEN: "${INTERNAL_SERVICE_TOKEN:-azure-local-dev-token}"
    depends_on:
      azure-local-init:
        condition: service_completed_successfully
      aspire-dashboard:
        condition: service_started

  web:
    build:
      args:
        # Gli URL SAS delle cover puntano a http://localhost:2xxxx: senza questo build arg la CSP
        # (img-src 'self' data: https:) li blocca, come per MinIO (compose.e2e-storage.yml).
        NEXT_PUBLIC_CSP_ALLOW_LOCAL_BLOB: "true"

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
    networks: [meepleai]

  appconfig-emulator:
    image: mcr.microsoft.com/azure-app-configuration/app-configuration-emulator:1.2.0
    container_name: meepleai-appconfig-emulator
    # Il Dockerfile dell'emulatore non imposta l'ambiente ASP.NET: tenant e auth vanno dichiarati qui
    # con la convenzione Section__Key. Anonimo per il seed via curl, HMAC per il provider .NET.
    environment:
      Tenant__AnonymousAuthEnabled: "true"
      Tenant__HmacSha256Enabled: "true"
      Tenant__AccessKeys__0__Id: "azure-local-dev"
      Tenant__AccessKeys__0__Name: "primary"
      Tenant__AccessKeys__0__Secret: "YXp1cmUtbG9jYWwtZGV2LXNlY3JldC0wMTIzNDU2Nzg5YWJjZGVm"
      Tenant__AccessKeys__0__ReadOnly: "false"
      Authentication__Anonymous__AnonymousUserRole: "Owner"
      Hosting__IPAddress: "0.0.0.0"
    ports:
      - "127.0.0.1:${AZURE_LOCAL_PORT_APPCONFIG:-20003}:8483"
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
    networks: [meepleai]

  azure-local-init:
    # Readiness + seed: sonda gli emulatori da fuori (le loro immagini non hanno curl/wget) e semina
    # l'App Configuration emulator; `api` parte solo quando questo container e' uscito 0.
    image: curlimages/curl:8.11.1
    container_name: meepleai-azure-local-init
    depends_on: [azurite, appconfig-emulator, servicebus-emulator]
    volumes:
      - ./azure-local/appconfig-seed.json:/seed.json:ro
      - ./azure-local/wait-and-seed.sh:/wait-and-seed.sh:ro
    entrypoint: ["/bin/sh", "/wait-and-seed.sh"]
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
    # docs/setup.sh del progetto james-gould/azure-keyvault-emulator e copiati in
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

- [ ] **Step 6: Makefile** (subito dopo `dev-down`)

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
AZURE_LOCAL_KEYVAULT_ENV := AZURE_KEYVAULT_URI=https://keyvault-emulator:4997 AZURE_KEYVAULT_ANONYMOUS=true AZURE_SDK_ALLOW_INSECURE_TLS=true

dev-azure-local: ## Start core + Azure emulators (azurite, appconfig, servicebus, cosmos, aspire, eventgrid)
	@bash scripts/seed-status.sh --brief || true
	$(AZURE_LOCAL_PORTS) $(AZURE_LOCAL_COMPOSE) up -d

dev-azure-local-keyvault: ## Same as dev-azure-local plus the Key Vault emulator (needs infra/azure-local/keyvault-certs)
	$(AZURE_LOCAL_PORTS) $(AZURE_LOCAL_KEYVAULT_ENV) $(AZURE_LOCAL_COMPOSE) --profile azure-keyvault up -d

dev-azure-local-down: ## Stop the azure-local profile (emulators included)
	$(AZURE_LOCAL_PORTS) $(AZURE_LOCAL_COMPOSE) --profile azure-keyvault down

dev-azure-local-config: ## Validate the azure-local compose stack (no containers started)
	$(AZURE_LOCAL_PORTS) $(AZURE_LOCAL_COMPOSE) --profile azure-keyvault config --quiet && echo "azure-local compose OK"
```

- [ ] **Step 7: `.gitignore`**

In `infra/.gitignore` (pattern relativi a `infra/`) aggiungere la riga `azure-local/keyvault-certs/`. Verifica: `git check-ignore -v infra/azure-local/keyvault-certs/emulator.pfx` stampa la regola.

- [ ] **Step 8: Validare lo stack e la readiness**

Run (da `infra/`, Git Bash): `make dev-azure-local-config` → Expected: `azure-local compose OK`, nessun warning «variable is not set».
Run: `make dev-azure-local` (primo avvio: pull delle immagini, alcuni minuti) poi `pwsh -c "docker logs meepleai-azure-local-init"` → Expected: tre righe `ready: …` e `seeded Azure:Profile:Greeting`; `curl -fsS http://localhost:8080/api/v1/health` → 200. Poi `make dev-azure-local-down`. Se un emulatore non diventa pronto entro il timeout dello script, il log dice quale: riportare l'errore, non allargare il timeout.

- [ ] **Step 9: Doc operativa `docs/for-developers/operations/azure-local-profile.md`**

Sezioni, ognuna con i comandi: **Cosa avvia** (tabella servizio → immagine → porta host in funzione di `AZURE_LOCAL_PORT_BASE`), **Comandi** (`make dev-azure-local`, `dev-azure-local-down`, `dev-azure-local-config`, `dev-azure-local-keyvault`), **Readiness** (`azure-local-init`: sonda da fuori perché le immagini .NET non hanno curl/wget; `api` parte dopo), **Credenziali di sviluppo** (chiave ben nota Azurite, `SAS_KEY_VALUE`, HMAC `azure-local-dev`; tutte pubbliche), **Precedenza della configurazione** (App Configuration > variabili d'ambiente > `SecretLoader`, che aggiunge solo le chiavi assenti), **Key Vault opt-in** (clonare `james-gould/azure-keyvault-emulator`, eseguire `docs/setup.sh`, copiare `emulator.pfx` e `emulator.crt` in `infra/azure-local/keyvault-certs/`; `make dev-azure-local-keyvault` abilita TLS insicuro SOLO nel profilo locale), **Limiti degli emulatori** (Service Bus: niente entità partizionate né AMQP WebSocket; Cosmos vNext: nessuna autenticazione, solo gateway, DiskANN; App Configuration: auth via env), **Variabili iniettate nell'API** (la lista di Interfaces con una riga di significato ciascuna), **Verifica manuale** (compilata nel Task 10). Niente conteggi in prosa.

- [ ] **Step 10: Commit**

```bash
git add infra/compose.azure-local.yml infra/azure-local/servicebus-config.json infra/azure-local/appconfig-seed.json infra/azure-local/eventgrid-appsettings.json infra/azure-local/wait-and-seed.sh infra/Makefile infra/.gitignore docs/for-developers/operations/azure-local-profile.md
git commit -m "feat(infra): profilo compose azure-local con emulatori Azure" -m "Azurite, App Configuration (HMAC+anonimo), Service Bus (+MSSQL), Cosmos vNext, Aspire Dashboard, Event Grid; Key Vault opt-in; readiness e seed da un container curl." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 2: Client HTTP `AzureSdk`/`AzureSdkLocal`, provider `azureblob` e `AzureBlobStorageService`

**Files:**
- Create: `apps/api/src/Api/Infrastructure/AzureSdk/AzureSdkHttpClients.cs`
- Create: `apps/api/src/Api/Services/Pdf/AzureBlobStorageOptions.cs`
- Create: `apps/api/src/Api/Services/Pdf/AzureStorageConnectionString.cs`
- Create: `apps/api/src/Api/Services/Pdf/AzureBlobStorageService.cs`
- Modify: `apps/api/src/Api/Services/Pdf/BlobStorageServiceFactory.cs`
- Modify: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Egress.cs` (`EgressSinks`)
- Modify: `apps/api/src/Api/Program.cs` (dopo `builder.Services.AddInfrastructureServices(...)`, riga 350)
- Modify: `apps/api/src/Api/Api.csproj`
- Modify: `apps/api/tests/Api.Tests/Architecture/EgressHttpClientPinArchitectureTests.cs` (`MustBePinned`, `Exempt`)
- Test: `apps/api/tests/Api.Tests/Services/Pdf/AzureBlobStorageServiceTests.cs`
- Test: `apps/api/tests/Api.Tests/Services/Pdf/AzureStorageConnectionStringTests.cs`
- Test: `apps/api/tests/Api.Tests/Services/Pdf/BlobStorageServiceFactoryAzureTests.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/AzureSdk/AzureSdkHttpClientsTests.cs`

**Interfaces:**
- Consumes: `IBlobStorageService` (11 metodi), `BlobCategory.ToS3Folder()`, `PathSecurity.ValidateIdentifier(string, string)` (`Api.Infrastructure.Security`), `StringHelper.SanitizeFilename(fileName, maxLength: 200, fallbackName: "file")` (`Api.Helpers`; NON sostituisce gli spazi, sostituisce `< > ? * | " :` e i caratteri invalidi del file system), `ConfigureSsrfPin(sink, allowAutoRedirect, configureHandler, allowedHostSuffixes)`.
- Produces: `AzureSdkHttpClients.PinnedName = "AzureSdk"`, `LocalName = "AzureSdkLocal"`, `CreateTransport(IServiceProvider) : HttpClientTransport`, `IsLocalProfile(IConfiguration)`, `AllowInsecureTls(IConfiguration)`, `ReadAllowedHostSuffixes(IConfiguration)`; `AzureBlobStorageOptions { ConnectionString, AccountUrl, ContainerName, PublicBlobEndpoint, SasExpirySeconds, AccountName, AccountKey }`; `AzureStorageConnectionString.TryParse(string, out string accountName, out string accountKey)`; `AzureBlobStorageService(BlobServiceClient, BlobContainerClient, AzureBlobStorageOptions, ILogger<AzureBlobStorageService>)` con proprietà interna `Options`; `MeepleAiMetrics.EgressSinks.AzureSdk = "azure_sdk"`.

- [ ] **Step 1: Pacchetti**

Run (da `apps/api/src/Api`):
```bash
dotnet add package Azure.Storage.Blobs --version 12.30.1
dotnet add package Azure.Identity --version 1.21.0
```

- [ ] **Step 2: Test del selettore di client** `apps/api/tests/Api.Tests/Infrastructure/AzureSdk/AzureSdkHttpClientsTests.cs`

```csharp
using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Microsoft.Extensions.DependencyInjection;
using Xunit;

namespace Api.Tests.Infrastructure.AzureSdk;

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
    public void AllowInsecureTls_OnlyInLocalProfileWithExplicitTrue()
    {
        AzureSdkHttpClients.AllowInsecureTls(Config(("MEEPLEAI_PROFILE", "azure-local"), ("AZURE_SDK_ALLOW_INSECURE_TLS", "true"))).Should().BeTrue();
        AzureSdkHttpClients.AllowInsecureTls(Config(("MEEPLEAI_PROFILE", "azure-exam"), ("AZURE_SDK_ALLOW_INSECURE_TLS", "true"))).Should().BeFalse();
        AzureSdkHttpClients.AllowInsecureTls(Config(("MEEPLEAI_PROFILE", "azure-local"), ("AZURE_SDK_ALLOW_INSECURE_TLS", ""))).Should().BeFalse();
    }

    [Fact]
    public void ReadAllowedHostSuffixes_TrimsAndDropsBlanks()
    {
        var config = Config(
            ("Azure:Sdk:AllowedHostSuffixes:0", " .blob.core.windows.net "),
            ("Azure:Sdk:AllowedHostSuffixes:1", ""),
            ("Azure:Sdk:AllowedHostSuffixes:2", ".vault.azure.net"));

        AzureSdkHttpClients.ReadAllowedHostSuffixes(config).Should().Equal(".blob.core.windows.net", ".vault.azure.net");
    }

    [Fact]
    public void CreateTransport_LocalProfile_UsesLocalClientEvenWithoutSuffixes()
    {
        using var sp = Build(Config(("MEEPLEAI_PROFILE", "azure-local")));

        AzureSdkHttpClients.CreateTransport(sp).Should().NotBeNull();
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
        using var sp = Build(Config(("MEEPLEAI_PROFILE", "azure-exam"), ("Azure:Sdk:AllowedHostSuffixes:0", ".blob.core.windows.net")));

        AzureSdkHttpClients.CreateTransport(sp).Should().NotBeNull();
    }
}
```

- [ ] **Step 3: Eseguire per vedere il fallimento di compilazione**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureSdkHttpClientsTests"`

- [ ] **Step 4: Sink, `AzureSdkHttpClients`, registrazione, gate**

In `MeepleAiMetrics.Egress.cs`, dentro `EgressSinks`, dopo `Slack`:
```csharp
        /// <summary>SDK Azure (Blob, Key Vault, App Configuration) nel profilo Azure vero — spec azure-local §4.5.</summary>
        public const string AzureSdk = "azure_sdk";
```

`apps/api/src/Api/Infrastructure/AzureSdk/AzureSdkHttpClients.cs`:
```csharp
using Api.Observability;
using Api.SharedKernel.Infrastructure.Http;
using Azure.Core.Pipeline;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// I due <see cref="HttpClient"/> nominati che trasportano gli SDK Azure (spec azure-local §4.5).
/// <para>
/// <c>AzureSdk</c> è pinnato con <c>ConfigureSsrfPin</c> e serve il profilo Azure vero: il pin blocca OGNI
/// indirizzo privato (<c>SsrfPolicy.IsBlocked</c>), quindi non può raggiungere gli emulatori sulla rete
/// compose. Per questo esiste <c>AzureSdkLocal</c>, esente dal pin e scelto SOLO con
/// <c>MEEPLEAI_PROFILE=azure-local</c>. La scelta avviene a runtime in <see cref="CreateTransport"/>.
/// </para>
/// <para>Il client pinnato senza suffissi ammessi non restringerebbe l'host: <see cref="CreateTransport"/>
/// rifiuta quel caso invece di lasciare un default permissivo.</para>
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

    /// <summary>Il transport da passare nelle <c>ClientOptions</c> di ogni SDK Azure basato su HTTP.</summary>
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

In `Program.cs`, dopo `builder.Services.AddInfrastructureServices(builder.Configuration, builder.Environment);`:
```csharp
builder.Services.AddAzureSdkHttpClients(builder.Configuration); // spec azure-local §4.5
```
con `using Api.Infrastructure.AzureSdk;` in testa.

Nel gate `EgressHttpClientPinArchitectureTests.cs`, `MustBePinned`:
```csharp
        ["AzureSdk"] = "Azure SDK (Blob/Key Vault/App Configuration) verso *.windows.net/*.azure.net/*.azconfig.io (spec azure-local §4.5)",
```
`Exempt`:
```csharp
        ["AzureSdkLocal"] = "stessi SDK verso gli emulatori sulla rete compose: usato SOLO con MEEPLEAI_PROFILE=azure-local "
            + "(AzureSdkHttpClients.CreateTransport); il pin bloccherebbe gli indirizzi privati degli emulatori",
```

- [ ] **Step 5: Eseguire selettore + gate**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureSdkHttpClientsTests|FullyQualifiedName~EgressHttpClientPinArchitectureTests"` → Expected: verdi.

- [ ] **Step 6: Test del parser della connection string** `apps/api/tests/Api.Tests/Services/Pdf/AzureStorageConnectionStringTests.cs`

```csharp
using Api.Services.Pdf;
using Api.Tests.Constants;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Services.Pdf;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class AzureStorageConnectionStringTests
{
    [Fact]
    public void TryParse_DevelopmentStorage_ReturnsWellKnownAccount()
    {
        AzureStorageConnectionString.TryParse("UseDevelopmentStorage=true", out var name, out var key).Should().BeTrue();

        name.Should().Be("devstoreaccount1");
        key.Should().Be(AzureStorageConnectionString.DevelopmentStorageKey);
    }

    [Fact]
    public void TryParse_ExplicitPairs_ReturnsThem()
    {
        var cs = "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=a2V5;BlobEndpoint=http://azurite:10000/devstoreaccount1;";

        AzureStorageConnectionString.TryParse(cs, out var name, out var key).Should().BeTrue();

        name.Should().Be("devstoreaccount1");
        key.Should().Be("a2V5");
    }

    [Theory]
    [InlineData("")]
    [InlineData("BlobEndpoint=http://x;")]
    [InlineData("AccountName=only;")]
    public void TryParse_MissingPairs_IsFalse(string cs)
    {
        AzureStorageConnectionString.TryParse(cs, out _, out _).Should().BeFalse();
    }
}
```

- [ ] **Step 7: Test del servizio blob** `apps/api/tests/Api.Tests/Services/Pdf/AzureBlobStorageServiceTests.cs`

```csharp
using System.Text.RegularExpressions;
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
    private static readonly Uri ContainerUri = new("http://azurite:10000/devstoreaccount1/meepleai-uploads");
    private static readonly Response<BlobContentInfo> UploadOk =
        Response.FromValue(BlobsModelFactory.BlobContentInfo(new ETag("e"), DateTimeOffset.UtcNow, null, null, null, null, 1), Mock.Of<Response>());

    private readonly Mock<BlobServiceClient> _service = new();
    private readonly Mock<BlobContainerClient> _container = new();
    private readonly Mock<BlobClient> _blob = new();

    public AzureBlobStorageServiceTests()
    {
        _container.SetupGet(c => c.Name).Returns(Container);
        _container.SetupGet(c => c.Uri).Returns(ContainerUri);
        _container.Setup(c => c.GetBlobClient(It.IsAny<string>())).Returns(_blob.Object);
        _container
            .Setup(c => c.CreateIfNotExistsAsync(It.IsAny<PublicAccessType>(), It.IsAny<IDictionary<string, string>>(), It.IsAny<BlobContainerEncryptionScopeOptions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(BlobsModelFactory.BlobContainerInfo(new ETag("x"), DateTimeOffset.UtcNow), Mock.Of<Response>()));
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>())).ReturnsAsync(UploadOk);
    }

    private AzureBlobStorageService CreateSut(string? publicEndpoint = null, bool sharedKey = true) =>
        new(_service.Object, _container.Object, new AzureBlobStorageOptions
        {
            ConnectionString = "UseDevelopmentStorage=true",
            ContainerName = Container,
            PublicBlobEndpoint = publicEndpoint,
            SasExpirySeconds = 600,
            AccountName = sharedKey ? "devstoreaccount1" : null,
            AccountKey = sharedKey ? AzureStorageConnectionString.DevelopmentStorageKey : null,
        }, Mock.Of<ILogger<AzureBlobStorageService>>());

    private static Page<BlobItem> PageOf(params string[] names) =>
        Page<BlobItem>.FromValues(names.Select(n => BlobsModelFactory.BlobItem(name: n)).ToList(), null, Mock.Of<Response>());

    private void SetupListing(string prefix, params string[] names) =>
        _container
            .Setup(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), prefix, It.IsAny<CancellationToken>()))
            .Returns(AsyncPageable<BlobItem>.FromPages([PageOf(names)]));

    private void VerifyNoListing() =>
        _container.Verify(c => c.GetBlobsAsync(It.IsAny<BlobTraits>(), It.IsAny<BlobStates>(), It.IsAny<string>(), It.IsAny<CancellationToken>()), Times.Never);

    [Fact]
    public async Task StoreAsync_WritesCategorizedKeyWithPdfContentType()
    {
        BlobUploadOptions? uploadOptions = null;
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobUploadOptions, CancellationToken>((_, o, _) => uploadOptions = o)
            .ReturnsAsync(UploadOk);
        var sut = CreateSut();
        using var content = new MemoryStream(new byte[] { 1, 2, 3 });

        var result = await sut.StoreAsync(content, "Rule:Book.pdf", BlobCategory.Pdf, "game-1");

        result.Success.Should().BeTrue(result.ErrorMessage);
        result.FileSizeBytes.Should().Be(3);
        result.FileId.Should().HaveLength(32);
        // ':' è in AdditionalInvalidChars di StringHelper → '_'; lo spazio NON verrebbe sostituito.
        result.FilePath.Should().MatchRegex("^pdfs/game-1/[0-9a-f]{32}_Rule_Book\\.pdf$");
        _container.Verify(c => c.GetBlobClient(It.Is<string>(k => Regex.IsMatch(k, "^pdfs/game-1/[0-9a-f]{32}_Rule_Book\\.pdf$"))), Times.Once);
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

        (await CreateSut().RetrieveAsync("missing", BlobCategory.Pdf, "game-1")).Should().BeNull();
    }

    [Fact]
    public async Task RetrieveAsync_TraversalFileId_ReturnsNullWithoutListing()
    {
        (await CreateSut().RetrieveAsync("../x", BlobCategory.Pdf, "game-1")).Should().BeNull();
        VerifyNoListing();
    }

    [Fact]
    public async Task DeleteAsync_DeletesEveryMatchAndReturnsTrue()
    {
        SetupListing("pdfs/game-1/abc_", "pdfs/game-1/abc_a.pdf", "pdfs/game-1/abc_b.pdf");
        _container.Setup(c => c.DeleteBlobIfExistsAsync(It.IsAny<string>(), It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        (await CreateSut().DeleteAsync("abc", BlobCategory.Pdf, "game-1")).Should().BeTrue();

        _container.Verify(c => c.DeleteBlobIfExistsAsync("pdfs/game-1/abc_a.pdf", It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
        _container.Verify(c => c.DeleteBlobIfExistsAsync("pdfs/game-1/abc_b.pdf", It.IsAny<DeleteSnapshotsOption>(), It.IsAny<BlobRequestConditions>(), It.IsAny<CancellationToken>()), Times.Once);
    }

    [Fact]
    public async Task DeleteAsync_NoMatch_ReturnsFalse()
    {
        SetupListing("pdfs/game-1/abc_");

        (await CreateSut().DeleteAsync("abc", BlobCategory.Pdf, "game-1")).Should().BeFalse();
    }

    [Fact]
    public async Task DeleteAsync_TraversalFileId_ReturnsFalseWithoutListing()
    {
        (await CreateSut().DeleteAsync("../x", BlobCategory.Pdf, "game-1")).Should().BeFalse();
        VerifyNoListing();
    }

    [Fact]
    public async Task ExistsAsync_TraversalFileId_ReturnsFalse()
    {
        (await CreateSut().ExistsAsync("../x", BlobCategory.Pdf, "game-1")).Should().BeFalse();
        VerifyNoListing();
    }

    [Fact]
    public void GetStoragePath_UsesSameLayoutAsS3()
    {
        CreateSut().GetStoragePath("abc", BlobCategory.GameImage, "game-1", "cover|one.png")
            .Should().Be("game-images/game-1/abc_cover_one.png");
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_MissingBlob_ReturnsNull()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(false, Mock.Of<Response>()));

        (await CreateSut().GetPresignedUrlForRawKeyAsync("covers/x.png")).Should().BeNull();
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_SharedKey_SignsWithoutParsingTheContainerUri()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));

        var url = await CreateSut(publicEndpoint: "http://localhost:20000").GetPresignedUrlForRawKeyAsync("covers/game 1/x.png", 120);

        // Host riscritto, path del container preservato, segmento con spazio codificato, firma presente.
        url.Should().StartWith("http://localhost:20000/devstoreaccount1/meepleai-uploads/covers/game%201/x.png?");
        url.Should().Contain("sig=").And.Contain("sp=r").And.Contain("sr=b");
    }

    [Fact]
    public async Task GetPresignedUrlForRawKeyAsync_UserDelegation_UsesServiceKey()
    {
        _blob.Setup(b => b.ExistsAsync(It.IsAny<CancellationToken>())).ReturnsAsync(Response.FromValue(true, Mock.Of<Response>()));
        _service.SetupGet(s => s.AccountName).Returns("devstoreaccount1");
        var key = BlobsModelFactory.UserDelegationKey(
            "oid", "tid", DateTimeOffset.UtcNow.AddMinutes(-5), DateTimeOffset.UtcNow.AddHours(1), "b", "2024-11-04",
            Convert.ToBase64String(new byte[32]));
        _service.Setup(s => s.GetUserDelegationKeyAsync(It.IsAny<DateTimeOffset?>(), It.IsAny<DateTimeOffset>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(Response.FromValue(key, Mock.Of<Response>()));

        var url = await CreateSut(sharedKey: false).GetPresignedUrlForRawKeyAsync("covers/x.png", 120);

        url.Should().StartWith("http://azurite:10000/devstoreaccount1/meepleai-uploads/covers/x.png?");
        url.Should().Contain("skoid=oid").And.Contain("sig=");
    }

    [Fact]
    public async Task StoreRawKeyAsync_WritesVerbatimKeyWithGivenContentType()
    {
        BlobUploadOptions? uploadOptions = null;
        _blob.Setup(b => b.UploadAsync(It.IsAny<Stream>(), It.IsAny<BlobUploadOptions>(), It.IsAny<CancellationToken>()))
            .Callback<Stream, BlobUploadOptions, CancellationToken>((_, o, _) => uploadOptions = o)
            .ReturnsAsync(UploadOk);
        using var content = new MemoryStream(new byte[] { 1 });

        (await CreateSut().StoreRawKeyAsync("covers/game-1/cover.png", content, "image/png")).Should().BeTrue();

        _container.Verify(c => c.GetBlobClient("covers/game-1/cover.png"), Times.Once);
        uploadOptions!.HttpHeaders!.ContentType.Should().Be("image/png");
    }

    [Fact]
    public async Task RetrieveRawKeyAsync_NotFound_ReturnsNull()
    {
        _blob.Setup(b => b.DownloadStreamingAsync(It.IsAny<BlobDownloadOptions>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new RequestFailedException(404, "BlobNotFound"));

        (await CreateSut().RetrieveRawKeyAsync("covers/none.png")).Should().BeNull();
    }
}
```

- [ ] **Step 8: Eseguire per vedere il fallimento di compilazione**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureBlobStorageServiceTests|FullyQualifiedName~AzureStorageConnectionStringTests"`

- [ ] **Step 9: `AzureBlobStorageOptions.cs` e `AzureStorageConnectionString.cs`**

```csharp
namespace Api.Services.Pdf;

/// <summary>Configurazione del provider <c>azureblob</c> (spec azure-local §4.1).</summary>
internal sealed class AzureBlobStorageOptions
{
    /// <summary>Connection string con chiave condivisa (Azurite o account vero). Alternativa a <see cref="AccountUrl"/>.</summary>
    public string? ConnectionString { get; init; }

    /// <summary>URL dell'account (<c>https://&lt;account&gt;.blob.core.windows.net</c>) con <c>DefaultAzureCredential</c>.</summary>
    public string? AccountUrl { get; init; }

    public required string ContainerName { get; init; }

    /// <summary>
    /// Host pubblico con cui riscrivere scheme/host/porta degli URL SAS (come <c>S3_PUBLIC_ENDPOINT</c>, #3498):
    /// la firma SAS copre risorsa e parametri, non l'host.
    /// </summary>
    public string? PublicBlobEndpoint { get; init; }

    public int SasExpirySeconds { get; init; } = 3600;

    /// <summary>Estratti dalla connection string: con entrambi la SAS è firmata con la chiave condivisa, altrimenti con la delega utente.</summary>
    public string? AccountName { get; init; }
    public string? AccountKey { get; init; }
}
```

```csharp
namespace Api.Services.Pdf;

/// <summary>
/// Parser minimale della connection string di Azure Storage: serve SOLO per ricavare account e chiave con cui
/// firmare le SAS senza passare da <c>BlobUriBuilder</c>, che con un host come <c>azurite</c> (né IP né
/// localhost) scambia l'account nel path per il container e produce una firma invalida.
/// </summary>
internal static class AzureStorageConnectionString
{
    public const string DevelopmentStorageAccount = "devstoreaccount1";
    public const string DevelopmentStorageKey = "Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==";

    public static bool TryParse(string? connectionString, out string accountName, out string accountKey)
    {
        accountName = string.Empty;
        accountKey = string.Empty;
        if (string.IsNullOrWhiteSpace(connectionString))
        {
            return false;
        }

        var pairs = connectionString.Split(';', StringSplitOptions.RemoveEmptyEntries | StringSplitOptions.TrimEntries)
            .Select(p => p.Split('=', 2))
            .Where(kv => kv.Length == 2)
            .ToDictionary(kv => kv[0], kv => kv[1], StringComparer.OrdinalIgnoreCase);

        if (pairs.TryGetValue("UseDevelopmentStorage", out var dev) && string.Equals(dev, "true", StringComparison.OrdinalIgnoreCase))
        {
            accountName = DevelopmentStorageAccount;
            accountKey = DevelopmentStorageKey;
            return true;
        }

        if (pairs.TryGetValue("AccountName", out var name) && pairs.TryGetValue("AccountKey", out var key)
            && !string.IsNullOrWhiteSpace(name) && !string.IsNullOrWhiteSpace(key))
        {
            accountName = name;
            accountKey = key;
            return true;
        }

        return false;
    }
}
```

- [ ] **Step 10: `AzureBlobStorageService.cs`**

```csharp
using Api.Helpers;
using Api.Infrastructure.Security;
using Azure;
using Azure.Storage;
using Azure.Storage.Blobs;
using Azure.Storage.Blobs.Models;
using Azure.Storage.Sas;

namespace Api.Services.Pdf;

/// <summary>
/// <see cref="IBlobStorageService"/> su Azure Blob Storage (spec azure-local §4.1). Stesso layout di chiave di
/// <see cref="S3BlobStorageService"/> — <c>{category}/{resourceKey}/{fileId}_{fileName}</c> — e stesse semantiche:
/// risoluzione per prefisso, raw-key senza validazione, HEAD prima della firma, mai un'eccezione oltre il confine.
/// Il Content-Type di <see cref="StoreAsync"/> è sempre <c>application/pdf</c> come nel ramo S3 (difetto ereditato,
/// non introdotto: le altre categorie usano <see cref="StoreRawKeyAsync"/> con il tipo esplicito).
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

    /// <summary>
    /// Firma esplicita con <see cref="BlobSasBuilder"/> su container e chiave NOTI: niente <c>BlobUriBuilder</c>,
    /// che con un host come <c>azurite</c> (né IP né localhost) scambia l'account nel path per il container e
    /// produce una firma che Azurite rifiuta. L'URL è <c>{container.Uri}/{segmenti codificati}?{sas}</c>.
    /// </summary>
    private async Task<string> BuildReadSasAsync(string key, int expirySeconds)
    {
        var startsOn = DateTimeOffset.UtcNow.AddMinutes(-5);
        var expiresOn = DateTimeOffset.UtcNow.AddSeconds(expirySeconds);
        var sas = new BlobSasBuilder(BlobSasPermissions.Read, expiresOn)
        {
            BlobContainerName = _options.ContainerName,
            BlobName = key,
            Resource = "b",
            StartsOn = startsOn,
        };

        string query;
        if (!string.IsNullOrWhiteSpace(_options.AccountName) && !string.IsNullOrWhiteSpace(_options.AccountKey))
        {
            // Chiave condivisa (connection string: Azurite o account con chiave).
            query = sas.ToSasQueryParameters(new StorageSharedKeyCredential(_options.AccountName, _options.AccountKey)).ToString();
        }
        else
        {
            // Identità Entra (DefaultAzureCredential): SAS con chiave di delega utente.
            var delegationKey = await _serviceClient.GetUserDelegationKeyAsync(startsOn, expiresOn, CancellationToken.None).ConfigureAwait(false);
            query = sas.ToSasQueryParameters(delegationKey.Value, _serviceClient.AccountName).ToString();
        }

        var encodedKey = string.Join('/', key.Split('/').Select(Uri.EscapeDataString));
        var signed = new Uri($"{_container.Uri.AbsoluteUri.TrimEnd('/')}/{encodedKey}?{query}", UriKind.Absolute);

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

- [ ] **Step 11: Il ramo `azureblob` nella factory**

In `BlobStorageServiceFactory.Create`, nello switch, prima di `"s3"`:
```csharp
            "azureblob" => CreateAzureBlobStorageService(serviceProvider, config),
```
Nuovo metodo privato (dopo `CreateS3StorageService`), con `using Azure.Storage.Blobs;`, `using Azure.Identity;`, `using Api.Infrastructure.AzureSdk;` in testa:
```csharp
    private static IBlobStorageService CreateAzureBlobStorageService(IServiceProvider serviceProvider, IConfiguration config)
    {
        var logger = serviceProvider.GetRequiredService<ILogger<AzureBlobStorageService>>();
        var connectionString = config["AZURE_STORAGE_CONNECTION_STRING"];
        var accountUrl = config["AZURE_STORAGE_ACCOUNT_URL"];
        var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
        var hasAccountUrl = !string.IsNullOrWhiteSpace(accountUrl);
        if (!hasConnectionString && !hasAccountUrl)
        {
            throw new InvalidOperationException(
                "AZURE_STORAGE_CONNECTION_STRING or AZURE_STORAGE_ACCOUNT_URL is required when STORAGE_PROVIDER=azureblob");
        }

        AzureStorageConnectionString.TryParse(connectionString, out var accountName, out var accountKey);
        var options = new AzureBlobStorageOptions
        {
            ConnectionString = connectionString,
            AccountUrl = accountUrl,
            ContainerName = config["AZURE_STORAGE_CONTAINER"] is { Length: > 0 } name ? name : "meepleai-uploads",
            PublicBlobEndpoint = config["AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT"],
            SasExpirySeconds = int.TryParse(config["AZURE_STORAGE_SAS_EXPIRY"], CultureInfo.InvariantCulture, out var expiry) ? expiry : 3600,
            AccountName = hasConnectionString ? accountName : null,
            AccountKey = hasConnectionString ? accountKey : null,
        };

        var clientOptions = new BlobClientOptions { Transport = AzureSdkHttpClients.CreateTransport(serviceProvider) };
        var serviceClient = hasConnectionString
            ? new BlobServiceClient(connectionString, clientOptions)
            : new BlobServiceClient(new Uri(accountUrl!, UriKind.Absolute), new DefaultAzureCredential(), clientOptions);
        var container = serviceClient.GetBlobContainerClient(options.ContainerName);

        logger.LogInformation(
            "Initialized Azure Blob storage: endpoint={Endpoint}, container={Container}, publicEndpoint={PublicEndpoint}, sharedKey={SharedKey}",
            serviceClient.Uri, options.ContainerName, options.PublicBlobEndpoint ?? serviceClient.Uri.ToString(), options.AccountKey is not null);

        return new AzureBlobStorageService(serviceClient, container, options, logger);
    }
```
Il servizio resta Scoped come il ramo S3 (un client per scope): registrare `BlobServiceClient` singleton è un follow-up, non parte di questo task.

- [ ] **Step 12: Test della factory** `apps/api/tests/Api.Tests/Services/Pdf/BlobStorageServiceFactoryAzureTests.cs`

```csharp
using Api.Infrastructure.AzureSdk;
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

        act.Should().Throw<InvalidOperationException>().WithMessage("*AZURE_STORAGE_CONNECTION_STRING*AZURE_STORAGE_ACCOUNT_URL*");
    }

    [Fact]
    public void Create_AzureBlobWithConnectionString_ReturnsAzureServiceWithParsedKey()
    {
        using var sp = Build(
            ("STORAGE_PROVIDER", "AzureBlob"),
            ("MEEPLEAI_PROFILE", "azure-local"),
            ("AZURE_STORAGE_CONNECTION_STRING", "UseDevelopmentStorage=true"),
            ("AZURE_STORAGE_CONTAINER", "uploads-test"),
            ("AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT", "http://localhost:20000"),
            ("AZURE_STORAGE_SAS_EXPIRY", "120"));

        var azure = BlobStorageServiceFactory.Create(sp).Should().BeOfType<AzureBlobStorageService>().Subject;

        azure.Options.ContainerName.Should().Be("uploads-test");
        azure.Options.PublicBlobEndpoint.Should().Be("http://localhost:20000");
        azure.Options.SasExpirySeconds.Should().Be(120);
        azure.Options.AccountName.Should().Be("devstoreaccount1");
        azure.Options.AccountKey.Should().Be(AzureStorageConnectionString.DevelopmentStorageKey);
    }

    [Fact]
    public void Create_UnknownProvider_StillFallsBackToLocal()
    {
        using var sp = Build(("STORAGE_PROVIDER", "nope"));

        BlobStorageServiceFactory.Create(sp).Should().BeOfType<BlobStorageService>();
    }
}
```

- [ ] **Step 13: Eseguire tutti i test del task**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureBlobStorageServiceTests|FullyQualifiedName~AzureStorageConnectionStringTests|FullyQualifiedName~BlobStorageServiceFactoryAzureTests|FullyQualifiedName~AzureSdkHttpClientsTests|FullyQualifiedName~EgressHttpClientPinArchitectureTests"`
Expected: tutti verdi. Se un `Setup` Moq su un overload dell'SDK non compila, allineare alla firma **virtuale** della 12.30.1 (`GetBlobsAsync(BlobTraits, BlobStates, string, CancellationToken)`, `DeleteBlobIfExistsAsync(string, DeleteSnapshotsOption, BlobRequestConditions, CancellationToken)`, `UploadAsync(Stream, BlobUploadOptions, CancellationToken)`, `DownloadStreamingAsync(BlobDownloadOptions, CancellationToken)`, `GetUserDelegationKeyAsync(DateTimeOffset?, DateTimeOffset, CancellationToken)`).

- [ ] **Step 14: Commit**

```bash
git add apps/api/src/Api/Infrastructure/AzureSdk/AzureSdkHttpClients.cs apps/api/src/Api/Services/Pdf/AzureBlobStorageOptions.cs apps/api/src/Api/Services/Pdf/AzureStorageConnectionString.cs apps/api/src/Api/Services/Pdf/AzureBlobStorageService.cs apps/api/src/Api/Services/Pdf/BlobStorageServiceFactory.cs apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.Egress.cs apps/api/src/Api/Program.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Architecture/EgressHttpClientPinArchitectureTests.cs apps/api/tests/Api.Tests/Services/Pdf/AzureBlobStorageServiceTests.cs apps/api/tests/Api.Tests/Services/Pdf/AzureStorageConnectionStringTests.cs apps/api/tests/Api.Tests/Services/Pdf/BlobStorageServiceFactoryAzureTests.cs apps/api/tests/Api.Tests/Infrastructure/AzureSdk/AzureSdkHttpClientsTests.cs
git commit -m "feat(storage): provider azureblob su Azure.Storage.Blobs" -m "AzureBlobStorageService con lo stesso layout di chiave di S3, SAS firmata esplicitamente (chiave condivisa o delega utente) senza BlobUriBuilder, riscrittura dell'host pubblico. Client AzureSdk pinnato e AzureSdkLocal esente, scelti dal profilo." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 3: Test di integrazione su Azurite con fixture per classe

**Files:**
- Modify: `apps/api/tests/Api.Tests/Api.Tests.csproj`
- Modify: `apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs`
- Create: `apps/api/tests/Api.Tests/Infrastructure/DockerDaemonProbe.cs`
- Create: `apps/api/tests/Api.Tests/Infrastructure/EmulatorFixtureBase.cs`
- Create: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/AzuriteFixture.cs`
- Test: `apps/api/tests/Api.Tests/Integration/DocumentProcessing/AzureBlobStorageIntegrationTests.cs`

**Interfaces:**
- Consumes: `AzureBlobStorageService`, `AzureBlobStorageOptions`, `AzureStorageConnectionString` (Task 2).
- Produces: `TestcontainersConfiguration.AzuriteImage`, `AzuriteBlobPort = 10000`, `AzuriteTestContainer`; `DockerDaemonProbe.IsAvailable() : bool`; `EmulatorFixtureBase` (`UnavailableReason`, `SkipIfUnavailable()` che sceglie `PREVISTO:` o `GUASTO:`); `AzuriteFixture { ConnectionString, PublicEndpoint }`. I Task 4 e 7 riusano `EmulatorFixtureBase`.

- [ ] **Step 1: Pacchetto e costanti**

Run (da `apps/api/tests/Api.Tests`): `dotnet add package Testcontainers.Azurite --version 4.14.0`

In `TestcontainersConfiguration.cs`, dopo `MinioTestBucket`:
```csharp
    /// <summary>Azurite (spec azure-local): servizio L3 — la sua assenza è un salto PREVISTO, non un rosso.</summary>
    public const string AzuriteImage = "mcr.microsoft.com/azure-storage/azurite:3.37.0";
    public const int AzuriteBlobPort = 10000;
    public const string AzuriteTestContainer = "meepleai-uploads-test";
```

- [ ] **Step 2: `DockerDaemonProbe.cs` e `EmulatorFixtureBase.cs`**

```csharp
using System.Diagnostics;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Distingue «Docker assente» (salto PREVISTO) da «Docker presente ma il container non parte» (salto GUASTO):
/// i due casi si correggono in modi diversi e un motivo generico chiude l'indagine prima che inizi (#3978).
/// </summary>
internal static class DockerDaemonProbe
{
    private static readonly Lazy<bool> Available = new(Probe);

    public static bool IsAvailable() => Available.Value;

    private static bool Probe()
    {
        try
        {
            using var process = new Process
            {
                StartInfo = new ProcessStartInfo
                {
                    FileName = "docker",
                    Arguments = "version --format {{.Server.Version}}",
                    RedirectStandardOutput = true,
                    RedirectStandardError = true,
                    UseShellExecute = false,
                    CreateNoWindow = true,
                },
            };
            process.Start();
            return process.WaitForExit(15_000) && process.ExitCode == 0;
        }
        catch (Exception ex) when (ex is System.ComponentModel.Win32Exception or InvalidOperationException)
        {
            return false;
        }
    }
}
```

```csharp
using Xunit;

namespace Api.Tests.Infrastructure;

/// <summary>
/// Base delle fixture per CLASSE degli emulatori Azure (spec azure-local §6): un container per classe, perché
/// <c>methodTimeout</c> copre <c>InitializeAsync</c> e un emulatore per metodo scadrebbe in timeout. L'avvio
/// non lancia: registra il motivo e i test saltano con la classe giusta.
/// </summary>
public abstract class EmulatorFixtureBase : IAsyncLifetime
{
    public string? UnavailableReason { get; private set; }

    protected abstract string EmulatorName { get; }

    protected abstract Task StartAsync();

    protected abstract Task StopAsync();

    public async ValueTask InitializeAsync()
    {
        if (!DockerDaemonProbe.IsAvailable())
        {
            UnavailableReason = "PREVISTO: Docker non disponibile. In locale: cd infra && make dev-azure-local (avvia anche " + EmulatorName + ")";
            return;
        }

        try
        {
            await StartAsync();
        }
        catch (Exception ex) when (ex is not OutOfMemoryException)
        {
            UnavailableReason = $"GUASTO: {EmulatorName} non avviato con Docker presente: {ex.GetType().Name}: {ex.Message}";
        }
    }

    public async ValueTask DisposeAsync()
    {
        if (UnavailableReason is null)
        {
            await StopAsync();
        }
    }

    public void SkipIfUnavailable()
    {
        if (UnavailableReason is not null)
        {
            Assert.Skip(UnavailableReason);
        }
    }
}
```

- [ ] **Step 3: `AzuriteFixture.cs`**

```csharp
using Api.Tests.Infrastructure;
using Testcontainers.Azurite;

namespace Api.Tests.Integration.DocumentProcessing;

public sealed class AzuriteFixture : EmulatorFixtureBase
{
    private AzuriteContainer? _container;

    public string ConnectionString { get; private set; } = string.Empty;
    public string PublicEndpoint { get; private set; } = string.Empty;

    protected override string EmulatorName => "Azurite";

    protected override async Task StartAsync()
    {
        _container = new AzuriteBuilder().WithImage(TestcontainersConfiguration.AzuriteImage).Build();
        await _container.StartAsync();
        ConnectionString = _container.GetConnectionString();
        PublicEndpoint = $"http://127.0.0.1:{_container.GetMappedPublicPort(TestcontainersConfiguration.AzuriteBlobPort)}";
    }

    protected override async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

- [ ] **Step 4: Il test**

```csharp
using Api.Services.Pdf;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using Azure.Storage.Blobs;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.Integration.DocumentProcessing;

/// <summary>Ciclo completo Store → Exists → Retrieve → SAS → Delete contro Azurite vero (un container per classe).</summary>
[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "DocumentProcessing")]
[Trait("Feature", "AzureBlobStorage")]
public sealed class AzureBlobStorageIntegrationTests : IClassFixture<AzuriteFixture>
{
    private readonly AzuriteFixture _azurite;

    public AzureBlobStorageIntegrationTests(AzuriteFixture azurite)
    {
        _azurite = azurite;
    }

    private AzureBlobStorageService CreateSut()
    {
        var serviceClient = new BlobServiceClient(_azurite.ConnectionString);
        var container = serviceClient.GetBlobContainerClient(TestcontainersConfiguration.AzuriteTestContainer);
        AzureStorageConnectionString.TryParse(_azurite.ConnectionString, out var accountName, out var accountKey);
        return new AzureBlobStorageService(serviceClient, container, new AzureBlobStorageOptions
        {
            ConnectionString = _azurite.ConnectionString,
            ContainerName = TestcontainersConfiguration.AzuriteTestContainer,
            PublicBlobEndpoint = _azurite.PublicEndpoint,
            SasExpirySeconds = 300,
            AccountName = accountName,
            AccountKey = accountKey,
        }, Mock.Of<ILogger<AzureBlobStorageService>>());
    }

    [Fact]
    public async Task StoreExistsRetrieveSignDelete_RoundTrip()
    {
        _azurite.SkipIfUnavailable();
        var sut = CreateSut();
        var payload = new byte[] { 0x25, 0x50, 0x44, 0x46, 0x2D, 0x31, 0x2E, 0x34 }; // "%PDF-1.4"
        using var content = new MemoryStream(payload);

        var stored = await sut.StoreAsync(content, "rulebook.pdf", BlobCategory.Pdf, "game-rt");
        stored.Success.Should().BeTrue(stored.ErrorMessage);

        (await sut.ExistsAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeTrue();

        await using (var retrieved = await sut.RetrieveAsync(stored.FileId!, BlobCategory.Pdf, "game-rt"))
        {
            retrieved.Should().NotBeNull();
            using var copy = new MemoryStream();
            await retrieved!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(payload);
        }

        var url = await sut.GetPresignedDownloadUrlAsync(stored.FileId!, BlobCategory.Pdf, "game-rt");
        url.Should().NotBeNull().And.StartWith(_azurite.PublicEndpoint);
        using (var http = new HttpClient())
        {
            (await http.GetByteArrayAsync(new Uri(url!))).Should().Equal(payload);
        }

        (await sut.DeleteAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeTrue();
        (await sut.ExistsAsync(stored.FileId!, BlobCategory.Pdf, "game-rt")).Should().BeFalse();
    }

    [Fact]
    public async Task RawKey_StoreSignRetrieveDelete_RoundTrip()
    {
        _azurite.SkipIfUnavailable();
        var sut = CreateSut();
        var payload = new byte[] { 1, 2, 3, 4 };
        using var content = new MemoryStream(payload);

        (await sut.StoreRawKeyAsync("covers/game-rt/cover.png", content, "image/png")).Should().BeTrue();

        var url = await sut.GetPresignedUrlForRawKeyAsync("covers/game-rt/cover.png", 60);
        url.Should().NotBeNull().And.Contain("covers/game-rt/cover.png?");
        using (var http = new HttpClient())
        {
            (await http.GetByteArrayAsync(new Uri(url!))).Should().Equal(payload);
        }

        await using (var retrieved = await sut.RetrieveRawKeyAsync("covers/game-rt/cover.png"))
        {
            using var copy = new MemoryStream();
            await retrieved!.CopyToAsync(copy);
            copy.ToArray().Should().Equal(payload);
        }

        (await sut.DeleteRawKeyAsync("covers/game-rt/cover.png")).Should().BeTrue();
        (await sut.GetPresignedUrlForRawKeyAsync("covers/game-rt/cover.png")).Should().BeNull();
    }
}
```

- [ ] **Step 5: Eseguire**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AzureBlobStorageIntegrationTests"`
Expected: 2 superati con Docker. Se Azurite rifiuta la versione API dell'SDK (`InvalidHeaderValue` su `x-ms-version`), aggiungere alla fixture `.WithCommand("azurite", "--blobHost", "0.0.0.0", "--queueHost", "0.0.0.0", "--tableHost", "0.0.0.0", "--skipApiVersionCheck", "--loose")` e riportarlo nel report. Senza Docker: 2 salti `PREVISTO:`.

- [ ] **Step 6: Commit**

```bash
git add apps/api/tests/Api.Tests/Api.Tests.csproj apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs apps/api/tests/Api.Tests/Infrastructure/DockerDaemonProbe.cs apps/api/tests/Api.Tests/Infrastructure/EmulatorFixtureBase.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/AzuriteFixture.cs apps/api/tests/Api.Tests/Integration/DocumentProcessing/AzureBlobStorageIntegrationTests.cs
git commit -m "test(storage): round-trip del provider azureblob su Azurite" -m "Fixture per classe; salto PREVISTO senza Docker, GUASTO se il container non parte." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 4: Sorgenti `IConfiguration` da App Configuration (HMAC) e Key Vault

**Files:**
- Create: `apps/api/src/Api/Infrastructure/AzureSdk/StaticTokenCredential.cs`
- Create: `apps/api/src/Api/Infrastructure/AzureSdk/EnvironmentStyleKeyVaultSecretManager.cs`
- Create: `apps/api/src/Api/Infrastructure/AzureSdk/AzureConfigurationSources.cs`
- Modify: `apps/api/src/Api/Program.cs` (subito dopo `var builder = WebApplication.CreateBuilder(args);`, PRIMA del blocco `SecretLoader`)
- Modify: `apps/api/src/Api/Api.csproj`
- Create: `apps/api/tests/Api.Tests/Integration/Infrastructure/AppConfigurationEmulatorFixture.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/AzureSdk/EnvironmentStyleKeyVaultSecretManagerTests.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/AzureSdk/AzureConfigurationSourcesTests.cs`
- Test: `apps/api/tests/Api.Tests/Integration/Infrastructure/AppConfigurationEmulatorIntegrationTests.cs`

**Interfaces:**
- Consumes: `AzureSdkHttpClients.AllowInsecureTls(IConfiguration)` (Task 2), `EmulatorFixtureBase` (Task 3), credenziali HMAC dell'emulatore (Task 1).
- Produces: `AzureConfigurationSources.AddAzureConfigurationSources(this IConfigurationBuilder builder, IConfiguration settings)`; chiavi `AZURE_APPCONFIG_CONNECTION_STRING` (HMAC: emulatore o store con chiavi di accesso), `AZURE_APPCONFIG_ENDPOINT` (Azure vero con `DefaultAzureCredential`), `AZURE_KEYVAULT_URI`, `AZURE_KEYVAULT_ANONYMOUS`; `StaticTokenCredential(string token)`; `EnvironmentStyleKeyVaultSecretManager.GetKey`.

- [ ] **Step 1: Pacchetti**

Run (da `apps/api/src/Api`):
```bash
dotnet add package Microsoft.Azure.AppConfiguration.AspNetCore --version 8.6.0
dotnet add package Azure.Extensions.AspNetCore.Configuration.Secrets --version 1.5.2
```

- [ ] **Step 2: Test del mapping dei nomi dei secret**

```csharp
using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using Azure.Security.KeyVault.Secrets;
using FluentAssertions;
using Xunit;

namespace Api.Tests.Infrastructure.AzureSdk;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class EnvironmentStyleKeyVaultSecretManagerTests
{
    [Theory]
    [InlineData("OPENROUTER-API-KEY", "OPENROUTER_API_KEY")]
    [InlineData("Embedding--Provider", "Embedding:Provider")]
    [InlineData("Seq--ApiKey", "Seq:ApiKey")]
    [InlineData("plain", "plain")]
    public void GetKey_MapsDoubleDashToSectionAndSingleDashToUnderscore(string secretName, string expectedKey)
    {
        // Convenzione (spec §4.2): `--` separa le sezioni, `-` sta per `_` nei nomi in stile variabile
        // d'ambiente. Una chiave CamelCase con trattino interno (es. Api-Key) NON è esprimibile: usare il
        // nome CamelCase senza trattini (Seq--ApiKey).
        var sut = new EnvironmentStyleKeyVaultSecretManager();

        sut.GetKey(new KeyVaultSecret(secretName, "v")).Should().Be(expectedKey);
    }
}
```

- [ ] **Step 3: Test del gating delle sorgenti**

```csharp
using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using Azure.Core;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Infrastructure.AzureSdk;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "Infrastructure")]
public sealed class AzureConfigurationSourcesTests
{
    private static IConfiguration Settings(params (string Key, string? Value)[] pairs) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    [InlineData("   ")]
    public void AddAzureConfigurationSources_NoEndpoints_AddsNothing(string? value)
    {
        // ConfigurationBuilder (non ConfigurationManager): Add registra la sorgente senza costruirla,
        // quindi il test ispeziona Sources senza aprire alcuna connessione.
        var builder = new ConfigurationBuilder();
        var settings = Settings(("AZURE_APPCONFIG_CONNECTION_STRING", value), ("AZURE_APPCONFIG_ENDPOINT", value), ("AZURE_KEYVAULT_URI", value));

        builder.AddAzureConfigurationSources(settings);

        builder.Sources.Should().BeEmpty();
    }

    [Fact]
    public void AddAzureConfigurationSources_AppConfigConnectionString_AddsOneSource()
    {
        var builder = new ConfigurationBuilder();

        builder.AddAzureConfigurationSources(Settings(("AZURE_APPCONFIG_CONNECTION_STRING", "Endpoint=http://127.0.0.1:9;Id=i;Secret=c2VjcmV0")));

        builder.Sources.Should().HaveCount(1);
    }

    [Fact]
    public void AddAzureConfigurationSources_KeyVaultUri_AddsOneSource()
    {
        var builder = new ConfigurationBuilder();

        builder.AddAzureConfigurationSources(Settings(("AZURE_KEYVAULT_URI", "https://127.0.0.1:9/"), ("AZURE_KEYVAULT_ANONYMOUS", "true")));

        builder.Sources.Should().HaveCount(1);
    }

    [Fact]
    public void AddAzureConfigurationSources_InvalidEndpoint_Throws()
    {
        var builder = new ConfigurationBuilder();

        var act = () => builder.AddAzureConfigurationSources(Settings(("AZURE_APPCONFIG_ENDPOINT", "not a url")));

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

- [ ] **Step 5: Implementare**

`StaticTokenCredential.cs`:
```csharp
using Azure.Core;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// Credenziale fissa per il Key Vault emulator (https, bearer ignorato). NON per App Configuration: lì il
/// bearer su http è rifiutato dall'SDK prima di ogni richiesta, e la via è la connection string HMAC.
/// Mai usata nel profilo Azure vero.
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

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// Convenzione dei nomi (spec azure-local §4.2): i secret portano i nomi delle variabili d'ambiente che il repo
/// già usa, con <c>-</c> al posto di <c>_</c> (Key Vault non ammette l'underscore); <c>--</c> resta il separatore
/// di sezione come nel manager di default. <c>OPENROUTER-API-KEY</c> → <c>OPENROUTER_API_KEY</c>,
/// <c>Embedding--Provider</c> → <c>Embedding:Provider</c>. Le chiavi CamelCase con trattino interno non sono
/// esprimibili: usare il nome senza trattini (<c>Seq--ApiKey</c>).
/// </summary>
internal sealed class EnvironmentStyleKeyVaultSecretManager : KeyVaultSecretManager
{
    public override string GetKey(KeyVaultSecret secret) =>
        secret.Name.Replace("--", ":", StringComparison.Ordinal).Replace('-', '_');
}
```

`AzureConfigurationSources.cs`:
```csharp
using Azure.Core.Pipeline;
using Azure.Identity;
using Azure.Security.KeyVault.Secrets;
using Microsoft.Extensions.Configuration.AzureAppConfiguration;

namespace Api.Infrastructure.AzureSdk;

/// <summary>
/// Sorgenti <see cref="IConfiguration"/> Azure (spec azure-local §4.2). Girano PRIMA del container DI, quindi non
/// passano dal client <c>AzureSdk</c> pinnato: gli endpoint vengono da configurazione statica (mai da input
/// utente), la stessa condizione con cui il gate di egress esenta OpenRouter o Resend. Con le variabili assenti o
/// vuote non viene aggiunta alcuna sorgente. Le chiavi lette da qui VINCONO sulle variabili d'ambiente
/// (aggiunte dopo); <c>SecretLoader</c> aggiunge solo le chiavi ancora assenti.
/// </summary>
internal static class AzureConfigurationSources
{
    public const string AppConfigConnectionStringKey = "AZURE_APPCONFIG_CONNECTION_STRING";
    public const string AppConfigEndpointKey = "AZURE_APPCONFIG_ENDPOINT";
    public const string KeyVaultUriKey = "AZURE_KEYVAULT_URI";
    public const string KeyVaultAnonymousKey = "AZURE_KEYVAULT_ANONYMOUS";
    private const string AnonymousToken = "anonymous";

    public static IConfigurationBuilder AddAzureConfigurationSources(this IConfigurationBuilder builder, IConfiguration settings)
    {
        var appConfigConnectionString = settings[AppConfigConnectionStringKey];
        var appConfigEndpoint = settings[AppConfigEndpointKey];
        if (!string.IsNullOrWhiteSpace(appConfigConnectionString))
        {
            // HMAC (emulatore o store con chiavi di accesso): funziona anche su http.
            builder.AddAzureAppConfiguration(options => options
                .Connect(appConfigConnectionString.Trim())
                .Select(KeyFilter.Any, LabelFilter.Null));
        }
        else if (!string.IsNullOrWhiteSpace(appConfigEndpoint))
        {
            // Azure vero: identità Entra, endpoint https.
            var endpoint = ParseAbsoluteUri(appConfigEndpoint, AppConfigEndpointKey);
            builder.AddAzureAppConfiguration(options => options
                .Connect(endpoint, new DefaultAzureCredential())
                .Select(KeyFilter.Any, LabelFilter.Null));
        }

        var keyVaultUri = settings[KeyVaultUriKey];
        if (!string.IsNullOrWhiteSpace(keyVaultUri))
        {
            var vault = ParseAbsoluteUri(keyVaultUri, KeyVaultUriKey);
            var clientOptions = new SecretClientOptions { DisableChallengeResourceVerification = true };
            if (AzureSdkHttpClients.AllowInsecureTls(settings))
            {
                // Solo profilo azure-local + opt-in esplicito: certificato self-signed del Key Vault emulator.
                var handler = new SocketsHttpHandler();
                handler.SslOptions.RemoteCertificateValidationCallback = static (_, _, _, _) => true;
                clientOptions.Transport = new HttpClientTransport(new HttpClient(handler));
            }

            var anonymous = string.Equals(settings[KeyVaultAnonymousKey], "true", StringComparison.OrdinalIgnoreCase);
            var client = new SecretClient(vault, anonymous ? new StaticTokenCredential(AnonymousToken) : new DefaultAzureCredential(), clientOptions);
            builder.AddAzureKeyVault(client, new EnvironmentStyleKeyVaultSecretManager());
        }

        return builder;
    }

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
// AZURE_APPCONFIG_CONNECTION_STRING / AZURE_APPCONFIG_ENDPOINT / AZURE_KEYVAULT_URI valorizzate. Su
// ConfigurationManager l'Add carica subito la sorgente: qui è voluto. Prima del SecretLoader, che
// aggiunge soltanto le chiavi ancora assenti.
builder.Configuration.AddAzureConfigurationSources(builder.Configuration);
```
(`using Api.Infrastructure.AzureSdk;` già presente da Task 2.)

- [ ] **Step 6: Eseguire i test unit**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~EnvironmentStyleKeyVaultSecretManagerTests|FullyQualifiedName~AzureConfigurationSourcesTests"` → Expected: verdi.

- [ ] **Step 7: Fixture e test di integrazione contro l'App Configuration emulator**

`AppConfigurationEmulatorFixture.cs`:
```csharp
using Api.Tests.Infrastructure;
using DotNet.Testcontainers.Builders;
using DotNet.Testcontainers.Containers;

namespace Api.Tests.Integration.Infrastructure;

public sealed class AppConfigurationEmulatorFixture : EmulatorFixtureBase
{
    public const string Image = "mcr.microsoft.com/azure-app-configuration/app-configuration-emulator:1.2.0";
    public const int Port = 8483;
    public const string AccessKeyId = "azure-local-dev";
    public const string AccessKeySecret = "YXp1cmUtbG9jYWwtZGV2LXNlY3JldC0wMTIzNDU2Nzg5YWJjZGVm";

    private IContainer? _container;

    public string Endpoint { get; private set; } = string.Empty;
    public string ConnectionString => $"Endpoint={Endpoint};Id={AccessKeyId};Secret={AccessKeySecret}";

    protected override string EmulatorName => "App Configuration emulator";

    protected override async Task StartAsync()
    {
        _container = new ContainerBuilder()
            .WithImage(Image)
            .WithEnvironment("Tenant__AnonymousAuthEnabled", "true")
            .WithEnvironment("Tenant__HmacSha256Enabled", "true")
            .WithEnvironment("Tenant__AccessKeys__0__Id", AccessKeyId)
            .WithEnvironment("Tenant__AccessKeys__0__Name", "primary")
            .WithEnvironment("Tenant__AccessKeys__0__Secret", AccessKeySecret)
            .WithEnvironment("Tenant__AccessKeys__0__ReadOnly", "false")
            .WithEnvironment("Authentication__Anonymous__AnonymousUserRole", "Owner")
            .WithEnvironment("Hosting__IPAddress", "0.0.0.0")
            .WithPortBinding(Port, true)
            .WithWaitStrategy(Wait.ForUnixContainer().UntilHttpRequestIsSucceeded(r => r.ForPath("/health").ForPort(Port)))
            .Build();
        await _container.StartAsync();
        Endpoint = $"http://127.0.0.1:{_container.GetMappedPublicPort(Port)}";
    }

    protected override async Task StopAsync()
    {
        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

`AppConfigurationEmulatorIntegrationTests.cs`:
```csharp
using System.Text;
using Api.Infrastructure.AzureSdk;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Configuration;
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

[Trait("Category", TestCategories.Integration)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Feature", "AzureAppConfiguration")]
public sealed class AppConfigurationEmulatorIntegrationTests : IClassFixture<AppConfigurationEmulatorFixture>
{
    private readonly AppConfigurationEmulatorFixture _emulator;

    public AppConfigurationEmulatorIntegrationTests(AppConfigurationEmulatorFixture emulator)
    {
        _emulator = emulator;
    }

    private async Task SeedAsync(string key, string value)
    {
        using var http = new HttpClient();
        using var body = new StringContent($"{{\"value\":\"{value}\"}}", Encoding.UTF8, "application/vnd.microsoft.appconfig.kv+json");
        var put = await http.PutAsync(new Uri($"{_emulator.Endpoint}/kv/{Uri.EscapeDataString(key)}?api-version=2023-11-01"), body);
        put.EnsureSuccessStatusCode();
    }

    [Fact]
    public async Task HmacConnectionString_ReadsSeededKey()
    {
        _emulator.SkipIfUnavailable();
        await SeedAsync("Azure:Profile:Greeting", "from-emulator");
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AzureConfigurationSources.AppConfigConnectionStringKey] = _emulator.ConnectionString })
            .Build();

        var configuration = new ConfigurationBuilder().AddAzureConfigurationSources(settings).Build();

        configuration["Azure:Profile:Greeting"].Should().Be("from-emulator");
    }

    [Fact]
    public async Task HmacConnectionString_WinsOverEnvironmentStyleValue()
    {
        _emulator.SkipIfUnavailable();
        await SeedAsync("Azure:Profile:Precedence", "from-emulator");
        var settings = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { [AzureConfigurationSources.AppConfigConnectionStringKey] = _emulator.ConnectionString })
            .Build();

        // Stesso ordine di Program.cs: prima le "variabili d'ambiente" (in-memory), poi le sorgenti Azure.
        var configuration = new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["Azure:Profile:Precedence"] = "from-env" })
            .AddAzureConfigurationSources(settings)
            .Build();

        configuration["Azure:Profile:Precedence"].Should().Be("from-emulator");
    }
}
```

- [ ] **Step 8: Eseguire il test di integrazione**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~AppConfigurationEmulatorIntegrationTests"`
Expected: 2 superati. Se l'emulatore risponde 401 all'HMAC (il binder non ha letto `Tenant__AccessKeys__0__Secret` come byte[] base64), riportare BLOCKED con il corpo della risposta e il log del container (`docker logs`): il controller decide; non modificare l'asserzione.

- [ ] **Step 9: Commit**

```bash
git add apps/api/src/Api/Infrastructure/AzureSdk/StaticTokenCredential.cs apps/api/src/Api/Infrastructure/AzureSdk/EnvironmentStyleKeyVaultSecretManager.cs apps/api/src/Api/Infrastructure/AzureSdk/AzureConfigurationSources.cs apps/api/src/Api/Program.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Infrastructure/AzureSdk/EnvironmentStyleKeyVaultSecretManagerTests.cs apps/api/tests/Api.Tests/Infrastructure/AzureSdk/AzureConfigurationSourcesTests.cs apps/api/tests/Api.Tests/Integration/Infrastructure/AppConfigurationEmulatorFixture.cs apps/api/tests/Api.Tests/Integration/Infrastructure/AppConfigurationEmulatorIntegrationTests.cs
git commit -m "feat(config): App Configuration (HMAC) e Key Vault come sorgenti IConfiguration" -m "Attive solo con AZURE_APPCONFIG_CONNECTION_STRING / AZURE_APPCONFIG_ENDPOINT / AZURE_KEYVAULT_URI; credenziale fissa per il Key Vault emulator; nomi dei secret in stile variabile d'ambiente." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 5: `IIntegrationEventPublisher`, publisher Service Bus ed evento `PdfUploaded` v1

**Files:**
- Create: `apps/api/src/Api/SharedKernel/Application/Interfaces/IIntegrationEventPublisher.cs`
- Create: `apps/api/src/Api/SharedKernel/Application/Interfaces/IVersionedContract.cs`
- Create: `apps/api/src/Api/Infrastructure/Messaging/NullIntegrationEventPublisher.cs`
- Create: `apps/api/src/Api/Infrastructure/Messaging/ServiceBusIntegrationEventPublisher.cs`
- Create: `apps/api/src/Api/Infrastructure/Messaging/MessagingServiceExtensions.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/IntegrationEvents/PdfUploadedIntegrationEvent.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PdfUploadedIntegrationEventRelay.cs`
- Create: `apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.PdfIntegrationEvents.cs`
- Modify: `apps/api/src/Api/Observability/MeepleAiMetrics.cs` (elenco dei file partial nel commento)
- Modify: `apps/api/src/Api/Program.cs` (dopo `AddAzureSdkHttpClients`)
- Modify: `apps/api/src/Api/Api.csproj`
- Create: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/PdfUploadedEventMetricsCollection.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Messaging/ServiceBusIntegrationEventPublisherTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/PdfUploadedIntegrationEventRelayTests.cs`
- Test: `apps/api/tests/Api.Tests/Infrastructure/Messaging/MessagingServiceExtensionsTests.cs`

**Interfaces:**
- Consumes: `IntegrationEventBase(string sourceContext)`, `PdfStateChangedEvent`, `PdfProcessingState.Pending/Uploading`, `IPdfDocumentRepository.GetByIdAsync(Guid, CancellationToken)`, `PdfDocument.Id/GameId/SharedGameId/FilePath`, `PdfDocumentBuilder` (test helper esistente: `WithId`, `WithFilePath`, `Build`).
- Produces: `IIntegrationEventPublisher.PublishAsync<TEvent>(TEvent, CancellationToken) where TEvent : IntegrationEventBase`; `IVersionedContract { string ContractVersion }` (SharedKernel); `PdfUploadedIntegrationEvent(Guid pdfDocumentId, Guid gameId, Guid? sharedGameId, string filePath)` con `ContractVersion = "v1"` e `CurrentContractVersion`; JSON camelCase con chiavi `eventId, occurredAt, sourceContext, contractVersion, pdfDocumentId, gameId, sharedGameId, filePath`; `ServiceBusIntegrationEventPublisher.JsonOptions` (static, usato dal test di contratto in Task 8); `MeepleAiMetrics.RecordPdfUploadedEvent(string outcome)`, `PdfUploadedEventOutcomes.{Published, PublishFailed, NotFound, Received, Indexed, Noop, Rejected, Failed}`, strumento `meepleai.pdf.uploaded_events.total` SENZA unit (il nome Prometheus resta `meepleai_pdf_uploaded_events_total`); chiavi `AZURE_SERVICEBUS_CONNECTION_STRING` (emulatore/SAS), `AZURE_SERVICEBUS_NAMESPACE` (Azure vero, `DefaultAzureCredential`), `AZURE_SERVICEBUS_QUEUE` (default `pdf-uploaded`).

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

- [ ] **Step 3: Collection non parallela e test del relay** (il `MeterListener` è di processo: la classe gira da sola)

`PdfUploadedEventMetricsCollection.cs`:
```csharp
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

/// <summary>Il MeterListener è di processo e non filtra per test: questa classe non gira in parallelo con altre.</summary>
[CollectionDefinition("PdfUploadedEventMetrics", DisableParallelization = true)]
public sealed class PdfUploadedEventMetricsCollection;
```

`PdfUploadedIntegrationEventRelayTests.cs`:
```csharp
using System.Diagnostics.Metrics;
using Api.BoundedContexts.DocumentProcessing.Application.EventHandlers;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.BoundedContexts.DocumentProcessing.Domain.Entities;
using Api.BoundedContexts.DocumentProcessing.Domain.Enums;
using Api.BoundedContexts.DocumentProcessing.Domain.Events;
using Api.BoundedContexts.DocumentProcessing.Domain.Repositories;
using Api.Observability;
using Api.SharedKernel.Application.Interfaces;
using Api.Tests.BoundedContexts.DocumentProcessing.TestHelpers;
using Api.Tests.Constants;
using FluentAssertions;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

[Collection("PdfUploadedEventMetrics")]
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
        var document = new PdfDocumentBuilder()
            .WithId(id)
            .WithFilePath($"pdfs/{Guid.NewGuid():N}/{Guid.NewGuid():N}_rulebook.pdf")
            .Build();
        _repository.Setup(r => r.GetByIdAsync(id, It.IsAny<CancellationToken>())).ReturnsAsync(document);
        return document;
    }

    private static PdfStateChangedEvent PendingToUploading(Guid pdfId) =>
        new(pdfId, PdfProcessingState.Pending, PdfProcessingState.Uploading, Guid.NewGuid());

    /// <summary>Raccoglie i tag <c>outcome</c> di <c>meepleai.pdf.uploaded_events.total</c> emessi durante <paramref name="action"/>.</summary>
    private static async Task<List<string?>> CollectOutcomesAsync(Func<Task> action)
    {
        var outcomes = new List<string?>();
        using var listener = new MeterListener
        {
            InstrumentPublished = (instrument, l) =>
            {
                if (instrument.Name == "meepleai.pdf.uploaded_events.total")
                {
                    l.EnableMeasurementEvents(instrument);
                }
            },
        };
        listener.SetMeasurementEventCallback<long>((_, _, tags, _) =>
        {
            foreach (var tag in tags)
            {
                if (tag.Key == "outcome")
                {
                    outcomes.Add(tag.Value as string);
                }
            }
        });
        listener.Start();
        await action();
        return outcomes;
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

        var outcomes = await CollectOutcomesAsync(() => _sut.Handle(PendingToUploading(pdfId), CancellationToken.None));

        published.Should().NotBeNull();
        published!.PdfDocumentId.Should().Be(pdfId);
        published.GameId.Should().Be(document.GameId);
        published.FilePath.Should().Be(document.FilePath);
        published.ContractVersion.Should().Be("v1");
        outcomes.Should().Equal(MeepleAiMetrics.PdfUploadedEventOutcomes.Published);
    }

    [Fact]
    public async Task Handle_OtherTransition_DoesNotPublish()
    {
        await _sut.Handle(new PdfStateChangedEvent(Guid.NewGuid(), PdfProcessingState.Uploading, PdfProcessingState.Extracting, Guid.NewGuid()), CancellationToken.None);

        _publisher.Verify(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        _repository.Verify(r => r.GetByIdAsync(It.IsAny<Guid>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_DocumentMissing_DoesNotPublishAndCountsNotFound()
    {
        var pdfId = Guid.NewGuid();
        _repository.Setup(r => r.GetByIdAsync(pdfId, It.IsAny<CancellationToken>())).ReturnsAsync((PdfDocument?)null);

        var outcomes = await CollectOutcomesAsync(() => _sut.Handle(PendingToUploading(pdfId), CancellationToken.None));

        _publisher.Verify(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()), Times.Never);
        outcomes.Should().Equal(MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound);
    }

    [Fact]
    public async Task Handle_PublisherThrows_SwallowsAndCountsFailure()
    {
        var pdfId = Guid.NewGuid();
        SeedDocument(pdfId);
        _publisher.Setup(p => p.PublishAsync(It.IsAny<PdfUploadedIntegrationEvent>(), It.IsAny<CancellationToken>()))
            .ThrowsAsync(new InvalidOperationException("bus down"));

        // L'upload NON deve fallire perché il bus è giù (Review Focus 5): il relay assorbe e conta.
        var act = () => CollectOutcomesAsync(() => _sut.Handle(PendingToUploading(pdfId), CancellationToken.None));

        var outcomes = await act.Should().NotThrowAsync();
        outcomes.Subject.Should().Equal(MeepleAiMetrics.PdfUploadedEventOutcomes.PublishFailed);
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
    private static ServiceProvider Build(params (string Key, string? Value)[] pairs)
    {
        var config = new ConfigurationBuilder()
            .AddInMemoryCollection(pairs.Select(p => new KeyValuePair<string, string?>(p.Key, p.Value)))
            .Build();
        var services = new ServiceCollection();
        services.AddLogging();
        services.AddIntegrationEventPublishing(config);
        return services.BuildServiceProvider();
    }

    [Theory]
    [InlineData(null)]
    [InlineData("")]
    public void NoBus_RegistersNullPublisher(string? value)
    {
        using var sp = Build(("AZURE_SERVICEBUS_CONNECTION_STRING", value), ("AZURE_SERVICEBUS_NAMESPACE", value));

        sp.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<NullIntegrationEventPublisher>();
    }

    [Fact]
    public void ConnectionString_RegistersServiceBusPublisher()
    {
        // La connessione AMQP è lazy: risolvere il publisher non apre socket.
        using var sp = Build(("AZURE_SERVICEBUS_CONNECTION_STRING", "Endpoint=sb://localhost;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;"));

        sp.GetRequiredService<IIntegrationEventPublisher>().Should().BeOfType<ServiceBusIntegrationEventPublisher>();
    }

    [Fact]
    public void Namespace_RegistersServiceBusPublisherWithIdentity()
    {
        using var sp = Build(("AZURE_SERVICEBUS_NAMESPACE", "meepleai-sb.servicebus.windows.net"));

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
/// Pubblica un integration event verso un sistema esterno al processo (spec azure-local §4.3). I domain event
/// restano su MediatR/outbox; questa porta serve ai consumer fuori dall'API (Azure Functions).
/// </summary>
internal interface IIntegrationEventPublisher
{
    Task PublishAsync<TEvent>(TEvent integrationEvent, CancellationToken cancellationToken) where TEvent : IntegrationEventBase;
}
```

`SharedKernel/Application/Interfaces/IVersionedContract.cs`:
```csharp
namespace Api.SharedKernel.Application.Interfaces;

/// <summary>Un integration event con versione di contratto esplicita sul filo (proprietà di messaggio e campo JSON).</summary>
internal interface IVersionedContract
{
    string ContractVersion { get; }
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
```

`Infrastructure/Messaging/MessagingServiceExtensions.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;
using Azure.Identity;
using Azure.Messaging.ServiceBus;

namespace Api.Infrastructure.Messaging;

internal static class MessagingServiceExtensions
{
    public const string ConnectionStringKey = "AZURE_SERVICEBUS_CONNECTION_STRING";
    public const string NamespaceKey = "AZURE_SERVICEBUS_NAMESPACE";
    public const string QueueKey = "AZURE_SERVICEBUS_QUEUE";
    public const string DefaultQueue = "pdf-uploaded";

    /// <summary>
    /// Service Bus parla AMQP, non HTTP: il client <c>AzureSdk</c> pinnato non si applica. L'endpoint viene da
    /// configurazione statica (stessa esenzione di OpenRouter/Resend nel gate di egress). Con la connection
    /// string (emulatore o SAS) o il namespace FQDN (Azure vero, identità Entra) registra il publisher Service Bus;
    /// altrimenti il publisher nullo.
    /// </summary>
    public static IServiceCollection AddIntegrationEventPublishing(this IServiceCollection services, IConfiguration configuration)
    {
        var connectionString = configuration[ConnectionStringKey];
        var fullyQualifiedNamespace = configuration[NamespaceKey];
        var hasConnectionString = !string.IsNullOrWhiteSpace(connectionString);
        var hasNamespace = !string.IsNullOrWhiteSpace(fullyQualifiedNamespace);
        if (!hasConnectionString && !hasNamespace)
        {
            services.AddSingleton<IIntegrationEventPublisher, NullIntegrationEventPublisher>();
            return services;
        }

        var queue = configuration[QueueKey] is { Length: > 0 } q ? q : DefaultQueue;
        var clientOptions = new ServiceBusClientOptions { TransportType = ServiceBusTransportType.AmqpTcp };
        services.AddSingleton(_ => hasConnectionString
            ? new ServiceBusClient(connectionString, clientOptions)
            : new ServiceBusClient(fullyQualifiedNamespace!.Trim(), new DefaultAzureCredential(), clientOptions));
        services.AddSingleton(sp => sp.GetRequiredService<ServiceBusClient>().CreateSender(queue));
        services.AddSingleton<IIntegrationEventPublisher, ServiceBusIntegrationEventPublisher>();
        return services;
    }
}
```

`BoundedContexts/DocumentProcessing/Application/IntegrationEvents/PdfUploadedIntegrationEvent.cs`:
```csharp
using Api.SharedKernel.Application.Interfaces;
using Api.SharedKernel.Domain.Events;

namespace Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;

/// <summary>
/// Contratto v1 (spec azure-local §4.3): un PDF è stato ricevuto dall'API e la pipeline è partita (transizione
/// Pending → Uploading; a quel punto il blob è già stato scritto e <c>FilePath</c> è valorizzato). Chiavi sul
/// filo congelate in <c>tests/fixtures/contracts/pdf-uploaded.v1.json</c>; cambiarle significa una v2.
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
/// <see cref="PdfUploadedIntegrationEvent"/>. Mai un'eccezione verso l'upload: il bus giù non deve far fallire
/// la richiesta dell'utente (Review Focus 5); l'esito finisce nel contatore.
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

    /// <summary>Senza unit: l'exporter Prometheus mette l'unit nel nome, e la spec fissa `meepleai_pdf_uploaded_events_total`.</summary>
    public static readonly Counter<long> PdfUploadedEventsTotal = Meter.CreateCounter<long>(
        name: "meepleai.pdf.uploaded_events.total",
        description: "PdfUploaded integration events by outcome (publish side and internal consumer side)");

    public static void RecordPdfUploadedEvent(string outcome) =>
        PdfUploadedEventsTotal.Add(1, new TagList { { "outcome", outcome } });
}
```

Nel commento di testa di `Observability/MeepleAiMetrics.cs` aggiungere la riga `///   - MeepleAiMetrics.PdfIntegrationEvents.cs — PdfUploaded integration event outcomes (spec azure-local)`.

In `Program.cs`, dopo `builder.Services.AddAzureSdkHttpClients(builder.Configuration);`:
```csharp
builder.Services.AddIntegrationEventPublishing(builder.Configuration); // spec azure-local §4.3
```
con `using Api.Infrastructure.Messaging;`. Il relay è un `INotificationHandler` nell'assembly dell'API: la registrazione MediatR via scansione dell'assembly lo prende (verifica: `grep -rn "RegisterServicesFromAssembly" apps/api/src/Api`).

- [ ] **Step 7: Eseguire i test**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServiceBusIntegrationEventPublisherTests|FullyQualifiedName~PdfUploadedIntegrationEventRelayTests|FullyQualifiedName~MessagingServiceExtensionsTests"` → Expected: verdi.

- [ ] **Step 8: Commit**

```bash
git add apps/api/src/Api/SharedKernel/Application/Interfaces/IIntegrationEventPublisher.cs apps/api/src/Api/SharedKernel/Application/Interfaces/IVersionedContract.cs apps/api/src/Api/Infrastructure/Messaging/NullIntegrationEventPublisher.cs apps/api/src/Api/Infrastructure/Messaging/ServiceBusIntegrationEventPublisher.cs apps/api/src/Api/Infrastructure/Messaging/MessagingServiceExtensions.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/IntegrationEvents/PdfUploadedIntegrationEvent.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/EventHandlers/PdfUploadedIntegrationEventRelay.cs apps/api/src/Api/Observability/Metrics/MeepleAiMetrics.PdfIntegrationEvents.cs apps/api/src/Api/Observability/MeepleAiMetrics.cs apps/api/src/Api/Program.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/PdfUploadedEventMetricsCollection.cs apps/api/tests/Api.Tests/Infrastructure/Messaging/ServiceBusIntegrationEventPublisherTests.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/PdfUploadedIntegrationEventRelayTests.cs apps/api/tests/Api.Tests/Infrastructure/Messaging/MessagingServiceExtensionsTests.cs
git commit -m "feat(messaging): PdfUploaded v1 su Service Bus via IIntegrationEventPublisher" -m "Relay sulla transizione Pending→Uploading, publisher Service Bus (SAS o identità) e Null, contatore degli esiti." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 6: Endpoint interno `POST /api/v1/internal/pdf-events/uploaded` con token di servizio e indicizzazione «se inattivo»

**Files:**
- Create: `apps/api/src/Api/Filters/InternalServiceTokenFilter.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfIfIdleCommand.cs`
- Create: `apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfIfIdleCommandHandler.cs`
- Create: `apps/api/src/Api/Routing/InternalPdfEventsEndpoints.cs`
- Modify: `apps/api/src/Api/Program.cs` (dopo `v1Api.MapAdminPdfManagementEndpoints();`)
- Modify: `apps/api/tests/Api.Tests/Architecture/ConstantTimeComparisonArchitectureTests.cs` (`ConstantTimeSites`)
- Test: `apps/api/tests/Api.Tests/Filters/InternalServiceTokenFilterTests.cs`
- Test: `apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/IndexPdfIfIdleCommandHandlerTests.cs`
- Test: `apps/api/tests/Api.Tests/Routing/InternalPdfEventsEndpointsTests.cs`

**Interfaces:**
- Consumes: `IndexPdfCommand(string PdfId)` → `IndexingResultDto { Success, ErrorCode, ErrorMessage, ChunkCount }`; `MeepleAiDbContext.PdfDocuments` (`PdfDocumentEntity.Id : Guid`, `ProcessingState : string` con i nomi dell'enum), `ProcessingJobs` (`ProcessingJobEntity.PdfDocumentId`, `Status : string` con valori `Queued`/`Processing`/`Completed`/`Failed`), `VectorDocuments` (`VectorDocumentEntity.PdfDocumentId : Guid`); `TestDbContextFactory.CreateInMemoryDbContext()` e il seeding di `PdfDocumentEntity` come in `IndexPdfCommandHandlerTests` (copiare da lì l'helper che costruisce l'entità); `MeepleAiMetrics.RecordPdfUploadedEvent`, `PdfUploadedIntegrationEvent.CurrentContractVersion` (Task 5); `SharedHostPerTestDatabaseFixture`.
- Produces: header `X-Internal-Service-Token`, chiave `INTERNAL_SERVICE_TOKEN`; `IndexPdfIfIdleCommand(Guid PdfDocumentId) : ICommand<IndexPdfIfIdleResult>`; `IndexPdfIfIdleResult(IndexPdfIfIdleOutcome Outcome, IndexingResultDto? Indexing)`; `IndexPdfIfIdleOutcome { NotFound, PipelineBusy, AlreadyIndexed, Indexed, Failed }`; richiesta `PdfUploadedEventRequest(Guid EventId, Guid PdfDocumentId, string? ContractVersion)`; risposte 200 `{outcome:"indexed"}`, 200 `{outcome:"noop", reason:"already_indexed"}`, 202 `{outcome:"noop", reason:"pipeline_busy"}`, 404 `{outcome:"not_found"}`, 400 `{outcome:"rejected"}`, 422 `{outcome:"failed"}`; 401/403/404 dal filtro.

Cosa l'evento deve CAUSARE (ruling, recepito nella spec in Task 10): l'upload avvia già la pipeline nell'API, quindi `IndexPdfCommand` chiamato alla cieca ri-indicizzerebbe un documento già indicizzato o in lavorazione. L'endpoint indicizza SOLO un PDF inattivo che ne ha bisogno: stato `Failed`, oppure `Ready` senza `VectorDocument`. Pipeline in corso (stato intermedio o `ProcessingJob` attivo) e `Ready` con vettori sono `noop`.

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

- [ ] **Step 2: Implementare il filtro e aggiornare l'inventario**

```csharp
using System.Security.Cryptography;
using System.Text;

namespace Api.Filters;

/// <summary>
/// Autenticazione servizio-a-servizio per il gruppo <c>/api/v1/internal</c> (spec azure-local §4.3): un token
/// condiviso in <c>INTERNAL_SERVICE_TOKEN</c>, confrontato a tempo costante. Senza token configurato il gruppo NON
/// esiste (404 per tutti): un deployment che dimentica la variabile non espone un endpoint aperto (Review Focus 4).
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

In `ConstantTimeComparisonArchitectureTests.cs`, dentro `ConstantTimeSites`, dopo la riga Slack:
```csharp
        ("Filters/InternalServiceTokenFilter.cs", "token di servizio interno X-Internal-Service-Token (spec azure-local)"),
```

- [ ] **Step 3: Eseguire filtro + gate**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~InternalServiceTokenFilterTests|FullyQualifiedName~ConstantTimeComparisonArchitectureTests"` → Expected: verdi.

- [ ] **Step 4: Test del comando «se inattivo»**

```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.Infrastructure;
using Api.Infrastructure.Entities.DocumentProcessing;
using Api.Infrastructure.Entities.KnowledgeBase;
using Api.Tests.Constants;
using Api.Tests.Infrastructure;
using FluentAssertions;
using MediatR;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;

namespace Api.Tests.BoundedContexts.DocumentProcessing.Application.Handlers;

[Trait("Category", TestCategories.Unit)]
[Trait("BoundedContext", "DocumentProcessing")]
public sealed class IndexPdfIfIdleCommandHandlerTests : IDisposable
{
    private readonly MeepleAiDbContext _db = TestDbContextFactory.CreateInMemoryDbContext();
    private readonly Mock<IMediator> _mediator = new();
    private readonly IndexPdfIfIdleCommandHandler _sut;

    public IndexPdfIfIdleCommandHandlerTests()
    {
        _sut = new IndexPdfIfIdleCommandHandler(_db, _mediator.Object, Mock.Of<ILogger<IndexPdfIfIdleCommandHandler>>());
    }

    public void Dispose() => _db.Dispose();

    // Copiare da IndexPdfCommandHandlerTests l'helper che costruisce una PdfDocumentEntity valida e
    // parametrizzare qui solo Id e ProcessingState.
    private async Task<Guid> SeedPdfAsync(string state)
    {
        var pdf = IndexPdfCommandHandlerTests.CreateTestPdfEntity(); // rendere `internal static` l'helper esistente se è privato
        pdf.ProcessingState = state;
        _db.PdfDocuments.Add(pdf);
        await _db.SaveChangesAsync();
        return pdf.Id;
    }

    [Fact]
    public async Task Handle_UnknownPdf_NotFound()
    {
        var result = await _sut.Handle(new IndexPdfIfIdleCommand(Guid.NewGuid()), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.NotFound);
        _mediator.Verify(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Uploading")]
    [InlineData("Extracting")]
    [InlineData("Indexing")]
    public async Task Handle_PipelineState_PipelineBusyWithoutIndexing(string state)
    {
        var id = await SeedPdfAsync(state);

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.PipelineBusy);
        _mediator.Verify(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Fact]
    public async Task Handle_ActiveProcessingJob_PipelineBusy()
    {
        var id = await SeedPdfAsync("Failed");
        _db.ProcessingJobs.Add(new ProcessingJobEntity { Id = Guid.NewGuid(), PdfDocumentId = id, Status = "Queued" });
        await _db.SaveChangesAsync();

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.PipelineBusy);
    }

    [Fact]
    public async Task Handle_ReadyWithVectors_AlreadyIndexed()
    {
        var id = await SeedPdfAsync("Ready");
        _db.VectorDocuments.Add(new VectorDocumentEntity { Id = Guid.NewGuid(), PdfDocumentId = id, IndexingStatus = "completed" });
        await _db.SaveChangesAsync();

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.AlreadyIndexed);
        _mediator.Verify(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()), Times.Never);
    }

    [Theory]
    [InlineData("Failed")]
    [InlineData("Ready")]
    public async Task Handle_IdleWithoutVectors_SendsIndexPdfCommand(string state)
    {
        var id = await SeedPdfAsync(state);
        _mediator.Setup(m => m.Send(It.Is<IndexPdfCommand>(c => c.PdfId == id.ToString()), It.IsAny<CancellationToken>()))
            .ReturnsAsync(IndexingResultDto.CreateSuccess("vec-1", 3, DateTime.UtcNow));

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.Indexed);
        result.Indexing!.ChunkCount.Should().Be(3);
    }

    [Fact]
    public async Task Handle_IndexingFails_Failed()
    {
        var id = await SeedPdfAsync("Failed");
        _mediator.Setup(m => m.Send(It.IsAny<IndexPdfCommand>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new IndexingResultDto { Success = false, ErrorCode = PdfIndexingErrorCode.EmbeddingFailed, ErrorMessage = "boom" });

        var result = await _sut.Handle(new IndexPdfIfIdleCommand(id), CancellationToken.None);

        result.Outcome.Should().Be(IndexPdfIfIdleOutcome.Failed);
        result.Indexing!.ErrorMessage.Should().Be("boom");
    }
}
```
Le proprietà minime di `ProcessingJobEntity` e `VectorDocumentEntity` usate sopra (`Id`, `PdfDocumentId`, `Status`/`IndexingStatus`) vanno confermate leggendo le due classi in `Infrastructure/Entities/`; se una proprietà `required` manca, aggiungerla nell'inizializzatore con un valore qualunque valido.

- [ ] **Step 5: Implementare comando e handler**

`IndexPdfIfIdleCommand.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.DTOs;
using Api.SharedKernel.Application.Interfaces;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>Indicizza un PDF SOLO se è inattivo e ne ha bisogno (Failed, oppure Ready senza vettori). Spec azure-local §4.3.</summary>
internal sealed record IndexPdfIfIdleCommand(Guid PdfDocumentId) : ICommand<IndexPdfIfIdleResult>;

internal enum IndexPdfIfIdleOutcome
{
    NotFound,
    PipelineBusy,
    AlreadyIndexed,
    Indexed,
    Failed,
}

internal sealed record IndexPdfIfIdleResult(IndexPdfIfIdleOutcome Outcome, IndexingResultDto? Indexing);
```

`IndexPdfIfIdleCommandHandler.cs`:
```csharp
using Api.Infrastructure;
using Api.SharedKernel.Application.Interfaces;
using MediatR;
using Microsoft.EntityFrameworkCore;

namespace Api.BoundedContexts.DocumentProcessing.Application.Commands;

/// <summary>
/// Le guardie di idempotenza del repo stanno nei handler di coda (<c>ProcessingJob</c>) e nello stato del
/// documento, NON in <see cref="IndexPdfCommand"/>, che ri-indicizza sempre. Questo handler le applica prima di
/// delegare: un messaggio riconsegnato o duplicato non deve far ripartire una pipeline già in corso o conclusa.
/// </summary>
internal sealed class IndexPdfIfIdleCommandHandler : ICommandHandler<IndexPdfIfIdleCommand, IndexPdfIfIdleResult>
{
    private static readonly string[] PipelineStates = ["Pending", "Uploading", "Extracting", "Chunking", "Embedding", "Indexing"];
    private static readonly string[] ActiveJobStatuses = ["Queued", "Processing"];

    private readonly MeepleAiDbContext _db;
    private readonly IMediator _mediator;
    private readonly ILogger<IndexPdfIfIdleCommandHandler> _logger;

    public IndexPdfIfIdleCommandHandler(MeepleAiDbContext db, IMediator mediator, ILogger<IndexPdfIfIdleCommandHandler> logger)
    {
        _db = db;
        _mediator = mediator;
        _logger = logger;
    }

    public async Task<IndexPdfIfIdleResult> Handle(IndexPdfIfIdleCommand request, CancellationToken cancellationToken)
    {
        var pdf = await _db.PdfDocuments.AsNoTracking()
            .Where(p => p.Id == request.PdfDocumentId)
            .Select(p => new { p.Id, p.ProcessingState })
            .FirstOrDefaultAsync(cancellationToken).ConfigureAwait(false);
        if (pdf is null)
        {
            return new IndexPdfIfIdleResult(IndexPdfIfIdleOutcome.NotFound, null);
        }

        var activeJob = await _db.ProcessingJobs.AsNoTracking()
            .AnyAsync(j => j.PdfDocumentId == pdf.Id && ActiveJobStatuses.Contains(j.Status), cancellationToken).ConfigureAwait(false);
        if (activeJob || PipelineStates.Contains(pdf.ProcessingState, StringComparer.Ordinal))
        {
            _logger.LogInformation("PDF {PdfId} pipeline busy (state {State}, activeJob {ActiveJob}): no re-index", pdf.Id, pdf.ProcessingState, activeJob);
            return new IndexPdfIfIdleResult(IndexPdfIfIdleOutcome.PipelineBusy, null);
        }

        var hasVectors = await _db.VectorDocuments.AsNoTracking()
            .AnyAsync(v => v.PdfDocumentId == pdf.Id, cancellationToken).ConfigureAwait(false);
        if (string.Equals(pdf.ProcessingState, "Ready", StringComparison.Ordinal) && hasVectors)
        {
            return new IndexPdfIfIdleResult(IndexPdfIfIdleOutcome.AlreadyIndexed, null);
        }

        _logger.LogInformation("PDF {PdfId} idle (state {State}, vectors {HasVectors}): indexing", pdf.Id, pdf.ProcessingState, hasVectors);
        var indexing = await _mediator.Send(new IndexPdfCommand(pdf.Id.ToString()), cancellationToken).ConfigureAwait(false);
        return new IndexPdfIfIdleResult(indexing.Success ? IndexPdfIfIdleOutcome.Indexed : IndexPdfIfIdleOutcome.Failed, indexing);
    }
}
```

- [ ] **Step 6: Eseguire i test del comando**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~IndexPdfIfIdleCommandHandlerTests"` → Expected: verdi.

- [ ] **Step 7: Test dell'endpoint con la fixture host condivisa**

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

- [ ] **Step 8: Implementare l'endpoint**

`Routing/InternalPdfEventsEndpoints.cs`:
```csharp
using Api.BoundedContexts.DocumentProcessing.Application.Commands;
using Api.BoundedContexts.DocumentProcessing.Application.IntegrationEvents;
using Api.Filters;
using Api.Observability;
using MediatR;

namespace Api.Routing;

/// <summary>Corpo accettato dal consumer interno: il sottoinsieme del contratto v1 che l'API deve rileggere.</summary>
internal sealed record PdfUploadedEventRequest(Guid EventId, Guid PdfDocumentId, string? ContractVersion);

/// <summary>
/// Lato API del ciclo PdfUploaded (spec azure-local §4.3): la Function consuma il messaggio dal bus e richiama
/// qui. L'indicizzazione passa da <see cref="IndexPdfIfIdleCommand"/>: parte SOLO per un PDF inattivo che ne ha
/// bisogno; pipeline in corso e documenti già indicizzati sono <c>noop</c>.
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
        var result = await mediator.Send(new IndexPdfIfIdleCommand(body.PdfDocumentId), ct).ConfigureAwait(false);

        switch (result.Outcome)
        {
            case IndexPdfIfIdleOutcome.Indexed:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Indexed);
                return Results.Ok(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Indexed, chunkCount = result.Indexing?.ChunkCount });
            case IndexPdfIfIdleOutcome.AlreadyIndexed:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Noop);
                return Results.Ok(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Noop, reason = "already_indexed" });
            case IndexPdfIfIdleOutcome.PipelineBusy:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Noop);
                return Results.Accepted(value: new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Noop, reason = "pipeline_busy" });
            case IndexPdfIfIdleOutcome.NotFound:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound);
                return Results.NotFound(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.NotFound });
            default:
                MeepleAiMetrics.RecordPdfUploadedEvent(MeepleAiMetrics.PdfUploadedEventOutcomes.Failed);
                return Results.UnprocessableEntity(new { outcome = MeepleAiMetrics.PdfUploadedEventOutcomes.Failed, error = result.Indexing?.ErrorMessage });
        }
    }
}
```

In `Program.cs`, dopo `v1Api.MapAdminPdfManagementEndpoints();`:
```csharp
v1Api.MapInternalPdfEventsEndpoints(); // spec azure-local §4.3: consumer interno del PdfUploaded v1
```

- [ ] **Step 9: Eseguire i test dell'endpoint**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~InternalPdfEventsEndpointsTests"` → Expected: 4 superati (Docker per Postgres/Redis della fixture condivisa). Se un test di architettura esistente enumera gli endpoint per classe di autorizzazione (`grep -rln "internal" apps/api/tests/Api.Tests/Architecture/*Endpoint*`), aggiungere il gruppo con la classe «token di servizio».

- [ ] **Step 10: Commit**

```bash
git add apps/api/src/Api/Filters/InternalServiceTokenFilter.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfIfIdleCommand.cs apps/api/src/Api/BoundedContexts/DocumentProcessing/Application/Commands/IndexPdfIfIdleCommandHandler.cs apps/api/src/Api/Routing/InternalPdfEventsEndpoints.cs apps/api/src/Api/Program.cs apps/api/tests/Api.Tests/Architecture/ConstantTimeComparisonArchitectureTests.cs apps/api/tests/Api.Tests/Filters/InternalServiceTokenFilterTests.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/IndexPdfIfIdleCommandHandlerTests.cs apps/api/tests/Api.Tests/BoundedContexts/DocumentProcessing/Application/Handlers/IndexPdfCommandHandlerTests.cs apps/api/tests/Api.Tests/Routing/InternalPdfEventsEndpointsTests.cs
git commit -m "feat(api): endpoint interno PdfUploaded con token e indicizzazione se inattivo" -m "IndexPdfIfIdleCommand applica le guardie (stato, ProcessingJob, vettori) prima di delegare a IndexPdfCommand; filtro a tempo costante nell'inventario." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---
### Task 7: Test di integrazione sul Service Bus emulator (publish → receive → dead-letter), fixture per classe

**Files:**
- Modify: `apps/api/tests/Api.Tests/Api.Tests.csproj`
- Modify: `apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs`
- Create: `apps/api/tests/Api.Tests/Integration/Infrastructure/ServiceBusEmulatorFixture.cs`
- Test: `apps/api/tests/Api.Tests/Integration/Infrastructure/ServiceBusEmulatorIntegrationTests.cs`

**Interfaces:**
- Consumes: `ServiceBusIntegrationEventPublisher`, `PdfUploadedIntegrationEvent` (Task 5), `EmulatorFixtureBase` (Task 3).
- Produces: `TestcontainersConfiguration.ServiceBusEmulatorImage`, `ServiceBusEmulatorDefaultQueue = "queue.1"`.

- [ ] **Step 1: Pacchetto e costanti**

Run (da `apps/api/tests/Api.Tests`): `dotnet add package Testcontainers.ServiceBus --version 4.14.0`

In `TestcontainersConfiguration.cs`, dopo `AzuriteTestContainer`:
```csharp
    /// <summary>Service Bus emulator (spec azure-local): L3; avvia da sé un MSSQL di appoggio (~1 min, una volta per classe).</summary>
    public const string ServiceBusEmulatorImage = "mcr.microsoft.com/azure-messaging/servicebus-emulator:2.0.1";
    /// <summary>Coda presente nel Config.json di default dell'emulatore.</summary>
    public const string ServiceBusEmulatorDefaultQueue = "queue.1";
```

- [ ] **Step 2: Fixture**

```csharp
using Api.Tests.Infrastructure;
using Azure.Messaging.ServiceBus;
using Testcontainers.ServiceBus;

namespace Api.Tests.Integration.Infrastructure;

public sealed class ServiceBusEmulatorFixture : EmulatorFixtureBase
{
    private ServiceBusContainer? _container;

    public ServiceBusClient Client { get; private set; } = null!;

    protected override string EmulatorName => "Service Bus emulator";

    protected override async Task StartAsync()
    {
        _container = new ServiceBusBuilder()
            .WithImage(TestcontainersConfiguration.ServiceBusEmulatorImage)
            .WithAcceptLicenseAgreement(true)
            .Build();
        await _container.StartAsync();
        Client = new ServiceBusClient(_container.GetConnectionString());
    }

    protected override async Task StopAsync()
    {
        if (Client is not null)
        {
            await Client.DisposeAsync();
        }

        if (_container is not null)
        {
            await _container.DisposeAsync();
        }
    }
}
```

- [ ] **Step 3: Test**

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
using Xunit;

namespace Api.Tests.Integration.Infrastructure;

[Trait("Category", TestCategories.Integration)]
[Trait("Category", TestCategories.Slow)]
[Trait("BoundedContext", "Infrastructure")]
[Trait("Feature", "AzureServiceBus")]
public sealed class ServiceBusEmulatorIntegrationTests : IClassFixture<ServiceBusEmulatorFixture>
{
    private readonly ServiceBusEmulatorFixture _emulator;

    public ServiceBusEmulatorIntegrationTests(ServiceBusEmulatorFixture emulator)
    {
        _emulator = emulator;
    }

    [Fact]
    public async Task Publish_ThenReceive_RoundTripsTheContract()
    {
        _emulator.SkipIfUnavailable();
        await using var sender = _emulator.Client.CreateSender(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var receiver = _emulator.Client.CreateReceiver(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
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
        _emulator.SkipIfUnavailable();
        await using var sender = _emulator.Client.CreateSender(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var receiver = _emulator.Client.CreateReceiver(TestcontainersConfiguration.ServiceBusEmulatorDefaultQueue);
        await using var deadLetters = _emulator.Client.CreateReceiver(
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

- [ ] **Step 4: Eseguire**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~ServiceBusEmulatorIntegrationTests"`
Expected: 2 superati; il primo avvio scarica emulatore e MSSQL (alcuni minuti: il pull non conta nel `methodTimeout` perché avviene nella fixture di classe, ma se supera il timeout della fixture riportarlo). Senza Docker: 2 salti `PREVISTO:`.

- [ ] **Step 5: Commit**

```bash
git add apps/api/tests/Api.Tests/Api.Tests.csproj apps/api/tests/Api.Tests/Infrastructure/TestcontainersConfiguration.cs apps/api/tests/Api.Tests/Integration/Infrastructure/ServiceBusEmulatorFixture.cs apps/api/tests/Api.Tests/Integration/Infrastructure/ServiceBusEmulatorIntegrationTests.cs
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
- Modify: `.gitignore` (radice): riga `apps/functions/**/local.settings.json`

**Interfaces:**
- Consumes: formato di filo di `PdfUploadedIntegrationEvent` e `ServiceBusIntegrationEventPublisher.JsonOptions` (Task 5); endpoint `POST /api/v1/internal/pdf-events/uploaded` + header `X-Internal-Service-Token` + esiti 2xx/400/404/422 (Task 6); porte host 20000–20004 (Task 1).
- Produces: fixture `pdf-uploaded.v1.json`; app settings della Function `ServiceBusConnection`, `ServiceBusQueue`, `MeepleAiApiBaseUrl`, `MeepleAiInternalServiceToken`.

- [ ] **Step 1: Fixture di contratto** `tests/fixtures/contracts/pdf-uploaded.v1.json`

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
using Api.Tests.Architecture;
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
        while (dir is not null && !SourceScanner.IsRepoRoot(dir.FullName))
        {
            dir = dir.Parent;
        }

        dir.Should().NotBeNull("the test binary must live inside the repo (or a worktree)");
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

`MeepleAI.Functions.csproj`:
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
Se `dotnet restore` rifiuta la coppia `Worker 2.52.0` / `Worker.Sdk 2.1.0`, usare le versioni che il restore propone (`dotnet add package … ` senza `--version`) e riportarle nel report: le due versioni sono state lette dal feed ma non ne è stata verificata la compatibilità.

`host.json`:
```json
{
  "version": "2.0",
  "logging": { "logLevel": { "default": "Information" } },
  "extensions": { "serviceBus": { "prefetchCount": 0, "autoCompleteMessages": false, "maxConcurrentCalls": 4 } }
}
```

`local.settings.json.example` (il file reale è git-ignorato; porte host del profilo `azure-local` con base 20000):
```json
{
  "IsEncrypted": false,
  "Values": {
    "AzureWebJobsStorage": "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://127.0.0.1:20000/devstoreaccount1;QueueEndpoint=http://127.0.0.1:20001/devstoreaccount1;TableEndpoint=http://127.0.0.1:20002/devstoreaccount1;",
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
    /// <summary>L'API ha risposto in modo definitivo (2xx, 400, 404, 422): il messaggio si completa.</summary>
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
/// Consumer della coda <c>pdf-uploaded</c> (spec azure-local §4.3). Settlement esplicito: contratto invalido in
/// dead-letter con motivo (Review Focus 3), guasto transitorio abbandonato (Service Bus riconsegna fino a
/// MaxDeliveryCount), esito definitivo completato.
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

- [ ] **Step 4: Progetto di test**

`MeepleAI.Functions.Tests.csproj` (versioni = quelle di `apps/api/tests/Api.Tests/Api.Tests.csproj`):
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
    <PackageReference Include="Microsoft.NET.Test.Sdk" Version="18.0.1" />
    <PackageReference Include="xunit.v3" Version="3.2.1" />
    <PackageReference Include="xunit.runner.visualstudio" Version="3.1.5" />
    <PackageReference Include="FluentAssertions" Version="8.8.0" />
    <PackageReference Include="NSubstitute" Version="5.3.0" />
  </ItemGroup>
  <ItemGroup>
    <ProjectReference Include="../MeepleAI.Functions/MeepleAI.Functions.csproj" />
  </ItemGroup>
</Project>
```

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
        PdfUploadedContract.TryParse(BinaryData.FromString("{not json")).Reason.Should().Be(PdfUploadedContract.ReasonMalformed);
    }

    [Fact]
    public void TryParse_EmptyPdfId_IsRejected()
    {
        var json = ContractFixture.ReadV1().Replace("7b2d9e4a-1c3f-4a5b-9d8e-2f1a3b4c5d6e", Guid.Empty.ToString(), StringComparison.Ordinal);

        PdfUploadedContract.TryParse(BinaryData.FromString(json)).Reason.Should().Be(PdfUploadedContract.ReasonMissingPdfId);
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
        handler.LastRequest.Headers.GetValues(PdfUploadedForwarder.TokenHeader).Should().ContainSingle().Which.Should().Be("t");
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
Se nella 5.24.0 `DeadLetterMessageAsync`/`AbandonMessageAsync` hanno `propertiesToModify` prima del motivo, gli argomenti nominati compilano comunque; con l'argomento omesso NSubstitute confronta `null` con `null`.

- [ ] **Step 5: Build e test**

Run (da `apps/functions`):
```bash
dotnet new sln -n MeepleAI.Functions
dotnet sln add MeepleAI.Functions/MeepleAI.Functions.csproj MeepleAI.Functions.Tests/MeepleAI.Functions.Tests.csproj
dotnet test
```
Expected: compilazione verde; 4 (contratto) + 8 (forwarder: 5 + 2 + 1) + 4 (consumer) = 16 superati.

- [ ] **Step 6: Commit**

```bash
git add tests/fixtures/contracts/pdf-uploaded.v1.json apps/functions/MeepleAI.Functions.sln apps/functions/MeepleAI.Functions apps/functions/MeepleAI.Functions.Tests apps/api/tests/Api.Tests/Contracts/PdfUploadedContractTests.cs .gitignore
git commit -m "feat(functions): consumer PdfUploaded su Azure Functions isolated" -m "Contratto v1 congelato in tests/fixtures/contracts e verificato da API e Function; dead-letter sui contratti invalidi, abandon sui guasti transitori." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```
(`git add apps/functions/MeepleAI.Functions` aggiunge una cartella nuova e tutta del task: `git status --short` prima del commit deve mostrare solo file di questo task.)

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
- Produces: `OtlpExport.IsEnabled(IConfiguration)`, `OtlpExport.Endpoint(IConfiguration)`, `OtlpExport.ProfileName(IConfiguration)`; attributo risorsa `meepleai.profile` (`deployment.environment` resta `EnvironmentName`: emendamento di spec in Task 10).

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
    private static IConfiguration Config(string? endpoint, string? profile = null) =>
        new ConfigurationBuilder()
            .AddInMemoryCollection(new Dictionary<string, string?> { ["OTEL_EXPORTER_OTLP_ENDPOINT"] = endpoint, ["MEEPLEAI_PROFILE"] = profile })
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

    [Fact]
    public void ProfileName_DefaultsToDefault()
    {
        OtlpExport.ProfileName(Config(null)).Should().Be("default");
        OtlpExport.ProfileName(Config(null, "azure-local")).Should().Be("azure-local");
    }
}
```

- [ ] **Step 3: Implementare**

`Observability/OtlpExport.cs`:
```csharp
namespace Api.Observability;

/// <summary>
/// Gate unico dell'export OTLP (spec azure-local §4.4): trace e metriche OpenTelemetry e i log Serilog vanno
/// all'endpoint di <c>OTEL_EXPORTER_OTLP_ENDPOINT</c> quando è valorizzato (Aspire Dashboard in locale, Azure
/// Monitor via collector in Azure). Prometheus e Seq restano sempre attivi.
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

In `ObservabilityServiceExtensions.AddOpenTelemetryServices`: aggiungere `["meepleai.profile"] = OtlpExport.ProfileName(configuration)` al dizionario degli attributi (`deployment.environment` resta `environment.EnvironmentName`: cambiarlo romperebbe le dashboard esistenti); passare il gate: `.WithTracing(tracing => tracing.ConfigureTracing(OtlpExport.IsEnabled(configuration)))`, `.WithMetrics(metrics => metrics.ConfigureMetrics(OtlpExport.IsEnabled(configuration)))`. Le firme diventano `ConfigureTracing(this TracerProviderBuilder tracing, bool exportOtlp)` e `ConfigureMetrics(this MeterProviderBuilder metrics, bool exportOtlp)`; `ConfigureTracing` oggi è un'unica espressione `return tracing.SetSampler(...)…;`: spezzarla in `tracing.SetSampler(...)...;` + 

```csharp
        if (exportOtlp)
        {
            // L'exporter legge OTEL_EXPORTER_OTLP_ENDPOINT / OTEL_EXPORTER_OTLP_PROTOCOL dall'ambiente.
            tracing.AddOtlpExporter();
        }

        return tracing;
```
e lo stesso per `metrics`, mantenendo `.AddPrometheusExporter()` prima dell'`if`.

In `LoggingConfiguration.ConfigureSerilog`, dopo il blocco Seq, con `using Api.Observability;`:
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

- [ ] **Step 4: Test e build**

Run: `dotnet test tests/Api.Tests --filter "FullyQualifiedName~OtlpExportTests"` → Expected: 6 superati. Run: `dotnet build src/Api` → verde senza warning nuovi.

- [ ] **Step 5: Commit**

```bash
git add apps/api/src/Api/Observability/OtlpExport.cs apps/api/src/Api/Extensions/ObservabilityServiceExtensions.cs apps/api/src/Api/Logging/LoggingConfiguration.cs apps/api/src/Api/Api.csproj apps/api/tests/Api.Tests/Observability/OtlpExportTests.cs
git commit -m "feat(observability): export OTLP di trace, metriche e log" -m "Attivo solo con OTEL_EXPORTER_OTLP_ENDPOINT; Prometheus e Seq invariati." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

### Task 10: Bicep in `infra/azure/`, workflow CI, emendamenti alla spec e checklist manuale

**Files:**
- Create: `infra/azure/main.bicep`, `infra/azure/main.bicepparam`, `infra/azure/azure.yaml`, `infra/azure/modules/{monitoring,storage,servicebus,keyvault,appconfig,postgres,redis,containerapps,functions}.bicep`
- Create: `.github/workflows/azure-line.yml`
- Modify: `docs/for-developers/specs/2026-10-09-azure-local-profile-design.md`
- Modify: `docs/for-developers/operations/azure-local-profile.md` (sezione «Verifica manuale»)
- Modify: `docs/superpowers/plans/2026-10-09-azure-local-profile-plan-notes.md` (nota «Eventi»: la transizione segue lo store, non lo precede)
- Modify: `CLAUDE.md`

**Interfaces:**
- Consumes: nomi `pdf-uploaded`, `meepleai-uploads`; chiavi `AZURE_SERVICEBUS_NAMESPACE` (Task 5), `Azure:Sdk:AllowedHostSuffixes` (Task 2), `AZURE_APPCONFIG_ENDPOINT`/`AZURE_KEYVAULT_URI` (Task 4).
- Produces: workflow `azure-line.yml` (job `bicep`, `functions-tests`).

- [ ] **Step 1: Bicep CLI locale**

Run (Git Bash, nello scratchpad): `curl -sSL -o bicep.exe https://github.com/Azure/bicep/releases/download/v0.48.1/bicep-win-x64.exe && ./bicep.exe --version` → Expected: `0.48.1`. Il binario resta nello scratchpad.

- [ ] **Step 2: Moduli Bicep**

`infra/azure/main.bicep`:
```bicep
targetScope = 'resourceGroup'

@description('Prefisso dei nomi (minuscole).')
@minLength(3)
@maxLength(8)
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

var suffix = take(uniqueString(resourceGroup().id), 10)

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
    logAnalyticsWorkspaceName: monitoring.outputs.workspaceName
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

output workspaceName string = workspace.name
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
param logAnalyticsWorkspaceName string
param appInsightsConnectionString string
param storageAccountBlobEndpoint string
param serviceBusNamespace string
param keyVaultUri string
param appConfigEndpoint string

resource workspace 'Microsoft.OperationalInsights/workspaces@2023-09-01' existing = {
  name: logAnalyticsWorkspaceName
}

resource environment 'Microsoft.App/managedEnvironments@2024-03-01' = {
  name: '${namePrefix}-cae'
  location: location
  properties: {
    appLogsConfiguration: {
      destination: 'log-analytics'
      logAnalyticsConfiguration: {
        customerId: workspace.properties.customerId
        sharedKey: workspace.listKeys().primarySharedKey
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
Expected: nessun errore; i warning `BCP081` (versione API non nota al CLI) sono ammessi, ogni altro warning va risolto. `git status` deve mostrare solo i sorgenti `.bicep`/`.bicepparam`/`azure.yaml`.

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
    env:
      BICEP_VERSION: v0.48.1
    steps:
      - uses: actions/checkout@v6
      - name: Install Bicep CLI
        run: |
          set -euo pipefail
          curl -sSL -o bicep "https://github.com/Azure/bicep/releases/download/${BICEP_VERSION}/bicep-linux-x64"
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
      - uses: actions/setup-dotnet@v6
        with:
          dotnet-version: '9.0.x'
      - name: Test
        working-directory: apps/functions
        run: dotnet test --configuration Release --logger "trx;LogFileName=functions.trx"
      - uses: actions/upload-artifact@v7
        if: ${{ !cancelled() }}
        with:
          name: functions-test-results
          path: apps/functions/**/TestResults/*.trx
```

- [ ] **Step 5: Emendamenti alla spec** (`docs/for-developers/specs/2026-10-09-azure-local-profile-design.md`)

- §2, riga Key Vault: «**opt-in** (profilo compose `azure-keyvault`, `make dev-azure-local-keyvault`): richiede certificati generati sull'host con `docs/setup.sh` del progetto; non parte con `make dev-azure-local`».
- §2, riga App Configuration: «auth HMAC via env `Tenant__AccessKeys__0__*` (+ anonima per il seed); il provider .NET usa la connection string `Endpoint=…;Id=…;Secret=…` perché rifiuta un bearer su http».
- §3: aggiungere `AZURE_APPCONFIG_CONNECTION_STRING`, `AZURE_KEYVAULT_ANONYMOUS`, `INTERNAL_SERVICE_TOKEN`; «le sorgenti Azure vincono sulle variabili d'ambiente».
- §4.2: «Le sorgenti girano prima del container DI e non passano dal client pinnato: endpoint da configurazione statica, come le esenzioni OpenRouter/Resend del gate. Emulatore App Configuration: connection string HMAC; Key Vault emulator: `StaticTokenCredential` (`AZURE_KEYVAULT_ANONYMOUS=true`). Mapping dei secret: `--` → `:`, `-` → `_`; le chiavi CamelCase con trattino interno non sono esprimibili».
- §4.3: il payload è `{ eventId, occurredAt, sourceContext, contractVersion, pdfDocumentId, gameId, sharedGameId, filePath }` (non `BlobUri`); l'evento parte alla transizione Pending → Uploading, DOPO lo store del blob; «cosa causa l'evento: il consumer interno indicizza SOLO un PDF inattivo che ne ha bisogno (`Failed`, o `Ready` senza vettori) tramite `IndexPdfIfIdleCommand`; pipeline in corso o documento già indicizzato ⇒ `noop`» (le due guardie non stanno in `IndexPdfCommand`, che ri-indicizza sempre); profilo Azure: `AZURE_SERVICEBUS_NAMESPACE` + `DefaultAzureCredential`; metrica `meepleai_pdf_uploaded_events_total{outcome}` senza unit.
- §4.4: «`deployment.environment` resta l'ambiente ASP.NET (dashboard esistenti); il profilo va nell'attributo `meepleai.profile`».
- §4.5: «Due client nominati: `AzureSdk` pinnato (`MustBePinned`, suffissi da `Azure:Sdk:AllowedHostSuffixes`, rifiuta di partire senza lista) e `AzureSdkLocal` esente (solo `MEEPLEAI_PROFILE=azure-local`: `SsrfPinnedConnect` blocca ogni IP privato e gli emulatori stanno sulla rete compose). Service Bus parla AMQP e non passa da nessuno dei due. Il namespace C# è `Api.Infrastructure.AzureSdk`».
- §5: workflow `azure-line.yml` (job `bicep` + `functions-tests`), Bicep CLI pinnato a v0.48.1.
- §6: riga Integrazione: «App Configuration emulator via HMAC (test di integrazione); Key Vault: sorgente implementata, verificata con il profilo opt-in nella checklist manuale».
- §7: riga 3 → «Chiavi lette dall'App Configuration emulator con HMAC (test di integrazione); Key Vault opt-in verificato a mano»; riga 4 → «Upload PDF ⇒ messaggio v1 ⇒ Function ⇒ endpoint interno (noop con pipeline in corso; indicizzazione di un PDF Failed)».

Nelle note (`…-plan-notes.md`), voce «Eventi»: sostituire «subito prima dello store sul blob» con «dopo lo store del blob e la scrittura di `FilePath` (`UploadPdfCommandHandler.cs:574,609`), nel `ProcessPdfAsync` in background (`Processing.cs:195`)».

- [ ] **Step 6: Checklist manuale** in `docs/for-developers/operations/azure-local-profile.md`, sezione «Verifica manuale» (comandi in forma `pwsh -c "docker …"` su Windows)

1. `cd infra && make dev-azure-local`; `pwsh -c "docker logs meepleai-azure-local-init"` mostra tre `ready:` e `seeded Azure:Profile:Greeting`; `curl -fsS http://localhost:8080/api/v1/health` → 200.
2. Login admin, upload di un PDF dalla UI; `pwsh -c "docker logs meepleai-api"` contiene `Stored blob pdfs/<game>/<fileId>_<nome>.pdf`; `az storage blob list --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://localhost:20000/devstoreaccount1;" -c meepleai-uploads -o table` elenca lo stesso blob.
3. URL firmato dal profilo compose (host `azurite`, non IP): dal dettaglio del gioco copiare l'URL della cover (o chiamare l'endpoint che restituisce `coverUrl`) e `curl -fsS -o /dev/null -w "%{http_code}" "<url>"` → 200.
4. `curl -fsS "http://localhost:20003/kv?api-version=2023-11-01"` mostra `Azure:Profile:Greeting`; `pwsh -c "docker logs meepleai-api"` non contiene errori `AppConfiguration`.
5. `cd apps/functions/MeepleAI.Functions && cp local.settings.json.example local.settings.json && func start` (Azure Functions Core Tools v4); dopo un upload il log della Function stampa `forwarded: 202` (pipeline in corso) e, per un PDF che l'admin ha marcato `Failed`, `forwarded: 200`; `curl -s http://localhost:8080/metrics | grep meepleai_pdf_uploaded_events_total`.
6. Messaggio malformato: con il Service Bus Explorer di VS Code (connection string `Endpoint=sb://localhost:20004;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`) inviare `{"contractVersion":"v9"}` sulla coda `pdf-uploaded`; il log della Function stampa `dead-lettered: unsupported_contract` e il messaggio compare nella dead-letter della coda.
7. Event Grid smoke: `curl -fsS -X POST http://localhost:20006/pdf-events/api/events -H "Content-Type: application/json" -d '[{"id":"1","eventType":"Smoke","subject":"s","eventTime":"2026-10-09T10:00:00Z","data":{},"dataVersion":"1"}]'` → 200; `pwsh -c "docker logs meepleai-eventgrid-emulator"` mostra la consegna a `pull://smoke-subscription`.
8. `http://localhost:20009` (Aspire Dashboard): la richiesta a `/api/v1/health` appare in Traces, le metriche `meepleai.*` in Metrics, i log in Structured logs.
9. Key Vault opt-in: dopo il setup dei certificati, `make dev-azure-local-keyvault`; `pwsh -c "docker logs meepleai-api"` non contiene errori `KeyVault`; un secret `AZURE-LOCAL-PROBE=ok` creato con `az keyvault secret set --vault-name … ` (o con l'API REST dell'emulatore su `https://localhost:20011`) compare come chiave `AZURE_LOCAL_PROBE` in `GET /api/v1/admin/config` (o nel log di avvio).
10. `make dev-azure-local-down`; `pwsh -c "docker ps --filter name=meepleai-"` non elenca i servizi del profilo.

- [ ] **Step 7: Riga in CLAUDE.md** (tabella Quick Reference, dopo «Dev from Snapshot»)

```markdown
| Dev Azure emulators | `make dev-azure-local` | `infra/` — [guide](./docs/for-developers/operations/azure-local-profile.md) |
```

- [ ] **Step 8: Commit**

```bash
git add infra/azure .github/workflows/azure-line.yml docs/for-developers/specs/2026-10-09-azure-local-profile-design.md docs/for-developers/operations/azure-local-profile.md docs/superpowers/plans/2026-10-09-azure-local-profile-plan-notes.md CLAUDE.md
git commit -m "feat(infra): Bicep per il profilo Azure, gate CI azure-line, checklist" -m "bicep build/lint pinnato a v0.48.1 e test delle Functions su ogni PR che tocca infra/azure o apps/functions; mai un deploy. Spec emendata: HMAC per App Configuration, Key Vault opt-in, due client SDK, IndexPdfIfIdle, metrica senza unit." -m "Co-Authored-By: Claude Fable 5.1 <noreply@anthropic.com>"
```

---

## Chiusura

Dopo il Task 10: suite unit backend completa (`dotnet test tests/Api.Tests --filter "Category=Unit"`), gate di architettura (`--filter "FullyQualifiedName~Architecture"`, verdi nel worktree grazie al Task 0), `pnpm typecheck && pnpm lint` in `apps/web` (solo un build arg aggiunto al compose: nessun file web toccato), `make dev-azure-local-config`. Poi `superpowers:finishing-a-development-branch` con base `azure/main`; aprire le issue di follow-up: debito `IAmazonS3` (spec §4.1), `BlobServiceClient` singleton (oggi per scope come S3), Content-Type fisso in `StoreAsync` (ereditato da S3), seconda iterazione (Cosmos per ConversationMemory, Redis vettoriale, multi-arch, KEDA/AKS).
