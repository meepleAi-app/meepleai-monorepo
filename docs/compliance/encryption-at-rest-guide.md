# Encryption at Rest -- Implementation Guide

## 1. Overview

Hetzner Cloud's Data Processing Agreement (DPA), Appendix 2, Section 7, explicitly states that **encryption of data at rest is the client's responsibility**. Hetzner provides physical security, network isolation, and infrastructure-level controls, but does not encrypt customer data volumes by default.

Under GDPR Article 32, controllers and processors must implement appropriate technical and organisational measures to ensure a level of security appropriate to the risk. Encryption at rest is listed as a specific measure in Article 32(1)(a). Failure to encrypt personal data at rest can constitute a violation of the accountability principle (Article 5(2)) and may result in administrative fines under Article 83.

MeepleAI stores the following categories of personal data:

| Service    | Data Types                                           | Sensitivity |
|------------|------------------------------------------------------|-------------|
| PostgreSQL | User accounts (email, hashed passwords, OAuth tokens), game sessions, audit logs | High |
| Redis      | Session tokens, rate-limit counters, cached user data | Medium |
| Qdrant     | Vector embeddings derived from user-uploaded PDFs     | Medium |

This guide covers three layers of encryption:

1. **Volume-level** (LUKS2) -- encrypts all data on disk transparently
2. **Transport-level** (TLS) -- encrypts data in transit between services
3. **Application-level** (column encryption) -- encrypts specific high-sensitivity fields

All three layers should be implemented for defense in depth.

---

## 2. Volume-Level Encryption (LUKS)

### Why LUKS

LUKS2 (Linux Unified Key Setup) provides full-disk encryption at the block device level. Every byte written to the volume is encrypted with AES-256-XTS before reaching the physical disk. This means:

- Database files, WAL segments, RDB snapshots, and vector storage are all encrypted transparently.
- No application changes are required.
- Performance overhead is typically 2-5% on modern CPUs with AES-NI support.
- Hetzner CX-series instances include AES-NI.

### Implementation

Hetzner Cloud volumes can be formatted with LUKS2 at creation time or after provisioning. Use the provided setup script:

```bash
infra/scripts/setup-luks-encryption.sh
```

Each data service should have its own encrypted volume:

| Volume        | Mount Point              | Size (minimum) | Purpose                    |
|---------------|--------------------------|-----------------|----------------------------|
| vol-pg-data   | /mnt/encrypted/pgdata    | 50 GB           | PostgreSQL data directory   |
| vol-redis-data| /mnt/encrypted/redisdata | 10 GB           | Redis RDB/AOF persistence   |
| vol-qdrant-data| /mnt/encrypted/qdrantdata| 30 GB          | Qdrant collections storage  |

After encrypting and mounting the volumes, update `docker-compose.yml` volume mappings:

```yaml
volumes:
  postgres-data:
    driver: local
    driver_opts:
      type: none
      o: bind
      device: /mnt/encrypted/pgdata

  redis-data:
    driver: local
    driver_opts:
      type: none
      o: bind
      device: /mnt/encrypted/redisdata

  qdrant-data:
    driver: local
    driver_opts:
      type: none
      o: bind
      device: /mnt/encrypted/qdrantdata
```

### Key Management

- Store the LUKS passphrase in a separate, access-controlled location (not on the same server).
- For automated boot, use a key file stored on an encrypted root volume, or use a remote key server (e.g., Tang/Clevis for network-bound disk encryption).
- Rotate LUKS passphrases annually: `cryptsetup luksChangeKey /dev/sdX`.
- Back up the LUKS header: `cryptsetup luksHeaderBackup /dev/sdX --header-backup-file header.bak`. Store the backup securely offsite.

---

## 3. PostgreSQL Encryption

### 3a. TLS for Connections

PostgreSQL should accept only TLS-encrypted connections to prevent credential and query interception on the network.

**Generate a self-signed certificate (or use Let's Encrypt):**

```bash
openssl req -new -x509 -days 365 -nodes \
  -out /etc/ssl/certs/pg-server.crt \
  -keyout /etc/ssl/private/pg-server.key \
  -subj "/CN=meepleai-postgres"

chmod 600 /etc/ssl/private/pg-server.key
chown 999:999 /etc/ssl/private/pg-server.key /etc/ssl/certs/pg-server.crt
```

**Docker Compose configuration:**

```yaml
postgres:
  image: pgvector/pgvector:pg16
  command: >
    postgres
    -c ssl=on
    -c ssl_cert_file=/etc/ssl/certs/server.crt
    -c ssl_key_file=/etc/ssl/private/server.key
    -c ssl_min_protocol_version=TLSv1.3
  volumes:
    - ./certs/pg-server.crt:/etc/ssl/certs/server.crt:ro
    - ./certs/pg-server.key:/etc/ssl/private/server.key:ro
    - postgres-data:/var/lib/postgresql/data
```

**Update the connection string in `infra/secrets/database.secret`:**

```
DATABASE_CONNECTION_STRING=Host=postgres;Port=5432;Database=meepleai;Username=meepleai;Password=<password>;SslMode=Require
```

Use `SslMode=VerifyFull` if you configure a proper CA chain. For self-signed certificates, `SslMode=Require` enforces encryption without certificate validation.

### 3b. Column-Level Encryption (Application Layer)

For high-sensitivity fields that require encryption independent of volume-level protections (defense in depth), use the ASP.NET Core Data Protection API with an EF Core ValueConverter.

**Target fields:**

- `User.Email`
- `User.IpAddress` (if stored)
- `OAuthToken.AccessToken`
- `OAuthToken.RefreshToken`

**Implementation pattern:**

```csharp
// Infrastructure/Encryption/EncryptedStringConverter.cs
public class EncryptedStringConverter : ValueConverter<string, string>
{
    public EncryptedStringConverter(IDataProtector protector)
        : base(
            v => protector.Protect(v),
            v => protector.Unprotect(v))
    {
    }
}

// In DbContext OnModelCreating:
var protector = serviceProvider
    .GetRequiredService<IDataProtectionProvider>()
    .CreateProtector("MeepleAI.ColumnEncryption");

builder.Entity<User>(entity =>
{
    entity.Property(u => u.Email)
        .HasConversion(new EncryptedStringConverter(protector));
});
```

**Considerations:**

- Encrypted columns cannot be indexed or queried with `WHERE` clauses. Maintain a separate hashed column (SHA-256) for lookups if needed.
- Key rotation requires re-encrypting all affected rows. Plan a migration strategy.
- Store Data Protection keys on the encrypted LUKS volume, not in the default filesystem location.

### 3c. pg_tde (Transparent Data Encryption)

PostgreSQL 16 supports the `pg_tde` extension for transparent data encryption at the tablespace level. This encrypts data files, WAL, and temporary files without application changes.

**Current status:**

- `pg_tde` is available but still maturing (check compatibility with pgvector before enabling).
- If pgvector compatibility is confirmed, pg_tde provides stronger guarantees than LUKS alone (protects against memory dump attacks on a running system).
- For now, rely on LUKS volume encryption as the primary control and evaluate pg_tde as an additional layer.

**To evaluate:**

```sql
-- Check if pg_tde is available
SELECT * FROM pg_available_extensions WHERE name = 'pg_tde';

-- If available and compatible with pgvector:
CREATE EXTENSION pg_tde;
-- Follow pg_tde documentation for key management setup
```

---

## 4. Redis Encryption

### 4a. TLS for Connections

Redis 7.x supports native TLS. All connections between the application and Redis should be encrypted.

**Generate certificates:**

```bash
openssl req -new -x509 -days 365 -nodes \
  -out /etc/ssl/certs/redis.crt \
  -keyout /etc/ssl/private/redis.key \
  -subj "/CN=meepleai-redis"

# Generate CA cert (or reuse the same for self-signed)
cp /etc/ssl/certs/redis.crt /etc/ssl/certs/redis-ca.crt
```

**Docker Compose configuration:**

```yaml
redis:
  image: redis:7.4.1-alpine
  command: >
    redis-server
    --requirepass ${REDIS_PASSWORD}
    --tls-port 6380
    --port 0
    --tls-cert-file /tls/redis.crt
    --tls-key-file /tls/redis.key
    --tls-ca-cert-file /tls/ca.crt
    --tls-auth-clients no
  volumes:
    - ./certs/redis.crt:/tls/redis.crt:ro
    - ./certs/redis.key:/tls/redis.key:ro
    - ./certs/redis-ca.crt:/tls/ca.crt:ro
    - redis-data:/data
```

**Update the connection string in `infra/secrets/redis.secret`:**

```
REDIS_CONNECTION_STRING=redis:6380,password=<password>,ssl=true,abortConnect=false
```

### 4b. RDB/AOF File Encryption

Redis does not natively encrypt its RDB snapshots or AOF persistence files. These files contain plaintext copies of all cached data.

**Mitigation:** The LUKS-encrypted volume mounted at `/mnt/encrypted/redisdata` ensures that RDB and AOF files are encrypted at rest on disk. This is sufficient for GDPR compliance when combined with TLS for transport.

If additional protection is needed (e.g., before transferring snapshots offsite), encrypt manually:

```bash
gpg --symmetric --cipher-algo AES256 /mnt/encrypted/redisdata/dump.rdb
```

---

## 5. Qdrant Encryption

Qdrant (v1.12.4) does not provide native encryption at rest for its collection storage files.

**Mitigation strategy:**

1. **Volume encryption (primary):** Mount Qdrant's storage directory on a LUKS-encrypted volume at `/mnt/encrypted/qdrantdata`.
2. **API key authentication:** Already configured via `infra/secrets/qdrant.secret`. Ensure the API key is set and that Qdrant is not exposed on public interfaces.
3. **TLS for gRPC/HTTP:** Configure Qdrant with TLS certificates for its API endpoints:

```yaml
qdrant:
  image: qdrant/qdrant:v1.12.4
  environment:
    - QDRANT__SERVICE__API_KEY=${QDRANT_API_KEY}
    - QDRANT__TLS__CERT=/tls/qdrant.crt
    - QDRANT__TLS__KEY=/tls/qdrant.key
  volumes:
    - ./certs/qdrant.crt:/tls/qdrant.crt:ro
    - ./certs/qdrant.key:/tls/qdrant.key:ro
    - qdrant-data:/qdrant/storage
```

4. **Network isolation:** Qdrant should only be accessible from the Docker internal network. Do not expose port 6333/6334 to the host or public internet.

---

## 6. Backup Encryption

All backups must be encrypted before storage or transfer. Unencrypted backups negate the value of encryption at rest.

### PostgreSQL Backups

```bash
# Encrypted full backup
pg_dumpall -h postgres -U meepleai \
  | gzip \
  | gpg --encrypt --recipient admin@meepleai.com \
  > backup-$(date +%Y%m%d).sql.gz.gpg

# Restore
gpg --decrypt backup-20260310.sql.gz.gpg \
  | gunzip \
  | psql -h postgres -U meepleai
```

### Redis Backups

```bash
# Copy RDB snapshot and encrypt
cp /mnt/encrypted/redisdata/dump.rdb /tmp/redis-backup.rdb
gpg --encrypt --recipient admin@meepleai.com /tmp/redis-backup.rdb
rm /tmp/redis-backup.rdb
# Transfer redis-backup.rdb.gpg to offsite storage
```

### Qdrant Backups

```bash
# Trigger Qdrant snapshot via API
curl -X POST "http://localhost:6333/collections/meepleai/snapshots"

# Encrypt the snapshot file
gpg --encrypt --recipient admin@meepleai.com \
  /mnt/encrypted/qdrantdata/snapshots/meepleai/*.snapshot
```

### GPG Key Management

- Generate a dedicated GPG key pair for backup encryption.
- Store the private key securely offline (not on the production server).
- Distribute the public key to all servers that create backups.
- Test decryption regularly as part of disaster recovery drills.

---

## 7. Compliance Checklist

| # | Control                                  | Service    | Status        | Action Required                                    |
|---|------------------------------------------|------------|---------------|----------------------------------------------------|
| 1 | LUKS2 volume encryption                  | All        | NOT CONFIGURED | Provision encrypted volumes, run setup script       |
| 2 | PostgreSQL TLS (ssl=on)                  | PostgreSQL | NOT CONFIGURED | Generate certs, update compose and connection string|
| 3 | PostgreSQL column encryption             | PostgreSQL | NOT CONFIGURED | Implement ValueConverter for email, OAuth tokens    |
| 4 | pg_tde evaluation                        | PostgreSQL | PENDING        | Test pgvector compatibility                         |
| 5 | Redis TLS (tls-port 6380)               | Redis      | NOT CONFIGURED | Generate certs, update compose and connection string|
| 6 | Redis RDB/AOF on LUKS volume            | Redis      | NOT CONFIGURED | Depends on item 1                                   |
| 7 | Qdrant storage on LUKS volume           | Qdrant     | NOT CONFIGURED | Depends on item 1                                   |
| 8 | Qdrant API key authentication           | Qdrant     | CONFIGURED     | Verify via qdrant.secret                            |
| 9 | Qdrant TLS for API                      | Qdrant     | NOT CONFIGURED | Generate certs, update compose                      |
| 10| Backup encryption (GPG)                 | All        | NOT CONFIGURED | Generate GPG key pair, update backup scripts        |
| 11| LUKS key management procedure           | All        | NOT CONFIGURED | Document key storage and rotation policy            |
| 12| Docker network isolation                | All        | PARTIAL        | Audit exposed ports, restrict to internal network   |

### Priority Order

1. **LUKS volume encryption** (items 1, 6, 7) -- highest impact, covers all services
2. **TLS for all services** (items 2, 5, 9) -- prevents interception on internal network
3. **Backup encryption** (item 10) -- prevents data exposure via backup files
4. **Column-level encryption** (item 3) -- defense in depth for high-sensitivity fields
5. **pg_tde evaluation** (item 4) -- additional layer, lower priority

---

## References

- Hetzner DPA: https://www.hetzner.com/legal/data-processing-agreement
- GDPR Article 32: https://gdpr-info.eu/art-32-gdpr/
- LUKS2 documentation: https://gitlab.com/cryptsetup/cryptsetup
- PostgreSQL SSL: https://www.postgresql.org/docs/16/ssl-tcp.html
- Redis TLS: https://redis.io/docs/latest/operate/oss_and_stack/management/security/encryption/
- Qdrant Security: https://qdrant.tech/documentation/guides/security/
- ASP.NET Data Protection: https://learn.microsoft.com/en-us/aspnet/core/security/data-protection/
