import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

// #4538: `--destructive` colours three pairs in the light theme, and each must be AA (4.5:1) for normal text, since the
// badges are `text-xs`:
// - `bg-destructive` under `text-destructive-foreground` (Button `destructive`, Badge, alerts);
// - `text-destructive` on the cream page background, the form errors: the body paints `var(--bg)` from
//   design-tokens-canonical.css (#f7f3ee), darker than globals.css's `--background`, so that is the one measured;
// - `text-destructive` on a white card (`--card`).
// Decision of 2026-11-23: one token for the three, `0 72% 47%`, no dedicated text token. The dark theme is unchanged.
// design-tokens-canonical.css does not redefine `--destructive` (it has `--c-danger`, a different red outside this
// decision).
const CSS = readFileSync(resolve(__dirname, '..', '..', 'styles', 'globals.css'), 'utf8');
const CANONICAL = readFileSync(
  resolve(__dirname, '..', '..', 'styles', 'design-tokens-canonical.css'),
  'utf8'
);

function lightPageBackground(): [number, number, number] {
  const light = CANONICAL.slice(0, CANONICAL.indexOf(':root[data-theme="dark"]'));
  const m = light.match(/\n\s*--bg:\s*#([0-9a-f]{2})([0-9a-f]{2})([0-9a-f]{2})\s*;/i);
  if (!m) throw new Error('--bg not found in the light block of design-tokens-canonical.css');
  return [parseInt(m[1], 16), parseInt(m[2], 16), parseInt(m[3], 16)];
}

const LIGHT_START = CSS.indexOf('\n:root {');
const DARK_START = CSS.indexOf('\n:is(.dark, [data-theme="dark"]) {');

function block(theme: 'light' | 'dark'): string {
  if (LIGHT_START < 0 || DARK_START < 0) throw new Error('theme blocks not found in globals.css');
  if (theme === 'light') return CSS.slice(LIGHT_START, DARK_START);
  const end = CSS.indexOf('\n}', DARK_START);
  return CSS.slice(DARK_START, end);
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

/** The value of `name` in a theme block: `--x: H S% L%;`, decimals allowed. */
function readVar(theme: 'light' | 'dark', name: string): [number, number, number] {
  const m = block(theme).match(
    new RegExp(`\\n\\s*${name}:\\s*([\\d.]+)\\s+([\\d.]+)%\\s+([\\d.]+)%\\s*;`)
  );
  if (!m) throw new Error(`${name} not found in the ${theme} block of globals.css`);
  return hslToRgb(Number(m[1]), Number(m[2]), Number(m[3]));
}

describe('--destructive is AA in the light theme (#4538)', () => {
  it('under its foreground, as Button destructive, Badge and alerts paint it', () => {
    const ratio = contrast(
      readVar('light', '--destructive-foreground'),
      readVar('light', '--destructive')
    );
    expect(ratio).toBeGreaterThanOrEqual(4.5);
  });

  it('as text on the cream page background', () => {
    expect(
      contrast(readVar('light', '--destructive'), lightPageBackground())
    ).toBeGreaterThanOrEqual(4.5);
  });

  it('as text on a white card', () => {
    expect(
      contrast(readVar('light', '--destructive'), readVar('light', '--card'))
    ).toBeGreaterThanOrEqual(4.5);
  });

  it('leaves the dark theme where it was', () => {
    // 0 72% 51% under 0 0% 98%: 4.59:1 before this change, and it does not move.
    const ratio = contrast(
      readVar('dark', '--destructive-foreground'),
      readVar('dark', '--destructive')
    );
    expect(ratio).toBeGreaterThanOrEqual(4.5);
  });
});
