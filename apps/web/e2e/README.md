# E2E Testing with Page Object Model

**Status**: POM Architecture Design Complete (2025-11-10)
**Coverage**: 58% → Target 80%+ by 2025-11-30
**Framework**: Playwright + TypeScript + Page Object Model

**📚 Best Practices Guide**: See [docs/05-testing/playwright-best-practices.md](../../../docs/05-testing/playwright-best-practices.md) for comprehensive Playwright patterns, configuration, and examples (Issue #2919).

---

## Quick Start

### Running Tests

```bash
# All E2E tests (UI)
pnpm test:e2e

# API Smoke Tests (Postman/Newman) - NEW! 🎉
pnpm test:api:smoke              # Fast smoke test (~2min)
pnpm test:e2e:api                # Playwright API tests

# Combined (API smoke → UI E2E) - Recommended for pre-commit
pnpm test:e2e:full

# Specific test file
pnpm test:e2e chat.spec.ts

# UI mode (debug tests visually)
pnpm test:e2e:ui

# Generate coverage report
pnpm test:e2e --coverage
```

> **NEW**: API testing integration with Postman/Newman! See [e2e/api/README.md](./api/README.md) for details.

### Writing New Tests

**Option 1: Use Existing Page Object**

```typescript
import { test } from './fixtures/auth';
import { ChatPage } from './pages/chat/ChatPage';

test('user asks question', async ({ userPage }) => {
  const chatPage = new ChatPage(userPage);
  await chatPage.goto();
  await chatPage.askQuestionAndWait('How do I castle in chess?');
  await chatPage.assertAnswerContains('king');
});
```

**Option 2: Create New Page Object**

See [Migration Guide](../../../docs/testing/pom-migration-guide.md) Step 4.

---

## Documentation

### Architecture & Design

- **[POM Architecture Design](../../../docs/testing/pom-architecture-design.md)** - Complete architecture overview, base classes, page objects, components, fixtures
- **[POM Implementation Summary](../../../docs/testing/pom-implementation-summary.md)** - Deliverables, roadmap, success metrics

### Development Guides

- **[Migration Guide](../../../docs/testing/pom-migration-guide.md)** - Step-by-step instructions for migrating existing tests to POM
- **[Coding Standards](../../../docs/testing/pom-coding-standards.md)** - Naming conventions, selector strategy, method patterns, wait strategies

### Reference

- **[TypeScript Interfaces](./types/pom-interfaces.ts)** - Type definitions for all page objects and components
- **[Playwright UI Mode Guide](./PLAYWRIGHT-UI-MODE-GUIDE.md)** - Visual debugging guide

---

## Directory Structure

```
apps/web/e2e/
├── pages/                  # Page Objects (NEW)
│   ├── base/
│   │   ├── BasePage.ts             # Base class for all pages
│   │   └── BaseModal.ts            # Base class for modals
│   ├── auth/
│   │   ├── AuthPage.ts             # Login, register, OAuth, 2FA
│   │   └── PasswordResetPage.ts    # Password reset flow
│   ├── chat/
│   │   ├── ChatPage.ts             # Chat interface
│   │   └── ChatMessage.ts          # Message component wrapper
│   ├── editor/
│   │   ├── EditorPage.ts           # RuleSpec editor
│   │   └── VersionHistoryPage.ts   # Version management
│   ├── game/
│   │   ├── GameListPage.ts         # Browse/search games
│   │   └── PdfUploadPage.ts        # PDF upload wizard
│   └── admin/
│       ├── UserManagementPage.ts   # User CRUD
│       └── AnalyticsPage.ts        # Analytics dashboard
├── components/             # Reusable Component Wrappers (NEW)
│   ├── Modal.ts                    # Generic modal interactions
│   ├── Table.ts                    # Table sorting/filtering/pagination
│   ├── Toast.ts                    # Toast notifications
│   ├── Form.ts                     # Form filling/validation
│   └── FileUpload.ts               # File upload component
├── fixtures/               # Test Fixtures
│   ├── auth.ts                     # Mock authentication (existing)
│   ├── data.ts                     # Test data factories (NEW)
│   ├── cleanup.ts                  # Cleanup utilities (NEW)
│   └── i18n.ts                     # i18n support (existing)
├── types/                  # TypeScript Interfaces (NEW)
│   └── pom-interfaces.ts           # Type definitions
└── *.spec.ts               # Test Files (migrate to use POMs)
```

---

## Page Objects Available

### Implemented ✅

- **AuthPage** (`pages/auth/AuthPage.ts`)
  - Login, registration, OAuth (Google/Discord/GitHub), 2FA, logout
  - Example: `await authPage.loginAndWait({ email: '...', password: '...' })`

- **ChatPage** (`pages/chat/ChatPage.ts`)
  - Ask questions, view answers, citations, like/dislike, export
  - Example: `await chatPage.askQuestionAndWait('How do I castle?')`

### To Be Implemented 🚧

- EditorPage
- GameListPage
- PdfUploadPage
- UserManagementPage
- AnalyticsPage
- ConfigurationPage

See [Architecture Design](../../../docs/testing/pom-architecture-design.md) for complete list.

---

## Component Objects Available

### To Be Implemented 🚧

- Modal - Generic modal interactions
- Table - Sorting, filtering, pagination
- Toast - Toast notification handling
- Form - Form filling and validation
- FileUpload - File upload component

See [Architecture Design](../../../docs/testing/pom-architecture-design.md) for specifications.

---

## Fixtures Available

### Existing ✅

- **Auth Fixtures** (`fixtures/auth.ts`)
  - `adminPage`, `editorPage`, `userPage`, `setupUserPage`
  - Mock authentication without real login
  - Example: `test('...', async ({ adminPage }) => { ... })`

- **i18n Support** (`fixtures/i18n.ts`)
  - `getTextMatcher()`, `t()`
  - Multilingual text matching

### To Be Created 🚧

- **Data Factories** (`fixtures/data.ts`)
  - `DataFactory.createUser()`, `DataFactory.createGame()`
  - Generate test data with sensible defaults

- **Cleanup Utilities** (`fixtures/cleanup.ts`)
  - `CleanupHelper.clearSession()`, `CleanupHelper.logout()`
  - Reset state between tests

---

## Common Patterns

### Authentication

```typescript
import { test } from './fixtures/auth';

// Use fixture (recommended)
test('admin action', async ({ adminPage }) => {
  const userPage = new UserManagementPage(adminPage);
  await userPage.goto();
  // ... test logic ...
});

// Manual auth (when needed)
test('login flow', async ({ page }) => {
  const authPage = new AuthPage(page);
  await authPage.goto();
  await authPage.loginAndWait({ email: 'admin@meepleai.dev', password: 'Demo123!' });
});
```

### Test Data Generation

```typescript
import { DataFactory } from './fixtures/data';

const newUser = DataFactory.createUser({
  email: 'specific@example.com',  // Override defaults
  role: 'Editor',
});

await userPage.createUser(newUser);
```

### Assertions

```typescript
// Page object assertions (preferred)
await chatPage.assertAnswerContains('chess rules');
await userPage.assertUserVisible('test@example.com');

// Direct Playwright assertions (when needed)
import { expect } from '@playwright/test';
await expect(page.getByText('Success')).toBeVisible();
```

---

## Testing Best Practices

### DO ✅

- Use page objects for all UI interactions
- Use fixtures for authentication and setup
- Use data factories for test data
- Write self-contained tests (no shared state)
- Use semantic method names (`createUser()`, not `clickButton()`)
- Prefer accessibility-first selectors (`getByRole`, `getByLabel`)
- Let Playwright auto-wait (avoid `waitForTimeout()`)

### DON'T ❌

- Use direct `page.locator()` or `page.getByRole()` in tests
- Use CSS selectors (`.btn-submit`, `#user-table`)
- Add arbitrary timeouts (`waitForTimeout(5000)`)
- Share state between tests (global variables)
- Duplicate auth/mock setup in multiple tests
- Mix abstraction levels (high-level and low-level in same test)

---

## When NOT to Use E2E Tests

E2E tests are **expensive** (slow, fragile, infra-coupled). Before writing one,
check whether the same assertion already lives at a cheaper layer. The test
pyramid for this monorepo:

```
       E2E (Playwright)        ← user journeys (login → wizard → session → finalize)
     Integration (Vitest+MSW)  ← hook + API contract validation
   Unit (Vitest)               ← components, hooks, utilities, pure functions
```

**Spec panel rule** (Crispin / Fowler / Beck consensus, 2026-04-09):

> If a test verifies the rendering of a single component or the behavior of a
> single hook, it belongs in **Vitest** (Q1), not Playwright (Q2/Q3).

### ❌ Don't write an E2E for these

| Anti-pattern | Where to test instead |
|---|---|
| Single component rendering ("does badge X show on prop Y") | Component test (`*.test.tsx`) |
| Hook return shape ("does `useFoo` return `{ data, isLoading }`") | Hook test (`renderHook` + Vitest) |
| API client mapping ("does the client serialize the request") | API client test (`*.test.ts`) |
| Schema validation ("does Zod accept this shape") | Schema unit test |
| Conditional UI based on a single boolean | Component test with prop matrix |
| Anything that requires more mock layers than the test has assertions | Anything else |

### ✅ Do write an E2E for these

| Pattern | Example |
|---|---|
| Multi-step user journey across pages | Login → wizard → session → play → pause → resume → finalize |
| Cross-component coordination | Drag-and-drop between two panels with persistence |
| Browser-only behavior | Service worker, IndexedDB persistence across remounts, file upload |
| Visual regression on critical screens | Hero banner, dashboard, payment page |
| Accessibility audits on flows | Keyboard-only navigation through a 5-step flow |

### Cost signal: count the mock layers

If your E2E spec needs more than ~3 of these layers, it's probably the wrong
test level:

1. Cookie injection (`addCookies`)
2. Env var bypass (`PLAYWRIGHT_AUTH_BYPASS`)
3. `setupMockAuth()` for `/auth/me`
4. Route glob mocks for the actual API endpoints
5. Schema-compliant mock data that matches Zod validators
6. UI state setup (clicking through to reach the tested component)

A test that needs all 6 is verifying infrastructure choreography, not user
behavior. Move the assertion to a unit test.

### Historical example

PR #283 added `game-night-soft-filter.spec.ts` (4 tests for the F1 PDF-aware
soft filter). PR #297 attempted to fix its local execution. A multi-expert
spec panel review (2026-04-09) concluded that the test duplicated 15
existing unit tests (`GameKbBadge.test.tsx` × 3, `GameKbWarning.test.tsx` × 4,
`GameNightWizard.test.tsx` × 8) without adding signal, while requiring all
6 mock layers above. The file was deleted; PR #297 was closed.

---

## Troubleshooting

### Test Times Out

**Cause**: Element not appearing within timeout

**Fix**:
```typescript
await page.waitForElement(locator, { timeout: 30000 });  // Increase timeout
```

### Element Not Found

**Cause**: Selector doesn't match UI

**Fix**:
```typescript
await page.pause();  // Visual debugger
// Update selector in page object
```

### Test Fails in CI but Works Locally

**Cause**: Race condition or timing issue

**Fix**:
```typescript
await page.waitForLoadState('networkidle');  // Wait for network
await page.waitForTimeout(1000);  // Last resort (for debounced inputs only)
```

### Page Object Method Not Found

**Cause**: TypeScript compilation issue

**Fix**:
```bash
pnpm typecheck  # Check for errors
```

---

## Migration Status

**Target**: 30 test files
**Completed**: 0 test files (0%)
**Phase**: Design Complete (2025-11-10)

See [Migration Guide](../../../docs/testing/pom-migration-guide.md) for step-by-step instructions.

---

## Contributing

### Adding New Page Object

1. Create file in appropriate `pages/` subdirectory
2. Extend `BasePage` or `BaseModal`
3. Implement interface from `types/pom-interfaces.ts`
4. Follow [Coding Standards](../../../docs/testing/pom-coding-standards.md)
5. Add JSDoc documentation
6. Test with example test file

### Migrating Existing Test

1. Read [Migration Guide](../../../docs/testing/pom-migration-guide.md)
2. Follow 9-step process
3. Run `pnpm test:e2e <filename>` to verify
4. Update migration status document

### Reporting Issues

- Create GitHub issue with `[E2E]` prefix
- Include test file name and error message
- Link to relevant page object if applicable

---

## Resources

### Documentation
- [POM Architecture](../../../docs/testing/pom-architecture-design.md) - 6,500+ lines
- [TypeScript Interfaces](./types/pom-interfaces.ts) - 450+ lines
- [Migration Guide](../../../docs/testing/pom-migration-guide.md) - 1,000+ lines
- [Coding Standards](../../../docs/testing/pom-coding-standards.md) - 800+ lines

### Examples
- [BasePage.ts](./pages/base/BasePage.ts) - Base class (100+ lines)
- [AuthPage.ts](./pages/auth/AuthPage.ts) - Full auth page (320+ lines)
- [ChatPage.ts](./pages/chat/ChatPage.ts) - Chat interface (300+ lines)

### External
- [Playwright Documentation](https://playwright.dev/docs/intro)
- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [ARIA Roles Reference](https://www.w3.org/TR/wai-aria-1.2/#role_definitions)

---

## Support

**Questions?**
- Read documentation above
- Check examples in `pages/auth/` and `pages/chat/`
- Ask in team chat
- Create GitHub discussion

**Found a bug?**
- Check [Troubleshooting](#troubleshooting) section
- Create GitHub issue with `[E2E]` prefix

**Want to contribute?**
- Read [Contributing](#contributing) section
- Follow [Coding Standards](../../../docs/testing/pom-coding-standards.md)
- Submit PR with tests

---

**Last Updated**: 2025-11-10
**Issue**: #843 Phase 2
**Status**: ✅ Design Complete - Ready for Implementation
