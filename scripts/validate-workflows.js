#!/usr/bin/env node
/**
 * Workflow Validator - TDD barrier against common CI failures
 *
 * Validates that all GitHub workflows follow required patterns:
 *
 * 1. PNPM Cache Validation:
 *    - Workflows using `cache: 'pnpm'` in setup-node have pnpm installed BEFORE cache
 *    - Valid: pnpm/action-setup BEFORE setup-node, or pnpm-version parameter
 *
 * 2. E2E Build Validation (Issue #2XXX):
 *    - Workflows running `test:e2e` or `playwright test` MUST have `pnpm build` first
 *    - playwright.config.ts uses `next start` in CI which requires production build
 *    - Without build: "Could not find a production build in the '.next' directory"
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

/**
 * Validates E2E workflows have pnpm build before test:e2e
 * Issue #2XXX: playwright.config.ts uses 'next start' in CI mode,
 * which requires a production build (.next directory with BUILD_ID)
 */
function validateE2EBuild(filePath) {
  const content = fs.readFileSync(filePath, 'utf8');
  const fileName = path.basename(filePath);
  const errors = [];
  const lines = content.split('\n');

  // First pass: identify jobs that have pnpm build (build jobs)
  const buildJobs = new Set();
  let scanJob = null;
  for (const line of lines) {
    const jobMatch = line.match(/^  (\w[\w-]*):\s*$/);
    if (jobMatch && content.substring(0, content.indexOf(line)).includes('jobs:')) {
      scanJob = jobMatch[1];
    }
    if (scanJob && (line.match(/pnpm build\s*$/) || line.match(/pnpm build[^-]/))) {
      if (!line.includes('storybook') && !line.includes('build-storybook')) {
        buildJobs.add(scanJob);
      }
    }
  }

  // Track state per job
  let currentJob = null;
  let hasPnpmBuild = false;
  let hasArtifactDownload = false;  // Track if job downloads build artifact
  let jobNeeds = [];  // Track job dependencies
  let inSteps = false;
  let buildStepIndex = -1;
  let e2eStepIndex = -1;
  let stepIndex = 0;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];
    const lineNum = i + 1;

    // Detect job start
    const jobMatch = line.match(/^  (\w[\w-]*):\s*$/);
    if (jobMatch && content.substring(0, content.indexOf(line)).includes('jobs:')) {
      // Check previous job for E2E without build
      // Skip if job depends on a build job and downloads artifacts (composite workflow pattern)
      const hasBuildDependency = jobNeeds.some(dep => buildJobs.has(dep));
      if (currentJob && e2eStepIndex > 0 && buildStepIndex < 0 && !(hasBuildDependency && hasArtifactDownload)) {
        errors.push(
          `${fileName} (job: ${currentJob}): E2E tests found but no "pnpm build" step. ` +
          `playwright.config.ts uses "next start" in CI which requires production build.`
        );
      }

      currentJob = jobMatch[1];
      hasPnpmBuild = false;
      hasArtifactDownload = false;
      jobNeeds = [];
      buildStepIndex = -1;
      e2eStepIndex = -1;
      inSteps = false;
      stepIndex = 0;
    }

    // Detect needs: dependency (can be array or single value)
    const needsMatch = line.match(/^\s{4}needs:\s*(.+)$/);
    if (needsMatch && currentJob) {
      const needsValue = needsMatch[1].trim();
      // Parse array format: [job1, job2] or single value
      if (needsValue.startsWith('[')) {
        const deps = needsValue.slice(1, -1).split(',').map(d => d.trim().replace(/['"]/g, ''));
        jobNeeds = deps;
      } else {
        jobNeeds = [needsValue.replace(/['"]/g, '')];
      }
    }

    // Detect artifact download (indicates build may be from another job)
    if (line.includes('actions/download-artifact')) {
      hasArtifactDownload = true;
    }

    // Detect steps section
    if (line.match(/^\s{4}steps:\s*$/)) {
      inSteps = true;
      stepIndex = 0;
    }

    // Detect new step
    if (inSteps && line.match(/^\s{6}-\s+(name|uses):/)) {
      stepIndex++;
    }

    // Check for pnpm build (standalone, not storybook or other builds)
    // Valid patterns: "pnpm build" at end of line or followed by other flags
    if (line.match(/pnpm build\s*$/) || line.match(/pnpm build[^-]/)) {
      // Exclude storybook builds
      if (!line.includes('storybook') && !line.includes('build-storybook')) {
        hasPnpmBuild = true;
        buildStepIndex = stepIndex;
      }
    }

    // Check for E2E test commands
    // Patterns: test:e2e, playwright test, npx playwright (but NOT 'playwright install')
    // Exclude browser installation commands which are not test runs
    const isPlaywrightInstall = line.match(/playwright\s+install/) || line.match(/npx\s+playwright\s+install/);
    if (!isPlaywrightInstall && (
        line.match(/test:e2e/) ||
        line.match(/playwright\s+test/) ||
        (line.match(/npx\s+playwright/) && !line.includes('install')))) {
      e2eStepIndex = stepIndex;

      // Check if build happened before this step
      // Skip if job depends on a build job and downloads artifacts (composite workflow pattern)
      const hasBuildDependency = jobNeeds.some(dep => buildJobs.has(dep));
      const isCompositeWorkflow = hasBuildDependency && hasArtifactDownload;

      if (!isCompositeWorkflow && (!hasPnpmBuild || (buildStepIndex > 0 && buildStepIndex > e2eStepIndex))) {
        errors.push(
          `${fileName}:${lineNum} (job: ${currentJob || 'unknown'}): ` +
          `E2E tests run WITHOUT prior "pnpm build". ` +
          `Add "pnpm build" step BEFORE E2E tests. ` +
          `(playwright.config.ts uses "next start" in CI mode)`
        );
      }
    }
  }

  // Check last job
  // Skip if job depends on a build job and downloads artifacts (composite workflow pattern)
  const hasBuildDependency = jobNeeds.some(dep => buildJobs.has(dep));
  if (currentJob && e2eStepIndex > 0 && !hasPnpmBuild && !(hasBuildDependency && hasArtifactDownload)) {
    errors.push(
      `${fileName} (job: ${currentJob}): E2E tests found but no "pnpm build" step. ` +
      `playwright.config.ts uses "next start" in CI which requires production build.`
    );
  }

  return errors;
}

function main() {
  console.log('Validating GitHub Workflows...\n');

  if (!fs.existsSync(WORKFLOWS_DIR)) {
    console.error(`Workflows directory not found: ${WORKFLOWS_DIR}`);
    process.exit(1);
  }

  const files = fs.readdirSync(WORKFLOWS_DIR)
    .filter(f => f.endsWith('.yml') || f.endsWith('.yaml'));

  let allErrors = [];

  console.log('1. Validating pnpm cache patterns...');
  for (const file of files) {
    const filePath = path.join(WORKFLOWS_DIR, file);
    const errors = validateWorkflow(filePath);
    allErrors = allErrors.concat(errors);
  }

  console.log('2. Validating E2E build requirements...');
  for (const file of files) {
    const filePath = path.join(WORKFLOWS_DIR, file);
    const errors = validateE2EBuild(filePath);
    allErrors = allErrors.concat(errors);
  }

  if (allErrors.length > 0) {
    console.error('\nWORKFLOW VALIDATION FAILED:\n');
    allErrors.forEach(err => console.error(`  - ${err}\n`));
    process.exit(1);
  }

  console.log(`\n✅ All ${files.length} workflows validated successfully.`);
  process.exit(0);
}

main();
