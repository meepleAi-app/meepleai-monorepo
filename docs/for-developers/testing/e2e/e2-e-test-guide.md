# E2E Test Guide - Full Suite Execution

**Purpose**: Complete guide for running end-to-end tests requiring full infrastructure.

---

## Test Categories Overview

| Category | Test Count | Prerequisites | Offline Capable |
|----------|-----------|---------------|-----------------|
| **Unit** | ~3,500 | None | ✅ Yes |
| **Integration** | ~1,800 | Docker (Testcontainers) | ✅ Yes |
| **E2E** | ~700 | API + Full Infra | ❌ No |

---

## Quick Start - Run All Tests

### Option 1: Unit + Integration Only (Offline)
```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "Category!=E2E"
```

**Result**: ~5,300 tests pass without external services.

### Option 2: Full Suite (Requires Infrastructure)
```bash
# Terminal 1: Start infrastructure
cd infra
docker compose up postgres qdrant redis

# Terminal 2: Start API
cd apps/api/src/Api
dotnet run

# Terminal 3: Run all tests
cd apps/api/tests/Api.Tests
dotnet test
```

**Result**: All ~6,021 tests execute (requires API + services).

---

## Prerequisites by Test Category

### Unit Tests (~3,500 tests)
**No prerequisites** - Pure logic tests with mocks.

```bash
dotnet test --filter "Category=Unit"
```

**Examples**:
- Domain entity tests
- Value object validation
- Command handler logic (with mocked repositories)

---

### Integration Tests (~1,800 tests)
**Requires**: Docker Desktop (for Testcontainers)

```bash
# Ensure Docker is running
docker ps

# Run integration tests
dotnet test --filter "Category=Integration"
```

**How It Works**:
- **Testcontainers** automatically spins up PostgreSQL/Redis containers
- Each test gets isolated database (no pollution)
- Containers cleaned up after test completion

**Examples**:
- Repository persistence tests
- Database migration tests
- Cache integration tests
- Query handler tests

---

### E2E Tests
**Requires**: Full infrastructure + running API + **tre prerequisiti non ovvi** (vedi sotto)

Per il conteggio, misuralo invece di fidarti di un numero scritto:

```bash
cd apps/web && npx playwright test --list --reporter=list | tail -1
# e la ripartizione per project:
cd apps/web && npx playwright test --list --reporter=list \
  | grep -oP '^\s+\[\K[^\]]+' | sort | uniq -c | sort -rn
```

> ⚠️ Questa sezione dichiarava «~700 tests». Misurato il 2026-10-04: **18.780 test in 392 file**, di cui lo stesso insieme di **3.121** replicato su 6 project browser/viewport. Un numero in prosa qui invecchia in silenzio: usa il comando.

#### Step 1: Start Infrastructure Services

```bash
cd infra
make dev-core          # oppure: docker compose up -d postgres redis minio
```

> ⚠️ Questa sezione istruiva ad avviare `qdrant`. **Lo stack non usa Qdrant**: il vettoriale è
> `pgvector` dentro Postgres (immagine `pgvector/pgvector:pg16`). Verifica con
> `docker ps --format '{{.Names}}\t{{.Image}}'`.

**Verify Services**:
```bash
# PostgreSQL — il database e' meepleai_staging, non "meepleai"
docker exec -it meepleai-postgres psql -U meepleai -d meepleai_staging -c "SELECT version();"

# pgvector e' un'estensione, non un servizio a parte
docker exec -it meepleai-postgres psql -U meepleai -d meepleai_staging \
  -c "SELECT extname, extversion FROM pg_extension WHERE extname='vector';"

# Redis
docker exec -it meepleai-redis redis-cli ping
```

#### Step 1b 🔴 — I tre prerequisiti che bloccano un run locale

Misurati il 2026-10-04 facendo girare lo **stesso** spec (`e2e/a11y/games-library.spec.ts`, 3 test)
in più configurazioni. Nessuno dei fallimenti era un difetto dei test o del prodotto:

| configurazione | esito |
|---|---|
| contro il container `meepleai-web` | **0/3** in 130 s |
| contro `next dev` avviato da Playwright, a freddo | **1/3** in 4,1 min |
| contro `next dev` già caldo e riusato | **3/3 in 7,7 s** |

> ⚠️ Quel «3/3 in 7,7 s» era inizialmente attribuito a `next start`. **Sbagliato**: 7,7 s non bastano
> ad avviare un server, e `reuseExistingServer: !CI` aveva fatto riusare il `next dev` della prova
> precedente, già caldo e col bypass attivo. È lo stesso inganno del punto (b) qui sotto, visto dal
> lato opposto — e si riconosce solo da un tempo troppo breve per essere vero.

**(a) Il container web puo' essere `healthy` e irraggiungibile dall'host.**
L'healthcheck di Docker sonda *dall'interno*, quindi un port proxy incagliato gli e' invisibile.
Sintomo: `curl` esce **52** («Empty reply from server») su una porta in `LISTENING`, mentre
dall'interno la stessa URL risponde 200.

```bash
curl -sS -o /dev/null -w '%{http_code}\n' http://localhost:3000/        # dall'host
docker exec meepleai-web node -e "fetch('http://localhost:3000/').then(r=>console.log(r.status))"
# se il primo e' 000 e il secondo 200 -> port proxy incagliato: docker restart meepleai-web
```

**(b) Playwright RIUSA il server esistente, e quello sbagliato non ha il bypass di auth.**
`playwright.config.ts` ha un blocco `webServer` che avvia un Next.js proprio con
`PLAYWRIGHT_AUTH_BYPASS: 'true'`, ma anche `reuseExistingServer: !process.env.CI`. In locale
`CI` non e' impostato, quindi **se qualcosa ascolta sulla 3000 Playwright non avvia nulla** e quelle
env non esistono per il processo che serve le pagine. `proxy.ts` pretende
`PLAYWRIGHT_AUTH_BYPASS === 'true'` per fidarsi del cookie di sessione; senza, ogni rotta sotto
`src/app/(authenticated)/**` redirige:

```bash
curl -s -o /dev/null -w '%{http_code}\n' http://localhost:3000/library   # 307 -> /login
```

Rimedio: liberare la 3000 (`docker stop meepleai-web`) e lasciare che Playwright avvii il suo
server. In alternativa, aggiungere `PLAYWRIGHT_AUTH_BYPASS=true` all'env del container e
riavviarlo — **non** serve un rebuild, perche' non e' una variabile `NEXT_PUBLIC_*` inlineata.

**(c) `next dev` compila su richiesta e sfonda il timeout dei test.**
Il timeout locale e' 60 s (`playwright.config.ts`); la prima visita a una rotta in dev mode puo'
costare di piu'. Il sintomo inganna: il test fallisce con `Test timeout of 60000ms exceeded` **dopo**
aver renderizzato correttamente la pagina, e sembra un problema a11y o di selettore.

```bash
cd apps/web
NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 pnpm build     # una volta
FORCE_PRODUCTION_SERVER=true npx playwright test <spec> --project=desktop-chrome
```

🔴 **E qui c'e' la trappola peggiore: con `FORCE_PRODUCTION_SERVER=true` il bypass di auth e' MORTO,
e Playwright non te lo dice.**

`proxy.ts` ammette il bypass se `NODE_ENV !== 'production'` **oppure** se
`NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED === '1'`. Con `next start` `NODE_ENV` e' `production`,
quindi il primo ramo si piega a `false` e il bundler lo elimina: nel chunk compilato resta

```js
o = "1" === process.env.NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED
 && "true" === process.env.PLAYWRIGHT_AUTH_BYPASS && !!a;
```

Ma il blocco `webServer` di `playwright.config.ts` imposta **solo** `PLAYWRIGHT_AUTH_BYPASS` (e
`NEXT_PUBLIC_MECHANIC_VALIDATION_ENABLED`): **non** il flag visual-test. Quindi la prima condizione
e' falsa, il bypass non si innesca, e ogni rotta `(authenticated)` redirige — su TUTTA la suite,
con fallimenti che somigliano a selettori invecchiati.

Il flag va passato **a runtime** al server (nel chunk e' una lettura a runtime, non un valore
inlineato), quindi basta averlo nell'ambiente da cui lanci Playwright:

```bash
NEXT_PUBLIC_VISUAL_TEST_FIXTURE_ENABLED=1 FORCE_PRODUCTION_SERVER=true \
  pnpm exec dotenv -e .env.test -- playwright test <spec> --project=desktop-chrome
```

Verifica in un colpo, senza passare da Playwright:

```bash
# server avviato con ENTRAMBE le variabili
curl -s -o /dev/null -w '%{http_code}\n' \
  -H 'Cookie: meepleai_session=playwright-fixture-session-token; meepleai_user_role=admin' \
  http://localhost:3000/library
# 200 = bypass attivo | 307 = bypass morto
```

⚠️ Quando ripeti questa sonda, **accertati che il server vecchio sia morto**: `pkill` puo' non
liberare la porta, e si finisce a interrogare l'istanza precedente leggendo il suo esito come
quello della configurazione nuova. Costa un `curl` verificare che la 3000 risponda `000` prima di
riavviare.

#### Step 1c 🔴 — L'autenticazione ha tre meccanismi, e solo uno non richiede credenziali

Misurato il 2026-10-04: su 131 fallimenti di un primo run, **71 (54%) finivano su una pagina di
login**. Non e' un difetto dell'app: e' quale meccanismo lo spec usa.

| meccanismo | come riconoscerlo | serve una credenziale? |
|---|---|---|
| cookie + bypass | chiama `seedAuthSession(` o `seedMockRoleCookies(` | **no** |
| login vero | importa `test` da `e2e/fixtures` (`authenticateAsAdmin()`) | **sì** |
| nessuno | nessuno dei due | solo rotte pubbliche |

```bash
cd apps/web
for h in 'seedAuthSession(' 'seedMockRoleCookies('; do
  printf '%-24s %s spec\n' "$h" "$(grep -rl "$h" e2e/ --include=*.spec.ts | wc -l)"
done
grep -rl "from '\.\./fixtures'" e2e/ --include=*.spec.ts | wc -l   # login vero
```

🔴 **Le credenziali del login vero vengono dall'ambiente, e `.env.test` non esiste** (e' gitignorato;
il template e' `.env.test.example`). E `dotenv -e .env.test` **non fallisce** su un file mancante:
procede in silenzio con exit 0.

⚠️ Il run stampa `injected env (0) from .env.test`, ma **quel messaggio non distingue i due casi**:
dice `(0)` sia quando il file manca sia quando le variabili sono gia' state caricate da un primo
`dotenv` (la riga viene da `e2e/global-setup.ts`, che ricarica lo stesso file). Non usarlo come
diagnosi. L'unica verifica che risponde:

```bash
cd apps/web && pnpm exec dotenv -e .env.test -- node -e \
  'console.log("ADMIN_EMAIL:", process.env.ADMIN_EMAIL || "(ASSENTE)")'
```

Senza quelle variabili, `e2e/fixtures/api-client.ts` cade sul default `admin@meepleai.dev`, che nel
DB locale **non esiste** (c'e' `admin@meepleai.app`, TLD diverso). Verifica prima di accusare l'app:

```bash
# quale utente esiste davvero
docker exec meepleai-postgres psql -U meepleai -d meepleai_staging \
  -tAc 'select "Email", "Role" from users order by "Email"'

# una credenziale funziona? (200 = sì)
curl -s -o /dev/null -w '%{http_code}\n' -X POST http://localhost:8080/api/v1/auth/login \
  -H 'Content-Type: application/json' -d '{"email":"...","password":"..."}'
```

⚠️ Attenzione a `/api/v1/seed-e2e-users`: esiste ed e' il meccanismo previsto, ma
`RequireAdminSession()` lo protegge — **serve gia' una sessione admin per creare l'admin**. Anche
`POST /api/v1/auth/register` e' chiuso quando la registrazione e' a invito (403
«Registration is currently unavailable»), e il toggle sta nell'admin UI: lo stesso anello.

Via d'uscita **additiva**, senza indovinare l'algoritmo di hashing: il formato e'
`v1.600000.<salt>.<hash>` con salt casuale letto dalla stringa in verifica, e l'email non entra
nell'hash. Quindi si puo' creare un utente nuovo **copiando in-database** l'hash di un utente che
funziona — quel nuovo utente condividera' la stessa password, e nessun segreto passa dalla shell:

```sql
insert into users ("Id","Email","DisplayName","PasswordHash","Role","Tier","CreatedAt",
                   "IsDemoAccount","IsSuspended","Status","EmailVerified","IsContributor",
                   "OnboardingCompleted","OnboardingSkipped")
select gen_random_uuid(), 'e2e-admin@meepleai.test', 'E2E Admin (solo locale)',
       "PasswordHash", 'admin', 'free', now(), false, false, 'Active', true, false, true, false
from users where "Email" = '<un utente la cui password conosci>';
```

Reversibile con `delete from users where "Email" = 'e2e-admin@meepleai.test';`.

#### Step 1d 🔴 — Due configurazioni coerenti, e la suite le mischia

È la conclusione che spiega la maggior parte dei fallimenti locali. Il bypass fa rendere la
**pagina**, ma le chiamate API partono dal browser verso il backend **vero**, che non conosce il
token finto e risponde 401: la shell autenticata appare e il contenuto che dipende dai dati no.
Il sintomo e' un `waitForSelector` che scade su una pagina apparentemente corretta.

| configurazione | cosa serve | esempio nel repo |
|---|---|---|
| **A — tutto finto** | bypass + cookie + `page.route()` per **ogni** endpoint che la pagina usa | `e2e/admin/catalog-seed.spec.ts` |
| **B — login vero** | credenziali reali, sessione reale, **nessun** bypass | `e2e/audit/` (ha `playwright.audit.config.ts` proprio) |

Gli spec che seminano il cookie ma **non** mockano le API stanno in mezzo, e in locale non possono
passare: non e' ne' A ne' B. Prima di indagare un selettore, stabilisci in quale configurazione lo
spec vive — e se ci vive davvero.

#### Step 2: Configure Secrets

```bash
cd infra/secrets

# Copy example secrets
for file in *.secret.example; do cp "$file" "${file%.example}"; done

# Edit secrets with your values
nano admin.secret        # Admin credentials
nano openrouter.secret   # OpenRouter API key (for LLM tests)
nano database.secret     # Database credentials
```

**Required Secrets for E2E**:
- `admin.secret`: Admin user credentials
- `openrouter.secret`: `OPENROUTER_API_KEY` for LLM tests
- `database.secret`: PostgreSQL connection details

#### Step 3: Start API

```bash
cd apps/api/src/Api
dotnet run
```

**Verify API Running**:
```bash
# Health check
curl http://localhost:8080/health

# Swagger UI
open http://localhost:8080/scalar/v1
```

#### Step 4: Run E2E Tests

```bash
cd apps/api/tests/Api.Tests
dotnet test --filter "Category=E2E"
```

**Examples**:
- RAG accuracy validation (`FirstAccuracyBaselineTest`)
- AI agent integration tests
- Full workflow tests (chat → RAG → response)

---

## Environment Variables

### Required for E2E Tests
```bash
# OpenRouter (LLM provider)
OPENROUTER_API_KEY=sk-or-v1-xxxxx

# Database
POSTGRES_HOST=localhost
POSTGRES_PORT=5432
POSTGRES_DB=meepleai
POSTGRES_USER=meepleai
POSTGRES_PASSWORD=***

# Qdrant (Vector DB)
QDRANT_URL=http://localhost:PostgreSQL :5432

# Redis (Cache)
REDIS_URL=localhost:6379
```

### Optional (with Defaults)
```bash
# Embedding
EMBEDDING_PROVIDER=ollama
EMBEDDING_MODEL=nomic-embed-text

# API Base URL
API_BASE_URL=http://localhost:8080
```

---

## Troubleshooting

### ❌ "API not available at http://localhost:8080"

**Symptoms**:
```
❌ API not running
Prerequisites:
  1. Start API: cd apps/api/src/Api && dotnet run
  2. Ensure services: docker compose up postgres qdrant redis
```

**Solutions**:
1. **Check API Process**:
   ```bash
   # Windows
   netstat -ano | findstr :8080

   # Linux/Mac
   lsof -i :8080
   ```

2. **Restart API**:
   ```bash
   cd apps/api/src/Api
   dotnet run --launch-profile "Development"
   ```

3. **Check Logs**: Look for startup errors in console output.

---

### ❌ "PostgreSQL container failed to start"

**Symptoms**:
```
System.InvalidOperationException: PostgreSQL container failed to start after 3 attempts
```

**Solutions**:
1. **Check Docker**:
   ```bash
   docker ps -a | grep postgres
   ```

2. **Kill Conflicting Process**:
   ```bash
   # Windows
   netstat -ano | findstr :5432
   taskkill /PID <PID> /F

   # Linux/Mac
   lsof -i :5432
   kill -9 <PID>
   ```

3. **Restart Containers**:
   ```bash
   docker compose down -v  # Remove volumes
   docker compose up -d postgres
   ```

---

### ❌ "pgvector table not found"

**Symptoms**:
```
Collection 'game_rules' not found
```

**Solutions**:
1. **Check Qdrant**:
   ```bash
   curl http://localhost:PostgreSQL :5432/collections
   ```

2. **Restart Qdrant**:
   ```bash
   docker compose restart qdrant
   ```

3. **Reinitialize Collection**: Restart API (auto-creates collection on startup).

---

### ❌ "OpenRouter API key not configured"

**Symptoms**:
```
InvalidOperationException: OPENROUTER_API_KEY is required
```

**Solutions**:
1. **Get API Key**: https://openrouter.ai/keys
2. **Configure Secret**:
   ```bash
   echo "OPENROUTER_API_KEY=sk-or-v1-xxxxx" > infra/secrets/openrouter.secret
   ```
3. **Restart API** to load new secret.

---

### ❌ "789 test failures with NpgsqlConnectionStringBuilder"

**Symptoms**:
```
System.ArgumentException: Couldn't set connection timeout
Parameter name: connection timeout
```

**Status**: ✅ **FIXED** in commit `6228a1877` (2026-01-16)

**Solution**: Already resolved in `main-dev`. Pull latest changes:
```bash
git pull origin main-dev
```

---

## Test Execution Strategies

### Strategy 1: Fast Feedback (Unit Only)
```bash
dotnet test --filter "Category=Unit" --verbosity minimal
```

**Duration**: 2-3 minutes
**Use Case**: Quick validation during development

---

### Strategy 2: Pre-Commit Validation (Unit + Integration)
```bash
dotnet test --filter "Category!=E2E" --logger "console;verbosity=minimal"
```

**Duration**: 15-20 minutes
**Use Case**: Before creating PR, local CI simulation

---

### Strategy 3: Full Coverage (All Tests)
```bash
# Start infra first!
docker compose up -d && cd apps/api/src/Api && dotnet run &

# Then run all tests
dotnet test --logger "trx;LogFileName=test-results.trx"
```

**Duration**: 25-35 minutes
**Use Case**: Pre-release validation, comprehensive coverage

---

### Strategy 4: Specific Bounded Context
```bash
# GameManagement tests only
dotnet test --filter "BoundedContext=GameManagement"

# SharedGameCatalog tests only
dotnet test --filter "BoundedContext=SharedGameCatalog"
```

**Duration**: 1-5 minutes per context
**Use Case**: Focused testing after context-specific changes

---

## CI/CD Configuration

### Current GitHub Actions Setup

**backend-ci.yml** (runs on every PR):
```yaml
jobs:
  test:
    runs-on: ubuntu-latest
    steps:
      - name: Run Unit + Integration Tests
        run: dotnet test --filter "Category!=E2E"
```

**Why E2E Skipped in CI**:
- Requires long-running API process
- Depends on external API keys (OpenRouter)
- Takes 25-35 minutes (expensive for every PR)
- Better suited for nightly/release builds

### Recommended: Nightly E2E Workflow

Create `.github/workflows/nightly-e2e.yml`:
```yaml
name: Nightly E2E Tests

on:
  schedule:
    - cron: '0 2 * * *'  # 2 AM daily
  workflow_dispatch:

jobs:
  e2e-full:
    runs-on: ubuntu-latest
    services:
      postgres:
        image: postgres:16
        env:
          POSTGRES_USER: meepleai
          POSTGRES_PASSWORD: meepleai
      qdrant:
        image: qdrant/qdrant:latest
      redis:
        image: redis:7-alpine

    steps:
      - uses: actions/checkout@v4

      - name: Setup Secrets
        run: |
          mkdir -p infra/secrets
          echo "OPENROUTER_API_KEY=${{ secrets.OPENROUTER_API_KEY }}" > infra/secrets/openrouter.secret

      - name: Start API (Background)
        run: |
          cd apps/api/src/Api
          dotnet run &
          sleep 30

      - name: Run E2E Tests
        run: dotnet test --filter "Category=E2E"

      - name: Upload Results
        if: always()
        uses: actions/upload-artifact@v4
        with:
          name: e2e-test-results
          path: '**/TestResults/*.trx'
```

---

## Performance Benchmarks

| Test Type | Count | Avg Duration | Resources Used |
|-----------|-------|--------------|----------------|
| Unit (single) | 1 | 5-50ms | RAM only |
| Integration (single) | 1 | 100ms-2s | Docker container |
| E2E (single) | 1 | 500ms-5s | Full stack |
| Full Unit Suite | 3,500 | 2-3 min | ~500MB RAM |
| Full Integration Suite | 1,800 | 12-15 min | Docker + 2GB RAM |
| Full E2E Suite | 700 | 10-15 min | Full infra |

---

## Best Practices

### During Development
1. ✅ Run unit tests frequently (`dotnet test --filter Category=Unit`)
2. ✅ Run integration tests before committing
3. ❌ Don't run E2E tests on every change (too slow)

### Before PR Creation
1. ✅ Run full test suite locally (except E2E)
2. ✅ Verify CI checks will pass
3. ✅ Check test coverage if adding features

### Before Release
1. ✅ Run complete test suite (including E2E)
2. ✅ Validate on fresh database (reset Docker volumes)
3. ✅ Check all bounded contexts pass

---

## Test Isolation Principles

### Database Isolation (Integration Tests)
- Each test class gets unique database via `SharedTestcontainersFixture.CreateIsolatedDatabaseAsync()`
- Database dropped after test class completion
- No cross-contamination between test classes

### Example:
```csharp
public async ValueTask InitializeAsync()
{
    _dbName = $"test_{Guid.NewGuid():N}";
    var connString = await _fixture.CreateIsolatedDatabaseAsync(_dbName);
    // Use isolated database
}

public async ValueTask DisposeAsync()
{
    await _fixture.DropIsolatedDatabaseAsync(_dbName);
}
```

---

## Debugging Failed Tests

### Enable Verbose Logging
```bash
dotnet test --verbosity detailed --logger "console;verbosity=detailed"
```

### Run Single Test
```bash
dotnet test --filter "FullyQualifiedName~MyTest.MySpecificTestMethod"
```

### Attach Debugger
1. Open test file in IDE
2. Set breakpoint in test method
3. Right-click → Debug Test

### Check Test Output
- Test logs: `apps/api/tests/Api.Tests/TestResults/`
- Container logs: `docker compose logs postgres`
- API logs: Console output from `dotnet run`

---

## Known Issues

### Issue: 779 Test Failures (Pre-Fix)
**Status**: ✅ **RESOLVED** (commits `0c928386c`, `858109cf8`, `6228a1877`)

**History**:
- **Before**: 789 failures (PostgreSQL/in-memory mismatch + connection string typo)
- **Fix 1**: Corrected 11 integration test DbContext usage
- **Fix 2**: Fixed `SharedTestcontainersFixture` connection string (`Timeout=10`)
- **After**: 0 structural test failures

### Issue: E2E Tests Require Manual Setup
**Status**: ⚠️ **By Design**

**Rationale**: E2E tests validate full system integration, require:
- Running API (background process)
- External API keys (OpenRouter)
- Complete infrastructure stack

**Solution**: Follow setup steps in this guide or use automated scripts.

---

## Test Data

### Seed Data (Development)
- Auto-seeded by `AutoConfigurationService` on first run
- Admin user: From `infra/secrets/admin.secret`
- Test user: `Test@meepleai.com` / `Demo123!`
- AI models: 6 models (OpenRouter + Ollama)

### Test Fixtures
- User fixtures: `TestUserFactory`, `UserEntityBuilder`
- Game fixtures: `GameTestDataFactory`
- PDF fixtures: `PdfTestFixtureBuilder`

---

## Contact & Support

- **Documentation**: **docs/05-testing/** _(planned)_
- **Issues**: [GitHub Issues](https://github.com/meepleAi-app/meepleai-monorepo/issues)
- **Related Issue**: #2533 (E2E Test Documentation)

---

**Last Updated**: 2026-01-16
**Maintained By**: MeepleAI Development Team
