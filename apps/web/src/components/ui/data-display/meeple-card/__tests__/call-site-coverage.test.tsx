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
 * ─── What throws vs what is allowlisted ──────────────────────────────────
 *
 * `navItems=` hits ALWAYS throw. Zero hits is the Step 2 exit criterion;
 * the cleanup commit (Task 8) physically deletes the `navItems` prop
 * from `MeepleCardProps`, which is safe only once every call-site has
 * migrated to `connections=`.
 *
 * Spread attributes (`<MeepleCard {...foo} />`) are statically opaque:
 * we cannot tell whether the spread payload carries `navItems` without
 * executing the code. In Task 7 (2026-04-24) each of the 8 pre-existing
 * spread sites was manually audited:
 *
 *   - `useMetadataToMeepleCard` return type: no navItems field
 *   - `items`-mapped object literals (games/sessions/dashboard pages):
 *     plain literals with explicit keys, no navItems assignment
 *   - `entity-list-view`: spreads `cardProps` returned by
 *     `renderItem: (item: T) => Omit<MeepleCardProps, 'entity' | 'variant'>`.
 *     `Omit` structurally still permits `navItems`, but no consumer sets
 *     it. Once Task 8 deletes `navItems` from `MeepleCardProps`, carriage
 *     becomes mechanically impossible.
 *
 * We therefore allowlist the 8 known spread files below. Unknown spreads
 * on `Meeple*Card` components still throw — this catches regressions
 * where a new contributor adds a spread site without auditing it.
 *
 * Keyed by file only (not line) so edits that shift line numbers don't
 * break the gate.
 *
 * ─── For future contributors ────────────────────────────────────────────
 *
 * If you add a new spread of `MeepleCard`-bound props on a `Meeple*Card`
 * component, this gate will fail. To unblock:
 *   1. Manually verify the spread source's type/shape does NOT include
 *      `navItems` (and, post-Task-8, that the source doesn't carry any
 *      field that has since been renamed).
 *   2. Add your file path (normalized with forward slashes, relative to
 *      `apps/web/`) to `SPREAD_ALLOWLIST` below.
 *   3. Document your audit reason in the commit message.
 */
const SPREAD_ALLOWLIST: ReadonlySet<string> = new Set([
  // useMetadataToMeepleCard → no navItems field in return type
  'src/app/admin/(dashboard)/shared-games/import/steps/Step3PreviewConfirm.tsx',
  'src/app/admin/(dashboard)/shared-games/import/steps/Step2MetadataReview.tsx',
  // plain object literal maps — explicit keys, no navItems
  'src/app/(authenticated)/sessions/page.tsx',
  'src/app/(authenticated)/games/page.tsx',
  // MeepleCardProps[] items built inline; use manaPips, no navItems
  'src/app/(authenticated)/dashboard/DashboardClient.tsx',
  // renderItem returns Omit<MeepleCardProps, ...> — structurally allows
  // navItems but no consumer sets it; Task 8 deletion makes it impossible
  'src/components/ui/data-display/entity-list-view/entity-list-view.tsx',
]);

function normalize(p: string): string {
  return p.replace(/\\/g, '/');
}

describe('Step 2 — call-site migration coverage (Gate 1)', () => {
  it('zero production files use navItems= on Meeple* card components', () => {
    const root = resolve(__dirname, '..', '..', '..', '..', '..', '..'); // -> apps/web

    // Safeguard 1 — path-drift sanity assertion. If the __dirname-relative
    // path is wrong (e.g. package moved, or '..' segment count off), the
    // glob below would return nothing and the loop would run 0 iterations,
    // making the test pass vacuously. Verify root really points to apps/web
    // by reading package.json and asserting the package name — checking
    // existence alone would silently pass if root resolved to the monorepo
    // root or a sibling Next.js package which also have a package.json.
    const pkgJsonPath = resolve(root, 'package.json');
    if (!existsSync(pkgJsonPath)) {
      throw new Error(
        `Gate 1 path drift: resolved root ${root} does not contain package.json. The __dirname-relative path is wrong; fix the number of '..' segments.`
      );
    }
    const pkgName = (JSON.parse(readFileSync(pkgJsonPath, 'utf8')) as { name?: string }).name;
    if (pkgName !== '@meepleai/web') {
      throw new Error(
        `Gate 1 path drift: resolved root ${root} is package "${pkgName ?? '(unnamed)'}", expected "@meepleai/web". The __dirname-relative path lands in the wrong package.`
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
              file: normalize(file.replace(root + '/', '').replace(root + '\\', '')),
              line: attr.loc?.start.line ?? -1,
              componentName: name.name,
              kind: 'navItems',
            });
          }

          // JSXSpreadAttribute detection. <MeepleCard {...props} /> is
          // statically opaque — we cannot tell whether `navItems` is part
          // of the spread payload without executing the code. Surface
          // each spread as a separate hit; the allowlist below declares
          // which ones have been audited as safe.
          for (const attr of path.node.attributes) {
            if (attr.type !== 'JSXSpreadAttribute') continue;
            hits.push({
              file: normalize(file.replace(root + '/', '').replace(root + '\\', '')),
              line: attr.loc?.start.line ?? -1,
              componentName: name.name,
              kind: 'spread',
            });
          }
        },
      });
    }

    const navItemsHits = hits.filter(h => h.kind === 'navItems');
    const spreadHits = hits.filter(h => h.kind === 'spread');
    const unaudited = spreadHits.filter(h => !SPREAD_ALLOWLIST.has(h.file));

    const lines: string[] = [];

    if (navItemsHits.length > 0) {
      lines.push(
        `Found ${navItemsHits.length} unmigrated call-site(s) using navItems= on Meeple* cards:`
      );
      for (const h of navItemsHits) {
        lines.push(`  ${h.file}:${h.line}  <${h.componentName} navItems=...>`);
      }
      lines.push('');
      lines.push(
        'Migrate to connections= per spec docs/superpowers/specs/2026-04-24-connectionchip-step-2-call-site-migration-design.md.'
      );
    }

    if (unaudited.length > 0) {
      if (lines.length > 0) lines.push('');
      lines.push(
        `Found ${unaudited.length} un-audited spread-attribute call-site(s) on Meeple* cards:`
      );
      for (const h of unaudited) {
        lines.push(`  ${h.file}:${h.line}  <${h.componentName} {...} />`);
      }
      lines.push('');
      lines.push(
        'Spreads are statically opaque. Audit each new site to confirm no navItems is carried, then add the file path to SPREAD_ALLOWLIST in this test.'
      );
    }

    if (lines.length > 0) {
      throw new Error(lines.join('\n'));
    }

    // Informative assertions: navItems hits must be 0; spread hits are
    // allowed only if every one is in the allowlist.
    expect(navItemsHits).toEqual([]);
    expect(unaudited).toEqual([]);
  });
});
