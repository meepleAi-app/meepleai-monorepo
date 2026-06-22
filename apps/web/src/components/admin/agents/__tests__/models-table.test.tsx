import { fireEvent, render, screen, waitFor } from '@testing-library/react';
import { beforeEach, describe, expect, it, vi } from 'vitest';

import { AdminAiModelsApiError, type AiModelDto } from '@/lib/api/admin-ai-models';

const seedModels: AiModelDto[] = [
  {
    id: 'mdl-gpt4',
    modelId: 'gpt-4-turbo',
    displayName: 'GPT-4 Turbo',
    provider: 'OpenAI',
    priority: 1,
    isActive: true,
    isPrimary: true,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    settings: {
      maxTokens: 4096,
      temperature: 0.7,
      pricing: {
        inputPricePerMillion: 10, // $10/M = $0.0100/k
        outputPricePerMillion: 30,
        currency: 'USD',
      },
    },
    usage: {
      totalRequests: 8420,
      totalInputTokens: 1_000_000,
      totalOutputTokens: 500_000,
      totalTokensUsed: 1_500_000,
      totalCostUsd: 25.5,
      lastUsedAt: '2026-05-22T10:00:00Z',
    },
  },
  {
    id: 'mdl-gemini',
    modelId: 'gemini-pro',
    displayName: 'Gemini Pro',
    provider: 'Google',
    priority: 4,
    isActive: false,
    isPrimary: false,
    createdAt: '2026-01-01T00:00:00Z',
    updatedAt: null,
    settings: {
      maxTokens: 4096,
      temperature: 0.7,
      pricing: { inputPricePerMillion: 0.25, outputPricePerMillion: 0.5, currency: 'USD' },
    },
    usage: {
      totalRequests: 420,
      totalInputTokens: 50_000,
      totalOutputTokens: 25_000,
      totalTokensUsed: 75_000,
      totalCostUsd: 0.03,
      lastUsedAt: null,
    },
  },
];

const mocks = vi.hoisted(() => ({
  queryState: {
    data: undefined as AiModelDto[] | undefined,
    isLoading: false,
    isError: false,
    error: null as unknown,
  },
  toggleState: {
    isError: false,
    isPending: false,
    error: null as unknown,
  },
  toggleMutateAsync: vi.fn<(id: string) => Promise<AiModelDto>>(),
}));

vi.mock('@/hooks/queries/useAdminAiModels', () => ({
  useAdminAiModels: () => ({
    data: mocks.queryState.data,
    isLoading: mocks.queryState.isLoading,
    isError: mocks.queryState.isError,
    error: mocks.queryState.error,
  }),
  useToggleAdminAiModel: () => ({
    mutateAsync: mocks.toggleMutateAsync,
    isError: mocks.toggleState.isError,
    isPending: mocks.toggleState.isPending,
    error: mocks.toggleState.error,
  }),
}));

import { ModelsTable } from '../models-table';

beforeEach(() => {
  mocks.queryState.data = seedModels;
  mocks.queryState.isLoading = false;
  mocks.queryState.isError = false;
  mocks.queryState.error = null;
  mocks.toggleState.isError = false;
  mocks.toggleState.isPending = false;
  mocks.toggleState.error = null;
  mocks.toggleMutateAsync.mockReset();
  mocks.toggleMutateAsync.mockResolvedValue(seedModels[0]);
});

describe('ModelsTable (#1442 Phase 1a — live API)', () => {
  it('renders headers + section title', () => {
    render(<ModelsTable />);
    expect(screen.getByText('AI Models')).toBeInTheDocument();
    expect(screen.getByText('Provider')).toBeInTheDocument();
    expect(screen.getByText('Model')).toBeInTheDocument();
    expect(screen.getByText('Status')).toBeInTheDocument();
    expect(screen.getByText('Cost/1k')).toBeInTheDocument();
    expect(screen.getByText('Latency')).toBeInTheDocument();
    expect(screen.getByText('Usage')).toBeInTheDocument();
  });

  it('renders rows from the server-mapped DTO', () => {
    render(<ModelsTable />);
    expect(screen.getByText('GPT-4 Turbo')).toBeInTheDocument();
    expect(screen.getByText('Gemini Pro')).toBeInTheDocument();
    expect(screen.getByText('OpenAI')).toBeInTheDocument();
    expect(screen.getByText('Google')).toBeInTheDocument();
  });

  it('displays cost derived from `inputPricePerMillion / 1000`', () => {
    const { container } = render(<ModelsTable />);
    // GPT-4 Turbo: 10 / 1000 = $0.0100 per 1k
    expect(container.textContent).toContain('$0.0100');
  });

  it('renders avgLatency placeholder "—" because BE does not track it', () => {
    render(<ModelsTable />);
    expect(screen.getAllByText('—').length).toBeGreaterThanOrEqual(2);
  });

  it('displays usage counts from UsageStats.TotalRequests', () => {
    const { container } = render(<ModelsTable />);
    // 8420 may render as "8,420" or "8.420" depending on locale.
    expect(container.textContent).toMatch(/8[.,\s]?420/);
  });

  it('renders loading state while query is pending', () => {
    mocks.queryState.data = undefined;
    mocks.queryState.isLoading = true;
    render(<ModelsTable />);
    expect(screen.getByText(/loading ai models/i)).toBeInTheDocument();
  });

  it('renders error row when the list query fails', () => {
    mocks.queryState.data = undefined;
    mocks.queryState.isError = true;
    mocks.queryState.error = new Error('network down');
    render(<ModelsTable />);
    expect(screen.getByText(/failed to load ai models.*network down/i)).toBeInTheDocument();
  });

  it('renders empty state when no models exist', () => {
    mocks.queryState.data = [];
    render(<ModelsTable />);
    expect(screen.getByText(/no ai models configured/i)).toBeInTheDocument();
  });

  it('calls the toggle mutation with the row id when the switch is clicked', async () => {
    render(<ModelsTable />);
    const toggle = screen.getByLabelText(/toggle gemini pro/i);
    fireEvent.click(toggle);
    await waitFor(() => expect(mocks.toggleMutateAsync).toHaveBeenCalledWith('mdl-gemini'));
  });

  it('surfaces a 409 banner when the BE rejects toggling a primary model off', () => {
    mocks.toggleState.isError = true;
    mocks.toggleState.error = new AdminAiModelsApiError(409, 'Primary model cannot be deactivated');
    render(<ModelsTable />);
    expect(screen.getByRole('alert')).toHaveTextContent(/primary model cannot be deactivated/i);
  });

  it('disables toggle button while mutation is pending', () => {
    mocks.toggleState.isPending = true;
    render(<ModelsTable />);
    const toggle = screen.getByLabelText(/toggle gpt-4 turbo/i);
    expect(toggle).toBeDisabled();
  });
});
