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
  moduleNameMapper: {
    '^@/(.*)$': '<rootDir>/src/$1',
  },
  collectCoverageFrom: [
    'src/**/*.{js,jsx,ts,tsx}',
    '!src/**/*.d.ts',
    '!src/**/_app.tsx',
    '!src/**/_document.tsx',
    '!src/**/__tests__/fixtures/**',
    '!src/**/__tests__/utils/mock-api-router.ts',
    '!src/**/__tests__/utils/mock-api-presets.ts',
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
    '/__tests__/utils/mock-api-router\\.ts$',
    '/__tests__/utils/mock-api-presets\\.ts$',
    '/test-utils\\.tsx$', // Utility file, not a test file
    '/chat-test-utils\\.ts$', // Chat test utility file, not a test file
  ],
}

// createJestConfig is exported this way to ensure that next/jest can load the Next.js config which is async
module.exports = createJestConfig(customJestConfig)
