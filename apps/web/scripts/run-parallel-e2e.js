#!/usr/bin/env node
/**
 * Parallel E2E Test Runner
 * Issue #2008: Phase 2 - Parallel Execution
 *
 * Solves the port conflict issue when running multiple Playwright shards concurrently.
 *
 * Strategy:
 * 1. Start dev server once
 * 2. Wait for server health (manual health check)
 * 3. Run 4 shards in parallel with PARALLEL_E2E=true (prevents each shard from starting its own server)
 * 4. Cleanup server process
 */

const { spawn } = require('child_process');
const http = require('http');

const SHARDS = 6;
const SERVER_PORT = 3000;
const SERVER_URL = `http://localhost:${SERVER_PORT}`;

/**
 * Wait for server to respond to health checks
 */
async function waitForServerHealth(url, maxAttempts = 60, delayMs = 1000) {
  for (let i = 0; i < maxAttempts; i++) {
    try {
      await new Promise((resolve, reject) => {
        const req = http.get(url, res => {
          if (res.statusCode === 200 || res.statusCode === 404) {
            // 404 is ok - server is running, just no health endpoint
            resolve();
          } else {
            reject(new Error(`Server returned ${res.statusCode}`));
          }
        });

        req.on('error', reject);
        req.setTimeout(5000, () => {
          req.destroy();
          reject(new Error('Request timeout'));
        });
      });

      console.log(`✅ Server is healthy after ${i + 1} attempts`);
      return;
    } catch (error) {
      if (i === maxAttempts - 1) {
        throw new Error(
          `Server health check failed after ${maxAttempts} attempts: ${error.message}`
        );
      }
      await new Promise(resolve => setTimeout(resolve, delayMs));
    }
  }
}

async function main() {
  let serverProcess = null;

  try {
    console.log('🚀 Starting Parallel E2E Test Execution');
    console.log(`   Shards: ${SHARDS}`);
    console.log(`   Server: ${SERVER_URL}`);
    console.log('');

    // Step 1: Start dev server
    console.log('📡 Starting dev server...');
    serverProcess = spawn(
      'node',
      [
        '--max-old-space-size=4096',
        './node_modules/next/dist/bin/next',
        'dev',
        '-p',
        String(SERVER_PORT),
      ],
      {
        stdio: ['ignore', 'pipe', 'pipe'],
        env: { ...process.env, NODE_ENV: 'test' },
      }
    );

    // Log server output for debugging
    let serverReady = false;
    serverProcess.stdout.on('data', data => {
      const msg = data.toString();
      if (msg.includes('Ready') || msg.includes('started server')) {
        console.log(`   ✅ ${msg.trim()}`);
        serverReady = true;
      }
    });

    serverProcess.stderr.on('data', data => {
      const err = data.toString().trim();
      // Ignore Turbopack warnings
      if (!err.includes('Turbopack') && !err.includes('webpack')) {
        console.warn(`   ⚠️  ${err}`);
      }
    });

    serverProcess.on('exit', code => {
      if (!serverReady) {
        console.error(`   ❌ Server exited prematurely with code ${code}`);
      }
    });

    // Step 2: Wait for server to be healthy
    console.log('🔍 Waiting for server health...');
    await waitForServerHealth(SERVER_URL, 60, 1000);
    console.log('');

    // Step 3: Run shards in parallel with PARALLEL_E2E=true
    console.log('🧪 Running 4 test shards in parallel...\n');

    const shardProcesses = [];
    const shardPromises = [];

    for (let i = 1; i <= SHARDS; i++) {
      const shardProcess = spawn('pnpm', [`test:e2e:shard${i}`], {
        stdio: 'inherit',
        shell: true,
        env: { ...process.env, PARALLEL_E2E: 'true' },
      });

      shardProcesses.push(shardProcess);

      shardPromises.push(
        new Promise((resolve, reject) => {
          shardProcess.on('exit', code => {
            if (code === 0) {
              resolve(i);
            } else {
              reject(new Error(`Shard ${i} failed with exit code ${code}`));
            }
          });
        })
      );
    }

    // Wait for all shards to complete
    const results = await Promise.allSettled(shardPromises);

    // Report results
    console.log('\n📊 Parallel Execution Results:');
    const passed = results.filter(r => r.status === 'fulfilled').length;
    const failed = results.filter(r => r.status === 'rejected').length;

    console.log(`   ✅ Passed: ${passed}/${SHARDS} shards`);
    if (failed > 0) {
      console.log(`   ❌ Failed: ${failed}/${SHARDS} shards`);
      results.forEach((result, i) => {
        if (result.status === 'rejected') {
          console.error(`      Shard ${i + 1}: ${result.reason.message}`);
        }
      });
    }

    // Exit with error if any shard failed
    if (failed > 0) {
      console.error('\n❌ Some test shards failed. Check output above for details.');
      process.exit(1);
    }

    console.log('\n✅ All test shards passed successfully!');
  } catch (error) {
    console.error('\n❌ Parallel E2E execution failed:', error.message);
    process.exit(1);
  } finally {
    // Step 4: Cleanup server
    if (serverProcess && !serverProcess.killed) {
      console.log('\n🧹 Stopping dev server...');
      serverProcess.kill('SIGTERM');

      // Wait for graceful shutdown
      await new Promise(resolve => setTimeout(resolve, 2000));

      if (!serverProcess.killed) {
        console.warn('   ⚠️  Force killing server...');
        serverProcess.kill('SIGKILL');
      }
    }
  }
}

main().catch(error => {
  console.error('Fatal error:', error);
  process.exit(1);
});
