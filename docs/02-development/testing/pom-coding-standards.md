# Page Object Model Coding Standards

**Audience**: Developers writing or maintaining E2E tests with POM architecture
**Scope**: TypeScript, Playwright, React Testing patterns
**Related**: [POM Architecture Design](./pom-architecture-design.md), [POM Migration Guide](./pom-migration-guide.md)

---

## Table of Contents

1. [File Organization](#file-organization)
2. [Naming Conventions](#naming-conventions)
3. [Selector Strategy](#selector-strategy)
4. [Method Design Patterns](#method-design-patterns)
5. [Wait Strategies](#wait-strategies)
6. [Error Handling](#error-handling)
7. [Test Independence](#test-independence)
8. [Type Safety](#type-safety)
9. [Documentation Standards](#documentation-standards)
10. [Performance Guidelines](#performance-guidelines)

---

## 1. File Organization

### Directory Structure

```
apps/web/e2e/
├── pages/
│   ├── base/
│   │   ├── BasePage.ts         # Base class for all pages
│   │   └── BaseModal.ts        # Base class for modals
│   ├── auth/
│   │   ├── AuthPage.ts         # Login, register, OAuth, 2FA
│   │   └── PasswordResetPage.ts
│   ├── chat/
│   │   ├── ChatPage.ts         # Main chat interface
│   │   └── ChatMessage.ts      # Message component wrapper
│   ├── editor/
│   │   ├── EditorPage.ts       # RuleSpec editor
│   │   ├── RichTextEditor.ts   # TipTap wrapper
│   │   └── VersionHistoryPage.ts
│   ├── game/
│   │   ├── GameListPage.ts     # Browse/search games
│   │   ├── GameDetailPage.ts   # Game details
│   │   └── PdfUploadPage.ts    # PDF upload wizard
│   └── admin/
│       ├── UserManagementPage.ts
│       ├── AnalyticsPage.ts
│       └── ConfigurationPage.ts
├── components/
│   ├── Modal.ts                # Generic modal interactions
│   ├── Table.ts                # Table component wrapper
│   ├── Form.ts                 # Form component wrapper
│   ├── Toast.ts                # Toast notifications
│   └── FileUpload.ts           # File upload component
├── fixtures/
│   ├── auth.ts                 # Auth fixtures (existing)
│   ├── data.ts                 # Data factories
│   ├── cleanup.ts              # Cleanup utilities
│   └── i18n.ts                 # i18n support (existing)
├── types/
│   └── pom-interfaces.ts       # TypeScript interfaces
└── *.spec.ts                   # Test files
```

### File Naming Rules

| Type | Pattern | Example |
|------|---------|---------|
| Page Objects | `<Feature>Page.ts` | `AuthPage.ts`, `ChatPage.ts` |
| Component Wrappers | `<Component>.ts` | `Modal.ts`, `Table.ts` |
| Fixtures | `<feature>.ts` | `auth.ts`, `data.ts` |
| Test Files | `<feature>.spec.ts` | `chat.spec.ts`, `admin-users.spec.ts` |
| Base Classes | `Base<Type>.ts` | `BasePage.ts`, `BaseModal.ts` |
| Interfaces | `pom-interfaces.ts` | Centralized type definitions |

---

## 2. Naming Conventions

### Class Names

```typescript
// ✅ CORRECT: PascalCase with descriptive suffix
export class AuthPage extends BasePage { }
export class ChatMessage extends BasePage { }
export class UserManagementPage extends BasePage { }

// ❌ INCORRECT: No suffix, unclear purpose
export class Auth extends BasePage { }
export class Message extends BasePage { }
export class Users extends BasePage { }
```

### Method Names

| Method Type | Pattern | Example |
|-------------|---------|---------|
| **Actions** | Verb + Object | `login()`, `askQuestion()`, `createUser()` |
| **Queries** | Get + Object | `getLastAnswer()`, `getUserCount()` |
| **Assertions** | Assert + Condition | `assertLoginSuccess()`, `assertUserVisible()` |
| **Wait Helpers** | WaitFor + Condition | `waitForLoginSuccess()`, `waitForToastDisappear()` |
| **Composite** | Action + AndWait | `loginAndWait()`, `askQuestionAndWait()` |

```typescript
// ✅ CORRECT: Semantic method names
async login(credentials: LoginCredentials): Promise<void> { }
async getLastAnswer(): Promise<string> { }
async assertUserVisible(email: string): Promise<void> { }
async waitForLoginSuccess(): Promise<void> { }
async loginAndWait(credentials: LoginCredentials): Promise<void> { }

// ❌ INCORRECT: Low-level or unclear names
async clickLoginButton(): Promise<void> { }  // Too low-level
async checkUser(email: string): Promise<void> { }  // Unclear (check = assert or get?)
async doLogin(): Promise<void> { }  // "do" prefix is vague
```

### Locator Names

```typescript
// ✅ CORRECT: Descriptive property names (private getters)
private get loginForm(): Locator { }
private get emailInput(): Locator { }
private get submitButton(): Locator { }
private get loginSuccessMessage(): Locator { }

// ❌ INCORRECT: Generic or unclear names
private get form(): Locator { }  // Too generic
private get input1(): Locator { }  // Meaningless
private get btn(): Locator { }  // Abbreviation
```

### Variable Names

```typescript
// ✅ CORRECT: Descriptive, camelCase
const userPage = new UserManagementPage(page);
const newUser = DataFactory.createUser();
const citationCount = await chatPage.getCitationCount();

// ❌ INCORRECT: Unclear or abbreviated
const up = new UserManagementPage(page);
const u = DataFactory.createUser();
const cnt = await chatPage.getCitationCount();
```

---

## 3. Selector Strategy

### Selector Priority (Accessibility-First)

**Priority Order**:

1. **Role-based** (ARIA roles) - BEST
2. **Label-based** (form labels)
3. **Text-based** (visible text)
4. **Test IDs** (data-testid)
5. **CSS selectors** - LAST RESORT

```typescript
// ✅ PRIORITY 1: Role-based selectors (best for accessibility)
this.page.getByRole('button', { name: /submit/i })
this.page.getByRole('heading', { name: /login/i })
this.page.getByRole('link', { name: /home/i })

// ✅ PRIORITY 2: Label-based selectors (forms)
this.page.getByLabel(/email/i)
this.page.getByLabel(/password/i)

// ✅ PRIORITY 3: Text-based selectors (visible content)
this.page.getByText(/success/i)
this.page.getByPlaceholder(/search/i)

// ⚠️ PRIORITY 4: Test IDs (when semantic selectors unavailable)
this.page.getByTestId('user-row-123')
this.page.getByTestId('chat-messages')

// ❌ PRIORITY 5: CSS selectors (AVOID - brittle)
this.page.locator('.btn-submit')
this.page.locator('#user-table > tbody > tr:nth-child(2)')
```

### Selector Best Practices

```typescript
// ✅ CORRECT: Case-insensitive regex
this.page.getByRole('button', { name: /submit/i })

// ❌ INCORRECT: Case-sensitive exact match (fragile)
this.page.getByRole('button', { name: 'Submit' })

// ✅ CORRECT: Filter with semantic context
this.page.locator('form')
  .filter({ has: this.page.getByRole('heading', { name: /login/i }) })

// ❌ INCORRECT: Generic locator without context
this.page.locator('form').first()

// ✅ CORRECT: Use nth() for specific elements
this.messagesContainer.locator('.message').nth(0)

// ❌ INCORRECT: CSS :nth-child (couples to DOM structure)
this.page.locator('.message:nth-child(1)')
```

### i18n Support

```typescript
import { getTextMatcher, t } from './fixtures/i18n';

// ✅ CORRECT: Use i18n helpers for multilingual support
this.page.getByRole('button', { name: getTextMatcher('common.submit') })
this.page.getByText(getTextMatcher('chat.loginRequired'))

// ⚠️ ACCEPTABLE: Regex for multilingual support
this.page.getByRole('button', { name: /submit|invia/i })

// ❌ INCORRECT: Hardcoded English-only text
this.page.getByRole('button', { name: 'Submit' })
```

---

## 4. Method Design Patterns

### Action Methods

**Purpose**: Perform UI interactions
**Return**: `Promise<void>`

```typescript
// ✅ CORRECT: Clear, semantic action method
async login(credentials: LoginCredentials): Promise<void> {
  await this.fill(this.emailInput, credentials.email);
  await this.fill(this.passwordInput, credentials.password);
  await this.click(this.submitButton);
}

// ❌ INCORRECT: Returns data (not an action)
async login(credentials: LoginCredentials): Promise<string> {
  // ... login logic ...
  return await this.getUserId();  // ❌ Query, not action
}
```

### Query Methods

**Purpose**: Retrieve data from UI
**Return**: Data type (`Promise<string>`, `Promise<number>`, etc.)

```typescript
// ✅ CORRECT: Query method returns data
async getLastAnswer(): Promise<string> {
  const lastMessage = this.messagesContainer.locator('.answer').last();
  return await lastMessage.textContent() || '';
}

async getUserCount(): Promise<number> {
  return await this.userTable.locator('tbody tr').count();
}

// ❌ INCORRECT: Query method performs action
async getLastAnswer(): Promise<string> {
  await this.click(this.refreshButton);  // ❌ Should not perform action
  return await this.lastAnswer.textContent() || '';
}
```

### Assertion Methods

**Purpose**: Verify UI state
**Return**: `Promise<void>`
**Naming**: Start with `assert`

```typescript
// ✅ CORRECT: Assertion method with semantic name
async assertLoginSuccess(): Promise<void> {
  await this.waitForElement(this.successMessage);
}

async assertUserVisible(email: string): Promise<void> {
  await this.waitForElement(
    this.page.getByRole('cell', { name: email, exact: true })
  );
}

// ❌ INCORRECT: Returns boolean (not an assertion)
async assertLoginSuccess(): Promise<boolean> {
  return await this.isVisible(this.successMessage);  // ❌ Use isLoginSuccess() instead
}

// ❌ INCORRECT: No "assert" prefix
async loginSuccess(): Promise<void> {
  await this.waitForElement(this.successMessage);
}
```

### Composite Methods

**Purpose**: Combine action + wait for common flows
**Naming**: `<action>AndWait`

```typescript
// ✅ CORRECT: Composite method for common flow
async loginAndWait(credentials: LoginCredentials): Promise<void> {
  await this.login(credentials);
  await this.waitForLoginSuccess();
}

async askQuestionAndWait(question: string): Promise<void> {
  await this.askQuestion(question);
  await this.waitForAnswer();
}

// ❌ INCORRECT: Composite method does too much
async loginAndCheckUserCount(): Promise<number> {  // ❌ Mixing concerns
  await this.login(credentials);
  return await this.getUserCount();
}
```

### Helper Methods

**Purpose**: Internal utilities (reusable within page object only)
**Visibility**: `private`

```typescript
// ✅ CORRECT: Private helper method
private async fillCreateUserForm(user: User): Promise<void> {
  await this.fill(this.emailInput, user.email);
  await this.fill(this.passwordInput, user.password || 'DefaultPass123!');
  await this.fill(this.displayNameInput, user.displayName);
}

public async createUser(user: User): Promise<void> {
  await this.clickCreateUserButton();
  await this.fillCreateUserForm(user);  // ✅ Reuse helper
  await this.submitUserForm();
}
```

---

## 5. Wait Strategies

### Playwright Auto-Waiting

Playwright automatically waits for elements before actions. **Prefer implicit waits over explicit timeouts.**

```typescript
// ✅ CORRECT: Implicit wait (Playwright auto-waits)
async click(locator: Locator): Promise<void> {
  await locator.click();  // Auto-waits for visible + enabled
}

// ❌ INCORRECT: Unnecessary explicit wait
async click(locator: Locator): Promise<void> {
  await this.page.waitForTimeout(2000);  // ❌ Arbitrary timeout
  await locator.click();
}
```

### Semantic Wait Methods

```typescript
// ✅ CORRECT: Semantic wait for element
await this.waitForElement(this.successMessage);

// ✅ CORRECT: Semantic wait for element to disappear
await this.waitForElementToDisappear(this.loadingSpinner);

// ✅ CORRECT: Semantic wait for URL change
await this.waitForUrl(/\/dashboard/);

// ❌ INCORRECT: Arbitrary timeout
await this.page.waitForTimeout(5000);  // ❌ What are we waiting for?
```

### Wait for Network Idle

```typescript
// ✅ CORRECT: Wait for network idle after navigation
async goto(): Promise<void> {
  await this.page.goto('/chat');
  await this.waitForLoad();  // waitForLoadState('networkidle')
}

// ⚠️ ACCEPTABLE: Wait for specific request to complete
await this.page.waitForResponse((response) =>
  response.url().includes('/api/users') && response.status() === 200
);
```

### Wait for Streaming (Special Case)

```typescript
// ✅ CORRECT: Wait for streaming indicator to disappear
async waitForAnswer(): Promise<void> {
  await this.waitForElementToDisappear(this.streamingIndicator);
}

// ❌ INCORRECT: Fixed timeout (doesn't handle variable streaming time)
async waitForAnswer(): Promise<void> {
  await this.page.waitForTimeout(10000);  // ❌ What if streaming takes 15s?
}
```

### Debounced Inputs (Exception)

**Only use `waitForTimeout()` for debounced inputs (search, auto-save):**

```typescript
// ✅ ACCEPTABLE: Wait for debounced search
async searchGames(query: string): Promise<void> {
  await this.fill(this.searchInput, query);
  await this.page.waitForTimeout(500);  // Wait for 500ms debounce
}

// ✅ ACCEPTABLE: Wait for auto-save
async waitForAutoSave(): Promise<void> {
  await this.page.waitForTimeout(2500);  // Wait for 2s debounce + 500ms buffer
  await this.waitForElementToDisappear(this.unsavedIndicator);
}
```

---

## 6. Error Handling

### Let Playwright Handle Errors

Playwright automatically retries until timeout. **Avoid try-catch for normal failures.**

```typescript
// ✅ CORRECT: Let Playwright auto-retry and fail with clear error
async clickButton(): Promise<void> {
  await this.submitButton.click();  // Auto-retries until visible/enabled or timeout
}

// ❌ INCORRECT: Swallow errors with try-catch
async clickButton(): Promise<void> {
  try {
    await this.submitButton.click();
  } catch (error) {
    console.log('Button not found');  // ❌ Hides real issues
  }
}
```

### Custom Error Messages

```typescript
// ✅ CORRECT: Add context to error messages
async assertUserExists(email: string): Promise<void> {
  const user = this.page.getByText(email);
  await expect(user).toBeVisible({
    timeout: 5000,
    message: `User with email "${email}" not found in table`,
  });
}

// ❌ INCORRECT: Generic error (hard to debug)
async assertUserExists(email: string): Promise<void> {
  const user = this.page.getByText(email);
  await expect(user).toBeVisible();  // Error: "locator not visible"
}
```

### Retry Logic (Advanced)

```typescript
// ⚠️ ACCEPTABLE: Retry for known flaky operations
async clickWithRetry(locator: Locator, maxRetries: number = 3): Promise<void> {
  for (let i = 0; i < maxRetries; i++) {
    try {
      await locator.click({ timeout: 5000 });
      return;  // Success
    } catch (error) {
      if (i === maxRetries - 1) throw error;  // Last retry failed
      await this.page.waitForTimeout(1000);  // Wait before retry
    }
  }
}
```

---

## 7. Test Independence

### Each Test is Self-Contained

```typescript
// ✅ CORRECT: Self-contained test
test('create user', async ({ adminPage }) => {
  const userPage = new UserManagementPage(adminPage);
  await userPage.goto();  // ✅ Fresh navigation

  const newUser = DataFactory.createUser();
  await userPage.createUser(newUser);

  await userPage.assertUserVisible(newUser.email);
});

// ❌ INCORRECT: Shared state between tests
let createdUserId: string;  // ❌ Global variable

test('create user', async ({ adminPage }) => {
  // ...
  createdUserId = await userPage.getUserId();  // ❌ Modifies global
});

test('delete user', async ({ adminPage }) => {
  // ...
  await userPage.deleteUser(createdUserId);  // ❌ Depends on previous test
});
```

### Reset State Between Tests

```typescript
import { CleanupHelper } from './fixtures/cleanup';

test.afterEach(async ({ page }) => {
  const cleanup = new CleanupHelper(page);
  await cleanup.clearSession();  // ✅ Reset state
});
```

---

## 8. Type Safety

### Use Interfaces

```typescript
// ✅ CORRECT: Type-safe interface
import { LoginCredentials } from '../types/pom-interfaces';

async login(credentials: LoginCredentials): Promise<void> {
  // TypeScript ensures email and password are strings
}

// ❌ INCORRECT: Untyped parameters
async login(email: any, password: any): Promise<void> {
  // No type checking
}
```

### Type Guards

```typescript
// ✅ CORRECT: Type guard for validation
if (isValidEmail(user.email)) {
  await this.fill(this.emailInput, user.email);
}

// ❌ INCORRECT: Runtime error if invalid
await this.fill(this.emailInput, user.email);  // ❌ What if email is invalid?
```

### Return Types

```typescript
// ✅ CORRECT: Explicit return type
async getUserCount(): Promise<number> {
  return await this.userTable.locator('tbody tr').count();
}

// ⚠️ ACCEPTABLE: Inferred return type (less explicit)
async getUserCount() {
  return await this.userTable.locator('tbody tr').count();
}
```

---

## 9. Documentation Standards

### Class Documentation

```typescript
/**
 * AuthPage - Authentication page interactions
 *
 * Handles all authentication flows:
 * - Login (email/password, remember me)
 * - Registration
 * - OAuth (Google, Discord, GitHub)
 * - Two-Factor Authentication (TOTP, backup codes)
 * - Password reset
 * - Logout
 *
 * Usage:
 *   const authPage = new AuthPage(page);
 *   await authPage.goto();
 *   await authPage.loginAndWait({ email: '...', password: '...' });
 */
export class AuthPage extends BasePage { }
```

### Method Documentation

```typescript
/**
 * Fill and submit login form
 * @param credentials - Email, password, and optional rememberMe flag
 */
async login(credentials: LoginCredentials): Promise<void> { }

/**
 * Wait for streaming answer to complete
 * Waits for streaming indicator to disappear
 */
async waitForAnswer(): Promise<void> { }

/**
 * Get all citations from the last answer
 * @returns Array of citation objects with title and page number
 */
async getCitations(): Promise<ChatSource[]> { }
```

### Test Documentation

```typescript
/**
 * E2E Test: User Management Complete Lifecycle
 *
 * Scenario: Admin creates, edits, and deletes a user
 *
 * Given: An authenticated admin user
 * When: Admin creates a new user, updates their role, then deletes them
 * Then: All operations succeed and UI reflects changes
 *
 * @see UserManagementPage for page object implementation
 * @see DataFactory for test data generation
 */
test('complete user lifecycle: create → edit → delete', async ({ adminPage }) => {
  // ...
});
```

---

## 10. Performance Guidelines

### Lazy Locator Evaluation

```typescript
// ✅ CORRECT: Lazy locator (getter creates new locator each time)
private get submitButton(): Locator {
  return this.page.getByRole('button', { name: /submit/i });
}

// ❌ INCORRECT: Eager evaluation (stale reference)
private submitButton = this.page.getByRole('button', { name: /submit/i });
```

### Minimize Page Navigations

```typescript
// ✅ CORRECT: Setup mocks BEFORE navigation
await setupMockAuth(page);
await setupGamesRoutes(page);
await page.goto('/chat');  // Single navigation

// ❌ INCORRECT: Multiple navigations
await page.goto('/');
await setupMockAuth(page);
await page.goto('/chat');  // ❌ Redundant navigation
```

### Batch Operations

```typescript
// ✅ CORRECT: Batch API calls
await Promise.all([
  this.page.route('/api/users', mockUsersRoute),
  this.page.route('/api/games', mockGamesRoute),
  this.page.route('/api/auth/me', mockAuthRoute),
]);

// ❌ INCORRECT: Sequential route setup (slower)
await this.page.route('/api/users', mockUsersRoute);
await this.page.route('/api/games', mockGamesRoute);
await this.page.route('/api/auth/me', mockAuthRoute);
```

---

## Quick Reference

### DO ✅

- Use accessibility-first selectors (`getByRole`, `getByLabel`)
- Create semantic methods (`createUser()`, not `clickButton()`)
- Let Playwright auto-wait (avoid `waitForTimeout()`)
- Use fixtures for auth and data setup
- Write self-contained tests (no shared state)
- Add JSDoc comments to classes and complex methods
- Use TypeScript interfaces for type safety

### DON'T ❌

- Use CSS selectors (`.btn-submit`, `#user-table`)
- Create low-level methods (`clickSubmit()`, `fillEmailInput()`)
- Add arbitrary timeouts (`waitForTimeout(5000)`)
- Duplicate auth/mock setup in multiple tests
- Share state between tests (global variables)
- Swallow errors with try-catch
- Use `any` type for parameters

---

## Checklist for New Page Objects

- [ ] Extends `BasePage` or `BaseModal`
- [ ] All locators are private getters
- [ ] Method names are semantic (not low-level)
- [ ] Implements interface from `pom-interfaces.ts`
- [ ] Uses accessibility-first selectors
- [ ] Includes JSDoc class documentation
- [ ] No hardcoded URLs (uses `goto()` method)
- [ ] No arbitrary `waitForTimeout()` calls
- [ ] Assertion methods start with `assert`
- [ ] TypeScript strict mode compliant

---

## References

- [POM Architecture Design](./pom-architecture-design.md)
- [POM Migration Guide](./pom-migration-guide.md)
- [Playwright Best Practices](https://playwright.dev/docs/best-practices)
- [Playwright Locators](https://playwright.dev/docs/locators)
- [ARIA Roles Reference](https://www.w3.org/TR/wai-aria-1.2/#role_definitions)
