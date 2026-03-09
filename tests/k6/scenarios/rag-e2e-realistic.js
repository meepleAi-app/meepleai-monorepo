/**
 * K6 RAG End-to-End Realistic Load Test - Issue #5545
 *
 * Measures real user experience including LLM provider latency,
 * retry/fallback behavior, and full pipeline response times.
 *
 * Uses REAL LLM providers (OpenRouter/Ollama) with conservative VU counts
 * to respect rate limits.
 *
 * What it measures:
 * - Total chat response time (TTFT proxy via full response)
 * - LLM fallback activation rate
 * - Error rate under realistic load
 * - End-to-end pipeline reliability
 *
 * Prerequisites:
 * - API running with real LLM configuration (OpenRouter or Ollama)
 * - At least 1 game with indexed documents in Qdrant
 * - Test user with valid credentials, budget, and agent access
 * - OpenRouter API key configured (or Ollama running)
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

const chatResponseLatency = new Trend('chat_response_latency');
const firstTokenLatency = new Trend('first_token_latency');
const llmLatency = new Trend('llm_latency');
const e2eErrors = new Counter('e2e_errors');
const e2eRequests = new Counter('e2e_requests');
const e2eSuccessRate = new Rate('e2e_success_rate');
const fallbackActivations = new Counter('fallback_activations');
const fallbackRate = new Rate('fallback_activation_rate');
const rateLimitHits = new Counter('rate_limit_hits');

// ============================================
// TEST CONFIGURATION
// ============================================

const TEST_TYPE = __ENV.TEST_TYPE || 'smoke';

const testConfigs = {
  smoke: {
    executor: 'constant-vus',
    vus: 3,
    duration: '1m',
    exec: 'ragE2EScenario',
  },
  load: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '2m', target: 20 },
      { duration: '5m', target: 20 },
      { duration: '1m', target: 0 },
    ],
    exec: 'ragE2EScenario',
  },
  stress: {
    executor: 'ramping-vus',
    startVUs: 0,
    stages: [
      { duration: '2m', target: 20 },
      { duration: '3m', target: 50 },
      { duration: '1m', target: 0 },
    ],
    exec: 'ragE2EScenario',
  },
};

export const options = {
  scenarios: {
    [TEST_TYPE]: testConfigs[TEST_TYPE] || testConfigs.smoke,
  },
  thresholds: {
    'chat_response_latency': ['p(95)<5000', 'p(99)<10000'],
    'first_token_latency': ['p(95)<2000'],
    'e2e_errors': ['count<10'],
    'e2e_success_rate': ['rate>0.90'],
    'fallback_activation_rate': ['rate<0.05'],
    'http_req_failed': ['rate<0.05'],
    'checks': ['rate>0.85'],
  },
};

// ============================================
// RAG TEST QUERIES (realistic user questions)
// ============================================

const RAG_QUERIES = [
  'Come si prepara il gioco?',
  'Quanti giocatori possono partecipare?',
  'Quali sono le condizioni di vittoria?',
  'Come funziona il turno di gioco?',
  'Spiegami le regole base del gioco',
  'Cosa succede quando finiscono le carte?',
  'Come si calcolano i punti alla fine della partita?',
  'Qual è la strategia migliore per i principianti?',
  'Come funziona il sistema di combattimento?',
  'Quali sono le risorse disponibili nel gioco?',
  'Come si gestiscono i conflitti tra giocatori?',
  'Quanto dura in media una partita?',
  'Ci sono espansioni disponibili per questo gioco?',
  'Come funziona la fase di costruzione?',
  'Quali sono le azioni possibili in un turno?',
];

// Conversation follow-up queries (simulate multi-turn)
const FOLLOWUP_QUERIES = [
  'Puoi spiegare meglio questo punto?',
  'E come funziona con 2 giocatori?',
  'Ci sono eccezioni a questa regola?',
  'Quali carte sono le più importanti?',
  'Come interagisce con le altre meccaniche?',
];

// ============================================
// CONFIGURATION
// ============================================

const AGENT_ID = __ENV.RAG_AGENT_ID || '';

// ============================================
// SETUP / TEARDOWN
// ============================================

export function setup() {
  standardSetup('RAG E2E Realistic', options);

  const token = authenticateUser();
  if (!token) {
    throw new Error('Authentication failed - cannot run RAG E2E tests');
  }

  // Discover an agent
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
    throw new Error('No agent available for RAG E2E testing. Set RAG_AGENT_ID env var or ensure agents exist.');
  }

  return { token, agentId };
}

export function teardown() {
  standardTeardown();
}

// ============================================
// SCENARIO: RAG E2E Realistic
// ============================================

export function ragE2EScenario(data) {
  const { token, agentId } = data;
  if (!token || !agentId) return;

  // Simulate a realistic user session: 1-3 questions
  const sessionQueries = Math.floor(Math.random() * 3) + 1;

  for (let i = 0; i < sessionQueries; i++) {
    // First query is always a new topic, follow-ups may be contextual
    const isFollowUp = i > 0 && Math.random() < 0.6;
    const queryPool = isFollowUp ? FOLLOWUP_QUERIES : RAG_QUERIES;
    const query = queryPool[Math.floor(Math.random() * queryPool.length)];

    const chatStart = Date.now();

    const response = http.post(
      `${config.apiBaseUrl}/api/v1/agents/${agentId}/chat`,
      JSON.stringify({ message: query }),
      {
        headers: {
          ...authHeaders(token),
          'Accept': 'text/event-stream',
        },
        tags: { endpoint: 'agent_chat', scenario: 'rag_e2e' },
        timeout: '60s', // Longer timeout for real LLM
      }
    );

    const chatTime = Date.now() - chatStart;

    // Track rate limit hits separately (don't count as test failures)
    if (response.status === 429) {
      rateLimitHits.add(1);
      console.warn(`Rate limited on query ${i + 1}/${sessionQueries}`);
      sleep(5); // Back off on rate limit
      continue;
    }

    const success = check(response, {
      'e2e: status 200': (r) => r.status === 200,
      'e2e: has response body': (r) => r.body && r.body.length > 0,
      'e2e: no server error': (r) => r.status < 500,
      'e2e: response within 30s': (r) => r.timings.duration < 30000,
    });

    e2eRequests.add(1);
    e2eSuccessRate.add(success ? 1 : 0);

    if (success) {
      chatResponseLatency.add(chatTime);

      // Parse SSE events for detailed metrics
      const body = response.body || '';
      const events = parseSSEEvents(body);

      // Detect model downgrade (fallback activation)
      const downgradeEvent = events.find(e => e.type === '21'); // ModelDowngrade
      if (downgradeEvent) {
        fallbackActivations.add(1);
        fallbackRate.add(1);
      } else {
        fallbackRate.add(0);
      }

      // Extract token event timing as TTFT proxy
      const tokenEvent = events.find(e => e.type === '4'); // Token
      if (tokenEvent) {
        // First token timing is approximated by time to first Token event
        // Since k6 doesn't support streaming, we use total time as proxy
        firstTokenLatency.add(chatTime);
      }

      // Extract cost/token debug info for LLM latency
      const costEvent = events.find(e => e.type === '16'); // DebugCostUpdate
      if (costEvent) {
        try {
          const costData = JSON.parse(costEvent.data);
          if (costData.totalTokens) {
            llmLatency.add(chatTime); // Approximate since we can't separate
          }
        } catch (_) { /* ignore */ }
      }
    } else {
      e2eErrors.add(1);
      if (response.status >= 500) {
        sharedMetrics.endpointErrors.add(1);
      }
    }

    // User think time between questions in a session
    if (i < sessionQueries - 1) {
      sleep(Math.random() * 5 + 3); // 3-8 seconds between questions
    }
  }

  // Think time between sessions
  sleep(Math.random() * 10 + 5); // 5-15 seconds between sessions
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
    'reports/rag-e2e-realistic-summary.json': JSON.stringify(data, null, 2),
  };
}
