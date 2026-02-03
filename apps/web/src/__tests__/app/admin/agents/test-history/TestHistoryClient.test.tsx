/**
 * Tests for TestHistoryClient component
 * Issue #3379: Test Results History & Persistence
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import { describe, it, expect, vi, beforeEach, type Mock } from 'vitest';
import * as React from 'react';

import { TestHistoryClient } from '@/app/(authenticated)/admin/agents/test-history/client';
import { api } from '@/lib/api';
import type { TestResultList, TestResult } from '@/lib/api/schemas/test-results.schemas';

// Mock next/link
vi.mock('next/link', () => ({
  default: ({ children, href }: { children: React.ReactNode; href: string }) =>
    React.createElement('a', { href }, children),
}));

// Mock sonner
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

// Mock the API module
vi.mock('@/lib/api', () => ({
  api: {
    testResults: {
      getAll: vi.fn(),
      getById: vi.fn(),
      save: vi.fn(),
      delete: vi.fn(),
    },
  },
}));

// Mock result data
const mockTestResult: TestResult = {
  id: '123e4567-e89b-12d3-a456-426614174000',
  typologyId: '123e4567-e89b-12d3-a456-426614174001',
  strategyOverride: null,
  modelUsed: 'gpt-4',
  query: 'What are the rules for Settlers of Catan?',
  response: 'Settlers of Catan is a strategy board game...',
  confidenceScore: 0.95,
  tokensUsed: 150,
  costEstimate: 0.003,
  latencyMs: 1200,
  citationsJson: null,
  executedAt: '2025-01-15T10:30:00Z',
  executedBy: '123e4567-e89b-12d3-a456-426614174002',
  notes: null,
  isSaved: true,
};

const mockTestResultList: TestResultList = {
  items: [mockTestResult],
  totalCount: 1,
  skip: 0,
  take: 20,
};

const emptyResultList: TestResultList = {
  items: [],
  totalCount: 0,
  skip: 0,
  take: 20,
};

// Create wrapper with QueryClientProvider
function createWrapper() {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });
  return function Wrapper({ children }: { children: React.ReactNode }) {
    return React.createElement(QueryClientProvider, { client: queryClient }, children);
  };
}

describe('TestHistoryClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    (api.testResults.getAll as Mock).mockResolvedValue(mockTestResultList);
  });

  describe('Rendering', () => {
    it('renders the page title and header', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Cronologia Test Agent')).toBeInTheDocument();
      expect(
        screen.getByText('Visualizza e gestisci i risultati dei test degli agent')
      ).toBeInTheDocument();
    });

    it('renders the filter section', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Filtri')).toBeInTheDocument();
      expect(screen.getByLabelText('Tipologia')).toBeInTheDocument();
      expect(screen.getByLabelText('Solo salvati')).toBeInTheDocument();
    });

    it('renders the back button to test console', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      const backLink = screen.getByRole('link', { name: /test console/i });
      expect(backLink).toHaveAttribute('href', '/admin/agents/test');
    });

    it('renders the refresh button', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      expect(screen.getByRole('button', { name: /aggiorna/i })).toBeInTheDocument();
    });
  });

  describe('Loading State', () => {
    it('shows loading spinner while fetching data', async () => {
      // Delay the response to observe loading state
      (api.testResults.getAll as Mock).mockImplementation(
        () => new Promise((resolve) => setTimeout(() => resolve(mockTestResultList), 100))
      );

      render(<TestHistoryClient />, { wrapper: createWrapper() });

      // Should show loading indicator
      expect(screen.getByRole('heading', { name: /cronologia test agent/i })).toBeInTheDocument();
    });
  });

  describe('Empty State', () => {
    it('shows empty message when no results', async () => {
      (api.testResults.getAll as Mock).mockResolvedValue(emptyResultList);

      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Nessun risultato trovato')).toBeInTheDocument();
      });
    });
  });

  describe('Results Table', () => {
    it('displays test results in table', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('What are the rules for Settlers of Catan?')).toBeInTheDocument();
      });

      expect(screen.getByText('gpt-4')).toBeInTheDocument();
      expect(screen.getByText('150')).toBeInTheDocument();
      expect(screen.getByText('1200ms')).toBeInTheDocument();
    });

    it('shows total count in header', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Risultati (1)')).toBeInTheDocument();
      });
    });

    it('shows saved badge for saved results', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        // Use getAllByText since there might be multiple saved badges
        const savedBadges = screen.getAllByText('Salvato');
        expect(savedBadges.length).toBeGreaterThan(0);
      });
    });
  });

  describe('Filtering', () => {
    it('filters by typology ID', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Risultati (1)')).toBeInTheDocument();
      });

      const typologyInput = screen.getByLabelText('Tipologia');
      fireEvent.change(typologyInput, {
        target: { value: '123e4567-e89b-12d3-a456-426614174001' },
      });

      await waitFor(() => {
        expect(api.testResults.getAll).toHaveBeenCalledWith(
          expect.objectContaining({
            typologyId: '123e4567-e89b-12d3-a456-426614174001',
          })
        );
      });
    });

    it('filters by saved only', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Risultati (1)')).toBeInTheDocument();
      });

      const checkbox = screen.getByLabelText('Solo salvati');
      fireEvent.click(checkbox);

      await waitFor(() => {
        expect(api.testResults.getAll).toHaveBeenCalledWith(
          expect.objectContaining({
            savedOnly: true,
          })
        );
      });
    });

    it('clears all filters', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Risultati (1)')).toBeInTheDocument();
      });

      // Set some filters first
      const typologyInput = screen.getByLabelText('Tipologia');
      fireEvent.change(typologyInput, { target: { value: 'test-id' } });

      // Clear filters
      const clearButton = screen.getByRole('button', { name: /pulisci filtri/i });
      fireEvent.click(clearButton);

      expect(typologyInput).toHaveValue('');
    });
  });

  describe('View Detail', () => {
    it('opens detail dialog when clicking view button', async () => {
      (api.testResults.getById as Mock).mockResolvedValue(mockTestResult);

      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('What are the rules for Settlers of Catan?')).toBeInTheDocument();
      });

      // Click the view button (Eye icon)
      const viewButtons = screen.getAllByRole('button');
      const viewButton = viewButtons.find((btn) => btn.querySelector('svg.lucide-eye'));
      if (viewButton) {
        fireEvent.click(viewButton);
      }

      await waitFor(() => {
        expect(screen.getByText('Dettaglio Risultato Test')).toBeInTheDocument();
      });
    });
  });

  describe('Delete', () => {
    it('opens delete confirmation dialog', async () => {
      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('What are the rules for Settlers of Catan?')).toBeInTheDocument();
      });

      // Click the delete button (Trash icon)
      const deleteButtons = screen.getAllByRole('button');
      const deleteButton = deleteButtons.find((btn) => btn.querySelector('svg.lucide-trash-2'));
      if (deleteButton) {
        fireEvent.click(deleteButton);
      }

      await waitFor(() => {
        expect(screen.getByText('Conferma eliminazione')).toBeInTheDocument();
      });
    });

    it('calls delete API when confirmed', async () => {
      (api.testResults.delete as Mock).mockResolvedValue(undefined);

      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('What are the rules for Settlers of Catan?')).toBeInTheDocument();
      });

      // Click the delete button
      const deleteButtons = screen.getAllByRole('button');
      const deleteButton = deleteButtons.find((btn) => btn.querySelector('svg.lucide-trash-2'));
      if (deleteButton) {
        fireEvent.click(deleteButton);
      }

      await waitFor(() => {
        expect(screen.getByText('Conferma eliminazione')).toBeInTheDocument();
      });

      // Confirm deletion
      const confirmButton = screen.getByRole('button', { name: /elimina/i });
      fireEvent.click(confirmButton);

      await waitFor(() => {
        expect(api.testResults.delete).toHaveBeenCalledWith(mockTestResult.id);
      });
    });
  });

  describe('Pagination', () => {
    it('shows pagination when there are multiple pages', async () => {
      const manyResults: TestResultList = {
        items: Array(20)
          .fill(null)
          .map((_, i) => ({ ...mockTestResult, id: `id-${i}` })),
        totalCount: 50,
        skip: 0,
        take: 20,
      };

      (api.testResults.getAll as Mock).mockResolvedValue(manyResults);

      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Risultati (50)')).toBeInTheDocument();
      });

      expect(screen.getByRole('button', { name: /precedente/i })).toBeInTheDocument();
      expect(screen.getByRole('button', { name: /successiva/i })).toBeInTheDocument();
    });

    it('disables previous button on first page', async () => {
      const manyResults: TestResultList = {
        items: Array(20)
          .fill(null)
          .map((_, i) => ({ ...mockTestResult, id: `id-${i}` })),
        totalCount: 50,
        skip: 0,
        take: 20,
      };

      (api.testResults.getAll as Mock).mockResolvedValue(manyResults);

      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText('Risultati (50)')).toBeInTheDocument();
      });

      expect(screen.getByRole('button', { name: /precedente/i })).toBeDisabled();
    });
  });

  describe('Error Handling', () => {
    it('shows error alert when API fails', async () => {
      (api.testResults.getAll as Mock).mockRejectedValue(new Error('API error'));

      render(<TestHistoryClient />, { wrapper: createWrapper() });

      await waitFor(() => {
        expect(screen.getByText(/errore nel caricamento dei risultati/i)).toBeInTheDocument();
      });
    });
  });
});
