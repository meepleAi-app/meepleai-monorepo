/**
 * Edit Game Page - Visual Tests
 * Issue #2372 - Phase 3: Frontend Admin UI
 *
 * Chromatic visual regression tests for the Edit Game page.
 * Covers: tabs, status actions, delete workflow, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from 'storybook/test';

import { EditGameClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof EditGameClient> = {
  title: 'Admin/SharedGames/EditGame/Visual Tests',
  component: EditGameClient,
  parameters: {
    layout: 'fullscreen',
    nextjs: {
      appDirectory: true,
      navigation: {
        pathname: '/admin/shared-games/11111111-1111-1111-1111-111111111111',
      },
    },
    chromatic: {
      disableSnapshot: false,
      delay: 500,
    },
  },
  decorators: [
    Story => (
      <AuthProvider>
        <div className="min-h-screen bg-gray-50">
          <Story />
        </div>
      </AuthProvider>
    ),
  ],
};

export default meta;
type Story = StoryObj<typeof EditGameClient>;

// ========== Mock Data ==========

const mockCategories = [
  { id: 'cat-1', name: 'Strategia', gameCount: 45 },
  { id: 'cat-2', name: 'Famiglia', gameCount: 32 },
  { id: 'cat-3', name: 'Party', gameCount: 28 },
  { id: 'cat-4', name: 'Cooperativo', gameCount: 19 },
];

const mockMechanics = [
  { id: 'mech-1', name: 'Dice Rolling', gameCount: 67 },
  { id: 'mech-2', name: 'Hand Management', gameCount: 54 },
  { id: 'mech-3', name: 'Area Control', gameCount: 38 },
];

const mockPublishedGame = {
  id: '11111111-1111-1111-1111-111111111111',
  title: 'I Coloni di Catan',
  yearPublished: 1995,
  description:
    "Classico gioco di strategia e commercio dove i giocatori devono raccogliere risorse, costruire strade e insediamenti, e commerciare per diventare il dominatore dell'isola di Catan.",
  minPlayers: 3,
  maxPlayers: 4,
  playingTimeMinutes: 75,
  minAge: 10,
  complexityRating: 2.3,
  averageRating: 7.1,
  imageUrl:
    'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__imagepage/img/M_3Vg1j2HlNgkv7PL2xl2BJE2bw=/fit-in/900x600/filters:no_upscale():strip_icc()/pic2419375.jpg',
  thumbnailUrl:
    'https://cf.geekdo-images.com/W3Bsga_uLP9kO91gZ7H8yw__thumb/img/7a7S0c3Sdd4dWKpbYzTkf1E96sw=/fit-in/200x150/filters:strip_icc()/pic2419375.jpg',
  bggId: 13,
  status: 1, // Published
  categories: [
    { id: 'cat-1', name: 'Strategia' },
    { id: 'cat-2', name: 'Negoziazione' },
  ],
  mechanics: [
    { id: 'mech-1', name: 'Dice Rolling' },
    { id: 'mech-2', name: 'Trading' },
    { id: 'mech-3', name: 'Route Building' },
  ],
  rules: {
    id: 'rules-1',
    gameId: '11111111-1111-1111-1111-111111111111',
    content:
      '# Regole di Catan\n\n## Preparazione\n1. Disponi i tasselli esagonali\n2. Ogni giocatore sceglie un colore\n\n## Turno di gioco\n1. Lancia i dadi\n2. Commercia\n3. Costruisci',
    language: 'it',
    version: '1.0',
    createdAt: '2024-01-15T10:00:00Z',
    updatedAt: '2024-01-15T10:00:00Z',
  },
  createdAt: '2024-01-15T10:00:00Z',
  updatedAt: '2024-06-20T14:30:00Z',
};

const mockDraftGame = {
  ...mockPublishedGame,
  id: '22222222-2222-2222-2222-222222222222',
  title: 'Nuovo Gioco (Bozza)',
  status: 0, // Draft
  rules: null,
};

const mockArchivedGame = {
  ...mockPublishedGame,
  id: '33333333-3333-3333-3333-333333333333',
  title: 'Gioco Archiviato',
  status: 2, // Archived
};

const mockAuditHistory = [
  {
    id: 'audit-1',
    action: 'Created',
    performedBy: 'admin@meepleai.dev',
    performedAt: '2024-01-15T10:00:00Z',
    changes: { title: 'I Coloni di Catan' },
  },
  {
    id: 'audit-2',
    action: 'Updated',
    performedBy: 'editor@meepleai.dev',
    performedAt: '2024-03-20T14:30:00Z',
    changes: { description: 'Aggiunta descrizione dettagliata' },
  },
  {
    id: 'audit-3',
    action: 'Published',
    performedBy: 'admin@meepleai.dev',
    performedAt: '2024-06-20T14:30:00Z',
    changes: { status: '0 → 1' },
  },
];

// ========== Stories ==========

/**
 * Default View - Published game, Details tab
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Draft Game - Unpublished state
 */
export const DraftGame: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/shared-games/22222222-2222-2222-2222-222222222222',
      },
    },
    mockData: {
      '/api/v1/admin/shared-games/22222222-2222-2222-2222-222222222222': mockDraftGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Archived Game - Read-only state
 */
export const ArchivedGame: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/shared-games/33333333-3333-3333-3333-333333333333',
      },
    },
    mockData: {
      '/api/v1/admin/shared-games/33333333-3333-3333-3333-333333333333': mockArchivedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Loading State - Initial data fetch
 */
export const LoadingState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': { delay: 'infinite' },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Error State - Game not found
 */
export const ErrorState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': {
        error: { message: 'Gioco non trovato' },
        status: 404,
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Categories Tab - Category management view
 */
export const CategoriesTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('I Coloni di Catan');

    const categoriesTab = await canvas.findByRole('tab', { name: /categorie/i });
    await userEvent.click(categoriesTab);

    await expect(canvas.findByText('Strategia')).resolves.toBeInTheDocument();
  },
};

/**
 * Rules Tab - Rules content view
 */
export const RulesTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('I Coloni di Catan');

    const rulesTab = await canvas.findByRole('tab', { name: /regole/i });
    await userEvent.click(rulesTab);

    await expect(canvas.findByText(/Regole di Catan/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Rules Tab - No rules
 */
export const RulesTabEmpty: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/shared-games/22222222-2222-2222-2222-222222222222',
      },
    },
    mockData: {
      '/api/v1/admin/shared-games/22222222-2222-2222-2222-222222222222': mockDraftGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('Nuovo Gioco (Bozza)');

    const rulesTab = await canvas.findByRole('tab', { name: /regole/i });
    await userEvent.click(rulesTab);
  },
};

/**
 * History Tab - Audit log
 */
export const HistoryTab: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111/audit': mockAuditHistory,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('I Coloni di Catan');

    const historyTab = await canvas.findByRole('tab', { name: /cronologia/i });
    await userEvent.click(historyTab);
  },
};

/**
 * Delete Confirmation Dialog
 */
export const DeleteDialog: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('I Coloni di Catan');

    const deleteButton = await canvas.findByRole('button', { name: /elimina/i });
    await userEvent.click(deleteButton);

    await expect(canvas.findByText(/conferma eliminazione/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Archive Confirmation Dialog
 */
export const ArchiveDialog: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('I Coloni di Catan');

    const archiveButton = await canvas.findByRole('button', { name: /archivia/i });
    await userEvent.click(archiveButton);

    await expect(canvas.findByText(/archiviare questo gioco/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Publish Confirmation Dialog (Draft game)
 */
export const PublishDialog: Story = {
  parameters: {
    nextjs: {
      navigation: {
        pathname: '/admin/shared-games/22222222-2222-2222-2222-222222222222',
      },
    },
    mockData: {
      '/api/v1/admin/shared-games/22222222-2222-2222-2222-222222222222': mockDraftGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    await canvas.findByText('Nuovo Gioco (Bozza)');

    const publishButton = await canvas.findByRole('button', { name: /pubblica/i });
    await userEvent.click(publishButton);

    await expect(canvas.findByText(/pubblicare questo gioco/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Mobile View - Responsive layout
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet View - Medium screen
 */
export const TabletView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': mockPublishedGame,
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Game with Many Categories
 */
export const ManyCategories: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': {
        ...mockPublishedGame,
        categories: [
          { id: 'cat-1', name: 'Strategia' },
          { id: 'cat-2', name: 'Negoziazione' },
          { id: 'cat-3', name: 'Economia' },
          { id: 'cat-4', name: 'Costruzione' },
          { id: 'cat-5', name: 'Territorio' },
          { id: 'cat-6', name: 'Risorse' },
        ],
        mechanics: [
          { id: 'mech-1', name: 'Dice Rolling' },
          { id: 'mech-2', name: 'Trading' },
          { id: 'mech-3', name: 'Route Building' },
          { id: 'mech-4', name: 'Modular Board' },
          { id: 'mech-5', name: 'Resource Management' },
        ],
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};

/**
 * Long Title and Description
 */
export const LongContent: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/11111111-1111-1111-1111-111111111111': {
        ...mockPublishedGame,
        title: 'Twilight Imperium: Quarta Edizione - Edizione Speciale Anniversario con Espansioni',
        description:
          "Twilight Imperium è un epico gioco di conquista galattica in cui tre a sei giocatori assumono il ruolo di una delle diciassette fazioni che competono per la supremazia galattica attraverso la potenza militare, le manovre politiche e la negoziazione economica. Ogni fazione offre un'esperienza di gioco completamente diversa, dalle tartarughe guerriere degli Xxcha al sinistro Nessuno di Clan Sar. Questo è uno dei giochi da tavolo più complessi e appaganti mai creati.",
      },
      '/api/v1/shared-games/categories': mockCategories,
      '/api/v1/shared-games/mechanics': mockMechanics,
    },
  },
};
