#!/usr/bin/env node
/**
 * Bootstrap helper: copia le PNG baseline dei mockup (Phase 0, PR #575) nella
 * snapshot dir di una spec `e2e/visual-migrated/<slug>.spec.ts` rinominandole
 * con il pattern che Playwright si aspetta (`<arg>-<project>-<platform>.png`).
 *
 * Issue #583 (Wave A.1 FAQ pilot) · umbrella #578 V2 Migration Phase 1.
 *
 * Filename mapping:
 *   FROM: apps/web/e2e/visual-mockups/baseline.spec.ts-snapshots/
 *           <slug>-mockup-baseline-{desktop,mobile}-linux.png
 *   TO:   apps/web/e2e/visual-migrated/<slug>.spec.ts-snapshots/
 *           <slug>-visual-migrated-{desktop,mobile}-linux.png
 *
 * Quando una spec usa screenshot multipli (es. FAQ ha `<slug>.png` per default
 * state E `<slug>-search-password.png` per search-results state) lo script
 * copia il **medesimo** mockup PNG su tutti i nomi attesi: e' compito della
 * migration Task #16 garantire che ciascuno stato matchi visivamente quel
 * baseline (es. la search bar in default state e in search-results state ha
 * lo stesso layout dell'header/hero del mockup; il diff e' atteso solo dentro
 * la lista FAQ filtrata, mascherata via `data-dynamic`).
 *
 * Quando un nuovo nome screenshot si discosta troppo dal mockup (es. modale
 * aperto), il bootstrap NON deve essere usato per quel nome: lascia la PNG
 * mancante, esegui `pnpm test:visual:migrated:update` una volta dopo il
 * green della migration per generare il baseline da zero.
 *
 * Usage:
 *   node scripts/sync-migrated-baselines.cjs <slug> [--secondary <name>...]
 *
 * Esempi:
 *   node scripts/sync-migrated-baselines.cjs sp3-faq-enhanced \\
 *     --secondary sp3-faq-enhanced-search-password
 */
const fs = require('node:fs');
const path = require('node:path');

const PROJECTS = ['desktop', 'mobile'];
const PLATFORM = 'linux';

function parseArgs(argv) {
  const args = argv.slice(2);
  if (args.length === 0 || args[0].startsWith('--')) {
    console.error(
      'Usage: node scripts/sync-migrated-baselines.cjs <slug> [--secondary <name>...]',
    );
    process.exit(1);
  }
  const slug = args[0];
  const secondary = [];
  for (let i = 1; i < args.length; i += 1) {
    if (args[i] === '--secondary' && i + 1 < args.length) {
      secondary.push(args[i + 1]);
      i += 1;
    }
  }
  return { slug, secondary };
}

function copyOne({ slug, screenshotName, project }) {
  const sourceDir = path.resolve(
    __dirname,
    '../e2e/visual-mockups/baseline.spec.ts-snapshots',
  );
  const targetDir = path.resolve(
    __dirname,
    `../e2e/visual-migrated/${slug}.spec.ts-snapshots`,
  );
  const sourceFile = path.join(
    sourceDir,
    `${slug}-mockup-baseline-${project}-${PLATFORM}.png`,
  );
  const targetFile = path.join(
    targetDir,
    `${screenshotName}-visual-migrated-${project}-${PLATFORM}.png`,
  );

  if (!fs.existsSync(sourceFile)) {
    console.error(`  [skip] missing source: ${path.relative(process.cwd(), sourceFile)}`);
    return false;
  }

  fs.mkdirSync(targetDir, { recursive: true });
  fs.copyFileSync(sourceFile, targetFile);
  console.log(`  [ok]   ${path.relative(process.cwd(), targetFile)}`);
  return true;
}

function main() {
  const { slug, secondary } = parseArgs(process.argv);
  const screenshotNames = [slug, ...secondary];

  console.log(
    `Bootstrapping visual-migrated baselines for slug "${slug}"` +
      (secondary.length ? ` (+ ${secondary.length} secondary)` : ''),
  );

  let copied = 0;
  let skipped = 0;
  for (const screenshotName of screenshotNames) {
    for (const project of PROJECTS) {
      const ok = copyOne({ slug, screenshotName, project });
      if (ok) copied += 1;
      else skipped += 1;
    }
  }

  console.log(`\nDone. Copied ${copied}, skipped ${skipped}.`);
  if (copied === 0) {
    process.exit(2);
  }
}

main();
