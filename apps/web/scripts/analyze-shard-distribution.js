#!/usr/bin/env node
/**
 * Shard Distribution Analyzer
 * Issue #3082 Phase B: Analyze test execution balance across shards
 *
 * Reads duration-metrics-shard-*.json files and generates:
 * - Per-shard statistics (count, total duration, average)
 * - Coefficient of variation (identifies imbalance)
 * - Slowest tests across all shards
 * - Recommendations for balancing
 */

const fs = require('fs');
const path = require('path');

const RESULTS_DIR = path.join(process.cwd(), 'test-results');
const VARIANCE_THRESHOLD = 0.20; // 20% coefficient of variation = concerning

/**
 * Load all shard metrics files
 */
function loadShardMetrics() {
  const files = fs
    .readdirSync(RESULTS_DIR)
    .filter(f => f.startsWith('duration-metrics-shard-') && f.endsWith('.json'));

  return files.map(file => {
    const content = fs.readFileSync(path.join(RESULTS_DIR, file), 'utf8');
    return JSON.parse(content);
  });
}

/**
 * Calculate statistics
 */
function calculateStats(shardMetrics) {
  const shards = shardMetrics.map(sm => ({
    shard: sm.shard,
    testCount: sm.testCount,
    totalDuration: sm.totalDuration,
    avgDuration: sm.totalDuration / sm.testCount,
    tests: sm.tests,
  }));

  // Calculate variance
  const avgTotalDuration =
    shards.reduce((sum, s) => sum + s.totalDuration, 0) / shards.length;
  const variance =
    shards.reduce((sum, s) => sum + Math.pow(s.totalDuration - avgTotalDuration, 2), 0) /
    shards.length;
  const stdDev = Math.sqrt(variance);
  const coefficientOfVariation = stdDev / avgTotalDuration;

  return { shards, avgTotalDuration, stdDev, coefficientOfVariation };
}

/**
 * Find slowest tests across all shards
 */
function findSlowestTests(shardMetrics, limit = 15) {
  const allTests = shardMetrics.flatMap(sm =>
    sm.tests.map(t => ({ ...t, shard: sm.shard }))
  );

  return allTests.sort((a, b) => b.duration - a.duration).slice(0, limit);
}

/**
 * Generate recommendations
 */
function generateRecommendations(stats) {
  const recommendations = [];

  if (stats.coefficientOfVariation > VARIANCE_THRESHOLD) {
    recommendations.push({
      severity: 'high',
      message: `High variance detected (${(stats.coefficientOfVariation * 100).toFixed(1)}% CV)`,
      action: 'Implement shard balancing with custom test groups',
    });
  }

  const maxShard = stats.shards.reduce((max, s) =>
    s.totalDuration > max.totalDuration ? s : max
  );
  const minShard = stats.shards.reduce((min, s) =>
    s.totalDuration < min.totalDuration ? s : min
  );
  const imbalance = (maxShard.totalDuration - minShard.totalDuration) / minShard.totalDuration;

  if (imbalance > 0.3) {
    recommendations.push({
      severity: 'medium',
      message: `Shard ${maxShard.shard} is ${(imbalance * 100).toFixed(1)}% slower than shard ${minShard.shard}`,
      action: 'Move slow tests from overloaded shard to faster shards',
    });
  }

  return recommendations;
}

/**
 * Display results
 */
function displayResults(stats, slowest, recommendations) {
  console.log('\n' + '='.repeat(80));
  console.log('📊 E2E SHARD DISTRIBUTION ANALYSIS');
  console.log('='.repeat(80) + '\n');

  // Per-Shard Table
  console.log('Per-Shard Statistics:\n');
  console.log('Shard | Tests | Total Duration | Avg Duration | % of Total');
  console.log('-'.repeat(70));

  const totalTests = stats.shards.reduce((sum, s) => sum + s.testCount, 0);
  const totalDuration = stats.shards.reduce((sum, s) => sum + s.totalDuration, 0);

  stats.shards.forEach(shard => {
    const pct = ((shard.totalDuration / totalDuration) * 100).toFixed(1);
    console.log(
      `  ${shard.shard}   | ${String(shard.testCount).padEnd(5)} | ` +
        `${(shard.totalDuration / 1000).toFixed(2)}s${' '.repeat(11 - String((shard.totalDuration / 1000).toFixed(2)).length)}| ` +
        `${(shard.avgDuration / 1000).toFixed(2)}s${' '.repeat(9 - String((shard.avgDuration / 1000).toFixed(2)).length)}| ${pct}%`
    );
  });

  console.log('-'.repeat(70));
  console.log(
    `Total | ${String(totalTests).padEnd(5)} | ${(totalDuration / 1000).toFixed(2)}s${' '.repeat(11 - String((totalDuration / 1000).toFixed(2)).length)}| ` +
      `${(totalDuration / totalTests / 1000).toFixed(2)}s${' '.repeat(9 - String((totalDuration / totalTests / 1000).toFixed(2)).length)}| 100%`
  );

  // Balance Metrics
  console.log('\n\nBalance Metrics:\n');
  console.log(`  Average Duration per Shard: ${(stats.avgTotalDuration / 1000).toFixed(2)}s`);
  console.log(`  Standard Deviation: ${(stats.stdDev / 1000).toFixed(2)}s`);
  console.log(
    `  Coefficient of Variation: ${(stats.coefficientOfVariation * 100).toFixed(1)}% ${stats.coefficientOfVariation > VARIANCE_THRESHOLD ? '⚠️  HIGH' : '✅ OK'}`
  );

  // Slowest Tests
  console.log('\n\n🐌 Top 15 Slowest Tests:\n');
  console.log('Rank | Duration | Shard | File');
  console.log('-'.repeat(70));

  slowest.forEach((test, i) => {
    const fileName = test.file.replace('apps/web/e2e/', '');
    console.log(
      `  ${String(i + 1).padStart(2)}  | ${(test.duration / 1000).toFixed(2)}s${' '.repeat(6 - String((test.duration / 1000).toFixed(2)).length)}| ` +
        `  ${test.shard}   | ${fileName.substring(0, 45)}`
    );
  });

  // Recommendations
  if (recommendations.length > 0) {
    console.log('\n\n💡 Recommendations:\n');
    recommendations.forEach((rec, i) => {
      const icon = rec.severity === 'high' ? '🔴' : '🟡';
      console.log(`${icon} ${i + 1}. ${rec.message}`);
      console.log(`   Action: ${rec.action}\n`);
    });
  } else {
    console.log('\n\n✅ Shards are well-balanced! No action needed.\n');
  }

  console.log('='.repeat(80) + '\n');
}

/**
 * Main execution
 */
async function main() {
  try {
    if (!fs.existsSync(RESULTS_DIR)) {
      console.error('❌ test-results/ directory not found. Run tests first!');
      process.exit(1);
    }

    console.log('🔍 Loading shard metrics...');
    const shardMetrics = loadShardMetrics();

    if (shardMetrics.length === 0) {
      console.error('❌ No duration metrics found. Run tests with duration reporter enabled!');
      console.log('   Run: pnpm test:e2e:parallel');
      process.exit(1);
    }

    console.log(`✅ Loaded metrics for ${shardMetrics.length} shards`);

    const stats = calculateStats(shardMetrics);
    const slowest = findSlowestTests(shardMetrics, 15);
    const recommendations = generateRecommendations(stats);

    displayResults(stats, slowest, recommendations);

    // Exit with warning if high variance
    if (stats.coefficientOfVariation > VARIANCE_THRESHOLD) {
      console.log('⚠️  WARNING: High shard variance detected. Consider rebalancing.\n');
      process.exit(0); // Still success, just a warning
    }
  } catch (error) {
    console.error('❌ Analysis failed:', error.message);
    process.exit(1);
  }
}

main();
