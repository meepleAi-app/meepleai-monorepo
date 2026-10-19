/**
 * #4525: i riempimenti `bg-entity-kb` del pannello KB (invio, CTA dello stato vuoto, numero
 * della citazione) devono leggersi in AA in entrambi i temi.
 *
 * jsdom non calcola i colori (niente CSS di Tailwind, e axe in jsdom non misura il contrasto):
 * il test legge i valori dei token da `design-tokens-canonical.css` e `globals.css`, risolve le
 * classi del nodo nel colore di sfondo e del testo per ogni tema, e calcola il rapporto WCAG.
 * Un'utility di colore che il risolutore non conosce fa fallire il test, invece di saltarlo.
 */
import fs from 'node:fs';
import path from 'node:path';

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { CitationPill } from '../CitationPill';
import { DrawerEmpty } from '../DrawerEmpty';
import { DrawerIdle } from '../DrawerIdle';

type Rgb = readonly [number, number, number];
type Theme = 'light' | 'dark';

const STYLES = path.join(__dirname, '../../../../styles');
const canonicalCss = fs.readFileSync(path.join(STYLES, 'design-tokens-canonical.css'), 'utf8');
const globalsCss = fs.readFileSync(path.join(STYLES, 'globals.css'), 'utf8');

/** Le dichiarazioni della prima regola di primo livello con questo selettore. */
function ruleBody(css: string, selector: string): string {
  const start = css.indexOf(`${selector} {`);
  if (start < 0) throw new Error(`regola non trovata: ${selector}`);
  const open = css.indexOf('{', start);
  return css.slice(open + 1, css.indexOf('\n}', open));
}

function hslToRgb(h: number, s: number, l: number): Rgb {
  const sat = s / 100;
  const light = l / 100;
  const k = (n: number): number => (n + h / 30) % 12;
  const f = (n: number): number =>
    light - sat * Math.min(light, 1 - light) * Math.max(-1, Math.min(k(n) - 3, 9 - k(n), 1));
  return [Math.round(f(0) * 255), Math.round(f(8) * 255), Math.round(f(4) * 255)];
}

/** Un token `H S% L%` letto dalla regola indicata. */
function token(css: string, selector: string, name: string): Rgb {
  const match = new RegExp(`(?:^|\\s)${name}:\\s*([\\d.]+)\\s+([\\d.]+)%\\s+([\\d.]+)%`).exec(
    ruleBody(css, selector)
  );
  if (!match) throw new Error(`${name} non trovato in ${selector}`);
  return hslToRgb(Number(match[1]), Number(match[2]), Number(match[3]));
}

const CANONICAL_LIGHT = ':root';
const CANONICAL_DARK = '[data-theme="dark"]';
const GLOBALS_LIGHT = ':root';
const GLOBALS_DARK = ':is(.dark, [data-theme="dark"])';
const WHITE: Rgb = [255, 255, 255];

/** Le utility di colore che compaiono su questi nodi (o che una correzione userebbe). */
const PAINT: Record<string, Record<Theme, Rgb>> = {
  'bg-entity-kb': {
    light: token(canonicalCss, CANONICAL_LIGHT, '--c-kb'),
    dark: token(canonicalCss, CANONICAL_DARK, '--c-kb'),
  },
  'bg-primary': {
    light: token(globalsCss, GLOBALS_LIGHT, '--primary'),
    dark: token(globalsCss, GLOBALS_DARK, '--primary'),
  },
  'text-white': { light: WHITE, dark: WHITE },
  'text-background': {
    light: token(globalsCss, GLOBALS_LIGHT, '--background'),
    dark: token(globalsCss, GLOBALS_DARK, '--background'),
  },
  'text-primary-foreground': {
    light: token(globalsCss, GLOBALS_LIGHT, '--primary-foreground'),
    dark: token(globalsCss, GLOBALS_DARK, '--primary-foreground'),
  },
};

/** Sfondo e testo che le classi dipingono in un tema: in tema scuro vince l'utility `dark:`. */
function paint(className: string, theme: Theme): { bg: Rgb; fg: Rgb } {
  const classes = className.split(/\s+/).filter(Boolean);
  const pick = (kind: 'bg' | 'text'): Rgb => {
    const known = (c: string): boolean => c.startsWith(`${kind}-`) && c in PAINT;
    const base = classes.filter(known);
    const darkOnly = classes
      .filter(c => c.startsWith(`dark:${kind}-`))
      .map(c => c.slice('dark:'.length));
    const unknownDark = darkOnly.filter(c => !known(c));
    if (unknownDark.length > 0) {
      throw new Error(`utility dark: che il test non sa risolvere: ${unknownDark.join(', ')}`);
    }
    const chosen = theme === 'dark' && darkOnly.length > 0 ? darkOnly : base;
    if (chosen.length !== 1) {
      throw new Error(
        `attesa una sola utility ${kind} nota in "${className}" (${theme}), trovate: ${chosen.join(', ') || 'nessuna'}`
      );
    }
    return PAINT[chosen[0]][theme];
  };
  return { bg: pick('bg'), fg: pick('text') };
}

function luminance([r, g, b]: Rgb): number {
  const lin = (v: number): number => {
    const c = v / 255;
    return c <= 0.04045 ? c / 12.92 : ((c + 0.055) / 1.055) ** 2.4;
  };
  return 0.2126 * lin(r) + 0.7152 * lin(g) + 0.0722 * lin(b);
}

function contrast(a: Rgb, b: Rgb): number {
  const [hi, lo] = [luminance(a), luminance(b)].sort((x, y) => y - x);
  return (hi + 0.05) / (lo + 0.05);
}

/** SC 1.4.3: testo normale. Tutti e tre i nodi hanno testo piccolo (9-14px). */
const AA_NORMAL_TEXT = 4.5;

const targets: ReadonlyArray<readonly [string, () => HTMLElement]> = [
  [
    'DrawerIdle send button',
    () => {
      render(
        <DrawerIdle
          suggestions={[]}
          onAsk={vi.fn()}
          labels={{
            welcomeTitle: 'Ciao',
            welcomeBody: 'Cerco nei tuoi PDF.',
            suggestionsLabel: 'Suggerimenti',
            placeholder: 'Chiedi al Meeple…',
            sendLabel: 'Invia',
          }}
        />
      );
      return screen.getByRole('button', { name: 'Invia' });
    },
  ],
  [
    'DrawerEmpty call to action',
    () => {
      render(
        <DrawerEmpty
          onCta={vi.fn()}
          labels={{
            title: 'Nessun documento',
            body: 'Carica un PDF.',
            cta: 'Vai alla libreria',
          }}
        />
      );
      return screen.getByRole('button', { name: 'Vai alla libreria' });
    },
  ],
  [
    'CitationPill number',
    () => {
      render(<CitationPill n={3} refText="p.14" docId="doc-1" page={14} ariaLabel="Citazione 3" />);
      return screen.getByText('3');
    },
  ],
];

describe('#4525 — KB filled buttons of the panel read in AA in both themes', () => {
  describe.each(targets)('%s', (_name, renderTarget) => {
    it.each(['light', 'dark'] as const)('reaches 4.5:1 in the %s theme', theme => {
      const { bg, fg } = paint(renderTarget().className, theme);
      const ratio = contrast(bg, fg);
      expect(ratio, `${theme}: ${ratio.toFixed(2)}:1`).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
    });

    it('keeps the KB identity', () => {
      expect(renderTarget()).toHaveClass('bg-entity-kb');
    });

    /** `hover:bg-entity-kb/90` sopra la card bianca portava il testo bianco a 4,27:1 in tema chiaro. */
    it('does not change its colours on hover', () => {
      expect(renderTarget().className).not.toMatch(/(?:^|\s)(?:dark:)?hover:(?:bg|text)-/);
    });
  });
});
