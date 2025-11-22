import { spawn } from 'node:child_process';
import path from 'node:path';
import process from 'node:process';
import { setTimeout as delay } from 'node:timers/promises';

const repoRoot = path.resolve(__dirname, '../../..');
const infraDir = path.join(repoRoot, 'infra');
const defaultHealthUrl = 'http://localhost:5080/health';
const apiHealthUrl = process.env.PLAYWRIGHT_LOGIN_API_BASE ?? defaultHealthUrl;
const defaultSpec = 'e2e/demo-user-login.spec.ts';

async function main() {
  await ensureInfraContainers();
  await waitForApiHealth();
  await runPlaywright();
}

async function ensureInfraContainers() {
  console.info('🚀 Ensuring MeepleAI core services are running (postgres/redis/qdrant/api)...');
  await runCommand(
    'docker',
    ['compose', 'up', '-d', 'meepleai-postgres', 'meepleai-redis', 'meepleai-qdrant', 'meepleai-api'],
    { cwd: infraDir }
  );
}

async function waitForApiHealth() {
  const maxAttempts = Number(process.env.PLAYWRIGHT_LOGIN_HEALTH_RETRIES ?? 15);
  const delayMs = Number(process.env.PLAYWRIGHT_LOGIN_HEALTH_DELAY_MS ?? 2000);
  const timeoutMs = Number(process.env.PLAYWRIGHT_LOGIN_HEALTH_TIMEOUT_MS ?? 5000);

  for (let attempt = 1; attempt <= maxAttempts; attempt++) {
    try {
      const controller = new AbortController();
      const timeoutId = setTimeout(() => controller.abort(), timeoutMs);
      const response = await fetch(apiHealthUrl, { signal: controller.signal });
      clearTimeout(timeoutId);

      if (response.ok) {
        console.info(`✅ API healthy (${apiHealthUrl})`);
        return;
      }
      throw new Error(`Unexpected status ${response.status}`);
    } catch (error) {
      console.warn(`⌛ API not ready yet (attempt ${attempt}/${maxAttempts}): ${(error as Error).message}`);
      if (attempt === maxAttempts) {
        throw new Error(`API did not become healthy at ${apiHealthUrl} after ${maxAttempts} attempts`);
      }
      await delay(delayMs);
    }
  }
}

async function runPlaywright() {
  console.info('🎭 Running Playwright login suite...');
  const userArgs = process.argv.slice(2);
  const selectedSpec = process.env.PLAYWRIGHT_LOGIN_SPEC ?? defaultSpec;
  const specArgs = [selectedSpec, ...userArgs];
  const webPort = process.env.PLAYWRIGHT_WEB_PORT ?? '3100';
  const env = {
    ...process.env,
    PLAYWRIGHT_ENABLE_LOGIN: '1',
    PLAYWRIGHT_WEB_PORT: webPort
  };
  const command = 'playwright';
  await runCommand(
    command,
    ['test', ...specArgs],
    {
      cwd: path.join(repoRoot, 'apps/web'),
      env,
      shell: process.platform === 'win32'
    }
  );
}

function runCommand(
  command: string,
  args: string[],
  options: Omit<Parameters<typeof spawn>[2], 'stdio'> & { cwd?: string; env?: NodeJS.ProcessEnv } = {}
) {
  return new Promise<void>((resolve, reject) => {
    const child = spawn(command, args, {
      stdio: 'inherit',
      ...options
    });

    child.on('error', reject);
    child.on('exit', (code, signal) => {
      if (signal) {
        reject(new Error(`${command} exited due to signal ${signal}`));
        return;
      }
      if (typeof code === 'number' && code !== 0) {
        reject(new Error(`${command} exited with code ${code}`));
        return;
      }
      resolve();
    });
  });
}

main().catch((error) => {
  console.error(`❌ Login E2E run failed: ${(error as Error).message}`);
  process.exit(1);
});
