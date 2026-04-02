#!/usr/bin/env node
/**
 * Analyze test failures for Issue #1887 - Batch 4
 * Quick categorization script for Option 2 (Incremental) approach
 */

const fs = require('fs');
const path = require('path');

const baselineFile = process.argv[2] || 'test-batch4-baseline-issue1887.txt';
const outputFile = 'batch4-analysis-results.json';

console.log(`📊 Analyzing failures from: ${baselineFile}`);

try {
  const content = fs.readFileSync(baselineFile, 'utf-8');

  // Extract failure lines
  const failLines = content.split('\n').filter(line => line.includes(' FAIL '));

  // Categorize failures
  const categories = {
    'API/Mock': [],
    'Testing Library': [],
    'Provider/Context': [],
    'Component Props': [],
    'Timer/Async': [],
    'Test Logic': [],
    Other: [],
  };

  failLines.forEach(line => {
    // Extract test file path
    const match = line.match(/src\/.*?\.test\.(tsx?|jsx?)/);
    if (!match) return;

    const testFile = match[0];
    const testLine = line;

    // Categorize by pattern
    if (testLine.includes('404') || testLine.includes('undefined') || testLine.includes('API')) {
      categories['API/Mock'].push({ file: testFile, line: testLine });
    } else if (
      testLine.includes('multiple elements') ||
      testLine.includes('getBy') ||
      testLine.includes('findBy')
    ) {
      categories['Testing Library'].push({ file: testFile, line: testLine });
    } else if (
      testLine.includes('Provider') ||
      testLine.includes('context') ||
      testLine.includes('useAuth')
    ) {
      categories['Provider/Context'].push({ file: testFile, line: testLine });
    } else if (
      testLine.includes('missing') ||
      testLine.includes('required') ||
      testLine.includes('prop')
    ) {
      categories['Component Props'].push({ file: testFile, line: testLine });
    } else if (
      testLine.includes('timeout') ||
      testLine.includes('waitFor') ||
      testLine.includes('timer')
    ) {
      categories['Timer/Async'].push({ file: testFile, line: testLine });
    } else if (
      testLine.includes('expects') ||
      testLine.includes('skeleton') ||
      testLine.includes('dialog')
    ) {
      categories['Test Logic'].push({ file: testFile, line: testLine });
    } else {
      categories['Other'].push({ file: testFile, line: testLine });
    }
  });

  // Count unique files per category
  const summary = {};
  Object.keys(categories).forEach(cat => {
    const uniqueFiles = [...new Set(categories[cat].map(f => f.file))];
    summary[cat] = {
      total_failures: categories[cat].length,
      unique_files: uniqueFiles.length,
      files: uniqueFiles.slice(0, 10), // Top 10
    };
  });

  // Output results
  const result = {
    timestamp: new Date().toISOString(),
    total_failures: failLines.length,
    categories: summary,
  };

  fs.writeFileSync(outputFile, JSON.stringify(result, null, 2));

  console.log('\n📈 ANALYSIS SUMMARY:');
  console.log(`Total Failures: ${failLines.length}`);
  console.log('\nBy Category:');
  Object.entries(summary).forEach(([cat, data]) => {
    console.log(`  ${cat}: ${data.total_failures} failures, ${data.unique_files} files`);
  });
  console.log(`\n✅ Full analysis saved to: ${outputFile}`);
} catch (error) {
  console.error('❌ Error:', error.message);
  process.exit(1);
}
