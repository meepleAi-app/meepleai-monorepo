#!/usr/bin/env node
/**
 * Gate #4127 — nessun test E2E nuovo può passare senza asserire nulla.
 *
 * Il difetto che chiude: un test le cui UNICHE asserzioni stanno dentro un blocco condizionato
 * sulla presenza di un elemento non ha un esito «non applicabile» — ha un **verde**.
 *
 *   const citationList = page.getByTestId('citation-list');
 *   if (await citationList.isVisible({ timeout: 10000 }).catch(() => false)) {
 *     … tutte le asserzioni del test …
 *   } else {
 *     console.log('No citations returned from query');   // e il test PASSA
 *   }
 *
 * Se l'elemento non c'è il corpo non gira, il test passa, e il `console.log` fa sembrare un salto
 * legittimo per mancanza di dati. Il `.catch(() => false)` isola anche gli errori: un selettore
 * malformato e un elemento assente diventano indistinguibili.
 *
 * Il caso che ha motivato il gate è `e2e/pdf-viewer-modal.spec.ts` — 14 test su 18 in questo stato.
 * Il condizionale era **sempre falso**: la spec guida `/chat` e cerca `citation-list`, che esiste
 * in `features/kb-globale/DrawerCompleted.tsx`, un'altra superficie. Nessuna delle sue 18 prove
 * poteva fallire. È peggio di un rosso: un rosso si vede, questi si contano fra i passati.
 *
 * Perché un cricchetto e non «zero»: la baseline all'introduzione è troppo ampia. Il gate blocca
 * il PEGGIORAMENTO — un file nuovo deve essere a zero, uno esistente non deve crescere — con la
 * baseline committata file per file, come #4120.
 *
 * ── Limiti dichiarati ──────────────────────────────────────────────────────────────────────────
 *
 * 1. **Non accusa chi condiziona legittimamente.** Un blocco su UI davvero opzionale — un banner
 *    dei cookie, una funzione dietro un flag — è corretto. Il gate guarda solo i test in cui
 *    *ogni* `expect` è condizionato: se ne esiste uno fuori, il test non è contato. È il criterio
 *    della DoD di #4127, e serve a non far disattivare il gate per falsi positivi.
 * 2. **Un test senza alcun `expect` non è contato.** Alcuni sono legittimi (uno smoke che verifica
 *    solo che una navigazione non lanci). Sono una categoria a parte, misurata ma non accusata.
 * 3. **Niente parser TypeScript.** La profondità si conta sulle graffe, spogliando stringhe e
 *    commenti di riga: una graffa dentro un template annidato può sfalsare il conteggio. Per
 *    questo l'output elenca i file — va guardato, non creduto.
 * 4. **`test.describe` non è un test.** Una prima stesura della misura lo contava come tale e
 *    inghiottiva i test veri annidati dentro: dava 630 test totali invece di ~3000, e il file che
 *    aveva motivato la misura non compariva. La regex esclude esplicitamente `describe`.
 *
 * Uso:
 *   node scripts/lint-blind-tests.mjs             verifica contro la baseline
 *   node scripts/lint-blind-tests.mjs --update     riscrive la baseline dalla misura corrente
 *   node scripts/lint-blind-tests.mjs --list       elenca i test ciechi con la loro riga
 */

import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { join, relative, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const WEB_ROOT = join(dirnameOf(import.meta.url), '..');
export const BASELINE_FILE = 'blind-tests-baseline.json';

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

/** NON deve catturare `test.describe` (limite 4). */
export const IS_TEST = /^\s*(?:await\s+)?test(?:\.(?:only|skip|fixme|fail|slow))?\s*\(/;

/**
 * Un `if` la cui condizione interroga la PRESENZA di un elemento.
 *
 * `.count()` non pretende un confronto subito dopo: la forma reale e` `if ((await x.count()) > 0)`,
 * con le parentesi che `await` impone, e una prima stesura che cercava `\.count\(\)\s*[>!]` la
 * mancava. Dentro un `if`, una chiamata a `.count()` e` sempre un controllo di presenza.
 */
export const IS_PRESENCE_GATE =
  /^\s*(?:\}\s*else\s+)?if\s*\(.*(?:isVisible\(|\.count\(\)|toBeVisible|isEnabled\(|\.first\(\)|\.isChecked\()/;

export const HAS_EXPECT = /\bexpect\s*\(/;

/** File E2E che contengono test: le spec. Page object e helper non dichiarano `test(`. */
export function isE2ESpec(path) {
  const unix = path.split(sep).join('/');
  if (/\/__tests__\//.test(unix)) return false;
  return /\.spec\.[tj]sx?$/.test(unix);
}

/** Graffe nette di una riga, ignorando stringhe semplici e commenti di riga (limite 3). */
export function netBraces(line) {
  const stripped = line
    .replace(/\\./g, '')
    .replace(/'[^']*'/g, "''")
    .replace(/"[^"]*"/g, '""')
    .replace(/`[^`]*`/g, '``')
    .replace(/\/\/.*$/, '');
  let n = 0;
  for (const ch of stripped) {
    if (ch === '{') n++;
    else if (ch === '}') n--;
  }
  return n;
}

/**
 * I test di un file, con quante asserzioni hanno e quante sono condizionate.
 * Un test è CIECO quando ha almeno un `expect` e tutti sono condizionati.
 */
export function analyse(source) {
  const lines = source.split('\n');
  let depth = 0;
  const gates = []; // profondità del corpo di ogni gate ancora aperto
  let current = null;
  const tests = [];

  lines.forEach((line, idx) => {
    const at = depth;
    while (gates.length && at < gates[gates.length - 1]) gates.pop();

    if (current && at < current.depth) {
      tests.push(current);
      current = null;
    }
    if (!current && IS_TEST.test(line)) {
      current = { line: idx + 1, depth: at + 1, expects: 0, gated: 0 };
    }
    if (current && HAS_EXPECT.test(line)) {
      current.expects++;
      if (gates.length) current.gated++;
    }

    const net = netBraces(line);
    if (IS_PRESENCE_GATE.test(line) && net > 0) gates.push(at + net);
    depth += net;
  });
  if (current) tests.push(current);

  return {
    tests,
    blind: tests.filter(t => t.expects > 0 && t.gated === t.expects),
    withoutExpect: tests.filter(t => t.expects === 0),
  };
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
  const files = walk(join(webRoot, 'e2e'), isE2ESpec);
  const perFile = new Map();
  let totTests = 0;
  let totBlind = 0;
  let totNoExpect = 0;
  const detail = [];

  for (const file of files) {
    const rel = relative(webRoot, file).split(sep).join('/');
    const r = analyse(readFileSync(file, 'utf8'));
    totTests += r.tests.length;
    totNoExpect += r.withoutExpect.length;
    if (r.blind.length > 0) {
      perFile.set(rel, r.blind.length);
      totBlind += r.blind.length;
      for (const t of r.blind) detail.push({ file: rel, line: t.line, expects: t.expects });
    }
  }
  return { files, perFile, totTests, totBlind, totNoExpect, detail };
}

/** La regola del cricchetto. Nessun I/O: testabile. */
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

  if (r.files.length === 0 || r.totTests === 0) {
    console.error(
      `❌ la scansione non ha trovato test (spec: ${r.files.length}, test: ${r.totTests}).\n` +
        '   Il gate non puo` verificare niente: controlla la radice `e2e/`.'
    );
    process.exit(1);
  }

  console.log(`Spec analizzate: ${r.files.length} · test rilevati: ${r.totTests}`);
  console.log(
    `Test in cui OGNI expect e` +
      '` condizionato: ' +
      `${r.totBlind} in ${r.perFile.size} file · test senza alcun expect: ${r.totNoExpect}`
  );

  if (list) {
    console.log('\nTest ciechi, per file:');
    for (const [file, n] of [...r.perFile.entries()].sort((a, b) => b[1] - a[1])) {
      const righe = r.detail
        .filter(d => d.file === file)
        .map(d => d.line)
        .join(', ');
      console.log(`  ${String(n).padStart(3)}  ${file}\n       righe: ${righe}`);
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
            'Gate #4127 — test E2E le cui UNICHE asserzioni stanno dentro un blocco ' +
            'condizionato sulla presenza di un elemento, per file. Un file nuovo deve essere ' +
            'assente da questa mappa; uno presente non deve crescere. Rigenera con ' +
            '`pnpm lint:blind-tests --update` SOLO per registrare un miglioramento.',
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
        '   Generala con `pnpm lint:blind-tests --update`.'
    );
    process.exit(1);
  }

  const cmp = compareToBaseline(current, baseline);

  if (cmp.worsened) {
    console.error('\n❌ test che possono passare senza asserire nulla, in crescita.\n');
    for (const { file, count } of cmp.brandNew) {
      console.error(`   NUOVO   ${file} — ${count} (una spec nuova deve essere a zero)`);
    }
    for (const { file, was, now } of cmp.grown) {
      console.error(`   CRESCE  ${file} — da ${was} a ${now}`);
    }
    console.error(
      '\nUn test le cui UNICHE asserzioni stanno dentro `if (await x.isVisible())` non ha un\n' +
        'esito «non applicabile»: ha un VERDE. Se la precondizione e` necessaria, ASSERISCILA\n' +
        '  await expect(x).toBeVisible();\n' +
        'invece di condizionarla. Se davvero e` opzionale, lascia almeno un expect fuori dal\n' +
        'blocco. Se il caso non e` applicabile, `test.skip()` con un motivo classificato.\n' +
        'Per vedere quali: `pnpm lint:blind-tests --list`\n'
    );
    process.exit(1);
  }

  if (cmp.improved.length > 0 || cmp.cleared.length > 0) {
    console.log('\n✅ nessuna crescita — e ci sono miglioramenti da registrare:');
    for (const { file, was, now } of cmp.improved) console.log(`   ${file}: ${was} → ${now}`);
    for (const file of cmp.cleared) console.log(`   ${file}: ${baseline[file]} → 0 (pulito)`);
    console.log('\nAbbassa la baseline con `pnpm lint:blind-tests --update`.\n');
    return;
  }

  console.log('\n✅ nessun peggioramento rispetto alla baseline.');
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
