#!/usr/bin/env node
/* eslint-env node */
/* eslint-disable no-console */
/**
 * Fix retryUtils mock pattern
 *
 * Replaces `require('@/lib/retryUtils')` pattern with proper vi.mock() hoisting
 */

import { readFileSync, writeFileSync, readdirSync } from 'fs';
import { join, dirname } from 'path';
import { fileURLToPath } from 'url';

const __dirname = dirname(fileURLToPath(import.meta.url));
const rootDir = join(__dirname, '..');

// Find test files with require('@/lib/retryUtils')
function findTestFilesWithRetryUtils(dir, files = []) {
  const entries = readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = join(dir, entry.name);

    if (entry.isDirectory() && !entry.name.startsWith('.') && entry.name !== 'node_modules') {
      findTestFilesWithRetryUtils(fullPath, files);
    } else if (entry.isFile() && /\.(ts|tsx)$/.test(entry.name)) {
      const content = readFileSync(fullPath, 'utf-8');
      if (/require\(['"]@\/lib\/retryUtils['"]\)/.test(content)) {
        files.push(fullPath);
      }
    }
  }

  return files;
}

const testFiles = findTestFilesWithRetryUtils(join(rootDir, 'src'));
let fixedCount = 0;

console.log(`Found ${testFiles.length} files using require('@/lib/retryUtils')\n`);

testFiles.forEach((filePath) => {
  let content = readFileSync(filePath, 'utf-8');
  const originalContent = content;

  // Check if already has vi.mock for retryUtils
  if (/vi\.mock\(['"]@\/lib\/retryUtils['"]\)/.test(content)) {
    console.log(`Skipping (already mocked): ${filePath.replace(rootDir, '')}`);
    return;
  }

  console.log(`Fixing: ${filePath.replace(rootDir, '')}`);

  // Add vi.mock before first import
  const lines = content.split('\n');
  let firstImportIndex = -1;

  for (let i = 0; i < lines.length; i++) {
    if (/^import\s+/.test(lines[i])) {
      firstImportIndex = i;
      break;
    }
  }

  if (firstImportIndex !== -1) {
    const mockSetup = [
      "// Mock retryUtils",
      "vi.mock('@/lib/retryUtils', () => ({",
      "  retryWithBackoff: vi.fn(),",
      "}));",
      "",
    ].join('\n');

    lines.splice(firstImportIndex, 0, mockSetup);
    content = lines.join('\n');
  }

  // Remove inline require() calls
  content = content.replace(
    /const\s+{\s*retryWithBackoff\s*}\s*=\s*require\(['"]@\/lib\/retryUtils['"]\);?\s*/g,
    ''
  );

  // Add import at top if needed
  if (!/import.*retryWithBackoff.*from.*retryUtils/.test(content)) {
    content = content.replace(
      /^(\/\*\*[\s\S]*?\*\/\s*)?/,
      '$1import { retryWithBackoff } from \'@/lib/retryUtils\';\n'
    );
  }

  if (content !== originalContent) {
    writeFileSync(filePath, content, 'utf-8');
    fixedCount++;
  }
});

console.log(`\n✅ Fixed ${fixedCount} files`);
