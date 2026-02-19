/**
 * AddGameSheet Component Tests
 * Issue #4818: AddGameSheet Drawer + State Machine
 */

import { describe, it, expect, vi, beforeEach } from 'vitest';
import { render, screen, fireEvent, act } from '@testing-library/react';
import { AddGameSheet } from '../AddGameSheet';
import { useAddGameWizardStore } from '@/lib/stores/add-game-wizard-store';

// Mock framer-motion to avoid animation issues in tests
vi.mock('framer-motion', () => ({
  motion: {
    div: ({ children, ...props }: React.PropsWithChildren<Record<string, unknown>>) => (
      <div {...props}>{children}</div>
    ),
  },
  AnimatePresence: ({ children }: React.PropsWithChildren) => <>{children}</>,
}));

// Mock API for KnowledgeBaseStep and GameInfoStep
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

describe('AddGameSheet', () => {
  const defaultProps = {
    open: true,
    onOpenChange: vi.fn(),
    entryPoint: { type: 'fromLibrary' as const },
  };

  beforeEach(() => {
    vi.clearAllMocks();
    useAddGameWizardStore.getState().reset();
  });

  it('should render wizard title', () => {
    render(<AddGameSheet {...defaultProps} />);
    expect(screen.getByText('Aggiungi alla Collezione')).toBeInTheDocument();
  });

  it('should render step indicator', () => {
    render(<AddGameSheet {...defaultProps} />);
    expect(screen.getByText('Sorgente')).toBeInTheDocument();
    expect(screen.getByText('PDF')).toBeInTheDocument();
    expect(screen.getByText('Info & Salva')).toBeInTheDocument();
  });

  it('should show step 1 content for fromLibrary entry point', () => {
    render(<AddGameSheet {...defaultProps} />);
    expect(screen.getByText('Scegli Sorgente Gioco')).toBeInTheDocument();
  });

  it('should show step 2 content for fromGameCard entry point', () => {
    render(
      <AddGameSheet
        {...defaultProps}
        entryPoint={{ type: 'fromGameCard', sharedGameId: 'game-123' }}
        gameData={{
          gameId: 'game-123',
          title: 'Catan',
          source: 'catalog',
        }}
      />
    );
    expect(screen.getByTestId('knowledge-base-step')).toBeInTheDocument();
  });

  it('should show Avanti button for non-last steps', () => {
    render(<AddGameSheet {...defaultProps} />);
    expect(screen.getByText('Avanti')).toBeInTheDocument();
  });

  it('should not show Indietro button on first step for fromLibrary', () => {
    render(<AddGameSheet {...defaultProps} />);
    expect(screen.queryByText('Indietro')).not.toBeInTheDocument();
  });

  it('should show close button', () => {
    render(<AddGameSheet {...defaultProps} />);
    expect(screen.getByText('Chiudi', { selector: '.sr-only' })).toBeInTheDocument();
  });

  it('should show close confirmation when dirty and closing', () => {
    render(<AddGameSheet {...defaultProps} />);

    // Mark the store as dirty (wrap in act to trigger re-render)
    act(() => {
      useAddGameWizardStore.getState().markDirty();
    });

    // Click close button
    fireEvent.click(screen.getByText('Chiudi', { selector: '.sr-only' }).parentElement!);

    expect(screen.getByText('Modifiche non salvate')).toBeInTheDocument();
  });

  it('should call onOpenChange when closed without changes', () => {
    render(<AddGameSheet {...defaultProps} />);

    // Not dirty, so clicking close should work directly
    fireEvent.click(screen.getByText('Chiudi', { selector: '.sr-only' }).parentElement!);

    expect(defaultProps.onOpenChange).toHaveBeenCalledWith(false);
  });

  it('should not render when not open', () => {
    render(<AddGameSheet {...defaultProps} open={false} />);
    expect(screen.queryByText('Aggiungi alla Collezione')).not.toBeInTheDocument();
  });
});
