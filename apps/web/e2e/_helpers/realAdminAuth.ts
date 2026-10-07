/**
 * Autenticazione reale per le spec che visitano rotte protette (#4098).
 *
 * **Perché esiste, accanto a `seedAuthSession` e a `fixtures/auth.ts`.**
 * `proxy.ts` rimanda al login chi non ha una sessione valida. Le due strade esistenti per
 * aggirarlo nei test dipendono entrambe da `PLAYWRIGHT_AUTH_BYPASS=true`, che **solo il
 * webServer di `playwright.config.ts` imposta** (riga 368): `seedAuthSession` mette cookie
 * finti che il proxy accetta *soltanto* sotto bypass, e `loginAsAdmin` di `fixtures/auth.ts`
 * è un mock di `page.route()` con lo stesso vincolo. Contro un server avviato in altro modo
 * — un container locale, `next start` a mano — il bypass non c'è, il token finto viene
 * validato contro il backend, e la spec finisce su `/login?from=...`.
 *
 * Questo helper usa invece il login VERO (`authenticateViaAPI`, cioè
 * `POST /api/v1/auth/login`), quindi funziona in entrambi i casi. È la differenza che ha
 * reso misurabile #4098: le 24 spec «recuperate» che sembravano avere selettori derivati
 * fallivano tutte alla prima navigazione, mostrando la pagina di login.
 *
 * Quando le credenziali non ci sono, il chiamante SALTA con un motivo `PREVISTO:` che dice
 * come abilitare il test — mai un fallimento su un login impossibile.
 *
 * In locale: `E2E_ADMIN_EMAIL` + `E2E_ADMIN_PASSWORD` da `infra/secrets/admin.secret`.
 */

import { seedCookieConsent } from './seedCookieConsent';
import { authenticateViaAPI } from '../fixtures/auth';

import type { Cookie, Page } from '@playwright/test';

export const REAL_ADMIN_EMAIL = process.env.E2E_ADMIN_EMAIL;
export const REAL_ADMIN_PASSWORD = process.env.E2E_ADMIN_PASSWORD;

/** Vero quando le credenziali per il login reale sono disponibili. */
export const hasRealAdminCredentials = Boolean(REAL_ADMIN_EMAIL && REAL_ADMIN_PASSWORD);

/**
 * Motivo di salto conforme alla policy dei motivi di #4021: classe `PREVISTO:` perché il
 * prerequisito è un servizio/dato opzionale per progetto, e dice COME abilitarlo.
 */
export const MISSING_CREDENTIALS_REASON =
  'PREVISTO: imposta E2E_ADMIN_EMAIL e E2E_ADMIN_PASSWORD (in locale, i valori di un utente admin da infra/secrets/admin.secret)';

/**
 * Cookie di sessione del primo login riuscito di QUESTO worker.
 *
 * Playwright esegue ogni file in un processo worker separato, quindi la cache è per worker:
 * un login per worker invece di uno per test. Non è un'ottimizzazione, è una correzione —
 * con un login per test, 36 test in 48 secondi dallo stesso utente incontrano il rate limit
 * del backend e il login comincia a essere **rifiutato**. Il sintomo è crudele: i test
 * falliscono con «credenziali rifiutate» mentre le credenziali sono giuste, e il primo
 * sospetto cade sul segreto invece che sulla frequenza.
 */
let cachedSessionCookies: Cookie[] | null = null;

/**
 * Esegue il login reale e lascia i cookie di sessione sul contesto della pagina.
 *
 * @throws se il backend rifiuta le credenziali: un login fallito che prosegue in silenzio
 *   produrrebbe fallimenti di selettore sulla pagina di login, cioè la diagnosi sbagliata
 *   che #4098 ha dovuto correggere.
 */
export async function loginAsRealAdmin(page: Page): Promise<void> {
  // Il banner di consenso cookie copre la pagina e intercetta i click: senza questo seed il
  // primo `click()` di ogni spec va in timeout su un elemento visibile ma coperto, e
  // l'errore («locator.click: Timeout») non nomina il banner. Sta qui e non nei chiamanti
  // perché riguarda chiunque guidi un browser vero.
  await seedCookieConsent(page);

  if (cachedSessionCookies !== null) {
    await page.context().addCookies(cachedSessionCookies);
    return;
  }

  const ok = await authenticateViaAPI(
    page,
    REAL_ADMIN_EMAIL as string,
    REAL_ADMIN_PASSWORD as string
  );
  if (!ok) {
    throw new Error(
      'Login reale fallito con E2E_ADMIN_EMAIL/E2E_ADMIN_PASSWORD: il backend ha rifiutato le credenziali. Verifica che l utente esista e sia admin, e che non sia scattato il rate limit del login.'
    );
  }

  // Si conservano solo i cookie di sessione: riportare l'intero barattolo trascinerebbe
  // anche lo stato di consenso e le preferenze del primo test dentro tutti gli altri.
  const all = await page.context().cookies();
  cachedSessionCookies = all.filter(
    c => c.name.startsWith('meepleai_session') || c.name.startsWith('meepleai_user_role')
  );
}
