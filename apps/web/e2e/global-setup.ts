/**
 * Global setup for Playwright E2E tests
 * Issue #2007: E2E Server Stability - Phase 1, Batch 2
 * Issue #2008: E2E Server Stability - Phase 2 (Memory Monitoring)
 *
 * 1. Ensures server is healthy before running tests
 * 2. Starts memory monitoring for resource consumption visibility
 */

import { waitForServerHealth } from './helpers/server-health';
import { MemoryMonitor } from './helpers/memory-monitor';

export default async function globalSetup() {
  console.log('🔍 Waiting for server to be healthy...');

  try {
    await waitForServerHealth('http://localhost:3000', 60, 1000);
    console.log('✅ Server is ready for testing');
  } catch (error) {
    console.error('❌ Server health check failed:', error);
    throw error;
  }

  // Issue #2008: Start memory monitoring
  console.log('📊 Starting memory monitoring...');
  const monitor = new MemoryMonitor();
  monitor.start(10000); // Sample every 10 seconds

  // Note: globalSetup and globalTeardown run in separate processes
  // Monitor will auto-stop when this process exits
  // Teardown will read persisted log file for reporting
}
