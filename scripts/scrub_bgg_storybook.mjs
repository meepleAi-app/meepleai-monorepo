#!/usr/bin/env node
/**
 * scrub_bgg_storybook.mjs — issue #2123 Storybook fixtures cleanup.
 *
 * Replaces every `https://cf.geekdo-images.com/…` and
 * `https://*.boardgamegeek.com/…` URL inside `apps/web/src/**\/*.stor{ies,y}.tsx`
 * with a deterministic placehold.co URL, preserving the visual outcome (a
 * neutral-toned image with the game title overlay) so existing Storybook
 * snapshots still render meaningfully.
 *
 * This is a one-shot operational cleanup. After running, the
 * `apps/web/**\/*.stories.tsx` files no longer carry BGG host literals and
 * the `eslint.config.mjs` path override for Storybook fixtures can eventually
 * be tightened. For now the override remains in place; the cleanup just
 * removes the violation from existing files.
 *
 * Usage:
 *   node scripts/scrub_bgg_storybook.mjs
 *   pnpm lint:bgg   # should still pass; this only cleans storybook,
 *                    which the script already exempts from lint:bgg.
 *
 * Refs:
 *   Issue : https://github.com/meepleAi-app/meepleai-monorepo/issues/2123
 *   Spec  : docs/superpowers/specs/2026-06-10-issue-2123-bgg-tos-compliance.md §9 F2
 */

import { readdirSync, readFileSync, statSync, writeFileSync } from 'node:fs';
import { resolve, dirname } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const WEB_SRC = resolve(__dirname, '..', 'apps', 'web', 'src');

const STORY_PATTERN = /\.(?:stories|story)\.(?:ts|tsx|js|jsx)$/;
const BGG_URL_PATTERN = /(?:https?:\/\/)(?:cf\.geekdo-images\.com|geekdo-images\.com|images\.geekdo\.com|[a-z0-9-]+\.boardgamegeek\.com|boardgamegeek\.com)\/[^\s"'`)\\]+/gi;

/**
 * Deterministic placeholder URL. Encodes a short readable hash so two
 * different URLs don't collapse to the same image — this preserves story
 * snapshot stability.
 */
function placeholderFor(url) {
  // Cheap stable hash. Used only as a visual differentiator, not security.
  let h = 5381;
  for (let i = 0; i < url.length; i++) {
    h = (h << 5) + h + url.charCodeAt(i);
    h |= 0;
  }
  const tag = Math.abs(h).toString(16).slice(0, 6);
  return `https://placehold.co/600x400/4f46e5/ffffff?text=cover-${tag}`;
}

function walk(root) {
  const stats = statSync(root, { throwIfNoEntry: false });
  if (!stats) return [];
  if (stats.isFile()) return STORY_PATTERN.test(root) ? [root] : [];
  const out = [];
  for (const entry of readdirSync(root, { withFileTypes: true })) {
    const full = resolve(root, entry.name);
    if (entry.isDirectory()) out.push(...walk(full));
    else if (entry.isFile() && STORY_PATTERN.test(entry.name)) out.push(full);
  }
  return out;
}

let totalFiles = 0;
let totalReplacements = 0;
const stories = walk(WEB_SRC);

for (const path of stories) {
  const before = readFileSync(path, 'utf8');
  let replacements = 0;
  const after = before.replace(BGG_URL_PATTERN, match => {
    replacements++;
    return placeholderFor(match);
  });
  if (replacements === 0) continue;
  writeFileSync(path, after, 'utf8');
  totalFiles++;
  totalReplacements += replacements;
  console.log(`✓ ${path.replace(WEB_SRC, 'apps/web/src')}: ${replacements} URL(s) replaced`);
}

console.log(`\n${totalFiles} file(s), ${totalReplacements} URL(s) replaced with deterministic placehold.co fixtures.`);
