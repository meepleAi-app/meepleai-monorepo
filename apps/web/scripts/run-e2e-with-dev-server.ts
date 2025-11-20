import { spawn } from 'node:child_process';
import { setTimeout as wait } from 'node:timers/promises';

const WEB_PORT = Number(process.env.PLAYWRIGHT_WEB_PORT ?? '3100');
const BASE_URL = process.env.PLAYWRIGHT_BASE_URL ?? `http://localhost:${WEB_PORT}`;
const NEXT_BIN = './node_modules/next/dist/bin/next';

async function waitForServer(url: string, timeoutMs = 120_000, intervalMs = 1_000) {
  const start = Date.now();
  while (Date.now() - start < timeoutMs) {
    try {
      const res = await fetch(url, { method: 'HEAD' });
      if (res.ok || res.status === 404) {
        return;
      }
    } catch {
      // Ignore until server is ready
    }
    await wait(Math.min(intervalMs, timeoutMs));
  }
  throw new Error(`Next.js dev server did not become ready at ${url} within ${timeoutMs}ms`);
}

async function run() {
  const nextArgs = [
    '--max-old-space-size=6144',
    NEXT_BIN,
    'dev',
    '-p',
    String(WEB_PORT),
  ];

  const nextProcess = spawn('node', nextArgs, {
    cwd: process.cwd(),
    env: {
      ...process.env,
      PORT: String(WEB_PORT),
    },
    stdio: 'inherit',
  });

  const stopNext = () => {
    if (!nextProcess.killed) {
      nextProcess.kill('SIGINT');
    }
  };

  nextProcess.on('exit', (code) => {
    if (code !== 0) {
      console.error(`Next dev server exited with code ${code}`);
    }
  });

  try {
    await waitForServer(BASE_URL);
    console.log(`✅ Next dev server ready at ${BASE_URL}`);
  } catch (error) {
    stopNext();
    throw error;
  }

  const playwrightArgs = [
    'exec',
    'dotenv',
    '-e',
    '.env.test',
    '--',
    'playwright',
    'test',
    ...process.argv.slice(2),
  ];

  const nodeExec = process.env.npm_node_execpath ?? process.execPath;
  const packageManagerExec = process.env.npm_execpath;
  const command = packageManagerExec ? nodeExec : process.platform === 'win32' ? 'pnpm.cmd' : 'pnpm';
  const commandArgs = packageManagerExec
    ? [packageManagerExec, ...playwrightArgs]
    : playwrightArgs;

  const pwProcess = spawn(command, commandArgs, {
    cwd: process.cwd(),
    env: {
      ...process.env,
      PLAYWRIGHT_SKIP_WEB_SERVER: '1',
      PLAYWRIGHT_BASE_URL: BASE_URL,
      PLAYWRIGHT_WEB_PORT: String(WEB_PORT),
    },
    stdio: 'inherit',
  });

  const handleExit = (signal?: NodeJS.Signals) => {
    if (signal) {
      pwProcess.kill(signal);
    }
    stopNext();
  };

  process.on('SIGINT', () => handleExit('SIGINT'));
  process.on('SIGTERM', () => handleExit('SIGTERM'));

  pwProcess.on('exit', (code) => {
    stopNext();
    process.exit(code ?? 1);
  });
}

run().catch((error) => {
  console.error('❌ E2E runner failed:', error);
  process.exit(1);
});
