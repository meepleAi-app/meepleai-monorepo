// Shared configuration for load tests

export const config = {
  // Base API URL - can be overridden via environment variable
  baseUrl: __ENV.API_BASE_URL || 'http://localhost:8080',

  // Test user credentials (using demo users from DB-02 seed data)
  testUser: {
    email: 'user@meepleai.dev',
    password: 'Demo123!',
  },

  // Performance targets from TEST-04 requirements
  thresholds: {
    games: {
      users100: {
        http_req_duration: ['p(95)<200'], // 95th percentile <200ms
        http_req_failed: ['rate<0.001'],   // error rate <0.1%
      },
      users500: {
        http_req_duration: ['p(95)<500'], // 95th percentile <500ms
        http_req_failed: ['rate<0.001'],   // error rate <0.1%
      },
    },
    chat: {
      users100: {
        http_req_duration: ['p(95)<300'], // 95th percentile <300ms
        http_req_failed: ['rate<0.01'],    // error rate <1%
      },
      users500: {
        http_req_duration: ['p(95)<800'], // 95th percentile <800ms
        http_req_failed: ['rate<0.01'],    // error rate <1%
      },
    },
    qa: {
      users100: {
        http_req_duration: ['p(95)<500'], // 95th percentile <500ms
        http_req_failed: ['rate<0.01'],    // error rate <1%
      },
      users500: {
        http_req_duration: ['p(95)<1000'], // 95th percentile <1s
        http_req_failed: ['rate<0.01'],     // error rate <1%
      },
    },
    // AI-11.3: Quality scoring performance targets
    qualityScoring: {
      baseline: {
        http_req_duration: ['p(95)<400'], // Baseline without quality overhead
        http_req_failed: ['rate<0.05'],   // error rate <5%
      },
      users100: {
        http_req_duration: ['p(50)<500', 'p(95)<700', 'p(99)<1200'], // With quality overhead
        http_req_failed: ['rate<0.05'],   // error rate <5%
        'quality_score_present': ['rate>0.90'], // 90%+ responses should have scores
      },
      users500: {
        http_req_duration: ['p(50)<800', 'p(95)<1500', 'p(99)<2500'], // Stress test margins
        http_req_failed: ['rate<0.05'],   // error rate <5%
        'quality_score_present': ['rate>0.85'], // 85%+ under stress
      },
    },
  },

  // Load scenarios configuration
  scenarios: {
    // Scenario for baseline testing (AI-11.3: quality scoring disabled)
    baseline: {
      executor: 'ramping-vus',
      stages: [
        { duration: '30s', target: 50 },   // ramp up to 50 users
        { duration: '2m', target: 50 },    // maintain 50 users
        { duration: '30s', target: 0 },    // ramp down
      ],
    },
    // Scenario for 100 concurrent users
    users100: {
      executor: 'ramping-vus',
      stages: [
        { duration: '30s', target: 100 },  // ramp up to 100 users
        { duration: '2m', target: 100 },   // maintain 100 users
        { duration: '30s', target: 0 },    // ramp down
      ],
    },
    // Scenario for 500 concurrent users
    users500: {
      executor: 'ramping-vus',
      stages: [
        { duration: '1m', target: 500 },   // ramp up to 500 users
        { duration: '3m', target: 500 },   // maintain 500 users
        { duration: '1m', target: 0 },     // ramp down
      ],
    },
    // Scenario for 1000 concurrent users (stress test)
    users1000: {
      executor: 'ramping-vus',
      stages: [
        { duration: '2m', target: 1000 },  // ramp up to 1000 users
        { duration: '3m', target: 1000 },  // maintain 1000 users
        { duration: '1m', target: 0 },     // ramp down
      ],
    },
  },

  // Demo game IDs from seed data (DB-02)
  testData: {
    games: {
      chess: 'chess-uuid-will-be-fetched-dynamically',
      ticTacToe: 'tictactoe-uuid-will-be-fetched-dynamically',
    },
    queries: {
      chess: [
        'How do I set up a chess game?',
        'What are the basic rules of chess?',
        'How does the knight move?',
        'What is en passant?',
        'How do I castle in chess?',
        'What is checkmate?',
      ],
      ticTacToe: [
        'How do I play tic-tac-toe?',
        'What are the rules of tic-tac-toe?',
        'How do you win at tic-tac-toe?',
      ],
    },
  },
};

// HTML report summary template
export const htmlReportSummary = {
  enabled: true,
  fileName: 'load-test-report.html',
};
