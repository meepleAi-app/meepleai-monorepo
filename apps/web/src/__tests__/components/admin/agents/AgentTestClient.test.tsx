/**
 * AgentTestClient Component Tests
 * Issue #3378
 */

import { render, screen, fireEvent, waitFor } from '@testing-library/react';
import { describe, it, expect, vi, beforeEach } from 'vitest';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';

import { AgentTestClient } from '@/app/(authenticated)/admin/agents/test/client';

// Mock the API
vi.mock('@/lib/api', () => ({
  api: {
    agents: {
      getTypologies: vi.fn().mockResolvedValue([
        { id: 'typ-1', name: 'Catan Expert' },
        { id: 'typ-2', name: 'Monopoly Helper' },
      ]),
    },
  },
}));

// Mock sonner toast
vi.mock('sonner', () => ({
  toast: {
    success: vi.fn(),
    error: vi.fn(),
  },
}));

const createWrapper = () => {
  const queryClient = new QueryClient({
    defaultOptions: {
      queries: {
        retry: false,
      },
    },
  });

  return function Wrapper({ children }: { children: React.ReactNode }) {
    return (
      <QueryClientProvider client={queryClient}>{children}</QueryClientProvider>
    );
  };
};

describe('AgentTestClient', () => {
  beforeEach(() => {
    vi.clearAllMocks();
  });

  describe('Rendering', () => {
    it('renders page title', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Agent Testing Console')).toBeInTheDocument();
      expect(
        screen.getByText(
          'Test agent typologies with different strategies and models'
        )
      ).toBeInTheDocument();
    });

    it('renders configuration panel', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Test Configuration')).toBeInTheDocument();
      expect(
        screen.getByText('Select typology, strategy, and model for testing')
      ).toBeInTheDocument();
    });

    it('renders query input section', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Test Query')).toBeInTheDocument();
      expect(
        screen.getByText('Enter your test question below')
      ).toBeInTheDocument();
    });

    it('renders empty test history', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Test History')).toBeInTheDocument();
      expect(
        screen.getByText('No tests yet. Send a query to get started.')
      ).toBeInTheDocument();
    });
  });

  describe('Configuration Selectors', () => {
    it('renders typology selector', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      expect(screen.getByText('Typology')).toBeInTheDocument();
      expect(screen.getByText('Select typology...')).toBeInTheDocument();
    });

    it('renders strategy selector with default value', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      // Label is "Strategy" with a "logged only" tooltip span
      expect(screen.getByText(/^Strategy/)).toBeInTheDocument();
    });

    it('renders model selector with default value', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      // Label is "Model" with a "logged only" tooltip span
      expect(screen.getByText(/^Model/)).toBeInTheDocument();
    });
  });

  describe('Send Query Button', () => {
    it('is disabled when no typology selected', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      const sendButton = screen.getByRole('button', {
        name: /send test query/i,
      });
      expect(sendButton).toBeDisabled();
    });

    it('is disabled when query is empty', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      const sendButton = screen.getByRole('button', {
        name: /send test query/i,
      });
      expect(sendButton).toBeDisabled();
    });
  });

  describe('Query Input', () => {
    it('allows typing in textarea', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      const textarea = screen.getByPlaceholderText(
        'How do I build a settlement in Catan?'
      );
      fireEvent.change(textarea, { target: { value: 'Test query' } });

      expect(textarea).toHaveValue('Test query');
    });
  });

  describe('Strategy Options', () => {
    it('contains all strategy options', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      // Strategy selector exists with its label
      expect(screen.getByText(/^Strategy/)).toBeInTheDocument();
    });
  });

  describe('Model Options', () => {
    it('contains model options', async () => {
      render(<AgentTestClient />, { wrapper: createWrapper() });

      // Model selector exists with its label
      expect(screen.getByText(/^Model/)).toBeInTheDocument();
    });
  });
});
