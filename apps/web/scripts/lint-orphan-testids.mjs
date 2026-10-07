#!/usr/bin/env node
/**
 * Gate #4120 — nessun file E2E cerca un `data-testid` che il codice di produzione non dichiara.
 *
 * L'ambito sono le spec **e** i page object, gli helper e le fixture sotto `e2e/`: i page object
 * sono il posto dove i selettori si concentrano, e una spec apparentemente pulita puo` cercare un
 * selettore morto attraverso il proprio. `e2e/pages/game/GamePage.ts` da solo porta `game-list`,
 * `game-card`, `loading-spinner`, `game-name` e `game-description`. Una prima stesura di questo
 * gate guardava solo `*.spec.ts` e lasciava fuori diciannove file con un centinaio di sedi.
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
 * 3. **Solo letterali dal lato E2E.** `getByTestId(variabile)` e i template nei file E2E non sono
 *    verificabili e vengono contati a parte. Esclusi anche i test unit degli helper — qualunque
 *    `__tests__/` sotto `e2e/`, e i file `*.test.*` — perché non guidano un browser.
 * 4. Le dichiarazioni si leggono dal codice di produzione: `__tests__/`, `*.test.*`, `*.spec.*` e
 *    `*.story/stories.*` sono esclusi di proposito. L'unica occorrenza di `meeple-card` nel repo è
 *    un mock Vitest, e contarla come dichiarazione avrebbe mascherato il difetto di #4110.
 * 5. **Solo l'uguaglianza esatta `data-testid="x"`.** Gli operatori CSS di sottostringa e di
 *    prefisso — `data-testid*="x"`, `^=`, `$=` — non sono controllati: dire se combacino
 *    richiederebbe confrontarli con l'insieme dei dichiarati invece che cercarli per nome.
 *    `e2e/admin-first-time-setup/04-bounded-contexts-access.spec.ts` ne porta due, e sono morti
 *    quanto gli altri. Chi li incontra li converta all'uguaglianza, così il gate li vede.
 * 6. **Il gate copre `data-testid`, non `data-slot`.** Il repository ha due convenzioni, e la
 *    seconda ha quasi mille valori distinti in produzione. Un `[data-slot="x"]` inesistente NON è
 *    controllato: misurato il 2026-10-08, sono 11 sedi, di cui solo 6 reali — `mobile-body-tab`
 *    (uno slot legacy che due test unit asseriscono assente, e che `e2e/a11y/session-live.spec.ts`
 *    interroga ancora) e `game-detail-tabs`. Le altre sono falsi positivi che un gate dovrebbe
 *    saper escludere: un `data-slot` citato dentro un commento, e due banner che la spec stessa
 *    inietta con `document.createElement`. Allargare il gate costa quelle due esclusioni per sei
 *    sedi, e per ora non le vale. `--list` segnala però quando un orfano ha un `data-slot`
 *    omonimo, perché in quel caso la correzione non tocca la produzione.
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

/**
 * Un file E2E che CONSUMA selettori: le spec, ma anche i page object, gli helper e le fixture.
 * I page object sono il posto dove i selettori si concentrano — `e2e/pages/game/GamePage.ts` da
 * solo porta `game-list`, `game-card`, `loading-spinner`, `game-name` e `game-description` — e una
 * spec apparentemente pulita puo` cercare un selettore morto attraverso il proprio page object.
 * Esclusi i test unit degli helper (`__tests__/`, `*.test.*`): non guidano un browser.
 */
export function isE2EConsumer(path) {
  const unix = path.split(sep).join('/');
  if (/\/__tests__\//.test(unix)) return false;
  if (/\.test\.[tj]sx?$/.test(unix)) return false;
  return /\.[tj]sx?$/.test(unix);
}

export function isConstantsModule(path) {
  return /test-ids?\.[tj]sx?$/i.test(path.split(sep).join('/'));
}

/**
 * I `data-slot` dichiarati dalla produzione. Il repository ha DUE convenzioni di selettore —
 * `data-testid` e `data-slot`, quest'ultima con quasi mille valori distinti — e un `data-testid`
 * orfano ha a volte un `data-slot` omonimo: in quel caso la correzione non tocca la produzione,
 * basta puntare la spec all'attributo che c'e` gia`. Il gate non li confonde (un selettore
 * `[data-testid="x"]` non combacia con `data-slot="x"`), ma con `--list` lo SEGNALA.
 */
export function slotsFromSource(source) {
  const slots = new Set();
  for (const m of source.matchAll(/data-slot=["']([^"'$]+)["']/g)) slots.add(m[1]);
  for (const m of source.matchAll(/data-slot=\{\s*["']([^"']+)["']\s*\}/g)) slots.add(m[1]);
  for (const m of source.matchAll(/data-slot=\{`([^`$]+)`\}/g)) slots.add(m[1]);
  return slots;
}

/**
 * Gli `id` dichiarati dalla produzione — la TERZA convenzione di indirizzamento del repository.
 *
 * `RegisterForm.tsx` porta `id="register-email"` e `id="register-password"`, cioè esattamente i
 * nomi che `e2e/auth-email-registration-flow.spec.ts` cerca come `data-testid`. Per un campo di
 * form l'`id` è anche il selettore migliore, perché è quello a cui punta `<label for>`.
 *
 * ⚠️ Ma un suggerimento non è un permesso. Convertire un selettore su una spec che **non può
 * passare comunque** fa scendere il conteggio del gate senza riparare niente: è truccare la
 * metrica. Quella stessa spec asserisce `toBeVisible()` su un campo di conferma password che il
 * form non ha, e non accetta mai i termini che il form richiede — ripuntare due selettori
 * sposterebbe il fallimento di una riga. Prima si stabilisce che la spec possa passare, poi si
 * ripuntano i selettori.
 */
export function idsFromSource(source) {
  const ids = new Set();
  for (const m of source.matchAll(/\sid=["']([^"'$]+)["']/g)) ids.add(m[1]);
  for (const m of source.matchAll(/\sid=\{\s*["']([^"']+)["']\s*\}/g)) ids.add(m[1]);
  return ids;
}

/** Un id coperto da un pattern dinamico non è dimostrabile orfano (limite 1). */
export function isUnprovable(id, { suffixes, prefixes }) {
  for (const s of suffixes) if (id.endsWith(s)) return true;
  for (const p of prefixes) if (id.startsWith(p)) return true;
  return false;
}

/**
 * I pattern che assolvono un id, per poterli far verificare a mano.
 *
 * L'assoluzione è letteralmente corretta — `data-testid={`message-${x}`}` *può* produrre
 * `message-citations` — ma può anche essere implausibile: quel sito rende i messaggi di chat, non
 * i contenitori di citazioni, e `message-citations` è un orfano vero che solo un occhio umano
 * distingue. Un id assolto in silenzio è invisibile: né accusato né mostrato. `--list` lo stampa
 * col pattern che lo copre, così l'assoluzione si può contestare.
 */
export function absolvedBy(id, { suffixes, prefixes }) {
  return [
    ...[...suffixes].filter(s => id.endsWith(s)).map(s => `*${s}`),
    ...[...prefixes].filter(p => id.startsWith(p)).map(p => `${p}*`),
  ];
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
  const e2eFiles = walk(join(webRoot, 'e2e'), isE2EConsumer);
  const srcFiles = walk(join(webRoot, 'src'), isProductionSource);

  const sought = new Map();
  let dynamicSites = 0;
  for (const file of e2eFiles) {
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
  const slots = new Set();
  const htmlIds = new Set();
  for (const file of srcFiles) {
    const source = readFileSync(file, 'utf8');
    const d = declaredFromSource(source, { isConstantsModule: isConstantsModule(file) });
    for (const v of d.literals) literals.add(v);
    for (const v of d.suffixes) suffixes.add(v);
    for (const v of d.prefixes) prefixes.add(v);
    for (const v of slotsFromSource(source)) slots.add(v);
    for (const v of idsFromSource(source)) htmlIds.add(v);
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

  return {
    e2eFiles,
    srcFiles,
    sought,
    orphans,
    unprovable,
    declared,
    perFile,
    dynamicSites,
    slots,
    patterns: { suffixes, prefixes },
    htmlIds,
  };
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
  if (r.e2eFiles.length === 0 || r.srcFiles.length === 0) {
    console.error(
      `❌ la scansione non ha trovato nulla (e2e: ${r.e2eFiles.length}, src: ${r.srcFiles.length}).\n` +
        '   Il gate non puo` verificare niente: controlla le radici `e2e/` e `src/`.'
    );
    process.exit(1);
  }

  const sites = [...r.perFile.values()].reduce((a, b) => a + b, 0);
  console.log(
    `File E2E analizzati: ${r.e2eFiles.length} · file di produzione: ${r.srcFiles.length}`
  );
  console.log(
    `Id cercati: ${r.sought.size} · dichiarati: ${r.declared.length}` +
      ` · orfani dimostrabili: ${r.orphans.length} · indimostrabili: ${r.unprovable.length}`
  );
  console.log(
    `Sedi orfane: ${sites} in ${r.perFile.size} file · sedi dinamiche non verificabili: ${r.dynamicSites}`
  );

  if (list) {
    const withSlot = r.orphans.filter(id => r.slots.has(id));
    console.log('\nOrfani per numero di sedi:');
    for (const [id, where] of r.orphans
      .map(id => [id, r.sought.get(id)])
      .sort((a, b) => b[1].length - a[1].length)) {
      const hint = r.slots.has(id) ? '  ← esiste data-slot omonimo' : '';
      console.log(`  ${String(where.length).padStart(4)}x  ${id.padEnd(38)} ${where[0]}${hint}`);
    }
    if (r.unprovable.length > 0) {
      console.log(
        `\nIndimostrabili (${r.unprovable.length}): un pattern dinamico li assolve, e l'assoluzione`
      );
      console.log('puo` essere implausibile — contestala guardando COSA rende quel sito.\n');
      for (const [id, where] of r.unprovable
        .map(id => [id, r.sought.get(id)])
        .sort((a, b) => b[1].length - a[1].length)
        .slice(0, 30)) {
        const by = absolvedBy(id, r.patterns).join(' ');
        console.log(`  ${String(where.length).padStart(4)}x  ${id.padEnd(38)} assolto da: ${by}`);
      }
      if (r.unprovable.length > 30) console.log(`  … e altri ${r.unprovable.length - 30}`);
    }

    const withId = r.orphans.filter(id => r.htmlIds.has(id) && !r.slots.has(id));
    if (withId.length > 0) {
      const sites = withId.reduce((n, id) => n + r.sought.get(id).length, 0);
      console.log(
        `\n${withId.length} orfani (${sites} sedi) hanno un \`id\` omonimo in produzione:\n` +
          `  ${withId.join(' · ')}\n` +
          'Per un campo di form l`id e` anche il selettore migliore: e` quello a cui punta\n' +
          '`<label for>`. Si punta con `locator("#<id>")`.\n' +
          '⚠️ Ma prima stabilisci che la spec POSSA passare. `auth-email-registration-flow.spec.ts`\n' +
          'ha `register-email` e `register-password` come id, e resta comunque rotta: asserisce\n' +
          'un campo di conferma password che il form non ha. Ripuntare i selettori su una spec\n' +
          'che non passa fa scendere questo conteggio senza riparare niente.\n'
      );
    }

    if (withSlot.length > 0) {
      const sites = withSlot.reduce((n, id) => n + r.sought.get(id).length, 0);
      console.log(
        `\n${withSlot.length} orfani (${sites} sedi) hanno un \`data-slot\` omonimo in produzione:\n` +
          `  ${withSlot.join(' · ')}\n` +
          'Per questi la correzione NON tocca la produzione: punta la spec a\n' +
          '  [data-slot="<id>"]   invece di   [data-testid="<id>"]\n' +
          'e ricorda che `getByTestId()` non puo` puntare a `data-slot`: serve `locator()`.\n'
      );
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
