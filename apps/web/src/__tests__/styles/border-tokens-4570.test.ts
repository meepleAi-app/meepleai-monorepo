import { describe, it, expect } from 'vitest';
import { readFileSync, readdirSync, statSync } from 'node:fs';
import { join, relative, resolve } from 'node:path';

// #4570: `--color-border: hsl(var(--border))` met the canonical `--border: rgba(…)`, an invalid colour, so every
// `border-border` fell back to currentColor, the colour of the text; and `--border: hsl(var(--border))` referred to
// itself, a cycle that made the token invalid inside the admin shell. Measured in Chrome before the fix: the border of a
// card, an Input and a Select trigger was the text colour in both themes, and dark on dark (1.18:1) in the admin shell.
// Decision of 2026-11-23: the decorative border is the canonical `--border`; the border of a form control is `--input`,
// >= 3:1 on its background (WCAG 1.4.11).
const SRC = resolve(__dirname, '..', '..');
const STYLES = join(SRC, 'styles');
const GLOBALS = readFileSync(join(STYLES, 'globals.css'), 'utf8');
const CANONICAL = readFileSync(join(STYLES, 'design-tokens-canonical.css'), 'utf8');

function files(dir: string, keep: (path: string) => boolean): string[] {
  return readdirSync(dir).flatMap(name => {
    const path = join(dir, name);
    if (statSync(path).isDirectory()) return name === 'node_modules' ? [] : files(path, keep);
    return keep(path) ? [path] : [];
  });
}

function hslToRgb(h: number, s: number, l: number): [number, number, number] {
  s /= 100;
  l /= 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const hp = h / 60;
  const x = c * (1 - Math.abs((hp % 2) - 1));
  let r = 0,
    g = 0,
    b = 0;
  if (hp < 1) [r, g, b] = [c, x, 0];
  else if (hp < 2) [r, g, b] = [x, c, 0];
  else if (hp < 3) [r, g, b] = [0, c, x];
  else if (hp < 4) [r, g, b] = [0, x, c];
  else if (hp < 5) [r, g, b] = [x, 0, c];
  else [r, g, b] = [c, 0, x];
  const m = l - c / 2;
  return [Math.round((r + m) * 255), Math.round((g + m) * 255), Math.round((b + m) * 255)];
}

function lum([r, g, b]: [number, number, number]): number {
  const f = (v: number) => {
    const s = v / 255;
    return s <= 0.04045 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}

function contrast(a: [number, number, number], b: [number, number, number]): number {
  const [hi, lo] = lum(a) > lum(b) ? [lum(a), lum(b)] : [lum(b), lum(a)];
  return (hi + 0.05) / (lo + 0.05);
}

const LIGHT = GLOBALS.slice(
  GLOBALS.indexOf('\n:root {'),
  GLOBALS.indexOf('\n:is(.dark, [data-theme="dark"]) {')
);
const DARK = GLOBALS.slice(GLOBALS.indexOf('\n:is(.dark, [data-theme="dark"]) {'));

function hslVar(block: string, name: string): [number, number, number] {
  const m = block.match(
    new RegExp(`\\n\\s*${name}:\\s*([\\d.]+)\\s+([\\d.]+)%\\s+([\\d.]+)%\\s*;`)
  );
  if (!m) throw new Error(`${name} not found as an HSL triplet`);
  return hslToRgb(Number(m[1]), Number(m[2]), Number(m[3]));
}

function canonicalBg(theme: 'light' | 'dark'): [number, number, number] {
  const darkAt = CANONICAL.indexOf(':root[data-theme="dark"]');
  const block = theme === 'light' ? CANONICAL.slice(0, darkAt) : CANONICAL.slice(darkAt);
  const m = block.match(/\n\s*--bg:\s*#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})\s*;/i);
  if (!m) throw new Error(`--bg not found in the ${theme} block of the canonical tokens`);
  return [parseInt(m[1], 16), parseInt(m[2], 16), parseInt(m[3], 16)];
}

describe('border tokens (#4570)', () => {
  it('no custom property refers to itself, in any stylesheet', () => {
    const declaration = /(--[\w-]+)\s*:\s*([^;{}]*);/g;
    const cycles = files(SRC, path => path.endsWith('.css')).flatMap(path => {
      const css = readFileSync(path, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
      return [...css.matchAll(declaration)]
        .filter(([, name, value]) => new RegExp(`var\\(\\s*${name}\\s*[,)]`).test(value))
        .map(([text]) => `${relative(SRC, path).replace(/\\/g, '/')}: ${text.trim()}`);
    });
    expect(cycles).toEqual([]);
  });

  it('--border is never read as an HSL triplet: it is the canonical rgba()', () => {
    const offenders = files(
      SRC,
      // The mockup pilots' fixtures are frozen copies of the design handoff, and this file names the pattern it bans.
      path =>
        /\.(css|tsx?|mdx)$/.test(path) &&
        !path.includes(join('__tests__', 'fixtures')) &&
        resolve(path) !== resolve(__filename)
    ).flatMap(path => {
      const text = readFileSync(path, 'utf8').replace(/\/\*[\s\S]*?\*\//g, '');
      // --border-strong and --border-light are canonical rgba() too (#5134's review found four hsl() reads of the latter).
      return /hsl\(\s*var\(\s*--border(-strong|-light)?\s*\)/.test(text)
        ? [relative(SRC, path).replace(/\\/g, '/')]
        : [];
    });
    expect(offenders).toEqual([]);
    expect(GLOBALS).toMatch(/--color-border:\s*var\(--border\);/);
  });

  it('the canonical --border is a colour in both themes', () => {
    expect(CANONICAL.match(/\n\s*--border:\s*rgba\(/g)?.length).toBeGreaterThanOrEqual(2);
  });

  it('a form control does not take the decorative border-border', () => {
    // Text scan of the opening tag of <input>, <select>, <textarea>, the shape of the ~175 sites #4570 moved to
    // border-input. An `=>` in the tag (onChange={e => …}) is not its end: the first draft of this scan stopped there and
    // passed with most controls still on border-border (review of #5134). Not seen: a comparison `a > b` before the
    // class, a class built elsewhere and passed in by name, and a control styled from a CSS file.
    const tag =
      /<(input|select|textarea)\b(?:=>|[^>])*?(?<![\w-])(?:[\w-]+:)*border-border(?:\/\d+)?(?![\w-])/;
    const offenders = files(
      SRC,
      path => path.endsWith('.tsx') && !path.includes('__tests__') && !path.endsWith('.test.tsx')
    ).flatMap(path =>
      tag.test(readFileSync(path, 'utf8')) ? [relative(SRC, path).replace(/\\/g, '/')] : []
    );
    expect(offenders).toEqual([]);
  });

  it('--input, the border of a form control, is >= 3:1 on the light page and on a white card', () => {
    const input = hslVar(LIGHT, '--input');
    expect(contrast(input, canonicalBg('light'))).toBeGreaterThanOrEqual(3);
    expect(contrast(input, hslVar(LIGHT, '--card'))).toBeGreaterThanOrEqual(3);
  });

  it('--input is >= 3:1 on the dark page and on a dark card', () => {
    const input = hslVar(DARK, '--input');
    expect(contrast(input, canonicalBg('dark'))).toBeGreaterThanOrEqual(3);
    expect(contrast(input, hslVar(DARK, '--card'))).toBeGreaterThanOrEqual(3);
  });
});
