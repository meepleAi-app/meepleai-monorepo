/**
 * Confronta le chiamate dei client frontend con le rotte che il backend registra, verbo compreso.
 *
 * Due classi di difetto, entrambe invisibili a chi guarda solo lo stato HTTP delle GET del browser:
 *   - `unknown-path`: nessun verbo è registrato su quel percorso (#3954, 30 casi);
 *   - `wrong-verb`: il percorso esiste ma non con quel verbo. È il 405 di «Aggiungi partita» (#4752):
 *     `POST /games/{id}/sessions` con il solo `GET` registrato, e un confronto per percorso non lo
 *     vede, perché il percorso c'è.
 *
 * Limiti noti, tutti nella direzione dei falsi negativi (il confronto sovrastima la copertura, non
 * produce rilievi inventati):
 *   - si leggono solo le chiamate con un percorso letterale (o con costanti di file risolvibili):
 *     `httpClient.get(url)`, i path costruiti da un builder e le `fetch()` dirette non si vedono;
 *   - un parametro vale per qualunque segmento dall'altra parte, anche per un segmento letterale;
 *   - le rotte sono quelle di `Routing/**` (`extractApiEndpoints`): chi registra altrove non c'è.
 */

import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';

import type { EndpointEntry } from './types';

export type ClientCall = { method: string; path: string; file: string; line: number };

export type Mismatch = {
  kind: 'wrong-verb' | 'unknown-path';
  method: string;
  path: string;
  /** Verbi registrati sullo stesso percorso (vuoto per `unknown-path`). */
  registeredMethods: string[];
  file: string;
  line: number;
};

const STRING_LITERAL = '`(?:[^`\\\\]|\\\\.)*`|\'(?:[^\'\\\\]|\\\\.)*\'|"(?:[^"\\\\]|\\\\.)*"';
const CALL = new RegExp(
  `\\.(get|post|put|patch|delete)\\s*(?:<[^()\`'"]*>)?\\s*\\(\\s*(${STRING_LITERAL})`,
  'g'
);
const CONST_DECL = new RegExp(
  `\\bconst\\s+(\\w+)\\s*(?::\\s*string\\s*)?=\\s*(${STRING_LITERAL})\\s*(?:as\\s+const)?\\s*;`,
  'g'
);
const INTERPOLATION = /\$\{[^}]*\}/g;

const unquote = (literal: string): string => literal.slice(1, -1);

/** Costanti stringa del file (`const GAMES_BASE = '/api/v1/games'`), con quelle annidate risolte. */
function fileConstants(source: string): Map<string, string> {
  const constants = new Map<string, string>();
  for (const m of source.matchAll(CONST_DECL)) constants.set(m[1], unquote(m[2]));

  const resolve = (value: string): string =>
    value.replace(/\$\{(\w+)\}/g, (whole, name: string) => constants.get(name) ?? whole);
  // Tre passate bastano per le basi composte (`${BASE}/sub`) che i client usano davvero.
  for (let pass = 0; pass < 3; pass++) {
    for (const [name, value] of constants) constants.set(name, resolve(value));
  }
  return constants;
}

/** Percorso di una chiamata: costanti risolte, parametri a `{}`, senza query string. */
function normalizeClientPath(raw: string, constants: Map<string, string>): string {
  const resolved = raw.replace(
    /\$\{(\w+)\}/g,
    (whole, name: string) => constants.get(name) ?? whole
  );
  // Un template annidato nell'interpolazione (`${q ? `?${q}` : ''}`) chiude il letterale esterno prima
  // del tempo: quel che resta è un `${...` senza chiusura, cioè la query string. Non è un parametro.
  const noQuery = resolved
    .split('?')[0]
    .replace(/\$\{[^}]*$/, '')
    .replace(INTERPOLATION, '{}');
  const segments = noQuery.split('/').map(segment =>
    // `diary{}` è il percorso con in coda una query string interpolata: il segmento è `diary`.
    segment === '{}' ? segment : segment.replace(/\{\}/g, '')
  );
  return segments.join('/').replace(/\/+$/, '');
}

/** Chiamate `.get/.post/.put/.patch/.delete` con un percorso `/api/...` leggibile dal sorgente. */
export function extractClientCalls(source: string, file: string): ClientCall[] {
  const constants = fileConstants(source);
  const calls: ClientCall[] = [];

  for (const m of source.matchAll(CALL)) {
    const normalized = normalizeClientPath(unquote(m[2]), constants);
    if (!normalized.startsWith('/api/')) continue;
    calls.push({
      method: m[1].toUpperCase(),
      path: normalized,
      file,
      line: source.slice(0, m.index ?? 0).split('\n').length,
    });
  }
  return calls;
}

const isParam = (segment: string): boolean => segment.startsWith('{');
const isCatchAll = (segment: string): boolean => /^\{\*+\w*\}$/.test(segment);

/** I segmenti combaciano uno a uno; un parametro, da una parte o dall'altra, vale per qualunque segmento. */
function pathsMatch(clientPath: string, endpointPath: string): boolean {
  const client = clientPath.split('/').filter(Boolean);
  const endpoint = endpointPath.split('/').filter(Boolean);

  for (let i = 0; i < endpoint.length; i++) {
    if (isCatchAll(endpoint[i])) return client.length > i;
    if (i >= client.length) return false;
    if (isParam(endpoint[i]) || isParam(client[i])) continue;
    if (endpoint[i].toLowerCase() !== client[i].toLowerCase()) return false;
  }
  return client.length === endpoint.length;
}

/** Chiave stabile di un rilievo: verbo e percorso, senza file né riga (che si spostano). */
export const mismatchKey = (m: Pick<Mismatch, 'method' | 'path'>): string =>
  `${m.method} ${m.path}`;

/** Rilievi di un elenco di chiamate, uno per (verbo, percorso), con la posizione della prima. */
export function compareCallsToEndpoints(
  calls: readonly ClientCall[],
  endpoints: readonly EndpointEntry[]
): Mismatch[] {
  const found = new Map<string, Mismatch>();

  for (const call of calls) {
    const key = mismatchKey(call);
    if (found.has(key)) continue;

    const samePath = endpoints.filter(e => pathsMatch(call.path, e.path));
    if (samePath.some(e => e.method === call.method)) continue;

    found.set(key, {
      kind: samePath.length > 0 ? 'wrong-verb' : 'unknown-path',
      method: call.method,
      path: call.path,
      registeredMethods: [...new Set(samePath.map(e => e.method))].sort(),
      file: call.file,
      line: call.line,
    });
  }
  return [...found.values()];
}

/** Chiamate di tutti i client sotto `clientDir`, esclusi i test. */
export function extractClientCallsFromDir(clientDir: string, repoRelativeTo: string): ClientCall[] {
  const calls: ClientCall[] = [];

  const walk = (dir: string): void => {
    for (const name of readdirSync(dir).sort()) {
      const full = path.join(dir, name);
      if (statSync(full).isDirectory()) {
        if (name === '__tests__' || name === 'node_modules') continue;
        walk(full);
        continue;
      }
      if (!/\.tsx?$/.test(name) || /\.(test|spec|stories)\.tsx?$/.test(name)) continue;
      const rel = path.relative(repoRelativeTo, full).split(path.sep).join('/');
      calls.push(...extractClientCalls(readFileSync(full, 'utf8'), rel));
    }
  };

  walk(clientDir);
  return calls;
}
