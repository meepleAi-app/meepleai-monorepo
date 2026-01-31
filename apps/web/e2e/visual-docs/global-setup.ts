/**
 * Global Setup for Visual Documentation Tests
 *
 * 1. Checks if API is healthy, starts it if needed via Docker
 * 2. Cleans all screenshot directories
 *
 * This ensures the API is running and screenshots are overwritten rather than accumulated.
 */

import { execFileSync } from 'child_process';
import * as fs from 'fs';
import * as path from 'path';

const SCREENSHOTS_BASE = path.resolve(__dirname, '../../../../docs/screenshots');
const INFRA_DIR = path.resolve(__dirname, '../../../../infra');

const API_URL = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const API_HEALTH_TIMEOUT = 120000; // 2 minutes max wait for API
const API_HEALTH_INTERVAL = 2000; // Check every 2 seconds

const FLOW_DIRECTORIES = [
  'user-flows/authentication',
  'user-flows/game-discovery',
  'user-flows/library-management',
  'user-flows/ai-chat',
  'user-flows/game-sessions',
  'editor-flows/game-management',
  'editor-flows/document-management',
  'editor-flows/content-management',
  'editor-flows/publication-workflow',
  'admin-flows/approval-workflow',
  'admin-flows/user-management',
  'admin-flows/system-configuration',
  'admin-flows/monitoring',
];

// ============================================================================
// API Health Check & Startup
// ============================================================================

async function checkApiHealth(): Promise<boolean> {
  try {
    const response = await fetch(`${API_URL}/`, {
      method: 'GET',
      signal: AbortSignal.timeout(5000),
    });
    return response.ok;
  } catch {
    return false;
  }
}

async function waitForApiHealth(timeoutMs: number = API_HEALTH_TIMEOUT): Promise<boolean> {
  const startTime = Date.now();
  let lastError = '';

  while (Date.now() - startTime < timeoutMs) {
    try {
      const isHealthy = await checkApiHealth();
      if (isHealthy) {
        return true;
      }
    } catch (error) {
      lastError = error instanceof Error ? error.message : String(error);
    }

    // Wait before next check
    await new Promise(resolve => setTimeout(resolve, API_HEALTH_INTERVAL));
    process.stdout.write('.');
  }

  console.error(`\n❌ API health check failed after ${timeoutMs / 1000}s: ${lastError}`);
  return false;
}

function startDockerServices(): void {
  console.log('🐳 Starting Docker services (postgres, redis, qdrant, api)...\n');

  try {
    // Start required services with docker compose
    // Using execFileSync with 'docker' command and array of arguments for safety
    execFileSync('docker', ['compose', '--profile', 'dev', 'up', '-d', 'postgres', 'redis', 'qdrant', 'api'], {
      cwd: INFRA_DIR,
      stdio: 'inherit',
      timeout: 60000,
    });
    console.log('\n✅ Docker services started');
  } catch (error) {
    console.error('❌ Failed to start Docker services:', error);
    throw error;
  }
}

async function ensureApiRunning(): Promise<void> {
  console.log(`🔍 Checking API health at ${API_URL}...`);

  // First check if API is already running
  const isHealthy = await checkApiHealth();

  if (isHealthy) {
    console.log('✅ API is already running and healthy\n');
    return;
  }

  console.log('⚠️  API is not running, starting Docker services...\n');

  // Start Docker services
  startDockerServices();

  // Wait for API to become healthy
  console.log('\n⏳ Waiting for API to become healthy');
  const apiReady = await waitForApiHealth();

  if (!apiReady) {
    throw new Error('API failed to start within timeout. Check Docker logs with: docker compose logs api');
  }

  console.log('\n✅ API is now healthy and ready\n');
}

// ============================================================================
// Screenshot Directory Cleanup
// ============================================================================

function cleanDirectory(dir: string): void {
  if (!fs.existsSync(dir)) {
    fs.mkdirSync(dir, { recursive: true });
    return;
  }

  const files = fs.readdirSync(dir);
  let cleaned = 0;
  for (const file of files) {
    if (file.endsWith('.png') || file.endsWith('.json')) {
      const filePath = path.join(dir, file);
      fs.unlinkSync(filePath);
      cleaned++;
    }
  }
  if (cleaned > 0) {
    console.log(`🧹 Cleaned ${cleaned} files from ${path.basename(dir)}`);
  }
}

// ============================================================================
// Main Setup
// ============================================================================

async function globalSetup(): Promise<void> {
  console.log('\n📸 Visual Documentation - Global Setup\n');

  // Step 1: Ensure API is running
  await ensureApiRunning();

  // Step 2: Clean screenshot directories
  console.log('Cleaning screenshot directories...\n');

  for (const flowDir of FLOW_DIRECTORIES) {
    const fullPath = path.join(SCREENSHOTS_BASE, flowDir);
    cleanDirectory(fullPath);
  }

  console.log('\n✅ Directories ready for screenshot capture\n');
}

export default globalSetup;
