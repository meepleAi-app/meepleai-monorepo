import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { ENTITY_TOKENS, getEntityToken, type EntityType } from './entity-tokens';

describe('entity-tokens', () => {
  it('provides 9 canonical entity types', () => {
    const types: EntityType[] = [
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
    types.forEach(t => {
      const token = getEntityToken(t);
      expect(token.bg).toContain('bg-entity-');
      expect(token.text).toContain('text-entity-');
      expect(token.emoji).toBeTruthy();
      expect(token.label).toBeTruthy();
    });
  });

  it('maps kb to document tailwind class', () => {
    expect(getEntityToken('kb').bg).toBe('bg-entity-document');
  });

  it('returns emoji for toolkit as 🧰', () => {
    expect(getEntityToken('toolkit').emoji).toBe('🧰');
  });

  // #3161: In Tailwind v4, only CSS variables declared inside an @theme block emit
  // `entity-<key>` utilities. `kb` maps to the tailwind class `document`, but
  // `--color-entity-document` was missing from the @theme inline block (it lived only in
  // a plain :root in design-tokens.css), so `bg/text/border/ring-entity-document` produced
  // no CSS and the knowledge-base accent silently resolved to nothing.
  it('registers a @theme entity utility for every tailwind key (TW v4)', () => {
    const globalsCss = readFileSync(resolve(process.cwd(), 'src/styles/globals.css'), 'utf8');
    const themeStart = globalsCss.indexOf('@theme inline');
    expect(themeStart).toBeGreaterThan(-1);
    const braceStart = globalsCss.indexOf('{', themeStart);
    const braceEnd = globalsCss.indexOf('}', braceStart);
    const themeBlock = globalsCss.slice(braceStart, braceEnd);

    const missing = ENTITY_TOKENS.map(t => getEntityToken(t).bg.replace('bg-entity-', '')).filter(
      key => !themeBlock.includes(`--color-entity-${key}:`)
    );

    expect(missing).toEqual([]);
  });

  // #3917: `text` now resolves to the `-text` variant, which is the shade
  // calibrated for AA when the entity color is used as TEXT on a light tint
  // (the base hues are sized for borders and fills, ~3:1). Same Tailwind v4
  // trap as the test above: a variant missing from `@theme inline` emits no
  // CSS, so the class would be inert and the text would silently inherit its
  // colour — the failure mode #3161 documents.
  it('registers a @theme `-text` entity utility for every tailwind key (TW v4)', () => {
    const globalsCss = readFileSync(resolve(process.cwd(), 'src/styles/globals.css'), 'utf8');
    const themeStart = globalsCss.indexOf('@theme inline');
    const braceStart = globalsCss.indexOf('{', themeStart);
    const braceEnd = globalsCss.indexOf('}', braceStart);
    const themeBlock = globalsCss.slice(braceStart, braceEnd);

    const missing = ENTITY_TOKENS.map(t =>
      getEntityToken(t).text.replace('text-entity-', '')
    ).filter(key => !themeBlock.includes(`--color-entity-${key}:`));

    expect(missing).toEqual([]);
  });

  it('uses the AA-calibrated `-text` variant, not the base entity colour', () => {
    // Regression guard for the /shared-games contrast failures uncovered once
    // the a11y gate stopped scanning a 404 (#3917): toolkit 4.29:1 and agent
    // 4.26:1 against their own `bgSoft` tint, both just under the 4.5:1 floor.
    expect(getEntityToken('toolkit').text).toBe('text-entity-toolkit-text');
    expect(getEntityToken('agent').text).toBe('text-entity-agent-text');
    expect(getEntityToken('kb').text).toBe('text-entity-document-text');
  });
});
