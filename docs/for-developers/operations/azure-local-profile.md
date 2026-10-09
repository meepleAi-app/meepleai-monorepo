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

## Variabili iniettate nell'API

| Variabile | Significato |
|-----------|-------------|
| `MEEPLEAI_PROFILE` | Nome del profilo attivo (`azure-local`). |
| `STORAGE_PROVIDER` | Provider di storage (`azureblob`); vale quando il provider e' implementato, fino ad allora la factory ripiega su `local`. |
| `AZURE_STORAGE_CONNECTION_STRING` | Connection string di Azurite. |
| `AZURE_STORAGE_CONTAINER` | Container dei file caricati. |
| `AZURE_STORAGE_PUBLIC_BLOB_ENDPOINT` | Host raggiungibile dal browser per gli URL SAS (la firma non copre l'host). |
| `AZURE_APPCONFIG_CONNECTION_STRING` | Connection string HMAC dell'App Configuration emulator. |
| `AZURE_KEYVAULT_URI` | Endpoint del Key Vault emulator; vuota se non opt-in. |
| `AZURE_KEYVAULT_ANONYMOUS` | Accesso senza credenziali al Key Vault emulator; vuota se non opt-in. |
| `AZURE_SDK_ALLOW_INSECURE_TLS` | Accetta il certificato self-signed degli emulatori; `false` salvo opt-in Key Vault. |
| `AZURE_SERVICEBUS_CONNECTION_STRING` | Connection string del Service Bus emulator. |
| `AZURE_SERVICEBUS_QUEUE` | Coda dei PDF caricati (`pdf-uploaded`). |
| `OTEL_EXPORTER_OTLP_ENDPOINT` | Collector OTLP: l'Aspire Dashboard. |
| `OTEL_EXPORTER_OTLP_PROTOCOL` | Protocollo OTLP (`grpc`). |
| `INTERNAL_SERVICE_TOKEN` | Token condiviso con la Function locale. |

## Verifica manuale

Da compilare con il Task 10 del piano.
