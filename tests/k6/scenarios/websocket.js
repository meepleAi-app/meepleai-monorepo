/**
 * WebSocket Load Testing
 *
 * Target: 1000+ concurrent connections
 * Tests connection stability and message throughput
 *
 * Issue #873
 */

import { sleep } from 'k6';
import ws from 'k6/ws';
import { check } from 'k6';
import { loadConfig, getTestType } from '../utils/common.js';
import { setupTestUser, teardownTestUser } from '../utils/auth.js';
import { recordWsMetrics, wsErrors } from '../utils/metrics.js';

const config = loadConfig();
const testType = getTestType();

export const options = {
  scenarios: {
    // Gradual ramp-up to 1000+ connections
    websocket_load: {
      executor: 'ramping-vus',
      startVUs: 10,
      stages: [
        { duration: '2m', target: 100 },
        { duration: '2m', target: 500 },
        { duration: '2m', target: 1000 },
        { duration: '3m', target: 1000 }, // Hold at 1000
        { duration: '2m', target: 100 },
        { duration: '1m', target: 0 },
      ],
      gracefulRampDown: '30s',
    },
  },

  thresholds: {
    'ws_connection_duration': ['p(95)<5000'], // Connection should be stable
    'ws_message_rate': ['rate>0.5'], // At least 50% of connections send messages
    'ws_errors_total': ['count<100'], // Less than 100 total errors
  },

  tags: {
    test_name: 'websocket',
  },
};

export function setup() {
  return setupTestUser(config.apiBaseUrl, config.testUser.email, config.testUser.password);
}

export default function (data) {
  const wsUrl = config.websocketUrl;
  const sessionToken = data.sessionToken;

  let messageCount = 0;
  let hasError = false;
  const startTime = Date.now();

  const res = ws.connect(wsUrl, {
    headers: {
      'Cookie': `meepleai-session=${sessionToken}`
    },
    tags: { endpoint: 'websocket' },
  }, function (socket) {
    socket.on('open', () => {
      console.log(`VU ${__VU}: WebSocket connection established`);

      // Send ping every 10 seconds to keep connection alive
      socket.setInterval(() => {
        socket.send(JSON.stringify({ type: 'ping' }));
      }, 10000);

      // Send test messages
      for (let i = 0; i < 5; i++) {
        socket.send(JSON.stringify({
          type: 'test',
          data: `Test message ${i} from VU ${__VU}`,
        }));
        messageCount++;
      }
    });

    socket.on('message', (msg) => {
      try {
        const data = JSON.parse(msg);
        check(data, {
          'message has type': (d) => d.type !== undefined,
        });
      } catch (e) {
        console.error(`VU ${__VU}: Failed to parse message: ${e}`);
        hasError = true;
      }
    });

    socket.on('error', (e) => {
      console.error(`VU ${__VU}: WebSocket error: ${e}`);
      hasError = true;
      wsErrors.add(1);
    });

    socket.on('close', () => {
      console.log(`VU ${__VU}: WebSocket connection closed`);
    });

    // Keep connection open for 30 seconds
    socket.setTimeout(() => {
      socket.close();
    }, 30000);
  });

  const connectionDuration = Date.now() - startTime;

  check(res, {
    'WebSocket connection established': (r) => r && r.status === 101,
  });

  // Record metrics
  recordWsMetrics(connectionDuration, messageCount, hasError);

  sleep(1);
}

export function teardown(data) {
  teardownTestUser(config.apiBaseUrl, data.sessionToken);
}

export function handleSummary(data) {
  return {
    'reports/websocket-summary.json': JSON.stringify(data, null, 2),
    stdout: textSummary(data, { indent: '  ' }),
  };
}

function textSummary(data, options) {
  const indent = options?.indent || '';

  let summary = `\n${indent}WebSocket Load Test Summary\n`;
  summary += `${indent}===========================\n\n`;

  const metrics = data.metrics;

  summary += `${indent}Connection Stats:\n`;
  summary += `${indent}  Avg Duration: ${(metrics.ws_connection_duration?.values.avg || 0).toFixed(2)}ms\n`;
  summary += `${indent}  P95 Duration: ${(metrics.ws_connection_duration?.values['p(95)'] || 0).toFixed(2)}ms\n`;
  summary += `${indent}  Message Rate: ${((metrics.ws_message_rate?.values.rate || 0) * 100).toFixed(2)}%\n`;
  summary += `${indent}  Total Errors: ${metrics.ws_errors_total?.values.count || 0}\n\n`;

  summary += `${indent}Thresholds:\n`;
  const p95Threshold = 5000;
  const p95Value = metrics.ws_connection_duration?.values['p(95)'] || 0;
  const p95Status = p95Value < p95Threshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  P95 < 5000ms: ${p95Value.toFixed(2)}ms ${p95Status}\n`;

  const messageRateThreshold = 0.5;
  const messageRate = metrics.ws_message_rate?.values.rate || 0;
  const messageRateStatus = messageRate > messageRateThreshold ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  Message Rate > 50%: ${(messageRate * 100).toFixed(2)}% ${messageRateStatus}\n`;

  const errorCount = metrics.ws_errors_total?.values.count || 0;
  const errorStatus = errorCount < 100 ? '✓ PASS' : '✗ FAIL';
  summary += `${indent}  Errors < 100: ${errorCount} ${errorStatus}\n`;

  return summary;
}
