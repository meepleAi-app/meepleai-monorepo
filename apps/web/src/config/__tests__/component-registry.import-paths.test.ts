import fs from 'node:fs';
import path from 'node:path';
import { fileURLToPath } from 'node:url';

import { describe, it, expect } from 'vitest';

import { COMPONENT_REGISTRY } from '../component-registry';

/**
 * Guards the invariant that every `importPath` declared in COMPONENT_REGISTRY
 * resolves to a real file on disk.
 *
 * The existing component-registry.test.ts only checks that importPath is a
 * non-empty string — never that it points at a module that exists. That gap let
 * the "MeepleCard Features" block accumulate 26 entries pointing at a ghost
 * directory `@/components/ui/data-display/meeple-card-features/` (see audit
 * docs/for-developers/audits/2026-07-12-meeplecard-css-drift-audit.md). This test
 * turns "the map lies about the filesystem" into a red build.
 *
 * Resolution mirrors tsconfig paths: `@/*` → `src/*` (apps/web/tsconfig.json).
 * We assert file existence (fast, environment-agnostic) rather than dynamically
 * importing — importing would execute `use client` React modules needlessly.
 */

const HERE = path.dirname(fileURLToPath(import.meta.url));
const SRC_ROOT = path.resolve(HERE, '../..'); // apps/web/src/config/__tests__ → apps/web/src
const EXT_CANDIDATES = ['.ts', '.tsx', '/index.ts', '/index.tsx'];

function resolveAliasImport(importPath: string): string | null {
  if (!importPath.startsWith('@/')) return null;
  const rel = importPath.slice(2); // strip '@/'
  const base = path.join(SRC_ROOT, rel);
  if (fs.existsSync(base) && fs.statSync(base).isFile()) return base;
  for (const ext of EXT_CANDIDATES) {
    const candidate = ext.startsWith('/') ? path.join(base, ext.slice(1)) : base + ext;
    if (fs.existsSync(candidate)) return candidate;
  }
  return null;
}

describe('COMPONENT_REGISTRY importPath resolution', () => {
  it('every entry uses the "@/" path alias', () => {
    const nonAlias = COMPONENT_REGISTRY.filter(e => !e.importPath.startsWith('@/')).map(e => e.id);
    expect(nonAlias).toEqual([]);
  });

  it.each(COMPONENT_REGISTRY.map(e => [e.id, e.importPath] as const))(
    'entry "%s" resolves importPath "%s" to an existing file',
    (_id, importPath) => {
      expect(resolveAliasImport(importPath)).not.toBeNull();
    }
  );
});
