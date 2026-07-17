import { existsSync } from 'node:fs';
import path from 'node:path';

import { describe, expect, it } from 'vitest';

import { PAIRS, DESIGN_FILES_DIR } from '../manifest';

describe('mockup-compare manifest', () => {
  it('has at least one pair', () => {
    expect(PAIRS.length).toBeGreaterThan(0);
  });

  it('has unique ids', () => {
    const ids = PAIRS.map(p => p.id);
    expect(new Set(ids).size).toBe(ids.length);
  });

  it('has slug-safe ids (kebab-case)', () => {
    // Gli id finiscono in attributi HTML E in selettori CSS/JS della gallery:
    // vincolarli a [a-z0-9-] rende l'escape HTML-only provabilmente sufficiente.
    for (const pair of PAIRS) {
      expect(pair.id, `${pair.id} non è slug-safe`).toMatch(/^[a-z0-9-]+$/);
    }
  });

  it('references mockup HTML files that exist on disk', () => {
    for (const pair of PAIRS) {
      const abs = path.join(DESIGN_FILES_DIR, pair.mockupHtml);
      expect(existsSync(abs), `${pair.id}: missing ${abs}`).toBe(true);
    }
  });
});
