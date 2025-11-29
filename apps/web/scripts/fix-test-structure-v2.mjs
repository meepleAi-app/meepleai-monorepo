#!/usr/bin/env node
/* eslint-env node */
/* eslint-disable no-console */
/**
 * Fix Test Structure v2 - Issue #1780
 *
 * Adds proper describe wrapper and setup to split test files
 * that are missing outer describe blocks.
 */

import fs from 'fs/promises';
import path from 'path';
import { fileURLToPath } from 'url';
import { execSync } from 'child_process';

const __filename = fileURLToPath(import.meta.url);
const __dirname = path.dirname(__filename);

const DRY_RUN = process.argv.includes('--dry-run');

/**
 * Standard setup template to insert
 */
const getSetupTemplate = (componentName, aspectName) => `
describe('${componentName}${aspectName ? ' - ' + aspectName : ''}', () => {
  let mockSelectChat: Mock;
  let mockDeleteChat: Mock;
  let originalConfirm: any;

  beforeEach(() => {
    mockSelectChat = vi.fn();
    mockDeleteChat = vi.fn();
    originalConfirm = window.confirm;
    window.confirm = vi.fn(() => true);
    vi.clearAllMocks();
  });

  afterEach(() => {
    window.confirm = originalConfirm;
  });

  /**
   * Helper to setup chat context with default values
   */
  const setupChatContext = (overrides?: any) => {
    mockUseChatContext.mockReturnValue({
      chats: [],
      activeChatId: null,
      selectChat: mockSelectChat,
      deleteChat: mockDeleteChat,
      loading: { chats: false },
      ...overrides,
    });
  };
`;

/**
 * Extract component name and aspect from file path
 */
function extractComponentInfo(filePath) {
  const basename = path.basename(filePath, path.extname(filePath));
  const withoutTest = basename.replace(/\.test$/, '');
  const parts = withoutTest.split('.');

  if (parts.length >= 2) {
    const aspect = parts.pop();
    const component = parts.join('.');
    // Capitalize aspect
    const aspectTitle = aspect.split('-').map(w =>
      w.charAt(0).toUpperCase() + w.slice(1)
    ).join(' ');
    return { component, aspect: aspectTitle };
  }

  return { component: withoutTest, aspect: null };
}

/**
 * Check if file needs fixing (has inner describes but no outer wrapper)
 */
async function needsFix(filePath) {
  try {
    const content = await fs.readFile(filePath, 'utf-8');
    const lines = content.split('\n');

    // Find first describe after imports
    let foundImports = false;
    let foundDescribe = false;
    let describeIndent = -1;

    for (let i = 0; i < Math.min(100, lines.length); i++) {
      const line = lines[i];

      // Track if we're past imports
      if (line.includes('import ') || line.includes('vi.mock(')) {
        foundImports = true;
        continue;
      }

      // Found a describe block
      if (line.trim().startsWith('describe(')) {
        foundDescribe = true;
        describeIndent = line.search(/\S/); // Get indentation
        break;
      }

      // If we find test code without describe first, needs fix
      if (foundImports && (
        line.trim().startsWith('it(') ||
        line.trim().startsWith('test(') ||
        line.includes('setupChatContext(') ||
        line.includes('render(<')
      )) {
        return true; // Code without wrapper
      }
    }

    // If first describe is at root level (0 indent), needs wrapper
    if (foundDescribe && describeIndent === 0) {
      return true;
    }

    return false;
  } catch (error) {
    console.error(`Error checking ${filePath}:`, error.message);
    return false;
  }
}

/**
 * Fix a test file by adding wrapper structure
 */
async function fixTestFile(filePath) {
  try {
    const content = await fs.readFile(filePath, 'utf-8');
    const lines = content.split('\n');

    const { component, aspect } = extractComponentInfo(filePath);

    // Find where to insert setup (after imports/mocks/helpers)
    let insertLine = 0;

    for (let i = 0; i < lines.length; i++) {
      const line = lines[i].trim();

      // Skip imports and mocks
      if (line.startsWith('import ') ||
          line.startsWith('vi.mock(') ||
          line.startsWith('const mock') ||
          line.startsWith('type ') ||
          line.startsWith('interface ')) {
        insertLine = i + 1;
        continue;
      }

      // Found helper function (like createMockChatThread)
      if (line.includes('const create') || line.includes('function create')) {
        // Find end of helper
        let braceCount = 0;
        let inHelper = false;
        for (let j = i; j < lines.length; j++) {
          for (const char of lines[j]) {
            if (char === '{') { braceCount++; inHelper = true; }
            if (char === '}') braceCount--;
          }
          if (inHelper && braceCount === 0) {
            insertLine = j + 1;
            break;
          }
        }
        break;
      }

      // Skip empty lines and comments after imports
      if (line === '' || line.startsWith('//') || line.startsWith('/*') || line.startsWith('*')) {
        continue;
      }

      // Found actual code, insert before this
      if (line.length > 0) {
        insertLine = i;
        break;
      }
    }

    // Remove orphan code fragments (lines between helpers and first describe)
    let firstDescribeLine = -1;
    for (let i = insertLine; i < lines.length; i++) {
      if (lines[i].trim().startsWith('describe(')) {
        firstDescribeLine = i;
        break;
      }
    }

    // Build new content
    const setup = getSetupTemplate(component, aspect);
    const newLines = [
      ...lines.slice(0, insertLine),
      '',
      setup,
      '',
      // Indent and include rest of content
      ...lines.slice(firstDescribeLine >= 0 ? firstDescribeLine : insertLine).map(line =>
        line.trim() === '' ? line : `  ${line}`
      ),
      '});', // Close outer describe
      '',
    ];

    if (DRY_RUN) {
      console.log(`  🔍 Would fix: ${path.relative(process.cwd(), filePath)}`);
      console.log(`     Component: ${component}${aspect ? ' - ' + aspect : ''}`);
      return { status: 'dry-run' };
    }

    await fs.writeFile(filePath, newLines.join('\n'), 'utf-8');
    console.log(`  ✅ Fixed: ${path.relative(process.cwd(), filePath)}`);

    return { status: 'fixed' };

  } catch (error) {
    console.error(`  ❌ Error fixing ${filePath}:`, error.message);
    return { status: 'error', reason: error.message };
  }
}

/**
 * Get files with TypeScript errors
 */
async function getFilesWithErrors() {
  try {
    execSync('pnpm typecheck 2>&1', {
      cwd: path.join(__dirname, '..'),
      encoding: 'utf-8',
      stdio: 'pipe',
      maxBuffer: 10 * 1024 * 1024,
    });
    return [];
  } catch (error) {
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
    return [];
  }
}

/**
 * Main execution
 */
async function main() {
  console.log('🔧 Test Structure Fix v2 - Issue #1780\n');

  if (DRY_RUN) {
    console.log('⚠️  DRY RUN MODE\n');
  }

  console.log('🔍 Finding files with errors...\n');

  const files = await getFilesWithErrors();
  console.log(`📋 Found ${files.length} files with TypeScript errors\n`);

  if (files.length === 0) {
    console.log('✅ No files with errors!');
    return;
  }

  const results = { fixed: 0, skipped: 0, errors: 0 };

  for (const file of files) {
    const fullPath = path.join(__dirname, '..', file);

    // Check if needs fix
    if (!(await needsFix(fullPath))) {
      results.skipped++;
      continue;
    }

    const result = await fixTestFile(fullPath);

    if (result.status === 'fixed' || result.status === 'dry-run') {
      results.fixed++;
    } else if (result.status === 'error') {
      results.errors++;
    }
  }

  console.log('\n' + '='.repeat(60));
  console.log('📊 Summary:');
  console.log(`   Fixed: ${results.fixed}`);
  console.log(`   Skipped: ${results.skipped}`);
  console.log(`   Errors: ${results.errors}`);
  console.log('='.repeat(60));

  if (!DRY_RUN && results.fixed > 0) {
    console.log('\n✅ Files fixed! Run: pnpm typecheck');
  }
}

main().catch(error => {
  console.error('❌ Fatal error:', error);
  process.exit(1);
});
