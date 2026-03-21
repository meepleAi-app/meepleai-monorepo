import path from 'path';

import { defineCoverageReporterConfig } from '@bgotink/playwright-coverage';
// import { ChromaticConfig } from '@chromatic-com/playwright'; // Type not exported in v0.12.8
import { defineConfig, devices } from '@playwright/test';

// Issue #2009: Prometheus reporter configuration (typed for TypeScript)
// eslint-disable-next-line @typescript-eslint/no-explicit-any -- Playwright reporter config allows flexible structure
const prometheusReporter: any[] = process.env.PROMETHEUS_REMOTE_WRITE_URL
  ? [
      [
        'playwright-prometheus-remote-write-reporter',
        {
          // Prometheus Remote Write endpoint
          url: process.env.PROMETHEUS_REMOTE_WRITE_URL || 'http://localhost:9090/api/v1/write',

          // Authentication (if required)
          headers: process.env.PROMETHEUS_API_KEY
            ? {
                Authorization: `Bearer ${process.env.PROMETHEUS_API_KEY}`,
              }
            : {},

          // Metric prefix (default: 'pw_', we use 'playwright_' for clarity)
          prefix: 'playwright_',

          // Additional labels attached to all metrics
          labels: {
            environment: process.env.NODE_ENV || 'test',
            project: 'meepleai',
            team: 'qa',
            shard: process.env.SHARD_INDEX || 'unknown',
            total_shards: process.env.TOTAL_SHARDS || '1',
          },

          // Environment variables to expose as labels (be careful with secrets!)
          env: {
            CI: process.env.CI || 'false',
            GITHUB_REF_NAME: process.env.GITHUB_REF_NAME || 'local',
          },
        },
      ],
    ]
  : [];

export default defineConfig({
  testDir: './e2e',
  timeout: process.env.CI === 'true' ? 90000 : 60000, // Issue #20375956158: 90s in CI for accessibility tests, 60s local
  fullyParallel: process.env.CI !== 'true', // Issue #1868: Disable parallel in CI to prevent axe-core race conditions
  forbidOnly: process.env.CI === 'true',
  retries: process.env.CI === 'true' ? 2 : 0, // Issue #2008: Retry strategy - CI transient failures, local fast feedback
  workers: process.env.CI === 'true' ? 1 : 2, // Issue #1868: Single worker in CI for stability, 2 local for speed
  // Issue #2007: Add global setup/teardown for server health checks
  globalSetup: require.resolve('./e2e/global-setup.ts'),
  globalTeardown: require.resolve('./e2e/global-teardown.ts'),
  // Issue #1498: E2E Code Coverage Reporting
  // Issue #2009: Prometheus Metrics Export for Production-Grade Observability
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
    ...prometheusReporter, // Type-safe spread of Prometheus reporter
    // Issue #3082 Phase B: Duration tracking for shard balancing
    ['./e2e/reporters/duration-reporter.ts'],
  ],
  use: {
    baseURL: 'http://localhost:3000',
    trace: 'retain-on-failure',
    screenshot: 'only-on-failure',
    video: 'retain-on-failure',
    actionTimeout: 10000, // 10s for clicks/fills
    navigationTimeout: 60000, // 60s for page.goto (increased for dev server)

    // Chromatic Playwright fixture options
    // TODO: Check if these options still exist in current Chromatic version
    // disableAutoSnapshot: false, // Auto-capture at end of test
    // cropToViewport: false, // Capture full page
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

    // Admin First-Time Setup - Serial execution for first-time deployment validation
    {
      name: 'admin-first-time-setup',
      testDir: './e2e/admin-first-time-setup',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1920, height: 1080 },
        storageState: undefined, // No auth state initially
        trace: 'on-first-retry',
        video: 'retain-on-failure',
      },
      fullyParallel: false, // Serial execution - order matters
      workers: 1, // Single worker to maintain state
      timeout: 120000, // 2 min per test (PDF processing takes time)
      // Optional: Testcontainers integration (enable with E2E_USE_TESTCONTAINERS=true)
      testMatch: /.*\.spec\.ts/,
      // Project-specific setup/teardown (optional Testcontainers)
      // Uncomment to enable isolated infrastructure:
      // globalSetup: require.resolve('./e2e/admin-first-time-setup/setup.ts'),
      // globalTeardown: require.resolve('./e2e/admin-first-time-setup/teardown.ts'),
    },

    // Admin User Onboarding - Serial execution targeting local or staging environments
    {
      name: 'onboarding-local',
      testDir: './e2e/flows',
      testMatch: /admin-user-onboarding\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: 'http://localhost:3000',
      },
      fullyParallel: false,
      workers: 1,
      timeout: 180_000,
    },
    {
      name: 'onboarding-staging',
      testDir: './e2e/flows',
      testMatch: /admin-user-onboarding\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: 'https://meepleai.app',
      },
      fullyParallel: false,
      workers: 1,
      timeout: 180_000,
    },

    // Admin Embedding Flow — local dev and integration environments
    {
      name: 'embedding-flow-local',
      testDir: './e2e/flows',
      testMatch: /admin-embedding-flow\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: 'http://localhost:3000',
      },
      fullyParallel: false,
      workers: 1,
      timeout: 300_000, // 5 min — embedding jobs can be slow
    },
    {
      name: 'embedding-flow-integration',
      testDir: './e2e/flows',
      testMatch: /admin-embedding-flow\.spec\.ts/,
      use: {
        ...devices['Desktop Chrome'],
        baseURL: process.env.E2E_BASE_URL ?? 'http://localhost:3000',
      },
      fullyParallel: false,
      workers: 1,
      timeout: 300_000,
    },
  ],

  // Issue #2008: Disable webServer in parallel mode to prevent port conflicts
  // When PARALLEL_E2E=true, the server is started once by scripts/run-parallel-e2e.js
  // and all shards reuse it
  webServer:
    process.env.PLAYWRIGHT_SKIP_WEB_SERVER === '1'
      ? undefined
      : process.env.PARALLEL_E2E === 'true'
        ? undefined
        : {
            // Issue #2007 Phase 2: Use production server in CI for stability
            // Dev server crashes after ~48 minutes under sustained test load
            // Production build is already created by CI workflow (pnpm build)
            // Issue #2247: Heap increase LOCAL ONLY (dev server memory leak mitigation)
            // Issue #2261: Force production server for error state tests (FORCE_PRODUCTION_SERVER=true)
            command:
              process.env.CI === 'true' || process.env.FORCE_PRODUCTION_SERVER === 'true'
                ? 'node ./node_modules/next/dist/bin/next start -p 3000'
                : 'node --max-old-space-size=8192 ./node_modules/next/dist/bin/next dev -p 3000',
            url: 'http://localhost:3000',
            reuseExistingServer: !process.env.CI,
            timeout: 180 * 1000, // 3min for server startup
            // E2E test auth bypass: allows Playwright tests to mock auth via page.route()
            // without needing a running backend for session validation.
            // Only active in development (guarded by NODE_ENV !== 'production' in proxy).
            env: {
              PLAYWRIGHT_AUTH_BYPASS: 'true',
            },
          },
});
