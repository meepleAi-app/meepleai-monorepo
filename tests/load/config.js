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
  },

  // Load scenarios configuration
  scenarios: {
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
