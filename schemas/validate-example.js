// Simple validation script for RuleSpec examples
// Usage: node validate-example.js

const fs = require('fs');
const path = require('path');

const schemaPath = path.join(__dirname, 'rulespec.v0.schema.json');
const examplePath = path.join(__dirname, 'examples', 'tic-tac-toe.rulespec.json');

console.log('📋 RuleSpec v0 Validation');
console.log('=========================\n');

// Load files
let schema, example;

try {
  schema = JSON.parse(fs.readFileSync(schemaPath, 'utf8'));
  console.log('✅ Schema loaded:', schemaPath);
} catch (err) {
  console.error('❌ Failed to load schema:', err.message);
  process.exit(1);
}

try {
  example = JSON.parse(fs.readFileSync(examplePath, 'utf8'));
  console.log('✅ Example loaded:', examplePath);
} catch (err) {
  console.error('❌ Failed to load example:', err.message);
  process.exit(1);
}

// Basic validation checks
console.log('\n🔍 Validation Checks');
console.log('-------------------\n');

const requiredFields = ['gameId', 'version', 'metadata', 'setup', 'phases', 'actions', 'scoring', 'endConditions'];
let allValid = true;

for (const field of requiredFields) {
  if (example[field] !== undefined) {
    console.log(`✅ ${field}: present`);
  } else {
    console.log(`❌ ${field}: MISSING`);
    allValid = false;
  }
}

// Check version format
if (example.version && /^v\d+\.\d+$/.test(example.version)) {
  console.log(`✅ version format: valid (${example.version})`);
} else {
  console.log(`❌ version format: invalid (expected vX.Y, got ${example.version})`);
  allValid = false;
}

// Summary
console.log('\n📊 Summary');
console.log('----------\n');
console.log(`Game: ${example.metadata?.name || 'Unknown'}`);
console.log(`Game ID: ${example.gameId || 'Unknown'}`);
console.log(`Version: ${example.version || 'Unknown'}`);
console.log(`Players: ${example.metadata?.playerCount?.min || '?'}-${example.metadata?.playerCount?.max || '?'}`);
console.log(`Setup Steps: ${example.setup?.steps?.length || 0}`);
console.log(`Phases: ${example.phases?.length || 0}`);
console.log(`Actions: ${example.actions?.length || 0}`);
console.log(`Scoring Sources: ${example.scoring?.sources?.length || 0}`);
console.log(`End Conditions: ${example.endConditions?.length || 0}`);
console.log(`Edge Cases: ${example.edgeCases?.length || 0}`);
console.log(`Glossary Terms: ${example.glossary?.length || 0}`);

console.log(`\n${allValid ? '✅ Validation PASSED' : '❌ Validation FAILED'}\n`);

if (!allValid) {
  process.exit(1);
}

console.log('💡 For full JSON Schema validation, use:');
console.log('   npm install -g ajv-cli');
console.log('   ajv validate -s schemas/rulespec.v0.schema.json -d schemas/examples/tic-tac-toe.rulespec.json\n');