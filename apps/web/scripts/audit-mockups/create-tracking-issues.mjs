#!/usr/bin/env node
/**
 * create-tracking-issues.mjs — post designer sign-off: create GH issues + update fidelity refs.
 *
 * Reads audits/tracking-issues-drafts.md, parses each Draft N section, runs
 * `gh issue create` for each, updates the corresponding admin-mockups/design_files/
 * <name>.fidelity.json with the new issue number in `obsolete_tracking_issue`
 * (replacing the 'PENDING' sentinel).
 *
 * Rollback on partial failure: closes already-created issues (GitHub doesn't allow
 * delete; close is the strongest reversal available — spec accepts this trade-off).
 *
 * Usage:
 *   node create-tracking-issues.mjs --drafts audits/tracking-issues-drafts.md
 *
 * Refs:
 *   Spec: docs/superpowers/specs/2026-06-10-ds-17-phase-b-mockup-audit-design.md
 *   Umbrella: #2063
 *   Sub-issue: #2127
 */

import { readFileSync, writeFileSync, existsSync } from 'node:fs';
import { dirname, resolve, basename } from 'node:path';
import { fileURLToPath } from 'node:url';
import { execFileSync } from 'node:child_process';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');

/**
 * @param {string} draftsContent
 * @returns {Array<{mockup_path: string, title: string, body: string}>}
 */
export function parseDrafts(draftsContent) {
  const sections = draftsContent.split(/^## Draft \d+: /m).slice(1);
  const result = [];
  for (const section of sections) {
    const lines = section.split('\n');
    const mockup_path = lines[0].trim();
    const titleMatch = section.match(/\*\*Title\*\*: `([^`]+)`/);
    // Code-reviewer HIGH fix: terminate body at the LAST `---` line in the section
    // (greedy backtrack). Previous regex `\n---` (non-greedy) would silently truncate
    // any body containing a Markdown horizontal rule. Since `split(/^## Draft N: /m)`
    // strips draft headers, each section ends with one closing `---` separator —
    // greedy match correctly finds it even if the body has embedded `---` lines.
    const bodyMatch = section.match(/\*\*Body\*\*:\s*\n\s*\n([\s\S]+)\n---\s*\n*$/);
    if (titleMatch && bodyMatch) {
      result.push({
        mockup_path,
        title: titleMatch[1],
        body: bodyMatch[1].trim(),
      });
    }
  }
  return result;
}

/**
 * @param {string} fidelityPath
 * @param {number} issueNumber
 */
export function updateFidelityForIssue(fidelityPath, issueNumber) {
  const content = JSON.parse(readFileSync(fidelityPath, 'utf-8'));
  content.acceptance.obsolete_tracking_issue = `#${issueNumber}`;
  writeFileSync(fidelityPath, JSON.stringify(content, null, 2) + '\n');
}

/**
 * Code-reviewer Finding 4: extracted from main() for unit testability.
 *
 * @param {Array<{mockup_path: string, title: string, body: string}>} drafts
 * @param {{
 *   createFn: (title: string, body: string) => number,
 *   closeFn: (issueNumber: number) => void,
 *   updateFn: (mockup_path: string, issueNumber: number) => void
 * }} ops
 */
export async function runBatch(drafts, ops) {
  const created = [];
  for (const draft of drafts) {
    try {
      const issueNumber = ops.createFn(draft.title, draft.body);
      created.push({ draft, issueNumber });
      ops.updateFn(draft.mockup_path, issueNumber);
    } catch (err) {
      for (const c of created) {
        try {
          ops.closeFn(c.issueNumber);
        } catch (_) {
          // Best-effort: a failed close during rollback is non-fatal.
        }
      }
      throw err;
    }
  }
  return created;
}

/**
 * @param {string} title
 * @param {string} body
 * @returns {number}
 */
function createGithubIssue(title, body) {
  // execFileSync (no shell) avoids command injection if title/body contain shell metacharacters.
  // Body is passed via stdin (`--body-file -`) so it bypasses argv altogether.
  const result = execFileSync('gh', ['issue', 'create', '--title', title, '--body-file', '-'], {
    input: body,
    encoding: 'utf-8',
  });
  const match = result.match(/\/issues\/(\d+)$/m);
  if (!match) {
    throw new Error(`Failed to parse issue number from gh output: ${result}`);
  }
  return parseInt(match[1], 10);
}

function fidelityPathFor(mockup_path) {
  const sourceName = basename(mockup_path);
  const fidelityName = sourceName.replace(/\.(html|jsx|js|css)$/, '.fidelity.json');
  return resolve(REPO_ROOT, 'admin-mockups/design_files', fidelityName);
}

function main() {
  const argv = process.argv.slice(2);
  const draftsIdx = argv.indexOf('--drafts');
  if (draftsIdx === -1) {
    console.error('Usage: create-tracking-issues.mjs --drafts <path>');
    process.exit(2);
  }
  const draftsPath = resolve(REPO_ROOT, argv[draftsIdx + 1]);
  const drafts = parseDrafts(readFileSync(draftsPath, 'utf-8'));

  if (drafts.length === 0) {
    console.log('No drafts to process. Exiting.');
    return;
  }

  console.log(`Processing ${drafts.length} drafts...`);

  const createFn = (title, body) => {
    console.log(`Creating issue: ${title}`);
    return createGithubIssue(title, body);
  };
  const closeFn = (issueNumber) => {
    // execFileSync with argument array prevents shell injection on issueNumber + comment string.
    execFileSync('gh', [
      'issue',
      'close',
      String(issueNumber),
      '--comment',
      'Rollback: Phase B batch failed.',
    ]);
  };
  const updateFn = (mockup_path, issueNumber) => {
    const fidelityPath = fidelityPathFor(mockup_path);
    if (existsSync(fidelityPath)) {
      updateFidelityForIssue(fidelityPath, issueNumber);
      console.log(`  → #${issueNumber} + updated ${basename(fidelityPath)}`);
    } else {
      console.warn(`  → #${issueNumber} but fidelity file missing: ${fidelityPath}`);
    }
  };

  runBatch(drafts, { createFn, closeFn, updateFn })
    .then((created) => {
      console.log(
        `Done. Created ${created.length} issues, updated ${created.length} fidelity files.`
      );
    })
    .catch((err) => {
      console.error('FAILED batch:', err.message);
      process.exit(1);
    });
}

if (fileURLToPath(import.meta.url) === resolve(process.argv[1])) {
  main();
}
