/**
 * AgentCostCalculatorTab Tests
 * Issue #3725 - Agent Cost Calculator (Epic #3688)
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      estimateAgentCost: vi.fn().mockRejectedValue(new Error('Not available')),
      getCostScenarios: vi.fn().mockRejectedValue(new Error('Not available')),
      saveCostScenario: vi.fn().mockResolvedValue({ id: 'new-scenario-id' }),
      deleteCostScenario: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

import { AgentCostCalculatorTab } from '../AgentCostCalculatorTab';

describe('AgentCostCalculatorTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ========== Rendering Tests ==========

  it('renders the component header', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByText('Agent Cost Calculator')).toBeInTheDocument();
    });
  });

  it('renders the configuration section', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByText('Configuration')).toBeInTheDocument();
    });
  });

  it('renders the cost projection section', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByText('Cost Projection')).toBeInTheDocument();
    });
  });

  it('renders strategy selector with all strategies', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      const select = screen.getByLabelText('RAG Strategy');
      expect(select).toBeInTheDocument();
    });
    const options = screen.getByLabelText('RAG Strategy').querySelectorAll('option');
    expect(options.length).toBe(12);
  });

  it('renders model selector with all models', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      const select = screen.getByLabelText('LLM Model');
      expect(select).toBeInTheDocument();
    });
    const options = screen.getByLabelText('LLM Model').querySelectorAll('option');
    expect(options.length).toBe(8);
  });

  it('renders numeric inputs with default values', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByLabelText(/Msgs\/Day/)).toHaveValue(1000);
      expect(screen.getByLabelText(/Active Users/)).toHaveValue(100);
      expect(screen.getByLabelText(/Avg Tokens/)).toHaveValue(1000);
    });
  });

  it('renders the estimate button', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByText('Estimate Cost')).toBeInTheDocument();
    });
  });

  it('renders empty projection placeholder when no estimation', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(
        screen.getByText(/Configure parameters and click/)
      ).toBeInTheDocument();
    });
  });

  it('renders saved scenarios table with mock data', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByText('Saved Scenarios')).toBeInTheDocument();
      expect(screen.getByText('Production Baseline')).toBeInTheDocument();
      expect(screen.getByText('High Traffic Scenario')).toBeInTheDocument();
    });
  });

  it('renders scenario table headers', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByText('Name')).toBeInTheDocument();
      expect(screen.getByText('Strategy')).toBeInTheDocument();
      expect(screen.getByText('Model')).toBeInTheDocument();
      expect(screen.getByText('Monthly')).toBeInTheDocument();
      expect(screen.getByText('Daily Req')).toBeInTheDocument();
      expect(screen.getByText('Actions')).toBeInTheDocument();
    });
  });

  // ========== User Interaction Tests ==========

  it('updates strategy on selection change', async () => {
    render(<AgentCostCalculatorTab />);
    const select = screen.getByLabelText('RAG Strategy');
    fireEvent.change(select, { target: { value: 'Fast' } });
    expect(select).toHaveValue('Fast');
  });

  it('updates model on selection change', async () => {
    render(<AgentCostCalculatorTab />);
    const select = screen.getByLabelText('LLM Model');
    fireEvent.change(select, { target: { value: 'google/gemini-pro' } });
    expect(select).toHaveValue('google/gemini-pro');
  });

  it('updates messages per day input', async () => {
    render(<AgentCostCalculatorTab />);
    const input = screen.getByLabelText(/Msgs\/Day/);
    fireEvent.change(input, { target: { value: '5000' } });
    expect(input).toHaveValue(5000);
  });

  it('updates active users input', async () => {
    render(<AgentCostCalculatorTab />);
    const input = screen.getByLabelText(/Active Users/);
    fireEvent.change(input, { target: { value: '500' } });
    expect(input).toHaveValue(500);
  });

  it('updates avg tokens input', async () => {
    render(<AgentCostCalculatorTab />);
    const input = screen.getByLabelText(/Avg Tokens/);
    fireEvent.change(input, { target: { value: '2000' } });
    expect(input).toHaveValue(2000);
  });

  it('enforces minimum 0 on messages per day', async () => {
    render(<AgentCostCalculatorTab />);
    const input = screen.getByLabelText(/Msgs\/Day/);
    fireEvent.change(input, { target: { value: '-10' } });
    expect(input).toHaveValue(0);
  });

  it('enforces minimum 1 on avg tokens', async () => {
    render(<AgentCostCalculatorTab />);
    const input = screen.getByLabelText(/Avg Tokens/);
    fireEvent.change(input, { target: { value: '0' } });
    expect(input).toHaveValue(1);
  });

  // ========== Estimate Tests ==========

  it('calls estimateAgentCost API on estimate click', async () => {
    const { api } = await import('@/lib/api');
    render(<AgentCostCalculatorTab />);

    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(vi.mocked(api.admin.estimateAgentCost)).toHaveBeenCalledWith({
        strategy: 'Balanced',
        modelId: 'deepseek/deepseek-chat',
        messagesPerDay: 1000,
        activeUsers: 100,
        avgTokensPerRequest: 1000,
      });
    });
  });

  it('shows loading state during estimation', async () => {
    const { api } = await import('@/lib/api');
    let resolveEstimate!: (value: unknown) => void;
    const estimatePromise = new Promise((resolve) => {
      resolveEstimate = resolve;
    });
    vi.mocked(api.admin.estimateAgentCost).mockReturnValueOnce(estimatePromise as never);

    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Calculating...')).toBeInTheDocument();
    });

    resolveEstimate({
      strategy: 'Balanced',
      modelId: 'deepseek/deepseek-chat',
      provider: 'DeepSeek',
      inputCostPer1MTokens: 0.27,
      outputCostPer1MTokens: 1.1,
      costPerRequest: 0.00054,
      dailyProjection: 54.0,
      monthlyProjection: 1620.0,
      totalDailyRequests: 100000,
      avgTokensPerRequest: 1000,
      warnings: [],
    });

    await waitFor(() => {
      expect(screen.getByText('Estimate Cost')).toBeInTheDocument();
    });
  });

  it('falls back to mock data on API failure and shows error', async () => {
    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(
        screen.getByText('Using estimated data. Connect to API for live calculations.')
      ).toBeInTheDocument();
    });

    expect(screen.getByText('Cost / Request')).toBeInTheDocument();
    expect(screen.getByText('Daily Cost')).toBeInTheDocument();
    expect(screen.getByText('Monthly Cost')).toBeInTheDocument();
  });

  it('displays KPI cards after successful estimation', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.estimateAgentCost).mockResolvedValueOnce({
      strategy: 'Fast',
      modelId: 'google/gemini-pro',
      provider: 'Google',
      inputCostPer1MTokens: 0.5,
      outputCostPer1MTokens: 1.5,
      costPerRequest: 0.001,
      dailyProjection: 100.0,
      monthlyProjection: 3000.0,
      totalDailyRequests: 50000,
      avgTokensPerRequest: 1000,
      warnings: [],
    });

    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Cost / Request')).toBeInTheDocument();
      expect(screen.getByText('Daily Requests')).toBeInTheDocument();
      expect(screen.getByText('Daily Cost')).toBeInTheDocument();
      expect(screen.getByText('Monthly Cost')).toBeInTheDocument();
    });
  });

  it('displays model info after estimation', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.estimateAgentCost).mockResolvedValueOnce({
      strategy: 'Balanced',
      modelId: 'deepseek/deepseek-chat',
      provider: 'TestProvider',
      inputCostPer1MTokens: 0.27,
      outputCostPer1MTokens: 1.1,
      costPerRequest: 0.001,
      dailyProjection: 10.0,
      monthlyProjection: 300.0,
      totalDailyRequests: 10000,
      avgTokensPerRequest: 1000,
      warnings: [],
    });

    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('TestProvider')).toBeInTheDocument();
    });
  });

  it('displays warnings when present', async () => {
    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(
        screen.getByText(/Monthly projection exceeds/)
      ).toBeInTheDocument();
    });
  });

  // ========== Save Scenario Tests ==========

  it('shows save dialog when Save Scenario is clicked', async () => {
    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Save Scenario')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Save Scenario'));
    expect(screen.getByPlaceholderText('Scenario name...')).toBeInTheDocument();
    expect(screen.getByText('Save')).toBeInTheDocument();
    expect(screen.getByText('Cancel')).toBeInTheDocument();
  });

  it('disables save button when name is empty', async () => {
    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Save Scenario')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Save Scenario'));
    const saveBtn = screen.getByText('Save');
    expect(saveBtn).toBeDisabled();
  });

  it('enables save button when name is provided', async () => {
    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Save Scenario')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Save Scenario'));
    fireEvent.change(screen.getByPlaceholderText('Scenario name...'), {
      target: { value: 'My Scenario' },
    });
    const saveBtn = screen.getByText('Save');
    expect(saveBtn).not.toBeDisabled();
  });

  it('hides save dialog on cancel', async () => {
    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Save Scenario')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Save Scenario'));
    expect(screen.getByPlaceholderText('Scenario name...')).toBeInTheDocument();

    fireEvent.click(screen.getByText('Cancel'));
    expect(screen.queryByPlaceholderText('Scenario name...')).not.toBeInTheDocument();
    expect(screen.getByText('Save Scenario')).toBeInTheDocument();
  });

  it('calls saveCostScenario API on save', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.estimateAgentCost).mockResolvedValueOnce({
      strategy: 'Balanced',
      modelId: 'deepseek/deepseek-chat',
      provider: 'DeepSeek',
      inputCostPer1MTokens: 0.27,
      outputCostPer1MTokens: 1.1,
      costPerRequest: 0.00054,
      dailyProjection: 54.0,
      monthlyProjection: 1620.0,
      totalDailyRequests: 100000,
      avgTokensPerRequest: 1000,
      warnings: ['High cost'],
    });

    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Save Scenario')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Save Scenario'));
    fireEvent.change(screen.getByPlaceholderText('Scenario name...'), {
      target: { value: 'Test Scenario' },
    });
    fireEvent.click(screen.getByText('Save'));

    await waitFor(() => {
      expect(vi.mocked(api.admin.saveCostScenario)).toHaveBeenCalledWith(
        expect.objectContaining({
          name: 'Test Scenario',
          strategy: 'Balanced',
          modelId: 'deepseek/deepseek-chat',
        })
      );
    });
  });

  it('shows error on save failure', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.saveCostScenario).mockRejectedValueOnce(new Error('Save failed'));

    render(<AgentCostCalculatorTab />);
    fireEvent.click(screen.getByText('Estimate Cost'));

    await waitFor(() => {
      expect(screen.getByText('Save Scenario')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByText('Save Scenario'));
    fireEvent.change(screen.getByPlaceholderText('Scenario name...'), {
      target: { value: 'Failing Scenario' },
    });
    fireEvent.click(screen.getByText('Save'));

    await waitFor(() => {
      expect(screen.getByText('Failed to save scenario.')).toBeInTheDocument();
    });
  });

  // ========== Scenario Table Actions ==========

  it('renders load and delete buttons for each scenario', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByLabelText('Load scenario Production Baseline')).toBeInTheDocument();
      expect(screen.getByLabelText('Delete scenario Production Baseline')).toBeInTheDocument();
      expect(screen.getByLabelText('Load scenario High Traffic Scenario')).toBeInTheDocument();
      expect(screen.getByLabelText('Delete scenario High Traffic Scenario')).toBeInTheDocument();
    });
  });

  it('loads scenario into form when load button is clicked', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByLabelText('Load scenario High Traffic Scenario')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByLabelText('Load scenario High Traffic Scenario'));

    expect(screen.getByLabelText('RAG Strategy')).toHaveValue('Fast');
    expect(screen.getByLabelText('LLM Model')).toHaveValue('meta-llama/llama-3.3-70b-instruct:free');
    expect(screen.getByLabelText(/Msgs\/Day/)).toHaveValue(5000);
    expect(screen.getByLabelText(/Active Users/)).toHaveValue(500);
    expect(screen.getByLabelText(/Avg Tokens/)).toHaveValue(800);
  });

  it('displays estimation after loading scenario', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByLabelText('Load scenario Production Baseline')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByLabelText('Load scenario Production Baseline'));

    await waitFor(() => {
      expect(screen.getByText('Cost / Request')).toBeInTheDocument();
      expect(screen.getByText('Monthly Cost')).toBeInTheDocument();
    });
  });

  it('calls deleteCostScenario API on delete', async () => {
    const { api } = await import('@/lib/api');
    render(<AgentCostCalculatorTab />);

    await waitFor(() => {
      expect(screen.getByLabelText('Delete scenario Production Baseline')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByLabelText('Delete scenario Production Baseline'));

    await waitFor(() => {
      expect(vi.mocked(api.admin.deleteCostScenario)).toHaveBeenCalledWith(
        '00000000-0000-0000-0000-000000000001'
      );
    });
  });

  it('shows error on delete failure', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.deleteCostScenario).mockRejectedValueOnce(new Error('Delete failed'));

    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByLabelText('Delete scenario Production Baseline')).toBeInTheDocument();
    });

    fireEvent.click(screen.getByLabelText('Delete scenario Production Baseline'));

    await waitFor(() => {
      expect(screen.getByText('Failed to delete scenario.')).toBeInTheDocument();
    });
  });

  // ========== Scenario Display Tests ==========

  it('shows strategy labels in scenario table', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      const cells = screen.getAllByText('Balanced');
      expect(cells.length).toBeGreaterThanOrEqual(1);
    });
  });

  it('shows model labels in scenario table', async () => {
    render(<AgentCostCalculatorTab />);
    await waitFor(() => {
      expect(screen.getByText('DeepSeek Chat')).toBeInTheDocument();
      expect(screen.getByText('Llama 3.3 70B (Free)')).toBeInTheDocument();
    });
  });

  it('fetches scenarios from API on mount', async () => {
    const { api } = await import('@/lib/api');
    render(<AgentCostCalculatorTab />);

    await waitFor(() => {
      expect(vi.mocked(api.admin.getCostScenarios)).toHaveBeenCalledWith({
        page: 1,
        pageSize: 50,
      });
    });
  });

  it('uses API data when getCostScenarios succeeds', async () => {
    const { api } = await import('@/lib/api');
    vi.mocked(api.admin.getCostScenarios).mockResolvedValueOnce({
      items: [
        {
          id: 'api-scenario-1',
          name: 'API Scenario',
          strategy: 'Fast',
          modelId: 'deepseek/deepseek-chat',
          messagesPerDay: 2000,
          activeUsers: 200,
          avgTokensPerRequest: 1500,
          costPerRequest: 0.001,
          dailyProjection: 400.0,
          monthlyProjection: 12000.0,
          warnings: [],
          createdByUserId: 'user-1',
          createdAt: '2026-02-11T00:00:00Z',
        },
      ],
      total: 1,
      page: 1,
      pageSize: 50,
    });

    render(<AgentCostCalculatorTab />);

    await waitFor(() => {
      expect(screen.getByText('API Scenario')).toBeInTheDocument();
    });
  });
});
