# Dashboard Test Data Seeder - Issue #4576

Seed scripts per popolare il database con dati di test per la Gaming Hub Dashboard.

## 🎯 Data Creati

- **3 Utenti**: Marco (Free), Sara (Pro), Luca (Enterprise)
- **20 Giochi**: Catan, Azul, Wingspan, 7 Wonders, Ticket to Ride, Pandemic, Codenames, Splendor, Dixit, Carcassonne, Agricola, Terra Mystica, Scythe, Gloomhaven, King of Tokyo, Dominion, Terraforming Mars, Betrayal, Dead of Winter, Spirit Island
- **Play Records**: 2 sessioni di esempio per Marco (più verranno aggiunte in futuro)

## 🚀 Usage

**Con Docker** (raccomandato):
```powershell
.\scripts\seed-dashboard-data.ps1 -UseDocker
```

**Con PostgreSQL locale**:
```powershell
.\scripts\seed-dashboard-data.ps1
```

**Manuale con psql**:
```bash
psql -h localhost -U postgres -d meepleai -f scripts/seed-dashboard-data.sql
```

## ✅ Cosa Viene Creato

Dal seed script:
- **3 Utenti di Test**: Marco Rossi (Free), Sara Bianchi (Pro), Luca Verdi (Enterprise)
- **10+ Giochi Condivisi**: Catan, Azul, Wingspan, 7 Wonders, Ticket to Ride, Pandemic, Codenames, Splendor, Dixit, Carcassonne
- **2 Play Records per Marco**: Catan (4 players, oggi), Wingspan (solo, ieri)

## 📝 Test Credentials

Dopo il seeding, puoi fare login con:

| Email | Password | Tier | Note |
|-------|----------|------|------|
| marco.test@meeple.ai | [Same as admin] | Free | Ha 2 play records |
| sara.test@meeple.ai | [Same as admin] | Pro | Nessun record ancora |
| luca.test@meeple.ai | [Same as admin] | Enterprise | Nessun record ancora |

**Note**: La password è la stessa dell'utente admin (`admin@meepleai.dev`) per semplicità.

## 🔄 Idempotenza

Gli script sono **idempotenti**:
- ✅ Utenti: `ON CONFLICT DO NOTHING` (by Email)
- ✅ Giochi: `ON CONFLICT DO NOTHING` (by Title)
- ✅ Play Records: Check count before insert

Puoi rieseguire lo script senza creare duplicati.

## 🧪 Verification

Verifica che i dati siano stati creati:

```bash
# Check users
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT \"DisplayName\", \"Email\", \"Tier\" FROM \"Users\" WHERE \"Email\" LIKE '%test@meeple.ai';"

# Check games
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT \"Title\", \"MinPlayers\", \"MaxPlayers\", \"AverageRating\" FROM \"SharedGames\" WHERE \"IsDeleted\" = false LIMIT 10;"

# Check play records
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "SELECT pr.\"GameName\", pr.\"SessionDate\", pr.\"Duration\", u.\"DisplayName\"
   FROM \"PlayRecords\" pr
   JOIN \"Users\" u ON pr.\"CreatedByUserId\" = u.\"Id\"
   ORDER BY pr.\"SessionDate\" DESC LIMIT 5;"
```

## 🛠️ Troubleshooting

### PostgreSQL not running
```bash
# Start with Docker
cd infra && docker compose up -d postgres

# Check status
docker ps | grep postgres
```

### Connection refused
```bash
# Verify PostgreSQL is listening
docker exec meepleai-postgres pg_isready -U postgres

# Check connection from host
psql -h localhost -U postgres -d meepleai -c "SELECT version();"
```

### Duplicate key errors
```bash
# Script is idempotent, but if you want to reset:
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "DELETE FROM \"PlayRecords\" WHERE EXISTS (
     SELECT 1 FROM \"Users\"
     WHERE \"Users\".\"Id\" = \"PlayRecords\".\"CreatedByUserId\"
       AND \"Users\".\"Email\" LIKE '%test@meeple.ai'
   );"

# Delete test users (cascades to play records)
docker exec -it meepleai-postgres psql -U postgres -d meepleai -c \
  "DELETE FROM \"Users\" WHERE \"Email\" LIKE '%test@meeple.ai';"
```

## 📦 Files

- `seed-dashboard-data.sql` - SQL script principale
- `seed-dashboard-data.ps1` - PowerShell wrapper (raccomandato)

## 🔗 Related

- Epic #4575: Gaming Hub Dashboard
- Issue #4576: Seed data script (questo script)
- Docs: `docs/epics/epic-gaming-hub-dashboard.md`
