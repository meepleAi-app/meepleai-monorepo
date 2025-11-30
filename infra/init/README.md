# Infrastructure Initialization

## Contenuto

Script e file di inizializzazione per database, n8n workflows e altri servizi.

## Struttura

```
init/
├── postgres-init.sql          Inizializzazione PostgreSQL (minimal)
├── api-migrations-20251118.sql   Snapshot migrations API
└── n8n/                       n8n workflows initialization
    └── README.md              (vedi sottocartella)
```

## File di Inizializzazione

### postgres-init.sql

**Scopo**: Script di inizializzazione minimale per PostgreSQL.

**Contenuto**:
```sql
-- Extension creation
CREATE EXTENSION IF NOT EXISTS "uuid-ossp";
CREATE EXTENSION IF NOT EXISTS pg_trgm;  -- Per full-text search
```

**Quando Viene Eseguito**:
- Al primo avvio del container PostgreSQL
- Se database `meepleai` non esiste

**Utilizzo**:
```yaml
# docker-compose.yml
postgres:
  volumes:
    - ./init/postgres-init.sql:/docker-entrypoint-initdb.d/init.sql
```

**Note**:
- EF Core migrations gestiscono schema completo
- Questo script è solo per extensions e setup minimale

### api-migrations-20251118.sql

**Scopo**: Snapshot completo delle migrations API al 2025-11-18.

**Contenuto**: Schema completo database con:
- Tutte le tabelle (Users, Games, PdfDocuments, etc.)
- Indici
- Foreign keys
- Constraints
- Data seed (demo users)

**Quando Usare**:
- Setup rapido development (skip migrations)
- Restore database a stato noto
- Testing con schema completo

**Utilizzo**:
```bash
# Restore da snapshot
docker compose exec postgres psql -U meepleai -d meepleai -f /docker-entrypoint-initdb.d/api-migrations-20251118.sql

# O durante init
# Rinomina postgres-init.sql → postgres-init.sql.bak
# Copia api-migrations-20251118.sql → postgres-init.sql
# docker compose down -v && docker compose up -d
```

**Attenzione**:
⚠️ Snapshot può essere outdated rispetto a migrations correnti!

**Alternative Raccomandata**: Usa EF Core migrations:
```bash
cd apps/api/src/Api
dotnet ef database update
```

### n8n/

Vedi `n8n/README.md` per dettagli su workflow initialization.

**Contenuto**:
- Workflow template import scripts
- Credentials initialization
- n8n configuration

## Workflow di Inizializzazione

### Development (Fresh Start)

```bash
# 1. Start PostgreSQL
docker compose up -d postgres

# 2. Wait for healthy
docker compose ps postgres  # Deve essere "healthy"

# 3. Run migrations (API)
cd apps/api/src/Api
dotnet ef database update

# 4. Seed data (optional)
dotnet run -- --seed

# 5. Start resto dello stack
cd ../../../../infra
docker compose up -d
```

### Development (Quick Restore from Snapshot)

```bash
# 1. Stop services
docker compose down -v  # ⚠️ Rimuove volumes!

# 2. Use snapshot as init script
cp api-migrations-20251118.sql postgres-init.sql

# 3. Start stack
docker compose up -d

# 4. Verify
docker compose exec postgres psql -U meepleai -d meepleai -c "\dt"
```

### Production (Migrations-Based)

```bash
# 1. Backup database
docker compose exec postgres pg_dump -U meepleai meepleai > backup-$(date +%Y%m%d).sql

# 2. Run migrations
cd apps/api/src/Api
dotnet ef database update --connection "Host=prod-pg;..."

# 3. Verify schema
dotnet ef migrations list

# 4. Restore backup se problemi
cat backup-20251118.sql | docker compose exec -T postgres psql -U meepleai -d meepleai
```

## Seed Data

### Demo Users (inclusi in migrations)

```
Email: admin@meepleai.dev
Password: Demo123!
Role: Admin

Email: editor@meepleai.dev
Password: Demo123!
Role: Editor

Email: user@meepleai.dev
Password: Demo123!
Role: User
```

**⚠️ SECURITY**: Cambia password in production!

### Custom Seed Data

Aggiungi in API startup:

```csharp
// Program.cs
if (app.Environment.IsDevelopment() && args.Contains("--seed"))
{
    using var scope = app.Services.CreateScope();
    var dbContext = scope.ServiceProvider.GetRequiredService<MeepleAiDbContext>();

    // Seed games
    if (!await dbContext.Games.AnyAsync())
    {
        dbContext.Games.AddRange(
            new GameEntity { Title = "Catan", Publisher = "Kosmos", Year = 1995 },
            new GameEntity { Title = "Ticket to Ride", Publisher = "Days of Wonder", Year = 2004 }
        );
        await dbContext.SaveChangesAsync();
    }
}
```

## Extensions PostgreSQL

### uuid-ossp

**Scopo**: Generazione UUID v4

**Funzioni**:
- `uuid_generate_v4()` - Random UUID

**Utilizzo**:
```sql
CREATE TABLE users (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name TEXT
);
```

### pg_trgm (Trigram)

**Scopo**: Full-text search e similarity matching

**Funzioni**:
- `similarity(text, text)` - Calcola similarity score
- `word_similarity(text, text)` - Word-level similarity

**Utilizzo**:
```sql
-- Cerca giochi per nome (fuzzy)
SELECT * FROM games
WHERE similarity(title, 'catan') > 0.3
ORDER BY similarity(title, 'catan') DESC;

-- Indice GIN per performance
CREATE INDEX games_title_trgm_idx ON games USING GIN (title gin_trgm_ops);
```

**MeepleAI Usage**:
- Keyword search component di hybrid RAG
- Game search (fuzzy matching)

## Creazione Nuovo Script di Init

### Best Practices

1. **Idempotency**: Script deve essere re-runnable
   ```sql
   CREATE TABLE IF NOT EXISTS users (...);
   CREATE EXTENSION IF NOT EXISTS uuid-ossp;
   ```

2. **Transaction**: Wrap in transaction
   ```sql
   BEGIN;
   -- Your changes
   COMMIT;
   ```

3. **Error Handling**: Check failures
   ```sql
   DO $$
   BEGIN
       -- Try operation
   EXCEPTION WHEN OTHERS THEN
       RAISE NOTICE 'Error: %', SQLERRM;
   END $$;
   ```

4. **Logging**: Output progress
   ```sql
   RAISE NOTICE 'Creating table users...';
   ```

### Template

```sql
-- init/my-init.sql
BEGIN;

-- Extension
CREATE EXTENSION IF NOT EXISTS my_extension;

-- Table
CREATE TABLE IF NOT EXISTS my_table (
    id UUID PRIMARY KEY DEFAULT uuid_generate_v4(),
    name TEXT NOT NULL,
    created_at TIMESTAMP DEFAULT NOW()
);

-- Index
CREATE INDEX IF NOT EXISTS my_table_name_idx ON my_table(name);

-- Seed data (solo se tabella vuota)
INSERT INTO my_table (name)
SELECT 'seed_value'
WHERE NOT EXISTS (SELECT 1 FROM my_table LIMIT 1);

COMMIT;
```

### Eseguire Script Custom

```bash
# Durante init (aggiungi a docker-compose.yml)
volumes:
  - ./init/my-init.sql:/docker-entrypoint-initdb.d/02-my-init.sql

# Manualmente dopo init
docker compose exec postgres psql -U meepleai -d meepleai -f /init/my-init.sql
```

## Snapshot Migrations

### Creare Nuovo Snapshot

```bash
# 1. Assicurati che DB sia up-to-date con tutte le migrations
cd apps/api/src/Api
dotnet ef database update

# 2. Dump schema
docker compose exec postgres pg_dump -U meepleai -d meepleai --schema-only > infra/init/api-migrations-$(date +%Y%m%d).sql

# 3. Oppure schema + data
docker compose exec postgres pg_dump -U meepleai -d meepleai > infra/init/api-full-$(date +%Y%m%d).sql
```

### Quando Creare Snapshot

- Dopo grandi refactor (es. DDD migration complete)
- Milestone releases (v1.0, v2.0)
- Prima di breaking changes
- Quarterly (se database stabile)

### Retention

- Keep last 3 snapshots
- Archive older snapshots in `docs/archive/`

## Troubleshooting

### Init Script Non Eseguito

**Possibili Cause**:
1. Database già esiste (init scripts run solo su empty DB)
2. File non montato correttamente in `/docker-entrypoint-initdb.d/`
3. Permission denied su file

**Fix**:
```bash
# Rimuovi volume e ricrea
docker compose down -v
docker compose up -d postgres

# Verifica mount
docker compose exec postgres ls -la /docker-entrypoint-initdb.d/
```

### Extension Already Exists Error

**Causa**: Script non idempotente

**Fix**:
```sql
-- Usa IF NOT EXISTS
CREATE EXTENSION IF NOT EXISTS uuid-ossp;
```

### Migration Conflicts

**Causa**: Snapshot outdated, migrations aggiunte dopo

**Fix**:
```bash
# Option 1: Apply migrations on top of snapshot
dotnet ef database update

# Option 2: Fresh migrations (remove snapshot)
docker compose down -v
# Rimuovi snapshot da init/
docker compose up -d
dotnet ef database update
```

## Related Documentation

- `n8n/README.md` - n8n workflows initialization
- `../env/README.md` - Environment variables
- `../../docs/02-development/database-migrations.md`
- EF Core migrations: `apps/api/src/Api/Migrations/`
