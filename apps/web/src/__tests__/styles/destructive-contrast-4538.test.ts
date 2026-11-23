import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

import { AA_NORMAL_TEXT, contrastRatio } from '@/test-utils/wcag';

// #4538: `--destructive` colours three pairs in the light theme, and each must be AA (4.5:1) for normal text, since the
// badges are `text-xs`:
// - `bg-destructive` under `text-destructive-foreground` (Button `destructive`, Badge, at rest);
// - `text-destructive-text` on the cream page background, the form errors: the body paints `var(--bg)` from
//   design-tokens-canonical.css (#f7f3ee), darker than globals.css's `--background`, so that is the one measured;
// - `text-destructive-text` on a white card (`--card`).
// Decision of 2026-11-23: one token for the three, `0 72% 47%`, no dedicated text token. The dark theme is unchanged.
// design-tokens-canonical.css does not redefine `--destructive` (it has `--c-danger`, a different red outside this
// decision). The pairs this decision does not reach (the Alert tint, the Badge hover fill, the dark theme's text) are
// measured in #4538's PR and tracked in #5133.
const CSS = readFileSync(resolve(__dirname, '..', '..', 'styles', 'globals.css'), 'utf8');
const CANONICAL = readFileSync(
  resolve(__dirname, '..', '..', 'styles', 'design-tokens-canonical.css'),
  'utf8'
);

const LIGHT_START = CSS.indexOf('\n:root {');
const DARK_START = CSS.indexOf('\n:is(.dark, [data-theme="dark"]) {');

function block(theme: 'light' | 'dark'): string {
  if (LIGHT_START < 0 || DARK_START < 0) throw new Error('theme blocks not found in globals.css');
  if (theme === 'light') return CSS.slice(LIGHT_START, DARK_START);
  return CSS.slice(DARK_START, CSS.indexOf('\n}', DARK_START));
}

/** The HSL triplet of `name` in a theme block: `--x: H S% L%;`, decimals allowed. */
function triplet(theme: 'light' | 'dark', name: string): string {
  const m = block(theme).match(
    new RegExp(`\\n\\s*${name}:\\s*([\\d.]+\\s+[\\d.]+%\\s+[\\d.]+%)\\s*;`)
  );
  if (!m) throw new Error(`${name} not found in the ${theme} block of globals.css`);
  return m[1].replace(/\s+/g, ' ');
}

const hsl = (theme: 'light' | 'dark', name: string) => `hsl(${triplet(theme, name)})`;

function lightPageBackground(): string {
  const light = CANONICAL.slice(0, CANONICAL.indexOf(':root[data-theme="dark"]'));
  const m = light.match(/\n\s*--bg:\s*(#[0-9a-f]{6})\s*;/i);
  if (!m) throw new Error('--bg not found in the light block of design-tokens-canonical.css');
  return m[1];
}

describe('--destructive is AA in the light theme (#4538)', () => {
  it('under its foreground, as Button destructive and Badge paint it at rest', () => {
    expect(
      contrastRatio(hsl('light', '--destructive-foreground'), hsl('light', '--destructive'))
    ).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
  });

  it('as text on the cream page background', () => {
    expect(
      contrastRatio(hsl('light', '--destructive'), lightPageBackground())
    ).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
  });

  it('as text on a white card', () => {
    expect(
      contrastRatio(hsl('light', '--destructive'), hsl('light', '--card'))
    ).toBeGreaterThanOrEqual(AA_NORMAL_TEXT);
  });

  it('leaves the dark theme where it was', () => {
    // The decision covers the light theme only: the dark value is pinned, not just kept above a threshold.
    expect(triplet('dark', '--destructive')).toBe('0 72% 51%');
    expect(triplet('dark', '--destructive-foreground')).toBe('0 0% 98%');
  });
});
