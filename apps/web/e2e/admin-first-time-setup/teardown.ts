/**
 * Project-Specific Teardown - Admin First-Time Tests
 *
 * Cleans up Testcontainers if they were used
 */

import { getTestInfrastructure, resetTestInfrastructure } from '../fixtures/testcontainers';

async function teardown(): Promise<void> {
  const useTestcontainers = process.env.E2E_USE_TESTCONTAINERS === 'true';

  if (useTestcontainers) {
    console.log('\n🧹 Stopping Testcontainers for admin-first-time-setup...\n');

    try {
      const infrastructure = getTestInfrastructure();
      await infrastructure.stop();
      resetTestInfrastructure();

      console.log('✅ Testcontainers cleanup complete\n');
    } catch (error) {
      console.error('⚠️  Testcontainers teardown error:', error);
      // Don't throw - cleanup is best-effort
    }
  }
}

export default teardown;
