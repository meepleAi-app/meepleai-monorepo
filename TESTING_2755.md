# Testing Guide - Issue #2755

**Branch**: `fix/frontend-dev-2755`
**PR**: [#2757](https://github.com/DegrassiAaron/meepleai-monorepo/pull/2757)
**Status**: ✅ Implementation complete - ⏳ Final testing required

## 🎯 Obiettivo

Verificare che gli errori "Schema validation failed" siano risolti:
```
[ERROR] Schema validation failed: Response validation failed for /api/v1/sessions/active
[ERROR] Schema validation failed: Response validation failed for /api/v1/library
```

## 🔧 Setup Ambiente

### 1. Avviare Infrastruttura

```bash
cd D:\Repositories\meepleai-monorepo-dev\infra
docker compose up -d postgres redis qdrant
```

**Attendi 10-15 secondi** per PostgreSQL inizializzazione.

### 2. Avviare Backend

```bash
cd D:\Repositories\meepleai-monorepo-dev\apps\api\src\Api
dotnet run
```

**Attendi** il messaggio: `Now listening on: http://0.0.0.0:8080`

### 3. Verificare Health

```bash
curl http://localhost:8080/health
```

**Verifica** che `postgres`, `redis`, `qdrant` siano `Healthy`.

## 🧪 Test Scenarios

### Test 1: Dashboard Schema Validation

**Obiettivo**: Verificare che la dashboard carichi senza errori schema.

**Steps**:
1. Apri browser: `http://localhost:3000/login`
2. Login con credenziali admin:
   - Email: `admin@meepleai.dev`
   - Password: `pVKOMQNK0tFNgGlX` (da `infra/secrets/admin.secret`)
3. Dopo redirect a `/dashboard`, apri DevTools (F12)
4. Verifica tab Console

**Risultato atteso**:
```
✅ NO errori "Schema validation failed" per /api/v1/sessions/active
✅ NO errori "Schema validation failed" per /api/v1/library
✅ Dashboard mostra: "Buonanotte, System Admin! 👋"
```

**Risultato se fallisce**:
```
❌ [API Error] Schema validation failed: Response validation failed for /api/v1/sessions/active
❌ [API Error] Schema validation failed: Response validation failed for /api/v1/library
```

### Test 2: API Response Structure

**Obiettivo**: Verificare che backend ritorni DTO corretti.

**Test /sessions/active**:
```bash
curl -H "Cookie: session=YOUR_SESSION_TOKEN" \
  "http://localhost:8080/api/v1/sessions/active?limit=3&offset=0"
```

**Risposta attesa**:
```json
{
  "sessions": [],
  "total": 0,
  "page": 1,
  "pageSize": 3
}
```

**Test /library**:
```bash
curl -H "Cookie: session=YOUR_SESSION_TOKEN" \
  "http://localhost:8080/api/v1/library?page=1&pageSize=5&sortBy=addedAt&sortDescending=true"
```

**Risposta attesa**:
```json
{
  "items": [],
  "page": 1,
  "pageSize": 5,
  "totalCount": 0,
  "totalPages": 0,
  "hasNextPage": false,
  "hasPreviousPage": false
}
```

### Test 3: OpenAPI Documentation

**Obiettivo**: Verificare che Scalar UI mostri tutti gli endpoint.

**Steps**:
1. Apri browser: `http://localhost:8080/scalar/v1`
2. Scorri la lista endpoint

**Risultato atteso**:
```
✅ Admin/Sessions section (3 endpoint):
   - GET /admin/sessions
   - DELETE /admin/sessions/{sessionId}
   - DELETE /admin/users/{userId}/sessions

✅ Games section (6+ endpoint):
   - GET /games (PaginatedGamesResponse)
   - GET /games/{id} (GameDto)
   - ...

✅ Sessions section (20+ endpoint):
   - GET /sessions/active (PaginatedSessionsResponseDto)
   - GET /sessions/history
   - POST /sessions
   - ...

✅ Library section (17 endpoint):
   - GET /library (PaginatedLibraryResponseDto)
   - POST /library/games/{gameId}
   - ...
```

### Test 4: Altre Pagine Frontend

**Obiettivo**: Verificare assenza errori schema su altre pagine.

**Pagine da testare**:

| Pagina | URL | Verifica |
|--------|-----|----------|
| **Games Catalog** | `/games` | Console pulita |
| **Library** | `/library` | Console pulita |
| **Chat** | `/chat` | Console pulita |
| **Settings** | `/settings` | Console pulita |

**Per ognuna**:
1. Naviga alla pagina
2. Apri DevTools → Console
3. Verifica assenza errori "Schema validation failed"

## 📋 Checklist Finale

- [ ] Infrastruttura avviata (postgres, redis, qdrant)
- [ ] Backend avviato e healthy
- [ ] Test 1: Dashboard senza schema errors ✅
- [ ] Test 2: API response structure corretta ✅
- [ ] Test 3: Scalar UI mostra 46+ endpoint ✅
- [ ] Test 4: Altre pagine senza errori ✅
- [ ] PR #2757 approvata
- [ ] Merge to `frontend-dev`
- [ ] Branch cleanup: `git branch -D fix/frontend-dev-2755`

## 🐛 Troubleshooting

### Backend non parte

```bash
# Check port conflicts
netstat -ano | findstr :8080

# Kill process
taskkill /PID <PID> /F

# Restart
cd apps/api/src/Api && dotnet run
```

### PostgreSQL connection refused

```bash
# Check docker
docker ps | grep postgres

# Restart
cd infra && docker compose restart postgres

# Wait 15s
sleep 15
```

### Schema errors persistono

**Verifica**:
1. Backend esegue codice aggiornato: `git log -1 --oneline` deve mostrare commit `a3e473c`
2. Frontend cache cleared: Hard reload (Ctrl+Shift+R)
3. Verifica risposta API diretta con curl (Test 2)

## ✅ Success Criteria

**Issue #2755 risolta quando**:
1. ✅ Dashboard carica senza "Schema validation failed" errors
2. ✅ API ritorna strutture corrette (Test 2 pass)
3. ✅ Scalar UI mostra tutti endpoint (Test 3 pass)
4. ✅ Nessun errore su games/library/chat/settings (Test 4 pass)

---

**Last Updated**: 2026-01-20
**Branch**: `fix/frontend-dev-2755`
**PR**: #2757
