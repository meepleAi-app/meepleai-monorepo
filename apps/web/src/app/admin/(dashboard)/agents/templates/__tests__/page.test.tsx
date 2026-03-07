import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import AdminTemplateReviewPage from '../page';

const mockGetPendingReviewTemplates = vi.hoisted(() => vi.fn());
const mockApproveTemplate = vi.hoisted(() => vi.fn());
const mockRejectTemplate = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    gameToolkit: {
      getPendingReviewTemplates: mockGetPendingReviewTemplates,
      approveTemplate: mockApproveTemplate,
      rejectTemplate: mockRejectTemplate,
    },
  },
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

const MOCK_PENDING = [
  {
    id: '00000000-0000-0000-0000-000000000010',
    gameId: null,
    privateGameId: null,
    name: 'Pending Strategy Kit',
    version: 1,
    createdByUserId: '00000000-0000-0000-0000-000000000099',
    isPublished: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: '2026-01-01T00:00:00Z',
    templateStatus: 'PendingReview' as const,
    isTemplate: true,
    diceTools: [{ name: 'D6' }],
    cardTools: [],
    timerTools: [{ name: 'Turn Timer' }],
    counterTools: [],
    stateTemplate: {
      name: 'Strategy',
      description: 'A strategy template',
      category: 'Strategy',
    },
  },
];

describe('AdminTemplateReviewPage', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    mockGetPendingReviewTemplates.mockResolvedValue(MOCK_PENDING);
    mockApproveTemplate.mockResolvedValue({});
    mockRejectTemplate.mockResolvedValue({});
  });

  it('renders page title', async () => {
    renderWithQuery(<AdminTemplateReviewPage />);
    expect(screen.getByText('Template Review Queue')).toBeInTheDocument();
  });

  it('renders loading skeletons', () => {
    mockGetPendingReviewTemplates.mockReturnValue(new Promise(() => {}));
    renderWithQuery(<AdminTemplateReviewPage />);
    // Skeletons are rendered during loading
    expect(screen.queryByTestId('review-card')).not.toBeInTheDocument();
  });

  it('renders pending template cards', async () => {
    renderWithQuery(<AdminTemplateReviewPage />);
    await waitFor(() => {
      expect(screen.getByText('Pending Strategy Kit')).toBeInTheDocument();
    });
  });

  it('shows tool badges', async () => {
    renderWithQuery(<AdminTemplateReviewPage />);
    await waitFor(() => {
      expect(screen.getByText('1 dice')).toBeInTheDocument();
      expect(screen.getByText('1 timers')).toBeInTheDocument();
    });
  });

  it('shows empty state when no pending templates', async () => {
    mockGetPendingReviewTemplates.mockResolvedValue([]);
    renderWithQuery(<AdminTemplateReviewPage />);
    await waitFor(() => {
      expect(screen.getByText('No templates pending review')).toBeInTheDocument();
    });
  });

  it('renders approve and reject buttons', async () => {
    renderWithQuery(<AdminTemplateReviewPage />);
    await waitFor(() => {
      expect(screen.getByText('Approve')).toBeInTheDocument();
      expect(screen.getByText('Reject')).toBeInTheDocument();
    });
  });

  it('reject button is disabled without notes', async () => {
    renderWithQuery(<AdminTemplateReviewPage />);
    await waitFor(() => {
      expect(screen.getByText('Reject')).toBeInTheDocument();
    });
    const rejectBtn = screen.getByText('Reject').closest('button');
    expect(rejectBtn).toBeDisabled();
  });

  it('calls approve when approve button clicked', async () => {
    const user = userEvent.setup();
    renderWithQuery(<AdminTemplateReviewPage />);

    await waitFor(() => {
      expect(screen.getByText('Approve')).toBeInTheDocument();
    });

    await user.click(screen.getByText('Approve'));

    await waitFor(() => {
      expect(mockApproveTemplate).toHaveBeenCalledWith(
        '00000000-0000-0000-0000-000000000010',
        undefined
      );
    });
  });

  it('calls reject with notes when reject button clicked', async () => {
    const user = userEvent.setup();
    renderWithQuery(<AdminTemplateReviewPage />);

    await waitFor(() => {
      expect(screen.getByText('Reject')).toBeInTheDocument();
    });

    const textarea = screen.getByPlaceholderText('Review notes (required for rejection)');
    await user.type(textarea, 'Missing dice config');

    await user.click(screen.getByText('Reject'));

    await waitFor(() => {
      expect(mockRejectTemplate).toHaveBeenCalledWith(
        '00000000-0000-0000-0000-000000000010',
        'Missing dice config'
      );
    });
  });
});
