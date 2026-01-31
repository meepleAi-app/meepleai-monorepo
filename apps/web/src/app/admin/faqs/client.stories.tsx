import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AuthProvider } from '@/components/auth/AuthProvider';

import { FAQManagementClient } from './client';

import type { Meta, StoryObj } from '@storybook/react';

/**
 * FAQ Management Page - Issue #2028
 *
 * ## Features
 * - **Game-Specific FAQs**: Create and manage FAQs per game
 * - **Game Filter**: Dropdown to select which game's FAQs to manage
 * - **CRUD Operations**: Create, read, update, delete FAQs
 * - **Upvote Display**: Show upvote counts for community engagement
 * - **Character Limits**: Question (500), Answer (5000)
 * - **Table View**: Clean display with question preview
 *
 * ## Access Control
 * - Requires Admin role (wrapped with AdminAuthGuard)
 *
 * ## Visual Regression Testing
 * Chromatic captures visual snapshots at multiple viewports:
 * - Mobile (375px)
 * - Tablet (768px)
 * - Desktop (1920px)
 */
const meta = {
  title: 'Pages/Admin/FAQs',
  component: FAQManagementClient,
  parameters: {
    layout: 'fullscreen',
    chromatic: {
      viewports: [375, 768, 1920],
      delay: 300,
      diffThreshold: 0.2,
    },
  },
  tags: ['autodocs'],
  decorators: [
    Story => {
      const queryClient = new QueryClient({
        defaultOptions: {
          queries: {
            retry: false,
            refetchInterval: false,
          },
        },
      });
      return (
        <QueryClientProvider client={queryClient}>
          <AuthProvider>
            <div className="min-h-screen bg-gray-50">
              <Story />
            </div>
          </AuthProvider>
        </QueryClientProvider>
      );
    },
  ],
} satisfies Meta<typeof FAQManagementClient>;

export default meta;
type Story = StoryObj<typeof meta>;

const mockGameId = 'game-catan-id';

/**
 * Default view with FAQs for Catan
 */
export const Default: Story = {
  parameters: {
    msw: {
      handlers: [
        // Games list
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [
              {
                id: mockGameId,
                title: 'Catan',
                minPlayers: 3,
                maxPlayers: 4,
                playingTime: 60,
              },
              {
                id: 'game-ticket-id',
                title: 'Ticket to Ride',
                minPlayers: 2,
                maxPlayers: 5,
                playingTime: 45,
              },
              {
                id: 'game-pandemic-id',
                title: 'Pandemic',
                minPlayers: 2,
                maxPlayers: 4,
                playingTime: 45,
              },
            ],
            total: 3,
          },
        },
        // FAQs for Catan
        {
          url: `/api/v1/games/${mockGameId}/faqs`,
          method: 'GET',
          status: 200,
          response: {
            faqs: [
              {
                id: 'faq-1',
                gameId: mockGameId,
                question: 'Come si vince al gioco?',
                answer:
                  'Il primo giocatore a raggiungere 10 punti vittoria vince la partita. I punti possono essere ottenuti costruendo insediamenti, città, e ottenendo carte sviluppo speciali.',
                upvotes: 42,
                createdAt: '2025-01-10T00:00:00Z',
                updatedAt: '2025-12-05T10:30:00Z',
              },
              {
                id: 'faq-2',
                gameId: mockGameId,
                question: 'Posso commerciare con altri giocatori?',
                answer:
                  'Sì! Il commercio è una parte fondamentale di Catan. Puoi commerciare risorse con altri giocatori durante il tuo turno, negoziando liberamente i termini dello scambio.',
                upvotes: 28,
                createdAt: '2025-02-15T00:00:00Z',
                updatedAt: '2025-11-20T14:15:00Z',
              },
              {
                id: 'faq-3',
                gameId: mockGameId,
                question: 'Cosa succede se tiro un 7?',
                answer:
                  "Quando viene tirato un 7, si attiva il ladrone. Il giocatore deve muovere il ladrone su un nuovo esagono, bloccare la produzione di quella risorsa, e rubare una carta casuale da un giocatore con un insediamento o città adiacente all'esagono.",
                upvotes: 35,
                createdAt: '2025-03-05T00:00:00Z',
                updatedAt: '2025-12-01T09:00:00Z',
              },
            ],
            total: 3,
          },
        },
      ],
    },
  },
};

/**
 * Loading state
 */
export const Loading: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          delay: 5000,
          status: 200,
          response: { games: [], total: 0 },
        },
      ],
    },
  },
};

/**
 * Empty state - no FAQs for selected game
 */
export const Empty: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [
              {
                id: mockGameId,
                title: 'Catan',
                minPlayers: 3,
                maxPlayers: 4,
                playingTime: 60,
              },
            ],
            total: 1,
          },
        },
        {
          url: `/api/v1/games/${mockGameId}/faqs`,
          method: 'GET',
          status: 200,
          response: {
            faqs: [],
            total: 0,
          },
        },
      ],
    },
  },
};

/**
 * No games available
 */
export const NoGames: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [],
            total: 0,
          },
        },
      ],
    },
  },
};

/**
 * Single FAQ
 */
export const SingleFAQ: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [
              {
                id: mockGameId,
                title: 'Pandemic',
                minPlayers: 2,
                maxPlayers: 4,
                playingTime: 45,
              },
            ],
            total: 1,
          },
        },
        {
          url: `/api/v1/games/${mockGameId}/faqs`,
          method: 'GET',
          status: 200,
          response: {
            faqs: [
              {
                id: 'faq-1',
                gameId: mockGameId,
                question: 'How do you win in Pandemic?',
                answer:
                  'Players win collectively by discovering cures for all four diseases before any of the losing conditions occur (running out of time, disease outbreaks, or depleting disease cubes).',
                upvotes: 15,
                createdAt: '2025-06-10T00:00:00Z',
                updatedAt: '2025-12-10T11:00:00Z',
              },
            ],
            total: 1,
          },
        },
      ],
    },
  },
};

/**
 * FAQs with high upvote counts
 */
export const HighUpvotes: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [
              {
                id: mockGameId,
                title: '7 Wonders',
                minPlayers: 2,
                maxPlayers: 7,
                playingTime: 30,
              },
            ],
            total: 1,
          },
        },
        {
          url: `/api/v1/games/${mockGameId}/faqs`,
          method: 'GET',
          status: 200,
          response: {
            faqs: [
              {
                id: 'faq-1',
                gameId: mockGameId,
                question: 'Quali sono le migliori strategie per vincere?',
                answer:
                  'Le strategie vincenti in 7 Wonders includono: focalizzarsi su una strategia coerente (militare, scientifico, commerciale), monitorare i vicini, bilanciare risorse e punti vittoria, e sfruttare le carte Wonder della propria civiltà.',
                upvotes: 156,
                createdAt: '2025-01-05T00:00:00Z',
                updatedAt: '2025-12-12T15:30:00Z',
              },
              {
                id: 'faq-2',
                gameId: mockGameId,
                question: 'Come funziona il commercio con i vicini?',
                answer:
                  'Puoi acquistare risorse dai vicini pagando 2 monete (1 moneta se hai carte commerciali appropriate). Le risorse acquistate possono essere usate solo per costruire carte nel turno corrente.',
                upvotes: 98,
                createdAt: '2025-02-20T00:00:00Z',
                updatedAt: '2025-11-28T09:45:00Z',
              },
            ],
            total: 2,
          },
        },
      ],
    },
  },
};

/**
 * Mobile view
 */
export const MobileView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'mobile1',
    },
    chromatic: {
      viewports: [375],
    },
  },
};

/**
 * Tablet view
 */
export const TabletView: Story = {
  ...Default,
  parameters: {
    ...Default.parameters,
    viewport: {
      defaultViewport: 'tablet',
    },
    chromatic: {
      viewports: [768],
    },
  },
};

/**
 * Long answer preview truncation
 */
export const LongAnswers: Story = {
  parameters: {
    msw: {
      handlers: [
        {
          url: '/api/v1/games',
          method: 'GET',
          status: 200,
          response: {
            games: [
              {
                id: mockGameId,
                title: 'Gloomhaven',
                minPlayers: 1,
                maxPlayers: 4,
                playingTime: 120,
              },
            ],
            total: 1,
          },
        },
        {
          url: `/api/v1/games/${mockGameId}/faqs`,
          method: 'GET',
          status: 200,
          response: {
            faqs: [
              {
                id: 'faq-1',
                gameId: mockGameId,
                question: 'Come funziona il sistema di combattimento?',
                answer:
                  'Il sistema di combattimento in Gloomhaven è basato su carte azione. Ogni personaggio ha un mazzo di carte abilità uniche. Durante il combattimento, selezioni due carte: la parte superiore di una carta e la parte inferiore di un\'altra. Questo determina le tue azioni per il turno. Il danno viene calcolato sommando il valore base dell\'attacco più eventuali modificatori dalla carta attacco pescata casualmente. I nemici seguono pattern comportamentali definiti dalle loro carte AI. La strategia richiede pianificazione accurata dell\'uso delle carte, gestione della fatica (carte scartate permanentemente), e coordinazione con il team. Il sistema elimina completamente i dadi, rendendo il gioco più tattico e prevedibile rispetto ai giochi basati sulla fortuna.',
                upvotes: 67,
                createdAt: '2025-04-12T00:00:00Z',
                updatedAt: '2025-12-08T16:20:00Z',
              },
            ],
            total: 1,
          },
        },
      ],
    },
  },
};
