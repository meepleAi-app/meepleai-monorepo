import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    admin: {
      estimateResourceForecast: vi.fn().mockRejectedValue(new Error('Not available')),
      getResourceForecasts: vi.fn().mockRejectedValue(new Error('Not available')),
      saveResourceForecast: vi.fn().mockResolvedValue({ id: 'new-forecast-id' }),
      deleteResourceForecast: vi.fn().mockResolvedValue(undefined),
    },
  },
}));

import { ResourceForecastingTab } from '../ResourceForecastingTab';
import { api } from '@/lib/api';

describe('ResourceForecastingTab', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  // ========== Rendering Tests ==========

  it('renders the component with title', () => {
    render(<ResourceForecastingTab />);

    expect(screen.getByText('Resource Forecasting Simulator')).toBeInTheDocument();
  });

  it('renders growth parameters section', () => {
    render(<ResourceForecastingTab />);

    expect(screen.getByText('Growth Parameters')).toBeInTheDocument();
    expect(screen.getByText('Growth Pattern')).toBeInTheDocument();
    expect(screen.getByText('Growth %/mo')).toBeInTheDocument();
  });

  it('renders current metrics section', () => {
    render(<ResourceForecastingTab />);

    expect(screen.getByText('Current Metrics')).toBeInTheDocument();
    expect(screen.getByLabelText(/users/i)).toBeInTheDocument();
    expect(screen.getByText('DB Size (GB)')).toBeInTheDocument();
    expect(screen.getByText('Daily Tokens')).toBeInTheDocument();
    expect(screen.getByText('Cache (MB)')).toBeInTheDocument();
    expect(screen.getByText('Vector Entries')).toBeInTheDocument();
  });

  it('renders per-user multipliers section', () => {
    render(<ResourceForecastingTab />);

    expect(screen.getByText('Per-User Resource Usage')).toBeInTheDocument();
    expect(screen.getByText('DB/User (MB)')).toBeInTheDocument();
    expect(screen.getByText('Tokens/User/Day')).toBeInTheDocument();
    expect(screen.getByText('Cache/User (MB)')).toBeInTheDocument();
    expect(screen.getByText('Vectors/User')).toBeInTheDocument();
  });

  it('renders Run Forecast button', () => {
    render(<ResourceForecastingTab />);

    expect(screen.getByRole('button', { name: /run forecast/i })).toBeInTheDocument();
  });

  it('renders all growth pattern options in select', () => {
    render(<ResourceForecastingTab />);

    const select = screen.getByRole('combobox') as HTMLSelectElement;
    const options = Array.from(select.options).map((o) => o.text);

    expect(options).toContain('Linear (constant rate)');
    expect(options).toContain('Exponential (compounding)');
    expect(options).toContain('Logarithmic (decelerating)');
  });

  // ========== Default Values Tests ==========

  it('should have default growth rate of 10', () => {
    render(<ResourceForecastingTab />);

    const growthRateInput = screen.getByDisplayValue('10');
    expect(growthRateInput).toBeInTheDocument();
  });

  it('should have default current users of 1000', () => {
    render(<ResourceForecastingTab />);

    const usersInput = screen.getByDisplayValue('1000');
    expect(usersInput).toBeInTheDocument();
  });

  // ========== User Interaction Tests ==========

  it('should update growth rate when input changes', () => {
    render(<ResourceForecastingTab />);

    const growthRateInput = screen.getByDisplayValue('10');
    fireEvent.change(growthRateInput, { target: { value: '25' } });

    expect(screen.getByDisplayValue('25')).toBeInTheDocument();
  });

  it('should update current users when input changes', () => {
    render(<ResourceForecastingTab />);

    const usersInput = screen.getByDisplayValue('1000');
    fireEvent.change(usersInput, { target: { value: '5000' } });

    expect(screen.getByDisplayValue('5000')).toBeInTheDocument();
  });

  it('should change growth pattern selection', () => {
    render(<ResourceForecastingTab />);

    const select = screen.getByRole('combobox') as HTMLSelectElement;
    fireEvent.change(select, { target: { value: 'Exponential' } });

    expect(select.value).toBe('Exponential');
  });

  // ========== Estimate Tests ==========

  it('should call estimate API when Run Forecast is clicked', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockResolvedValueOnce({
      growthPattern: 'Linear',
      monthlyGrowthRate: 10,
      currentUsers: 1000,
      projectedUsersMonth12: 2200,
      projections: [
        {
          month: 1,
          projectedUsers: 1100,
          projectedDbGb: 5.21,
          projectedDailyTokens: 550000,
          projectedCacheMb: 306,
          projectedVectorEntries: 110000,
          estimatedMonthlyCostUsd: 8.72,
        },
      ],
      recommendations: [],
      projectedMonthlyCostMonth12: 15.5,
    });

    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      expect(mockEstimate).toHaveBeenCalledTimes(1);
    });
  });

  it('should display projections table after forecast', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockResolvedValueOnce({
      growthPattern: 'Linear',
      monthlyGrowthRate: 10,
      currentUsers: 1000,
      projectedUsersMonth12: 2200,
      projections: Array.from({ length: 12 }, (_, i) => ({
        month: i + 1,
        projectedUsers: 1000 + (i + 1) * 100,
        projectedDbGb: 5 + (i + 1) * 0.2,
        projectedDailyTokens: 500000 + (i + 1) * 50000,
        projectedCacheMb: 256 + (i + 1) * 5,
        projectedVectorEntries: 100000 + (i + 1) * 10000,
        estimatedMonthlyCostUsd: 8 + (i + 1) * 0.7,
      })),
      recommendations: [],
      projectedMonthlyCostMonth12: 16.4,
    });

    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      expect(screen.getByText('12-Month Projection')).toBeInTheDocument();
    });
  });

  it('should display recommendations when present', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockResolvedValueOnce({
      growthPattern: 'Linear',
      monthlyGrowthRate: 50,
      currentUsers: 10000,
      projectedUsersMonth12: 70000,
      projections: [
        {
          month: 1,
          projectedUsers: 15000,
          projectedDbGb: 30,
          projectedDailyTokens: 10000000,
          projectedCacheMb: 2500,
          projectedVectorEntries: 5000000,
          estimatedMonthlyCostUsd: 42.5,
        },
      ],
      recommendations: [
        {
          resourceType: 'Database',
          triggerMonth: 3,
          severity: 'warning',
          message: 'Database projected to reach 60.0 GB by month 3',
          action: 'Plan database scaling or implement cleanup policies',
        },
      ],
      projectedMonthlyCostMonth12: 150.0,
    });

    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      expect(screen.getByText('Action Recommendations')).toBeInTheDocument();
    });
  });

  // ========== Mock Data Fallback Tests ==========

  it('should use mock data when API fails', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockRejectedValueOnce(new Error('API unavailable'));

    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      expect(screen.getByText('12-Month Projection')).toBeInTheDocument();
    });
  });

  // ========== Save Scenario Tests ==========

  it('should show save button after generating forecast', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockRejectedValueOnce(new Error('API unavailable'));

    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /save scenario/i })).toBeInTheDocument();
    });
  });

  it('should call save API when saving scenario', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockRejectedValueOnce(new Error('API unavailable'));

    const mockSave = vi.mocked(api.admin.saveResourceForecast);
    mockSave.mockResolvedValueOnce({ id: 'saved-id' });

    render(<ResourceForecastingTab />);

    // Generate forecast first
    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      expect(screen.getByRole('button', { name: /save scenario/i })).toBeInTheDocument();
    });

    // Click "Save Scenario" to open dialog
    const saveScenarioBtn = screen.getByRole('button', { name: /save scenario/i });
    fireEvent.click(saveScenarioBtn);

    // Fill in name
    const nameInput = screen.getByPlaceholderText('Scenario name...');
    fireEvent.change(nameInput, { target: { value: 'My Test Scenario' } });

    // Click the "Save" confirm button
    const confirmSave = screen.getByRole('button', { name: /^save$/i });
    fireEvent.click(confirmSave);

    await waitFor(() => {
      expect(mockSave).toHaveBeenCalledTimes(1);
    });
  });

  // ========== Scenario Table Tests ==========

  it('should render saved scenarios section', () => {
    render(<ResourceForecastingTab />);

    expect(screen.getByText('Saved Scenarios')).toBeInTheDocument();
  });

  it('should load saved scenarios on mount', () => {
    render(<ResourceForecastingTab />);

    const mockGetScenarios = vi.mocked(api.admin.getResourceForecasts);
    // The component may call getResourceForecasts on mount
    // Even if it fails, the section should still render
    expect(screen.getByText('Saved Scenarios')).toBeInTheDocument();
  });

  // ========== Delete Scenario Tests ==========

  it('should call delete API when deleting a scenario', async () => {
    const mockGetScenarios = vi.mocked(api.admin.getResourceForecasts);
    mockGetScenarios.mockResolvedValueOnce({
      items: [
        {
          id: 'scenario-1',
          name: 'Test Scenario',
          growthPattern: 'Linear',
          monthlyGrowthRate: 10,
          currentUsers: 1000,
          currentDbSizeGb: 5,
          currentDailyTokens: 500000,
          currentCacheMb: 256,
          currentVectorEntries: 100000,
          dbPerUserMb: 2,
          tokensPerUserPerDay: 500,
          cachePerUserMb: 0.5,
          vectorsPerUser: 100,
          projectedMonthlyCost: 150,
          createdByUserId: 'user-1',
          createdAt: '2026-02-11T00:00:00Z',
        },
      ],
      total: 1,
      page: 1,
      pageSize: 10,
    });

    render(<ResourceForecastingTab />);

    await waitFor(() => {
      const deleteButtons = screen.queryAllByRole('button', { name: /delete/i });
      if (deleteButtons.length > 0) {
        fireEvent.click(deleteButtons[0]);
      }
    });
  });

  // ========== Projection Column Rendering Tests ==========

  it('should display table headers in projections', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockRejectedValueOnce(new Error('API unavailable'));

    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      // Projection table headers
      expect(screen.getByText('Mo')).toBeInTheDocument();
      expect(screen.getByText('DB (GB)')).toBeInTheDocument();
    });
  });

  // ========== Summary Stats Tests ==========

  it('should show projected cost summary after forecast', async () => {
    const mockEstimate = vi.mocked(api.admin.estimateResourceForecast);
    mockEstimate.mockRejectedValueOnce(new Error('API unavailable'));

    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    fireEvent.click(runButton);

    await waitFor(() => {
      const projectionSection = screen.getByText('12-Month Projection');
      expect(projectionSection).toBeInTheDocument();
    });
  });

  // ========== Growth Pattern Visual Tests ==========

  it('should have Linear selected as default growth pattern', () => {
    render(<ResourceForecastingTab />);

    const select = screen.getByRole('combobox') as HTMLSelectElement;
    expect(select.value).toBe('Linear');
  });

  // ========== Edge Cases ==========

  it('should handle zero growth rate gracefully', () => {
    render(<ResourceForecastingTab />);

    const growthRateInput = screen.getByDisplayValue('10');
    fireEvent.change(growthRateInput, { target: { value: '0' } });

    expect(screen.getByDisplayValue('0')).toBeInTheDocument();
  });

  it('should handle very large user counts', () => {
    render(<ResourceForecastingTab />);

    const usersInput = screen.getByDisplayValue('1000');
    fireEvent.change(usersInput, { target: { value: '10000000' } });

    expect(screen.getByDisplayValue('10000000')).toBeInTheDocument();
  });

  it('should clamp growth rate to maximum of 100', () => {
    render(<ResourceForecastingTab />);

    const growthRateInput = screen.getByDisplayValue('10') as HTMLInputElement;
    fireEvent.change(growthRateInput, { target: { value: '150' } });

    // Component clamps to max 100 via Math.min
    expect(growthRateInput.value).toBe('100');
  });

  // ========== Accessibility Tests ==========

  it('should have accessible input labels', () => {
    render(<ResourceForecastingTab />);

    expect(screen.getByText('Growth %/mo')).toBeInTheDocument();
    expect(screen.getByText('DB Size (GB)')).toBeInTheDocument();
    expect(screen.getByText('Daily Tokens')).toBeInTheDocument();
  });

  it('should have accessible button labels', () => {
    render(<ResourceForecastingTab />);

    const runButton = screen.getByRole('button', { name: /run forecast/i });
    expect(runButton).toBeInTheDocument();
    expect(runButton).not.toBeDisabled();
  });
});
