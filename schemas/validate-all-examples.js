// Validation script for all RuleSpec examples
// Usage: node validate-all-examples.js

const fs = require('fs');
const path = require('path');

const schemaPath = path.join(__dirname, 'rulespec.v0.schema.json');
const examplesDir = path.join(__dirname, 'examples');

console.log('📋 RuleSpec v0 - Validate All Examples');
console.log('======================================\n');

// Load schema
let schema;
try {
  schema = JSON.parse(fs.readFileSync(schemaPath, 'utf8'));
  console.log('✅ Schema loaded:', schemaPath);
} catch (err) {
  console.error('❌ Failed to load schema:', err.message);
  process.exit(1);
}

// Find all .rulespec.json files
const exampleFiles = fs.readdirSync(examplesDir)
  .filter(file => file.endsWith('.rulespec.json'))
  .map(file => path.join(examplesDir, file));

if (exampleFiles.length === 0) {
  console.error('❌ No example files found in:', examplesDir);
  process.exit(1);
}

console.log(`📁 Found ${exampleFiles.length} example(s) to validate\n`);

// Validation function
function validateExample(filePath) {
  const fileName = path.basename(filePath);
  console.log(`\n🔍 Validating: ${fileName}`);
  console.log('─'.repeat(50));

  // Load example
  let example;
  try {
    example = JSON.parse(fs.readFileSync(filePath, 'utf8'));
  } catch (err) {
    console.error(`❌ Failed to load: ${err.message}`);
    return false;
  }

  // Required fields check
  const requiredFields = ['gameId', 'version', 'metadata', 'setup', 'phases', 'actions', 'scoring', 'endConditions'];
  let allValid = true;

  for (const field of requiredFields) {
    if (example[field] !== undefined) {
      console.log(`  ✅ ${field}`);
    } else {
      console.log(`  ❌ ${field}: MISSING`);
      allValid = false;
    }
  }

  // Version format check
  if (example.version && /^v\d+\.\d+$/.test(example.version)) {
    console.log(`  ✅ version format: ${example.version}`);
  } else {
    console.log(`  ❌ version format: invalid (expected vX.Y, got ${example.version})`);
    allValid = false;
  }

  // Metadata required fields
  if (example.metadata) {
    if (example.metadata.name) {
      console.log(`  ✅ metadata.name`);
    } else {
      console.log(`  ❌ metadata.name: MISSING`);
      allValid = false;
    }
    if (example.metadata.createdAt) {
      console.log(`  ✅ metadata.createdAt`);
    } else {
      console.log(`  ❌ metadata.createdAt: MISSING`);
      allValid = false;
    }
  }

  // Setup steps check
  if (example.setup && Array.isArray(example.setup.steps)) {
    console.log(`  ✅ setup.steps: ${example.setup.steps.length} step(s)`);
  } else {
    console.log(`  ❌ setup.steps: invalid or missing`);
    allValid = false;
  }

  // Scoring method check
  const validScoringMethods = ['points', 'elimination', 'objective', 'hybrid'];
  if (example.scoring && validScoringMethods.includes(example.scoring.method)) {
    console.log(`  ✅ scoring.method: ${example.scoring.method}`);
  } else {
    console.log(`  ❌ scoring.method: invalid (got ${example.scoring?.method})`);
    allValid = false;
  }

  // Summary
  console.log(`\n  📊 Summary:`);
  console.log(`     Game: ${example.metadata?.name || 'Unknown'}`);
  console.log(`     ID: ${example.gameId || 'Unknown'}`);
  console.log(`     Version: ${example.version || 'Unknown'}`);
  console.log(`     Players: ${example.metadata?.playerCount?.min || '?'}-${example.metadata?.playerCount?.max || '?'}`);
  console.log(`     Setup Steps: ${example.setup?.steps?.length || 0}`);
  console.log(`     Phases: ${example.phases?.length || 0}`);
  console.log(`     Actions: ${example.actions?.length || 0}`);
  console.log(`     Scoring Sources: ${example.scoring?.sources?.length || 0}`);
  console.log(`     End Conditions: ${example.endConditions?.length || 0}`);
  console.log(`     Edge Cases: ${example.edgeCases?.length || 0}`);
  console.log(`     Position Examples: ${example.positionExamples?.length || 0}`);
  console.log(`     Variants: ${example.variants?.length || 0}`);
  console.log(`     Glossary Terms: ${example.glossary?.length || 0}`);

  return allValid;
}

// Validate all examples
let results = [];
for (const exampleFile of exampleFiles) {
  const isValid = validateExample(exampleFile);
  results.push({
    file: path.basename(exampleFile),
    valid: isValid
  });
}

// Final summary
console.log('\n\n═'.repeat(50));
console.log('📊 VALIDATION SUMMARY');
console.log('═'.repeat(50));

let allPassed = true;
for (const result of results) {
  const status = result.valid ? '✅ PASS' : '❌ FAIL';
  console.log(`${status} - ${result.file}`);
  if (!result.valid) {
    allPassed = false;
  }
}

console.log('\n' + '═'.repeat(50));
if (allPassed) {
  console.log('✅ All examples passed validation!\n');
  console.log('💡 For full JSON Schema validation with ajv-cli:');
  console.log('   npm install -g ajv-cli');
  console.log('   ajv validate -s schemas/rulespec.v0.schema.json -d "schemas/examples/*.rulespec.json"\n');
  process.exit(0);
} else {
  console.log('❌ Some examples failed validation\n');
  process.exit(1);
}
