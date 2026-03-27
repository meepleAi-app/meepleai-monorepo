import { screen, waitFor } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { describe, it, expect, vi, beforeEach } from 'vitest';

import { renderWithQuery } from '@/__tests__/utils/query-test-utils';

import NewAbTestPage from '../new/page';

const mockCreateAbTest = vi.hoisted(() => vi.fn());
const mockPush = vi.hoisted(() => vi.fn());

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      createAbTest: mockCreateAbTest,
    },
  },
}));

vi.mock('@/hooks/useSetNavConfig', () => ({
  useSetNavConfig: () => vi.fn(),
}));

vi.mock('next/navigation', () => ({
  useRouter: () => ({ push: mockPush }),
  redirect: vi.fn(),
}));

describe('NewAbTestPage', () => {
  const user = userEvent.setup();

  beforeEach(() => {
    vi.clearAllMocks();
  });

  it('renders page title and form elements', () => {
    renderWithQuery(<NewAbTestPage />);

    expect(screen.getByText('New A/B Test')).toBeInTheDocument();
    expect(screen.getByText('Question / Prompt')).toBeInTheDocument();
    expect(screen.getByText('Select Models')).toBeInTheDocument();
    expect(screen.getByRole('button', { name: /generate comparison/i })).toBeInTheDocument();
  });

  it('shows available models as checkboxes', () => {
    renderWithQuery(<NewAbTestPage />);

    expect(screen.getByText('GPT-4o')).toBeInTheDocument();
    expect(screen.getByText('Claude 3.5 Sonnet')).toBeInTheDocument();
    expect(screen.getByText('Gemini 2.0 Flash')).toBeInTheDocument();
  });

  it('disables generate button when no query or fewer than 2 models', () => {
    renderWithQuery(<NewAbTestPage />);

    const button = screen.getByRole('button', { name: /generate comparison/i });
    expect(button).toBeDisabled();
  });

  it('enables generate button when query and 2+ models selected', async () => {
    renderWithQuery(<NewAbTestPage />);

    const textarea = screen.getByPlaceholderText(/what are the rules/i);
    await user.type(textarea, 'Test query');

    const checkboxes = screen.getAllByRole('checkbox');
    await user.click(checkboxes[0]);
    await user.click(checkboxes[1]);

    const button = screen.getByRole('button', { name: /generate comparison/i });
    expect(button).toBeEnabled();
  });

  it('prevents selecting more than 4 models', async () => {
    renderWithQuery(<NewAbTestPage />);

    const checkboxes = screen.getAllByRole('checkbox');

    await user.click(checkboxes[0]);
    await user.click(checkboxes[1]);
    await user.click(checkboxes[2]);
    await user.click(checkboxes[3]);

    // 5th checkbox should be disabled
    expect(checkboxes[4]).toBeDisabled();
  });

  it('calls createAbTest and redirects on success', async () => {
    const sessionId = '00000000-0000-0000-0000-000000000001';
    mockCreateAbTest.mockResolvedValue({
      id: sessionId,
      query: 'Test',
      status: 'InProgress',
      variants: [],
      totalCost: 0,
      createdAt: new Date().toISOString(),
    });

    renderWithQuery(<NewAbTestPage />);

    const textarea = screen.getByPlaceholderText(/what are the rules/i);
    await user.type(textarea, 'Test query');

    const checkboxes = screen.getAllByRole('checkbox');
    await user.click(checkboxes[0]);
    await user.click(checkboxes[1]);

    const button = screen.getByRole('button', { name: /generate comparison/i });
    await user.click(button);

    await waitFor(() => {
      expect(mockCreateAbTest).toHaveBeenCalledWith({
        query: 'Test query',
        modelIds: expect.arrayContaining([expect.any(String)]),
      });
    });

    await waitFor(() => {
      expect(mockPush).toHaveBeenCalledWith(`/admin/agents/ab-testing/${sessionId}`);
    });
  });

  it('shows error message on API failure', async () => {
    mockCreateAbTest.mockRejectedValue(new Error('Budget exhausted'));

    renderWithQuery(<NewAbTestPage />);

    const textarea = screen.getByPlaceholderText(/what are the rules/i);
    await user.type(textarea, 'Test query');

    const checkboxes = screen.getAllByRole('checkbox');
    await user.click(checkboxes[0]);
    await user.click(checkboxes[1]);

    const button = screen.getByRole('button', { name: /generate comparison/i });
    await user.click(button);

    await waitFor(() => {
      expect(screen.getByText('Budget exhausted')).toBeInTheDocument();
    });
  });

  it('shows character count for query', async () => {
    renderWithQuery(<NewAbTestPage />);

    expect(screen.getByText('0/2000')).toBeInTheDocument();

    const textarea = screen.getByPlaceholderText(/what are the rules/i);
    await user.type(textarea, 'Hello');

    expect(screen.getByText('5/2000')).toBeInTheDocument();
  });
});
