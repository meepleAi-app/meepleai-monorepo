// Ensure NODE_ENV is set to 'test' before module resolution
// This prevents React from using production builds during testing
process.env.NODE_ENV = 'test';

const nextJest = require('next/jest')

const createJestConfig = nextJest({
  // Provide the path to your Next.js app to load next.config.js and .env files in your test environment
  dir: './',
})

// Add any custom config to be passed to Jest
const customJestConfig = {
  setupFilesAfterEnv: ['<rootDir>/jest.setup.js'],
  globalTeardown: '<rootDir>/jest.teardown.js',
  testEnvironment: 'jest-environment-jsdom',
  testTimeout: 30000, // Global timeout 30s for slow worker/async tests
  coverageProvider: 'v8', // Fix babel-plugin-istanbul schema error (test-exclude@6.0.0)
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
  },
  collectCoverageFrom: [
    'src/**/*.{js,jsx,ts,tsx}',
    '!src/**/*.d.ts',
    '!src/**/__tests__/fixtures/**',
    '!src/**/__tests__/utils/**',
    '!src/test-utils/**',
    '!src/**/test-utils.{ts,tsx}',
    '!src/**/chat-test-utils.{ts,tsx}',
    '!src/**/*.worker.{js,jsx,ts,tsx}',
    '!src/workers/**',
  ],
  coverageThreshold: {
    global: {
      branches: 90,
      functions: 90,
      lines: 90,
      statements: 90,
    },
  },
  testMatch: [
    '**/__tests__/**/*.{test,spec}.[jt]s?(x)',
    '**/?(*.)+(spec|test).[jt]s?(x)',
  ],
  testPathIgnorePatterns: [
    '/node_modules/',
    '/e2e/',
    '/.next/',
    '/__tests__/fixtures/',
    '/__tests__/utils/(?!__tests__/)', // Exclude utility files but not their tests
    '/test-utils\\.(ts|tsx)$', // Utility file, not a test file
    '/chat-test-utils\\.(ts|tsx)$', // Chat test utility file, not a test file
    '/async-test-helpers\\.(ts|tsx)$', // Async test helper utility
    '/query-test-utils\\.(ts|tsx)$', // Query test utility
    '/zustand-test-utils\\.(ts|tsx)$', // Zustand test utility
  ],
}

// createJestConfig is exported this way to ensure that next/jest can load the Next.js config which is async
module.exports = createJestConfig(customJestConfig)
