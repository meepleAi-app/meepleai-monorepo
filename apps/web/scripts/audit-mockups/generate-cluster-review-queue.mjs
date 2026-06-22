#!/usr/bin/env node
/**
 * generate-cluster-review-queue.mjs — emit designer review queue per Phase C cluster.
 *
 * Reads cluster-scoped audit JSON (subset of Phase B output annotated with
 * Phase C shipped stories) and emits markdown queue listing shipped stories,
 * obsolete deferred (post-Phase-B-tracking), and pair disagreements.
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-11-ds-17-phase-c-pilot-migration-design.md
 *   Umbrella: #2063
 *   Pattern: Phase B generate-deliverables.mjs (queue section)
 */

import { readFileSync, writeFileSync, mkdirSync } from 'node:fs';
import { dirname, resolve, join } from 'node:path';
import { fileURLToPath } from 'node:url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');

/**
 * @param {{
 *   auditPath: string,
 *   outDir: string
 * }} opts
 */
export function generateClusterReviewQueue(opts) {
  const { auditPath, outDir } = opts;
  const data = JSON.parse(readFileSync(auditPath, 'utf-8'));

  // Code-reviewer Finding 6: validate input contract to convert silent
  // malformed-output failures into loud build failures.
  if (!data.clusterId || !Array.isArray(data.classifications)) {
    throw new Error(
      `Invalid cluster audit JSON at ${auditPath}: missing clusterId or classifications array. ` +
        `Expected shape: { clusterId, classifications[], stories[] }. Got keys: ${Object.keys(data).join(', ')}`
    );
  }
  const clusterId = data.clusterId;

  mkdirSync(outDir, { recursive: true });

  const shippedStories = data.stories || [];
  const obsolete = data.classifications.filter(
    (c) => c.design_intent === 'forward-refactor-obsolete'
  );
  const pairDisagreements = data.classifications.filter((c) => c.pair_disagreement);

  const queue = `# Designer Review Queue — DS-17 Phase C-1 cluster: ${clusterId}

**Generated**: ${new Date().toISOString().split('T')[0]}
**Source**: Phase C-1 cluster migration (post Phase B audit)
**Cluster**: ${clusterId}

## Shipped stories (require designer review)

${
  shippedStories.length
    ? shippedStories
        .map(
          (s) =>
            `- [ ] \`${s.mockup_stem}\` (${s.frame_count} frames)\n  - story_path: \`${s.story_path}\`\n  - fixtures_path: \`${s.fixtures_path}\``
        )
        .join('\n')
    : '_None._'
}

## Obsolete deferred (Phase B tracking)

${
  obsolete.length
    ? obsolete
        .map(
          (o) =>
            `- \`${o.mockup_path}\` — DEFERRED post-Phase-B-tracking-${o.obsolete_tracking_issue_ref || '#TBD'}: ${o.reasoning}`
        )
        .join('\n')
    : '_None._'
}

## Pair disagreements (require arbitration)

${
  pairDisagreements.length
    ? pairDisagreements
        .map((p) => `- \`${p.mockup_path}\` — ${p.reasoning}`)
        .join('\n')
    : '_None._'
}

## How to approve

Comment on PR with magic phrase:

\`\`\`
DESIGNER APPROVED: <ISO date> <your-name>
\`\`\`

(Same protocol as Phase B; sign-off optional per user decision.)
`;

  writeFileSync(join(outDir, `c1-${clusterId}-review-queue.md`), queue);
  console.log(`Designer queue written: c1-${clusterId}-review-queue.md`);
  console.log(`  - Shipped stories: ${shippedStories.length}`);
  console.log(`  - Obsolete deferred: ${obsolete.length}`);
  console.log(`  - Pair disagreements: ${pairDisagreements.length}`);
}

function main() {
  const argv = process.argv.slice(2);
  const auditIdx = argv.indexOf('--audit');
  const outIdx = argv.indexOf('--out');
  if (auditIdx === -1 || outIdx === -1) {
    console.error('Usage: generate-cluster-review-queue.mjs --audit <path> --out <dir>');
    process.exit(2);
  }
  generateClusterReviewQueue({
    auditPath: resolve(REPO_ROOT, argv[auditIdx + 1]),
    outDir: resolve(REPO_ROOT, argv[outIdx + 1]),
  });
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main();
}
