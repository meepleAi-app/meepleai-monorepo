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

beforeEach(() => {
  const state = typeof expect !== 'undefined' ? expect.getState?.() : undefined;
  if (state?.testPath?.includes('src/pages/__tests__/admin.test.tsx')) {
    if (!process.env.NEXT_PUBLIC_API_BASE) {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';
    }
  }
});
