/**
 * Estrae gli endpoint registrati dal backend leggendo `Routing/**` e `Program.cs`.
 *
 * Tre insidie che il parser deve gestire, misurate sul codice reale:
 *   1. il path di una Map è relativo al gruppo della VARIABILE che la riceve, e
 *      un file può dichiarare più gruppi annidati;
 *   2. il prefisso esterno arriva da Program.cs, in due forme di registrazione;
 *   3. l'autorizzazione è spesso dichiarata sul gruppo, non sull'endpoint.
 *
 * Spec: docs/for-developers/specs/2026-08-26-full-feature-audit-design.md
 */

import { readdirSync, readFileSync, statSync } from 'node:fs';
import path from 'node:path';

import type { EndpointEntry } from './types';

const MAP_CALL = /(\w+)\.Map(Get|Post|Put|Delete|Patch)\(\s*"([^"]*)"/g;
// `\s*\.\s*`: la dichiarazione può andare a capo prima del punto (`var group = app\n    .MapGroup(...)`,
// come in AdminKbQualityEndpoints): senza, il gruppo si perde e le sue rotte escono senza prefisso.
const GROUP_DECL = /var\s+(\w+)\s*=\s*(\w+)\s*\.\s*MapGroup\(\s*"([^"]+)"\s*\)/g;
const PROGRAM_GROUP = /(\w+)\.MapGroup\(\s*"([^"]+)"\s*\)\s*\.\s*(Map\w+)\(/g;
const PROGRAM_DIRECT = /(\w+)\.(Map\w+Endpoints|Map\w+Routes)\(\s*\)/g;
const ADMIN_POLICY = /admin/i;

// Il progetto protegge gli endpoint con filtri custom definiti in
// Extensions/EndpointFilterExtensions.cs, non con le policy ASP.NET: 890 usi
// contro 264. Senza questi, l'81% degli endpoint resterebbe non classificato.
const ADMIN_FILTERS = ['.RequireAdminSession()', 'AddEndpointFilter<RequireAdminSessionFilter>'];
const AUTHENTICATED_FILTERS = [
  '.RequireAuthenticatedUser()',
  '.RequireSession()',
  '.RequireLiveSessionParticipant()',
  'AddEndpointFilter<RequireSessionFilter>',
  'AddEndpointFilter<RequireAuthenticatedUserFilter>',
  'AddEndpointFilter<RequireLiveSessionParticipantFilter>',
];
const HANDLER_REF = /\.Map(?:Get|Post|Put|Delete|Patch)\(\s*"[^"]*"\s*,\s*(\w+)\s*\)/;

export type GroupInfo = { prefix: string; auth: EndpointEntry['auth'] | null };

/**
 * Ritorna la statement C# che inizia a `index`: fino al ';' a profondità zero,
 * saltando stringhe e commenti.
 *
 * Serve perché gli handler sono lambda inline il cui corpo è pieno di ';':
 * fermarsi al primo significherebbe non vedere mai i modificatori che seguono
 * la chiusura del lambda, cioè proprio dove sta l'autorizzazione.
 */
export function statementFrom(source: string, index: number): string {
  let depth = 0;

  for (let i = index; i < source.length; i++) {
    const ch = source[i];
    const next = source[i + 1];

    if (ch === '/' && next === '/') {
      const eol = source.indexOf('\n', i);
      if (eol === -1) break;
      i = eol;
      continue;
    }
    if (ch === '/' && next === '*') {
      const end = source.indexOf('*/', i + 2);
      if (end === -1) break;
      i = end + 1;
      continue;
    }
    if (ch === '@' && next === '"') {
      i += 2;
      // In una verbatim string, "" è l'escape di una virgoletta.
      while (i < source.length && !(source[i] === '"' && source[i + 1] !== '"')) {
        i += source[i] === '"' ? 2 : 1;
      }
      continue;
    }
    if (ch === '"') {
      i++;
      while (i < source.length && source[i] !== '"') {
        i += source[i] === '\\' ? 2 : 1;
      }
      continue;
    }

    if (ch === '(' || ch === '{' || ch === '[') depth++;
    else if (ch === ')' || ch === '}' || ch === ']') depth--;
    else if (ch === ';' && depth === 0) return source.slice(index, i + 1);
  }

  return source.slice(index);
}

/** Deduce l'autorizzazione da una catena di modificatori. null = nessun modificatore. */
export function authFromChain(chain: string): EndpointEntry['auth'] | null {
  // Il vincolo più restrittivo vince: un endpoint che combina più filtri non va
  // declassato al meno severo.
  if (ADMIN_FILTERS.some(f => chain.includes(f))) return 'admin';
  if (chain.includes('.AllowAnonymous()')) return 'anonymous';
  if (AUTHENTICATED_FILTERS.some(f => chain.includes(f))) return 'authenticated';
  if (!chain.includes('.RequireAuthorization(')) return null;
  const policy = chain.match(/RequireAuthorization\(([^;]*?)\)\s*(?:\.|;)/)?.[1] ?? '';
  return ADMIN_POLICY.test(policy) ? 'admin' : 'authenticated';
}

/**
 * Corpo del metodo `name`, graffe bilanciate. Stringa vuota se non esiste.
 *
 * Serve perché diversi file non dichiarano l'autorizzazione nella catena ma la
 * controllano imperativamente nell'handler (`context.RequireAdminSession()`).
 */
export function methodBody(source: string, name: string): string {
  const decl = new RegExp(`static[\\w\\s<>?\\[\\],.]*\\b${name}\\s*\\(`).exec(source);
  if (!decl) return '';

  const open = source.indexOf('{', decl.index + decl[0].length);
  if (open === -1) return '';

  let depth = 0;
  for (let i = open; i < source.length; i++) {
    if (source[i] === '{') depth++;
    else if (source[i] === '}' && --depth === 0) return source.slice(open, i + 1);
  }
  return source.slice(open);
}

/**
 * Livello di protezione di un endpoint, per precedenza decrescente.
 *
 * Ordine: catena dell'endpoint -> corpo dell'handler riferito per nome -> gruppo.
 * Quando la catena non nomina un handler NON si interroga `methodBody`: con un
 * nome vuoto la sua espressione regolare combacia con il primo metodo statico
 * del file, e l'endpoint erediterebbe l'autorizzazione di codice che non c'entra.
 * Se nessuno dei tre parla, il livello va letto a mano e finisce nelle note.
 */
export function authOf(
  source: string,
  chain: string,
  group: GroupInfo | undefined
): EndpointEntry['auth'] {
  const handler = chain.match(HANDLER_REF)?.[1];
  return (
    authFromChain(chain) ??
    (handler ? authFromChain(methodBody(source, handler)) : null) ??
    group?.auth ??
    'unknown'
  );
}

/** Mappa variabile di gruppo → prefisso risolto e autorizzazione ereditata. */
export function parseGroupPrefixes(source: string): Map<string, GroupInfo> {
  const groups = new Map<string, GroupInfo>();
  for (const m of source.matchAll(GROUP_DECL)) {
    const [, name, receiver, groupPath] = m;
    const index = m.index ?? 0;
    const decl = statementFrom(source, index);
    const parent = groups.get(receiver);
    groups.set(name, {
      prefix: `${parent?.prefix ?? ''}${groupPath}`,
      auth: authFromChain(decl) ?? parent?.auth ?? null,
    });
  }
  return groups;
}

/** Costruisce la mappa metodoDiRegistrazione → prefisso completo leggendo Program.cs. */
export function parseProgramPrefixes(source: string): Map<string, string> {
  const rootMatch = source.match(/var\s+(\w+)\s*=\s*app\.MapGroup\(\s*"([^"]+)"\s*\)/);
  const rootVar = rootMatch?.[1] ?? 'app';
  const rootPrefix = rootMatch?.[2] ?? '';
  const prefixes = new Map<string, string>();

  for (const m of source.matchAll(PROGRAM_GROUP)) {
    const [, varName, groupPath, method] = m;
    prefixes.set(method, `${varName === rootVar ? rootPrefix : ''}${groupPath}`);
  }
  for (const m of source.matchAll(PROGRAM_DIRECT)) {
    const [, varName, method] = m;
    if (!prefixes.has(method)) prefixes.set(method, varName === rootVar ? rootPrefix : '');
  }
  return prefixes;
}

/** Estrae gli endpoint di un file, risolvendo il prefisso per variabile ricevente. */
export function parseRoutingFile(
  source: string,
  file: string,
  groupPrefix: string
): EndpointEntry[] {
  const groups = parseGroupPrefixes(source);
  const found: EndpointEntry[] = [];

  for (const m of source.matchAll(MAP_CALL)) {
    const [, receiver, verb, routePath] = m;
    const index = m.index ?? 0;
    // La catena arriva fino alla fine della statement, oltre il lambda inline.
    const chain = statementFrom(source, index);
    const group = groups.get(receiver);

    found.push({
      method: verb.toUpperCase(),
      // ASP.NET inserisce la '/' fra il prefisso del gruppo e un pattern relativo: `MapGet("{id:guid}")`
      // sotto `/evaluations` è `/evaluations/{id:guid}`, non `/evaluations{id:guid}`.
      path: `${groupPrefix}${group?.prefix ?? ''}${routePath === '' || routePath.startsWith('/') ? routePath : `/${routePath}`}`.replace(
        /\/{2,}/g,
        '/'
      ),
      // Precedenza: catena dell'endpoint → corpo dell'handler → gruppo. Se
      // nessuno dei tre parla, il livello di protezione va letto a mano e
      // finisce nelle note del tracker.
      auth: authOf(source, chain, group),
      tags: [...chain.matchAll(/\.WithTags\(\s*"([^"]+)"/g)].map(t => t[1]),
      file,
      line: source.slice(0, index).split('\n').length,
    });
  }
  return found;
}

/**
 * Nome del metodo che registra gli endpoint del file.
 *
 * Deve accettare `internal static` oltre a `public static`: diversi file
 * (AdminBulkImportEndpoints, AdminGameImportWizardEndpoints, …) usano `internal`
 * e dichiarano il prefisso `/api/v1/...` per intero al proprio interno. Non
 * riconoscerli fa applicare il prefisso di default e produce `/api/v1/api/v1/...`.
 */
export function registrarName(source: string): string {
  return (
    source.match(/(?:public|internal)\s+static\s+[\w<>?[\],. ]+?\s*\b(Map\w+)\s*\(/)?.[1] ?? ''
  );
}

/**
 * Percorre le radici indicate (default: solo `Routing/`, come l'audit) applicando i prefissi dedotti da
 * Program.cs. Altre rotte vere stanno in `BoundedContexts/*\/Routing`: chi confronta le chiamate dei
 * client con le rotte passa anche `BoundedContexts`, altrimenti vede come «sconosciute» rotte che esistono.
 */
export function extractApiEndpoints(
  apiDir: string,
  roots: readonly string[] = ['Routing']
): EndpointEntry[] {
  const prefixes = parseProgramPrefixes(readFileSync(path.join(apiDir, 'Program.cs'), 'utf8'));
  const found: EndpointEntry[] = [];

  const walk = (dir: string): void => {
    for (const name of readdirSync(dir).sort()) {
      const full = path.join(dir, name);
      if (statSync(full).isDirectory()) {
        walk(full);
        continue;
      }
      if (!name.endsWith('.cs')) continue;
      const source = readFileSync(full, 'utf8');
      const registrar = registrarName(source);
      const rel = path.relative(apiDir, full).split(path.sep).join('/');
      found.push(...parseRoutingFile(source, rel, prefixes.get(registrar) ?? '/api/v1'));
    }
  };

  for (const root of roots) walk(path.join(apiDir, root));
  return found.sort((a, b) => a.file.localeCompare(b.file) || a.line - b.line);
}
