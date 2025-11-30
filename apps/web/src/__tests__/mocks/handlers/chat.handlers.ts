/**
 * MSW handlers for chat and knowledge base endpoints
 *
 * Covers: /api/v1/chat/*, /api/v1/knowledge-base/* routes
 * - Chat threads, messages
 * - SSE streaming for real-time responses
 * - Knowledge base queries
 */

import { http, HttpResponse } from 'msw';
import {
  createMockChat,
  createMockChatMessage,
  createMockAgent,
} from '../../fixtures/common-fixtures';
import {
  createTokenEvent,
  createStateUpdateEvent,
  createCompleteEvent,
  createSSEResponse,
  type SSEOptions,
} from '../../fixtures/sse-test-helpers';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';

// In-memory chat store
let chats = [
  createMockChat({
    id: 'chat-1',
    gameId: 'demo-chess',
    gameName: 'Chess',
    messages: [
      createMockChatMessage({ role: 'user', content: 'How do knights move?' }),
      createMockChatMessage({ role: 'assistant', content: 'Knights move in an L-shape' }),
    ],
  }),
];

export const chatHandlers = [
  // GET /api/v1/chat/threads - List chat threads
  http.get(`${API_BASE}/api/v1/chat/threads`, () => {
    return HttpResponse.json(chats, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/chat/threads/:id - Get chat thread
  http.get(`${API_BASE}/api/v1/chat/threads/:id`, ({ params }) => {
    const { id } = params;
    const chat = chats.find((c) => c.id === id);

    if (!chat) {
      return HttpResponse.json(
        { error: 'Chat not found' },
        { status: 404 }
      );
    }

    return HttpResponse.json(chat, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/chat/threads - Create chat thread
  http.post(`${API_BASE}/api/v1/chat/threads`, async ({ request }) => {
    const body = await request.json() as { gameId: string };

    const newChat = createMockChat({
      id: `chat-${Date.now()}`,
      gameId: body.gameId,
      messages: [],
    });

    chats.push(newChat);

    return HttpResponse.json(newChat, {
      status: 201,
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/chat/ask - Non-streaming chat (basic REST)
  http.post(`${API_BASE}/api/v1/chat/ask`, async ({ request }) => {
    const body = await request.json() as { question: string; gameId: string };

    return HttpResponse.json({
      answer: `Mock answer for: ${body.question}`,
      confidence: 0.95,
      citations: [],
    }, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // POST /api/v1/knowledge-base/ask - SSE streaming endpoint (CRITICAL)
  http.post(`${API_BASE}/api/v1/knowledge-base/ask`, async ({ request }) => {
    const body = await request.json() as { question: string; gameId: string };

    // Build SSE events using centralized helpers (Issue #1495)
    const events: string[] = [];

    // Status event
    events.push(createStateUpdateEvent('Processing question...'));

    // Token events for answer
    const tokens = ['Hello', ' ', 'World', '!'];
    tokens.forEach(token => {
      events.push(createTokenEvent(token));
    });

    // Citation event (custom format for legacy compatibility)
    events.push(JSON.stringify({
      type: 'citation',
      data: {
        text: 'Test citation',
        source: 'Test source',
        page: 1,
      },
      timestamp: new Date().toISOString(),
    }));

    // Complete event
    events.push(createCompleteEvent(tokens.length, 0.95));

    // Create SSE response with default 10ms delay (prevents race conditions)
    // Use eventDelay: 0 in fast tests, 10ms for realistic timing
    const sseOptions: SSEOptions = { eventDelay: 10 };
    
    return createSSEResponse(events, sseOptions);
  }),

  // GET /api/v1/agents - List agents
  http.get(`${API_BASE}/api/v1/agents`, () => {
    const agents = [
      createMockAgent({ id: 'agent-1', gameId: 'demo-chess', name: 'Chess Expert', type: 'qa' }),
      createMockAgent({ id: 'agent-2', gameId: 'demo-tictactoe', name: 'TicTacToe Helper', type: 'qa' }),
    ];

    return HttpResponse.json(agents, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),

  // GET /api/v1/agents/:id - Get agent details
  http.get(`${API_BASE}/api/v1/agents/:id`, ({ params }) => {
    const { id } = params;
    const agent = createMockAgent({
      id: id as string,
      gameId: 'demo-chess',
      name: 'Chess Expert',
      type: 'qa'
    });

    return HttpResponse.json(agent, {
      headers: {
        'X-Correlation-Id': `test-correlation-${Date.now()}`,
      },
    });
  }),
];

// Helper to reset chat state between tests
export const resetChatState = () => {
  chats = [
    createMockChat({
      id: 'chat-1',
      gameId: 'demo-chess',
      gameName: 'Chess',
      messages: [
        createMockChatMessage({ role: 'user', content: 'How do knights move?' }),
        createMockChatMessage({ role: 'assistant', content: 'Knights move in an L-shape' }),
      ],
    }),
  ];
};