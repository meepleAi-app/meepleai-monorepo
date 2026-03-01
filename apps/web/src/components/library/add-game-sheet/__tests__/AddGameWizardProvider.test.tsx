/**
 * AddGameWizardProvider Tests
 * Issue #4822: MeepleCard Rewire - "Aggiungi alla Collezione" → Open Wizard
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { QueryClient, QueryClientProvider } from '@tanstack/react-query';
import type { ReactNode } from 'react';

import { AddGameWizardProvider, useAddGameWizard } from '../AddGameWizardProvider';
import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';

// Mock framer-motion
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Mock API
vi.mock('@/lib/api', () => ({
  api: {
    documents: { getDocumentsByGame: vi.fn().mockResolvedValue([]) },
    pdf: { uploadPdf: vi.fn() },
    library: { addGame: vi.fn(), updatePrivateGame: vi.fn() },
  },
}));

// Mock RagReadyIndicator
vi.mock('@/components/pdf/RagReadyIndicator', () => ({
  RagReadyIndicator: () => <div data-testid="rag-ready-indicator" />,
}));

const queryClient = new QueryClient({
  defaultOptions: { queries: { retry: false }, mutations: { retry: false } },
});

function Wrapper({ children }: { children: ReactNode }) {
  return (
    <QueryClientProvider client={queryClient}>
      <AddGameWizardProvider>{children}</AddGameWizardProvider>
    </QueryClientProvider>
  );
}

// Test component that consumes the context
function TestConsumer() {
  const { openWizard, isOpen } = useAddGameWizard();

  return (
    <div>
      <span data-testid="is-open">{String(isOpen)}</span>
      <button
        data-testid="open-from-library"
        onClick={() => openWizard({ type: 'fromLibrary' })}
      >
        Open from Library
      </button>
      <button
        data-testid="open-from-card"
        onClick={() =>
          openWizard(
            { type: 'fromGameCard', sharedGameId: 'game-123' },
            {
              gameId: 'game-123',
              title: 'Catan',
              source: 'catalog',
            },
          )
        }
      >
        Open from Card
      </button>
    </div>
  );
}

describe('AddGameWizardProvider', () => {
  beforeEach(() => {
    vi.clearAllMocks();
    useAddGameWizardStore.getState().reset();
  });

  it('provides isOpen as false initially', () => {
    render(<TestConsumer />, { wrapper: Wrapper });
    expect(screen.getByTestId('is-open')).toHaveTextContent('false');
  });

  it('opens wizard from library entry point', () => {
    render(<TestConsumer />, { wrapper: Wrapper });

    act(() => {
      fireEvent.click(screen.getByTestId('open-from-library'));
    });

    expect(screen.getByTestId('is-open')).toHaveTextContent('true');
    expect(screen.getByText('Aggiungi alla Collezione')).toBeInTheDocument();
  });

  it('opens wizard from game card entry point at step 2', () => {
    render(<TestConsumer />, { wrapper: Wrapper });

    act(() => {
      fireEvent.click(screen.getByTestId('open-from-card'));
    });

    expect(screen.getByTestId('is-open')).toHaveTextContent('true');
    // fromGameCard starts at step 2 (Knowledge Base)
    expect(screen.getByTestId('knowledge-base-step')).toBeInTheDocument();
  });

  it('throws when useAddGameWizard used outside provider', () => {
    // Suppress console.error for expected error
    const consoleSpy = vi.spyOn(console, 'error').mockImplementation(() => {});

    function BadConsumer() {
      useAddGameWizard();
      return null;
    }

    expect(() =>
      render(
        <QueryClientProvider client={queryClient}>
          <BadConsumer />
        </QueryClientProvider>,
      ),
    ).toThrow('useAddGameWizard must be used within AddGameWizardProvider');

    consoleSpy.mockRestore();
  });
});
