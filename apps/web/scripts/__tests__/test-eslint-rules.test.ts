/**
 * test-eslint-rules.test.ts — unit test per test-eslint-rules.mjs (#4533).
 *
 * Il rischio sta in due punti: quali file vengono raccolti, e cosa succede quando non se ne trova
 * nessuno. `node --test "<glob>"` senza corrispondenze esce con 0 e stampa «tests 0» (verificato su
 * Node 22 e 24): un gate vuoto che risulta verde.
 *
 * Run: pnpm vitest run scripts/__tests__/test-eslint-rules.test.ts
 */
import { mkdirSync, mkdtempSync, rmSync, writeFileSync } from 'node:fs';
import { tmpdir } from 'node:os';
import { dirname, join } from 'node:path';

import { afterEach, beforeEach, describe, expect, it } from 'vitest';

import { RULES_DIR, findRuleTestFiles, nodeTestArgs } from '../test-eslint-rules.mjs';

describe('findRuleTestFiles', () => {
  let dir: string;

  beforeEach(() => {
    dir = mkdtempSync(join(tmpdir(), 'eslint-rules-'));
  });

  afterEach(() => {
    rmSync(dir, { recursive: true, force: true });
  });

  function touch(relativePath: string): void {
    const full = join(dir, relativePath);
    mkdirSync(dirname(full), { recursive: true });
    writeFileSync(full, '');
  }

  it('raccoglie i test .js, .cjs e .mjs, anche nelle sottocartelle, e non le regole', () => {
    for (const f of [
      'rule.js',
      'index.js',
      'rule.test.js',
      'other.test.cjs',
      'third.test.mjs',
      'nested/deep.test.js',
    ]) {
      touch(f);
    }

    expect(findRuleTestFiles(dir)).toEqual([
      'nested/deep.test.js',
      'other.test.cjs',
      'rule.test.js',
      'third.test.mjs',
    ]);
  });

  it('lascia i .test.ts a Vitest, che li raccoglie gia`', () => {
    touch('typed.test.ts');
    touch('plain.test.js');

    expect(findRuleTestFiles(dir)).toEqual(['plain.test.js']);
  });

  it('restituisce un elenco vuoto per una cartella senza test', () => {
    touch('rule.js');

    expect(findRuleTestFiles(dir)).toEqual([]);
  });
});

describe('nodeTestArgs', () => {
  it('fallisce su zero file: un gate che non esegue nulla non e` verde', () => {
    expect(() => nodeTestArgs([])).toThrow(/nessun file di test/);
  });

  it('passa a node --test ogni file per nome, con il reporter spec', () => {
    expect(nodeTestArgs(['eslint-rules/a.test.js', 'eslint-rules/b.test.js'])).toEqual([
      '--test',
      '--test-reporter=spec',
      'eslint-rules/a.test.js',
      'eslint-rules/b.test.js',
    ]);
  });
});

describe('la cartella vera', () => {
  it('raccoglie anche i test che lo script precedente non eseguiva', () => {
    const files = findRuleTestFiles(RULES_DIR);

    // L'unico file che `test:eslint-rules` eseguiva prima di #4533.
    expect(files).toContain('no-hardcoded-hex.test.js');
    // Il caso che ha aperto #4533: i test di #4513 giravano solo a mano.
    expect(files).toContain('no-hardcoded-color-utility.test.js');
  });
});
