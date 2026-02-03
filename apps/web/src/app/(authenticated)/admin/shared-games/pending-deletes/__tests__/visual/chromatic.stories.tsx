/**
 * Pending Deletes Page - Visual Tests
 * Issue #2372 - Phase 3: Frontend Admin UI
 *
 * Chromatic visual regression tests for the Pending Deletes admin page.
 * Covers: list view, empty state, dialogs, pagination, responsive design
 */

import type { Meta, StoryObj } from '@storybook/react';
import { within, userEvent, expect } from 'storybook/test';

import { PendingDeletesClient } from '../../client';
import { AuthProvider } from '@/components/auth/AuthProvider';

const meta: Meta<typeof PendingDeletesClient> = {
  title: 'Admin/SharedGames/PendingDeletes/Visual Tests',
  component: PendingDeletesClient,
  parameters: {
    layout: 'fullscreen',
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
type Story = StoryObj<typeof PendingDeletesClient>;

// ========== Mock Data ==========

const mockDeleteRequests = [
  {
    id: 'req-1',
    sharedGameId: '11111111-1111-1111-1111-111111111111',
    gameTitle: 'I Coloni di Catan',
    reason: 'Gioco duplicato, già presente con ID diverso',
    requestedBy: 'editor@example.com',
    createdAt: '2024-06-15T10:30:00Z',
    status: 'pending',
  },
  {
    id: 'req-2',
    sharedGameId: '22222222-2222-2222-2222-222222222222',
    gameTitle: 'Ticket to Ride',
    reason: 'Informazioni errate non correggibili, richiede nuova entry',
    requestedBy: 'admin@example.com',
    createdAt: '2024-06-14T14:45:00Z',
    status: 'pending',
  },
  {
    id: 'req-3',
    sharedGameId: '33333333-3333-3333-3333-333333333333',
    gameTitle: 'Gloomhaven',
    reason: 'Versione obsoleta, sostituita da Gloomhaven: Jaws of the Lion',
    requestedBy: 'editor2@example.com',
    createdAt: '2024-06-13T09:15:00Z',
    status: 'pending',
  },
  {
    id: 'req-4',
    sharedGameId: '44444444-4444-4444-4444-444444444444',
    gameTitle: 'Codenames',
    reason: 'Contenuto inappropriato non autorizzato',
    requestedBy: 'moderator@example.com',
    createdAt: '2024-06-12T16:20:00Z',
    status: 'pending',
  },
  {
    id: 'req-5',
    sharedGameId: '55555555-5555-5555-5555-555555555555',
    gameTitle: 'Wingspan',
    reason: 'Copyright issues con le immagini caricate',
    requestedBy: 'legal@example.com',
    createdAt: '2024-06-11T11:00:00Z',
    status: 'pending',
  },
];

const mockPendingDeletesResponse = {
  items: mockDeleteRequests,
  total: 5,
  page: 1,
  pageSize: 10,
  totalPages: 1,
};

// ========== Stories ==========

/**
 * Default View - List of pending delete requests
 */
export const DefaultView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': mockPendingDeletesResponse,
    },
  },
};

/**
 * Empty State - No pending delete requests
 */
export const EmptyState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': {
        items: [],
        total: 0,
        page: 1,
        pageSize: 10,
        totalPages: 0,
      },
    },
  },
};

/**
 * Loading State - Initial data fetch
 */
export const LoadingState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': { delay: 'infinite' },
    },
  },
};

/**
 * Single Request - Only one pending request
 */
export const SingleRequest: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': {
        items: [mockDeleteRequests[0]],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
      },
    },
  },
};

/**
 * Many Requests - Multiple pages of requests
 */
export const ManyRequests: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': {
        items: mockDeleteRequests,
        total: 47,
        page: 1,
        pageSize: 10,
        totalPages: 5,
      },
    },
  },
};

/**
 * Approve Dialog Open - Confirmation dialog for approval
 */
export const ApproveDialogOpen: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': mockPendingDeletesResponse,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for table to load
    await canvas.findByText('I Coloni di Catan');

    // Click approve button on first row
    const approveButtons = await canvas.findAllByRole('button', { name: /approva/i });
    if (approveButtons.length > 0) {
      await userEvent.click(approveButtons[0]);
    }

    // Verify dialog opened
    await expect(canvas.findByText(/approva eliminazione/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Reject Dialog Open - Confirmation dialog for rejection
 */
export const RejectDialogOpen: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': mockPendingDeletesResponse,
    },
  },
  play: async ({ canvasElement }) => {
    const canvas = within(canvasElement);

    // Wait for table to load
    await canvas.findByText('I Coloni di Catan');

    // Click reject button on first row
    const rejectButtons = await canvas.findAllByRole('button', { name: /rifiuta/i });
    if (rejectButtons.length > 0) {
      await userEvent.click(rejectButtons[0]);
    }

    // Verify dialog opened
    await expect(canvas.findByText(/rifiuta eliminazione/i)).resolves.toBeInTheDocument();
  },
};

/**
 * Long Reason Text - Request with very long reason
 */
export const LongReasonText: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': {
        items: [
          {
            ...mockDeleteRequests[0],
            reason:
              'Questo gioco deve essere eliminato perché contiene informazioni errate che non possono essere corrette facilmente. ' +
              'Le immagini sono di bassa qualità e non rispettano le linee guida del catalogo. ' +
              'Inoltre, la descrizione contiene errori grammaticali e informazioni obsolete che potrebbero confondere gli utenti. ' +
              'Si consiglia di ricreare la entry con dati aggiornati e verificati dalla fonte ufficiale.',
          },
        ],
        total: 1,
        page: 1,
        pageSize: 10,
        totalPages: 1,
      },
    },
  },
};

/**
 * Mobile View - Responsive table layout
 */
export const MobileView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': mockPendingDeletesResponse,
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Mobile Empty State - Empty state on mobile
 */
export const MobileEmptyState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': {
        items: [],
        total: 0,
        page: 1,
        pageSize: 10,
        totalPages: 0,
      },
    },
    viewport: {
      defaultViewport: 'mobile1',
    },
  },
};

/**
 * Tablet View - Medium screen layout
 */
export const TabletView: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': mockPendingDeletesResponse,
    },
    viewport: {
      defaultViewport: 'tablet',
    },
  },
};

/**
 * Pagination Second Page - Viewing second page
 */
export const PaginationSecondPage: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': {
        items: mockDeleteRequests.slice(0, 3),
        total: 25,
        page: 2,
        pageSize: 10,
        totalPages: 3,
      },
    },
  },
};

/**
 * Error State - Failed to load requests
 */
export const ErrorState: Story = {
  parameters: {
    mockData: {
      '/api/v1/admin/shared-games/pending-deletes': {
        error: { message: 'Errore nel caricamento delle richieste' },
        status: 500,
      },
    },
  },
};
