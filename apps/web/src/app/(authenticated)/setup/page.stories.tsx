/**
 * /setup — Setup Guide page (fix/setup-page-redesign).
 *
 * DEC-A5 states: default (guide generated via play() click) / empty
 * (initial state, no guide yet) / loading (POST /agents/setup pending) /
 * error (GET /games 500). SetupView fetches games via `useGames()` (React
 * Query GET /api/v1/games) and generates the guide via a local
 * `useMutation` (POST /api/v1/agents/setup) — Default/Loading drive the
 * mutation through the real "Generate" button since the guide result is
 * plain `useState`, not React-Query-cached.
 */
import { http, HttpResponse } from 'msw';
import { expect, userEvent, waitFor, within } from 'storybook/test';

import SetupPage from './page';

import type { Meta, StoryObj } from '@storybook/react';

const API_BASE = process.env.NEXT_PUBLIC_API_BASE || 'http://localhost:8080';
const GAMES_URL = `${API_BASE}/api/v1/games`;
const SETUP_URL = `${API_BASE}/api/v1/agents/setup`;

const MOCK_GAMES = {
  games: [
    {
      id: '11111111-1111-1111-1111-111111111111',
      title: 'Wingspan',
      publisher: 'Stonemaier Games',
      yearPublished: 2019,
      minPlayers: 1,
      maxPlayers: 5,
      minPlayTimeMinutes: 40,
      maxPlayTimeMinutes: 70,
      bggId: 266192,
      createdAt: '2026-01-01T00:00:00Z',
    },
  ],
  total: 1,
  page: 1,
  pageSize: 20,
  totalPages: 1,
};

const MOCK_GUIDE = {
  gameTitle: 'Wingspan',
  steps: [
    {
      stepNumber: 1,
      title: 'Prepara il tabellone',
      instruction: 'Posiziona il tabellone al centro del tavolo e distribuisci le riserve di cibo.',
      references: [
        {
          documentId: '22222222-2222-2222-2222-222222222222',
          gameId: '11111111-1111-1111-1111-111111111111',
          snippet: 'Il tabellone principale mostra tre habitat: bosco, prateria e zona umida.',
          score: 0.91,
          pageNumber: 2,
          metadata: null,
        },
      ],
      isOptional: false,
    },
    {
      stepNumber: 2,
      title: 'Distribuisci le carte',
      instruction: 'Ogni giocatore riceve 5 carte uccello e 5 carte bonus.',
      references: [],
      isOptional: true,
    },
  ],
  estimatedSetupTimeMinutes: 8,
  promptTokens: 120,
  completionTokens: 64,
  totalTokens: 184,
  confidence: 0.87,
};

const meta: Meta<typeof SetupPage> = {
  title: 'Authenticated / setup',
  component: SetupPage,
  parameters: {
    canonicalStates: ['default', 'empty', 'loading', 'error'],
    layout: 'fullscreen',
    nextjs: { appDirectory: true },
    viewport: { defaultViewport: 'desktop' },
    msw: {
      handlers: [
        http.get(GAMES_URL, () => HttpResponse.json(MOCK_GAMES)),
        http.post(SETUP_URL, () => HttpResponse.json(MOCK_GUIDE)),
      ],
    },
    docs: {
      description: {
        component: 'AI-generated game setup guide with step checklist, progress and citations.',
      },
    },
  },
};

export default meta;

type Story = StoryObj<typeof SetupPage>;

/** Default: game auto-selected on load, guide generated via a real button click. */
export const Default: Story = {
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const generateButton = await canvas.findByRole('button', { name: /Genera Guida Setup/i });
    await waitFor(() => expect(generateButton).toBeEnabled());
    await userEvent.click(generateButton);
    await canvas.findByRole('heading', { name: 'Wingspan' }, { timeout: 5000 });
  },
};

/** Empty: initial state — games loaded, no guide generated yet. */
export const Empty: Story = {};

/** Loading: POST /agents/setup never resolves — shows the "generating" card. */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(GAMES_URL, () => HttpResponse.json(MOCK_GAMES)),
        http.post(SETUP_URL, () => new Promise(() => {})),
      ],
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);
    const generateButton = await canvas.findByRole('button', { name: /Genera Guida Setup/i });
    await waitFor(() => expect(generateButton).toBeEnabled());
    await userEvent.click(generateButton);
  },
};

/** Error: GET /games returns 500 — shows the error empty-state with retry. */
export const Error: Story = {
  parameters: {
    msw: {
      handlers: [
        http.get(GAMES_URL, () => HttpResponse.json({ error: 'server_error' }, { status: 500 })),
      ],
    },
  },
};
