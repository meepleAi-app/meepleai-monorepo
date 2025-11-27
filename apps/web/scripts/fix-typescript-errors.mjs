#!/usr/bin/env node
/**
 * Fix TypeScript compilation errors in split test files (Issue #1780)
 *
 * Root Cause: Issue #1504 file splitting removed describe() wrapper blocks,
 * leaving test code without proper structure.
 *
 * Fix Strategy:
 * 1. Detect files with TS errors (missing describe blocks)
 * 2. Extract component name and aspect from file path
 * 3. Insert describe wrapper around test code
 * 4. Validate syntax correctness
 */

import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

// Configuration
const DRY_RUN = process.argv.includes('--dry-run');
const VERBOSE = process.argv.includes('--verbose');

// Patterns to identify where test code starts (after imports/mocks/helpers)
const TEST_CODE_START_PATTERNS = [
  /^\s*describe\(/,
  /^\s*it\(/,
  /^\s*test\(/,
  /^\s*beforeEach\(/,
  /^\s*afterEach\(/,
  /^\s*beforeAll\(/,
  /^\s*afterAll\(/,
];

/**
 * Extract component name and aspect from file path
 * Example: "src/__tests__/components/chat/ChatHistory.loading.test.tsx"
 * Returns: { component: "ChatHistory", aspect: "loading" }
 */
function extractFileInfo(filePath) {
  const basename = path.basename(filePath, path.extname(filePath));

  // Remove .test suffix
  const withoutTest = basename.replace(/\.test$/, '');

  // Split by last dot to get component and aspect
  const parts = withoutTest.split('.');

  if (parts.length >= 2) {
    const aspect = parts.pop(); // Last part is aspect
    const component = parts.join('.'); // Rest is component name
    return { component, aspect };
  }

  // Fallback: use whole name as component
  return { component: withoutTest, aspect: null };
}

/**
 * Generate describe block title from file info
 */
function generateDescribeTitle(fileInfo) {
  if (fileInfo.aspect) {
    // Capitalize aspect
    const aspectTitle = fileInfo.aspect
      .split('-')
      .map(word => word.charAt(0).toUpperCase() + word.slice(1))
      .join(' ');
    return `${fileInfo.component} - ${aspectTitle}`;
  }
  return fileInfo.component;
}

/**
 * Find the line where test code starts (after imports/mocks/helpers)
 */
function findTestCodeStart(lines) {
  let lastImportOrMockLine = -1;
  let foundHelperFunctions = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i].trim();

    // Track imports and mocks
    if (line.startsWith('import ') ||
        line.startsWith('vi.mock(') ||
        line.startsWith('const mock') ||
        line.startsWith('const create')) {
      lastImportOrMockLine = i;
      continue;
    }

    // Skip empty lines and comments after imports
    if (line === '' || line.startsWith('//') || line.startsWith('/*') || line.startsWith('*')) {
      continue;
    }

    // Check if this is where test code starts
    for (const pattern of TEST_CODE_START_PATTERNS) {
      if (pattern.test(lines[i])) {
        return i;
      }
    }

    // If we hit non-import/mock code that's not a test, it might be a helper
    // Continue searching for actual test code
    if (lastImportOrMockLine > -1 && i > lastImportOrMockLine + 5) {
      foundHelperFunctions = true;
    }
  }

  // Default to after imports if no test code found
  return lastImportOrMockLine + 1;
}

/**
 * Check if file already has a top-level describe block
 */
function hasTopLevelDescribe(lines) {
  const testCodeStart = findTestCodeStart(lines);

  // Check first non-empty line after imports
  for (let i = testCodeStart; i < Math.min(testCodeStart + 5, lines.length); i++) {
    const line = lines[i].trim();
    if (line === '' || line.startsWith('//')) continue;

    if (line.startsWith('describe(')) {
      return true;
    }
    break; // First real code line checked
  }

  return false;
}

/**
 * Fix a single test file by adding describe wrapper
 */
async function fixTestFile(filePath) {
  try {
    const content = await fs.readFile(filePath, 'utf-8');
    const lines = content.split('\n');

    // Check if already has top-level describe
    if (hasTopLevelDescribe(lines)) {
      if (VERBOSE) {
        console.log(`  ⏭️  Skipping ${filePath} (already has describe block)`);
      }
      return { status: 'skipped', reason: 'already-fixed' };
    }

    // Extract file info
    const fileInfo = extractFileInfo(filePath);
    const describeTitle = generateDescribeTitle(fileInfo);

    // Find where to insert describe block
    const insertLine = findTestCodeStart(lines);

    if (insertLine === -1 || insertLine >= lines.length) {
      return { status: 'error', reason: 'no-test-code-found' };
    }

    // Get indentation of first test line
    const firstTestLine = lines[insertLine];
    const baseIndent = firstTestLine.match(/^(\s*)/)[1];

    // Create describe wrapper
    const describeOpen = `${baseIndent}describe('${describeTitle}', () => {`;
    const describeClose = `${baseIndent}});`;

    // Build new content
    const newLines = [
      ...lines.slice(0, insertLine),
      describeOpen,
      ...lines.slice(insertLine).map(line =>
        line.trim() === '' ? line : `  ${line}` // Add 2-space indent to test code
      ),
      describeClose,
    ];

    const newContent = newLines.join('\n');

    if (DRY_RUN) {
      console.log(`  🔍 DRY RUN: Would fix ${filePath}`);
      console.log(`     Title: "${describeTitle}"`);
      console.log(`     Insert at line: ${insertLine + 1}`);
      return { status: 'dry-run', title: describeTitle };
    }

    // Write fixed content
    await fs.writeFile(filePath, newContent, 'utf-8');

    if (VERBOSE) {
      console.log(`  ✅ Fixed ${filePath}`);
      console.log(`     Title: "${describeTitle}"`);
    }

    return { status: 'fixed', title: describeTitle };

  } catch (error) {
    console.error(`  ❌ Error fixing ${filePath}:`, error.message);
    return { status: 'error', reason: error.message };
  }
}

/**
 * Get list of files with TypeScript errors
 */
async function getFilesWithErrors() {
  try {
    // Run typecheck and extract files with errors
    const output = execSync('pnpm typecheck 2>&1', {
      cwd: path.join(__dirname, '..'),
      encoding: 'utf-8',
      stdio: 'pipe',
      maxBuffer: 10 * 1024 * 1024, // 10MB buffer
    });

    // Extract unique file paths from error output
    const files = new Set();
    const lines = output.split('\n');

    for (const line of lines) {
      // Match pattern: src/path/to/file.tsx(line,col): error
      const match = line.match(/^(src\/[^(]+\.test\.tsx?)\(/);
      if (match) {
        files.add(match[1]);
      }
    }

    return Array.from(files).sort();
  } catch (error) {
    // typecheck exits with code 1 when errors exist, but we still get output
    if (error.stdout) {
      const files = new Set();
      const lines = error.stdout.split('\n');

      for (const line of lines) {
        const match = line.match(/^(src\/[^(]+\.test\.tsx?)\(/);
        if (match) {
          files.add(match[1]);
        }
      }

      return Array.from(files).sort();
    }
    throw error;
  }
}

/**
 * Main execution
 */
async function main() {
  console.log('🔧 TypeScript Error Fix Script (Issue #1780)\n');

  if (DRY_RUN) {
    console.log('⚠️  DRY RUN MODE - No files will be modified\n');
  }

  console.log('🔍 Detecting files with TypeScript errors...\n');

  // Get files list
  const files = await getFilesWithErrors();

  console.log(`📋 Found ${files.length} files with TypeScript errors\n`);

  if (files.length === 0) {
    console.log('✅ No files with errors found!');
    return;
  }

  // Process each file
  const results = {
    fixed: 0,
    skipped: 0,
    errors: 0,
    dryRun: 0,
  };

  for (const file of files) {
    const fullPath = path.join(__dirname, '..', file);
    const result = await fixTestFile(fullPath);

    switch (result.status) {
      case 'fixed':
        results.fixed++;
        console.log(`  ✅ ${file}`);
        break;
      case 'skipped':
        results.skipped++;
        break;
      case 'dry-run':
        results.dryRun++;
        break;
      case 'error':
        results.errors++;
        console.log(`  ❌ ${file} - ${result.reason}`);
        break;
    }
  }

  // Summary
  console.log('\n' + '='.repeat(60));
  console.log('📊 Summary:');
  console.log(`   Fixed: ${results.fixed}`);
  console.log(`   Skipped: ${results.skipped}`);
  console.log(`   Errors: ${results.errors}`);
  if (DRY_RUN) {
    console.log(`   Dry Run: ${results.dryRun}`);
  }
  console.log('='.repeat(60));

  if (results.errors > 0) {
    console.log('\n⚠️  Some files had errors. Review the output above.');
    process.exit(1);
  }

  if (!DRY_RUN && results.fixed > 0) {
    console.log('\n✅ All files fixed successfully!');
    console.log('   Next steps:');
    console.log('   1. Run: cd apps/web && pnpm typecheck');
    console.log('   2. Run: cd apps/web && pnpm test');
    console.log('   3. Review changes and commit');
  }
}

// Run script
main().catch(error => {
  console.error('❌ Fatal error:', error);
  process.exit(1);
});