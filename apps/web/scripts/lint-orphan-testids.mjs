#!/usr/bin/env node
/**
 * Gate #4120 — nessuna spec Playwright cerca un `data-testid` che il codice di produzione non
 * dichiara.
 *
 * Il difetto che chiude: una spec che cerca un selettore inesistente non è un test da aggiustare,
 * è non eseguibile per costruzione. Non rompe la compilazione, non rompe il lint, e il gate E2E
 * non gira sulle PR verso `main-dev` — quindi resta verde e muta. Tre istanze sono emerse a mano
 * in due giorni per vie indipendenti (lo scaffold #4068 eliminato in #4106, le spec agent di
 * #4105, `meeple-card` in #4110); la misura che ha motivato questo gate dice che non erano casi
 * isolati ma campioni. Il più grosso, `game-card`, è una vittima della migrazione
 * `GameCard` → `MeepleCard` dichiarata in `CLAUDE.md`: il componente è andato via e la suite E2E
 * non è stata spazzata.
 *
 * Perché un cricchetto e non «zero orfani»: la baseline al momento dell'introduzione è troppo
 * ampia perché un gate bloccante a zero sia introducibile. Il gate blocca il PEGGIORAMENTO — un
 * file nuovo deve essere a zero, un file esistente non deve crescere — e il percorso verso zero
 * resta leggibile nella baseline committata, file per file. Stesso spirito di
 * `lint:fidelity --max-baseline`.
 *
 * ── Limiti dichiarati ──────────────────────────────────────────────────────────────────────────
 *
 * 1. **Accusa solo ciò che può dimostrare.** Le dichiarazioni dinamiche hanno forma
 *    data-testid={`${testId}-apply`}: il valore vero non è risolvibile staticamente, quindi un
 *    id cercato che finisca in `-apply` viene classificato NON DIMOSTRABILE, non orfano. Lo stesso
 *    per i prefissi. Senza questa soppressione la prima misura dava 700 orfani invece di 547: un
 *    gate che segnala falsi positivi viene disattivato, e porta via con sé anche i veri.
 * 2. **Verifica l'esistenza, non la raggiungibilità.** Un testid dichiarato dietro un ramo che la
 *    spec non percorre conta come dichiarato. Coprire anche quello richiederebbe eseguire la UI.
 * 3. **Solo letterali dal lato spec.** `getByTestId(variabile)` e i template nelle spec non sono
 *    verificabili e vengono contati a parte.
 * 4. Le dichiarazioni si leggono dal codice di produzione: `__tests__/`, `*.test.*`, `*.spec.*` e
 *    `*.story/stories.*` sono esclusi di proposito. L'unica occorrenza di `meeple-card` nel repo è
 *    un mock Vitest, e contarla come dichiarazione avrebbe mascherato il difetto di #4110.
 *
 * Uso:
 *   node scripts/lint-orphan-testids.mjs             verifica contro la baseline
 *   node scripts/lint-orphan-testids.mjs --update     riscrive la baseline dalla misura corrente
 *   node scripts/lint-orphan-testids.mjs --list       elenca gli orfani con le loro sedi
 */

import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const WEB_ROOT = join(dirnameOf(import.meta.url), '..');
export const BASELINE_FILE = 'orphan-testids-baseline.json';

const SKIP_DIRS = new Set([
  'node_modules',
  '.next',
  'dist',
  'coverage',
  'test-results',
  'playwright-report',
  'audit-results',
  'storybook-static',
]);

function dirnameOf(url) {
  const p = fileURLToPath(url);
  return p.slice(0, p.lastIndexOf(sep));
}

/** Un file di produzione? `__tests__/`, test, spec e storie sono esclusi di proposito (limite 4). */
export function isProductionSource(path) {
  const unix = path.split(sep).join('/');
  if (/\/__tests__\//.test(unix)) return false;
  if (/\.(test|spec|story|stories)\.[tj]sx?$/.test(unix)) return false;
  return /\.[tj]sx?$/.test(unix);
}

/** Gli id che una spec cerca. Solo letterali: il resto non è verificabile (limite 3). */
export function soughtFromSource(source) {
  const ids = [];
  let dynamicSites = 0;
  source.split('\n').forEach((line, idx) => {
    for (const re of [/data-testid=["']([^"'$]+)["']/g, /getByTestId\(\s*["']([^"'$]+)["']/g]) {
      for (const m of line.matchAll(re)) ids.push({ id: m[1], line: idx + 1 });
    }
    if (/data-testid=["'][^"']*\$\{|getByTestId\(\s*[`A-Za-z_$]/.test(line)) dynamicSites++;
  });
  return { ids, dynamicSites };
}

/**
 * Gli id che un file di produzione dichiara, più i pattern che rendono un id indimostrabile.
 * `isConstantsModule` abilita la lettura dei letterali kebab-case da un modulo `*test-ids.ts`,
 * dove i testid vivono come costanti applicate altrove per spread.
 */
export function declaredFromSource(source, { isConstantsModule = false } = {}) {
  const literals = new Set();
  const suffixes = new Set();
  const prefixes = new Set();
  for (const m of source.matchAll(/data-testid=["']([^"'$]+)["']/g)) literals.add(m[1]);
  for (const m of source.matchAll(/data-testid=\{\s*["']([^"']+)["']\s*\}/g)) literals.add(m[1]);
  for (const m of source.matchAll(/data-testid=\{`([^`]+)`\}/g)) {
    const tpl = m[1];
    if (!tpl.includes('${')) {
      literals.add(tpl);
      continue;
    }
    const tail = tpl.slice(tpl.lastIndexOf('}') + 1);
    const head = tpl.slice(0, tpl.indexOf('${'));
    if (tail.length > 2) suffixes.add(tail);
    if (head.length > 2) prefixes.add(head);
  }
  if (isConstantsModule) {
    for (const m of source.matchAll(/["']([a-z0-9][a-z0-9-]{2,})["']/gi)) literals.add(m[1]);
  }
  return { literals, suffixes, prefixes };
}

export function isConstantsModule(path) {
  return /test-ids?\.[tj]sx?$/i.test(path.split(sep).join('/'));
}

/** Un id coperto da un pattern dinamico non è dimostrabile orfano (limite 1). */
export function isUnprovable(id, { suffixes, prefixes }) {
  for (const s of suffixes) if (id.endsWith(s)) return true;
  for (const p of prefixes) if (id.startsWith(p)) return true;
  return false;
}

export function classify(soughtIds, { literals, suffixes, prefixes }) {
  const orphans = [];
  const unprovable = [];
  const declared = [];
  for (const id of soughtIds) {
    if (literals.has(id)) declared.push(id);
    else if (isUnprovable(id, { suffixes, prefixes })) unprovable.push(id);
    else orphans.push(id);
  }
  return { orphans, unprovable, declared };
}

function walk(dir, matches, acc = []) {
  for (const entry of readdirSync(dir)) {
    if (SKIP_DIRS.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) walk(full, matches, acc);
    else if (matches(full)) acc.push(full);
  }
  return acc;
}

export function scan(webRoot) {
  const rel = f => relative(webRoot, f).split(sep).join('/');
  const specFiles = walk(join(webRoot, 'e2e'), f => /\.spec\.tsx?$/.test(f));
  const srcFiles = walk(join(webRoot, 'src'), isProductionSource);

  const sought = new Map();
  let dynamicSites = 0;
  for (const file of specFiles) {
    const { ids, dynamicSites: dyn } = soughtFromSource(readFileSync(file, 'utf8'));
    dynamicSites += dyn;
    for (const { id, line } of ids) {
      if (!sought.has(id)) sought.set(id, []);
      sought.get(id).push(`${rel(file)}:${line}`);
    }
  }

  const literals = new Set();
  const suffixes = new Set();
  const prefixes = new Set();
  for (const file of srcFiles) {
    const d = declaredFromSource(readFileSync(file, 'utf8'), {
      isConstantsModule: isConstantsModule(file),
    });
    for (const v of d.literals) literals.add(v);
    for (const v of d.suffixes) suffixes.add(v);
    for (const v of d.prefixes) prefixes.add(v);
  }

  const { orphans, unprovable, declared } = classify([...sought.keys()], {
    literals,
    suffixes,
    prefixes,
  });

  const perFile = new Map();
  for (const id of orphans) {
    for (const site of sought.get(id)) {
      const file = site.slice(0, site.lastIndexOf(':'));
      perFile.set(file, (perFile.get(file) ?? 0) + 1);
    }
  }

  return { specFiles, srcFiles, sought, orphans, unprovable, declared, perFile, dynamicSites };
}

/** Confronta la misura con la baseline. Nessun I/O: è la regola del cricchetto, testabile. */
export function compareToBaseline(current, baseline) {
  const brandNew = [];
  const grown = [];
  const improved = [];
  for (const [file, count] of Object.entries(current)) {
    if (!(file in baseline)) brandNew.push({ file, count });
    else if (count > baseline[file]) grown.push({ file, was: baseline[file], now: count });
    else if (count < baseline[file]) improved.push({ file, was: baseline[file], now: count });
  }
  const cleared = Object.keys(baseline).filter(file => !(file in current));
  return { brandNew, grown, improved, cleared, worsened: brandNew.length + grown.length > 0 };
}

function main() {
  const update = process.argv.includes('--update');
  const list = process.argv.includes('--list');
  const baselinePath = join(WEB_ROOT, BASELINE_FILE);

  const r = scan(WEB_ROOT);
  if (r.specFiles.length === 0 || r.srcFiles.length === 0) {
    console.error(
      `❌ la scansione non ha trovato nulla (spec: ${r.specFiles.length}, src: ${r.srcFiles.length}).\n` +
        '   Il gate non puo` verificare niente: controlla le radici `e2e/` e `src/`.'
    );
    process.exit(1);
  }

  const sites = [...r.perFile.values()].reduce((a, b) => a + b, 0);
  console.log(`Spec analizzate: ${r.specFiles.length} · file di produzione: ${r.srcFiles.length}`);
  console.log(
    `Id cercati: ${r.sought.size} · dichiarati: ${r.declared.length}` +
      ` · orfani dimostrabili: ${r.orphans.length} · indimostrabili: ${r.unprovable.length}`
  );
  console.log(
    `Sedi orfane: ${sites} in ${r.perFile.size} spec · sedi dinamiche non verificabili: ${r.dynamicSites}`
  );

  if (list) {
    console.log('\nOrfani per numero di sedi:');
    for (const [id, where] of r.orphans
      .map(id => [id, r.sought.get(id)])
      .sort((a, b) => b[1].length - a[1].length)) {
      console.log(`  ${String(where.length).padStart(4)}x  ${id.padEnd(38)} ${where[0]}`);
    }
  }

  const current = Object.fromEntries(
    [...r.perFile.entries()].sort(([a], [b]) => a.localeCompare(b))
  );

  if (update) {
    writeFileSync(
      baselinePath,
      `${JSON.stringify(
        {
          description:
            'Gate #4120 — orfani `data-testid` per spec E2E. Un file nuovo deve essere assente ' +
            'da questa mappa (zero orfani); un file presente non deve crescere. Rigenera con ' +
            '`pnpm lint:orphan-testids --update` SOLO per registrare un miglioramento, mai per ' +
            'assorbire un peggioramento.',
          updatedAt: new Date().toISOString().slice(0, 10),
          files: current,
        },
        null,
        2
      )}\n`
    );
    console.log(`\n✅ baseline riscritta: ${BASELINE_FILE}`);
    return;
  }

  let baseline;
  try {
    baseline = JSON.parse(readFileSync(baselinePath, 'utf8')).files ?? {};
  } catch {
    console.error(
      `\n❌ baseline mancante o illeggibile: ${BASELINE_FILE}\n` +
        '   Generala con `pnpm lint:orphan-testids --update`.'
    );
    process.exit(1);
  }

  const cmp = compareToBaseline(current, baseline);

  if (cmp.worsened) {
    console.error('\n❌ orfani `data-testid` in crescita.\n');
    for (const { file, count } of cmp.brandNew) {
      console.error(
        `   NUOVO   ${file} — ${count} sedi orfane (una spec nuova deve essere a zero)`
      );
    }
    for (const { file, was, now } of cmp.grown) {
      console.error(`   CRESCE  ${file} — da ${was} a ${now}`);
    }
    console.error(
      '\nUna spec che cerca un `data-testid` inesistente non puo` passare: il selettore non\n' +
        'esiste, quindi il test non misura niente. Usa il selettore reale, oppure aggiungi il\n' +
        'testid all elemento di produzione se quell elemento esiste davvero.\n' +
        'Per vedere quali id: `pnpm lint:orphan-testids --list`\n'
    );
    process.exit(1);
  }

  if (cmp.improved.length > 0 || cmp.cleared.length > 0) {
    console.log('\n✅ nessuna crescita — e ci sono miglioramenti da registrare:');
    for (const { file, was, now } of cmp.improved) console.log(`   ${file}: ${was} → ${now}`);
    for (const file of cmp.cleared) console.log(`   ${file}: ${baseline[file]} → 0 (pulito)`);
    console.log('\nAbbassa la baseline con `pnpm lint:orphan-testids --update`.\n');
    return;
  }

  console.log('\n✅ nessun peggioramento rispetto alla baseline.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
