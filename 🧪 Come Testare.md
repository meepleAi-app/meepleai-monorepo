# 🧪 Guida Completa: Testing, Debugging e Diagnostica

**MeepleAI Development Guide** - Tutto quello che serve per testare, debuggare e diagnosticare problemi in Visual Studio Code.

---

## 📑 Indice

1. [Setup Iniziale](#-setup-iniziale)
2. [Debugging in VS Code](#-debugging-in-vs-code)
3. [Testing Frontend](#-testing-frontend)
4. [Testing Backend](#-testing-backend)
5. [API Testing con Postman](#-api-testing-con-postman)
6. [Log e Diagnostica](#-log-e-diagnostica)
7. [Troubleshooting Comune](#-troubleshooting-comune)
8. [Chrome DevTools](#-chrome-devtools)

---

## 🚀 Setup Iniziale

### Prerequisiti
```bash
# Verifica versioni
node --version      # v20+
pnpm --version      # v9+
dotnet --version    # 9.0+
docker --version    # 20+
```

### Avvio Ambiente Completo

**Opzione 1: Docker Compose (Recommended)**
```bash
# Dalla directory infra/
cd D:/Repositories/meepleai-monorepo-frontend/infra

# Inizializza secrets (solo prima volta)
bash tools/secrets/init-secrets.sh

# Avvia tutti i servizi
docker compose up -d

# Verifica health status
docker compose ps
```

**Opzione 2: Sviluppo Locale**
```bash
# Terminal 1: Backend solo (senza Docker)
cd apps/api/src/Api
dotnet run

# Terminal 2: Frontend solo
cd apps/web
pnpm dev

# Terminal 3: Servizi Docker essenziali
cd infra
docker compose up postgres qdrant redis seq -d
```

### Porte di Default

| Servizio | Porta | URL | Descrizione |
|----------|-------|-----|-------------|
| **Frontend** | 3000 | http://localhost:3000 | Next.js Web App |
| **Backend** | 8080 | http://localhost:8080 | ASP.NET Core API |
| **PostgreSQL** | 5432 | localhost:5432 | Database principale |
| **Qdrant** | 6333 | http://localhost:6333 | Vector database |
| **Redis** | 6379 | localhost:6379 | Cache e sessions |
| **Seq** | 8081 | http://localhost:8081 | Log aggregation UI |
| **Jaeger** | 16686 | http://localhost:16686 | Distributed tracing UI |
| **Prometheus** | 9090 | http://localhost:9090 | Metrics database |
| **Grafana** | 3001 | http://localhost:3001 | Dashboards e alerting |
| **n8n** | 5678 | http://localhost:5678 | Workflow automation |

---

## 🐛 Debugging in VS Code

### Frontend Debugging (Next.js + React)

#### 1. Debug Server-Side (Consigliato per API Routes e SSR)

**Configurazione**: `.vscode/launch.json` → "Next.js: debug server-side"

```bash
# In VS Code:
1. F5 → Seleziona "Next.js: debug server-side"
2. Apri http://localhost:3000 nel browser
3. Breakpoint funzioneranno per:
   - API routes (pages/api/*)
   - getServerSideProps
   - getStaticProps
   - Server components (App Router)
```

**Breakpoints**:
```typescript
// apps/web/src/pages/api/auth/login.ts
export default async function handler(req, res) {
  debugger; // ← Breakpoint automatico
  const { email, password } = req.body;
  // VS Code si fermerà qui
}
```

#### 2. Debug Client-Side (React Components)

**Configurazione**: `.vscode/launch.json` → "Next.js: debug client-side"

```bash
# In VS Code:
1. F5 → Seleziona "Next.js: debug client-side"
2. VS Code aprirà Chrome automaticamente
3. Breakpoint funzioneranno per:
   - React components
   - Event handlers
   - useEffect hooks
   - Client-side logic
```

**Breakpoints**:
```typescript
// apps/web/src/components/GameSelector.tsx
export function GameSelector() {
  const handleSelect = (gameId: string) => {
    debugger; // ← Breakpoint
    console.log('Selected:', gameId);
  };

  return <button onClick={() => handleSelect('123')}>Select</button>;
}
```

#### 3. Debug Full Stack (Server + Client)

**Configurazione**: `.vscode/launch.json` → "Next.js: debug full stack"

```bash
# Debugga sia server che client contemporaneamente
F5 → "Next.js: debug full stack"
```

**Variabili Ispezionabili**:
- Hover su variabili per vedere valori
- Watch panel: Aggiungi espressioni da monitorare
- Call Stack: Vedi chiamate di funzione
- Breakpoints panel: Gestisci tutti i breakpoint

---

### Backend Debugging (ASP.NET Core)

#### Setup Launch Configuration

Crea `.vscode/launch.json` per backend:

```json
{
  "version": "0.2.0",
  "configurations": [
    {
      "name": ".NET API: Launch",
      "type": "coreclr",
      "request": "launch",
      "preLaunchTask": "build-api",
      "program": "${workspaceFolder}/apps/api/src/Api/bin/Debug/net9.0/Api.dll",
      "args": [],
      "cwd": "${workspaceFolder}/apps/api/src/Api",
      "stopAtEntry": false,
      "env": {
        "ASPNETCORE_ENVIRONMENT": "Development"
      },
      "sourceFileMap": {
        "/Views": "${workspaceFolder}/Views"
      }
    },
    {
      "name": ".NET API: Attach",
      "type": "coreclr",
      "request": "attach"
    }
  ]
}
```

#### Aggiungere Task di Build

Crea `.vscode/tasks.json`:

```json
{
  "version": "2.0.0",
  "tasks": [
    {
      "label": "build-api",
      "command": "dotnet",
      "type": "process",
      "args": [
        "build",
        "${workspaceFolder}/apps/api/src/Api/Api.csproj",
        "/property:GenerateFullPaths=true",
        "/consoleloggerparameters:NoSummary"
      ],
      "problemMatcher": "$msCompile"
    },
    {
      "label": "watch-api",
      "command": "dotnet",
      "type": "process",
      "args": [
        "watch",
        "run",
        "--project",
        "${workspaceFolder}/apps/api/src/Api/Api.csproj"
      ],
      "problemMatcher": "$msCompile",
      "isBackground": true
    },
    {
      "label": "test-api",
      "command": "dotnet",
      "type": "process",
      "args": [
        "test",
        "${workspaceFolder}/apps/api/tests/Api.Tests/Api.Tests.csproj"
      ],
      "problemMatcher": "$msCompile",
      "group": {
        "kind": "test",
        "isDefault": true
      }
    }
  ]
}
```

#### Debug Backend

```bash
# Metodo 1: Launch con F5
1. Apri file C# (es. apps/api/src/Api/Program.cs)
2. F5 → Seleziona ".NET API: Launch"
3. Breakpoint su linee C#

# Metodo 2: Attach a processo esistente
1. Avvia API manualmente: dotnet run
2. F5 → ".NET API: Attach"
3. Seleziona processo "Api.dll"

# Metodo 3: Watch mode (auto-reload)
Ctrl+Shift+P → Run Task → watch-api
```

**Breakpoints Esempio**:
```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Application/Handlers/LoginCommandHandler.cs
public async Task<Result<LoginResponse>> Handle(
    LoginCommand request,
    CancellationToken cancellationToken)
{
    // ← Breakpoint qui
    _logger.LogInformation("Login attempt for {Email}", request.Email);

    var user = await _userRepository.GetByEmailAsync(request.Email);
    // ← Breakpoint qui per vedere user

    return user is null
        ? Result<LoginResponse>.Failure("Invalid credentials")
        : Result<LoginResponse>.Success(new LoginResponse(user.Id));
}
```

#### Conditional Breakpoints

```bash
# Click destro su breakpoint → Edit Breakpoint
# Esempi:
- request.Email == "admin@meepleai.dev"
- user.Role == "Admin"
- result.IsSuccess == false
```

---

## 🧪 Testing Frontend

### Struttura Test

```
apps/web/
├── __tests__/           # Test organizzati per tipo
│   ├── components/      # Component tests
│   ├── hooks/           # Custom hooks tests
│   ├── pages/           # Page tests
│   └── utils/           # Utility tests
├── jest.config.js       # Jest configuration
└── jest.setup.js        # Test environment setup
```

### Comandi Test

```bash
cd apps/web

# Run tutti i test
pnpm test

# Watch mode (auto-rerun su modifiche)
pnpm test --watch

# Test specifico file
pnpm test AccessibleButton

# Coverage report
pnpm test --coverage

# Test con verbose output
pnpm test --verbose

# Test solo file modificati
pnpm test --onlyChanged
```

### Task VS Code

```bash
# Ctrl+Shift+P → Tasks: Run Task
- test: Run all tests
- test --watch: Watch mode
- test --coverage: Coverage report
```

### Esempio Test Component

```typescript
// apps/web/__tests__/components/AccessibleButton.test.tsx
import { render, screen, fireEvent } from '@testing-library/react';
import { AccessibleButton } from '@/components/accessible/AccessibleButton';

describe('AccessibleButton', () => {
  it('should render with correct label', () => {
    render(<AccessibleButton>Click me</AccessibleButton>);
    expect(screen.getByRole('button', { name: /click me/i })).toBeInTheDocument();
  });

  it('should handle click events', () => {
    const handleClick = jest.fn();
    render(<AccessibleButton onClick={handleClick}>Click</AccessibleButton>);

    fireEvent.click(screen.getByRole('button'));
    expect(handleClick).toHaveBeenCalledTimes(1);
  });

  it('should be disabled when loading', () => {
    render(<AccessibleButton isLoading>Submit</AccessibleButton>);
    expect(screen.getByRole('button')).toBeDisabled();
  });
});
```

### Test Accessibilità (WCAG 2.1 AA)

```typescript
import { axe, toHaveNoViolations } from 'jest-axe';
expect.extend(toHaveNoViolations);

it('should have no accessibility violations', async () => {
  const { container } = render(<AccessibleFormInput label="Email" />);
  const results = await axe(container);
  expect(results).toHaveNoViolations();
});
```

---

## 🔧 Testing Backend

### Struttura Test

```
apps/api/tests/Api.Tests/
├── BoundedContexts/           # Test per bounded contexts
│   ├── Authentication/
│   ├── GameManagement/
│   └── KnowledgeBase/
├── Integration/               # Integration tests
│   ├── HealthChecks/
│   └── DatabaseTests/
└── UnitTests/                 # Unit tests
```

### Comandi Test

```bash
cd apps/api

# Run tutti i test
dotnet test

# Test con verbose output
dotnet test --logger "console;verbosity=detailed"

# Test specifico namespace
dotnet test --filter "FullyQualifiedName~Authentication"

# Test con coverage
dotnet test /p:CollectCoverage=true /p:CoverageReportsGenerator=html

# Test solo failed dall'ultimo run
dotnet test --filter "TestCategory=Failed"
```

### Task VS Code per Backend

Aggiungi a `.vscode/tasks.json`:

```json
{
  "label": "test-all",
  "command": "dotnet",
  "type": "process",
  "args": ["test", "--logger", "console;verbosity=normal"],
  "options": {
    "cwd": "${workspaceFolder}/apps/api"
  },
  "problemMatcher": "$msCompile"
}
```

### Esempio Integration Test (Testcontainers)

```csharp
// apps/api/tests/Api.Tests/Integration/AuthenticationTests.cs
public class LoginIntegrationTests : IAsyncLifetime
{
    private readonly PostgreSqlContainer _postgres;
    private readonly QdrantContainer _qdrant;
    private WebApplicationFactory<Program> _factory;

    public async Task InitializeAsync()
    {
        await _postgres.StartAsync();
        await _qdrant.StartAsync();

        _factory = new WebApplicationFactory<Program>()
            .WithWebHostBuilder(builder =>
            {
                builder.ConfigureTestServices(services =>
                {
                    // Override PostgreSQL connection
                    services.AddDbContext<AppDbContext>(options =>
                        options.UseNpgsql(_postgres.GetConnectionString()));
                });
            });
    }

    [Fact]
    public async Task Login_WithValidCredentials_ReturnsOk()
    {
        // Arrange
        var client = _factory.CreateClient();
        var request = new LoginRequest("admin@meepleai.dev", "Demo123!");

        // Act
        var response = await client.PostAsJsonAsync("/api/v1/auth/login", request);

        // Assert
        response.StatusCode.Should().Be(HttpStatusCode.OK);
        var result = await response.Content.ReadFromJsonAsync<LoginResponse>();
        result.Should().NotBeNull();
        result!.SessionId.Should().NotBeEmpty();
    }
}
```

---

## 📮 API Testing con Postman

### Setup Postman Collection

1. **Importa Collection**
   ```bash
   # Crea file: postman/MeepleAI.postman_collection.json
   # Importa in Postman: File → Import
   ```

2. **Environment Setup**

   **Development Environment**:
   ```json
   {
     "name": "MeepleAI Development",
     "values": [
       { "key": "base_url", "value": "http://localhost:8080", "enabled": true },
       { "key": "web_url", "value": "http://localhost:3000", "enabled": true },
       { "key": "api_key", "value": "", "enabled": true },
       { "key": "session_cookie", "value": "", "enabled": false }
     ]
   }
   ```

   **Production Environment**:
   ```json
   {
     "name": "MeepleAI Production",
     "values": [
       { "key": "base_url", "value": "https://api.meepleai.dev", "enabled": true },
       { "key": "api_key", "value": "{{prod_api_key}}", "enabled": true }
     ]
   }
   ```

### Collection Structure

```
MeepleAI API/
├── 🔐 Authentication/
│   ├── Register
│   ├── Login (Cookie)
│   ├── Login (API Key)
│   ├── Logout
│   └── Refresh Session
├── 🎮 Games/
│   ├── List Games
│   ├── Get Game Details
│   ├── Create Game
│   └── Update Game
├── 💬 Chat & RAG/
│   ├── Ask Question
│   ├── Search (Hybrid)
│   └── Get Chat History
├── 📄 Documents/
│   ├── Upload PDF
│   ├── Get Processing Status
│   └── List Documents
└── 🏥 Health & Monitoring/
    ├── Health Check
    ├── Readiness Check
    └── Metrics
```

### Request Examples

#### 1. Authentication - Login

**POST** `{{base_url}}/api/v1/auth/login`

Headers:
```
Content-Type: application/json
```

Body:
```json
{
  "email": "admin@meepleai.dev",
  "password": "Demo123!"
}
```

Tests Script:
```javascript
// Salva session cookie
pm.test("Status is 200", function () {
    pm.response.to.have.status(200);
});

pm.test("Response has sessionId", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.sessionId).to.be.a('string');
    pm.environment.set("session_id", jsonData.sessionId);
});

// Estrai cookie
var cookieJar = pm.cookies.jar();
cookieJar.get("MeepleAI-Session", function (error, cookie) {
    if (cookie) {
        pm.environment.set("session_cookie", cookie.value);
    }
});
```

#### 2. Games - List All Games

**GET** `{{base_url}}/api/v1/games`

Headers:
```
Authorization: Bearer {{api_key}}
# OPPURE usa Cookie: MeepleAI-Session={{session_cookie}}
```

Tests Script:
```javascript
pm.test("Status is 200", function () {
    pm.response.to.have.status(200);
});

pm.test("Response is array", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData).to.be.an('array');
});

pm.test("Each game has required fields", function () {
    var games = pm.response.json();
    games.forEach(game => {
        pm.expect(game).to.have.property('id');
        pm.expect(game).to.have.property('title');
        pm.expect(game).to.have.property('minPlayers');
    });
});
```

#### 3. Chat - Ask Question (Streaming)

**POST** `{{base_url}}/api/v1/chat`

Headers:
```
Content-Type: application/json
Accept: text/event-stream
Authorization: Bearer {{api_key}}
```

Body:
```json
{
  "gameId": "550e8400-e29b-41d4-a716-446655440000",
  "question": "Come si gioca a Catan?",
  "threadId": null
}
```

**Note**: Streaming SSE non supportato direttamente in Postman. Usa:
- **cURL** per testare streaming
- **Postman** per testare response finale
- **Browser EventSource** per test real-time

#### 4. Documents - Upload PDF

**POST** `{{base_url}}/api/v1/documents/upload`

Headers:
```
Authorization: Bearer {{api_key}}
```

Body (form-data):
```
file: [Select PDF file]
gameId: 550e8400-e29b-41d4-a716-446655440000
language: ita
```

Pre-request Script:
```javascript
// Generate unique filename
const timestamp = new Date().getTime();
pm.environment.set("upload_timestamp", timestamp);
```

Tests Script:
```javascript
pm.test("Status is 200", function () {
    pm.response.to.have.status(200);
});

pm.test("Response has documentId", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.documentId).to.be.a('string');
    pm.environment.set("document_id", jsonData.documentId);
});

pm.test("Quality score is valid", function () {
    var jsonData = pm.response.json();
    pm.expect(jsonData.qualityScore).to.be.within(0, 1);
});
```

### Pre-request Scripts Globali

```javascript
// Collection-level Pre-request Script
// Auto-refresh session se scaduta

const sessionExpiry = pm.environment.get("session_expiry");
const now = Date.now();

if (sessionExpiry && now > sessionExpiry) {
    console.log("Session expired, refreshing...");

    pm.sendRequest({
        url: pm.environment.get("base_url") + "/api/v1/auth/refresh",
        method: 'POST',
        header: {
            'Content-Type': 'application/json'
        }
    }, function (err, response) {
        if (!err && response.code === 200) {
            const newExpiry = Date.now() + (7 * 24 * 60 * 60 * 1000); // 7 days
            pm.environment.set("session_expiry", newExpiry);
        }
    });
}
```

### Automated Testing

**Collection Runner**:
```bash
1. Collections → MeepleAI API → Run
2. Select Environment: Development
3. Iterations: 1
4. Delay: 100ms
5. Run MeepleAI API
```

**Newman CLI** (per CI/CD):
```bash
# Install Newman
npm install -g newman

# Run collection
newman run postman/MeepleAI.postman_collection.json \
  -e postman/Development.postman_environment.json \
  --reporters cli,json \
  --reporter-json-export results.json

# Con data file per test multipli
newman run postman/MeepleAI.postman_collection.json \
  -d postman/test-data.json \
  -e postman/Development.postman_environment.json
```

---

## 📊 Log e Diagnostica

### Log Locations

| Componente | Tipo Log | Location | Formato |
|------------|----------|----------|---------|
| **Frontend** | Console | Browser DevTools | Plain text |
| **Frontend** | File | `.next/server/*.log` | JSON |
| **Backend** | Seq | http://localhost:8081 | Structured JSON |
| **Backend** | Console | Terminal output | Plain text |
| **Docker** | Container logs | `docker logs <container>` | Plain text |
| **Postgres** | Database logs | Container `/var/log/postgresql` | Plain text |
| **Qdrant** | Vector DB logs | Container stdout | JSON |
| **n8n** | Workflow logs | http://localhost:5678/executions | JSON |
| **Jaeger** | Traces | http://localhost:16686 | OpenTelemetry |
| **Prometheus** | Metrics | http://localhost:9090 | Time-series |

---

### Frontend Logs

#### Browser Console (Chrome DevTools)

```bash
# Apri DevTools
F12 o Ctrl+Shift+I

# Console panel
- Errors: Red
- Warnings: Yellow
- Info: Blue
- Debug: Gray

# Filtra per severity
- All levels
- Errors only
- Warnings only
- Info only

# Search logs
Ctrl+F → cerca testo

# Preserve log (mantieni tra navigazioni)
☑ Preserve log
```

**Console API Examples**:
```typescript
// apps/web/src/components/GameSelector.tsx
console.log('User selected game:', gameId);
console.error('Failed to load game:', error);
console.warn('Game not found, using fallback');
console.debug('API response:', response);

// Group logs
console.group('Authentication Flow');
console.log('Step 1: Validate credentials');
console.log('Step 2: Create session');
console.groupEnd();

// Performance timing
console.time('API Call');
await fetch('/api/games');
console.timeEnd('API Call'); // → API Call: 234.5ms
```

#### Next.js Server Logs

```bash
# Tail logs in real-time
tail -f apps/web/.next/server/*.log

# Search for errors
grep -i "error" apps/web/.next/server/*.log

# Last 100 lines
tail -n 100 apps/web/.next/server/app.log
```

**Log Format**:
```json
{
  "timestamp": "2025-01-12T10:30:45.123Z",
  "level": "error",
  "message": "API request failed",
  "error": {
    "message": "Network timeout",
    "stack": "Error: Network timeout\n    at fetch..."
  },
  "context": {
    "userId": "123",
    "route": "/api/games"
  }
}
```

---

### Backend Logs (Seq)

#### Accesso Seq UI

```bash
# Apri browser
http://localhost:8081

# Login (Development mode)
# No authentication required

# Interfaccia
- Events: Lista eventi log
- Signals: Alert configurati
- Dashboards: Visualizzazioni custom
- Settings: Configurazione
```

#### Query Logs

**Query Language (Seq)**:

```sql
-- Tutti gli errori nelle ultime 24h
Level = 'Error' and @Timestamp > Now() - 24h

-- Login attempts per email
RequestPath like '/api/v1/auth/login%' and Email is not null

-- Slow queries (>1s)
Elapsed > 1000 and RequestPath like '/api%'

-- Specifico utente
UserId = '550e8400-e29b-41d4-a716-446655440000'

-- Errori con stack trace
Exception is not null

-- RAG quality sotto threshold
QualityScore < 0.70 and @MessageTemplate like '%RAG%'

-- Eventi da specifico bounded context
SourceContext like '%Authentication%'
```

**Saved Queries**:
```bash
# Crea query salvate per pattern comuni
1. Click "Save" dopo aver scritto query
2. Nome: "Failed Logins"
3. Query: Level = 'Error' and RequestPath = '/api/v1/auth/login'
4. Pin to dashboard
```

#### Structured Logging

**Configurazione** (`appsettings.Development.json`):
```json
{
  "Serilog": {
    "MinimumLevel": {
      "Default": "Information",
      "Override": {
        "Microsoft.AspNetCore": "Warning",
        "System": "Warning",
        "Microsoft.EntityFrameworkCore": "Information"
      }
    },
    "WriteTo": [
      {
        "Name": "Console"
      },
      {
        "Name": "Seq",
        "Args": {
          "serverUrl": "http://localhost:5341",
          "apiKey": null
        }
      }
    ],
    "Enrich": ["FromLogContext", "WithMachineName", "WithThreadId"]
  }
}
```

**Log Examples in Code**:
```csharp
// apps/api/src/Api/BoundedContexts/Authentication/Application/Handlers/LoginCommandHandler.cs

// Structured logging con properties
_logger.LogInformation(
    "User login attempt for {Email} from {IpAddress}",
    request.Email,
    httpContext.Connection.RemoteIpAddress
);

// Error con exception
try
{
    await _userRepository.SaveAsync(user);
}
catch (Exception ex)
{
    _logger.LogError(
        ex,
        "Failed to save user {UserId} during registration",
        user.Id
    );
    throw;
}

// Performance monitoring
using (_logger.BeginScope(new Dictionary<string, object>
{
    ["GameId"] = gameId,
    ["UserId"] = userId
}))
{
    var sw = Stopwatch.StartNew();
    var result = await _ragService.AskQuestionAsync(question);
    sw.Stop();

    _logger.LogInformation(
        "RAG query completed in {ElapsedMs}ms with confidence {Confidence}",
        sw.ElapsedMilliseconds,
        result.Confidence
    );
}
```

---

### Docker Logs

#### Container Logs

```bash
# Lista containers
docker ps

# Logs di un container
docker logs meepleai-api

# Follow logs (real-time)
docker logs -f meepleai-api

# Ultime 100 righe
docker logs --tail 100 meepleai-api

# Logs con timestamp
docker logs -t meepleai-api

# Logs da timestamp specifico
docker logs --since "2025-01-12T10:00:00" meepleai-api

# Logs tra due timestamp
docker logs --since "2025-01-12T10:00:00" --until "2025-01-12T11:00:00" meepleai-api
```

#### Docker Compose Logs

```bash
cd infra/

# Logs di tutti i servizi
docker compose logs

# Follow mode
docker compose logs -f

# Specifico servizio
docker compose logs -f api

# Multipli servizi
docker compose logs -f api postgres seq

# Con timestamp
docker compose logs -f -t api
```

#### Export Logs

```bash
# Salva logs su file
docker logs meepleai-api > api-logs.txt 2>&1

# Con Docker Compose
docker compose logs api > api-logs.txt 2>&1

# Comprimi logs
docker logs meepleai-api 2>&1 | gzip > api-logs-$(date +%Y%m%d).gz
```

---

### Database Diagnostica

#### PostgreSQL Logs

```bash
# Accesso container
docker exec -it meepleai-postgres bash

# Logs PostgreSQL
cat /var/log/postgresql/postgresql-*.log

# Query attive
psql -U meeple -d meepleai -c "SELECT * FROM pg_stat_activity WHERE state = 'active';"

# Slow queries
psql -U meeple -d meepleai -c "SELECT query, calls, total_time, mean_time FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;"
```

#### Qdrant Diagnostica

```bash
# Health check
curl http://localhost:6333/healthz

# Collections info
curl http://localhost:6333/collections

# Collection details
curl http://localhost:6333/collections/game-rules

# Cluster info
curl http://localhost:6333/cluster

# Logs
docker logs meepleai-qdrant
```

---

### Distributed Tracing (Jaeger)

#### Accesso Jaeger UI

```bash
# Apri browser
http://localhost:16686

# Search traces
1. Service: meepleai-api
2. Operation: POST /api/v1/chat
3. Tags: http.status_code=200
4. Lookback: Last 1 hour
5. Limit Results: 20
6. Find Traces
```

#### Trace Analysis

**Trace Components**:
- **Span**: Singola operazione (es. DB query)
- **Trace**: Insieme di span per una richiesta
- **Tags**: Metadata (user_id, http_method, etc.)
- **Logs**: Eventi temporizzati in uno span

**Esempio Trace**:
```
POST /api/v1/chat (342ms total)
├─ Authentication (12ms)
├─ Vector Search (145ms)
│  ├─ Qdrant Query (120ms)
│  └─ RRF Fusion (25ms)
├─ LLM Generation (180ms)
│  ├─ OpenRouter Request (175ms)
│  └─ Response Processing (5ms)
└─ Response Formatting (5ms)
```

**Filter Examples**:
```bash
# Slow requests (>1s)
duration > 1s

# Errors
error=true

# Specific user
user_id=550e8400-e29b-41d4-a716-446655440000

# Specific operation
operation=RAG_Query
```

---

### Metrics (Prometheus + Grafana)

#### Prometheus Queries

```bash
# Apri Prometheus
http://localhost:9090

# Esempio query (PromQL)

# Request rate (req/sec)
rate(http_requests_total[5m])

# Error rate
rate(http_requests_total{status=~"5.."}[5m])

# 95th percentile latency
histogram_quantile(0.95, rate(http_request_duration_seconds_bucket[5m]))

# Memory usage
process_resident_memory_bytes

# CPU usage
rate(process_cpu_seconds_total[5m])
```

#### Grafana Dashboards

```bash
# Apri Grafana
http://localhost:3001

# Login
Username: admin
Password: (vedi infra/secrets/grafana-admin-password.txt)

# Dashboards disponibili
1. API Performance
2. Database Metrics
3. RAG Quality Metrics
4. System Health
```

---

## 🔍 Troubleshooting Comune

### Frontend Issues

#### 1. Port 3000 Already in Use

```bash
# Trova processo
lsof -ti:3000

# Kill processo (Linux/Mac)
lsof -ti:3000 | xargs kill -9

# Windows
netstat -ano | findstr :3000
taskkill /PID <PID> /F
```

#### 2. Module Not Found

```bash
# Reinstalla dependencies
cd apps/web
rm -rf node_modules .next
pnpm install
pnpm dev
```

#### 3. CORS Errors

```bash
# Verifica configurazione
# apps/api/src/Api/appsettings.Development.json
"Cors": {
  "AllowedOrigins": [
    "http://localhost:3000"  # ← Deve includere frontend URL
  ]
}

# Verifica fetch con credentials
fetch('http://localhost:8080/api/v1/games', {
  credentials: 'include'  # ← Necessario per cookies
})
```

#### 4. Hydration Errors

```bash
# Causa: Mismatch tra server e client render
# Soluzione: Usa dynamic import per componenti client-only

import dynamic from 'next/dynamic';

const ClientOnlyComponent = dynamic(
  () => import('./ClientComponent'),
  { ssr: false }
);
```

---

### Backend Issues

#### 1. Port 8080 Already in Use

```bash
# Linux/Mac
lsof -ti:8080 | xargs kill -9

# Windows
netstat -ano | findstr :8080
taskkill /PID <PID> /F

# Docker container
docker compose stop api
```

#### 2. Database Connection Failed

```bash
# Verifica PostgreSQL container
docker ps | grep postgres

# Se non running
docker compose up -d postgres

# Test connection
docker exec -it meepleai-postgres psql -U meeple -d meepleai

# Verifica logs
docker logs meepleai-postgres

# Verifica connection string
# apps/api/src/Api/appsettings.Development.json
"ConnectionStrings": {
  "Postgres": "Host=localhost;Port=5432;Database=meepleai;Username=meeple;Password=..."
}
```

#### 3. Migration Errors

```bash
# Rollback migration
cd apps/api/src/Api
dotnet ef database update <PreviousMigration>

# Drop database e ricrea
docker compose down postgres
docker volume rm infra_pgdata
docker compose up -d postgres

# Re-apply migrations (auto on startup)
dotnet run
```

#### 4. Qdrant Connection Failed

```bash
# Verifica container
docker ps | grep qdrant

# Start container
docker compose up -d qdrant

# Health check
curl http://localhost:6333/healthz

# Verifica configurazione
# apps/api/src/Api/appsettings.Development.json
"Qdrant": {
  "Url": "http://localhost:6333"
}
```

---

### Docker Issues

#### 1. Container Won't Start

```bash
# Verifica logs
docker logs <container_name>

# Verifica health status
docker inspect <container_name> | grep -A 10 Health

# Ricrea container
docker compose down <service>
docker compose up -d <service>

# Rebuild image
docker compose build --no-cache <service>
docker compose up -d <service>
```

#### 2. Volume Permissions

```bash
# Linux: Permessi volume
sudo chown -R $USER:$USER infra/volumes/

# Mac/Windows: Restart Docker Desktop
```

#### 3. Network Issues

```bash
# Ricrea network
docker compose down
docker network prune
docker compose up -d

# Verifica network
docker network ls
docker network inspect infra_meepleai
```

---

### Performance Issues

#### 1. Slow API Responses

```bash
# Check Seq logs per slow queries
Elapsed > 1000 and RequestPath like '/api%'

# Check Jaeger traces
http://localhost:16686 → Filter: duration > 1s

# Check database
docker exec -it meepleai-postgres psql -U meeple -d meepleai
SELECT * FROM pg_stat_statements ORDER BY mean_time DESC LIMIT 10;

# Check Redis cache
docker exec -it meepleai-redis redis-cli
INFO stats
```

#### 2. High Memory Usage

```bash
# Check container stats
docker stats

# Check specific container
docker stats meepleai-api

# Prometheus query
http://localhost:9090
Query: container_memory_usage_bytes{name="meepleai-api"}

# Grafana dashboard
http://localhost:3001 → System Health → Memory Usage
```

#### 3. High CPU Usage

```bash
# Check processes
docker stats

# Prometheus query
rate(container_cpu_usage_seconds_total{name="meepleai-api"}[5m])

# Profile .NET app
dotnet trace collect --process-id <PID>
dotnet trace report trace.nettrace
```

---

## 🌐 Chrome DevTools

### Network Panel

```bash
# Apri DevTools → Network tab
F12 → Network

# Filtra richieste
- All
- Fetch/XHR (API calls)
- JS
- CSS
- Img

# Analizza richiesta
1. Click su richiesta
2. Headers: Request/Response headers
3. Preview: JSON formatted
4. Response: Raw response
5. Timing: Request timing breakdown

# Preserve log
☑ Preserve log (mantiene tra navigazioni)

# Disable cache
☑ Disable cache (forza fresh fetch)

# Throttling
No throttling → Slow 3G / Fast 3G / Offline
```

**Timing Breakdown**:
```
Request to localhost:8080/api/v1/games
├─ Queueing: 2ms
├─ Stalled: 5ms
├─ DNS Lookup: 0ms (cached)
├─ Initial connection: 3ms
├─ SSL: 0ms
├─ Request sent: 1ms
├─ Waiting (TTFB): 145ms  ← Server processing time
└─ Content Download: 8ms
Total: 164ms
```

---

### Application Panel

```bash
# Apri DevTools → Application tab
F12 → Application

# Storage inspection
├─ Local Storage
│  └─ http://localhost:3000
│     ├─ theme: "dark"
│     └─ user_preferences: {...}
├─ Session Storage
├─ Cookies
│  └─ MeepleAI-Session: "abc123..."
├─ IndexedDB
└─ Cache Storage

# Clear storage
Application → Clear storage → Clear site data
```

**Cookie Inspection**:
```bash
# View cookies
Application → Cookies → http://localhost:3000

# Cookie properties
Name: MeepleAI-Session
Value: eyJhbGciOiJIUzI1NiIs...
Domain: localhost
Path: /
Expires: 2025-01-19 (7 days)
HttpOnly: ✓ (not accessible via JS)
Secure: - (only on HTTPS)
SameSite: Lax
```

---

### Performance Panel

```bash
# Apri DevTools → Performance tab
F12 → Performance

# Record session
1. Click Record (or Ctrl+E)
2. Interact with page
3. Stop recording

# Analyze
├─ Summary: Scripting, Rendering, Painting time
├─ Flame chart: Call stack timeline
├─ Bottom-Up: Functions by total time
└─ Call Tree: Hierarchical call graph

# Performance insights
- Long tasks (>50ms): Blocks main thread
- Layout shifts (CLS): Visual stability
- Large DOM: >1500 nodes
```

**Metrics**:
```
Performance Metrics:
├─ FCP (First Contentful Paint): 1.2s
├─ LCP (Largest Contentful Paint): 2.1s
├─ TBT (Total Blocking Time): 120ms
├─ CLS (Cumulative Layout Shift): 0.05
└─ TTI (Time to Interactive): 2.8s
```

---

### Console Debugging

```bash
# Apri Console
F12 → Console

# Shortcuts
Ctrl+L: Clear console
Ctrl+F: Search logs
Esc: Toggle console drawer

# Live Expressions
Click "Create live expression"
→ document.querySelectorAll('button').length
(updates in real-time)

# Copy object
copy(object)  # Copies JSON to clipboard

# Monitor function calls
monitor(fetch)  # Logs all fetch calls

# Debug breakpoints
debug(functionName)  # Breaks when function called
```

---

### Accessibility Panel (Lighthouse)

```bash
# Apri DevTools → Lighthouse tab
F12 → Lighthouse

# Audit configuration
☑ Performance
☑ Accessibility
☑ Best Practices
☑ SEO
Mode: Navigation / Timespan / Snapshot
Device: Mobile / Desktop

# Run audit
1. Analyze page load
2. Click "Generate report"

# Results
├─ Performance: 92/100
├─ Accessibility: 98/100  ← WCAG 2.1 AA target
├─ Best Practices: 95/100
└─ SEO: 100/100

# Fix issues
Accessibility → View issues
- Missing ARIA labels
- Insufficient contrast ratio
- Missing alt text
```

---

## 📚 Risorse Aggiuntive

### Documentazione

| Risorsa | Link |
|---------|------|
| **Architecture** | `docs/architecture/board-game-ai-architecture-overview.md` |
| **API Spec** | `docs/api/board-game-ai-api-specification.md` |
| **Testing Guide** | `docs/testing/test-writing-guide.md` |
| **Database Schema** | `docs/database-schema.md` |
| **Security** | `docs/SECURITY.md` |

### Tool Docs

| Tool | Official Docs |
|------|---------------|
| **VS Code Debugging** | https://code.visualstudio.com/docs/editor/debugging |
| **Next.js Debugging** | https://nextjs.org/docs/app/building-your-application/debugging |
| **ASP.NET Core Debugging** | https://learn.microsoft.com/en-us/aspnet/core/test/debug |
| **Chrome DevTools** | https://developer.chrome.com/docs/devtools/ |
| **Postman** | https://learning.postman.com/docs/ |
| **Seq** | https://docs.datalust.co/docs |
| **Jaeger** | https://www.jaegertracing.io/docs/ |
| **Prometheus** | https://prometheus.io/docs/ |
| **Grafana** | https://grafana.com/docs/ |

---

## ✅ Quick Start Checklist

### Per Iniziare a Debuggare

- [ ] **Docker services running**: `docker compose ps` (all healthy)
- [ ] **Frontend dev server**: `pnpm dev` (port 3000)
- [ ] **Backend dev server**: `dotnet run` (port 8080)
- [ ] **VS Code launch.json configurato**: Check `.vscode/launch.json`
- [ ] **Breakpoints impostati**: Click sulla riga per aggiungere breakpoint
- [ ] **Browser DevTools aperto**: F12 in Chrome
- [ ] **Seq UI accessibile**: http://localhost:8081
- [ ] **Postman collection importata**: Import collection JSON

### Per Test Completo

- [ ] **Unit tests passing**: `pnpm test` (frontend), `dotnet test` (backend)
- [ ] **Coverage >90%**: `pnpm test --coverage`
- [ ] **No accessibility violations**: `axe` tests passing
- [ ] **API endpoints testati**: Postman collection run
- [ ] **Logs puliti**: No errors in Seq
- [ ] **Performance OK**: Lighthouse score >90

---

**Versione**: 1.0
**Ultimo aggiornamento**: 2025-01-12
**Maintainer**: MeepleAI Team
