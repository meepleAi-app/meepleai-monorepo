# Page Object Model (POM) Architecture Design

**Issue**: #843 Phase 2
**Date**: 2025-11-10
**Status**: Design Document
**Goal**: Expand E2E coverage from 58% to 80%+ with maintainable, scalable test architecture

## Executive Summary

This document defines a comprehensive Page Object Model architecture for Playwright E2E tests. The design standardizes test patterns discovered across 30+ existing test files, eliminates duplication, and provides a foundation for systematic test expansion.

**Key Benefits**:
- **Maintainability**: Single source of truth for selectors and interactions
- **Reusability**: Shared page objects across 30+ test files
- **Type Safety**: Full TypeScript support with interfaces
- **Independence**: No shared state between tests
- **Scalability**: Clear patterns for adding new tests

---

## Current State Analysis

### Existing Patterns (Good ✅)

From analysis of `apps/web/e2e/`:

1. **Mock Authentication** (`fixtures/auth.ts`)
   - `setupMockAuth()` for reliable auth without UI timing issues
   - Role-based fixtures: `adminPage`, `editorPage`, `userPage`
   - Mock games API routes

2. **API Mocking Patterns** (`authenticated.spec.ts`, `pdf-upload-journey.spec.ts`)
   - Consistent route interception with `page.route()`
   - Request/response JSON mocking
   - Stateful mocks (e.g., user list modifications)

3. **Accessibility-First Selectors** (`editor-rich-text.spec.ts`, `admin-users.spec.ts`)
   - `getByRole()`, `getByLabel()`, `getByText()` preferred over CSS
   - `getTextMatcher()` for i18n support

### Problems to Solve (❌)

1. **Selector Duplication**: Same selectors repeated across 5+ files
2. **API Mock Duplication**: Auth mocking code in 8+ test files
3. **Mixed Abstraction Levels**: Tests mix high-level actions with low-level DOM queries
4. **No Wait Helpers**: Explicit `waitForTimeout()` instead of semantic waits
5. **Brittle Assertions**: Direct text matching instead of semantic state checks

---

## Architecture Overview

### 3-Tier Hierarchy

```
┌─────────────────────────────────────────────────────────┐
│                    Test Layer (*.spec.ts)                │
│  - Business logic scenarios                              │
│  - High-level assertions                                 │
│  - No direct DOM interaction                             │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│              Page Object Layer (pages/)                  │
│  - Page-specific interactions                            │
│  - Encapsulated selectors                                │
│  - Semantic methods (e.g., selectGame, askQuestion)      │
└──────────────────────┬──────────────────────────────────┘
                       │
┌──────────────────────▼──────────────────────────────────┐
│           Component & Fixture Layer (components/,        │
│                      fixtures/)                          │
│  - Reusable UI components (Modal, Form, Table)           │
│  - Auth fixtures                                         │
│  - Data fixtures                                         │
│  - Common wait helpers                                   │
└─────────────────────────────────────────────────────────┘
```

### Directory Structure

```
apps/web/e2e/
├── fixtures/
│   ├── auth.ts              # Existing auth fixtures (extend)
│   ├── data.ts              # NEW: Test data factories
│   ├── cleanup.ts           # NEW: Cleanup utilities
│   └── i18n.ts              # Existing i18n support
├── pages/
│   ├── base/
│   │   ├── BasePage.ts      # NEW: Common page utilities
│   │   └── BaseModal.ts     # NEW: Modal base class
│   ├── auth/
│   │   ├── AuthPage.ts      # NEW: Login/register/OAuth/2FA
│   │   └── PasswordResetPage.ts  # NEW: Password reset flow
│   ├── game/
│   │   ├── GameListPage.ts  # NEW: Browse/search games
│   │   ├── GameDetailPage.ts # NEW: Game details view
│   │   └── PdfUploadPage.ts  # NEW: PDF upload wizard
│   ├── chat/
│   │   ├── ChatPage.ts      # NEW: Chat interface
│   │   └── ChatMessage.ts   # NEW: Message component wrapper
│   ├── editor/
│   │   ├── EditorPage.ts    # NEW: RuleSpec editor
│   │   ├── RichTextEditor.ts # NEW: TipTap wrapper
│   │   └── VersionHistoryPage.ts # NEW: Version management
│   └── admin/
│       ├── AdminDashboardPage.ts    # NEW: Admin overview
│       ├── UserManagementPage.ts    # NEW: User CRUD
│       ├── AnalyticsPage.ts         # NEW: Analytics dashboard
│       ├── PromptManagementPage.ts  # NEW: Prompt templates
│       └── ConfigurationPage.ts     # NEW: System config
├── components/
│   ├── Modal.ts             # NEW: Generic modal interactions
│   ├── Form.ts              # NEW: Form filling/validation
│   ├── Table.ts             # NEW: Table sorting/filtering/pagination
│   ├── Toast.ts             # NEW: Toast notification handling
│   ├── FileUpload.ts        # NEW: File upload component
│   └── MonacoEditor.ts      # NEW: Code editor wrapper
└── *.spec.ts                # Existing tests (migrate to use POMs)
```

---

## Base Classes

### 1. BasePage

**Purpose**: Common utilities shared across all page objects

**File**: `apps/web/e2e/pages/base/BasePage.ts`

```typescript
import { Page, Locator, expect } from '@playwright/test';

export interface WaitOptions {
  timeout?: number;
  state?: 'attached' | 'detached' | 'visible' | 'hidden';
}

export abstract class BasePage {
  constructor(protected readonly page: Page) {}

  /**
   * Navigate to page URL (override in subclasses)
   */
  abstract goto(): Promise<void>;

  /**
   * Wait for page to be fully loaded
   */
  async waitForLoad(): Promise<void> {
    await this.page.waitForLoadState('networkidle');
  }

  /**
   * Wait for element to be visible with semantic timeout
   */
  async waitForElement(
    locator: Locator,
    options: WaitOptions = {}
  ): Promise<void> {
    await expect(locator).toBeVisible({
      timeout: options.timeout || 10000,
    });
  }

  /**
   * Wait for element to disappear (e.g., loading spinner)
   */
  async waitForElementToDisappear(
    locator: Locator,
    options: WaitOptions = {}
  ): Promise<void> {
    await expect(locator).not.toBeVisible({
      timeout: options.timeout || 10000,
    });
  }

  /**
   * Wait for URL to match pattern
   */
  async waitForUrl(pattern: string | RegExp): Promise<void> {
    await this.page.waitForURL(pattern, { timeout: 10000 });
  }

  /**
   * Take screenshot (for debugging)
   */
  async screenshot(name: string): Promise<void> {
    await this.page.screenshot({
      path: `e2e-screenshots/${name}.png`,
      fullPage: true,
    });
  }

  /**
   * Get current URL
   */
  getUrl(): string {
    return this.page.url();
  }

  /**
   * Check if element is visible
   */
  async isVisible(locator: Locator): Promise<boolean> {
    return await locator.isVisible();
  }

  /**
   * Fill input with automatic waiting
   */
  async fill(locator: Locator, value: string): Promise<void> {
    await locator.fill(value);
  }

  /**
   * Click element with automatic waiting
   */
  async click(locator: Locator, options: { force?: boolean } = {}): Promise<void> {
    await locator.click(options);
  }

  /**
   * Select option from dropdown
   */
  async selectOption(locator: Locator, value: string): Promise<void> {
    await locator.selectOption(value);
  }

  /**
   * Upload file
   */
  async uploadFile(locator: Locator, filePath: string): Promise<void> {
    await locator.setInputFiles(filePath);
  }
}
```

### 2. BaseModal

**Purpose**: Common modal interactions (open, close, submit, cancel)

**File**: `apps/web/e2e/pages/base/BaseModal.ts`

```typescript
import { Page, Locator } from '@playwright/test';
import { BasePage } from './BasePage';

export abstract class BaseModal extends BasePage {
  /**
   * Get modal container (override for specific modal selector)
   */
  abstract getModalLocator(): Locator;

  /**
   * Wait for modal to open
   */
  async waitForOpen(): Promise<void> {
    await this.waitForElement(this.getModalLocator());
  }

  /**
   * Wait for modal to close
   */
  async waitForClose(): Promise<void> {
    await this.waitForElementToDisappear(this.getModalLocator());
  }

  /**
   * Click cancel button
   */
  async cancel(): Promise<void> {
    const cancelButton = this.page.getByRole('button', { name: /cancel/i });
    await this.click(cancelButton);
    await this.waitForClose();
  }

  /**
   * Click confirm/submit button
   */
  async confirm(): Promise<void> {
    const confirmButton = this.page.getByRole('button', {
      name: /confirm|submit|save/i,
    });
    await this.click(confirmButton);
    await this.waitForClose();
  }

  /**
   * Close modal via X button or ESC key
   */
  async close(): Promise<void> {
    await this.page.keyboard.press('Escape');
    await this.waitForClose();
  }
}
```

---

## Page Objects

### 3. AuthPage

**Purpose**: Authentication flows (login, register, OAuth, 2FA, password reset)

**File**: `apps/web/e2e/pages/auth/AuthPage.ts`

```typescript
import { Page, Locator } from '@playwright/test';
import { BasePage } from '../base/BasePage';

export interface LoginCredentials {
  email: string;
  password: string;
  rememberMe?: boolean;
}

export interface RegistrationData {
  email: string;
  password: string;
  displayName: string;
}

export interface TwoFactorData {
  code?: string;         // TOTP code
  backupCode?: string;   // Backup code
}

export class AuthPage extends BasePage {
  // Locators
  private get loginForm(): Locator {
    return this.page
      .locator('form')
      .filter({ has: this.page.getByRole('heading', { name: /accesso|login/i }) });
  }

  private get registerForm(): Locator {
    return this.page
      .locator('form')
      .filter({ has: this.page.getByRole('heading', { name: /registrazione|register/i }) });
  }

  // OAuth buttons
  private get googleOAuthButton(): Locator {
    return this.page.getByRole('button', { name: /google/i });
  }

  private get discordOAuthButton(): Locator {
    return this.page.getByRole('button', { name: /discord/i });
  }

  private get githubOAuthButton(): Locator {
    return this.page.getByRole('button', { name: /github/i });
  }

  // 2FA elements
  private get totpInput(): Locator {
    return this.page.getByLabel(/totp|authenticator code/i);
  }

  private get backupCodeInput(): Locator {
    return this.page.getByLabel(/backup code/i);
  }

  // Navigation
  async goto(): Promise<void> {
    await this.page.goto('/');
    await this.waitForLoad();
  }

  // Login actions
  async login(credentials: LoginCredentials): Promise<void> {
    await this.fill(this.loginForm.getByLabel(/email/i), credentials.email);
    await this.fill(this.loginForm.getByLabel(/password/i), credentials.password);

    if (credentials.rememberMe) {
      await this.loginForm.getByLabel(/remember me/i).check();
    }

    await this.click(this.loginForm.getByRole('button', { name: /entra|login/i }));
  }

  async waitForLoginSuccess(): Promise<void> {
    await this.waitForElement(
      this.page.getByText(/accesso eseguito|logged in successfully/i)
    );
  }

  async loginAndWait(credentials: LoginCredentials): Promise<void> {
    await this.login(credentials);
    await this.waitForLoginSuccess();
  }

  // Registration actions
  async register(data: RegistrationData): Promise<void> {
    await this.fill(this.registerForm.getByLabel(/email/i), data.email);
    await this.fill(this.registerForm.getByLabel(/password/i), data.password);
    await this.fill(
      this.registerForm.getByLabel(/display name|nome/i),
      data.displayName
    );

    await this.click(
      this.registerForm.getByRole('button', { name: /registra|register/i })
    );
  }

  async waitForRegistrationSuccess(): Promise<void> {
    await this.waitForElement(
      this.page.getByText(/registrazione completata|registration successful/i)
    );
  }

  // OAuth actions
  async clickOAuthButton(provider: 'google' | 'discord' | 'github'): Promise<void> {
    const buttonMap = {
      google: this.googleOAuthButton,
      discord: this.discordOAuthButton,
      github: this.githubOAuthButton,
    };

    await this.click(buttonMap[provider]);
  }

  // 2FA actions
  async verify2FA(data: TwoFactorData): Promise<void> {
    if (data.code) {
      await this.fill(this.totpInput, data.code);
    } else if (data.backupCode) {
      await this.fill(this.backupCodeInput, data.backupCode);
    }

    await this.click(this.page.getByRole('button', { name: /verify|verifica/i }));
  }

  async waitFor2FAPrompt(): Promise<void> {
    await this.waitForElement(
      this.page.getByRole('heading', { name: /two.factor|2fa/i })
    );
  }

  // Logout
  async logout(): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /logout|esci/i }));
  }

  async waitForLogoutSuccess(): Promise<void> {
    await this.waitForElement(
      this.page.getByText(/logged out|uscito con successo/i)
    );
  }

  // Password reset (link to separate page)
  async clickForgotPassword(): Promise<void> {
    await this.click(this.page.getByRole('link', { name: /forgot password/i }));
  }

  // Assertions
  async assertLoginFormVisible(): Promise<void> {
    await this.waitForElement(this.loginForm);
  }

  async assertRegisterFormVisible(): Promise<void> {
    await this.waitForElement(this.registerForm);
  }

  async assertValidationError(message: string | RegExp): Promise<void> {
    await this.waitForElement(this.page.getByText(message));
  }
}
```

### 4. ChatPage

**Purpose**: Chat interface interactions

**File**: `apps/web/e2e/pages/chat/ChatPage.ts`

```typescript
import { Page, Locator } from '@playwright/test';
import { BasePage } from '../base/BasePage';

export interface ChatMessage {
  question: string;
  answer?: string;
  sources?: Array<{ title: string; page?: number }>;
}

export class ChatPage extends BasePage {
  // Locators
  private get chatHeading(): Locator {
    return this.page.getByRole('heading', { name: /meepleai chat/i });
  }

  private get questionInput(): Locator {
    return this.page.getByPlaceholder(/fai una domanda|ask a question/i);
  }

  private get sendButton(): Locator {
    return this.page.getByRole('button', { name: /invia|send/i });
  }

  private get messagesContainer(): Locator {
    return this.page.locator('[data-testid="chat-messages"]');
  }

  private get loginRequiredMessage(): Locator {
    return this.page.getByRole('heading', { name: /login required/i });
  }

  // Navigation
  async goto(): Promise<void> {
    await this.page.goto('/chat');
    await this.waitForLoad();
  }

  // Actions
  async askQuestion(question: string): Promise<void> {
    await this.fill(this.questionInput, question);
    await this.click(this.sendButton);
  }

  async waitForAnswer(): Promise<void> {
    // Wait for streaming to complete (spinner disappears)
    await this.waitForElementToDisappear(
      this.page.locator('[data-testid="streaming-indicator"]')
    );
  }

  async askQuestionAndWait(question: string): Promise<void> {
    await this.askQuestion(question);
    await this.waitForAnswer();
  }

  async getLastAnswer(): Promise<string> {
    const lastMessage = this.messagesContainer.locator('.answer').last();
    return await lastMessage.textContent() || '';
  }

  async getCitations(): Promise<Array<{ title: string; page?: number }>> {
    const citations = await this.messagesContainer
      .locator('[data-testid="citation"]')
      .all();

    return Promise.all(
      citations.map(async (citation) => {
        const text = await citation.textContent();
        const match = text?.match(/(.+?)\s*\(Pagina\s+(\d+)\)/);
        return {
          title: match?.[1] || text || '',
          page: match?.[2] ? parseInt(match[2]) : undefined,
        };
      })
    );
  }

  async clickCitation(index: number): Promise<void> {
    const citation = this.messagesContainer
      .locator('[data-testid="citation"]')
      .nth(index);
    await this.click(citation);
  }

  async likeAnswer(): Promise<void> {
    const likeButton = this.messagesContainer
      .last()
      .getByRole('button', { name: /👍|utile/i });
    await this.click(likeButton);
  }

  async dislikeAnswer(): Promise<void> {
    const dislikeButton = this.messagesContainer
      .last()
      .getByRole('button', { name: /👎|non utile/i });
    await this.click(dislikeButton);
  }

  async editMessage(messageIndex: number, newText: string): Promise<void> {
    const message = this.messagesContainer.locator('.message').nth(messageIndex);
    await this.click(message.getByRole('button', { name: /edit|modifica/i }));
    await this.fill(message.getByRole('textbox'), newText);
    await this.click(message.getByRole('button', { name: /save|salva/i }));
  }

  async deleteMessage(messageIndex: number): Promise<void> {
    const message = this.messagesContainer.locator('.message').nth(messageIndex);
    await this.click(message.getByRole('button', { name: /delete|elimina/i }));
    // Confirm deletion modal
    await this.click(this.page.getByRole('button', { name: /confirm|conferma/i }));
  }

  async exportConversation(format: 'json' | 'txt'): Promise<void> {
    await this.click(this.page.getByRole('button', { name: /export|esporta/i }));
    await this.click(
      this.page.getByRole('button', { name: new RegExp(format, 'i') })
    );
  }

  async selectGame(gameName: string): Promise<void> {
    const gameSelect = this.page.getByLabel(/game|gioco/i);
    await this.selectOption(gameSelect, gameName);
  }

  // Assertions
  async assertChatPageVisible(): Promise<void> {
    await this.waitForElement(this.chatHeading);
  }

  async assertLoginRequired(): Promise<void> {
    await this.waitForElement(this.loginRequiredMessage);
  }

  async assertAnswerContains(text: string): Promise<void> {
    await this.waitForElement(this.messagesContainer.getByText(new RegExp(text)));
  }

  async assertCitationVisible(title: string): Promise<void> {
    await this.waitForElement(
      this.messagesContainer.getByText(new RegExp(title))
    );
  }
}
```

### 5. EditorPage

**Purpose**: RuleSpec editor (rich text, JSON, save, undo/redo)

**File**: `apps/web/e2e/pages/editor/EditorPage.ts`

```typescript
import { Page, Locator } from '@playwright/test';
import { BasePage } from '../base/BasePage';

export type EditorMode = 'rich-text' | 'json';

export class EditorPage extends BasePage {
  // Locators
  private get editorHeading(): Locator {
    return this.page.getByRole('heading', { name: /editor rulespec/i });
  }

  private get richTextModeButton(): Locator {
    return this.page.getByText(/📝|editor visuale/i);
  }

  private get jsonModeButton(): Locator {
    return this.page.getByText(/\{|codice json/i);
  }

  private get jsonTextarea(): Locator {
    return this.page.locator('textarea');
  }

  private get richTextEditor(): Locator {
    return this.page.locator('.ProseMirror');
  }

  private get saveButton(): Locator {
    return this.page.getByRole('button', { name: /save|salva/i });
  }

  private get unsavedIndicator(): Locator {
    return this.page.getByText(/unsaved|modifiche non salvate/i);
  }

  private get validationError(): Locator {
    return this.page.getByText(/invalid|non valido/i);
  }

  // Toolbar buttons (rich text mode)
  private get boldButton(): Locator {
    return this.page.getByTitle(/grassetto|bold/i);
  }

  private get italicButton(): Locator {
    return this.page.getByTitle(/corsivo|italic/i);
  }

  private get heading1Button(): Locator {
    return this.page.getByTitle(/titolo 1|heading 1/i);
  }

  private get bulletListButton(): Locator {
    return this.page.getByTitle(/elenco puntato|bullet list/i);
  }

  private get undoButton(): Locator {
    return this.page.getByTitle(/undo|annulla/i).first();
  }

  private get redoButton(): Locator {
    return this.page.getByTitle(/redo|ripeti/i).first();
  }

  // Navigation
  async goto(gameId: string): Promise<void> {
    await this.page.goto(`/editor?gameId=${gameId}`);
    await this.waitForLoad();
  }

  // Mode switching
  async switchToRichTextMode(): Promise<void> {
    await this.click(this.richTextModeButton, { force: true });
    await this.waitForElement(this.richTextEditor);
  }

  async switchToJsonMode(): Promise<void> {
    await this.click(this.jsonModeButton, { force: true });
    await this.waitForElement(this.jsonTextarea);
  }

  async getCurrentMode(): Promise<EditorMode> {
    const isRichTextVisible = await this.isVisible(this.richTextEditor);
    return isRichTextVisible ? 'rich-text' : 'json';
  }

  // JSON mode actions
  async setJsonContent(json: string): Promise<void> {
    await this.switchToJsonMode();
    await this.fill(this.jsonTextarea, json);
  }

  async getJsonContent(): Promise<string> {
    await this.switchToJsonMode();
    return await this.jsonTextarea.inputValue();
  }

  // Rich text mode actions (using toolbar)
  async applyBold(): Promise<void> {
    await this.switchToRichTextMode();
    await this.click(this.boldButton);
  }

  async applyItalic(): Promise<void> {
    await this.switchToRichTextMode();
    await this.click(this.italicButton);
  }

  async applyHeading1(): Promise<void> {
    await this.switchToRichTextMode();
    await this.click(this.heading1Button);
  }

  async applyBulletList(): Promise<void> {
    await this.switchToRichTextMode();
    await this.click(this.bulletListButton);
  }

  // Undo/Redo
  async undo(): Promise<void> {
    await this.click(this.undoButton);
  }

  async redo(): Promise<void> {
    await this.click(this.redoButton);
  }

  // Save actions
  async save(): Promise<void> {
    await this.click(this.saveButton);
  }

  async waitForAutoSave(): Promise<void> {
    // Wait for auto-save indicator (2s debounce)
    await this.page.waitForTimeout(2500);
    await this.waitForElementToDisappear(this.unsavedIndicator);
  }

  // Navigation links
  async goToVersionHistory(): Promise<void> {
    await this.click(
      this.page.getByRole('link', { name: /version history|storico versioni/i })
    );
  }

  async goToHome(): Promise<void> {
    await this.click(this.page.getByRole('link', { name: /home/i }));
  }

  // Assertions
  async assertEditorVisible(): Promise<void> {
    await this.waitForElement(this.editorHeading);
  }

  async assertUnsavedChanges(): Promise<void> {
    await this.waitForElement(this.unsavedIndicator);
  }

  async assertNoUnsavedChanges(): Promise<void> {
    await this.waitForElementToDisappear(this.unsavedIndicator);
  }

  async assertValidationError(): Promise<void> {
    await this.waitForElement(this.validationError);
  }

  async assertSaveButtonEnabled(): Promise<void> {
    await expect(this.saveButton).toBeEnabled();
  }

  async assertSaveButtonDisabled(): Promise<void> {
    await expect(this.saveButton).toBeDisabled();
  }

  async assertToolbarVisible(): Promise<void> {
    await this.waitForElement(this.boldButton);
    await this.waitForElement(this.italicButton);
  }
}
```

### 6. GameListPage

**Purpose**: Browse, search, filter games

**File**: `apps/web/e2e/pages/game/GameListPage.ts`

```typescript
import { Page, Locator } from '@playwright/test';
import { BasePage } from '../base/BasePage';

export interface Game {
  id: string;
  name: string;
  description?: string;
}

export class GameListPage extends BasePage {
  // Locators
  private get searchInput(): Locator {
    return this.page.getByPlaceholder(/search games|cerca giochi/i);
  }

  private get gameCards(): Locator {
    return this.page.locator('[data-testid="game-card"]');
  }

  private get uploadButton(): Locator {
    return this.page.getByRole('button', { name: /upload pdf/i });
  }

  // Navigation
  async goto(): Promise<void> {
    await this.page.goto('/games');
    await this.waitForLoad();
  }

  // Actions
  async searchGames(query: string): Promise<void> {
    await this.fill(this.searchInput, query);
    // Wait for debounced search
    await this.page.waitForTimeout(500);
  }

  async getGameCount(): Promise<number> {
    return await this.gameCards.count();
  }

  async selectGame(gameName: string): Promise<void> {
    const gameCard = this.page.locator(`[data-testid="game-card"]:has-text("${gameName}")`);
    await this.click(gameCard);
  }

  async clickUploadPDF(): Promise<void> {
    await this.click(this.uploadButton);
  }

  // Assertions
  async assertGameVisible(gameName: string): Promise<void> {
    await this.waitForElement(this.page.getByText(gameName));
  }

  async assertGameNotVisible(gameName: string): Promise<void> {
    await expect(this.page.getByText(gameName)).not.toBeVisible();
  }
}
```

### 7. UserManagementPage

**Purpose**: Admin user CRUD operations

**File**: `apps/web/e2e/pages/admin/UserManagementPage.ts`

```typescript
import { Page, Locator } from '@playwright/test';
import { BasePage } from '../base/BasePage';

export interface User {
  email: string;
  displayName: string;
  role: 'Admin' | 'Editor' | 'User';
  password?: string;
}

export class UserManagementPage extends BasePage {
  // Locators
  private get createUserButton(): Locator {
    return this.page.getByTestId('open-create-user-modal');
  }

  private get searchInput(): Locator {
    return this.page.getByPlaceholder(/search|cerca/i);
  }

  private get roleFilter(): Locator {
    return this.page.locator('select').first();
  }

  private get userTable(): Locator {
    return this.page.locator('table');
  }

  private get deleteConfirmButton(): Locator {
    return this.page.getByRole('button', { name: /confirm|conferma/i });
  }

  // Navigation
  async goto(): Promise<void> {
    await this.page.goto('/admin/users');
    await this.waitForLoad();
  }

  // Create User
  async clickCreateUser(): Promise<void> {
    await this.click(this.createUserButton);
  }

  async fillCreateUserForm(user: User): Promise<void> {
    await this.fill(this.page.getByLabel(/email/i), user.email);
    await this.fill(this.page.getByLabel(/password/i), user.password || 'DefaultPass123!');
    await this.fill(this.page.getByLabel(/display name|nome/i), user.displayName);
    await this.selectOption(this.page.getByLabel(/role|ruolo/i), user.role);
  }

  async submitUserForm(): Promise<void> {
    await this.click(this.page.getByTestId('submit-user-form'));
  }

  async createUser(user: User): Promise<void> {
    await this.clickCreateUser();
    await this.fillCreateUserForm(user);
    await this.submitUserForm();
    await this.waitForSuccessToast(/created successfully/i);
  }

  // Edit User
  async clickEditUser(email: string): Promise<void> {
    const userRow = this.getUserRow(email);
    await this.click(userRow.getByRole('button', { name: /edit|modifica/i }));
  }

  async updateUser(currentEmail: string, updates: Partial<User>): Promise<void> {
    await this.clickEditUser(currentEmail);

    if (updates.displayName) {
      await this.fill(
        this.page.getByLabel(/display name|nome/i),
        updates.displayName
      );
    }
    if (updates.role) {
      await this.selectOption(this.page.getByLabel(/role|ruolo/i), updates.role);
    }

    await this.click(this.page.getByRole('button', { name: /save|salva/i }));
    await this.waitForSuccessToast(/updated successfully/i);
  }

  // Delete User
  async deleteUser(email: string): Promise<void> {
    const userRow = this.getUserRow(email);
    await this.click(userRow.getByRole('button', { name: /delete|elimina/i }), {
      force: true,
    });
    await this.click(this.deleteConfirmButton, { force: true });
    await this.waitForSuccessToast(/deleted successfully/i);
  }

  // Search & Filter
  async searchUsers(query: string): Promise<void> {
    await this.fill(this.searchInput, query);
  }

  async filterByRole(role: 'all' | 'Admin' | 'Editor' | 'User'): Promise<void> {
    await this.selectOption(this.roleFilter, role);
  }

  // Bulk operations
  async selectUserCheckbox(email: string): Promise<void> {
    const userRow = this.getUserRow(email);
    await userRow.getByRole('checkbox').check();
  }

  async clickBulkDelete(): Promise<void> {
    await this.click(this.page.getByText(/delete selected|elimina selezionati/i));
    await this.click(this.deleteConfirmButton);
  }

  // Helpers
  private getUserRow(email: string): Locator {
    return this.page.locator(`tr:has-text("${email}")`);
  }

  private async waitForSuccessToast(pattern: RegExp): Promise<void> {
    await this.waitForElement(this.page.getByText(pattern));
    await this.page.waitForTimeout(1000); // Wait for toast to dismiss
  }

  // Assertions
  async assertUserVisible(email: string): Promise<void> {
    await this.waitForElement(this.page.getByRole('cell', { name: email, exact: true }));
  }

  async assertUserNotVisible(email: string): Promise<void> {
    await expect(this.page.getByText(email)).not.toBeVisible();
  }

  async assertUserCount(count: number): Promise<void> {
    const rows = this.userTable.locator('tbody tr');
    await expect(rows).toHaveCount(count);
  }
}
```

---

## Component Objects

### 8. Modal Component

**Purpose**: Reusable modal interactions

**File**: `apps/web/e2e/components/Modal.ts`

```typescript
import { Page, Locator } from '@playwright/test';

export class Modal {
  constructor(
    private readonly page: Page,
    private readonly modalSelector: string | Locator
  ) {}

  get modal(): Locator {
    return typeof this.modalSelector === 'string'
      ? this.page.locator(this.modalSelector)
      : this.modalSelector;
  }

  async waitForOpen(): Promise<void> {
    await expect(this.modal).toBeVisible();
  }

  async waitForClose(): Promise<void> {
    await expect(this.modal).not.toBeVisible();
  }

  async close(): Promise<void> {
    await this.page.keyboard.press('Escape');
    await this.waitForClose();
  }

  async clickButton(name: string | RegExp): Promise<void> {
    await this.modal.getByRole('button', { name }).click();
  }

  async cancel(): Promise<void> {
    await this.clickButton(/cancel|annulla/i);
    await this.waitForClose();
  }

  async confirm(): Promise<void> {
    await this.clickButton(/confirm|conferma|submit/i);
    await this.waitForClose();
  }

  async fillInput(label: string | RegExp, value: string): Promise<void> {
    await this.modal.getByLabel(label).fill(value);
  }

  async selectOption(label: string | RegExp, value: string): Promise<void> {
    await this.modal.getByLabel(label).selectOption(value);
  }
}
```

### 9. Table Component

**Purpose**: Table interactions (sort, filter, pagination)

**File**: `apps/web/e2e/components/Table.ts`

```typescript
import { Page, Locator } from '@playwright/test';

export class Table {
  constructor(
    private readonly page: Page,
    private readonly tableSelector: string = 'table'
  ) {}

  get table(): Locator {
    return this.page.locator(this.tableSelector);
  }

  get rows(): Locator {
    return this.table.locator('tbody tr');
  }

  async getRowCount(): Promise<number> {
    return await this.rows.count();
  }

  async getRowByText(text: string): Locator {
    return this.table.locator(`tr:has-text("${text}")`);
  }

  async getCellValue(rowIndex: number, columnName: string): Promise<string> {
    const headerIndex = await this.getColumnIndex(columnName);
    const cell = this.rows.nth(rowIndex).locator('td').nth(headerIndex);
    return await cell.textContent() || '';
  }

  async clickColumnHeader(columnName: string): Promise<void> {
    const header = this.table.getByRole('columnheader', { name: new RegExp(columnName, 'i') });
    await header.click();
  }

  async sortByColumn(columnName: string, order: 'asc' | 'desc'): Promise<void> {
    await this.clickColumnHeader(columnName);
    // Click again if we need descending order (first click is ascending)
    if (order === 'desc') {
      const currentOrder = await this.getSortOrder(columnName);
      if (currentOrder === 'asc') {
        await this.clickColumnHeader(columnName);
      }
    }
  }

  async getSortOrder(columnName: string): Promise<'asc' | 'desc' | null> {
    const header = this.table.getByRole('columnheader', { name: new RegExp(columnName, 'i') });
    const text = await header.textContent();
    if (text?.includes('↑')) return 'asc';
    if (text?.includes('↓')) return 'desc';
    return null;
  }

  private async getColumnIndex(columnName: string): Promise<number> {
    const headers = await this.table.locator('thead th').allTextContents();
    return headers.findIndex((header) =>
      header.toLowerCase().includes(columnName.toLowerCase())
    );
  }

  // Pagination
  async goToNextPage(): Promise<void> {
    await this.page.getByTestId('pagination-next').click();
  }

  async goToPreviousPage(): Promise<void> {
    await this.page.getByTestId('pagination-previous').click();
  }

  async getCurrentPage(): Promise<number> {
    const pageInfo = await this.page.getByText(/page \d+ of \d+/i).textContent();
    const match = pageInfo?.match(/page (\d+) of \d+/i);
    return match ? parseInt(match[1]) : 1;
  }

  // Checkbox selection
  async selectRow(rowIndex: number): Promise<void> {
    await this.rows.nth(rowIndex).getByRole('checkbox').check();
  }

  async deselectRow(rowIndex: number): Promise<void> {
    await this.rows.nth(rowIndex).getByRole('checkbox').uncheck();
  }

  async selectAllRows(): Promise<void> {
    await this.table.locator('thead').getByRole('checkbox').check();
  }
}
```

### 10. Toast Component

**Purpose**: Toast notification handling

**File**: `apps/web/e2e/components/Toast.ts`

```typescript
import { Page, Locator, expect } from '@playwright/test';

export type ToastType = 'success' | 'error' | 'warning' | 'info';

export class Toast {
  constructor(private readonly page: Page) {}

  private get toastContainer(): Locator {
    return this.page.locator('[data-testid="toast"]');
  }

  async waitForToast(type?: ToastType): Promise<void> {
    const locator = type
      ? this.toastContainer.filter({ has: this.page.locator(`[data-type="${type}"]`) })
      : this.toastContainer;
    await expect(locator).toBeVisible();
  }

  async waitForToastToDisappear(): Promise<void> {
    await expect(this.toastContainer).not.toBeVisible({ timeout: 5000 });
  }

  async getToastMessage(): Promise<string> {
    return await this.toastContainer.textContent() || '';
  }

  async assertToastMessage(message: string | RegExp): Promise<void> {
    await expect(this.toastContainer).toContainText(message);
  }

  async closeToast(): Promise<void> {
    const closeButton = this.toastContainer.getByRole('button', { name: /close/i });
    await closeButton.click();
    await this.waitForToastToDisappear();
  }
}
```

---

## Fixture Integration

### 11. Enhanced Auth Fixture

**File**: `apps/web/e2e/fixtures/auth.ts` (extend existing)

```typescript
import { test as base, Page } from '@playwright/test';
import { AuthPage } from '../pages/auth/AuthPage';

// ... existing setupMockAuth, loginAsAdmin, etc. ...

/**
 * Extended test with POM-based auth pages
 */
export const test = base.extend<{
  adminPage: Page;
  editorPage: Page;
  userPage: Page;
  authPage: AuthPage;
}>({
  // Existing fixtures
  adminPage: async ({ page }, use) => {
    await loginAsAdmin(page);
    await use(page);
  },
  editorPage: async ({ page }, use) => {
    await loginAsEditor(page);
    await use(page);
  },
  userPage: async ({ page }, use) => {
    await loginAsUser(page);
    await use(page);
  },

  // NEW: AuthPage fixture for tests needing full auth flows
  authPage: async ({ page }, use) => {
    const authPage = new AuthPage(page);
    await use(authPage);
  },
});

export { expect } from '@playwright/test';
```

### 12. Data Fixture

**Purpose**: Test data factories

**File**: `apps/web/e2e/fixtures/data.ts` (NEW)

```typescript
import { User } from '../pages/admin/UserManagementPage';
import { Game } from '../pages/game/GameListPage';

export class DataFactory {
  private static userIdCounter = 1;
  private static gameIdCounter = 1;

  static createUser(overrides?: Partial<User>): User {
    return {
      email: `user${this.userIdCounter++}@example.com`,
      displayName: `Test User ${this.userIdCounter}`,
      role: 'User',
      password: 'SecurePass123!',
      ...overrides,
    };
  }

  static createGame(overrides?: Partial<Game>): Game {
    return {
      id: `game-${this.gameIdCounter++}`,
      name: `Test Game ${this.gameIdCounter}`,
      description: 'A test game description',
      ...overrides,
    };
  }

  static createAdminUser(overrides?: Partial<User>): User {
    return this.createUser({ role: 'Admin', ...overrides });
  }

  static createEditorUser(overrides?: Partial<User>): User {
    return this.createUser({ role: 'Editor', ...overrides });
  }

  static reset(): void {
    this.userIdCounter = 1;
    this.gameIdCounter = 1;
  }
}
```

### 13. Cleanup Fixture

**Purpose**: Test cleanup utilities

**File**: `apps/web/e2e/fixtures/cleanup.ts` (NEW)

```typescript
import { Page } from '@playwright/test';

export class CleanupHelper {
  constructor(private readonly page: Page) {}

  /**
   * Clear all cookies and local storage
   */
  async clearSession(): Promise<void> {
    await this.page.context().clearCookies();
    await this.page.evaluate(() => {
      localStorage.clear();
      sessionStorage.clear();
    });
  }

  /**
   * Reset to unauthenticated state
   */
  async logout(): Promise<void> {
    await this.clearSession();
    await this.page.goto('/');
  }

  /**
   * Clear browser cache
   */
  async clearCache(): Promise<void> {
    await this.page.context().clearCookies();
  }
}
```

---

## Migration Guide

### Converting Existing Tests

**Before (Direct DOM manipulation)**:

```typescript
// admin-users.spec.ts (old)
test('create user', async ({ page }) => {
  await mockAuthenticatedAdmin(page);
  await page.goto('http://localhost:3000/admin/users');

  await page.getByTestId('open-create-user-modal').click();
  await page.getByLabel('Email').fill('newuser@example.com');
  await page.getByLabel('Password').fill('SecurePass123!');
  await page.getByLabel('Display Name').fill('New User');
  await page.getByLabel('Role').selectOption('Editor');
  await page.getByTestId('submit-user-form').click();

  await expect(page.getByText(/created successfully/)).toBeVisible();
});
```

**After (POM-based)**:

```typescript
// admin-users.spec.ts (new)
import { test, expect } from './fixtures/auth';
import { UserManagementPage } from './pages/admin/UserManagementPage';
import { DataFactory } from './fixtures/data';

test('create user', async ({ adminPage }) => {
  const userPage = new UserManagementPage(adminPage);
  await userPage.goto();

  const newUser = DataFactory.createUser({
    email: 'newuser@example.com',
    displayName: 'New User',
    role: 'Editor',
  });

  await userPage.createUser(newUser);
  await userPage.assertUserVisible(newUser.email);
});
```

### Migration Checklist

- [ ] **Step 1**: Create page object for target page
- [ ] **Step 2**: Move selectors from test to page object
- [ ] **Step 3**: Create semantic methods (e.g., `createUser()` vs `clickCreateButton()`)
- [ ] **Step 4**: Replace direct `page.` calls with page object methods
- [ ] **Step 5**: Update assertions to use page object methods
- [ ] **Step 6**: Remove duplicated setup code (use fixtures)
- [ ] **Step 7**: Add test data factories if needed
- [ ] **Step 8**: Test migration with `pnpm test:e2e <filename>`

---

## Coding Standards

### 1. Naming Conventions

- **Page Objects**: `<Feature>Page.ts` (e.g., `AuthPage.ts`, `ChatPage.ts`)
- **Components**: `<Component>.ts` (e.g., `Modal.ts`, `Table.ts`)
- **Methods**: Semantic action verbs (`login()`, `askQuestion()`, `createUser()`)
- **Locators**: Descriptive property names (`loginForm`, `sendButton`, `userTable`)

### 2. Selector Priority

```typescript
// ✅ PREFER: Accessibility-first selectors
this.page.getByRole('button', { name: /submit/i })
this.page.getByLabel(/email/i)
this.page.getByText(/success/i)

// ⚠️ ACCEPTABLE: Test IDs for dynamic elements
this.page.getByTestId('user-row-123')

// ❌ AVOID: CSS selectors (brittle)
this.page.locator('.btn-submit')
this.page.locator('#user-table > tbody > tr:nth-child(2)')
```

### 3. Method Design Patterns

**Action Methods** (return `Promise<void>`):
```typescript
async login(credentials: LoginCredentials): Promise<void> {
  await this.fill(this.emailInput, credentials.email);
  await this.fill(this.passwordInput, credentials.password);
  await this.click(this.submitButton);
}
```

**Query Methods** (return data):
```typescript
async getLastAnswer(): Promise<string> {
  return await this.lastAnswer.textContent() || '';
}
```

**Assertion Methods** (include `assert` prefix):
```typescript
async assertLoginSuccess(): Promise<void> {
  await this.waitForElement(this.successMessage);
}
```

**Composite Methods** (combine action + wait):
```typescript
async loginAndWait(credentials: LoginCredentials): Promise<void> {
  await this.login(credentials);
  await this.waitForLoginSuccess();
}
```

### 4. Wait Strategies

```typescript
// ✅ PREFER: Semantic waits
await this.waitForElement(locator);
await this.waitForUrl(/\/dashboard/);

// ⚠️ ACCEPTABLE: State-based waits
await locator.waitFor({ state: 'visible' });

// ❌ AVOID: Arbitrary timeouts
await this.page.waitForTimeout(2000); // Only for debounced inputs
```

### 5. Error Handling

```typescript
// ✅ PREFER: Let Playwright auto-retry fail
async clickButton(): Promise<void> {
  await this.submitButton.click(); // Auto-retries until visible/enabled
}

// ⚠️ ACCEPTABLE: Custom error messages
async assertUserExists(email: string): Promise<void> {
  const user = this.page.getByText(email);
  await expect(user).toBeVisible({
    timeout: 5000,
    message: `User with email "${email}" not found in table`,
  });
}
```

### 6. Test Independence

```typescript
// ✅ Each test is self-contained
test('create user', async ({ adminPage }) => {
  const userPage = new UserManagementPage(adminPage);
  await userPage.goto(); // Fresh navigation
  // ... test actions ...
});

// ❌ AVOID: Shared state between tests
let createdUserId: string; // Global variable
test('create user', async () => {
  createdUserId = await createUser(); // Modifies global
});
test('delete user', async () => {
  await deleteUser(createdUserId); // Depends on previous test
});
```

---

## Testing the POM

### Unit Testing Page Objects (Optional)

```typescript
// __tests__/pages/AuthPage.test.ts
import { test, expect } from '@playwright/test';
import { AuthPage } from '../pages/auth/AuthPage';

test.describe('AuthPage', () => {
  test('login fills form and submits', async ({ page }) => {
    const authPage = new AuthPage(page);
    await authPage.goto();

    await authPage.login({
      email: 'test@example.com',
      password: 'password123',
    });

    // Verify form was filled
    const emailInput = page.getByLabel(/email/i);
    await expect(emailInput).toHaveValue('test@example.com');
  });
});
```

---

## Performance Considerations

### 1. Lazy Locator Evaluation

```typescript
// ✅ Locators are lazy (not evaluated until used)
private get submitButton(): Locator {
  return this.page.getByRole('button', { name: /submit/i });
}

// ❌ AVOID: Eager evaluation (creates locator on construction)
private submitButton = this.page.getByRole('button', { name: /submit/i });
```

### 2. Minimize `waitForTimeout()`

```typescript
// ✅ Use Playwright's auto-waiting
await this.waitForElement(locator);

// ❌ AVOID: Arbitrary timeouts
await this.page.waitForTimeout(2000);
```

### 3. Batch Navigation

```typescript
// ✅ Setup mocks BEFORE navigation
await setupMockAuth(page);
await setupGamesRoutes(page);
await page.goto('/chat'); // Single navigation

// ❌ AVOID: Multiple navigations
await page.goto('/');
await setupMockAuth(page);
await page.goto('/chat'); // Redundant navigation
```

---

## Implementation Phases

### Phase 1: Foundation (Week 1)
- [ ] Create base classes (`BasePage`, `BaseModal`)
- [ ] Implement `AuthPage` with full auth flows
- [ ] Create component objects (`Modal`, `Table`, `Toast`)
- [ ] Extend `fixtures/auth.ts` with POM fixtures

### Phase 2: Core Pages (Week 2)
- [ ] Implement `ChatPage`
- [ ] Implement `EditorPage`
- [ ] Implement `GameListPage`
- [ ] Implement `PdfUploadPage`

### Phase 3: Admin Pages (Week 3)
- [ ] Implement `UserManagementPage`
- [ ] Implement `AnalyticsPage`
- [ ] Implement `PromptManagementPage`
- [ ] Implement `ConfigurationPage`

### Phase 4: Migration (Week 4)
- [ ] Migrate 10 existing tests to POM
- [ ] Document migration patterns
- [ ] Create video tutorial (optional)
- [ ] Update team documentation

---

## Success Metrics

**Target**: 80%+ E2E coverage by 2025-11-30

1. **Code Reuse**: 50% reduction in duplicated selectors
2. **Test Reliability**: 95%+ pass rate in CI
3. **Development Speed**: 2x faster to write new tests
4. **Maintainability**: Single source of truth for selectors
5. **Coverage**: 80%+ critical user journeys covered

---

## References

- **Existing Tests**: `apps/web/e2e/*.spec.ts`
- **Auth Fixture**: `apps/web/e2e/fixtures/auth.ts`
- **Playwright Config**: `apps/web/playwright.config.ts`
- **Issue**: #843 (E2E Test Coverage Expansion)

---

## Appendix: Full Example Test

**File**: `apps/web/e2e/user-journey.spec.ts` (NEW)

```typescript
import { test, expect } from './fixtures/auth';
import { AuthPage } from './pages/auth/AuthPage';
import { ChatPage } from './pages/chat/ChatPage';
import { GameListPage } from './pages/game/GameListPage';
import { DataFactory } from './fixtures/data';

test.describe('Complete User Journey', () => {
  test('user registers, selects game, and asks question', async ({ page }) => {
    // 1. Registration
    const authPage = new AuthPage(page);
    await authPage.goto();

    const userData = DataFactory.createUser();
    await authPage.register(userData);
    await authPage.waitForRegistrationSuccess();

    // 2. Select Game
    const gamePage = new GameListPage(page);
    await gamePage.goto();
    await gamePage.searchGames('Chess');
    await gamePage.assertGameVisible('Chess');
    await gamePage.selectGame('Chess');

    // 3. Ask Question
    const chatPage = new ChatPage(page);
    await chatPage.goto();
    await chatPage.askQuestionAndWait('How do I castle in chess?');

    // 4. Verify Answer
    const answer = await chatPage.getLastAnswer();
    expect(answer).toContain('king');
    expect(answer).toContain('rook');

    await chatPage.assertCitationVisible('Chess Rules');

    // 5. Like Answer
    await chatPage.likeAnswer();
  });
});
```

---

**END OF DOCUMENT**
