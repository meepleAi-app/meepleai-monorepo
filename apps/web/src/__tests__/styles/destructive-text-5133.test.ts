import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';

import { AA_NORMAL_TEXT, contrastRatio, parseCssColor } from '@/test-utils/wcag';

// #5133: the pairs #4538's single token did not reach. Decision of 2026-11-23:
// - light theme: one darker --destructive, 0 74% 42%, for the fills and the text alike;
// - dark theme: a token only for text, --destructive-text 0 90% 74%. One value cannot serve both there: a fill under
//   light text must be dark, the same red as text on a dark surface must be light.
// Surfaces: the page is the canonical --bg (the body paints var(--bg)); a card, bg-muted are globals.css's --card and
// --muted; the Alert tint is bg-destructive/10 (light) and /20 (dark) over the page; a Badge's hover fill is 80%.
const STYLES = resolve(__dirname, '..', '..', 'styles');
const CSS = readFileSync(resolve(STYLES, 'globals.css'), 'utf8');
const CANONICAL = readFileSync(resolve(STYLES, 'design-tokens-canonical.css'), 'utf8');

const DARK_START = CSS.indexOf('\n:is(.dark, [data-theme="dark"]) {');
const LIGHT = CSS.slice(CSS.indexOf('\n:root {'), DARK_START);
const DARK = CSS.slice(DARK_START, CSS.indexOf('\n}', DARK_START));

function triplet(block: string, name: string): string {
  const m = block.match(new RegExp(`\\n\\s*${name}:\\s*([\\d.]+\\s+[\\d.]+%\\s+[\\d.]+%)\\s*;`));
  if (!m) throw new Error(`${name} not found as an HSL triplet`);
  return m[1].replace(/\s+/g, ' ');
}

const hsl = (block: string, name: string) => `hsl(${triplet(block, name)})`;

function page(theme: 'light' | 'dark'): string {
  const darkAt = CANONICAL.indexOf(':root[data-theme="dark"]');
  const block = theme === 'light' ? CANONICAL.slice(0, darkAt) : CANONICAL.slice(darkAt);
  const m = block.match(/\n\s*--bg:\s*(#[0-9a-f]{6})\s*;/i);
  if (!m) throw new Error(`--bg not found in the ${theme} block of the canonical tokens`);
  return m[1];
}

/** `top` at `alpha` over the opaque `bottom`, as the browser composites a translucent fill. */
function over(top: string, alpha: number, bottom: string): string {
  const t = parseCssColor(top);
  const b = parseCssColor(bottom);
  const [r, g, bl] = t.map((c, i) => Math.round((c * alpha + b[i] * (1 - alpha)) * 255));
  return `rgb(${r}, ${g}, ${bl})`;
}

describe('--destructive in the light theme reaches every pair (#5133)', () => {
  const red = hsl(LIGHT, '--destructive');
  const text = hsl(LIGHT, '--destructive-text');

  it('is the decided value, and the text token is the same red', () => {
    expect(triplet(LIGHT, '--destructive')).toBe('0 74% 42%');
    expect(triplet(LIGHT, '--destructive-text')).toBe(triplet(LIGHT, '--destructive'));
  });

  it('keeps a Badge readable at its 80% hover fill', () => {
    const fill = over(red, 0.8, page('light'));
    expect(contrastRatio(hsl(LIGHT, '--destructive-foreground'), fill)).toBeGreaterThanOrEqual(
      AA_NORMAL_TEXT
    );
  });

  it.each([
    ['the cream page', () => page('light')],
    ['a white card', () => hsl(LIGHT, '--card')],
    ['bg-muted', () => hsl(LIGHT, '--muted')],
    ['the Alert tint (bg-destructive/10)', () => over(red, 0.1, page('light'))],
  ])('as text on %s', (_, surface) => {
    expect(contrastRatio(text, surface())).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
  });
});

describe('--destructive-text in the dark theme (#5133)', () => {
  const text = hsl(DARK, '--destructive-text');

  it.each([
    ['the dark page', () => page('dark')],
    ['a dark card', () => hsl(DARK, '--card')],
    ['bg-muted', () => hsl(DARK, '--muted')],
    [
      'the Alert tint (bg-destructive/20)',
      () => over(hsl(DARK, '--destructive'), 0.2, page('dark')),
    ],
  ])('as text on %s', (_, surface) => {
    expect(contrastRatio(text, surface())).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
  });

  it('is a different red from the fill, which stays dark under its light text', () => {
    expect(triplet(DARK, '--destructive-text')).not.toBe(triplet(DARK, '--destructive'));
    expect(
      contrastRatio(hsl(DARK, '--destructive-foreground'), hsl(DARK, '--destructive'))
    ).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
  });
});

describe('text-destructive is not used for text any more (#5133)', () => {
  // The red of text is text-destructive-text: in the dark theme the fill's red is too dark to read on a dark surface.
  // bg-, border-, ring- and text-destructive-foreground keep the fill's token. The mockup fixtures are frozen copies.
  const SRC = resolve(__dirname, '..', '..');

  function files(dir: string): string[] {
    return readdirSync(dir).flatMap(name => {
      const path = join(dir, name);
      if (statSync(path).isDirectory()) return name === 'node_modules' ? [] : files(path);
      return /\.(tsx?|mdx|css)$/.test(name) ? [path] : [];
    });
  }

  // Also the arbitrary form, text-[hsl(var(--destructive))]: the settings row's label used it, and the first pass,
  // which converted only the named utility, missed it (self-review of #5138).
  it('no source names the bare text-destructive, or reads the fill token as text', () => {
    const offenders = files(SRC)
      .filter(
        path =>
          !path.includes(join('__tests__', 'fixtures')) && resolve(path) !== resolve(__filename)
      )
      .filter(path =>
        /(?<![\w-])text-destructive(?![\w-])|(?<![\w-])text-\[(?:color:)?hsl\(var\(--destructive\)\)\]/.test(
          readFileSync(path, 'utf8')
        )
      )
      .map(path => relative(SRC, path).replace(/\\/g, '/'));
    expect(offenders).toEqual([]);
  });
});
