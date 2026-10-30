/**
 * Gate: ogni chiamata dei client frontend (`src/lib/api/**`) corrisponde a una rotta che il backend
 * registra, con quel verbo (#4752, forma di #3954).
 *
 * Il caso che lo ha fatto nascere: «Aggiungi partita» faceva `POST /api/v1/games/{id}/sessions`, e il
 * backend registra su quel percorso solo il `GET`. Un confronto per percorso non lo vede, e un 405 non
 * lascia traccia in nessun log di chi guarda le pagine.
 *
 * Il gate è un cricchetto: `client-routes-baseline.json` elenca i candidati già presenti quando è nato
 * (difetti veri e falsi positivi dell'estrattore, da triare: #3954). Fallisce per una voce NUOVA e per
 * una voce che non serve più, così l'elenco può solo accorciarsi.
 *
 * Limiti dell'estrattore: vedi `client-routes.ts`. Le rotte vengono da `extractApiEndpoints`.
 */

import { readFileSync } from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, expect, it } from 'vitest';

import {
  compareCallsToEndpoints,
  extractClientCallsFromDir,
  mismatchKey,
  type Mismatch,
} from '../client-routes';
import { extractApiEndpoints } from '../extract-api-endpoints';

const here = path.dirname(fileURLToPath(import.meta.url));
const webRoot = path.resolve(here, '../../..');

type Baseline = { wrongVerb: string[]; unknownPath: string[] };
const baseline: Baseline = JSON.parse(
  readFileSync(path.join(here, '../client-routes-baseline.json'), 'utf8')
);

const endpoints = extractApiEndpoints(path.resolve(webRoot, '../api/src/Api'));
const calls = extractClientCallsFromDir(path.join(webRoot, 'src/lib/api'), webRoot);
const mismatches = compareCallsToEndpoints(calls, endpoints);

const keysOf = (kind: Mismatch['kind']): string[] =>
  mismatches
    .filter(m => m.kind === kind)
    .map(mismatchKey)
    .sort();

const describeNew = (kind: Mismatch['kind'], keys: string[]): string =>
  keys
    .map(key => {
      const m = mismatches.find(x => x.kind === kind && mismatchKey(x) === key)!;
      const registered =
        m.registeredMethods.length > 0
          ? ` (sul percorso il backend registra: ${m.registeredMethods.join(', ')})`
          : '';
      return `  ${key}${registered}  <- ${m.file}:${m.line}`;
    })
    .join('\n');

describe('chiamate dei client e rotte del backend', () => {
  it('il confronto vede qualcosa da entrambe le parti', () => {
    // Un estrattore cieco (cartella sbagliata, rotte non lette) farebbe sembrare ogni chiamata
    // «sconosciuta»: il cricchetto resterebbe verde per le voci già note e rosso per tutte le altre,
    // ma nessun difetto nuovo si distinguerebbe. Qui si verifica che la maggioranza combaci.
    expect(calls.length).toBeGreaterThan(0);
    expect(endpoints.length).toBeGreaterThan(0);
    expect(mismatches.length).toBeLessThan(calls.length / 2);
  });

  it('nessuna chiamata nuova usa un verbo che il backend non registra su quel percorso', () => {
    const fresh = keysOf('wrong-verb').filter(k => !baseline.wrongVerb.includes(k));
    expect(
      fresh,
      `Il percorso esiste, ma non con questo verbo: la chiamata risponde 405.\n` +
        `Correggi il client (o registra la rotta). Se è un falso positivo dell'estrattore, ` +
        `aggiungi la voce a scripts/audit/client-routes-baseline.json dicendo perché.\n` +
        describeNew('wrong-verb', fresh)
    ).toEqual([]);
  });

  it('nessuna chiamata nuova punta a un percorso che il backend non registra', () => {
    const fresh = keysOf('unknown-path').filter(k => !baseline.unknownPath.includes(k));
    expect(
      fresh,
      `Nessun verbo è registrato su questo percorso: la chiamata risponde 404.\n` +
        `Correggi il client (o registra la rotta). Se la rotta esiste, verifica che l'estrattore la veda ` +
        `(extract-api-endpoints.ts); solo se è un suo limite, aggiungi la voce al baseline dicendo perché.\n` +
        describeNew('unknown-path', fresh)
    ).toEqual([]);
  });

  it('il baseline non contiene voci che non servono più', () => {
    const stale = [
      ...baseline.wrongVerb
        .filter(k => !keysOf('wrong-verb').includes(k))
        .map(k => `wrongVerb: ${k}`),
      ...baseline.unknownPath
        .filter(k => !keysOf('unknown-path').includes(k))
        .map(k => `unknownPath: ${k}`),
    ];
    expect(
      stale,
      'Queste voci non sono più un rilievo (chiamata corretta o rimossa): cancellale da ' +
        'scripts/audit/client-routes-baseline.json.'
    ).toEqual([]);
  });
});
