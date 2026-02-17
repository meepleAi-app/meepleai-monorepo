# 🧪 Test Plan: SharedGame Admin Workflows

**Date:** 2026-02-12
**Scope:** Validazione end-to-end dei due flussi di creazione SharedGame

---

## 🎯 Test Objectives

1. **Flusso 1 (Manuale):** Validare creazione SharedGame senza PDF
2. **Flusso 2 (con PDF):** Validare upload PDF → embedding → KB → agent → SharedGame
3. **Integration:** Verificare tutti i componenti lavorano insieme correttamente
4. **Performance:** Assicurare tempi di risposta accettabili
5. **Security:** Validare controlli di accesso admin/editor

---

## 📋 Test Matrix

### Test Levels
| Level | Tool | Coverage Target | Status |
|-------|------|----------------|--------|
| **Unit** | Vitest (FE) + xUnit (BE) | ≥90% BE, ≥85% FE | ⏳ Verificare |
| **Integration** | xUnit + Testcontainers | ≥85% BE | ⏳ Verificare |
| **E2E** | Playwright | Critical paths 100% | ❌ Da creare |
| **Performance** | k6 | Response time <30s | ⏳ Opzionale |

---

## 🔴 E2E Test Suite 1: Flusso Manuale (Playwright)

### Test ID: E2E-SGC-001
**File:** `apps/web/tests/e2e/admin/shared-game-manual-flow.spec.ts`

```typescript
describe('SharedGame Manual Creation Flow', () => {
  test('Admin can create SharedGame manually and view it', async ({ page }) => {
    // Setup
    await loginAsAdmin(page);

    // Step 1: Navigate to dashboard
    await page.goto('/admin');
    await expect(page.locator('h1')).toContainText('Admin Dashboard');

    // Step 2: Navigate to SharedGame management
    await page.click('text=Shared Games'); // Sidebar or card
    await expect(page).toHaveURL('/admin/shared-games');

    // Step 3: Click "Create Manual"
    await page.click('text=Nuovo Gioco'); // Or "Create Manual" button
    await expect(page).toHaveURL('/admin/shared-games/new');

    // Step 4: Fill manual creation form
    await page.fill('[name="title"]', 'Test Game Manual');
    await page.fill('[name="publisher"]', 'Test Publisher');
    await page.fill('[name="yearPublished"]', '2024');
    await page.fill('[name="minPlayers"]', '2');
    await page.fill('[name="maxPlayers"]', '4');
    await page.fill('[name="playingTimeMinutes"]', '60');
    await page.fill('[name="minAge"]', '12');
    await page.fill('[name="description"]', 'Test game description');

    // Step 5: Submit form
    await page.click('button[type="submit"]');

    // Step 6: Wait for creation and redirect
    await page.waitForURL(/\/admin\/shared-games\/[a-f0-9-]+/);

    // Step 7: Verify game is displayed in detail view
    await expect(page.locator('h1')).toContainText('Test Game Manual');
    await expect(page.locator('text=Test Publisher')).toBeVisible();
    await expect(page.locator('text=2-4 players')).toBeVisible();
    await expect(page.locator('text=60 min')).toBeVisible();

    // Step 8: Verify status is Draft
    await expect(page.locator('text=Draft')).toBeVisible();

    // Cleanup
    const gameId = page.url().split('/').pop();
    await deleteSharedGame(gameId);
  });

  test('Form validation prevents invalid data', async ({ page }) => {
    await loginAsAdmin(page);
    await page.goto('/admin/shared-games/new');

    // Try to submit empty form
    await page.click('button[type="submit"]');

    // Verify validation errors
    await expect(page.locator('text=Title is required')).toBeVisible();
    await expect(page.locator('text=Year is required')).toBeVisible();
  });

  test('Duplicate detection warns user', async ({ page }) => {
    // Create first game
    const gameId = await createSharedGameManually({
      title: 'Duplicate Test',
      publisher: 'Test Pub'
    });

    await loginAsAdmin(page);
    await page.goto('/admin/shared-games/new');

    // Try to create duplicate
    await page.fill('[name="title"]', 'Duplicate Test');
    await page.fill('[name="publisher"]', 'Test Pub');
    await page.click('button[type="submit"]');

    // Verify duplicate warning
    await expect(page.locator('text=already exists')).toBeVisible();

    // Cleanup
    await deleteSharedGame(gameId);
  });
});
```

**Priority:** P1 - High
**Estimate:** 1 giorno
**Issue:** ❌ **DA CREARE** → #4231

---

## 🔵 E2E Test Suite 2: Flusso PDF Completo (Playwright)

### Test ID: E2E-SGC-002
**File:** `apps/web/tests/e2e/admin/shared-game-pdf-complete-flow.spec.ts`

```typescript
describe('SharedGame PDF Upload with KB and Agent Flow', () => {
  test('Admin can create SharedGame via PDF wizard and link agent', async ({ page }) => {
    // Setup: Prepare test PDF
    const testPdfPath = './tests/fixtures/test-rulebook.pdf';

    // Step 1: Login and navigate
    await loginAsAdmin(page);
    await page.goto('/admin');

    // Step 2: Navigate to SharedGame management
    await page.click('text=Shared Games');
    await expect(page).toHaveURL('/admin/shared-games');

    // Step 3: Click "Upload PDF" to start wizard
    await page.click('text=Upload PDF'); // Or wizard entry point
    await expect(page).toHaveURL(/\/admin\/wizard/); // New wizard route

    // Step 4: Upload PDF (Wizard Step 1)
    await page.setInputFiles('input[type="file"]', testPdfPath);
    await page.click('button:has-text("Next")');

    // Step 5: Wait for metadata extraction (Wizard Step 2)
    await page.waitForSelector('text=Metadata Extracted', { timeout: 30000 });
    await expect(page.locator('[data-testid="extracted-title"]')).toBeVisible();

    // Verify extracted data displayed
    const extractedTitle = await page.locator('[data-testid="extracted-title"]').textContent();
    expect(extractedTitle).toBeTruthy();

    await page.click('button:has-text("Next")');

    // Step 6: BGG Match (Wizard Step 3)
    await page.fill('input[placeholder*="BGG"]', '12345'); // Manual BGG ID
    await page.click('button:has-text("Search")');
    await page.waitForSelector('text=BGG Data Loaded');
    await page.click('button:has-text("Next")');

    // Step 7: Enrich & Confirm (Wizard Step 4)
    await page.fill('[name="description"]', 'Final game description');
    await page.click('button:has-text("Create Game")');

    // Step 8: Wait for SharedGame creation
    await page.waitForURL(/\/admin\/shared-games\/[a-f0-9-]+/, { timeout: 5000 });
    const gameId = page.url().split('/').pop()!;

    // Step 9: Verify PDF embedding starts
    await expect(page.locator('text=Processing') || page.locator('text=Embedding')).toBeVisible();

    // Step 10: Monitor embedding progress (PdfIndexingStatus)
    await page.waitForSelector('[data-testid="indexing-status"]');
    const statusText = await page.locator('[data-testid="indexing-status"]').textContent();
    expect(['Processing', 'Embedding', 'Indexed']).toContain(statusText);

    // Optional: Wait for embedding completion (può richiedere minuti)
    // await page.waitForSelector('text=Indexed', { timeout: 120000 });

    // Step 11: Check KB Documents tab (#4229)
    await page.click('button[role="tab"]:has-text("Knowledge Base")');
    await expect(page.locator('[data-testid="kb-documents-list"]')).toBeVisible();

    // Verify at least one document listed
    const docCount = await page.locator('[data-testid="kb-document-item"]').count();
    expect(docCount).toBeGreaterThan(0);

    // Step 12: Create AI Agent (#4230)
    await page.click('button:has-text("Create Agent")');
    await page.waitForSelector('[data-testid="agent-builder-modal"]');

    // Fill agent form
    await page.fill('[name="agentName"]', `${extractedTitle} Arbitro`);
    await page.selectOption('[name="model"]', 'gpt-4');
    await page.click('button:has-text("Create and Link")');

    // Step 13: Verify agent linked
    await page.waitForSelector('[data-testid="linked-agent-card"]');
    await expect(page.locator('text=Agent linked successfully')).toBeVisible();

    // Step 14: Verify final SharedGame state
    await expect(page.locator('h1')).toContainText(extractedTitle || 'Test');
    await expect(page.locator('[data-testid="game-status"]')).toContainText('Draft');
    await expect(page.locator('[data-testid="linked-agent-card"]')).toBeVisible();
    await expect(page.locator('[data-testid="kb-documents-count"]')).toContainText(/\d+ document/);

    // Cleanup
    await deleteSharedGame(gameId);
  });

  test('Embedding progress updates in real-time', async ({ page }) => {
    // Create SharedGame with PDF
    const gameId = await createSharedGameViaPdfWizard(testPdfPath);

    await loginAsAdmin(page);
    await page.goto(`/admin/shared-games/${gameId}`);

    // Monitor status changes
    const statuses: string[] = [];

    page.on('console', msg => {
      if (msg.text().includes('SSE')) {
        console.log('SSE Event:', msg.text());
      }
    });

    // Wait and collect status changes
    for (let i = 0; i < 5; i++) {
      const status = await page.locator('[data-testid="indexing-status"]').textContent();
      if (status) statuses.push(status);
      await page.waitForTimeout(2000);
    }

    // Verify status progression
    expect(statuses).toContain('Processing');
    expect(statuses.length).toBeGreaterThan(1); // Status changed at least once

    await deleteSharedGame(gameId);
  });

  test('KB documents are visible after indexing', async ({ page }) => {
    // Prerequisites: Game with indexed PDF
    const gameId = await createAndWaitForIndexing(testPdfPath);

    await loginAsAdmin(page);
    await page.goto(`/admin/shared-games/${gameId}`);

    // Navigate to KB tab
    await page.click('button[role="tab"]:has-text("Knowledge Base")');

    // Verify documents listed
    await expect(page.locator('[data-testid="kb-document-item"]')).toHaveCount(1);
    await expect(page.locator('text=Indexed')).toBeVisible();
    await expect(page.locator('text=chunks')).toBeVisible();

    await deleteSharedGame(gameId);
  });
});
```

**Priority:** P0 - Critical (blocca validazione Flusso 2)
**Estimate:** 2 giorni
**Issue:** Incluso in #4168 (wizard E2E), ma serve test completo

---

## 🟢 Integration Test Suite (Backend)

### Test ID: INT-SGC-001
**File:** `apps/api/tests/Api.Tests/BoundedContexts/SharedGameCatalog/Integration/CompleteWorkflowIntegrationTests.cs`

```csharp
public class CompleteWorkflowIntegrationTests : IntegrationTestBase
{
    [Fact]
    public async Task Flusso1_ManualCreation_CreatesSharedGameSuccessfully()
    {
        // Arrange
        var command = new CreateSharedGameCommand(
            Title: "Integration Test Game",
            YearPublished: 2024,
            Description: "Test description",
            MinPlayers: 2,
            MaxPlayers: 4,
            PlayingTimeMinutes: 60,
            MinAge: 12,
            ComplexityRating: 2.5m,
            AverageRating: 7.5m,
            ImageUrl: "https://example.com/image.jpg",
            ThumbnailUrl: "https://example.com/thumb.jpg",
            Rules: null,
            CreatedBy: AdminUserId
        );

        // Act
        var gameId = await Mediator.Send(command);

        // Assert
        var game = await DbContext.SharedGames.FindAsync(gameId);
        game.Should().NotBeNull();
        game!.Title.Should().Be("Integration Test Game");
        game.Status.Should().Be(GameStatus.Draft);
        game.IsDeleted.Should().BeFalse();
    }

    [Fact]
    public async Task Flusso2_PdfWizard_CreatesSharedGameWithDocument()
    {
        // Arrange - Upload PDF
        var uploadCommand = new UploadPdfForGameExtractionCommand(
            File: CreateTestPdfFile(),
            UserId: AdminUserId
        );
        var uploadResult = await Mediator.Send(uploadCommand);
        uploadResult.Success.Should().BeTrue();

        // Act 1 - Extract metadata
        var extractQuery = new ExtractGameMetadataFromPdfQuery(
            FilePath: uploadResult.FilePath!,
            UserId: AdminUserId
        );
        var metadata = await Mediator.Send(extractQuery);

        // Assert metadata extraction
        metadata.Title.Should().NotBeNullOrEmpty();
        metadata.ConfidenceScore.Should().BeGreaterThan(0.5m);

        // Act 2 - Import game
        var importCommand = new ImportGameFromBggCommand(
            BggId: 12345,
            UserId: AdminUserId
        );
        var gameId = await Mediator.Send(importCommand);

        // Assert game created
        var game = await DbContext.SharedGames
            .Include(g => g.Documents)
            .FirstOrDefaultAsync(g => g.Id == gameId);

        game.Should().NotBeNull();
        game!.BggId.Should().Be(12345);
        game.Status.Should().Be(GameStatus.Draft);
    }

    [Fact]
    public async Task AgentLinking_LinksAgentToSharedGame()
    {
        // Arrange - Create SharedGame
        var gameId = await CreateTestSharedGame();

        // Arrange - Create Agent (assume AgentDefinition exists)
        var agentId = await CreateTestAgent("Test Arbitro");

        // Act - Link agent to game
        var linkCommand = new LinkAgentToSharedGameCommand(gameId, agentId, AdminUserId);
        await Mediator.Send(linkCommand);

        // Assert
        var game = await DbContext.SharedGames.FindAsync(gameId);
        game.Should().NotBeNull();
        game!.AgentDefinitionId.Should().Be(agentId);
    }

    [Fact]
    public async Task KbDocuments_AreVisibleAfterEmbedding()
    {
        // Arrange - Game with PDF and embedding complete
        var gameId = await CreateSharedGameWithIndexedPdf();

        // Act - Query agent documents
        var query = new GetAgentDocumentsQuery(AgentId: /* agent linked to game */);
        var result = await Mediator.Send(query);

        // Assert
        result.Should().NotBeNull();
        result!.Documents.Should().NotBeEmpty();
        result.Documents.First().Status.Should().Be("indexed");
    }
}
```

**Priority:** P1 - High
**Estimate:** 1.5 giorni
**Issue:** ❌ **DA CREARE** → #4232

---

## 🟡 Unit Test Checklist

### Backend (xUnit + FluentAssertions)

**Commands:**
- [x] CreateSharedGameCommandHandlerTests ✅ Esiste
- [ ] LinkAgentToSharedGameCommandHandlerTests ❌ Da creare (#4228)
- [ ] UnlinkAgentFromSharedGameCommandHandlerTests ❌ Da creare (#4228)

**Queries:**
- [x] GetSharedGameByIdQueryHandlerTests ✅ Esiste
- [x] GetAgentDocumentsQueryHandlerTests ✅ Esiste
- [ ] GetSharedGameWithAgentQueryHandlerTests ❓ Verificare se serve

**Validators:**
- [x] CreateSharedGameCommandValidatorTests ✅ Esiste
- [ ] LinkAgentToSharedGameCommandValidatorTests ❌ Da creare (#4228)

### Frontend (Vitest + Testing Library)

**Components:**
- [x] SharedGamesBlock.test.tsx ✅ Esiste
- [x] SharedGameDetailModal.test.tsx ✅ Esiste
- [ ] KbDocumentsList.test.tsx ❌ Da creare (#4229)
- [ ] LinkedAgentCard.test.tsx ❌ Da creare (#4230)
- [ ] AgentBuilderModal.test.tsx ❌ Da creare (#3809)

**Hooks:**
- [x] useSharedGamesQuery.test.tsx ✅ Esiste
- [ ] useKbDocuments.test.tsx ❌ Da creare (#4229)
- [ ] useAgentLinking.test.tsx ❌ Da creare (#4230)

---

## 🟣 Performance Test Suite (k6)

### Test ID: PERF-SGC-001
**File:** `tests/performance/shared-game-workflows.js`

```javascript
import http from 'k6/http';
import { check, sleep } from 'k6';
import { Rate } from 'k6/metrics';

const errorRate = new Rate('errors');

export const options = {
  stages: [
    { duration: '30s', target: 5 },   // Ramp up
    { duration: '1m', target: 10 },   // Steady state
    { duration: '30s', target: 0 },   // Ramp down
  ],
  thresholds: {
    'http_req_duration': ['p(95)<2000'],  // 95% < 2s
    'errors': ['rate<0.1'],               // Error rate < 10%
  },
};

export default function () {
  const BASE_URL = __ENV.API_BASE || 'http://localhost:8080';
  const token = __ENV.ADMIN_TOKEN;

  // Test 1: Manual creation performance
  const manualPayload = JSON.stringify({
    title: `Perf Test ${Date.now()}`,
    yearPublished: 2024,
    description: 'Performance test game',
    minPlayers: 2,
    maxPlayers: 4,
    playingTimeMinutes: 60,
    minAge: 12,
    imageUrl: 'https://example.com/img.jpg',
    thumbnailUrl: 'https://example.com/thumb.jpg',
    createdBy: 'admin-user-id'
  });

  const createRes = http.post(
    `${BASE_URL}/api/v1/admin/shared-games`,
    manualPayload,
    {
      headers: {
        'Content-Type': 'application/json',
        'Authorization': `Bearer ${token}`
      }
    }
  );

  check(createRes, {
    'manual creation status 201': (r) => r.status === 201,
    'manual creation < 2s': (r) => r.timings.duration < 2000,
  }) || errorRate.add(1);

  sleep(1);

  // Test 2: Get game by ID performance
  if (createRes.status === 201) {
    const gameId = createRes.json('id');

    const getRes = http.get(
      `${BASE_URL}/api/v1/admin/shared-games/${gameId}`,
      {
        headers: { 'Authorization': `Bearer ${token}` }
      }
    );

    check(getRes, {
      'get by id status 200': (r) => r.status === 200,
      'get by id < 500ms': (r) => r.timings.duration < 500,
    }) || errorRate.add(1);
  }

  sleep(1);
}
```

**Performance Targets:**
- Manual creation: <2s (p95)
- PDF upload: <5s for 10MB
- Metadata extraction: <30s
- Get by ID: <500ms
- Embedding: <2min for 50-page PDF

**Priority:** P2 - Medium (opzionale)
**Estimate:** 1 giorno
**Issue:** ⏳ Opzionale

---

## 🔧 Test Infrastructure Needed

### Fixtures & Utilities

**Test PDFs:**
```bash
tests/fixtures/
├── test-rulebook-simple.pdf      # 5 pages, high quality
├── test-rulebook-complex.pdf     # 50 pages, multi-language
├── test-rulebook-low-quality.pdf # Scanned, requires OCR
└── test-rulebook-invalid.pdf     # Corrupted for error testing
```

**Helper Functions:**
```typescript
// apps/web/tests/e2e/helpers/shared-game-helpers.ts

export async function loginAsAdmin(page: Page): Promise<void>
export async function createSharedGameManually(data: CreateGameData): Promise<string>
export async function createSharedGameViaPdfWizard(pdfPath: string): Promise<string>
export async function createAndWaitForIndexing(pdfPath: string): Promise<string>
export async function createTestAgent(name: string): Promise<string>
export async function deleteSharedGame(gameId: string): Promise<void>
export async function waitForEmbeddingComplete(gameId: string, timeout?: number): Promise<void>
```

---

## 📊 Test Execution Plan

### Phase 1: Existing Tests Validation
```bash
# Backend
cd apps/api/src/Api
dotnet test --filter "Category=Unit&BoundedContext=SharedGameCatalog"
dotnet test --filter "Category=Integration&BoundedContext=SharedGameCatalog"

# Frontend
cd apps/web
pnpm test src/components/admin/dashboard/shared-games-block.test.tsx
pnpm test src/components/shared-games/__tests__/
```

### Phase 2: Create Missing Tests
1. Create E2E test file for Flusso 1 (Issue #4231)
2. Extend #4168 for complete Flusso 2 E2E
3. Add integration tests (#4232)

### Phase 3: Full Test Suite Execution
```bash
# All tests together
pnpm test:all-workflows

# Expected results:
# - Flusso 1 E2E: Pass (5 scenarios)
# - Flusso 2 E2E: Pass (8 scenarios)
# - Integration: Pass (15 tests)
# - Coverage: ≥85% FE, ≥90% BE
```

---

## 🎯 Success Criteria

| Criterio | Target | Status |
|----------|--------|--------|
| **Flusso 1 E2E pass** | 100% | ❌ Test da creare |
| **Flusso 2 E2E pass** | 100% | ❌ Dopo Wave 1-2 |
| **Unit coverage** | ≥90% BE, ≥85% FE | ⏳ Verificare |
| **Integration coverage** | ≥85% BE | ⏳ Verificare |
| **Performance** | <30s PDF workflow | ⏳ Da misurare |
| **No regressions** | 0 breaking | ⏳ CI/CD |

---

## 📝 Issue da Creare per Testing

### Issue #4231: E2E Tests - Flusso Manuale
```yaml
Title: "[Testing] E2E Tests - SharedGame Manual Creation Flow"
Labels: tests, kind/test, priority: high, size:M
Description: Complete E2E test for manual SharedGame creation flow
Dependencies: None (usa UI già esistente)
Estimate: 1 giorno
```

### Issue #4232: Integration Tests - Complete Workflows
```yaml
Title: "[Testing] Integration Tests - SharedGame Complete Workflows"
Labels: tests, kind/test, priority: high, size:M, backend
Description: Backend integration tests for both creation flows
Dependencies: #4228 (agent linking backend)
Estimate: 1.5 giorni
```

---

**Total Testing Effort:** 4.5 giorni (E2E + Integration + Performance)

**Last Updated:** 2026-02-12
**Owner:** PM Agent - Quality Assurance
