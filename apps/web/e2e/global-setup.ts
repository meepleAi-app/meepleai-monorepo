/**
 * Global setup for Playwright E2E tests
 * Issue #2007: E2E Server Stability - Phase 1, Batch 2
 *
 * Ensures server is healthy before running tests
 */

import { waitForServerHealth } from './helpers/server-health';

export default async function globalSetup() {
  console.log('🔍 Waiting for server to be healthy...');

  try {
    await waitForServerHealth('http://localhost:3000', 60, 1000);
    console.log('✅ Server is ready for testing');
  } catch (error) {
    console.error('❌ Server health check failed:', error);
    throw error;
  }
}
