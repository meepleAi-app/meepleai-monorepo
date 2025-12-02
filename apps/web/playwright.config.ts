import { defineConfig, devices } from '@playwright/test';
import { ChromaticConfig } from '@chromatic-com/playwright';

export default defineConfig<ChromaticConfig>({
  testDir: './e2e',
  timeout: 60000, // 60s global timeout for dev mode
  fullyParallel: !process.env.CI, // Issue #1868: Disable parallel in CI to prevent axe-core race conditions
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 2 : 0,
  workers: process.env.CI ? 1 : 2, // Issue #1868: Single worker in CI for stability, 2 local for speed
  reporter: process.env.CI ? 'dot' : 'html', // Standard reporters only (Chromatic uses fixture, not reporter)
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
    {
      name: 'mobile',
      use: {
        // Use Chromium with mobile viewport (avoids WebKit dependency in CI)
        ...devices['Pixel 5'],
        viewport: { width: 390, height: 844 },
      },
    },
    {
      name: 'tablet',
      use: {
        // Use Chromium with tablet viewport (avoids WebKit dependency in CI)
        ...devices['Galaxy Tab S4'],
        viewport: { width: 1024, height: 1366 },
      },
    },
    {
      name: 'desktop',
      use: {
        ...devices['Desktop Chrome'],
        viewport: { width: 1920, height: 1080 },
      },
    },
  ],

  webServer: {
    // Issue #1868: Use production server in CI (after pnpm build), dev server locally
    command: process.env.CI
      ? 'node ./node_modules/next/dist/bin/next start -p 3000'
      : 'node --max-old-space-size=4096 ./node_modules/next/dist/bin/next dev -p 3000',
    url: 'http://localhost:3000',
    reuseExistingServer: !process.env.CI,
    timeout: process.env.CI ? 30 * 1000 : 180 * 1000, // 30s for prod, 3min for dev startup
  },
});
