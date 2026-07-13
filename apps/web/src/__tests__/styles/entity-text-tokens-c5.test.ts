import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';

const CSS = readFileSync(
  resolve(__dirname, '..', '..', 'styles', 'design-tokens-canonical.css'),
  'utf8'
);

// The EntityBadge glass pill renders on Tailwind's `bg-card`, i.e. the semantic
// `--card` token (globals.css), NOT the `--bg-card` mockup family. Its solid
// value is the conservative AA surface: light `--card` = `0 0% 100%` (#ffffff),
// dark `--card` = `0 0% 18%` (~#2e2e2e, globals.css:658). Validate against those
// — the same target `--c-session-text` is tuned for (globals.css:714).
const LIGHT_BG: [number, number, number] = [255, 255, 255];
const DARK_BG: [number, number, number] = hslToRgb(0, 0, 18);

function block(theme: 'light' | 'dark'): string {
  if (theme === 'light') return CSS.slice(0, CSS.indexOf('[data-theme="dark"]'));
  return CSS.slice(CSS.indexOf('[data-theme="dark"]'));
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
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  };
  return 0.2126 * f(r) + 0.7152 * f(g) + 0.0722 * f(b);
}
function contrast(a: [number, number, number], b: [number, number, number]): number {
  const [hi, lo] = lum(a) > lum(b) ? [lum(a), lum(b)] : [lum(b), lum(a)];
  return (hi + 0.05) / (lo + 0.05);
}
function readVar(theme: 'light' | 'dark', name: string): [number, number, number] {
  const m = block(theme).match(new RegExp(name + ':\\s*(\\d+)\\s+(\\d+)%\\s+(\\d+)%'));
  if (!m) throw new Error(`${name} not found in ${theme} block`);
  return hslToRgb(Number(m[1]), Number(m[2]), Number(m[3]));
}

describe('C5 — new --c-*-text vars are AA on the EntityBadge pill (#2862)', () => {
  it.each(['--c-event-text', '--c-agent-text', '--c-chat-text'])(
    '%s light value >= 4.5:1 on white card',
    name => {
      expect(contrast(readVar('light', name), LIGHT_BG)).toBeGreaterThanOrEqual(4.5);
    }
  );
  it.each(['--c-event-text', '--c-agent-text', '--c-chat-text'])(
    '%s dark value >= 4.5:1 on dark card',
    name => {
      expect(contrast(readVar('dark', name), DARK_BG)).toBeGreaterThanOrEqual(4.5);
    }
  );
});
