/**
 * #5001 (ADR-098 §5): i suggerimenti d'avvio sono domande sulle regole.
 *
 * Con un gioco scelto il terzo era «Strategia per {game}»: i consigli di strategia non stanno nel
 * regolamento. Senza gioco c'era «Risolvi una disputa»: l'arbitrato non è della chat, perché lì
 * mancano la situazione di partita e il seguito, e vive nella partita dal vivo (ADR-098 §2, #4171).
 */
import { describe, expect, it } from 'vitest';

import { getQuickStartSuggestions } from '../constants';

describe('getQuickStartSuggestions (#5001)', () => {
  it.each([
    ['senza gioco', undefined],
    ['con un gioco', 'Catan'],
  ])('%s, nessun suggerimento parla di strategia o di dispute', (_case, game) => {
    const suggestions = getQuickStartSuggestions(game);

    expect(suggestions.length).toBeGreaterThan(0);
    for (const s of suggestions) {
      expect(`${s.label} ${s.message}`).not.toMatch(/strateg|disput/i);
    }
  });

  it('con un gioco, ogni suggerimento nomina il gioco', () => {
    for (const s of getQuickStartSuggestions('Catan')) {
      expect(s.message).toContain('Catan');
    }
  });
});
