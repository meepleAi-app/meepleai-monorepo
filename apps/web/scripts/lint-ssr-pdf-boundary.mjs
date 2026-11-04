#!/usr/bin/env node
/**
 * lint-ssr-pdf-boundary.mjs — issue #4058, follow-up gate.
 *
 * Impedisce che `react-pdf` / `pdfjs-dist` rientrino nel grafo valutato dal render server.
 *
 * ## Il difetto che questo gate rende impossibile
 *
 * Quei pacchetti toccano API del DOM al momento del caricamento del modulo. Se una entry di
 * rotta (`page.tsx` / `layout.tsx`) li raggiunge per import statici, Next li valuta durante il
 * render server e la rotta esplode. La correzione di #4058 e' stata mettere un confine; questo
 * script impedisce che venga rimosso o aggirato da un import aggiunto altrove nella catena.
 *
 * ## 🔴 Il punto su cui il gate si gioca
 *
 * **`dynamic()` senza `ssr: false` NON e' un confine.** Nell'App Router il percorso e'
 * `next/dist/shared/lib/lazy-dynamic/loadable.js` — *non* `next/dist/shared/lib/dynamic.js`, che
 * e' l'implementazione del Pages Router e in questa app non gira mai. In `loadable.js`
 * `defaultOptions` porta `ssr: true`, e il ramo e':
 *
 *     const children = opts.ssr
 *       ? <Fragment>…<Lazy {...props} /></Fragment>
 *       : <BailoutToCSR reason="next/dynamic"><Lazy {...props} /></BailoutToCSR>
 *
 * Solo il secondo tiene il modulo fuori dal render server. Col default, `React.lazy` valuta il
 * loader anche la'. **E `'use client'` non e' un confine**: una direttiva client non impedisce
 * la valutazione server del modulo, decide solo dove vive l'interattivita'.
 *
 * ⚠️ Un probe che tratta ogni `dynamic(() => import(...))` come confine produce **lo zero che
 * conferma il fix**. E' accaduto davvero durante #4062: un probe scritto cosi' ha riportato
 * «4 percorsi → 0 dopo il fix» mentre la misura corretta diceva «5 → 1», e l'unico superstite
 * era proprio la rotta che un esame adversarial ha poi trovato rotta. Per questo il
 * riconoscimento del confine qui pretende `ssr: false` nello stesso argomento di opzioni.
 *
 * ## Cosa calcola, in ordine
 *
 * 1. I moduli **contaminati**: chiusura transitiva *all'indietro* degli import statici di
 *    `react-pdf` / `pdfjs-dist`. Si parte da chi li importa e si risale a chi importa quelli.
 *    Un arco che passa da un confine non propaga la contaminazione.
 * 2. Per ogni entry di rotta, se raggiunge un modulo contaminato → errore, stampando la
 *    **catena** completa e non solo il file, perche' il file da correggere e' quasi sempre un
 *    anello intermedio.
 *
 * ## Uso
 *
 *   pnpm lint:ssr-pdf           # exit 0 se pulito, exit 1 su ogni catena trovata
 *   pnpm lint:ssr-pdf --verbose # stampa anche l'insieme contaminato e i confini riconosciuti
 *
 * Refs:
 *   Issue: https://github.com/meepleAi-app/meepleai-monorepo/issues/4058
 */

import { readdirSync, readFileSync, statSync, existsSync } from 'node:fs';
import { dirname, join, relative, resolve } from 'node:path';
import { fileURLToPath } from 'node:url';

const HERE = dirname(fileURLToPath(import.meta.url));
const WEB_ROOT = resolve(HERE, '..');
const SRC = join(WEB_ROOT, 'src');
const VERBOSE = process.argv.includes('--verbose');

/** Pacchetti il cui **JS** non deve essere valutato dal render server. */
const FORBIDDEN_PACKAGES = ['react-pdf', 'pdfjs-dist'];

/**
 * 🔴 Gli asset non sono JS, e questa esclusione e' load-bearing.
 *
 * `PdfInlineViewer.tsx` importa STATICAMENTE `react-pdf/dist/Page/AnnotationLayer.css` e
 * `.../TextLayer.css`, e lo fa **di proposito**: il commento di #4058 nel file lo dichiara —
 * «a stylesheet is resolved by the bundler and never `require`d in Node, so it cannot throw».
 * La prima stesura di questo gate non distingueva, trattava quei due import come accesso al
 * pacchetto, e riportava **cinque catene di violazione su codice gia' corretto**: avrebbe
 * bloccato la CI chiedendo di disfare la correzione che proteggeva.
 *
 * Il difetto vero e' la valutazione del JS: `pdfjs-dist` tocca `Iterator.prototype` al
 * caricamento del modulo e lancia su Node 20. Un `.css` non esegue nulla.
 */
const ASSET_EXTENSIONS = ['.css', '.scss', '.sass', '.less', '.svg', '.png', '.jpg', '.webp'];

function isJsAccessToForbiddenPackage(specifier) {
  if (ASSET_EXTENSIONS.some(ext => specifier.endsWith(ext))) return false;
  return FORBIDDEN_PACKAGES.some(pkg => specifier === pkg || specifier.startsWith(pkg + '/'));
}

const EXTENSIONS = ['.ts', '.tsx', '.js', '.jsx', '.mjs'];

// ---------------------------------------------------------------------------
// Raccolta dei file
// ---------------------------------------------------------------------------

function walk(dir, out = []) {
  for (const entry of readdirSync(dir)) {
    if (entry === 'node_modules' || entry === '__tests__' || entry.startsWith('.')) continue;
    const full = join(dir, entry);
    const st = statSync(full);
    if (st.isDirectory()) {
      walk(full, out);
    } else if (EXTENSIONS.some(ext => entry.endsWith(ext)) && !/\.(test|spec|stories)\./.test(entry)) {
      out.push(full);
    }
  }
  return out;
}

function stripComments(source) {
  return source.replace(/\/\*[\s\S]*?\*\//g, '').replace(/\/\/.*$/gm, '');
}

// ---------------------------------------------------------------------------
// Risoluzione degli import
// ---------------------------------------------------------------------------

/** Risolve uno specifier relativo o con alias `@/` a un file reale. Null se non risolvibile. */
function resolveSpecifier(specifier, fromFile) {
  let base;
  if (specifier.startsWith('@/')) {
    base = join(SRC, specifier.slice(2));
  } else if (specifier.startsWith('.')) {
    base = resolve(dirname(fromFile), specifier);
  } else {
    return null; // pacchetto esterno: gestito separatamente
  }

  for (const ext of ['', ...EXTENSIONS]) {
    const candidate = base + ext;
    if (existsSync(candidate) && statSync(candidate).isFile()) return candidate;
  }
  for (const ext of EXTENSIONS) {
    const candidate = join(base, 'index' + ext);
    if (existsSync(candidate) && statSync(candidate).isFile()) return candidate;
  }
  return null;
}

/**
 * Estrae gli import STATICI di un file: `import … from 'x'`, `export … from 'x'`,
 * `require('x')`. Esclude gli `import()` dinamici, che sono archi a parte.
 */
function staticImports(code) {
  const found = [];
  const patterns = [
    /\bimport\s+(?:[\w*{}\s,]+\s+from\s+)?['"]([^'"]+)['"]/g,
    /\bexport\s+(?:[\w*{}\s,]+\s+)?from\s+['"]([^'"]+)['"]/g,
    /\brequire\(\s*['"]([^'"]+)['"]\s*\)/g,
  ];
  for (const re of patterns) {
    let m;
    while ((m = re.exec(code)) !== null) found.push(m[1]);
  }
  return found;
}

/**
 * Partiziona gli specifier caricati via `dynamic(() => import('X'), …)` in due insiemi.
 *
 * 🔴 **Entrambi gli insiemi servono, e il secondo e' il motivo per cui questo gate esiste.**
 *   - con `ssr: false` → **confine**: il modulo non entra nel passaggio server.
 *   - senza           → **arco server**: `React.lazy` valuta il loader anche sul server, quindi
 *     e' un accesso al pacchetto esattamente come un import statico.
 *
 * ⚠️ La prima stesura raccoglieva solo i confini, e un `dynamic()` senza `ssr: false` restava
 * **invisibile**: `staticImports` non vede un `import()` dentro una arrow function, quindi lo
 * specifier non entrava da nessuna parte. Verificato per perturbazione: tolto `ssr: false` dai
 * due `dynamic()` di `PdfInlineViewer.tsx` — cioe' riaprendo esattamente il difetto di #4058 —
 * il gate restava **VERDE**. Aveva il difetto che era scritto per prevenire, e il suo stesso
 * docstring lo dichiarava come «il punto su cui il gate si gioca».
 *
 * La finestra di testo va dalla chiamata `dynamic(` alla sua parentesi di chiusura bilanciata,
 * cosi' `ssr: false` deve stare in QUELLA chiamata e non altrove nel file.
 *
 * Nota di perimetro: un `import('X')` **fuori** da `dynamic()` non e' classificato qui. Un
 * `await import()` dentro un gestore di eventi non viene valutato dal render server, e
 * distinguerlo da uno a livello di modulo richiede analisi di flusso. Il caso documentato di
 * #4058 passa da `dynamic()`, che e' anche l'unica forma che il progetto usa per questi
 * pacchetti (`grep -n "import('react-pdf')" src/`).
 */
function classifyDynamicImports(code) {
  const boundaries = new Set();
  const serverEvaluated = new Set();
  const re = /\bdynamic\s*\(/g;
  let m;
  while ((m = re.exec(code)) !== null) {
    let depth = 0;
    let i = m.index + m[0].length - 1; // sulla '('
    for (; i < code.length; i++) {
      if (code[i] === '(') depth++;
      else if (code[i] === ')') {
        depth--;
        if (depth === 0) break;
      }
    }
    const call = code.slice(m.index, i + 1);
    const target = /\bssr\s*:\s*false\b/.test(call) ? boundaries : serverEvaluated;
    const imp = /\bimport\s*\(\s*['"]([^'"]+)['"]\s*\)/g;
    let inner;
    while ((inner = imp.exec(call)) !== null) target.add(inner[1]);
  }
  return { boundaries, serverEvaluated };
}

// ---------------------------------------------------------------------------
// Grafo
// ---------------------------------------------------------------------------

const files = walk(SRC);
/** file -> { staticSpecifiers, boundarySpecifiers, importsForbiddenDirectly } */
const graph = new Map();

for (const file of files) {
  const code = stripComments(readFileSync(file, 'utf8'));
  const { boundaries, serverEvaluated } = classifyDynamicImports(code);
  // Gli specifier EFFETTIVI del passaggio server: gli import statici piu` i `dynamic()` che
  // NON dichiarano `ssr: false` — quelli React.lazy li valuta anche sul server.
  const specifiers = [...staticImports(code), ...serverEvaluated];
  graph.set(file, {
    specifiers,
    boundaries,
    importsForbiddenDirectly: specifiers.some(isJsAccessToForbiddenPackage),
  });
}

/** Archi diretti file -> file, ESCLUSI quelli che passano da un confine `ssr: false`. */
const edges = new Map();
for (const [file, info] of graph) {
  const targets = new Set();
  for (const spec of info.specifiers) {
    if (info.boundaries.has(spec)) continue; // non e' un arco server
    const resolved = resolveSpecifier(spec, file);
    if (resolved && graph.has(resolved)) targets.add(resolved);
  }
  edges.set(file, targets);
}

/**
 * Chiusura all'indietro: un file e' contaminato se importa direttamente un pacchetto vietato
 * (e non dietro un confine), oppure se importa un file contaminato.
 */
const contaminated = new Set();
for (const [file, info] of graph) {
  if (!info.importsForbiddenDirectly) continue;
  // Se l'unico accesso al pacchetto e' dietro un confine, il file non e' contaminato.
  const behindBoundaryOnly = info.specifiers
    .filter(isJsAccessToForbiddenPackage)
    .every(s => info.boundaries.has(s));
  if (!behindBoundaryOnly) contaminated.add(file);
}

let grew = true;
while (grew) {
  grew = false;
  for (const [file, targets] of edges) {
    if (contaminated.has(file)) continue;
    for (const target of targets) {
      if (contaminated.has(target)) {
        contaminated.add(file);
        grew = true;
        break;
      }
    }
  }
}

// ---------------------------------------------------------------------------
// Entry di rotta e catene
// ---------------------------------------------------------------------------

const rel = f => relative(WEB_ROOT, f).replace(/\\/g, '/');

const routeEntries = files.filter(f => {
  const r = rel(f);
  return r.startsWith('src/app/') && /\/(page|layout)\.(tsx|ts|jsx|js)$/.test(r);
});

/** Catena piu` corta dall'entry a un modulo che importa direttamente un pacchetto vietato. */
function chainToForbidden(entry) {
  const queue = [[entry]];
  const seen = new Set([entry]);
  while (queue.length > 0) {
    const path = queue.shift();
    const last = path[path.length - 1];
    if (graph.get(last)?.importsForbiddenDirectly && contaminated.has(last)) return path;
    for (const next of edges.get(last) ?? []) {
      if (seen.has(next) || !contaminated.has(next)) continue;
      seen.add(next);
      queue.push([...path, next]);
    }
  }
  return null;
}

const violations = [];
for (const entry of routeEntries) {
  if (!contaminated.has(entry)) continue;
  violations.push({ entry, chain: chainToForbidden(entry) ?? [entry] });
}

// ---------------------------------------------------------------------------
// Esito
// ---------------------------------------------------------------------------

console.log(`SSR/PDF boundary gate (#4058)`);
console.log(`   file analizzati        : ${files.length}`);
console.log(`   entry di rotta         : ${routeEntries.length}`);
console.log(`   moduli contaminati     : ${contaminated.size}`);

if (VERBOSE) {
  const boundaryFiles = [...graph].filter(([, i]) => i.boundaries.size > 0);
  console.log(`   confini \`ssr: false\`   : ${boundaryFiles.length}`);
  for (const [f, i] of boundaryFiles) {
    console.log(`      ${rel(f)} -> ${[...i.boundaries].join(', ')}`);
  }
  if (contaminated.size > 0) {
    console.log('   insieme contaminato:');
    for (const f of [...contaminated].sort()) console.log(`      ${rel(f)}`);
  }
}

if (routeEntries.length === 0) {
  // Guardia di non-vacuita'. Un gate che non trova entry passerebbe sempre, e il motivo
  // (struttura delle cartelle cambiata, glob sbagliata) resterebbe invisibile.
  console.error('\n❌ nessuna entry di rotta trovata sotto src/app/ — il gate non ha guardato nulla.');
  process.exit(1);
}

if (violations.length === 0) {
  console.log('\n✅ nessuna entry di rotta raggiunge react-pdf/pdfjs-dist senza un confine `ssr: false`.');
  process.exit(0);
}

console.error(`\n❌ ${violations.length} entry di rotta raggiunge(ono) un pacchetto vietato dal grafo server:\n`);
for (const { chain } of violations) {
  console.error('   catena:');
  chain.forEach((f, idx) => console.error(`      ${'  '.repeat(idx)}${idx === 0 ? '' : '└─ '}${rel(f)}`));
  console.error('');
}
console.error('   Il confine e\' SOLO `dynamic(() => import("X"), { ssr: false })`.');
console.error('   `dynamic()` senza `ssr: false` e `\'use client\'` NON sono confini: vedi');
console.error('   l\'intestazione di scripts/lint-ssr-pdf-boundary.mjs e la issue #4058.');
process.exit(1);
