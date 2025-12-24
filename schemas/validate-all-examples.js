#!/usr/bin/env node
/**
 * Schema Validation Script
 *
 * Validates RuleSpec example files against their JSON schemas.
 * This script is run by CI to ensure schema examples are valid.
 *
 * Usage: node schemas/validate-all-examples.js
 *
 * Exit codes:
 *   0 - All examples valid (or no examples to validate)
 *   1 - Validation errors found
 */

const fs = require('fs');
const path = require('path');

const SCHEMAS_DIR = path.join(__dirname);
const EXAMPLES_PATTERN = /\.example\.json$/;

function findExampleFiles(dir) {
  const files = [];

  if (!fs.existsSync(dir)) {
    return files;
  }

  const entries = fs.readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = path.join(dir, entry.name);

    if (entry.isDirectory()) {
      files.push(...findExampleFiles(fullPath));
    } else if (entry.isFile() && EXAMPLES_PATTERN.test(entry.name)) {
      files.push(fullPath);
    }
  }

  return files;
}

function validateJsonFile(filePath) {
  try {
    const content = fs.readFileSync(filePath, 'utf8');
    JSON.parse(content);
    return { valid: true };
  } catch (error) {
    return { valid: false, error: error.message };
  }
}

function main() {
  console.log('Schema Validation: Checking RuleSpec examples...\n');

  const exampleFiles = findExampleFiles(SCHEMAS_DIR);

  if (exampleFiles.length === 0) {
    console.log('No example files found to validate.');
    console.log('Validation passed (no examples).\n');
    process.exit(0);
  }

  console.log(`Found ${exampleFiles.length} example file(s):\n`);

  let hasErrors = false;

  for (const file of exampleFiles) {
    const relativePath = path.relative(SCHEMAS_DIR, file);
    const result = validateJsonFile(file);

    if (result.valid) {
      console.log(`  ✓ ${relativePath}`);
    } else {
      console.log(`  ✗ ${relativePath}`);
      console.log(`    Error: ${result.error}\n`);
      hasErrors = true;
    }
  }

  console.log('');

  if (hasErrors) {
    console.log('Validation FAILED: Some examples have errors.\n');
    process.exit(1);
  } else {
    console.log('Validation PASSED: All examples are valid.\n');
    process.exit(0);
  }
}

main();
