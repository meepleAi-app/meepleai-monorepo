#!/usr/bin/env tsx
/**
 * Frontend Test Generator for Issue #2309
 *
 * Generates Vitest tests for:
 * - API clients (lib/api/clients/*)
 * - Utilities (lib/errorUtils, retryUtils, etc.)
 * - Zustand stores (store/chat/slices/*)
 *
 * Pattern: AAA (Arrange-Act-Assert), Vitest, mocks
 */

import * as fs from 'fs';
import * as path from 'path';

interface TestGenerationConfig {
  sourceFile: string;
  outputFile: string;
  testType: 'api-client' | 'utility' | 'zustand-store';
  functions?: string[]; // Optional: specific functions to test
}

interface FunctionInfo {
  name: string;
  isAsync: boolean;
  params: string[];
  returnType?: string;
}

/**
 * Analyzes TypeScript file to find exported functions
 */
function extractFunctions(sourceCode: string): FunctionInfo[] {
  const functions: FunctionInfo[] = [];

  // Match: export function name(...) or export async function name(...)
  const functionRegex = /export\s+(async\s+)?function\s+(\w+)\s*\((.*?)\)/g;
  let match;

  while ((match = functionRegex.findAll(sourceCode)) !== null) {
    const isAsync = !!match[1];
    const name = match[2];
    const paramsStr = match[3];

    const params = paramsStr
      .split(',')
      .map(p => p.trim().split(':')[0].trim())
      .filter(p => p.length > 0);

    functions.push({ name, isAsync, params });
  }

  return functions;
}

/**
 * Generates test template for API client
 */
function generateApiClientTest(config: TestGenerationConfig, funcs: FunctionInfo[]): string {
  const fileName = path.basename(config.sourceFile, '.ts');
  const importPath = config.sourceFile
    .replace(/^.*[\/\\]src[\/\\]/, '@/')
    .replace(/\.ts$/, '')
    .replace(/\\/g, '/');

  let testContent = `/**
 * Tests for ${fileName} (Issue #2309)
 *
 * Coverage target: 90%+
 * Auto-generated base tests - enhance with edge cases
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';

describe('${fileName}', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

`;

  // Generate test for each function
  funcs.forEach(func => {
    const asyncKeyword = func.isAsync ? 'async ' : '';
    const awaitKeyword = func.isAsync ? 'await ' : '';

    testContent += `  describe('${func.name}', () => {
    it('should handle successful operation', ${asyncKeyword}() => {
      // Arrange
      const mockData = {}; // TODO: Add mock data

      // Act
      const result = ${awaitKeyword}${func.name}(mockData);

      // Assert
      expect(result).toBeDefined();
    });

    it('should handle errors gracefully', ${asyncKeyword}() => {
      // Arrange
      const invalidInput = null;

      // Act & Assert
      ${func.isAsync ? 'await expect(' + func.name + '(invalidInput)).rejects.toThrow();' : 'expect(() => ' + func.name + '(invalidInput)).toThrow();'}
    });
  });

`;
  });

  testContent += `});
`;

  return testContent;
}

/**
 * Generates test template for utility functions
 */
function generateUtilityTest(config: TestGenerationConfig, funcs: FunctionInfo[]): string {
  const fileName = path.basename(config.sourceFile, '.ts');
  const importPath = config.sourceFile
    .replace(/^.*[\/\\]src[\/\\]/, '@/')
    .replace(/\.ts$/, '')
    .replace(/\\/g, '/');

  let testContent = `/**
 * Tests for ${fileName} (Issue #2309)
 *
 * Coverage target: 90%+
 * Tests: Core functionality, edge cases, error handling
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { ${funcs.map(f => f.name).join(', ')} } from '${importPath}';

describe('${fileName}', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

`;

  funcs.forEach(func => {
    const asyncKeyword = func.isAsync ? 'async ' : '';
    const awaitKeyword = func.isAsync ? 'await ' : '';

    testContent += `  describe('${func.name}', () => {
    it('should handle valid input', ${asyncKeyword}() => {
      // Arrange
      const validInput = {}; // TODO: Add valid input

      // Act
      const result = ${awaitKeyword}${func.name}(validInput);

      // Assert
      expect(result).toBeDefined();
    });

    it('should handle null/undefined', ${asyncKeyword}() => {
      // Act & Assert for null
      ${func.isAsync ? 'await expect(' + func.name + '(null)).resolves.toBeDefined();' : 'const resultNull = ' + func.name + '(null); expect(resultNull).toBeDefined();'}

      // Act & Assert for undefined
      ${func.isAsync ? 'await expect(' + func.name + '(undefined)).resolves.toBeDefined();' : 'const resultUndef = ' + func.name + '(undefined); expect(resultUndef).toBeDefined();'}
    });

    it('should handle edge cases', ${asyncKeyword}() => {
      // TODO: Add edge case tests
    });
  });

`;
  });

  testContent += `});
`;

  return testContent;
}

/**
 * Generates test template for Zustand store slice
 */
function generateZustandStoreTest(config: TestGenerationConfig): string {
  const fileName = path.basename(config.sourceFile, '.ts');
  const storeName = fileName.replace('Slice', '');

  const testContent = `/**
 * Tests for ${fileName} (Issue #2309)
 *
 * Coverage target: 90%+
 * Tests: State management, actions, selectors
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { useChatStore } from '../store';

// Helper to reset store
function resetChatStore() {
  const store = useChatStore;
  store.setState({
    // TODO: Add initial state
  });
}

describe('${fileName}', () => {
  beforeEach(() => {
    resetChatStore();
    vi.clearAllMocks();
  });

  describe('Initial State', () => {
    it('should initialize with correct default values', () => {
      const state = useChatStore.getState();

      // TODO: Add specific state assertions
      expect(state).toBeDefined();
    });
  });

  describe('State Updates', () => {
    it('should update ${storeName} state correctly', () => {
      const store = useChatStore.getState();

      // TODO: Call action and verify state change
      // store.someAction(params);
      // expect(store.someProperty).toBe(expectedValue);
    });
  });

  describe('Error Handling', () => {
    it('should handle errors gracefully', async () => {
      const store = useChatStore.getState();

      // TODO: Test error scenarios
    });
  });
});
`;

  return testContent;
}

/**
 * Main generator function
 */
async function generateTests(configs: TestGenerationConfig[]) {
  let successCount = 0;
  let errorCount = 0;

  for (const config of configs) {
    try {
      console.log(`\n📝 Generating tests for: ${config.sourceFile}`);

      // Read source file
      if (!fs.existsSync(config.sourceFile)) {
        console.error(`   ❌ Source file not found: ${config.sourceFile}`);
        errorCount++;
        continue;
      }

      const sourceCode = fs.readFileSync(config.sourceFile, 'utf-8');

      // Extract functions
      const functions = extractFunctions(sourceCode);
      console.log(`   Found ${functions.length} exported functions`);

      // Generate test based on type
      let testCode: string;

      switch (config.testType) {
        case 'api-client':
          testCode = generateApiClientTest(config, functions);
          break;
        case 'utility':
          testCode = generateUtilityTest(config, functions);
          break;
        case 'zustand-store':
          testCode = generateZustandStoreTest(config);
          break;
        default:
          console.error(`   ❌ Unknown test type: ${config.testType}`);
          errorCount++;
          continue;
      }

      // Create output directory if needed
      const outputDir = path.dirname(config.outputFile);
      if (!fs.existsSync(outputDir)) {
        fs.mkdirSync(outputDir, { recursive: true });
        console.log(`   📁 Created directory: ${outputDir}`);
      }

      // Write test file
      fs.writeFileSync(config.outputFile, testCode);
      console.log(`   ✅ Generated: ${config.outputFile}`);
      successCount++;
    } catch (error) {
      console.error(`   ❌ Error generating test: ${error}`);
      errorCount++;
    }
  }

  console.log(`\n📊 Summary:`);
  console.log(`   ✅ Success: ${successCount}`);
  console.log(`   ❌ Errors: ${errorCount}`);
  console.log(`   📝 Total: ${configs.length}`);
}

// CLI mode
if (require.main === module) {
  console.log('🚀 Frontend Test Generator - Issue #2309');
  console.log('Usage: tsx scripts/frontend-test-generator.ts');
  console.log('\nNote: Edit the script to add TestGenerationConfig entries');
}

export { generateTests, type TestGenerationConfig };
