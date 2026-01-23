/**
 * ISSUE-2918: K6 Performance Report Generator
 *
 * Generates HTML report from K6 summary.json output
 * Usage: node utils/generate-report.js reports/summary.json
 */

const fs = require('fs');
const path = require('path');

// ANSI color codes for terminal output
const colors = {
  reset: '\x1b[0m',
  green: '\x1b[32m',
  red: '\x1b[31m',
  yellow: '\x1b[33m',
  blue: '\x1b[34m',
  cyan: '\x1b[36m',
};

/**
 * Format duration from milliseconds to human-readable string
 */
function formatDuration(ms) {
  if (ms < 1000) return `${ms.toFixed(2)}ms`;
  if (ms < 60000) return `${(ms / 1000).toFixed(2)}s`;
  return `${(ms / 60000).toFixed(2)}m`;
}

/**
 * Format bytes to human-readable string
 */
function formatBytes(bytes) {
  if (bytes < 1024) return `${bytes}B`;
  if (bytes < 1024 * 1024) return `${(bytes / 1024).toFixed(2)}KB`;
  return `${(bytes / (1024 * 1024)).toFixed(2)}MB`;
}

/**
 * Format number with thousands separator
 */
function formatNumber(num) {
  return num.toString().replace(/\B(?=(\d{3})+(?!\d))/g, ',');
}

/**
 * Determine status color based on metric value and threshold
 */
function getStatusColor(value, threshold, isErrorRate = false) {
  if (isErrorRate) {
    return value < threshold ? 'green' : 'red';
  }
  return value < threshold ? 'green' : 'red';
}

/**
 * Generate HTML report from K6 summary data
 */
function generateHtmlReport(data) {
  const timestamp = new Date().toISOString();
  const metrics = data.metrics || {};

  // Extract key metrics
  const httpReqDuration = metrics['http_req_duration'] || {};
  const httpReqFailed = metrics['http_req_failed'] || {};
  const checks = metrics['checks'] || {};
  const pollingLatency = metrics['polling_latency'] || {};
  const cacheHitRate = metrics['cache_hit_rate'] || {};
  const authErrors = metrics['auth_errors'] || {};
  const endpointErrors = metrics['endpoint_errors'] || {};

  // Calculate statistics
  const p95 = httpReqDuration.values?.['p(95)'] || 0;
  const p99 = httpReqDuration.values?.['p(99)'] || 0;
  const errorRate = httpReqFailed.values?.rate || 0;
  const checkRate = checks.values?.rate || 0;
  const cacheRate = cacheHitRate.values?.rate || 0;

  // Determine pass/fail status
  const p95Pass = p95 < 500;
  const p99Pass = p99 < 1000;
  const errorPass = errorRate < 0.01;
  const checkPass = checkRate > 0.95;
  const cachePass = cacheRate > 0.7;

  const allPassed = p95Pass && p99Pass && errorPass && checkPass;

  const html = `
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>K6 Performance Report - Admin Polling</title>
  <style>
    * {
      margin: 0;
      padding: 0;
      box-sizing: border-box;
    }

    body {
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, 'Helvetica Neue', Arial, sans-serif;
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      min-height: 100vh;
      padding: 2rem;
    }

    .container {
      max-width: 1200px;
      margin: 0 auto;
      background: white;
      border-radius: 12px;
      box-shadow: 0 20px 60px rgba(0, 0, 0, 0.3);
      overflow: hidden;
    }

    .header {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      padding: 2rem;
      text-align: center;
    }

    .header h1 {
      font-size: 2rem;
      margin-bottom: 0.5rem;
    }

    .header p {
      opacity: 0.9;
      font-size: 0.9rem;
    }

    .status-badge {
      display: inline-block;
      padding: 0.5rem 1rem;
      border-radius: 20px;
      font-weight: bold;
      font-size: 1.1rem;
      margin-top: 1rem;
    }

    .status-pass {
      background: #10b981;
      color: white;
    }

    .status-fail {
      background: #ef4444;
      color: white;
    }

    .content {
      padding: 2rem;
    }

    .metric-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 1.5rem;
      margin-bottom: 2rem;
    }

    .metric-card {
      background: #f9fafb;
      padding: 1.5rem;
      border-radius: 8px;
      border-left: 4px solid #667eea;
    }

    .metric-card.pass {
      border-left-color: #10b981;
    }

    .metric-card.fail {
      border-left-color: #ef4444;
    }

    .metric-label {
      font-size: 0.85rem;
      color: #6b7280;
      margin-bottom: 0.5rem;
      text-transform: uppercase;
      letter-spacing: 0.05em;
    }

    .metric-value {
      font-size: 2rem;
      font-weight: bold;
      color: #1f2937;
      margin-bottom: 0.25rem;
    }

    .metric-threshold {
      font-size: 0.85rem;
      color: #9ca3af;
    }

    .metric-threshold.pass {
      color: #10b981;
    }

    .metric-threshold.fail {
      color: #ef4444;
    }

    .section-title {
      font-size: 1.5rem;
      font-weight: bold;
      color: #1f2937;
      margin-bottom: 1rem;
      padding-bottom: 0.5rem;
      border-bottom: 2px solid #e5e7eb;
    }

    .details-table {
      width: 100%;
      border-collapse: collapse;
      margin-bottom: 2rem;
    }

    .details-table th {
      background: #f3f4f6;
      padding: 0.75rem;
      text-align: left;
      font-weight: 600;
      color: #374151;
      border-bottom: 2px solid #e5e7eb;
    }

    .details-table td {
      padding: 0.75rem;
      border-bottom: 1px solid #e5e7eb;
    }

    .details-table tr:hover {
      background: #f9fafb;
    }

    .footer {
      background: #f3f4f6;
      padding: 1rem 2rem;
      text-align: center;
      color: #6b7280;
      font-size: 0.85rem;
    }

    @media (max-width: 768px) {
      .metric-grid {
        grid-template-columns: 1fr;
      }

      .header h1 {
        font-size: 1.5rem;
      }

      .metric-value {
        font-size: 1.5rem;
      }
    }
  </style>
</head>
<body>
  <div class="container">
    <div class="header">
      <h1>🎯 K6 Performance Report</h1>
      <p>Admin Dashboard Load Testing - Issue #2918</p>
      <p>Generated: ${timestamp}</p>
      <div class="status-badge ${allPassed ? 'status-pass' : 'status-fail'}">
        ${allPassed ? '✅ All Checks Passed' : '❌ Some Checks Failed'}
      </div>
    </div>

    <div class="content">
      <h2 class="section-title">📊 Key Performance Metrics</h2>
      <div class="metric-grid">
        <div class="metric-card ${p95Pass ? 'pass' : 'fail'}">
          <div class="metric-label">Response Time (p95)</div>
          <div class="metric-value">${formatDuration(p95)}</div>
          <div class="metric-threshold ${p95Pass ? 'pass' : 'fail'}">
            Target: &lt; 500ms ${p95Pass ? '✓' : '✗'}
          </div>
        </div>

        <div class="metric-card ${p99Pass ? 'pass' : 'fail'}">
          <div class="metric-label">Response Time (p99)</div>
          <div class="metric-value">${formatDuration(p99)}</div>
          <div class="metric-threshold ${p99Pass ? 'pass' : 'fail'}">
            Target: &lt; 1s ${p99Pass ? '✓' : '✗'}
          </div>
        </div>

        <div class="metric-card ${errorPass ? 'pass' : 'fail'}">
          <div class="metric-label">Error Rate</div>
          <div class="metric-value">${(errorRate * 100).toFixed(2)}%</div>
          <div class="metric-threshold ${errorPass ? 'pass' : 'fail'}">
            Target: &lt; 1% ${errorPass ? '✓' : '✗'}
          </div>
        </div>

        <div class="metric-card ${checkPass ? 'pass' : 'fail'}">
          <div class="metric-label">Check Success Rate</div>
          <div class="metric-value">${(checkRate * 100).toFixed(2)}%</div>
          <div class="metric-threshold ${checkPass ? 'pass' : 'fail'}">
            Target: &gt; 95% ${checkPass ? '✓' : '✗'}
          </div>
        </div>

        <div class="metric-card ${cachePass ? 'pass' : 'fail'}">
          <div class="metric-label">Cache Hit Rate</div>
          <div class="metric-value">${(cacheRate * 100).toFixed(2)}%</div>
          <div class="metric-threshold ${cachePass ? 'pass' : 'fail'}">
            Target: &gt; 70% ${cachePass ? '✓' : '✗'}
          </div>
        </div>

        <div class="metric-card">
          <div class="metric-label">Auth Errors</div>
          <div class="metric-value">${authErrors.values?.count || 0}</div>
          <div class="metric-threshold">Total failures</div>
        </div>
      </div>

      <h2 class="section-title">📈 Detailed Statistics</h2>
      <table class="details-table">
        <thead>
          <tr>
            <th>Metric</th>
            <th>Average</th>
            <th>Min</th>
            <th>Median</th>
            <th>Max</th>
            <th>p(90)</th>
            <th>p(95)</th>
            <th>p(99)</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td><strong>HTTP Request Duration</strong></td>
            <td>${formatDuration(httpReqDuration.values?.avg || 0)}</td>
            <td>${formatDuration(httpReqDuration.values?.min || 0)}</td>
            <td>${formatDuration(httpReqDuration.values?.med || 0)}</td>
            <td>${formatDuration(httpReqDuration.values?.max || 0)}</td>
            <td>${formatDuration(httpReqDuration.values?.['p(90)'] || 0)}</td>
            <td>${formatDuration(httpReqDuration.values?.['p(95)'] || 0)}</td>
            <td>${formatDuration(httpReqDuration.values?.['p(99)'] || 0)}</td>
          </tr>
          <tr>
            <td><strong>Polling Latency</strong></td>
            <td>${formatDuration(pollingLatency.values?.avg || 0)}</td>
            <td>${formatDuration(pollingLatency.values?.min || 0)}</td>
            <td>${formatDuration(pollingLatency.values?.med || 0)}</td>
            <td>${formatDuration(pollingLatency.values?.max || 0)}</td>
            <td>${formatDuration(pollingLatency.values?.['p(90)'] || 0)}</td>
            <td>${formatDuration(pollingLatency.values?.['p(95)'] || 0)}</td>
            <td>${formatDuration(pollingLatency.values?.['p(99)'] || 0)}</td>
          </tr>
        </tbody>
      </table>

      <h2 class="section-title">🔍 Test Summary</h2>
      <table class="details-table">
        <thead>
          <tr>
            <th>Aspect</th>
            <th>Value</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Total HTTP Requests</td>
            <td>${formatNumber(httpReqDuration.values?.count || 0)}</td>
          </tr>
          <tr>
            <td>Failed Requests</td>
            <td>${formatNumber((httpReqFailed.values?.count || 0))}</td>
          </tr>
          <tr>
            <td>Total Checks</td>
            <td>${formatNumber(checks.values?.passes + checks.values?.fails || 0)}</td>
          </tr>
          <tr>
            <td>Check Passes</td>
            <td>${formatNumber(checks.values?.passes || 0)}</td>
          </tr>
          <tr>
            <td>Check Failures</td>
            <td>${formatNumber(checks.values?.fails || 0)}</td>
          </tr>
          <tr>
            <td>Authentication Errors</td>
            <td>${authErrors.values?.count || 0}</td>
          </tr>
          <tr>
            <td>Endpoint Errors</td>
            <td>${endpointErrors.values?.count || 0}</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="footer">
      <p>Generated by K6 Performance Test Suite | MeepleAI Monorepo</p>
      <p>Issue #2918: Admin Dashboard Load Testing</p>
    </div>
  </div>
</body>
</html>
  `;

  return html;
}

/**
 * Main execution
 */
function main() {
  const args = process.argv.slice(2);

  if (args.length === 0) {
    console.error(`${colors.red}Error: Missing summary.json path${colors.reset}`);
    console.log(`${colors.cyan}Usage: node utils/generate-report.js reports/summary.json${colors.reset}`);
    process.exit(1);
  }

  const summaryPath = path.resolve(args[0]);

  if (!fs.existsSync(summaryPath)) {
    console.error(`${colors.red}Error: File not found: ${summaryPath}${colors.reset}`);
    process.exit(1);
  }

  console.log(`${colors.cyan}📊 Generating K6 Performance Report${colors.reset}`);
  console.log(`${colors.blue}Input: ${summaryPath}${colors.reset}`);

  try {
    // Read summary JSON
    const summaryData = JSON.parse(fs.readFileSync(summaryPath, 'utf-8'));

    // Generate HTML report
    const html = generateHtmlReport(summaryData);

    // Write HTML report
    const reportDir = path.dirname(summaryPath);
    const reportPath = path.join(reportDir, 'performance-report.html');
    fs.writeFileSync(reportPath, html, 'utf-8');

    console.log(`${colors.green}✅ Report generated successfully${colors.reset}`);
    console.log(`${colors.blue}Output: ${reportPath}${colors.reset}`);

    // Print summary to console
    const metrics = summaryData.metrics || {};
    const httpReqDuration = metrics['http_req_duration'] || {};
    const httpReqFailed = metrics['http_req_failed'] || {};

    console.log('\n' + '='.repeat(60));
    console.log(`${colors.cyan}Performance Summary${colors.reset}`);
    console.log('='.repeat(60));
    console.log(`p95 Response Time: ${formatDuration(httpReqDuration.values?.['p(95)'] || 0)}`);
    console.log(`p99 Response Time: ${formatDuration(httpReqDuration.values?.['p(99)'] || 0)}`);
    console.log(`Error Rate: ${((httpReqFailed.values?.rate || 0) * 100).toFixed(2)}%`);
    console.log('='.repeat(60) + '\n');
  } catch (error) {
    console.error(`${colors.red}Error generating report: ${error.message}${colors.reset}`);
    process.exit(1);
  }
}

// Run if executed directly
if (require.main === module) {
  main();
}

module.exports = { generateHtmlReport, formatDuration, formatBytes, formatNumber };
