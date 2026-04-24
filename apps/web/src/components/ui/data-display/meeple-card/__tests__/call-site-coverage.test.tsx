import { describe, it, expect } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { sync as globSync } from 'glob';
import { parse } from '@babel/parser';
import _traverse from '@babel/traverse';

// CJS/ESM interop: @babel/traverse's default export is the function itself.
const traverse = (_traverse as unknown as { default?: typeof _traverse }).default ?? _traverse;

interface Hit {
  file: string;
  line: number;
  componentName: string;
}

/**
 * Gate 1 — Step 2 call-site migration coverage.
 *
 * Walks every production .tsx file under src/{app,components} (excluding
 * tests, dev playground, and showcase) and uses Babel's AST to find any
 * JSX element whose name matches /^Meeple.*Card$/ AND has an attribute
 * named exactly `navItems`. Any match is a Step 2 violation.
 *
 * Design notes:
 * - We match `Meeple*Card` rather than `MeepleCard` directly because in
 *   production the card components are wrapper components (e.g.
 *   MeepleSessionCard) that forward props to MeepleCard. The wrappers
 *   are where authors actually write `navItems=...`.
 * - The old `navItems` prop on the wrapper components will be removed in
 *   commit 8 (Cleanup). Until commit 8, this test is the source of truth
 *   for "have we migrated everything?".
 */
describe('Step 2 — call-site migration coverage (Gate 1)', () => {
  it('zero production files use navItems= on Meeple* card components', () => {
    const root = resolve(__dirname, '..', '..', '..', '..', '..', '..'); // -> apps/web
    const files = globSync('src/{app,components}/**/*.tsx', {
      cwd: root,
      ignore: [
        '**/__tests__/**',
        'src/app/(public)/dev/**',
        'src/components/**/dev/**',
        '**/showcase/**',
        // Exclude the meeple-card package itself: its variants and renderer
        // legitimately reference navItems internally until commit 8.
        'src/components/ui/data-display/meeple-card/**',
      ],
      absolute: true,
    });

    const hits: Hit[] = [];

    for (const file of files) {
      const source = readFileSync(file, 'utf8');
      let ast;
      try {
        ast = parse(source, {
          sourceType: 'module',
          plugins: ['jsx', 'typescript'],
        });
      } catch {
        // Skip unparseable files (none expected in production)
        continue;
      }

      traverse(ast, {
        JSXOpeningElement(path) {
          const name = path.node.name;
          if (name.type !== 'JSXIdentifier') return;
          if (!/^Meeple.*Card$/.test(name.name)) return;

          for (const attr of path.node.attributes) {
            if (attr.type !== 'JSXAttribute') continue;
            if (attr.name.type !== 'JSXIdentifier') continue;
            if (attr.name.name !== 'navItems') continue;
            hits.push({
              file: file.replace(root + '/', '').replace(root + '\\', ''),
              line: attr.loc?.start.line ?? -1,
              componentName: name.name,
            });
          }
        },
      });
    }

    if (hits.length > 0) {
      const detail = hits
        .map(h => `  ${h.file}:${h.line}  <${h.componentName} navItems=...>`)
        .join('\n');
      throw new Error(
        `Found ${hits.length} unmigrated call-site(s) using navItems= on Meeple* cards:\n${detail}\n\nMigrate to connections= per spec docs/superpowers/specs/2026-04-24-connectionchip-step-2-call-site-migration-design.md.`
      );
    }

    expect(hits).toEqual([]);
  });
});
