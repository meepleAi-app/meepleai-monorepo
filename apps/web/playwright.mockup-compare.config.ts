/**
 * Config Playwright del tool mockup↔live compare (#2999).
 * NON è un gate CI — dev-tool locale. Avvia il Next app (auth-bypass) +
 * http-server per i mockup statici. La capture spec produce screenshot, non
 * assert.
 */
import { defineConfig, devices } from '@playwright/test';

const MOCKUP_PORT = 5175;

// La route live richiede un server con PLAYWRIGHT_AUTH_BYPASS attivo. Se la
// :3000 è occupata da un server SENZA bypass (es. lo stack Docker `make dev`),
// il gate SSR di proxy.ts reindirizza a /login e il capture prende la pagina
// di login. Punta a un dev server con bypass via COMPARE_APP_PORT.
const APP_PORT = process.env.COMPARE_APP_PORT ?? '3000';
const APP_URL = `http://localhost:${APP_PORT}`;

export default defineConfig({
  testDir: './e2e/mockup-compare',
  testMatch: 'capture.spec.ts',
  fullyParallel: false,
  workers: 1,
  retries: 0,
  reporter: 'list',
  timeout: 60_000,
  use: {
    baseURL: APP_URL,
    trace: 'retain-on-failure',
    locale: 'it-IT',
    timezoneId: 'UTC',
    colorScheme: 'light',
  },
  projects: [
    {
      name: 'compare-desktop',
      use: { ...devices['Desktop Chrome'], viewport: { width: 1920, height: 1080 } },
    },
  ],
  webServer: [
    {
      // cross-env garantisce che PLAYWRIGHT_AUTH_BYPASS raggiunga il processo
      // Next: il webServer.env di Playwright NON propaga in modo affidabile su
      // Windows → il gate SSR di proxy.ts reindirizzava a /login. Con l'env nel
      // comando, il bypass ingaggia e le route (authenticated) renderizzano.
      command: `cross-env PLAYWRIGHT_AUTH_BYPASS=true node --max-old-space-size=8192 ./node_modules/next/dist/bin/next dev -p ${APP_PORT}`,
      url: APP_URL,
      reuseExistingServer: true,
      timeout: 180_000,
      env: { PLAYWRIGHT_AUTH_BYPASS: 'true' },
    },
    {
      command: `pnpm exec http-server ../../admin-mockups/design_files -p ${MOCKUP_PORT} -s --cors`,
      url: `http://127.0.0.1:${MOCKUP_PORT}`,
      reuseExistingServer: true,
      timeout: 30_000,
    },
  ],
});
