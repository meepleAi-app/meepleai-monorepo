/**
 * ApiKeyFilterPanel Component Tests (Issue #910)
 *
 * Unit tests for API Key filter panel with comprehensive coverage.
 * Target: 90%+ code coverage with edge cases and user interactions.
 */

import React from 'react';
import { render, screen, fireEvent, within } from '@testing-library/react';
import userEvent from '@testing-library/user-event';
import { ApiKeyFilterPanel } from '../ApiKeyFilterPanel';
import type { ApiKeyFilters } from '@/types';

describe('ApiKeyFilterPanel', () => {
  const mockOnFiltersChange = vi.fn();
  const mockOnReset = vi.fn();

  beforeEach(() => {
    mockOnFiltersChange.mockClear();
    mockOnReset.mockClear();
  });

  describe('Rendering', () => {
    it('should render all filter sections', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.getByText('Filters')).toBeInTheDocument();
      expect(screen.getByLabelText('Search API keys by name')).toBeInTheDocument();
      expect(screen.getByLabelText('Status')).toBeInTheDocument();
      expect(screen.getByText('Scopes')).toBeInTheDocument();
      expect(screen.getByText('Created Date')).toBeInTheDocument();
      expect(screen.getByText('Expires Date')).toBeInTheDocument();
      expect(screen.getByLabelText('Last Used')).toBeInTheDocument();
    });

    it('should render Clear All button when filters are active', () => {
      const filters: ApiKeyFilters = { search: 'test' };

      render(<ApiKeyFilterPanel filters={filters} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.getByText('Clear All')).toBeInTheDocument();
    });

    it('should not render Clear All button when no filters are active', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.queryByText('Clear All')).not.toBeInTheDocument();
    });

    it('should render active filters summary when filters are applied', () => {
      const filters: ApiKeyFilters = {
        search: 'production',
        status: 'active',
        scopes: ['read', 'write'],
      };

      render(<ApiKeyFilterPanel filters={filters} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.getByText('Active Filters')).toBeInTheDocument();
      expect(screen.getByText('Search: production')).toBeInTheDocument();
      expect(screen.getByText('Status: Active')).toBeInTheDocument();
      expect(screen.getByText('Scopes: 2')).toBeInTheDocument();
    });

    it('should apply custom className', () => {
      const { container } = render(
        <ApiKeyFilterPanel
          filters={{}}
          onFiltersChange={mockOnFiltersChange}
          className="custom-class"
        />
      );

      const panel = container.querySelector('.custom-class');
      expect(panel).toBeInTheDocument();
    });
  });

  describe('Search Filter', () => {
    it('should update search filter on input change', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const searchInput = screen.getByLabelText('Search API keys by name');
      fireEvent.change(searchInput, { target: { value: 'my-api-key' } });

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        search: 'my-api-key',
      });
    });

    it('should clear search filter when input is empty', async () => {
      const user = userEvent.setup();

      render(
        <ApiKeyFilterPanel filters={{ search: 'test' }} onFiltersChange={mockOnFiltersChange} />
      );

      const searchInput = screen.getByLabelText('Search API keys by name');
      await user.clear(searchInput);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        search: undefined,
      });
    });
  });

  describe('Status Filter', () => {
    it('should update status filter on selection', async () => {
      const user = userEvent.setup();

      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const statusTrigger = screen.getByRole('combobox', { name: /status/i });
      await user.click(statusTrigger);

      const activeOption = screen.getByRole('option', { name: /active/i });
      await user.click(activeOption);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        status: 'active',
      });
    });

    it('should clear status filter when "All" is selected', async () => {
      const user = userEvent.setup();

      render(
        <ApiKeyFilterPanel filters={{ status: 'active' }} onFiltersChange={mockOnFiltersChange} />
      );

      const statusTrigger = screen.getByRole('combobox', { name: /status/i });
      await user.click(statusTrigger);

      const allOption = screen.getByRole('option', { name: /all keys/i });
      await user.click(allOption);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        status: undefined,
      });
    });
  });

  describe('Scope Filter', () => {
    it('should add scope on button click', async () => {
      const user = userEvent.setup();

      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const readButton = screen.getByRole('button', { name: /filter by read scope/i });
      await user.click(readButton);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        scopes: ['read'],
      });
    });

    it('should remove scope when already selected', async () => {
      const user = userEvent.setup();

      render(
        <ApiKeyFilterPanel
          filters={{ scopes: ['read', 'write'] }}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const readButton = screen.getByRole('button', { name: /filter by read scope/i });
      await user.click(readButton);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        scopes: ['write'],
      });
    });

    it('should handle multiple scope selections', async () => {
      const user = userEvent.setup();

      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const readButton = screen.getByRole('button', { name: /filter by read scope/i });
      const writeButton = screen.getByRole('button', { name: /filter by write scope/i });
      const adminButton = screen.getByRole('button', { name: /filter by admin scope/i });

      await user.click(readButton);
      expect(mockOnFiltersChange).toHaveBeenLastCalledWith({
        scopes: ['read'],
      });

      await user.click(writeButton);
      expect(mockOnFiltersChange).toHaveBeenLastCalledWith({
        scopes: ['write'],
      });

      await user.click(adminButton);
      expect(mockOnFiltersChange).toHaveBeenLastCalledWith({
        scopes: ['admin'],
      });
    });

    it('should clear scopes when last scope is deselected', async () => {
      const user = userEvent.setup();

      render(
        <ApiKeyFilterPanel filters={{ scopes: ['read'] }} onFiltersChange={mockOnFiltersChange} />
      );

      const readButton = screen.getByRole('button', { name: /filter by read scope/i });
      await user.click(readButton);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        scopes: undefined,
      });
    });

    it('should apply correct aria-pressed state for selected scopes', () => {
      render(
        <ApiKeyFilterPanel
          filters={{ scopes: ['read', 'write'] }}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const readButton = screen.getByRole('button', { name: /filter by read scope/i });
      const writeButton = screen.getByRole('button', { name: /filter by write scope/i });
      const adminButton = screen.getByRole('button', { name: /filter by admin scope/i });

      expect(readButton).toHaveAttribute('aria-pressed', 'true');
      expect(writeButton).toHaveAttribute('aria-pressed', 'true');
      expect(adminButton).toHaveAttribute('aria-pressed', 'false');
    });
  });

  describe('Date Range Filters', () => {
    it('should update createdFrom date', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const createdFromInput = screen.getByLabelText('Created from date');
      fireEvent.change(createdFromInput, { target: { value: '2024-01-01' } });

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        createdFrom: new Date('2024-01-01'),
      });
    });

    it('should update createdTo date', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const createdToInput = screen.getByLabelText('Created to date');
      fireEvent.change(createdToInput, { target: { value: '2024-12-31' } });

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        createdTo: new Date('2024-12-31'),
      });
    });

    it('should update expiresFrom date', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const expiresFromInput = screen.getByLabelText('Expires from date');
      fireEvent.change(expiresFromInput, { target: { value: '2025-01-01' } });

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        expiresFrom: new Date('2025-01-01'),
      });
    });

    it('should update expiresTo date', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const expiresToInput = screen.getByLabelText('Expires to date');
      fireEvent.change(expiresToInput, { target: { value: '2025-12-31' } });

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        expiresTo: new Date('2025-12-31'),
      });
    });

    it('should clear date when input is empty', () => {
      render(
        <ApiKeyFilterPanel
          filters={{ createdFrom: new Date('2024-01-01') }}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const createdFromInput = screen.getByLabelText('Created from date');
      fireEvent.change(createdFromInput, { target: { value: '' } });

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        createdFrom: undefined,
      });
    });

    it('should display active date filters in summary', () => {
      const filters: ApiKeyFilters = {
        createdFrom: new Date('2024-01-01'),
        createdTo: new Date('2024-12-31'),
        expiresFrom: new Date('2025-01-01'),
        expiresTo: new Date('2025-12-31'),
      };

      render(<ApiKeyFilterPanel filters={filters} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.getByText(/Created:/)).toBeInTheDocument();
      expect(screen.getByText(/Expires:/)).toBeInTheDocument();
    });
  });

  describe('Last Used Filter', () => {
    it('should update lastUsedDays on selection', async () => {
      const user = userEvent.setup();

      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const lastUsedTrigger = screen.getByRole('combobox', { name: /last used/i });
      await user.click(lastUsedTrigger);

      const option30Days = screen.getByRole('option', { name: /last 30 days/i });
      await user.click(option30Days);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        lastUsedDays: 30,
      });
    });

    it('should clear lastUsedDays when "Any time" is selected', async () => {
      const user = userEvent.setup();

      render(
        <ApiKeyFilterPanel filters={{ lastUsedDays: 30 }} onFiltersChange={mockOnFiltersChange} />
      );

      const lastUsedTrigger = screen.getByRole('combobox', { name: /last used/i });
      await user.click(lastUsedTrigger);

      const anyTimeOption = screen.getByRole('option', { name: /any time/i });
      await user.click(anyTimeOption);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        lastUsedDays: undefined,
      });
    });

    it('should display lastUsedDays in active filters summary', () => {
      render(
        <ApiKeyFilterPanel filters={{ lastUsedDays: 7 }} onFiltersChange={mockOnFiltersChange} />
      );

      expect(screen.getByText('Used: Last 7d')).toBeInTheDocument();
    });
  });

  describe('Clear All Functionality', () => {
    it('should call onReset when Clear All is clicked and onReset is provided', async () => {
      const user = userEvent.setup();

      render(
        <ApiKeyFilterPanel
          filters={{ search: 'test', status: 'active' }}
          onFiltersChange={mockOnFiltersChange}
          onReset={mockOnReset}
        />
      );

      const clearButton = screen.getByRole('button', { name: /clear all filters/i });
      await user.click(clearButton);

      expect(mockOnReset).toHaveBeenCalled();
    });

    it('should call onFiltersChange with empty object when onReset is not provided', async () => {
      const user = userEvent.setup();

      render(
        <ApiKeyFilterPanel
          filters={{ search: 'test', status: 'active' }}
          onFiltersChange={mockOnFiltersChange}
        />
      );

      const clearButton = screen.getByRole('button', { name: /clear all filters/i });
      await user.click(clearButton);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({});
    });
  });

  describe('Accessibility', () => {
    it('should have proper aria-labels for all inputs', () => {
      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.getByLabelText('Search API keys by name')).toBeInTheDocument();
      expect(screen.getByLabelText('Status')).toBeInTheDocument();
      expect(screen.getByLabelText('Filter by Read scope')).toBeInTheDocument();
      expect(screen.getByLabelText('Filter by Write scope')).toBeInTheDocument();
      expect(screen.getByLabelText('Filter by Admin scope')).toBeInTheDocument();
      expect(screen.getByLabelText('Created from date')).toBeInTheDocument();
      expect(screen.getByLabelText('Created to date')).toBeInTheDocument();
      expect(screen.getByLabelText('Expires from date')).toBeInTheDocument();
      expect(screen.getByLabelText('Expires to date')).toBeInTheDocument();
      expect(screen.getByLabelText('Last Used')).toBeInTheDocument();
    });

    it('should support keyboard navigation for scope buttons', async () => {
      const user = userEvent.setup();

      render(<ApiKeyFilterPanel filters={{}} onFiltersChange={mockOnFiltersChange} />);

      const readButton = screen.getByRole('button', { name: /filter by read scope/i });
      readButton.focus();
      expect(readButton).toHaveFocus();

      await user.keyboard('{Enter}');
      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        scopes: ['read'],
      });
    });
  });

  describe('Edge Cases', () => {
    it('should handle undefined filter values gracefully', () => {
      const filters: ApiKeyFilters = {
        search: undefined,
        status: undefined,
        scopes: undefined,
        createdFrom: undefined,
        createdTo: undefined,
        expiresFrom: undefined,
        expiresTo: undefined,
        lastUsedDays: undefined,
      };

      render(<ApiKeyFilterPanel filters={filters} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.queryByText('Active Filters')).not.toBeInTheDocument();
    });

    it('should handle empty scopes array', () => {
      render(<ApiKeyFilterPanel filters={{ scopes: [] }} onFiltersChange={mockOnFiltersChange} />);

      expect(screen.queryByText('Active Filters')).not.toBeInTheDocument();
    });

    it('should preserve other filters when updating one filter', async () => {
      const user = userEvent.setup();
      const filters: ApiKeyFilters = {
        search: 'test',
        status: 'active',
      };

      render(<ApiKeyFilterPanel filters={filters} onFiltersChange={mockOnFiltersChange} />);

      const readButton = screen.getByRole('button', { name: /filter by read scope/i });
      await user.click(readButton);

      expect(mockOnFiltersChange).toHaveBeenCalledWith({
        ...filters,
        scopes: ['read'],
      });
    });
  });
});
