/**
 * Percorsi degli storageState per ruolo.
 *
 * Modulo separato dagli spec: Playwright vieta a un test file di importarne un
 * altro, e sia auth-setup che crawl hanno bisogno di questi percorsi.
 */
import path from 'node:path';

export const AUTH_DIR = path.join(__dirname, '.auth');

export const authFile = (role: string): string => path.join(AUTH_DIR, `${role}.json`);
