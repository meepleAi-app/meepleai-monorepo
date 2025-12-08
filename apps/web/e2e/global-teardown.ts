/**
 * Global teardown for Playwright E2E tests
 * Issue #2007: E2E Server Stability - Phase 1, Batch 2
 * Issue #2008: E2E Server Stability - Phase 2 (Memory Monitoring)
 *
 * Cleanup and final reporting after all tests complete
 */

import { MemoryMonitor } from './helpers/memory-monitor';

export default async function globalTeardown() {
  // Issue #2008: Generate memory report from persisted log file
  // Note: globalSetup ran in a separate process, so we can't access its monitor instance
  // Instead, we read the persisted file that the monitor wrote during execution
  const report = MemoryMonitor.getReport();
  if (report) {
    console.log('\n📊 Memory Usage Report:');
    console.log(`   Peak Heap Used: ${(report.peakHeapUsed / 1024 / 1024).toFixed(2)} MB`);
    console.log(`   Avg Heap Used:  ${(report.avgHeapUsed / 1024 / 1024).toFixed(2)} MB`);
    console.log(`   Peak Heap Total: ${(report.peakHeapTotal / 1024 / 1024).toFixed(2)} MB`);
    console.log(`   Samples: ${report.samples}`);
    console.log(`   Duration: ${(report.duration / 1000).toFixed(1)}s`);

    if (report.alerts.length > 0) {
      console.warn(`\n⚠️  Memory Alerts (${report.alerts.length}):`);
      report.alerts.forEach(alert => console.warn(`   ${alert}`));
    }
  }

  // Cleanup memory log file
  MemoryMonitor.cleanup();

  console.log('\n🧹 Global teardown complete');
}
