import { describe, it, expect } from 'vitest';
import { existsSync, readFileSync } from 'node:fs';
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
  kind: 'navItems' | 'spread';
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

    // Safeguard 1 — path-drift sanity assertion. If the __dirname-relative
    // path is wrong (e.g. package moved, or '..' segment count off), the
    // glob below would return nothing and the loop would run 0 iterations,
    // making the test pass vacuously. Verify root really points to apps/web
    // by checking package.json exists there.
    if (!existsSync(resolve(root, 'package.json'))) {
      throw new Error(
        `Gate 1 path drift: resolved root ${root} does not contain package.json. The __dirname-relative path is wrong; fix the number of '..' segments.`
      );
    }

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

    // Safeguard 2 — files-scanned floor. Production app has hundreds of
    // .tsx files. If glob returned 0 or near-0 (e.g. cwd wrong, glob
    // pattern broken), the loop would run too few iterations to surface
    // real violations. >50 catches both vacuous-pass and severely
    // truncated scans.
    expect(files.length).toBeGreaterThan(50);

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
              kind: 'navItems',
            });
          }

          // Safeguard 3 — JSXSpreadAttribute detection. <MeepleCard {...props} />
          // is statically opaque: we cannot tell whether `navItems` is
          // part of the spread payload without executing the code. Surface
          // each spread as a separate hit kind so a human auditor can
          // verify the spread doesn't carry `navItems` before Task 8
          // cleanup (which deletes the navItems channel entirely).
          for (const attr of path.node.attributes) {
            if (attr.type !== 'JSXSpreadAttribute') continue;
            hits.push({
              file: file.replace(root + '/', '').replace(root + '\\', ''),
              line: attr.loc?.start.line ?? -1,
              componentName: name.name,
              kind: 'spread',
            });
          }
        },
      });
    }

    if (hits.length > 0) {
      const navItemsHits = hits.filter(h => h.kind === 'navItems');
      const spreadHits = hits.filter(h => h.kind === 'spread');
      const lines: string[] = [];
      if (navItemsHits.length > 0) {
        lines.push(
          `Found ${navItemsHits.length} unmigrated call-site(s) using navItems= on Meeple* cards:`
        );
        for (const h of navItemsHits) {
          lines.push(`  ${h.file}:${h.line}  <${h.componentName} navItems=...>`);
        }
      }
      if (spreadHits.length > 0) {
        if (lines.length > 0) lines.push('');
        lines.push(
          `Found ${spreadHits.length} spread-attribute call-site(s) on Meeple* cards (statically opaque — must be audited manually):`
        );
        for (const h of spreadHits) {
          lines.push(`  ${h.file}:${h.line}  <${h.componentName} {...} />`);
        }
      }
      lines.push('');
      lines.push(
        'Migrate to connections= per spec docs/superpowers/specs/2026-04-24-connectionchip-step-2-call-site-migration-design.md.'
      );
      throw new Error(lines.join('\n'));
    }

    expect(hits).toEqual([]);
  });
});
