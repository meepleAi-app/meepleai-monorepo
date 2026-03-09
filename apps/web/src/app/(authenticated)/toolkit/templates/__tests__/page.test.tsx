import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import ToolkitTemplatesPage from '../page';

const mockGetApprovedTemplates = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    gameToolkit: {
      getApprovedTemplates: mockGetApprovedTemplates,
    },
  },
}));

const MOCK_TEMPLATES = [
  {
    id: '00000000-0000-0000-0000-000000000001',
    gameId: null,
    privateGameId: null,
    name: 'Catan Toolkit',
    version: 1,
    createdByUserId: '00000000-0000-0000-0000-000000000099',
    isPublished: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    templateStatus: 'Approved' as const,
    isTemplate: true,
    diceTools: [{ name: 'D6' }, { name: 'Event Die' }],
    cardTools: [{ name: 'Dev Cards' }],
    timerTools: [],
    counterTools: [{ name: 'Victory Points' }],
    stateTemplate: { name: 'Catan', description: 'Classic strategy game', category: 'Strategy' },
  },
  {
    id: '00000000-0000-0000-0000-000000000002',
    gameId: null,
    privateGameId: null,
    name: 'Empty Toolkit',
    version: 1,
    createdByUserId: '00000000-0000-0000-0000-000000000099',
    isPublished: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    templateStatus: 'Approved' as const,
    isTemplate: true,
    diceTools: [],
    cardTools: [],
    timerTools: [],
    counterTools: [],
    stateTemplate: null,
  },
];

describe('ToolkitTemplatesPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetApprovedTemplates.mockResolvedValue(MOCK_TEMPLATES);
  });

  it('renders page title', async () => {
    renderWithQuery(<ToolkitTemplatesPage />);
    expect(screen.getByText('Toolkit Templates')).toBeInTheDocument();
  });

  it('renders loading state', () => {
    mockGetApprovedTemplates.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<ToolkitTemplatesPage />);
    expect(screen.getByText('Loading templates...')).toBeInTheDocument();
  });

  it('renders template cards after loading', async () => {
    renderWithQuery(<ToolkitTemplatesPage />);
    await waitFor(() => {
      expect(screen.getByText('Catan Toolkit')).toBeInTheDocument();
    });
    expect(screen.getByText('Empty Toolkit')).toBeInTheDocument();
  });

  it('shows tool count badges', async () => {
    renderWithQuery(<ToolkitTemplatesPage />);
    await waitFor(() => {
      expect(screen.getByText('2 dice')).toBeInTheDocument();
    });
    expect(screen.getByText('1 cards')).toBeInTheDocument();
    expect(screen.getByText('1 counters')).toBeInTheDocument();
  });

  it('shows "No tools configured" for empty toolkit', async () => {
    renderWithQuery(<ToolkitTemplatesPage />);
    await waitFor(() => {
      expect(screen.getByText('No tools configured')).toBeInTheDocument();
    });
  });

  it('shows category badge from stateTemplate', async () => {
    renderWithQuery(<ToolkitTemplatesPage />);
    await waitFor(() => {
      expect(screen.getByText('Strategy')).toBeInTheDocument();
    });
  });

  it('shows empty state when no templates', async () => {
    mockGetApprovedTemplates.mockResolvedValue([]);
    renderWithQuery(<ToolkitTemplatesPage />);
    await waitFor(() => {
      expect(screen.getByText('No approved templates found.')).toBeInTheDocument();
    });
  });

  it('renders Use This Template buttons', async () => {
    renderWithQuery(<ToolkitTemplatesPage />);
    await waitFor(() => {
      const buttons = screen.getAllByText('Use This Template');
      expect(buttons).toHaveLength(2);
    });
  });

  it('calls getApprovedTemplates with undefined for All category', async () => {
    renderWithQuery(<ToolkitTemplatesPage />);
    await waitFor(() => {
      expect(mockGetApprovedTemplates).toHaveBeenCalledWith(undefined);
    });
  });
});
