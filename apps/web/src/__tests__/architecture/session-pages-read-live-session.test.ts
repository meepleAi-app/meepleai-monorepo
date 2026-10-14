/**
 * Gate di architettura — #4156: le pagine sotto `/sessions/[id]` parlano solo con la
 * `LiveGameSession`.
 *
 * **Il difetto che questo gate tiene fermo.** L'id nella rotta è quello di una `LiveGameSession`
 * (decisione D4 di #4156), ma le pagine leggevano e scrivevano anche altri due aggregati:
 * `GameSession` di GameManagement (`/api/v1/sessions/{id}`) e la `Session` di SessionTracking
 * (`/api/v1/game-sessions/{id}`). Con l'id di una live entrambi rispondono 404, e la pagina
 * dopo «Crea Sessione» era un errore. ADR-089 §2 vieta di indovinare l'aggregato dall'id: o la
 * pagina legge la live, o non legge niente.
 *
 * **Perché sugli import e non sul testo delle pagine.** La prima stesura del criterio chiedeva di
 * cercare `/api/v1/game-sessions/` nei file sotto `sessions/[id]/`. Non vedeva il difetto: la
 * chiamata sbagliata passa quasi sempre da un hook o da un componente fuori dalla cartella
 * (`useSessionDetail`, `ScoreboardPage`, `useUpdateSessionScores`). Questo gate parte dalle pagine
 * e segue gli import nel sorgente, e controlla ogni file raggiunto.
 *
 * **È un cricchetto.** Alla sua introduzione le pagine violavano la regola in più punti, e ognuno
 * è un'eccezione in `KNOWN` con la issue che la chiude. Il gate fallisce in due casi: una
 * violazione nuova, e un'eccezione che non si verifica più (va tolta, così l'elenco resta vero).
 *
 * ── Limiti dichiarati ───────────────────────────────────────────────────────────────────────
 *
 * 1. **Niente parser TypeScript.** Gli import si leggono con espressioni regolari: le forme
 *    `import … from '…'`, `export … from '…'` e `import('…')` con una stringa letterale.
 * 2. **Dai barili segue solo i nomi importati.** `import { A } from '@/components/session'` segue
 *    la riga del barile che esporta `A`, non tutte le altre: senza questo, un barile porta nella
 *    chiusura componenti che la pagina non monta. `export *` si segue sempre.
 * 3. **`lib/api/**` non si attraversa.** Lì si definiscono i client, non si usano. Si controlla
 *    invece chi importa direttamente i client degli altri aggregati (`FORBIDDEN_CLIENTS`); un
 *    `import type` non conta, perché non chiama niente.
 * 4. **Raggiungibilità statica.** Un file importato conta anche se la pagina lo monta dietro un
 *    ramo che non percorre mai.
 */

import { existsSync, readFileSync, statSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';

import { sync as globSync } from 'glob';
import { describe, expect, it } from 'vitest';

const SRC = join(process.cwd(), 'src');
const SESSION_PAGES_DIR = join(SRC, 'app', '(authenticated)', 'sessions', '[id]');

/**
 * Uso, nel codice di un file raggiunto, di un altro aggregato. Gli spazi fra i membri sono
 * ammessi: una catena come `api.sessions\n  .getById(id)` va a capo, e un pattern stretto la
 * perderebbe (la pagina delle note scrive proprio così).
 */
const FORBIDDEN_USAGE: ReadonlyArray<readonly [name: string, pattern: RegExp]> = [
  ['api.sessions.', /\bapi\s*\.\s*sessions\s*\./],
  ['api.sessionTracking.', /\bapi\s*\.\s*sessionTracking\s*\./],
  ['api.agentSessions.', /\bapi\s*\.\s*agentSessions\s*\./],
  ['api.games.getSessions', /\bapi\s*\.\s*games\s*\.\s*getSessions\b/],
  // Il client «Session Flow v2.1» parla con SessionTracking per tutto ciò che prende l'id di una
  // sessione (`/api/v1/sessions/{id}/diary`, `/turn/advance`, `/scores-with-diary`, il lancio dei
  // dadi su `/game-sessions/{id}`). La fetta 0 non lo aveva in elenco, e il riepilogo leggeva il
  // diario di SessionTracking con l'id di una live senza che il gate se ne accorgesse.
  ['api.sessionFlow.', /\bapi\s*\.\s*sessionFlow\s*\./],
  ['/api/v1/sessions/', /\/api\/v1\/sessions\//],
  ['/api/v1/game-sessions/', /\/api\/v1\/game-sessions\//],
];

/** Client degli altri aggregati, importati direttamente invece che attraverso `api`. */
const FORBIDDEN_CLIENTS: ReadonlyArray<string> = [
  'lib/api/clients/sessionsClient.ts',
  'lib/api/clients/gameSessionsClient.ts',
  'lib/api/clients/sessionTrackingClient.ts',
  'lib/api/clients/agentSessionsClient.ts',
  'lib/api/clients/sessionAgentClient.ts',
];

/**
 * Le violazioni ammesse, ciascuna con la issue che la chiude. Vuoto dalla fetta 2 di #4156,
 * quando l'ultima — la scheda Score — è passata a `/live-sessions/{id}/scores`: una voce nuova
 * va motivata con la issue che la chiude.
 * Chiave: `pagina d'ingresso ⇒ file del sorgente · cosa usa`.
 */
const KNOWN: Readonly<Record<string, string>> = {};

// ─── Analisi ────────────────────────────────────────────────────────────────────────────────

const rel = (file: string): string => relative(SRC, file).replace(/\\/g, '/');

function stripComments(source: string): string {
  return source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/(^|[^:])\/\/.*$/gm, '$1');
}

function isSkipped(file: string): boolean {
  return /[\\/]__tests__[\\/]|\.test\.|\.stories\./.test(file);
}

function isApiLayer(file: string): boolean {
  return rel(file).startsWith('lib/api/');
}

function resolveSpecifier(from: string, specifier: string): string | null {
  let base: string;
  if (specifier.startsWith('@/')) base = join(SRC, specifier.slice(2));
  else if (specifier.startsWith('.')) base = resolve(dirname(from), specifier);
  else return null;
  for (const candidate of [
    base,
    `${base}.ts`,
    `${base}.tsx`,
    join(base, 'index.ts'),
    join(base, 'index.tsx'),
  ]) {
    if (existsSync(candidate) && statSync(candidate).isFile()) return candidate;
  }
  return null;
}

interface ImportEdge {
  specifier: string;
  /** Nomi importati; `null` = tutto (namespace, side effect, `export *`, import dinamico). */
  names: Set<string> | null;
  typeOnly: boolean;
}

/** `{ type A, type B }`: tutti i nomi sono tipi, quindi l'import non porta codice. */
function isTypeOnlyClause(clause: string): boolean {
  const named = /^\{([^}]*)\}$/.exec(clause.trim());
  if (!named) return false;
  const parts = named[1]
    .split(',')
    .map(part => part.trim())
    .filter(Boolean);
  return parts.length > 0 && parts.every(part => part.startsWith('type '));
}

function parseNames(clause: string): Set<string> | null {
  const named = /\{([\s\S]*?)\}/.exec(clause);
  if (/\*\s+as\s+\w+/.test(clause)) return null;
  const names = new Set<string>();
  if (named) {
    for (const part of named[1].split(',')) {
      const name = part
        .replace(/^\s*type\s+/, '')
        .split(/\s+as\s+/)[0]
        .trim();
      if (name) names.add(name);
    }
  }
  const defaultImport = /^\s*(?:type\s+)?([A-Za-z_$][\w$]*)\s*(?:,|$)/.exec(
    clause.replace(/\{[\s\S]*\}/, '')
  );
  if (defaultImport) names.add('default');
  return names;
}

/** Gli import del file, senza le riesportazioni. */
function importsOf(source: string): ImportEdge[] {
  const edges: ImportEdge[] = [];
  // La clausola non può contenere `;` né virgolette: senza questo vincolo, un `import './x.css'`
  // farebbe leggere la clausola fino al `from` dell'istruzione successiva.
  for (const m of source.matchAll(/import\s+(type\s+)?([^;'"]*?)\s+from\s*['"]([^'"]+)['"]/g)) {
    edges.push({
      specifier: m[3],
      names: parseNames(m[2]),
      typeOnly: Boolean(m[1]) || isTypeOnlyClause(m[2]),
    });
  }
  for (const m of source.matchAll(/import\s*['"]([^'"]+)['"]/g)) {
    edges.push({ specifier: m[1], names: null, typeOnly: false });
  }
  for (const m of source.matchAll(/import\(\s*['"]([^'"]+)['"]\s*\)/g)) {
    edges.push({ specifier: m[1], names: null, typeOnly: false });
  }
  return edges;
}

/** Le riesportazioni del file: chi chiede `name` segue solo quelle che lo forniscono. */
function reExportsFor(source: string, names: Set<string> | null): ImportEdge[] {
  const edges: ImportEdge[] = [];
  for (const m of source.matchAll(/export\s+\*\s+(?:as\s+[\w$]+\s+)?from\s*['"]([^'"]+)['"]/g)) {
    edges.push({ specifier: m[1], names: null, typeOnly: false });
  }
  for (const m of source.matchAll(/export\s+(type\s+)?\{([^}]*)\}\s*from\s*['"]([^'"]+)['"]/g)) {
    const provided = new Map<string, string>();
    for (const part of m[2].split(',')) {
      const clean = part.replace(/^\s*type\s+/, '').trim();
      if (!clean) continue;
      const [original, alias] = clean.split(/\s+as\s+/).map(s => s.trim());
      provided.set(alias ?? original, original);
    }
    const wanted =
      names === null
        ? new Set(provided.values())
        : new Set([...names].filter(n => provided.has(n)).map(n => provided.get(n)!));
    if (wanted.size > 0) edges.push({ specifier: m[3], names: wanted, typeOnly: Boolean(m[1]) });
  }
  return edges;
}

/** I file che Next monta come segmenti della rotta: sono gli ingressi dell'analisi. */
const ROUTE_FILE = /(^|\/)(page|layout|error|loading|not-found|template)\.tsx$/;

interface Analysis {
  /** File raggiunti dall'ingresso, con il file da cui sono stati raggiunti la prima volta. */
  parentOf: Map<string, string | null>;
  /** `ingresso ⇒ file · uso` → catena di import che lo raggiunge. */
  violations: Map<string, string>;
}

/**
 * Chiusura degli import di un ingresso. Una per ingresso, e non una sola per tutti: così la
 * chiave di una violazione dice anche QUALE pagina la raggiunge, e una pagina che si mettesse a
 * importare un hook già in `KNOWN` per un'altra sarebbe una violazione nuova.
 */
function analyseFrom(entry: string): Analysis {
  const entryKey = rel(entry);
  const parentOf = new Map<string, string | null>([[entry, null]]);
  const wantedNames = new Map<string, Set<string> | null>([[entry, null]]);
  const violations = new Map<string, string>();
  const queue: Array<[string, Set<string> | null]> = [[entry, null]];

  const chain = (file: string): string => {
    const steps: string[] = [];
    for (let at: string | null | undefined = file; at; at = parentOf.get(at))
      steps.unshift(rel(at));
    return steps.join(' → ');
  };

  while (queue.length > 0) {
    const [file, names] = queue.shift()!;
    const source = stripComments(readFileSync(file, 'utf8'));

    for (const [name, pattern] of FORBIDDEN_USAGE) {
      if (pattern.test(source)) violations.set(`${entryKey} ⇒ ${rel(file)} · ${name}`, chain(file));
    }

    const edges = [...importsOf(source), ...reExportsFor(source, names)];
    for (const edge of edges) {
      const target = resolveSpecifier(file, edge.specifier);
      if (!target || isSkipped(target)) continue;
      if (FORBIDDEN_CLIENTS.includes(rel(target)) && !edge.typeOnly) {
        violations.set(`${entryKey} ⇒ ${rel(file)} · import ${rel(target)}`, chain(file));
      }
      if (isApiLayer(target)) continue;

      // Un file si rivisita solo se gli si chiedono nomi nuovi; `null` vuol dire «tutto».
      const seen = wantedNames.has(target);
      const known = wantedNames.get(target) ?? null;
      if (seen && known === null) continue;
      const next = !seen || edge.names === null ? edge.names : new Set([...known!, ...edge.names]);
      if (seen && next !== null && next.size === known!.size) continue;
      wantedNames.set(target, next);
      if (!parentOf.has(target)) parentOf.set(target, file);
      queue.push([target, next]);
    }
  }
  return { parentOf, violations };
}

function analyse(): { entries: string[]; reached: Set<string>; violations: Map<string, string> } {
  const entries = globSync('**/*.tsx', { cwd: SESSION_PAGES_DIR, absolute: true }).filter(
    file => !isSkipped(file) && ROUTE_FILE.test(file.replace(/\\/g, '/'))
  );
  const reached = new Set<string>();
  const violations = new Map<string, string>();
  for (const entry of entries) {
    const analysis = analyseFrom(entry);
    for (const file of analysis.parentOf.keys()) reached.add(rel(file));
    for (const [key, chain] of analysis.violations) violations.set(key, chain);
  }
  return { entries: entries.map(rel), reached, violations };
}

// ─── Gate ───────────────────────────────────────────────────────────────────────────────────

describe('gate: le pagine di /sessions/[id] parlano solo con la LiveGameSession (#4156)', () => {
  const { entries, reached, violations } = analyse();

  it('raggiunge le pagine e ciò che montano', () => {
    // Guardia di non vacuità: una cartella spostata o una risoluzione rotta darebbero un gate
    // verde su un insieme vuoto.
    expect(entries).toContain('app/(authenticated)/sessions/[id]/page.tsx');
    expect(entries).toContain('app/(authenticated)/sessions/[id]/live/page.tsx');
    expect(reached).toContain(
      'app/(authenticated)/sessions/[id]/live/_components/SessionLiveView.tsx'
    );
    expect(reached).toContain('components/session/ScoreboardPage.tsx');
  });

  it('non aggiunge violazioni a quelle note', () => {
    const fresh = [...violations.entries()]
      .filter(([key]) => !(key in KNOWN))
      .map(([key, chain]) => `${key}\n      raggiunto da: ${chain}`);
    expect(
      fresh,
      'una pagina di /sessions/[id] arriva a un altro aggregato. La rotta porta l’id di una ' +
        'LiveGameSession: leggi /live-sessions, oppure togli la dipendenza.'
    ).toEqual([]);
  });

  it('non tiene eccezioni che non si verificano più', () => {
    const stale = Object.keys(KNOWN).filter(key => !violations.has(key));
    expect(stale, 'queste eccezioni sono risolte: toglile da KNOWN').toEqual([]);
  });
});
