# TEST-06: Expand and Enhance Playwright E2E Test Coverage

**Status:** Proposed
**Created:** 2025-10-17
**Type:** Enhancement
**Priority:** Medium
**Effort:** M (1 week)
**Labels:** `enhancement`, `type:tech`, `priority:medium`, `effort:M`, `sprint:3-4`, `frontend`, `tests`, `e2e`

## Context

Following TEST-03's unit test improvements (86.76% coverage, 874 passing tests), we have a solid **foundation of E2E tests** with Playwright already in place:

- **Current Status:** 15 spec files, ~147 E2E tests
- **Infrastructure:** Playwright installed and configured
- **Coverage:** Auth, chat, streaming, setup, upload, admin, accessibility, editor, timeline, error handling
- **CI Integration:** Partial (only accessibility tests run in CI)

### Why Additional E2E Tests Are Needed

While unit tests (Jest + Testing Library) excel at component-level testing, they have limitations:

1. **Real Browser Behavior**: jsdom doesn't fully replicate browser APIs (window.location, localStorage, complex DOM interactions)
2. **Multi-Page Flows**: User journeys spanning multiple pages (login → upload → chat)
3. **Complex Interactions**: Modal flows, drag-and-drop, multi-step forms
4. **Integration Scenarios**: Frontend + Backend + Database working together
5. **Visual Regressions**: Layout, styling, responsive design issues

### Current Test Gaps

From analysis of existing tests and TEST-03, we identified scenarios that need better E2E coverage:

1. **Critical User Journeys**: End-to-end flows (registration → PDF upload → RAG indexing → chat)
2. **Error Scenarios**: Network failures, API errors, timeout handling
3. **Concurrent Users**: Multiple chats, race conditions, session conflicts
4. **Cross-Browser Testing**: Currently only Chromium in CI
5. **Visual Regression**: No screenshot comparison tests
6. **CI Coverage**: Only accessibility tests run in CI, not all E2E tests

## Goal

**Expand E2E test suite** to achieve **80% coverage of critical user journeys** and **integrate all E2E tests into CI pipeline**.

## Current E2E Test Inventory

### Existing Tests (15 files, ~147 tests)

| File | Tests | Coverage |
|------|-------|----------|
| `accessibility.spec.ts` | 13 | WCAG compliance, keyboard nav, screen readers |
| `authenticated.spec.ts` | 4 | Login, chat exchange, PDF wizard, logs filtering |
| `chat.spec.ts` | 2 | Auth requirement, navigation links |
| `chat-streaming.spec.ts` | 12 | Streaming UI, SSE, stop button, citations, state updates |
| `setup.spec.ts` | 20 | Setup wizard, game selection, step generation, progress tracking |
| `pdf-upload-journey.spec.ts` | 1 | PDF upload and verification |
| `admin.spec.ts` | 4 | Admin dashboard access and features |
| `editor.spec.ts` | 30 | RuleSpec editor functionality |
| `error-handling.spec.ts` | 17 | Error scenarios and recovery |
| `timeline.spec.ts` | 22 | Timeline visualization and interactions |
| `versions.spec.ts` | 5 | Version history and comparison |
| `n8n.spec.ts` | 2 | n8n integration pages |
| `home.spec.ts` | 4 | Landing page |
| `chess-registration.spec.ts` | 6 | Chess game registration flow |
| `ai04-qa-snippets.spec.ts` | 5 | Q&A with code snippets |

**Total:** ~147 tests

## Scope

### Phase 1: Fill Critical Gaps (Days 1-3)

#### 1. Complete Authentication Flow Tests (~6 new tests)
**File:** `e2e/auth-complete.spec.ts`

Current gap: Only basic login tested in `authenticated.spec.ts`

```typescript
test.describe('Complete Authentication Flow', () => {
  test('user can register new account with form validation');
  test('user cannot register with weak password');
  test('user sees helpful error for existing email');
  test('user can reset forgotten password');
  test('user session persists across page reloads');
  test('user session expires after inactivity and shows warning');
});
```

#### 2. End-to-End RAG Pipeline (~8 new tests)
**File:** `e2e/rag-pipeline-e2e.spec.ts`

Current gap: Upload and chat tested separately, not complete flow

```typescript
test.describe('Complete RAG Pipeline', () => {
  test('user uploads PDF → chunks generated → embeddings created → can query content');
  test('user uploads multiple PDFs for same game → all indexed correctly');
  test('user query retrieves relevant citations from correct PDF');
  test('user can verify chunk count after upload');
  test('user sees processing status updates in real-time');
  test('system handles PDF upload failure gracefully');
  test('system detects and rejects duplicate PDF uploads');
  test('user can delete PDF and verify chunks removed from index');
});
```

#### 3. Multi-Chat Session Management (~6 new tests)
**File:** `e2e/chat-sessions.spec.ts`

Current gap: Only single chat tested

```typescript
test.describe('Chat Session Management', () => {
  test('user creates multiple chats and switches between them');
  test('user sees correct message history when switching chats');
  test('user can delete old chat and verify removal');
  test('user can rename chat');
  test('concurrent requests to same chat handled correctly');
  test('chat history persists after logout/login');
});
```

#### 4. Complex Admin Workflows (~8 new tests)
**File:** `e2e/admin-workflows.spec.ts`

Current gap: Only basic admin dashboard tested

```typescript
test.describe('Admin Workflows', () => {
  test('admin views and exports system metrics');
  test('admin manages user roles (promote/demote)');
  test('admin deletes user and verifies cascade deletion');
  test('admin views AI request logs with filtering');
  test('admin monitors RAG performance metrics');
  test('admin revokes user session remotely');
  test('admin views and manages API keys');
  test('non-admin cannot access admin endpoints (403)');
});
```

### Phase 2: Edge Cases & Error Scenarios (Days 4-5)

#### 5. Network & Error Resilience (~10 new tests)
**File:** `e2e/network-resilience.spec.ts`

```typescript
test.describe('Network Resilience', () => {
  test('user retries failed chat request');
  test('user experiences API timeout → sees error → can retry');
  test('user loses connection during PDF upload → sees progress preserved');
  test('user sees offline indicator when network lost');
  test('user regains connection → UI updates automatically');
  test('user handles 500 server error gracefully');
  test('user handles 503 service unavailable with retry');
  test('user experiences rate limit → sees clear message');
  test('user handles stale session → redirected to login');
  test('user handles CORS error gracefully');
});
```

#### 6. Concurrent User Scenarios (~6 new tests)
**File:** `e2e/concurrent-users.spec.ts`

```typescript
test.describe('Concurrent Users', () => {
  test('two users query same game simultaneously → both get correct responses');
  test('two editors upload PDFs for different games → no conflicts');
  test('admin revokes user session while user is active → user sees logout');
  test('user opens multiple browser tabs → sessions synced');
  test('two users edit same RuleSpec → optimistic locking prevents conflicts');
  test('concurrent chat requests to same agent handled correctly');
});
```

### Phase 3: Visual Regression & Cross-Browser (Days 6-7)

#### 7. Visual Regression Tests (~10 new tests)
**File:** `e2e/visual-regression.spec.ts`

```typescript
test.describe('Visual Regression', () => {
  test('landing page visual consistency');
  test('chat interface layout stability');
  test('setup wizard step flow visual consistency');
  test('admin dashboard charts render correctly');
  test('PDF upload wizard steps visual consistency');
  test('modal dialogs render correctly');
  test('error messages display consistently');
  test('loading states visual consistency');
  test('citation tooltips render correctly');
  test('responsive design: mobile viewport (375x667)');
});
```

#### 8. Cross-Browser Compatibility (~8 new tests)
**File:** `e2e/cross-browser.spec.ts`

```typescript
test.describe('Cross-Browser Compatibility', () => {
  // Run critical flows on Chromium, Firefox, WebKit
  test('login flow works across browsers');
  test('chat streaming works across browsers');
  test('PDF upload works across browsers');
  test('setup wizard works across browsers');
  test('admin dashboard works across browsers');
  test('accessibility features work across browsers');
  test('responsive design works across browsers');
  test('WebSocket fallback works across browsers');
});
```

### Phase 4: CI Integration & Documentation (Day 7)

#### 9. Expand CI Pipeline
**File:** `.github/workflows/e2e-tests.yml`

Current: Only accessibility tests run in CI
Goal: All E2E tests run on every PR

```yaml
name: E2E Tests
on: [push, pull_request]
jobs:
  e2e-critical:
    name: E2E - Critical Flows
    runs-on: ubuntu-latest
    strategy:
      matrix:
        browser: [chromium, firefox]
    steps:
      - uses: actions/checkout@v3
      - uses: pnpm/action-setup@v4
      - name: Install dependencies
        run: pnpm install
      - name: Install Playwright
        run: pnpm exec playwright install --with-deps ${{ matrix.browser }}
      - name: Start services (API + DB + Qdrant)
        run: docker compose -f infra/docker-compose.yml up -d postgres qdrant redis api
      - name: Wait for services
        run: |
          timeout 60 bash -c 'until curl -f http://localhost:8080/health; do sleep 2; done'
      - name: Run E2E tests
        run: pnpm test:e2e --project=${{ matrix.browser }}
      - name: Upload artifacts
        if: failure()
        uses: actions/upload-artifact@v3
        with:
          name: playwright-report-${{ matrix.browser }}
          path: apps/web/playwright-report/
```sql
#### 10. Update Documentation
**File:** `docs/guide/e2e-testing-guide.md`

- Running E2E tests locally
- Writing new E2E tests (patterns, best practices)
- Debugging failed tests
- Visual regression workflow
- CI integration details
- Test data management strategy

## Acceptance Criteria

### Infrastructure
- [x] Playwright installed and configured (already done)
- [x] Test structure created (`e2e/` directory) (already done)
- [ ] Page object models created for common flows
- [ ] Test utilities and helpers expanded
- [ ] Visual regression baseline screenshots captured

### Test Coverage (New Tests: 62)
- [ ] Complete auth flow: 6 tests ✓
- [ ] RAG pipeline E2E: 8 tests ✓
- [ ] Multi-chat sessions: 6 tests ✓
- [ ] Admin workflows: 8 tests ✓
- [ ] Network resilience: 10 tests ✓
- [ ] Concurrent users: 6 tests ✓
- [ ] Visual regression: 10 tests ✓
- [ ] Cross-browser: 8 tests ✓

**New Total:** 147 (existing) + 62 (new) = **209 E2E tests**

### Quality
- [ ] Tests follow Page Object Model pattern
- [ ] Proper test isolation (database reset between tests)
- [ ] No flaky tests (≥95% pass rate over 10 runs)
- [ ] Clear test descriptions (BDD-style: "user can...")
- [ ] Comprehensive assertions
- [ ] Screenshot/video on failure

### Documentation
- [ ] E2E testing guide updated (`docs/guide/e2e-testing-guide.md`)
- [ ] Page object patterns documented
- [ ] Test data management strategy documented
- [ ] Visual regression workflow documented
- [ ] Troubleshooting guide updated

### CI/CD
- [ ] New E2E workflow added to GitHub Actions
- [ ] All E2E tests run on every PR (not just accessibility)
- [ ] Tests run on Chromium + Firefox
- [ ] Docker services start correctly in CI
- [ ] Test reports uploaded as artifacts
- [ ] Screenshots/videos uploaded on failure
- [ ] Execution time <15 minutes (parallel execution)

### Performance
- [ ] Tests run in parallel (4 workers)
- [ ] Database seeding optimized
- [ ] Unnecessary waits eliminated
- [ ] Smart wait strategies (networkidle, domcontentloaded)

## Definition of Done

- [ ] 62 new E2E tests implemented and passing
- [ ] All existing 147 tests still passing
- [ ] Page object models created for new flows
- [ ] Visual regression baselines captured
- [ ] Cross-browser tests passing (Chromium + Firefox)
- [ ] CI pipeline expanded to run all E2E tests
- [ ] CI execution time <15 minutes
- [ ] Documentation updated
- [ ] No flaky tests (≥95% pass rate)
- [ ] Test reports generated and uploaded as artifacts
- [ ] All tests follow Page Object Model pattern

## Related Issues

- Builds on TEST-03 (#394) unit test coverage
- Complements TEST-05 (#444) final coverage push
- Should be completed after TEST-02 (backend coverage)
- Expands existing E2E suite (not starting from scratch)

## Technical Notes

### Recommended Page Object Model
```typescript
// e2e/helpers/pages/ChatPage.ts
export class ChatPage {
  constructor(private page: Page) {}

  async selectGame(gameName: string) {
    await this.page.selectOption('#gameSelect', { label: gameName });
  }

  async selectAgent(agentName: string) {
    await this.page.selectOption('#agentSelect', { label: agentName });
  }

  async createNewChat() {
    await this.page.click('button:has-text("Nuova Chat")');
  }

  async sendMessage(message: string) {
    await this.page.fill('#message-input', message);
    await this.page.click('button:has-text("Invia")');
  }

  async waitForStreamingComplete() {
    await this.page.waitForSelector('button:has-text("Stop")', { state: 'hidden' });
  }

  async getLastMessage() {
    return await this.page.locator('li[aria-label="AI response"]').last().textContent();
  }
}
```

### Visual Regression Configuration
```typescript
// playwright.config.ts (add to existing config)
export default defineConfig({
  // ... existing config
  expect: {
    toHaveScreenshot: {
      maxDiffPixels: 100,
      threshold: 0.2,
    },
  },
});
```

### Test Data Management
```typescript
// e2e/fixtures/test-data.ts
export const testUsers = {
  admin: { email: 'admin@meepleai.dev', password: 'Demo123!' },
  editor: { email: 'editor@meepleai.dev', password: 'Demo123!' },
  user: { email: 'user@meepleai.dev', password: 'Demo123!' },
};

export const testGames = [
  { name: 'Tic-Tac-Toe', id: 'from-seed-data' },
  { name: 'Chess', id: 'from-seed-data' },
];
```

### Docker Services for CI
```yaml
# infra/docker-compose.e2e.yml
services:
  postgres:
    image: postgres:16-alpine
    environment:
      POSTGRES_USER: meeple
      POSTGRES_PASSWORD: meeplepass
      POSTGRES_DB: meepleai_test
    ports:
      - "5432:5432"

  qdrant:
    image: qdrant/qdrant:v1.12.4
    ports:
      - "6333:6333"

  redis:
    image: redis:7-alpine
    ports:
      - "6379:6379"
```

## Success Metrics

**Primary:**
- 62 new E2E tests implemented (total: 209)
- ≥95% pass rate across all tests
- <15 minutes execution time in CI
- All critical flows covered

**Secondary:**
- No flaky tests
- Clear page object patterns established
- Comprehensive documentation
- CI integration working for all tests (not just accessibility)
- Visual regression baseline established

## Estimated Effort

**Effort Size:** M (1 week)
- Days 1-3: Critical gap tests (28 tests)
- Days 4-5: Edge cases and concurrency (16 tests)
- Days 6-7: Visual regression, cross-browser, CI integration, documentation (18 tests)

**Priority:** Medium (should follow TEST-03, TEST-05, and issue #445)

## Implementation Strategy

### Phase 1: Setup (Day 1 morning)
1. Create page object models for common flows
2. Set up test data fixtures
3. Create utility helpers for authentication, mocking, etc.

### Phase 2: Critical Tests (Days 1-3)
1. Auth complete flow (6 tests)
2. RAG pipeline E2E (8 tests)
3. Multi-chat sessions (6 tests)
4. Admin workflows (8 tests)

### Phase 3: Resilience (Days 4-5)
1. Network error handling (10 tests)
2. Concurrent user scenarios (6 tests)

### Phase 4: Polish (Days 6-7)
1. Visual regression tests (10 tests)
2. Cross-browser tests (8 tests)
3. CI integration
4. Documentation

## Notes

- Existing tests are well-structured and follow good patterns
- Current CI only runs accessibility tests; we need to expand this
- The project already has demo users seeded (admin/editor/user@meepleai.dev)
- Playwright config exists but needs updates for visual regression and cross-browser
- Focus on reusing existing patterns from current E2E tests
