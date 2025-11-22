/**
 * HTML Report Generator for k6 Results
 *
 * Converts JSON summary to HTML dashboard
 *
 * Usage: node utils/generate-report.js [json-file]
 */

const fs = require('fs');
const path = require('path');

function generateHtmlReport(jsonData, outputPath) {
  const metrics = jsonData.metrics;
  const timestamp = new Date().toISOString();

  const html = `
<!DOCTYPE html>
<html lang="en">
<head>
  <meta charset="UTF-8">
  <meta name="viewport" content="width=device-width, initial-scale=1.0">
  <title>MeepleAI Performance Test Report</title>
  <style>
    * {
      margin: 0;
      padding: 0;
      box-sizing: border-box;
    }

    body {
      font-family: -apple-system, BlinkMacSystemFont, 'Segoe UI', Roboto, Oxygen, Ubuntu, Cantarell, sans-serif;
      background: #f5f7fa;
      color: #2c3e50;
      padding: 20px;
    }

    .container {
      max-width: 1200px;
      margin: 0 auto;
    }

    .header {
      background: linear-gradient(135deg, #667eea 0%, #764ba2 100%);
      color: white;
      padding: 40px;
      border-radius: 12px;
      margin-bottom: 30px;
      box-shadow: 0 10px 30px rgba(0,0,0,0.1);
    }

    .header h1 {
      font-size: 32px;
      margin-bottom: 10px;
    }

    .header p {
      opacity: 0.9;
      font-size: 16px;
    }

    .stats-grid {
      display: grid;
      grid-template-columns: repeat(auto-fit, minmax(250px, 1fr));
      gap: 20px;
      margin-bottom: 30px;
    }

    .stat-card {
      background: white;
      padding: 24px;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
      border-left: 4px solid #667eea;
    }

    .stat-card.success {
      border-left-color: #10b981;
    }

    .stat-card.warning {
      border-left-color: #f59e0b;
    }

    .stat-card.error {
      border-left-color: #ef4444;
    }

    .stat-label {
      font-size: 14px;
      color: #6b7280;
      margin-bottom: 8px;
      text-transform: uppercase;
      letter-spacing: 0.5px;
    }

    .stat-value {
      font-size: 32px;
      font-weight: bold;
      color: #1f2937;
    }

    .stat-unit {
      font-size: 16px;
      color: #6b7280;
      margin-left: 4px;
    }

    .section {
      background: white;
      padding: 30px;
      border-radius: 8px;
      box-shadow: 0 2px 8px rgba(0,0,0,0.1);
      margin-bottom: 20px;
    }

    .section h2 {
      font-size: 24px;
      margin-bottom: 20px;
      color: #1f2937;
      border-bottom: 2px solid #e5e7eb;
      padding-bottom: 10px;
    }

    .table {
      width: 100%;
      border-collapse: collapse;
    }

    .table th {
      text-align: left;
      padding: 12px;
      background: #f9fafb;
      border-bottom: 2px solid #e5e7eb;
      font-weight: 600;
      color: #374151;
    }

    .table td {
      padding: 12px;
      border-bottom: 1px solid #e5e7eb;
    }

    .table tr:hover {
      background: #f9fafb;
    }

    .status-badge {
      display: inline-block;
      padding: 4px 12px;
      border-radius: 12px;
      font-size: 12px;
      font-weight: 600;
    }

    .status-badge.pass {
      background: #d1fae5;
      color: #065f46;
    }

    .status-badge.fail {
      background: #fee2e2;
      color: #991b1b;
    }

    .footer {
      text-align: center;
      margin-top: 40px;
      color: #6b7280;
      font-size: 14px;
    }

    .chart-placeholder {
      background: #f9fafb;
      padding: 40px;
      text-align: center;
      border-radius: 8px;
      color: #6b7280;
    }
  </style>
</head>
<body>
  <div class="container">
    <div class="header">
      <h1>🚀 MeepleAI Performance Test Report</h1>
      <p>Generated: ${timestamp}</p>
    </div>

    <div class="stats-grid">
      <div class="stat-card">
        <div class="stat-label">Total Requests</div>
        <div class="stat-value">${(metrics.http_reqs?.values.count || 0).toLocaleString()}</div>
      </div>

      <div class="stat-card ${(metrics.http_req_failed?.values.rate || 0) < 0.01 ? 'success' : 'error'}">
        <div class="stat-label">Error Rate</div>
        <div class="stat-value">${((metrics.http_req_failed?.values.rate || 0) * 100).toFixed(2)}<span class="stat-unit">%</span></div>
      </div>

      <div class="stat-card">
        <div class="stat-label">Avg Response Time</div>
        <div class="stat-value">${(metrics.http_req_duration?.values.avg || 0).toFixed(0)}<span class="stat-unit">ms</span></div>
      </div>

      <div class="stat-card">
        <div class="stat-label">P95 Response Time</div>
        <div class="stat-value">${(metrics.http_req_duration?.values['p(95)'] || 0).toFixed(0)}<span class="stat-unit">ms</span></div>
      </div>

      <div class="stat-card">
        <div class="stat-label">Throughput</div>
        <div class="stat-value">${(metrics.http_reqs?.values.rate || 0).toFixed(1)}<span class="stat-unit">req/s</span></div>
      </div>

      <div class="stat-card">
        <div class="stat-label">Data Transferred</div>
        <div class="stat-value">${formatBytes(metrics.data_received?.values.count || 0)}</div>
      </div>
    </div>

    <div class="section">
      <h2>⏱️ Response Time Breakdown</h2>
      <table class="table">
        <thead>
          <tr>
            <th>Metric</th>
            <th>Value</th>
          </tr>
        </thead>
        <tbody>
          <tr>
            <td>Minimum</td>
            <td>${(metrics.http_req_duration?.values.min || 0).toFixed(2)}ms</td>
          </tr>
          <tr>
            <td>Average</td>
            <td>${(metrics.http_req_duration?.values.avg || 0).toFixed(2)}ms</td>
          </tr>
          <tr>
            <td>Median (P50)</td>
            <td>${(metrics.http_req_duration?.values['p(50)'] || 0).toFixed(2)}ms</td>
          </tr>
          <tr>
            <td>P90</td>
            <td>${(metrics.http_req_duration?.values['p(90)'] || 0).toFixed(2)}ms</td>
          </tr>
          <tr>
            <td>P95</td>
            <td>${(metrics.http_req_duration?.values['p(95)'] || 0).toFixed(2)}ms</td>
          </tr>
          <tr>
            <td>P99</td>
            <td>${(metrics.http_req_duration?.values['p(99)'] || 0).toFixed(2)}ms</td>
          </tr>
          <tr>
            <td>Maximum</td>
            <td>${(metrics.http_req_duration?.values.max || 0).toFixed(2)}ms</td>
          </tr>
        </tbody>
      </table>
    </div>

    <div class="section">
      <h2>🎯 Endpoint Performance</h2>
      <table class="table">
        <thead>
          <tr>
            <th>Endpoint</th>
            <th>P95 Latency</th>
            <th>Threshold</th>
            <th>Status</th>
          </tr>
        </thead>
        <tbody>
          ${generateEndpointRows(metrics)}
        </tbody>
      </table>
    </div>

    <div class="section">
      <h2>📊 Custom Metrics</h2>
      <table class="table">
        <thead>
          <tr>
            <th>Metric</th>
            <th>Value</th>
          </tr>
        </thead>
        <tbody>
          ${generateCustomMetricsRows(metrics)}
        </tbody>
      </table>
    </div>

    <div class="footer">
      <p>MeepleAI Performance Test Suite | Issue #873</p>
      <p>Powered by k6</p>
    </div>
  </div>
</body>
</html>
  `;

  fs.writeFileSync(outputPath, html, 'utf8');
  console.log(`✅ HTML report generated: ${outputPath}`);
}

function generateEndpointRows(metrics) {
  const endpoints = [
    { name: 'RAG Search', tag: 'rag-search', threshold: 2000 },
    { name: 'Chat', tag: 'chat', threshold: 1000 },
    { name: 'Games', tag: 'games', threshold: 500 },
    { name: 'Sessions', tag: 'sessions', threshold: 100 },
  ];

  return endpoints.map(endpoint => {
    const metricKey = `http_req_duration{endpoint:${endpoint.tag}}`;
    const endpointMetric = metrics[metricKey];

    if (!endpointMetric) {
      return `
        <tr>
          <td>${endpoint.name}</td>
          <td colspan="3">No data</td>
        </tr>
      `;
    }

    const p95 = endpointMetric.values['p(95)'] || 0;
    const status = p95 < endpoint.threshold ? 'pass' : 'fail';

    return `
      <tr>
        <td>${endpoint.name}</td>
        <td>${p95.toFixed(2)}ms</td>
        <td>${endpoint.threshold}ms</td>
        <td><span class="status-badge ${status}">${status.toUpperCase()}</span></td>
      </tr>
    `;
  }).join('');
}

function generateCustomMetricsRows(metrics) {
  const customMetrics = [
    { key: 'rag_confidence', label: 'RAG Confidence (avg)', format: (v) => v.toFixed(3) },
    { key: 'rag_snippet_count', label: 'RAG Snippets (avg)', format: (v) => v.toFixed(1) },
    { key: 'rag_tokens', label: 'RAG Tokens (avg)', format: (v) => v.toFixed(0) },
    { key: 'cache_hit_rate', label: 'Cache Hit Rate', format: (v) => `${(v * 100).toFixed(2)}%` },
    { key: 'cache_operations_total', label: 'Cache Operations', format: (v) => v.toLocaleString() },
    { key: 'db_query_time', label: 'DB Query Time (avg)', format: (v) => `${v.toFixed(2)}ms` },
  ];

  return customMetrics.map(metric => {
    const metricData = metrics[metric.key];
    if (!metricData) return '';

    const value = metricData.values.avg || metricData.values.rate || metricData.values.count || 0;

    return `
      <tr>
        <td>${metric.label}</td>
        <td>${metric.format(value)}</td>
      </tr>
    `;
  }).filter(Boolean).join('');
}

function formatBytes(bytes) {
  if (bytes < 1024) return bytes + ' B';
  if (bytes < 1024 * 1024) return (bytes / 1024).toFixed(2) + ' KB';
  if (bytes < 1024 * 1024 * 1024) return (bytes / (1024 * 1024)).toFixed(2) + ' MB';
  return (bytes / (1024 * 1024 * 1024)).toFixed(2) + ' GB';
}

// Main execution
if (require.main === module) {
  const args = process.argv.slice(2);
  const inputFile = args[0] || path.join(__dirname, '../reports/summary.json');

  if (!fs.existsSync(inputFile)) {
    console.error(`❌ File not found: ${inputFile}`);
    console.log('Usage: node utils/generate-report.js [json-file]');
    process.exit(1);
  }

  const jsonData = JSON.parse(fs.readFileSync(inputFile, 'utf8'));
  const outputPath = inputFile.replace('.json', '.html');

  generateHtmlReport(jsonData, outputPath);
}

module.exports = { generateHtmlReport };
