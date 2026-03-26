# DatabaseSync Bounded Context

## Responsabilita

Gestisce la sincronizzazione dei database tra ambienti (local e staging), inclusa la gestione del tunnel SSH, il confronto di schema e dati, l'anteprima e l'applicazione di migrazioni. Tutte le operazioni sono riservate esclusivamente ai SuperAdmin.

> **Nota**: Questo context opera su 39 file e non persiste entita proprie in database. Lavora direttamente con connessioni Npgsql raw per confrontare e sincronizzare dati tra ambienti.

## Funzionalita Principali

- **Gestione Tunnel SSH**: Apertura, chiusura e monitoraggio stato del tunnel verso il database di staging tramite un sidecar container
- **Confronto Schema (Migrations)**: Diff delle migrazioni EF Core tra local e staging (comuni, solo local, solo staging)
- **Confronto Dati Tabella**: Diff a livello di riga tra local e staging con rilevamento di righe modificate, aggiunte e rimosse
- **Anteprima SQL Migrazioni**: Generazione dello SQL che verrebbe eseguito senza applicarlo
- **Applicazione Migrazioni**: Esecuzione delle migrazioni pendenti in una direzione specificata (LocalToStaging o StagingToLocal)
- **Sincronizzazione Dati Tabella**: Copia dei dati di una tabella da un ambiente all'altro con conteggio insert/update
- **Storico Operazioni**: Registro delle operazioni di sync con risultati, admin responsabile e timestamp
- **Feature Flag Gate**: Tutte le operazioni sono protette dal flag `Features.DatabaseSync`

## Struttura

### Domain/

Modelli di dominio puri, enumerazioni e interfacce:

- **Enums/**:
  - `SyncDirection`: Direzione della sincronizzazione (`LocalToStaging`, `StagingToLocal`)
  - `TunnelState`: Stato del tunnel SSH (`Closed`, `Opening`, `Open`, `Error`)
- **Interfaces/**:
  - `ISshTunnelClient`: Contratto per gestione lifecycle del tunnel (status, open, close)
  - `IRemoteDatabaseConnector`: Contratto per ottenere una `NpgsqlConnection` al database remoto via tunnel
- **Models/**:
  - `TunnelStatusResult`: Stato corrente del tunnel con uptime e messaggio
  - `SchemaDiffResult`: Risultato del confronto schema (migrazioni comuni, solo local, solo staging)
  - `MigrationInfo`: Informazioni su una singola migrazione (ID, versione, data applicazione)
  - `DataDiffResult`: Risultato del confronto dati di una tabella (conteggi righe, righe identiche, modificate, solo local, solo staging)
  - `RowDiff`: Differenza a livello di riga con chiave primaria e lista di colonne diverse
  - `ColumnDiff`: Differenza di un singolo valore colonna tra local e staging
  - `TableInfo`: Metadati tabella con schema, conteggio righe per ambiente e bounded context associato
  - `SyncResult`: Esito di un'operazione di sync (successo, righe inserite/aggiornate, errore)
  - `SyncOperationEntry`: Voce nello storico operazioni con tipo, tabella, admin, timestamp

### Application/

Orchestrazione e casi d'uso (CQRS pattern con MediatR):

- **Commands/**:
  - `OpenTunnelCommand` / `OpenTunnelHandler`: Apre il tunnel SSH verso staging
  - `CloseTunnelCommand` / `CloseTunnelHandler`: Chiude il tunnel SSH attivo
  - `PreviewMigrationSqlCommand` / `PreviewMigrationSqlHandler`: Genera anteprima SQL delle migrazioni
  - `ApplyMigrationsCommand` / `ApplyMigrationsHandler`: Applica le migrazioni pendenti nella direzione specificata
  - `SyncTableDataCommand` / `SyncTableDataHandler`: Sincronizza i dati di una tabella specifica
  - `DatabaseSyncValidators`: Validatori FluentValidation per `ApplyMigrationsCommand` e `SyncTableDataCommand`
- **Queries/**:
  - `GetTunnelStatusQuery` / `GetTunnelStatusHandler`: Recupera lo stato corrente del tunnel
  - `CompareSchemaQuery` / `CompareSchemaHandler`: Confronta le migrazioni EF Core tra i due ambienti
  - `CompareTableDataQuery` / `CompareTableDataHandler`: Confronta i dati di una tabella specifica
  - `ListTablesQuery` / `ListTablesHandler`: Elenca tutte le tabelle con conteggio righe per ambiente
  - `GetSyncOperationsHistoryQuery` / `GetSyncOperationsHistoryHandler`: Recupera lo storico delle operazioni

### Infrastructure/

Implementazioni concrete e motori di diffing:

- `SshTunnelClient`: Client HTTP che comunica con il sidecar SSH tunnel (endpoint `/status`, `/open`, `/close`) con autenticazione Bearer token
- `RemoteDatabaseConnector`: Crea connessioni `NpgsqlConnection` al database remoto, verificando che il tunnel sia aperto
- `SchemaDiffEngine`: Legge `__EFMigrationsHistory` da entrambi i database e calcola il diff delle migrazioni
- `DataDiffEngine`: Confronta i dati riga per riga tra le tabelle, filtrando colonne unsafe (`bytea`, `vector`, `json`, `jsonb`, `xml`)
- `DatabaseSyncServiceExtensions`: Registrazione DI con configurazione da variabili d'ambiente (`SIDECAR_BASE_URL`, `SIDECAR_AUTH_TOKEN`, `REMOTE_DB_*`)

## Pattern Architetturali

- **CQRS**: Separazione tra Commands (scrittura/azioni) e Queries (lettura/confronto)
- **MediatR**: Tutti gli endpoint HTTP usano `IMediator.Send()` per invocare handlers
- **Sidecar Pattern**: Il tunnel SSH e gestito da un container sidecar separato, comunicazione via HTTP
- **Feature Flag Gate**: Ogni endpoint verifica `Features.DatabaseSync` prima dell'esecuzione
- **Confirmation Guard**: Le operazioni distruttive (apply, sync) richiedono una stringa di conferma esplicita
- **Tipi `internal`**: Tutti i tipi del context sono `internal` per isolamento a livello di assembly

## API Endpoints

Tutti gli endpoint sono sotto `/api/v1/admin/database-sync` e richiedono autorizzazione SuperAdmin + feature flag attivo.

### Tunnel Management

```
GET    /admin/database-sync/tunnel/status          -> GetTunnelStatusQuery
POST   /admin/database-sync/tunnel/open             -> OpenTunnelCommand
DELETE /admin/database-sync/tunnel/close             -> CloseTunnelCommand
```

### Schema Operations

```
GET    /admin/database-sync/schema/compare           -> CompareSchemaQuery
POST   /admin/database-sync/schema/preview-sql       -> PreviewMigrationSqlCommand
POST   /admin/database-sync/schema/apply             -> ApplyMigrationsCommand
```

### Table Data Operations

```
GET    /admin/database-sync/tables                   -> ListTablesQuery
GET    /admin/database-sync/tables/{name}/compare    -> CompareTableDataQuery
POST   /admin/database-sync/tables/{name}/sync       -> SyncTableDataCommand
```

### Operations History

```
GET    /admin/database-sync/operations/history       -> GetSyncOperationsHistoryQuery
```

## Configurazione Ambiente

Variabili d'ambiente necessarie (gestite tramite secrets):

| Variabile | Descrizione | Default |
|-----------|-------------|---------|
| `SIDECAR_BASE_URL` | URL base del sidecar SSH tunnel | `http://ssh-tunnel-sidecar:2222` |
| `SIDECAR_AUTH_TOKEN` | Token Bearer per autenticazione sidecar | (vuoto) |
| `REMOTE_DB_HOST` | Host del database remoto | `ssh-tunnel-sidecar` |
| `REMOTE_DB_PORT` | Porta del database remoto (tunnel) | `15432` |
| `REMOTE_DB_NAME` | Nome del database remoto | `meepleai` |
| `REMOTE_DB_USER` | Utente del database remoto | `meepleai` |
| `REMOTE_DB_PASSWORD` | Password del database remoto | (vuoto) |

## Dipendenze

- **Npgsql**: Connessioni dirette PostgreSQL per operazioni di confronto e sync
- **MediatR**: Orchestrazione CQRS
- **FluentValidation**: Validazione input per comandi distruttivi
- **HttpClient**: Comunicazione con sidecar SSH tunnel
- **IFeatureFlagService**: Gate di attivazione feature

## Sicurezza

> **Accesso**: Riservato esclusivamente ai **SuperAdmin**. Ogni endpoint verifica la sessione admin tramite `RequireSuperAdminSession()` e il feature flag `Features.DatabaseSync`.
>
> **Credenziali SSH**: Le credenziali per il tunnel SSH e il database remoto sono gestite tramite variabili d'ambiente e file `.secret` nella directory `infra/secrets/`. Non vengono mai incluse nel codice sorgente o nei commit.
>
> **Conferma esplicita**: Le operazioni distruttive (applicazione migrazioni, sincronizzazione dati) richiedono una stringa di conferma nel body della richiesta e registrano l'ID dell'admin responsabile.
>
> **Filtro colonne unsafe**: Il `DataDiffEngine` esclude automaticamente colonne di tipo `bytea`, `vector`, `json`, `jsonb`, `xml` dal confronto per evitare problemi di serializzazione e performance.

## Related Documentation

- `docs/01-architecture/overview/system-architecture.md`
- `docs/operations/operations-manual.md`
