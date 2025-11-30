#!/usr/bin/env node
/* eslint-env node */
/* eslint-disable no-console */
/**
 * Fix Missing Mock Type Imports - Native Node.js version
 *
 * Adds `import type { Mock } from 'vitest'` to test files that use `as Mock`
 * but don't have the import statement.
 */

import { readFileSync, writeFileSync, readdirSync } from 'fs';
import { join } from 'path';
import { fileURLToPath } from 'url';
import { dirname } from 'path';

const __dirname = dirname(fileURLToPath(import.meta.url));
const rootDir = join(__dirname, '..');

// Recursively find all test files
function findTestFiles(dir, files = []) {
  const entries = readdirSync(dir, { withFileTypes: true });

  for (const entry of entries) {
    const fullPath = join(dir, entry.name);

    if (entry.isDirectory()) {
      if (entry.name === '__tests__' || entry.name === 'tests') {
        findTestFiles(fullPath, files);
      } else if (!entry.name.startsWith('.') && !entry.name.startsWith('node_modules')) {
        findTestFiles(fullPath, files);
      }
    } else if (entry.isFile() && /\.(ts|tsx)$/.test(entry.name) && !entry.name.endsWith('.d.ts')) {
      files.push(fullPath);
    }
  }

  return files;
}

const testFiles = findTestFiles(join(rootDir, 'src'));

let fixedCount = 0;
let skippedCount = 0;

console.log(`Found ${testFiles.length} test files to check\n`);

testFiles.forEach((filePath) => {
  const content = readFileSync(filePath, 'utf-8');

  // Check if file uses "as Mock" pattern
  if (!/as\s+Mock\b/.test(content)) {
    return;
  }

  // Check if Mock import already exists
  if (/import.*\bMock\b.*from\s+['"]vitest['"]/.test(content)) {
    skippedCount++;
    return;
  }

  console.log(`Fixing: ${filePath.replace(rootDir, '')}`);

  // Find the best place to insert the import
  const lines = content.split('\n');
  let insertIndex = -1;
  let hasVitestImport = false;

  for (let i = 0; i < lines.length; i++) {
    const line = lines[i];

    // Check for existing vitest import (to add Mock there)
    if (/^import.*from\s+['"]vitest['"]/.test(line)) {
      // Check if it's a type-only import
      if (/^import\s+type/.test(line)) {
        // Add Mock to existing type import
        const updatedLine = line.replace(
          /import\s+type\s+{\s*([^}]+)\s*}\s+from/,
          (match, imports) => `import type { ${imports.trim()}, Mock } from`
        );
        lines[i] = updatedLine;
        hasVitestImport = true;
        break;
      }
    }

    // Find first import to insert before it
    if (insertIndex === -1 && /^import\s+/.test(line)) {
      insertIndex = i;
    }
  }

  if (!hasVitestImport) {
    // Add new import statement
    const newImport = "import type { Mock } from 'vitest';";

    if (insertIndex === -1) {
      // No imports found, add after initial comments
      let commentEnd = 0;
      for (let i = 0; i < lines.length; i++) {
        const trimmed = lines[i].trim();
        if (trimmed !== '' && !trimmed.startsWith('//') && !trimmed.startsWith('/*') && !trimmed.startsWith('*')) {
          commentEnd = i;
          break;
        }
      }
      lines.splice(commentEnd, 0, newImport, '');
    } else {
      lines.splice(insertIndex, 0, newImport);
    }
  }

  const updatedContent = lines.join('\n');
  writeFileSync(filePath, updatedContent, 'utf-8');
  fixedCount++;
});

console.log(`\n✅ Fixed ${fixedCount} files`);
console.log(`⏭️  Skipped ${skippedCount} files (already have Mock import)`);
console.log(`📊 Total checked: ${testFiles.length} files`);
