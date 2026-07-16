#!/usr/bin/env node
/**
 * validate-fidelity.mjs — validate mockup.fidelity.{json,yml} files (DS-17-4)
 *
 * Validates acceptance criteria files that define what "pixel-perfect" means
 * for each mockup migration. Schema enforced via zod (already in apps/web).
 *
 * Phase 1 mode: JSON-only (no yaml devDep required, ship-it Wave 1 friendly).
 * Phase 2 (DS-17-5 Storybook setup) will add `yaml` devDep + enable .yml parsing.
 * For now, write fidelity files as .json. Template .yml exists as reference only.
 *
 * Usage:
 *   node validate-fidelity.mjs <file.json|file.yml>
 *   node validate-fidelity.mjs --all                  # scan repo for *.fidelity.{json,yml}
 *   node validate-fidelity.mjs --all --max-baseline N # tolerate N pre-existing structural failures
 *   node validate-fidelity.mjs --schema               # print zod schema as JSON Schema
 *
 * Designer signoff (ADR-077, Option D): design_intent="current" fidelity files
 * MUST carry a non-empty acceptance.designer_approved_by (the "self-waiver P250"
 * token is accepted for solo-maintainer context). Signoff failures are strict
 * and never baselined. Structural (schema/cross-ref) failures are whitelist-
 * incremental via --max-baseline (mirrors lint:tokens:mockups / lint:bgg-mockups).
 *
 * Exit codes:
 *   0  all validations passed (structural failures within --max-baseline; signoff OK)
 *   1  signoff missing on a 'current' surface, OR structural failures exceed baseline
 *   2  invocation error (missing arg, file not found)
 *
 * Refs:
 *   Spec:    docs/superpowers/specs/2026-06-09-mockup-to-app-drift-spec-panel-review.md
 *   Issue:   #2072 (DS-17-4)
 *   Umbrella: #2063
 *   Plan:    docs/superpowers/plans/2026-06-09-ds-17-phase-1-implementation-plan.md
 */

import { readFileSync, existsSync, statSync } from 'node:fs';
import { resolve, dirname, relative } from 'node:path';
import { fileURLToPath, pathToFileURL } from 'node:url';
import { z } from 'zod';
import { globSync } from 'glob';

const __dirname = dirname(fileURLToPath(import.meta.url));
const REPO_ROOT = resolve(__dirname, '..', '..', '..', '..');

// ---------------------------------------------------------------------------
// Schema (zod) — single source of truth for mockup.fidelity contract
// ---------------------------------------------------------------------------

const StateName = z.enum(['default', 'empty', 'loading', 'error', 'sse', 'offline', 'quota-soft', 'quota-hard']);

export const FidelitySchema = z.object({
  mockup: z.object({
    source: z.string().min(1).describe('Path to canonical mockup file (e.g. admin-mockups/design_files/sp4-dashboard.html)'),
    states: z.array(StateName).min(1).describe('Which states this mockup covers'),
  }),
  acceptance: z.object({
    visual_diff_max_px: z.number().int().nonnegative().default(5),
    color_delta_e_max: z.number().nonnegative().default(3),
    tokens_used: z.enum(['canonical_only', 'mixed_legacy_allowed']).default('canonical_only'),
    legacy_token_names_forbidden: z.boolean().default(true),
    states_covered: z.array(StateName).min(1),
    a11y_axe: z.enum(['AA', 'AAA']).default('AA'),
    a11y_violations_max: z.number().int().nonnegative().default(0),
    responsive_breakpoints: z.array(z.number().int().positive()).min(1).default([375, 768, 1024, 1440]),
    designer_approved_by: z.string().default(''),
    designer_approved_on: z.string().regex(/^\d{4}-\d{2}-\d{2}$|^$/, 'ISO date YYYY-MM-DD or empty').default(''),
    story_path: z.string().default(''),
    fixtures_path: z.string().default(''),

    // DEC-P3-1+2 (2026-06-10): design intent classification
    // 'deferred' added 2026-07-15 (#2063 ratchet): a current-intent mockup whose
    // story authoring is deliberately deferred to a tracked umbrella (e.g. per-game
    // session mockups → Phase C-3 #2234). Excluded from lint:storybook-states like
    // obsolete, but semantically distinct (will be built later, not retired).
    design_intent: z
      .enum(['current', 'forward-refactor', 'forward-refactor-obsolete', 'deferred'])
      .default('current')
      .describe(
        'Mockup intent vs codebase corrente. forward-refactor-obsolete → skip story migration; deferred → skip until a tracked umbrella builds it. Both require a tracking issue.'
      ),

    // DEC-P3-4 (2026-06-10): viewport opt-in (default desktop)
    viewports: z
      .array(z.enum(['desktop', 'mobile']))
      .min(1)
      .default(['desktop'])
      .describe('Snapshot viewports per story.'),

    // Required when design_intent = forward-refactor-obsolete
    // DS-17 Phase B (#2127): accept 'PENDING' sentinel for pre-signoff stubs;
    // create-tracking-issues.mjs replaces with real #N ref post designer approval.
    obsolete_tracking_issue: z
      .string()
      .regex(/^#\d+$|^$|^PENDING$/, 'Format: #1234, empty, or PENDING (pre-signoff sentinel)')
      .default('')
      .describe('GitHub issue # tracking mockup rewrite OR component rollback (or PENDING).'),
  }),
});

// ---------------------------------------------------------------------------
// Parsers
// ---------------------------------------------------------------------------

async function parseFile(filePath) {
  const raw = readFileSync(filePath, 'utf-8');
  const ext = filePath.toLowerCase();

  if (ext.endsWith('.json')) {
    return JSON.parse(raw);
  }

  if (ext.endsWith('.yml') || ext.endsWith('.yaml')) {
    throw new Error(
      `YAML parsing not enabled in Phase 1 (no "yaml" devDep). Convert ${filePath} to .json, ` +
        `or wait for Phase 2 (DS-17-5) which adds yaml devDep. Template .yml is reference-only.`
    );
  }

  throw new Error(`Unsupported file extension. Expected .json. Got: ${filePath}`);
}

// ---------------------------------------------------------------------------
// Cross-reference checks
// ---------------------------------------------------------------------------

function crossReferenceCheck(fidelity, fidelityFilePath) {
  const errors = [];

  // mockup.source must exist
  const sourcePath = resolve(REPO_ROOT, fidelity.mockup.source);
  if (!existsSync(sourcePath)) {
    errors.push(`mockup.source file does not exist: ${fidelity.mockup.source} (resolved: ${sourcePath})`);
  }

  // mockup.states must equal acceptance.states_covered (set equality)
  const sourceSet = new Set(fidelity.mockup.states);
  const coveredSet = new Set(fidelity.acceptance.states_covered);
  if (sourceSet.size !== coveredSet.size || [...sourceSet].some((s) => !coveredSet.has(s))) {
    errors.push(
      `mockup.states and acceptance.states_covered must contain the same elements. ` +
        `mockup.states=[${[...sourceSet].sort().join(',')}], states_covered=[${[...coveredSet].sort().join(',')}]`
    );
  }

  // story_path: if non-empty, must exist (Phase 2+ requirement)
  if (fidelity.acceptance.story_path) {
    const storyPath = resolve(REPO_ROOT, fidelity.acceptance.story_path);
    if (!existsSync(storyPath)) {
      errors.push(`acceptance.story_path file does not exist: ${fidelity.acceptance.story_path}`);
    }
  }

  // fixtures_path: same logic
  if (fidelity.acceptance.fixtures_path) {
    const fixturesPath = resolve(REPO_ROOT, fidelity.acceptance.fixtures_path);
    if (!existsSync(fixturesPath)) {
      errors.push(`acceptance.fixtures_path file does not exist: ${fidelity.acceptance.fixtures_path}`);
    }
  }

  // DEC-P3-2: design_intent = 'forward-refactor-obsolete' requires obsolete_tracking_issue
  // DS-17 Phase B (#2127): accept 'PENDING' sentinel during the window between fidelity
  // stub generation and designer sign-off + tracking issue creation.
  // create-tracking-issues.mjs replaces PENDING with the real #NNNN ref post-signoff.
  // #2063 ratchet (2026-07-15): 'deferred' carries the same tracking-issue requirement
  // (the umbrella that will build the story, e.g. #2234), reusing obsolete_tracking_issue.
  const tracking = fidelity.acceptance.obsolete_tracking_issue;
  const intent = fidelity.acceptance.design_intent;
  if ((intent === 'forward-refactor-obsolete' || intent === 'deferred') && !tracking) {
    errors.push(
      `acceptance.design_intent='${intent}' requires acceptance.obsolete_tracking_issue (use 'PENDING' placeholder pre-signoff or '#NNNN' post-signoff). ` +
        `Open/link a GitHub issue tracking the mockup rewrite, component rollback, or deferred build (DEC-P3-2).`
    );
  }

  return errors;
}

// ---------------------------------------------------------------------------
// Designer signoff attestation (ADR-077, Option D)
// ---------------------------------------------------------------------------

// A fidelity file whose design_intent is "current" represents a live design
// surface and MUST carry a non-empty acceptance.designer_approved_by. The
// solo-maintainer P250 self-waiver (e.g.
// "user@meepleAi (self-waiver P250, single-person team)") is an explicitly
// accepted approval token — it documents the developer-is-designer exception
// rather than bypassing a real review. forward-refactor / -obsolete / deferred
// intents are speculative or not-yet-built surfaces and are NOT approval-gated
// (advisory only). See docs/for-claude/architecture/adr/adr-077-designer-signoff-ci-gate.md
export const P250_WAIVER_TOKEN = 'self-waiver P250';

/**
 * Returns approval errors for a parsed fidelity object. Empty array = signoff OK.
 * Only `design_intent: "current"` surfaces are gated. Any non-empty
 * designer_approved_by (including the P250 self-waiver) satisfies the gate.
 */
export function checkApprovalStatus(fidelity) {
  const errors = [];
  const acc = (fidelity && fidelity.acceptance) || {};
  // Mirror the schema default: an absent design_intent is "current".
  const intent = acc.design_intent ?? 'current';
  if (intent !== 'current') {
    return errors; // advisory intents are not approval-gated
  }
  const approver = String(acc.designer_approved_by ?? '').trim();
  if (!approver) {
    errors.push(
      `acceptance.design_intent='current' requires a non-empty acceptance.designer_approved_by. ` +
        `Fill in the designer signoff or use the '${P250_WAIVER_TOKEN}' token for solo-maintainer ` +
        `context (e.g. "you@meepleAi (self-waiver P250, single-person team)"). See ADR-077.`
    );
  }
  return errors;
}

/**
 * Pure gate-verdict decision over an array of validate() results.
 *
 * Two failure classes with different severities (ADR-077 Option D):
 *  - approval failures: `design_intent: current` missing signoff. ALWAYS hard —
 *    never baselined (the attestation is a deliberate 5-second ceremony).
 *  - structural failures: schema / cross-reference errors. Whitelist-incremental
 *    via `maxBaseline` (mirrors lint:tokens:mockups / lint:bgg-mockups) so
 *    pre-existing drift is tolerated while NEW drift fails.
 */
export function computeGateVerdict(results, maxBaseline = 0) {
  const structural = results.filter((r) => !r.ok);
  const approval = results.filter((r) => ((r.approvalErrors && r.approvalErrors.length) || 0) > 0);
  const structuralFailCount = structural.length;
  const approvalFailCount = approval.length;
  const overBaseline = structuralFailCount > maxBaseline;
  return {
    pass: approvalFailCount === 0 && !overBaseline,
    structural,
    approval,
    structuralFailCount,
    approvalFailCount,
    maxBaseline,
    overBaseline,
    baselinedCount: Math.min(structuralFailCount, maxBaseline),
  };
}

// ---------------------------------------------------------------------------
// Main validator
// ---------------------------------------------------------------------------

export async function validate(filePath) {
  if (!existsSync(filePath)) {
    return { ok: false, file: filePath, errors: [`File not found: ${filePath}`], approvalErrors: [] };
  }
  if (statSync(filePath).isDirectory()) {
    return { ok: false, file: filePath, errors: [`Path is a directory, not a file: ${filePath}`], approvalErrors: [] };
  }

  let raw;
  try {
    raw = await parseFile(filePath);
  } catch (err) {
    return { ok: false, file: filePath, errors: [`Parse error: ${err.message}`], approvalErrors: [] };
  }

  const parsed = FidelitySchema.safeParse(raw);
  if (!parsed.success) {
    const errs = parsed.error.issues.map((i) => `${i.path.join('.')}: ${i.message}`);
    // Signoff is STRICT and must not be reclassifiable as baseline-tolerated
    // structural drift when the file ALSO fails schema validation. Compute it
    // from the raw (pre-zod) input so a design_intent:"current" file with an
    // empty designer_approved_by is still flagged as a signoff failure even when
    // another field is malformed (e.g. an invalid state name). checkApprovalStatus
    // defaults an absent design_intent to "current", matching the zod schema.
    return { ok: false, file: filePath, errors: errs, approvalErrors: checkApprovalStatus(raw) };
  }

  // Designer signoff attestation is computed independently of structural
  // validity so the CLI gate can treat it as a strict (never-baselined) class.
  const approvalErrors = checkApprovalStatus(parsed.data);

  const crossErrors = crossReferenceCheck(parsed.data, filePath);
  if (crossErrors.length > 0) {
    return { ok: false, file: filePath, errors: crossErrors, approvalErrors, data: parsed.data };
  }

  return { ok: true, file: filePath, data: parsed.data, errors: [], approvalErrors };
}

// ---------------------------------------------------------------------------
// CLI entry
// ---------------------------------------------------------------------------

/**
 * Parse `--max-baseline N` / `--max-baseline=N`. Default 0 (strict).
 * Caps the number of tolerated PRE-EXISTING structural (schema/cross-ref)
 * failures; does NOT relax the strict designer-signoff attestation.
 */
function parseMaxBaseline(args) {
  for (let i = 0; i < args.length; i++) {
    const a = args[i];
    if (a === '--max-baseline') {
      const v = Number(args[i + 1]);
      return Number.isFinite(v) && v >= 0 ? Math.floor(v) : 0;
    }
    if (a.startsWith('--max-baseline=')) {
      const v = Number(a.slice('--max-baseline='.length));
      return Number.isFinite(v) && v >= 0 ? Math.floor(v) : 0;
    }
  }
  return 0;
}

async function main() {
  const args = process.argv.slice(2);
  const maxBaseline = parseMaxBaseline(args);

  if (args.includes('--schema')) {
    // Print zod schema as a navigable shape (simplified — full JSON Schema conversion is out of scope)
    console.log(JSON.stringify({ schema: 'FidelitySchema', see: 'validate-fidelity.mjs source for zod definition' }, null, 2));
    process.exit(0);
  }

  let files = [];

  if (args.includes('--all')) {
    files = globSync('**/*.fidelity.{json,yml,yaml}', {
      cwd: REPO_ROOT,
      // templates/** holds illustrative fidelity examples (ADR-077 surface
      // definition: doc templates are not gated design surfaces). The canonical
      // copy-me template (docs/.../templates/examples/sp4-library-desktop.fidelity.json,
      // referenced in CLAUDE.md) intentionally ships with a blank
      // designer_approved_by + placeholder mockup.source — excluded here so the
      // signoff + cross-reference gate only scans real mockup surfaces.
      ignore: ['**/node_modules/**', '**/.next/**', '**/.claude/**', '**/dist/**', '**/templates/**'],
      absolute: true,
    });
    if (files.length === 0) {
      console.log('No *.fidelity.{json,yml,yaml} files found.');
      process.exit(0);
    }
  } else if (args.length === 0 || args[0].startsWith('--')) {
    console.error('Usage: validate-fidelity.mjs <file> | --all [--max-baseline N] | --schema');
    process.exit(2);
  } else {
    files = [resolve(args[0])];
  }

  const results = [];
  for (const f of files) {
    const result = await validate(f);
    result.rel = relative(REPO_ROOT, f).split('\\').join('/');
    results.push(result);
  }

  for (const r of results) {
    const hasApproval = ((r.approvalErrors && r.approvalErrors.length) || 0) > 0;
    if (r.ok && !hasApproval) {
      console.log(`PASS     ${r.rel}`);
      continue;
    }
    if (r.ok && hasApproval) {
      // Structurally valid but missing the designer signoff (hard failure).
      console.error(`SIGNOFF  ${r.rel}`);
      for (const err of r.approvalErrors) console.error(`         - ${err}`);
      continue;
    }
    // Structural failure (schema / cross-reference); may also lack signoff.
    console.error(`FAIL     ${r.rel}`);
    for (const err of r.errors) console.error(`         - ${err}`);
    if (hasApproval) for (const err of r.approvalErrors) console.error(`         - [signoff] ${err}`);
  }

  const verdict = computeGateVerdict(results, maxBaseline);

  console.error('');
  console.error(
    `Fidelity gate: ${results.length - verdict.structuralFailCount} passed structural checks, ` +
      `${verdict.structuralFailCount} structural failure(s) (baseline ${maxBaseline}), ` +
      `${verdict.approvalFailCount} signoff failure(s).`
  );
  if (verdict.approvalFailCount > 0) {
    console.error(
      `ERROR: ${verdict.approvalFailCount} 'current' fidelity file(s) missing designer_approved_by. ` +
        `Signoff is strict and never baselined (ADR-077).`
    );
  }
  if (verdict.structuralFailCount > 0 && !verdict.overBaseline) {
    console.error(
      `NOTE: ${verdict.structuralFailCount} pre-existing structural drift file(s) tolerated ` +
        `(<= baseline ${maxBaseline}). Reconcile the mockup.source references to ratchet the baseline down.`
    );
  }
  if (verdict.overBaseline) {
    console.error(
      `ERROR: structural failures (${verdict.structuralFailCount}) exceed baseline (${maxBaseline}). ` +
        `A NEW fidelity file with a broken schema/cross-reference was introduced — fix it or raise the baseline deliberately.`
    );
  }

  process.exit(verdict.pass ? 0 : 1);
}

// CLI guard — only run main() when invoked directly (not when imported as module)
// Use pathToFileURL for cross-platform Windows compatibility (file:/// triple-slash)
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    console.error(`Unexpected error: ${err.message}`);
    process.exit(2);
  });
}
