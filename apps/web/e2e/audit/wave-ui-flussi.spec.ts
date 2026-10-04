/**
 * I flussi che un utente percorre davvero, dal primo click.
 *
 * `crawl.spec.ts` apre le pagine; `wave-ui-interazioni.spec.ts` agisce su alcune; qui si
 * percorrono i **flussi**: navigare fra le sezioni, cercare nella propria libreria, cambiare
 * scheda, avviare la creazione di una serata, aprire una chat, entrare nelle impostazioni,
 * cambiare tema. Sono i gesti che compongono una sessione d'uso normale, ed erano l'unica parte
 * dell'interfaccia che nessuna suite percorreva.
 *
 * Selettori **verificati sul DOM reale** prima di scrivere i test, non dedotti dal codice: un
 * selettore immaginato produce un test che fallisce per sé stesso, e si legge come un difetto del
 * prodotto. I `data-slot` qui sotto vengono da una sonda su /library, /game-nights/new, /settings.
 *
 * Due categorie di esito, e la differenza conta:
 *   - `registraIn()` descrive ciò che si è visto e NON fa fallire: `difforme` è un finding;
 *   - `prerequisito()` FA fallire, perché se l'elemento che il flusso richiede non c'è, il test
 *     non ha misurato nulla — e un verde che non ha cliccato niente è peggio di un rosso.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { test, type Page } from '@playwright/test';

import { authFile } from './auth-paths';
import { prerequisito, registraIn, type Esito } from './registra';

const LOG = 'ui-flussi.jsonl';

function registra(o: {
  caso: string;
  rotta: string;
  ruolo: string;
  esito: Esito;
  osservato: string;
}): void {
  registraIn(LOG, o);
}

/** Raccoglie le richieste fallite scatenate dal flusso: è il legame col comportamento del backend. */
function osservaRete(page: Page): string[] {
  const fallite: string[] = [];
  page.on('response', r => {
    if (r.status() >= 400) fallite.push(`${r.status()} ${new URL(r.url()).pathname}`);
  });
  return fallite;
}

test.describe('Flussi — utente', () => {
  test.use({ storageState: authFile('user') });

  test('la navigazione principale raggiunge ogni sezione', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/dashboard');

    // Le cinque voci di `TOP_BAR_NAV_IDS`: AppTopBar è la sorgente unica della navigazione
    // desktop (#1977 + #2158), quindi è qui che si misura se le sezioni sono raggiungibili.
    const voci = ['Dashboard', 'Libreria', 'Games', 'Sessioni', 'Toolkit'];
    const esiti: string[] = [];

    for (const voce of voci) {
      // Si riparte dalla dashboard a ogni voce: cliccare in sequenza misurerebbe anche l'ordine,
      // che non è ciò che interessa qui.
      await page.goto('/dashboard');
      const link = page.getByRole('link', { name: voce, exact: true }).first();
      await prerequisito(link, `voce di navigazione "${voce}"`);

      const href = await link.getAttribute('href');
      await link.click();

      // 🔴 `waitForLoadState('domcontentloaded')` NON attende una navigazione client-side: con
      // l'App Router il documento non si ricarica, quindi quello stato è già soddisfatto e
      // ritorna subito — si legge la URL **prima** che il router abbia navigato. La prima stesura
      // di questo test faceva così e riportava che tutte e cinque le voci restavano su
      // /dashboard: un difetto inventato, con la navigazione perfettamente sana (verificato a
      // mano: Libreria→/library, Games→/games, Sessioni→/sessions, Toolkit→/toolkit).
      // Si attende la URL, che è l'effetto osservabile del gesto.
      const arrivato = await page
        .waitForURL(u => new URL(u).pathname === href, { timeout: 10_000 })
        .then(() => true)
        .catch(() => false);

      const url = new URL(page.url()).pathname;
      // Il titolo in pagina: una rotta può rispondere 200 e rendere lo shell vuoto.
      const h1 = await page
        .locator('h1, [role="heading"][aria-level="1"]')
        .first()
        .innerText()
        .catch(() => '');
      esiti.push(
        `${voce}→${url}${arrivato ? '' : ' ✗ATTESA-SCADUTA'}${h1 ? ' ✓' : ' (nessun h1)'}`
      );
    }

    const senzaTitolo = esiti.filter(e => /ATTESA-SCADUTA|nessun h1/.test(e)).length;
    registra({
      caso: 'navigazione principale: cinque sezioni',
      rotta: '/dashboard',
      ruolo: 'user',
      esito: senzaTitolo === 0 && fallite.length === 0 ? 'atteso' : 'difforme',
      osservato: `${esiti.join(' · ')} · richieste fallite: ${fallite.length}${
        fallite.length ? ' → ' + fallite.slice(0, 2).join(' ; ') : ''
      }`,
    });
  });

  test('la ricerca nella libreria filtra il contenuto', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/library');

    // A differenza di /games — dove la ricerca è dichiaratamente non implementata (#3848) — qui
    // il campo è attivo: `library-search-input`, placeholder "Cerca in libreria... (premi /)".
    const campo = page.locator('[data-slot="library-search-input"]').first();
    await prerequisito(campo, 'campo di ricerca della libreria');

    const stato = await campo.evaluate((e: HTMLInputElement) => ({
      readOnly: e.readOnly,
      disabled: e.disabled,
    }));
    if (stato.readOnly || stato.disabled) {
      registra({
        caso: 'ricerca libreria',
        rotta: '/library',
        ruolo: 'user',
        esito: 'difforme',
        osservato: `campo inerte — readOnly: ${stato.readOnly} · disabled: ${stato.disabled}`,
      });
      return;
    }

    const prima = await page.locator('main').first().innerText();
    await campo.fill('zzzqwerty-nessun-gioco');
    await page.waitForTimeout(1500);
    const dopo = await page.locator('main').first().innerText();

    // Una ricerca senza risultati DEVE cambiare la pagina: o mostra uno stato vuoto, o azzera la
    // griglia. Se il contenuto è identico, il campo accetta il testo e non filtra — il difetto
    // peggiore dei tre, perché sembra funzionare.
    registra({
      caso: 'ricerca libreria: termine senza risultati',
      rotta: '/library',
      ruolo: 'user',
      esito: prima !== dopo ? 'atteso' : 'difforme',
      osservato:
        prima === dopo
          ? 'il contenuto non cambia: il campo accetta il testo ma non filtra'
          : `contenuto aggiornato · richieste fallite: ${fallite.length}`,
    });
  });

  test('le schede della libreria cambiano il contenuto', async ({ page }) => {
    await page.goto('/library');
    const schede = page.locator('[data-slot="library-tab"]');
    await prerequisito(schede.first(), 'schede della libreria');

    // La lingua va registrata accanto all'osservazione. Le etichette sono comparse una volta in
    // italiano («Tutti · I miei giochi · Agenti») e una in inglese («All · My games · Agents») su
    // due passate con la stessa configurazione: la UI rende secondo il `locale` del contesto, e
    // Playwright usa `en-US` per default. Senza questo campo l'oscillazione si ripresenta come
    // mistero invece che come dato.
    const lingua = await page.evaluate(() => document.documentElement.lang || '(non dichiarata)');

    const quante = await schede.count();
    const osservati: string[] = [];
    let invariati = 0;

    for (let i = 0; i < Math.min(quante, 4); i++) {
      const scheda = schede.nth(i);
      const nome = (await scheda.innerText()).replace(/\s+/g, ' ').trim().slice(0, 18);
      const prima = await page.locator('main').first().innerText();
      await scheda.click();
      await page.waitForTimeout(1200);
      const dopo = await page.locator('main').first().innerText();
      if (prima === dopo) invariati += 1;
      osservati.push(`${nome}${prima === dopo ? ' =' : ' ≠'}`);
    }

    // La prima scheda è già attiva all'apertura, quindi un solo "invariato" è previsto.
    registra({
      caso: 'schede libreria: il contenuto segue la selezione',
      rotta: '/library',
      ruolo: 'user',
      esito: invariati <= 1 ? 'atteso' : 'difforme',
      osservato: `lingua: ${lingua} · ${quante} schede · provate ${Math.min(quante, 4)}: ${osservati.join(' · ')} · invariate: ${invariati}`,
    });
  });

  test('ogni sezione principale ha un titolo di primo livello', async ({ page }) => {
    // Un `h1` per pagina non è pedanteria formale: è ciò che uno screen reader annuncia per dire
    // dove si è, e l'unica struttura su cui un utente che non vede può orientarsi. Il gate axe
    // controlla l'ordine delle intestazioni, non la **presenza** di una di primo livello.
    const rotte = ['/dashboard', '/library', '/games', '/sessions', '/toolkit', '/settings'];
    const senza: string[] = [];
    const con: string[] = [];

    for (const rotta of rotte) {
      await page.goto(rotta, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(2000);
      // Si escludono le intestazioni dentro elementi fissi come il banner cookie: su /toolkit
      // l'unica intestazione della pagina era l'`h3` del banner, e contarla avrebbe mascherato
      // l'assenza (#4060 per la copertura, questo caso per la struttura).
      const titolo = await page.evaluate(() => {
        const h1 = [...document.querySelectorAll('h1, [role="heading"][aria-level="1"]')].find(
          e => {
            if (!(e as HTMLElement).offsetHeight) return false;
            let p: HTMLElement | null = e as HTMLElement;
            while (p) {
              if (getComputedStyle(p).position === 'fixed') return false;
              p = p.parentElement;
            }
            return true;
          }
        );
        return h1 ? (h1.textContent || '').trim().slice(0, 30) : null;
      });
      if (titolo) con.push(`${rotta}: "${titolo}"`);
      else senza.push(rotta);
    }

    registra({
      caso: 'struttura: un h1 per sezione',
      rotta: rotte.join(','),
      ruolo: 'user',
      esito: senza.length === 0 ? 'atteso' : 'difforme',
      osservato:
        senza.length === 0
          ? `tutte con h1 · ${con.join(' · ')}`
          : `SENZA h1: ${senza.join(', ')} · con h1: ${con.length}/${rotte.length}`,
    });
  });

  test('il wizard della serata avanza e torna indietro', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/game-nights/new');

    const titolo = page.locator('[data-slot="game-night-create-title-input"]').first();
    const avanti = page.locator('[data-slot="game-night-create-nav-next"]').first();
    await prerequisito(titolo, 'campo titolo del wizard');
    await prerequisito(avanti, 'pulsante Avanti del wizard');

    await titolo.fill('Serata di prova — flussi');
    const scritto = await titolo.inputValue();

    // Il passo 1 chiede data e ora: senza, "Avanti" resta disabilitato ed è corretto che lo sia.
    const data = page.locator('input[type="datetime-local"]').first();
    const haData = (await data.count()) > 0;
    if (haData) {
      await data.fill('2027-03-15T20:30');
    }
    await page.waitForTimeout(400);

    const avantiAttivo = await avanti.isEnabled();
    let passo2 = false;
    let tornato = false;
    let copertoDa: string | null = null;

    if (avantiAttivo) {
      await avanti.click();
      await page.waitForTimeout(900);
      passo2 = await page
        .locator(
          '[data-slot="game-night-create-step-2-container"], [data-slot^="game-night-create-step2"]'
        )
        .first()
        .isVisible()
        .catch(() => false);

      const indietro = page.locator('[data-slot="game-night-create-nav-back"]').first();

      // 🔴 Non si clicca «Indietro» alla cieca. Al passo 2 il contenuto cresce, i comandi scendono
      // negli ultimi 169px della viewport e il banner cookie (`fixed bottom-0 z-50`) li intercetta:
      // il pulsante risulta visibile, abilitato e immobile, e `click()` va in timeout dopo 15s
      // facendo fallire il test con un messaggio che non nomina la causa. Difetto in #4060.
      //
      // Si misura PRIMA chi riceve il click, così l'esito dice «coperto da X» invece di morire.
      copertoDa = await indietro.evaluate((e: HTMLElement) => {
        const r = e.getBoundingClientRect();
        const sopra = document.elementFromPoint(r.x + r.width / 2, r.y + r.height / 2);
        if (!sopra || sopra === e || e.contains(sopra)) return null;
        const cls = typeof sopra.className === 'string' ? sopra.className : '';
        return `${sopra.tagName.toLowerCase()}${cls ? '.' + cls.split(/\s+/).slice(0, 3).join('.') : ''}`;
      });

      if (!copertoDa && (await indietro.isEnabled())) {
        await indietro.click({ timeout: 5_000 }).catch(() => undefined);
        await page.waitForTimeout(700);
        tornato = await page
          .locator('[data-slot="game-night-create-step-1-container"]')
          .first()
          .isVisible()
          .catch(() => false);
      }
    }

    const viewport = page.viewportSize();
    registra({
      caso: 'wizard serata: titolo, data, avanti e indietro',
      rotta: '/game-nights/new',
      ruolo: 'user',
      esito: scritto.length > 0 && avantiAttivo && passo2 && tornato ? 'atteso' : 'difforme',
      osservato:
        `titolo scritto: ${scritto.length > 0} · campo data: ${haData} · Avanti attivo: ${avantiAttivo} · ` +
        `passo 2: ${passo2} · ritorno al passo 1: ${tornato}` +
        (copertoDa
          ? ` · ⚠️ Indietro COPERTO da ${copertoDa} a ${viewport?.width}x${viewport?.height} (#4060)`
          : '') +
        ` · richieste fallite: ${fallite.length}`,
    });
  });

  test('la chat si apre da zero', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/chat');

    const nuova = page.getByRole('button', { name: /nuova chat/i }).first();
    await prerequisito(nuova, 'pulsante "Nuova chat"');

    const urlPrima = new URL(page.url()).pathname;
    await nuova.click();
    await page.waitForTimeout(2000);
    const urlDopo = new URL(page.url()).pathname;

    // Un pulsante che non cambia né la URL né il contenuto è il caso che il crawler non vede:
    // la pagina risponde 200 e il gesto non produce nulla.
    const contenuto = await page.locator('main').first().innerText();
    const cambiato = urlDopo !== urlPrima || /scegli|seleziona|gioco|agente/i.test(contenuto);

    registra({
      caso: 'chat: "Nuova chat" produce un effetto',
      rotta: '/chat',
      ruolo: 'user',
      esito: cambiato && fallite.length === 0 ? 'atteso' : 'difforme',
      osservato: `url: ${urlPrima} → ${urlDopo} · effetto osservabile: ${cambiato} · richieste fallite: ${fallite.length}${
        fallite.length ? ' → ' + fallite.slice(0, 2).join(' ; ') : ''
      }`,
    });
  });

  test('le impostazioni aprono le loro sezioni', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/settings');

    // L'hub /settings ha avuto un ping-pong di redirect (308) in passato: la prima cosa da
    // misurare è che la URL finale sia quella chiesta e non un rimbalzo.
    const urlFinale = new URL(page.url()).pathname;

    const sezioni = ['Profile', 'Security', 'Preferences'];
    const esiti: string[] = [];
    for (const s of sezioni) {
      const voce = page.getByRole('button', { name: new RegExp(s, 'i') }).first();
      if ((await voce.count()) === 0) {
        esiti.push(`${s}: assente`);
        continue;
      }
      await voce.click();
      await page.waitForTimeout(900);
      const testo = await page.locator('main').first().innerText();
      esiti.push(`${s}: ${testo.length > 200 ? 'contenuto' : 'vuoto'}`);
    }

    registra({
      caso: 'impostazioni: le sezioni si aprono',
      rotta: '/settings',
      ruolo: 'user',
      esito:
        urlFinale === '/settings' &&
        !esiti.some(e => /assente|vuoto/.test(e)) &&
        fallite.length === 0
          ? 'atteso'
          : 'difforme',
      osservato: `url finale: ${urlFinale} · ${esiti.join(' · ')} · richieste fallite: ${fallite.length}`,
    });
  });

  test('il cambio di tema si applica e sopravvive al ricaricamento', async ({ page }) => {
    await page.goto('/settings');

    const prima = await page.evaluate(() => ({
      tema: document.documentElement.dataset.theme ?? null,
      classe: document.documentElement.className,
    }));

    // Il tema si commuta da `next-themes`, che scrive sia `class="dark"` sia `data-theme`:
    // si guardano entrambi, perché una correzione che tocca uno solo passerebbe inosservata.
    const nuovo = prima.tema === 'dark' ? 'light' : 'dark';
    await page.evaluate(t => window.localStorage.setItem('theme', t), nuovo);
    await page.reload({ waitUntil: 'domcontentloaded' });
    await page.waitForTimeout(900);

    const dopo = await page.evaluate(() => ({
      tema: document.documentElement.dataset.theme ?? null,
      classe: document.documentElement.className,
      sfondo: getComputedStyle(document.body).backgroundColor,
    }));

    registra({
      caso: 'tema: commutazione persistente',
      rotta: '/settings',
      ruolo: 'user',
      esito: dopo.tema === nuovo ? 'atteso' : 'difforme',
      osservato: `${prima.tema ?? '(nessuno)'} → richiesto ${nuovo} → ottenuto ${dopo.tema ?? '(nessuno)'} · class: "${dopo.classe}" · sfondo: ${dopo.sfondo}`,
    });
  });
});

test.describe('Flussi — amministratore', () => {
  test.use({ storageState: authFile('admin') });

  test('il pannello admin raggiunge le sue sezioni principali', async ({ page }) => {
    const fallite = osservaRete(page);
    await page.goto('/admin');

    const sezioni = ['/admin/users', '/admin/config', '/admin/shared-games'];
    const esiti: string[] = [];
    for (const rotta of sezioni) {
      const res = await page.goto(rotta, { waitUntil: 'domcontentloaded' });
      await page.waitForTimeout(1500);
      const testo = await page
        .locator('main')
        .first()
        .innerText()
        .catch(() => '');
      esiti.push(`${rotta}: HTTP ${res?.status()} ${testo.length > 200 ? 'contenuto' : 'scarno'}`);
    }

    registra({
      caso: 'admin: sezioni principali',
      rotta: '/admin',
      ruolo: 'admin',
      esito:
        !esiti.some(e => /HTTP [45]|scarno/.test(e)) && fallite.length === 0
          ? 'atteso'
          : 'difforme',
      osservato: `${esiti.join(' · ')} · richieste fallite: ${fallite.length}${
        fallite.length ? ' → ' + fallite.slice(0, 3).join(' ; ') : ''
      }`,
    });
  });
});
