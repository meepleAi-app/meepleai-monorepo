/**
 * glass-alpha-audit.test.ts — unit tests per glass-alpha-audit.mjs
 * (scala glass a 3 livelli, #3919 · Epic #3916)
 *
 * Run: pnpm vitest run scripts/__tests__/glass-alpha-audit.test.ts
 */
import { describe, it, expect } from 'vitest';
import {
  SCALE,
  GUARDED_TEXTS,
  ADHOC_LEVEL_MAP,
  parseHex,
  luminance,
  contrast,
  composite,
  minAlphaFor,
  levelFor,
  readToken,
  readThemes,
} from '../glass-alpha-audit.mjs';

const WHITE = [255, 255, 255];
const BLACK = [0, 0, 0];

describe('parseHex', () => {
  it('legge la forma a 6 e a 3 cifre', () => {
    expect(parseHex('#1e1710')).toEqual([30, 23, 16]);
    expect(parseHex('#fff')).toEqual([255, 255, 255]);
  });
  it('rifiuta un valore non esadecimale invece di restituire NaN', () => {
    expect(() => parseHex('rgba(255,255,255,0.88)')).toThrow(/hex non valido/);
  });
});

describe('contrast', () => {
  it('nero su bianco è 21:1, il massimo della scala WCAG', () => {
    expect(contrast(BLACK, WHITE)).toBeCloseTo(21, 5);
  });
  it('è simmetrico rispetto all\'ordine degli argomenti', () => {
    expect(contrast(BLACK, WHITE)).toBe(contrast(WHITE, BLACK));
  });
  it('un colore contro sé stesso è 1:1', () => {
    expect(contrast([30, 23, 16], [30, 23, 16])).toBeCloseTo(1, 10);
  });
});

describe('luminance', () => {
  it('vale 0 sul nero e 1 sul bianco', () => {
    expect(luminance(BLACK)).toBeCloseTo(0, 10);
    expect(luminance(WHITE)).toBeCloseTo(1, 10);
  });
});

describe('composite', () => {
  it('con alpha 1 restituisce la superficie, con 0 il fondo', () => {
    const S = [30, 23, 16];
    expect(composite(S, WHITE, 1)).toEqual(S);
    expect(composite(S, WHITE, 0)).toEqual(WHITE);
  });
  it('a metà strada sta a metà fra i due, canale per canale', () => {
    expect(composite([0, 0, 0], [200, 100, 50], 0.5)).toEqual([100, 50, 25]);
  });
});

describe('minAlphaFor', () => {
  const darkSurface = [30, 23, 16]; // --bg-card dark
  const darkText = [240, 228, 210]; // --text dark
  const darkTextSec = [200, 184, 150]; // --text-sec dark

  it('la soglia trovata raggiunge il target, un filo sotto no', () => {
    const a = minAlphaFor(darkSurface, darkTextSec, WHITE, 4.5);
    expect(a).not.toBeNull();
    expect(contrast(composite(darkSurface, WHITE, a!), darkTextSec)).toBeGreaterThanOrEqual(4.5);
    expect(contrast(composite(darkSurface, WHITE, a! - 0.01), darkTextSec)).toBeLessThan(4.5);
  });

  it('--text-sec richiede più opacità di --text: è lui il vincolo binding', () => {
    const aText = minAlphaFor(darkSurface, darkText, WHITE, 4.5)!;
    const aSec = minAlphaFor(darkSurface, darkTextSec, WHITE, 4.5)!;
    expect(aSec).toBeGreaterThan(aText);
  });

  it('restituisce null quando nemmeno la superficie opaca raggiunge il target', () => {
    // #9a8870 (--text-muted light) su bianco pieno resta a 3,43:1
    expect(minAlphaFor(WHITE, [154, 136, 112], BLACK, 4.5)).toBeNull();
  });
});

describe('readToken', () => {
  const css = `
:root[data-theme="light"] {
  --bg-card:   #ffffff;
  --text:      #2b1f12;
}

:root[data-theme="dark"] {
  --bg-card:   #1e1710;
  --text:      #f0e4d2;
}
`;
  it('legge il token dal blocco richiesto, non da quello successivo', () => {
    expect(readToken(css, ':root[data-theme="light"]', '--bg-card')).toEqual([255, 255, 255]);
    expect(readToken(css, ':root[data-theme="dark"]', '--bg-card')).toEqual([30, 23, 16]);
  });
  it('fallisce se il blocco o il token non esistono, invece di ripiegare su un default', () => {
    expect(() => readToken(css, ':root[data-theme="sepia"]', '--bg-card')).toThrow(/blocco/);
    expect(() => readToken(css, ':root[data-theme="dark"]', '--nope')).toThrow(/non trovato/);
  });
});

describe('la terna decisa in #3919 §6', () => {
  const themes = readThemes();

  it('legge entrambi i temi dal CSS canonico', () => {
    expect(Object.keys(themes).sort()).toEqual(['dark', 'light']);
    for (const t of Object.values(themes)) expect(t.surface).toHaveLength(3);
  });

  it('il fondo peggiore è l\'estremo opposto alla superficie del tema', () => {
    expect(themes.dark.worstBackdrop).toEqual(WHITE);
    expect(themes.light.worstBackdrop).toEqual(BLACK);
  });

  it.each(['functional', 'content'] as const)(
    '%s tiene AA su --text e --text-sec, in entrambi i temi, sul fondo peggiore',
    (level) => {
      for (const theme of Object.values(themes)) {
        for (const text of GUARDED_TEXTS) {
          const c = contrast(
            composite(theme.surface, theme.worstBackdrop, SCALE[level].alpha),
            theme.texts[text]
          );
          expect(c).toBeGreaterThanOrEqual(4.5);
        }
      }
    }
  );

  it('content raggiunge AAA su --text-sec: è il livello del testo long-form', () => {
    for (const theme of Object.values(themes)) {
      const c = contrast(
        composite(theme.surface, theme.worstBackdrop, SCALE.content.alpha),
        theme.texts['--text-sec']
      );
      expect(c).toBeGreaterThanOrEqual(7);
    }
  });

  it('decorative non promette contrasto: chi ci mette testo fallisce in modo visibile', () => {
    expect(SCALE.decorative.carriesText).toBe(false);
    const c = contrast(
      composite(themes.dark.surface, themes.dark.worstBackdrop, SCALE.decorative.alpha),
      themes.dark.texts['--text']
    );
    expect(c).toBeLessThan(2);
  });

  it('functional non ha margine da regalare: 0,80 sarebbe a meno di mezzo punto da AA', () => {
    const c = contrast(
      composite(themes.dark.surface, themes.dark.worstBackdrop, 0.8),
      themes.dark.texts['--text-sec']
    );
    expect(c).toBeGreaterThanOrEqual(4.5);
    expect(c).toBeLessThan(5);
  });
});

describe('levelFor', () => {
  it('non deriva il livello dalla distanza: /50 porta testo, /90 sono input', () => {
    expect(levelFor(50)).toBe('functional'); // il punto medio direbbe decorative
    expect(levelFor(90)).toBe('functional'); // il punto medio direbbe content
  });
  it('un alpha non classificato fallisce invece di essere interpolato', () => {
    expect(() => levelFor(45)).toThrow(/non classificato/);
  });
  // 14, non 13: la issue e il piano omettono /25, che ha un solo uso.
  it('copre i 14 alpha ad-hoc rilevati sul codice', () => {
    expect(ADHOC_LEVEL_MAP.size).toBe(14);
    for (const level of ADHOC_LEVEL_MAP.values()) expect(SCALE).toHaveProperty(level);
  });
});
