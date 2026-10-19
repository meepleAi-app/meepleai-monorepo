#!/usr/bin/env node
/**
 * Esegue TUTTI i test delle regole ESLint locali — i file `*.test.{js,cjs,mjs}` sotto
 * `eslint-rules/`, sottocartelle comprese — con `node --test`. Issue #4533.
 *
 * Il difetto che chiude: Vitest raccoglie solo `.ts`/`.tsx`, lo script `test:eslint-rules` eseguiva
 * un solo file e nessun workflow lo chiamava. Una modifica che rompeva una regola, e il suo test,
 * passava la CI.
 *
 * Perche' uno script e non `node --test "<glob>"` scritto nel package.json: su Node 22 e su Node 24
 * un glob che non trova nulla esce con 0 e stampa «tests 0», cioe' un gate vuoto e verde. E
 * `node --test eslint-rules/` e' peggio: esegue la cartella come un modulo (carica `index.js`),
 * riporta un test superato e non esegue nessun test di regola. Qui i file si elencano a mano, zero
 * file e' un errore, e `node --test` riceve i file per nome.
 *
 * I `.test.ts` restano fuori: se ne nascesse uno qui, lo raccoglierebbe gia' Vitest.
 */

import { spawnSync } from 'node:child_process';
import { readdirSync } from 'node:fs';
import { dirname, join, sep } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';

export const WEB_ROOT = join(dirname(fileURLToPath(import.meta.url)), '..');
export const RULES_DIR = join(WEB_ROOT, 'eslint-rules');

const TEST_FILE = /\.test\.[cm]?js$/;

/**
 * I file di test sotto `rulesDir`, sottocartelle comprese, relativi a `rulesDir`, con `/` come
 * separatore e in ordine alfabetico.
 */
export function findRuleTestFiles(rulesDir) {
  return readdirSync(rulesDir, { recursive: true })
    .map(entry => String(entry).split(sep).join('/'))
    .filter(entry => TEST_FILE.test(entry))
    .sort();
}

/** Gli argomenti per `node`. Zero file e' un errore, non un successo. */
export function nodeTestArgs(files) {
  if (files.length === 0) {
    throw new Error(
      'test:eslint-rules: nessun file di test trovato in eslint-rules/ (attesi *.test.js). ' +
        'Un gate che non esegue nulla non e` verde.'
    );
  }
  return ['--test', '--test-reporter=spec', ...files];
}

function main() {
  const files = findRuleTestFiles(RULES_DIR).map(f => `eslint-rules/${f}`);

  let args;
  try {
    args = nodeTestArgs(files);
  } catch (error) {
    console.error(error.message);
    process.exit(1);
  }

  console.log(`test:eslint-rules: ${files.length} file di test in eslint-rules/`);
  for (const f of files) console.log(`  - ${f}`);

  const result = spawnSync(process.execPath, args, { cwd: WEB_ROOT, stdio: 'inherit' });
  if (result.error) {
    console.error(`test:eslint-rules: node --test non e' partito: ${result.error.message}`);
    process.exit(1);
  }
  process.exit(result.status ?? 1);
}

if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main();
}
