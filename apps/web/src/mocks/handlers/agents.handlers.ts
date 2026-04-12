/**
 * MSW handlers for agents endpoints (browser-safe)
 * Covers: /api/v1/agents, /api/v1/agent-typologies
 */
import { http, HttpResponse } from 'msw';

import { mockId, HANDLER_BASE } from '../data/factories';

const API_BASE = HANDLER_BASE;

interface AgentMock {
  id: string;
  name: string;
  type: string;
  strategyName: string;
  strategyParameters: Record<string, unknown>;
  isActive: boolean;
  createdAt: string;
  lastInvokedAt: string | null;
  invocationCount: number;
  isRecentlyUsed: boolean;
  isIdle: boolean;
  gameId: string | null;
  gameName: string | null;
  createdByUserId: string | null;
}

const SEED_AGENTS: AgentMock[] = [
  {
    id: mockId(501),
    name: 'Chess Tutor',
    type: 'qa',
    strategyName: 'hybrid-rag',
    strategyParameters: {},
    isActive: true,
    createdAt: new Date('2026-01-10T10:00:00Z').toISOString(),
    lastInvokedAt: new Date('2026-04-10T14:30:00Z').toISOString(),
    invocationCount: 42,
    isRecentlyUsed: true,
    isIdle: false,
    gameId: mockId(101),
    gameName: 'Chess',
    createdByUserId: null,
  },
  {
    id: mockId(502),
    name: 'Wingspan Expert',
    type: 'qa',
    strategyName: 'hybrid-rag',
    strategyParameters: {},
    isActive: true,
    createdAt: new Date('2026-02-15T10:00:00Z').toISOString(),
    lastInvokedAt: null,
    invocationCount: 0,
    isRecentlyUsed: false,
    isIdle: true,
    gameId: mockId(103),
    gameName: 'Wingspan',
    createdByUserId: null,
  },
];

const agents: AgentMock[] = SEED_AGENTS.map(a => ({ ...a }));

export const agentsHandlers = [
  http.get(`${API_BASE}/api/v1/agents`, ({ request }) => {
    const url = new URL(request.url);
    const activeOnly = url.searchParams.get('activeOnly');
    const filtered = activeOnly === 'true' ? agents.filter(a => a.isActive) : agents;
    return HttpResponse.json({
      success: true,
      agents: filtered,
      count: filtered.length,
    });
  }),

  http.get(`${API_BASE}/api/v1/agents/recent`, ({ request }) => {
    const url = new URL(request.url);
    const limit = parseInt(url.searchParams.get('limit') ?? '10', 10);
    return HttpResponse.json(agents.slice(0, limit));
  }),

  http.get(`${API_BASE}/api/v1/agents/:id`, ({ params }) => {
    const agent = agents.find(a => a.id === params.id);
    if (!agent) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json(agent);
  }),

  http.get(`${API_BASE}/api/v1/agents/:id/status`, ({ params }) => {
    const agent = agents.find(a => a.id === params.id);
    if (!agent) return HttpResponse.json({ error: 'Not found' }, { status: 404 });
    return HttpResponse.json({
      agentId: agent.id,
      name: agent.name,
      isActive: agent.isActive,
      isReady: true,
      hasConfiguration: true,
      hasDocuments: true,
      documentCount: 2,
      ragStatus: 'ready',
      blockingReason: null,
    });
  }),

  http.get(`${API_BASE}/api/v1/agent-typologies`, () => {
    return HttpResponse.json({
      success: true,
      typologies: [],
      total: 0,
    });
  }),

  http.post(`${API_BASE}/api/v1/agents`, async ({ request }) => {
    const body = (await request.json()) as Partial<AgentMock>;
    const newAgent: AgentMock = {
      id: mockId(Math.floor(Math.random() * 9000) + 1000),
      name: body.name ?? 'New Agent',
      type: body.type ?? 'qa',
      strategyName: 'hybrid-rag',
      strategyParameters: {},
      isActive: true,
      createdAt: new Date().toISOString(),
      lastInvokedAt: null,
      invocationCount: 0,
      isRecentlyUsed: false,
      isIdle: true,
      gameId: null,
      gameName: null,
      createdByUserId: mockId(1),
    };
    agents.push(newAgent);
    return HttpResponse.json(newAgent, { status: 201 });
  }),
];

export const resetAgentsState = () => {
  agents.splice(0, agents.length, ...SEED_AGENTS.map(a => ({ ...a })));
};
