# Profilo `azure-local`

Spec: [`2026-10-09-azure-local-profile-design.md`](../specs/2026-10-09-azure-local-profile-design.md).

Il profilo aggiunge allo stack di sviluppo gli emulatori Azure, senza conto Azure e senza segreti. Vive in `infra/compose.azure-local.yml`, composto sopra `docker-compose.yml` + `compose.dev.yml` **solo** dai target `make dev-azure-local*`: senza questi target lo stack attuale resta invariato.

Tutti i comandi si lanciano da `infra/` (Git Bash su Windows). Non invocare `docker compose` a mano: gli `env_file` e le porte derivate sono impostati dal Makefile.

## Cosa avvia

`P` = `AZURE_LOCAL_PORT_BASE` (default `20000`). Le porte host sono legate a `127.0.0.1`.

| Servizio | Immagine | Porta host |
|----------|----------|------------|
| `azurite` (blob) | `mcr.microsoft.com/azure-storage/azurite` | `P` (queue `P+1`, table `P+2`) |
| `appconfig-emulator` | `mcr.microsoft.com/azure-app-configuration/app-configuration-emulator` | `P+3` |
| `servicebus-emulator` (AMQP / HTTP) | `mcr.microsoft.com/azure-messaging/servicebus-emulator` | `P+4` / `P+5` |
| `servicebus-mssql` | `mcr.microsoft.com/mssql/server` | nessuna (solo rete compose) |
| `eventgrid-emulator` | `workleap/eventgridemulator` | `P+6` |
| `cosmos-emulator` (gateway / explorer) | `mcr.microsoft.com/cosmosdb/linux/azure-cosmos-emulator` (vNext) | `P+7` / `P+8` |
| `aspire-dashboard` (UI / OTLP gRPC) | `mcr.microsoft.com/dotnet/aspire-dashboard` | `P+9` / `P+10` |
| `keyvault-emulator` (opt-in) | `jamesgoulddev/azure-keyvault-emulator` | `P+11` |
| `azure-local-init` | `curlimages/curl` | nessuna (esce a lavoro finito) |

I tag esatti sono in `infra/compose.azure-local.yml`. Un secondo checkout usa un'altra base: `make dev-azure-local AZURE_LOCAL_PORT_BASE=21000`.

## Comandi

```bash
cd infra
make dev-azure-local            # core + emulatori
make dev-azure-local-down       # ferma tutto, Key Vault incluso
make dev-azure-local-config     # valida il compose, nessun container
make dev-azure-local-keyvault   # come dev-azure-local + Key Vault emulator (vedi sotto)
```

## Readiness

Le immagini .NET degli emulatori non contengono `curl` ne' `wget`, quindi nessun `healthcheck` interno e' possibile. Il container `azure-local-init` (`infra/azure-local/wait-and-seed.sh`) li sonda da fuori sulla rete compose: Azurite (qualsiasi risposta HTTP), App Configuration e Service Bus (`/health` 2xx). Poi semina le chiavi di `infra/azure-local/appconfig-seed.json` con un PUT anonimo per chiave. `api` dipende da `azure-local-init` con `condition: service_completed_successfully`, quindi parte solo dopo.

```bash
pwsh -c "docker logs meepleai-azure-local-init"   # righe «ready: ...» e «seeded ...»
curl -fsS http://localhost:8080/api/v1/health
```

Se un emulatore non diventa pronto, lo script esce con errore e nomina quale; leggere `pwsh -c "docker logs meepleai-<servizio>"`.

## Credenziali di sviluppo

Tutte pubbliche, documentate dai rispettivi progetti; non sono segreti e non vanno in `infra/secrets/`.

- Azurite: account `devstoreaccount1` con la chiave ben nota.
- Service Bus emulator: `SharedAccessKey=SAS_KEY_VALUE` con `UseDevelopmentEmulator=true`.
- App Configuration emulator: HMAC con `Id=azure-local-dev` e un `Secret` di sviluppo (base64 di una stringa fissa); in piu' l'accesso anonimo, usato dal seed.
- SQL Server del Service Bus: `SERVICEBUS_MSSQL_SA_PASSWORD`, con default di sviluppo nel compose.
- `INTERNAL_SERVICE_TOKEN`: default di sviluppo, sovrascrivibile da ambiente.

## Precedenza della configurazione

Le sorgenti Azure (App Configuration) sono aggiunte **dopo** le variabili d'ambiente e vincono sulle chiavi coincidenti. Ordine effettivo, dalla piu' forte: App Configuration > variabili d'ambiente > `SecretLoader`, che aggiunge solo le chiavi assenti. Per questo `appconfig-seed.json` contiene solo una chiave di prova e non chiavi reali dell'API.

## Key Vault opt-in

L'emulatore richiede un certificato generato sull'host.

```bash
git clone https://github.com/james-gould/azure-keyvault-emulator
# eseguire docs/setup.sh del progetto, poi copiare emulator.pfx ed emulator.crt in:
#   infra/azure-local/keyvault-certs/        (git-ignorata)
cd infra && make dev-azure-local-keyvault
```

Il target imposta `AZURE_KEYVAULT_URI`, `AZURE_KEYVAULT_ANONYMOUS=true` e `AZURE_SDK_ALLOW_INSECURE_TLS=true`: il TLS insicuro vale **solo** per questo profilo locale e non va mai portato altrove.

## Limiti degli emulatori

- Service Bus: niente entita' partizionate, niente AMQP su WebSocket.
- Cosmos vNext: nessuna autenticazione, solo il gateway; indici vettoriali DiskANN.
- App Configuration: l'autenticazione si dichiara via variabili d'ambiente (`Tenant__*`, `Authentication__*`) perche' l'immagine non imposta l'ambiente ASP.NET.
- Cosmos e' nel profilo per un'iterazione successiva: oggi nessun consumer.
- Service Bus emulator 2.0.1: completare un messaggio ricevuto dalla sub-queue dead-letter lancia `MessageLockLost` (caratterizzato con tre run isolati). Il test di integrazione lascia quindi il messaggio DLQ non liquidato.
- Azurite: con l'SDK 12.30.1 serve `--skipApiVersionCheck` (gia' nel compose; la fixture Testcontainers lo aggiunge).

### Note per i test

`WithCommand` di Testcontainers **appende** al comando di default del modulo invece di sostituirlo: per un flag in piu' (es. `--skipApiVersionCheck`) basta aggiungerlo, senza ripetere il comando base.

## Variabili iniettate nell'API

| Variabile | Significato |
|-----------|-------------|
| `MEEPLEAI_PROFILE` | Nome del profilo attivo (`azure-local`). |
| `STORAGE_PROVIDER` | Provider di storage (`azureblob`); vale quando il provider e' implementato, fino ad allora la factory ripiega su `local`. |
| `AZURE_STORAGE_CONNECTION_STRING` | Connection string di Azurite. |
| `AZURE_STORAGE_CONTAINER` | Container dei file caricati. |
| `AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT` | Host raggiungibile dal browser per gli URL SAS (la firma non copre l'host). |
| `AZURE_APPCONFIG_CONNECTION_STRING` | Connection string HMAC dell'App Configuration emulator. |
| `AZURE_APPCONFIG_STARTUP_TIMEOUT_SECONDS` | Timeout di avvio del provider App Configuration (30 s nel profilo), accorciato per non bloccare l'API se l'emulatore manca. |
| `AZURE_KEYVAULT_URI` | Endpoint del Key Vault emulator; vuota se non opt-in. |
| `AZURE_KEYVAULT_ANONYMOUS` | Accesso senza credenziali al Key Vault emulator; vuota se non opt-in. |
| `AZURE_SDK_ALLOW_INSECURE_TLS` | Accetta il certificato self-signed degli emulatori; `false` salvo opt-in Key Vault. |
| `AZURE_SERVICEBUS_CONNECTION_STRING` | Connection string del Service Bus emulator. |
| `AZURE_SERVICEBUS_QUEUE` | Coda dei PDF caricati (`pdf-uploaded`). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Collector OTLP: l'Aspire Dashboard. |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Protocollo OTLP (`grpc`). |
| `INTERNAL_SERVICE_TOKEN` | Token condiviso con la Function locale. |

## Verifica manuale

Comandi in forma `pwsh -c "docker …"` su Windows.

1. `cd infra && make dev-azure-local`; `pwsh -c "docker logs meepleai-azure-local-init"` mostra tre `ready:` e `seeded Azure:Profile:Greeting`; `curl -fsS http://localhost:8080/api/v1/health` → 200.
2. Login admin, upload di un PDF dalla UI; `pwsh -c "docker logs meepleai-api"` contiene `Stored blob pdfs/<game>/<fileId>_<nome>.pdf`; `az storage blob list --connection-string "DefaultEndpointsProtocol=http;AccountName=devstoreaccount1;AccountKey=Eby8vdM02xNOcqFlqUwJPLlmEtlCDXJ1OUzFT50uSRZ6IFsuFq2UVErCz4I6tq/K1SZFPTOtr/KBHBeksoGMGw==;BlobEndpoint=http://localhost:20000/devstoreaccount1;" -c meepleai-uploads -o table` elenca lo stesso blob.
3. URL firmato dal profilo compose (host `azurite`, non IP): dal dettaglio del gioco copiare l'URL della cover (o chiamare l'endpoint che restituisce `coverUrl`) e `curl -fsS -o /dev/null -w "%{http_code}" "<url>"` → 200.
4. `curl -fsS "http://localhost:20003/kv?api-version=2023-11-01"` mostra `Azure:Profile:Greeting`; `pwsh -c "docker logs meepleai-api"` non contiene errori `AppConfiguration`.
5. `cd apps/functions/MeepleAI.Functions && cp local.settings.json.example local.settings.json && func start` (Azure Functions Core Tools v4); dopo un upload il log della Function stampa `forwarded: 202` (pipeline in corso) e, per un PDF che l'admin ha marcato `Failed`, `forwarded: 200`; `curl -s http://localhost:8080/metrics | grep meepleai_pdf_uploaded_events_total`.
6. Messaggio malformato: con il Service Bus Explorer di VS Code (connection string `Endpoint=sb://localhost:20004;SharedAccessKeyName=RootManageSharedAccessKey;SharedAccessKey=SAS_KEY_VALUE;UseDevelopmentEmulator=true;`) inviare `{"contractVersion":"v9"}` sulla coda `pdf-uploaded`; il log della Function stampa `dead-lettered: unsupported_contract` e il messaggio compare nella dead-letter della coda.
7. Event Grid smoke: `curl -fsS -X POST http://localhost:20006/pdf-events/api/events -H "Content-Type: application/json" -d '[{"id":"1","eventType":"Smoke","subject":"s","eventTime":"2026-10-09T10:00:00Z","data":{},"dataVersion":"1"}]'` → 200; `pwsh -c "docker logs meepleai-eventgrid-emulator"` mostra la consegna a `pull://smoke-subscription`.
8. `http://localhost:20009` (Aspire Dashboard): la richiesta a `/api/v1/health` appare in Traces, le metriche `meepleai.*` in Metrics, i log in Structured logs.
9. Key Vault opt-in (resta manuale: la sorgente e' implementata nell'API ma non e' mai esercitata contro l'emulatore in CI): dopo il setup dei certificati, `make dev-azure-local-keyvault`; `pwsh -c "docker logs meepleai-api"` non contiene errori `KeyVault`; un secret `AZURE-LOCAL-PROBE=ok` creato con `az keyvault secret set --vault-name … ` (o con l'API REST dell'emulatore su `https://localhost:20011`) compare come chiave `AZURE_LOCAL_PROBE` in `GET /api/v1/admin/config` (o nel log di avvio).
10. `make dev-azure-local-down`; `pwsh -c "docker ps --filter name=meepleai-"` non elenca i servizi del profilo.
