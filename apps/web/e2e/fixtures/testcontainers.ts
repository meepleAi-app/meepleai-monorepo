// @ts-nocheck - Optional testcontainers feature, packages not installed by default
/**
 * Testcontainers Fixture - Infrastructure Setup
 *
 * Provides isolated PostgreSQL, Redis, and Qdrant containers for E2E tests
 * NOTE: Requires manual installation: pnpm add -D @testcontainers/postgresql testcontainers
 * Only used when E2E_USE_TESTCONTAINERS=true environment variable is set
 */

import { PostgreSqlContainer, type StartedPostgreSqlContainer } from '@testcontainers/postgresql';
import { GenericContainer, type StartedTestContainer } from 'testcontainers';

export class TestInfrastructure {
  private pgContainer?: StartedPostgreSqlContainer;
  private redisContainer?: StartedTestContainer;
  private qdrantContainer?: StartedTestContainer;

  /**
   * Starts all infrastructure containers
   * Sets environment variables for backend connection
   */
  async start(): Promise<void> {
    console.log('🚀 Starting test infrastructure containers...');

    // Start PostgreSQL
    console.log('  📦 Starting PostgreSQL 16...');
    this.pgContainer = await new PostgreSqlContainer('postgres:16-alpine')
      .withDatabase('meepleai_test')
      .withUsername('test_user')
      .withPassword('test_password')
      .withExposedPorts(5432)
      .start();

    // Start Redis
    console.log('  📦 Starting Redis 7...');
    this.redisContainer = await new GenericContainer('redis:7-alpine')
      .withExposedPorts(6379)
      .start();

    // Start Qdrant
    console.log('  📦 Starting Qdrant 1.7...');
    this.qdrantContainer = await new GenericContainer('qdrant/qdrant:v1.7.0')
      .withExposedPorts(6333)
      .start();

    // Set environment variables for backend
    const dbConnectionString = this.pgContainer.getConnectionUri();
    const redisPort = this.redisContainer.getMappedPort(6379);
    const qdrantPort = this.qdrantContainer.getMappedPort(6333);

    process.env.DATABASE_URL = dbConnectionString;
    process.env.REDIS_URL = `redis://localhost:${redisPort}`;
    process.env.QDRANT_URL = `http://localhost:${qdrantPort}`;

    console.log('✅ Test infrastructure started successfully');
    console.log(`  PostgreSQL: ${dbConnectionString}`);
    console.log(`  Redis: redis://localhost:${redisPort}`);
    console.log(`  Qdrant: http://localhost:${qdrantPort}`);
  }

  /**
   * Stops all infrastructure containers and cleans up resources
   */
  async stop(): Promise<void> {
    console.log('🛑 Stopping test infrastructure containers...');

    try {
      await this.pgContainer?.stop();
      console.log('  ✅ PostgreSQL stopped');
    } catch (error) {
      console.warn('  ⚠️  PostgreSQL stop error:', error);
    }

    try {
      await this.redisContainer?.stop();
      console.log('  ✅ Redis stopped');
    } catch (error) {
      console.warn('  ⚠️  Redis stop error:', error);
    }

    try {
      await this.qdrantContainer?.stop();
      console.log('  ✅ Qdrant stopped');
    } catch (error) {
      console.warn('  ⚠️  Qdrant stop error:', error);
    }

    console.log('✅ Test infrastructure cleanup complete');
  }

  /**
   * Gets PostgreSQL connection details
   */
  getPostgresConnection(): {
    host: string;
    port: number;
    database: string;
    username: string;
    password: string;
    connectionString: string;
  } {
    if (!this.pgContainer) {
      throw new Error('PostgreSQL container not started');
    }

    return {
      host: this.pgContainer.getHost(),
      port: this.pgContainer.getMappedPort(5432),
      database: this.pgContainer.getDatabase(),
      username: this.pgContainer.getUsername(),
      password: this.pgContainer.getPassword(),
      connectionString: this.pgContainer.getConnectionUri(),
    };
  }

  /**
   * Gets Redis connection details
   */
  getRedisConnection(): { host: string; port: number; url: string } {
    if (!this.redisContainer) {
      throw new Error('Redis container not started');
    }

    const port = this.redisContainer.getMappedPort(6379);
    return {
      host: this.redisContainer.getHost(),
      port,
      url: `redis://localhost:${port}`,
    };
  }

  /**
   * Gets Qdrant connection details
   */
  getQdrantConnection(): { host: string; port: number; url: string } {
    if (!this.qdrantContainer) {
      throw new Error('Qdrant container not started');
    }

    const port = this.qdrantContainer.getMappedPort(6333);
    return {
      host: this.qdrantContainer.getHost(),
      port,
      url: `http://localhost:${port}`,
    };
  }
}

// Singleton instance for global setup/teardown
let infrastructureInstance: TestInfrastructure | null = null;

export function getTestInfrastructure(): TestInfrastructure {
  if (!infrastructureInstance) {
    infrastructureInstance = new TestInfrastructure();
  }
  return infrastructureInstance;
}

export function resetTestInfrastructure(): void {
  infrastructureInstance = null;
}
