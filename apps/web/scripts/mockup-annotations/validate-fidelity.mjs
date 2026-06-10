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
 *   node validate-fidelity.mjs --schema               # print zod schema as JSON Schema
 *
 * Exit codes:
 *   0  all validations passed
 *   1  schema mismatch or missing referenced file
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

const StateName = z.enum(['default', 'empty', 'loading', 'error', 'sse', 'offline']);

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
    design_intent: z
      .enum(['current', 'forward-refactor', 'forward-refactor-obsolete'])
      .default('current')
      .describe(
        'Mockup intent vs codebase corrente. forward-refactor-obsolete → skip story migration, tracking issue required.'
      ),

    // DEC-P3-4 (2026-06-10): viewport opt-in (default desktop)
    viewports: z
      .array(z.enum(['desktop', 'mobile']))
      .min(1)
      .default(['desktop'])
      .describe('Snapshot viewports per story.'),

    // Required when design_intent = forward-refactor-obsolete
    obsolete_tracking_issue: z
      .string()
      .regex(/^#\d+$|^$/, 'Format: #1234 or empty')
      .default('')
      .describe('GitHub issue # tracking mockup rewrite OR component rollback.'),
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
  if (
    fidelity.acceptance.design_intent === 'forward-refactor-obsolete' &&
    !fidelity.acceptance.obsolete_tracking_issue
  ) {
    errors.push(
      `acceptance.design_intent='forward-refactor-obsolete' requires acceptance.obsolete_tracking_issue (#NNNN). ` +
        `Open a GitHub issue tracking mockup rewrite OR component rollback (DEC-P3-2).`
    );
  }

  return errors;
}

// ---------------------------------------------------------------------------
// Main validator
// ---------------------------------------------------------------------------

export async function validate(filePath) {
  if (!existsSync(filePath)) {
    return { ok: false, file: filePath, errors: [`File not found: ${filePath}`] };
  }
  if (statSync(filePath).isDirectory()) {
    return { ok: false, file: filePath, errors: [`Path is a directory, not a file: ${filePath}`] };
  }

  let raw;
  try {
    raw = await parseFile(filePath);
  } catch (err) {
    return { ok: false, file: filePath, errors: [`Parse error: ${err.message}`] };
  }

  const parsed = FidelitySchema.safeParse(raw);
  if (!parsed.success) {
    const errs = parsed.error.issues.map((i) => `${i.path.join('.')}: ${i.message}`);
    return { ok: false, file: filePath, errors: errs };
  }

  const crossErrors = crossReferenceCheck(parsed.data, filePath);
  if (crossErrors.length > 0) {
    return { ok: false, file: filePath, errors: crossErrors };
  }

  return { ok: true, file: filePath, data: parsed.data };
}

// ---------------------------------------------------------------------------
// CLI entry
// ---------------------------------------------------------------------------

async function main() {
  const args = process.argv.slice(2);

  if (args.includes('--schema')) {
    // Print zod schema as a navigable shape (simplified — full JSON Schema conversion is out of scope)
    console.log(JSON.stringify({ schema: 'FidelitySchema', see: 'validate-fidelity.mjs source for zod definition' }, null, 2));
    process.exit(0);
  }

  let files = [];

  if (args.includes('--all')) {
    files = globSync('**/*.fidelity.{json,yml,yaml}', {
      cwd: REPO_ROOT,
      ignore: ['**/node_modules/**', '**/.next/**', '**/.claude/**', '**/dist/**'],
      absolute: true,
    });
    if (files.length === 0) {
      console.log('No *.fidelity.{json,yml,yaml} files found.');
      process.exit(0);
    }
  } else if (args.length === 0 || args[0].startsWith('--')) {
    console.error('Usage: validate-fidelity.mjs <file> | --all | --schema');
    process.exit(2);
  } else {
    files = [resolve(args[0])];
  }

  let allOk = true;
  for (const f of files) {
    const result = await validate(f);
    const rel = relative(REPO_ROOT, f);
    if (result.ok) {
      console.log(`PASS  ${rel}`);
    } else {
      allOk = false;
      console.error(`FAIL  ${rel}`);
      for (const err of result.errors) {
        console.error(`      - ${err}`);
      }
    }
  }

  process.exit(allOk ? 0 : 1);
}

// CLI guard — only run main() when invoked directly (not when imported as module)
// Use pathToFileURL for cross-platform Windows compatibility (file:/// triple-slash)
if (process.argv[1] && import.meta.url === pathToFileURL(process.argv[1]).href) {
  main().catch((err) => {
    console.error(`Unexpected error: ${err.message}`);
    process.exit(2);
  });
}
