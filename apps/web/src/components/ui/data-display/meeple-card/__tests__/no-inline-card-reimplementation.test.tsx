import { describe, it, expect } from 'vitest';
import { existsSync, readFileSync } from 'node:fs';
import { resolve } from 'node:path';
import { sync as globSync } from 'glob';
import { parse } from '@babel/parser';
import _traverse from '@babel/traverse';

// CJS/ESM interop: @babel/traverse's default export is the function itself.
const traverse = (_traverse as unknown as { default?: typeof _traverse }).default ?? _traverse;

const CARD_NAME_RE = /^[A-Z][A-Za-z0-9]*Card([A-Z][A-Za-z0-9]*)?$/;
const STAR_RE = /[★☆]/;

/**
 * Grandfathered hand-rolled star-cards (pre-C4). Follow-up: convert to MeepleCard
 * adapters like MeepleCardGame in C1 (#2858) — tracked separately, out of C4 scope.
 * Paths are relative to apps/web/, normalized with forward slashes.
 */
const STAR_CARD_ALLOWLIST: ReadonlySet<string> = new Set([
  'src/components/features/hub/HubGameCard.tsx',
  'src/components/features/hub/HubToolkitCard.tsx',
]);

function normalize(p: string): string {
  return p.replace(/\\/g, '/');
}

interface Violation {
  component: string;
  line: number;
}

/**
 * A file violates C4 when it declares a `*Card` component, renders an inline
 * star glyph (in code, not a comment), and never composes <MeepleCard>.
 * Returns the first violation or null. Unparseable files return null.
 */
export function detectInlineStarCard(source: string, _relPath: string): Violation | null {
  let ast;
  try {
    ast = parse(source, { sourceType: 'module', plugins: ['jsx', 'typescript'] });
  } catch {
    return null;
  }

  let card: { name: string; line: number } | null = null;
  let starLine = -1;
  let rendersMeepleCard = false;

  const markCard = (name: string, line: number) => {
    if (!card) card = { name, line };
  };
  const markStar = (line: number) => {
    if (starLine < 0) starLine = line;
  };

  traverse(ast, {
    FunctionDeclaration(path) {
      const id = path.node.id;
      if (id && CARD_NAME_RE.test(id.name)) markCard(id.name, id.loc?.start.line ?? -1);
    },
    VariableDeclarator(path) {
      const id = path.node.id;
      const init = path.node.init;
      if (
        id.type === 'Identifier' &&
        CARD_NAME_RE.test(id.name) &&
        (init?.type === 'ArrowFunctionExpression' || init?.type === 'FunctionExpression')
      ) {
        markCard(id.name, id.loc?.start.line ?? -1);
      }
    },
    JSXOpeningElement(path) {
      const name = path.node.name;
      if (name.type === 'JSXIdentifier' && name.name === 'MeepleCard') rendersMeepleCard = true;
    },
    StringLiteral(path) {
      if (STAR_RE.test(path.node.value)) markStar(path.node.loc?.start.line ?? -1);
    },
    JSXText(path) {
      if (STAR_RE.test(path.node.value)) markStar(path.node.loc?.start.line ?? -1);
    },
    TemplateLiteral(path) {
      if (path.node.quasis.some(q => STAR_RE.test(q.value.raw ?? ''))) {
        markStar(path.node.loc?.start.line ?? -1);
      }
    },
  });

  if (card && starLine !== -1 && !rendersMeepleCard) {
    return { component: card.name, line: starLine };
  }
  return null;
}

describe('C4 — no inline card re-implementation (#2861)', () => {
  it('self-check: flags a *Card with inline star glyphs and no <MeepleCard>', () => {
    const src = `
      export function RogueGameCard() {
        return <div><span>{'★'}</span><span>☆</span></div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/RogueGameCard.tsx')).not.toBeNull();
  });

  it('self-check: does NOT flag a *Card that composes <MeepleCard>', () => {
    const src = `
      export function OkGameCard() {
        return <MeepleCard entity="game" title="x">{'★'}</MeepleCard>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/OkGameCard.tsx')).toBeNull();
  });

  it('self-check: does NOT flag a *Card with no star glyph', () => {
    const src = `
      export function PlainCard() {
        return <div>hello</div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/PlainCard.tsx')).toBeNull();
  });

  it('self-check: does NOT flag a non-*Card component with star glyphs', () => {
    const src = `
      export function StarRating() {
        return <div><span>★</span></div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/StarRating.tsx')).toBeNull();
  });

  it('self-check: does NOT flag a star glyph that lives only in a comment', () => {
    const src = `
      // renders a ★ rating elsewhere
      export function CommentedCard() {
        return <div>plain</div>;
      }
    `;
    expect(detectInlineStarCard(src, 'x/CommentedCard.tsx')).toBeNull();
  });

  it('production scan: no un-allowlisted *Card renders stars inline without <MeepleCard>', () => {
    // apps/web root
    const root = resolve(__dirname, '..', '..', '..', '..', '..', '..');

    // Path-drift safeguard: verify root is @meepleai/web.
    const pkgJsonPath = resolve(root, 'package.json');
    if (!existsSync(pkgJsonPath)) {
      throw new Error(`C4 path drift: ${root} has no package.json; fix the '..' count.`);
    }
    const pkgName = (JSON.parse(readFileSync(pkgJsonPath, 'utf8')) as { name?: string }).name;
    if (pkgName !== '@meepleai/web') {
      throw new Error(
        `C4 path drift: root ${root} is "${pkgName ?? '(unnamed)'}", expected "@meepleai/web".`
      );
    }

    const files = globSync('src/{app,components}/**/*.tsx', {
      cwd: root,
      ignore: [
        '**/__tests__/**',
        'src/app/(public)/dev/**',
        'src/components/**/dev/**',
        '**/showcase/**',
        'src/components/ui/data-display/meeple-card/**',
        'src/components/ui/data-display/extra-meeple-card/**',
      ],
      absolute: true,
    });

    // Files-scanned floor: catch a broken glob/cwd (vacuous pass).
    expect(files.length).toBeGreaterThan(50);

    const offenders: string[] = [];
    for (const file of files) {
      const n = normalize(file);
      const rel = n.slice(n.indexOf('/src/') + 1); // -> src/...
      if (STAR_CARD_ALLOWLIST.has(rel)) continue;
      const v = detectInlineStarCard(readFileSync(file, 'utf8'), rel);
      if (v)
        offenders.push(
          `${rel}:${v.line}  <${v.component}> renders inline ★/☆ without <MeepleCard>`
        );
    }

    expect(
      offenders,
      offenders.length
        ? `These *Card components hand-roll a star rating instead of composing <MeepleCard>.\n` +
            `Compose <MeepleCard> (adapter), or use the shared <Stars> (@/components/ui/feedback/Stars)\n` +
            `for a non-entity rating. If genuinely intentional, add the path to STAR_CARD_ALLOWLIST\n` +
            `in this test with a reason.\n` +
            offenders.join('\n')
        : ''
    ).toEqual([]);
  });
});
