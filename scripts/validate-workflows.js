#!/usr/bin/env node
/**
 * Validate GitHub Actions workflow files for common issues.
 *
 * Checks:
 * 1. pnpm cache patterns are correctly configured
 * 2. Action versions are pinned
 * 3. Required permissions are specified
 *
 * Issue #3102: Created to fix missing script error in auto-validate.yml
 */

const fs = require('fs');
const path = require('path');
const yaml = require('js-yaml');

const WORKFLOWS_DIR = path.join(__dirname, '..', '.github', 'workflows');
const ACTIONS_DIR = path.join(__dirname, '..', '.github', 'actions');

// Track validation results
let hasErrors = false;
const errors = [];
const warnings = [];

function log(type, file, message) {
  const prefix = type === 'error' ? '❌' : type === 'warn' ? '⚠️' : '✅';
  console.log(`${prefix} [${path.basename(file)}] ${message}`);
  if (type === 'error') {
    hasErrors = true;
    errors.push({ file, message });
  } else if (type === 'warn') {
    warnings.push({ file, message });
  }
}

function validatePnpmCache(workflow, filePath) {
  const content = JSON.stringify(workflow);

  // Check for pnpm/action-setup usage
  if (content.includes('pnpm/action-setup')) {
    // Verify it's used correctly
    const hasRunInstall = content.includes('run_install');
    if (!hasRunInstall) {
      log('warn', filePath, 'pnpm/action-setup found but run_install not specified');
    }
  }

  // Check for proper cache configuration with actions/cache
  if (content.includes('actions/cache')) {
    // Look for pnpm store path
    if (content.includes('pnpm') && !content.includes('pnpm-store')) {
      log('warn', filePath, 'Using actions/cache with pnpm but pnpm-store path may not be configured');
    }
  }
}

function validateActionVersions(workflow, filePath) {
  const content = JSON.stringify(workflow);

  // Common actions that should be pinned to specific versions
  const actionsToCheck = [
    'actions/checkout',
    'actions/setup-node',
    'actions/cache',
    'pnpm/action-setup'
  ];

  actionsToCheck.forEach(action => {
    // Check if action is used without version (just the name)
    const regex = new RegExp(`uses:\\s*['"]?${action.replace('/', '\\/')}['"]?\\s*$`, 'gm');
    if (regex.test(content)) {
      log('warn', filePath, `Action ${action} may not have a pinned version`);
    }
  });
}

function validatePermissions(workflow, filePath) {
  // Check if permissions are specified at workflow level
  if (!workflow.permissions) {
    // Only warn for workflows that push, deploy, or modify things
    const content = JSON.stringify(workflow);
    if (content.includes('github.token') || content.includes('secrets.GITHUB_TOKEN')) {
      log('warn', filePath, 'Workflow uses GITHUB_TOKEN but does not specify permissions');
    }
  }
}

function validateWorkflow(filePath) {
  try {
    const content = fs.readFileSync(filePath, 'utf8');
    const workflow = yaml.load(content);

    if (!workflow) {
      log('error', filePath, 'Failed to parse workflow YAML');
      return;
    }

    // Run validations
    validatePnpmCache(workflow, filePath);
    validateActionVersions(workflow, filePath);
    validatePermissions(workflow, filePath);

    log('info', filePath, 'Validation complete');
  } catch (err) {
    // js-yaml might not be installed - just do basic checks
    if (err.code === 'MODULE_NOT_FOUND') {
      console.log('⚠️  js-yaml not installed, running basic validation only');
      const content = fs.readFileSync(filePath, 'utf8');

      // Basic YAML syntax check
      if (!content.includes('name:')) {
        log('warn', filePath, 'Workflow may be missing name field');
      }
      if (!content.includes('on:')) {
        log('warn', filePath, 'Workflow may be missing trigger (on:) field');
      }
      if (!content.includes('jobs:')) {
        log('error', filePath, 'Workflow is missing jobs field');
      }

      log('info', filePath, 'Basic validation complete');
    } else {
      log('error', filePath, `Parse error: ${err.message}`);
    }
  }
}

function validateAction(dirPath) {
  const actionFile = path.join(dirPath, 'action.yml');
  const actionFileYaml = path.join(dirPath, 'action.yaml');

  const filePath = fs.existsSync(actionFile) ? actionFile :
                   fs.existsSync(actionFileYaml) ? actionFileYaml : null;

  if (!filePath) {
    // Not an error - directory might contain other files
    return;
  }

  try {
    const content = fs.readFileSync(filePath, 'utf8');

    // Basic validation
    if (!content.includes('name:')) {
      log('error', filePath, 'Action is missing name field');
    }
    if (!content.includes('description:')) {
      log('warn', filePath, 'Action is missing description field');
    }
    if (!content.includes('runs:')) {
      log('error', filePath, 'Action is missing runs field');
    }

    log('info', filePath, 'Action validation complete');
  } catch (err) {
    log('error', filePath, `Read error: ${err.message}`);
  }
}

// Main execution
console.log('🔍 Validating GitHub Actions workflows...\n');

// Validate workflows
if (fs.existsSync(WORKFLOWS_DIR)) {
  const workflowFiles = fs.readdirSync(WORKFLOWS_DIR)
    .filter(f => f.endsWith('.yml') || f.endsWith('.yaml'));

  workflowFiles.forEach(file => {
    validateWorkflow(path.join(WORKFLOWS_DIR, file));
  });
} else {
  console.log('⚠️  Workflows directory not found');
}

// Validate custom actions
if (fs.existsSync(ACTIONS_DIR)) {
  const actionDirs = fs.readdirSync(ACTIONS_DIR, { withFileTypes: true })
    .filter(d => d.isDirectory())
    .map(d => d.name);

  actionDirs.forEach(dir => {
    validateAction(path.join(ACTIONS_DIR, dir));
  });
}

// Summary
console.log('\n📊 Validation Summary:');
console.log(`   Errors: ${errors.length}`);
console.log(`   Warnings: ${warnings.length}`);

if (hasErrors) {
  console.log('\n❌ Validation failed with errors');
  process.exit(1);
} else {
  console.log('\n✅ Validation passed');
  process.exit(0);
}
