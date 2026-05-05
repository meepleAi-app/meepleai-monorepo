import { describe, it, expect } from 'vitest';
import { entityColors, entityHsl, entityLabel, entityIcon, statusColors } from '../tokens';
import type { MeepleEntityType } from '../types';

const allEntities: MeepleEntityType[] = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
];

// WCAG 2.1 SC 1.4.3 helpers — used for regression coverage of #636.
type Rgb = [number, number, number];

function hslToRgb(h: number, sPct: string, lPct: string): Rgb {
  const s = parseFloat(sPct) / 100;
  const l = parseFloat(lPct) / 100;
  const c = (1 - Math.abs(2 * l - 1)) * s;
  const hp = h / 60;
  const x = c * (1 - Math.abs((hp % 2) - 1));
  let r1 = 0;
  let g1 = 0;
  let b1 = 0;
  if (hp < 1) [r1, g1, b1] = [c, x, 0];
  else if (hp < 2) [r1, g1, b1] = [x, c, 0];
  else if (hp < 3) [r1, g1, b1] = [0, c, x];
  else if (hp < 4) [r1, g1, b1] = [0, x, c];
  else if (hp < 5) [r1, g1, b1] = [x, 0, c];
  else [r1, g1, b1] = [c, 0, x];
  const m = l - c / 2;
  return [Math.round((r1 + m) * 255), Math.round((g1 + m) * 255), Math.round((b1 + m) * 255)];
}

function hexToRgb(hex: string): Rgb {
  const cleaned = hex.replace('#', '');
  return [
    parseInt(cleaned.slice(0, 2), 16),
    parseInt(cleaned.slice(2, 4), 16),
    parseInt(cleaned.slice(4, 6), 16),
  ];
}

function relLum(rgb: Rgb): number {
  const [r, g, b] = rgb.map(v => {
    const s = v / 255;
    return s <= 0.03928 ? s / 12.92 : Math.pow((s + 0.055) / 1.055, 2.4);
  });
  return 0.2126 * r + 0.7152 * g + 0.0722 * b;
}

function contrastRatio(a: Rgb, b: Rgb): number {
  const la = relLum(a);
  const lb = relLum(b);
  const [light, dark] = la > lb ? [la, lb] : [lb, la];
  return (light + 0.05) / (dark + 0.05);
}

const WHITE: Rgb = [255, 255, 255];
const WCAG_AA_NORMAL = 4.5; // SC 1.4.3 normal-size text threshold

describe('entityColors', () => {
  it.each(allEntities)('has color definition for %s', entity => {
    const color = entityColors[entity];
    expect(color).toBeDefined();
    expect(color.h).toBeTypeOf('number');
    expect(color.s).toMatch(/%$/);
    expect(color.l).toMatch(/%$/);
  });

  it('has exactly 9 entity types', () => {
    expect(Object.keys(entityColors)).toHaveLength(9);
  });
});

describe('entityHsl', () => {
  it('returns hsl string without alpha', () => {
    expect(entityHsl('game')).toMatch(/^hsl\(/);
  });

  it('returns hsla string with alpha', () => {
    expect(entityHsl('game', 0.5)).toMatch(/^hsla\(/);
  });
});

describe('entityLabel', () => {
  it.each(allEntities)('has label for %s', entity => {
    expect(entityLabel[entity]).toBeTypeOf('string');
    expect(entityLabel[entity].length).toBeGreaterThan(0);
  });
});

describe('entityIcon', () => {
  it.each(allEntities)('has icon for %s', entity => {
    expect(entityIcon[entity]).toBeTypeOf('string');
  });
});

/**
 * WCAG 2.1 AA SC 1.4.3 regression suite — issue #636.
 *
 * EntityBadge renders white text over `entityHsl(entity)` solid bg at 9px
 * font-extrabold (normal-size text per WCAG, threshold 4.5:1).
 * StatusBadge renders `statusColors[k].text` over `statusColors[k].bg` at
 * 9px font-bold (also normal-size, 4.5:1 threshold).
 *
 * Lock the contract here so any future token tweak that drops below 4.5:1
 * fails fast in unit tests instead of leaking to axe-core E2E exclusions.
 */
describe('entityColors WCAG 2.1 AA contrast (regression for #636)', () => {
  it.each(allEntities)('%s solid bg + white text >= 4.5:1', entity => {
    const c = entityColors[entity];
    const bg = hslToRgb(c.h, c.s, c.l);
    const ratio = contrastRatio(bg, WHITE);
    expect(ratio).toBeGreaterThanOrEqual(WCAG_AA_NORMAL);
  });
});

describe('statusColors WCAG 2.1 AA contrast (regression for #636)', () => {
  it.each(Object.keys(statusColors))('%s text on bg >= 4.5:1', statusKey => {
    const { bg, text } = statusColors[statusKey];
    const ratio = contrastRatio(hexToRgb(text), hexToRgb(bg));
    expect(ratio).toBeGreaterThanOrEqual(WCAG_AA_NORMAL);
  });
});
