#!/usr/bin/env node

/**
 * Validate all RuleSpec example JSON files against the schema
 * This script ensures all example RuleSpec files are valid before deployment
 */

const fs = require('fs');
const path = require('path');

const SCHEMA_DIR = __dirname;
const EXAMPLES_DIR = path.join(SCHEMA_DIR, 'examples');

// Simple validation function (extend with actual schema validation if needed)
function validateRuleSpec(filePath) {
  try {
    const content = fs.readFileSync(filePath, 'utf8');
    const json = JSON.parse(content);

    // Basic required fields check
    const requiredFields = ['gameId', 'gameName', 'version'];
    const missingFields = requiredFields.filter(field => !json[field]);

    if (missingFields.length > 0) {
      console.error(`❌ ${path.basename(filePath)}: Missing fields: ${missingFields.join(', ')}`);
      return false;
    }

    console.log(`✅ ${path.basename(filePath)}: Valid`);
    return true;
  } catch (error) {
    console.error(`❌ ${path.basename(filePath)}: ${error.message}`);
    return false;
  }
}

function main() {
  console.log('🔍 Validating RuleSpec examples...\n');

  // Create examples directory if it doesn't exist
  if (!fs.existsSync(EXAMPLES_DIR)) {
    fs.mkdirSync(EXAMPLES_DIR, { recursive: true });
    console.log('⚠️  No examples directory found. Created empty directory.');
    console.log('✅ Validation passed (no examples to validate)\n');
    return;
  }

  // Find all JSON files in examples directory
  const exampleFiles = fs.readdirSync(EXAMPLES_DIR)
    .filter(file => file.endsWith('.json'))
    .map(file => path.join(EXAMPLES_DIR, file));

  if (exampleFiles.length === 0) {
    console.log('⚠️  No example files found in schemas/examples/');
    console.log('✅ Validation passed (no examples to validate)\n');
    return;
  }

  // Validate each file
  let allValid = true;
  for (const file of exampleFiles) {
    const isValid = validateRuleSpec(file);
    if (!isValid) {
      allValid = false;
    }
  }

  console.log();
  if (allValid) {
    console.log(`✅ All ${exampleFiles.length} example(s) validated successfully`);
    process.exit(0);
  } else {
    console.log('❌ Validation failed. Please fix the errors above.');
    process.exit(1);
  }
}

main();
