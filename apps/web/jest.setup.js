import '@testing-library/jest-dom'

// Mock framer-motion to avoid animation issues in tests
jest.mock('framer-motion', () => {
  const React = require('react');

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
    AnimatePresence: ({ children }) => <>{children}</>,
    useInView: () => [React.useRef(null), true], // Always return true for inView
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
