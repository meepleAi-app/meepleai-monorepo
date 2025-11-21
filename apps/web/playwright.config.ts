import { defineConfig, devices } from '@playwright/test';

const webPort = Number(process.env.PLAYWRIGHT_WEB_PORT ?? '3000');
const baseURL = process.env.PLAYWRIGHT_BASE_URL ?? `http://localhost:${webPort}`;
const shouldStartWebServer = process.env.PLAYWRIGHT_SKIP_WEB_SERVER !== '1';

const webServerConfig = shouldStartWebServer
  ? {
      command: `node ./node_modules/next/dist/bin/next dev -p ${webPort}`,
      url: baseURL,
      reuseExistingServer: !process.env.CI,
      timeout: 120 * 1000,
    }
  : undefined;

export default defineConfig({
  testDir: './e2e',
  timeout: 60000, // 60s global timeout for dev mode
  fullyParallel: true, // Issue #843: Enable parallel for better CI performance
  forbidOnly: !!process.env.CI,
  retries: process.env.CI ? 1 : 0, // Reduce retries from 2 to 1 for faster feedback
  workers: process.env.CI ? 8 : 4, // Issue #843: Increased from 4 to 8 workers in CI, 4 local for optimal speed
  reporter: process.env.CI ? 'dot' : 'html', // Concise output in CI, HTML report locally
  use: {
    baseURL,
    trace: 'on-first-retry',
    actionTimeout: 10000,     // 10s for clicks/fills
    navigationTimeout: 30000, // 30s for page.goto
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
      name: 'chromium',
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  webServer: webServerConfig,
});
