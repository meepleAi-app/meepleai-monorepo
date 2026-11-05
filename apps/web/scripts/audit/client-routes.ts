/**
 * Confronta le chiamate dei client frontend con le rotte che il backend registra, verbo compreso.
 *
 * Due classi di difetto, entrambe invisibili a chi guarda solo lo stato HTTP delle GET del browser:
 *   - `unknown-path`: nessun verbo è registrato su quel percorso (#3954);
 *   - `wrong-verb`: il percorso esiste ma non con quel verbo. È il 405 di «Aggiungi partita» (#4752):
 *     `POST /games/{id}/sessions` con il solo `GET` registrato, e un confronto per percorso non lo
 *     vede, perché il percorso c'è.
 *
 * L'estrattore è statico (regex e uno scanner di commenti e stringhe, non un parser), quindi sbaglia in
 * ENTRAMBI i versi. Falsi negativi (un difetto che il confronto non vede):
 *   - si leggono solo le chiamate il cui primo argomento è un letterale INTERO (`'/a/' + id + '/b'` non
 *     lo è), o un identificatore che è una costante con valore letterale (`const URL = '/api/…'`, anche
 *     composta da altre costanti): `httpClient.get(builder(x))`, le costanti importate da un altro file,
 *     gli oggetti di rotte (`ENDPOINTS.users`) e le `fetch()` dirette non si vedono;
 *   - una costante di file (livello più alto) dichiarata più volte con valori diversi è ambigua e non si
 *     legge; una locale vale nel blocco che la dichiara (vince la dichiarazione più vicina, ancora aperta
 *     alla chiamata), come il `const url = …` ripetuto in ogni metodo di un client;
 *   - un parametro vale per qualunque segmento dall'altra parte, anche per un segmento letterale.
 * Falsi positivi (un rilievo che non è un difetto):
 *   - le rotte registrate da un metodo che riceve un SOTTOGRUPPO già prefissato
 *     (`DocumentCollectionEndpoints`, `RagExecutionAdminEndpoints`) perdono il prefisso: `extractApiEndpoints`
 *     compone i prefissi per variabile dentro il file, non attraverso le chiamate fra metodi. Quelle
 *     rotte risultano `/api/v1/{id}/…` e le chiamate vere ai loro percorsi risultano sconosciute. Qui si
 *     scartano le rotte con un parametro come primo segmento (nessuna rotta vera comincia così: sono
 *     sempre un prefisso perso), che altrimenti assorbirebbero qualunque chiamata; le chiamate a
 *     quei percorsi restano nel baseline come falsi positivi noti;
 *   - un parametro di funzione omonimo di una costante di file si legge come la costante: l'estrattore
 *     non conosce i parametri (`function f(url)` accanto a un `const url` di file);
 *   - le rotte registrate in catena senza una variabile di gruppo
 *     (`group.MapGroup("/x").WithTags(…).MapPost("/clear", …)`, come in `AdminKBSettingsEndpoints` e
 *     `AdminLlmConfigEndpoints`) non si vedono, e le chiamate ai loro percorsi risultano sconosciute;
 *   - le rotte vengono da `Routing/**` e `BoundedContexts/**` (`Routing`); chi registra altrove non c'è.
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

type Part = { text: string } | { expr: string };

const CALL_OPEN = /\.(get|post|put|patch|delete)\s*(?:<[^()`'"]*>)?\s*\(/g;
const CONST_DECL = /\bconst\s+([A-Za-z_$][\w$]*)\s*(?::\s*string\s*)?=\s*/g;
const IDENTIFIER = /^[A-Za-z_$][\w$]*$/;

/** Copia di `source` con i commenti sostituiti da spazi: gli a capo restano, le righe non si spostano. */
export function stripComments(source: string): string {
  const n = source.length;
  let out = '';
  const blank = (from: number, to: number): string => source.slice(from, to).replace(/[^\n]/g, ' ');

  const copyQuoted = (start: number): number => {
    const quote = source[start];
    let j = start + 1;
    while (j < n && source[j] !== quote && source[j] !== '\n') j += source[j] === '\\' ? 2 : 1;
    j = Math.min(n, j + 1);
    out += source.slice(start, j);
    return j;
  };

  // Le interpolazioni di un template sono codice: possono contenere altri template e commenti.
  const copyTemplate = (start: number): number => {
    out += '`';
    let j = start + 1;
    while (j < n && source[j] !== '`') {
      if (source[j] === '\\') {
        out += source.slice(j, j + 2);
        j += 2;
      } else if (source[j] === '$' && source[j + 1] === '{') {
        out += '${';
        j = scanCode(j + 2, true);
      } else {
        out += source[j];
        j++;
      }
    }
    if (j < n) {
      out += '`';
      j++;
    }
    return j;
  };

  const scanCode = (start: number, untilBrace: boolean): number => {
    let j = start;
    let depth = 0;
    while (j < n) {
      const ch = source[j];
      const next = source[j + 1];
      if (ch === '/' && next === '/') {
        const eol = source.indexOf('\n', j);
        const end = eol === -1 ? n : eol;
        out += blank(j, end);
        j = end;
      } else if (ch === '/' && next === '*') {
        const close = source.indexOf('*/', j + 2);
        const end = close === -1 ? n : close + 2;
        out += blank(j, end);
        j = end;
      } else if (ch === "'" || ch === '"') {
        j = copyQuoted(j);
      } else if (ch === '`') {
        j = copyTemplate(j);
      } else if (untilBrace && ch === '}' && depth === 0) {
        out += '}';
        return j + 1;
      } else {
        if (untilBrace && ch === '{') depth++;
        if (untilBrace && ch === '}') depth--;
        out += ch;
        j++;
      }
    }
    return j;
  };

  scanCode(0, false);
  return out;
}

/** Indice della `}` che chiude un'interpolazione cominciata in `from`, o -1. */
function matchingBrace(code: string, from: number): number {
  let depth = 0;
  for (let j = from; j < code.length; j++) {
    const ch = code[j];
    if (ch === "'" || ch === '"' || ch === '`') {
      const literal = readLiteral(code, j);
      if (!literal) return -1;
      j = literal.end - 1;
    } else if (ch === '{') {
      depth++;
    } else if (ch === '}') {
      if (depth === 0) return j;
      depth--;
    }
  }
  return -1;
}

/** Legge il letterale (stringa o template) che comincia in `start`: testo e interpolazioni, e dove finisce. */
function readLiteral(code: string, start: number): { parts: Part[]; end: number } | null {
  const quote = code[start];
  if (quote !== '`' && quote !== "'" && quote !== '"') return null;

  const parts: Part[] = [];
  let text = '';
  let j = start + 1;
  while (j < code.length) {
    const ch = code[j];
    if (ch === '\\') {
      text += code[j + 1] ?? '';
      j += 2;
    } else if (ch === quote) {
      if (text) parts.push({ text });
      return { parts, end: j + 1 };
    } else if (quote === '`' && ch === '$' && code[j + 1] === '{') {
      const close = matchingBrace(code, j + 2);
      if (close === -1) return null;
      if (text) parts.push({ text });
      text = '';
      parts.push({ expr: code.slice(j + 2, close) });
      j = close + 1;
    } else if (ch === '\n' && quote !== '`') {
      return null;
    } else {
      text += ch;
      j++;
    }
  }
  return null;
}

/** Un'assegnazione `const NAME = <letterale>` e il blocco in cui vale. */
type Declaration = {
  name: string;
  index: number;
  /** Profondità di graffe alla dichiarazione: 0 è il livello più alto del file. */
  depth: number;
  /** Indice del carattere dopo la `}` che chiude il blocco della dichiarazione (infinito al livello più alto). */
  end: number;
  parts: Part[];
};

type Resolve = (name: string) => string | null;
type Lookup = (name: string, at: number, exclude?: Declaration) => string | null;

/** Il testo di un letterale: le costanti note si sostituiscono, ogni altra interpolazione diventa `{}`. */
function evaluate(parts: Part[], resolve: Resolve): string {
  return parts
    .map(part => {
      if ('text' in part) return part.text;
      const name = part.expr.trim();
      return (IDENTIFIER.test(name) ? resolve(name) : null) ?? '{}';
    })
    .join('');
}

/** Profondità di graffe PRIMA di ogni carattere; le graffe dentro stringhe e template non contano. */
function braceDepths(code: string): Int32Array {
  const depths = new Int32Array(code.length + 1);
  let depth = 0;
  let i = 0;
  while (i < code.length) {
    const ch = code[i];
    if (ch === "'" || ch === '"' || ch === '`') {
      const literal = readLiteral(code, i);
      const end = literal ? literal.end : i + 1;
      for (; i < end; i++) depths[i] = depth;
      continue;
    }
    depths[i] = depth;
    if (ch === '{') depth++;
    else if (ch === '}') depth--;
    i++;
  }
  depths[code.length] = depth;
  return depths;
}

/**
 * Risolve le costanti con valore letterale (`const NAME = '…'`, o un template). Una costante locale vale nel
 * blocco che la dichiara, dalla dichiarazione alla `}` che lo chiude: fra più dichiarazioni dello stesso nome
 * vince la più vicina ancora aperta alla chiamata (`const url = …` ripetuto in ogni metodo di un client).
 * Una costante di file (livello più alto) vale in tutto il file, anche sopra la dichiarazione: le funzioni
 * girano dopo. Due di file con lo stesso nome e valori diversi sono ambigue e non si risolvono.
 * L'estrattore non conosce i parametri: un `function f(url)` accanto a un `const url` di file si legge
 * come la costante.
 */
function makeLookup(code: string): Lookup {
  const depths = braceDepths(code);
  const declarations: Declaration[] = [];
  for (const m of code.matchAll(CONST_DECL)) {
    const index = m.index ?? 0;
    const literal = readLiteral(code, index + m[0].length);
    if (!literal) continue;
    const depth = depths[index];
    let end = Number.POSITIVE_INFINITY;
    if (depth > 0) {
      end = index;
      while (end < code.length && depths[end] >= depth) end++;
    }
    declarations.push({ name: m[1], index, depth, end, parts: literal.parts });
  }

  const cache = new Map<Declaration, string>();
  const visiting = new Set<Declaration>();

  function valueOf(declaration: Declaration): string {
    const cached = cache.get(declaration);
    if (cached !== undefined) return cached;
    if (visiting.has(declaration)) return '{}';
    visiting.add(declaration);
    const value = evaluate(declaration.parts, name => lookup(name, declaration.index, declaration));
    visiting.delete(declaration);
    cache.set(declaration, value);
    return value;
  }

  function lookup(name: string, at: number, exclude?: Declaration): string | null {
    const named = declarations.filter(d => d.name === name && d !== exclude);
    const local = named.filter(d => d.depth > 0 && d.index < at && at < d.end);
    if (local.length > 0) return valueOf(local.reduce((a, b) => (b.index > a.index ? b : a)));
    const values = new Set(named.filter(d => d.depth === 0).map(valueOf));
    return values.size === 1 ? [...values][0] : null;
  }

  return lookup;
}

/** Percorso di una chiamata: senza query string, con il suffisso interpolato attaccato all'ultimo segmento tolto. */
function normalizeClientPath(raw: string): string {
  const segments = raw
    .split('?')[0]
    .split('/')
    // `diary{}` è il percorso con in coda una query string interpolata: il segmento è `diary`.
    .map(segment => (segment === '{}' ? segment : segment.replace(/\{\}/g, '')));
  return segments.join('/').replace(/\/+$/, '');
}

const ENDS_ARGUMENT = /^\s*[,)]/;

/**
 * Il primo argomento di una chiamata, se è leggibile: un letterale INTERO, o una costante con valore
 * letterale. `'/api/x/' + id + '/y'` non è `/api/x/`: il letterale deve finire con una virgola o una
 * parentesi, altrimenti la chiamata si legge come «non letterale».
 */
function firstArgument(code: string, from: number, lookup: Lookup): string | null {
  let i = from;
  while (/\s/.test(code[i] ?? '')) i++;
  const resolve: Resolve = name => lookup(name, from);

  const literal = readLiteral(code, i);
  if (literal) {
    return ENDS_ARGUMENT.test(code.slice(literal.end, literal.end + 200))
      ? evaluate(literal.parts, resolve)
      : null;
  }

  const identifier = /^[A-Za-z_$][\w$]*/.exec(code.slice(i, i + 120))?.[0];
  if (
    !identifier ||
    !ENDS_ARGUMENT.test(code.slice(i + identifier.length, i + identifier.length + 200))
  ) {
    return null;
  }
  return resolve(identifier);
}

/** Chiamate `.get/.post/.put/.patch/.delete` con un percorso `/api/...` leggibile dal sorgente. */
export function extractClientCalls(source: string, file: string): ClientCall[] {
  const code = stripComments(source);
  const lookup = makeLookup(code);
  const calls: ClientCall[] = [];

  for (const m of code.matchAll(CALL_OPEN)) {
    const index = m.index ?? 0;
    const raw = firstArgument(code, index + m[0].length, lookup);
    if (raw === null) continue;
    const normalized = normalizeClientPath(raw);
    if (!normalized.startsWith('/api/')) continue;
    calls.push({
      method: m[1].toUpperCase(),
      path: normalized,
      file,
      line: code.slice(0, index).split('\n').length,
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

/**
 * Una rotta `/api/v1/{x}/…` è sempre un prefisso perso (nessuna rotta vera comincia con un parametro):
 * tenerla farebbe assorbire al suo jolly qualunque chiamata con quella forma.
 */
const isLostPrefixRoute = (endpoint: EndpointEntry): boolean =>
  /^\/api\/v\d+\/\{/.test(endpoint.path);

/** Chiave stabile di un rilievo: verbo e percorso, senza file né riga (che si spostano). */
export const mismatchKey = (m: Pick<Mismatch, 'method' | 'path'>): string =>
  `${m.method} ${m.path}`;

/** Rilievi di un elenco di chiamate, uno per (verbo, percorso), con la posizione della prima. */
export function compareCallsToEndpoints(
  calls: readonly ClientCall[],
  endpoints: readonly EndpointEntry[]
): Mismatch[] {
  const usable = endpoints.filter(e => !isLostPrefixRoute(e));
  const found = new Map<string, Mismatch>();

  for (const call of calls) {
    const key = mismatchKey(call);
    if (found.has(key)) continue;

    const samePath = usable.filter(e => pathsMatch(call.path, e.path));
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
