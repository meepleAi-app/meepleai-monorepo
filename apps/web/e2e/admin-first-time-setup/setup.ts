/**
 * Project-Specific Setup - Admin First-Time Tests
 *
 * Optional Testcontainers integration for isolated infrastructure
 * Can be enabled via environment variable: E2E_USE_TESTCONTAINERS=true
 */

async function setup(): Promise<void> {
  const useTestcontainers = process.env.E2E_USE_TESTCONTAINERS === 'true';

  if (useTestcontainers) {
    console.log('\n🐳 Starting Testcontainers for admin-first-time-setup...\n');

    try {
      // Dynamic import - testcontainers.ts uses @ts-nocheck for optional packages
      const { getTestInfrastructure } = await import('../fixtures/testcontainers');
      const infrastructure = getTestInfrastructure();
      await infrastructure.start();

      console.log('✅ Testcontainers ready for admin setup tests\n');
    } catch (error) {
      console.error('❌ Testcontainers setup failed:', error);
      console.log('\n⚠️  Falling back to existing infrastructure\n');
      // Don't throw - allow tests to use existing infra
    }
  } else {
    console.log('⏭️  Using existing infrastructure (E2E_USE_TESTCONTAINERS not set)\n');
  }
}

export default setup;
