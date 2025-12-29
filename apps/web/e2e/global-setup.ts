/**
 * Global setup for Playwright E2E tests
 * Issue #2007: E2E Server Stability - Phase 1, Batch 2
 * Issue #2008: E2E Server Stability - Phase 2 (Memory Monitoring)
 *
 * 1. Ensures server is healthy before running tests
 * 2. Starts memory monitoring for resource consumption visibility
 */

import { MemoryMonitor } from './helpers/memory-monitor';
import { waitForServerHealth } from './helpers/server-health';

export default async function globalSetup() {
  console.log('🔍 Waiting for servers to be healthy...');

  const apiBase = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

  try {
    // Check backend API first
    console.log(`🔍 Checking backend API health (${apiBase})...`);
    await waitForServerHealth(`${apiBase}`, 60, 1000);
    console.log('✅ Backend API is ready');

    // Then check frontend
    console.log('🔍 Checking frontend server health...');
    await waitForServerHealth('http://localhost:3000', 60, 1000);
    console.log('✅ Frontend server is ready');

    console.log('✅ All servers ready for E2E testing');
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
