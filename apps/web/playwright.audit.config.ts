/**
 * Config Playwright dedicata all'audit esaustivo (ondata 0+).
 *
 * Differenza sostanziale da playwright.config.ts: NIENTE PLAYWRIGHT_AUTH_BYPASS.
 * I 381 spec E2E esistenti girano con l'autenticazione bypassata e cookie
 * sintetici — dimostrano che il frontend si comporta bene dato un backend
 * ipotetico. Qui il login è reale, contro lo stack locale, perché l'audit deve
 * provare anche l'autenticazione.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import path from 'node:path';

import { defineConfig, devices } from '@playwright/test';

export default defineConfig({
  testDir: './e2e/audit',
  timeout: 90_000,
  // Un audit non deve nascondere l'intermittenza dietro un retry: se una pagina
  // è instabile, quella è l'informazione che cerchiamo.
  retries: 0,
  // Serialità: l'ordine conta per correlare ogni azione con la finestra di log.
  workers: 1,
  reporter: [['list'], ['html', { open: 'never', outputFolder: 'audit-results/html' }]],

  use: {
    baseURL: process.env.AUDIT_BASE_URL || 'http://localhost:3000',
    trace: 'retain-on-failure',
    actionTimeout: 15_000,
    navigationTimeout: 30_000,
  },

  projects: [
    { name: 'setup', testMatch: /auth-setup\.ts/ },
    {
      name: 'audit',
      dependencies: ['setup'],
      testMatch: /(crawl|wave\d+\w*)[-.]?\w*\.spec\.ts/,
      use: { ...devices['Desktop Chrome'] },
    },
  ],

  outputDir: path.join(__dirname, 'audit-results/artifacts'),
});
