/**
 * HTTP Client Test Helpers
 *
 * Shared mocks, utilities, and setup functions for httpClient tests.
 */

import { HttpClient } from '../core/httpClient';
import { globalRequestCache } from '../core/requestCache';
import { resetAllCircuits } from '../core/circuitBreaker';

export interface MockStorages {
  localStorage: { [key: string]: string };
  sessionStorage: { [key: string]: string };
}

export interface TestSetup {
  mockFetch: Mock;
  client: HttpClient;
  storages: MockStorages;
}

/**
 * Sets up test environment with mocked fetch, client, and browser APIs.
 */
export function setupTestEnvironment(): TestSetup {
  // Clear request cache before each test
  globalRequestCache.clear();
  // Reset circuit breaker to prevent test interference
  resetAllCircuits();

  const mockFetch = vi.fn();
  const client = new HttpClient({ baseUrl: 'http://localhost:8080', fetchImpl: mockFetch });

  // Mock browser storage
  const mockLocalStorage: { [key: string]: string } = {};
  const mockSessionStorage: { [key: string]: string } = {};

  // Mock sessionStorage globally (for both browser and Node.js test environments)
  Object.defineProperty(global, 'sessionStorage', {
    value: {
      getItem: vi.fn((key: string) => mockSessionStorage[key] || null),
      setItem: vi.fn((key: string, value: string) => {
        mockSessionStorage[key] = value;
      }),
      removeItem: vi.fn((key: string) => {
        delete mockSessionStorage[key];
      }),
      clear: vi.fn(() => {
        Object.keys(mockSessionStorage).forEach(key => delete mockSessionStorage[key]);
      }),
    },
    writable: true,
    configurable: true,
  });

  // Mock localStorage globally (for both browser and Node.js test environments)
  Object.defineProperty(global, 'localStorage', {
    value: {
      getItem: vi.fn((key: string) => mockLocalStorage[key] || null),
      setItem: vi.fn((key: string, value: string) => {
        mockLocalStorage[key] = value;
      }),
    },
    writable: true,
    configurable: true,
  });

  // If window exists (jsdom), also set on window
  if (typeof window !== 'undefined') {
    Object.defineProperty(window, 'localStorage', {
      value: global.localStorage,
      writable: true,
    });

    Object.defineProperty(window, 'sessionStorage', {
      value: global.sessionStorage,
      writable: true,
    });
  }

  // Mock URL APIs
  if (typeof URL.createObjectURL !== 'function') {
    URL.createObjectURL = () => 'blob:mock';
  }
  if (typeof URL.revokeObjectURL !== 'function') {
    URL.revokeObjectURL = () => {};
  }

  // Mock crypto.randomUUID
  Object.defineProperty(global, 'crypto', {
    value: {
      randomUUID: vi.fn(() => 'test-uuid-123'),
    },
    writable: true,
  });

  return {
    mockFetch,
    client,
    storages: {
      localStorage: mockLocalStorage,
      sessionStorage: mockSessionStorage,
    },
  };
}

/**
 * Creates a successful mock response.
 */
export function createSuccessResponse(data: any, status = 200) {
  return {
    ok: true,
    status,
    json: async () => data,
    headers: new Headers(),
  };
}

/**
 * Creates an error mock response.
 */
export function createErrorResponse(status: number, error: any, correlationId?: string) {
  const headers = new Headers();
  if (correlationId) {
    headers.set('X-Correlation-Id', correlationId);
  }

  return {
    ok: false,
    status,
    json: async () => error,
    headers,
  };
}

/**
 * Creates a blob response for file downloads.
 */
export function createBlobResponse(content: string, filename?: string) {
  const blob = new Blob([content], { type: 'application/octet-stream' });
  const headers = new Headers();

  if (filename) {
    headers.set('Content-Disposition', `attachment; filename="${filename}"`);
  }

  return {
    ok: true,
    status: 200,
    blob: async () => blob,
    headers,
  };
}

/**
 * Creates a 204 No Content response.
 */
export function createNoContentResponse() {
  return {
    ok: true,
    status: 204,
    headers: new Headers(),
  };
}

/**
 * Mocks document.createElement for download tests.
 */
export function setupDownloadMocks() {
  const mockLink = {
    href: '',
    download: '',
    click: vi.fn(),
  };

  const mockCreateElement = vi.spyOn(document, 'createElement').mockReturnValue(mockLink as any);
  // jsdom 27+ requires appendChild/removeChild to return the node passed as argument
  const mockAppendChild = vi.spyOn(document.body, 'appendChild').mockImplementation(node => node);
  const mockRemoveChild = vi.spyOn(document.body, 'removeChild').mockImplementation(node => node);
  const mockCreateObjectURL = vi.spyOn(URL, 'createObjectURL').mockReturnValue('blob:test-url');
  const mockRevokeObjectURL = vi.spyOn(URL, 'revokeObjectURL').mockImplementation();

  return {
    mockLink,
    mockCreateElement,
    mockAppendChild,
    mockRemoveChild,
    mockCreateObjectURL,
    mockRevokeObjectURL,
    cleanup: () => {
      mockCreateElement.mockRestore();
      mockAppendChild.mockRestore();
      mockRemoveChild.mockRestore();
      mockCreateObjectURL.mockRestore();
      mockRevokeObjectURL.mockRestore();
    },
  };
}
