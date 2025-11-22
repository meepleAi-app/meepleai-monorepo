# Database Operations

Scripts for database seeding, migrations, and vector store management.

## Scripts

### 🌱 **seed-demo-data.sql**
**Purpose:** Seed database with demo data for development/testing

**What it contains:**
- Demo users (admin@meepleai.dev, editor@meepleai.dev, user@meepleai.dev)
- Sample games (Catan, Ticket to Ride, etc.)
- Sample PDF documents
- Test chat threads and messages

**Usage:**
```bash
# Run via psql
psql -U postgres -d meepleai -f tools/database/seed-demo-data.sql

# Or via EF Core migrations (auto-seeded on startup in dev)
cd apps/api && dotnet run
```

**Who:** Developers needing test data
**When:** Fresh database setup, integration testing
**Password:** All demo users have password `Demo123!`

---

### 🗑️ **delete-qdrant-collection.ps1**
**Purpose:** Delete vector collections from Qdrant database

**What it does:**
- Lists all Qdrant collections
- Deletes specified collection
- Clears vector embeddings for fresh re-indexing

**Usage:**
```powershell
# List collections
.\tools\database\delete-qdrant-collection.ps1 -List

# Delete specific collection
.\tools\database\delete-qdrant-collection.ps1 -Collection "board_games"

# Delete all collections (dangerous!)
.\tools\database\delete-qdrant-collection.ps1 -All -Confirm
```

**Who:** Developers troubleshooting RAG/vector search
**When:** Re-indexing documents, clearing bad embeddings
**Requirements:** Qdrant running on localhost:6333, PowerShell 5.1+

**Warning:** Deleting collections requires re-indexing documents (slow operation)

---

**Last Updated:** 2025-11-22
**Maintained by:** Backend team
