import { defineConfig, devices } from '@playwright/test';
import { ChromaticConfig } from '@chromatic-com/playwright';
import { defineCoverageReporterConfig } from '@bgotink/playwright-coverage';
import path from 'path';

export default defineConfig<ChromaticConfig>({
  testDir: './e2e',
  timeout: 60000, // 60s global timeout for dev mode
  fullyParallel: process.env.CI !== 'true', // Issue #1868: Disable parallel in CI to prevent axe-core race conditions
  forbidOnly: process.env.CI === 'true',
  retries: process.env.CI === 'true' ? 2 : 0, // Issue #2008: Retry strategy - CI transient failures, local fast feedback
  workers: process.env.CI === 'true' ? 1 : 2, // Issue #1868: Single worker in CI for stability, 2 local for speed
  // Issue #2007: Add global setup/teardown for server health checks
  globalSetup: require.resolve('./e2e/global-setup.ts'),
  globalTeardown: require.resolve('./e2e/global-teardown.ts'),
  // Issue #1498: E2E Code Coverage Reporting
  reporter: [
    [process.env.CI === 'true' ? 'dot' : 'html'], // Standard reporters
    [
      '@bgotink/playwright-coverage',
      defineCoverageReporterConfig({
        // Path to the root files should be resolved from (repository root)
        sourceRoot: path.resolve(__dirname, '../..'),
        // Files to ignore in coverage
        exclude: [
          // Test files
          '**/e2e/**',
          '**/__tests__/**',
          '**/*.test.{ts,tsx}',
          '**/*.spec.{ts,tsx}',
          // Generated files
          '**/generated/**',
          '**/.next/**',
          '**/node_modules/**',
          // Build artifacts
          '**/dist/**',
          '**/build/**',
          '**/coverage/**',
          '**/playwright-report/**',
          // Configuration files
          '**/*.config.{js,ts}',
          '**/scripts/**',
          // Storybook
          '**/.storybook/**',
          '**/*.stories.{ts,tsx}',
        ],
        // Directory in which to write coverage reports
        resultDir: path.resolve(__dirname, 'coverage-e2e'),
        // Istanbul reporters to use
        reports: ['lcov', 'html', 'json', 'text-summary'],
        // Watermarks for coverage thresholds (issue #1498: start conservative)
        watermarks: {
          statements: [30, 60],
          functions: [30, 60],
          branches: [30, 60],
          lines: [30, 60],
        },
      }),
    ],
  ],
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'on-first-retry',
    actionTimeout: 10000, // 10s for clicks/fills
    navigationTimeout: 60000, // 60s for page.goto (increased for dev server)

    // Chromatic Playwright fixture options
    disableAutoSnapshot: false, // Auto-capture at end of test
    cropToViewport: false, // Capture full page
    launchOptions: {
      args: [
        '--no-sandbox',
        '--disable-setuid-sandbox',
        '--disable-dev-shm-usage',
        '--disable-accelerated-2d-canvas',
        '--disable-gpu',
        // Removed --single-process: causes "Target page has been closed" errors (#797)
      ],
    },
  },

  projects: [
    // Desktop - Multi-browser (Chrome, Firefox, Safari)
    // Issue #1497: Added Firefox and Safari for comprehensive desktop testing
    {
      name: 'desktop-chrome',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1920, height: 1080 },
      },
    },
    {
      name: 'desktop-firefox',
      use: {
        ...devices['Desktop Firefox'],
        viewport: { width: 1920, height: 1080 },
      },
    },
    {
      name: 'desktop-safari',
      use: {
        ...devices['Desktop Safari'],
        viewport: { width: 1920, height: 1080 },
      },
    },

    // Mobile - Chrome + Safari (iOS simulation critical for market coverage)
    // Issue #1497: Added Safari for iOS browser testing
    {
      name: 'mobile-chrome',
      use: {
        ...devices['Pixel 5'],
        viewport: { width: 390, height: 844 },
      },
    },
    {
      name: 'mobile-safari',
      use: {
        ...devices['iPhone 13'],
        viewport: { width: 390, height: 844 },
      },
    },

    // Tablet - Chrome only (cost optimization, diminishing returns for tablet multi-browser)
    {
      name: 'tablet-chrome',
      use: {
        ...devices['Galaxy Tab S4'],
        viewport: { width: 1024, height: 1366 },
      },
    },
  ],

  webServer: {
    // Issue #1868: Use production server in CI (after pnpm build), dev server locally
    // Issue #1951: Use standalone server in CI (compatible with output: 'standalone')
    // Issue #1951: In CI, NEXT_PUBLIC_API_BASE=http://localhost:8081 (mock server started in CI step)
    // Issue #2008: Check CI === 'true' (not just truthy) to avoid treating CI=false as true
    command:
      process.env.CI === 'true'
        ? 'cross-env PORT=3000 node .next/standalone/server.js'
        : 'node --max-old-space-size=4096 ./node_modules/next/dist/bin/next dev -p 3000',
    url: 'http://localhost:3000',
    reuseExistingServer: process.env.CI !== 'true',
    timeout: process.env.CI === 'true' ? 30 * 1000 : 180 * 1000, // 30s for prod, 3min for dev startup
  },
});
