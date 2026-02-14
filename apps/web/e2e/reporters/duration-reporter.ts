import type {
  FullConfig,
  FullResult,
  Reporter,
  Suite,
  TestCase,
  TestResult,
} from '@playwright/test/reporter';
import * as fs from 'fs';
import * as path from 'path';

/**
 * Custom Playwright Reporter - Test Duration Tracking
 * Issue #3082 Phase B: Collect execution metrics for shard balancing
 *
 * Tracks:
 * - Test file path and duration
 * - Shard information (from env vars)
 * - Pass/fail status
 * - Timestamp for trend analysis
 *
 * Output: test-results/duration-metrics.json
 */

interface TestMetric {
  file: string;
  duration: number;
  status: 'passed' | 'failed' | 'skipped' | 'timedOut' | 'interrupted';
  retries: number;
}

interface ShardMetrics {
  timestamp: string;
  shard: number;
  totalShards: number;
  totalDuration: number;
  testCount: number;
  tests: TestMetric[];
}

class DurationReporter implements Reporter {
  private startTime: number = 0;
  private tests: TestMetric[] = [];
  private outputDir: string;

  constructor() {
    this.outputDir = path.join(process.cwd(), 'test-results');
  }

  onBegin(config: FullConfig, suite: Suite) {
    this.startTime = Date.now();
    console.log(`\n📊 Duration Reporter: Tracking ${suite.allTests().length} tests`);

    const shardInfo = process.env.SHARD_INDEX
      ? `Shard ${process.env.SHARD_INDEX}/${process.env.TOTAL_SHARDS}`
      : 'No sharding';
    console.log(`   Shard: ${shardInfo}`);
  }

  onTestEnd(test: TestCase, result: TestResult) {
    // Extract relative file path from test location
    const relativePath = test.location.file
      .replace(process.cwd(), '')
      .replace(/\\/g, '/')
      .replace(/^\//, '');

    this.tests.push({
      file: relativePath,
      duration: result.duration,
      status: result.status,
      retries: result.retry,
    });
  }

  async onEnd(result: FullResult) {
    const totalDuration = Date.now() - this.startTime;

    const metrics: ShardMetrics = {
      timestamp: new Date().toISOString(),
      shard: parseInt(process.env.SHARD_INDEX || '0', 10),
      totalShards: parseInt(process.env.TOTAL_SHARDS || '1', 10),
      totalDuration,
      testCount: this.tests.length,
      tests: this.tests,
    };

    // Ensure output directory exists
    if (!fs.existsSync(this.outputDir)) {
      fs.mkdirSync(this.outputDir, { recursive: true });
    }

    // Write metrics file
    const shard = process.env.SHARD_INDEX || 'all';
    const outputPath = path.join(this.outputDir, `duration-metrics-shard-${shard}.json`);

    fs.writeFileSync(outputPath, JSON.stringify(metrics, null, 2));

    console.log(`\n📊 Duration Metrics:`);
    console.log(`   Total Duration: ${(totalDuration / 1000).toFixed(2)}s`);
    console.log(`   Test Count: ${this.tests.length}`);
    console.log(`   Average: ${(totalDuration / this.tests.length / 1000).toFixed(2)}s per test`);
    console.log(`   Metrics saved: ${outputPath}`);

    // Find slowest tests
    const slowest = [...this.tests]
      .sort((a, b) => b.duration - a.duration)
      .slice(0, 5);

    console.log(`\n   🐌 Slowest Tests:`);
    slowest.forEach((t, i) => {
      const fileName = t.file.split('/').pop();
      console.log(`      ${i + 1}. ${fileName}: ${(t.duration / 1000).toFixed(2)}s`);
    });
  }
}

export default DurationReporter;
