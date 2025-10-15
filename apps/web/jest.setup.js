import '@testing-library/jest-dom'

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
