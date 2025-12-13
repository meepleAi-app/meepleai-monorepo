#!/usr/bin/env node
/**
 * Workflow Validator - TDD barrier against pnpm cache errors
 *
 * Validates that all GitHub workflows using `cache: 'pnpm'` in setup-node
 * have pnpm properly installed BEFORE the cache operation.
 *
 * Valid patterns:
 * 1. pnpm/action-setup BEFORE setup-node with cache: pnpm
 * 2. setup-node with pnpm-version parameter (handles pnpm internally)
 * 3. Uses composite action like setup-frontend that handles this correctly
 */

const fs = require('fs');
const path = require('path');

const WORKFLOWS_DIR = path.join(__dirname, '..', '.github', 'workflows');

function validateWorkflow(filePath) {
  const content = fs.readFileSync(filePath, 'utf8');
  const fileName = path.basename(filePath);
  const errors = [];
  const lines = content.split('\n');

  // Track state per job
  let currentJob = null;
  let pnpmInstalled = false;
  let pnpmVersionUsed = false;
  let inSteps = false;
  let stepIndex = 0;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const lineNum = i + 1;

    // Detect job start (top-level key under jobs:)
    const jobMatch = line.match(/^  (\w[\w-]*):\s*$/);
    if (jobMatch && content.substring(0, content.indexOf(line)).includes('jobs:')) {
      currentJob = jobMatch[1];
      pnpmInstalled = false;
      pnpmVersionUsed = false;
      inSteps = false;
      stepIndex = 0;
    }

    // Detect steps section
    if (line.match(/^\s{4}steps:\s*$/)) {
      inSteps = true;
      stepIndex = 0;
    }

    // Detect new step (- name: or - uses:)
    if (inSteps && line.match(/^\s{6}-\s+(name|uses):/)) {
      stepIndex++;
    }

    // Check for pnpm/action-setup
    if (line.includes('pnpm/action-setup')) {
      pnpmInstalled = true;
    }

    // Check for composite actions that handle pnpm correctly
    if (line.includes('./.github/actions/setup-frontend') ||
        line.includes('./.github/actions/setup-playwright')) {
      pnpmInstalled = true;
    }

    // Check for pnpm-version in setup-node
    if (line.includes('pnpm-version:')) {
      pnpmVersionUsed = true;
    }

    // Check for problematic pattern: cache: 'pnpm' or cache: pnpm
    const cacheMatch = line.match(/cache:\s*['"]?pnpm['"]?/);
    if (cacheMatch) {
      // Look backwards for setup-node in this step
      let isSetupNode = false;
      for (let j = i; j >= Math.max(0, i - 5); j--) {
        if (lines[j].includes('actions/setup-node')) {
          isSetupNode = true;
          break;
        }
        // Stop if we hit a different step
        if (j < i && lines[j].match(/^\s{6}-\s+(name|uses):/)) {
          break;
        }
      }

      if (isSetupNode && !pnpmInstalled && !pnpmVersionUsed) {
        errors.push(
          `${fileName}:${lineNum} (job: ${currentJob || 'unknown'}, step: ${stepIndex}): ` +
          `"cache: 'pnpm'" used in setup-node WITHOUT prior pnpm installation. ` +
          `Add "pnpm/action-setup@v4" BEFORE this step, or use "pnpm-version" parameter.`
        );
      }
    }

    // Check for corepack after setup-node with cache (informational)
    if (line.includes('corepack enable') || line.includes('corepack prepare')) {
      // This is informational - the main check above catches the real issue
    }
  }

  return errors;
}

function main() {
  console.log('Validating GitHub Workflows for pnpm cache issues...\n');

  if (!fs.existsSync(WORKFLOWS_DIR)) {
    console.error(`Workflows directory not found: ${WORKFLOWS_DIR}`);
    process.exit(1);
  }

  const files = fs.readdirSync(WORKFLOWS_DIR)
    .filter(f => f.endsWith('.yml') || f.endsWith('.yaml'));

  let allErrors = [];

  for (const file of files) {
    const filePath = path.join(WORKFLOWS_DIR, file);
    const errors = validateWorkflow(filePath);
    allErrors = allErrors.concat(errors);
  }

  if (allErrors.length > 0) {
    console.error('WORKFLOW VALIDATION FAILED:\n');
    allErrors.forEach(err => console.error(`  - ${err}\n`));
    process.exit(1);
  }

  console.log(`All ${files.length} workflows validated successfully.`);
  process.exit(0);
}

main();
