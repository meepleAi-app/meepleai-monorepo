# TODO: S3 Storage Activation

## Stato attuale: IMPLEMENTATO e VALIDATO ✅

Configurazione completata. Bug critico nel secret loader fixato. Integration tests, health check reale, migration endpoint, e MinIO per test locali implementati.

## Modifiche effettuate

### Bug fix: SecretLoader non propagava valori a IConfiguration
- **File**: `Program.cs:98-103`
- **Problema**: `SecretLoader.ApplyAsEnvironmentVariables()` settava `Environment.SetEnvironmentVariable()`, ma `IConfiguration` aveva gia' fatto lo snapshot delle env vars in `WebApplication.CreateBuilder()`. I valori da `storage.secret` non erano mai visibili a `config["KEY"]`. Questo rendeva impossibile attivare S3 via secret file.
- **Fix**: Dopo `LoadAndValidate()`, i valori caricati vengono aggiunti a `builder.Configuration` via `AddInMemoryCollection()`:
```csharp
var loadedSecrets = secretLoader.GetLoadedValues();
if (loadedSecrets.Count > 0)
{
    builder.Configuration.AddInMemoryCollection(
        loadedSecrets.Select(kv => new KeyValuePair<string, string?>(kv.Key, kv.Value)));
}
```
- **Impatto**: TUTTI i valori dei secret file ora sono visibili a `IConfiguration`, non solo quelli S3

### Configurazione storage.secret
- **File**: `infra/secrets/storage.secret`
- Aggiunto `STORAGE_PROVIDER=s3`
- Aggiunto `S3_ENDPOINT=https://fcfdd747188848995e29f2b4ae81f7c9.r2.cloudflarestorage.com`

### Script generazione secret
- **File**: `infra/secrets/setup-secrets.ps1`
- Aggiunta generazione `S3_ACCESS_KEY` (32 char hex, 16 bytes random)
- Aggiunta generazione `S3_SECRET_KEY` (64 char hex, 32 bytes random)
- Aggiunto placeholder `S3_ENDPOINT=change_me_r2_endpoint` (richiede config manuale)

### Template secret aggiornato
- **File**: `infra/secrets/storage.secret.example`
- Placeholder cambiati da `your_*` a `change_me_*` per compatibilita' con pattern di auto-replace dello script

## Architettura (Factory Pattern)
| File | Ruolo |
|---|---|
| `Services/Pdf/IBlobStorageService.cs` | Interfaccia: Store, Retrieve, Delete, Exists |
| `Services/Pdf/BlobStorageService.cs` | Impl **local** (`pdf_uploads/`) |
| `Services/Pdf/S3BlobStorageService.cs` | Impl **S3** (R2, AWS, MinIO, B2, DO Spaces) |
| `Services/Pdf/BlobStorageServiceFactory.cs` | Switch su `STORAGE_PROVIDER` env var |
| `Services/Pdf/S3StorageOptions.cs` | Config: endpoint, bucket, region, encryption, presigned URLs |
| `Infrastructure/Health/Checks/S3StorageHealthCheck.cs` | Health check local vs S3 |

### DI Registration
`ApplicationServiceExtensions.cs:206`:
```csharp
services.AddScoped<IBlobStorageService>(sp => BlobStorageServiceFactory.Create(sp));
```

### Consumer (9 handler in 3 bounded context)
- **DocumentProcessing**: Upload, UploadPrivate, Extract, Download, Delete, CompleteChunkedUpload
- **SharedGameCatalog**: UploadPdfForGameExtraction, ExtractGameMetadataFromPdf
- **GameManagement**: UploadGameImage

### Features S3 implementate
- Server-side encryption (AES256)
- Pre-signed URLs per download temporanei
- Path traversal protection (`PathSecurity.ValidateIdentifier`)
- Multi-provider (R2, AWS, MinIO, B2, DigitalOcean Spaces)
- Health check dedicato
- Logging completo

## Implementazioni completate (PR feature/s3-validation)

### 1. IBlobStorageService: aggiunto GetPresignedDownloadUrlAsync ✅
- Aggiunto metodo all'interfaccia `IBlobStorageService`
- `BlobStorageService` (local) ritorna `null` (fallback locale)
- `S3BlobStorageService` genera URL pre-firmati funzionanti

### 2. S3StorageHealthCheck: check di connettivita' reale ✅
- Riscritto con `ListObjectsV2Async(MaxKeys=1)` per verificare accesso reale al bucket
- Riporta endpoint e bucket name nel messaggio healthy
- Degraded se il tipo di servizio non corrisponde a S3

### 3. docker-compose.yml: storage.secret + MinIO ✅
- Aggiunto `./secrets/storage.secret` a `env_file` del servizio api
- Aggiunto servizio `minio` (porta 9000 API, 9001 console) con profile `dev`/`full`
- Aggiunto servizio `minio-init` per creare automaticamente il bucket `meepleai-uploads`
- Aggiunto volume `minio_data`

### 4. Integration tests con MinIO Testcontainer ✅
- **File**: `tests/Api.Tests/Integration/DocumentProcessing/S3BlobStorageIntegrationTests.cs`
- Standalone MinIO Testcontainer fixture (non SharedTestcontainersFixture)
- 11 test: Store, Exists, Retrieve, PresignedUrl (con download HTTP), Delete, PathTraversal, NonExistent, HealthCheck, FullLifecycle
- Supporta endpoint S3 esterno via `TEST_S3_ENDPOINT` per test CI/R2
- Traits: `[Trait("Category", "Integration")]`, `[Trait("Feature", "S3Storage")]`

### 5. Endpoint migrazione local → S3 ✅
- **Command**: `MigrateStorageCommand(bool DryRun)` + `MigrateStorageResult`
- **Handler**: Scansione `pdf_uploads/`, parse `{gameId}/{fileId}_{filename}`, upload S3 preservando key originali
- **Endpoint**: `POST /api/v1/admin/storage/migrate?dryRun=true`
- Idempotente (skip se file gia' su S3), dry-run mode, encryption opzionale

### 6. S3BlobStorageService: proprieta' interne per health check ✅
- Aggiunte `internal IAmazonS3 S3Client` e `internal S3StorageOptions Options`
- Usate da health check e migration handler per accesso diretto al client S3

## Validazione R2 completata ✅ (2026-02-18)

### Bucket Cloudflare R2
- Bucket `meepleai-uploads` creato con EU jurisdiction
- Token R2 con permessi **Object Read & Write**
- Credenziali in `storage.secret` (ACCESS_KEY, SECRET_KEY, TOKEN_KEY)

### Test di connettivita' eseguiti
- Upload (PutObject): ✅
- Head (exists check): ✅
- Download (GetObject): ✅ contenuto verificato
- Pre-signed URL + HTTP download: ✅
- List objects: ✅
- Delete + verifica: ✅
- Health check API Docker: ✅ `S3 storage accessible`

### Step rimanente: Eseguire migrazione dati
- `POST /api/v1/admin/storage/migrate?dryRun=true` → preview
- `POST /api/v1/admin/storage/migrate?dryRun=false` → esecuzione
- Verificare risultato (TotalFiles, Migrated, Skipped, Failed)

## Note
- `storage.secret.example` ha documentazione completa con istruzioni per ogni provider
- Terraform config gia' presente: `infra/terraform/epic-4068/main.tf`
- Backup config separata: `S3_BACKUP_*` keys per PostgreSQL backups via n8n
- Docker-compose ora include `storage.secret` in env_file — secret propagati correttamente
- MinIO disponibile per test locali: `docker compose --profile dev up -d minio`
