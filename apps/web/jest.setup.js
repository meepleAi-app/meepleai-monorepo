import '@testing-library/jest-dom'
import { toHaveNoViolations } from 'jest-axe'

// Extend Jest matchers with jest-axe for accessibility testing
expect.extend(toHaveNoViolations)

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

beforeEach(() => {
  const state = typeof expect !== 'undefined' ? expect.getState?.() : undefined;
  if (state?.testPath?.includes('src/pages/__tests__/admin.test.tsx')) {
    if (!process.env.NEXT_PUBLIC_API_BASE) {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';
    }
  }
});
