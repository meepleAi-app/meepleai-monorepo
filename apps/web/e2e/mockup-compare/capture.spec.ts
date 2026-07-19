import { mkdirSync, writeFileSync } from 'node:fs';
import path from 'node:path';

import { test } from '@playwright/test';

import { PAIRS, OUTPUT_DIR } from './manifest';
import { mockAuthEndpoints, seedMockRoleCookies } from '../_helpers/seedAuthSession';

const MOCKUP_PORT = 5175;

interface CaptureRecord {
  id: string;
  label: string;
  route: string;
  viewport: { width: number; height: number };
  mockupPng: string | null;
  mockupError?: string;
  livePng: string | null;
  liveError?: string;
}

const records: CaptureRecord[] = [];

test.beforeAll(() => {
  mkdirSync(OUTPUT_DIR, { recursive: true });
});

test.afterAll(() => {
  writeFileSync(path.join(OUTPUT_DIR, 'captures.json'), JSON.stringify(records, null, 2), 'utf8');

  console.log(`[compare] captures.json scritto (${records.length} coppie)`);
});

for (const pair of PAIRS) {
  test(`capture ${pair.id}`, async ({ page }) => {
    const viewport = pair.viewport ?? { width: 1920, height: 1080 };
    const rec: CaptureRecord = {
      id: pair.id,
      label: pair.label,
      route: pair.route,
      viewport,
      mockupPng: null,
      livePng: null,
    };
    // Push SUBITO: la coppia deve apparire SEMPRE nel report (fail-visible),
    // anche se una riga fuori dai try (es. setViewportSize) lancia.
    records.push(rec);
    await page.setViewportSize(viewport);

    // 1) MOCKUP statico via http-server. NB: molti page-mock caricano
    // React/ReactDOM/Babel da unpkg.com e transpilano JSX in-browser → serve
    // RETE (unpkg) + attesa del MOUNT reale, non un timeout fisso.
    try {
      await page.goto(`http://127.0.0.1:${MOCKUP_PORT}/${pair.mockupHtml}`, {
        waitUntil: 'networkidle',
      });
      await page.waitForFunction(
        () => {
          const root = document.querySelector('#root');
          if (root) return root.childElementCount > 0;
          return document.body.childElementCount > 0;
        },
        { timeout: 15_000 }
      );
      const mockupFile = `${pair.id}__mockup.png`;
      await page.screenshot({ path: path.join(OUTPUT_DIR, mockupFile), fullPage: true });
      rec.mockupPng = mockupFile;
    } catch (err) {
      rec.mockupError = (err as Error).message;

      console.log(`[compare] ${pair.id}: mockup capture failed — ${rec.mockupError}`);
    }

    // 2) LIVE route reale. Auth = seed cookie (proxy SSR gate) + mockAuthEndpoints
    // (client /auth/me + /auth/session/status, regex host-agnostico). Dati via
    // pair.mock (glob host-agnostici). NESSUN mock auth hand-rolled.
    try {
      await seedMockRoleCookies(page, pair.auth === 'admin' ? 'Admin' : 'User');
      await mockAuthEndpoints(page, { role: pair.auth === 'admin' ? 'admin' : 'user' });
      if (pair.mock) await pair.mock(page);
      // 'load' (non 'networkidle'): route con SSE/EventSource aperto non
      // raggiungerebbero mai networkidle → hang fino al timeout.
      await page.goto(pair.route, { waitUntil: 'load' });
      await page.waitForTimeout(1000);
      // Guard: se il gate SSR ha reindirizzato a /login (server senza bypass),
      // il capture sarebbe una login page fuorviante → segnala come liveError.
      if (/(^|\/)login(\/|$)/.test(new URL(page.url()).pathname)) {
        rec.liveError =
          'redirected to /login (auth-bypass non ingaggiato — usa COMPARE_APP_PORT su una porta libera)';
        console.log(`[compare] ${pair.id}: ${rec.liveError}`);
      } else {
        const liveFile = `${pair.id}__live.png`;
        await page.screenshot({ path: path.join(OUTPUT_DIR, liveFile), fullPage: true });
        rec.livePng = liveFile;
      }
    } catch (err) {
      rec.liveError = (err as Error).message;
      console.log(`[compare] ${pair.id}: live capture failed — ${rec.liveError}`);
    }
  });
}
