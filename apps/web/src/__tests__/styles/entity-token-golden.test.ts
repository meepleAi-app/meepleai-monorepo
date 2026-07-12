import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, it, expect } from 'vitest';

/**
 * Golden token test (issue #2855, follow-up of audit
 * docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md).
 *
 * The design tokens have a single source of truth: the mockup
 * `admin-mockups/design_files/tokens.css` is mirrored by the app's
 * `design-tokens-canonical.css`. This test asserts every entity accent token
 * `--c-{entity}` (all light + dark declarations, in order) is identical between
 * the two files, so the app cannot silently drift from the mockup.
 *
 * The canonical (and mockup) light values were aligned to the AA-safe #807 gate
 * values that already win at runtime via globals.css import-order — chosen over
 * the original vivid mockup values to avoid an AA migration of 68 text sites
 * (see the audit + issue #2855 decision).
 */

const HERE = path.dirname(fileURLToPath(import.meta.url));
const CANONICAL = path.resolve(HERE, '../../styles/design-tokens-canonical.css');
const MOCKUP = path.resolve(HERE, '../../../../../admin-mockups/design_files/tokens.css');

const ENTITIES = [
  'game',
  'player',
  'session',
  'agent',
  'kb',
  'chat',
  'event',
  'toolkit',
  'tool',
] as const;

function extractEntityTokens(css: string, entity: string): string[] {
  // Matches `--c-game: ...;` but NOT `--c-game-text: ...;` (the ':' must follow the entity name).
  const re = new RegExp('--c-' + entity + ':\\s*([^;]+);', 'g');
  return [...css.matchAll(re)].map(m => m[1].trim());
}

describe('entity token golden — canonical mirrors the mockup', () => {
  const canonicalCss = fs.readFileSync(CANONICAL, 'utf8');
  const mockupCss = fs.readFileSync(MOCKUP, 'utf8');

  it.each(ENTITIES)('--c-%s values are identical in canonical and mockup', entity => {
    const canonical = extractEntityTokens(canonicalCss, entity);
    const mockup = extractEntityTokens(mockupCss, entity);
    expect(canonical.length).toBeGreaterThan(0);
    expect(canonical).toEqual(mockup);
  });
});
