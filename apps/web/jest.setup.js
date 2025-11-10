// Force NODE_ENV to 'test' to ensure React development build is used in CI
process.env.NODE_ENV = 'test';

import '@testing-library/jest-dom'
import { toHaveNoViolations } from 'jest-axe'

// Extend Jest matchers with jest-axe for accessibility testing
expect.extend(toHaveNoViolations)

// Mock Prism.js globally to avoid "Prism is not defined" errors
// Prism.js requires a global Prism object when loading language modules
global.Prism = {
  highlight: jest.fn((code) => code),
  languages: {
    json: {},
  },
  highlightAll: jest.fn(),
  highlightElement: jest.fn(),
};

// Mock prismjs module
jest.mock('prismjs', () => global.Prism, { virtual: true });

// Mock prismjs/components/prism-json
jest.mock('prismjs/components/prism-json', () => ({}), { virtual: true });

// Mock framer-motion to avoid animation issues in tests
jest.mock('framer-motion', () => {
  const React = require('react');

  // Factory function to create mock motion components with proper ref forwarding
  const makeMockComponent = (type) => {
    return React.forwardRef((props, ref) => {
      // Remove all framer-motion specific props and keep only standard HTML props
      const {
        animate,
        initial,
        exit,
        transition,
        whileHover,
        whileTap,
        whileInView,
        viewport,
        drag,
        dragConstraints,
        onAnimationComplete,
        style,
        ...rest
      } = props;
      // Only keep inline styles that are NOT from framer-motion animations
      const cleanStyle = style && typeof style === 'object' && !('opacity' in style && 'transform' in style) ? style : undefined;
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
    AnimatePresence: ({ children, mode }) => {
      // In test environment, render all children immediately without animation delays
      // This ensures form elements are accessible to tests via getByLabelText/getByRole
      return <>{React.Children.map(children, child => child)}</>;
    },
    // Always return true for inView to trigger animations immediately in tests
    useInView: () => [React.useRef(null), true],
    // Disable motion by returning true for reduced motion preference
    useReducedMotion: () => true,
  };
});

// Mock ResizeObserver for Recharts (used in AdminCharts component)
global.ResizeObserver = class ResizeObserver {
  constructor(callback) {
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
  constructor(callback) {
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

// Setup browser API polyfills for test environment (issue #463)
// See test-utils/browser-polyfills.ts for implementation details
import('./src/test-utils/browser-polyfills').then(({ setupBrowserPolyfills }) => {
  setupBrowserPolyfills();
});

// Mock ReadableStream for SSE streaming tests
if (typeof global.ReadableStream === 'undefined') {
  global.ReadableStream = class ReadableStream {
    constructor(underlyingSource) {
      this.underlyingSource = underlyingSource;
      this._chunks = [];
      this._closed = false;

      // Create real controller that captures enqueued chunks
      this.controller = {
        enqueue: (chunk) => {
          this._chunks.push(chunk);
        },
        close: () => {
          this._closed = true;
        },
        error: (err) => {
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
        cancel: jest.fn(),
        releaseLock: jest.fn(),
      };
    }

    cancel() {
      this._closed = true;
      return Promise.resolve();
    }
  };
}

// Mock fetch Response and Headers for API testing (PDF-06)
if (typeof global.Response === 'undefined') {
  global.Response = class Response {
    constructor(body, init = {}) {
      this.body = body;
      this.ok = (init.status || 200) >= 200 && (init.status || 200) < 300;
      this.status = init.status || 200;
      this.statusText = init.statusText || '';

      // Create proper headers object
      this.headers = {
        _map: new Map(),
        get(key) {
          return this._map.get(key.toLowerCase()) || null;
        },
        set(key, value) {
          this._map.set(key.toLowerCase(), value);
        },
        has(key) {
          return this._map.has(key.toLowerCase());
        }
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
  };
}

// Mock FileReader for PDF validation tests
// jsdom's FileReader doesn't properly support readAsArrayBuffer, so we mock it
if (typeof global.FileReader === 'undefined' || !global.FileReader.prototype.readAsArrayBuffer) {
  global.FileReader = class FileReader {
    constructor() {
      this.result = null;
      this.error = null;
      this.onload = null;
      this.onerror = null;
      this.onabort = null;
      this.onloadstart = null;
      this.onloadend = null;
      this.onprogress = null;
      this.readyState = 0; // EMPTY
    }

    readAsArrayBuffer(blob) {
      this.readyState = 1; // LOADING
      if (this.onloadstart) this.onloadstart({ target: this });

      // Simulate async reading
      setTimeout(() => {
        try {
          // For test files, extract the content
          if (blob && blob.size > 0) {
            // Create ArrayBuffer from blob text content
            const text = blob.text ? blob.text() : Promise.resolve('%PDF-1.4');
            text.then(content => {
              const encoder = new TextEncoder();
              this.result = encoder.encode(content).buffer;
              this.readyState = 2; // DONE
              if (this.onload) this.onload({ target: this });
              if (this.onloadend) this.onloadend({ target: this });
            });
          } else {
            this.result = new ArrayBuffer(0);
            this.readyState = 2; // DONE
            if (this.onload) this.onload({ target: this });
            if (this.onloadend) this.onloadend({ target: this });
          }
        } catch (err) {
          this.error = err;
          this.readyState = 2; // DONE
          if (this.onerror) this.onerror({ target: this });
          if (this.onloadend) this.onloadend({ target: this });
        }
      }, 0);
    }

    readAsDataURL(blob) {
      // Simple mock for readAsDataURL
      this.readyState = 1;
      setTimeout(() => {
        this.result = 'data:application/pdf;base64,JVBERi0=';
        this.readyState = 2;
        if (this.onload) this.onload({ target: this });
        if (this.onloadend) this.onloadend({ target: this });
      }, 0);
    }

    readAsText(blob) {
      this.readyState = 1;
      setTimeout(() => {
        this.result = '%PDF-1.4';
        this.readyState = 2;
        if (this.onload) this.onload({ target: this });
        if (this.onloadend) this.onloadend({ target: this });
      }, 0);
    }

    abort() {
      this.readyState = 2;
      if (this.onabort) this.onabort({ target: this });
    }
  };

  // Add static constants
  FileReader.EMPTY = 0;
  FileReader.LOADING = 1;
  FileReader.DONE = 2;
}

// Suppress expected React 19 warnings in tests
const originalError = console.error;
beforeAll(() => {
  console.error = (...args) => {
    // Suppress expected framer-motion prop warnings in React 19
    if (typeof args[0] === 'string' && args[0].includes('React does not recognize the `whileHover` prop')) {
      return;
    }
    // Suppress act() warnings for provider initialization effects
    if (typeof args[0] === 'string' && args[0].includes('An update to ChatProvider inside a test was not wrapped in act(')) {
      return;
    }
    if (typeof args[0] === 'string' && args[0].includes('An update to AnalyticsDashboard inside a test was not wrapped in act(')) {
      return;
    }
    // Suppress JSDOM navigation warnings (known limitation)
    if (typeof args[0] === 'string' && args[0].includes('Error: Not implemented: navigation (except hash changes)')) {
      return;
    }
    originalError.call(console, ...args);
  };
});

afterAll(() => {
  console.error = originalError;
});

beforeEach(() => {
// Mock DOM APIs for TipTap/ProseMirror (TEST-633)
if (typeof Range.prototype.getClientRects === 'undefined') {
  Range.prototype.getClientRects = jest.fn().mockReturnValue([{ bottom: 0, height: 0, left: 0, right: 0, top: 0, width: 0 }]);
}
if (typeof Range.prototype.getBoundingClientRect === 'undefined') {
  Range.prototype.getBoundingClientRect = jest.fn().mockReturnValue({ bottom: 0, height: 0, left: 0, right: 0, top: 0, width: 0, x: 0, y: 0, toJSON: () => ({}) });
}

  const state = typeof expect !== 'undefined' ? expect.getState?.() : undefined;
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
  jest.clearAllTimers();
  // Note: We don't call jest.clearAllMocks() globally as it can interfere with
  // test-specific mocks that are still needed during teardown.
  // Individual test files should clear their own mocks in their afterEach hooks.
});
