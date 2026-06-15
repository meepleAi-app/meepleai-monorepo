/**
 * lint-bgg-mockups.test.ts — unit tests for lint-bgg-mockups.mjs (DS-17 §2151)
 *
 * Run: pnpm vitest run scripts/__tests__/lint-bgg-mockups.test.ts
 *
 * Refs:
 *   - Issue: #2151
 *   - Pattern: scripts/__tests__/lint-tokens-mockups.test.ts (DS-17-2 #2070)
 *   - ADR: docs/for-claude/architecture/adr/adr-059-catalog-seed-legal-posture.md
 */

import { describe, it, expect, beforeAll, afterAll } from 'vitest';
import { writeFileSync, mkdirSync, rmSync, existsSync } from 'node:fs';
import { resolve } from 'node:path';
import { tmpdir } from 'node:os';

import {
  BGG_USER_COPY_REGEX,
  findViolationsInText,
  hasAllowedJustification,
  isAdminScope,
  isWellKnownLegitimate,
  readDesignIntent,
  lintFiles,
} from '../lint-bgg-mockups.mjs';

const TMP_DIR = resolve(tmpdir(), `lint-bgg-mockups-tests-${process.pid}`);

beforeAll(() => {
  if (!existsSync(TMP_DIR)) mkdirSync(TMP_DIR, { recursive: true });
});

afterAll(() => {
  if (existsSync(TMP_DIR)) rmSync(TMP_DIR, { recursive: true, force: true });
});

describe('BGG_USER_COPY_REGEX', () => {
  it('matches BGG, BoardGameGeek, boardgamegeek case-insensitive', () => {
    const cases = ['BGG', 'bgg', 'BoardGameGeek', 'boardgamegeek', 'BOARDGAMEGEEK'];
    for (const s of cases) {
      const re = new RegExp(BGG_USER_COPY_REGEX.source, BGG_USER_COPY_REGEX.flags);
      expect(re.test(s), `should match: ${s}`).toBe(true);
    }
  });

  it('honors word boundaries — does NOT match substrings inside identifiers', () => {
    // "blogger" contains "bgg" but not as a word — must NOT match.
    const cases = ['blogger', 'beggar', 'BGGish identifier'];
    for (const s of cases) {
      const re = new RegExp(BGG_USER_COPY_REGEX.source, BGG_USER_COPY_REGEX.flags);
      const match = re.test(s);
      if (s === 'BGGish identifier') {
        // 'BGGish' should NOT match because 'BGG' has no trailing word boundary.
        expect(match, `should NOT match (no word boundary): ${s}`).toBe(false);
      } else {
        expect(match, `should NOT match (substring): ${s}`).toBe(false);
      }
    }
  });

  it('matches BGG followed by punctuation (still a word boundary)', () => {
    const cases = ['Cerca su BGG.', 'BGG?', '"BGG"', 'BGG-tagged'];
    for (const s of cases) {
      const re = new RegExp(BGG_USER_COPY_REGEX.source, BGG_USER_COPY_REGEX.flags);
      expect(re.test(s), `should match: ${s}`).toBe(true);
    }
  });
});

describe('hasAllowedJustification', () => {
  it('returns true when marker is on the same line', () => {
    const lines = ['const x = "BGG"; // BGG-ALLOWED: legitimate'];
    expect(hasAllowedJustification(lines, 0)).toBe(true);
  });

  it('returns true when marker is on the immediately preceding line', () => {
    const lines = ['// BGG-ALLOWED: block-comment header', 'const x = "BoardGameGeek";'];
    expect(hasAllowedJustification(lines, 1)).toBe(true);
  });

  it('returns true when marker is 2 lines above (block-comment + blank + usage)', () => {
    const lines = ['/* BGG-ALLOWED: parser */', '', 'const x = "BGG";'];
    expect(hasAllowedJustification(lines, 2)).toBe(true);
  });

  it('returns false when marker is 3+ lines above (out of window)', () => {
    const lines = ['// BGG-ALLOWED: top of file', '', '', 'const x = "BGG";'];
    expect(hasAllowedJustification(lines, 3)).toBe(false);
  });

  it('returns false when no marker is present', () => {
    const lines = ['const x = "BGG";'];
    expect(hasAllowedJustification(lines, 0)).toBe(false);
  });
});

describe('findViolationsInText', () => {
  it('returns empty array for clean text', () => {
    const result = findViolationsInText('const x = "neutral copy";', 'clean.ts');
    expect(result).toEqual([]);
  });

  it('flags BGG occurrence without justification marker', () => {
    const text = 'const cta = "Importa BGG";';
    const result = findViolationsInText(text, 'cta.ts');
    expect(result).toHaveLength(1);
    expect(result[0]).toMatchObject({ file: 'cta.ts', line: 1, match: 'BGG' });
  });

  it('skips BGG occurrence with same-line justification', () => {
    const text = 'const cta = "Importa BGG"; // BGG-ALLOWED: legacy admin tool';
    const result = findViolationsInText(text, 'admin-cta.ts');
    expect(result).toEqual([]);
  });

  it('skips BGG occurrence with justification on the preceding line', () => {
    const text = [
      '// BGG-ALLOWED: legitimate gamebook parser',
      'const TSV = "BoardGameGeek collection";',
    ].join('\n');
    const result = findViolationsInText(text, 'parser.ts');
    expect(result).toEqual([]);
  });

  it('flags multiple violations across separate unjustified lines', () => {
    const text = [
      'const cta1 = "Importa BGG";',
      'const cta2 = "Connetti BoardGameGeek";',
      'const cta3 = "Cerca su boardgamegeek";',
    ].join('\n');
    const result = findViolationsInText(text, 'multi.ts');
    expect(result).toHaveLength(3);
  });

  it('flags all hits on a line when none has justification', () => {
    const text = 'const both = "BGG and BoardGameGeek";';
    const result = findViolationsInText(text, 'same-line.ts');
    expect(result).toHaveLength(2);
  });

  it('skips ALL hits on a line when the line itself has justification', () => {
    const text = 'const both = "BGG and BoardGameGeek"; // BGG-ALLOWED: docstring example';
    const result = findViolationsInText(text, 'justified-same-line.ts');
    expect(result).toEqual([]);
  });
});

describe('isAdminScope', () => {
  it('matches FE admin app paths', () => {
    expect(isAdminScope('apps/web/src/app/admin/page.tsx')).toBe(true);
    expect(isAdminScope('apps/web/src/components/admin/foo.tsx')).toBe(true);
    expect(isAdminScope('apps/web/src/app/api/route.ts')).toBe(true);
  });

  it('normalizes backslashes', () => {
    expect(isAdminScope('apps\\web\\src\\app\\admin\\page.tsx')).toBe(true);
  });

  it('does NOT match non-admin paths', () => {
    expect(isAdminScope('apps/web/src/app/dashboard/page.tsx')).toBe(false);
    expect(isAdminScope('apps/web/src/components/dashboard/foo.tsx')).toBe(false);
  });
});

describe('isWellKnownLegitimate', () => {
  it('matches the documented allowlist exactly', () => {
    expect(isWellKnownLegitimate('apps/web/src/types/bgg.ts')).toBe(true);
    expect(isWellKnownLegitimate('apps/web/src/lib/api/clients/bggClient.ts')).toBe(true);
    expect(isWellKnownLegitimate('apps/web/src/lib/parsers/bgg-tsv.ts')).toBe(true);
    expect(isWellKnownLegitimate('apps/web/src/lib/games/cover-utils.ts')).toBe(true);
  });

  it('does NOT match unrelated files', () => {
    expect(isWellKnownLegitimate('apps/web/src/components/random.tsx')).toBe(false);
    expect(isWellKnownLegitimate('apps/web/src/types/other.ts')).toBe(false);
  });
});

describe('readDesignIntent', () => {
  it('returns design_intent from sibling fidelity.json', () => {
    const dir = resolve(TMP_DIR, 'fidelity-current');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'page.html'), '<div>BGG</div>', 'utf-8');
    writeFileSync(
      resolve(dir, 'page.fidelity.json'),
      JSON.stringify({ design_intent: 'current' }),
      'utf-8'
    );
    const intent = readDesignIntent(resolve(dir, 'page.html'));
    expect(intent).toBe('current');
  });

  it('returns "forward-refactor-obsolete" when set', () => {
    const dir = resolve(TMP_DIR, 'fidelity-obsolete');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'page.jsx'), 'const x = "BGG";', 'utf-8');
    writeFileSync(
      resolve(dir, 'page.fidelity.json'),
      JSON.stringify({ design_intent: 'forward-refactor-obsolete' }),
      'utf-8'
    );
    const intent = readDesignIntent(resolve(dir, 'page.jsx'));
    expect(intent).toBe('forward-refactor-obsolete');
  });

  it('strips -state-NN-<label> suffix to find canonical fidelity', () => {
    const dir = resolve(TMP_DIR, 'fidelity-state');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'page-state-02-empty.html'), '<div>BGG</div>', 'utf-8');
    writeFileSync(
      resolve(dir, 'page.fidelity.json'),
      JSON.stringify({ design_intent: 'forward-refactor-obsolete' }),
      'utf-8'
    );
    const intent = readDesignIntent(resolve(dir, 'page-state-02-empty.html'));
    expect(intent).toBe('forward-refactor-obsolete');
  });

  it('returns null when fidelity.json is absent', () => {
    const dir = resolve(TMP_DIR, 'fidelity-missing');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'page.html'), '<div>x</div>', 'utf-8');
    expect(readDesignIntent(resolve(dir, 'page.html'))).toBeNull();
  });

  it('returns null on malformed JSON', () => {
    const dir = resolve(TMP_DIR, 'fidelity-malformed');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'page.html'), '<div>x</div>', 'utf-8');
    writeFileSync(resolve(dir, 'page.fidelity.json'), '{ this is not json }', 'utf-8');
    expect(readDesignIntent(resolve(dir, 'page.html'))).toBeNull();
  });
});

describe('lintFiles (mockup mode — design_intent allowlist)', () => {
  it('flags violations when design_intent is "current"', () => {
    const dir = resolve(TMP_DIR, 'mockup-current');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'page.html'), '<div>Importa BGG</div>', 'utf-8');
    writeFileSync(
      resolve(dir, 'page.fidelity.json'),
      JSON.stringify({ design_intent: 'current' }),
      'utf-8'
    );
    const result = lintFiles('mockup-current/**/*.{html,jsx}', TMP_DIR, { isMockup: true });
    expect(result.violations.length).toBeGreaterThanOrEqual(1);
    expect(result.skipped.design_intent_obsolete).toBe(0);
  });

  it('skips entire file when design_intent is "forward-refactor-obsolete"', () => {
    const dir = resolve(TMP_DIR, 'mockup-obsolete');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'page.html'), '<div>Importa BGG cento volte</div>', 'utf-8');
    writeFileSync(
      resolve(dir, 'page.fidelity.json'),
      JSON.stringify({ design_intent: 'forward-refactor-obsolete' }),
      'utf-8'
    );
    const result = lintFiles('mockup-obsolete/**/*.{html,jsx}', TMP_DIR, { isMockup: true });
    expect(result.violations).toEqual([]);
    expect(result.skipped.design_intent_obsolete).toBe(1);
  });
});

describe('lintFiles (FE source mode — admin + well-known allowlist)', () => {
  it('skips files under admin paths', () => {
    const adminDir = resolve(TMP_DIR, 'apps/web/src/app/admin');
    mkdirSync(adminDir, { recursive: true });
    writeFileSync(resolve(adminDir, 'page.tsx'), 'const x = "BGG";', 'utf-8');

    const result = lintFiles('apps/web/src/app/admin/**/*.{ts,tsx,js,jsx}', TMP_DIR, {
      isMockup: false,
    });
    expect(result.violations).toEqual([]);
    expect(result.skipped.admin_scope).toBe(1);
  });

  it('skips well-known legitimate files', () => {
    const dir = resolve(TMP_DIR, 'apps/web/src/types');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'bgg.ts'), 'export interface BggSearchResult { /* … */ }', 'utf-8');

    const result = lintFiles('apps/web/src/types/**/*.{ts,tsx,js,jsx}', TMP_DIR, {
      isMockup: false,
    });
    expect(result.violations).toEqual([]);
    expect(result.skipped.well_known_legitimate).toBe(1);
  });

  it('flags non-allowlisted FE files', () => {
    const dir = resolve(TMP_DIR, 'apps/web/src/components/dashboard');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'card.tsx'), 'const cta = "Importa BGG";', 'utf-8');

    const result = lintFiles('apps/web/src/components/dashboard/**/*.{ts,tsx,js,jsx}', TMP_DIR, {
      isMockup: false,
    });
    expect(result.violations.length).toBeGreaterThanOrEqual(1);
  });
});

describe('lintFiles (stable output)', () => {
  it('produces stable file/line/column sort', () => {
    const dir = resolve(TMP_DIR, 'sort-stable');
    mkdirSync(dir, { recursive: true });
    writeFileSync(resolve(dir, 'b-second.tsx'), 'const x = "BGG";', 'utf-8');
    writeFileSync(
      resolve(dir, 'a-first.tsx'),
      'const x = "BGG";\nconst y = "BoardGameGeek";',
      'utf-8'
    );

    const result = lintFiles('sort-stable/**/*.tsx', TMP_DIR, { isMockup: false });
    expect(result.violations.length).toBe(3);
    expect(result.violations[0].file).toContain('a-first.tsx');
    expect(result.violations[2].file).toContain('b-second.tsx');
  });
});
