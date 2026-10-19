/**
 * #4525: i riempimenti `bg-entity-kb` del pannello KB (invio, CTA dello stato vuoto, numero
 * della citazione) devono leggersi in AA in entrambi i temi.
 *
 * jsdom non calcola i colori (niente CSS di Tailwind, e axe in jsdom non misura il contrasto).
 * Il test fa quello che farebbe il browser, sui file veri:
 * - un'utility di colore (`bg-<nome>`, `text-<nome>`) diventa la `--color-<nome>` del tema di
 *   Tailwind: prima il tema predefinito di `tailwindcss/theme.css`, poi i blocchi `@theme` di
 *   `globals.css`;
 * - i `var()` di quel valore si risolvono con la cascata delle regole di tema di primo livello di
 *   `design-tokens-canonical.css` e `globals.css` (specificità, poi ordine dei file);
 * - poi il rapporto WCAG fra sfondo e testo, in tema chiaro e in tema scuro.
 * Un'utility di colore che il test non sa risolvere, o con una variante che non gestisce, fa
 * fallire il test invece di essere scartata.
 */
import fs from 'node:fs';
import { createRequire } from 'node:module';
import path from 'node:path';

import { render, screen } from '@testing-library/react';
import { describe, expect, it, vi } from 'vitest';

import { CitationPill } from '../CitationPill';
import { DrawerEmpty } from '../DrawerEmpty';
import { DrawerIdle } from '../DrawerIdle';

type Rgb = readonly [number, number, number];
type Theme = 'light' | 'dark';

/** I commenti tolti prima di cercare: una dichiarazione commentata non deve contare. */
function readCss(file: string): string {
  return fs.readFileSync(file, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
}

const STYLES = path.join(__dirname, '../../../../styles');
const canonicalCss = readCss(path.join(STYLES, 'design-tokens-canonical.css'));
const globalsCss = readCss(path.join(STYLES, 'globals.css'));
const tailwindThemeCss = readCss(createRequire(__filename).resolve('tailwindcss/theme.css'));

const escapeRegExp = (s: string): string => s.replace(/[.*+?^${}()|[\]\\]/g, '\\$&');

/** I corpi dei blocchi di primo livello che si aprono con `head {` all'inizio di una riga. */
function blocks(css: string, head: string): string[] {
  const bodies: string[] = [];
  for (const match of css.matchAll(new RegExp(`^${escapeRegExp(head)}\\s*\\{`, 'gm'))) {
    const open = match.index + match[0].length - 1;
    let depth = 0;
    for (let i = open; i < css.length; i++) {
      if (css[i] === '{') depth++;
      else if (css[i] === '}' && --depth === 0) {
        bodies.push(css.slice(open + 1, i));
        break;
      }
    }
  }
  if (bodies.length === 0) throw new Error(`blocco non trovato: ${head}`);
  return bodies;
}

/** Le proprietà personalizzate dichiarate nei blocchi: a parità di nome vince l'ultima. */
function declarations(css: string, head: string): Map<string, string> {
  const map = new Map<string, string>();
  for (const body of blocks(css, head)) {
    for (const m of body.matchAll(/(--[\w-]+)\s*:\s*([^;{}]+);/g)) map.set(m[1], m[2].trim());
  }
  return map;
}

/** Il tema di Tailwind, nell'ordine in cui si sovrascrive: predefinito, poi `globals.css`. */
const THEME = new Map([
  ...declarations(tailwindThemeCss, '@theme default'),
  ...declarations(globalsCss, '@theme'),
  ...declarations(globalsCss, '@theme inline'),
]);

/**
 * Le regole di tema sull'`<html>`. `specificity` per tema: 0 = la regola non si applica.
 * Nell'ordine in cui `app/layout.tsx` carica i file.
 */
const RULES: ReadonlyArray<{ decls: Map<string, string>; specificity: Record<Theme, number> }> = [
  { decls: declarations(canonicalCss, ':root'), specificity: { light: 10, dark: 10 } },
  {
    decls: declarations(canonicalCss, ':root[data-theme="light"]'),
    specificity: { light: 20, dark: 10 },
  },
  {
    decls: declarations(canonicalCss, '[data-theme="dark"]'),
    specificity: { light: 0, dark: 20 },
  },
  { decls: declarations(globalsCss, ':root'), specificity: { light: 10, dark: 10 } },
  {
    decls: declarations(globalsCss, ':is(.dark, [data-theme="dark"])'),
    specificity: { light: 0, dark: 10 },
  },
];

/** Il valore calcolato di una proprietà personalizzata sull'`<html>`, nel tema. */
function customProperty(name: string, theme: Theme): string | undefined {
  let winner: { value: string; specificity: number } | undefined;
  for (const rule of RULES) {
    const specificity = rule.specificity[theme];
    const value = rule.decls.get(name);
    if (specificity === 0 || value === undefined) continue;
    if (!winner || specificity >= winner.specificity) winner = { value, specificity };
  }
  return winner?.value ?? THEME.get(name);
}

function substituteVars(value: string, theme: Theme, depth = 0): string {
  if (depth > 10) throw new Error(`var() circolari in "${value}"`);
  return value.replace(/var\((--[\w-]+)\s*(?:,\s*([^)]*))?\)/g, (_all, name: string, fallback) => {
    const resolved = customProperty(name, theme) ?? fallback;
    if (resolved === undefined) throw new Error(`${name} non è dichiarata (${theme})`);
    return substituteVars(resolved.trim(), theme, depth + 1);
  });
}

function hslToRgb(h: number, s: number, l: number): Rgb {
  const sat = s / 100;
  const light = l / 100;
  const k = (n: number): number => (n + h / 30) % 12;
  const f = (n: number): number =>
    light - sat * Math.min(light, 1 - light) * Math.max(-1, Math.min(k(n) - 3, 9 - k(n), 1));
  return [Math.round(f(0) * 255), Math.round(f(8) * 255), Math.round(f(4) * 255)];
}

/** Colori opachi in esadecimale, `hsl()` o `rgb()`. Altri formati, o la trasparenza, falliscono. */
function parseColour(value: string): Rgb {
  const v = value.trim();
  const hex = /^#([0-9a-f]{3}|[0-9a-f]{6})$/i.exec(v);
  if (hex) {
    const digits = hex[1].length === 3 ? [...hex[1]].map(d => d + d) : hex[1].match(/../g)!;
    return digits.map(d => parseInt(d, 16)) as unknown as Rgb;
  }
  const hsl = /^hsl\(\s*([\d.]+)[\s,]+([\d.]+)%[\s,]+([\d.]+)%\s*\)$/.exec(v);
  if (hsl) return hslToRgb(Number(hsl[1]), Number(hsl[2]), Number(hsl[3]));
  const rgb = /^rgb\(\s*([\d.]+)[\s,]+([\d.]+)[\s,]+([\d.]+)\s*\)$/.exec(v);
  if (rgb) return [Number(rgb[1]), Number(rgb[2]), Number(rgb[3])];
  throw new Error(`colore che il test non sa leggere: "${v}"`);
}

/** Le utility `text-*` e `bg-*` che non sono colori. */
const NOT_A_COLOUR = {
  text: /^(?:xs|sm|base|lg|xl|[2-9]xl|left|center|right|justify|start|end|wrap|nowrap|balance|pretty|ellipsis|clip|\[[\d.]+(?:px|rem|em|%)\])$/,
  bg: /^(?:none|fixed|local|scroll|cover|contain|auto|center|top|bottom|left|right|left-top|left-bottom|right-top|right-bottom|repeat|no-repeat|repeat-x|repeat-y|repeat-round|repeat-space|(?:clip|origin|blend|gradient|linear|radial|conic)-.+)$/,
} as const;

/** Le varianti di stato: un colore che cambia lì non è coperto dal rapporto calcolato. */
const STATE_VARIANTS = new Set([
  'hover',
  'active',
  'focus',
  'focus-visible',
  'focus-within',
  'group-hover',
]);

interface ColourClass {
  readonly cls: string;
  readonly kind: 'bg' | 'text';
  readonly name: string;
  readonly variants: readonly string[];
}

function colourClasses(className: string): ColourClass[] {
  return className
    .split(/\s+/)
    .filter(Boolean)
    .flatMap(cls => {
      const [, prefix, utility] = /^((?:[a-z][\w-]*:)*)(.+)$/.exec(cls)!;
      const m = /^(bg|text)-(.+)$/.exec(utility);
      if (!m) return [];
      const kind = m[1] as 'bg' | 'text';
      if (NOT_A_COLOUR[kind].test(m[2])) return [];
      return [{ cls, kind, name: m[2], variants: prefix.split(':').filter(Boolean) }];
    });
}

function resolveUtility({ cls, name }: ColourClass, theme: Theme): Rgb {
  if (name.includes('/') || name.startsWith('[')) {
    throw new Error(`${cls}: trasparenza o valore arbitrario, il test non sa comporli`);
  }
  const value = THEME.get(`--color-${name}`);
  if (value === undefined) throw new Error(`${cls}: --color-${name} non è nel tema`);
  return parseColour(substituteVars(value, theme));
}

/** Sfondo e testo che le classi dipingono in un tema: in tema scuro vince l'utility `dark:`. */
function paint(className: string, theme: Theme): { bg: Rgb; fg: Rgb } {
  const classes = colourClasses(className).filter(
    c => !c.variants.some(v => STATE_VARIANTS.has(v))
  );
  const unhandled = classes.filter(c => c.variants.some(v => v !== 'dark'));
  if (unhandled.length > 0) {
    throw new Error(`varianti che il test non gestisce: ${unhandled.map(c => c.cls).join(', ')}`);
  }
  const pick = (kind: 'bg' | 'text'): Rgb => {
    const own = classes.filter(c => c.kind === kind);
    const darkOnly = own.filter(c => c.variants.includes('dark'));
    const base = own.filter(c => c.variants.length === 0);
    const chosen = theme === 'dark' && darkOnly.length > 0 ? darkOnly : base;
    if (chosen.length !== 1) {
      throw new Error(
        `attesa una sola utility ${kind} in "${className}" (${theme}), trovate: ${chosen.map(c => c.cls).join(', ') || 'nessuna'}`
      );
    }
    return resolveUtility(chosen[0], theme);
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
    it('does not change its colours in hover, active or focus states', () => {
      const stateColours = colourClasses(renderTarget().className)
        .filter(c => c.variants.some(v => STATE_VARIANTS.has(v)))
        .map(c => c.cls);
      expect(stateColours).toEqual([]);
    });
  });

  /**
   * Revisione di #4526: col solo `hover:shadow-md` l'hover non si vedeva sulla card scura, e il
   * preflight di Tailwind 4 mette `cursor: default` sui `<button>`. Un anello attorno al bottone
   * si vede in entrambi i temi e non tocca la coppia sfondo/testo.
   */
  describe.each(targets.slice(0, 2))('%s', (_name, renderTarget) => {
    it('shows the pointer and a hover ring outside the colour pair', () => {
      const button = renderTarget();
      expect(button).toHaveClass('cursor-pointer', 'hover:ring-2', 'hover:ring-entity-kb/40');
    });
  });
});
