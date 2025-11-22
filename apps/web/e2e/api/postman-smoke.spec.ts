/**
 * Postman API Smoke Tests - Newman Integration
 *
 * This test file runs Postman collections via Newman CLI within Playwright.
 * It provides fast API smoke testing before running UI E2E tests.
 *
 * Usage:
 *   pnpm test:e2e:api               # Run all API tests
 *   pnpm test:e2e api/postman-smoke # Run this file only
 *
 * @see ../../tests/postman/README.md for Postman collection documentation
 */

import { test, expect } from '@playwright/test';
import { exec } from 'child_process';
import { promisify } from 'util';
import * as fs from 'fs';
import * as path from 'path';

const execAsync = promisify(exec);

// Paths relative to monorepo root
const MONOREPO_ROOT = path.resolve(__dirname, '../../../..');
const POSTMAN_DIR = path.join(MONOREPO_ROOT, 'postman');
const TESTS_POSTMAN_DIR = path.join(MONOREPO_ROOT, 'tests/postman');
const RESULTS_DIR = path.join(MONOREPO_ROOT, 'newman-results');

// Collection files
const KNOWLEDGE_BASE_COLLECTION = path.join(POSTMAN_DIR, 'KnowledgeBase-DDD-Tests.postman_collection.json');
const KNOWLEDGE_BASE_ENV = path.join(POSTMAN_DIR, 'Local-Development.postman_environment.json');
const HEALTH_COLLECTION = path.join(TESTS_POSTMAN_DIR, 'collections/01-health/HealthCheck.postman_collection.json');
const AUTH_COLLECTION = path.join(TESTS_POSTMAN_DIR, 'collections/02-authentication/Authentication.postman_collection.json');
const GAMES_COLLECTION = path.join(TESTS_POSTMAN_DIR, 'collections/03-game-management/GameManagement.postman_collection.json');
const KB_COLLECTION = path.join(TESTS_POSTMAN_DIR, 'collections/04-knowledge-base/KnowledgeBase.postman_collection.json');
const LOCAL_ENV = path.join(TESTS_POSTMAN_DIR, 'environments/local.postman_environment.json');

/**
 * Helper function to run Newman and parse results
 */
async function runNewmanCollection(
  collection: string,
  environment: string,
  options: {
    bail?: boolean;
    timeout?: number;
    folder?: string;
  } = {}
): Promise<{
  success: boolean;
  stats: {
    assertions: { passed: number; failed: number; total: number };
    requests: { total: number };
    tests: { passed: number; failed: number; total: number };
  };
  failures: any[];
  stdout: string;
  stderr: string;
}> {
  // Ensure results directory exists
  if (!fs.existsSync(RESULTS_DIR)) {
    fs.mkdirSync(RESULTS_DIR, { recursive: true });
  }

  const resultFile = path.join(RESULTS_DIR, `newman-${Date.now()}.json`);

  let command = `newman run "${collection}" -e "${environment}" -r json --reporter-json-export "${resultFile}"`;

  if (options.bail) {
    command += ' --bail';
  }

  if (options.folder) {
    command += ` --folder "${options.folder}"`;
  }

  const timeout = options.timeout || 120000; // 2 minutes default

  try {
    const { stdout, stderr } = await execAsync(command, {
      cwd: MONOREPO_ROOT,
      timeout,
      env: {
        ...process.env,
        NODE_TLS_REJECT_UNAUTHORIZED: '0' // For local development with self-signed certs
      }
    });

    // Read and parse Newman JSON results
    const results = JSON.parse(fs.readFileSync(resultFile, 'utf8'));

    // Clean up result file
    fs.unlinkSync(resultFile);

    return {
      success: results.run.stats.assertions.failed === 0 && results.run.failures.length === 0,
      stats: {
        assertions: {
          passed: results.run.stats.assertions.passed || 0,
          failed: results.run.stats.assertions.failed || 0,
          total: results.run.stats.assertions.total || 0
        },
        requests: {
          total: results.run.stats.requests.total || 0
        },
        tests: {
          passed: results.run.stats.tests.passed || 0,
          failed: results.run.stats.tests.failed || 0,
          total: results.run.stats.tests.total || 0
        }
      },
      failures: results.run.failures || [],
      stdout,
      stderr
    };
  } catch (error: any) {
    // If Newman fails, try to read partial results
    if (fs.existsSync(resultFile)) {
      const results = JSON.parse(fs.readFileSync(resultFile, 'utf8'));
      fs.unlinkSync(resultFile);

      return {
        success: false,
        stats: {
          assertions: {
            passed: results.run?.stats?.assertions?.passed || 0,
            failed: results.run?.stats?.assertions?.failed || 0,
            total: results.run?.stats?.assertions?.total || 0
          },
          requests: {
            total: results.run?.stats?.requests?.total || 0
          },
          tests: {
            passed: results.run?.stats?.tests?.passed || 0,
            failed: results.run?.stats?.tests?.failed || 0,
            total: results.run?.stats?.tests?.total || 0
          }
        },
        failures: results.run?.failures || [],
        stdout: error.stdout || '',
        stderr: error.stderr || error.message
      };
    }

    throw error;
  }
}

test.describe('API Smoke Tests (Newman + Postman)', () => {
  test.describe.configure({ mode: 'serial' }); // Run in order

  test('should have Newman installed', async () => {
    const { stdout } = await execAsync('newman --version');
    expect(stdout).toMatch(/^\d+\.\d+\.\d+/);
  });

  test('should have Postman collections available', () => {
    expect(fs.existsSync(KNOWLEDGE_BASE_COLLECTION)).toBeTruthy();
    expect(fs.existsSync(KNOWLEDGE_BASE_ENV)).toBeTruthy();
    expect(fs.existsSync(HEALTH_COLLECTION)).toBeTruthy();
    expect(fs.existsSync(AUTH_COLLECTION)).toBeTruthy();
    expect(fs.existsSync(GAMES_COLLECTION)).toBeTruthy();
    expect(fs.existsSync(KB_COLLECTION)).toBeTruthy();
    expect(fs.existsSync(LOCAL_ENV)).toBeTruthy();
  });

  test('API should be healthy before running tests', async ({ request }) => {
    const response = await request.get('http://localhost:8080/health');
    expect(response.ok()).toBeTruthy();

    const data = await response.json();
    expect(data.status).toBe('Healthy');
  });
});

test.describe('KnowledgeBase API Tests (DDD Phase 3)', () => {
  test.describe.configure({ mode: 'serial' });

  test('should pass all KnowledgeBase DDD tests (11/11)', async () => {
    const result = await runNewmanCollection(
      KNOWLEDGE_BASE_COLLECTION,
      KNOWLEDGE_BASE_ENV,
      { timeout: 60000 }
    );

    // Log results for debugging
    console.log('\n📊 KnowledgeBase DDD Test Results:');
    console.log(`  ✅ Assertions Passed: ${result.stats.assertions.passed}`);
    console.log(`  ❌ Assertions Failed: ${result.stats.assertions.failed}`);
    console.log(`  📝 Total Tests: ${result.stats.tests.total}`);
    console.log(`  📨 Total Requests: ${result.stats.requests.total}`);

    if (result.failures.length > 0) {
      console.log('\n❌ Failures:');
      result.failures.forEach((failure, index) => {
        console.log(`  ${index + 1}. ${failure.error.name}: ${failure.error.message}`);
        console.log(`     Source: ${failure.source.name || 'Unknown'}`);
      });
    }

    // Assertions
    expect(result.stats.assertions.failed).toBe(0);
    expect(result.failures).toHaveLength(0);
    expect(result.stats.tests.passed).toBeGreaterThanOrEqual(11);
    expect(result.success).toBeTruthy();
  });

  test('should validate Search endpoint (4 tests)', async () => {
    const result = await runNewmanCollection(
      KNOWLEDGE_BASE_COLLECTION,
      KNOWLEDGE_BASE_ENV,
      { folder: 'KnowledgeBase - Search', timeout: 30000 }
    );

    console.log('\n🔍 Search Tests:');
    console.log(`  ✅ Passed: ${result.stats.tests.passed}/4`);

    expect(result.stats.assertions.failed).toBe(0);
    expect(result.success).toBeTruthy();
  });

  test('should validate Q&A endpoint (4 tests)', async () => {
    const result = await runNewmanCollection(
      KNOWLEDGE_BASE_COLLECTION,
      KNOWLEDGE_BASE_ENV,
      { folder: 'KnowledgeBase - Q&A', timeout: 30000 }
    );

    console.log('\n🤖 Q&A Tests:');
    console.log(`  ✅ Passed: ${result.stats.tests.passed}/4`);

    expect(result.stats.assertions.failed).toBe(0);
    expect(result.success).toBeTruthy();
  });
});

test.describe('DDD Bounded Context Tests (New Structure)', () => {
  test.describe.configure({ mode: 'serial' });

  test('should pass Health Check tests', async () => {
    const result = await runNewmanCollection(
      HEALTH_COLLECTION,
      LOCAL_ENV,
      { timeout: 30000 }
    );

    console.log('\n💚 Health Check Tests:');
    console.log(`  ✅ Passed: ${result.stats.tests.passed}`);
    console.log(`  ❌ Failed: ${result.stats.tests.failed}`);

    expect(result.stats.assertions.failed).toBe(0);
    expect(result.success).toBeTruthy();
  });

  test('should pass Authentication tests', async () => {
    const result = await runNewmanCollection(
      AUTH_COLLECTION,
      LOCAL_ENV,
      { timeout: 60000 }
    );

    console.log('\n🔐 Authentication Tests:');
    console.log(`  ✅ Passed: ${result.stats.tests.passed}`);
    console.log(`  ❌ Failed: ${result.stats.tests.failed}`);

    expect(result.stats.assertions.failed).toBe(0);
    expect(result.success).toBeTruthy();
  });

  test('should pass Game Management tests', async () => {
    const result = await runNewmanCollection(
      GAMES_COLLECTION,
      LOCAL_ENV,
      { timeout: 30000 }
    );

    console.log('\n🎲 Game Management Tests:');
    console.log(`  ✅ Passed: ${result.stats.tests.passed}`);

    expect(result.stats.assertions.failed).toBe(0);
    expect(result.success).toBeTruthy();
  });

  test('should pass Knowledge Base tests (DDD)', async () => {
    const result = await runNewmanCollection(
      KB_COLLECTION,
      LOCAL_ENV,
      { timeout: 90000 } // Longer timeout for AI
    );

    console.log('\n🤖 Knowledge Base Tests:');
    console.log(`  ✅ Passed: ${result.stats.tests.passed}`);

    expect(result.stats.assertions.failed).toBe(0);
    expect(result.success).toBeTruthy();
  });
});

test.describe('Performance & Reliability', () => {
  test('Newman smoke test should complete in under 60 seconds', async () => {
    const startTime = Date.now();

    await runNewmanCollection(
      KNOWLEDGE_BASE_COLLECTION,
      KNOWLEDGE_BASE_ENV,
      { bail: true, timeout: 60000 }
    );

    const duration = Date.now() - startTime;
    console.log(`\n⏱️  Smoke test duration: ${duration}ms`);

    expect(duration).toBeLessThan(60000);
  });
});
