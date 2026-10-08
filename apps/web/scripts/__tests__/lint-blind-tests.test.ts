/**
 * lint-blind-tests.test.ts — unit test per lint-blind-tests.mjs (gate #4127).
 *
 * Copre la logica dove sta il rischio: distinguere un test **cieco** — tutte le asserzioni dentro
 * un blocco condizionato sulla presenza di un elemento — da uno che condiziona legittimamente su
 * UI opzionale. Un gate che confonde i due viene disattivato, e porta via con sé i casi veri.
 *
 * Run: pnpm vitest run scripts/__tests__/lint-blind-tests.test.ts
 */
import { describe, expect, it } from 'vitest';

import {
  analyse,
  compareToBaseline,
  HAS_EXPECT,
  IS_PRESENCE_GATE,
  IS_TEST,
  isE2ESpec,
  netBraces,
} from '../lint-blind-tests.mjs';

describe('IS_TEST', () => {
  it('riconosce un test e le sue varianti', () => {
    expect(IS_TEST.test(`test('x', async () => {`)).toBe(true);
    expect(IS_TEST.test(`  test('y', async ({ page }) => {`)).toBe(true);
    expect(IS_TEST.test(`  test.skip('s', async () => {`)).toBe(true);
    expect(IS_TEST.test(`  test.only('o', async () => {`)).toBe(true);
  });

  it('NON riconosce `test.describe`', () => {
    // Limite 4: una prima stesura lo contava come test, inghiottiva i test veri annidati dentro,
    // e dava 630 test totali invece di ~3000 — con il file che aveva motivato la misura assente
    // dal risultato. La misura contraddiceva un caso verificato a mano, ed e` cosi` che si e`
    // scoperta sbagliata.
    expect(IS_TEST.test(`test.describe('gruppo', () => {`)).toBe(false);
    expect(IS_TEST.test(`  test.describe.serial('gruppo', () => {`)).toBe(false);
  });
});

describe('IS_PRESENCE_GATE', () => {
  it('riconosce le forme che interrogano la presenza di un elemento', () => {
    expect(IS_PRESENCE_GATE.test('    if (await x.isVisible()) {')).toBe(true);
    expect(IS_PRESENCE_GATE.test('    if (await x.isVisible({ timeout: 3000 })) {')).toBe(true);
    expect(IS_PRESENCE_GATE.test('    if ((await x.count()) > 0) {')).toBe(true);
    expect(IS_PRESENCE_GATE.test('    } else if (await y.isEnabled()) {')).toBe(true);
  });

  it('non riconosce un `if` su altro', () => {
    expect(IS_PRESENCE_GATE.test('    if (process.env.CI) {')).toBe(false);
    expect(IS_PRESENCE_GATE.test('    if (items.length === 3) {')).toBe(false);
  });
});

describe('netBraces', () => {
  it('conta le graffe nette', () => {
    expect(netBraces('if (x) {')).toBe(1);
    expect(netBraces('}')).toBe(-1);
    expect(netBraces('const o = { a: 1 };')).toBe(0);
  });

  it('ignora le graffe dentro stringhe e commenti di riga', () => {
    expect(netBraces(`const s = '{{{';`)).toBe(0);
    expect(netBraces('const s = "}}}";')).toBe(0);
    expect(netBraces('doThing(); // {')).toBe(0);
  });
});

describe('isE2ESpec', () => {
  it('accetta le spec e rifiuta il resto', () => {
    expect(isE2ESpec('e2e/library.spec.ts')).toBe(true);
    expect(isE2ESpec('e2e/pages/game/GamePage.ts')).toBe(false);
    expect(isE2ESpec('e2e/_helpers/__tests__/x.spec.ts')).toBe(false);
    expect(isE2ESpec('e2e/README.md')).toBe(false);
  });
});

describe('analyse — il criterio di #4127', () => {
  const gated = [
    `test('tutte condizionate', async ({ page }) => {`,
    `  const x = page.getByTestId('a');`,
    `  if (await x.isVisible()) {`,
    `    await expect(x).toHaveText('ciao');`,
    `    await expect(x).toBeEnabled();`,
    `  }`,
    `});`,
  ].join('\n');

  it('un test con tutte le asserzioni condizionate è CIECO', () => {
    const r = analyse(gated);
    expect(r.tests).toHaveLength(1);
    expect(r.tests[0].expects).toBe(2);
    expect(r.tests[0].gated).toBe(2);
    expect(r.blind).toHaveLength(1);
  });

  it('un expect FUORI dal blocco salva il test', () => {
    // Limite 1, ed e` la proprieta` che evita i falsi positivi: condizionare su UI davvero
    // opzionale e` legittimo, purche` il test asserisca comunque qualcosa.
    const r = analyse(
      [
        `test('una fuori', async ({ page }) => {`,
        `  await expect(page).toHaveTitle(/x/);`,
        `  const x = page.getByTestId('a');`,
        `  if (await x.isVisible()) {`,
        `    await expect(x).toHaveText('ciao');`,
        `  }`,
        `});`,
      ].join('\n')
    );
    expect(r.tests[0].expects).toBe(2);
    expect(r.tests[0].gated).toBe(1);
    expect(r.blind).toHaveLength(0);
  });

  it('un test senza alcun expect non è cieco, è una categoria a parte', () => {
    const r = analyse(
      [`test('solo navigazione', async ({ page }) => {`, `  await page.goto('/');`, `});`].join(
        '\n'
      )
    );
    expect(r.blind).toHaveLength(0);
    expect(r.withoutExpect).toHaveLength(1);
  });

  it('il gate si chiude alla graffa giusta: un expect dopo il blocco non è condizionato', () => {
    const r = analyse(
      [
        `test('dopo il blocco', async ({ page }) => {`,
        `  const x = page.getByTestId('a');`,
        `  if (await x.isVisible()) {`,
        `    await x.click();`,
        `  }`,
        `  await expect(page).toHaveURL('/ok');`,
        `});`,
      ].join('\n')
    );
    expect(r.tests[0].expects).toBe(1);
    expect(r.tests[0].gated).toBe(0);
    expect(r.blind).toHaveLength(0);
  });

  it('conta i test di un describe, non il describe', () => {
    const r = analyse(
      [
        `test.describe('gruppo', () => {`,
        `  test('primo', async ({ page }) => {`,
        `    await expect(page).toHaveTitle(/a/);`,
        `  });`,
        `  test('secondo', async ({ page }) => {`,
        `    await expect(page).toHaveTitle(/b/);`,
        `  });`,
        `});`,
      ].join('\n')
    );
    expect(r.tests).toHaveLength(2);
    expect(r.blind).toHaveLength(0);
  });
});

describe('compareToBaseline (la regola del cricchetto)', () => {
  const baseline = { 'e2e/a.spec.ts': 3, 'e2e/b.spec.ts': 1 };

  it('una spec nuova con test ciechi peggiora', () => {
    const cmp = compareToBaseline({ ...baseline, 'e2e/c.spec.ts': 1 }, baseline);
    expect(cmp.worsened).toBe(true);
    expect(cmp.brandNew).toEqual([{ file: 'e2e/c.spec.ts', count: 1 }]);
  });

  it('una spec esistente che cresce peggiora', () => {
    const cmp = compareToBaseline({ ...baseline, 'e2e/a.spec.ts': 4 }, baseline);
    expect(cmp.grown).toEqual([{ file: 'e2e/a.spec.ts', was: 3, now: 4 }]);
  });

  it('un calo è un miglioramento', () => {
    const cmp = compareToBaseline({ ...baseline, 'e2e/a.spec.ts': 1 }, baseline);
    expect(cmp.worsened).toBe(false);
    expect(cmp.improved).toEqual([{ file: 'e2e/a.spec.ts', was: 3, now: 1 }]);
  });

  it('una spec ripulita del tutto è segnalata', () => {
    const cmp = compareToBaseline({ 'e2e/a.spec.ts': 3 }, baseline);
    expect(cmp.cleared).toEqual(['e2e/b.spec.ts']);
  });
});

describe('HAS_EXPECT', () => {
  it('riconosce un expect e non una parola che lo contiene', () => {
    expect(HAS_EXPECT.test('await expect(x).toBe(1);')).toBe(true);
    expect(HAS_EXPECT.test('const unexpected = 1;')).toBe(false);
  });
});
