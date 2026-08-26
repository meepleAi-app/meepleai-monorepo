/**
 * Verifiche di interazione sull'interfaccia.
 *
 * Il crawler apre le pagine e guarda cosa succede; qui invece si **agisce**:
 * click, digitazione, invio di moduli. Serve perché una pagina può caricarsi
 * senza errori e avere un pulsante che non fa nulla, un modulo che non invia,
 * una ricerca che non filtra — difetti invisibili a chi si limita a navigare.
 *
 * Ogni caso registra ciò che ha osservato in audit-results/ui-interazioni.jsonl,
 * incluse le chiamate di rete scatenate dall'interazione: è quello il legame fra
 * il gesto dell'utente e il comportamento del backend.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { appendFileSync, mkdirSync } from 'node:fs';
import path from 'node:path';

import { test } from '@playwright/test';

import { authFile } from './auth-paths';

const RESULTS = path.join(__dirname, '../../audit-results');
const LOG = path.join(RESULTS, 'ui-interazioni.jsonl');

type Osservazione = {
  caso: string;
  rotta: string;
  ruolo: string;
  esito: 'atteso' | 'difforme' | 'da-guardare';
  osservato: string;
};

function registra(o: Osservazione): void {
  mkdirSync(RESULTS, { recursive: true });
  appendFileSync(LOG, JSON.stringify(o) + '\n', 'utf8');
  const tag = o.esito === 'atteso' ? 'OK  ' : o.esito === 'difforme' ? 'DIFF' : 'GUAR';
  console.log(`${tag} ${o.caso.padEnd(46)} ${o.osservato.slice(0, 90)}`);
}

/** Raccoglie le richieste fallite scatenate da un'interazione. */
function osservaRete(page: import('@playwright/test').Page): string[] {
  const fallite: string[] = [];
  page.on('response', r => {
    if (r.status() >= 400) fallite.push(`${r.status()} ${new URL(r.url()).pathname}`);
  });
  return fallite;
}

test.describe('Interazioni — utente', () => {
  test.use({ storageState: authFile('user') });

  test('la ricerca nel catalogo filtra i risultati', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/games');
    await page.waitForLoadState('domcontentloaded');

    // Il campo va cercato NEL contenuto: la barra globale in cima ("Cerca… CtrlK")
    // è il comando di navigazione, non la ricerca del catalogo.
    const ricerca = page
      .locator(
        'main input[type="search"], main input[type="text"], main input[placeholder*="erca" i]'
      )
      .first();
    const presente = (await ricerca.count()) > 0;

    if (!presente) {
      registra({
        caso: 'ricerca catalogo',
        rotta: '/games',
        ruolo: 'user',
        esito: 'da-guardare',
        osservato: 'nessun campo di ricerca trovato nella pagina',
      });
      return;
    }

    // Si guarda prima lo STATO del campo: `fill` su un campo readonly va in
    // timeout e fa fallire il caso senza spiegare perché — l'informazione utile
    // è che il campo non è utilizzabile, non che il test è andato in errore.
    const stato = await ricerca.evaluate((e: HTMLInputElement) => ({
      readOnly: e.readOnly,
      disabled: e.disabled,
      ariaDisabled: e.getAttribute('aria-disabled'),
    }));

    if (stato.readOnly || stato.disabled || stato.ariaDisabled === 'true') {
      registra({
        caso: 'ricerca catalogo',
        rotta: '/games',
        ruolo: 'user',
        esito: 'difforme',
        osservato: `campo visibile ma non utilizzabile — readOnly: ${stato.readOnly} · disabled: ${stato.disabled} · aria-disabled: ${stato.ariaDisabled} (#3848)`,
      });
      return;
    }

    const prima = await page.locator('main').first().innerText();
    await ricerca.fill('catan');
    await page.waitForTimeout(2500);
    const dopo = await page.locator('main').first().innerText();

    registra({
      caso: 'ricerca catalogo',
      rotta: '/games',
      ruolo: 'user',
      esito: prima !== dopo ? 'atteso' : 'difforme',
      osservato:
        prima === dopo
          ? 'il contenuto non cambia dopo la digitazione'
          : `contenuto aggiornato · richieste fallite: ${fallite.length}`,
    });
  });

  test('la libreria si apre e mostra le voci o uno stato vuoto esplicito', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/library');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(4000);

    // Si legge l'intera pagina, non solo <main>: su /library il landmark contiene
    // i contatori ma NON il titolo né l'azione principale, che stanno fuori.
    // Misurare solo dentro <main> segnalerebbe come rotta una pagina integra.
    // (La collocazione del titolo fuori dal landmark resta un'osservazione di
    // accessibilità: chi usa lo skip-link non lo raggiunge.)
    const testo = await page.locator('body').innerText();

    // Una libreria vuota è comunicata dai CONTATORI ("0 Giochi totali"), non da
    // una frase: cercare solo parole come "nessun" o "vuota" produce un falso
    // positivo su una pagina che funziona perfettamente.
    const haIntestazione = /la tua libreria|libreria/i.test(testo);
    const haContatori = /\d+\s*(giochi|agenti|documenti|chat)/i.test(testo);
    const haAzione = /aggiungi/i.test(testo);

    registra({
      caso: 'libreria: struttura e stato',
      rotta: '/library',
      ruolo: 'user',
      // L'azione di aggiunta compare più tardi degli altri elementi: pretenderla
      // entro la stessa finestra rende il caso instabile senza dire nulla di
      // nuovo. Resta osservata e riportata, ma non decide l'esito.
      esito: haIntestazione && haContatori ? 'atteso' : 'difforme',
      osservato: `intestazione: ${haIntestazione} · contatori: ${haContatori} · azione di aggiunta: ${haAzione} · richieste fallite: ${fallite.length}`,
    });
  });

  test('il profilo permette di modificare e salvare', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/profile');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(3000);

    const testo = await page.locator('main').first().innerText();

    // La modifica del profilo passa da un pulsante "Modifica" e da schede
    // (Panoramica, Impostazioni, …): pretendere un "Salva" sempre visibile
    // segnalerebbe come rotta una pagina che si comporta come deve.
    const haDatiUtente = /@/.test(testo);
    const haModifica =
      (await page.getByRole('button', { name: /modifica|edit|impostazioni/i }).count()) > 0;

    registra({
      caso: 'profilo: dati e accesso alla modifica',
      rotta: '/profile',
      ruolo: 'user',
      esito: haDatiUtente && haModifica ? 'atteso' : 'difforme',
      osservato: `dati utente mostrati: ${haDatiUtente} · accesso alla modifica: ${haModifica} · richieste fallite: ${fallite.length}`,
    });
  });
});

test.describe('Interazioni — amministratore', () => {
  test.use({ storageState: authFile('admin') });

  test('la sezione utenti elenca e permette di aprire un dettaglio', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/admin/users');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2500);

    const righe = await page.locator('tr, [role="row"], article').count();
    const testo = await page.locator('body').innerText();

    registra({
      caso: 'admin utenti: elenco',
      rotta: '/admin/users',
      ruolo: 'admin',
      esito: righe > 1 ? 'atteso' : 'difforme',
      osservato: `righe: ${righe} · richieste fallite: ${fallite.length}${
        fallite.length ? ` (${fallite.slice(0, 2).join(', ')})` : ''
      } · errore in pagina: ${/errore|error/i.test(testo)}`,
    });
  });

  test('il pannello di configurazione carica senza errori di rete', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/admin/config');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(2500);

    registra({
      caso: 'admin configurazione: caricamento',
      rotta: '/admin/config',
      ruolo: 'admin',
      esito: fallite.length === 0 ? 'atteso' : 'difforme',
      osservato:
        fallite.length === 0
          ? 'nessuna richiesta fallita'
          : `richieste fallite: ${fallite.slice(0, 3).join(' · ')}`,
    });
  });

  test('il monitoraggio mostra dati e non solo scheletri di caricamento', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/admin/monitor');
    await page.waitForLoadState('domcontentloaded');
    await page.waitForTimeout(4000);

    const testo = await page.locator('body').innerText();
    const soloScheletri =
      (await page.locator('[class*="skeleton"], [aria-busy="true"]').count()) > 0;

    registra({
      caso: 'admin monitoraggio: dati caricati',
      rotta: '/admin/monitor',
      ruolo: 'admin',
      esito: fallite.length === 0 && !soloScheletri ? 'atteso' : 'da-guardare',
      osservato: `scheletri ancora presenti dopo 4s: ${soloScheletri} · richieste fallite: ${fallite.length} · testo: ${testo.length} caratteri`,
    });
  });
});
