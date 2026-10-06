#!/usr/bin/env node
/**
 * Gate #4092 — nessuna spec Playwright fuori dalle radici che una config raccoglie.
 *
 * Il difetto che chiude: dieci spec vivevano sotto `apps/web/tests/e2e/`, nessun `testDir` puntava
 * a `./tests`, e quindi non venivano eseguite da nessuno. Non erano skippate né rosse: assenti. In
 * quello stato sono rimaste dal 2025-11-30 al 2026-10-06, e nulla lo segnalava — il conteggio «392
 * file raccolti» non dice quanti file ESISTONO.
 *
 * Il controllo è statico di proposito: non invoca Playwright (cinque `--list` in CI costano minuti)
 * e legge i `testDir` dalle config, così si aggiorna da sé quando una config cambia radice.
 *
 * Limite dichiarato: verifica la RADICE, non il `testMatch`. Una spec dentro una radice ma esclusa
 * da un `testMatch` o da un `testIgnore` resta invisibile a questo gate. Il caso che ha motivato il
 * gate è la radice; coprire anche i pattern richiederebbe invocare Playwright.
 */

import { readdirSync, readFileSync, statSync } from 'node:fs';
import { join, relative, sep } from 'node:path';

const WEB_ROOT = new URL('..', import.meta.url).pathname.replace(/^\/([A-Za-z]:)/, '$1');
const SKIP_DIRS = new Set(['node_modules', '.next', 'dist', 'coverage', 'test-results', 'playwright-report', 'audit-results', 'storybook-static']);

/** Le radici dichiarate dalle config Playwright, lette dalle config stesse. */
function declaredTestDirs() {
  const dirs = new Set();
  for (const name of readdirSync(WEB_ROOT)) {
    if (!/^playwright.*\.config\.ts$/.test(name)) continue;
    const src = readFileSync(join(WEB_ROOT, name), 'utf8');
    for (const m of src.matchAll(/testDir:\s*['"]\.?\/?([^'"]+)['"]/g)) {
      dirs.add(m[1].replace(/\/$/, ''));
    }
  }
  return [...dirs];
}

function findSpecs(dir, acc = []) {
  for (const entry of readdirSync(dir)) {
    if (SKIP_DIRS.has(entry)) continue;
    const full = join(dir, entry);
    if (statSync(full).isDirectory()) findSpecs(full, acc);
    else if (/\.spec\.tsx?$/.test(entry)) acc.push(relative(WEB_ROOT, full).split(sep).join('/'));
  }
  return acc;
}

const testDirs = declaredTestDirs();
if (testDirs.length === 0) {
  console.error('❌ nessun `testDir` trovato nelle config Playwright: il gate non può verificare nulla.');
  process.exit(1);
}

const specs = findSpecs(WEB_ROOT);
const orphans = specs.filter(s => !testDirs.some(d => s === d || s.startsWith(`${d}/`)));

console.log(`Radici dichiarate: ${testDirs.map(d => `./${d}`).join(' · ')}`);
console.log(`Spec trovate su disco: ${specs.length}`);

if (orphans.length > 0) {
  console.error(`\n❌ ${orphans.length} spec fuori da ogni radice raccolta — non le esegue nessuno:\n`);
  for (const o of orphans) console.error(`   ${o}`);
  console.error(
    '\nSpostale sotto una radice dichiarata, oppure aggiungi una config Playwright che le raccolga.\n' +
      'Una spec che nessuna config raccoglie non e` un test: e` un file.\n'
  );
  process.exit(1);
}

console.log('✅ nessuna spec orfana.');
