/**
 * Page Object Model Type Definitions
 *
 * Centralized TypeScript interfaces for all POM classes.
 * Ensures type safety and consistency across test suite.
 */

import { Page, Locator } from '@playwright/test';

// ============================================================================
// Base Interfaces
// ============================================================================

export interface WaitOptions {
  timeout?: number;
  state?: 'attached' | 'detached' | 'visible' | 'hidden';
}

export interface ClickOptions {
  force?: boolean;
  timeout?: number;
}

export interface NavigationOptions {
  waitUntil?: 'load' | 'domcontentloaded' | 'networkidle';
  timeout?: number;
}

// ============================================================================
// Auth Domain
// ============================================================================

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
  code?: string;         // TOTP 6-digit code
  backupCode?: string;   // Backup code (XXXX-XXXX format)
}

export interface PasswordResetData {
  email: string;
  newPassword?: string;  // After clicking email link
}

export interface OAuthProvider {
  type: 'google' | 'discord' | 'github';
  displayName: string;
}

// ============================================================================
// Chat Domain
// ============================================================================

export interface ChatMessage {
  question: string;
  answer?: string;
  sources?: ChatSource[];
  timestamp?: Date;
}

export interface ChatSource {
  title: string;
  snippet?: string;
  page?: number;
  url?: string;
}

export interface ChatExportOptions {
  format: 'json' | 'txt';
  filename?: string;
}

export interface ChatFeedback {
  messageId: string;
  helpful: boolean;
  comment?: string;
}

// ============================================================================
// Game Domain
// ============================================================================

export interface Game {
  id: string;
  name: string;
  description?: string;
  createdAt?: string;
  updatedAt?: string;
  pdfCount?: number;
}

export interface GameSearchFilters {
  query?: string;
  sortBy?: 'name' | 'createdAt' | 'updatedAt';
  sortOrder?: 'asc' | 'desc';
}

export interface PdfDocument {
  id: string;
  filename: string;
  size: number;
  uploadedAt: string;
  status: 'processing' | 'completed' | 'failed';
}

export interface PdfUploadOptions {
  file: string | Buffer;
  filename: string;
  gameId: string;
  validate?: boolean;
}

// ============================================================================
// Editor Domain
// ============================================================================

export type EditorMode = 'rich-text' | 'json';

export interface RuleSpecData {
  gameId: string;
  version: string;
  createdAt: string;
  rules: any[];  // Schema-specific
}

export interface EditorToolbarButton {
  action: 'bold' | 'italic' | 'heading1' | 'heading2' | 'bulletList' | 'orderedList' | 'link' | 'code';
  shortcut?: string;
}

export interface VersionHistoryEntry {
  version: string;
  createdAt: string;
  createdBy: string;
  changes?: string;
}

export interface EditorAutoSaveStatus {
  enabled: boolean;
  lastSaved?: Date;
  debounceMs: number;
}

// ============================================================================
// Admin Domain
// ============================================================================

export interface User {
  id?: string;
  email: string;
  displayName: string;
  role: UserRole;
  password?: string;  // Only for creation
  createdAt?: string;
  lastSeenAt?: string | null;
  isActive?: boolean;
}

export type UserRole = 'Admin' | 'Editor' | 'User';

export interface UserSearchFilters {
  search?: string;
  role?: UserRole | 'all';
  sortBy?: 'email' | 'displayName' | 'role' | 'createdAt';
  sortOrder?: 'asc' | 'desc';
  page?: number;
  limit?: number;
}

export interface UserBulkAction {
  action: 'delete' | 'changeRole' | 'activate' | 'deactivate';
  userIds: string[];
  targetRole?: UserRole;  // For changeRole action
}

export interface AnalyticsMetric {
  name: string;
  value: number;
  change?: number;  // Percentage change
  trend?: 'up' | 'down' | 'stable';
}

export interface AnalyticsChartData {
  type: 'line' | 'bar' | 'pie';
  labels: string[];
  datasets: {
    label: string;
    data: number[];
  }[];
}

export interface PromptTemplate {
  id?: string;
  name: string;
  category: string;
  content: string;
  variables?: string[];
  isActive?: boolean;
  version?: number;
  createdAt?: string;
  updatedAt?: string;
}

export interface PromptVersion {
  id: string;
  version: number;
  content: string;
  createdAt: string;
  createdBy: string;
  changelog?: string;
  isActive: boolean;
}

export interface ConfigurationItem {
  key: string;
  value: string | number | boolean;
  valueType: 'string' | 'int' | 'long' | 'double' | 'bool' | 'json';
  category: string;
  environment: 'Development' | 'Staging' | 'Production' | 'All';
  description?: string;
  isActive: boolean;
}

// ============================================================================
// Component Interfaces
// ============================================================================

export interface ModalConfig {
  title?: string;
  closable?: boolean;
  confirmText?: string;
  cancelText?: string;
  width?: string;
}

export interface TableColumn {
  name: string;
  key: string;
  sortable?: boolean;
  filterable?: boolean;
}

export interface TableSort {
  column: string;
  order: 'asc' | 'desc';
}

export interface TablePagination {
  page: number;
  pageSize: number;
  total: number;
}

export interface FormField {
  label: string;
  type: 'text' | 'email' | 'password' | 'textarea' | 'select' | 'checkbox';
  value?: string;
  required?: boolean;
  validation?: RegExp;
}

export interface FormValidationError {
  field: string;
  message: string;
}

export interface ToastNotification {
  type: 'success' | 'error' | 'warning' | 'info';
  message: string;
  duration?: number;  // milliseconds
}

export interface FileUploadConfig {
  accept?: string[];  // MIME types
  maxSize?: number;   // bytes
  multiple?: boolean;
}

// ============================================================================
// Test Data Factory Interfaces
// ============================================================================

export interface UserFactory {
  createUser(overrides?: Partial<User>): User;
  createAdminUser(overrides?: Partial<User>): User;
  createEditorUser(overrides?: Partial<User>): User;
  reset(): void;
}

export interface GameFactory {
  createGame(overrides?: Partial<Game>): Game;
  createGames(count: number): Game[];
  reset(): void;
}

export interface ChatFactory {
  createMessage(overrides?: Partial<ChatMessage>): ChatMessage;
  createConversation(messageCount: number): ChatMessage[];
  reset(): void;
}

// ============================================================================
// Fixture Interfaces
// ============================================================================

export interface AuthFixture {
  adminPage: Page;
  editorPage: Page;
  userPage: Page;
  setupUserPage: Page;
}

export interface CleanupHelper {
  clearSession(): Promise<void>;
  logout(): Promise<void>;
  clearCache(): Promise<void>;
}

// ============================================================================
// API Mock Interfaces
// ============================================================================

export interface MockRoute {
  url: string | RegExp;
  method: 'GET' | 'POST' | 'PUT' | 'DELETE' | 'PATCH';
  status: number;
  body: any;
  headers?: Record<string, string>;
}

export interface MockAuthResponse {
  user: {
    id: string;
    email: string;
    displayName: string;
    role: UserRole;
  };
  expiresAt: string;
}

export interface MockApiConfig {
  baseUrl: string;
  defaultDelay?: number;  // milliseconds
  enableCors?: boolean;
}

// ============================================================================
// Page Object Base Interfaces
// ============================================================================

export interface IBasePage {
  readonly page: Page;
  goto(): Promise<void>;
  waitForLoad(): Promise<void>;
  waitForElement(locator: Locator, options?: WaitOptions): Promise<void>;
  waitForElementToDisappear(locator: Locator, options?: WaitOptions): Promise<void>;
  waitForUrl(pattern: string | RegExp): Promise<void>;
  screenshot(name: string): Promise<void>;
  getUrl(): string;
  isVisible(locator: Locator): Promise<boolean>;
  fill(locator: Locator, value: string): Promise<void>;
  click(locator: Locator, options?: ClickOptions): Promise<void>;
  selectOption(locator: Locator, value: string): Promise<void>;
  uploadFile(locator: Locator, filePath: string): Promise<void>;
}

export interface IBaseModal extends IBasePage {
  getModalLocator(): Locator;
  waitForOpen(): Promise<void>;
  waitForClose(): Promise<void>;
  cancel(): Promise<void>;
  confirm(): Promise<void>;
  close(): Promise<void>;
}

// ============================================================================
// Page Object Method Interfaces
// ============================================================================

export interface IAuthPage extends IBasePage {
  // Login
  login(credentials: LoginCredentials): Promise<void>;
  waitForLoginSuccess(): Promise<void>;
  loginAndWait(credentials: LoginCredentials): Promise<void>;

  // Registration
  register(data: RegistrationData): Promise<void>;
  waitForRegistrationSuccess(): Promise<void>;

  // OAuth
  clickOAuthButton(provider: 'google' | 'discord' | 'github'): Promise<void>;

  // 2FA
  verify2FA(data: TwoFactorData): Promise<void>;
  waitFor2FAPrompt(): Promise<void>;

  // Logout
  logout(): Promise<void>;
  waitForLogoutSuccess(): Promise<void>;

  // Password Reset
  clickForgotPassword(): Promise<void>;

  // Assertions
  assertLoginFormVisible(): Promise<void>;
  assertRegisterFormVisible(): Promise<void>;
  assertValidationError(message: string | RegExp): Promise<void>;
}

export interface IChatPage extends IBasePage {
  // Actions
  askQuestion(question: string): Promise<void>;
  waitForAnswer(): Promise<void>;
  askQuestionAndWait(question: string): Promise<void>;
  getLastAnswer(): Promise<string>;
  getCitations(): Promise<ChatSource[]>;
  clickCitation(index: number): Promise<void>;
  likeAnswer(): Promise<void>;
  dislikeAnswer(): Promise<void>;
  editMessage(messageIndex: number, newText: string): Promise<void>;
  deleteMessage(messageIndex: number): Promise<void>;
  exportConversation(format: 'json' | 'txt'): Promise<void>;
  selectGame(gameName: string): Promise<void>;

  // Assertions
  assertChatPageVisible(): Promise<void>;
  assertLoginRequired(): Promise<void>;
  assertAnswerContains(text: string): Promise<void>;
  assertCitationVisible(title: string): Promise<void>;
}

export interface IEditorPage extends IBasePage {
  // Mode switching
  switchToRichTextMode(): Promise<void>;
  switchToJsonMode(): Promise<void>;
  getCurrentMode(): Promise<EditorMode>;

  // JSON mode
  setJsonContent(json: string): Promise<void>;
  getJsonContent(): Promise<string>;

  // Rich text mode
  applyBold(): Promise<void>;
  applyItalic(): Promise<void>;
  applyHeading1(): Promise<void>;
  applyBulletList(): Promise<void>;

  // Undo/Redo
  undo(): Promise<void>;
  redo(): Promise<void>;

  // Save
  save(): Promise<void>;
  waitForAutoSave(): Promise<void>;

  // Navigation
  goToVersionHistory(): Promise<void>;
  goToHome(): Promise<void>;

  // Assertions
  assertEditorVisible(): Promise<void>;
  assertUnsavedChanges(): Promise<void>;
  assertNoUnsavedChanges(): Promise<void>;
  assertValidationError(): Promise<void>;
  assertSaveButtonEnabled(): Promise<void>;
  assertSaveButtonDisabled(): Promise<void>;
  assertToolbarVisible(): Promise<void>;
}

export interface IGameListPage extends IBasePage {
  // Actions
  searchGames(query: string): Promise<void>;
  getGameCount(): Promise<number>;
  selectGame(gameName: string): Promise<void>;
  clickUploadPDF(): Promise<void>;

  // Assertions
  assertGameVisible(gameName: string): Promise<void>;
  assertGameNotVisible(gameName: string): Promise<void>;
}

export interface IUserManagementPage extends IBasePage {
  // Create
  clickCreateUser(): Promise<void>;
  fillCreateUserForm(user: User): Promise<void>;
  submitUserForm(): Promise<void>;
  createUser(user: User): Promise<void>;

  // Edit
  clickEditUser(email: string): Promise<void>;
  updateUser(currentEmail: string, updates: Partial<User>): Promise<void>;

  // Delete
  deleteUser(email: string): Promise<void>;

  // Search & Filter
  searchUsers(query: string): Promise<void>;
  filterByRole(role: UserRole | 'all'): Promise<void>;

  // Bulk operations
  selectUserCheckbox(email: string): Promise<void>;
  clickBulkDelete(): Promise<void>;

  // Assertions
  assertUserVisible(email: string): Promise<void>;
  assertUserNotVisible(email: string): Promise<void>;
  assertUserCount(count: number): Promise<void>;
}

// ============================================================================
// Component Method Interfaces
// ============================================================================

export interface IModal {
  readonly modal: Locator;
  waitForOpen(): Promise<void>;
  waitForClose(): Promise<void>;
  close(): Promise<void>;
  clickButton(name: string | RegExp): Promise<void>;
  cancel(): Promise<void>;
  confirm(): Promise<void>;
  fillInput(label: string | RegExp, value: string): Promise<void>;
  selectOption(label: string | RegExp, value: string): Promise<void>;
}

export interface ITable {
  readonly table: Locator;
  readonly rows: Locator;
  getRowCount(): Promise<number>;
  getRowByText(text: string): Locator;
  getCellValue(rowIndex: number, columnName: string): Promise<string>;
  clickColumnHeader(columnName: string): Promise<void>;
  sortByColumn(columnName: string, order: 'asc' | 'desc'): Promise<void>;
  getSortOrder(columnName: string): Promise<'asc' | 'desc' | null>;
  goToNextPage(): Promise<void>;
  goToPreviousPage(): Promise<void>;
  getCurrentPage(): Promise<number>;
  selectRow(rowIndex: number): Promise<void>;
  deselectRow(rowIndex: number): Promise<void>;
  selectAllRows(): Promise<void>;
}

export interface IToast {
  waitForToast(type?: ToastNotification['type']): Promise<void>;
  waitForToastToDisappear(): Promise<void>;
  getToastMessage(): Promise<string>;
  assertToastMessage(message: string | RegExp): Promise<void>;
  closeToast(): Promise<void>;
}

export interface IForm {
  fillField(field: FormField): Promise<void>;
  fillForm(fields: FormField[]): Promise<void>;
  submit(): Promise<void>;
  reset(): Promise<void>;
  getValidationErrors(): Promise<FormValidationError[]>;
  assertFieldError(fieldLabel: string, message: string | RegExp): Promise<void>;
}

export interface IFileUpload {
  selectFile(filePath: string): Promise<void>;
  selectFiles(filePaths: string[]): Promise<void>;
  getSelectedFileNames(): Promise<string[]>;
  clearSelection(): Promise<void>;
  waitForUploadComplete(): Promise<void>;
  getUploadProgress(): Promise<number>;  // 0-100
  assertUploadSuccess(): Promise<void>;
  assertUploadError(message: string | RegExp): Promise<void>;
}

export interface IMonacoEditor {
  setValue(content: string): Promise<void>;
  getValue(): Promise<string>;
  insertText(text: string, position?: { line: number; column: number }): Promise<void>;
  selectAll(): Promise<void>;
  undo(): Promise<void>;
  redo(): Promise<void>;
  format(): Promise<void>;
  getLineCount(): Promise<number>;
  assertNoErrors(): Promise<void>;
  assertError(line: number, message: string | RegExp): Promise<void>;
}

// ============================================================================
// Utility Type Helpers
// ============================================================================

/**
 * Extract locator properties from a class
 */
export type LocatorProperties<T> = {
  [K in keyof T]: T[K] extends Locator ? K : never;
}[keyof T];

/**
 * Extract method names from a class
 */
export type MethodNames<T> = {
  [K in keyof T]: T[K] extends (...args: any[]) => any ? K : never;
}[keyof T];

/**
 * Make certain properties optional
 */
export type PartialBy<T, K extends keyof T> = Omit<T, K> & Partial<Pick<T, K>>;

/**
 * Make certain properties required
 */
export type RequiredBy<T, K extends keyof T> = Omit<T, K> & Required<Pick<T, K>>;

// ============================================================================
// Test Configuration Types
// ============================================================================

export interface PomTestConfig {
  baseUrl: string;
  apiBaseUrl: string;
  timeout: number;
  retries: number;
  parallel: boolean;
  screenshots: 'on' | 'off' | 'only-on-failure';
  trace: 'on' | 'off' | 'on-first-retry' | 'retain-on-failure';
}

export interface MockDataConfig {
  enableMocks: boolean;
  mockDelay: number;
  seedData: {
    users: User[];
    games: Game[];
  };
}

// ============================================================================
// Type Guards
// ============================================================================

export function isUser(obj: any): obj is User {
  return (
    typeof obj === 'object' &&
    typeof obj.email === 'string' &&
    typeof obj.displayName === 'string' &&
    ['Admin', 'Editor', 'User'].includes(obj.role)
  );
}

export function isGame(obj: any): obj is Game {
  return (
    typeof obj === 'object' &&
    typeof obj.id === 'string' &&
    typeof obj.name === 'string'
  );
}

export function isChatMessage(obj: any): obj is ChatMessage {
  return (
    typeof obj === 'object' &&
    typeof obj.question === 'string'
  );
}

export function isValidEmail(email: string): boolean {
  return /^[^\s@]+@[^\s@]+\.[^\s@]+$/.test(email);
}

export function isValidPassword(password: string): boolean {
  // At least 8 characters, 1 uppercase, 1 lowercase, 1 digit
  return /^(?=.*[a-z])(?=.*[A-Z])(?=.*\d).{8,}$/.test(password);
}
