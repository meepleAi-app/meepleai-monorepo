import { spawn } from 'node:child_process';
import { setTimeout as wait } from 'node:timers/promises';

const WEB_PORT = Number(process.env.PLAYWRIGHT_WEB_PORT ?? '3100');
const BASE_URL = process.env.PLAYWRIGHT_BASE_URL ?? `http://localhost:${WEB_PORT}`;
const NEXT_BIN = './node_modules/next/dist/bin/next';

// Test groups to run in sequence
const TEST_GROUPS = [
  { name: 'Authentication', patterns: ['e2e/auth*', 'e2e/demo-user*', 'e2e/login*'] },
  { name: 'Chat', patterns: ['e2e/chat*'] },
  { name: 'Admin', patterns: ['e2e/admin*'] },
  { name: 'PDF Processing', patterns: ['e2e/pdf*'] },
  { name: 'API', patterns: ['e2e/api'] },
  { name: 'Pages', patterns: ['e2e/pages*'] },
  { name: 'Accessibility', patterns: ['e2e/accessibility*'] },
];

interface TestResult {
  group: string;
  passed: boolean;
  exitCode: number | null;
  duration: number;
}

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

function runTestGroup(groupPatterns: string[]): Promise<{ exitCode: number | null; duration: number }> {
  return new Promise((resolve) => {
    const startTime = Date.now();

    const playwrightArgs = [
      'exec',
      'dotenv',
      '-e',
      '.env.test',
      '--',
      'playwright',
      'test',
      ...groupPatterns, // Spread patterns as separate arguments
      '--reporter=list', // Use list reporter for better group visibility
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

    pwProcess.on('exit', (code) => {
      const duration = Date.now() - startTime;
      resolve({ exitCode: code, duration });
    });
  });
}

async function run() {
  console.log('🚀 Starting Next.js dev server...\n');

  const nextArgs = [
    '--max-old-space-size=8192',
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
    if (code !== 0 && code !== null) {
      console.error(`Next dev server exited with code ${code}`);
    }
  });

  try {
    await waitForServer(BASE_URL);
    console.log(`✅ Next dev server ready at ${BASE_URL}\n`);
  } catch (error) {
    stopNext();
    throw error;
  }

  const results: TestResult[] = [];
  const totalStartTime = Date.now();

  console.log('📋 Running E2E test groups sequentially...\n');
  console.log('═'.repeat(60));

  for (const group of TEST_GROUPS) {
    console.log(`\n🧪 Running ${group.name} tests...`);
    console.log('─'.repeat(60));

    const { exitCode, duration } = await runTestGroup(group.patterns);
    const passed = exitCode === 0;

    results.push({
      group: group.name,
      passed,
      exitCode,
      duration,
    });

    const status = passed ? '✅ PASSED' : '❌ FAILED';
    const time = (duration / 1000).toFixed(2);
    console.log(`\n${status} - ${group.name} (${time}s)`);
    console.log('═'.repeat(60));
  }

  stopNext();

  const totalDuration = Date.now() - totalStartTime;
  const passedCount = results.filter((r) => r.passed).length;
  const failedCount = results.filter((r) => !r.passed).length;

  console.log('\n\n📊 Test Summary');
  console.log('═'.repeat(60));
  console.log(`Total Groups: ${TEST_GROUPS.length}`);
  console.log(`✅ Passed: ${passedCount}`);
  console.log(`❌ Failed: ${failedCount}`);
  console.log(`⏱️  Total Duration: ${(totalDuration / 1000 / 60).toFixed(2)} minutes`);
  console.log('═'.repeat(60));

  console.log('\n📋 Detailed Results:');
  results.forEach((result) => {
    const status = result.passed ? '✅' : '❌';
    const time = (result.duration / 1000).toFixed(2);
    console.log(`${status} ${result.group.padEnd(20)} ${time}s`);
  });

  console.log('\n');

  process.exit(failedCount > 0 ? 1 : 0);
}

run().catch((error) => {
  console.error('❌ E2E group runner failed:', error);
  process.exit(1);
});
