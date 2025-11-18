/**
 * Performance Regression Detection
 *
 * Compares current test results against baseline to detect regressions.
 * Fails if any metric degrades by more than threshold (default 10%).
 *
 * Usage: node utils/detect-regression.js <baseline.json> <current.json> [threshold%]
 */

const fs = require('fs');
const path = require('path');

const REGRESSION_THRESHOLD = 0.10; // 10% degradation threshold

function detectRegression(baselineFile, currentFile, threshold = REGRESSION_THRESHOLD) {
  console.log('🔍 Detecting performance regressions...\n');

  // Load JSON files
  const baseline = JSON.parse(fs.readFileSync(baselineFile, 'utf8'));
  const current = JSON.parse(fs.readFileSync(currentFile, 'utf8'));

  const baselineMetrics = baseline.metrics;
  const currentMetrics = current.metrics;

  let hasRegression = false;
  const regressions = [];

  // Define metrics to compare
  const metricsToCompare = [
    {
      key: 'http_req_duration',
      name: 'Overall Response Time (P95)',
      getValue: (m) => m.values['p(95)'],
      higherIsBetter: false,
    },
    {
      key: 'http_req_failed',
      name: 'Error Rate',
      getValue: (m) => m.values.rate,
      higherIsBetter: false,
    },
    {
      key: 'http_reqs',
      name: 'Throughput',
      getValue: (m) => m.values.rate,
      higherIsBetter: true,
    },
  ];

  // Endpoint-specific metrics
  const endpoints = ['rag-search', 'chat', 'games', 'sessions'];
  for (const endpoint of endpoints) {
    metricsToCompare.push({
      key: `http_req_duration{endpoint:${endpoint}}`,
      name: `${endpoint} Response Time (P95)`,
      getValue: (m) => m.values['p(95)'],
      higherIsBetter: false,
    });
  }

  // Compare metrics
  console.log('Metric Comparison:\n');
  console.log('─'.repeat(80));

  for (const metric of metricsToCompare) {
    const baselineMetric = baselineMetrics[metric.key];
    const currentMetric = currentMetrics[metric.key];

    if (!baselineMetric || !currentMetric) {
      console.log(`⚠️  ${metric.name.padEnd(40)} SKIPPED (no data)`);
      continue;
    }

    const baselineValue = metric.getValue(baselineMetric);
    const currentValue = metric.getValue(currentMetric);

    const change = ((currentValue - baselineValue) / baselineValue) * 100;
    const isRegression = metric.higherIsBetter
      ? change < -(threshold * 100)
      : change > (threshold * 100);

    const changeSymbol = change > 0 ? '+' : '';
    const status = isRegression ? '❌ REGRESSION' : '✅ OK';

    console.log(
      `${status.padEnd(20)} ${metric.name.padEnd(40)} ${baselineValue.toFixed(2)} → ${currentValue.toFixed(2)} (${changeSymbol}${change.toFixed(2)}%)`
    );

    if (isRegression) {
      hasRegression = true;
      regressions.push({
        metric: metric.name,
        baseline: baselineValue,
        current: currentValue,
        change: change,
      });
    }
  }

  console.log('─'.repeat(80));

  // Summary
  if (hasRegression) {
    console.log('\n❌ Performance Regression Detected!\n');
    console.log('The following metrics exceeded the regression threshold:\n');

    for (const regression of regressions) {
      console.log(
        `  • ${regression.metric}: ${regression.baseline.toFixed(2)} → ${regression.current.toFixed(2)} (${regression.change > 0 ? '+' : ''}${regression.change.toFixed(2)}%)`
      );
    }

    console.log(`\nThreshold: ${threshold * 100}%`);
    console.log('\nPlease investigate and optimize before merging.\n');

    process.exit(1);
  } else {
    console.log('\n✅ No performance regressions detected!\n');
    console.log('All metrics are within acceptable thresholds.\n');

    process.exit(0);
  }
}

// Generate regression report
function generateRegressionReport(baselineFile, currentFile, outputFile) {
  const baseline = JSON.parse(fs.readFileSync(baselineFile, 'utf8'));
  const current = JSON.parse(fs.readFileSync(currentFile, 'utf8'));

  const report = {
    timestamp: new Date().toISOString(),
    baseline: {
      file: path.basename(baselineFile),
      metrics: baseline.metrics,
    },
    current: {
      file: path.basename(currentFile),
      metrics: current.metrics,
    },
    comparison: {},
  };

  // Compare all metrics
  const allMetricKeys = new Set([
    ...Object.keys(baseline.metrics),
    ...Object.keys(current.metrics),
  ]);

  for (const key of allMetricKeys) {
    const baselineMetric = baseline.metrics[key];
    const currentMetric = current.metrics[key];

    if (baselineMetric && currentMetric) {
      report.comparison[key] = {
        baseline: baselineMetric.values,
        current: currentMetric.values,
      };
    }
  }

  fs.writeFileSync(outputFile, JSON.stringify(report, null, 2), 'utf8');
  console.log(`📊 Regression report saved: ${outputFile}`);
}

// Main execution
if (require.main === module) {
  const args = process.argv.slice(2);

  if (args.length < 2) {
    console.error('Usage: node detect-regression.js <baseline.json> <current.json> [threshold%]');
    console.error('Example: node detect-regression.js baseline.json current.json 10');
    process.exit(1);
  }

  const baselineFile = args[0];
  const currentFile = args[1];
  const threshold = args[2] ? parseFloat(args[2]) / 100 : REGRESSION_THRESHOLD;

  if (!fs.existsSync(baselineFile)) {
    console.error(`❌ Baseline file not found: ${baselineFile}`);
    process.exit(1);
  }

  if (!fs.existsSync(currentFile)) {
    console.error(`❌ Current file not found: ${currentFile}`);
    process.exit(1);
  }

  // Generate comparison report
  const reportFile = currentFile.replace('.json', '-regression.json');
  generateRegressionReport(baselineFile, currentFile, reportFile);

  // Detect regressions
  detectRegression(baselineFile, currentFile, threshold);
}

module.exports = { detectRegression, generateRegressionReport };
