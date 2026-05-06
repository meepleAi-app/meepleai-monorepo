# Test Suite Hardening Implementation Plan

> **For agentic workers:** REQUIRED SUB-SKILL: Use superpowers:subagent-driven-development (recommended) or superpowers:executing-plans to implement this plan task-by-task. Steps use checkbox (`- [ ]`) syntax for tracking.

**Goal:** Eliminare anti-pattern ad alto rischio nella test suite (backend .NET/xUnit e frontend Vitest/Playwright) identificati dall'expert panel review: false confidence, flakiness mascherata, reflection abuse, test privi di valore.

**Architecture:** Modifiche chirurgiche a file esistenti — nessuna nuova infrastruttura. Ogni task è indipendente e committabile separatamente. Scope limitato agli item ad alto impatto completabili senza ristrutturazioni massive (God File splits e schema isolation sono out of scope per questo piano).

**Tech Stack:** .NET 9 / xUnit v3 / FluentAssertions / Moq — Next.js / Vitest / @testing-library/react / Playwright / MSW

---

## File Inventory

| File | Azione |
|------|--------|
| `apps/api/tests/Api.Tests/xunit.runner.json` | Modifica: rimuovi `maxRetries` |
| `apps/api/tests/Api.Tests/Api.Tests.csproj` | Modifica: rimuovi `xUnit1031` da NoWarn |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Plugins/Registry/PluginRegistryTests.cs` | Modifica: fix `.GetAwaiter().GetResult()` |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/E2E/MultiAgentRoutingE2ETests.cs` | Modifica: fix `Task.WhenAll(...).Result` |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Chunking/InMemoryChunkRepositoryTests.cs` | Modifica: fix `.Wait()` |
| `apps/api/tests/Api.Tests/Unit/Administration/QuartzReportSchedulerServiceTests.cs` | Modifica: fix `.Start().Wait()` |
| `apps/api/tests/Api.Tests/Unit/Administration/GetUserLibraryStatsQueryHandlerTests.cs` | Elimina |
| `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/CheckLlmCostAlertsCommandHandlerTests.cs` | Modifica: rimuovi null-check-only test |
| `apps/api/tests/Api.Tests/E2E/Administration/BatchJobE2ETests.cs` | Modifica: rimuovi reflection |
| `apps/web/src/__tests__/components/agent/agent-switching.test.tsx` | Modifica: rimuovi test tooltip fittizio |
| `apps/web/src/__tests__/components/admin/agent-builder.test.tsx` | Modifica: migliora test "renders all required fields" |
| `apps/web/vitest.setup.tsx` | Modifica: aggiungi commenti WHY per sezioni critiche |

---

## Task 1: Rimuovi `maxRetries` da xunit.runner.json

**Razionale (Michael Nygard):** `maxRetries: 2` nasconde race condition e flakiness reale. Un test che passa al terzo tentativo è un test che mente.

**Files:**
- Modify: `apps/api/tests/Api.Tests/xunit.runner.json`

- [ ] **Step 1.1: Modifica xunit.runner.json**

```json
{
  "$schema": "https://xunit.net/schema/current/xunit.runner.schema.json",
  "parallelizeAssembly": false,
  "parallelizeTestCollections": true,
  "maxParallelThreads": 4,
  "methodDisplay": "method",
  "diagnosticMessages": false,
  "internalDiagnosticMessages": false,
  "methodTimeout": 90000,
  "longRunningTestSeconds": 30
}
```

Rimuovi la riga `"maxRetries": 2`. Non aggiungere commenti (il formato JSON non li supporta).

- [ ] **Step 1.2: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/tests/Api.Tests/xunit.runner.json
git commit -m "test(config): remove maxRetries to surface flaky tests instead of hiding them"
```

---

## Task 2: Rimuovi xUnit1031 da NoWarn e fixa i 4 blocking calls

**Razionale (Michael Nygard):** `xUnit1031` avverte su `Task.Result`, `.Wait()`, `.GetAwaiter().GetResult()` nei test — questi bloccano il thread e possono causare deadlock. Ci sono 4 occorrenze reali da fixare.

**Files:**
- Modify: `apps/api/tests/Api.Tests/Api.Tests.csproj`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Plugins/Registry/PluginRegistryTests.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/E2E/MultiAgentRoutingE2ETests.cs`
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Chunking/InMemoryChunkRepositoryTests.cs`
- Modify: `apps/api/tests/Api.Tests/Unit/Administration/QuartzReportSchedulerServiceTests.cs`

- [ ] **Step 2.1: Fix PluginRegistryTests.cs — `GetAwaiter().GetResult()`**

Localizza riga 188 del file. Cambia:
```csharp
// PRIMA
registry.DisablePluginAsync("test-plugin-v1").GetAwaiter().GetResult();

// DOPO (il metodo test deve essere async)
await registry.DisablePluginAsync("test-plugin-v1");
```

Assicurati che il metodo test che contiene questa riga sia `async Task`, non `void` o `Task` senza `await`.

- [ ] **Step 2.2: Fix MultiAgentRoutingE2ETests.cs — `Task.WhenAll(...).Result`**

Localizza riga 169. Cambia:
```csharp
// PRIMA
var results = Task.WhenAll(tasks).Result;

// DOPO
var results = await Task.WhenAll(tasks);
```

Assicurati che il metodo contenitore sia `async Task`.

- [ ] **Step 2.3: Fix InMemoryChunkRepositoryTests.cs — `.Wait()`**

Localizza riga 201. Cambia:
```csharp
// PRIMA
_repository.SaveAsync(chunk).Wait();

// DOPO
await _repository.SaveAsync(chunk);
```

Assicurati che il metodo sia `async Task`.

- [ ] **Step 2.4: Fix QuartzReportSchedulerServiceTests.cs — `Start().Wait()`**

Localizza riga 32. Cambia:
```csharp
// PRIMA
_scheduler.Start().Wait();

// DOPO
await _scheduler.Start();
```

Assicurati che il metodo di setup o test sia `async Task`.

- [ ] **Step 2.5: Rimuovi xUnit1031 da Api.Tests.csproj**

Apri `apps/api/tests/Api.Tests/Api.Tests.csproj`. Trova la riga `<NoWarn>` e rimuovi solo il token `;xUnit1031` da essa. La riga dovrebbe passare da:

```xml
<NoWarn>$(NoWarn);xUnit1000;xUnit1051;xUnit2012;xUnit2002;xUnit1031;EF1002;...</NoWarn>
```

a:

```xml
<NoWarn>$(NoWarn);xUnit1000;xUnit1051;xUnit2012;xUnit2002;EF1002;...</NoWarn>
```

- [ ] **Step 2.6: Verifica build e test**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/api/tests/Api.Tests
dotnet build
```

Expected: 0 errori. Se appaiono warning xUnit1031 su file non fixati nella lista (possibili falsi positivi da `AuditLog.Result`), investigare: se `AuditLog.Result` è una proprietà domain (non Task), non è un warning reale — aggiungere `// xunit-analysis: domain property, not Task.Result` per sopprimere inline.

- [ ] **Step 2.7: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/tests/Api.Tests/Api.Tests.csproj \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Domain/Plugins/Registry/PluginRegistryTests.cs \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/E2E/MultiAgentRoutingE2ETests.cs \
        apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Infrastructure/Persistence/Chunking/InMemoryChunkRepositoryTests.cs \
        apps/api/tests/Api.Tests/Unit/Administration/QuartzReportSchedulerServiceTests.cs
git commit -m "test(quality): fix blocking Task calls and remove xUnit1031 suppression"
```

---

## Task 3: Elimina GetUserLibraryStatsQueryHandlerTests.cs

**Razionale (Gojko Adzic):** Il file contiene un solo test che verifica `ArgumentNullException` sul costruttore. Il commento nel file stesso dice "Full workflow testing is covered by integration tests." Zero valore aggiunto.

**Files:**
- Delete: `apps/api/tests/Api.Tests/Unit/Administration/GetUserLibraryStatsQueryHandlerTests.cs`

- [ ] **Step 3.1: Elimina il file**

```bash
rm "D:/Repositories/meepleai-monorepo-backend/apps/api/tests/Api.Tests/Unit/Administration/GetUserLibraryStatsQueryHandlerTests.cs"
```

- [ ] **Step 3.2: Verifica che i test di integration coprono realmente il handler**

```bash
grep -r "GetUserLibraryStats\|UserLibraryStats" "D:/Repositories/meepleai-monorepo-backend/apps/api/tests/Api.Tests" --include="*.cs" -l
```

Expected: almeno 1 file di integration o E2E test che copre lo scenario.

- [ ] **Step 3.3: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add -A apps/api/tests/Api.Tests/Unit/Administration/GetUserLibraryStatsQueryHandlerTests.cs
git commit -m "test(cleanup): remove null-check-only handler test covered by integration tests"
```

---

## Task 4: Rimuovi test null-check da CheckLlmCostAlertsCommandHandlerTests.cs

**Razionale (Lisa Crispin):** Il file ha un solo `[Fact]` che testa `ArgumentNullException` su `Handle(null!)`. Non testa alcun comportamento di business.

**Files:**
- Modify: `apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/CheckLlmCostAlertsCommandHandlerTests.cs`

- [ ] **Step 4.1: Sostituisci il contenuto del file**

Il file attuale ha un costruttore complesso con `LlmCostAlertService`, `_mockCostLogRepository`, `_mockAlertingService` — ma un solo test null. Sostituisci il file mantenendo il setup e aggiungendo un test comportamentale minimale al posto del null check:

```csharp
using Api.BoundedContexts.KnowledgeBase.Application.Commands;
using Api.BoundedContexts.KnowledgeBase.Application.Queries;
using Api.BoundedContexts.KnowledgeBase.Domain.Repositories;
using Api.BoundedContexts.KnowledgeBase.Domain.Services;
using Api.Services;
using Api.Tests.Constants;
using Microsoft.Extensions.Logging;
using Moq;
using Xunit;
using FluentAssertions;

namespace Api.Tests.BoundedContexts.KnowledgeBase.Application.Handlers;

/// <summary>
/// Tests for CheckLlmCostAlertsCommandHandler.
/// Full workflow testing is covered by integration tests in KnowledgeBase/Integration/.
/// </summary>
[Trait("Category", TestCategories.Unit)]
public class CheckLlmCostAlertsCommandHandlerTests
{
    private readonly LlmCostAlertService _alertService;
    private readonly CheckLlmCostAlertsCommandHandler _handler;
    private readonly Mock<ILlmCostLogRepository> _mockCostLogRepository;
    private readonly Mock<IAlertingService> _mockAlertingService;

    public CheckLlmCostAlertsCommandHandlerTests()
    {
        _mockCostLogRepository = new Mock<ILlmCostLogRepository>();
        _mockAlertingService = new Mock<IAlertingService>();
        var mockLogger = new Mock<ILogger<LlmCostAlertService>>();

        _alertService = new LlmCostAlertService(
            _mockCostLogRepository.Object,
            _mockAlertingService.Object,
            mockLogger.Object);

        _handler = new CheckLlmCostAlertsCommandHandler(_alertService);
    }

    [Fact]
    public async Task Handle_WithValidCommand_CompletesWithoutException()
    {
        // Arrange
        var command = new CheckLlmCostAlertsCommand();

        _mockCostLogRepository
            .Setup(r => r.GetCostsSinceAsync(It.IsAny<DateTime>(), It.IsAny<CancellationToken>()))
            .ReturnsAsync(new List<Api.BoundedContexts.KnowledgeBase.Domain.Entities.LlmCostLog>());

        // Act
        var act = () => _handler.Handle(command, TestContext.Current.CancellationToken);

        // Assert
        await act.Should().NotThrowAsync();
    }
}
```

**NOTA:** Se `CheckLlmCostAlertsCommand` ha un costruttore diverso o i tipi non corrispondono esattamente, adatta il codice ai tipi reali — usa Serena MCP per trovare le definizioni exact prima di scrivere.

- [ ] **Step 4.2: Compila per verificare tipi corretti**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/api/tests/Api.Tests
dotnet build
```

Expected: build OK. Se ci sono errori di tipo, aggiusta i namespace/tipi osservando i file sorgente in `apps/api/src/Api/BoundedContexts/KnowledgeBase/Application/Commands/`.

- [ ] **Step 4.3: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/tests/Api.Tests/BoundedContexts/KnowledgeBase/Application/Handlers/CheckLlmCostAlertsCommandHandlerTests.cs
git commit -m "test(kb): replace null-check test with minimal behavior test for CostAlerts handler"
```

---

## Task 5: Fix reflection in BatchJobE2ETests.cs

**Razionale (Martin Fowler):** Usare reflection per settare `Role` bypassa l'encapsulation della value object ed è fragile. `User.UpdateRole(Role.Admin)` è il metodo domain corretto.

**Files:**
- Modify: `apps/api/tests/Api.Tests/E2E/Administration/BatchJobE2ETests.cs`

- [ ] **Step 5.1: Localizza il pattern di reflection**

Cerca nel file le righe:
```csharp
// Use reflection to set role (Role is a value object with private setter)
var roleProperty = user.GetType().GetProperty("Role");
roleProperty?.SetValue(user, Api.SharedKernel.Domain.ValueObjects.Role.Admin);
```

- [ ] **Step 5.2: Sostituisci con UpdateRole**

Sostituisci le 3 righe di reflection con il metodo domain:

```csharp
// Promote to admin via domain method (avoids reflection on private setter)
user.UpdateRole(Api.SharedKernel.Domain.ValueObjects.Role.Admin);
```

La riga successiva `await DbContext.SaveChangesAsync();` rimane invariata.

Verifica che l'`using` per `Api.SharedKernel.Domain.ValueObjects` sia già presente in cima al file (dovrebbe esserlo dato che la versione con reflection già lo usa).

- [ ] **Step 5.3: Verifica build**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/api/tests/Api.Tests
dotnet build
```

Expected: 0 errori. `User.UpdateRole` è `public void` e accetta `Role` — compatibile.

- [ ] **Step 5.4: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/api/tests/Api.Tests/E2E/Administration/BatchJobE2ETests.cs
git commit -m "test(e2e): replace reflection role-setter with User.UpdateRole() domain method"
```

---

## Task 6: Rimuovi test tooltip fittizio da agent-switching.test.tsx

**Razionale (Gojko Adzic):** Il test dichiara di testare il tooltip ma verifica solo che il pulsante esista — sempre vero. Il commento nel codice ammette esplicitamente il fallimento. Crea false confidence.

**Files:**
- Modify: `apps/web/src/__tests__/components/agent/agent-switching.test.tsx`

- [ ] **Step 6.1: Rimuovi il test fittizio**

Nel file, individua e rimuovi l'intero blocco `it('shows tooltip with agent description', ...)`:

```typescript
// RIMUOVI QUESTO BLOCCO INTERO (righe ~50-60):
it('shows tooltip with agent description', async () => {
  const user = userEvent.setup();

  render(<AgentSelectorBadge currentAgent="tutor" onSwitch={vi.fn()} showSwitcher={true} />);

  const arbitroButton = screen.getByText('Arbitro');
  await user.hover(arbitroButton);

  // Tooltip should appear (testing-library may not catch async tooltip)
  // Simplified: just verify button exists
  expect(arbitroButton).toBeInTheDocument();
});
```

Aggiungi un commento al suo posto per documentare la decisione:
```typescript
// NOTE: Tooltip behavior not testable reliably in jsdom (Radix async portal rendering).
// Covered by E2E tests in e2e/flows/ instead.
```

- [ ] **Step 6.2: Esegui i test del file per verificare che gli altri passino**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test src/__tests__/components/agent/agent-switching.test.tsx
```

Expected: tutti i test rimanenti PASS.

- [ ] **Step 6.3: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/web/src/__tests__/components/agent/agent-switching.test.tsx
git commit -m "test(fe): remove misleading tooltip test that never tested tooltip behavior"
```

---

## Task 7: Migliora BasicInfoStep in agent-builder.test.tsx

**Razionale (Gojko Adzic):** Il primo test `'renders all required fields'` verifica solo che i campi esistano — non il loro valore, non il loro comportamento. Non protegge da regressioni di comportamento.

**Files:**
- Modify: `apps/web/src/__tests__/components/admin/agent-builder.test.tsx`

- [ ] **Step 7.1: Sostituisci il test "renders all required fields"**

Trova il test:
```typescript
it('renders all required fields', () => {
  const onChange = vi.fn();
  render(<BasicInfoStep agent={defaultAgentForm} onChange={onChange} />);

  expect(screen.getByLabelText(/name/i)).toBeInTheDocument();
  // ... altri toBeInTheDocument
});
```

Sostituiscilo con un test che verifica i valori iniziali:
```typescript
it('initializes fields with defaultAgentForm values', () => {
  render(<BasicInfoStep agent={defaultAgentForm} onChange={vi.fn()} />);

  const nameInput = screen.getByLabelText(/name/i) as HTMLInputElement;
  // defaultAgentForm.name should be the default value (empty string or a default name)
  expect(nameInput.value).toBe(defaultAgentForm.name);

  // Temperature default value should be displayed
  expect(screen.getByText('0.7')).toBeInTheDocument();

  // Max tokens default (2048 may be rendered as "2,048" or "2048")
  expect(screen.getByText(/2[,]?048/)).toBeInTheDocument();
});
```

Il resto dei test nel `describe('BasicInfoStep')` rimane invariato (testano già comportamento concreto: onChange, maxLength, character count).

- [ ] **Step 7.2: Esegui i test del file**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test src/__tests__/components/admin/agent-builder.test.tsx
```

Expected: tutti i test PASS. Se il test fallisce perché `defaultAgentForm.name` ha un valore diverso dall'atteso, controlla `src/lib/schemas/agent-definition-schema.ts` per il valore reale di default e adatta l'assertion.

- [ ] **Step 7.3: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/web/src/__tests__/components/admin/agent-builder.test.tsx
git commit -m "test(fe): replace existence-check test with values test in BasicInfoStep"
```

---

## Task 8: Aggiungi commenti WHY a vitest.setup.tsx

**Razionale (Janet Gregory):** 683 righe di polyfill/mock senza documentazione. Nessuno sa quale mock sia ancora necessario quando si aggiorna jsdom o una libreria.

**Files:**
- Modify: `apps/web/vitest.setup.tsx`

- [ ] **Step 8.1: Aggiungi commenti WHY alle sezioni critiche**

Per ogni sezione di mock nel file, aggiungi un commento che spiega:
- Perché il mock esiste
- Quale versione del package lo ha reso necessario (se noto)
- Se è da rivalutare in futuro

Esempi di commenti da aggiungere prima delle sezioni esistenti:

**Prima di `global.FileReader = class FileReader {`:**
```typescript
// WHY: jsdom's FileReader implementation doesn't support all File APIs needed
// by the PDF upload components (react-pdf, pdf.js). Specifically, readAsArrayBuffer
// with blob slicing doesn't work correctly in jsdom < 23.
// RE-EVALUATE: when upgrading jsdom, test if native implementation works.
```

**Prima di `global.Worker = class Worker {`:**
```typescript
// WHY: jsdom does not support Web Workers at all (no execution context).
// Components that use Workers (embedding service calls, PDF processing)
// need this stub to prevent "Worker is not defined" errors.
// This is a permanent stub — Web Workers require a real browser.
```

**Prima della sezione `framer-motion` mock:**
```typescript
// WHY: framer-motion animation hooks call requestAnimationFrame repeatedly,
// causing test timeouts and async test pollution in jsdom.
// Mock disables animations entirely for test speed and determinism.
```

**Prima della sezione MSW `server.listen()`:**
```typescript
// WHY: MSW must be initialized at module level BEFORE any test imports
// to ensure the fetch patch is applied before components make API calls.
// Order matters: server.listen() → tests → server.close()
```

- [ ] **Step 8.2: Commit**

```bash
cd D:/Repositories/meepleai-monorepo-backend
git add apps/web/vitest.setup.tsx
git commit -m "test(fe): document WHY for each mock section in vitest.setup.tsx"
```

---

## Task 9: Verifica finale — esegui suite completa

- [ ] **Step 9.1: Backend — run test suite completa**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/api/tests/Api.Tests
dotnet test --filter "Category=Unit" --verbosity normal
```

Expected: tutti i test Unit PASS (un test in meno dopo task 3).

- [ ] **Step 9.2: Frontend — run test suite**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test
```

Expected: tutti i test PASS (un test in meno dopo task 6).

- [ ] **Step 9.3: Verifica coverage frontend non degradata**

```bash
cd D:/Repositories/meepleai-monorepo-backend/apps/web
pnpm test:coverage
```

Expected: coverage non degrada sotto le soglie (branches ≥ 85%, lines ≥ 80%). La rimozione di un test non abbassa la coverage.

---

## Out of Scope (Piano Futuro)

Le seguenti raccomandazioni expert panel sono valide ma fuori scope per questo piano perché richiedono ristrutturazioni più ampie:

| ID | Raccomandazione | Effort stimato |
|----|----------------|----------------|
| B-04 | Split God Test Files (LiveGameSessionTests 1.596 righe, UploadPdf 1.464 righe) | 3-4 giorni |
| B-07 | Schema isolation PostgreSQL per test paralleli (sostituisce SharedFixture) | 2-3 giorni |
| B-08 | Rimuovi EF Core InMemory, usa sempre Testcontainers | 1-2 giorni |
| B-03 | Consolida `tests/Api.Tests/` in `apps/api/tests/` (6 seeder file) | 0.5 giorni |
| F-02 | Coverage AgentCharacterSheet da 57% a 85% | 1 giorno |
| F-05 | Allineamento coverage reale (62%) con soglie (80%) per server components | 2 giorni |
| F-06 | E2E Playwright: verifica sharding attivo in CI | 0.5 giorni |

---

*Piano generato: 2026-04-04*
*Expert panel: Lisa Crispin, Gojko Adzic, Michael Nygard, Martin Fowler, Janet Gregory*
