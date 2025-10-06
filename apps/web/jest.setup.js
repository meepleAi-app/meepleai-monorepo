import '@testing-library/jest-dom'

beforeEach(() => {
  const state = typeof expect !== 'undefined' ? expect.getState?.() : undefined;
  if (state?.testPath?.includes('src/pages/__tests__/admin.test.tsx')) {
    if (!process.env.NEXT_PUBLIC_API_BASE) {
      process.env.NEXT_PUBLIC_API_BASE = 'https://api.example.com';
    }
  }
});
