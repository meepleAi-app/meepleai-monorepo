/**
 * Test Data Factories and Cleanup Utilities
 * Issue #3082: Implement Missing E2E Test Flows
 *
 * Provides:
 * - Unique test data generation with prefixes
 * - Factory functions for common entities
 * - Automatic cleanup tracking
 * - Test isolation helpers
 *
 * Usage:
 *   const testData = new TestDataManager(apiClient);
 *   const user = await testData.createTestUser({ role: 'Admin' });
 *   // ... run tests ...
 *   await testData.cleanup(); // Removes all created test data
 */

import { ApiClient } from './api-client';

import type {
  User,
  UserRole,
  Game,
  ChatMessage,
} from '../types/pom-interfaces';

// Test data prefix for easy identification and cleanup
const TEST_PREFIX = 'e2e_test_';

/**
 * Generate unique test identifier using crypto
 */
export function generateTestId(): string {
  // Use crypto for unique ID generation (available in Node.js and modern browsers)
  const randomBytes = new Uint8Array(4);
  if (typeof globalThis.crypto !== 'undefined') {
    globalThis.crypto.getRandomValues(randomBytes);
  } else {
    // Fallback for environments without crypto
    for (let i = 0; i < 4; i++) {
      randomBytes[i] = Math.floor(Math.random() * 256);
    }
  }
  const hex = Array.from(randomBytes)
    .map((b) => b.toString(16).padStart(2, '0'))
    .join('');
  return `${TEST_PREFIX}${hex}_${Date.now().toString(36)}`;
}

/**
 * Check if an entity was created by tests (has test prefix)
 */
export function isTestData(identifier: string): boolean {
  return identifier.startsWith(TEST_PREFIX);
}

// ============================================================================
// Data Factories
// ============================================================================

/**
 * User factory - creates unique test users
 */
export const UserFactory = {
  /**
   * Create a test user data object
   */
  create(overrides: Partial<User> = {}): Omit<User, 'id' | 'createdAt'> {
    const testId = generateTestId();
    return {
      email: `${testId}@test.meepleai.dev`,
      displayName: `Test User ${testId}`,
      role: 'User' as UserRole,
      password: 'TestPassword123!',
      isActive: true,
      ...overrides,
    };
  },

  /**
   * Create admin user data
   */
  createAdmin(overrides: Partial<User> = {}): Omit<User, 'id' | 'createdAt'> {
    return this.create({ role: 'Admin', ...overrides });
  },

  /**
   * Create editor user data
   */
  createEditor(overrides: Partial<User> = {}): Omit<User, 'id' | 'createdAt'> {
    return this.create({ role: 'Editor', ...overrides });
  },

  /**
   * Create multiple users
   */
  createMany(count: number, overrides: Partial<User> = {}): Omit<User, 'id' | 'createdAt'>[] {
    return Array.from({ length: count }, () => this.create(overrides));
  },
};

/**
 * Game factory - creates unique test games
 */
export const GameFactory = {
  /**
   * Create a test game data object
   */
  create(overrides: Partial<Game> = {}): Omit<Game, 'id' | 'createdAt' | 'updatedAt'> {
    const testId = generateTestId();
    return {
      title: `Test Game ${testId}`,
      description: `A test game created for E2E testing (${testId})`,
      ...overrides,
    };
  },

  /**
   * Create multiple games
   */
  createMany(count: number, overrides: Partial<Game> = {}): Omit<Game, 'id' | 'createdAt' | 'updatedAt'>[] {
    return Array.from({ length: count }, () => this.create(overrides));
  },
};

/**
 * Chat message factory
 */
export const ChatFactory = {
  /**
   * Create a test chat message
   */
  createMessage(overrides: Partial<ChatMessage> = {}): ChatMessage {
    return {
      question: `Test question ${generateTestId()}`,
      answer: 'This is a test answer from the E2E test suite.',
      sources: [],
      timestamp: new Date(),
      ...overrides,
    };
  },

  /**
   * Create a conversation (array of messages)
   */
  createConversation(messageCount: number = 3): ChatMessage[] {
    return Array.from({ length: messageCount }, (_, i) =>
      this.createMessage({
        question: `Test question ${i + 1}`,
        answer: `Test answer ${i + 1}`,
      })
    );
  },
};

// ============================================================================
// Test Data Manager
// ============================================================================

/**
 * Tracks and manages test data for automatic cleanup
 */
export class TestDataManager {
  private createdUsers: string[] = [];
  private createdGames: string[] = [];
  private createdThreads: string[] = [];
  private originalConfig: Map<string, unknown> = new Map();

  constructor(private apiClient: ApiClient) {}

  // ============================================================================
  // User Operations
  // ============================================================================

  /**
   * Create a test user and track for cleanup
   */
  async createTestUser(overrides: Partial<User> = {}): Promise<User | null> {
    const userData = UserFactory.create(overrides);
    const result = await this.apiClient.createUser(userData);

    if (result.success && result.data?.id) {
      this.createdUsers.push(result.data.id);
      return result.data;
    }

    console.warn(`Failed to create test user: ${result.error}`);
    return null;
  }

  /**
   * Create a test admin user
   */
  async createTestAdmin(overrides: Partial<User> = {}): Promise<User | null> {
    return this.createTestUser({ role: 'Admin', ...overrides });
  }

  /**
   * Create a test editor user
   */
  async createTestEditor(overrides: Partial<User> = {}): Promise<User | null> {
    return this.createTestUser({ role: 'Editor', ...overrides });
  }

  // ============================================================================
  // Game Operations
  // ============================================================================

  /**
   * Create a test game and track for cleanup
   */
  async createTestGame(overrides: Partial<Game> = {}): Promise<Game | null> {
    const gameData = GameFactory.create(overrides);
    const result = await this.apiClient.createGame(gameData);

    if (result.success && result.data?.id) {
      this.createdGames.push(result.data.id);
      return result.data;
    }

    console.warn(`Failed to create test game: ${result.error}`);
    return null;
  }

  /**
   * Create multiple test games
   */
  async createTestGames(count: number, overrides: Partial<Game> = {}): Promise<Game[]> {
    const games: Game[] = [];
    for (let i = 0; i < count; i++) {
      const game = await this.createTestGame(overrides);
      if (game) games.push(game);
    }
    return games;
  }

  // ============================================================================
  // Chat Operations
  // ============================================================================

  /**
   * Create a test chat thread and track for cleanup
   */
  async createTestThread(gameId: string): Promise<string | null> {
    const result = await this.apiClient.createChatThread(gameId);

    if (result.success && result.data?.id) {
      this.createdThreads.push(result.data.id);
      return result.data.id;
    }

    console.warn(`Failed to create test thread: ${result.error}`);
    return null;
  }

  // ============================================================================
  // Configuration Operations
  // ============================================================================

  /**
   * Set configuration and track original value for restoration
   */
  async setConfigWithRestore(key: string, value: string | number | boolean): Promise<boolean> {
    // Get current value first
    const current = await this.apiClient.getConfiguration();
    if (current.success && current.data) {
      const existingItem = current.data.find((item) => item.key === key);
      if (existingItem && !this.originalConfig.has(key)) {
        this.originalConfig.set(key, existingItem.value);
      }
    }

    // Set new value
    const result = await this.apiClient.updateConfiguration(key, value);
    return result.success;
  }

  // ============================================================================
  // Cleanup
  // ============================================================================

  /**
   * Clean up all created test data
   * Call this in afterEach/afterAll hooks
   */
  async cleanup(): Promise<{ success: boolean; errors: string[] }> {
    const errors: string[] = [];

    // Delete chat threads first (may depend on games)
    for (const threadId of this.createdThreads) {
      const result = await this.apiClient.deleteChatThread(threadId);
      if (!result.success) {
        errors.push(`Failed to delete thread ${threadId}: ${result.error}`);
      }
    }
    this.createdThreads = [];

    // Delete games
    for (const gameId of this.createdGames) {
      const result = await this.apiClient.deleteGame(gameId);
      if (!result.success) {
        errors.push(`Failed to delete game ${gameId}: ${result.error}`);
      }
    }
    this.createdGames = [];

    // Delete users last (may be referenced by other entities)
    for (const userId of this.createdUsers) {
      const result = await this.apiClient.deleteUser(userId);
      if (!result.success) {
        errors.push(`Failed to delete user ${userId}: ${result.error}`);
      }
    }
    this.createdUsers = [];

    // Restore original configuration
    const configEntries = Array.from(this.originalConfig.entries());
    for (const [key, value] of configEntries) {
      await this.apiClient.updateConfiguration(key, value as string | number | boolean);
    }
    this.originalConfig.clear();

    return { success: errors.length === 0, errors };
  }

  /**
   * Get count of tracked items
   */
  getTrackedCounts(): { users: number; games: number; threads: number; configs: number } {
    return {
      users: this.createdUsers.length,
      games: this.createdGames.length,
      threads: this.createdThreads.length,
      configs: this.originalConfig.size,
    };
  }

  /**
   * Clear tracking without deleting (use when data was already cleaned up externally)
   */
  clearTracking(): void {
    this.createdUsers = [];
    this.createdGames = [];
    this.createdThreads = [];
    this.originalConfig.clear();
  }
}

// ============================================================================
// Test Isolation Helpers
// ============================================================================

/**
 * Create isolated test context with automatic cleanup
 *
 * Usage:
 *   test('my test', async ({ page }) => {
 *     await withTestData(page, async (api, data) => {
 *       const user = await data.createTestUser();
 *       // ... test code ...
 *     }); // Automatic cleanup
 *   });
 */
export async function withTestData<T>(
  apiClient: ApiClient,
  testFn: (api: ApiClient, data: TestDataManager) => Promise<T>
): Promise<T> {
  const dataManager = new TestDataManager(apiClient);

  try {
    return await testFn(apiClient, dataManager);
  } finally {
    const cleanup = await dataManager.cleanup();
    if (!cleanup.success) {
      console.warn('Test data cleanup had errors:', cleanup.errors);
    }
  }
}

/**
 * Generate unique email for testing
 */
export function generateTestEmail(prefix: string = 'user'): string {
  const uniqueId = generateTestId().replace(TEST_PREFIX, '');
  return `${TEST_PREFIX}${prefix}_${uniqueId}@test.meepleai.dev`;
}

/**
 * Generate unique name for testing
 */
export function generateTestName(prefix: string = 'Test'): string {
  return `${prefix} ${generateTestId()}`;
}

// Export factories and utilities
export { TEST_PREFIX };
