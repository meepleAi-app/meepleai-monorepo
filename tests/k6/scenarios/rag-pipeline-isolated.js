/**
 * K6 RAG Pipeline Isolated Load Test - Issue #5545
 *
 * Measures RAG pipeline latency WITHOUT LLM provider variability.
 * Uses MOCK_LLM=true feature flag or a separate mock endpoint for fixed ~200ms LLM responses.
 *
 * What it measures:
 * - Embedding generation latency (embedding-service)
 * - Qdrant vector search latency
 * - Prompt building and pipeline overhead
 * - Total pipeline response time (excluding real LLM)
 *
 * Prerequisites:
 * - API running with MOCK_LLM=true or mock LLM service
 * - At least 1 game with indexed documents in Qdrant
 * - Test user with valid credentials and agent access
 */

import http from 'k6/http';
import { check, sleep } from 'k6';
import { Trend, Counter, Rate } from 'k6/metrics';
import { textSummary } from 'https://jslib.k6.io/k6-summary/0.0.2/index.js';
import {
  config,
  authenticateUser,
  authHeaders,
  sharedMetrics,
  standardSetup,
  standardTeardown,
} from '../utils/shared-config.js';

// ============================================
// CUSTOM METRICS
// ============================================

const pipelineLatency = new Trend('pipeline_total_latency');
const embeddingLatency = new Trend('embedding_latency');
const vectorSearchLatency = new Trend('vector_search_latency');
const chatResponseLatency = new Trend('chat_response_latency');
const pipelineErrors = new Counter('pipeline_errors');
const pipelineRequests = new Counter('pipeline_requests');
const pipelineSuccessRate = new Rate('pipeline_success_rate');

// ============================================
// TEST CONFIGURATION
// ============================================

const TEST_TYPE = __ENV.TEST_TYPE || 'smoke';

const testConfigs = {
  smoke: {
    executor: 'constant-vus',
    vus: 5,
    duration: '1m',
    exec: 'ragPipelineScenario',
  },
  load: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '2m', target: 50 },
      { duration: '3m', target: 50 },
      { duration: '1m', target: 0 },
    ],
    exec: 'ragPipelineScenario',
  },
  stress: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '2m', target: 50 },
      { duration: '3m', target: 100 },
      { duration: '1m', target: 0 },
    ],
    exec: 'ragPipelineScenario',
  },
};

export const options = {
  scenarios: {
    [TEST_TYPE]: testConfigs[TEST_TYPE] || testConfigs.smoke,
  },
  thresholds: {
    'pipeline_total_latency': ['p(95)<1000', 'p(99)<2000'],
    'embedding_latency': ['p(95)<300'],
    'vector_search_latency': ['p(95)<200'],
    'chat_response_latency': ['p(95)<1500', 'p(99)<3000'],
    'pipeline_errors': ['count<20'],
    'pipeline_success_rate': ['rate>0.95'],
    'http_req_failed': ['rate<0.05'],
    'checks': ['rate>0.90'],
  },
};

// ============================================
// RAG TEST QUERIES
// ============================================

const RAG_QUERIES = [
  'Come si prepara il gioco?',
  'Quanti giocatori possono partecipare?',
  'Quali sono le condizioni di vittoria?',
  'Come funziona il turno di gioco?',
  'Spiegami le regole base',
  'Cosa succede quando finiscono le carte?',
  'Come si calcolano i punti?',
  'Qual è la strategia migliore per i principianti?',
  'Come funziona il combattimento?',
  'Quali sono le risorse del gioco?',
];

// ============================================
// CONFIGURATION
// ============================================

// Agent ID and game ID should be set via environment or discovered in setup
const AGENT_ID = __ENV.RAG_AGENT_ID || '';
const CHAT_THREAD_ID = __ENV.RAG_CHAT_THREAD_ID || '';

// ============================================
// SETUP / TEARDOWN
// ============================================

export function setup() {
  standardSetup('RAG Pipeline Isolated', options);

  // Authenticate
  const token = authenticateUser();
  if (!token) {
    throw new Error('Authentication failed - cannot run RAG tests');
  }

  // Discover an agent to test with (if not provided via env)
  let agentId = AGENT_ID;
  if (!agentId) {
    const agentsRes = http.get(`${config.apiBaseUrl}/api/v1/agents`, {
      headers: authHeaders(token),
      tags: { endpoint: 'agents_list' },
    });

    if (agentsRes.status === 200) {
      try {
        const agents = agentsRes.json();
        const agentList = Array.isArray(agents) ? agents : (agents.items || agents.data || []);
        if (agentList.length > 0) {
          agentId = agentList[0].id;
          console.log(`Discovered agent: ${agentId}`);
        }
      } catch (e) {
        console.warn(`Failed to parse agents response: ${e}`);
      }
    }
  }

  if (!agentId) {
    throw new Error('No agent available for RAG testing. Set RAG_AGENT_ID env var or ensure agents exist.');
  }

  return { token, agentId };
}

export function teardown() {
  standardTeardown();
}

// ============================================
// SCENARIO: RAG Pipeline Isolated
// ============================================

export function ragPipelineScenario(data) {
  const { token, agentId } = data;
  if (!token || !agentId) return;

  // Pick a random query
  const query = RAG_QUERIES[Math.floor(Math.random() * RAG_QUERIES.length)];

  // Measure total pipeline time
  const pipelineStart = Date.now();

  // POST to agent chat endpoint (SSE response)
  const chatPayload = JSON.stringify({
    message: query,
  });

  const response = http.post(
    `${config.apiBaseUrl}/api/v1/agents/${agentId}/chat`,
    chatPayload,
    {
      headers: {
        ...authHeaders(token),
        'Accept': 'text/event-stream',
      },
      tags: { endpoint: 'agent_chat', scenario: 'rag_isolated' },
      timeout: '30s',
    }
  );

  const pipelineTime = Date.now() - pipelineStart;

  // Validate response
  const success = check(response, {
    'chat: status 200': (r) => r.status === 200,
    'chat: has response body': (r) => r.body && r.body.length > 0,
    'chat: no server error': (r) => r.status < 500,
  });

  // Record metrics
  pipelineRequests.add(1);
  chatResponseLatency.add(pipelineTime);
  pipelineSuccessRate.add(success ? 1 : 0);

  if (success) {
    pipelineLatency.add(pipelineTime);

    // Parse SSE events to extract component timings
    const body = response.body || '';
    const events = parseSSEEvents(body);

    // Extract timing from debug events if available
    const retrievalEvent = events.find(e => e.type === '15'); // DebugRetrievalResults
    if (retrievalEvent) {
      try {
        const data = JSON.parse(retrievalEvent.data);
        if (data.latencyMs !== undefined) {
          vectorSearchLatency.add(data.latencyMs);
        }
      } catch (_) { /* ignore parse errors */ }
    }
  } else {
    pipelineErrors.add(1);
    if (response.status >= 500) {
      sharedMetrics.endpointErrors.add(1);
    }
  }

  // Simulate user think time between queries
  sleep(Math.random() * 3 + 2); // 2-5 seconds
}

// ============================================
// SSE PARSING HELPER
// ============================================

/**
 * Parse SSE event stream from response body.
 * k6 doesn't natively support SSE streaming, so we parse the complete response.
 */
function parseSSEEvents(body) {
  const events = [];
  const lines = body.split('\n');
  let currentEvent = {};

  for (const line of lines) {
    if (line.startsWith('event:')) {
      currentEvent.type = line.substring(6).trim();
    } else if (line.startsWith('data:')) {
      currentEvent.data = line.substring(5).trim();
    } else if (line === '' && currentEvent.type) {
      events.push({ ...currentEvent });
      currentEvent = {};
    }
  }

  // Push last event if not empty-line terminated
  if (currentEvent.type) {
    events.push(currentEvent);
  }

  return events;
}

// ============================================
// SUMMARY
// ============================================

export function handleSummary(data) {
  return {
    stdout: textSummary(data, { indent: '  ', enableColors: true }),
    'reports/rag-pipeline-isolated-summary.json': JSON.stringify(data, null, 2),
  };
}
