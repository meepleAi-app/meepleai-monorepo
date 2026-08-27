/**
 * Ondata 1A — verifiche di livello L2/L3 su Authentication.
 *
 * A differenza del crawler, questi test ESERCITANO le funzioni: registrazione,
 * accesso, uscita, recupero password, più i casi negativi che il livello L3
 * richiede. Ogni test lascia la propria evidenza in audit-results/wave1a.jsonl
 * (esito, risposta API, riga DB attesa), che il report poi raccoglie.
 *
 * NON asserisce il comportamento atteso dove l'obiettivo è scoprirlo: registra
 * ciò che accade e lo confronta con ciò che il prodotto promette.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { appendFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';

import { expect, test } from '@playwright/test';

const RESULTS = path.join(__dirname, '../../audit-results');
const LOG = path.join(RESULTS, 'wave1a.jsonl');
const API = process.env.PLAYWRIGHT_API_BASE ?? 'http://localhost:8080';

type Evidence = {
  caso: string;
  livello: 'L2' | 'L3';
  esito: 'atteso' | 'difforme';
  osservato: string;
  nota?: string;
};

function record(e: Evidence): void {
  mkdirSync(RESULTS, { recursive: true });
  appendFileSync(LOG, JSON.stringify(e) + '\n', 'utf8');
  console.log(`[${e.esito === 'atteso' ? 'OK ' : 'DIFF'}] ${e.caso} — ${e.osservato}`);
}

test.describe('Authentication — ondata 1A', () => {
  test('registrazione in modalità invite-only mostra la richiesta di accesso', async ({ page }) => {
    const mode = await (await page.request.get(`${API}/api/v1/auth/registration-mode`)).json();
    expect(mode.publicRegistrationEnabled, 'questo caso vale solo con registrazione chiusa').toBe(
      false
    );

    await page.goto('/register');
    await page.waitForLoadState('domcontentloaded');
    const body = await page.locator('body').innerText();

    // Con la registrazione chiusa il prodotto promette il modulo di richiesta
    // accesso al posto di quello standard.
    const chiedeAccesso = /richiedi|richiesta di accesso|request access|invito/i.test(body);
    const formStandard = (await page.locator('input[type="password"]').count()) > 0;

    record({
      caso: 'registrazione invite-only → modulo di richiesta accesso',
      livello: 'L3',
      esito: chiedeAccesso && !formStandard ? 'atteso' : 'difforme',
      osservato: `richiesta accesso: ${chiedeAccesso} · campo password presente: ${formStandard}`,
      nota: body.slice(0, 160).replace(/\s+/g, ' '),
    });
  });

  test('login con password errata non concede sessione', async ({ page }) => {
    const res = await page.request.post(`${API}/api/v1/auth/login`, {
      data: { email: 'test@meepleai.com', password: 'password-sbagliata-di-proposito' },
      failOnStatusCode: false,
    });
    const body = await res.text();

    record({
      caso: 'login con password errata',
      livello: 'L3',
      esito: res.status() >= 400 && !res.headers()['set-cookie'] ? 'atteso' : 'difforme',
      osservato: `HTTP ${res.status()} · cookie di sessione emesso: ${Boolean(res.headers()['set-cookie'])}`,
      nota: body.slice(0, 120),
    });
  });

  test('login con email inesistente non rivela se l’utente esiste', async ({ page }) => {
    const inesistente = await page.request.post(`${API}/api/v1/auth/login`, {
      data: { email: 'non-esiste-affatto@meepleai.test', password: 'qualunque' },
      failOnStatusCode: false,
    });
    const esistente = await page.request.post(`${API}/api/v1/auth/login`, {
      data: { email: 'test@meepleai.com', password: 'password-sbagliata-di-proposito' },
      failOnStatusCode: false,
    });

    const msgA = (await inesistente.json().catch(() => ({}))).message ?? '';
    const msgB = (await esistente.json().catch(() => ({}))).message ?? '';

    record({
      caso: 'enumerazione utenti dal messaggio di errore',
      livello: 'L3',
      esito: msgA === msgB && inesistente.status() === esistente.status() ? 'atteso' : 'difforme',
      osservato: `utente inesistente: ${inesistente.status()} "${msgA}" · utente reale: ${esistente.status()} "${msgB}"`,
    });
  });

  test('accesso e uscita reali dalla UI', async ({ page, context }) => {
    await page.goto('/login');
    await page.locator('input[type="email"]').first().fill('test@meepleai.com');
    await page
      .locator('input[type="password"]')
      .first()
      .fill(process.env.AUDIT_USER_PASSWORD ?? '');
    await page.locator('button[type="submit"]').first().click();
    await expect(page).not.toHaveURL(/\/login/, { timeout: 30_000 });

    const dopoLogin = (await context.cookies()).filter(c => c.name === 'meepleai_session');
    record({
      caso: 'accesso dalla UI emette il cookie di sessione',
      livello: 'L2',
      esito: dopoLogin.length === 1 ? 'atteso' : 'difforme',
      osservato: `cookie meepleai_session presenti: ${dopoLogin.length} · url: ${page.url()}`,
    });

    const uscita = await page.request.post(`${API}/api/v1/auth/logout`, {
      failOnStatusCode: false,
    });
    const verifica = await page.request.get(`${API}/api/v1/auth/me`, { failOnStatusCode: false });

    record({
      caso: 'uscita invalida la sessione',
      livello: 'L2',
      esito: uscita.ok() && verifica.status() === 401 ? 'atteso' : 'difforme',
      osservato: `logout: HTTP ${uscita.status()} · /auth/me dopo l'uscita: HTTP ${verifica.status()}`,
      nota: verifica.status() === 403 ? 'atteso 401 (non autenticato), ricevuto 403' : undefined,
    });
  });

  test('un utente non può leggere il profilo di un altro utente', async ({ page }) => {
    await page.request.post(`${API}/api/v1/auth/login`, {
      data: { email: 'test@meepleai.com', password: process.env.AUDIT_USER_PASSWORD ?? '' },
    });

    const altrui = await page.request.get(`${API}/api/v1/admin/users`, { failOnStatusCode: false });

    record({
      caso: 'utente non privilegiato sulla lista utenti admin',
      livello: 'L3',
      esito: altrui.status() === 401 || altrui.status() === 403 ? 'atteso' : 'difforme',
      osservato: `GET /api/v1/admin/users come utente semplice: HTTP ${altrui.status()}`,
    });
  });
});
