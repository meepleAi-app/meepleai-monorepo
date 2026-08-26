/**
 * Crawler esplorativo: percorre ogni rotta dell'inventario con ciascun ruolo e
 * registra cosa succede.
 *
 * Il crawler NON asserisce. Un test rosso interromperebbe la passata al primo
 * difetto, che è l'opposto di ciò che serve: qui vogliamo attraversare tutte le
 * 220 rotte e poi guardare l'elenco completo delle anomalie. La classificazione
 * la fa `render-report.ts`.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { appendFileSync, existsSync, mkdirSync, readFileSync } from 'node:fs';
import path from 'node:path';

import { test } from '@playwright/test';

import { authFile } from './auth-paths';
import { resolveRouteUrl } from '../../scripts/audit/resolve-params';

const INVENTORY = path.join(
  __dirname,
  '../../../../docs/for-developers/audits/2026-08-26-full-feature-audit/inventory.csv'
);
const PARAMS = path.join(__dirname, 'route-params.json');
const RESULTS_DIR = path.join(__dirname, '../../audit-results');
const ENTRIES = path.join(RESULTS_DIR, 'entries.jsonl');

/** Testi che indicano un guasto anche quando la risposta HTTP è 200. */
const FAILURE_MARKERS: Array<[string, RegExp]> = [
  ['errore-generico-it', /qualcosa è andato storto/i],
  ['errore-generico-en', /something went wrong/i],
  ['errore-imprevisto', /errore imprevisto/i],
  ['application-error', /application error/i],
  ['not-found', /pagina non trovata|page not found/i],
];

type Row = { id: string; path: string; ruolo: string };

function readRoutes(role: string): Row[] {
  const [, ...lines] = readFileSync(INVENTORY, 'utf8').trim().split('\n');
  return (
    lines
      .map(line => line.split(','))
      // Le note quotate sono l'ultima colonna: anche se contengono virgole, le
      // colonne 0-8 restano allineate.
      .map(c => ({ id: c[0], tipo: c[1], path: c[2], ruolo: c[5] }))
      .filter(r => r.tipo === 'route' && r.ruolo === role)
      .map(({ id, path: p, ruolo }) => ({ id, path: p, ruolo }))
  );
}

const params: Record<string, string> = existsSync(PARAMS)
  ? (JSON.parse(readFileSync(PARAMS, 'utf8')) as Record<string, string>)
  : {};

for (const role of ['user', 'admin']) {
  test.describe(`ruolo ${role}`, () => {
    test.use({ storageState: authFile(role) });

    for (const row of readRoutes(role)) {
      test(`[${role}] ${row.path}`, async ({ page }) => {
        const url = resolveRouteUrl(row.path, params);
        test.skip(url === null, `parametro non risolto per ${row.path}`);

        const consoleErrors: string[] = [];
        const failedRequests: string[] = [];

        page.on('console', msg => {
          if (msg.type() === 'error') consoleErrors.push(msg.text().slice(0, 300));
        });
        page.on('response', res => {
          if (res.status() >= 400) failedRequests.push(`${res.status()} ${res.url()}`);
        });

        // `networkidle` non si verifica mai su una pagina con SSE o SignalR — che
        // il prodotto usa — e la rotta andrebbe in timeout risultando "rotta"
        // senza esserlo. Si attende il quietarsi della rete, ma senza farne una
        // condizione: scaduto il tempo si prosegue e si guarda comunque la pagina.
        const response = await page.goto(url as string, { waitUntil: 'domcontentloaded' });
        await page.waitForLoadState('networkidle', { timeout: 5_000 }).catch(() => {});
        const body = await page.locator('body').innerText();
        const bodyMarkers = FAILURE_MARKERS.filter(([, re]) => re.test(body)).map(([name]) => name);

        const shot = path.join(RESULTS_DIR, 'shots', `${row.id}-${role}.png`);
        mkdirSync(path.dirname(shot), { recursive: true });
        await page.screenshot({ path: shot, fullPage: true });

        // Scrittura diretta su JSONL: il reporter JSON di Playwright salva gli
        // attachment come riferimenti su disco, non inline, quindi non è una
        // sorgente affidabile per il report. Con workers=1 l'append è sicuro.
        appendFileSync(
          ENTRIES,
          JSON.stringify({
            id: row.id,
            route: row.path,
            url,
            role,
            status: response?.status() ?? 0,
            consoleErrors,
            failedRequests,
            bodyMarkers,
            screenshot: path.relative(RESULTS_DIR, shot),
          }) + '\n',
          'utf8'
        );
      });
    }
  });
}
