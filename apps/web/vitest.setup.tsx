/* eslint-disable @typescript-eslint/no-explicit-any */
/* eslint-disable @typescript-eslint/consistent-type-assertions */
// Force NODE_ENV to 'test' to ensure React development build is used in CI
process.env.NODE_ENV = 'test';

import '@testing-library/jest-dom/vitest';
import { toHaveNoViolations } from 'jest-axe';

// MSW Server Setup for API mocking (Issue #2760)
import { server } from './src/__tests__/mocks/server';

// Start MSW server before all tests
beforeAll(() => {
  server.listen({
    onUnhandledRequest: 'bypass', // Don't fail on unhandled requests (allows real API calls in integration tests)
  });
});

// Reset handlers after each test to ensure test isolation
afterEach(() => {
  server.resetHandlers();
});

// Close server after all tests
afterAll(() => {
  server.close();
});

// Extend Vitest matchers with jest-axe for accessibility testing
expect.extend(toHaveNoViolations);

// Mock Prism.js globally to avoid "Prism is not defined" errors
// Prism.js requires a global Prism object when loading language modules
global.Prism = {
  highlight: vi.fn(code => code),
  languages: {
    json: {},
  },
  highlightAll: vi.fn(),
  highlightElement: vi.fn(),
};

// Mock prismjs module with default export for Vitest compatibility
vi.mock('prismjs', () => ({
  default: global.Prism,
  ...global.Prism,
}));

// Mock prismjs/components/prism-json
vi.mock('prismjs/components/prism-json', () => ({}));

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => {
  const React = require('react');

  // Factory function to create mock motion components with proper ref forwarding
  const makeMockComponent = (type: string) => {
    return React.forwardRef((props: any, ref: any) => {
      // Remove all framer-motion specific props and keep only standard HTML props
      const {
        animate: _animate,
        initial: _initial,
        exit: _exit,
        transition: _transition,
        whileHover: _whileHover,
        whileTap: _whileTap,
        whileInView: _whileInView,
        viewport: _viewport,
        drag: _drag,
        dragConstraints: _dragConstraints,
        onAnimationComplete: _onAnimationComplete,
        style,
        ...rest
      } = props;
      // Only keep inline styles that are NOT from framer-motion animations
      const cleanStyle =
        style && typeof style === 'object' && !('opacity' in style && 'transform' in style)
          ? style
          : undefined;
      return React.createElement(type, { ...rest, style: cleanStyle, ref });
    });
  };

  return {
    motion: {
      div: makeMockComponent('div'),
      button: makeMockComponent('button'),
      a: makeMockComponent('a'),
      span: makeMockComponent('span'),
      p: makeMockComponent('p'),
      h1: makeMockComponent('h1'),
      h2: makeMockComponent('h2'),
      h3: makeMockComponent('h3'),
      section: makeMockComponent('section'),
      article: makeMockComponent('article'),
    },
    // AnimatePresence mock that renders all children immediately
    // This is necessary because jsdom doesn't support requestAnimationFrame
    // and AnimatePresence uses lazy rendering that doesn't work in test environment
    AnimatePresence: ({ children, mode: _mode }: any) => {
      // In test environment, render all children immediately without animation delays
      // This ensures form elements are accessible to tests via getByLabelText/getByRole
      return React.createElement(
        React.Fragment,
        null,
        React.Children.map(children, (child: any) => child)
      );
    },
    // Always return true for inView to trigger animations immediately in tests
    useInView: () => [React.useRef(null), true],
    // Disable motion by returning true for reduced motion preference
    useReducedMotion: () => true,
  };
});

// Mock ResizeObserver for Recharts (used in AdminCharts component)
global.ResizeObserver = class ResizeObserver {
  constructor(callback: any) {
    // @ts-ignore
    this.callback = callback;
  }
  observe() {
    // Mock observe method
  }
  unobserve() {
    // Mock unobserve method
  }
  disconnect() {
    // Mock disconnect method
  }
};

// Mock IntersectionObserver for framer-motion InView (used in index.tsx)
global.IntersectionObserver = class IntersectionObserver {
  constructor(callback: any) {
    // @ts-ignore
    this.callback = callback;
  }
  observe() {
    // Mock observe method
  }
  unobserve() {
    // Mock unobserve method
  }
  disconnect() {
    // Mock disconnect method
  }
  takeRecords() {
    return [];
  }
};

// Mock File.slice() for PDF validation tests (Issue #871, #1141)
// jsdom's File doesn't properly implement slice() which is needed for PDF magic bytes validation
if (typeof File !== 'undefined' && !File.prototype.slice) {
  File.prototype.slice = function (start?: number, end?: number) {
    // Return a Blob-like object that supports arrayBuffer()
    // @ts-ignore
    const content = this.text ? this.text() : Promise.resolve('%PDF-1.4');
    return {
      // @ts-ignore
      size: Math.max(0, (end || this.size) - (start || 0)),
      // @ts-ignore
      type: this.type,
      arrayBuffer: () =>
        content.then((text: string) => {
          const encoder = new TextEncoder();
          const fullBuffer = encoder.encode(text).buffer;
          const sliceStart = start || 0;
          const sliceEnd = Math.min(end || fullBuffer.byteLength, fullBuffer.byteLength);
          return fullBuffer.slice(sliceStart, sliceEnd);
        }),
      text: () => content,
    } as Blob;
  };
}

// Mock Blob.arrayBuffer() for PDF validation tests
if (typeof Blob !== 'undefined' && !Blob.prototype.arrayBuffer) {
  Blob.prototype.arrayBuffer = function () {
    return new Promise(resolve => {
      const reader = new FileReader();
      reader.onload = () => resolve(reader.result as ArrayBuffer);
      reader.readAsArrayBuffer(this);
    });
  };
}

// Setup browser API polyfills for test environment (issue #463)
// See test-utils/browser-polyfills.ts for implementation details
Promise.all([import('./src/test-utils/browser-polyfills')]).then(([{ setupBrowserPolyfills }]) => {
  setupBrowserPolyfills();
});

// Mock TextEncoder/TextDecoder for SSE streaming tests (Issue #1007)
// Node.js has these in util module, but they're not global in Vitest environment
if (typeof global.TextEncoder === 'undefined') {
  const { TextEncoder, TextDecoder } = require('util');
  global.TextEncoder = TextEncoder;
  global.TextDecoder = TextDecoder;
}

// Mock Worker API for Vitest (Issue #1301 - UploadQueueStore lazy init tests)
// jsdom does not provide Worker constructor, so we mock it globally
if (typeof global.Worker === 'undefined') {
  global.Worker = class Worker {
    scriptURL: string | URL;
    onmessage: ((this: Worker, ev: MessageEvent) => any) | null = null;
    onerror: ((this: Worker, ev: ErrorEvent) => any) | null = null;
    onmessageerror: ((this: Worker, ev: MessageEvent) => any) | null = null;

    constructor(scriptURL: string | URL) {
      this.scriptURL = scriptURL;
    }

    postMessage(_message: any) {
      // Mock implementation - tests will override with vi.spyOn()
    }

    terminate() {
      // Mock implementation
    }

    addEventListener(type: string, listener: any) {
      if (type === 'message') {
        this.onmessage = listener;
      } else if (type === 'error') {
        this.onerror = listener;
      }
    }

    removeEventListener(type: string, listener: any) {
      if (type === 'message' && this.onmessage === listener) {
        this.onmessage = null;
      } else if (type === 'error' && this.onerror === listener) {
        this.onerror = null;
      }
    }
  } as any;
}

// Mock ReadableStream for SSE streaming tests
if (typeof global.ReadableStream === 'undefined') {
  global.ReadableStream = class ReadableStream {
    underlyingSource: any;
    controller: any;
    _chunks: any[] = [];
    _closed = false;
    _error: any = null;

    constructor(underlyingSource?: any) {
      this.underlyingSource = underlyingSource;

      // Create real controller that captures enqueued chunks
      this.controller = {
        enqueue: (chunk: any) => {
          this._chunks.push(chunk);
        },
        close: () => {
          this._closed = true;
        },
        error: (err: any) => {
          this._error = err;
        },
      };

      // Start the underlying source
      if (underlyingSource && underlyingSource.start) {
        underlyingSource.start(this.controller);
      }
    }

    getReader() {
      let index = 0;
      const chunks = this._chunks;
      const closed = () => this._closed;
      const error = () => this._error;

      return {
        read: async () => {
          // Check for errors
          if (error()) {
            throw error();
          }

          // Return chunks that have been enqueued
          if (index < chunks.length) {
            return { value: chunks[index++], done: false };
          }

          // If stream is closed and no more chunks, return done
          if (closed()) {
            return { done: true };
          }

          // Wait a bit for more chunks (simulate async behavior)
          await new Promise(resolve => setTimeout(resolve, 10));

          // Check again after waiting
          if (index < chunks.length) {
            return { value: chunks[index++], done: false };
          }

          if (closed()) {
            return { done: true };
          }

          // If still not closed, return done anyway to avoid infinite loop
          return { done: true };
        },
        cancel: vi.fn(),
        releaseLock: vi.fn(),
      };
    }

    cancel() {
      this._closed = true;
      return Promise.resolve();
    }
  } as any;
}

// Mock fetch Response and Headers for API testing (PDF-06)
if (typeof global.Response === 'undefined') {
  global.Response = class Response {
    body: any;
    ok: boolean;
    status: number;
    statusText: string;
    headers: any;

    constructor(body?: any, init: any = {}) {
      this.body = body;
      this.ok = (init.status || 200) >= 200 && (init.status || 200) < 300;
      this.status = init.status || 200;
      this.statusText = init.statusText || '';

      // Create proper headers object
      this.headers = {
        _map: new Map(),
        get(key: string) {
          return this._map.get(key.toLowerCase()) || null;
        },
        set(key: string, value: string) {
          this._map.set(key.toLowerCase(), value);
        },
        has(key: string) {
          return this._map.has(key.toLowerCase());
        },
      };

      if (init.headers) {
        Object.entries(init.headers).forEach(([key, value]) => {
          this.headers.set(key, value);
        });
      }
    }

    json() {
      return Promise.resolve(this.body ? JSON.parse(this.body) : {});
    }

    text() {
      return Promise.resolve(this.body || '');
    }
  } as any;
}

// Mock global fetch for API testing
// jsdom doesn't provide fetch, so we need to mock it for components that use API client
if (typeof global.fetch === 'undefined') {
  global.fetch = vi.fn((_url: string, _options?: any) => {
    // Default mock implementation returns successful empty response
    // Individual tests should override this with vi.fn() or test-specific implementations
    return Promise.resolve(
      new global.Response(JSON.stringify({}), {
        status: 200,
        statusText: 'OK',
        headers: { 'content-type': 'application/json' },
      })
    );
  });
}

// Mock FileReader for PDF validation tests
// jsdom's FileReader doesn't properly support readAsArrayBuffer, so we mock it
if (typeof global.FileReader === 'undefined' || !global.FileReader.prototype.readAsArrayBuffer) {
  global.FileReader = class FileReader {
    result: any = null;
    error: any = null;
    onload: ((this: FileReader, ev: ProgressEvent<FileReader>) => any) | null = null;
    onerror: ((this: FileReader, ev: ProgressEvent<FileReader>) => any) | null = null;
    onabort: ((this: FileReader, ev: ProgressEvent<FileReader>) => any) | null = null;
    onloadstart: ((this: FileReader, ev: ProgressEvent<FileReader>) => any) | null = null;
    onloadend: ((this: FileReader, ev: ProgressEvent<FileReader>) => any) | null = null;
    onprogress: ((this: FileReader, ev: ProgressEvent<FileReader>) => any) | null = null;
    readyState = 0; // EMPTY

    readAsArrayBuffer(blob: Blob) {
      this.readyState = 1; // LOADING
      if (this.onloadstart) this.onloadstart({ target: this } as any);

      // Simulate async reading
      setTimeout(() => {
        try {
          // For test files, extract the content
          if (blob && blob.size > 0) {
            // Create ArrayBuffer from blob text content
            const text = (blob as any).text ? (blob as any).text() : Promise.resolve('%PDF-1.4');
            text.then((content: string) => {
              const encoder = new TextEncoder();
              this.result = encoder.encode(content).buffer;
              this.readyState = 2; // DONE
              if (this.onload) this.onload({ target: this } as any);
              if (this.onloadend) this.onloadend({ target: this } as any);
            });
          } else {
            this.result = new ArrayBuffer(0);
            this.readyState = 2; // DONE
            if (this.onload) this.onload({ target: this } as any);
            if (this.onloadend) this.onloadend({ target: this } as any);
          }
        } catch (err) {
          this.error = err;
          this.readyState = 2; // DONE
          if (this.onerror) this.onerror({ target: this } as any);
          if (this.onloadend) this.onloadend({ target: this } as any);
        }
      }, 0);
    }

    readAsDataURL(_blob: Blob) {
      // Simple mock for readAsDataURL
      this.readyState = 1;
      setTimeout(() => {
        this.result = 'data:application/pdf;base64,JVBERi0=';
        this.readyState = 2;
        if (this.onload) this.onload({ target: this } as any);
        if (this.onloadend) this.onloadend({ target: this } as any);
      }, 0);
    }

    readAsText(_blob: Blob) {
      this.readyState = 1;
      setTimeout(() => {
        this.result = '%PDF-1.4';
        this.readyState = 2;
        if (this.onload) this.onload({ target: this } as any);
        if (this.onloadend) this.onloadend({ target: this } as any);
      }, 0);
    }

    abort() {
      this.readyState = 2;
      if (this.onabort) this.onabort({ target: this } as any);
    }

    // Static constants
    static EMPTY = 0;
    static LOADING = 1;
    static DONE = 2;
  } as any;
}

// Suppress expected React 19 warnings in tests
const originalError = console.error;
const originalWarn = console.warn;

const isRadixDialogMessage = (message: unknown) =>
  typeof message === 'string' &&
  (message.includes('`DialogContent` requires a `DialogTitle`') ||
    message.includes('Missing `Description` or `aria-describedby={undefined}`'));

console.warn = (...args: any[]) => {
  if (isRadixDialogMessage(args[0])) {
    return;
  }
  originalWarn.call(console, ...args);
};
beforeAll(() => {
  console.error = (...args: any[]) => {
    if (isRadixDialogMessage(args[0])) {
      return;
    }
    // Suppress expected framer-motion prop warnings in React 19
    if (
      typeof args[0] === 'string' &&
      args[0].includes('React does not recognize the `whileHover` prop')
    ) {
      return;
    }
    // Suppress act() warnings for provider initialization effects
    if (
      typeof args[0] === 'string' &&
      args[0].includes('An update to ChatProvider inside a test was not wrapped in act')
    ) {
      return;
    }
    if (
      typeof args[0] === 'string' &&
      args[0].includes('An update to AnalyticsDashboard inside a test was not wrapped in act')
    ) {
      return;
    }
    if (
      typeof args[0] === 'string' &&
      args[0].includes('An update to GameProvider inside a test was not wrapped in act')
    ) {
      return;
    }
    if (
      typeof args[0] === 'string' &&
      args[0].includes('An update to AuthProvider inside a test was not wrapped in act')
    ) {
      return;
    }
    if (
      typeof args[0] === 'string' &&
      args[0].includes('An update to CacheDashboard inside a test was not wrapped in act')
    ) {
      return;
    }
    // Suppress all other component act() warnings (provider mount/unmount effects)
    if (typeof args[0] === 'string' && args[0].includes('inside a test was not wrapped in act')) {
      return;
    }
    // Suppress JSDOM navigation warnings (known limitation)
    if (
      typeof args[0] === 'string' &&
      args[0].includes('Error: Not implemented: navigation (except hash changes)')
    ) {
      return;
    }
    originalError.call(console, ...args);
  };
});

afterAll(() => {
  console.error = originalError;
  console.warn = originalWarn;
});

beforeEach(() => {
  // Mock DOM APIs for TipTap/ProseMirror (TEST-633)
  if (typeof Range.prototype.getClientRects === 'undefined') {
    Range.prototype.getClientRects = vi
      .fn()
      .mockReturnValue([{ bottom: 0, height: 0, left: 0, right: 0, top: 0, width: 0 }]);
  }
  if (typeof Range.prototype.getBoundingClientRect === 'undefined') {
    Range.prototype.getBoundingClientRect = vi.fn().mockReturnValue({
      bottom: 0,
      height: 0,
      left: 0,
      right: 0,
      top: 0,
      width: 0,
      x: 0,
      y: 0,
      toJSON: () => ({}),
    });
  }

  // Mock Pointer Capture API for Radix UI components (Select, Dropdown, etc.)
  // jsdom doesn't implement these methods which Radix UI requires
  if (typeof Element.prototype.hasPointerCapture === 'undefined') {
    Element.prototype.hasPointerCapture = vi.fn().mockReturnValue(false);
  }
  if (typeof Element.prototype.setPointerCapture === 'undefined') {
    Element.prototype.setPointerCapture = vi.fn();
  }
  if (typeof Element.prototype.releasePointerCapture === 'undefined') {
    Element.prototype.releasePointerCapture = vi.fn();
  }

  // Reset fetch mock before each test to prevent cross-test pollution
  if (global.fetch && (global.fetch as any).mockClear) {
    (global.fetch as any).mockClear();
  }

  const state = typeof expect !== 'undefined' ? (expect as any).getState?.() : undefined;
  if (state?.testPath?.includes('src/pages/__tests__/admin.test.tsx')) {
    if (!process.env.NEXT_PUBLIC_API_BASE) {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';
    }
  }
});

// Global cleanup after each test to prevent mock pollution between test files
// This is critical for test isolation, especially for fetch mocks that cause timeouts
afterEach(() => {
  // Clear all timers to prevent polling/timeout issues between tests
  vi.clearAllTimers();
  // Note: We don't call vi.clearAllMocks() globally as it can interfere with
  // test-specific mocks that are still needed during teardown.
  // Individual test files should clear their own mocks in their afterEach hooks.
});
