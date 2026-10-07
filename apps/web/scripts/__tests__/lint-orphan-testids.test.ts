/**
 * lint-orphan-testids.test.ts — unit test per lint-orphan-testids.mjs (gate #4120).
 *
 * Copre la logica dove sta il rischio: la distinzione fra un orfano **dimostrabile** e un id che
 * un pattern dinamico rende indimostrabile. La prima stesura della misura dava 700 orfani invece
 * di 547 proprio perché quella distinzione mancava, e un gate che accusa il falso viene
 * disattivato — portandosi via anche i veri.
 *
 * Run: pnpm vitest run scripts/__tests__/lint-orphan-testids.test.ts
 */
import { describe, expect, it } from 'vitest';

import {
  absolvedBy,
  classify,
  compareToBaseline,
  declaredFromSource,
  isConstantsModule,
  isE2EConsumer,
  isProductionSource,
  isUnprovable,
  slotsFromSource,
  soughtFromSource,
} from '../lint-orphan-testids.mjs';

describe('isE2EConsumer', () => {
  it('include le spec e anche i page object, gli helper e le fixture', () => {
    // La prima stesura del gate guardava solo `*.spec.ts` e lasciava fuori diciannove file con un
    // centinaio di sedi. I page object sono dove i selettori si concentrano: `GamePage.ts` da solo
    // porta game-list, game-card, loading-spinner, game-name e game-description, quindi una spec
    // apparentemente pulita puo` cercare un selettore morto attraverso il proprio page object.
    expect(isE2EConsumer('e2e/library.spec.ts')).toBe(true);
    expect(isE2EConsumer('e2e/pages/game/GamePage.ts')).toBe(true);
    expect(isE2EConsumer('e2e/helpers/WaitHelper.ts')).toBe(true);
    expect(isE2EConsumer('e2e/fixtures/robust-selectors.ts')).toBe(true);
  });

  it('esclude i test unit degli helper, che non guidano un browser', () => {
    expect(isE2EConsumer('e2e/_helpers/__tests__/dataAssertionUtils.test.ts')).toBe(false);
    expect(isE2EConsumer('e2e/_helpers/seedEntities.test.ts')).toBe(false);
  });

  it('esclude i file non sorgente', () => {
    expect(isE2EConsumer('e2e/README.md')).toBe(false);
    expect(isE2EConsumer('e2e/fixtures/sample.pdf')).toBe(false);
  });
});

describe('isProductionSource', () => {
  it('accetta un componente', () => {
    expect(isProductionSource('src/components/features/game-chat/ChatInputBar.tsx')).toBe(true);
  });

  it('esclude i file sotto __tests__, e i test/spec/storie per nome', () => {
    // Il motivo e` #4110: l'unica occorrenza di `meeple-card` nel repo e` un mock Vitest, e
    // contarla come dichiarazione avrebbe mascherato il difetto.
    expect(isProductionSource('src/components/x/__tests__/LibraryPublicHome.test.tsx')).toBe(false);
    expect(isProductionSource('src/lib/foo.test.ts')).toBe(false);
    expect(isProductionSource('src/lib/foo.spec.ts')).toBe(false);
    expect(isProductionSource('src/components/showcase/meeple-card.story.tsx')).toBe(false);
    expect(isProductionSource('src/components/x/GameChatTab.stories.tsx')).toBe(false);
  });

  it('esclude i file non sorgente', () => {
    expect(isProductionSource('src/styles/tokens.css')).toBe(false);
  });
});

describe('soughtFromSource', () => {
  it('estrae da un selettore attributo e da getByTestId', () => {
    const { ids } = soughtFromSource(
      [
        `await page.locator('[data-testid="chat-input"]').fill('x');`,
        `await page.getByTestId('send-message-button').click();`,
      ].join('\n')
    );
    expect(ids.map(i => i.id)).toEqual(['chat-input', 'send-message-button']);
    expect(ids[1].line).toBe(2);
  });

  it('estrae da un selettore composto con attributi aggiuntivi', () => {
    // La forma reale che #4105 ha trovato: `[data-testid="chat-message"][data-type="user"]`.
    const { ids } = soughtFromSource(
      `page.locator('[data-testid="chat-message"][data-type="user"]')`
    );
    expect(ids.map(i => i.id)).toEqual(['chat-message']);
  });

  it('conta a parte i siti dinamici invece di inventarsi un id', () => {
    const { ids, dynamicSites } = soughtFromSource(
      [
        'await page.getByTestId(`row-${id}`).click();',
        'await page.getByTestId(someVariable).click();',
        `await page.locator('[data-testid="static-one"]').click();`,
      ].join('\n')
    );
    expect(ids.map(i => i.id)).toEqual(['static-one']);
    expect(dynamicSites).toBe(2);
  });
});

describe('declaredFromSource', () => {
  it('raccoglie i letterali, anche in forma di espressione', () => {
    const { literals } = declaredFromSource(
      ['<div data-testid="chat-bubble" />', `<div data-testid={'typing-dot'} />`].join('\n')
    );
    expect([...literals].sort()).toEqual(['chat-bubble', 'typing-dot']);
  });

  it('da un template con interpolazione ricava un pattern, non un id', () => {
    const { literals, suffixes, prefixes } = declaredFromSource(
      '<button data-testid={`${testId}-apply`} />'
    );
    expect([...literals]).toEqual([]);
    expect([...suffixes]).toEqual(['-apply']);
    expect([...prefixes]).toEqual([]);
  });

  it('da un template con testa fissa ricava un prefisso', () => {
    const { prefixes } = declaredFromSource('<div data-testid={`admin-tab-${tab.id}`} />');
    expect([...prefixes]).toEqual(['admin-tab-']);
  });

  it('un template senza interpolazione è un letterale', () => {
    const { literals } = declaredFromSource('<div data-testid={`game-chat-header`} />');
    expect([...literals]).toEqual(['game-chat-header']);
  });

  it('legge i letterali kebab-case solo dai moduli di costanti', () => {
    const src = `export const IDS = { drawer: 'drawer-root', close: 'drawer-close' };`;
    expect([...declaredFromSource(src).literals]).toEqual([]);
    expect([...declaredFromSource(src, { isConstantsModule: true }).literals].sort()).toEqual([
      'drawer-close',
      'drawer-root',
    ]);
  });
});

describe('slotsFromSource', () => {
  it('raccoglie i data-slot letterali, anche in forma di espressione', () => {
    const slots = slotsFromSource(
      [
        '<button data-slot="citation-chip" />',
        `<div data-slot={'chat-citations'} />`,
        '<div data-slot={`typing-indicator`} />',
      ].join('\n')
    );
    expect([...slots].sort()).toEqual(['chat-citations', 'citation-chip', 'typing-indicator']);
  });

  it('ignora i template interpolati, che non danno un nome risolvibile', () => {
    expect([...slotsFromSource('<div data-slot={`tab-${id}`} />')]).toEqual([]);
  });

  it('serve a dire quando la correzione NON tocca la produzione', () => {
    // Il repository ha due convenzioni. Cinque orfani `data-testid` avevano un `data-slot`
    // omonimo — fra cui `offline-banner` con 12 sedi — e per quelli basta ripuntare la spec.
    const slots = slotsFromSource('<div data-slot="offline-banner" />');
    expect(slots.has('offline-banner')).toBe(true);
  });
});

describe('isConstantsModule', () => {
  it('riconosce i moduli di testid', () => {
    expect(
      isConstantsModule('src/components/ui/data-display/extra-meeple-card/drawer-test-ids.ts')
    ).toBe(true);
    expect(isConstantsModule('src/components/ui/button.tsx')).toBe(false);
  });
});

describe('isUnprovable', () => {
  const patterns = { suffixes: new Set(['-apply', '-count']), prefixes: new Set(['admin-tab-']) };

  it('un id coperto da un suffisso dinamico non è dimostrabile orfano', () => {
    expect(isUnprovable('filters-apply', patterns)).toBe(true);
    expect(isUnprovable('pending-count', patterns)).toBe(true);
  });

  it('un id coperto da un prefisso dinamico non è dimostrabile orfano', () => {
    expect(isUnprovable('admin-tab-users', patterns)).toBe(true);
  });

  it('un id che nessun pattern copre è dimostrabile', () => {
    expect(isUnprovable('game-card', patterns)).toBe(false);
  });
});

describe('absolvedBy', () => {
  const patterns = {
    suffixes: new Set(['-input', '-title']),
    prefixes: new Set(['message-', 'filter-chip-', 'filter-']),
  };

  it('nomina il pattern che assolve un id, per poterlo contestare', () => {
    // `message-citations` e` assolto da `message-*`, che nasce da
    // `data-testid={`message-${x}`}`. L'assoluzione e` letteralmente corretta — quel sito
    // POTREBBE produrre quel nome — ma e` implausibile: rende i messaggi di chat, non i
    // contenitori di citazioni. Senza vedere il pattern, l'assoluzione non e` contestabile.
    expect(absolvedBy('message-citations', patterns)).toEqual(['message-*']);
  });

  it('elenca tutti i pattern quando piu` di uno copre lo stesso id', () => {
    expect(absolvedBy('filter-chip-all', patterns).sort()).toEqual(['filter-*', 'filter-chip-*']);
  });

  it('distingue i suffissi dai prefissi nella notazione', () => {
    expect(absolvedBy('chess-message-input', patterns)).toContain('*-input');
  });

  it('non assolve un id che nessun pattern copre', () => {
    expect(absolvedBy('game-card', patterns)).toEqual([]);
  });
});

describe('classify', () => {
  it('separa dichiarati, orfani e indimostrabili', () => {
    const { orphans, unprovable, declared } = classify(
      ['chat-bubble', 'game-card', 'filters-apply'],
      {
        literals: new Set(['chat-bubble']),
        suffixes: new Set(['-apply']),
        prefixes: new Set(),
      }
    );
    expect(declared).toEqual(['chat-bubble']);
    expect(orphans).toEqual(['game-card']);
    expect(unprovable).toEqual(['filters-apply']);
  });

  it('un letterale dichiarato vince su un pattern che lo coprirebbe', () => {
    const { declared, unprovable } = classify(['filters-apply'], {
      literals: new Set(['filters-apply']),
      suffixes: new Set(['-apply']),
      prefixes: new Set(),
    });
    expect(declared).toEqual(['filters-apply']);
    expect(unprovable).toEqual([]);
  });
});

describe('compareToBaseline (la regola del cricchetto)', () => {
  const baseline = { 'e2e/a.spec.ts': 3, 'e2e/b.spec.ts': 1 };

  it('una spec nuova con orfani peggiora', () => {
    const cmp = compareToBaseline({ ...baseline, 'e2e/c.spec.ts': 1 }, baseline);
    expect(cmp.worsened).toBe(true);
    expect(cmp.brandNew).toEqual([{ file: 'e2e/c.spec.ts', count: 1 }]);
  });

  it('una spec esistente che cresce peggiora', () => {
    const cmp = compareToBaseline({ ...baseline, 'e2e/a.spec.ts': 4 }, baseline);
    expect(cmp.worsened).toBe(true);
    expect(cmp.grown).toEqual([{ file: 'e2e/a.spec.ts', was: 3, now: 4 }]);
  });

  it('un calo è un miglioramento, non un peggioramento', () => {
    const cmp = compareToBaseline({ ...baseline, 'e2e/a.spec.ts': 1 }, baseline);
    expect(cmp.worsened).toBe(false);
    expect(cmp.improved).toEqual([{ file: 'e2e/a.spec.ts', was: 3, now: 1 }]);
  });

  it('una spec ripulita del tutto sparisce dalla misura ed è segnalata', () => {
    const cmp = compareToBaseline({ 'e2e/a.spec.ts': 3 }, baseline);
    expect(cmp.worsened).toBe(false);
    expect(cmp.cleared).toEqual(['e2e/b.spec.ts']);
  });

  it('nessuna differenza non è né peggioramento né miglioramento', () => {
    const cmp = compareToBaseline({ ...baseline }, baseline);
    expect(cmp.worsened).toBe(false);
    expect(cmp.improved).toEqual([]);
    expect(cmp.cleared).toEqual([]);
  });
});
