import { describe, expect, it } from 'vitest';
import { readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { sync as globSync } from 'glob';

// This test lives at apps/web/src/__tests__/ → up 2 to apps/web, up 4 to repo root.
const APPS_WEB = resolve(__dirname, '..', '..');
const REPO_ROOT = resolve(APPS_WEB, '..', '..');
const DOC_PATH = resolve(REPO_ROOT, 'docs/for-developers/frontend/card-decision-table.md');

// Exported components (function|const).
const EXPORT_RE = /export\s+(?:default\s+)?(?:function|const)\s+([A-Z][A-Za-z0-9]*)/g;
// Any exported symbol incl. types (used for the no-dangling existence check so a
// type mentioned in prose — e.g. `MeepleCardProps` — is not flagged).
const EXPORT_ANY_RE =
  /export\s+(?:default\s+)?(?:function|const|interface|type|class)\s+([A-Z][A-Za-z0-9]*)/g;
// `<MeepleCard` followed by whitespace, `/`, or `>` — excludes MeepleCardGame,
// MeepleCardAttributionFooter, MeepleCardSkeleton, etc.
const RENDERS_MEEPLE_CARD_RE = /<MeepleCard[\s/>]/;

function readDocNames(): Set<string> {
  const doc = readFileSync(DOC_PATH, 'utf8');
  return new Set([...doc.matchAll(/`([A-Z][A-Za-z0-9]*)`/g)].map(m => m[1]));
}

describe('card decision-table living documentation (#2858)', () => {
  it('every <MeepleCard>-rendering production file has an exported component in the decision-table', () => {
    const files = globSync('src/{app,components}/**/*.tsx', {
      cwd: APPS_WEB,
      ignore: [
        '**/__tests__/**',
        'src/app/(public)/dev/**',
        'src/components/**/dev/**',
        '**/showcase/**',
        // The dispatcher package renders the variants, not <MeepleCard>; excluded
        // for clarity so only true adapters are considered.
        'src/components/ui/data-display/meeple-card/**',
      ],
      absolute: true,
    });

    // Safeguard: a broken glob/cwd would make this pass vacuously.
    expect(files.length).toBeGreaterThan(50);

    const docNames = readDocNames();
    const undocumented: string[] = [];

    for (const file of files) {
      const src = readFileSync(file, 'utf8');
      if (!RENDERS_MEEPLE_CARD_RE.test(src)) continue;
      // Among files that render <MeepleCard>, a reusable ADAPTER exports a
      // component whose name contains "Card". Files that render <MeepleCard>
      // inline but export no Card-named component (pages/containers such as
      // DashboardClient, EntityListView, sessions/games pages) are inline
      // consumers, not reusable adapters — skip them.
      const cardExports = [...src.matchAll(EXPORT_RE)]
        .map(m => m[1])
        .filter(name => /Card/.test(name));
      if (cardExports.length === 0) continue;
      if (!cardExports.some(name => docNames.has(name))) {
        undocumented.push(`${file.replace(/\\/g, '/')} (card exports: ${cardExports.join(', ')})`);
      }
    }

    expect(
      undocumented,
      `These files render <MeepleCard> but no exported component is listed in ` +
        `docs/for-developers/frontend/card-decision-table.md. Add a row for each:\n` +
        undocumented.join('\n')
    ).toEqual([]);
  });

  it('every adapter named in the decision-table exists as an export', () => {
    const componentFiles = globSync('src/components/**/*.tsx', {
      cwd: APPS_WEB,
      ignore: ['**/__tests__/**'],
      absolute: true,
    });
    const allExports = new Set<string>();
    for (const file of componentFiles) {
      const src = readFileSync(file, 'utf8');
      for (const m of src.matchAll(EXPORT_ANY_RE)) allExports.add(m[1]);
    }

    const docNames = readDocNames();
    const dangling = [...docNames].filter(
      name =>
        /^(?:Meeple[A-Za-z0-9]*|[A-Za-z0-9]*ExtraMeepleCard)$/.test(name) && !allExports.has(name)
    );

    expect(
      dangling,
      `The decision-table names these adapters but no matching export was found in ` +
        `src/components/**: ${dangling.join(', ')}`
    ).toEqual([]);
  });
});
